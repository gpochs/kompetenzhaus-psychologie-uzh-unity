import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { designs } from '../content/module-learning-design.mjs';
import { buildPackage, validatePackage, readInputs } from './build-module-learning-design.mjs';

const { catalog, framework, hashes } = await readInputs();
const originalCatalog = JSON.stringify(catalog), originalFramework = JSON.stringify(framework);
const build = () => buildPackage(catalog, framework, designs, hashes);
const mutate = callback => { const content = build(); callback(content); return content; };
const get = (content, id) => content.modules.find(m => m.id === id);
const rejects = (callback, pattern) => assert.throws(() => validatePackage(mutate(callback), catalog, framework), pattern);

test('covers all 43 source slots and 3 optional modules; preserves every baseline value', () => {
  const content = build();
  assert.equal(content.coverage.slots, 43);
  assert.equal(content.coverage.optionalModules, 3);
  for (const source of [...catalog.modules, ...catalog.optionalModules]) {
    const entry = get(content, source.id);
    for (const field of ['id', 'code', 'title', 'ects', 'description', 'houseId', 'semester', 'stageId']) assert.deepEqual(entry.baseline[field], source[field], `${source.id}.${field}`);
    assert.equal(entry.baseline.status, 'inherited-public-game-current-baseline');
    assert.equal(entry.proposal.status, 'design-proposal');
  }
  assert.equal(JSON.stringify(catalog), originalCatalog);
  assert.equal(JSON.stringify(framework), originalFramework);
});

test('AI phase references use actual objective contexts and never impose whole-module AI rules',()=>{
  for(const m of build().modules){
    const phases=m.proposal.aiStudyAssessment;
    assert.equal(phases.status,'design-proposal');assert.equal(phases.wholeModuleAiRequirement,'not-specified');
    for(const [key,context] of [['withoutAiObjectiveIds','without-ai'],['withAiObjectiveIds','with-ai'],['aboutAiObjectiveIds','about-ai']]){
      assert.deepEqual(phases[key],m.proposal.objectives.filter(o=>o.contextIds.includes(context)).map(o=>o.id));
    }
    assert.equal(phases.policyRef,'aiAcrossCurriculum.phasePolicy');
  }
  rejects(c=>{c.modules[0].proposal.aiStudyAssessment.withAiObjectiveIds.push('invented');},/AI phase references/);
  rejects(c=>{c.modules[0].proposal.aiStudyAssessment.wholeModuleAiRequirement='without-ai';},/AI phase references/);
});

test('Psychological explanations and context interpretation have concrete AI variants with independent reasoning',()=>{
  const content=build();
  const cases=[['001','001:o4','P1.1',1],['002','002:o3','P1.2',1],['200','200:o1','P1.2',2],['300','300:o2','P2.2',2]];
  for(const [moduleId,objectiveId,criterionId,level] of cases){
    const objective=get(content,moduleId).proposal.objectives.find(o=>o.id===objectiveId);
    assert.equal(objective.criterionId,criterionId);assert.equal(objective.targetLevel,level);
    assert.ok(objective.contextIds.includes('without-ai')&&objective.contextIds.includes('with-ai'));
    assert.match(objective.task.en,/prepared AI/);
    assert.match(objective.task.en,/independently|without AI/i);
    assert.match(objective.successCriteria.en,/defining features|psychological mechanism|model assumptions|variation and situation/);
  }
  for(const id of ['001','002','300'])assert.ok(get(content,id).proposal.objectives.some(o=>o.criterionId.startsWith('P')&&o.contextIds.includes('about-ai')));
  assert.match(get(content,'300').proposal.objectives[1].successCriteria.en,/measurement validity.*separately under R2/);
  assert.equal(content.coverage.objectives,153);
});

test('build is deterministic and generated output matches inputs and source hashes', async () => {
  const first = build(), second = build();
  assert.deepEqual(first, second);
  assert.ok(first.sources.every(s => /^[a-f0-9]{64}$/.test(s.sha256)));
  const actual = await readFile(new URL('../content/module-learning-design.json', import.meta.url), 'utf8');
  assert.equal(actual, `${JSON.stringify(first, null, 2)}\n`);
});

test('rejects missing, duplicate and invented modules instead of silently omitting or substituting', () => {
  assert.throws(() => buildPackage(catalog, framework, designs.slice(1)), /coverage/);
  assert.throws(() => buildPackage(catalog, framework, [...designs, designs[0]]), /Duplicate/);
  rejects(c => { c.modules[0].id = 'AI-Ethics-I'; }, /Unknown/);
  rejects(c => { c.modules[1].id = c.modules[0].id; }, /duplicate/);
});

test('rejects changed baseline credits, content, identity and study metadata', () => {
  for (const field of ['ects', 'title', 'description', 'houseId', 'semester', 'stageId']) {
    rejects(c => { const b = c.modules[0].baseline; b[field] = typeof b[field] === 'number' ? b[field] + 1 : 'altered'; }, /baseline/);
  }
  rejects(c => { c.modules[0].code = 'new-module'; }, /source code/);
});

test('every objective has one valid criterion, a valid level and explicit context', () => {
  const content = build();
  assert.equal(content.coverage.representedCompetencies, 16);
  assert.equal(content.coverage.representedCriteria, 48);
  assert.deepEqual(content.coverage.unrepresentedCriterionIds, []);
  const change = (key, value) => c => { c.modules[0].proposal.objectives[0][key] = value; };
  rejects(change('criterionId', 'KI1'), /canonical criterion/);
  rejects(change('criterionId', ['P1.1', 'P1.2']), /canonical criterion/);
  rejects(change('criterionIds', ['P1.1']), /canonical criterion/);
  for (const level of [0, 4, 1.5, '2']) rejects(change('targetLevel', level), /targetLevel/);
  rejects(change('contextIds', ['all-ai']), /context/);
  rejects(change('contextIds', ['without-ai', 'without-ai']), /context/);
});

test('unique objective identity and 3–5 distinct criterion-focused goals are mandatory', () => {
  rejects(c => { c.modules[0].proposal.objectives[1].id = c.modules[0].proposal.objectives[0].id; }, /objective id/);
  rejects(c => { c.modules[0].proposal.objectives[1].criterionId = c.modules[0].proposal.objectives[0].criterionId; }, /duplicate criterion/);
  rejects(c => { c.modules[0].proposal.objectives = c.modules[0].proposal.objectives.slice(0, 2); }, /3 to 5/);
  rejects(c => { const o = c.modules[0].proposal.objectives; o.push(...structuredClone(o)); }, /3 to 5/);
});

test('checks bilingual content throughout objectives, module design and spiral reasons', () => {
  for (const language of ['de', 'en']) {
    for (const field of ['text', 'task', 'evidence', 'successCriteria']) rejects(c => { c.modules[0].proposal.objectives[0][field][language] = ''; }, /substantive text/);
    for (const field of ['knowledgeAnchors', 'futureSummary', 'activitySequence', 'aiRole', 'independentEvidence', 'assessmentProposal', 'workloadIntegration', 'openDecisions']) rejects(c => { c.modules[0].proposal[field][language] = ''; }, /substantive text/);
    rejects(c => { c.modules[0].proposal.spiralLinks.next[0].reason[language] = ''; }, /substantive text/);
  }
});

test('spiral links refer to real learning opportunities and cannot introduce gates or attainment', () => {
  rejects(c => { c.modules[0].proposal.spiralLinks.next[0].moduleId = 'invented'; }, /spiral module/);
  rejects(c => { c.modules[0].proposal.spiralLinks.next[0].moduleId = c.modules[0].id; }, /spiral module/);
  for (const field of ['prerequisites', 'earnedLevel', 'ects', 'credits']) rejects(c => { c.modules[0].proposal[field] = 3; }, /gates, credits or attainment/);
  const content = build();
  assert.match(content.levelRule.en, /do not establish personal competence/);
  assert.match(content.spiralRule.en, /not entry requirements/);
  assert.match(content.aiEthicsRule.en, /not three new modules/);
});

test('optional content has precise slot mappings and retains mentoring’s own six credits', () => {
  const content = build();
  assert.deepEqual(get(content, 'option:06SM200-510').parentSlotIds, ['wp']);
  assert.deepEqual(get(content, 'option:06SM200-511').parentSlotIds, ['wp']);
  assert.deepEqual(get(content, 'option:10SMSTS-505').parentSlotIds, ['s01c']);
  assert.equal(get(content, 's01c').baseline.ects, 4);
  assert.equal(get(content, 'option:10SMSTS-505').baseline.ects, 6);
  assert.match(content.selectionRule, /replaces.*entirely/);
  rejects(c => { get(c, 'option:10SMSTS-505').baseline.ects = 4; }, /baseline/);
  rejects(c => { get(c, 'option:06SM200-511').parentSlotIds = ['s01c']; }, /parent slot/);
  const idSets = content.modules.map(m => m.proposal.objectives.map(o => o.id));
  assert.equal(new Set(idSets.flat()).size, idSets.flat().length);
});

test('default-option substitution keeps all 48 canonical criteria represented without adding slot goals twice', () => {
  const content = build();
  const selected = catalog.modules.map(source => {
    const optional = content.modules.find(m => m.kind === 'option' && m.code === source.code && m.parentSlotIds.includes(source.id));
    return optional ?? get(content, source.id);
  });
  assert.equal(selected.length, 43);
  assert.equal(selected.filter(m => m.id === 'option:06SM200-510').length, 1);
  assert.equal(selected.filter(m => m.id === 'wp').length, 0);
  const represented = new Set(selected.flatMap(m => m.proposal.objectives.map(o => o.criterionId)));
  assert.equal(represented.size, 48);
});

test('variable offerings explicitly require individualisation rather than pretending to be confirmed teaching', () => {
  const content = build();
  for (const id of ['s11', 's12', 's13', 's01a', 's01b', 's01c', 's04', 's05', 's06', 's07', 's08', 's09', 's03', '600', 'BA', 'MA']) {
    const p = get(content, id).proposal;
    assert.equal(p.topicRequired, true, id);
    assert.equal(p.individualisationRequired, true, id);
    assert.ok(p.openDecisions.de.length > 30, id);
  }
  assert.match(get(content, 's04').proposal.knowledgeAnchors.en, /DeNC.*HEA.*SEOP/);
  assert.match(get(content, '600').proposal.knowledgeAnchors.en, /practice or research/);
  assert.match(get(content, 'option:10SMSTS-505').proposal.knowledgeAnchors.en, /primary school child/);
});

test('authored proposals contain differentiated tasks, revisions and independent evidence rather than duplicate summaries', () => {
  const content = build();
  const tasks = content.modules.flatMap(m => m.proposal.objectives.map(o => o.task.en));
  assert.equal(new Set(tasks).size, tasks.length);
  assert.equal(new Set(content.modules.map(m => m.proposal.futureSummary.en)).size, 46);
  assert.ok(content.modules.every(m => m.proposal.objectives.some(o => o.contextIds.includes('without-ai'))));
  assert.ok(get(content, 'MA').proposal.objectives.some(o => o.criterionId === 'V2.3'));
  assert.ok(get(content, '302').proposal.objectives.some(o => o.criterionId === 'H2.2'));
  assert.ok(get(content, 'option:10SMSTS-505').proposal.objectives.some(o => o.criterionId === 'K2.3'));
  assert.ok(get(content, 's09').proposal.objectives.some(o => o.criterionId === 'T3.3'));
  assert.match(content.costRule.en, /Paid AI access, new accounts and additional credits are not assumed/);
});

test('tool application and system design remain separate in concrete module tasks', () => {
  const content = build();
  const objective = (id, criterion) => get(content, id).proposal.objectives.find(o => o.criterionId === criterion);
  const prompt = objective('003', 'T2.1');
  assert.match(prompt.task.en, /independent attempt.*supplied interface/);
  const operation = objective('s06', 'T2.2');
  assert.match(operation.task.en, /supplied.*two existing settings/);
  const documentation = objective('s06', 'T2.3');
  assert.match(documentation.task.en, /tool version.*settings.*interaction sequence.*output/);
  assert.match(documentation.successCriteria.en, /neither the statistical analysis nor ethical permissibility/);
  assert.ok(objective('103', 'T3.1'));
  assert.ok(objective('903', 'T3.1'));
  const prototype = objective('s09', 'T3.2');
  assert.match(prototype.task.en, /choice.*feedback.*manual restart/);
  assert.match(prototype.successCriteria.en, /operating an unchanged template is insufficient/);
  for (const id of ['101', 's09', 's03']) assert.ok(objective(id, 'T3.3'), id);
  assert.ok(get(content, 's09').proposal.objectives.indexOf(prototype) < get(content, 's09').proposal.objectives.indexOf(objective('s09', 'T3.3')));
  assert.match(content.toolSeparationRule.en, /T2 covers.*T3 concerns/);
});

test('new public prose has no private source paths, contact details, secrets or unfinished placeholders', () => {
  const content = build();
  const authored = JSON.stringify(content.modules.map(m => m.proposal));
  assert.doesNotMatch(authored, /C:\\|OneDrive|Teaching Hub|Lehrentwicklung - KI|BEGIN (?:RSA |OPENSSH )?PRIVATE KEY|sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{20,}|[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}|\bTODO\b|Lorem ipsum/i);
  assert.match(content.sourceNotice.en, /no raw internal documents are published/);
});
