# Curriculum profile projection

## Canonical framework 2.2

`FrameworkV2Catalog` and `FrameworkV2Projection` implement the current design proposal, version `2.2.0-draft`: **16 competencies, 48 uniquely owned criteria, three task-demand levels and four separate study milestones**. The sources are `content/competency-framework.json`, `career-profiles.json` and `module-learning-design.json`. They define 12 activity-based career views, 14 Future Skills perspectives and 153 proposed objectives across 43 module slots plus three alternatives. Each competency includes an explicit AI role, student responsibility, assessment focus and example task; T1 understanding, T2 tool operation and T3 system/workflow design remain distinct. AI-related disciplinary judgement, consequences, relationships and values also belong to the other domains.

Load the validated resources once with `FrameworkV2Catalog.LoadResources(catalog)`, then call `FrameworkV2Projection.Create(framework, catalog, progression)`. The bridge exposes the localized result as `frameworkProfile`; missing or invalid data produces `frameworkError`, with no fallback to the old scores.

`frameworkProfile.aiAcrossCurriculum` projects the source's six domain rows, four dimensions (knowledge, skills, attitudes and values), eight cross-domain links, three phases, seven assessment-design rules and four examples into the selected language. The six rows partition the same 48 canonical criterion IDs. Connections and examples may refer to an ID again without creating another criterion or score. The catalogue requires this metadata for version 2.2. The phases **without AI, with AI and about AI** describe task-specific design choices: they do not mandate AI use, wholly AI-free modules or extra assessments. Independent thinking remains explicit. In the source module JSON, `proposal.aiStudyAssessment` indexes existing objectives by these phase opportunities; it does not add attained levels or new objectives.

Built modules contribute proposed learning opportunities. A selected optional module replaces the slot's proposed objectives. Task levels describe those opportunities, not attained competence. Open topics and individualized tasks remain explicitly unconfirmed. ECTS, quiz completion and reward XP never promote a learner to a competence level. There are no assessed records: `assessmentStatus` stays `unassessed` and `hasAssessedEvidence` stays false. Quiz and quest completion appears separately under `gamePractice`. The eleven new quests reference canonical criteria as practice topics, not evidence of meeting the complete criteria.

Career facets and Future Skills perspectives reference these same canonical criteria. Optional digital-context facets retain their context conditions. They show relevant opportunities and next modules, without personal fit percentages or normative target curves.

## Historical compatibility

`CompetenceProjection`, `CompetenceModels` and `LegacyCompetenceRules.generated.cs` preserve the public source's 19-identifier model, six-axis radar, career-fit calculations and what-if behaviour for compatibility and regression. **Those calculations are not the current competence or achievement display.**

The bridge still exposes legacy `profile` and `profilePreview` fields and their selection/preview commands. The current HTML shell reads only `profile.credits` for the study overview and `profile.choiceOptions` for saved specialization, topic and research-question selections. It does not render the old scores, stages, radar, fit percentages or preview. All competence and career presentation uses `frameworkProfile`. Historical `competenceChoices` and `preStageChecks` remain validated save data; they do not silently establish new-framework attainment or confirmed topic alignment.

Legacy ECTS totals use the selected optional module's actual credits: Mentoring has 6 ECTS in a slot whose base weight is 4, so a historical MSc selection can total 122 against the separate programme target of 120. These are curriculum-planning totals, not awarded academic credit or competence measures.

`GameWorld.ModuleModel` still checks the inherited `Fa2`/`Fa3`/`Fa4` tags to select furniture. This is a furniture heuristic only: it grants no score, level, evidence or reward. Historical module texts and quizzes remain in the source catalogue. `QuizPresentation` supplies display-only DE/EN wording for the three quizzes naming obsolete competence labels; their source records and answer indices remain unchanged.

## Reproducibility and verification

`generate-legacy-rules.mjs` executes the original public JavaScript functions to generate historical rules and six golden scenarios. Its 19-score, stage and career-fit comparisons are regression evidence for source fidelity, not validation of the new framework. The fixture setup maps implicit optional choices to the equivalent explicit legacy selection.

Verification on 12 September 2026 is recorded separately; these counts must not be presented as one combined run:

- **15/15 current FrameworkV2Projection Unity tests** passed in `Logs/framework-v22-tests.xml`. They include unique ownership of all 48 criteria, deliberate phases without AI, cross-domain separation and DE/EN projection.
- **42 distinct earlier core test cases** passed across separate runs covering progression, architecture, room/stair access, house switching and audio preferences. The earlier `Logs/framework-v21-results.xml` and targeted `Logs/quiz-alignment-repaired-results.xml` remain historical evidence, not a fresh combined v2.2 suite. The quiz tests preserve the three adapted questions' answer indices and explicit feedback/Continue; quest completion changes game practice only.
- **3/3 current quest alignment Node tests** passed via `node --test tools/quest-framework-alignment.test.mjs` after the eleven quests' version metadata was updated to 2.2. The validated content export preserves 43 slots, 130 inherited questions and 22 additional questions.
- **16/16 module-design Node tests** passed; the actual standard pathway still covers all 48 criteria after elective replacement. Both canonical builders pass `--check`.
- **20/20 current web UI/contract tests** passed via `node --test web-template/contract.test.mjs web-template/framework-view.test.mjs`. The 2.2 interface was checked in DE/EN using actual C# DTO fixtures. Browser verification and its limits are documented in `web-template/README.md`.

`Editor/FrameworkV2ContentSync` synchronizes validated resources and exports synthetic DE/EN profile fixtures. Hash records distinguish source data, copied resources and fixtures; repeat synchronization after any content change before building. QA files under Unity `Logs` are local and ignored by Git; approved synthetic browser fixtures live under `web-template/qa` for development preview only. Their rendered profile is a genuine C# projection of diagnostic inputs, not a screenshot of a running world.

The same canonical sources produce the checked v2.2 Word documents in the parent project directory: model documents of 53 pages DE and 52 pages EN, and module documents of 111 pages in each language. They describe proposals and preserve earlier versions; neither document length nor source objective counts constitute an assessment.

The URP import issue was resolved by reimporting exactly three incorrectly cached probe-baking shader assets through Unity's AssetDatabase API. Fresh Direct3D11 and headless starts passed without the import error; package versions were unchanged. `graphics-import-repair.json` and `graphics-import-verified.log` retain the evidence locally. These checks establish import and logic health, not production world assembly, rendered-game quality or a completed WebGL release. No model API or paid service is used by this projection code.
