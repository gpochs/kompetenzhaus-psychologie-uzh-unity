using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;

namespace Kompetenzhaus.Competencies
{
    public static class FrameworkV2Projection
    {
        public static FrameworkV2Profile Create(FrameworkV2Catalog source, ContentCatalog curriculum, ProgressionService progression)
        {
            if (source == null || curriculum == null || progression == null) throw new ArgumentNullException("Framework projection inputs must be present.");
            var language = progression.Data.language;
            var english = language == "en";
            var built = new HashSet<string>(progression.Data.placedModuleIds);
            var candidates = curriculum.Document.modules.Select(slot => Candidate.Create(slot, source, progression)).ToArray();
            // Iterating canonical slots, rather than saved ids, makes a repeated id inert.
            var opportunities = candidates.Where(item => built.Contains(item.slot.id)).SelectMany(item =>
                item.design.proposal.objectives.Select(objective => Opportunity(item, objective, language))).ToArray();
            var byCriterion = opportunities.GroupBy(item => item.criterionId).ToDictionary(group => group.Key, group => group.ToArray());
            var competencies = source.Framework.competencies.Select(competency => new FrameworkV2CompetencyView
            {
                id = competency.id, domainId = competency.domainId, title = Text(competency.title, language),
                scope = Text(competency.scope, language), boundary = Text(competency.boundary, language),
                aiIntegration = competency.aiIntegration == null ? null : new FrameworkV2AiIntegrationView
                {
                    possibleRole = Text(competency.aiIntegration.possibleRole, language),
                    learnerResponsibility = Text(competency.aiIntegration.learnerResponsibility, language),
                    assessmentFocus = Text(competency.aiIntegration.assessmentFocus, language),
                    exampleTask = Text(competency.aiIntegration.exampleTask, language),
                    relatedCriterionIds = competency.aiIntegration.relatedCriterionIds.ToArray()
                },
                levelDescriptions = competency.levels.Select(level => new FrameworkV2LevelDescriptionView
                { level = level.level, description = Text(level.description, language) }).ToArray(),
                criteria = competency.criteria.Select(criterion =>
                {
                    var planned = byCriterion.TryGetValue(criterion.id, out var values) ? values : Array.Empty<FrameworkV2Opportunity>();
                    return new FrameworkV2CriterionView
                    {
                        id = criterion.id, competencyId = competency.id, title = Text(criterion.text, language),
                        opportunities = planned,
                        opportunityLevels = planned.Select(item => item.targetLevel).Distinct().OrderBy(level => level).ToArray(),
                        plannedModuleIds = planned.Select(item => item.moduleId).Distinct().ToArray()
                    };
                }).ToArray()
            }).ToArray();
            var nextCandidates = candidates.Where(item => !built.Contains(item.slot.id)).ToArray();
            return new FrameworkV2Profile
            {
                frameworkVersion = source.Framework.version, language = language,
                title = Text(source.Framework.title, language), status = Text(source.Framework.status, language),
                limitation = Text(source.Framework.limitation, language), careerNotice = Text(source.Careers.notice, language),
                milestoneRule = Text(source.Framework.milestoneRule, language),
                opportunityNotice = english
                    ? "Built modules show planned learning opportunities. Task levels describe the proposed work; personal competence is unassessed. Topic-dependent tasks still need an agreed topic and context."
                    : "Gebaute Module zeigen geplante Lerngelegenheiten. Aufgabenniveaus beschreiben die vorgeschlagene Arbeit; persönliche Kompetenzen sind nicht beurteilt. Themenabhängige Aufgaben brauchen noch ein abgestimmtes Thema und einen Kontext.",
                gamePracticeNotice = english
                    ? "Quiz and quest completion records practice in this game. It is not assessed evidence for module objectives or competence levels."
                    : "Quiz- und Questabschlüsse dokumentieren Übung im Spiel. Sie sind keine beurteilten Nachweise für Modulziele oder Kompetenzniveaus.",
                domains = source.Framework.domains.Select(item => Named(item, language)).ToArray(),
                contexts = source.Framework.contexts.Select(item => Named(item, language)).ToArray(),
                levels = source.Framework.levelDefinitions.Select(item => new FrameworkV2LevelView
                { level = item.level, label = item.label, title = Text(item.title, language), description = Text(item.description, language) }).ToArray(),
                studyMilestones = source.Framework.studyMilestones.Select(item => new FrameworkV2MilestoneView
                { id = item.id, title = Text(item.title, language), typicalSemesters = item.typicalSemesters }).ToArray(),
                futureCoverageRule = Text(source.Framework.futureCoverageRule, language),
                futureDesignTests = source.Framework.futureDesignTests.Select(item => Text(item, language)).ToArray(),
                futureLenses = source.Framework.futureLenses.Select(lens => new FrameworkV2LensView
                {
                    id = lens.id, title = Text(lens.title, language), description = Text(lens.description, language),
                    competencyIds = lens.competencyIds.ToArray(), criterionIds = lens.criterionIds.ToArray(), contextIds = lens.contextIds.ToArray(),
                    plannedCriterionIds = lens.criterionIds.Where(id => byCriterion.TryGetValue(id, out var planned) &&
                        planned.Any(opportunity => opportunity.contextIds.Intersect(lens.contextIds).Any())).ToArray()
                }).ToArray(),
                competencies = competencies,
                careers = source.Careers.roles.Select(role => new FrameworkV2RoleView
                {
                    id = role.id, title = Text(role.title, language), status = role.status, sourceUrl = role.sourceUrl,
                    facets = role.facets.Select(facet => new FrameworkV2FacetView
                    {
                        id = facet.id, title = Text(facet.title, language), activity = Text(facet.activity, language),
                        criterionIds = facet.criterionIds.ToArray(), optionalContext = facet.optionalContext, contextIds = facet.contextIds.ToArray(),
                        plannedCriterionIds = facet.criterionIds.Where(id => byCriterion.TryGetValue(id, out var planned) &&
                            planned.Any(item => MatchesContexts(item.contextIds, facet.contextIds))).ToArray(),
                        opportunityModuleIds = facet.criterionIds.Where(byCriterion.ContainsKey).SelectMany(id => byCriterion[id])
                            .Where(item => MatchesContexts(item.contextIds, facet.contextIds)).Select(item => item.moduleId).Distinct().ToArray(),
                        nextModules = NextModules(nextCandidates, new[] { facet }, byCriterion, progression, language, 3)
                    }).ToArray(),
                    nextModules = NextModules(nextCandidates, role.facets, byCriterion, progression, language, 5)
                }).ToArray(),
                gamePractice = new FrameworkV2PracticeView
                {
                    modules = progression.Data.quizMasteredModuleIds.Where(curriculum.HasModule).Distinct().Select(id => new FrameworkV2PractisedModule
                    { moduleId = id, moduleCode = progression.GetSelectedCode(id) }).ToArray(),
                    questIds = progression.Data.completedQuestIds.Where(id => Array.Exists(curriculum.Document.quests, quest => quest.id == id)).Distinct().ToArray()
                },
                selfReportedModuleIds = progression.Data.selfCheckedModuleIds.Where(curriculum.HasModule).Distinct().ToArray()
            };
        }

        private static FrameworkV2Opportunity Opportunity(Candidate item, FrameworkV2Objective objective, string language) => new()
        {
            moduleId = item.slot.id, moduleCode = item.code, moduleTitle = item.title.Get(language),
            houseId = item.slot.houseId, studyMilestoneId = item.slot.stageId,
            objectiveId = objective.id, criterionId = objective.criterionId, targetLevel = objective.targetLevel,
            contextIds = objective.contextIds.ToArray(), text = Text(objective.text, language), task = Text(objective.task, language),
            evidence = Text(objective.evidence, language), successCriteria = Text(objective.successCriteria, language),
            selectionStatus = item.selectionStatus, proposalStatus = item.design.proposal.status,
            topicRequired = item.design.proposal.topicRequired, individualisationRequired = item.design.proposal.individualisationRequired,
            openDecisions = Text(item.design.proposal.openDecisions, language), confirmed = false
        };

        private static FrameworkV2NextModule[] NextModules(Candidate[] candidates, FrameworkV2FacetDefinition[] facets,
            Dictionary<string, FrameworkV2Opportunity[]> planned, ProgressionService progression, string language, int limit)
        {
            var criterionContexts = facets.SelectMany(facet => facet.criterionIds.Select(id => (id, facet.contextIds)))
                .ToDictionary(item => item.id, item => item.contextIds);
            // This is navigation among available modules, not a fit score. Readiness
            // comes first; then new criterion opportunities and curriculum order.
            return candidates.Select(item => new
                {
                    item,
                    objectives = item.design.proposal.objectives.Where(objective => criterionContexts.TryGetValue(objective.criterionId, out var contexts) &&
                        MatchesContexts(objective.contextIds, contexts)).ToArray(),
                    eligibility = progression.GetEligibility(item.slot.id)
                }).Where(item => item.objectives.Length > 0)
                .OrderByDescending(item => item.eligibility.Allowed)
                .ThenBy(item => item.eligibility.MissingPrerequisiteIds.Length + (item.eligibility.NeedsMasterEntry ? 1 : 0))
                .ThenByDescending(item => item.objectives.Select(objective => objective.criterionId).Distinct().Count(id =>
                    !planned.TryGetValue(id, out var existing) || !existing.Any(opportunity => MatchesContexts(opportunity.contextIds, criterionContexts[id]))))
                .ThenBy(item => item.item.slot.stage).ThenBy(item => item.item.slot.id, StringComparer.Ordinal)
                .Take(limit).Select(item => new FrameworkV2NextModule
                {
                    moduleId = item.item.slot.id, moduleCode = item.item.code, title = item.item.title.Get(language),
                    houseId = item.item.slot.houseId, studyMilestoneId = item.item.slot.stageId,
                    selectionStatus = item.item.selectionStatus, proposalStatus = item.item.design.proposal.status,
                    criterionIds = item.objectives.Select(objective => objective.criterionId).Distinct().ToArray(),
                    opportunityLevels = item.objectives.Select(objective => objective.targetLevel).Distinct().OrderBy(value => value).ToArray(),
                    canBuild = item.eligibility.Allowed, needsSelfCheck = item.eligibility.NeedsSelfCheck,
                    needsQuiz = item.eligibility.NeedsQuiz, needsMasterEntry = item.eligibility.NeedsMasterEntry,
                    missingPrerequisiteIds = item.eligibility.MissingPrerequisiteIds.ToArray(),
                    topicRequired = item.item.design.proposal.topicRequired, individualisationRequired = item.item.design.proposal.individualisationRequired,
                    openDecisions = Text(item.item.design.proposal.openDecisions, language)
                }).ToArray();
        }

        private static string Text(LocalizedText value, string language) => value?.Get(language) ?? "";
        private static bool MatchesContexts(string[] actual, string[] required) => required.Length == 0 || actual.Intersect(required).Any();
        private static FrameworkV2NamedView Named(FrameworkV2NamedItem value, string language) => new() { id = value.id, title = Text(value.title, language) };

        private sealed class Candidate
        {
            public ModuleDefinition slot;
            public FrameworkV2ModuleDesign design;
            public string code, selectionStatus;
            public LocalizedText title;
            public static Candidate Create(ModuleDefinition slot, FrameworkV2Catalog source, ProgressionService progression)
            {
                var code = progression.GetSelectedCode(slot.id);
                var optional = slot.optionCodes != null && slot.optionCodes.Length > 0;
                return new Candidate
                {
                    slot = slot, code = code, design = source.Resolve(slot, code),
                    title = progression.GetContentForSlot(slot.id).title ?? source.Resolve(slot, code).baseline.title ?? new LocalizedText(),
                    selectionStatus = optional ? progression.Data.moduleChoices.Any(choice => choice.slotId == slot.id && choice.moduleCode == code)
                        ? "selected-option" : "default-option-unconfirmed" : "fixed-module"
                };
            }
        }
    }
}
