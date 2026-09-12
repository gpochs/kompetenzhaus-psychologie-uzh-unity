import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { quests } from '../content/additional-quests.mjs';
import { validateQuestAlignment } from './quest-framework-alignment.mjs';
const framework = JSON.parse(fs.readFileSync(new URL('../content/competency-framework.json', import.meta.url), 'utf8'));

test('all eleven fictional exercises use canonical criteria and independent game rewards', () => {
  assert.equal(quests.length, 11);
  assert.equal(quests.reduce((count, quest) => count + quest.questions.length, 0), 22);
  for (const quest of quests) {
    validateQuestAlignment(quest, framework);
    assert.equal(quest.reward.academicCredit, false);
    assert.equal(quest.reward.type, 'cosmetic-and-game-progress');
  }
});
test('reject obsolete, duplicated or cross-owned alignment instead of inventing a second criterion', () => {
  for (const patch of [{ competencyIds: ['KI6'] }, { criterionIds: ['R4.1', 'R4.1'] },
    { criterionIds: ['R4.99'] }, { competencyIds: ['R4', 'T1'] }, { frameworkVersion: '2.0.0-draft' }])
    assert.throws(() => validateQuestAlignment({ ...quests[0], ...patch }, framework));
});
test('quest completion has no attained level or competency score', () => {
  for (const patch of [{ attainedLevel: 3 }, { score: 100 }, { alignmentMeaning: 'competence-attained' },
    { description: { de: 'KI6 erreicht', en: 'KI6 attained' } }])
    assert.throws(() => validateQuestAlignment({ ...quests[0], ...patch }, framework));
});
