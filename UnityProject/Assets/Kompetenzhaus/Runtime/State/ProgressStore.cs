using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.Competencies;
using UnityEngine;

namespace Kompetenzhaus.State
{
    public enum ViewMode { BirdView, FirstPerson }
    public enum LearningMode { Free, Serious }

    [Serializable]
    public sealed class AccessibilitySettings
    {
        public bool highContrast;
        public bool reducedMotion;
        public bool captions = true;
        public float textScale = 1f;
        public float lookSensitivity = 1f;
        public float masterVolume = 0.75f;
    }

    [Serializable]
    public sealed class ProgressData
    {
        public int schemaVersion = 1;
        public string language = "de";
        public ViewMode viewMode = ViewMode.BirdView;
        public LearningMode learningMode = LearningMode.Free;
        public string selectedHouseId = "all";
        public bool futureCurriculum = true;
        public bool directMasterEntry;
        public List<ModuleChoice> moduleChoices = new List<ModuleChoice>();
        public List<CompetenceChoice> competenceChoices = new List<CompetenceChoice>();
        public List<int> preStageChecks = new List<int>();
        public List<string> placedModuleIds = new List<string>();
        public List<string> quizMasteredModuleIds = new List<string>();
        public List<string> selfCheckedModuleIds = new List<string>();
        public List<string> completedQuestIds = new List<string>();
        public List<string> unlockedCosmeticIds = new List<string>();
        public int earnedExperience;
        public AccessibilitySettings accessibility = new AccessibilitySettings();
        public ArchitectureState architecture = ArchitectureState.CreateDefault();
    }

    [Serializable]
    public sealed class ModuleChoice
    {
        public string slotId;
        public string moduleCode;
    }

    public sealed class ProgressStore
    {
        // Deliberately distinct from the original game's keys: GitHub Pages projects share an origin.
        public const string SaveKey = "uzh.kompetenzhaus.unity.v1.progress";
        public ProgressData Data { get; private set; }
        public string LoadWarning { get; private set; }
        private readonly ContentCatalog catalog;

        public ProgressStore(ContentCatalog catalog = null)
        {
            this.catalog = catalog;
            Data = new ProgressData();
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            if (TryParseValidated(PlayerPrefs.GetString(SaveKey), catalog, out var loaded, out _)) Data = loaded;
            else LoadWarning = "Der gespeicherte Fortschritt konnte nicht gelesen werden. Er wird erst beim nächsten Speichern ersetzt.";
        }

        public void Save()
        {
            Normalize(Data);
            if (!Validate(Data, catalog, out var error)) throw new InvalidOperationException("Cannot save invalid progress: " + error);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public void ResetThisGame()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            // Keep the instance shared with ProgressionService; resetting must not leave stale service references.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new ProgressData()), Data);
        }

        public static bool TryParseValidated(string json, ContentCatalog catalog, out ProgressData data, out string error)
        {
            data = null;
            try
            {
                var candidate = JsonUtility.FromJson<ProgressData>(json);
                if (candidate == null || candidate.schemaVersion != 1) throw new InvalidOperationException("Unknown save version.");
                Normalize(candidate);
                if (!Validate(candidate, catalog, out error)) return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static void Normalize(ProgressData data)
        {
            data.placedModuleIds ??= new List<string>();
            data.quizMasteredModuleIds ??= new List<string>();
            data.selfCheckedModuleIds ??= new List<string>();
            data.completedQuestIds ??= new List<string>();
            data.unlockedCosmeticIds ??= new List<string>();
            data.moduleChoices ??= new List<ModuleChoice>();
            data.competenceChoices ??= new List<CompetenceChoice>();
            data.preStageChecks ??= new List<int>();
            data.accessibility ??= new AccessibilitySettings();
            data.architecture ??= ArchitectureState.CreateDefault();
            data.language = data.language == "en" ? "en" : "de";
            if (data.selectedHouseId != "bsc" && data.selectedHouseId != "msc") data.selectedHouseId = "all";
        }

        private static bool Validate(ProgressData data, ContentCatalog catalog, out string error)
        {
            error = null;
            if (!Enum.IsDefined(typeof(LearningMode), data.learningMode) || !Enum.IsDefined(typeof(ViewMode), data.viewMode) || data.earnedExperience < 0)
                return Fail("Invalid progress mode or experience.", out error);
            foreach (var ids in new[] { data.placedModuleIds, data.quizMasteredModuleIds, data.selfCheckedModuleIds, data.completedQuestIds, data.unlockedCosmeticIds })
                if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Count) return Fail("Invalid or duplicate progress id.", out error);
            if (data.moduleChoices.Any(choice => choice == null || string.IsNullOrWhiteSpace(choice.slotId) || string.IsNullOrWhiteSpace(choice.moduleCode)) ||
                data.moduleChoices.Select(choice => choice.slotId).Distinct().Count() != data.moduleChoices.Count)
                return Fail("Invalid optional-module selection.", out error);
            if (data.competenceChoices.Any(choice => choice == null || string.IsNullOrWhiteSpace(choice.moduleId)) ||
                data.competenceChoices.Select(choice => choice.moduleId).Distinct().Count() != data.competenceChoices.Count)
                return Fail("Invalid competency-profile selection.", out error);
            foreach (var choice in data.competenceChoices)
                if (!CompetenceProjection.ValidateChoice(choice, out error)) return false;
            if (data.preStageChecks.Any(index => index < 0 || index > 3) || data.preStageChecks.Distinct().Count() != data.preStageChecks.Count)
                return Fail("Invalid self-reported prior-learning selection.", out error);
            var access = data.accessibility;
            if (!InRange(access.textScale, 1f, 1.5f) || !InRange(access.lookSensitivity, 0.2f, 3f) || !InRange(access.masterVolume, 0f, 1f))
                return Fail("Invalid accessibility settings.", out error);
            if (catalog != null)
            {
                if (data.competenceChoices.Any(choice => !catalog.HasModule(choice.moduleId))) return Fail("Saved competency-choice module no longer exists.", out error);
                foreach (var id in data.placedModuleIds.Concat(data.quizMasteredModuleIds).Concat(data.selfCheckedModuleIds))
                    if (!catalog.HasModule(id)) return Fail("Saved module no longer exists in the curriculum.", out error);
                foreach (var choice in data.moduleChoices)
                    if (!(catalog.GetModule(choice.slotId)?.optionCodes ?? Array.Empty<string>()).Contains(choice.moduleCode))
                        return Fail("Saved option is not valid for its slot.", out error);
                if (data.completedQuestIds.Any(id => !(catalog.Document.quests ?? Array.Empty<QuestDefinition>()).Any(q => q.id == id)))
                    return Fail("Saved quest no longer exists.", out error);
                var temporary = new ProgressionService(catalog, data, () => { });
                if (!new ArchitectureService(catalog, temporary).Validate(data.architecture, out error)) return false;
            }
            return true;
        }

        private static bool InRange(float value, float minimum, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
