import { previewFramework } from './preview-framework.mjs';
// Development-only, prominently labelled UI fixture. It does not emulate Unity gameplay.
const data = await (await fetch('../content/kompetenzhaus-content.json')).json();
const view = () => document.getElementById('preview').contentWindow;
const blankHouse = houseId => ({ houseId, theme: 'warm-timber', facadeStyle: 'open-bays', roofStyle: 'flat', starterPreset: 'empty', floors: 1, wallColor: '#e9e2d2', accentColor: '#247b80', footprintTiles: [], courtyardCutouts: [], rooms: [], connections: [], modulePlacements: [], decorations: [] });
const snapshot = { schemaVersion: 1, ready: true, worldReady: true, error: '', message: '', language: 'de', learningMode: 'Free', selectedHouseId: 'bsc', futureCurriculum: true, directMasterEntry: false, viewMode: 'BirdView', accessibility: { highContrast: false, reducedMotion: false, captions: true, textScale: 1, lookSensitivity: 1, masterVolume: .75 }, placedModuleIds: [], quizMasteredModuleIds: [], selfCheckedModuleIds: [], completedQuestIds: [], unlockedCosmeticIds: [], earnedExperience: 0, moduleChoices: [], moduleStates: data.modules.map(module => ({ id: module.id, selectedCode: module.code, canBuild: true, alreadyPlaced: false, positioned: false, missingPrerequisiteIds: [], needsSelfCheck: false, needsQuiz: false, needsMasterEntry: false })), questStates: data.quests.map(quest => ({ id: quest.id, canStart: !quest.prerequisiteQuestIds.length, completed: false })), quiz: { phase: 'Idle' }, architecture: { schemaVersion: 1, gridSizeMetres: 4, timeOfDay: 14, weather: 'clear', houses: [blankHouse('bsc'), blankHouse('msc')] }, canUndoArchitecture: false };
snapshot.visibleFloor = 0;
snapshot.showRoofs = false;
function push() { view().KompetenzhausBridge.receive(JSON.stringify(snapshot)); }
function showQuestion(id = data.modules[0].id, questId) {
  const selectedCode = snapshot.moduleStates.find(item => item.id === id)?.selectedCode;
  const item = questId ? data.quests.find(item => item.id === questId) : data.optionalModules.find(item => item.code === selectedCode) || data.modules.find(item => item.id === id);
  const q = item.questions[0], localise = value => value?.[snapshot.language] || value?.de || value;
  snapshot.quiz = { phase: 'Question', moduleId: questId ? '' : id, questId: questId || '', questionId: q.id, type: q.type, prompt: localise(q.prompt), options: q.options.map(localise), positions: (q.positions || []).map(localise), mastered: 0, total: item.questions.length, selectedIndex: -1, correct: false, explanation: '', correctAnswer: '', mistakes: 0 }; push();
}
document.getElementById('connect').onclick = () => {
  view().KompetenzhausShell.attach({ SendMessage(target, method, raw) { const { command, payload } = JSON.parse(raw); if (command === 'selectHouse') snapshot.selectedHouseId = payload.houseId; else if (command === 'setViewMode') snapshot.viewMode = payload.mode; else if (command === 'setLanguage') { snapshot.language = payload.language; snapshot.frameworkProfile = previewFramework(data, snapshot.language); } else if (command === 'setLearningMode') snapshot.learningMode = payload.mode; else if (command === 'setFutureCurriculum') snapshot.futureCurriculum = payload.enabled; else if (command === 'setDirectMasterEntry') snapshot.directMasterEntry = payload.enabled; else if (command === 'startModuleQuiz') return showQuestion(payload.moduleId); else if (command === 'startQuest') return showQuestion('', payload.questId); else if (command === 'closeQuiz') snapshot.quiz = { phase: 'Idle' }; else if (command === 'answer') { snapshot.quiz.phase = 'Feedback'; snapshot.quiz.selectedIndex = payload.index; snapshot.quiz.correct = false; snapshot.quiz.explanation = 'Expliziter UI-Test: Die Antwort wird hier nicht fachlich bewertet. In der echten Spielwelt liefert Unity die passende Erklärung.'; } else if (command === 'continue') { snapshot.quiz.phase = 'Complete'; } else if (command === 'commitArchitecture') { snapshot.architecture = payload.architecture; snapshot.canUndoArchitecture = false; } else if (command === 'setAccessibility') snapshot.accessibility = payload.accessibility; else if (command === 'setArchitectureView') { snapshot.visibleFloor = payload.visibleFloor; snapshot.showRoofs = payload.showRoofs; } else if (command === 'setUiFocus') return; push(); } }); push();
};
document.getElementById('question').onclick = () => showQuestion();
document.getElementById('last').onclick = () => { if (snapshot.quiz.phase === 'Idle') showQuestion(); snapshot.quiz.phase = 'Feedback'; snapshot.quiz.mastered = snapshot.quiz.total; snapshot.quiz.correct = true; snapshot.quiz.explanation = 'Dies ist der explizite UI-Test für die letzte Erklärung. Dieser Text bleibt sichtbar, bis du Weiter wählst.'; push(); };
document.getElementById('complete').onclick = () => { snapshot.quiz.phase = 'Complete'; push(); };
document.getElementById('profile').onclick = () => {
  snapshot.frameworkProfile = previewFramework(data, snapshot.language);
  snapshot.placedModuleIds = [data.modules.find(item => item.id === '003')?.id || data.modules[0].id];
  snapshot.quiz = { phase: 'Idle' }; push();
};
document.getElementById('profile-host').onclick = async () => {
  const button = document.getElementById('profile-host');
  try {
    const fixture = document.getElementById('profile-fixture').value;
    const response = await fetch('qa/framework-v2-' + fixture + '.json');
    if (!response.ok) throw new Error('C#-Fixture noch nicht bereit');
    const actual = await response.json();
    if (!actual.isSyntheticQaState || actual.frameworkProfile?.schemaVersion !== 2) throw new Error('Unbekannte Fixture');
    snapshot.frameworkProfile = actual.frameworkProfile;
    snapshot.language = actual.frameworkProfile.language;
    const opportunities = actual.frameworkProfile.competencies.flatMap(item => item.criteria.flatMap(criterion => criterion.opportunities));
    snapshot.placedModuleIds = [...new Set(opportunities.map(item => item.moduleId))];
    snapshot.moduleStates = snapshot.moduleStates.map(item => { const choice = opportunities.find(opportunity => opportunity.moduleId === item.id); return { ...item, alreadyPlaced: Boolean(choice), selectedCode: choice?.moduleCode || data.modules.find(module => module.id === item.id).code }; });
    snapshot.moduleChoices = opportunities.filter((item, index) => opportunities.findIndex(other => other.moduleId === item.moduleId) === index).map(item => ({ slotId: item.moduleId, moduleCode: item.moduleCode }));
    snapshot.quiz = { phase: 'Idle' }; push(); button.textContent = 'C# V2 geladen';
  } catch (error) { button.textContent = error.message; }
};
document.getElementById('language').onclick = () => {
  snapshot.language = snapshot.language === 'de' ? 'en' : 'de';
  snapshot.frameworkProfile = previewFramework(data, snapshot.language);
  push();
};
document.getElementById('profile-clear').onclick = () => { snapshot.frameworkProfile = null; push(); };
