// Display the host's proposed learning opportunities. No competence assessment,
// curriculum projection, or recommendation ranking is performed in this view.
export function frameworkProfile(snapshot) {
  const value = snapshot?.frameworkProfile;
  return value?.schemaVersion === 2 && value.assessmentStatus === 'unassessed' &&
    value.hasAssessedEvidence === false && Array.isArray(value.competencies) &&
    Array.isArray(value.careers) ? value : null;
}

// References reuse the host's canonical objects. Neither this map nor its
// learning phases create criteria, inferred attainment, or additional scores.
export function aiCurriculum(profile) {
  const value = profile?.aiAcrossCurriculum;
  if (value?.schemaVersion !== 1 || value.status !== 'design-proposal' || !Array.isArray(value.domainRows)) return null;
  const domains = new Set((profile.domains || []).map(item => item.id));
  const competencies = new Map((profile.competencies || []).map(item => [item.id, item]));
  const criteria = new Set([...competencies.values()].flatMap(item => (item.criteria || []).map(criterion => criterion.id)));
  const validCriteria = ids => Array.isArray(ids) && ids.every(id => criteria.has(id));
  if (value.domainRows.length !== domains.size || new Set(value.domainRows.map(item => item.domainId)).size !== domains.size) return null;
  for (const row of value.domainRows) if (!domains.has(row.domainId) || !Array.isArray(row.competencyIds) ||
      !row.competencyIds.length || row.competencyIds.some(id => competencies.get(id)?.domainId !== row.domainId) || !validCriteria(row.criterionIds)) return null;
  if (new Set(value.domainRows.flatMap(row => row.competencyIds)).size !== competencies.size) return null;
  if (!Array.isArray(value.flowEdges) || value.flowEdges.some(edge => !domains.has(edge.fromDomainId) || !domains.has(edge.toDomainId) || !validCriteria(edge.criterionIds))) return null;
  const phases = value.phasePolicy?.phases;
  if (!Array.isArray(phases) || phases.length !== 3 || ['without-ai', 'with-ai', 'about-ai'].some(id => !phases.some(phase => phase.contextId === id))) return null;
  if (!Array.isArray(value.examples) || value.examples.some(example => !validCriteria(example.criterionIds))) return null;
  return value;
}

export function moduleAiPhases(proposal) {
  const plan = proposal?.aiStudyAssessment;
  if (plan?.status !== 'design-proposal' || !Array.isArray(proposal.objectives)) return [];
  const objectives = new Map(proposal.objectives.map(item => [item.id, item]));
  const phases = [['without-ai', 'withoutAiObjectiveIds'], ['with-ai', 'withAiObjectiveIds'], ['about-ai', 'aboutAiObjectiveIds']];
  if (phases.some(([context, field]) => !Array.isArray(plan[field]) || plan[field].some(id => !objectives.get(id)?.contextIds?.includes(context)))) return [];
  return phases.map(([contextId, field]) => ({ contextId, objectives: [...new Set(plan[field])].map(id => objectives.get(id)) }));
}

export function moduleLearningProposal(design, slotId, selectedCode, frameworkVersion) {
  if (design?.schema !== 'kompetenzhaus.module-learning-design' || design.schemaVersion !== 1 ||
      design.status !== 'design-proposal' || design.frameworkVersion !== frameworkVersion || !Array.isArray(design.modules)) return null;
  // A default elective can share its code with the slot (wp / 510). The option
  // replaces that slot's general planning objectives, matching the host resolver.
  const entry = design.modules.find(item => item.code === selectedCode && item.kind === 'option' && item.parentSlotIds?.includes(slotId)) ||
    design.modules.find(item => item.code === selectedCode && item.kind === 'slot' && item.id === slotId);
  return entry?.proposal?.status === 'design-proposal' && Array.isArray(entry.proposal.objectives) ? entry.proposal : null;
}

export function matchesContext(actual = [], required = []) {
  return !required.length || actual.some(id => required.includes(id));
}

export function visibleCriteria(competency, lens = null) {
  return (competency.criteria || []).filter(criterion => !lens || lens.criterionIds.includes(criterion.id));
}

export function visibleOpportunities(criterion, lens = null) {
  return (criterion.opportunities || []).filter(item => !lens || matchesContext(item.contextIds, lens.contextIds));
}

const unique = values => [...new Set(values)];
const roman = level => ({ 1: 'I', 2: 'II', 3: 'III' })[level] || '?';
const create = (tag, text, className) => { const el = document.createElement(tag); if (text != null) el.textContent = String(text); if (className) el.className = className; return el; };

export function createFrameworkView({ onModule, moduleTitle = id => id }) {
  let snapshot, profile, lensId = '', roleId = '', aiDomainId = '';
  const $ = id => document.getElementById(id);
  const t = (de, en) => snapshot?.language === 'en' ? en : de;
  const names = ids => ids.map(id => profile.contexts?.find(item => item.id === id)?.title || id).join(' · ');
  const levelsText = levels => levels.length ? `${t('Aufgabenniveau', 'Task level')} ${unique(levels).sort().map(roman).join(', ')}` : t('Noch keine Lerngelegenheit im Haus', 'No learning opportunity in the house yet');
  const opened = root => new Set([...root.querySelectorAll('details[open][data-view-key]')].map(item => item.dataset.viewKey));
  const disclosure = (title, key, saved, className = '') => { const el = create('details', null, className); el.dataset.viewKey = key; el.open = saved.has(key); el.append(create('summary', title)); return el; };
  const moduleButton = (id, title) => { const button = create('button', title, 'button subtle'); button.type = 'button'; button.addEventListener('click', () => onModule(id)); return button; };
  function labelled(parent, title, text) {
    if (!text) return;
    const line = create('div', null, 'opportunity-line'); line.append(create('dt', title), create('dd', text)); parent.append(line);
  }
  function proposalNotes(item, parent) {
    const notes = [];
    if (item.selectionStatus === 'default-option-unconfirmed') notes.push(t('Vorausgewählte Variante; Wahl noch nicht bestätigt.', 'Default option; your choice is not confirmed yet.'));
    if (item.topicRequired) notes.push(t('Das Thema ist noch abzustimmen.', 'The topic still needs to be agreed.'));
    if (item.individualisationRequired) notes.push(t('Die Aufgabe ist auf den gewählten Kontext abzustimmen.', 'The task needs to be adapted to the chosen context.'));
    if (item.openDecisions) notes.push(item.openDecisions);
    if (notes.length) parent.append(create('p', notes.join(' '), 'proposal-note'));
  }
  function opportunity(item, saved, prefix) {
    const detail = disclosure(`${item.moduleTitle} · ${t('Aufgabe', 'Task')} ${roman(item.targetLevel)}`, `${prefix}:${item.moduleId}:${item.objectiveId}`, saved, 'opportunity-detail');
    detail.append(create('p', item.text));
    const descriptions = create('dl', null, 'opportunity-description');
    labelled(descriptions, t('Vorgeschlagene Aufgabe', 'Proposed task'), item.task);
    labelled(descriptions, t('Vorgeschlagener Nachweis', 'Proposed evidence'), item.evidence);
    labelled(descriptions, t('Vorgeschlagenes Beurteilungskriterium', 'Proposed success criterion'), item.successCriteria);
    const milestone = profile.studyMilestones?.find(stage => stage.id === item.studyMilestoneId);
    labelled(descriptions, t('Studienabschnitt', 'Study milestone'), milestone?.title || item.studyMilestoneId);
    labelled(descriptions, t('Kontext', 'Context'), names(item.contextIds || []));
    detail.append(descriptions);
    proposalNotes(item, detail);
    detail.append(create('p', t('Entwurf einer Lerngelegenheit · Leistung nicht beurteilt', 'Proposed learning opportunity · performance unassessed'), 'assessment-note'));
    detail.append(moduleButton(item.moduleId, t('Modul und Lerncheck ansehen', 'View module and learning check')));
    return detail;
  }
  function criterionCard(criterion, lens, saved, prefix) {
    const section = create('section', null, 'criterion-card'); section.dataset.criterionId = criterion.id;
    if (prefix === 'profile') section.id = `profile-criterion-${criterion.id}`;
    const heading = create('h5'); heading.append(create('span', criterion.id, 'criterion-code'), document.createTextNode(' ' + criterion.title)); section.append(heading);
    const opportunities = visibleOpportunities(criterion, lens);
    section.append(create('p', levelsText(opportunities.map(item => item.targetLevel)), 'criterion-status'));
    if (!opportunities.length) section.append(create('p', t('Dazu ist in dieser Ansicht noch keine Lerngelegenheit aus einem gebauten Modul eingetragen. Deine Fähigkeit bleibt unbeurteilt.', 'This view has no learning opportunity from a built module for this criterion yet. Your ability remains unassessed.'), 'field-help'));
    for (const item of opportunities) section.append(opportunity(item, saved, `${prefix}:${criterion.id}`));
    return section;
  }
  function nextModules(items, label) {
    const section = create('section', null, 'next-learning'); section.append(create('h4', label));
    if (!items?.length) { section.append(create('p', t('Für diese Tätigkeit nennt der aktuelle Modulplan keine weiteren passenden Bausteine.', 'The current module plan lists no further building blocks for this activity.'), 'field-help')); return section; }
    for (const item of items) {
      const card = create('article', null, 'next-module');
      card.append(create('h5', item.title), create('p', `${item.moduleCode} · ${levelsText(item.opportunityLevels || [])}`, 'field-help'));
      const reasons = [];
      if (item.needsMasterEntry) reasons.push(t('Masterzugang im Spiel klären', 'Resolve Master entry in the game'));
      if (item.missingPrerequisiteIds?.length) reasons.push(`${t('Bausteine vorausgesetzt', 'Required building blocks')}: ${item.missingPrerequisiteIds.join(', ')}`);
      if (item.needsSelfCheck) reasons.push(t('Selbstauskunft im Modul offen', 'Module self-report pending'));
      if (item.needsQuiz) reasons.push(t('Lerncheck im Spiel offen', 'In-game learning check pending'));
      card.append(create('p', item.canBuild ? t('Im Spiel zum Bauen freigegeben', 'Available to build in the game') : reasons.join(' · ') || t('Im Spiel noch nicht zum Bauen freigegeben', 'Not yet available to build in the game'), 'build-readiness'));
      card.append(create('p', `${t('Bezug zu Kriterien', 'Related criteria')}: ${(item.criterionIds || []).join(', ')}`, 'field-help'));
      proposalNotes(item, card);
      card.append(moduleButton(item.moduleId, t('Modul und Lerncheck ansehen', 'View module and learning check')));
      section.append(card);
    }
    return section;
  }
  function canonicalLink(id, target = 'criterion') {
    const competency = profile.competencies.find(item => target === 'ai' ? item.id === id : item.criteria?.some(criterion => criterion.id === id));
    if (!competency) return null;
    const criterion = competency.criteria?.find(item => item.id === id);
    const button = create('button', `${id} · ${target === 'ai' ? competency.title : criterion.title}`, 'canonical-link'); button.type = 'button';
    button.dataset[target === 'ai' ? 'aiCompetencyLink' : 'aiCriterionLink'] = id;
    button.setAttribute('aria-controls', target === 'ai' ? `profile-ai-${id}` : `profile-criterion-${id}`);
    button.addEventListener('click', () => {
      // A lens must not hide the destination selected in the curriculum map.
      lensId = ''; renderProfile();
      const detail = [...$('competency-list').querySelectorAll('[data-competency-id]')].find(item => item.dataset.competencyId === competency.id);
      if (!detail) return;
      detail.open = true;
      const destination = target === 'ai' ? detail.querySelector('.competency-ai') : [...detail.querySelectorAll('[data-criterion-id]')].find(item => item.dataset.criterionId === id);
      if (destination) { destination.tabIndex = -1; destination.focus({ preventScroll: true }); destination.scrollIntoView({ block: 'start', behavior: 'instant' }); }
    });
    return button;
  }
  function addCanonicalLinks(parent, ids, target = 'criterion') {
    const links = create('div', null, 'canonical-links');
    for (const id of unique(ids || [])) { const button = canonicalLink(id, target); if (button) links.append(button); }
    if (links.childNodes.length) parent.append(links);
  }
  function renderAiCurriculum(saved) {
    const map = aiCurriculum(profile);
    if (!map) return create('p', t('Die Übersicht zur KI im gesamten Curriculum ist für diese Spielverbindung noch nicht verfügbar.', 'The overview of AI across the curriculum is not available for this game connection yet.'), 'field-help');
    const section = disclosure(map.title, 'ai-across-curriculum', saved, 'framework-guide ai-curriculum'); section.id = 'ai-curriculum';
    section.append(create('p', map.principle), create('p', t('Wähle einen Bereich. Die Verbindungen führen zu seinen Kriterien, KI-Karten und Bezügen zu anderen Bereichen.', 'Choose a domain. The connections lead to its criteria, AI cards and links with other domains.'), 'ai-map-intro'));
    const choices = create('div', null, 'ai-domain-choices'); choices.setAttribute('role', 'group'); choices.setAttribute('aria-label', t('KI in einem Kompetenzbereich erkunden', 'Explore AI in a competency domain'));
    const panel = create('section', null, 'ai-domain-panel'); panel.id = 'ai-domain-panel'; panel.setAttribute('aria-labelledby', 'ai-domain-title');
    if (!map.domainRows.some(row => row.domainId === aiDomainId)) aiDomainId = map.domainRows[0]?.domainId || '';
    const domainTitle = id => profile.domains.find(domain => domain.id === id)?.title || id;
    const roles = { tool: t('Werkzeug', 'Tool'), subject: t('Gegenstand', 'Subject'), context: t('Kontext', 'Context'), 'deliberate-nonuse': t('Bewusster Verzicht', 'Deliberate non-use') };
    function showDomain() {
      const row = map.domainRows.find(item => item.domainId === aiDomainId); panel.replaceChildren();
      for (const button of choices.children) button.setAttribute('aria-pressed', String(button.dataset.aiDomain === aiDomainId));
      if (!row) return;
      const title = create('h4', `${row.domainId} · ${domainTitle(row.domainId)}`); title.id = 'ai-domain-title'; panel.append(title, create('p', row.summary));
      panel.append(create('p', (row.aiRoles || []).map(role => roles[role]).filter(Boolean).join(' · '), 'ai-role-labels'));
      const descriptions = create('dl', null, 'opportunity-description');
      labelled(descriptions, t('Was bei dir bleibt', 'What remains your responsibility'), row.learnerResponsibility);
      labelled(descriptions, t('Woran ein Nachweis ansetzen kann', 'What evidence can address'), row.evidenceFocus); panel.append(descriptions);
      const criteria = create('details', null, 'framework-guide'); criteria.append(create('summary', t('Bestehende Kriterien öffnen', 'Open existing criteria'))); addCanonicalLinks(criteria, row.criterionIds); panel.append(criteria);
      const cards = create('details', null, 'framework-guide'); cards.append(create('summary', t('KI-Karten dieses Bereichs öffnen', 'Open this domain’s AI cards'))); addCanonicalLinks(cards, row.competencyIds, 'ai'); panel.append(cards);
      const edges = map.flowEdges.filter(edge => edge.fromDomainId === row.domainId || edge.toDomainId === row.domainId);
      if (edges.length) {
        const connections = create('details', null, 'framework-guide'); connections.append(create('summary', t('Wie die Bereiche zusammenarbeiten', 'How the domains work together')), create('p', map.flowRule));
        for (const edge of edges) { const connection = create('div', null, 'ai-flow-edge'); connection.append(create('p', `${domainTitle(edge.fromDomainId)} → ${domainTitle(edge.toDomainId)}`, 'ai-flow-path'), create('p', edge.label)); addCanonicalLinks(connection, edge.criterionIds); connections.append(connection); } panel.append(connections);
      }
    }
    for (const row of map.domainRows) {
      const button = create('button', null, 'ai-domain-choice'); button.type = 'button'; button.dataset.aiDomain = row.domainId; button.setAttribute('aria-controls', panel.id);
      button.append(create('span', row.domainId, 'ai-domain-code'), create('span', domainTitle(row.domainId)));
      button.addEventListener('click', () => { aiDomainId = row.domainId; showDomain(); }); choices.append(button);
    }
    showDomain(); section.append(choices, panel);
    const phases = disclosure(t('Ohne, mit und über KI lernen', 'Learn without, with and about AI'), 'ai-learning-phases', saved, 'framework-guide');
    const phaseGrid = create('div', null, 'ai-phase-grid');
    for (const phase of map.phasePolicy.phases) {
      const card = create('section', null, 'ai-phase-card'); card.dataset.aiPhase = phase.contextId; card.append(create('h4', phase.title), create('p', phase.purpose));
      const descriptions = create('dl', null, 'opportunity-description'); labelled(descriptions, t('Deine Tätigkeit', 'Your activity'), phase.learnerAction); labelled(descriptions, t('Beurteilung im Entwurf', 'Proposed assessment'), phase.assessmentUse); card.append(descriptions); phaseGrid.append(card);
    }
    phases.append(phaseGrid);
    for (const rule of map.phasePolicy.assessmentRules || []) phases.append(create('p', rule.text, 'assessment-note'));
    if (map.phasePolicy.noWholeModuleMandate) phases.append(create('p', t('Die Phasen begründen keine pauschale KI-Pflicht oder ein KI-Verbot für ein ganzes Modul. Die konkrete Aufgabe und ihre Regeln entscheiden.', 'These phases imply neither an AI requirement nor an AI ban for an entire module. The specific task and its rules determine use.'), 'proposal-note'));
    section.append(phases);
    const examples = disclosure(t('Beispiele für verbundene Lernphasen', 'Examples of connected learning phases'), 'ai-phase-examples', saved, 'framework-guide');
    for (const example of map.examples) {
      const card = disclosure(example.title, `ai-example:${example.id}`, saved, 'ai-phase-example'); card.dataset.aiExample = example.id;
      for (const phase of example.phases || []) { const heading = map.phasePolicy.phases.find(item => item.contextId === phase.contextId)?.title || phase.contextId; card.append(create('h5', heading), create('p', phase.activity)); const description = create('dl', null, 'opportunity-description'); labelled(description, t('Vorgeschlagener Nachweis', 'Proposed evidence'), phase.evidence); card.append(description); }
      card.append(create('p', example.assessmentProposal, 'proposal-note')); addCanonicalLinks(card, example.criterionIds);
      for (const id of example.moduleIds || []) if (snapshot.moduleStates?.some(item => item.id === id)) card.append(moduleButton(id, t('Modul ansehen: ', 'View module: ') + moduleTitle(id)));
      examples.append(card);
    }
    section.append(examples);
    const dimensions = disclosure(t('Wissen, Können, Haltungen und Werte', 'Knowledge, skills, attitudes and values'), 'ai-dimensions', saved, 'framework-guide');
    for (const dimension of map.dimensions || []) dimensions.append(create('h4', dimension.title), create('p', dimension.description));
    section.append(dimensions, create('p', map.assessmentNotice, 'assessment-note'));
    return section;
  }
  function renderProfile() {
    const overview = $('profile-overview'), list = $('competency-list');
    const saved = new Set([...opened(overview), ...opened(list)]);
    overview.replaceChildren(); list.replaceChildren();
    if (!profile) {
      overview.append(create('p', snapshot?.ready ? t('Das neue Kompetenzprofil ist für diese Spielverbindung noch nicht verfügbar.', 'The new competency profile is not available for this game connection yet.') : t('Das Kompetenzprofil wird geladen.', 'The competency profile is loading.'), 'profile-empty'));
      return;
    }
    const heading = create('div', null, 'framework-heading'); heading.append(create('span', t('Kompetenzen nicht beurteilt', 'Competencies unassessed'), 'unassessed-pill'), create('small', profile.frameworkVersion)); overview.append(heading);
    overview.append(create('p', profile.opportunityNotice, 'framework-intro'));
    const summary = create('div', null, 'opportunity-summary');
    for (const [count, label] of [[unique(snapshot.placedModuleIds || []).length, t('Module gebaut', 'modules built')], [profile.gamePractice?.modules?.length || 0, t('Modulchecks geübt', 'module checks practised')], [profile.gamePractice?.questIds?.length || 0, t('Quests abgeschlossen', 'quests completed')]]) {
      const card = create('div'); card.append(create('strong', count), create('span', label)); summary.append(card);
    }
    overview.append(summary, create('p', profile.gamePracticeNotice, 'field-help'));
    overview.append(renderAiCurriculum(saved));
    const filter = create('label', null, 'field lens-filter'); filter.append(create('span', t('Perspektive: Future Skills und KI', 'Perspective: Future Skills and AI')));
    const select = create('select'); select.id = 'framework-lens'; select.append(new Option(t('Alle Kompetenzen', 'All competencies'), ''));
    for (const lens of profile.futureLenses || []) select.append(new Option(lens.title, lens.id));
    if (!profile.futureLenses?.some(item => item.id === lensId)) lensId = '';
    select.value = lensId; select.addEventListener('change', () => { lensId = select.value; renderProfile(); $('framework-lens').focus({ preventScroll: true }); }); filter.append(select); overview.append(filter);
    const lens = profile.futureLenses?.find(item => item.id === lensId) || null;
    const lensNote = create('div', null, 'lens-note');
    if (lens) lensNote.append(create('h3', lens.title), create('p', lens.description), create('p', `${t('Kontexte', 'Contexts')}: ${names(lens.contextIds || [])}`, 'field-help'));
    lensNote.append(create('p', t('Die Perspektive filtert dieselben Kriterien und ihre Lerngelegenheiten. Es entstehen keine zusätzlichen Punkte oder Kompetenzgruppen.', 'This perspective filters the same criteria and learning opportunities. It adds no points or competency groups.'))); overview.append(lensNote);
    const levelGuide = disclosure(t('Was bedeuten die Aufgabenniveaus I, II und III?', 'What do task levels I, II and III mean?'), 'level-guide', saved, 'framework-guide');
    for (const level of profile.levels || []) levelGuide.append(create('h4', `${level.label || roman(level.level)} · ${level.title}`), create('p', level.description));
    levelGuide.append(create('p', profile.milestoneRule, 'assessment-note')); overview.append(levelGuide);
    const frameworkNote = disclosure(t('Modell und Grenzen', 'Framework and limitations'), 'framework-note', saved, 'framework-guide');
    frameworkNote.append(create('p', profile.status), create('p', profile.limitation), create('p', profile.futureCoverageRule));
    if (profile.futureDesignTests?.length) { const tests = create('ul'); for (const text of profile.futureDesignTests) tests.append(create('li', text)); frameworkNote.append(tests); } overview.append(frameworkNote);
    let count = 0;
    for (const domain of profile.domains || []) {
      const competencies = profile.competencies.filter(item => item.domainId === domain.id && visibleCriteria(item, lens).length);
      if (!competencies.length) continue;
      list.append(create('h3', domain.title, 'list-heading'));
      for (const competency of competencies) {
        count++;
        const detail = disclosure('', `competency:${competency.id}`, saved, 'competence-detail'); detail.dataset.competencyId = competency.id;
        const criteria = visibleCriteria(competency, lens);
        const levels = unique(criteria.flatMap(item => visibleOpportunities(item, lens).map(item => item.targetLevel)));
        detail.firstChild.append(create('span', `${competency.id} · ${competency.title}`), create('span', levels.length ? `${t('Aufgaben', 'Tasks')} ${levels.sort().map(roman).join(', ')}` : t('Offene Planung', 'Planning open'), 'stage-pill'));
        detail.append(create('p', competency.scope));
        const scope = disclosure(t('Abgrenzung und Niveaubeschreibungen', 'Scope boundaries and level descriptions'), `scope:${competency.id}`, saved, 'framework-guide'); scope.append(create('p', competency.boundary));
        for (const level of competency.levelDescriptions || []) scope.append(create('p', `${roman(level.level)} · ${level.description}`)); detail.append(scope);
        if (competency.aiIntegration) {
          const integration = competency.aiIntegration;
          const ai = create('section', null, 'competency-ai'); ai.id = `profile-ai-${competency.id}`; ai.append(create('h4', t('KI in diesem Kompetenzbereich', 'AI in this competency area')));
          const descriptions = create('dl', null, 'opportunity-description');
          labelled(descriptions, t('Mögliche Rolle der KI', 'Possible role of AI'), integration.possibleRole);
          labelled(descriptions, t('Deine Eigenleistung', 'Your own contribution'), integration.learnerResponsibility);
          labelled(descriptions, t('Vorgeschlagener Prüffokus', 'Proposed assessment focus'), integration.assessmentFocus);
          labelled(descriptions, t('Beispielaufgabe', 'Example task'), integration.exampleTask);
          ai.append(descriptions);
          if (integration.relatedCriterionIds?.length) ai.append(create('p', `${t('Bezug zu denselben Kriterien', 'Links to the same criteria')}: ${integration.relatedCriterionIds.join(', ')}`, 'field-help'));
          detail.append(ai);
        }
        for (const criterion of criteria) detail.append(criterionCard(criterion, lens, saved, 'profile'));
        list.append(detail);
      }
    }
    const resultCount = create('p', `${count} ${t('Kompetenzen in dieser Ansicht', 'competencies in this view')}`, 'field-help'); resultCount.setAttribute('role', 'status'); list.prepend(resultCount);
  }
  function renderCompass() {
    const result = $('compass-result'), saved = opened(result); result.replaceChildren();
    const select = $('career-select');
    select.replaceChildren(); select.disabled = !profile;
    if (!profile) { result.append(create('p', t('Der Berufskompass erscheint mit dem neuen Kompetenzprofil.', 'The career compass will appear with the new competency profile.'), 'profile-empty')); return; }
    for (const role of profile.careers) select.append(new Option(role.title, role.id));
    if (!profile.careers.some(item => item.id === roleId)) roleId = profile.careers[0]?.id || '';
    select.value = roleId;
    const role = profile.careers.find(item => item.id === roleId);
    if (!role) return;
    result.append(create('p', profile.careerNotice, 'career-notice'), create('h3', role.title));
    const facets = create('div', null, 'career-facets');
    for (const facet of role.facets || []) {
      const card = create('article', null, `career-facet${facet.optionalContext ? ' optional-context' : ''}`);
      if (facet.optionalContext) card.append(create('span', t('Optionaler digitaler Kontext', 'Optional digital context'), 'optional-pill'));
      card.append(create('h4', facet.title), create('p', facet.activity));
      if (facet.optionalContext) card.append(create('p', t('Du kannst diese Tätigkeit auch ohne KI planen. Diese Ansicht zeigt zusätzlich passende technische Lerngelegenheiten.', 'You can plan this activity without AI. This view also shows related technical learning opportunities.'), 'field-help'));
      const ids = new Set(facet.criterionIds || []);
      const criteria = profile.competencies.flatMap(item => item.criteria || []).filter(item => ids.has(item.id));
      const planned = facet.plannedCriterionIds || [];
      card.append(create('p', planned.length ? `${t('Gelegenheiten im Haus für', 'Opportunities in the house for')}: ${planned.join(', ')}` : t('Für diese Tätigkeit ist noch keine passende Lerngelegenheit im Haus eingetragen.', 'No matching learning opportunity is recorded in the house for this activity yet.'), 'criterion-status'));
      const detail = disclosure(t('Kriterien und geplante Aufgaben ansehen', 'View criteria and planned tasks'), `facet:${role.id}:${facet.id}`, saved, 'facet-detail');
      for (const criterion of criteria) detail.append(criterionCard(criterion, { contextIds: facet.contextIds || [] }, saved, `career:${role.id}:${facet.id}`));
      card.append(detail);
      if (facet.nextModules?.length) {
        const next = disclosure(`${t('Passende nächste Module', 'Related next modules')} (${facet.nextModules.length})`, `facet-next:${role.id}:${facet.id}`, saved, 'facet-detail');
        next.append(nextModules(facet.nextModules, t('Aus deinem aktuellen Modulplan', 'From your current module plan'))); card.append(next);
      } else card.append(create('p', t('Im aktuellen Plan sind dazu keine weiteren Module vorgeschlagen.', 'No further modules are proposed for this activity in the current plan.'), 'field-help'));
      facets.append(card);
    }
    result.append(facets);
    if (role.nextModules?.length) {
      const next = disclosure(t('Nächste Module für dieses Berufsfeld', 'Next modules for this field'), `role-next:${role.id}`, saved, 'framework-guide'); next.append(nextModules(role.nextModules, t('Aus deinem aktuellen Modulplan', 'From your current module plan'))); result.append(next);
    }
    try { const url = new URL(role.sourceUrl); if (url.protocol === 'https:') { const link = create('a', t('Quelle zur beruflichen Orientierung', 'Source for professional orientation')); link.href = url.href; link.target = '_blank'; link.rel = 'noopener noreferrer'; result.append(link, create('p', t('Die Tätigkeitsansicht ist ein Entwurf für das Spiel, kein offizieller Berufsstandard.', 'This activity view is a game design proposal, not an official professional standard.'), 'field-help')); } } catch { /* A missing source is not replaced with an invented link. */ }
  }
  $('career-select').addEventListener('change', event => { roleId = event.target.value; renderCompass(); $('career-select').focus({ preventScroll: true }); });
  return {
    render(next) {
      snapshot = next; profile = frameworkProfile(next);
      for (const [id, de, en] of [
        ['study-kicker', 'Dein Lernweg im Überblick', 'Your learning path'], ['study-title', 'Was dein Haus verbindet.', 'What your house connects.'],
        ['study-intro', 'Module, geplante Lerngelegenheiten und berufliche Tätigkeiten.', 'Modules, planned learning opportunities and professional activities.'],
        ['study-tab-modules', 'Module', 'Modules'], ['study-tab-profile', 'Kompetenzprofil', 'Competency profile'], ['study-tab-compass', 'Berufskompass', 'Career compass'],
        ['profile-intro', profile ? `Erkunde, welche Aufgaben deine gebauten Module für die ${profile.competencies.length} Kompetenzen vorsehen.` : 'Erkunde die Lerngelegenheiten deiner gebauten Module.', profile ? `Explore the tasks your built modules propose for the ${profile.competencies.length} competencies.` : 'Explore the learning opportunities in your built modules.'],
        ['career-intro', 'Wähle ein Berufsfeld. Die Tätigkeitskarten verbinden typische Aufgaben mit Kriterien und deinem Modulplan.', 'Choose a field. The activity cards connect typical work to criteria and your module plan.'],
        ['career-label', 'Berufsfeld erkunden', 'Explore a professional field']
      ]) if ($(id)) $(id).textContent = t(de, en);
      renderProfile(); renderCompass();
    }
  };
}
