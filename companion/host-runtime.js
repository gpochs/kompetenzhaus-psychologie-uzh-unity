/* Inlined by build-companion.mjs. Host integration never sends a prompt automatically. */
const COMPANION_ORIGINS = new Set(['https://gpochs.github.io']);
let offeredContext = null;
let contextReplyTarget = null;
let sessionNonce = 0;
let primarySending = null;

function markGenerated(el) {
  el.dataset.kind = 'generated';
  if (el.querySelector('.generation-label')) return;
  const tag = document.createElement('span');
  tag.className = 'generation-label';
  tag.textContent = T('Mit Claude generiert · bitte prüfen', 'Generated with Claude · please check');
  el.appendChild(tag);
}

function acceptLearningContext(raw) {
  const incoming = normalizeTutorContext(raw, CONTEXT_CATALOG);
  const o = incoming.context;
  const placed = o.placed[o.mode];
  const ids = Object.keys(placed);
  const rc = { klin: 0, ekn: 0, swo: 0 }, sc = { DeNC: 0, HEA: 0, SEOP: 0 };
  for (const [id, st] of Object.entries(placed)) {
    if (st.thema && THEMA_R[st.thema]) rc[THEMA_R[st.thema]]++;
    if (st.sp && Object.hasOwn(sc, st.sp) && /^s0[4-9]$/.test(id)) sc[st.sp]++;
  }
  const ranked = Object.entries(rc).sort((a, b) => b[1] - a[1]);
  const richtung = ranked[0][1] >= 2 && ranked[0][1] > ranked[1][1] ? ranked[0][0] : null;
  const dom = Object.keys(sc).find(id => sc[id] >= 4) || null;
  const wpOpt = placed.wp && placed.wp.opt
    ? (placed.wp.opt === '06SM200-511' ? 'Klinische Neuropsychologie' : 'Economic and Consumer Psychology') : null;
  const baF = placed.BA && placed.BA.frage ? BA_FRAGEN[placed.BA.frage] || null : null;
  const baA = placed.BA && placed.BA.artefakt ? BA_ART[placed.BA.artefakt] || null : null;
  const supplied = o.tutor;
  // Commit only after the entire envelope has passed validation. No partial state updates.
  spielstand = {
    titel: ids.map(id => SLOT_TITEL[id][0]), ects: supplied.ects.bsc + supplied.ects.msc,
    profil: { richtung, rc, sc, dom, wpOpt, baF, baA },
    moduleIds: ids, placed, gamePractice: o.gamePractice, frameworkVersion: incoming.frameworkVersion, activeModuleId: incoming.launch.moduleId,
    trust: 'unverified-player-input', gameMode: o.mode
  };
  const wasEnvelope = typeof raw === 'string' ? Object.hasOwn(JSON.parse(raw), 'schema') : Object.hasOwn(raw, 'schema');
  if (wasEnvelope) { lang = incoming.locale; applyLang(); setMode(incoming.mode); }
  fillCvKomp();
  if (incoming.launch.scenarioId) { $('vignSel').value = incoming.launch.scenarioId; updateVignInfo(); }
  $('standTa').value = JSON.stringify(incoming, null, 2);
  $('standInfo').textContent = T(
    `${ids.length} Module übernommen · ${spielstand.ects} ECTS im Spiel · ${o.mode === 'serious' ? 'Serious Mode' : 'Freies Bauen'}. Selbst deklarierter Lernkontext, kein Leistungsnachweis.`,
    `${ids.length} modules imported · ${spielstand.ects} ECTS in the game · ${o.mode === 'serious' ? 'Serious mode' : 'Free building'}. Self-declared learning context, not a transcript.`
  ) + (incoming.ignored ? T(` ${incoming.ignored} unbekannte Einträge ausgelassen.`, ` ${incoming.ignored} unknown entries omitted.`) : '');
  $('standInfo').dataset.error = 'false';
  offeredContext = null;
  const offer = $('contextOffer'); if (offer) offer.hidden = true;
  if (contextReplyTarget) {
    contextReplyTarget.source.postMessage({ type: 'kh-tutor-context-accepted', version: 3, mode: incoming.mode, moduleCount: ids.length }, contextReplyTarget.origin);
    contextReplyTarget = null;
  }
  return incoming;
}

function importLearningContext() {
  try {
    const raw = $('standTa').value.trim();
    if (!raw) { clearLearningContext(); return; }
    acceptLearningContext(raw);
  } catch (_) {
    // Do not echo parser messages or untrusted payload fragments into HTML.
    $('standInfo').textContent = T('Der Lernkontext ist ungültig oder zu gross. Der bisherige Kontext bleibt erhalten. Bitte kopiere ihn erneut aus dem Spiel.',
      'The learning context is invalid or too large. Your previous context is unchanged. Please copy it from the game again.');
    $('standInfo').dataset.error = 'true';
  }
}

function clearLearningContext() {
  spielstand = null; offeredContext = null; contextReplyTarget = null;
  $('standTa').value = ''; $('standInfo').textContent = T('Kein Spielstand übergeben.', 'No game state supplied.');
  $('standInfo').dataset.error = 'false'; $('contextOffer').hidden = true;
  fillCvKomp();
}

function receiveLearningContext(event) {
  if (!COMPANION_ORIGINS.has(event.origin) || event.source !== window.parent) return;
  if (!event.data || event.data.type !== 'kh-tutor-context' || ![2, 3].includes(event.data.version)) return;
  try {
    const safe = normalizeTutorContext(event.data.payload, CONTEXT_CATALOG);
    offeredContext = safe;
    contextReplyTarget = { source: event.source, origin: event.origin };
    const count = Object.keys(safe.context.placed[safe.context.mode]).length;
    $('contextOfferText').textContent = T(`Das Spiel bietet ${count} Module als Lernkontext an. Erst mit deiner Auswahl werden sie übernommen.`,
      `The game offers ${count} modules as learning context. They are only applied when you accept.`);
    $('contextOffer').hidden = false;
  } catch (_) { /* Ignore malformed external messages; keep the learner's existing context. */ }
}

function initCompanionHost() {
  $('standImport').onclick = importLearningContext;
  $('standClear').onclick = clearLearningContext;
  $('contextAccept').onclick = () => { if (offeredContext) acceptLearningContext(offeredContext); };
  $('contextReject').onclick = () => { offeredContext = null; contextReplyTarget = null; $('contextOffer').hidden = true; };
  $('standTa').addEventListener('keydown', e => { if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') { e.preventDefault(); importLearningContext(); } });
  window.addEventListener('message', receiveLearningContext);
  if (window.parent !== window) window.parent.postMessage({ type: 'kh-tutor-ready', version: 3 }, 'https://gpochs.github.io');
  $('runtimeState').textContent = api
    ? T('Claude verbunden · Nutzung über dein Konto', 'Claude connected · uses your account limits')
    : T('Vorschau · für KI-Antworten in Claude öffnen', 'Preview · open in Claude for AI responses');
}
