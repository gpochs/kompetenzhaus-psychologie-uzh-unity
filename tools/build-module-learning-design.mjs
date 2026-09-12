import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { designs, designMetadata } from '../content/module-learning-design.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const sourcePath = 'content/kompetenzhaus-content.json';
const frameworkPath = 'content/competency-framework.json';
const authoredPath = 'content/module-learning-design.mjs';
const outputPath = 'content/module-learning-design.json';
const bilingualFields = ['knowledgeAnchors', 'futureSummary', 'activitySequence', 'aiRole', 'independentEvidence', 'assessmentProposal', 'workloadIntegration', 'openDecisions'];
const objectiveFields = ['text', 'task', 'evidence', 'successCriteria'];
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const fail = message => { throw new Error(message); };
const requireValue = (condition, message) => { if (!condition) fail(message); };
const equal = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const expectedBaseline = module => ({
  status: 'inherited-public-game-current-baseline', id: module.id, code: module.code,
  title: structuredClone(module.title), ects: module.ects, description: structuredClone(module.description),
  houseId: module.houseId, semester: module.semester, stageId: module.stageId,
});
const aiStudyAssessment = proposal => ({
  status: 'design-proposal', wholeModuleAiRequirement: 'not-specified',
  withoutAiObjectiveIds: proposal.objectives.filter(o=>o.contextIds.includes('without-ai')).map(o=>o.id),
  withAiObjectiveIds: proposal.objectives.filter(o=>o.contextIds.includes('with-ai')).map(o=>o.id),
  aboutAiObjectiveIds: proposal.objectives.filter(o=>o.contextIds.includes('about-ai')).map(o=>o.id),
  independentEvidenceRef: 'proposal.independentEvidence', assessmentProposalRef: 'proposal.assessmentProposal',
  policyRef: 'aiAcrossCurriculum.phasePolicy',
});
function bilingual(value, location, authored = true) {
  requireValue(value && typeof value === 'object', `${location}: bilingual object required`);
  for (const language of ['de', 'en']) requireValue(typeof value[language] === 'string' && value[language].trim().length > 5, `${location}.${language}: substantive text required`);
  if (authored) {
    requireValue(!value.de.includes('ß'), `${location}.de: use Swiss Standard German ss`);
    requireValue(!/[—–]/u.test(value.en), `${location}.en: avoid dash constructions in authored English`);
    requireValue(!/\b(?:TODO|TBD|Lorem ipsum)\b/i.test(value.de + value.en), `${location}: unfinished placeholder`);
  }
}

export function buildPackage(catalog, framework, sourceDesigns = designs, hashes = {}) {
  const records = [...catalog.modules, ...catalog.optionalModules];
  const definitions = new Map(sourceDesigns.map(item => [item.id, item]));
  requireValue(definitions.size === sourceDesigns.length, 'Duplicate authored module id');
  requireValue(sourceDesigns.length === records.length && records.every(item => definitions.has(item.id)), 'Authored coverage must match every source slot and option exactly');
  const modulePackage = {
    ...structuredClone(designMetadata), frameworkVersion: framework.version,
    frameworkSchemaVersion: framework.schemaVersion,
    sources: [
      { path: sourcePath, sha256: hashes.catalog ?? '', status: 'inherited-public-game-current-baseline', url: catalog.metadata.sourceUrl, sourceDate: catalog.metadata.sourceDate, originalPublicFiles: structuredClone(catalog.metadata.sourceFiles) },
      { path: frameworkPath, sha256: hashes.framework ?? '', status: framework.status, version: framework.version },
      { path: authoredPath, sha256: hashes.authored ?? '', status: 'independently-authored-design-proposal' },
    ],
    modules: records.map(source => ({
      id: source.id, code: source.code, kind: catalog.modules.some(m => m.id === source.id) ? 'slot' : 'option',
      parentSlotIds: source.id.startsWith('option:') ? catalog.modules.filter(m => m.optionCodes.includes(source.code)).map(m => m.id) : [],
      baseline: expectedBaseline(source), proposal: {
        ...structuredClone(definitions.get(source.id).proposal),
        aiStudyAssessment: aiStudyAssessment(definitions.get(source.id).proposal),
      },
    })),
  };
  const report = validatePackage(modulePackage, catalog, framework);
  modulePackage.coverage = report;
  return modulePackage;
}

export function validatePackage(modulePackage, catalog, framework) {
  requireValue(modulePackage.schema === 'kompetenzhaus.module-learning-design' && modulePackage.schemaVersion === 1, 'Unsupported learning-design schema');
  requireValue(modulePackage.status === 'design-proposal', 'Package must remain a design proposal');
  requireValue(modulePackage.frameworkVersion === framework.version, 'Framework version mismatch');
  const records = [...catalog.modules, ...catalog.optionalModules];
  const sourceById = new Map(records.map(item => [item.id, item]));
  const criteria = new Set(framework.competencies.flatMap(c => c.criteria.map(k => k.id)));
  const contexts = new Set(framework.contexts.map(c => c.id));
  const levels = new Set(framework.levelDefinitions.map(l => l.level));
  const ids = new Set(), objectiveIds = new Set(), usedCriteria = new Set(), countsByContext = {}, countsByLevel = {};
  requireValue(modulePackage.modules.length === records.length, 'Wrong module coverage');
  for (const module of modulePackage.modules) {
    requireValue(sourceById.has(module.id) && !ids.has(module.id), `Unknown or duplicate module ${module.id}`);
    ids.add(module.id);
    const source = sourceById.get(module.id);
    requireValue(module.code === source.code, `${module.id}: source code changed`);
    requireValue(equal(module.baseline, expectedBaseline(source)), `${module.id}: baseline identity/title/ECTS/description or study metadata changed`);
    const isOption = catalog.optionalModules.some(m => m.id === module.id);
    requireValue(module.kind === (isOption ? 'option' : 'slot'), `${module.id}: wrong kind`);
    const expectedParents = isOption ? catalog.modules.filter(m => m.optionCodes.includes(source.code)).map(m => m.id) : [];
    requireValue(equal(module.parentSlotIds, expectedParents), `${module.id}: invalid parent slot mapping`);
    bilingual(module.baseline.title, `${module.id}.baseline.title`, false);
    bilingual(module.baseline.description, `${module.id}.baseline.description`, false);
    const p = module.proposal;
    requireValue(p?.status === 'design-proposal', `${module.id}: missing proposal status`);
    requireValue(typeof p.topicRequired === 'boolean' && typeof p.individualisationRequired === 'boolean', `${module.id}: topic status required`);
    for (const field of bilingualFields) bilingual(p[field], `${module.id}.${field}`);
    requireValue(Array.isArray(p.objectives) && p.objectives.length >= 3 && p.objectives.length <= 5, `${module.id}: requires 3 to 5 objectives`);
    requireValue(p.objectives.some(o => o.contextIds.includes('without-ai')), `${module.id}: independent learning opportunity missing`);
    const moduleCriteria = new Set();
    for (const [index, objective] of p.objectives.entries()) {
      requireValue(objective.id === `${module.id}:o${index + 1}` && !objectiveIds.has(objective.id), `${module.id}: invalid or duplicate objective id`);
      objectiveIds.add(objective.id);
      requireValue(typeof objective.criterionId === 'string' && criteria.has(objective.criterionId) && !('criterionIds' in objective), `${objective.id}: exactly one canonical criterion required`);
      requireValue(!moduleCriteria.has(objective.criterionId), `${objective.id}: duplicate criterion within module`);
      moduleCriteria.add(objective.criterionId); usedCriteria.add(objective.criterionId);
      requireValue(levels.has(objective.targetLevel) && Number.isInteger(objective.targetLevel), `${objective.id}: targetLevel must be 1, 2 or 3`);
      requireValue(Array.isArray(objective.contextIds) && objective.contextIds.length > 0 && new Set(objective.contextIds).size === objective.contextIds.length && objective.contextIds.every(c => contexts.has(c)), `${objective.id}: invalid context reference`);
      countsByLevel[objective.targetLevel] = (countsByLevel[objective.targetLevel] ?? 0) + 1;
      for (const context of objective.contextIds) countsByContext[context] = (countsByContext[context] ?? 0) + 1;
      for (const field of objectiveFields) bilingual(objective[field], `${objective.id}.${field}`);
    }
    requireValue(p.activitySequence.de === p.objectives.map((o, i) => `${i + 1}. ${o.task.de}`).join('\n'), `${module.id}: German sequence diverges from tasks`);
    requireValue(equal(p.aiStudyAssessment,aiStudyAssessment(p)),`${module.id}: AI phase references must match actual objectives and create no module mandate`);
    requireValue(p.activitySequence.en === p.objectives.map((o, i) => `${i + 1}. ${o.task.en}`).join('\n'), `${module.id}: English sequence diverges from tasks`);
    for (const direction of ['prior', 'next']) {
      requireValue(Array.isArray(p.spiralLinks?.[direction]), `${module.id}: missing spiral ${direction}`);
      const linkedIds = new Set();
      for (const linked of p.spiralLinks[direction]) {
        requireValue(sourceById.has(linked.moduleId) && linked.moduleId !== module.id && !linkedIds.has(linked.moduleId), `${module.id}: invalid spiral module reference ${linked.moduleId}`);
        linkedIds.add(linked.moduleId); bilingual(linked.reason, `${module.id}.spiral.${linked.moduleId}`);
      }
    }
    requireValue(!('prerequisites' in p) && !('earnedLevel' in p) && !('credits' in p) && !('ects' in p), `${module.id}: proposal must not create gates, credits or attainment`);
  }
  return {
    slots: modulePackage.modules.filter(m => m.kind === 'slot').length,
    optionalModules: modulePackage.modules.filter(m => m.kind === 'option').length,
    objectives: objectiveIds.size,
    representedCompetencies: new Set([...usedCriteria].map(id => id.split('.')[0])).size,
    representedCriteria: usedCriteria.size,
    unrepresentedCriterionIds: [...criteria].filter(id => !usedCriteria.has(id)),
    objectiveCountsByTargetLevel: countsByLevel,
    objectiveCountsByContext: countsByContext,
    coverageMeaning: 'Authored opportunity coverage only; no assessment result or measurement validity is implied.',
  };
}

export async function readInputs() {
  const [catalogBytes, frameworkBytes, authoredBytes] = await Promise.all([sourcePath, frameworkPath, authoredPath].map(path => readFile(resolve(root, path))));
  return {
    catalog: JSON.parse(catalogBytes), framework: JSON.parse(frameworkBytes),
    hashes: { catalog: sha256(catalogBytes), framework: sha256(frameworkBytes), authored: sha256(authoredBytes) },
  };
}

async function main() {
  const { catalog, framework, hashes } = await readInputs();
  const content = buildPackage(catalog, framework, designs, hashes);
  const output = `${JSON.stringify(content, null, 2)}\n`;
  if (process.argv.includes('--check')) {
    const existing = await readFile(resolve(root, outputPath), 'utf8');
    requireValue(existing === output, 'Generated module-learning-design.json is stale; run the builder');
    console.log('Learning-design JSON is deterministic and current.');
  } else {
    await writeFile(resolve(root, outputPath), output, 'utf8');
    console.log(`Wrote ${outputPath}`);
  }
  console.log(JSON.stringify(content.coverage, null, 2));
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
