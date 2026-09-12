// Display the host's proposed learning opportunities. No competence assessment,
// curriculum projection, or recommendation ranking is performed in this view.
export function frameworkProfile(snapshot) {
  const value = snapshot?.frameworkProfile;
  return value?.schemaVersion === 2 && value.assessmentStatus === 'unassessed' &&
    value.hasAssessedEvidence === false && Array.isArray(value.competencies) &&
    Array.isArray(value.careers) ? value : null;
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

export function createFrameworkView({ onModule }) {
  let snapshot, profile, lensId = '', roleId = '';
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
          const ai = create('section', null, 'competency-ai'); ai.append(create('h4', t('KI in diesem Kompetenzbereich', 'AI in this competency area')));
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
