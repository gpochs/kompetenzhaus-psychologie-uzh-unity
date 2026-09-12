"""Promote an already reviewed render set and normalize its source render path.

Blender --background --factory-startup --python art/blender/promote_review.py -- <repo>
This performs no modelling or export. Form approval is recorded separately.
"""
import bpy, hashlib, json, shutil, sys
from datetime import datetime, timezone
from pathlib import Path

root = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
out = root / 'art/models'
review = root / 'art/review'
audit_path = out / 'asset-audit.json'
audit = json.loads(audit_path.read_text(encoding='utf-8'))
source = out / audit['sourceBlend']['path']
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
before_source = digest(source)
if before_source != audit['sourceBlend']['sha256']:
    raise RuntimeError('Source changed since generation; promotion refused.')
for image in audit['reviewImages']:
    original = (out / image['path']).resolve()
    if not original.is_relative_to(root) or digest(original) != image['sha256']:
        raise RuntimeError('Review image identity changed.')
    destination = review / original.name
    if original != destination:
        shutil.copyfile(original, destination)
    image['path'] = '../review/' + destination.name

bpy.ops.wm.open_mainfile(filepath=str(source))

def geometry_fingerprint():
    payload = []
    for obj in sorted((item for item in bpy.data.objects if item.type == 'MESH'), key=lambda item: item.name):
        payload.append([obj.name, [list(row) for row in obj.matrix_world],
                        [list(v.co) for v in obj.data.vertices],
                        [[list(p.vertices), p.material_index] for p in obj.data.polygons],
                        [m.name if m else None for m in obj.data.materials]])
    return hashlib.sha256(json.dumps(payload, separators=(',', ':')).encode()).hexdigest()

before_geometry = geometry_fingerprint()
bpy.context.scene.render.filepath = '//../review/'
bpy.ops.wm.save_as_mainfile(filepath=str(source))
if geometry_fingerprint() != before_geometry:
    raise RuntimeError('Unexpected geometry change during path normalization.')
audit['sourceBlend']['sha256'] = digest(source)
audit['sourcePostprocess'] = {
    'script': '../blender/promote_review.py', 'scriptSha256': digest(Path(__file__)),
    'processedAt': datetime.now(timezone.utc).isoformat(),
    'inputBlendSha256': before_source, 'outputBlendSha256': digest(source),
    'geometryFingerprintBeforeAndAfter': before_geometry,
    'operation': 'Copy exact reviewed images to canonical review directory; normalize saved relative render output path only. No mesh, material, transform, pivot or export change.'
}
audit_path.write_text(json.dumps(audit, indent=2) + '\n', encoding='utf-8')
print('REVIEW_PROMOTED', len(audit['reviewImages']), digest(source))
