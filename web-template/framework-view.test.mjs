import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { frameworkProfile, visibleCriteria, visibleOpportunities, moduleLearningProposal } from './framework-view.mjs';

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
