import test from 'node:test';
import { nextLearningStep } from './bridge-contract.mjs';
import assert from 'node:assert/strict';
import { createBridge, safeCompanionUrl, copyArchitecture, roomTiles, supportTiles, moveRoom, removeRoom, buildTutorContext, connectionCandidates, addConnection } from './bridge-contract.mjs';
import { normalizeTutorContext } from '../companion/context-contract.mjs';
import { readFileSync } from 'node:fs';

const journeyContent = { modules: [{ id: 'intro', houseId: 'bsc' }, { id: 'advanced', houseId: 'bsc' }, { id: 'master', houseId: 'msc' }], quests: [{ id: 'bsc-quest', moduleIds: ['intro'] }, { id: 'msc-quest', moduleIds: ['master'] }] };
function journeyState() { return { architecture: { houses: [{ houseId: 'bsc', rooms: [] }, { houseId: 'msc', rooms: [{ tiles: [{}] }] }] }, placedModuleIds: [], quizMasteredModuleIds: [], moduleStates: [{ id: 'intro', canBuild: true, missingPrerequisiteIds: [], positioned: false }, { id: 'advanced', canBuild: false, missingPrerequisiteIds: ['intro'], positioned: false }], questStates: [{ id: 'bsc-quest', canStart: true, completed: false }, { id: 'msc-quest', canStart: true, completed: false }] }; }

test('empty-plot guidance still offers a real source quiz and never changes the host state', () => {
  const state = journeyState(), before = JSON.stringify(state);
  const next = nextLearningStep(journeyContent, state, 'bsc');
  assert.equal(next.phase, 'room'); assert.equal(next.moduleId, 'intro'); assert.equal(next.canBuild, false);
  assert.equal(JSON.stringify(state), before);
});
test('guidance never awards build permission from quiz completion and can recover unpositioned inventory', () => {
  const state = journeyState(); state.architecture.houses[0].rooms.push({ tiles: [{}] });
  state.quizMasteredModuleIds = ['intro']; state.moduleStates[0].canBuild = false;
  assert.equal(nextLearningStep(journeyContent, state, 'bsc').phase, 'learn');
  state.moduleStates[0].canBuild = true;
  assert.equal(nextLearningStep(journeyContent, state, 'bsc').phase, 'build');
  state.quizMasteredModuleIds = []; state.placedModuleIds = ['intro']; state.moduleStates[0].alreadyPlaced = true;
  assert.equal(nextLearningStep(journeyContent, state, 'bsc').phase, 'build');
});
test('after all placed modules guidance uses only a host-unlocked quest in the selected house', () => {
  const state = journeyState(); state.architecture.houses[0].rooms.push({ tiles: [{}] });
  state.placedModuleIds = ['intro', 'advanced']; state.moduleStates.forEach(item => item.positioned = true);
  const next = nextLearningStep(journeyContent, state, 'bsc');
  assert.equal(next.phase, 'quest'); assert.equal(next.questId, 'bsc-quest'); assert.equal(next.placedCount, 2); assert.equal(next.total, 2);
  state.questStates[0].canStart = false;
  assert.equal(nextLearningStep(journeyContent, state, 'bsc').phase, 'explore');
});

test('answer transport waits for host and uses the displayed option index without calculating correctness', () => {
  const messages = [];
  const bridge = createBridge({ onSnapshot() {} });
  assert.throws(() => bridge.send('answer', { index: 0 }), /verbunden/);
  bridge.attach({ SendMessage: (...args) => messages.push(args) });
  bridge.receive({ schemaVersion: 1, ready: true, quiz: { phase: 'Question', prompt: 'Host question', options: ['B', 'A', 'C'] } });
  bridge.send('answer', { index: 1 });
  assert.deepEqual(JSON.parse(messages.at(-1)[2]), { command: 'answer', payload: { index: 1 } });
  assert.equal(bridge.state.quiz.phase, 'Question', 'sending must not invent host completion');
  assert.throws(() => bridge.send('answer', { index: 3 }), /Antwort/);
});

test('the last explanation requires explicit Continue and answers are locked during feedback', () => {
  const messages = [];
  const bridge = createBridge({ onSnapshot() {} });
  bridge.attach({ SendMessage: (...args) => messages.push(args) });
  bridge.receive({ schemaVersion: 1, ready: true, quiz: { phase: 'Feedback', mastered: 3, total: 3, explanation: 'Last explanation' } });
  assert.equal(messages.length, 1, 'a snapshot sends no automatic continuation');
  assert.throws(() => bridge.send('answer', { index: 0 }), /gerade/);
  bridge.send('continue');
  assert.equal(JSON.parse(messages.at(-1)[2]).command, 'continue');
  bridge.receive({ schemaVersion: 1, ready: true, quiz: { phase: 'Complete' } });
  assert.throws(() => bridge.send('continue'), /Fortsetzen/);
  bridge.send('closeQuiz');
  assert.equal(JSON.parse(messages.at(-1)[2]).command, 'closeQuiz');
});

test('invalid snapshot does not replace the last valid state; unknown commands fail closed', () => {
  const errors = [];
  const bridge = createBridge({ onSnapshot() {}, onError: error => errors.push(error) });
  bridge.receive({ schemaVersion: 1, ready: true, quiz: { phase: 'Idle' } });
  assert.equal(bridge.receive('{bad json'), false);
  assert.equal(bridge.state.quiz.phase, 'Idle');
  assert.equal(errors.length, 1);
  assert.throws(() => bridge.send('deleteEverything'), /Unbekannte/);
});

test('companion navigation rejects script and credential-bearing URLs', () => {
  assert.equal(safeCompanionUrl('javascript:alert(1)', 'https://example.org/'), '');
  assert.equal(safeCompanionUrl('https://user:secret@example.org/', 'https://example.org/'), '');
  assert.equal(safeCompanionUrl('../companion/index.html', 'https://example.org/game/'), 'https://example.org/companion/index.html');
});

test('an upper-floor L room adds support and never mutates the host snapshot', () => {
  const state = { architecture: { schemaVersion: 1, houses: [{ houseId: 'bsc', floors: 1, footprintTiles: [], courtyardCutouts: [], rooms: [], modulePlacements: [], decorations: [], connections: [] }] } };
  const candidate = copyArchitecture(state);
  const tiles = roomTiles({ x: 0, z: 0, floor: 2, width: 3, depth: 3, shape: 'l' });
  assert.equal(tiles.length, 8);
  supportTiles(candidate.houses[0], tiles);
  assert.equal(candidate.houses[0].footprintTiles.length, 24);
  assert.equal(candidate.houses[0].floors, 3);
  assert.equal(state.architecture.houses[0].footprintTiles.length, 0);
  assert.throws(() => roomTiles({ x: 3, z: 0, floor: 0, width: 2, depth: 2 }), /hinausragen/);
});

test('room edits preserve learning allocations and remove dangling room references', () => {
  const house = { floors: 1, footprintTiles: [{ x: 0, z: 0, floor: 0 }], courtyardCutouts: [], rooms: [{ id: 'r1', tiles: [{ x: 0, z: 0, floor: 0 }] }], modulePlacements: [{ moduleId: '003', roomId: 'r1', gridX: 0, gridZ: 0 }], decorations: [{ roomId: 'r1', x: 2, z: 2 }], connections: [] };
  moveRoom(house, 'r1', 1, 0);
  assert.equal(house.modulePlacements[0].gridX, 1);
  assert.equal(house.decorations[0].x, 6, 'furniture follows a room move in metres');
  assert.equal(house.decorations[0].z, 2);
  removeRoom(house, 'r1');
  assert.equal(house.modulePlacements[0].moduleId, '003');
  assert.equal(house.modulePlacements[0].roomId, '');
  assert.equal(house.footprintTiles.length, 2);
});

test('explicit v3 tutor export sends known choices and practice, never legacy scores or private text', () => {
  const catalog = JSON.parse(readFileSync(new URL('../companion/context-catalog.json', import.meta.url), 'utf8'));
  const questId = catalog.questIds?.[0];
  const source = { ready: true, learningMode: 'Serious', language: 'de', placedModuleIds: ['003', 's01c', '__proto__', 'unknown'], moduleChoices: [{ slotId: 's01c', moduleCode: '10SMSTS-505' }], competenceChoices: [{ moduleId: '003', topicId: 'unknown-topic' }], frameworkProfile: { frameworkVersion: '2.0.0-draft' }, quizMasteredModuleIds: ['003', '003', 'unknown'], completedQuestIds: [questId, 'unknown'], profile: { competencies: [{ id: 'Fa1', stage: 2 }], fields: [{ id: 'fa', percent: 80 }], careers: [{ id: 'psycho', fitPercent: 86, hasEvidence: true }] }, architecture: { houses: [{ rooms: [{ name: 'Private room note' }] }] }, playerName: 'Private name' };
  const envelope = buildTutorContext(source, catalog), normalized = normalizeTutorContext(envelope, catalog);
  assert.equal(envelope.version, 3);
  assert.equal(envelope.frameworkVersion, '2.0.0-draft');
  assert.equal(normalized.context.mode, 'serious');
  assert.equal(normalized.context.placed.serious.s01c.opt, '10SMSTS-505');
  assert.deepEqual(envelope.context.gamePractice.moduleIds, ['003']);
  assert.deepEqual(envelope.context.gamePractice.questIds, questId ? [questId] : []);
  assert.equal(Object.hasOwn(envelope.context, 'tutor'), false);
  for (const forbidden of ['stufen', 'felder', 'passung', 'fitPercent', 'Private', 'unknown-topic', '__proto__']) assert.equal(JSON.stringify(envelope).includes(forbidden), false);
  const unknownVersion = buildTutorContext({ ...source, frameworkProfile: { frameworkVersion: 'unrecognized' } }, catalog);
  assert.equal(Object.hasOwn(unknownVersion, 'frameworkVersion'), false);
  assert.equal(buildTutorContext({ ...source, frameworkProfile: { frameworkVersion: '2.1.0-draft' } }, catalog).frameworkVersion, '2.1.0-draft');
});

const connectionHouse = () => ({ rooms: [
  { id: 'ground', tiles: [{ x: -1, z: 0, floor: 0 }, { x: -1, z: 1, floor: 0 }] },
  { id: 'next', tiles: [{ x: 0, z: 0, floor: 0 }] },
  { id: 'upper', tiles: [{ x: -1, z: 0, floor: 1 }, { x: -1, z: 1, floor: 1 }] },
  { id: 'offset', tiles: [{ x: 0, z: 0, floor: 1 }] }
], modulePlacements: [], decorations: [], connections: [] });

test('door creation offers only shared same-floor walls and rejects a duplicate in reverse direction', () => {
  const house = connectionHouse();
  const pairs = connectionCandidates(house, 'ground', 'next', 'door'); assert.equal(pairs.length, 1);
  addConnection(house, { id: 'door1', fromRoomId: 'ground', toRoomId: 'next', kind: 'door', ...pairs[0] });
  assert.equal(connectionCandidates(house, 'next', 'ground', 'door').length, 0);
  assert.equal(connectionCandidates(house, 'ground', 'upper', 'door').length, 0);
  assert.equal(connectionCandidates(house, 'ground', 'ground', 'door').length, 0);
});

test('stairs require aligned cells on consecutive floors and reserve both endpoints', () => {
  const house = connectionHouse();
  assert.equal(connectionCandidates(house, 'ground', 'upper', 'stair').length, 2);
  assert.equal(connectionCandidates(house, 'ground', 'next', 'stair').length, 0);
  assert.equal(connectionCandidates(house, 'ground', 'offset', 'stair').length, 0);
  const pair = connectionCandidates(house, 'ground', 'upper', 'stair')[0];
  addConnection(house, { id: 'stair1', fromRoomId: 'ground', toRoomId: 'upper', kind: 'stair', ...pair });
  assert.equal(connectionCandidates(house, 'upper', 'ground', 'stair').length, 1);
});

test('rotated module footprints and furniture centres remove occupied stair choices without deleting anything', () => {
  const house = connectionHouse();
  house.modulePlacements.push({ moduleId: '003', gridX: -1, gridZ: 0, floor: 0, width: 2, depth: 1, quarterTurns: 1 });
  assert.equal(connectionCandidates(house, 'ground', 'upper', 'stair').length, 0);
  house.modulePlacements = [];
  house.decorations.push({ instanceId: 'chair', x: -2, z: 2, floor: 1 });
  const pairs = connectionCandidates(house, 'ground', 'upper', 'stair');
  assert.equal(pairs.length, 1); assert.equal(pairs[0].toTile.z, 1);
  const before = JSON.stringify(house);
  assert.throws(() => addConnection(house, { id: 'bad', fromRoomId: 'ground', toRoomId: 'upper', kind: 'stair', fromTile: { x: -1, z: 0, floor: 0 }, toTile: { x: -1, z: 0, floor: 1 } }), /freie Felder/);
  assert.equal(JSON.stringify(house), before, 'an invalid connection never removes furniture or mutates the plan');
});
