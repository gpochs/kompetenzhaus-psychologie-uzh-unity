# Kompetenzhaus Unity project

Unity 6000.6.0f1, URP 17.6.0, WebGL. The source project uses the locally installed editor and free template packages. No cloud backend or paid service is required.

## Current boundary

This is a functional development scaffold. It provides curriculum loading, learning progression, optional-module choices, quiz feedback/retries, quest rewards, accessibility preferences, and bird/first-person camera controllers. The final BSc/MSc campus composition and character assets are a separate visual integration step.

`Assets/Kompetenzhaus/Runtime` owns serializable content and gameplay state. `Editor` owns generation, content import and builds through Unity APIs. `Tests/EditMode` exercises learning flow behavior. Existing `.unity` scenes are never regenerated automatically.

## Content

The canonical source is `../content/kompetenzhaus-content.json`. Run the repository exporter when its source changes, then use **Kompetenzhaus > Sync content**. The editor imports a byte-faithful copy as `Resources/kompetenzhaus-content` and validates schema, prerequisites and quiz explanations. `houseId` separates `bsc` and `msc`; stage 1–2 and 3–4 remain separate. Recommendations never become prerequisites. Optional content changes use a stable structural slot plus selected module code.

Current curriculum text and future proposals remain distinct. In-game mastery, self-checks and cosmetic experience are not academic credit or a measurement of personal competence.

## Learning state

`QuizSession` owns Question → Feedback → explicit Continue. Every original question must be answered correctly at least once. Misses return to the queue after feedback; a solved question separates the last failed item from an immediate retry when possible. Answer options are shuffled without changing the source `correctIndex`. The final explanation remains visible until Continue, which is the only completion trigger.

`ProgressionService` resolves placement prerequisites, master entry, self-checks, quiz mastery, option changes and quest prerequisites. Quest rewards are granted once. Persistence uses the dedicated `uzh.kompetenzhaus.unity.v1.progress` key; no old-game key is read, migrated or deleted.

Free mode is an architectural sandbox: curriculum prerequisites do not block building or starting a learning quest. Serious mode retains the inherited prerequisite/self-check/quiz route. Direct master entry can bypass the BSc entry gate and BSc quest chain. Academic or in-game achievements never fix a module's physical position.

## Architecture and browser bridge

`ArchitectureState` stores two independently configurable houses, user-drawn footprints, courtyard cutouts, room polygons, door/stair connections, floor counts, facades, roofs, palettes, module transforms, decorations and time of day. Source curriculum positions remain suggestions only. Each degree lot uses local tile coordinates -4 through 3; a 4 m tile spans `[x*4,(x+1)*4]` with its centre at `(x+0.5)*4`. Four floors and at most 64 occupied floor tiles per house bound the initial browser workload. Decoration x/z values are local metres.

Schema 1 requires a 4 m bay; room size and shape change through connected sets of bays. Each house supports up to 128 decorations. `TryBuildModuleInFreeBay` commits learning/build status together with a validated free physical bay. Undo may return a built module to the inventory without removing its learning history; building that inventory item again positions it without repeating achievements.

`ArchitectureService.TryCommit` clones and validates a proposed design before committing it. Room overlap, unsupported upper floors, invalid connections, module collisions and placement outside the correct degree lot are rejected. Undo restores design only, retaining quizzes, achievements and rewards. Save loading validates nested architecture and curriculum references before activating state; a rejected save is not overwritten during loading.

Doors join neighbouring bays on the same floor. The current straight staircase joins identical x/z bays on adjacent floors. Both endpoint bays remain free of modules and decorations; another flight needs different bays so its solid structure does not block the preceding flight's headroom. The reviewed stair geometry and upper landing still require an actual collision/traversal check before release.

The WebGL host sends `JSON.stringify({command,payload})` to `KompetenzhausBridge.ReceiveCommand` on the scene object named `KompetenzhausBridge`. Unity emits localized, answer-order-aware state snapshots through `window.KompetenzhausBridge.receive(json)`. Commands and DTOs are defined in `Runtime/App/KompetenzhausBridge.cs`. Browser panels own presentation and temporary option selection; Unity owns answers, explanations, progression and architecture validity. Use `setUiFocus` while a browser panel holds keyboard focus.

`CompetenceProjection` retains the source game's curriculum exposure profile and competency compass. Profile/compass scores derive from built curriculum slots, choices and explicit prior-learning self-checks, never quiz accuracy or cosmetic experience. `setCompetenceChoice` and `setPreStageChecks` validate changes before persistence; every bridge snapshot includes the projection and its available choice metadata.

## Editor operations

- **Create development scaffold scene** creates the scene once, with controllers and a functional development HUD. It does not add a final environment. Set the camera controller's `worldReady` only after importing the reviewed campus and colliders.
- **Validate and save** writes its evidence to `Logs/content-validation.json`.
- `Kompetenzhaus.Editor.ProjectTools.ImportAndSave` can run using Unity CLI `run -- -executeMethod ...` to import scripts/content and verify compilation.
- `Kompetenzhaus.Editor.BuildGame.Web` builds the saved scene to `Build/WebGL`. The method accepts the CLI's `-buildOutput`, restricted to this Unity project. It checks `BuildReport` and never deploys.

On this Windows workspace, the `U:` subst drive maps to this same project directory to keep Unity package paths below legacy path limits. Use `U:\` for editor imports, tests and builds. Pass `-sourceRoot` with the full duplicate repository path for commands that sync canonical content; an alias drive has no physical parent repository. The alias is not a second copy of the project.

For GitHub Pages, the initial build configuration uses gzip with Unity's decompression fallback. Revisit compression only after measuring actual hosting headers and browser loading. Keep caches, logs and local build outputs out of Git.

## Integration points

`GameBootstrap.HouseFocusRequested`, `ProgressionService.ModulePlaced`, `ProgressionService.Changed` and `QuizSession.Changed` connect the reviewed world and final HUD to gameplay without moving rules into scene objects. `GetContentForSlot` resolves the selected optional course; use the slot id for building and the chosen content for quizzes/text. `CameraModeController.FitBirdView` supports house/campus framing. All camera input must be blocked while a modal is active.

`WorldSupport.WorldAudio.Initialize(game)` binds the nine authored clips to learning/build cues and distance-based grounded footsteps. Audio stays unarmed until an actual user gesture; the HTML bridge sends `userGesture` and Unity canvas input also arms it. Ambience is quiet, fades in, and uses the same master volume as other sounds. Optional hover cues start disabled.

`WorldSupport.WorldInteractor.Initialize(game,audio)` raycasts bird-view clicks and first-person E interactions up to 4 m, respecting modal/input blocks. Add `WorldInteraction` to a collider parent and configure exactly one `moduleId` or `questId`. The bridge exposes target labels and floor/roof cutaway controls. `GuideMotion` animates the separate authored limb meshes using shoulder/hip pivots and an explicit local waypoint route; reduced motion restores its rest pose and stops its route. Imported model orientation and traversability still require visual/runtime verification.

`Kompetenzhaus.Editor.BridgeFixtureExporter.Export` writes real C# profile/bridge DTOs from the legacy-golden test cases to ignored `Logs/qa-snapshots`, without creating a scene or accessing player saves. These fixtures support semantic browser UI checks, not evidence that a finished world has run.

The development HUD offers DE/EN, BSc/MSc filters, current/future text, module alternatives, quizzes, quests, larger text, contrast and reduced-motion preferences. It is intentionally not the final UI. First-person movement and pointer-lock input await the approved traversable world. Screen-reader support has not been claimed or verified.
