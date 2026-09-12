using System;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using NUnit.Framework;

namespace Kompetenzhaus.Tests
{
    public sealed class LearningFlowTests
    {
        [Test]
        public void FinalAnswerRetainsExplanationUntilExplicitContinue()
        {
            var session = new QuizSession(1);
            var completed = 0;
            session.Completed += () => completed++;
            session.Start(new[] { Question("a") }, false);
            Assert.That(session.Answer(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Feedback));
            Assert.That(session.CurrentQuestion.explanation.de, Is.EqualTo("Begründung a"));
            Assert.That(completed, Is.Zero);
            Assert.That(session.Answer(1), Is.False);
            Assert.That(session.Continue(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Complete));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(session.Continue(), Is.False);
        }

        [Test]
        public void WrongAnswerIsRequeuedAfterFeedbackAndCanBeMastered()
        {
            var session = new QuizSession(1);
            session.Start(new[] { Question("a"), Question("b") }, false);
            session.Answer(1);
            Assert.That(session.CurrentQuestion.id, Is.EqualTo("a"));
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Feedback));
            session.Continue();
            Assert.That(session.CurrentQuestion.id, Is.EqualTo("b"));
            session.Answer(0);
            session.Continue();
            Assert.That(session.CurrentQuestion.id, Is.EqualTo("a"));
            session.Answer(0);
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Feedback));
            session.Continue();
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Complete));
            Assert.That(session.Mistakes, Is.EqualTo(1));
        }

        [Test]
        public void FinalMissIsSeparatedFromRetryByAnAlreadySolvedQuestion()
        {
            var session = new QuizSession(1);
            session.Start(new[] { Question("a"), Question("b") }, false);
            session.Answer(0); session.Continue();
            session.Answer(1); session.Continue();
            Assert.That(session.CurrentQuestion.id, Is.EqualTo("a"));
            session.Answer(0); session.Continue();
            Assert.That(session.CurrentQuestion.id, Is.EqualTo("b"));
        }

        [Test]
        public void ShuffledDisplayAnswerMapsBackToAuthoritativeCorrectIndex()
        {
            var session = new QuizSession(12);
            session.Start(new[] { Question("a") });
            var correctDisplayIndex = -1;
            for (var i = 0; i < session.OptionOrder.Count; i++) if (session.OptionOrder[i] == 0) correctDisplayIndex = i;
            session.Answer(correctDisplayIndex);
            Assert.That(session.LastAnswerCorrect, Is.True);
        }

        [Test]
        public void RecommendedModuleDoesNotBecomeABuildPrerequisite()
        {
            var foundation = Module("a");
            var elective = Module("b");
            elective.recommendedIds = new[] { "a" };
            var progression = Service(new[] { foundation, elective });
            progression.Data.learningMode = LearningMode.Serious;
            progression.Data.selfCheckedModuleIds.Add("b");
            Assert.That(progression.GetEligibility("b").Allowed, Is.True);
            elective.prerequisiteIds = new[] { "a" };
            Assert.That(progression.GetEligibility("b").Allowed, Is.False);
        }

        [Test]
        public void OptionalModuleChoiceResetsOnlyItsOwnMasteryAndKeepsSlotPrerequisites()
        {
            var slot = Module("wp");
            slot.code = "option-a";
            slot.optionCodes = new[] { "option-a", "option-b" };
            slot.prerequisiteIds = new[] { "foundation" };
            var optional = Module("option:option-b"); optional.code = "option-b";
            var document = new ContentDocument { schemaVersion = 1, modules = new[] { Module("foundation"), slot }, optionalModules = new[] { optional } };
            var data = new ProgressData();
            data.learningMode = LearningMode.Serious;
            data.quizMasteredModuleIds.AddRange(new[] { "wp", "foundation" });
            var progression = new ProgressionService(new ContentCatalog(document), data, () => { });
            Assert.That(progression.SetModuleChoice("wp", "option-b"), Is.True);
            Assert.That(progression.GetContentForSlot("wp").code, Is.EqualTo("option-b"));
            Assert.That(data.quizMasteredModuleIds, Does.Not.Contain("wp"));
            Assert.That(data.quizMasteredModuleIds, Does.Contain("foundation"));
            Assert.That(progression.GetEligibility("wp").MissingPrerequisiteIds, Is.EqualTo(new[] { "foundation" }));
        }

        [Test]
        public void QuestRewardIsGrantedOnceAndOnlyAfterPrerequisites()
        {
            var quests = new[]
            {
                new QuestDefinition { id = "first", reward = new QuestReward { cosmeticId = "lamp", experience = 40 } },
                new QuestDefinition { id = "second", prerequisiteQuestIds = new[] { "first" } }
            };
            var document = new ContentDocument { schemaVersion = 1, modules = new[] { Module("a") }, quests = quests };
            var data = new ProgressData();
            data.learningMode = LearningMode.Serious;
            var progression = new ProgressionService(new ContentCatalog(document), data, () => { });
            Assert.That(progression.CanStartQuest("second"), Is.False);
            progression.MarkQuestCompleted("first");
            progression.MarkQuestCompleted("first");
            Assert.That(data.earnedExperience, Is.EqualTo(40));
            Assert.That(data.unlockedCosmeticIds.Count, Is.EqualTo(1));
            Assert.That(progression.CanStartQuest("second"), Is.True);
        }

        [Test]
        public void ConfirmingDefaultChoicePersistsSelectionWithoutRemovingPractice()
        {
            var slot = Module("wp"); slot.code = "option-a"; slot.optionCodes = new[] { "option-a", "option-b" };
            var data = new ProgressData(); data.quizMasteredModuleIds.Add("wp"); data.selfCheckedModuleIds.Add("wp");
            var saves = 0;
            var service = new ProgressionService(new ContentCatalog(new ContentDocument { schemaVersion = 1, modules = new[] { slot } }), data, () => saves++);
            Assert.That(service.SetModuleChoice("wp", "option-a"), Is.True);
            Assert.That(data.moduleChoices.Single().moduleCode, Is.EqualTo("option-a"));
            Assert.That(data.quizMasteredModuleIds, Does.Contain("wp"));
            Assert.That(data.selfCheckedModuleIds, Does.Contain("wp"));
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(service.SetModuleChoice("wp", "option-a"), Is.True);
            Assert.That(saves, Is.EqualTo(1));
        }

        [Test]
        public void FirstBuildPinsTheDisplayedOptionalCodeAgainstFutureDefaultChanges()
        {
            var slot = Module("wp"); slot.code = "option-a"; slot.optionCodes = new[] { "option-a", "option-b" };
            var data = new ProgressData { learningMode = LearningMode.Free };
            var service = new ProgressionService(new ContentCatalog(new ContentDocument { schemaVersion = 1, modules = new[] { slot } }), data, () => { });
            Assert.That(service.TryPlace("wp", out _), Is.True);
            slot.code = "option-b";
            Assert.That(service.GetSelectedCode("wp"), Is.EqualTo("option-a"));
            Assert.That(data.moduleChoices.Count, Is.EqualTo(1));
        }

        private static QuizQuestion Question(string id) => new QuizQuestion
        {
            id = id, prompt = Text("Frage " + id), explanation = Text("Begründung " + id),
            options = new[] { Text("richtig"), Text("falsch") }, correctIndex = 0
        };
        private static LocalizedText Text(string value) => new LocalizedText { de = value, en = value };
        private static ModuleDefinition Module(string id) => new ModuleDefinition { id = id, code = id, title = Text(id), stageId = "1", houseId = "bsc" };
        private static ProgressionService Service(ModuleDefinition[] modules) => new ProgressionService(
            new ContentCatalog(new ContentDocument { schemaVersion = 1, modules = modules }), new ProgressData(), () => { });
    }
}
