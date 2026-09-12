/** Validate new game exercises against the canonical framework, never the legacy scores. */
import assert from 'node:assert/strict';

export function validateQuestAlignment(quest, framework) {
  const prefix = `Quest ${quest.id}`;
  assert.equal(quest.frameworkVersion, framework.version, `${prefix}: framework version mismatch`);
  assert.equal(quest.alignmentMeaning, 'game-practice-topics-not-assessed-competence', `${prefix}: practice meaning missing`);
  const owners = new Map();
  for (const competency of framework.competencies) for (const criterion of competency.criteria) {
    assert(!owners.has(criterion.id), `Canonical criterion has multiple owners: ${criterion.id}`);
    owners.set(criterion.id, competency.id);
  }
  assert(Array.isArray(quest.criterionIds) && quest.criterionIds.length > 0, `${prefix}: criteria missing`);
  assert.equal(new Set(quest.criterionIds).size, quest.criterionIds.length, `${prefix}: duplicate criterion`);
  for (const id of quest.criterionIds) assert(owners.has(id), `${prefix}: unknown canonical criterion ${id}`);
  const expected = [...new Set(quest.criterionIds.map(id => owners.get(id)))].sort();
  assert.deepEqual(quest.competencyIds?.toSorted(), expected, `${prefix}: competencies must be the criteria owners`);
  for (const key of ['score', 'level', 'attainedLevel', 'assessedLevel', 'competencyPoints'])
    assert(!(key in quest), `${prefix}: practice cannot claim ${key}`);
  assert(!/\b(?:Fa\d+|KI[1-6]|Fu[1-3])\b/.test(JSON.stringify(quest)), `${prefix}: obsolete framework label`);
}
