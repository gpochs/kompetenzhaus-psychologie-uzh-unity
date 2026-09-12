// Run from the repository root. Reads only the inherited public source; writes only this directory.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const window = {};
for (const name of ['daten-struktur.js', 'daten-texte.js', 'daten-karriere.js'])
  vm.runInNewContext(fs.readFileSync(path.join(root, 'js', name), 'utf8'), { window }, { timeout: 2000 });
const main = fs.readFileSync(path.join(root, 'js/main.js'), 'utf8');
const between = (start, end) => main.slice(main.indexOf(start), main.indexOf(end, main.indexOf(start)));
const context = { window, ST: window.STRUKTUR, TEXTE: window.MODUL_TEXTE, THEMEN: window.STRUKTUR.themen };
vm.runInNewContext(between('const FALLBACK =', '/* ---------- State') + between('const WAHL_KOMP =', 'function slotKomp(slot)') +
  between('function slotKompMax(slot)', 'function slotTitel(slot)') + '\nglobalThis.maximum = ST.slots.map(s=>({id:s.slot,...slotKompMax(s)})); globalThis.extras=WAHL_KOMP;', context);
const s = window.STRUKTUR;
const str = value => value == null ? 'null' : '@"' + String(value).replaceAll('"', '""') + '"';
const arr = values => 'new string[] { ' + values.map(str).join(', ') + ' }';
const nums = (values, type='double') => 'new ' + type + '[] { ' + values.join(', ') + ' }';
const local = value => 'new LocalizedText { de = ' + str(value.de) + ', en = ' + str(value.en) + ' }';
const flatten = value => [...(value.fa || []), ...(value.ki || []), ...(value.fu || [])];
const options = [];
for (const slot of s.slots) {
  if (slot.schwerpunktwahl) for (const [id, value] of Object.entries(s.schwerpunkte))
    options.push({ moduleId:slot.slot,kind:'specialisation',id,name:value.name,competencyIds:flatten(context.extras.sp[id]) });
  for (const topic of s.themen[slot.slot] || [])
    options.push({moduleId:slot.slot,kind:'topic',id:topic.id,directionId:topic.r,name:topic.name,competencyIds:flatten(context.extras.r[topic.r])});
  if (slot.slot === 'BA') for (const [topicId, questions] of Object.entries(s.baFragen)) for (const question of questions)
    options.push({moduleId:'BA',kind:'thesisQuestion',id:question.id,parentId:topicId,name:question.name,careerIds:question.pfade,competencyIds:flatten(context.extras.form[question.form])});
}
const generated = `// Generated from public js/main.js, daten-struktur.js and daten-karriere.js. Do not edit by hand.
using Kompetenzhaus.Content;
namespace Kompetenzhaus.Competencies
{
    internal static class LegacyCompetenceRules
    {
        internal static readonly ModuleProjectionRule[] Modules = {
${context.maximum.map(r=>'            new ModuleProjectionRule { moduleId = '+str(r.id)+', maximumIds = '+arr(flatten(r.komp))+', maximumPrimaryIds = '+arr(r.haupt)+' }').join(',\n')}
        };
        internal static readonly CompetenceChoiceOption[] Options = {
${options.map(o=>'            new CompetenceChoiceOption { moduleId = '+str(o.moduleId)+', kind = '+str(o.kind)+', id = '+str(o.id)+', parentId = '+str(o.parentId)+', directionId = '+str(o.directionId)+', name = '+local(o.name)+', competencyIds = '+arr(o.competencyIds)+', careerIds = '+arr(o.careerIds||[])+' }').join(',\n')}
        };
        internal static readonly PreStageCheck[] PreStageChecks = {
${s.vorstufe.map((c,i)=>'            new PreStageCheck { index = '+i+', name = '+local(c.text)+', competencyIds = '+arr(c.ids)+' }').join(',\n')}
        };
        internal static readonly CareerRule[] Careers = {
${window.KARRIERE.pfade.map(c=>'            new CareerRule { id = '+str(c.id)+', name = '+local(c.name)+', description = '+local(c.hint)+', directionId = '+str(c.wahl?.r)+', specialisationId = '+str(c.wahl?.sp)+', optionalModuleCode = '+str(c.wahl?.wp)+', weightIds = '+arr(Object.keys(c.w))+', weights = '+nums(Object.values(c.w))+', targetIds = '+arr(Object.keys(c.ziel||{}))+', targetStages = '+nums(Object.values(c.ziel||{}),'int')+', targetRadar = '+nums(c.soll||[])+' }').join(',\n')}
        };
        internal static readonly SemesterRule[] Semesters = {
${Object.entries(s.bauplan).flatMap(([house,terms])=>terms.map(t=>'            new SemesterRule { id = '+str((house==='bsc'?'B':'M')+t.sem)+', moduleIds = '+arr(t.slots)+' }')).join(',\n')}
        };
    }
}
`;
fs.writeFileSync(path.join(here, 'LegacyCompetenceRules.generated.cs'), generated);
console.log(`Generated ${context.maximum.length} module reference rules, ${options.length} choices, ${window.KARRIERE.pfade.length} career paths.`);

// Independent golden fixtures execute the original functions, not a JS reimplementation of the port.
const legacy = { window };
vm.runInNewContext('const ST=window.STRUKTUR,TEXTE=window.MODUL_TEXTE,THEMEN=ST.themen;let S;const selectedId="",pendingOpt=null;\n' +
  between('const FALLBACK =', '/* ---------- State') + between('const SLOTS =', 'const visitor =') +
  between('function themaFor(', '/* ---------- Regeln') +
  'function isPlaced(id){return !!S.placed[S.mode][id];}\n' +
  between('const ectsGewicht =', 'let profilView =') +
  between('const STUFEN_SCHWELLE =', 'function kompDetailHTML') +
  between('function wahlAnteil(p)', '/* Karriere-Ansicht */') +
  '\nglobalThis.evaluate=(placed,p0)=>{S={mode:"frei",placed:{frei:placed},p0};MAX_STUFE=null;const {score,max}=profilWerte();const pct={};ST.kompetenzen.forEach(k=>pct[k.id]=max[k.id]?score[k.id]/max[k.id]:0);return {scores:ST.kompetenzen.map(k=>score[k.id]),maxima:ST.kompetenzen.map(k=>max[k.id]),stages:ST.kompetenzen.map(k=>kompStufe(k.id)),fits:window.KARRIERE.pfade.map(p=>pfadPassung(p,pct))};};', legacy);
// Legacy placeSlot always writes pendingOpt (initially the first option); Unity stores this default implicitly.
// Map that representation before comparison, otherwise wpWahl() reads null instead of the selected default.
const all = Object.fromEntries(s.slots.map(slot=>[slot.slot,slot.optionen ? {opt:slot.optionen[0]} : {}]));
const specialised = JSON.parse(JSON.stringify(all));
for(const id of ['s04','s05','s06','s07','s08','s09'])specialised[id].sp='HEA';
for(const id of ['s11','s12','s13','BA'])specialised[id].thema=s.themen[id].find(topic=>topic.r==='klin').id;
specialised.BA.frage=s.baFragen[specialised.BA.thema][0].id;
specialised.wp.opt='06SM200-511';specialised.s01c.opt='10SMSTS-505';
const cases=[['empty',{},[]],['foundation',{'003':{}},[]],['full-default',all,[]],['clinical-mentoring',specialised,[]],['prestage-only',{},[0,1,2,3]],['foundation-and-prestage',{'001':{},'002':{},'003':{}},[0,1,2,3]]];
const fixtures=cases.map(([id,placed,checks])=>({id,placedIds:Object.keys(placed),
  choices:Object.entries(placed).filter(([,value])=>value.sp||value.thema||value.frage).map(([moduleId,value])=>({moduleId,specialisationId:value.sp||'',topicId:value.thema||'',thesisQuestionId:value.frage||''})),
  moduleChoices:Object.entries(placed).filter(([,value])=>value.opt).map(([slotId,value])=>({slotId,moduleCode:value.opt})),
  preStageChecks:checks,...legacy.evaluate(placed,[0,1,2,3].map(i=>checks.includes(i)))}));
const fixtureDirectory=path.join(here,'Tests','Fixtures');fs.mkdirSync(fixtureDirectory,{recursive:true});
fs.writeFileSync(path.join(fixtureDirectory,'legacy-projection.json'),JSON.stringify({competencyIds:s.kompetenzen.map(k=>k.id),careerIds:window.KARRIERE.pfade.map(p=>p.id),cases:fixtures},null,2)+'\n');
console.log(`Generated ${fixtures.length} independent original-source golden scenarios.`);
