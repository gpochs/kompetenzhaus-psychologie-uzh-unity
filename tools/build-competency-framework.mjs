import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {framework} from '../content/competency-framework.mjs';
import {careerProfiles} from '../content/career-profiles.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const ids=new Set(); const criteria=new Set();
const bilingual=(value,label)=>{assert.ok(value?.de?.trim(),`${label}: German missing`);assert.ok(value?.en?.trim(),`${label}: English missing`);};
for(const c of framework.competencies){
  assert.ok(!ids.has(c.id),`Duplicate competency ${c.id}`);ids.add(c.id);
  assert.ok(framework.domains.some(d=>d.id===c.domainId));
  for(const field of ['title','scope','boundary'])bilingual(c[field],`${c.id}.${field}`);
  assert.deepEqual(c.levels.map(l=>l.level),[1,2,3]);
  for(const l of c.levels)bilingual(l.description,`${c.id}.level${l.level}`);
  for(const k of c.criteria){assert.ok(!criteria.has(k.id));criteria.add(k.id);bilingual(k.text,k.id);assert.ok(k.id.startsWith(c.id+'.'));}
}
assert.equal(ids.size,16);assert.equal(criteria.size,48);
assert.equal(framework.version,'2.1.0-draft');
assert.equal(careerProfiles.frameworkVersion,framework.version);
for(const c of framework.competencies){
  for(const field of ['possibleRole','learnerResponsibility','assessmentFocus','exampleTask'])bilingual(c.aiIntegration?.[field],`${c.id}.aiIntegration.${field}`);
  assert.ok(c.aiIntegration.relatedCriterionIds.some(id=>id.startsWith(c.id+'.')),`${c.id}: missing own criterion`);
  for(const id of c.aiIntegration.relatedCriterionIds)assert.ok(criteria.has(id),`${c.id}: unknown AI criterion ${id}`);
}
for(const lens of framework.futureLenses){
  bilingual(lens.description,lens.id);
  for(const id of lens.criterionIds)assert.ok(criteria.has(id),`${lens.id}: unknown criterion ${id}`);
}
for(const row of framework.sourceCrosswalk)for(const [,targetIds] of row.mapping)for(const id of targetIds)assert.ok(ids.has(id),`${row.sourceId}: unknown competency ${id}`);
assert.equal(careerProfiles.roles.length,12);
for(const role of careerProfiles.roles){
  const seen=new Set();bilingual(role.title,role.id);assert.ok(role.sourceUrl.startsWith('https://'));
  for(const f of role.facets){bilingual(f.title,f.id);bilingual(f.activity,f.id);for(const id of f.criterionIds){assert.ok(criteria.has(id),`Unknown criterion ${id}`);assert.ok(!seen.has(id),`Double counted criterion ${role.id}/${id}`);seen.add(id);}}
}
for(const row of framework.legacyCrosswalk)for(const id of row.competencyIds)assert.ok(ids.has(id));
assert.equal(framework.spiralExample.isModule,false);
assert.deepEqual(framework.spiralExample.occasions.map(x=>x.level),[1,2,3]);
for(const [name,data] of [['competency-framework',framework],['career-profiles',careerProfiles]]){
  const target=path.join(root,'content',name+'.json');const expected=JSON.stringify(data,null,2)+'\n';
  if(process.argv.includes('--check'))assert.equal(fs.readFileSync(target,'utf8'),expected,`Regenerate ${name}`);
  else fs.writeFileSync(target,expected);
}
console.log('Validated 16 competencies with explicit AI roles, 48 uniquely owned criteria, three task levels and 12 career views without duplicate criteria within each view.');
