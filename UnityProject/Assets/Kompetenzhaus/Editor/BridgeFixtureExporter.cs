using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kompetenzhaus.Competencies;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using UnityEditor;
using UnityEngine;

namespace Kompetenzhaus.Editor
{
    // Generates diagnostic DTOs from the real C# projection and source-golden states.
    // It neither creates a game scene nor reads/writes any player's PlayerPrefs.
    public static class BridgeFixtureExporter
    {
        [MenuItem("Kompetenzhaus/Export browser QA snapshots")]
        public static void Export()
        {
            var catalog = ContentCatalog.LoadResource();
            var path = Path.Combine(Application.dataPath, "Kompetenzhaus/Runtime/Competencies/Tests/Fixtures/legacy-projection.json");
            var fixtures = JsonUtility.FromJson<FixtureDocument>(File.ReadAllText(path));
            var output = Path.Combine(ProjectTools.ProjectRoot, "Logs", "qa-snapshots");
            Directory.CreateDirectory(output);
            foreach (var fixture in fixtures.cases)
            {
                if (!Regex.IsMatch(fixture.id ?? "", "^[a-z0-9-]+$")) throw new InvalidOperationException("Invalid fixture id.");
                var data = new ProgressData();
                data.placedModuleIds.AddRange(fixture.placedIds ?? Array.Empty<string>());
                data.moduleChoices.AddRange(fixture.moduleChoices ?? Array.Empty<ModuleChoice>());
                data.competenceChoices.AddRange(fixture.choices ?? Array.Empty<CompetenceChoice>());
                data.preStageChecks.AddRange(fixture.preStageChecks ?? Array.Empty<int>());
                var progress = new ProgressionService(catalog, data, () => throw new InvalidOperationException("Diagnostic snapshots cannot save progress."));
                var snapshot = new BridgeSnapshot
                {
                    ready = true, error = "", message = "C# QA fixture: " + fixture.id,
                    language = data.language, learningMode = data.learningMode.ToString(), selectedHouseId = data.selectedHouseId,
                    viewMode = ViewMode.BirdView.ToString(), worldReady = false,
                    visibleFloor = 3, showRoofs = false, interactionHint = "", focusedModuleId = "", focusedQuestId = "",
                    futureCurriculum = data.futureCurriculum, directMasterEntry = data.directMasterEntry,
                    accessibility = data.accessibility, placedModuleIds = data.placedModuleIds.ToArray(),
                    quizMasteredModuleIds = data.quizMasteredModuleIds.ToArray(), selfCheckedModuleIds = data.selfCheckedModuleIds.ToArray(),
                    completedQuestIds = data.completedQuestIds.ToArray(), unlockedCosmeticIds = data.unlockedCosmeticIds.ToArray(),
                    earnedExperience = data.earnedExperience, moduleChoices = data.moduleChoices.ToArray(),
                    competenceChoices = data.competenceChoices.ToArray(), preStageChecks = data.preStageChecks.ToArray(),
                    profile = CompetenceProjection.Create(catalog, progress, data.competenceChoices, data.preStageChecks),
                    architecture = data.architecture, canUndoArchitecture = false,
                    quiz = new BridgeQuizState
                    {
                        phase = "Idle", moduleId = "", questId = "", questionId = "", type = "", prompt = "", explanation = "", correctAnswer = "",
                        positions = Array.Empty<string>(), options = Array.Empty<string>(), selectedIndex = -1
                    },
                    moduleStates = catalog.Document.modules.Select(module =>
                    {
                        var eligibility = progress.GetEligibility(module.id);
                        return new BridgeModuleState
                        {
                            id = module.id, selectedCode = progress.GetSelectedCode(module.id), canBuild = eligibility.Allowed,
                            alreadyPlaced = eligibility.AlreadyPlaced, positioned = false,
                            missingPrerequisiteIds = eligibility.MissingPrerequisiteIds, needsSelfCheck = eligibility.NeedsSelfCheck,
                            needsQuiz = eligibility.NeedsQuiz, needsMasterEntry = eligibility.NeedsMasterEntry
                        };
                    }).ToArray(),
                    questStates = catalog.Document.quests.Select(quest => new BridgeQuestState
                    { id = quest.id, canStart = progress.CanStartQuest(quest.id), completed = false }).ToArray()
                };
                File.WriteAllText(Path.Combine(output, fixture.id + ".json"), JsonUtility.ToJson(snapshot, true));
            }
            Debug.Log($"[Kompetenzhaus] QA_SNAPSHOTS_COMPLETE count={fixtures.cases.Length} directory={output}");
        }

        [Serializable] private sealed class FixtureDocument { public Fixture[] cases; }
        [Serializable] private sealed class Fixture
        {
            public string id;
            public string[] placedIds;
            public ModuleChoice[] moduleChoices;
            public CompetenceChoice[] choices;
            public int[] preStageChecks;
        }
    }
}
