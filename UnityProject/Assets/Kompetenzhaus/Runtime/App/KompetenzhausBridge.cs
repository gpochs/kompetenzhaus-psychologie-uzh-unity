using System;
using System.Linq;
using System.Runtime.InteropServices;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using Kompetenzhaus.Competencies;
using Kompetenzhaus.World;
using Kompetenzhaus.WorldSupport;
using Kompetenzhaus.Gameplay;
using UnityEngine;

namespace Kompetenzhaus
{
    public sealed class KompetenzhausBridge : MonoBehaviour
    {
        public GameBootstrap game;
        public GameWorld world;
        public WorldAudio worldAudio;
        public WorldInteractor worldInteractor;
        public event Action<string> SnapshotProduced;
        private string message = "";
        private string commandError = "";
        private CompetenceProfileSnapshot profilePreview;
        private FrameworkV2Catalog frameworkCatalog;
        private FrameworkV2Profile frameworkProfile;
        private string frameworkStateKey;
        private string frameworkError = "";
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void KompetenzhausEmitState(string json);
#endif

        private void Start()
        {
            gameObject.name = "KompetenzhausBridge";
            if (game == null) game = FindAnyObjectByType<GameBootstrap>();
            if (world == null) world = FindAnyObjectByType<GameWorld>();
            if (worldAudio == null) worldAudio = FindAnyObjectByType<WorldAudio>();
            if (worldInteractor == null) worldInteractor = FindAnyObjectByType<WorldInteractor>();
            if (worldInteractor != null) worldInteractor.FocusChanged += OnWorldFocusChanged;
            if (game?.Progression != null)
            {
                game.Progression.Changed += OnProgressChanged;
                game.Quiz.Changed += Publish;
                game.Message += OnMessage;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
            Publish();
        }

        public void ReceiveCommand(string json)
        {
            try
            {
                if (json == null || json.Length > 1000000) throw new ArgumentException("Invalid command length.");
                var request = JsonUtility.FromJson<BridgeCommand>(json);
                if (request == null || string.IsNullOrWhiteSpace(request.command)) throw new ArgumentException("Missing command.");
                if (request.command == "getState") { Publish(); return; }
                if (game?.Progression == null) throw new InvalidOperationException("The game is not ready.");
                var payload = request.payload ?? new BridgePayload();
                commandError = message = "";
                switch (request.command)
                {
                    case "selectHouse": game.SelectHouse(payload.houseId); break;
                    case "setLanguage": game.SetLanguage(payload.language); break;
                    case "setLearningMode":
                        if (!Enum.TryParse(payload.mode, out LearningMode learningMode) || !Enum.IsDefined(typeof(LearningMode), learningMode)) throw new ArgumentException("Unknown learning mode.");
                        game.Progression.Data.learningMode = learningMode; game.Progression.SaveAndNotify(); break;
                    case "setFutureCurriculum": game.Progression.Data.futureCurriculum = payload.enabled; game.Progression.SaveAndNotify(); break;
                    case "setDirectMasterEntry":
                        if (game.Catalog.Document.rules?.allowDirectMasterEntry != true) throw new InvalidOperationException("Direct master entry is unavailable.");
                        game.Progression.Data.directMasterEntry = payload.enabled; game.Progression.SaveAndNotify(); break;
                    case "setViewMode":
                        if (!Enum.TryParse(payload.mode, out ViewMode viewMode) || !Enum.IsDefined(typeof(ViewMode), viewMode)) throw new ArgumentException("Unknown view mode.");
                        if (!game.cameraController.SetMode(viewMode)) throw new InvalidOperationException("The reviewed environment is not ready for first-person exploration.");
                        break;
                    case "setUiFocus": game.SetUiFocus(payload.enabled); break;
                    case "userGesture": worldAudio?.NotifyUserInteraction(); break;
                    case "setArchitectureView":
                        if (world == null) throw new InvalidOperationException("The world view is not ready.");
                        if (payload.visibleFloor < 0 || payload.visibleFloor > 3) throw new ArgumentException("Visible floor must be between 0 and 3.");
                        world.SetArchitectureView(payload.visibleFloor, payload.showRoofs);
                        break;
                    case "setAccessibility":
                        if (payload.accessibility == null) throw new ArgumentException("Missing accessibility settings.");
                        game.ApplyAccessibility(payload.accessibility); break;
                    case "selfCheck": game.Progression.SetSelfCheck(payload.moduleId, payload.enabled); break;
                    case "chooseModule":
                        var previousCode = game.Progression.GetSelectedCode(payload.moduleId);
                        if (!game.Progression.SetModuleChoice(payload.moduleId, payload.code)) throw new InvalidOperationException("This module choice is unavailable or the slot is already built.");
                        if (previousCode != payload.code && game.ActiveModuleId == payload.moduleId && game.Quiz.Phase != QuizPhase.Idle &&
                            !game.StartModuleQuiz(payload.moduleId)) game.Quiz.Cancel();
                        break;
                    case "setCompetenceChoice":
                        if (!game.Progression.SetCompetenceChoice(payload.competenceChoice, out var choiceError)) throw new ArgumentException(choiceError);
                        break;
                    case "setPreStageChecks":
                        if (!game.Progression.SetPreStageChecks(payload.preStageChecks, out var checksError)) throw new ArgumentException(checksError);
                        break;
                    case "previewCompetence":
                        profilePreview = CompetenceProjection.SimulateChoiceProfile(game.Catalog, game.Progression,
                            payload.bachelorDirection, payload.specialisationId, payload.optionalModuleCode);
                        break;
                    case "clearCompetencePreview": profilePreview = null; break;
                    case "startModuleQuiz": if (!game.StartModuleQuiz(payload.moduleId)) throw new InvalidOperationException("The module quiz is unavailable."); break;
                    case "startQuest": if (!game.StartQuest(payload.questId)) throw new InvalidOperationException("The quest is unavailable or prerequisites are still open."); break;
                    case "answer": if (!game.Quiz.Answer(payload.index)) throw new InvalidOperationException("An answer cannot be submitted in the current quiz state."); break;
                    case "continue": if (!game.Quiz.Continue()) throw new InvalidOperationException("Continue is only available after feedback."); break;
                    case "closeQuiz": game.Quiz.Cancel(); break;
                    case "placeModule":
                        if (!game.Architecture.TryBuildModuleInFreeBay(payload.moduleId, out var placementError)) throw new InvalidOperationException(placementError); break;
                    case "moveModule":
                        if (!game.Architecture.TryMoveModule(payload.moduleId, payload.houseId, payload.x, payload.z, payload.floor, payload.rotation,
                            out var moveError, payload.width, payload.depth, payload.roomId)) throw new InvalidOperationException(moveError);
                        break;
                    case "commitArchitecture":
                        if (payload.architecture == null) throw new ArgumentException("Missing architecture.");
                        if (!game.Architecture.TryCommit(state =>
                        {
                            state.schemaVersion = payload.architecture.schemaVersion;
                            state.gridSizeMetres = payload.architecture.gridSizeMetres;
                            state.timeOfDay = payload.architecture.timeOfDay;
                            state.weather = payload.architecture.weather;
                            state.houses = payload.architecture.houses;
                        }, out var designError)) throw new InvalidOperationException(designError);
                        break;
                    case "undoArchitecture": if (!game.Architecture.Undo(out var undoError)) throw new InvalidOperationException(undoError); break;
                    default: throw new ArgumentException("Unknown command: " + request.command);
                }
            }
            catch (Exception exception) { commandError = exception.Message; Debug.LogWarning("[Kompetenzhaus bridge] " + exception.Message); }
            Publish();
        }

        public string CreateSnapshotJson()
        {
            var ready = game?.Progression != null;
            var snapshot = new BridgeSnapshot { ready = ready, error = ready ? commandError : game?.StartupError ?? "Game initialization pending.", message = message };
            if (!ready) return JsonUtility.ToJson(snapshot);
            var progress = game.Progression.Data;
            snapshot.language = progress.language;
            snapshot.learningMode = progress.learningMode.ToString();
            snapshot.selectedHouseId = progress.selectedHouseId;
            snapshot.futureCurriculum = progress.futureCurriculum;
            snapshot.directMasterEntry = progress.directMasterEntry;
            snapshot.viewMode = (game.cameraController?.Mode ?? ViewMode.BirdView).ToString();
            snapshot.worldReady = game.cameraController != null && game.cameraController.worldReady;
            snapshot.visibleFloor = world != null ? world.visibleFloor : 3;
            snapshot.showRoofs = world != null && world.showRoofs;
            snapshot.interactionHint = worldInteractor?.CurrentHint ?? "";
            snapshot.focusedModuleId = worldInteractor?.FocusedTarget?.moduleId ?? "";
            snapshot.focusedQuestId = worldInteractor?.FocusedTarget?.questId ?? "";
            snapshot.accessibility = progress.accessibility;
            snapshot.placedModuleIds = progress.placedModuleIds.ToArray();
            snapshot.quizMasteredModuleIds = progress.quizMasteredModuleIds.ToArray();
            snapshot.selfCheckedModuleIds = progress.selfCheckedModuleIds.ToArray();
            snapshot.completedQuestIds = progress.completedQuestIds.ToArray();
            snapshot.unlockedCosmeticIds = progress.unlockedCosmeticIds.ToArray();
            snapshot.earnedExperience = progress.earnedExperience;
            snapshot.moduleChoices = progress.moduleChoices.ToArray();
            snapshot.competenceChoices = progress.competenceChoices.ToArray();
            snapshot.preStageChecks = progress.preStageChecks.ToArray();
            snapshot.profile = CompetenceProjection.Create(game.Catalog, game.Progression, progress.competenceChoices, progress.preStageChecks);
            try
            {
                frameworkCatalog ??= FrameworkV2Catalog.LoadResources(game.Catalog);
                var currentFrameworkState = JsonUtility.ToJson(progress);
                if (frameworkProfile == null || frameworkStateKey != currentFrameworkState)
                {
                    frameworkProfile = FrameworkV2Projection.Create(frameworkCatalog, game.Catalog, game.Progression);
                    frameworkStateKey = currentFrameworkState;
                }
                frameworkError = "";
            }
            catch (Exception exception)
            {
                frameworkProfile = null; frameworkStateKey = null; frameworkError = exception.Message;
            }
            snapshot.frameworkProfile = frameworkProfile;
            snapshot.frameworkError = frameworkError;
            snapshot.journey = JourneyDirector.Create(game.Catalog, game.Progression);
            snapshot.profilePreview = profilePreview;
            snapshot.moduleStates = game.Catalog.Document.modules.Select(module =>
            {
                var eligibility = game.Progression.GetEligibility(module.id);
                var positioned = game.Architecture.IsPositioned(module.id);
                return new BridgeModuleState
                {
                    id = module.id, selectedCode = game.Progression.GetSelectedCode(module.id), canBuild = eligibility.Allowed && !positioned, positioned = positioned,
                    alreadyPlaced = eligibility.AlreadyPlaced, missingPrerequisiteIds = eligibility.MissingPrerequisiteIds,
                    needsSelfCheck = eligibility.NeedsSelfCheck, needsQuiz = eligibility.NeedsQuiz, needsMasterEntry = eligibility.NeedsMasterEntry
                };
            }).ToArray();
            snapshot.questStates = game.Catalog.Document.quests.Select(quest => new BridgeQuestState
            { id = quest.id, canStart = game.Progression.CanStartQuest(quest.id), completed = progress.completedQuestIds.Contains(quest.id) }).ToArray();
            var quiz = game.Quiz;
            var question = quiz.CurrentQuestion;
            snapshot.quiz = new BridgeQuizState
            {
                phase = quiz.Phase.ToString(), moduleId = game.ActiveModuleId, questId = game.ActiveQuestId,
                questionId = question?.id ?? "", type = question?.type ?? "", prompt = QuizPresentation.Prompt(question, progress.language),
                positions = question?.positions?.Select(text => text.Get(progress.language)).ToArray() ?? Array.Empty<string>(),
                options = question == null ? Array.Empty<string>() : quiz.OptionOrder.Select(index => question.options[index].Get(progress.language)).ToArray(),
                selectedIndex = quiz.OptionOrder.ToList().IndexOf(quiz.SelectedOptionIndex), correct = quiz.LastAnswerCorrect,
                explanation = quiz.Phase == QuizPhase.Feedback || quiz.Phase == QuizPhase.Complete ? QuizPresentation.Explanation(question, progress.language) : "",
                correctAnswer = (quiz.Phase == QuizPhase.Feedback || quiz.Phase == QuizPhase.Complete) && question != null ? question.options[question.correctIndex].Get(progress.language) : "",
                mastered = quiz.MasteredCount, total = quiz.QuestionCount, mistakes = quiz.Mistakes
            };
            snapshot.architecture = game.Architecture.State;
            snapshot.canUndoArchitecture = game.Architecture.CanUndo;
            return JsonUtility.ToJson(snapshot);
        }

        private void OnMessage(string value) { message = value; Publish(); }
        private void OnProgressChanged() { profilePreview = null; Publish(); }
        private void OnWorldFocusChanged(WorldInteraction target) => Publish();
        private void Publish()
        {
            var json = CreateSnapshotJson();
            SnapshotProduced?.Invoke(json);
#if UNITY_WEBGL && !UNITY_EDITOR
            KompetenzhausEmitState(json);
#endif
        }

        private void OnDestroy()
        {
            if (game?.Progression != null) game.Progression.Changed -= OnProgressChanged;
            if (game?.Quiz != null) game.Quiz.Changed -= Publish;
            if (game != null) game.Message -= OnMessage;
            if (worldInteractor != null) worldInteractor.FocusChanged -= OnWorldFocusChanged;
        }
    }

    [Serializable] public sealed class BridgeCommand { public string command; public BridgePayload payload; }
    [Serializable] public sealed class BridgePayload
    {
        public string houseId, language, mode, moduleId, questId, code, roomId;
        public string bachelorDirection, specialisationId, optionalModuleCode;
        public bool enabled;
        public bool showRoofs;
        public int visibleFloor = 3;
        public int index, x, z, floor, rotation;
        public int width = 1, depth = 1;
        public AccessibilitySettings accessibility;
        public ArchitectureState architecture;
        public CompetenceChoice competenceChoice;
        public int[] preStageChecks;
    }
    [Serializable] public sealed class BridgeSnapshot
    {
        public int schemaVersion = 1;
        public bool ready;
        public string error, message, language, learningMode, selectedHouseId, viewMode;
        public bool futureCurriculum, directMasterEntry, worldReady;
        public int visibleFloor = 3;
        public bool showRoofs;
        public string interactionHint, focusedModuleId, focusedQuestId;
        public AccessibilitySettings accessibility;
        public string[] placedModuleIds, quizMasteredModuleIds, selfCheckedModuleIds, completedQuestIds, unlockedCosmeticIds;
        public int earnedExperience;
        public ModuleChoice[] moduleChoices;
        public CompetenceChoice[] competenceChoices;
        public int[] preStageChecks;
        public CompetenceProfileSnapshot profile;
        public FrameworkV2Profile frameworkProfile;
        public string frameworkError;
        public JourneySnapshot journey;
        public CompetenceProfileSnapshot profilePreview;
        public BridgeModuleState[] moduleStates;
        public BridgeQuestState[] questStates;
        public BridgeQuizState quiz;
        public ArchitectureState architecture;
        public bool canUndoArchitecture;
    }
    [Serializable] public sealed class BridgeModuleState
    {
        public string id, selectedCode;
        public bool canBuild, alreadyPlaced, positioned, needsSelfCheck, needsQuiz, needsMasterEntry;
        public string[] missingPrerequisiteIds;
    }
    [Serializable] public sealed class BridgeQuestState { public string id; public bool canStart, completed; }
    [Serializable] public sealed class BridgeQuizState
    {
        public string phase, moduleId, questId, questionId, type, prompt, explanation, correctAnswer;
        public string[] positions, options;
        public int selectedIndex, mastered, total, mistakes;
        public bool correct;
    }
}
