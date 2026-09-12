using System.Linq;
using System.Text.RegularExpressions;
using Kompetenzhaus.Competencies;
using Kompetenzhaus.Content;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Tests
{
    public sealed class QuizPresentationTests
    {
        private static readonly string[] Adapted = { "06SM200-400:q2", "06SM200-103:q2", "06SM200-502:q3" };

        [TestCase("06SM200-400:q2", 0)]
        [TestCase("06SM200-103:q2", 2)]
        [TestCase("06SM200-502:q3", 1)]
        public void LegacyLabelsAreDisplayOnlyAndFinalFeedbackKeepsTheSameAnswer(string id, int answer)
        {
            var question = ContentCatalog.LoadResource().Document.quizBanks.SelectMany(bank => bank.questions).Single(item => item.id == id);
            var before = JsonUtility.ToJson(question);
            var session = new QuizSession(9);
            session.Start(new[] { question }, false);
            Assert.That(session.Answer(answer), Is.True);
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Feedback));
            foreach (var language in new[] { "de", "en" })
            {
                var prompt = QuizPresentation.Prompt(session.CurrentQuestion, language);
                var explanation = QuizPresentation.Explanation(session.CurrentQuestion, language);
                Assert.That(prompt, Is.Not.Empty);
                Assert.That(explanation, Is.Not.Empty);
                Assert.That(Regex.IsMatch(prompt + explanation, @"\b(?:Fa\d+|KI[1-6]|Fu[1-3])\b"), Is.False);
                Assert.That(prompt, Is.Not.EqualTo(question.prompt.Get(language)));
                Assert.That(explanation, Is.Not.EqualTo(question.explanation.Get(language)));
            }
            Assert.That(question.correctIndex, Is.EqualTo(answer));
            Assert.That(JsonUtility.ToJson(question), Is.EqualTo(before), "Presentation must not rewrite historical quiz data.");
            Assert.That(session.Continue(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(QuizPhase.Complete));
        }

        [Test]
        public void EveryOtherInheritedAndNewQuestionRetainsItsExactDisplayText()
        {
            var document = ContentCatalog.LoadResource().Document;
            var questions = document.quizBanks.SelectMany(bank => bank.questions)
                .Concat(document.quests.SelectMany(quest => quest.questions)).Where(question => !Adapted.Contains(question.id)).ToArray();
            Assert.That(questions.Length, Is.EqualTo(149)); // 127 inherited + 22 authored.
            foreach (var question in questions)
                foreach (var language in new[] { "de", "en" })
                {
                    Assert.That(QuizPresentation.Prompt(question, language), Is.EqualTo(question.prompt.Get(language)), question.id);
                    Assert.That(QuizPresentation.Explanation(question, language), Is.EqualTo(question.explanation.Get(language)), question.id);
                }
            Assert.That(QuizPresentation.Prompt(null, "en"), Is.Empty);
            Assert.That(QuizPresentation.Explanation(null, "de"), Is.Empty);
        }

        [Test]
        public void CanonicalQuestTopicsSurviveImportWithoutCreatingAssessedAbility()
        {
            var catalog = ContentCatalog.LoadResource();
            var framework = FrameworkV2Catalog.LoadResources(catalog);
            var owners = framework.Framework.competencies.SelectMany(competency => competency.criteria.Select(criterion => new { criterion.id, owner = competency.id }))
                .ToDictionary(item => item.id, item => item.owner);
            foreach (var quest in catalog.Document.quests)
            {
                Assert.That(quest.frameworkVersion, Is.EqualTo(framework.Framework.version));
                Assert.That(quest.alignmentMeaning, Is.EqualTo("game-practice-topics-not-assessed-competence"));
                Assert.That(quest.criterionIds.Length, Is.GreaterThan(0));
                Assert.That(quest.competencyIds, Is.EquivalentTo(quest.criterionIds.Select(id => owners[id]).Distinct()));
            }
            var data = new ProgressData();
            var progression = new ProgressionService(catalog, data, () => { });
            var before = FrameworkV2Projection.Create(framework, catalog, progression);
            data.completedQuestIds.AddRange(catalog.Document.quests.Select(quest => quest.id));
            var after = FrameworkV2Projection.Create(framework, catalog, progression);
            Assert.That(after.gamePractice.questIds.Length, Is.EqualTo(11));
            Assert.That(after.hasAssessedEvidence, Is.False);
            before.gamePractice = after.gamePractice = null;
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)), "Quest completion belongs only to game practice.");
        }
    }
}
