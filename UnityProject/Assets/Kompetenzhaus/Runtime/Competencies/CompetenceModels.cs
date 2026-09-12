using System;
using Kompetenzhaus.Content;

namespace Kompetenzhaus.Competencies
{
    [Serializable] public sealed class CompetenceChoice
    {
        public string moduleId, specialisationId, topicId, thesisQuestionId;
    }
    [Serializable] public sealed class CompetenceChoiceOption
    {
        public string moduleId, kind, id, parentId, directionId;
        public LocalizedText name;
        public string[] competencyIds = Array.Empty<string>();
        public string[] careerIds = Array.Empty<string>();
    }
    [Serializable] public sealed class CompetenceValue
    {
        public string id;
        public double score, maximum, ratio;
        public int percent, stage;
        public string[] builtModuleIds, availableModuleIds;
    }
    [Serializable] public sealed class CompetenceAggregate
    {
        public string id;
        public LocalizedText name;
        public string[] competencyIds;
        public double score, maximum, ratio;
        public int percent;
    }
    [Serializable] public sealed class DegreeCredits
    {
        public string houseId;
        public double selectedEcts;
        public int targetEcts = 120;
    }
    [Serializable] public sealed class CompetenceGap
    {
        public string competencyId;
        public int currentStage, targetStage;
    }
    [Serializable] public sealed class CareerProjection
    {
        public string id;
        public LocalizedText name, description;
        public int fitPercent;
        public bool hasEvidence;
        public double[] targetRadar;
        public CompetenceGap[] gaps;
        public string[] recommendedModuleIds;
    }
    [Serializable] public sealed class CompetenceCount { public string id; public int count; }
    [Serializable] public sealed class ChoiceProfile
    {
        public string bachelorDirection, masterSpecialisation;
        public CompetenceCount[] bachelorCounts, masterCounts;
    }
    [Serializable] public sealed class SemesterProjection
    {
        public string id;
        public double fa, ki, fu;
    }
    [Serializable] public sealed class PreStageCheck
    {
        public int index;
        public LocalizedText name;
        public string[] competencyIds;
    }
    [Serializable] public sealed class CompetenceProfileSnapshot
    {
        public string meaning = "modelled-curricular-exposure-not-measured-personal-competence";
        public string sourceStatus = "Inherited public design model, 2026; career and further-education descriptions are not newly verified requirements.";
        public string sourceUrl = "https://github.com/gpochs/kompetenzhaus-psychologie-uzh";
        public string calculationVersion = "legacy-profile-1";
        public bool isPreview;
        public CompetenceValue[] competencies;
        public CompetenceAggregate[] fields, radar;
        public DegreeCredits[] credits;
        public ChoiceProfile choices;
        public CareerProjection[] careers;
        public SemesterProjection[] semesters;
        public CompetenceChoiceOption[] choiceOptions;
        public PreStageCheck[] preStageOptions;
    }
    internal sealed class ModuleProjectionRule
    {
        public string moduleId;
        public string[] maximumIds, maximumPrimaryIds;
    }
    internal sealed class CareerRule
    {
        public string id, directionId, specialisationId, optionalModuleCode;
        public LocalizedText name, description;
        public string[] weightIds, targetIds;
        public double[] weights, targetRadar;
        public int[] targetStages;
    }
    internal sealed class SemesterRule { public string id; public string[] moduleIds; }
}
