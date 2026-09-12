import fs from 'node:fs';
import path from 'node:path';
export function upgradeFramework(html,root,catalog){
  const read=name=>JSON.parse(fs.readFileSync(path.join(root,'content',name+'.json'),'utf8'));
  const framework=read('competency-framework'),careers=read('career-profiles'),learning=read('module-learning-design');
  if(careers.frameworkVersion!==framework.version||learning.frameworkVersion!==framework.version)throw new Error('Framework versions differ.');
  // Only fields actually used by the hosted tutor. The full design remains in the game.
  const tutorLearning={modules:learning.modules.map(m=>({id:m.id,code:m.code,kind:m.kind,baseline:{title:m.baseline.title,description:m.baseline.description},proposal:{futureSummary:m.proposal.futureSummary,aiRole:m.proposal.aiRole,independentEvidence:m.proposal.independentEvidence,openDecisions:m.proposal.openDecisions,objectives:m.proposal.objectives.map(o=>({criterionId:o.criterionId,targetLevel:o.targetLevel,contextIds:o.contextIds,text:o.text,task:o.task,evidence:o.evidence}))}}))};
  const safe=value=>JSON.stringify(value).replace(/<\/script/gi,'<\\/script');
  const replaceSection=(start,end,value)=>{
    const a=html.indexOf(start),b=html.indexOf(end,a+start.length);
    if(a<0||b<0)throw new Error('Missing companion section '+start);
    html=html.slice(0,a)+value+'\n'+html.slice(b);
  };
  const oldCatalog=html.match(/const CONTEXT_CATALOG = ([\s\S]*?);\n/);
  if(!oldCatalog)throw new Error('Context catalog anchor absent.');
  catalog.frameworkVersion=framework.version;catalog.competenceIds=framework.competencies.map(c=>c.id);
  catalog.careerNames=Object.fromEntries(careers.roles.map(r=>[r.id,r.title]));
  catalog.questIds=JSON.parse(fs.readFileSync(path.join(root,'content/kompetenzhaus-content.json'),'utf8')).quests.map(q=>q.id);
  catalog.optionCredits=Object.fromEntries(learning.modules.filter(m=>m.kind==='option').map(m=>[m.code,m.baseline.ects]));
  html=html.replace(oldCatalog[0],()=>`const CONTEXT_CATALOG = ${safe(catalog)};\n`);
  replaceSection('const KOMP = [','/* Rollenverteilung',`const FRAMEWORK=${safe(framework)};\nconst CAREERS=${safe(careers)};\nconst LEARNING=${safe(tutorLearning)};\nconst KOMP=${safe(framework.competencies.map(c=>[c.id,c.title.de,c.title.en]))};`);
  replaceSection('const ROLLEN = {','/* Wahlprofil-Logik',`const ROLLEN=${safe(Object.fromEntries(framework.competencies.map(c=>[c.id,{de:'Mitarbeit in einem psychologischen Forschungs- oder Praxisprojekt',en:'Work on a psychology research or practice project'}])))};`);
  replaceSection('const PFADE = [','/* ===== Zustand',`const PFADE=${safe(careers.roles.map(r=>({id:r.id,de:r.title.de,en:r.title.en,f:{de:r.facets.map(f=>f.title.de+': '+f.activity.de).join(' '),en:r.facets.map(f=>f.title.en+': '+f.activity.en).join(' ')}})))};`);
  replaceSection('const STUFEN_TXT = {','function fillSelects()',`const STUFEN_TXT=${safe(Object.fromEntries(framework.levelDefinitions.map(l=>[l.level,l.description])))};`);
  replaceSection('function fillCvKomp() {','function updateVignInfo()', '');
  replaceSection('function standText() {','function ctxVign(v) {',fs.readFileSync(path.join(root,'companion/framework-runtime.js'),'utf8'));
  replaceSection('$("cvGo").onclick = async () => {','$("karrGo").onclick',`$("cvGo").onclick=generateReflection;`);
  html=html.replace('<div id="cvBeispiel"></div>',`<label style="flex-basis:100%;display:grid;gap:5px" data-de="Eigene Erfahrung (optional, ohne sensible Angaben)" data-en="Your experience (optional, no sensitive details)">Eigene Erfahrung (optional, ohne sensible Angaben)</label><textarea id="cvEvidence" aria-label="Eigene Erfahrung / Your experience" rows="3" maxlength="800" style="width:100%;font:inherit;padding:8px;border:1px solid #cfc5b7;border-radius:8px"></textarea><div id="cvBeispiel"></div>`);
  html=html.replaceAll('Fa1–Fa10, KI1–KI6, Fu1–Fu3','Fachwissenschaft, Future Skills und KI').replaceAll('competences (Fachwissenschaft, Future Skills und KI)','competencies (psychological science, future skills and AI)');
  html=html.replaceAll('Modulwahl und vorhandene Kompetenzwerte','Modulwahl und Spielübungen').replaceAll('module choices and available competence values','module choices and game practice');
  const modelRelease=framework.version.replace(/\.0-draft$/, '');
  html=html.replaceAll('Kompetenzmodell vom 02.07.2026',`Kompetenzmodell ${modelRelease} vom September 2026`).replaceAll('model of 2 July 2026',`model ${modelRelease} of September 2026`);
  html=html.replaceAll('karrAktiv.de.replace(/^[^ ]+ /, "")','karrAktiv.de').replaceAll('karrAktiv.en.replace(/^[^ ]+ /, "")','karrAktiv.en');
  replaceSection('function stufenFuss(stufe) {','const STARTFRAGEN = {','');
  for(const [before,after] of [
    ['Was bedeutet [B] bei meinen Modulen konkret?','Wie kann KI das Lernen in meinem gewählten Modul unterstützen?'],
    ['What does [B] mean for my modules in practice?','How could AI support learning in my selected module?'],
    ['Welche Kompetenz ist bei mir am schwächsten und woran liegt das?','Welche Lerngelegenheiten könnte ich als Nächstes erkunden?'],
    ['Which competence is weakest for me and why?','Which learning opportunities could I explore next?'],
    ['Was fehlt mir für diesen Weg noch am meisten?','Welche Tätigkeiten und Lerngelegenheiten gehören zu diesem Weg?'],
    ['What is still missing most for this route?','Which activities and learning opportunities relate to this route?'],
    ['Was kostet mich die Weiterbildung an Zeit und Geld?','Welche Anforderungen müsste ich bei offiziellen Stellen abklären?'],
    ['What will the further training cost me in time and money?','Which requirements should I check with official sources?'],
    ['Welche Alternative passt fast so gut zu meinem Profil?','Welche verwandten Tätigkeitsfelder könnte ich kennenlernen?'],
    ['Which alternative fits my profile almost as well?','Which related fields of work could I explore?'],
    ['zur Prüfungslogik [A]/[B]/[C]','zu KI-Rollen und eigener Denkarbeit'],
    ['the [A]/[B]/[C] assessment logic','AI roles and independent reasoning'],
    ['Wähle eine Kompetenz, nenne deine Zielrolle, du erhältst genau einen CV-tauglichen Satz — formuliert auf der Stufe, die dein selbst deklarierter Spielstand angibt.','Wähle eine Kompetenz und eine Zielrolle. Mit einer eigenen Erfahrung entsteht ein vorsichtiger CV-Entwurf; ohne Erfahrung formulierst du ein zukünftiges Lernziel.'],
    ["Pick a competence, state your target role, you'll get exactly one CV-ready sentence — phrased at the level shown by your self-declared game state.",'Choose a competence and a target role. With a personal experience, you receive a cautious CV draft; without one, you formulate a future learning goal.'],
    ['Wähle einen Berufsweg und klick «Weg prüfen»: Du erhältst die Passung deines Wahlprofils, die Etappen nach dem Master (datierte Orientierung aus dem Entwurf) und einen nächsten Schritt. Mit eingefügtem Spielstand wird die Einschätzung persönlich. Danach kannst du im Chat nachfragen.','Wähle einen Berufsweg und erkunde typische Tätigkeiten, passende Lerngelegenheiten und einen nächsten Schritt. Eingefügte Modulwahlen helfen bei der Orientierung; sie belegen keine persönliche Eignung. Danach kannst du nachfragen.'],
    ["Pick a career path and click 'Check route': you'll get the fit of your elective profile, the stages after the Master's (dated guidance from the draft) and one next step. With a pasted game state the assessment becomes personal. You can ask follow-up questions in the chat afterwards.",'Choose a career path and explore typical activities, relevant learning opportunities and a next step. Imported module choices support orientation; they do not establish personal suitability. You can then ask follow-up questions.']
  ])html=html.replaceAll(before,after);
  return html;
}
