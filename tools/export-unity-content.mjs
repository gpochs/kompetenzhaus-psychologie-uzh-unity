/** Deterministic, dependency-free export of the public legacy curriculum. */
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { quests as authoredQuests } from '../content/additional-quests.mjs';
import { validateQuestAlignment } from './quest-framework-alignment.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const framework = JSON.parse(fs.readFileSync(path.join(root, 'content', 'competency-framework.json'), 'utf8'));
const sourceNames = ['daten-struktur.js', 'daten-texte.js', 'daten-quiz.js', 'daten-stufen.js'];
const window = {};
const sources = sourceNames.map(name => {
  const relativePath = `js/${name}`;
  const source = fs.readFileSync(path.join(root, relativePath), 'utf8');
  vm.runInNewContext(source, { window }, { timeout: 2000, filename: name });
  return { path: relativePath, sha256: crypto.createHash('sha256').update(source).digest('hex'), status: 'inherited-public-game-content' };
});
const structure = window.STRUKTUR;
const quests = authoredQuests.toSorted((a, b) => (a.type === 'main' ? 0 : 1) - (b.type === 'main' ? 0 : 1) || Number(a.stageId) - Number(b.stageId));
const texts = window.MODUL_TEXTE;
const quiz = window.QUIZ;
const fail = message => { throw new Error(message); };
const assert = (condition, message) => condition || fail(message);
const local = (value, context) => {
  assert(value && typeof value.de === 'string' && typeof value.en === 'string', `Missing de/en: ${context}`);
  assert(value.de.trim() && value.en.trim(), `Empty localization: ${context}`);
  return { de: value.de, en: value.en };
};
const unique = values => [...new Set(values)];
const dictionary = obj => Object.entries(obj).map(([id, value]) => ({ id, ...value }));
const questionFor = (code, question, index) => ({
  id: `${code}:q${index + 1}`,
  sourceIndex: index,
  type: question.typ || 'single-choice',
  positions: (question.pos || []).map((position, i) => local(position, `${code} position ${i}`)),
  prompt: local(question.q, `${code} question ${index}`),
  options: question.a.map((answer, answerIndex) => local(answer, `${code} answer ${answerIndex}`)),
  correctIndex: question.korrekt,
  explanation: local(question.erkl, `${code} explanation ${index}`),
});
const quizBanks = Object.entries(quiz).map(([id, questions]) => ({ id, questions: questions.map((q, i) => questionFor(id, q, i)) }));
const bankById = new Map(quizBanks.map(bank => [bank.id, bank]));
const competencies = structure.kompetenzen.map(competency => ({
  id: competency.id, fieldId: competency.feld,
  name: local(competency.name, competency.id), description: local(competency.ich, competency.id),
  progression: (window.ICH_STUFEN[competency.id] || []).map((text, index) => ({ stageId: String(index + 1), description: local(text, competency.id) })),
}));
const competencyIds = new Set(competencies.map(c => c.id));
const slotIds = new Set(structure.slots.map(s => s.slot));
assert(slotIds.size === structure.slots.length, 'Duplicate slot id');
const moduleFor = (slot, optional = false) => {
  const text = texts[slot.code];
  assert(text, `No module text for ${slot.code}`);
  const bank = bankById.get(slot.code);
  assert(bank, `No quiz for ${slot.code}`);
  const references = unique(Object.values(text.komp || {}).flat());
  references.forEach(id => assert(competencyIds.has(id), `Unknown competency ${id}`));
  const source = slot.pos || {};
  return {
    id: optional ? `option:${slot.code}` : slot.slot, code: slot.code,
    title: local(slot.titel, slot.code), shortTitle: local(slot.kurz || slot.titel, slot.code),
    stageId: String(slot.stufe || 3), stage: slot.stufe || 3,
    houseId: slot.haus || 'msc', groupId: slot.gruppe, ects: slot.ects,
    semester: slot.sem || 0, spansTwoSemesters: Boolean(slot.sem2), category: slot.kategorie,
    optionCodes: slot.optionen || [],
    prerequisiteIds: slot.voraus || [], recommendedIds: slot.empf || [],
    prerequisiteStatus: 'inherited-game-progression-not-a-certified-study-regulation',
    competencyIds: references, primaryCompetencyIds: text.haupt || [],
    description: local(text.heute, slot.code),
    futureDescription: local(text.zukunft, slot.code),
    learningObjectives: text.lernziele.map((objective, i) => ({ ...local(objective, `${slot.code} objective ${i}`), tags: objective.b || [] })),
    aiDescription: local(text.ki, slot.code), aiMode: text.kat,
    proposalStatus: 'Designentwurf',
    quizBankId: slot.code, questions: bank.questions,
    practiceQuest: { title: local(text.quest.titel, slot.code), description: local(text.quest.text, slot.code), status: 'inherited-public-design-proposal' },
    sourceReferences: text.quellen || [],
    legacyShape: slot.form || '',
    position: { x: source.x || 0, y: source.y || 0, z: source.z || 0, width: source.w || 1, depth: source.d || 1, height: source.h || 1 },
  };
};
const modules = structure.slots.map(slot => moduleFor(slot));
const optionalModules = structure.optionsmodule.map(slot => moduleFor(slot, true));
const stageIds = new Set(structure.stufen.map(s => String(s.n)));
const validateQuestion = (question, context) => {
  local(question.prompt, context);
  assert(Array.isArray(question.options) && question.options.length >= 2, `Too few options: ${context}`);
  question.options.forEach((option, i) => local(option, `${context} option ${i}`));
  assert(Number.isInteger(question.correctIndex) && question.correctIndex >= 0 && question.correctIndex < question.options.length, `Invalid correctIndex: ${context}`);
  local(question.explanation, context);
};
quizBanks.forEach(bank => bank.questions.forEach(q => validateQuestion(q, q.id)));
modules.forEach(module => {
  assert(stageIds.has(module.stageId), `Unknown stage: ${module.id}`);
  [...module.prerequisiteIds, ...module.recommendedIds].forEach(id => assert(slotIds.has(id), `Unknown prerequisite ${id}`));
});
const questIds = new Set();
const questionIds = new Set();
for (const quest of quests) {
  assert(!questIds.has(quest.id), `Duplicate quest ${quest.id}`); questIds.add(quest.id);
  local(quest.title, quest.id); local(quest.description, quest.id);
  local(quest.instructions, quest.id); local(quest.feedback, quest.id);
  assert(quest.status === 'Designentwurf', `Missing proposal status ${quest.id}`);
  assert(stageIds.has(quest.stageId), `Unknown quest stage ${quest.id}`);
  assert(['main', 'side'].includes(quest.type), `Unknown quest type ${quest.id}`);
  assert(quest.moduleIds.length > 0, `Missing modules ${quest.id}`);
  quest.moduleIds.forEach(id => assert(slotIds.has(id), `Unknown quest module ${id}`));
  validateQuestAlignment(quest, framework);
  assert(quest.questions.length >= 2, `Quest needs at least two checks ${quest.id}`);
  quest.questions.forEach(q => {
    assert(!questionIds.has(q.id), `Duplicate new question ${q.id}`); questionIds.add(q.id);
    validateQuestion(q, q.id);
  });
  assert(quest.reward && quest.reward.cosmeticId && quest.reward.experience > 0, `Missing cosmetic reward ${quest.id}`);
}
quests.forEach(quest => (quest.prerequisiteQuestIds || []).forEach(id => assert(questIds.has(id), `Unknown quest prerequisite ${id}`)));
const output = {
  schemaVersion: 1,
  metadata: {
    title: 'Kompetenzhaus Psychologie UZH — Unity', version: '1.0.0-content',
    sourceUrl: 'https://github.com/gpochs/kompetenzhaus-psychologie-uzh',
    publicGameSourceStatus: 'Previously published educational game; source statements preserved, not freshly verified study regulations.',
    contentStatus: 'Designentwurf',
    proposalNotice: {
      de: 'Die KI-Integration und zusätzlichen Lernaufgaben sind Designentwürfe. Modulabschluss und Belohnungen im Spiel sind keine Studienleistungen und keine Messung persönlicher Kompetenzen.',
      en: 'The AI integration and additional learning tasks are design proposals. In-game completion and rewards are not academic credit or a measurement of personal competence.',
    },
    generatedBy: 'tools/export-unity-content.mjs',
    sourceFiles: sources,
    inheritedCounts: { slots: modules.length, competencies: competencies.length, stages: structure.stufen.length, quizBanks: quizBanks.length, uniqueQuestions: quizBanks.reduce((n, bank) => n + bank.questions.length, 0) },
    additionalCounts: { quests: quests.length, questions: quests.reduce((n, quest) => n + quest.questions.length, 0) },
    sourceDate: structure.meta.stand,
    additionalQuestSource: { path: 'content/additional-quests.mjs', sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(root, 'content', 'additional-quests.mjs'))).digest('hex') },
  },
  rules: {
    masterEntryPrerequisiteId: 'BA', masterHouseId: 'msc', masterStageIds: ['3', '4'], allowDirectMasterEntry: true,
    legacyFreeBuildRequiresQuiz: false, legacySeriousBuildRequiresQuiz: true,
    quizCompletionRule: 'all-questions-correct-with-retry',
    feedbackAdvanceRule: 'explicit-continue-after-every-answer-including-final-answer',
    wrongAnswerRule: 'append-to-queue-after-feedback',
    progressMeaning: 'educational-game-progress-not-academic-credit-or-measured-competence',
  },
  competencies,
  stages: structure.stufen.map(stage => ({ id: String(stage.n), index: stage.n, name: local(stage.name, 'stage'), anchor: local(stage.anker, 'stage') })),
  modules, optionalModules, quizBanks, quests,
  journeyNarratives: [
    {
      houseId: 'bsc', status: 'Designentwurf', stageIds: ['1', '2'],
      title: { de: 'Bachelor: ein tragfähiges Fundament und ein eigenes Projekt', en: 'Bachelor: a sound foundation and a project of your own' },
      description: { de: 'Baue fachliche Grundlagen auf, prüfe Quellen und Methoden und arbeite zunehmend eigenständig. KI wird zum Lernpartner und Prüfgegenstand. Im Bachelordach verbindest du Evidenz, dokumentierte Entscheidungen und eine eigene Antwort auf neue Information.', en: 'Build disciplinary foundations, examine sources and methods and work with increasing independence. AI becomes a learning partner and an object of scrutiny. In the Bachelor roof, connect evidence, documented decisions and your own response to new information.' },
    },
    {
      houseId: 'msc', status: 'Designentwurf', stageIds: ['3', '4'],
      title: { de: 'Master: Wissen verbinden, gestalten und verantworten', en: 'Master: integrate knowledge, create and take responsibility' },
      description: { de: 'Gestalte ein eigenes Profil und übertrage Wissen auf neue Forschungs- und Praxissituationen. Verbinde widersprüchliche Evidenz, prüfe KI-gestützte Werkzeuge und begründe Entscheidungen unter Unsicherheit. Der Anspruch wächst durch fachliche Tiefe, Originalität und Selbstständigkeit.', en: 'Shape your own profile and transfer knowledge to new research and practice settings. Integrate conflicting evidence, audit AI-assisted tools and justify decisions under uncertainty. Expectations rise through disciplinary depth, originality and independence.' },
    },
  ],
  groups: dictionary(structure.gruppen), fields: dictionary(structure.felder),
  assessmentModes: dictionary(structure.pruefungslogik),
  specialisations: dictionary(structure.schwerpunkte),
  designStyles: structure.stile, palettes: structure.paletten,
  houses: dictionary(structure.haeuser),
  milestones: structure.meilensteine.map(m => ({ id: m.id, houseId: m.haus, moduleIds: m.slots, title: m.name, description: m.text })),
};
assert(output.metadata.inheritedCounts.slots === 43, 'Expected 43 inherited slots');
assert(output.metadata.inheritedCounts.competencies === 19, 'Expected 19 inherited competencies');
assert(output.metadata.inheritedCounts.uniqueQuestions === 130, 'Expected 130 inherited quiz questions');
// Every source prompt, option, answer and explanation survives exactly, including alternate modules.
for (const bank of quizBanks) {
  const original = quiz[bank.id];
  bank.questions.forEach((q, i) => {
    assert(JSON.stringify(q.prompt) === JSON.stringify(original[i].q), `Prompt fidelity failure ${q.id}`);
    assert(JSON.stringify(q.options) === JSON.stringify(original[i].a), `Option fidelity failure ${q.id}`);
    assert(JSON.stringify(q.explanation) === JSON.stringify(original[i].erkl), `Explanation fidelity failure ${q.id}`);
    assert(q.correctIndex === original[i].korrekt, `Answer fidelity failure ${q.id}`);
  });
}
const serialized = JSON.stringify(output, null, 2) + '\n';
const destination = path.join(root, 'content', 'kompetenzhaus-content.json');
const sourceSnapshot = JSON.stringify({ schemaVersion: 1, status: 'inherited-public-source-snapshot', structure, moduleTexts: texts, quiz, competencyStatements: window.ICH_STUFEN }, null, 2) + '\n';
const snapshotPath = path.join(root, 'content', 'legacy-public-data.json');
if (process.argv.includes('--check')) {
  assert(fs.existsSync(destination) && fs.readFileSync(destination, 'utf8') === serialized, 'Content JSON is out of date; run exporter without --check');
  assert(fs.existsSync(snapshotPath) && fs.readFileSync(snapshotPath, 'utf8') === sourceSnapshot, 'Public source snapshot is out of date');
} else {
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  fs.writeFileSync(destination, serialized, 'utf8');
  if (!fs.existsSync(snapshotPath) || fs.readFileSync(snapshotPath, 'utf8') !== sourceSnapshot)
    fs.writeFileSync(snapshotPath, sourceSnapshot, 'utf8');
}
console.log(JSON.stringify({ validated: true, output: path.relative(root, destination), ...output.metadata.inheritedCounts, ...output.metadata.additionalCounts, sha256: crypto.createHash('sha256').update(serialized).digest('hex') }, null, 2));
