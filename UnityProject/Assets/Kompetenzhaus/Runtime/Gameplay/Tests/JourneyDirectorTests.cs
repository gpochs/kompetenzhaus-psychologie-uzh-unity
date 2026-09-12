using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Gameplay.Tests
{
    public sealed class JourneyDirectorTests
    {
        [Test]
        public void GuidedMasterDetoursToAvailablePredecessorWithoutChangingProgress()
        {
            var catalog = Catalog();
            var data = new ProgressData { learningMode = LearningMode.Serious, selectedHouseId = "msc" };
            var progression = new ProgressionService(catalog, data, () => Assert.Fail("Guidance must not save."));
            var before = JsonUtility.ToJson(data);
            var result = JourneyDirector.Create(catalog, progression);
            Assert.That(result.questId, Is.EqualTo("foundation"));
            Assert.That(result.prerequisiteDetour, Is.True);
            Assert.That(result.houseId, Is.EqualTo("msc"));
            Assert.That(result.total, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
        }

        [Test]
        public void DirectMasterEntryStartsWithMasterAndSandboxKeepsTheRouteOptional()
        {
            var catalog = Catalog();
            var data = new ProgressData { learningMode = LearningMode.Serious, selectedHouseId = "all", directMasterEntry = true };
            var progression = new ProgressionService(catalog, data, () => Assert.Fail("Guidance must not save."));
            Assert.That(JourneyDirector.Create(catalog, progression).questId, Is.EqualTo("diagnostics"));
            data.directMasterEntry = false;
            data.learningMode = LearningMode.Free;
            data.selectedHouseId = "msc";
            Assert.That(JourneyDirector.Create(catalog, progression).questId, Is.EqualTo("diagnostics"));
            Assert.That(progression.CanStartQuest("research"), Is.True);
        }

        [Test]
        public void CompletedRouteOffersExplorationAndDoesNotCountSideQuests()
        {
            var catalog = Catalog();
            var data = new ProgressData { selectedHouseId = "bsc" };
            data.completedQuestIds.AddRange(new[] { "foundation", "bachelor" });
            var progression = new ProgressionService(catalog, data, () => Assert.Fail("Guidance must not save."));
            var result = JourneyDirector.Create(catalog, progression);
            Assert.That(result.complete, Is.True);
            Assert.That(result.questId, Is.Null);
            Assert.That(result.completed, Is.EqualTo(2));
            Assert.That(result.total, Is.EqualTo(2));
            Assert.That(result.purpose, Does.Contain("gestalte dein Haus"));
        }

        private static ContentCatalog Catalog() => new(new ContentDocument
        {
            schemaVersion = 1,
            modules = new[] { new ModuleDefinition { id = "module", code = "module", stageId = "1" } },
            rules = new ContentRules { allowDirectMasterEntry = true },
            quests = new[]
            {
                Quest("foundation", "1"), Quest("bachelor", "2", "foundation"),
                Quest("diagnostics", "3", "bachelor"), Quest("research", "4", "diagnostics"),
                new QuestDefinition { id = "optional", type = "side", stageId = "1" }
            }
        });
        private static QuestDefinition Quest(string id, string stage, params string[] prerequisites) => new()
        { id = id, type = "main", stageId = stage, prerequisiteQuestIds = prerequisites, title = new LocalizedText { de = id } };
    }
}
