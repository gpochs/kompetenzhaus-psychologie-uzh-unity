using System;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Competencies.Tests
{
    public sealed class FrameworkV2ProjectionTests
    {
        private const int ExpectedCompetencies = 16, ExpectedCriteria = 48;
        private ContentCatalog curriculum;
        private FrameworkV2Catalog source;
        private ProgressData data;
        private ProgressionService progression;
        [SetUp] public void Setup()
        {
            curriculum = ContentCatalog.LoadResource();
            source = FrameworkV2Catalog.LoadResources(curriculum);
            data = new ProgressData();
            progression = new ProgressionService(curriculum, data, () => Assert.Fail("Projection must not save."));
        }
        private FrameworkV2Profile Profile() => FrameworkV2Projection.Create(source, curriculum, progression);
        private void BuildAll() => data.placedModuleIds.AddRange(curriculum.Document.modules.Select(item => item.id));
        private static FrameworkV2CriterionView[] Criteria(FrameworkV2Profile profile) => profile.competencies.SelectMany(item => item.criteria).ToArray();
        private static string CriterionJson(FrameworkV2Profile profile) => string.Join("\n", Criteria(profile).Select(item => JsonUtility.ToJson(item)));

        [Test] public void EmptyProfileIsUnassessedRatherThanZeroAbility()
        {
            var profile = Profile();
            Assert.That(profile.assessmentStatus, Is.EqualTo("unassessed"));
            Assert.That(profile.hasAssessedEvidence, Is.False);
            Assert.That(Criteria(profile), Has.Length.EqualTo(ExpectedCriteria));
            Assert.That(Criteria(profile).All(item => item.assessmentStatus == "unassessed" && item.opportunities.Length == 0 && item.opportunityLevels.Length == 0), Is.True);
            Assert.That(profile.levels.Select(item => item.level), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(profile.studyMilestones, Has.Length.EqualTo(4));
        }

        [Test] public void CreditsQuizAndRewardsDoNotPromoteOpportunityOrAssessmentLevels()
        {
            BuildAll();
            var before = Profile();
            foreach (var module in curriculum.Document.modules) module.ects = 100000;
            foreach (var design in source.Learning.modules) design.baseline.ects = 100000;
            data.earnedExperience = int.MaxValue;
            data.quizMasteredModuleIds.AddRange(curriculum.Document.modules.Select(item => item.id));
            var after = Profile();
            Assert.That(CriterionJson(after), Is.EqualTo(CriterionJson(before)));
            Assert.That(after.gamePractice.modules.Length, Is.EqualTo(curriculum.Document.modules.Length));
            Assert.That(after.hasAssessedEvidence, Is.False);
            Assert.That(Criteria(after).All(item => item.assessmentStatus == "unassessed"), Is.True);
        }

        [Test] public void SelectedOptionalModuleReplacesDefaultObjectivesWithoutStacking()
        {
            data.placedModuleIds.Add("wp");
            var defaultObjectives = Criteria(Profile()).SelectMany(item => item.opportunities).Select(item => item.objectiveId).ToArray();
            data.moduleChoices.Add(new ModuleChoice { slotId = "wp", moduleCode = "06SM200-511" });
            var selected = Criteria(Profile()).SelectMany(item => item.opportunities).ToArray();
            var expected = source.Resolve(curriculum.GetModule("wp"), "06SM200-511").proposal.objectives;
            Assert.That(selected.Select(item => item.objectiveId), Is.EquivalentTo(expected.Select(item => item.id)));
            Assert.That(selected.All(item => item.moduleId == "wp" && item.moduleCode == "06SM200-511" && item.selectionStatus == "selected-option"), Is.True);
            Assert.That(selected.Select(item => item.objectiveId).Intersect(defaultObjectives), Is.Empty);
        }

        [Test] public void MentoringOptionUsesItsOwnTasksAndKeepsStructuralSlotIdentity()
        {
            data.placedModuleIds.Add("s01c");
            data.moduleChoices.Add(new ModuleChoice { slotId = "s01c", moduleCode = "10SMSTS-505" });
            var opportunities = Criteria(Profile()).SelectMany(item => item.opportunities).ToArray();
            var expected = source.Resolve(curriculum.GetModule("s01c"), "10SMSTS-505").proposal.objectives;
            Assert.That(opportunities.Select(item => item.objectiveId), Is.EquivalentTo(expected.Select(item => item.id)));
            Assert.That(opportunities.All(item => item.moduleId == "s01c" && item.moduleCode == "10SMSTS-505"), Is.True);
        }

        [Test] public void RepeatedSavedModuleIdsDoNotDuplicateCanonicalCriteriaOrObjectives()
        {
            BuildAll();
            var once = Profile();
            BuildAll();
            var repeated = Profile();
            Assert.That(CriterionJson(repeated), Is.EqualTo(CriterionJson(once)));
            Assert.That(Criteria(repeated).Select(item => item.id).Distinct().Count(), Is.EqualTo(ExpectedCriteria));
            Assert.That(Criteria(repeated).All(item => item.plannedModuleIds.Distinct().Count() == item.plannedModuleIds.Length), Is.True);
        }

        [Test] public void ThreeTaskLevelsRemainProposedOpportunitiesWithTopicConditions()
        {
            BuildAll();
            var profile = Profile();
            var opportunities = Criteria(profile).SelectMany(item => item.opportunities).ToArray();
            Assert.That(opportunities.Select(item => item.targetLevel).Distinct().OrderBy(item => item), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(opportunities.All(item => !item.confirmed && item.proposalStatus == "design-proposal"), Is.True);
            Assert.That(opportunities.Any(item => item.topicRequired), Is.True);
            Assert.That(opportunities.All(item => item.contextIds.Length > 0 && !string.IsNullOrWhiteSpace(item.task) && !string.IsNullOrWhiteSpace(item.evidence)), Is.True);
            Assert.That(opportunities.Any(item => item.selectionStatus == "default-option-unconfirmed"), Is.True);
            Assert.That(profile.hasAssessedEvidence, Is.False);
        }

        [Test] public void TwelveRolesUseUniqueCriteriaAndSuggestUnbuiltModules()
        {
            data.placedModuleIds.Add("wp");
            var profile = Profile();
            Assert.That(profile.careers, Has.Length.EqualTo(12));
            foreach (var role in profile.careers)
            {
                var ids = role.facets.SelectMany(item => item.criterionIds).ToArray();
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), role.id);
                Assert.That(role.nextModules.All(item => item.moduleId != "wp"), Is.True);
                Assert.That(role.nextModules.Select(item => item.moduleId).Distinct().Count(), Is.EqualTo(role.nextModules.Length));
            }
            Assert.That(profile.careers.Any(role => role.nextModules.Length > 0), Is.True);
        }

        [Test] public void FuturePerspectivesRemainVisibleViewsOfCanonicalCriteria()
        {
            BuildAll();
            var profile = Profile();
            Assert.That(profile.futureLenses, Has.Length.EqualTo(14));
            var canonical = Criteria(profile).Select(item => item.id).ToHashSet();
            Assert.That(profile.futureLenses.All(lens => lens.criterionIds.All(canonical.Contains)), Is.True);
            Assert.That(profile.futureLenses.All(lens => lens.plannedCriterionIds.All(lens.criterionIds.Contains)), Is.True);
            var byCriterion = Criteria(profile).ToDictionary(item => item.id);
            Assert.That(profile.futureLenses.All(lens => lens.plannedCriterionIds.All(id =>
                byCriterion[id].opportunities.Any(opportunity => opportunity.contextIds.Intersect(lens.contextIds).Any()))), Is.True);
            Assert.That(profile.competencies, Has.Length.EqualTo(ExpectedCompetencies));
            Assert.That(Criteria(profile), Has.Length.EqualTo(ExpectedCriteria));
            Assert.That(profile.futureCoverageRule, Is.Not.Empty);
        }

        [Test] public void InvalidRepeatedCareerCriterionIsRejectedBeforeProjection()
        {
            var roles = JsonUtility.FromJson<FrameworkV2CareersDefinition>(JsonUtility.ToJson(source.Careers));
            var role = roles.roles[0];
            role.facets[1].criterionIds = role.facets[1].criterionIds.Append(role.facets[0].criterionIds[0]).ToArray();
            Assert.Throws<ArgumentException>(() => new FrameworkV2Catalog(source.Framework, roles, source.Learning, curriculum));
        }

        [Test] public void OptionalDigitalCareerFacetsRetainTheirContextAndDoNotClaimUnrelatedPractice()
        {
            BuildAll();
            var profile = Profile();
            var criteria = Criteria(profile).ToDictionary(item => item.id);
            var optional = profile.careers.SelectMany(role => role.facets).Where(facet => facet.optionalContext).ToArray();
            Assert.That(optional, Has.Length.EqualTo(12));
            foreach (var facet in optional)
            {
                Assert.That(facet.contextIds, Is.EquivalentTo(new[] { "with-ai", "about-ai" }));
                Assert.That(facet.plannedCriterionIds.All(id => criteria[id].opportunities.Any(item => item.contextIds.Intersect(facet.contextIds).Any())), Is.True);
            }
        }

        [Test] public void ResolvedDefaultCurriculumProvidesAnOpportunityForEveryCanonicalCriterion()
        {
            BuildAll();
            var missing = Criteria(Profile()).Where(item => item.opportunities.Length == 0).Select(item => item.id).ToArray();
            Assert.That(missing, Is.Empty, "Check actual default-option replacement, not the union of incompatible alternatives.");
        }

        [Test] public void EveryCompetencyExplainsAiRoleAndRetainedLearnerResponsibility()
        {
            data.language = "en";
            var profile = Profile();
            var criteria = Criteria(profile).Select(item => item.id).ToHashSet();
            Assert.That(profile.frameworkVersion, Is.EqualTo("2.2.0-draft"));
            foreach (var competency in profile.competencies)
            {
                var integration = competency.aiIntegration;
                Assert.That(integration, Is.Not.Null, competency.id);
                Assert.That(integration.possibleRole, Is.Not.Empty);
                Assert.That(integration.learnerResponsibility, Is.Not.Empty);
                Assert.That(integration.assessmentFocus, Is.Not.Empty);
                Assert.That(integration.exampleTask, Is.Not.Empty);
                Assert.That(integration.relatedCriterionIds.Length, Is.GreaterThan(0));
                Assert.That(integration.relatedCriterionIds.All(criteria.Contains), Is.True);
                var definition = source.Framework.competencies.Single(item => item.id == competency.id);
                Assert.That(definition.aiIntegration.possibleRole.en, Is.Not.Empty);
                Assert.That(definition.aiIntegration.learnerResponsibility.en, Is.Not.Empty);
                Assert.That(definition.aiIntegration.assessmentFocus.en, Is.Not.Empty);
                Assert.That(definition.aiIntegration.exampleTask.en, Is.Not.Empty);
                Assert.That(integration.possibleRole, Is.EqualTo(definition.aiIntegration.possibleRole.Get("en")));
            }
        }

        [Test] public void AiAcrossKeepsEveryCriterionOnceAndLocalisesAllPhases()
        {
            data.language = "en";
            var profile = Profile();
            var ai = profile.aiAcrossCurriculum;
            Assert.That(ai.domainRows.SelectMany(r => r.criterionIds), Is.EquivalentTo(Criteria(profile).Select(c => c.id)));
            Assert.That(ai.principle, Is.EqualTo(source.Framework.aiAcrossCurriculum.principle.en));
            Assert.That(ai.phasePolicy.noWholeModuleMandate, Is.True);
            Assert.That(ai.phasePolicy.phases.Select(p => p.contextId), Is.EquivalentTo(new[] { "without-ai", "with-ai", "about-ai" }));
            foreach (var phase in ai.phasePolicy.phases)
                Assert.That(phase.assessmentUse, Is.EqualTo(source.Framework.aiAcrossCurriculum.phasePolicy.phases.Single(p => p.contextId == phase.contextId).assessmentUse.en));
            Assert.That(ai.domainRows.Single(r => r.domainId == "R").criterionIds, Does.Contain("R4.2"));
            Assert.That(ai.domainRows.Single(r => r.domainId == "T").criterionIds, Does.Not.Contain("R4.2"));
            Assert.That(ai.examples.SelectMany(e => e.phases).All(p => !string.IsNullOrWhiteSpace(p.evidence)), Is.True);
        }

        [Test] public void AiViewCannotMoveScientificCriteriaIntoTechnicalDomain()
        {
            var copy = JsonUtility.FromJson<FrameworkV2Definition>(JsonUtility.ToJson(source.Framework));
            var row = copy.aiAcrossCurriculum.domainRows.Single(r => r.domainId == "T");
            row.criterionIds = row.criterionIds.Append("R4.2").ToArray();
            Assert.Throws<ArgumentException>(() => new FrameworkV2Catalog(copy, source.Careers, source.Learning, curriculum));
        }

        [Test] public void AiViewCannotSilentlyRemoveIndependentReasoningPhase()
        {
            var copy = JsonUtility.FromJson<FrameworkV2Definition>(JsonUtility.ToJson(source.Framework));
            copy.aiAcrossCurriculum.phasePolicy.phases = copy.aiAcrossCurriculum.phasePolicy.phases.Where(p => p.contextId != "without-ai").ToArray();
            Assert.Throws<ArgumentException>(() => new FrameworkV2Catalog(copy, source.Careers, source.Learning, curriculum));
        }
    }
}
