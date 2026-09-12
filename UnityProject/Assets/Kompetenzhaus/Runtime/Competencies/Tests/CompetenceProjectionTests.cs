using System;
using System.IO;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Competencies.Tests
{
    public sealed class CompetenceProjectionTests
    {
        [Serializable] private sealed class FixtureDocument { public string[] competencyIds, careerIds; public Fixture[] cases; }
        [Serializable] private sealed class Fixture
        {
            public string id;
            public string[] placedIds;
            public CompetenceChoice[] choices;
            public ModuleChoice[] moduleChoices;
            public int[] preStageChecks, stages, fits;
            public double[] scores, maxima;
        }

        [Test]
        public void ProjectionMatchesOriginalJavaScriptAcrossSixIndependentScenarios()
        {
            var path = Path.Combine(Application.dataPath, "Kompetenzhaus/Runtime/Competencies/Tests/Fixtures/legacy-projection.json");
            var fixtures = JsonUtility.FromJson<FixtureDocument>(File.ReadAllText(path));
            var catalog = ContentCatalog.LoadResource();
            foreach (var fixture in fixtures.cases)
            {
                var data = new ProgressData();
                data.placedModuleIds.AddRange(fixture.placedIds);
                data.moduleChoices.AddRange(fixture.moduleChoices);
                var progression = new ProgressionService(catalog, data, () => Assert.Fail("Projection must never persist."));
                var before = JsonUtility.ToJson(data);
                var result = CompetenceProjection.Create(catalog, progression, fixture.choices, fixture.preStageChecks);
                for (var i = 0; i < fixtures.competencyIds.Length; i++)
                {
                    var value = result.competencies.Single(c => c.id == fixtures.competencyIds[i]);
                    Assert.That(value.score, Is.EqualTo(fixture.scores[i]).Within(0.0000001), fixture.id + " score " + value.id);
                    Assert.That(value.maximum, Is.EqualTo(fixture.maxima[i]).Within(0.0000001), fixture.id + " maximum " + value.id);
                    Assert.That(value.stage, Is.EqualTo(fixture.stages[i]), fixture.id + " stage " + value.id);
                }
                for (var i = 0; i < fixtures.careerIds.Length; i++)
                    Assert.That(result.careers.Single(c => c.id == fixtures.careerIds[i]).fitPercent, Is.EqualTo(fixture.fits[i]), fixture.id + " fit " + fixtures.careerIds[i]);
                Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before), "Projection must never mutate progress.");
                if (fixture.id == "empty") Assert.That(result.careers.All(c => !c.hasEvidence), Is.True);
                if (fixture.id == "clinical-mentoring") Assert.That(result.credits.Single(c => c.houseId == "msc").selectedEcts, Is.EqualTo(122));
            }
        }

        [Test]
        public void SelectionRejectsForeignModuleTopicAndQuestionCombinations()
        {
            Assert.That(CompetenceProjection.ValidateChoice(new CompetenceChoice { moduleId = "003", specialisationId = "HEA" }, out _), Is.False);
            Assert.That(CompetenceProjection.ValidateChoice(new CompetenceChoice { moduleId = "s04", specialisationId = "HEA" }, out _), Is.True);
            Assert.That(CompetenceProjection.ValidateChoice(new CompetenceChoice { moduleId = "BA", topicId = "missing", thesisQuestionId = "BAk1" }, out _), Is.False);
        }

        [Test]
        public void WhatIfChangesOnlyCopiesAndOnlyAlreadyPlacedChoices()
        {
            var catalog = ContentCatalog.LoadResource(); var data = new ProgressData();
            data.placedModuleIds.AddRange(new[] { "s04", "s05", "wp", "s11", "BA" });
            data.competenceChoices.Add(new CompetenceChoice { moduleId = "s04", specialisationId = "HEA" });
            var service = new ProgressionService(catalog, data, () => Assert.Fail("Preview must not save."));
            var before = JsonUtility.ToJson(data);
            var preview = CompetenceProjection.SimulateChoiceProfile(catalog, service, "klin", "DeNC", "06SM200-511");
            Assert.That(preview.isPreview, Is.True);
            Assert.That(preview.choices.masterCounts.Single(count => count.id == "DeNC").count, Is.EqualTo(2));
            Assert.That(preview.choices.bachelorDirection, Is.EqualTo("klin"));
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
            Assert.That(() => CompetenceProjection.SimulateChoiceProfile(catalog, service, "invented"), Throws.ArgumentException);
        }

        [Test]
        public void QuizMasteryDoesNotCreateCurriculumVolumeAndPrestageDoesNotCreateStages()
        {
            var catalog = ContentCatalog.LoadResource();
            var data = new ProgressData();
            data.quizMasteredModuleIds.AddRange(catalog.Document.modules.Select(m => m.id));
            var service = new ProgressionService(catalog, data, () => { });
            var empty = CompetenceProjection.Create(catalog, service);
            Assert.That(empty.competencies.All(c => c.score == 0 && c.stage == 0), Is.True);
            var started = CompetenceProjection.Create(catalog, service, preStageChecks: new[] { 0, 1, 2, 3 });
            Assert.That(started.competencies.All(c => c.stage == 0), Is.True);
            Assert.That(started.competencies.Single(c => c.id == "Fa4").ratio, Is.EqualTo(0.025).Within(0.0000001));
            foreach (var axis in started.radar)
            {
                var members = started.competencies.Where(c => axis.competencyIds.Contains(c.id)).ToArray();
                Assert.That(axis.ratio, Is.EqualTo(members.Sum(c => c.score) / members.Sum(c => c.maximum)).Within(0.0000001));
            }
        }
    }
}
