# Unity AI companion

The standalone `ki-baututor-unity.html` accompanies the new game. [Companion 2.2 is published here](https://claude.ai/public/artifacts/28b92e04-206d-41b5-ad4e-2382126be043). The original and earlier published artifacts are preserved. The exact public URL, source hash, 488,478-byte source size and live-verification scope are recorded in [published.json](published.json). A later local rebuild is not automatically published.

The published revision uses framework **2.2.0-draft**: 16 competencies, 48 criteria, concrete AI roles and disciplinary responsibilities, and 153 proposed objectives across the catalogue and alternatives. AI spans all six domains, including disciplinary reasoning, output evaluation, consequences, relationships and values. T1–T3 form its technical subset. Without-AI, with-AI and about-AI phases describe possible ways of learning, not compulsory AI-free modules or additional assessments. Existing module information and future proposals remain separate. Building progress describes learning opportunities, never assessed competence or professional eligibility.

## Four modes

- Building tutor: module choices, disciplinary learning, AI roles and next activities. An explicitly selected module is prioritised.
- Fictional conversation: four authored scenarios, role-play, optional self-reflection and generated formative feedback. Worked examples are labelled, not represented as generated responses or university assessment records.
- CV / reflection: a cautious draft based only on experience supplied by the visitor. Without experience, it produces a future learning goal.
- Career: professional activities, criteria and further learning. No fit percentage, inferred personal suitability or invented current fees.

Generated responses use the Claude artifact host's window.claude.complete function. No owner API key, paid endpoint or paid fallback is present. Visitors need an available Claude host and sufficient account allowance. Outside that host, the file shows a preview and disables generation. This project does not enable paid usage; it cannot control another visitor's account settings.

## Build and verification

Run node companion/build-companion.mjs and node --test companion/companion.test.mjs companion/feedback-evidence.test.mjs.

The builder starts with the inherited public companion, replaces its scoring prompts and catalogue, applies the context boundary and compiles the script. Thirty-nine tests cover privacy filtering, choices, actual elective credits, malformed input, all four request paths, independent experience, relevant module retrieval, obsolete attainment claims and evidence-bound practice feedback. DOM simulations use a MOCK host; live model behaviour is verified separately in published.json.

On the current **2.2 public page**, Tutor mode generated actual Claude responses in German and English. The exercised answers distinguished disciplinary evidence evaluation and consequences from normative judgement, treated T1–T3 as a technical subset, and described independent prediction and transfer alongside prepared AI output. These are real host-response checks, separate from the 39 mock tests. All four mode controls are present, but the other modes were not re-exercised on 2.2. The successful four-mode and repeated-stop conversation checks belong to the preserved 2.1 publication. After switching to English, some status text and accessibility labels still remain German; main controls and generated answers switch language.

Final practice feedback requires at least two actual learner contributions. A shorter scene produces an authored notice and makes no feedback request, including prefetch. The visitor can resume the same conversation or start again. For longer scenes, structured feedback must cite exact words from the recorded learner turns; a quoted character response must follow the cited learner action. Possible effects are explicitly distinguished from observed text. Invalid JSON, invented quotations and request failures produce a visible notice without an automatic retry. Quotation checks do not establish that the model's interpretation is correct; the interface explicitly asks the visitor to examine it.

## Context contract

The receiver accepts the old active-mode export and envelopes version 2 or 3. It emits version 3 and discards all old stufen, felder and passung values. ECTS are recomputed from known module IDs and actual selected options. Only known module choices and known game-practice IDs survive. No names, free notes, neighbours, private transcripts or full save data are exported automatically.

Example:

    {"schema":"kompetenzhaus.tutor-context","version":3,"frameworkVersion":"2.2.0-draft","locale":"de","mode":"tutor","launch":{"moduleId":"003","scenarioId":null},"context":{"v":1,"mode":"frei","placed":{"frei":{"003":{}}},"gamePractice":{"moduleIds":[],"questIds":[]}}}

Accepted input remains self-declared. Selecting a mode, importing context or receiving an offer never starts generation. Data remains in page memory. Visitors choose what to paste and when to send it.

## Embedding and publication

The released game uses the verified **Open in Claude** route with optional explicit context copy. In the actual GitHub Pages test, the embedded interface loaded and exposed its host bridge, but the submitted question returned no AI answer. The same question on the direct published Claude artifact produced a real answer distinguishing P1.2, R4.2 and V1 and proposing an independent without-AI prediction. `published.json` therefore sets **`embedGenerationVerified: false`**, and the shell does not present the iframe as a working generator.

The domain `gpochs.github.io` remains configured for embedding, but domain permission alone does not establish generation. The receiver's optional parent-message contract accepts only validated offers from that origin and its direct parent frame; the released game makes no automatic transfer. Visitors may copy known module choices and game-practice context, then import it in Claude. Names and private notes are omitted. No successful end-to-end Pages iframe generation or automatic context import is claimed. See the [game release record](../docs/RELEASE.md).

Publish the exact reviewed HTML as a new revision of the NEW companion, compare hashes and verify the public page. Updating the author preview does not prove the public revision changed. Preserve the separate original artifact throughout.
