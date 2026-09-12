/** Read-only local Blender evidence audit. No network, secrets, paid service or approval mutation.
 * --source: validate preparation, reporting unfinished evidence as pending (exit 0 if structurally sound).
 * --release: every required measurement and recorded review must pass (pending also exits 1).
 * --json: machine-readable report on stdout. --self-test: isolated integrity checks, no files created.
 *
 * Production audit contract (art/models/asset-audit.json, all paths relative to that file):
 * conventions:{units:'meters',gridSizeMetres:4,floorHeightMetres:3.2,blenderUp:'Z',unityUp:'Y'};
 * sourceBlend:{path,sha256}; assets:[{id,triangles,min[3],max[3],dimensions[3],sourceReference,
 * sourceReferenceSha256,pivot[3],pivotMode:'base-center'|'grid-contact-plane'|'named-part-pivots',
 * objects?:[{name,origin[3]}],triangleBudget?,manifestAssetId?,dimensionException?:{reason,approvedBy}}];
 * openings:[{assetId,clearWidthMetres:2.4,clearHeightMetres:2.7,depthMetres,unobstructed:true,
 * measurementMethod:'evaluated-mesh',evidence:'relative-file'}];
 * exports:[{assetId,path,sha256}]; exported:boolean.
 * Optional review/runtime audit: art/review/runtime-audit.json with {loaded:true,sceneTriangles,
 * totalModelBytes,highTextureBytes,viewport:[width,height],evidence:[paths],checks:{...booleans}}.
 * Reviewed file hashes belong to milestone-reviews.json gate.evidenceSha256[path] and
 * props.json image.sha256. A hash calculated now is not proof that this file was approved before.
 */
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

const scriptFile = fileURLToPath(import.meta.url);
const defaultRoot = path.resolve(path.dirname(scriptFile), '..');
const finite3 = value => Array.isArray(value) && value.length === 3 && value.every(Number.isFinite);
const positive3 = value => finite3(value) && value.every(number => number > 0);
const hashPattern = /^[a-f0-9]{64}$/i;
const near = (a, b, tolerance = 0.005) => Number.isFinite(a) && Number.isFinite(b) && Math.abs(a - b) <= tolerance;
export function dimensionsAgree(record) {
  return positive3(record.dimensions) && finite3(record.min) && finite3(record.max) && record.dimensions.every((dimension, i) => near(dimension, record.max[i] - record.min[i], 0.001));
}
export function approvalProblem(review) {
  if (!review || !['pending', 'approved', 'rejected'].includes(review.status)) return 'Unknown or missing review status.';
  if (review.status !== 'approved') return null;
  if (typeof review.approvedBy !== 'string' || !review.approvedBy.trim()) return 'Approval has no identified reviewer.';
  if (typeof review.approvedAt !== 'string' || !Number.isFinite(Date.parse(review.approvedAt))) return 'Approval has no valid timestamp.';
  if (!Array.isArray(review.evidence) || review.evidence.length === 0) return 'Approval has no evidence files.';
  if (/agent|codex|assistant|claude/i.test(review.approvedBy) && !review.delegationEvidence) return 'Agent review has no recorded explicit delegation evidence.';
  return null;
}

export function validateArtPipeline(root = defaultRoot, mode = 'source') {
  if (!['source', 'release'].includes(mode)) throw new Error('Use source or release mode.');
  root = fs.realpathSync(path.resolve(root));
  const reviewDirectory = path.join(root, 'art', 'review');
  const modelsDirectory = path.join(root, 'art', 'models');
  const checks = [], measuredFiles = new Map();
  const add = (status, code, message, details) => checks.push({ status, code, message, ...(details === undefined ? {} : { details }) });
  const required = (condition, code, message) => add(condition ? 'pass' : 'fail', code, message);
  const pending = (code, message, details) => add('pending', code, message, details);
  const safePath = (base, relative) => {
    if (typeof relative !== 'string' || !relative.trim() || path.isAbsolute(relative) || /^[a-z]+:/i.test(relative)) throw new Error('Expected a relative local artifact path.');
    const target = path.resolve(base, relative);
    const rel = path.relative(root, target);
    if (rel === '..' || rel.startsWith('..' + path.sep) || path.isAbsolute(rel)) throw new Error('Artifact path leaves the repository.');
    if (fs.existsSync(target)) {
      const actual = fs.realpathSync(target), resolved = path.relative(root, actual);
      if (resolved === '..' || resolved.startsWith('..' + path.sep) || path.isAbsolute(resolved)) throw new Error('Artifact symlink leaves the repository.');
    }
    return target;
  };
  const inspect = (base, relative, code, optional = false, expectedHash = null, requireRecordedHash = false) => {
    let target;
    try { target = safePath(base, relative); }
    catch (error) { add('fail', code, error.message); return null; }
    if (!fs.existsSync(target) || !fs.statSync(target).isFile()) {
      add(optional ? 'pending' : 'fail', code, `Missing artifact: ${path.relative(root, target).replaceAll('\\', '/')}`); return null;
    }
    const bytes = fs.readFileSync(target), sha256 = crypto.createHash('sha256').update(bytes).digest('hex');
    const record = { path: path.relative(root, target).replaceAll('\\', '/'), bytes: bytes.length, sha256 };
    measuredFiles.set(record.path, record);
    if (!bytes.length) { add('fail', code, 'Artifact is empty.'); return null; }
    if (path.extname(target).toLowerCase() === '.png') {
      const png = bytes.length >= 24 && bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]));
      if (!png) { add('fail', code, 'Invalid PNG header.'); return null; }
      record.width = bytes.readUInt32BE(16); record.height = bytes.readUInt32BE(20);
      if (record.width <= 0 || record.height <= 0) { add('fail', code, 'PNG has invalid dimensions.'); return null; }
    }
    if (expectedHash != null) required(hashPattern.test(expectedHash) && sha256 === expectedHash.toLowerCase(), code + '.hash', 'Recorded SHA-256 matches the current file.');
    else if (requireRecordedHash) pending(code + '.hash', 'Current hash measured; no previously recorded review/source hash to compare.');
    add('pass', code, 'Artifact exists; bytes and SHA-256 measured.', record); return record;
  };
  const json = (directory, name, schema, optional = false) => {
    const file = inspect(directory, name, name, optional);
    if (!file) return null;
    try {
      const value = JSON.parse(fs.readFileSync(path.join(directory, name), 'utf8'));
      const valid = value && typeof value === 'object' && !Array.isArray(value) && (!schema || value.schema === schema);
      required(valid, name + '.schema', 'Expected object/schema is present.');
      return valid ? value : null;
    } catch (error) { add('fail', name + '.parse', 'Invalid JSON: ' + error.message); return null; }
  };

  const brief = json(reviewDirectory, 'room-brief.json', 'game-room.room-brief.v1');
  const props = json(reviewDirectory, 'props.json', 'game-room.prop-manifest.v1');
  const layout = json(reviewDirectory, 'room-layout.json', 'game-room.layout.v1');
  const openings = json(reviewDirectory, 'openings.json', 'game-room.opening-schedule.v1');
  const reviews = json(reviewDirectory, 'milestone-reviews.json', 'game-room.milestone-reviews.v1');
  const plan = json(reviewDirectory, 'plan-metadata.json', 'kompetenzhaus.plan.v1');
  if ([brief, props, layout, openings, reviews, plan].some(value => !value)) return finish();
  const collectionsValid = Array.isArray(props.assets) && Array.isArray(layout.surfaces) && Array.isArray(openings.openings) &&
    [props.assets, layout.surfaces, openings.openings].every(items => items.every(item => item && typeof item === 'object' && !Array.isArray(item)));
  required(collectionsValid, 'manifest-collections', 'Required manifest collections contain objects.');
  if (!collectionsValid) return finish();

  required([props, layout, openings, reviews, plan].every(value => value.roomId === brief.roomId), 'room-id', 'Review manifests identify the same room.');
  required(brief.creditBudget?.maximumCredits === 0 && props.generation?.hardCreditCeiling === 0 && props.generation?.provider === 'local-blender' && plan.paidServiceBudget === 0,
    'zero-cost-local-route', 'Local Blender route has a strict zero additional-service budget.');
  required(positive3(brief.boundsMeters) && positive3(plan.bounds) && layout.units === 'meters' && plan.units === 'meters', 'metres', 'Review dimensions use metres and finite positive bounds.');
  if (brief.layoutRole === 'optional-starter' || plan.layoutRole === 'optional-starter') add('pass', 'starter-layout-role', 'Function layout is explicitly an optional starter; its bounds do not limit the free-build world.');
  else pending('starter-layout-role', 'Label brief or plan layoutRole as optional-starter so planning bounds are not mistaken for free-build limits.');
  required(brief.performanceBudget?.maxSceneTriangles > 0 && brief.performanceBudget?.maxVLowBytes > 0, 'performance-budget', 'Scene triangle and byte budgets are declared.');
  inspect(root, 'art/blender/build_assets.py', 'builder-source');
  inspect(root, 'art/blender/build_review_plan.py', 'plan-source');
  for (const [name, relative] of Object.entries({ floorPlan: plan.floorPlan, ceilingPlan: plan.reflectedCeilingPlan, geometrySource: plan.geometrySource })) inspect(reviewDirectory, relative, name);
  inspect(reviewDirectory, 'function-layout.blend', 'function-blend');
  for (const relative of ['../references/architecture-kit.png', '../references/asset-references.png', brief.artifacts?.visualTarget]) inspect(reviewDirectory, relative, 'reference-image');

  const propById = new Map();
  required(Array.isArray(props.assets) && props.assets.length > 0, 'props.assets', 'Prop manifest is nonempty.');
  for (const prop of props.assets || []) {
    const code = 'prop.' + prop.id;
    required(typeof prop.id === 'string' && prop.id.length > 0 && !propById.has(prop.id), code + '.identity', 'Prop id is unique.'); propById.set(prop.id, prop);
    required(positive3(prop.dimensionsMeters) && Number.isInteger(prop.targetPolycount) && prop.targetPolycount > 0 && Number.isInteger(prop.intendedInstances) && prop.intendedInstances > 0,
      code + '.dimensions-budget', 'Prop dimensions, triangle target and instance count are valid.');
    required(Boolean(prop.function && prop.attachmentFace && prop.semanticFront && prop.image?.generator && prop.image?.prompt && prop.image?.sheetCell), code + '.provenance', 'Function, mount, orientation, reference cell and image provenance are declared.');
    inspect(reviewDirectory, prop.image?.source, code + '.image', false, prop.image?.sha256, true);
    if (prop.image?.approved === true) {
      required(typeof prop.image.approvedBy === 'string' && Boolean(prop.image.approvedBy.trim()), code + '.image-approval', 'Image approval identifies its reviewer.');
      if (/agent|codex|assistant|claude/i.test(prop.image.approvedBy || '') && !prop.image.delegationEvidence) pending(code + '.image-delegation', 'Explicit delegation evidence is needed for agent image review.');
    } else pending(code + '.image-approval', 'Reference image approval is pending.');
  }
  const wallIds = new Set((layout.surfaces || []).map(surface => surface.id));
  for (const surface of layout.surfaces || []) required(finite3(surface.center) && positive3(surface.dimensions), 'surface.' + surface.id, 'Surface bounds are finite and positive.');
  const openingIds = new Set();
  for (const opening of openings.openings || []) {
    const code = 'opening.' + opening.id;
    required(Boolean(opening.id) && !openingIds.has(opening.id) && wallIds.has(opening.wallId) && positive3(opening.dimensions) && finite3(opening.center), code + '.structure', 'Opening has a unique id, existing wall and valid dimensions.'); openingIds.add(opening.id);
    required(['door', 'window', 'deep_alcove'].includes(opening.type) && Boolean(opening.destination && opening.purpose), code + '.function', 'Opening type, purpose and destination are declared.');
    if (opening.type === 'door' && positive3(opening.dimensions)) {
      const width = Math.max(opening.dimensions[0], opening.dimensions[1]), height = opening.dimensions[2];
      if (!near(width, 2.4) || !near(height, 2.7)) pending(code + '.clear-contract', 'Review plan differs from the 2.4 × 2.7 m production opening contract.', { declaredWidth: width, declaredHeight: height });
      else add('pass', code + '.clear-contract', 'Scheduled opening matches 2.4 × 2.7 m; actual mesh clearance still requires measurement.');
    }
  }
  required(openingIds.has(openings.primaryArrival?.openingId), 'arrival', 'Primary arrival references a scheduled opening.');

  for (const gate of ['function', 'form', 'runtime']) {
    const review = reviews[gate], problem = approvalProblem(review);
    if (problem) add('fail', gate + '.approval', problem);
    else if (review.status !== 'approved') pending(gate + '.approval', `Recorded ${gate} review is ${review.status}; no approval inferred.`);
    else add('pass', gate + '.approval', 'An identified, dated approval is recorded; validator does not authenticate human authorship.');
    if (review?.evidence != null && !Array.isArray(review.evidence)) add('fail', gate + '.evidence-list', 'Review evidence must be an array of local paths.');
    for (const evidence of Array.isArray(review?.evidence) ? review.evidence : []) inspect(reviewDirectory, evidence, gate + '.evidence', false, review.evidenceSha256?.[evidence], review.status === 'approved');
    if (review?.delegationEvidence) inspect(reviewDirectory, review.delegationEvidence, gate + '.delegation');
  }

  const audit = json(modelsDirectory, 'asset-audit.json', null, true);
  if (!audit) { pending('production-models', 'Production model geometry, pivots, exports and Form/Runtime evidence have not been audited yet.'); return finish(); }
  const objectArray = value => Array.isArray(value) && value.every(item => item && typeof item === 'object' && !Array.isArray(item));
  const auditArraysValid = objectArray(audit.assets) &&
    (audit.openings == null || objectArray(audit.openings)) && (audit.exports == null || objectArray(audit.exports));
  required(auditArraysValid, 'model-audit-collections', 'Model audit contains structured asset/opening/export arrays.');
  if (!auditArraysValid) return finish();
  const conventions = audit.conventions;
  if (!conventions) pending('model-conventions', 'Measured/model export conventions are not recorded.');
  else required(conventions.units === 'meters' && near(conventions.gridSizeMetres, 4) && near(conventions.floorHeightMetres, 3.2) && conventions.blenderUp === 'Z' && conventions.unityUp === 'Y', 'model-conventions', 'Model grid is 4 m, floor pitch 3.2 m, Blender Z-up and Unity Y-up.');
  inspect(modelsDirectory, audit.sourceBlend?.path || 'kompetenzhaus-assets.blend', 'production-blend', true, audit.sourceBlend?.sha256, true);
  inspect(reviewDirectory, 'model-contact-sheet.png', 'model-contact-sheet', true);
  const assetIds = new Set(); let uniqueTriangles = 0;
  for (const asset of audit.assets || []) {
    const code = 'model.' + asset.id;
    required(typeof asset.id === 'string' && !assetIds.has(asset.id), code + '.identity', 'Model id is unique.'); assetIds.add(asset.id);
    required(dimensionsAgree(asset), code + '.bounds', 'Measured dimensions agree with finite transformed bounds.');
    required(Number.isInteger(asset.triangles) && asset.triangles > 0, code + '.triangles', 'Triangle count is an integer from evaluated geometry.');
    uniqueTriangles += Number.isInteger(asset.triangles) ? asset.triangles : 0;
    const prop = propById.get(asset.manifestAssetId || (asset.id === 'wall-door' ? 'entry-portal' : asset.id));
    const budget = prop?.targetPolycount || asset.triangleBudget;
    if (Number.isFinite(budget) && budget > 0) required(asset.triangles <= budget, code + '.budget', 'Measured triangles are within the declared asset budget.');
    else pending(code + '.budget', 'Structural asset needs an explicit triangle budget.');
    if (prop && positive3(asset.dimensions)) {
      const matches = asset.dimensions.every((size, i) => Math.abs(size - prop.dimensionsMeters[i]) <= Math.max(0.02, prop.dimensionsMeters[i] * 0.05));
      if (!matches && !asset.dimensionException?.reason) add('fail', code + '.target-scale', 'Measured model bounds differ by more than 5%/2 cm from the prop contract.', { measured: asset.dimensions, target: prop.dimensionsMeters });
      else if (!matches && !asset.dimensionException?.approvedBy) pending(code + '.target-scale', 'Dimension exception has a reason but no recorded review.');
      else add('pass', code + '.target-scale', matches ? 'Model scale matches the manifest.' : 'Dimension difference has an identified recorded exception.');
    }
    inspect(modelsDirectory, asset.sourceReference, code + '.source-reference', false, asset.sourceReferenceSha256, true);
    if (!finite3(asset.pivot) || !['base-center', 'grid-contact-plane', 'named-part-pivots'].includes(asset.pivotMode)) pending(code + '.pivot', 'Measured pivot and its declared placement convention are missing.');
    else if (asset.pivotMode === 'named-part-pivots') required(Array.isArray(asset.objects) && asset.objects.length > 0 && asset.objects.every(object => object && typeof object.name === 'string' && finite3(object.origin)), code + '.pivot', 'Animated guide preserves measured named part origins.');
    else {
      required(asset.pivot.every(value => near(value, 0)), code + '.pivot', 'Static asset pivot is at its declared local grid/contact origin.');
      if (asset.pivotMode === 'base-center') required(finite3(asset.min) && Math.abs(asset.min[2]) <= 0.005, code + '.ground-contact', 'Floor-mounted asset base is within 5 mm of its contact plane.');
    }
  }
  required(assetIds.size > 0 && uniqueTriangles === audit.totalUniqueTriangles, 'unique-triangle-total', 'Asset triangle sum equals the reported total.');
  for (const prop of props.assets || []) if (![...assetIds].some(id => id === prop.id || (prop.id === 'entry-portal' && id === 'wall-door') || audit.assets.find(asset => asset.id === id)?.manifestAssetId === prop.id)) pending('model.missing.' + prop.id, 'Approved prop has no corresponding production model audit.');
  for (const assetId of ['wall-door', 'wall-partition']) {
    const opening = (audit.openings || []).find(item => item.assetId === assetId);
    if (!opening) { pending(assetId + '.mesh-opening', 'Evaluated mesh opening measurement is missing; code containing a Boolean call is not geometry proof.'); continue; }
    required(near(opening.clearWidthMetres, 2.4) && near(opening.clearHeightMetres, 2.7) && opening.depthMetres > 0 && opening.unobstructed === true && opening.measurementMethod === 'evaluated-mesh', assetId + '.mesh-opening', 'Evaluated mesh measures a clear 2.4 × 2.7 m passage with positive depth.');
    inspect(modelsDirectory, opening.evidence, assetId + '.opening-evidence');
  }
  const exportsById = new Map((audit.exports || []).map(item => [item.assetId, item]));
  for (const assetId of assetIds) {
    const exportRecord = exportsById.get(assetId);
    if (!exportRecord) { pending(assetId + '.export', 'Export path and SHA-256 have not been recorded.'); continue; }
    const file = inspect(modelsDirectory, exportRecord.path, assetId + '.export', true, exportRecord.sha256, true);
    if (file) required(file.path.startsWith('UnityProject/Assets/Kompetenzhaus/Art/Models/') && /\.(fbx|glb)$/i.test(file.path), assetId + '.export-location', 'Model export remains inside the Unity model asset directory.');
  }
  if (audit.exported !== true) pending('export-state', 'Production audit is not marked exported.');
  if (audit.exported === true && reviews.form.status !== 'approved') add('fail', 'export-approval-order', 'Production exports exist without recorded Form approval.');
  const runtime = json(reviewDirectory, 'runtime-audit.json', null, true);
  if (runtime) {
    required(runtime.loaded === true && Number.isInteger(runtime.sceneTriangles) && runtime.sceneTriangles > 0 && runtime.sceneTriangles <= brief.performanceBudget.maxSceneTriangles, 'runtime-triangles', 'Loaded runtime scene fits the scene triangle budget.');
    required(Number.isFinite(runtime.totalModelBytes) && runtime.totalModelBytes > 0 && runtime.totalModelBytes <= brief.performanceBudget.maxVLowBytes && Number.isFinite(runtime.highTextureBytes) && runtime.highTextureBytes >= 0 && runtime.highTextureBytes <= brief.performanceBudget.maxHighTextureBytes, 'runtime-bytes', 'Loaded geometry and textures fit declared byte budgets.');
    required(Array.isArray(runtime.viewport) && runtime.viewport.length === 2 && runtime.viewport.every(number => Number.isInteger(number) && number > 0), 'runtime-viewport', 'Actual production viewport is recorded.');
    for (const check of ['collision', 'doorTraversal', 'boundsRecovery', 'support', 'referenceFidelity', 'productionCamera', 'interaction', 'performance']) {
      if (runtime.checks?.[check] === true) add('pass', 'runtime.' + check, 'Recorded runtime check passed.');
      else pending('runtime.' + check, 'Runtime check is not recorded as passing.');
    }
    if (!Array.isArray(runtime.evidence) || !runtime.evidence.length) pending('runtime-evidence', 'Loaded-runtime screenshots or measurement files are missing.');
    for (const evidence of Array.isArray(runtime.evidence) ? runtime.evidence : []) inspect(reviewDirectory, evidence, 'runtime-capture');
  }
  return finish();

  function finish() {
    const counts = Object.fromEntries(['pass', 'pending', 'fail'].map(status => [status, checks.filter(check => check.status === status).length]));
    return { schema: 'kompetenzhaus.local-art-audit.v1', mode, passed: counts.fail === 0 && (mode === 'source' || counts.pending === 0),
      releaseReady: counts.fail === 0 && counts.pending === 0, counts,
      scope: 'Read-only recorded-evidence validation; source success is not geometric, visual or runtime approval. Human authorship and visual quality require actual review.',
      checks, measuredFiles: [...measuredFiles.values()] };
  }
}

function selfTest() {
  assert.equal(dimensionsAgree({ min: [0, 0, 0], max: [4, 0.24, 3.2], dimensions: [4, 0.24, 3.2] }), true);
  assert.equal(dimensionsAgree({ min: [0, 0, 0], max: [4, 0.24, 3.2], dimensions: [4, 0.24, 2.7] }), false);
  assert.equal(dimensionsAgree({ min: [0, 0, 0], max: [4, 0.24, Infinity], dimensions: [4, 0.24, Infinity] }), false);
  assert.equal(approvalProblem({ status: 'pending' }), null);
  assert.match(approvalProblem({ status: 'approved' }), /reviewer/);
  assert.match(approvalProblem({ status: 'approved', approvedBy: 'Codex', approvedAt: '2026-09-12T10:00:00Z', evidence: ['review.png'] }), /delegation/);
  assert.equal(approvalProblem({ status: 'approved', approvedBy: 'User', approvedAt: '2026-09-12T10:00:00Z', evidence: ['review.png'] }), null);
  console.log('7 integrity checks passed; no review files changed.');
}

if (process.argv[1] && path.resolve(process.argv[1]) === scriptFile) {
  const args = process.argv.slice(2);
  if (args.includes('--self-test')) selfTest();
  else {
    if (args.some(arg => !['--source', '--release', '--json'].includes(arg)) || (args.includes('--source') && args.includes('--release'))) throw new Error('Usage: node tools/validate-art-pipeline.mjs [--source | --release] [--json] | --self-test');
    const report = validateArtPipeline(defaultRoot, args.includes('--release') ? 'release' : 'source');
    if (args.includes('--json')) console.log(JSON.stringify(report, null, 2));
    else {
      console.log(`${report.mode.toUpperCase()} ${report.passed ? 'PASS' : 'FAIL'}: ${report.counts.pass} checks passed, ${report.counts.pending} pending, ${report.counts.fail} failed.`);
      for (const check of report.checks.filter(check => check.status !== 'pass')) console.log(`${check.status.toUpperCase()} ${check.code}: ${check.message}`);
      console.log(report.scope);
    }
    process.exitCode = report.passed ? 0 : 1;
  }
}
