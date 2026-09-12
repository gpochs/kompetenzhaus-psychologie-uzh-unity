using System;
using Kompetenzhaus.Content;
using Kompetenzhaus.Presentation;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using UnityEngine;

namespace Kompetenzhaus
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public CameraModeController cameraController;
        public ContentCatalog Catalog { get; private set; }
        public ProgressionService Progression { get; private set; }
        public ArchitectureService Architecture { get; private set; }
        public QuizSession Quiz { get; private set; }
        public string StartupError { get; private set; }
        public string SaveWarning { get; private set; }
        public string ActiveModuleId { get; private set; }
        public string ActiveQuestId { get; private set; }
        public string ActiveModuleCode { get; private set; }
        public bool UiFocus { get; private set; }
        public event Action Ready;
        public event Action<string> Message;
        public event Action<string> HouseFocusRequested;

        private void Awake()
        {
            try
            {
                Catalog = ContentCatalog.LoadResource();
                var store = new ProgressStore(Catalog);
                SaveWarning = store.LoadWarning;
                Progression = new ProgressionService(Catalog, store);
                Architecture = new ArchitectureService(Catalog, Progression);
                Quiz = new QuizSession();
                Quiz.Changed += OnQuizChanged;
                Quiz.Completed += OnQuizCompleted;
                Progression.Changed += ApplyPreferences;
                if (cameraController != null) cameraController.Initialize(Progression);
                ApplyPreferences();
                Ready?.Invoke();
            }
            catch (Exception exception)
            {
                StartupError = exception.Message;
                Debug.LogError("[Kompetenzhaus] " + exception.Message);
            }
        }

        public bool StartModuleQuiz(string moduleId)
        {
            if (Catalog == null || !Catalog.TryGetModule(moduleId, out var module)) return false;
            var questions = Catalog.GetQuestions(Progression.GetContentForSlot(moduleId));
            if (questions.Length == 0) { Message?.Invoke("Für dieses Modul ist noch kein Quiz hinterlegt."); return false; }
            ActiveModuleId = moduleId;
            ActiveModuleCode = Progression.GetSelectedCode(moduleId);
            ActiveQuestId = null;
            Quiz.Start(questions);
            return true;
        }

        public bool StartQuest(string questId)
        {
            if (Catalog == null) return false;
            var quest = Array.Find(Catalog.Document.quests ?? Array.Empty<QuestDefinition>(), q => q.id == questId);
            if (quest == null || quest.questions == null || quest.questions.Length == 0 || !Progression.CanStartQuest(questId)) return false;
            ActiveModuleId = null;
            ActiveQuestId = questId;
            Quiz.Start(quest.questions);
            return true;
        }

        public void SetLanguage(string language)
        {
            Progression.Data.language = language == "en" ? "en" : "de";
            Progression.SaveAndNotify();
        }

        public void SelectHouse(string houseId)
        {
            Progression.Data.selectedHouseId = houseId == "bsc" || houseId == "msc" ? houseId : "all";
            Progression.SaveAndNotify();
            HouseFocusRequested?.Invoke(Progression.Data.selectedHouseId);
        }

        public void SetUiFocus(bool enabled)
        {
            UiFocus = enabled;
            if (cameraController != null) cameraController.SetInputBlocked(enabled || Quiz.Phase != QuizPhase.Idle);
        }

        public void SetAccessibility(bool highContrast, bool reducedMotion, bool largerText)
        {
            var settings = Progression.Data.accessibility;
            settings.highContrast = highContrast;
            settings.reducedMotion = reducedMotion;
            settings.textScale = largerText ? 1.25f : 1f;
            Progression.SaveAndNotify();
        }

        public void ApplyAccessibility(AccessibilitySettings settings)
        {
            if (settings == null || !ValidRange(settings.textScale, 1f, 1.5f) || !ValidRange(settings.lookSensitivity, 0.2f, 3f) ||
                !ValidRange(settings.masterVolume, 0f, 1f)) throw new ArgumentException("Invalid accessibility settings.");
            Progression.Data.accessibility = JsonUtility.FromJson<AccessibilitySettings>(JsonUtility.ToJson(settings));
            Progression.SaveAndNotify();
        }

        private static bool ValidRange(float value, float minimum, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;

        private void OnQuizChanged()
        {
            if (cameraController != null) cameraController.SetInputBlocked(UiFocus || Quiz.Phase != QuizPhase.Idle);
        }

        private void OnQuizCompleted()
        {
            if (!string.IsNullOrEmpty(ActiveModuleId) && Progression.GetSelectedCode(ActiveModuleId) == ActiveModuleCode)
                Progression.MarkModuleQuizMastered(ActiveModuleId);
            if (!string.IsNullOrEmpty(ActiveQuestId)) Progression.MarkQuestCompleted(ActiveQuestId);
        }

        private void ApplyPreferences() => AudioListener.volume = Progression.Data.accessibility.masterVolume;

        private void OnDestroy()
        {
            if (Quiz != null) { Quiz.Changed -= OnQuizChanged; Quiz.Completed -= OnQuizCompleted; }
            if (Progression != null) Progression.Changed -= ApplyPreferences;
        }
    }
}
