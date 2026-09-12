# Generative AI companion

Status: 12 September 2026. Framework 2.1 uses 16 competencies and 48 criteria. This document describes the implemented source and its boundaries. The exact publication and live-test record is in `companion/published.json`; a local rebuild alone does not update the public artifact.

## Hosting and costs

The game remains a static Unity Web application. Its companion runs in Claude's artifact host through `window.claude.complete`, using the visitor's account. There is no project API key, paid proxy, credit purchase, automatic reload or paid fallback in this implementation. The project does not change visitors' account settings or guarantee that their own account has available allowance.

Outside the Claude host, the companion disables generation and labels the page as a preview. Authored scenario openers and worked examples are distinguished from model responses. An unavailable host, limit or connection produces an unavailable state, never a fabricated model answer.

The new [framework 2.1 companion](https://claude.ai/public/artifacts/3c6ead1a-4ca5-4d72-ade2-a463e3d9841f) has been published separately. The [original companion](https://claude.ai/public/artifacts/f66a1b12-cc7f-4cc3-ad43-5146ef4b5277) and [earlier Unity companion](https://claude.ai/public/artifacts/5a04170a-76aa-4bbf-97d0-d60e1e789f2c) remain available. The author preview of the earlier file changed without its public page updating; the new artifact therefore uses its own filename and public identifier.

The publisher interface supplied this embed endpoint:

    https://claude.site/public/artifacts/3c6ead1a-4ca5-4d72-ade2-a463e3d9841f/embed

The allowed embedding domain is `gpochs.github.io`. The browser shell reads publication metadata and retains an **Open in Claude** route. Embedding, account access and generation must still be tested from the eventual deployed game origin. A successful public-page conversation is not proof of a successful game embed.

## Four implemented modes

| Mode | Generated work | Boundary |
| --- | --- | --- |
| Building tutor | Explains module choices, learning opportunities, AI roles and the student's own disciplinary work. | Selected modules are prioritised; current module information and proposed redesigns remain distinct. |
| Fictional conversation | Responds as a fictional counterpart and supplies formative feedback or a final reflection. | Four authored scenarios, worked examples and observation dimensions are scaffolding, not real cases or assessed qualifications. |
| CV / reflection | Drafts a cautious sentence from an experience supplied by the visitor. | Without experience, it formulates a future learning goal. Building or passing a quiz does not justify a competence claim. |
| Career | Discusses professional activities, relevant criteria and further learning. | No fit percentage, inferred personal suitability or invented current fees. Professional information needs current independent verification. |

The companion contains the same framework, career views and proposed module learning designs as the game. All 16 competencies include an AI role, learner responsibility, assessment focus and example. T2 concerns purposeful use of an existing tool; T3 concerns the design and testing of a system or workflow. AI and Future Skills are views onto shared criteria, not additional scores.

## Optional learning context

The shell offers a preview and a copy action. Nothing is exported merely by opening the companion. A visitor can paste a restricted context into the hosted artifact. A validated parent-message offer also requires explicit acceptance; its delivery through Claude's wrapper remains an end-to-end deployment check.

Version 3 carries the active game mode, known module choices, known game-practice identifiers and an optional requested tutor mode, module or fictional scenario. The receiver also accepts older envelopes but discards legacy `stufen`, `felder` and `passung` values. ECTS are recalculated from known module IDs and actual selected alternatives. Imported values remain self-declared, not assessed evidence.

```json
{
  "schema": "kompetenzhaus.tutor-context",
  "version": 3,
  "frameworkVersion": "2.1.0-draft",
  "locale": "de",
  "mode": "tutor",
  "launch": { "moduleId": "003", "scenarioId": null },
  "context": {
    "v": 1,
    "mode": "frei",
    "placed": { "frei": { "003": {} } },
    "gamePractice": { "moduleIds": [], "questIds": [] }
  }
}
```

The contract validates known identifiers and choices, bounds input size, and excludes names, private notes, neighbours, full saves and practice transcripts from the game's context export. Only the active mode is included. Message offers must come from the allowlisted origin and the direct parent frame. Importing data does not start a model call or grant any achievement.

Generation sends the visitor's conversation to Claude under that account's terms and settings. The external session is not local processing. Visitors should use fictional practice material. Generated advice does not change module completion, grades, prerequisites or competency evidence in Unity.

## Verification and remaining work

The source builder compiles the generated JavaScript. Thirty-nine companion tests cover context filtering, choices, actual elective credits, malformed input, all four prompt paths, experience requirements, relevant module retrieval, obsolete attainment claims and evidence-bound feedback. Those tests use a mock host and are reported separately from real generated responses.

A public test exposed an unsupported criticism after a scene with only one learner contribution. The current source therefore makes no feedback request for fewer than two actual learner contributions. It displays an authored notice and permits voluntary continuation of the same dialogue or a new attempt. Longer-scene feedback must provide structured observations with exact quotations from the recorded learner turns. Invalid JSON, invented quotations and unavailable responses are not shown as feedback; no automatic retry occurs. Checked wording does not validate the interpretation or establish real-world effects.

`companion/published.json` records the final source hash and the scope of public-page checks. The following remain game-level checks: authentication and generation in the deployed embed, accepted-context delivery through the host wrapper, return to the running Unity scene, and unavailable-provider behaviour in that deployed environment. There is no released Unity Web build yet.

Rebuild with `node companion/build-companion.mjs`; verify with `node --test companion/companion.test.mjs companion/feedback-evidence.test.mjs`. Publish only the reviewed generated HTML, preserve prior artifacts, and verify the public page rather than relying on an updated author preview.
