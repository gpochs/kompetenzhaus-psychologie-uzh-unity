/* Bound to the actual practice UI: role labels inside text never create evidence. */
let practiceTurns = [], feedbackTurns = [], validatedFeedbackReport = null;
let vorabAnalyse = null, feedbackDisplayedSession = -1;
function recordPracticeTurn(role, text) {
  practiceTurns.push({ id: 'turn-' + (practiceTurns.length + 1), role, text });
}
function resetPracticeEvidence() {
  practiceTurns = []; feedbackTurns = []; validatedFeedbackReport = null;
  vorabAnalyse = null; feedbackDisplayedSession = -1;
}
function rollenEnde() {
  const v = vignAktiv;
  if (!v) return;
  gespraechsVerlauf = hist.slice();
  feedbackTurns = practiceTurns.map(turn => ({ ...turn }));
  validatedFeedbackReport = null;
  feedbackDisplayedSession = -1;
  vignAktiv = null; hist = [];
  uebungsbannerSetzen(false);
  // A short scene never triggers a host request, including this optional prefetch.
  vorabAnalyse = analyseHolen(v);
  if (vorabAnalyse) vorabAnalyse.catch(() => {});
  selbsteinschaetzungZeigen(v);
}
function feedbackLanguage() { return lang === 'de' ? 'Deutsch mit Schweizer Rechtschreibung (ss statt ß)' : 'English'; }
function feedbackQuote(reference) { return '«' + reference.quote + '» [' + reference.turnId + ']'; }
function renderValidatedFeedback(report) {
  return report.points.map(point => [
    '**' + T('Beobachtung', 'Observation') + ':** ' + point.observation,
    T('Dein Wortlaut: ', 'Your words: ') + point.studentEvidence.map(feedbackQuote).join('; '),
    '**' + T('Wirkung', 'Effect') + ':** ' + (point.effect.kind === 'possible'
      ? T('Mögliche Wirkung, im Gespräch nicht nachgewiesen: ', 'Possible effect, not demonstrated in this conversation: ') + point.effect.text
      : point.effect.kind === 'response'
        ? T('Darauf folgende fiktive Figurenantwort (kein Wirkungsnachweis): ', 'Following fictional character response (not evidence of an effect): ') + feedbackQuote(point.effect.responseEvidence)
        : T('Im vorhandenen Text nicht beobachtbar.', 'Not observable in the available text.')),
    '**' + T('Alternative für einen nächsten Versuch', 'Alternative for another attempt') + ':** ' + point.alternative
  ].join('\n\n')).join('\n\n---\n\n') + '\n\n**' + T('Fokus für den nächsten Durchgang', 'Focus for the next round') + ':** ' + report.nextFocus
    + '\n\n**' + T('Frage zum Weiterdenken', 'Question to reflect on') + ':** ' + report.curiosityQuestion;
}
function analyseHolen(v) {
  const evidence = collectFeedbackEvidence(feedbackTurns);
  if (!api || !evidence.canGenerate) return null;
  const requestSession = sessionNonce;
  const dims = VIGN_DIMS[v.id] || [];
  const fok = fokusDim ? (lang === 'de' ? fokusDim.de : fokusDim.en) : null;
  return ask(`FEEDBACK_JSON
Du bist Supervisor:in in der psychologischen Ausbildung. Gib eng begrenzte Übungsrückmeldung zum fiktiven Rollenspiel, keine Gesamtbeurteilung.
SZENARIO: ${lang === 'de' ? v.info.de : v.info.en}
Bekannte Beobachtungsdimensionen, ausschliesslich diese: ${dims.map(d => (lang === 'de' ? d.de : d.en) + ' — ' + (lang === 'de' ? d.was.de : d.was.en)).join('; ')}
${fok ? 'Selbst gewählter Schwerpunkt, beginne damit: ' + fok : ''}
${feedbackEvidencePrompt(evidence)}
Antworte ausschliesslich als JSON-Objekt, ohne Einleitung oder Markdown:
{"points":[{"observation":"nur eine tatsächlich beobachtete Handlung","studentEvidence":[{"turnId":"echte Studententurn-ID","quote":"wörtlicher Textausschnitt"}],"effect":{"kind":"possible","text":"ausdrücklich hypothetische Wirkung"},"alternative":"direkt nachsprechbarer Satz als Vorschlag"}],"nextFocus":"ein nächster Übungsfokus","curiosityQuestion":"offene Reflexionsfrage ohne unterstellte Handlung"}
points enthält 1 bis höchstens 3 Punkte, keine Bewertungen oder Noten. effect kann stattdessen {"kind":"response","responseEvidence":{"turnId":"unmittelbar folgende Figurenantwort-ID","quote":"wörtlicher Ausschnitt"}} oder {"kind":"not-observed"} sein. response enthält keine Deutung. Wenn die Wirkung nicht belegt oder sicher als Möglichkeit beschreibbar ist, verwende not-observed.
Keine Aussagen über Fähigkeiten, Berufseignung, Gefühle der studierenden Person, Stimme, Mimik, Tonfall, Tempo oder Nervosität. Keine reale Diagnostik. Keine Unterlassungsbehauptung am Gesprächsende. Nichts erfinden, um drei Punkte zu füllen. Höchstens 200 Wörter in den Textfeldern. Sprache: ${feedbackLanguage()}.`).then(raw => {
    if (requestSession !== sessionNonce) throw new Error('Practice session changed.');
    const report = validateFeedbackResponse(raw, evidence);
    validatedFeedbackReport = report;
    return renderValidatedFeedback(report);
  });
}
async function comparisonHolen(selbst) {
  const evidence = collectFeedbackEvidence(feedbackTurns), report = validatedFeedbackReport;
  if (!evidence.canGenerate || !report) throw new Error('No validated feedback available.');
  const requestSession = sessionNonce;
  const raw = await ask(`FEEDBACK_COMPARISON_JSON
Vergleiche ausschliesslich die folgende Selbsteinschätzung mit der bereits beleggeprüften Rückmeldung. Keine neuen Beobachtungen, keine Gesamtbeurteilung.
${feedbackEvidencePrompt(evidence)}
SELBSTEINSCHÄTZUNG (unbestätigtes Material): ${JSON.stringify(selbst)}
BELEGGEPRÜFTE RÜCKMELDUNG: ${JSON.stringify(report)}
Antworte ausschliesslich als JSON mit genau den drei bestehenden Kategorien agreement (Übereinstimmung), blindSpot (Blinder Fleck), tooStrict (Zu streng):
{"comparisons":[{"kind":"agreement","status":"supported","text":"kurzer Vergleich","studentEvidence":[{"turnId":"ID","quote":"exakt derselbe Beleg wie in der Rückmeldung"}]},{"kind":"blindSpot","status":"not-supported"},{"kind":"tooStrict","status":"not-supported"}]}
supported nur bei tatsächlich belegtem Unterschied bzw. belegter Übereinstimmung; wörtliche Belege müssen bereits in studentEvidence der Rückmeldung stehen. Pro Kategorie höchstens 25 Wörter. Wenn keine Grundlage für eine Kategorie vorliegt, status not-supported ohne Text. Erzwinge weder einen blinden Fleck noch eine zu strenge Einschätzung. Keine nicht gestellten Fragen, unterstellten Folgeschritte oder ausgelassenen Antworten. Kein Fehler aus Stopp/Abbruch. Sprache: ${feedbackLanguage()}.`);
  if (requestSession !== sessionNonce) throw new Error('Practice session changed.');
  const checked = validateFeedbackComparison(raw, evidence, report);
  const labels = { agreement: T('Übereinstimmung', 'Agreement'), blindSpot: T('Blinder Fleck', 'Blind spot'), tooStrict: T('Zu streng', 'Too strict') };
  return checked.comparisons.map(item => '**' + labels[item.kind] + ':** ' + (item.status === 'not-supported'
    ? T('Dafür liegt im Gespräch keine ausreichende Grundlage vor.', 'The conversation does not provide enough evidence for this.')
    : item.text + '\n\n' + T('Dein Wortlaut: ', 'Your words: ') + item.studentEvidence.map(feedbackQuote).join('; '))).join('\n\n');
}
function continuePractice(v, stoppedSession) {
  if (stoppedSession !== sessionNonce || mode !== 'vignette' || vignAktiv) return;
  sessionNonce++; primarySending = null; $('send').disabled = !api;
  hist = gespraechsVerlauf.slice();
  practiceTurns = feedbackTurns.map(turn => ({ ...turn }));
  vignAktiv = v; vorabAnalyse = null; validatedFeedbackReport = null;
  feedbackDisplayedSession = -1;
  uebungsbannerSetzen(true);
  add('sys', T('Dasselbe fiktive Gespräch läuft weiter. Deine bisherigen Beiträge bleiben erhalten. Du kannst jederzeit wieder stoppen.',
    'The same fictional conversation continues. Your earlier contributions are retained. You can stop again at any time.'));
  $('inp').focus();
}
function addPracticeActions(box, v, stoppedSession) {
  const actions = document.createElement('div'); actions.className = 'fbact';
  const resume = document.createElement('button'); resume.type = 'button';
  resume.textContent = T('Gespräch fortsetzen', 'Continue the conversation');
  resume.onclick = () => { if (stoppedSession === sessionNonce) { resume.disabled = true; restart.disabled = true; continuePractice(v, stoppedSession); } };
  const restart = document.createElement('button'); restart.type = 'button';
  restart.textContent = '↻ ' + T('Neu beginnen', 'Start again');
  restart.onclick = () => { if (stoppedSession === sessionNonce) { resume.disabled = true; restart.disabled = true; sessionNonce++; resetPracticeEvidence(); vignVorbereiten(v, true); } };
  actions.appendChild(resume); actions.appendChild(restart); box.appendChild(actions);
}
function showInsufficientPractice(v) {
  const box = add('sys', ''); box.className = 'm fb'; box.dataset.kind = 'authored';
  box.textContent = T('Vorgegebener Hinweis · Noch zu wenig Gesprächsmaterial. Für eine KI-Rückmeldung sind mindestens zwei eigene Gesprächsbeiträge nötig. Deshalb wurde keine Abschlussrückmeldung angefordert. Ein früher Stopp ist kein Fehler. Du kannst freiwillig weiterüben oder neu beginnen.',
    'Authored notice · Not enough conversation material yet. AI feedback needs at least two contributions of your own. No final feedback was requested. Stopping early is not a mistake. You can choose to continue practising or start again.');
  addPracticeActions(box, v, sessionNonce);
}
function feedbackUnavailable(element, comparison = false) {
  element.dataset.kind = 'status';
  element.textContent = comparison
    ? T('Vorgegebener Hinweis: Der Abgleich ist nicht verfügbar oder liess sich nicht anhand deiner Beiträge prüfen. Es wird kein ungeprüfter Abgleich angezeigt; es erfolgt kein automatischer Neuabruf.',
      'Authored notice: the comparison is unavailable or could not be verified against your contributions. No unverified comparison is displayed and no automatic retry will occur.')
    : T('Vorgegebener Hinweis: Die Rückmeldung ist nicht verfügbar oder liess sich nicht anhand deiner Beiträge prüfen. Es wird kein ungeprüftes Feedback angezeigt; es erfolgt kein automatischer Neuabruf. Du kannst freiwillig weiterüben.',
      'Authored notice: feedback is unavailable or could not be verified against your contributions. No unverified feedback is displayed and no automatic retry will occur. You can choose to continue practising.');
}
async function rueckmeldungHolen(v, selbst) {
  if (feedbackDisplayedSession === sessionNonce || mode !== 'vignette' || vignAktiv) return;
  feedbackDisplayedSession = sessionNonce;
  const evidence = collectFeedbackEvidence(feedbackTurns);
  if (!evidence.canGenerate) { showInsufficientPractice(v); return; }
  const requestSession = sessionNonce;
  const wait = add('bot', ''); wait.className = 'm fb';
  const denkStop = denkAn(wait, T('Rückmeldung wird geschrieben und ihre Belege werden geprüft', 'Writing feedback and checking its quotations'));
  const verlauf = gespraechsVerlauf.join('\n');
  const fok = fokusDim ? (lang === 'de' ? fokusDim.de : fokusDim.en) : null;
  try {
    if (!api) throw new Error('Host unavailable.');
    // Reuse the single prefetch even if it rejected; never retry it automatically.
    const pending = vorabAnalyse || analyseHolen(v);
    const out = await pending;
    if (requestSession !== sessionNonce) return;
    if (!out || !validatedFeedbackReport) throw new Error('No validated feedback.');
    denkStop(); mdRender(wait, out);
    const kopf = document.createElement('div'); kopf.className = 'fbkopf';
    kopf.textContent = T('KI-Übungsrückmeldung mit geprüften Wortlautbelegen. Deutungen bitte selbst prüfen. Keine Studienleistung, keine Beurteilung deiner Person, keine Grundlage für Noten.',
      'AI practice feedback with checked verbatim quotations. Check the interpretations yourself. Not coursework, not an assessment of you, not a basis for grades.');
    wait.prepend(kopf); kopierKnopf(wait, out);
    let abgleich = '';
    if (selbst) {
      const ab = document.createElement('div'); ab.className = 'm mikro'; ab.style.marginTop = '9px';
      const title = document.createElement('div'); title.className = 'mikrokopf';
      title.textContent = T('Deine Einschätzung neben der Beobachtung', 'Your rating next to the observation');
      ab.appendChild(title);
      const content = document.createElement('div'); ab.appendChild(content); wait.appendChild(ab);
      const stop = denkAn(content, T('gleicht ab', 'comparing'));
      try {
        abgleich = await comparisonHolen(selbst);
        if (requestSession !== sessionNonce) return;
        stop(); mdRender(content, abgleich);
      } catch (_) {
        if (requestSession === sessionNonce) feedbackUnavailable(content, true);
        abgleich = '';
      } finally { stop(); }
    }
    if (requestSession !== sessionNonce) return;
    addPracticeActions(wait, v, requestSession);
    const copy = document.createElement('button'); copy.type = 'button';
    copy.textContent = '📋 ' + T('Für die Supervision mitnehmen', 'Take to supervision');
    copy.onclick = async () => {
      const text = [T('Fiktives Übungsgespräch', 'Fictional practice conversation') + ': ' + (lang === 'de' ? v.de : v.en), T('Schwerpunkt', 'Focus') + ': ' + (fok || '—'),
        '\n— ' + T('Verlauf', 'Transcript') + ' —', verlauf, '\n— ' + T('Selbsteinschätzung', 'Self-assessment') + ' —', selbst || T('(übersprungen)', '(skipped)'),
        '\n— ' + T('KI-Rückmeldung; Wortlautbelege geprüft, Deutungen selbst prüfen', 'AI feedback; quotations checked, interpretations need your review') + ' —', out,
        ...(abgleich ? ['\n— ' + T('Abgleich', 'Comparison') + ' —', abgleich] : [])].join('\n');
      try { await navigator.clipboard.writeText(text); copy.textContent = T('Kopiert', 'Copied'); }
      catch (_) { copy.textContent = T('Kopieren nicht möglich', 'Copying not possible'); }
    };
    wait.appendChild(copy);
    if (fokusDim) { const example = exemplarBlock(fokusDim, false); if (example) wait.appendChild(example); }
  } catch (_) {
    if (requestSession === sessionNonce) { feedbackUnavailable(wait); addPracticeActions(wait, v, requestSession); }
  } finally { denkStop(); }
}
