using System;
using Kompetenzhaus.Content;

namespace Kompetenzhaus.Competencies
{
    [Serializable] public sealed class FrameworkV2Definition
    {
        public string schema, version;
        public int schemaVersion;
        public LocalizedText title, status, limitation, milestoneRule, futureCoverageRule;
        public LocalizedText[] futureDesignTests = Array.Empty<LocalizedText>();
        public FrameworkV2NamedItem[] domains = Array.Empty<FrameworkV2NamedItem>();
        public FrameworkV2LevelDefinition[] levelDefinitions = Array.Empty<FrameworkV2LevelDefinition>();
        public FrameworkV2MilestoneDefinition[] studyMilestones = Array.Empty<FrameworkV2MilestoneDefinition>();
        public FrameworkV2NamedItem[] contexts = Array.Empty<FrameworkV2NamedItem>();
        public FrameworkV2CompetencyDefinition[] competencies = Array.Empty<FrameworkV2CompetencyDefinition>();
        public FrameworkV2LensDefinition[] futureLenses = Array.Empty<FrameworkV2LensDefinition>();
        public FrameworkV2AiAcrossDefinition aiAcrossCurriculum;
    }
    [Serializable] public sealed class FrameworkV2AiAcrossDefinition
    {
        public int schemaVersion;
        public string status;
        public LocalizedText title, principle, assessmentNotice, flowRule;
        public FrameworkV2AiDimensionDefinition[] dimensions = Array.Empty<FrameworkV2AiDimensionDefinition>();
        public FrameworkV2AiDomainDefinition[] domainRows = Array.Empty<FrameworkV2AiDomainDefinition>();
        public FrameworkV2AiEdgeDefinition[] flowEdges = Array.Empty<FrameworkV2AiEdgeDefinition>();
        public FrameworkV2AiPhasePolicyDefinition phasePolicy;
        public FrameworkV2AiExampleDefinition[] examples = Array.Empty<FrameworkV2AiExampleDefinition>();
    }
    [Serializable] public sealed class FrameworkV2AiDimensionDefinition { public string id; public LocalizedText title, description; }
    [Serializable] public sealed class FrameworkV2AiDomainDefinition
    {
        public string domainId;
        public string[] competencyIds, criterionIds, aiRoles;
        public LocalizedText summary, learnerResponsibility, evidenceFocus;
    }
    [Serializable] public sealed class FrameworkV2AiEdgeDefinition
    {
        public string fromDomainId, toDomainId;
        public string[] criterionIds;
        public LocalizedText label;
    }
    [Serializable] public sealed class FrameworkV2AiPhasePolicyDefinition
    {
        public bool noWholeModuleMandate;
        public FrameworkV2AiPhaseDefinition[] phases;
        public FrameworkV2AiRuleDefinition[] assessmentRules;
    }
    [Serializable] public sealed class FrameworkV2AiPhaseDefinition { public string contextId; public LocalizedText title, purpose, learnerAction, assessmentUse; }
    [Serializable] public sealed class FrameworkV2AiRuleDefinition { public string id; public LocalizedText text; }
    [Serializable] public sealed class FrameworkV2AiExampleDefinition
    {
        public string id;
        public LocalizedText title, assessmentProposal;
        public string[] moduleIds, criterionIds;
        public FrameworkV2AiExamplePhaseDefinition[] phases;
    }
    [Serializable] public sealed class FrameworkV2AiExamplePhaseDefinition { public string contextId; public LocalizedText activity, evidence; }
    [Serializable] public sealed class FrameworkV2NamedItem { public string id; public LocalizedText title; }
    [Serializable] public sealed class FrameworkV2LevelDefinition { public int level; public string label; public LocalizedText title, description; }
    [Serializable] public sealed class FrameworkV2MilestoneDefinition { public string id, typicalSemesters; public LocalizedText title; }
    [Serializable] public sealed class FrameworkV2CompetencyDefinition
    {
        public string id, domainId;
        public LocalizedText title, scope, boundary;
        public FrameworkV2CriterionDefinition[] criteria = Array.Empty<FrameworkV2CriterionDefinition>();
        public FrameworkV2CompetencyLevel[] levels = Array.Empty<FrameworkV2CompetencyLevel>();
        public FrameworkV2AiIntegrationDefinition aiIntegration;
    }
    [Serializable] public sealed class FrameworkV2AiIntegrationDefinition
    {
        public LocalizedText possibleRole, learnerResponsibility, assessmentFocus, exampleTask;
        public string[] relatedCriterionIds = Array.Empty<string>();
    }
    [Serializable] public sealed class FrameworkV2CriterionDefinition { public string id; public LocalizedText text; }
    [Serializable] public sealed class FrameworkV2CompetencyLevel { public int level; public LocalizedText description; }
    [Serializable] public sealed class FrameworkV2LensDefinition
    {
        public string id;
        public LocalizedText title, description;
        public string[] competencyIds = Array.Empty<string>(), criterionIds = Array.Empty<string>(), contextIds = Array.Empty<string>();
    }
    [Serializable] public sealed class FrameworkV2CareersDefinition
    {
        public string schema, frameworkVersion, displayRule;
        public int schemaVersion;
        public LocalizedText notice;
        public FrameworkV2RoleDefinition[] roles = Array.Empty<FrameworkV2RoleDefinition>();
    }
    [Serializable] public sealed class FrameworkV2RoleDefinition
    {
        public string id, sourceUrl, status;
        public LocalizedText title;
        public FrameworkV2FacetDefinition[] facets = Array.Empty<FrameworkV2FacetDefinition>();
    }
    [Serializable] public sealed class FrameworkV2FacetDefinition
    {
        public string id;
        public LocalizedText title, activity;
        public string[] criterionIds = Array.Empty<string>();
        public bool optionalContext;
        public string[] contextIds = Array.Empty<string>();
    }
    [Serializable] public sealed class FrameworkV2LearningDesign
    {
        public string schema, status, frameworkVersion;
        public int schemaVersion;
        public FrameworkV2ModuleDesign[] modules = Array.Empty<FrameworkV2ModuleDesign>();
    }
    [Serializable] public sealed class FrameworkV2ModuleDesign
    {
        public string id, code, kind;
        public string[] parentSlotIds = Array.Empty<string>();
        public FrameworkV2Baseline baseline;
        public FrameworkV2Proposal proposal;
    }
    [Serializable] public sealed class FrameworkV2Baseline
    {
        public string status, houseId, stageId;
        public LocalizedText title, description;
        public float ects;
    }
    [Serializable] public sealed class FrameworkV2Proposal
    {
        public string status;
        public bool topicRequired, individualisationRequired;
        public LocalizedText knowledgeAnchors, futureSummary, openDecisions;
        public FrameworkV2Objective[] objectives = Array.Empty<FrameworkV2Objective>();
    }
    [Serializable] public sealed class FrameworkV2Objective
    {
        public string id, criterionId;
        public int targetLevel;
        public string[] contextIds = Array.Empty<string>();
        public LocalizedText text, task, evidence, successCriteria;
    }

    // This view intentionally has no attainedLevel, ability score, career-fit
    // percentage or assessment judgement. No such records exist in the save.
    [Serializable] public sealed class FrameworkV2Profile
    {
        public int schemaVersion = 2;
        public string frameworkVersion, title, status, limitation, careerNotice, language;
        public string assessmentStatus = "unassessed";
        public bool hasAssessedEvidence;
        public string opportunityNotice, gamePracticeNotice, milestoneRule, futureCoverageRule;
        public string[] futureDesignTests;
        public FrameworkV2NamedView[] domains, contexts;
        public FrameworkV2LevelView[] levels;
        public FrameworkV2MilestoneView[] studyMilestones;
        public FrameworkV2LensView[] futureLenses;
        public FrameworkV2AiAcrossView aiAcrossCurriculum;
        public FrameworkV2CompetencyView[] competencies;
        public FrameworkV2RoleView[] careers;
        public FrameworkV2PracticeView gamePractice;
        public string[] selfReportedModuleIds;
    }
    [Serializable] public sealed class FrameworkV2NamedView { public string id, title; }
    [Serializable] public sealed class FrameworkV2AiAcrossView
    {
        public int schemaVersion;
        public string status, title, principle, assessmentNotice, flowRule;
        public FrameworkV2AiDimensionView[] dimensions;
        public FrameworkV2AiDomainView[] domainRows;
        public FrameworkV2AiEdgeView[] flowEdges;
        public FrameworkV2AiPhasePolicyView phasePolicy;
        public FrameworkV2AiExampleView[] examples;
    }
    [Serializable] public sealed class FrameworkV2AiDimensionView { public string id, title, description; }
    [Serializable] public sealed class FrameworkV2AiDomainView
    {
        public string domainId, summary, learnerResponsibility, evidenceFocus;
        public string[] competencyIds, criterionIds, aiRoles;
    }
    [Serializable] public sealed class FrameworkV2AiEdgeView { public string fromDomainId, toDomainId, label; public string[] criterionIds; }
    [Serializable] public sealed class FrameworkV2AiPhasePolicyView
    {
        public bool noWholeModuleMandate;
        public FrameworkV2AiPhaseView[] phases;
        public FrameworkV2AiRuleView[] assessmentRules;
    }
    [Serializable] public sealed class FrameworkV2AiPhaseView { public string contextId, title, purpose, learnerAction, assessmentUse; }
    [Serializable] public sealed class FrameworkV2AiRuleView { public string id, text; }
    [Serializable] public sealed class FrameworkV2AiExampleView
    {
        public string id, title, assessmentProposal;
        public string[] moduleIds, criterionIds;
        public FrameworkV2AiExamplePhaseView[] phases;
    }
    [Serializable] public sealed class FrameworkV2AiExamplePhaseView { public string contextId, activity, evidence; }
    [Serializable] public sealed class FrameworkV2LevelView { public int level; public string label, title, description; }
    [Serializable] public sealed class FrameworkV2MilestoneView { public string id, title, typicalSemesters; }
    [Serializable] public sealed class FrameworkV2LensView
    {
        public string id, title, description;
        public string[] competencyIds, criterionIds, plannedCriterionIds, contextIds;
    }
    [Serializable] public sealed class FrameworkV2CompetencyView
    {
        public string id, domainId, title, scope, boundary;
        public string assessmentStatus = "unassessed";
        public FrameworkV2CriterionView[] criteria;
        public FrameworkV2LevelDescriptionView[] levelDescriptions;
        public FrameworkV2AiIntegrationView aiIntegration;
    }
    [Serializable] public sealed class FrameworkV2AiIntegrationView
    {
        public string possibleRole, learnerResponsibility, assessmentFocus, exampleTask;
        public string[] relatedCriterionIds;
    }
    [Serializable] public sealed class FrameworkV2LevelDescriptionView { public int level; public string description; }
    [Serializable] public sealed class FrameworkV2CriterionView
    {
        public string id, competencyId, title;
        public string assessmentStatus = "unassessed";
        public int[] opportunityLevels;
        public string[] plannedModuleIds;
        public FrameworkV2Opportunity[] opportunities;
    }
    [Serializable] public sealed class FrameworkV2Opportunity
    {
        public string moduleId, moduleCode, moduleTitle, houseId, studyMilestoneId, objectiveId, criterionId;
        public string selectionStatus, proposalStatus;
        public int targetLevel;
        public string[] contextIds;
        public string text, task, evidence, successCriteria, openDecisions;
        public bool topicRequired, individualisationRequired;
        public bool confirmed;
    }
    [Serializable] public sealed class FrameworkV2RoleView
    {
        public string id, title, status, sourceUrl;
        public FrameworkV2FacetView[] facets;
        public FrameworkV2NextModule[] nextModules;
    }
    [Serializable] public sealed class FrameworkV2FacetView
    {
        public string id, title, activity;
        public string[] criterionIds, plannedCriterionIds, opportunityModuleIds;
        public bool optionalContext;
        public string[] contextIds;
        public FrameworkV2NextModule[] nextModules;
    }
    [Serializable] public sealed class FrameworkV2NextModule
    {
        public string moduleId, moduleCode, title, houseId, studyMilestoneId, selectionStatus, proposalStatus;
        public string[] criterionIds;
        public int[] opportunityLevels;
        public bool canBuild, needsSelfCheck, needsQuiz, needsMasterEntry, topicRequired, individualisationRequired;
        public string[] missingPrerequisiteIds;
        public string openDecisions;
    }
    [Serializable] public sealed class FrameworkV2PracticeView
    {
        public string status = "game-practice-only";
        public FrameworkV2PractisedModule[] modules;
        public string[] questIds;
    }
    [Serializable] public sealed class FrameworkV2PractisedModule { public string moduleId, moduleCode; }
}
