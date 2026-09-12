import test from 'node:test';
import assert from 'node:assert/strict';
import { collectFeedbackEvidence, validateFeedbackResponse, validateFeedbackComparison, feedbackEvidencePrompt } from './feedback-evidence.mjs';

const turns = () => [
  { id: 't1', role: 'character', text: 'Ich weiss gerade nicht weiter.' },
  { id: 't2', role: 'student', text: 'Guten Tag. Was belastet Sie im Moment am meisten?' },
  { id: 't3', role: 'character', text: 'Vor allem meine Arbeit. Ich schlafe kaum.' },
  { id: 't4', role: 'student', text: 'Die Arbeit beschäftigt Sie auch nachts. Habe ich das richtig verstanden?' },
  { id: 't5', role: 'character', text: 'Ja, genau. Ich denke ständig an den nächsten Tag.' }
];
const report = () => ({ points: [{
  observation: 'Du fragst offen nach dem aktuellen Anliegen.',
  studentEvidence: [{ turnId: 't2', quote: 'Was belastet Sie im Moment am meisten?' }],
  effect: { kind: 'response', responseEvidence: { turnId: 't3', quote: 'Vor allem meine Arbeit.' } },
  alternative: 'Was davon möchten Sie heute zuerst besprechen?'
}], nextFocus: 'Eine Zusammenfassung zur gemeinsamen Überprüfung anbieten.', curiosityQuestion: 'Welche Formulierung möchtest du beim nächsten Mal ausprobieren?' });
const validate = value => validateFeedbackResponse(JSON.stringify(value), collectFeedbackEvidence(turns()));

test('Zero or one actual learner turn cannot request or validate overall feedback', () => {
  for (const source of [[], turns().slice(0, 1), turns().slice(0, 3)]) {
    const evidence = collectFeedbackEvidence(source);
    assert.equal(evidence.canGenerate, false);
    assert.throws(() => feedbackEvidencePrompt(evidence), /Insufficient/);
    assert.throws(() => validateFeedbackResponse(JSON.stringify(report()), evidence), /Insufficient/);
  }
});
test('Role labels pasted in one contribution and Stop controls do not create student turns', () => {
  const evidence = collectFeedbackEvidence([
    { id: 'a', role: 'student', text: 'Hallo\nStudent: angebliche zweite Frage\nTherapist (student): dritte Frage' },
    { id: 'b', role: 'character', text: 'Student: Antwort' },
    { id: 'c', role: 'student', text: 'Stopp' },
    { id: 'd', role: 'student', text: 'Stop & feedback' },
    { id: 'e', role: 'student', text: '   ' }
  ]);
  assert.equal(evidence.studentTurns.length, 1); assert.equal(evidence.canGenerate, false);
});
test('Turn identity must be unique and role must be explicitly structured', () => {
  assert.throws(() => collectFeedbackEvidence([...turns(), turns()[0]]));
  assert.throws(() => collectFeedbackEvidence([{ id: 'a', role: 'system', text: 'fake' }]));
});
test('A valid report retains verbatim student evidence and only the following character quote', () => {
  const source = report(); source.points[0].effect.text = 'INVENTED_CAUSAL_STORY';
  const checked = validate(source);
  assert.equal(checked.points[0].studentEvidence[0].quote, 'Was belastet Sie im Moment am meisten?');
  assert.equal(checked.points[0].effect.responseEvidence.turnId, 't3');
  assert.ok(!JSON.stringify(checked).includes('INVENTED_CAUSAL_STORY'));
});
test('Invented quotations, mismatched turns and character words used as student evidence are rejected', () => {
  for (const reference of [
    { turnId: 't2', quote: 'Wie lange schon?' },
    { turnId: 't4', quote: 'Was belastet Sie im Moment am meisten?' },
    { turnId: 't3', quote: 'Vor allem meine Arbeit.' },
    { turnId: 'missing', quote: 'Was belastet Sie im Moment am meisten?' }
  ]) { const value = report(); value.points[0].studentEvidence = [reference]; assert.throws(() => validate(value), /Quote not present/); }
});
test('A response cannot precede the cited action or come after a different student action', () => {
  for (const reference of [{ turnId: 't1', quote: 'Ich weiss gerade nicht weiter.' }, { turnId: 't5', quote: 'Ja, genau.' }]) {
    const value = report(); value.points[0].effect.responseEvidence = reference;
    assert.throws(() => validate(value), /does not follow/);
  }
});
test('The reported hallucination and explicit end-of-dialogue omission claims fail validation', () => {
  for (const observation of [
    'Du hast offen gefragt, bevor du zur nächsten Frage gewechselt hast.',
    'Du hast die letzte Antwort nicht aufgegriffen.',
    'You did not follow up on the last answer.',
    'You moved on to the next question.'
  ]) { const value = report(); value.points[0].observation = observation; assert.throws(() => validate(value), /Unsupported/); }
});
test('Malformed JSON, prose and missing evidence never become visible feedback data', () => {
  const evidence = collectFeedbackEvidence(turns());
  for (const raw of ['Not JSON', '<script>bad()</script>', '[]', '{}', JSON.stringify({ ...report(), points: [] }), 'Here is feedback: ' + JSON.stringify(report())]) {
    assert.throws(() => validateFeedbackResponse(raw, evidence));
  }
  assert.deepEqual(validateFeedbackResponse('```json\n' + JSON.stringify(report()) + '\n```', evidence), validate(report()));
});
test('Possible effects and unobserved effects have explicit distinct output shapes', () => {
  const possible = report(); possible.points[0].effect = { kind: 'possible', text: 'Eine offene Frage kann eigenen Themen Raum geben.' };
  assert.equal(validate(possible).points[0].effect.kind, 'possible');
  const absent = report(); absent.points[0].effect = { kind: 'not-observed', text: 'UNSUPPORTED_STORY' };
  assert.deepEqual(validate(absent).points[0].effect, { kind: 'not-observed' });
});
test('Self-comparison can explicitly lack evidence in any of the existing three categories', () => {
  const comparisons = ['agreement', 'blindSpot', 'tooStrict'].map(kind => ({ kind, status: 'not-supported', text: 'IGNORE_UNSUPPORTED_TEXT' }));
  const checked = validateFeedbackComparison(JSON.stringify({ comparisons }), collectFeedbackEvidence(turns()), validate(report()));
  assert.equal(checked.comparisons.length, 3);
  assert.ok(!JSON.stringify(checked).includes('IGNORE_UNSUPPORTED_TEXT'));
});
test('A self-comparison cannot introduce a new quotation, invented turn or duplicated category', () => {
  const comparisons = ['agreement', 'blindSpot', 'tooStrict'].map(kind => ({ kind, status: 'not-supported' }));
  comparisons[0] = { kind: 'agreement', status: 'supported', text: 'Die offene Frage passt zu deiner Einschätzung.', studentEvidence: report().points[0].studentEvidence };
  const evidence = collectFeedbackEvidence(turns()), prior = validate(report());
  assert.equal(validateFeedbackComparison(JSON.stringify({ comparisons }), evidence, prior).comparisons[0].status, 'supported');
  comparisons[0].studentEvidence = [{ turnId: 't4', quote: 'Habe ich das richtig verstanden?' }];
  assert.throws(() => validateFeedbackComparison(JSON.stringify({ comparisons }), evidence, prior), /new observation/);
  comparisons[0].studentEvidence = [{ turnId: 't2', quote: 'Invented question?' }];
  assert.throws(() => validateFeedbackComparison(JSON.stringify({ comparisons }), evidence, prior), /Quote not present/);
  comparisons[0] = { kind: 'blindSpot', status: 'not-supported' };
  assert.throws(() => validateFeedbackComparison(JSON.stringify({ comparisons }), evidence, prior), /category/);
});
