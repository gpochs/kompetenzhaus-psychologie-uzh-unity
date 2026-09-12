"""Export the approved source with portable, typed FBX header metadata.

blender --background --factory-startup --python art/blender/export_public_fbx.py -- <repo>
No modelling, source save, installed exporter edit, or raw FBX-byte replacement.
"""
import array, hashlib, json, re, runpy, struct, sys
from datetime import datetime, timezone
from pathlib import Path
from io_scene_fbx import export_fbx_bin, fbx_utils, parse_fbx

root = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
audit_path = root / 'art/models/asset-audit.json'
audit = json.loads(audit_path.read_text(encoding='utf-8'))
source = root / 'art/models' / audit['sourceBlend']['path']
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_hash = digest(source)
if source_hash != audit['sourceBlend']['sha256']:
    raise RuntimeError('Approved source identity changed.')

def value_key(value):
    if isinstance(value, array.array): return ['array', value.typecode, value.tobytes().hex()]
    if isinstance(value, (bytes, bytearray)): return ['bytes', bytes(value).hex()]
    if isinstance(value, float): return ['float64', struct.pack('>d', value).hex()]
    return value

def element_key(element, skip_uid=False):
    return [element.id.decode('ascii'), [value_key(value) for value in element.props[int(skip_uid):]],
            [element_key(child) for child in element.elems]]

def scene_fingerprint(tree):
    objects = next(element for element in tree.elems if element.id == b'Objects')
    identities = {obj.props[0]: [obj.id.decode('ascii'), value_key(obj.props[1])] for obj in objects.elems}
    identities[0] = ['SceneRoot']
    payload = sorted([element_key(obj, True) for obj in objects.elems], key=repr)
    connections = next(element for element in tree.elems if element.id == b'Connections')
    edges = []
    for edge in connections.elems:
        if edge.id != b'C': raise RuntimeError('Unexpected connection record.')
        props = [value_key(edge.props[0])]
        for uid in edge.props[1:3]:
            if uid not in identities: raise RuntimeError('Connection references an unknown object.')
            props.append(identities[uid])
        props += [value_key(value) for value in edge.props[3:]]
        edges.append(props)
    global_settings = next(element for element in tree.elems if element.id == b'GlobalSettings')
    payload += [element_key(global_settings), sorted(edges, key=repr)]
    return hashlib.sha256(json.dumps(payload, separators=(',', ':')).encode()).hexdigest()

def strings(tree):
    for value in tree.props:
        if isinstance(value, bytes):
            yield value.decode('utf-8', errors='replace')
    for child in tree.elems: yield from strings(child)

baseline = {}
for entry in audit['exports']:
    path = (root / 'art/models' / entry['path']).resolve()
    if not path.is_relative_to(root): raise RuntimeError('Model path escaped repository.')
    tree, version = parse_fbx.parse(str(path))
    baseline[entry['assetId']] = {'sha256': digest(path), 'fingerprint': scene_fingerprint(tree), 'version': version}
if len(baseline) != 22: raise RuntimeError('Expected the 22 already reviewed exports.')

# Intercept values before the exporter creates any encoded property. Both the
# direct writer and compound-property writer resolve this typed setter. The
# process-local patch is restored after the normal Blender export completes.
original_utils_set = fbx_utils.elem_props_set
original_export_set = export_fbx_bin.elem_props_set
replacements = {b'Original|ApplicationNativeFile': source.name,
                b'Original|FileName': 'authored-model.fbx',
                b'DocumentUrl': 'authored-model.fbx', b'SrcDocumentUrl': 'authored-model.fbx'}
changed = {key.decode('ascii'): 0 for key in replacements}

def portable_property(element, ptype, name, value=None, **flags):
    if name in replacements:
        value = replacements[name]
        changed[name.decode('ascii')] += 1
    return original_utils_set(element, ptype, name, value, **flags)

fbx_utils.elem_props_set = portable_property
export_fbx_bin.elem_props_set = portable_property
prior_args = sys.argv[:]
try:
    sys.argv = prior_args[:prior_args.index('--') + 1] + [str(root), '--export']
    try: runpy.run_path(str(root / 'art/blender/build_assets.py'), run_name='__main__')
    except SystemExit as end:
        if end.code not in (None, 0): raise
finally:
    fbx_utils.elem_props_set = original_utils_set
    export_fbx_bin.elem_props_set = original_export_set
    sys.argv = prior_args

audit = json.loads(audit_path.read_text(encoding='utf-8'))
records = []
for entry in audit['exports']:
    path = (root / 'art/models' / entry['path']).resolve()
    tree, version = parse_fbx.parse(str(path))
    fingerprint = scene_fingerprint(tree)
    if fingerprint != baseline[entry['assetId']]['fingerprint']:
        raise RuntimeError('Export scene data changed: ' + entry['assetId'])
    private_paths = [value for value in strings(tree) if re.search(r'(?i)^[a-z]:[\\/]|^\\\\|[\\/]Users[\\/]', value)]
    if private_paths: raise RuntimeError('Private file metadata remains in ' + entry['assetId'])
    native_values = []
    def inspect(element):
        if element.id == b'P' and element.props and element.props[0] == b'Original|ApplicationNativeFile':
            native_values.append(element.props[-1].decode('utf-8'))
        for child in element.elems: inspect(child)
    inspect(tree)
    if native_values != [source.name]: raise RuntimeError('Unexpected native-source metadata.')
    records.append({'assetId': entry['assetId'], 'path': path.relative_to(root).as_posix(),
                    'sha256': digest(path), 'bytes': path.stat().st_size, 'fbxVersion': version,
                    'previousFbxSha256': baseline[entry['assetId']]['sha256'],
                    'sceneDataFingerprintBeforeAndAfter': fingerprint,
                    'applicationNativeFile': source.name, 'absolutePrivatePathCount': 0})
if digest(source) != source_hash: raise RuntimeError('Approved source was modified.')
if any(count != 22 for count in changed.values()): raise RuntimeError('Not all headers used the portable metadata writer.')

report_path = root / 'art/models/fbx-metadata-audit.json'
report = {'schema': 'kompetenzhaus.fbx-metadata-audit.v1', 'status': 'checked-clean-scene-data-unchanged',
          'recordedAt': datetime.now(timezone.utc).isoformat(), 'sourceBlendSha256': source_hash,
          'exporter': '../blender/export_public_fbx.py', 'exporterSha256': digest(Path(__file__)),
          'method': 'Typed property values neutralized before normal Blender FBX encoding. Parsed Objects, GlobalSettings and semantic Connections fingerprints equal before/after; object UIDs are resolved by object identity. Source blend was not saved or modified.',
          'metadataReplacements': changed, 'models': records,
          'runtimeScope': 'Metadata-only re-export after the recorded geometry/physics tests; no new Unity or browser test is claimed here.'}
report_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
audit['publicExport'] = {'script': '../blender/export_public_fbx.py', 'scriptSha256': digest(Path(__file__)),
                         'metadataAudit': 'fbx-metadata-audit.json', 'metadataAuditSha256': digest(report_path)}
audit_path.write_text(json.dumps(audit, indent=2) + '\n', encoding='utf-8')
print('PUBLIC_FBX_METADATA_CLEAN', len(records), 'scene data unchanged; source unchanged')
