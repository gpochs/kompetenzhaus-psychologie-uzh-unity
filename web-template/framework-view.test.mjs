import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { frameworkProfile, visibleCriteria, visibleOpportunities, moduleLearningProposal, aiCurriculum, moduleAiPhases } from './framework-view.mjs';

test('a missing V2 snapshot cannot fall back to legacy competence or career scores', () => {
  assert.equal(frameworkProfile({ profile: { competencies: [{ stage: 4 }], careers: [{ fitPercent: 86 }] } }), null);
  assert.equal(frameworkProfile({ frameworkProfile: { schemaVersion: 2, assessmentStatus: 'assessed', hasAssessedEvidence: true, competencies: [], careers: [] } }), null);
  const profile = { schemaVersion: 2, assessmentStatus: 'unassessed', hasAssessedEvidence: false, competencies: [], careers: [] };
  assert.equal(frameworkProfile({ frameworkProfile: profile }), profile);
});

test('module-card proposals match actual C# option replacement, including default option sharing the slot code', () => {
  const read = path => JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8'));
  const design = read('../content/module-learning-design.json');
  for (const fixture of ['all-modules','selected-options']) {
    const profile = read('./qa/framework-v2-' + fixture + '.json').frameworkProfile;
    const opportunities = profile.competencies.flatMap(item => item.criteria.flatMap(criterion => criterion.opportunities));
    for (const id of new Set(opportunities.map(item => item.moduleId))) {
      const expected = opportunities.filter(item => item.moduleId === id);
      const proposal = moduleLearningProposal(design,id,expected[0].moduleCode,profile.frameworkVersion);
      assert.ok(proposal, id);
      assert.deepEqual(proposal.objectives.map(item=>item.id).sort(), [...new Set(expected.map(item=>item.objectiveId))].sort(), id);
    }
  }
});

test('module learning design replaces the slot with its selected option and rejects wrong parent or framework', () => {
  const baseline = {status:'design-proposal',objectives:[{id:'base'}]}, optional = {status:'design-proposal',objectives:[{id:'option'}]};
  const design = {schema:'kompetenzhaus.module-learning-design',schemaVersion:1,status:'design-proposal',frameworkVersion:'2.1.0-draft',modules:[
    {id:'s01c',code:'base-code',kind:'slot',proposal:baseline},
    {id:'option:505',code:'option-code',kind:'option',parentSlotIds:['s01c'],proposal:optional},
  ]};
  const before = JSON.stringify(design);
  assert.equal(moduleLearningProposal(design,'s01c','base-code','2.1.0-draft'),baseline);
  assert.equal(moduleLearningProposal(design,'s01c','option-code','2.1.0-draft'),optional);
  assert.deepEqual(moduleLearningProposal(design,'s01c','option-code','2.1.0-draft').objectives,[{id:'option'}]);
  assert.equal(moduleLearningProposal(design,'wp','option-code','2.1.0-draft'),null);
  assert.equal(moduleLearningProposal(design,'s01c','option-code','2.0.0-draft'),null);
  assert.equal(moduleLearningProposal(null,'s01c','base-code','2.1.0-draft'),null);
  assert.equal(JSON.stringify(design),before);
});

test('a future lens reuses canonical criteria and filters opportunities by context without altering levels', () => {
  const original = { id: 'R4', criteria: [{ id: 'R4.1', opportunities: [{ objectiveId: 'source', targetLevel: 2, contextIds: ['without-ai'] }, { objectiveId: 'ai-source', targetLevel: 1, contextIds: ['with-ai'] }] }, { id: 'R4.2', opportunities: [] }] };
  const before = JSON.stringify(original), lens = { criterionIds: ['R4.1'], contextIds: ['with-ai', 'about-ai'] };
  assert.deepEqual(visibleCriteria(original, lens).map(item => item.id), ['R4.1']);
  assert.equal(visibleCriteria(original, lens)[0], original.criteria[0], 'the view preserves canonical identity');
  assert.deepEqual(visibleOpportunities(original.criteria[0], lens).map(item => [item.objectiveId, item.targetLevel]), [['ai-source', 1]]);
  assert.equal(visibleOpportunities(original.criteria[0]).length, 2);
  assert.equal(JSON.stringify(original), before);
});

test('AI curriculum links reuse every domain and competency and reject unknown canonical destinations', () => {
  const profile = JSON.parse(readFileSync(new URL('./qa/framework-v2-all-modules.json', import.meta.url), 'utf8')).frameworkProfile;
  assert.equal(aiCurriculum(profile.aiAcrossCurriculum ? { ...profile, aiAcrossCurriculum: null } : profile), null);
  // Structural UI fixture; it does not invent a learner record or source text.
  const metadata = { schemaVersion: 1, status: 'design-proposal', domainRows: profile.domains.map(domain => {
    const items = profile.competencies.filter(item => item.domainId === domain.id);
    return { domainId: domain.id, competencyIds: items.map(item => item.id), criterionIds: items.flatMap(item => item.criteria.map(criterion => criterion.id)) };
  }), flowEdges: [{ fromDomainId: 'T', toDomainId: 'R', criterionIds: ['R4.1'] }], phasePolicy: { phases: ['without-ai', 'with-ai', 'about-ai'].map(contextId => ({ contextId })) }, examples: [{ criterionIds: ['V1.1', 'P2.1'] }] };
  profile.aiAcrossCurriculum = metadata;
  const before = JSON.stringify(profile);
  assert.equal(aiCurriculum(profile), metadata, 'the diagram is a view of canonical data');
  assert.equal(JSON.stringify(profile), before, 'reading the diagram cannot change opportunities or assessments');
  assert.equal(aiCurriculum({ ...profile, aiAcrossCurriculum: { ...metadata, domainRows: metadata.domainRows.slice(1) } }), null);
  assert.equal(aiCurriculum({ ...profile, aiAcrossCurriculum: { ...metadata, flowEdges: [{ fromDomainId: 'T', toDomainId: 'R', criterionIds: ['R4.99'] }] } }), null);
  assert.equal(aiCurriculum({ ...profile, aiAcrossCurriculum: { ...metadata, examples: [{ criterionIds: ['new-ai-bonus'] }] } }), null);
});

test('module AI phases link existing objectives by explicit context and never infer a whole-module requirement', () => {
  const independent = { id: 'one', criterionId: 'R4.1', contextIds: ['without-ai'] };
  const assisted = { id: 'two', criterionId: 'R4.1', contextIds: ['with-ai', 'about-ai'] };
  const proposal = { objectives: [independent, assisted], aiStudyAssessment: { status: 'design-proposal', wholeModuleAiRequirement: 'not-specified', withoutAiObjectiveIds: ['one'], withAiObjectiveIds: ['two'], aboutAiObjectiveIds: ['two'] } };
  const before = JSON.stringify(proposal), phases = moduleAiPhases(proposal);
  assert.equal(phases[0].objectives[0], independent);
  assert.equal(phases[1].objectives[0], assisted);
  assert.equal(phases[2].objectives[0], assisted, 'a different context is not a duplicate criterion or new outcome');
  assert.equal(JSON.stringify(proposal), before);
  assert.deepEqual(moduleAiPhases({ ...proposal, aiStudyAssessment: { ...proposal.aiStudyAssessment, withoutAiObjectiveIds: ['two'] } }), [], 'an assisted objective cannot be relabelled as independent work');
  assert.deepEqual(moduleAiPhases({ objectives: proposal.objectives }), [], 'missing policy does not imply an AI-free requirement');
});
