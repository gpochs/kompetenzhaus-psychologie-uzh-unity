import { createFrameworkView, moduleLearningProposal, moduleAiPhases } from './framework-view.mjs';
import { createStaticShellLocalizer, translateShellText } from './shell-locale.mjs';
import { createBridge, safeCompanionUrl, copyArchitecture, tileKey, roomTiles, supportTiles, moveRoom, removeRoom, buildTutorContext, connectionCandidates, addConnection, nextLearningStep } from './bridge-contract.mjs';

const $ = id => document.getElementById(id);
const all = selector => [...document.querySelectorAll(selector)];
const config = window.KOMPETENZHAUS_CONFIG || {};
let content = null, state = null, started = false, selectedAnswer = -1, questionKey = '', lastMessage = '', toastTimer, uiFocused = false;
let builderHouse = 'bsc', brush = 'select', selectedRoom = '', target = { x: -1, z: -1 }, lastVolume = .75;
let companionPublication = null, companionCatalog = null;
let moduleLearningDesign = null;
let audioGestureSent = false;
let moduleDetailsKey = '';
let guideHidden = false, preferredModuleId = '';
let roomNameIsDefault = true;
const staticLocale = createStaticShellLocalizer(document);
let lastLanguage = 'de';
const ui = text => translateShellText(text, state?.language || 'de');
const t = (de, en) => state?.language === 'en' ? en : de;
const originalDecor = { plant: 'Pflanze', bench: 'Bank', lamp: 'Leuchte', noticeboard: 'Pinnwand', desk: 'Schreibtisch', chair: 'Stuhl', bookshelf: 'Bücherregal' };
const localise = value => typeof value === 'string' ? value : value?.[state?.language || 'de'] || value?.de || value?.en || '';
const node = (tag, text, className, translate = true) => { const element = document.createElement(tag); if (text != null) element.textContent = translate ? ui(text) : String(text); if (className) element.className = className; return element; };
const setText = (id, text, translate = true) => { $(id).textContent = translate ? ui(text) : String(text ?? ''); };
const visible = (id, show) => { $(id).hidden = !show; };
const moduleState = id => state?.moduleStates?.find(item => item.id === id);
const moduleSlot = id => content?.modules.find(item => item.id === id);
const moduleContent = id => {
  const slot = moduleSlot(id), code = moduleState(id)?.selectedCode;
  return content?.optionalModules?.find(item => item.code === code) || slot;
};
const houseName = id => ui(id === 'msc' ? 'Masterhaus' : id === 'all' ? 'Campus' : 'Bachelorhaus');
const command = (name, payload = {}) => { try { bridge.send(name, payload); return true; } catch (error) { toast(error.message, true); return false; } };
const bridge = createBridge({ onSnapshot: receiveState, onError: message => toast(message, true) });
window.KompetenzhausBridge = bridge;
window.KompetenzhausShell = {
  attach(instance) { bridge.attach(instance); },
  setLoading(message, progress) { setText('load-message', message); $('load-progress').value = Math.max(0, Math.min(1, progress || 0)); },
  failLoading(message) { setText('load-message', message); visible('reload-button', true); $('start-button').disabled = true; },
};

function toast(message, error = false) {
  if (!message) return;
  message = ui(message);
  clearTimeout(toastTimer); $('toast').textContent = message; $('toast').classList.toggle('error', error); visible('toast', true);
  $('live-status').textContent = message;
  toastTimer = setTimeout(() => visible('toast', false), error ? 9000 : 5200);
}
function updateUiFocus() {
  const focus = Boolean(document.querySelector('dialog[open]')) || (!$('builder').hidden && $('builder').contains(document.activeElement)) || (!started && Boolean(state?.ready));
  if (focus !== uiFocused && bridge.connected) { uiFocused = focus; command('setUiFocus', { enabled: focus }); }
}
function openDialog(id) {
  const dialog = $(id);
  if (id === 'modules-dialog') renderModules();
  if (id === 'quests-dialog') renderQuests();
  if (id === 'settings-dialog') renderSettings();
  if (id === 'companion-dialog') renderCompanion();
  if (id === 'study-dialog') renderStudy();
  for (const open of all('dialog[open]')) if (open !== dialog) open.close();
  if (!dialog.open) dialog.showModal();
  updateUiFocus();
}
function closeDialog(dialog) { if (dialog.id === 'quiz-dialog' && state?.quiz?.phase !== 'Idle') command('closeQuiz'); dialog.close(); updateUiFocus(); }
function closeAllDialogs() { for (const dialog of all('dialog[open]')) dialog.close(); updateUiFocus(); }
all('[data-open]').forEach(button => button.addEventListener('click', () => openDialog(button.dataset.open)));
all('[data-close]').forEach(button => button.addEventListener('click', () => closeDialog(button.closest('dialog'))));
all('dialog').forEach(dialog => {
  dialog.addEventListener('cancel', event => { event.preventDefault(); closeDialog(dialog); });
  dialog.addEventListener('close', updateUiFocus);
  dialog.addEventListener('keydown', event => {
    if (event.key !== 'Tab') return;
    const focusable = [...dialog.querySelectorAll('button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled),summary,[tabindex="0"]')].filter(element => element.getClientRects().length);
    if (!focusable.length) { event.preventDefault(); return; }
    if (event.shiftKey && document.activeElement === focusable[0]) { event.preventDefault(); focusable.at(-1).focus(); }
    if (!event.shiftKey && document.activeElement === focusable.at(-1)) { event.preventDefault(); focusable[0].focus(); }
  });
});
document.addEventListener('focusin', updateUiFocus);
for (const type of ['pointerdown', 'keydown']) document.addEventListener(type, event => { if (event.isTrusted && bridge.connected && !audioGestureSent) { audioGestureSent = true; command('userGesture'); } }, { capture: true });
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && !document.querySelector('dialog[open]') && !$('builder').hidden) { setBuilder(false); $('build-button').focus(); }
});

function receiveState(next) {
  const previousFocusId = document.activeElement?.id;
  state = next;
  const languageChanged = lastLanguage !== next.language;
  if (languageChanged) { staticLocale(next.language); lastLanguage = next.language; moduleDetailsKey = ''; if (roomNameIsDefault && !selectedRoom) $('room-name').value = ui('Mein Lernraum'); }
  if (next.selectedHouseId && next.selectedHouseId !== 'all') builderHouse = next.selectedHouseId;
  const accessible = next.accessibility || {};
  document.body.classList.toggle('high-contrast', Boolean(accessible.highContrast));
  document.body.classList.toggle('reduced-motion', Boolean(accessible.reducedMotion));
  document.documentElement.style.setProperty('--text-scale', Math.max(1, Math.min(1.4, accessible.textScale || 1)));
  $('sound-button').setAttribute('aria-pressed', String((accessible.masterVolume ?? .75) > 0));
  $('sound-button').setAttribute('aria-label', ui((accessible.masterVolume ?? .75) > 0 ? 'Ton ausschalten' : 'Ton einschalten'));
  $('start-button').disabled = !(content && next.ready && next.worldReady);
  if (next.ready && next.worldReady && content) window.KompetenzhausShell.setLoading(config.preview ? 'Der gekennzeichnete UI-Test ist verbunden.' : 'Deine Spielwelt ist bereit.', 1);
  const message = next.error || next.message || '';
  if (message && message !== lastMessage) toast(message, Boolean(next.error));
  lastMessage = message;
  renderHud();
  if (!$('builder').hidden) renderBuilder();
  if ($('modules-dialog').open) renderModules();
  if ($('quests-dialog').open) renderQuests();
  if ($('study-dialog')?.open) renderStudy();
  if (languageChanged && $('companion-dialog').open) renderCompanion();
  const previewBanner = document.querySelector('.preview-banner');
  if (previewBanner) previewBanner.textContent = ui('Oberflächenvorschau · keine verbundene Unity-Spielwelt');
  renderQuiz();
  if (previousFocusId && document.activeElement === document.body && $(previousFocusId)?.getClientRects().length) $(previousFocusId).focus({ preventScroll: true });
  updateUiFocus();
}
function renderHud() {
  if (!state || !content) return;
  all('[data-house]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.house === state.selectedHouseId)));
  setText('house-caption', state.selectedHouseId === 'all' ? ui('Dein Psychologiecampus') : t(`Dein ${houseName(state.selectedHouseId)}`, `Your ${houseName(state.selectedHouseId)}`));
  const slots = content.modules.filter(item => state.selectedHouseId === 'all' || item.houseId === state.selectedHouseId);
  const placed = new Set(state.placedModuleIds || []);
  const count = slots.filter(item => placed.has(item.id)).length;
  setText('progress-caption', t(`${count} von ${slots.length} Modulen gebaut`, `${count} of ${slots.length} modules built`));
  const exploring = state.viewMode === 'FirstPerson';
  $('build-button').setAttribute('aria-pressed', String(!exploring)); $('explore-button').setAttribute('aria-pressed', String(exploring));
  visible('movement-help', exploring && started); visible('world-hint', !exploring);
  const hint = state.interactionHint || (exploring ? t('E am Lernort · Zum Umsehen die linke Maustaste halten und ziehen.', 'Press E near a learning place. Hold the left mouse button and drag to look around.') : (state.language === 'en' ? 'Click a learning place to start. Design your house in the building studio.' : 'Lernort anklicken und loslegen. Im Bauatelier gestaltest du dein Haus.'));
  setText('world-hint', hint);
  setText('interaction-help', state.language === 'en' ? 'E · interact' : 'E · Lernort');
  const focusedHint = (state.focusedModuleId || state.focusedQuestId) && typeof state.interactionHint === 'string' ? state.interactionHint.trim() : '';
  setText('focused-learning-hint', focusedHint, false);
  visible('focused-learning-hint', exploring && started && Boolean(focusedHint));
  visible('zoom-controls', !exploring && started);
  visible('open-builder', !exploring && $('builder').hidden);
  renderJourney();
}
function begin() {
  if (!state?.ready || !state.worldReady || !content) return;
  const houseId = document.querySelector('input[name=degree]:checked').value;
  started = true; builderHouse = houseId;
  visible('welcome', false); visible('playing-ui', true); visible('journey-switch', true);
  command('selectHouse', { houseId });
  if (houseId === 'msc') command('setDirectMasterEntry', { enabled: true });
  command('setViewMode', { mode: 'BirdView' });
  guideHidden = false; setBuilder(false); renderJourney(); $('journey-action').focus(); updateUiFocus();
}
$('start-button').addEventListener('click', begin);
$('reload-button').addEventListener('click', () => location.reload());
$('home-button').addEventListener('click', () => { closeAllDialogs(); if (state?.quiz?.phase !== 'Idle') command('closeQuiz'); started = false; setBuilder(false); visible('welcome', true); visible('playing-ui', false); visible('journey-switch', false); $('start-button').focus(); updateUiFocus(); });
all('[data-house]').forEach(button => button.addEventListener('click', () => { selectedRoom = ''; guideHidden = false; command('selectHouse', { houseId: button.dataset.house }); }));
$('build-button').addEventListener('click', () => { command('setViewMode', { mode: 'BirdView' }); setBuilder(true); });
$('explore-button').addEventListener('click', () => { setBuilder(false); command('setViewMode', { mode: 'FirstPerson' }); $('unity-canvas').focus(); updateUiFocus(); });
$('zoom-in').addEventListener('click', () => command('zoomIn'));
$('zoom-out').addEventListener('click', () => command('zoomOut'));
$('open-builder').addEventListener('click', () => setBuilder(true));
$('close-builder').addEventListener('click', () => { setBuilder(false); $('build-button').focus(); });
function setBuilder(show) { visible('builder', show && started); if (show) { renderBuilder(); applyFloorView(); } if (state) renderHud(); updateUiFocus(); }

function journey() { return nextLearningStep(content, state, builderHouse, preferredModuleId); }
function renderJourney() {
  if (!state || !content) return;
  const step = journey(), module = moduleContent(step.moduleId), title = localise(module?.title);
  const show = started && state.viewMode !== 'FirstPerson' && $('builder').hidden;
  visible('journey-guide', show && !guideHidden); visible('show-journey', show && guideHidden);
  const names = {
    room: ['Hier beginnt dein Haus.', builderHouse === 'msc' ? 'Ein Ort für deine Forschungsfragen, Gespräche und nächsten Entdeckungen. Beginne mit einem Raum nach deinen Vorstellungen.' : 'Was möchtest du über Menschen verstehen? Gib dieser Neugier einen ersten Raum. Form und Grösse bestimmst du.', 'Ersten Raum entwerfen', 'Zuerst einen Lerncheck ausprobieren'],
    learn: ['Dein Raum wartet auf eine Idee.', title ? t('Entdecke «' + title + '». Der Lerncheck öffnet direkt; die Modulbeschreibung findest du dort unter «Modul verstehen».', 'Explore “' + title + '”. The learning check opens directly; its description is under “Understand this module”.') : 'Wähle einen Baustein aus deinem Studium und entdecke, was dahintersteckt.', 'Lerncheck öffnen', step.canBuild ? 'Im freien Modus direkt bauen' : 'Alle Module ansehen'],
    build: ['Bring deinen Baustein ins Haus.', t('Für «' + title + '» ist der nächste Schritt das Platzieren. Dein Lernweg wird so zu einem Ort, den du betreten kannst.', 'The next step for “' + title + '” is placement. Turn your learning path into a place you can enter.'), 'Modul ins Haus bauen', 'Lerncheck noch einmal öffnen'],
    quest: ['Dein Wissen bekommt einen neuen Kontext.', 'Eine Lernquest verbindet dein Studium mit einer konkreten Frage. Erkunde einen neuen Blickwinkel auf Psychologie und KI.', 'Lernquest starten', 'Haus erkunden'],
    explore: ['Dein Haus verbindet viele Perspektiven.', 'Erkunde deine Räume, gestalte sie weiter oder finde im Studium-Menü neue Zusammenhänge zwischen deinen Modulen.', 'Haus erkunden', 'Studium öffnen'],
  };
  const copy = names[step.phase];
  setText('journey-eyebrow', houseName(builderHouse) + ' · ' + ui('Dein nächster Schritt')); setText('journey-title', copy[0]); setText('journey-story', copy[1]); setText('journey-action', copy[2]); setText('journey-secondary', copy[3]);
  $('journey-action').disabled = !state.ready || !state.worldReady;
  $('journey-secondary').disabled = step.phase === 'room' && !step.moduleId;
  visible('starter-options', step.phase === 'room');
  const route = state.journey, knownQuest = route?.questId && content.quests.some(quest => quest.id === route.questId);
  visible('journey-quest', step.hasRoom && Boolean(knownQuest));
  if (knownQuest) {
    setText('journey-quest-title', route.title || 'Deine aktuelle Hauptquest');
    setText('journey-quest-purpose', route.purpose || '');
    setText('journey-quest-instructions', [route.instructions, route.location].filter(Boolean).join(' · '));
    setText('journey-quest-start', route.actionLabel || 'Quest öffnen');
  }
  for (const item of all('[data-step]')) { const active = item.dataset.step === step.phase || (step.phase === 'explore' && item.dataset.step === 'quest'); if (active) item.setAttribute('aria-current', 'step'); else item.removeAttribute('aria-current'); }
}
function openRoomEditor() {
  guideHidden = false; selectedRoom = ''; brush = 'select';
  command('setViewMode', { mode: 'BirdView' });
  $('floor-select').value = '0'; $('room-x').value = '-1'; $('room-z').value = '-1'; $('room-width').value = '2'; $('room-depth').value = '2'; $('room-shape').value = 'rectangle';
  $('room-name').value = ui(builderHouse === 'msc' ? 'Mein Forschungslabor' : 'Mein erster Lernraum');
  roomNameIsDefault = true;
  all('[data-brush]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.brush === 'select')));
  setBuilderTab('layout'); setBuilder(true); $('room-editor').open = true; $('room-name').focus(); $('room-editor').scrollIntoView({ block: 'start', behavior: 'instant' }); showRoomSize();
}
function openGuidedQuiz() {
  const id = journey().moduleId; if (!id) return openDialog('modules-dialog');
  preferredModuleId = id; command('startModuleQuiz', { moduleId: id });
}
function exploreHouse() { setBuilder(false); command('setViewMode', { mode: 'FirstPerson' }); $('unity-canvas').focus(); updateUiFocus(); }
$('journey-action').addEventListener('click', () => {
  const step = journey();
  if (step.phase === 'room') openRoomEditor();
  else if (step.phase === 'learn') openGuidedQuiz();
  else if (step.phase === 'build') command('placeModule', { moduleId: step.moduleId });
  else if (step.phase === 'quest') command('startQuest', { questId: step.questId });
  else exploreHouse();
});
$('journey-secondary').addEventListener('click', () => {
  const step = journey();
  if (step.phase === 'room' || step.phase === 'build') openGuidedQuiz();
  else if (step.phase === 'learn' && step.canBuild) command('placeModule', { moduleId: step.moduleId });
  else if (step.phase === 'quest') exploreHouse();
  else openDialog(step.phase === 'explore' ? 'study-dialog' : 'modules-dialog');
});
$('hide-journey').addEventListener('click', () => { guideHidden = true; renderJourney(); $('show-journey').focus(); });
$('show-journey').addEventListener('click', () => { guideHidden = false; renderJourney(); $('journey-action').focus(); });
$('builder-next').addEventListener('click', openGuidedQuiz);
$('journey-quest-start').addEventListener('click', () => { const id = state?.journey?.questId; if (content?.quests.some(quest => quest.id === id)) command('startQuest', { questId: id }); });
all('[data-starter]').forEach(button => button.addEventListener('click', () => {
  if (activeHouse()?.rooms?.length) return toast('Dein Haus hat bereits Räume. Vorlagen findest du im Bauatelier.');
  applyPreset(button.dataset.starter);
}));

function moduleName(id) { const module = moduleSlot(id); return module ? `${localise(module.shortTitle || module.title)} (${module.code})` : id; }
function appendStudyStructure(parent, slot) {
  const detail = node('details', null, 'study-structure'); detail.append(node('summary', 'Studienplatz und Voraussetzungen'));
  detail.append(node('p', 'Die folgenden Abhängigkeiten stammen aus der Spielplanung. Sie sind keine geprüften Zulassungs- oder Buchungsregeln.', 'field-help'));
  detail.append(node('p', slot.prerequisiteIds?.length ? t('Voraussetzungen im Spielmodell: ', 'Prerequisites in the game model: ') + slot.prerequisiteIds.map(moduleName).join(' · ') : 'Keine Modulvoraussetzung im Spielmodell.'));
  if (slot.recommendedIds?.length) detail.append(node('p', t('Empfohlene Vorbereitung: ', 'Recommended preparation: ') + slot.recommendedIds.map(moduleName).join(' · ')));
  if (slot.optionCodes?.length) detail.append(node('p', 'Dieses Feld enthält eine Wahloption. Du kannst sie im Lerncheck unter «Modul verstehen» vor dem Bauen festlegen.'));
  parent.append(detail);
}

function reasons(ms) {
  if (!ms) return ui('Das Modul wird vorbereitet.');
  const messages = [];
  if (ms.needsMasterEntry) messages.push(ui('Erst den Bachelorpfad abschliessen oder direkt im Master beginnen.'));
  if (ms.missingPrerequisiteIds?.length) messages.push(t('Vorher im Lernpfad: ', 'Earlier in the learning path: ') + ms.missingPrerequisiteIds.map(id => localise(moduleSlot(id)?.shortTitle || moduleSlot(id)?.title) || id).join(', ') + '.');
  if (ms.needsSelfCheck) messages.push(ui('Bestätige deine Selbsteinschätzung im Modul.'));
  if (ms.needsQuiz) messages.push(ui('Meistere zuerst den Lerncheck.'));
  return messages.join(' ');
}
function renderModules() {
  if (!content) return;
  const list = $('module-list'); list.replaceChildren();
  const query = $('module-search').value.toLocaleLowerCase('de');
  for (const slot of content.modules) {
    const module = moduleContent(slot.id), ms = moduleState(slot.id);
    if (state?.selectedHouseId !== 'all' && slot.houseId !== (state?.selectedHouseId || builderHouse)) continue;
    if (query && !`${localise(module.title)} ${module.code}`.toLocaleLowerCase('de').includes(query)) continue;
    const card = node('article', null, `entry-card${ms?.alreadyPlaced ? ' complete' : ''}`);
    const meta = node('div', null, 'entry-meta'); meta.append(node('span', houseName(slot.houseId)), node('span', module.code), node('span', `${module.ects} ` + t('ECTS im Quellenstand', 'ECTS in the source version')), node('span', `Semester ${slot.semester}${slot.spansTwoSemesters ? `–${slot.semester + 1}` : ''}`), node('span', t('Studienabschnitt ', 'Study milestone ') + slot.stageId));
    card.append(meta, node('h3', localise(module.title), null, false), node('p', localise(module.shortTitle || slot.shortTitle), null, false));
    if (ms?.alreadyPlaced) card.append(node('p', ms.positioned ? 'Im Haus platziert' : 'Gebaut · noch nicht im Bauplan platziert', 'entry-status'));
    appendStudyStructure(card, slot);
    const button = node('button', state?.quizMasteredModuleIds?.includes(slot.id) ? 'Lerncheck wiederholen' : 'Quiz starten', 'button primary'); button.disabled = !state?.ready;
    button.addEventListener('click', () => { closeAllDialogs(); command('startModuleQuiz', { moduleId: slot.id }); }); card.append(button);
    list.append(card);
  }
  if (!list.children.length) list.append(node('p', 'Für diese Suche gibt es kein Modul.'));
}
$('module-search').addEventListener('input', renderModules);
function renderQuests() {
  if (!content) return;
  const list = $('quest-list'); list.replaceChildren();
  for (const type of ['main', 'side']) {
    list.append(node('h3', type === 'main' ? 'Dein roter Faden' : 'Neue Blickwinkel', 'list-heading'));
    for (const quest of content.quests.filter(item => item.type === type)) {
      const qs = state?.questStates?.find(item => item.id === quest.id);
      const card = node('article', null, `entry-card${qs?.completed ? ' complete' : ''}`);
      const meta = node('div', null, 'entry-meta'); meta.append(node('span', quest.status || 'Designentwurf'), node('span', t(`ca. ${quest.estimatedMinutes} Minuten`, `about ${quest.estimatedMinutes} minutes`)));
      card.append(meta, node('h3', localise(quest.title), null, false), node('p', localise(quest.description), null, false));
      const detail = node('details'), summary = node('summary', 'Deine Aufgabe'); detail.append(summary, node('p', localise(quest.instructions), null, false)); card.append(detail);
      if (quest.reward) card.append(node('p', t('Im Spiel freischalten: ', 'Unlock in the game: ') + localise(quest.reward.title)));
      if (!qs?.canStart && !qs?.completed) {
        const missing = (quest.prerequisiteQuestIds || []).filter(id => !state?.completedQuestIds?.includes(id)).map(id => localise(content.quests.find(item => item.id === id)?.title));
        card.append(node('p', missing.length ? t('Vorher: ', 'First: ') + missing.join(' · ') : 'Dieser Schritt ist im aktuellen Lernpfad noch gesperrt.', 'blocked'));
      }
      const button = node('button', qs?.completed ? 'Nochmals erkunden' : 'Quest beginnen', 'button primary'); button.disabled = !state?.ready || (!qs?.canStart && !qs?.completed);
      button.addEventListener('click', () => { closeAllDialogs(); command('startQuest', { questId: quest.id }); }); card.append(button); list.append(card);
    }
  }
}

function renderQuiz() {
  const quiz = state?.quiz;
  if (!quiz || quiz.phase === 'Idle') { if ($('quiz-dialog').open) $('quiz-dialog').close(); questionKey = ''; return; }
  const module = moduleContent(quiz.moduleId), quest = content?.quests.find(item => item.id === quiz.questId);
  if (module) preferredModuleId = quiz.moduleId;
  setText('quiz-context', quest ? (quest.type === 'main' ? 'Hauptquest' : 'Nebenquest') : `${module?.code || ''} · ${ui('Lerncheck')}`);
  setText('quiz-title', localise(quest?.title || module?.shortTitle || module?.title) || 'Lerncheck');
  const key = `${quiz.moduleId}|${quiz.questId}|${quiz.questionId}|${quiz.phase}|${quiz.prompt}`;
  const fresh = key !== questionKey; questionKey = key;
  if (fresh) selectedAnswer = quiz.phase === 'Question' ? -1 : quiz.selectedIndex;
  const progress = $('quiz-progress'); progress.replaceChildren();
  for (let index = 0; index < quiz.total; index++) progress.append(node('span', null, index < quiz.mastered ? 'done' : ''));
  progress.append(node('small', t(`${quiz.mastered || 0} von ${quiz.total || 0} Fragen gemeistert`, `${quiz.mastered || 0} of ${quiz.total || 0} questions completed`)));
  setText('quiz-instructions', quiz.phase === 'Question' ? t(`Wähle aus ${quiz.options.length} Antworten und prüfe deine Auswahl. Scrolle bei Bedarf nach unten.`, `Choose from ${quiz.options.length} answers, then check your selection. Scroll down if needed.`) : quiz.phase === 'Feedback' ? 'Lies die Erklärung. Mit «Weiter» gehst du zum nächsten Schritt.' : 'Der Lerncheck ist abgeschlossen.');
  setText('question-prompt', quiz.prompt || '', false);
  visible('quiz-question', quiz.phase !== 'Complete');
  const positions = $('quiz-positions'); positions.replaceChildren();
  if (quiz.phase !== 'Complete') for (const item of quiz.positions || []) positions.append(node('p', item, null, false));
  if (fresh) {
    const options = $('answer-options'); options.replaceChildren();
    (quiz.options || []).forEach((text, index) => {
      const label = node('label', null, 'answer-option'), input = node('input');
      input.type = 'radio'; input.name = 'quiz-answer'; input.value = String(index); input.checked = index === selectedAnswer; input.disabled = quiz.phase !== 'Question';
      if (quiz.phase === 'Feedback' && index === quiz.selectedIndex) label.classList.add(quiz.correct ? 'correct' : 'wrong');
      input.addEventListener('change', () => { selectedAnswer = index; $('quiz-check').disabled = false; });
      label.append(input, node('span', text, null, false)); options.append(label);
    });
  }
  visible('quiz-check', quiz.phase === 'Question'); $('quiz-check').disabled = selectedAnswer < 0;
  visible('quiz-feedback', quiz.phase === 'Feedback');
  $('quiz-feedback').dataset.correct = String(Boolean(quiz.correct));
  setText('feedback-title', quiz.correct ? 'Genau. Darauf kommt es an.' : 'Ein guter Moment zum Nachdenken.');
  const explanation = [!quiz.correct && quiz.correctAnswer ? t('Die passende Antwort: ', 'The appropriate answer: ') + quiz.correctAnswer : '', quiz.explanation].filter(Boolean).join('\n\n');
  setText('feedback-text', explanation, false);
  visible('quiz-next', quiz.phase === 'Feedback');
  visible('quiz-complete', quiz.phase === 'Complete');
  setText('completion-text', quest ? localise(quest.feedback) : 'Du hast alle Fragen gemeistert. Dein Wissen bleibt die Grundlage für deinen nächsten Schritt.');
  visible('quiz-finish', quiz.phase === 'Complete');
  const ms = moduleState(quiz.moduleId);
  visible('quiz-build', Boolean(module && (quiz.phase === 'Complete' || state.learningMode === 'Free')));
  const moduleHouse = state.architecture?.houses?.find(house => house.houseId === moduleSlot(quiz.moduleId)?.houseId);
  const needsRoom = Boolean(module && !moduleHouse?.rooms?.some(room => room.tiles?.length));
  $('quiz-build').disabled = !needsRoom && !ms?.canBuild;
  setText('quiz-build', needsRoom ? 'Raum für dieses Modul entwerfen' : ms?.alreadyPlaced ? (ms.positioned ? 'Im Haus platziert' : 'Modul platzieren') : 'Modul ins Haus bauen');
  $('quiz-build').title = reasons(ms);
  visible('module-details', Boolean(module));
  const detailsKey = JSON.stringify([quiz.moduleId, ms?.selectedCode, state.competenceChoices, state.selfCheckedModuleIds, state.futureCurriculum, state.learningMode, state.language]);
  if (module && (fresh || detailsKey !== moduleDetailsKey)) { moduleDetailsKey = detailsKey; renderModuleDetails(quiz.moduleId); }
  setText('quiz-status-note', quest ? `${ui(quest.status || 'Designentwurf')} · ` + t('Spielbelohnung, kein Leistungsnachweis', 'game reward, not an academic assessment') : 'Lernaufgabe im Spiel · Modul- und KI-Planungen als Designentwurf');
  openDialog('quiz-dialog');
  if (fresh) {
    const focusTarget = quiz.phase === 'Question' ? $('answer-options').querySelector('input') : quiz.phase === 'Feedback' ? $('quiz-feedback') : $('quiz-complete');
    if (focusTarget) { if (focusTarget.tagName !== 'INPUT') focusTarget.tabIndex = -1; focusTarget.focus({ preventScroll: true }); }
    if (quiz.phase === 'Feedback') focusTarget?.scrollIntoView({ block: 'start', behavior: 'instant' });
    else $('quiz-dialog').scrollTop = 0;
  }
}
function renderModuleDetails(id) {
  const slot = moduleSlot(id), module = moduleContent(id), ms = moduleState(id), body = $('module-detail-body'); body.replaceChildren();
  body.append(node('h3', localise(module.title), null, false), node('p', 'Modul im Quellenstand', 'field-help'), node('p', localise(module.description), null, false));
  appendStudyStructure(body, slot);
  if (slot?.optionCodes?.length) {
    const label = node('label', 'Wahlpflichtmodul', 'field'), select = node('select');
    for (const code of slot.optionCodes) { const optionModule = content.optionalModules.find(item => item.code === code); const option = node('option', `${localise(optionModule?.title) || code} (${optionModule?.ects || slot.ects} ECTS)`); option.value = code; option.selected = code === (ms?.selectedCode || slot.code); select.append(option); }
    select.addEventListener('change', () => command('chooseModule', { moduleId: id, code: select.value })); label.append(select); body.append(label);
  }
  if (state.futureCurriculum) renderModuleProposal(body, slot, module);
  if (state.learningMode === 'Serious') {
    const label = node('label', null, 'check-field'), input = node('input'); input.type = 'checkbox'; input.id = `self-check-${id}`; input.checked = state.selfCheckedModuleIds?.includes(id) || false;
    input.addEventListener('change', () => command('selfCheck', { moduleId: id, enabled: input.checked })); label.append(input, node('span', 'Ich habe meine Vorkenntnisse zu diesem Modul eingeschätzt.')); body.append(label);
    if (reasons(ms)) body.append(node('p', reasons(ms), 'field-help'));
  }
  const choiceOptions = state.profile?.choiceOptions?.filter(option => option.moduleId === id) || [];
  if (choiceOptions.length) {
    body.append(node('h3', 'Dein Schwerpunkt'));
    const current = state.competenceChoices?.find(choice => choice.moduleId === id) || { moduleId: id, specialisationId: '', topicId: '', thesisQuestionId: '' };
    for (const [kind, field, title] of [['specialisation', 'specialisationId', 'Vertiefung'], ['topic', 'topicId', 'Thema'], ['thesisQuestion', 'thesisQuestionId', 'Forschungsfrage']]) {
      const options = choiceOptions.filter(option => option.kind === kind && (!option.parentId || option.parentId === current.topicId || option.parentId === current.specialisationId));
      if (!options.length) continue;
      const label = node('label', title, 'field'), select = node('select'); select.id = `module-choice-${id}-${kind}`; populate(select, options.map(option => [option.id, localise(option.name)]), current[field], 'Offen lassen');
      select.addEventListener('change', () => { const choice = { ...current, [field]: select.value }; if (kind === 'specialisation') { choice.topicId = ''; choice.thesisQuestionId = ''; } if (kind === 'topic') choice.thesisQuestionId = ''; command('setCompetenceChoice', { competenceChoice: choice }); }); label.append(select); body.append(label);
    }
    body.append(node('p', t('Deine Auswahl wird gespeichert. Die konkrete Zuordnung von Thema und Lerngelegenheiten bleibt ein offener Entwurf; sie erzeugt keine Kompetenzbewertung.', 'Your selection is saved. The specific connection between the topic and learning opportunities remains an open design question; it does not create a competence assessment.'), 'field-help'));
  }
}
function renderModuleProposal(body, slot, module) {
  const version = state?.frameworkProfile?.frameworkVersion || moduleLearningDesign?.frameworkVersion || '';
  const proposal = moduleLearningProposal(moduleLearningDesign, slot.id, module.code, version);
  body.append(node('h3', t('Modulbezogener Lernentwurf', 'Module learning design') + ' · ' + version));
  if (!proposal) { body.append(node('p', t('Der aktuelle Lernentwurf ist für dieses Modul noch nicht verfügbar.', 'The current learning design is not available for this module yet.'), 'field-help')); return; }
  const section = node('section', null, 'module-learning-proposal'); section.dataset.proposalVersion = version;
  section.append(node('p', localise(moduleLearningDesign.proposalNotice), 'proposal-note', false), node('p', localise(proposal.futureSummary), null, false));
  section.append(node('p', localise(moduleLearningDesign.levelRule), 'field-help', false));
  section.append(node('h4', t('Vorgeschlagene Lernziele', 'Proposed learning objectives')));
  for (const objective of proposal.objectives) {
    const detail = node('details', null, 'opportunity-detail'); detail.dataset.objectiveId = objective.id;
    detail.append(node('summary', objective.criterionId + ' · ' + t('Zielniveau ', 'Target level ') + ({1:'I',2:'II',3:'III'})[objective.targetLevel] + ' · ' + localise(objective.text), null, false));
    const descriptions = node('dl', null, 'opportunity-description');
    for (const [label, value] of [
      [t('Vorgeschlagene Aufgabe', 'Proposed task'), localise(objective.task)],
      [t('Vorgeschlagener Nachweis', 'Proposed evidence'), localise(objective.evidence)],
      [t('Vorgeschlagenes Beurteilungskriterium', 'Proposed success criterion'), localise(objective.successCriteria)],
      [t('Kontext', 'Context'), (objective.contextIds || []).map(id => state.frameworkProfile?.contexts?.find(item => item.id === id)?.title || ({'without-ai':t('Ohne KI','Without AI'),'with-ai':t('Mit KI als Werkzeug','With AI as a tool'),'about-ai':t('KI als Gegenstand','AI as the subject')})[id] || id).join(' · ')],
    ]) { const line = node('div', null, 'opportunity-line'); line.append(node('dt', label), node('dd', value, null, false)); descriptions.append(line); }
    detail.append(descriptions); section.append(detail);
  }
  const activity = node('details', null, 'opportunity-detail'); activity.append(node('summary', t('Lernaktivität, KI-Rolle und offene Entscheidungen', 'Learning activity, AI role and open decisions')));
  const descriptions = node('dl', null, 'opportunity-description');
  for (const [field, label] of [
    ['activitySequence', t('Vorgeschlagener Ablauf','Proposed sequence')], ['aiRole',t('Mögliche KI-Rolle','Possible AI role')],
    ['independentEvidence',t('Vorgeschlagener Eigenleistungsnachweis','Proposed evidence of independent work')],
    ['assessmentProposal',t('Beurteilung im Entwurf','Proposed assessment')], ['workloadIntegration',t('Einbindung in den Arbeitsaufwand','Workload integration')], ['openDecisions',t('Offene Entscheidungen','Open decisions')],
  ]) { const line = node('div', null, 'opportunity-line'); line.append(node('dt', label), node('dd', localise(proposal[field]), null, false)); descriptions.append(line); }
  activity.append(descriptions); section.append(activity);
  const aiPhases = moduleAiPhases(proposal);
  if (aiPhases.length) {
    const phases = node('details', null, 'opportunity-detail module-ai-phases');
    phases.append(node('summary', t('Lernphasen ohne, mit und über KI', 'Learning phases without, with and about AI')),
      node('p', t('Die Zuordnung verweist auf bestehende Lernziele. Sie schreibt weder KI-Nutzung noch KI-Verzicht für das gesamte Modul vor.', 'These links refer to existing learning objectives. They require neither AI use nor AI avoidance throughout the entire module.'), 'proposal-note'));
    const contextRule = state.frameworkProfile?.aiAcrossCurriculum?.phasePolicy?.assessmentRules?.find(rule => rule.id === 'context-variants');
    if (contextRule?.text) phases.append(node('p', localise(contextRule.text), 'field-help', false));
    const titles = { 'without-ai': t('Variante ohne eigenen KI-Einsatz', 'Variant without personal AI use'), 'with-ai': t('Variante mit KI-Unterstützung', 'Variant with AI assistance'), 'about-ai': t('KI als Gegenstand untersuchen', 'Examine AI as a subject') };
    for (const phase of aiPhases) {
      const part = node('section'); part.dataset.moduleAiPhase = phase.contextId; part.append(node('h4', titles[phase.contextId]));
      if (!phase.objectives.length) part.append(node('p', t('Für diese Phase ist hier noch kein Lernziel ausdrücklich zugeordnet.', 'No objective is explicitly assigned to this phase here yet.'), 'field-help'));
      for (const objective of phase.objectives) {
        const button = node('button', `${objective.criterionId} · ${localise(objective.text)}`, 'canonical-link', false); button.type = 'button'; button.dataset.phaseObjective = objective.id;
        button.addEventListener('click', () => { const target = [...section.querySelectorAll('[data-objective-id]')].find(item => item.dataset.objectiveId === objective.id); if (target) { target.open = true; target.querySelector('summary').focus({preventScroll:true}); target.scrollIntoView({block:'start',behavior:'instant'}); } }); part.append(button);
      }
      phases.append(part);
    }
    phases.append(node('p', t('Beurteilungsvorschlag: ', 'Proposed assessment: ') + localise(proposal.assessmentProposal), 'assessment-note', false));
    section.append(phases);
  }
  if (proposal.topicRequired || proposal.individualisationRequired) section.append(node('p', t('Thema oder Aufgabenkontext müssen noch konkret abgestimmt werden.', 'The topic or task context still needs to be agreed.'), 'proposal-note'));
  section.append(node('p', localise(moduleLearningDesign.costRule), 'field-help', false)); body.append(section);
}
$('quiz-check').addEventListener('click', () => { if (command('answer', { index: selectedAnswer })) $('quiz-check').disabled = true; });
$('quiz-next').addEventListener('click', () => command('continue'));
$('quiz-finish').addEventListener('click', () => command('closeQuiz'));
$('close-quiz').addEventListener('click', () => command('closeQuiz'));
$('quiz-build').addEventListener('click', () => {
  const id = state?.quiz?.moduleId; if (!id) return;
  const houseId = moduleSlot(id)?.houseId, house = state.architecture?.houses?.find(house => house.houseId === houseId);
  if (!house?.rooms?.some(room => room.tiles?.length)) { preferredModuleId = id; command('closeQuiz'); command('selectHouse', { houseId }); builderHouse = houseId; openRoomEditor(); }
  else command('placeModule', { moduleId: id });
});

function activeHouse(architecture = state?.architecture) { return architecture?.houses?.find(item => item.houseId === builderHouse); }
function editArchitecture(mutator) {
  try {
    const candidate = copyArchitecture(state), house = activeHouse(candidate);
    if (!house) throw new Error('Wähle ein Haus zum Bauen.');
    mutator(house, candidate); house.starterPreset = 'custom';
    if (house.footprintTiles.length > 64) throw new Error('Dein Haus darf insgesamt bis zu 64 Bodenfelder haben. Entferne zunächst unbenutzte Felder.');
    command('commitArchitecture', { architecture: candidate });
  } catch (error) { toast(error.message, true); }
}
function populate(select, entries, current, emptyText, translateEntries = true) {
  select.replaceChildren();
  if (emptyText) { const item = node('option', emptyText); item.value = ''; select.append(item); }
  for (const [value, text] of entries) { const option = node('option'); option.textContent = translateEntries ? ui(text) : String(text); option.value = value; select.append(option); }
  if (entries.some(([value]) => value === current)) select.value = current;
}
function renderBuilder() {
  const house = activeHouse(); if (!house) return;
  if (!house.rooms.some(room => room.id === selectedRoom)) selectedRoom = '';
  setText('builder-house-label', houseName(builderHouse)); setText('bay-count', `${house.footprintTiles.length} / 64 ` + t('Felder', 'tiles'));
  setText('builder-guide', house.rooms.length ? 'Dein Raum ist bereit für einen Lernbaustein. Öffne einen Lerncheck oder gestalte deinen Grundriss weiter.' : 'Dein erster Raum: Name und Grösse wählen, dann hinzufügen. Den Grundriss kannst du später verfeinern.');
  setText('builder-next', house.rooms.length ? 'Jetzt Lerncheck öffnen' : 'Zuerst einen Lerncheck ausprobieren');
  $('undo-button').disabled = !state.canUndoArchitecture;
  if (typeof state.showRoofs === 'boolean') $('show-roofs').checked = state.showRoofs;
  const floor = Number($('floor-select').value), occupied = new Set(house.footprintTiles.filter(tile => tile.floor === floor).map(tileKey));
  const grid = $('plan-grid'), gridFocused = grid.contains(document.activeElement); grid.replaceChildren();
  for (let z = -4; z <= 3; z++) {
    const row = node('div'); row.setAttribute('role', 'row');
    for (let x = -4; x <= 3; x++) {
      const tile = { x, z, floor }, room = house.rooms.find(item => item.tiles.some(cell => tileKey(cell) === tileKey(tile)));
      const button = node('button'); button.setAttribute('role', 'gridcell'); button.dataset.x = x; button.dataset.z = z;
      button.setAttribute('aria-label', t(`Feld ${x}, ${z}, Geschoss ${floor}`, `Tile ${x}, ${z}, floor ${floor}`) + ': ' + (room?.name || (occupied.has(tileKey(tile)) ? ui('Boden') : t('frei', 'clear'))));
      button.tabIndex = x === target.x && z === target.z ? 0 : -1;
      button.classList.toggle('occupied', occupied.has(tileKey(tile))); button.classList.toggle('room-tile', Boolean(room)); button.classList.toggle('selected-room', Boolean(room && room.id === selectedRoom)); button.classList.toggle('target', x === target.x && z === target.z);
      button.setAttribute('aria-selected', String(x === target.x && z === target.z));
      button.addEventListener('click', () => {
        target = { x, z }; $('room-x').value = x; $('room-z').value = z;
        if (brush === 'add') editArchitecture(next => supportTiles(next, [tile]));
        else if (brush === 'remove') editArchitecture(next => { next.footprintTiles = next.footprintTiles.filter(cell => tileKey(cell) !== tileKey(tile)); });
        else { if (room) { selectedRoom = room.id; loadRoom(); } renderBuilder(); }
      });
      button.addEventListener('keydown', event => {
        const delta = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[event.key];
        if (!delta) return; event.preventDefault(); target = { x: Math.max(-4, Math.min(3, x + delta[0])), z: Math.max(-4, Math.min(3, z + delta[1])) }; renderBuilder(); grid.querySelector('[tabindex="0"]')?.focus();
      }); row.append(button);
    } grid.append(row);
  }
  if (gridFocused) grid.querySelector('[tabindex="0"]')?.focus({ preventScroll: true });
  populate($('room-select'), house.rooms.map(room => [room.id, room.name || ui('Raum')]), selectedRoom, 'Neuen Raum anlegen', false);
  visible('room-edit-actions', Boolean(selectedRoom)); setText('save-room', selectedRoom ? 'Raum anpassen' : 'Raum hinzufügen');
  const builtModules = (state.placedModuleIds || []).map(id => moduleSlot(id)).filter(item => item?.houseId === builderHouse);
  populate($('placement-module'), builtModules.map(item => [item.id, localise(moduleContent(item.id)?.title)]), $('placement-module').value, 'Modul auswählen', false);
  $('place-built-module').disabled = !builtModules.length;
  for (const [id, field] of [['theme-select', 'theme'], ['facade-select', 'facadeStyle'], ['roof-select', 'roofStyle'], ['wall-color', 'wallColor'], ['accent-color', 'accentColor']]) if (document.activeElement !== $(id)) $(id).value = house[field];
  const cosmetics = new Map(Object.entries(originalDecor));
  for (const id of state.unlockedCosmeticIds || []) cosmetics.set(id, localise(content.quests.find(quest => quest.reward?.cosmeticId === id)?.reward?.title) || id);
  populate($('decoration-select'), [...cosmetics], $('decoration-select').value);
  populate($('placed-decoration'), house.decorations.map(item => [item.instanceId, cosmetics.get(item.cosmeticId) || item.cosmeticId]), $('placed-decoration').value, 'Objekt auswählen');
  if (document.activeElement !== $('time-slider')) $('time-slider').value = state.architecture.timeOfDay;
  showTime();
  showRoomSize();
  renderConnections();
}
function showRoomSize() {
  const width = Number($('room-width').value), depth = Number($('room-depth').value);
  setText('room-size-hint', width > 0 && depth > 0 ? t(`${width} × ${depth} Felder · ${width * 4} × ${depth * 4} m Aussenmass. Ein Feld im Plan legt die Position fest.`, `${width} × ${depth} tiles · ${width * 4} × ${depth * 4} m external dimensions. Select a tile on the plan to set the position.`) : 'Wähle Breite und Tiefe zwischen 1 und 8 Feldern.');
}
for (const id of ['room-width', 'room-depth']) $(id).addEventListener('input', showRoomSize);
$('room-name').addEventListener('input', () => { roomNameIsDefault = false; });
function loadRoom() {
  const room = activeHouse()?.rooms.find(item => item.id === selectedRoom);
  if (!room) { $('room-name').value = ui('Mein Lernraum'); roomNameIsDefault = true; return; }
  roomNameIsDefault = false;
  const xs = room.tiles.map(tile => tile.x), zs = room.tiles.map(tile => tile.z);
  $('room-name').value = room.name || ''; $('room-function').value = room.function || 'learning';
  $('room-x').value = Math.min(...xs); $('room-z').value = Math.min(...zs); $('room-width').value = Math.max(...xs) - Math.min(...xs) + 1; $('room-depth').value = Math.max(...zs) - Math.min(...zs) + 1;
  $('room-shape').value = room.tiles.length === Number($('room-width').value) * Number($('room-depth').value) ? 'rectangle' : 'l';
  if (room.tiles.length) $('floor-select').value = room.tiles[0].floor;
  applyFloorView();
}
$('room-select').addEventListener('change', () => { selectedRoom = $('room-select').value; loadRoom(); renderBuilder(); });
function applyFloorView() {
  const floor = Number($('floor-select').value); if (!Number.isInteger(floor) || floor < 0 || floor > 3 || !state?.worldReady) return;
  if (state.visibleFloor !== floor || state.showRoofs !== $('show-roofs').checked) command('setArchitectureView', { visibleFloor: floor, showRoofs: $('show-roofs').checked });
}
$('floor-select').addEventListener('change', () => { applyFloorView(); renderBuilder(); });
$('show-roofs').addEventListener('change', applyFloorView);

const connectionPairKey = pair => `${tileKey(pair.fromTile)}|${tileKey(pair.toTile)}`;
const floorName = floor => t(floor === 0 ? 'EG' : `${floor}. OG`, floor === 0 ? 'Ground floor' : `Floor ${floor}`);
function renderConnections() {
  const house = activeHouse(); if (!house) return;
  const roomEntries = house.rooms.map(room => [room.id, `${room.name || ui('Raum')} · ${floorName(room.tiles[0]?.floor || 0)}`]);
  populate($('connection-from'), roomEntries, $('connection-from').value || selectedRoom, 'Raum auswählen');
  populate($('connection-to'), roomEntries.filter(([id]) => id !== $('connection-from').value), $('connection-to').value, 'Raum auswählen');
  const kind = $('connection-kind').value;
  const pairs = connectionCandidates(house, $('connection-from').value, $('connection-to').value, kind, state.architecture.gridSizeMetres);
  const label = pair => `${floorName(pair.fromTile.floor)} (${pair.fromTile.x}, ${pair.fromTile.z}) → ${floorName(pair.toTile.floor)} (${pair.toTile.x}, ${pair.toTile.z})`;
  populate($('connection-endpoints'), pairs.map(pair => [connectionPairKey(pair), label(pair)]), $('connection-endpoints').value);
  $('add-connection').disabled = !pairs.length;
  if (!$('connection-from').value || !$('connection-to').value) setText('connection-help', 'Wähle zwei unterschiedliche Räume.');
  else setText('connection-help', pairs.length ? t(`${pairs.length} mögliche Verbindungsstelle${pairs.length > 1 ? 'n' : ''}.`, `${pairs.length} possible connection point${pairs.length > 1 ? 's' : ''}.`) : kind === 'stair' ? 'Keine passende Treppenstelle. Zwei Felder müssen frei übereinanderliegen; unten braucht es einen Zugang aus demselben Raum oder durch eine Tür.' : 'Keine freie gemeinsame Wand auf derselben Etage.');
  const roomLabel = id => house.rooms.find(room => room.id === id)?.name || ui('Raum');
  populate($('connection-existing'), house.connections.map(link => [link.id, `${ui(link.kind === 'stair' ? 'Treppe' : 'Tür')}: ${roomLabel(link.fromRoomId)} → ${roomLabel(link.toRoomId)} · ${floorName(link.fromTile.floor)}`]), $('connection-existing').value, 'Verbindung auswählen');
  $('remove-connection').disabled = !$('connection-existing').value;
}
for (const id of ['connection-kind', 'connection-from', 'connection-to']) $(id).addEventListener('change', renderConnections);
$('connection-existing').addEventListener('change', () => { $('remove-connection').disabled = !$('connection-existing').value; });
$('add-connection').addEventListener('click', () => editArchitecture((house, architecture) => {
  const kind = $('connection-kind').value, fromRoomId = $('connection-from').value, toRoomId = $('connection-to').value;
  const pair = connectionCandidates(house, fromRoomId, toRoomId, kind, architecture.gridSizeMetres).find(pair => connectionPairKey(pair) === $('connection-endpoints').value);
  if (!pair) throw new Error('Die Verbindungsstelle ist nicht mehr frei. Wähle eine passende Stelle.');
  addConnection(house, { id: `connection-${crypto.randomUUID()}`, fromRoomId, toRoomId, kind, ...pair }, architecture.gridSizeMetres);
}));
$('remove-connection').addEventListener('click', () => editArchitecture(house => {
  const id = $('connection-existing').value; if (!house.connections.some(link => link.id === id)) throw new Error('Wähle zuerst eine vorhandene Verbindung.');
  house.connections = house.connections.filter(link => link.id !== id);
}));
all('[data-brush]').forEach(button => button.addEventListener('click', () => { brush = button.dataset.brush; all('[data-brush]').forEach(item => item.setAttribute('aria-pressed', String(item === button))); }));
function setBuilderTab(name) { for (const tab of ['layout', 'style']) { const active = tab === name; $('tab-' + tab).setAttribute('aria-selected', String(active)); $('tab-' + tab).tabIndex = active ? 0 : -1; visible(tab + '-tools', active); } visible('builder-learning-actions', name === 'layout'); }
for (const name of ['layout', 'style']) { $('tab-' + name).addEventListener('click', () => setBuilderTab(name)); $('tab-' + name).addEventListener('keydown', event => { if (['ArrowLeft', 'ArrowRight'].includes(event.key)) { event.preventDefault(); const next = name === 'layout' ? 'style' : 'layout'; setBuilderTab(next); $('tab-' + next).focus(); } }); }
$('save-room').addEventListener('click', () => editArchitecture(house => {
  const tiles = roomTiles({ x: Number($('room-x').value), z: Number($('room-z').value), floor: Number($('floor-select').value), width: Number($('room-width').value), depth: Number($('room-depth').value), shape: $('room-shape').value });
  const room = house.rooms.find(item => item.id === selectedRoom);
  if (room) { room.tiles = tiles; room.name = $('room-name').value.trim() || ui('Mein Raum'); room.function = $('room-function').value; }
  else { selectedRoom = `room-${crypto.randomUUID()}`; house.rooms.push({ id: selectedRoom, name: $('room-name').value.trim() || ui('Mein Raum'), function: $('room-function').value, tiles }); }
  supportTiles(house, tiles);
}));
all('[data-nudge]').forEach(button => button.addEventListener('click', () => { const [dx, dz] = button.dataset.nudge.split(',').map(Number); editArchitecture((house, architecture) => moveRoom(house, selectedRoom, dx, dz, architecture.gridSizeMetres)); loadRoom(); }));
$('remove-room').addEventListener('click', () => editArchitecture(house => removeRoom(house, selectedRoom)));
$('rotate-room').addEventListener('click', () => editArchitecture((house, architecture) => {
  const room = house.rooms.find(item => item.id === selectedRoom); if (!room) throw new Error('Wähle zuerst einen Raum.');
  const minX = Math.min(...room.tiles.map(tile => tile.x)), minZ = Math.min(...room.tiles.map(tile => tile.z)), maxZ = Math.max(...room.tiles.map(tile => tile.z));
  const turn = tile => ({ ...tile, x: minX + maxZ - tile.z, z: minZ + tile.x - minX });
  const tiles = room.tiles.map(turn); if (tiles.some(tile => tile.x < -4 || tile.x > 3 || tile.z < -4 || tile.z > 3)) throw new Error('Der gedrehte Raum würde den Bauplatz verlassen.');
  room.tiles = tiles; supportTiles(house, tiles);
  for (const placement of house.modulePlacements.filter(item => item.roomId === selectedRoom)) { const effectiveDepth = placement.quarterTurns % 2 ? placement.width : placement.depth; const next = turn({ x: placement.gridX, z: placement.gridZ + effectiveDepth - 1 }); placement.gridX = next.x; placement.gridZ = next.z; placement.quarterTurns = (placement.quarterTurns + 1) % 4; }
  for (const decoration of house.decorations.filter(item => item.roomId === selectedRoom)) { const x = decoration.x / architecture.gridSizeMetres, z = decoration.z / architecture.gridSizeMetres; decoration.x = (minX + maxZ + 1 - z) * architecture.gridSizeMetres; decoration.z = (minZ + x - minX) * architecture.gridSizeMetres; decoration.rotationDegrees = (decoration.rotationDegrees + 90) % 360; }
  for (const link of house.connections) { if (link.fromRoomId === selectedRoom) link.fromTile = turn(link.fromTile); if (link.toRoomId === selectedRoom) link.toTile = turn(link.toTile); }
}));
function applyPreset(preset) { editArchitecture(house => {
  let tiles = [];
  if (preset === 'linear') tiles = roomTiles({ x: -3, z: -1, floor: 0, width: 6, depth: 2 });
  if (preset === 'courtyard') tiles = roomTiles({ x: -3, z: -2, floor: 0, width: 6, depth: 5 }).filter(tile => tile.x === -3 || tile.x === 2 || tile.z === -2);
  if (preset === 'pavilion-cluster') for (const [x, z] of [[-3, -3], [1, -3], [-1, 1]]) tiles.push(...roomTiles({ x, z, floor: 0, width: 2, depth: 2 }));
  house.footprintTiles = tiles; house.courtyardCutouts = []; house.rooms = []; house.modulePlacements = []; house.connections = []; house.decorations = []; house.floors = 1;
  // Add useful editable starter rooms rather than a fixed curriculum layout.
  if (preset === 'linear') house.rooms = [{ id: `room-${crypto.randomUUID()}`, name: ui('Mein Lernraum'), function: 'learning', tiles: tiles.map(tile => ({ ...tile })) }];
  if (preset === 'pavilion-cluster') for (let index = 0; index < 3; index++) house.rooms.push({ id: `room-${crypto.randomUUID()}`, name: t('Pavillon ', 'Pavilion ') + (index + 1), function: 'learning', tiles: tiles.slice(index * 4, index * 4 + 4).map(tile => ({ ...tile })) });
  if (preset === 'courtyard') house.rooms = [{ id: `room-${crypto.randomUUID()}`, name: ui('Raum am Innenhof'), function: 'learning', tiles: tiles.map(tile => ({ ...tile })) }];
}); }
$('apply-preset').addEventListener('click', () => applyPreset($('preset-select').value));
$('place-built-module').addEventListener('click', () => { const moduleId = $('placement-module').value; if (!moduleId) return toast('Wähle zuerst ein gebautes Modul.'); command('moveModule', { moduleId, houseId: builderHouse, x: target.x, z: target.z, floor: Number($('floor-select').value), rotation: 0, width: 1, depth: 1, roomId: selectedRoom }); });
$('save-style').addEventListener('click', () => editArchitecture(house => { house.theme = $('theme-select').value; house.facadeStyle = $('facade-select').value; house.roofStyle = $('roof-select').value; house.wallColor = $('wall-color').value; house.accentColor = $('accent-color').value; }));
$('add-decoration').addEventListener('click', () => editArchitecture((house, architecture) => { const cosmeticId = $('decoration-select').value; if (!cosmeticId) throw new Error('Wähle zuerst ein Objekt.'); house.decorations.push({ instanceId: `decor-${crypto.randomUUID()}`, cosmeticId, roomId: selectedRoom, x: (target.x + .5) * architecture.gridSizeMetres, z: (target.z + .5) * architecture.gridSizeMetres, floor: Number($('floor-select').value), rotationDegrees: 0, scale: 1 }); }));
$('rotate-decoration').addEventListener('click', () => editArchitecture(house => { const item = house.decorations.find(item => item.instanceId === $('placed-decoration').value); if (!item) throw new Error('Wähle zuerst ein aufgestelltes Objekt.'); item.rotationDegrees = (item.rotationDegrees + 90) % 360; }));
$('remove-decoration').addEventListener('click', () => editArchitecture(house => { const id = $('placed-decoration').value; if (!id) throw new Error('Wähle zuerst ein aufgestelltes Objekt.'); house.decorations = house.decorations.filter(item => item.instanceId !== id); }));
function showTime() { const value = Number($('time-slider').value); setText('time-label', `${String(Math.floor(value)).padStart(2, '0')}:${value % 1 ? '30' : '00'}`); }
$('time-slider').addEventListener('input', showTime);
$('save-time').addEventListener('click', () => editArchitecture((house, architecture) => { architecture.timeOfDay = Number($('time-slider').value); }));
$('undo-button').addEventListener('click', () => command('undoArchitecture'));

function accessibilityWithAudioDefaults(value) {
  return { ...value, audioMixVersion: 1, ambienceVolume: value?.ambienceVolume ?? 1, effectsVolume: value?.effectsVolume ?? 1, footstepsVolume: value?.footstepsVolume ?? 1 };
}
function renderSettings() {
  if (!state) return;
  $('language-select').value = state.language; $('learning-mode').value = state.learningMode; $('future-curriculum').checked = state.futureCurriculum; $('direct-master').checked = state.directMasterEntry;
  const access = accessibilityWithAudioDefaults(state.accessibility);
  for (const [id, field] of [['volume-slider', 'masterVolume'], ['ambience-volume', 'ambienceVolume'], ['effects-volume', 'effectsVolume'], ['footsteps-volume', 'footstepsVolume'], ['text-scale', 'textScale'], ['look-sensitivity', 'lookSensitivity']]) $(id).value = access[field] ?? 1;
  for (const [id, field] of [['reduced-motion', 'reducedMotion'], ['high-contrast', 'highContrast'], ['captions', 'captions']]) $(id).checked = Boolean(access[field]);
}
$('save-settings').addEventListener('click', () => {
  const accessibility = { ...accessibilityWithAudioDefaults(state?.accessibility), highContrast: $('high-contrast').checked, reducedMotion: $('reduced-motion').checked, captions: $('captions').checked, textScale: Number($('text-scale').value), lookSensitivity: Number($('look-sensitivity').value), masterVolume: Number($('volume-slider').value), ambienceVolume: Number($('ambience-volume').value), effectsVolume: Number($('effects-volume').value), footstepsVolume: Number($('footsteps-volume').value) };
  const changes = [['setLanguage', { language: $('language-select').value }], ['setLearningMode', { mode: $('learning-mode').value }], ['setFutureCurriculum', { enabled: $('future-curriculum').checked }], ['setDirectMasterEntry', { enabled: $('direct-master').checked }], ['setAccessibility', { accessibility }]];
  for (const [name, payload] of changes) command(name, payload);
  closeDialog($('settings-dialog'));
});
$('sound-button').addEventListener('click', () => { const value = state?.accessibility?.masterVolume ?? .75; if (value > 0) lastVolume = value; command('setAccessibility', { accessibility: { ...accessibilityWithAudioDefaults(state?.accessibility), masterVolume: value > 0 ? 0 : lastVolume } }); });
function renderCompanion() {
  const url = safeCompanionUrl(config.companionUrl || companionPublication?.publicUrl, location.href);
  visible('companion-link', Boolean(url)); visible('companion-unavailable', !url);
  if (url) $('companion-link').href = url;
  const module = moduleContent(state?.quiz?.moduleId); setText('companion-context', module ? t('Dein aktueller Lernkontext: ', 'Your current learning context: ') + localise(module.title) : 'Du bestimmst, welche Frage du mitnimmst.');
  $('copy-context').disabled = !state?.ready || !companionCatalog;
  const embed = safeCompanionUrl(config.companionEmbedUrl || companionPublication?.embedUrl, location.href);
  const allowed = companionPublication?.embedGenerationVerified === true && companionPublication?.allowedEmbedDomains?.includes(location.hostname);
  visible('companion-frame', Boolean(embed && allowed)); $('companion-dialog').classList.toggle('connected', Boolean(embed && allowed));
  if (embed && allowed && !$('companion-frame').src) $('companion-frame').src = embed;
  setText('companion-embed-note', embed && allowed ? 'Falls die Einbettung eine Anmeldung verlangt oder leer bleibt, öffne die Begleitung separat.' : t('Öffne die Begleitung in Claude, um KI-Antworten zu erhalten. Deinen Lernkontext kannst du freiwillig mitnehmen; das Spiel bleibt hier geöffnet.', 'Open the companion in Claude to receive AI responses. You can choose to take your learning context with you; the game stays open here.'));
}
$('copy-context').addEventListener('click', async () => {
  try { const text = JSON.stringify(buildTutorContext(state, companionCatalog), null, 2); await navigator.clipboard.writeText(text); setText('copy-context-status', 'Kopiert. Öffne in der KI-Begleitung «Spielstand importieren» und füge den Text dort ein. Der Import startet kein Gespräch.'); }
  catch (error) { setText('copy-context-status', t('Der Kontext konnte nicht kopiert werden: ', 'The context could not be copied: ') + error.message); }
});
const frameworkView = createFrameworkView({ onModule(id) { closeAllDialogs(); command('startModuleQuiz', { moduleId: id }); }, moduleTitle: id => localise(moduleContent(id)?.title) || id });
function renderStudy() {
  const credits = $('study-credits'); credits.replaceChildren();
  const english = state?.language === 'en';
  // Academic credit totals remain module metadata, separate from framework V2.
  for (const degree of state?.profile?.credits || []) {
    const card = node('div', null, 'degree-credit');
    card.append(node('strong', english ? (degree.houseId === 'msc' ? 'Master house' : 'Bachelor house') : houseName(degree.houseId)), node('p', degree.selectedEcts + (english ? ' ECTS in built modules' : ' ECTS in gebauten Modulen')), node('small', degree.targetEcts + (english ? ' ECTS programme target in the model' : ' ECTS Zielumfang im Modell'))); credits.append(card);
  }
  frameworkView.render(state);
}
function selectStudyTab(name) { for (const tab of ['modules', 'profile', 'compass']) { const active = tab === name; $('study-tab-' + tab).setAttribute('aria-selected', String(active)); $('study-tab-' + tab).tabIndex = active ? 0 : -1; visible('study-' + tab, active); } }
for (const [index, name] of ['modules', 'profile', 'compass'].entries()) {
  $('study-tab-' + name).addEventListener('click', () => selectStudyTab(name));
  $('study-tab-' + name).addEventListener('keydown', event => { if (['ArrowLeft', 'ArrowRight'].includes(event.key)) { event.preventDefault(); const next = ['modules', 'profile', 'compass'][(index + (event.key === 'ArrowRight' ? 1 : 2)) % 3]; selectStudyTab(next); $('study-tab-' + next).focus(); } });
}

async function initialise() {
  if (config.preview === true) { const banner = node('div', 'Oberflächenvorschau · keine verbundene Unity-Spielwelt', 'preview-banner'); document.body.append(banner); }
  if (config.coverUrl) { $('cover-image').addEventListener('load', () => visible('welcome-art', true)); $('cover-image').src = config.coverUrl; }
  try {
    const response = await fetch(config.contentUrl, { credentials: 'same-origin' }); if (!response.ok) throw new Error('Die Lerninhalte konnten nicht geladen werden.');
    content = await response.json();
    if (content.schemaVersion !== 1 || !Array.isArray(content.modules) || !Array.isArray(content.quests)) throw new Error('Die Lerninhalte haben ein unbekanntes Format.');
    try {
      const designResponse = await fetch(config.moduleDesignUrl || (config.preview ? '../content/' : 'content/') + 'module-learning-design.json', { credentials: 'same-origin' });
      if (designResponse.ok) moduleLearningDesign = await designResponse.json();
    } catch { moduleLearningDesign = null; }
    if (state) receiveState(state);
  } catch (error) { window.KompetenzhausShell.failLoading(error.message); }
  const companionBase = config.preview ? '../companion/' : 'companion/';
  const responses = await Promise.allSettled([fetch(config.companionPublicationUrl || companionBase + 'published.json').then(response => response.ok ? response.json() : null), fetch(config.companionCatalogUrl || companionBase + 'context-catalog.json').then(response => response.ok ? response.json() : null)]);
  companionPublication = responses[0].status === 'fulfilled' ? responses[0].value : null;
  companionCatalog = responses[1].status === 'fulfilled' ? responses[1].value : null;
  if ($('companion-dialog').open) renderCompanion();
}
initialise();
window.dispatchEvent(new Event('kompetenzhaus-shell-ready'));
