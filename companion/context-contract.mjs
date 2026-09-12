/** Pure import boundary, also inlined into the standalone hosted companion. */
export function normalizeTutorContext(input, catalog) {
  const MAX_BYTES = 100000;
  const record = value => value !== null && typeof value === 'object' && !Array.isArray(value)
    && (Object.getPrototypeOf(value) === Object.prototype || Object.getPrototypeOf(value) === null);
  const fail = message => { throw new Error(message); };
  const own = (o, key) => Object.prototype.hasOwnProperty.call(o, key);
  const bounded = (value, min, max, integer = false) => typeof value === 'number' && Number.isFinite(value)
    && value >= min && value <= max && (!integer || Number.isInteger(value));
  if (typeof input === 'string' && new TextEncoder().encode(input).length > MAX_BYTES) fail('Context is too large.');
  const raw = typeof input === 'string' ? JSON.parse(input) : input;
  if (!record(raw)) fail('Expected a context object.');
  if (JSON.stringify(raw).length > MAX_BYTES) fail('Context is too large.');
  const envelope = own(raw, 'schema');
  if (envelope && (raw.schema !== 'kompetenzhaus.tutor-context' || ![2, 3].includes(raw.version))) fail('Unsupported context version.');
  const source = envelope ? raw.context : raw;
  if (!record(source)) fail('Missing game context.');
  const uiMode = envelope && ['tutor', 'vignette', 'cv', 'karriere'].includes(raw.mode) ? raw.mode : 'tutor';
  const locale = envelope && raw.locale === 'en' ? 'en' : 'de';
  const placedSource = source.placed;
  if (!record(placedSource)) fail('Missing placed modules.');
  let gameMode = ['frei', 'serious'].includes(source.mode) ? source.mode : null;
  if (!gameMode) {
    if (record(placedSource.frei) && !record(placedSource.serious)) gameMode = 'frei';
    else if (record(placedSource.serious) && !record(placedSource.frei)) gameMode = 'serious';
    else fail('A single active game mode is required.');
  }
  if (!record(placedSource[gameMode])) fail('Invalid active game mode.');
  const placed = {};
  let ignored = 0;
  const allowedChoice = (value, choices, field) => {
    if (value == null) return null;
    if (typeof value !== 'string' || !choices.includes(value)) fail(`Invalid ${field} choice.`);
    return value;
  };
  for (const [id, entry] of Object.entries(placedSource[gameMode])) {
    if (!own(catalog.slots, id)) { ignored++; continue; }
    if (!record(entry)) fail('Invalid module entry.');
    const slot = catalog.slots[id];
    placed[id] = {
      sp: allowedChoice(entry.sp, slot.sp, 'specialisation'),
      opt: allowedChoice(entry.opt, slot.opt, 'module'),
      thema: allowedChoice(entry.thema, slot.thema, 'theme'),
      frage: allowedChoice(entry.frage, slot.frage, 'question'),
      artefakt: allowedChoice(entry.artefakt, slot.artefakt, 'artefact')
    };
  }
  // Legacy profile scores and claimed levels are deliberately not imported.
  // They describe the former game's formula, not evidence for this framework.
  if (source.tutor != null && !record(source.tutor)) fail('Invalid legacy context.');
  const tutor = {};
  const safeIds = (value, allowed) => {
    if (value == null) return [];
    if (!Array.isArray(value) || value.length > 200) fail('Invalid practice list.');
    return [...new Set(value.filter(id => typeof id === 'string' && allowed.includes(id)))];
  };
  if (source.gamePractice != null && !record(source.gamePractice)) fail('Invalid practice context.');
  const gamePractice = {
    moduleIds: safeIds(source.gamePractice?.moduleIds, Object.keys(catalog.slots)),
    questIds: safeIds(source.gamePractice?.questIds, catalog.questIds || [])
  };
  // Recompute ECTS from known module IDs; imported totals are not authoritative.
  const ects = { bsc: 0, msc: 0 };
  for (const [id, choice] of Object.entries(placed)) ects[catalog.slots[id].haus] += catalog.optionCredits?.[choice.opt] ?? catalog.slots[id].ects;
  tutor.ects = ects;
  const request = record(raw.launch) ? raw.launch : {};
  const launch = {
    moduleId: typeof request.moduleId === 'string' && own(catalog.slots, request.moduleId) ? request.moduleId : null,
    scenarioId: ['klin', 'diag', 'ges', 'ao'].includes(request.scenarioId) ? request.scenarioId : null
  };
  return {
    schema: 'kompetenzhaus.tutor-context', version: 3, frameworkVersion: catalog.frameworkVersion || '2.0.0-draft', locale, mode: uiMode, launch,
    context: { v: 1, mode: gameMode, placed: { [gameMode]: placed }, tutor, gamePractice },
    trust: 'unverified-player-input', ignored
  };
}
