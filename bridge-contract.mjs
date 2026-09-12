export const COMMANDS = new Set([
  'getState', 'selectHouse', 'setLanguage', 'setLearningMode', 'setFutureCurriculum',
  'setDirectMasterEntry', 'setViewMode', 'setAccessibility', 'selfCheck', 'chooseModule',
  'startModuleQuiz', 'startQuest', 'answer', 'continue', 'closeQuiz', 'placeModule',
  'commitArchitecture', 'moveModule', 'undoArchitecture',
  'setUiFocus',
  'setCompetenceChoice', 'setPreStageChecks',
  'previewCompetence', 'clearCompetencePreview',
  'userGesture', 'setArchitectureView', 'zoomIn', 'zoomOut',
]);

export function parseSnapshot(value) {
  const state = typeof value === 'string' ? JSON.parse(value) : value;
  if (!state || typeof state !== 'object' || Array.isArray(state) || state.schemaVersion !== 1) throw new Error('Unbekannte Spielnachricht.');
  if (typeof state.ready !== 'boolean') throw new Error('Der Bereitschaftsstatus fehlt.');
  if (state.quiz && !['Idle', 'Question', 'Feedback', 'Complete'].includes(state.quiz.phase)) throw new Error('Unbekannter Quizstatus.');
  if (state.quiz?.phase === 'Question' && (!Array.isArray(state.quiz.options) || state.quiz.options.length < 2 || typeof state.quiz.prompt !== 'string')) throw new Error('Die Quizfrage ist unvollständig.');
  return state;
}

export function createBridge({ onSnapshot, onError = () => {} }) {
  let instance = null;
  let state = null;
  return {
    get state() { return state; },
    get connected() { return Boolean(instance && state?.ready); },
    attach(unityInstance) {
      if (!unityInstance || typeof unityInstance.SendMessage !== 'function') throw new Error('Die Spielverbindung fehlt.');
      instance = unityInstance;
      instance.SendMessage('KompetenzhausBridge', 'ReceiveCommand', JSON.stringify({ command: 'getState', payload: {} }));
    },
    receive(message) {
      try { state = parseSnapshot(message); onSnapshot(state); return true; }
      catch (error) { onError(error.message); return false; }
    },
    send(command, payload = {}) {
      if (!COMMANDS.has(command)) throw new Error(`Unbekannte Aktion: ${command}`);
      if (!instance || (!state?.ready && command !== 'getState')) throw new Error('Das Spiel ist noch nicht verbunden.');
      if (!payload || typeof payload !== 'object' || Array.isArray(payload)) throw new Error('Ungültige Aktionsdaten.');
      if (command === 'answer') {
        if (state?.quiz?.phase !== 'Question') throw new Error('Diese Frage kann gerade nicht beantwortet werden.');
        if (!Number.isInteger(payload.index) || payload.index < 0 || payload.index >= state.quiz.options.length) throw new Error('Wähle zuerst eine Antwort.');
      }
      if (command === 'continue' && state?.quiz?.phase !== 'Feedback') throw new Error('Es gibt noch keine Rückmeldung zum Fortsetzen.');
      instance.SendMessage('KompetenzhausBridge', 'ReceiveCommand', JSON.stringify({ command, payload }));
    },
  };
}

export function safeCompanionUrl(value, base) {
  if (!value || typeof value !== 'string') return '';
  try {
    const url = new URL(value, base);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password ? url.href : '';
  } catch { return ''; }
}

// This is navigation guidance, not another progression model. Eligibility and
// achievements are read from Unity; the helper never changes the host snapshot.
export function nextLearningStep(content, state, houseId, preferredModuleId = '') {
  const house = state?.architecture?.houses?.find(item => item.houseId === houseId);
  const slots = content?.modules?.filter(item => item.houseId === houseId) || [];
  const placed = new Set(state?.placedModuleIds || []);
  const remaining = slots.filter(item => !placed.has(item.id) || state?.moduleStates?.some(status => status.id === item.id && !status.positioned));
  const preferred = remaining.find(item => item.id === preferredModuleId);
  const candidate = preferred || remaining.find(item => {
    const status = state?.moduleStates?.find(status => status.id === item.id);
    return status && !status.needsMasterEntry && !(status.missingPrerequisiteIds || []).length;
  }) || remaining[0];
  const status = state?.moduleStates?.find(item => item.id === candidate?.id);
  const hasRoom = Boolean(house?.rooms?.some(room => room.tiles?.length));
  const mastered = (state?.quizMasteredModuleIds || []).includes(candidate?.id);
  const quest = content?.quests?.find(item => (item.houseId === houseId || item.moduleIds?.some(id => slots.some(slot => slot.id === id))) && state?.questStates?.some(status => status.id === item.id && status.canStart && !status.completed));
  return {
    phase: !hasRoom ? 'room' : candidate ? ((mastered || status?.alreadyPlaced) && status?.canBuild ? 'build' : 'learn') : quest ? 'quest' : 'explore',
    moduleId: candidate?.id || '', questId: quest?.id || '', hasRoom,
    canBuild: Boolean(hasRoom && status?.canBuild),
    placedCount: slots.filter(item => placed.has(item.id)).length,
    total: slots.length,
  };
}

export function buildTutorContext(state, catalog) {
  if (!catalog?.slots || !state?.ready) throw new Error('Der Lernkontext ist noch nicht bereit.');
  const mode = state.learningMode === 'Serious' ? 'serious' : 'frei', placed = {};
  for (const id of state.placedModuleIds || []) {
    if (!Object.hasOwn(catalog.slots, id)) continue;
    const slot = catalog.slots[id];
    const choice = state.competenceChoices?.find(item => item.moduleId === id);
    const code = state.moduleChoices?.find(item => item.slotId === id)?.moduleCode || state.moduleStates?.find(item => item.id === id)?.selectedCode;
    const candidate = { sp: choice?.specialisationId, thema: choice?.topicId, frage: choice?.thesisQuestionId, opt: code };
    placed[id] = Object.fromEntries(Object.entries(candidate).filter(([field, value]) => typeof value === 'string' && slot[field]?.includes(value)));
  }
  const gamePractice = {
    moduleIds: [...new Set(state.quizMasteredModuleIds || [])].filter(id => Object.hasOwn(catalog.slots, id)),
    questIds: [...new Set(state.completedQuestIds || [])].filter(id => catalog.questIds?.includes(id)),
  };
  const envelope = { schema: 'kompetenzhaus.tutor-context', version: 3, locale: state.language === 'en' ? 'en' : 'de', mode: 'tutor', launch: { moduleId: Object.hasOwn(catalog.slots, state.quiz?.moduleId) ? state.quiz.moduleId : null, scenarioId: null }, context: { v: 1, mode, placed: { [mode]: placed }, gamePractice } };
  if (['2.0.0-draft', '2.1.0-draft', '2.2.0-draft'].includes(state.frameworkProfile?.frameworkVersion)) envelope.frameworkVersion = state.frameworkProfile.frameworkVersion;
  return envelope;
}

export function copyArchitecture(state) {
  if (!state?.architecture || state.architecture.schemaVersion !== 1) throw new Error('Der Bauplan ist noch nicht bereit.');
  return JSON.parse(JSON.stringify(state.architecture));
}

export const tileKey = tile => `${tile.x}:${tile.z}:${tile.floor}`;
const endpointKey = (a, b) => [tileKey(a), tileKey(b)].sort().join('|');

function stairEntryPredicate(house) {
  // Match ArchitectureCirculationPlan: only named rooms enclose space. Every
  // ground-floor room gets its first exterior edge in z/x, then S/E/N/W order.
  const directions = [[0, -1], [1, 0], [0, 1], [-1, 0]];
  const rooms = new Map(house.rooms.flatMap(room => room.tiles.map(tile => [tileKey(tile), room.id])));
  const doors = new Set((house.connections || []).filter(link => link.kind === 'door').map(link => endpointKey(link.fromTile, link.toTile)));
  const entrances = new Set();
  const neighbours = tile => directions.map(([x, z]) => ({ x: tile.x + x, z: tile.z + z, floor: tile.floor }));
  for (const room of house.rooms.filter(room => room.tiles[0]?.floor === 0)) {
    const edge = [...room.tiles].sort((a, b) => a.z - b.z || a.x - b.x).flatMap(tile => neighbours(tile).filter(next => !rooms.has(tileKey(next))).map(next => endpointKey(tile, next)))[0];
    if (edge) entrances.add(edge);
  }
  return lower => neighbours(lower).some(next => rooms.get(tileKey(next)) === rooms.get(tileKey(lower)) || doors.has(endpointKey(lower, next)) || entrances.has(endpointKey(lower, next)));
}

export function connectionCandidates(house, fromRoomId, toRoomId, kind, gridSizeMetres = 4) {
  if (!house || fromRoomId === toRoomId || !['door', 'stair'].includes(kind)) return [];
  const from = house.rooms.find(room => room.id === fromRoomId), to = house.rooms.find(room => room.id === toRoomId);
  if (!from || !to) return [];
  const blocked = new Set();
  for (const placement of house.modulePlacements || []) {
    const width = placement.quarterTurns % 2 ? placement.depth : placement.width, depth = placement.quarterTurns % 2 ? placement.width : placement.depth;
    for (let x = 0; x < width; x++) for (let z = 0; z < depth; z++) blocked.add(tileKey({ x: placement.gridX + x, z: placement.gridZ + z, floor: placement.floor }));
  }
  for (const item of house.decorations || []) blocked.add(tileKey({ x: Math.floor(item.x / gridSizeMetres), z: Math.floor(item.z / gridSizeMetres), floor: item.floor }));
  for (const link of house.connections || []) if (link.kind === 'stair') { blocked.add(tileKey(link.fromTile)); blocked.add(tileKey(link.toTile)); }
  const existing = new Set((house.connections || []).map(link => `${link.kind}:${endpointKey(link.fromTile, link.toTile)}`));
  const hasStairEntry = kind === 'stair' ? stairEntryPredicate(house) : null;
  const pairs = [];
  for (const a of from.tiles) for (const b of to.tiles) {
    const horizontal = Math.abs(a.x - b.x) + Math.abs(a.z - b.z), vertical = Math.abs(a.floor - b.floor);
    if (kind === 'door' ? vertical !== 0 || horizontal !== 1 : vertical !== 1 || horizontal !== 0 || blocked.has(tileKey(a)) || blocked.has(tileKey(b))) continue;
    if (existing.has(`${kind}:${endpointKey(a, b)}`)) continue;
    if (hasStairEntry && !hasStairEntry(a.floor < b.floor ? a : b)) continue;
    pairs.push({ fromTile: { ...a }, toTile: { ...b } });
  }
  return pairs;
}

export function addConnection(house, { id, fromRoomId, toRoomId, kind, fromTile, toTile }, gridSizeMetres = 4) {
  if (!id || house.connections.some(link => link.id === id)) throw new Error('Diese Verbindung besitzt keine eindeutige Kennung.');
  const pair = connectionCandidates(house, fromRoomId, toRoomId, kind, gridSizeMetres).find(item => tileKey(item.fromTile) === tileKey(fromTile) && tileKey(item.toTile) === tileKey(toTile));
  if (!pair) throw new Error(kind === 'stair' ? 'Die Treppe braucht zwei freie Felder genau übereinander und unten einen Zugang. Verschiebe belegende Module oder Möbel; erweitere bei Bedarf den unteren Raum oder ergänze eine Tür.' : 'Diese Räume brauchen eine gemeinsame Wand auf derselben Etage.');
  house.connections.push({ id, fromRoomId, toRoomId, kind, fromTile: pair.fromTile, toTile: pair.toTile });
}

export function roomTiles({ x, z, floor, width, depth, shape = 'rectangle' }) {
  if (![x, z, floor, width, depth].every(Number.isInteger) || width < 1 || depth < 1 || width > 8 || depth > 8 || floor < 0 || floor > 3) throw new Error('Raummasse oder Geschoss liegen ausserhalb des Baubereichs.');
  const tiles = [];
  for (let dx = 0; dx < width; dx++) for (let dz = 0; dz < depth; dz++) {
    if (shape === 'l' && dx >= Math.ceil(width / 2) && dz >= Math.ceil(depth / 2)) continue;
    if (shape === 'courtyard' && dx > 0 && dz > 0 && dx < width - 1 && dz < depth - 1) continue;
    const tile = { x: x + dx, z: z + dz, floor };
    if (tile.x < -4 || tile.x > 3 || tile.z < -4 || tile.z > 3) throw new Error('Der Raum würde über den Bauplatz hinausragen.');
    tiles.push(tile);
  }
  return tiles;
}

export function supportTiles(house, tiles) {
  const occupied = new Set(house.footprintTiles.map(tileKey));
  for (const tile of tiles) for (let floor = 0; floor <= tile.floor; floor++) {
    const supporting = { x: tile.x, z: tile.z, floor };
    if (!occupied.has(tileKey(supporting))) { house.footprintTiles.push(supporting); occupied.add(tileKey(supporting)); }
  }
  house.courtyardCutouts = house.courtyardCutouts.filter(tile => !occupied.has(tileKey(tile)));
  house.floors = Math.max(house.floors, ...tiles.map(tile => tile.floor + 1));
}

export function moveRoom(house, roomId, dx, dz, gridSizeMetres = 4) {
  const room = house.rooms.find(item => item.id === roomId);
  if (!room) throw new Error('Wähle zuerst einen Raum.');
  const next = room.tiles.map(tile => ({ ...tile, x: tile.x + dx, z: tile.z + dz }));
  if (next.some(tile => tile.x < -4 || tile.x > 3 || tile.z < -4 || tile.z > 3)) throw new Error('Der Raum würde über den Bauplatz hinausragen.');
  room.tiles = next;
  supportTiles(house, next);
  for (const placement of house.modulePlacements) if (placement.roomId === roomId) { placement.gridX += dx; placement.gridZ += dz; }
  for (const decoration of house.decorations) if (decoration.roomId === roomId) { decoration.x += dx * gridSizeMetres; decoration.z += dz * gridSizeMetres; }
  for (const connection of house.connections) {
    if (connection.fromRoomId === roomId) { connection.fromTile.x += dx; connection.fromTile.z += dz; }
    if (connection.toRoomId === roomId) { connection.toTile.x += dx; connection.toTile.z += dz; }
  }
}

export function removeRoom(house, roomId) {
  if (!house.rooms.some(room => room.id === roomId)) throw new Error('Wähle zuerst einen Raum.');
  house.rooms = house.rooms.filter(room => room.id !== roomId);
  house.connections = house.connections.filter(link => link.fromRoomId !== roomId && link.toRoomId !== roomId);
  for (const placement of [...house.modulePlacements, ...house.decorations]) if (placement.roomId === roomId) placement.roomId = '';
  // The floor and learning progress remain. A validated host transaction owns the actual change.
}
