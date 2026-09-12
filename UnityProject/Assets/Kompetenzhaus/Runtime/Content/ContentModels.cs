using System;

namespace Kompetenzhaus.Content
{
    [Serializable]
    public sealed class LocalizedText
    {
        public string de = "";
        public string en = "";
        public string Get(string language) => language == "en" && !string.IsNullOrWhiteSpace(en) ? en : de ?? "";
    }

    [Serializable]
    public sealed class ContentDocument
    {
        public int schemaVersion;
        public ContentMetadata metadata;
        public ContentRules rules;
        public CompetencyDefinition[] competencies = Array.Empty<CompetencyDefinition>();
        public StageDefinition[] stages = Array.Empty<StageDefinition>();
        public ModuleDefinition[] modules = Array.Empty<ModuleDefinition>();
        public ModuleDefinition[] optionalModules = Array.Empty<ModuleDefinition>();
        public QuizBankDefinition[] quizBanks = Array.Empty<QuizBankDefinition>();
        public QuestDefinition[] quests = Array.Empty<QuestDefinition>();
    }

    [Serializable]
    public sealed class ContentMetadata
    {
        public string title;
        public string version;
        public string sourceUrl;
        public string contentStatus;
        public LocalizedText proposalNotice;
    }

    [Serializable]
    public sealed class ContentRules
    {
        public string masterEntryPrerequisiteId = "";
        public string[] masterStageIds = Array.Empty<string>();
        public string masterHouseId = "msc";
        public bool allowDirectMasterEntry;
    }

    [Serializable]
    public sealed class CompetencyDefinition
    {
        public string id;
        public LocalizedText name;
        public LocalizedText description;
        public string fieldId;
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public string id;
        public int index;
        public LocalizedText name;
        public LocalizedText anchor;
    }

    [Serializable]
    public sealed class ModuleDefinition
    {
        public string id;
        public string code;
        public LocalizedText title;
        public LocalizedText shortTitle;
        public string stageId;
        public int stage;
        public string houseId;
        public string groupId;
        public float ects;
        public int semester;
        public bool spansTwoSemesters;
        public string category;
        public string[] optionCodes = Array.Empty<string>();
        public string[] prerequisiteIds = Array.Empty<string>();
        public string[] recommendedIds = Array.Empty<string>();
        public string[] competencyIds = Array.Empty<string>();
        public string[] primaryCompetencyIds = Array.Empty<string>();
        public string prerequisiteStatus;
        public LocalizedText description;
        public LocalizedText futureDescription;
        public LocalizedText[] learningObjectives = Array.Empty<LocalizedText>();
        public LocalizedText aiDescription;
        public string aiMode;
        public string proposalStatus;
        public string[] sourceReferences = Array.Empty<string>();
        public string quizBankId;
        public QuizQuestion[] questions = Array.Empty<QuizQuestion>();
        public PracticeQuest practiceQuest;
        public ModulePosition position;
    }

    [Serializable]
    public sealed class ModulePosition
    {
        public float x, y, z, width, depth, height;
    }

    [Serializable]
    public sealed class PracticeQuest
    {
        public LocalizedText title;
        public LocalizedText description;
    }

    [Serializable]
    public sealed class QuizBankDefinition
    {
        public string id;
        public QuizQuestion[] questions = Array.Empty<QuizQuestion>();
    }

    [Serializable]
    public sealed class QuizQuestion
    {
        public string id;
        public string type;
        public LocalizedText prompt;
        public LocalizedText[] positions = Array.Empty<LocalizedText>();
        public LocalizedText[] options = Array.Empty<LocalizedText>();
        public int correctIndex;
        public LocalizedText explanation;
    }

    [Serializable]
    public sealed class QuestDefinition
    {
        public string id;
        public string type;
        public LocalizedText title;
        public LocalizedText description;
        public LocalizedText instructions;
        public LocalizedText feedback;
        public string stageId;
        public string frameworkVersion;
        public string alignmentMeaning;
        public string[] criterionIds = Array.Empty<string>();
        public string[] competencyIds = Array.Empty<string>();
        public string[] moduleIds = Array.Empty<string>();
        public string[] prerequisiteQuestIds = Array.Empty<string>();
        public string worldLocation;
        public string interaction;
        public int estimatedMinutes;
        public QuestReward reward;
        public QuizQuestion[] questions = Array.Empty<QuizQuestion>();
    }

    [Serializable]
    public sealed class QuestReward
    {
        public string cosmeticId;
        public LocalizedText title;
        public int experience;
        public string type;
        public bool academicCredit;
    }
}
