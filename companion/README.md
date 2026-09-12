# Unity AI companion

The standalone ki-baututor-unity.html accompanies the new game. The original published artifact is preserved. The new public URL, source revision and live-verification scope are recorded in published.json. A local rebuild is not automatically published.

The local revision uses framework 2.1: 16 competencies, 48 criteria, concrete AI roles and disciplinary responsibilities, and 153 proposed objectives across the catalogue and alternatives. Existing module information and future proposals remain separate. Building progress describes learning opportunities, never assessed competence or professional eligibility.

## Four modes

- Building tutor: module choices, disciplinary learning, AI roles and next activities. An explicitly selected module is prioritised.
- Fictional conversation: four authored scenarios, role-play, optional self-reflection and generated formative feedback. Worked examples are labelled, not represented as generated responses or university assessment records.
- CV / reflection: a cautious draft based only on experience supplied by the visitor. Without experience, it produces a future learning goal.
- Career: professional activities, criteria and further learning. No fit percentage, inferred personal suitability or invented current fees.

Generated responses use the Claude artifact host's window.claude.complete function. No owner API key, paid endpoint or paid fallback is present. Visitors need an available Claude host and sufficient account allowance. Outside that host, the file shows a preview and disables generation. This project does not enable paid usage; it cannot control another visitor's account settings.

## Build and verification

Run node companion/build-companion.mjs and node --test companion/companion.test.mjs companion/feedback-evidence.test.mjs.

The builder starts with the inherited public companion, replaces its scoring prompts and catalogue, applies the context boundary and compiles the script. Thirty-nine tests cover privacy filtering, choices, actual elective credits, malformed input, all four request paths, independent experience, relevant module retrieval, obsolete attainment claims and evidence-bound practice feedback. DOM simulations use a MOCK host; live model behaviour is verified separately in published.json.

Final practice feedback requires at least two actual learner contributions. A shorter scene produces an authored notice and makes no feedback request, including prefetch. The visitor can resume the same conversation or start again. For longer scenes, structured feedback must cite exact words from the recorded learner turns; a quoted character response must follow the cited learner action. Possible effects are explicitly distinguished from observed text. Invalid JSON, invented quotations and request failures produce a visible notice without an automatic retry. Quotation checks do not establish that the model's interpretation is correct; the interface explicitly asks the visitor to examine it.

## Context contract

The receiver accepts the old active-mode export and envelopes version 2 or 3. It emits version 3 and discards all old stufen, felder and passung values. ECTS are recomputed from known module IDs and actual selected options. Only known module choices and known game-practice IDs survive. No names, free notes, neighbours, private transcripts or full save data are exported automatically.

Example:

    {"schema":"kompetenzhaus.tutor-context","version":3,"frameworkVersion":"2.1.0-draft","locale":"de","mode":"tutor","launch":{"moduleId":"003","scenarioId":null},"context":{"v":1,"mode":"frei","placed":{"frei":{"003":{}}},"gamePractice":{"moduleIds":[],"questIds":[]}}}

Accepted input remains self-declared. Selecting a mode, importing context or receiving an offer never starts generation. Data remains in page memory. Visitors choose what to paste and when to send it.

## Embedding and publication

Use the actual embed URL and allowed domain in published.json, with a direct Open in Claude alternative. The receiver accepts only validated offers from https://gpochs.github.io and its direct parent frame. The learner must explicitly accept. Clipboard import remains available because host wrappers may prevent parent messaging. Automatic transfer is not verified until tested from the published game origin.

Publish the exact reviewed HTML as a new revision of the NEW companion, compare hashes and verify the public page. Updating the author preview does not prove the public revision changed. Preserve the separate original artifact throughout.
