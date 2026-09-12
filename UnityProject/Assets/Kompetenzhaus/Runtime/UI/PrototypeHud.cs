using System;
using System.Collections.Generic;
using Kompetenzhaus.Content;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kompetenzhaus.UI
{
    // Functional development UI. Art direction and final spatial composition are a separate gate.
    public sealed class PrototypeHud : MonoBehaviour
    {
        public GameBootstrap game;
        private RectTransform page;
        private Text heading;
        private Text status;
        private Font font;
        private string selectedModuleId;
        private string selectedQuestId;
        private bool questList;
        private readonly List<GameObject> dynamicObjects = new List<GameObject>();
        private readonly List<Button> answerButtons = new List<Button>();
        private string Language => game.Progression?.Data.language ?? "de";
        private bool English => Language == "en";

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildShell();
            if (game.StartupError != null) { AddText(game.StartupError, 24, Color.white); return; }
            game.Quiz.Changed += Refresh;
            game.Progression.Changed += Refresh;
            game.Message += ShowMessage;
            Refresh();
        }

        private void BuildShell()
        {
            var canvasObject = new GameObject("Development HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 1000);
            scaler.matchWidthOrHeight = 1f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.035f, 0.035f);
            panelRect.anchorMax = new Vector2(0.65f, 0.965f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.045f, 0.075f, 0.11f, 0.98f);
            var scroll = panel.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(24, 24);
            viewportRect.offsetMax = new Vector2(-24, -24);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            page = content.GetComponent<RectTransform>();
            page.anchorMin = new Vector2(0, 1);
            page.anchorMax = Vector2.one;
            page.pivot = new Vector2(0.5f, 1);
            page.offsetMin = page.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = page;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void Refresh()
        {
            foreach (var item in dynamicObjects) { item.SetActive(false); Destroy(item); }
            dynamicObjects.Clear();
            answerButtons.Clear();
            heading = AddText(English ? "COMPETENCY HOUSE · DEVELOPMENT PREVIEW" : "KOMPETENZHAUS · ENTWICKLUNGSVORSCHAU", 27, Accent);
            if (game.Quiz.Phase != QuizPhase.Idle) RenderQuiz();
            else RenderModules();
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
        }

        private void RenderModules()
        {
            status = AddText(English
                ? "Curriculum and learning logic are available. The final 3D environment follows the visual review."
                : "Curriculum und Lernlogik sind verfügbar. Die finale 3D-Umgebung folgt nach der visuellen Prüfung.", 21, Color.white);
            AddText($"{game.Progression.Data.placedModuleIds.Count}/{game.Catalog.Document.modules.Length} " +
                (English ? "modules built · " : "Module gebaut · ") + game.Progression.Data.quizMasteredModuleIds.Count +
                (English ? " quizzes mastered" : "Quiz-Lernziele erreicht"), 20, Color.white);
            AddButton(English ? "Deutsch" : "English", () => game.SetLanguage(English ? "de" : "en"));
            AddButton(English ? "Both houses" : "Beide Häuser", () => { selectedModuleId = null; game.SelectHouse("all"); });
            AddButton(English ? "Bachelor house" : "Bachelorhaus", () => { selectedModuleId = null; game.SelectHouse("bsc"); });
            AddButton(English ? "Master house" : "Masterhaus", () => { selectedModuleId = null; game.SelectHouse("msc"); });
            AddButton(game.Progression.Data.futureCurriculum ? (English ? "Curriculum: future design" : "Curriculum: Zukunftsentwurf") :
                (English ? "Curriculum: inherited present" : "Curriculum: übernommener Ist-Stand"), () =>
            {
                game.Progression.Data.futureCurriculum = !game.Progression.Data.futureCurriculum;
                game.Progression.SaveAndNotify();
            });
            var access = game.Progression.Data.accessibility;
            AddButton((English ? "High contrast: " : "Hoher Kontrast: ") + (access.highContrast ? "✓" : "–"),
                () => game.SetAccessibility(!access.highContrast, access.reducedMotion, access.textScale > 1f));
            AddButton((English ? "Larger text: " : "Grössere Schrift: ") + (access.textScale > 1f ? "✓" : "–"),
                () => game.SetAccessibility(access.highContrast, access.reducedMotion, access.textScale <= 1f));
            AddButton((English ? "Reduced motion: " : "Weniger Bewegung: ") + (access.reducedMotion ? "✓" : "–"),
                () => game.SetAccessibility(access.highContrast, !access.reducedMotion, access.textScale > 1f));
            AddButton((English ? "Mode: " : "Modus: ") + game.Progression.Data.learningMode, () =>
            {
                game.Progression.Data.learningMode = game.Progression.Data.learningMode == LearningMode.Free ? LearningMode.Serious : LearningMode.Free;
                game.Progression.SaveAndNotify();
            });
            AddButton(English ? "Bird view / First person" : "Vogelperspektive / Ich-Perspektive", () =>
            {
                var controller = game.cameraController;
                if (!controller.SetMode(controller.Mode == ViewMode.BirdView ? ViewMode.FirstPerson : ViewMode.BirdView))
                    ShowMessage(English ? "The environment must be built before first-person exploration." : "Die Umgebung muss vor dem Erkunden aufgebaut sein.");
            });
            AddText(English ? "Game progress is not proof of academic completion." : "Der Spielfortschritt ist kein Nachweis eines Studienabschlusses.", 18, Color.white);
            AddButton(questList ? (English ? "Show modules" : "Module anzeigen") : (English ? "Learning quests" : "Lernaufträge"), () =>
            { questList = !questList; selectedModuleId = selectedQuestId = null; Refresh(); });
            if (game.Progression.Data.selectedHouseId == "msc" && game.Catalog.Document.rules?.allowDirectMasterEntry == true)
                AddButton((English ? "Direct master entry: " : "Direkter Mastereinstieg: ") + (game.Progression.Data.directMasterEntry ? "✓" : "–"), () =>
                { game.Progression.Data.directMasterEntry = !game.Progression.Data.directMasterEntry; game.Progression.SaveAndNotify(); });
            if (questList) RenderQuests();
            else if (!string.IsNullOrEmpty(selectedModuleId)) RenderModule(game.Catalog.GetModule(selectedModuleId));
            else
                foreach (var module in game.Catalog.ModulesForHouse(game.Progression.Data.selectedHouseId))
                {
                    var moduleId = module.id;
                    AddButton(module.code + " · " + module.title.Get(Language), () => { selectedModuleId = moduleId; Refresh(); });
                }
        }

        private void RenderModule(ModuleDefinition module)
        {
            var slot = module;
            module = game.Progression.GetContentForSlot(slot.id);
            AddButton(English ? "← All modules" : "← Alle Module", () => { selectedModuleId = null; Refresh(); });
            AddText(module.title.Get(Language), 28, Accent);
            if (slot.optionCodes != null && slot.optionCodes.Length > 1)
                foreach (var code in slot.optionCodes)
                {
                    var choiceCode = code;
                    var choice = game.Catalog.GetOptionalModule(code);
                    AddButton((game.Progression.GetSelectedCode(slot.id) == code ? "✓ " : "") + (choice?.title.Get(Language) ?? code),
                        () => game.Progression.SetModuleChoice(slot.id, choiceCode), !game.Progression.Data.placedModuleIds.Contains(slot.id));
                }
            AddText(game.Progression.Data.futureCurriculum ? (module.futureDescription?.Get(Language) ?? "") : (module.description?.Get(Language) ?? ""), 22, Color.white);
            if (game.Progression.Data.futureCurriculum)
            {
                AddText(game.Catalog.Document.metadata?.proposalNotice?.Get(Language) ?? "Designentwurf", 18, Accent);
                AddText(module.aiDescription?.Get(Language) ?? "", 21, Color.white);
            }
            foreach (var objective in module.learningObjectives ?? Array.Empty<LocalizedText>()) AddText("• " + objective.Get(Language), 21, Color.white);
            AddButton(English ? "Start learning quiz" : "Lernquiz starten", () => game.StartModuleQuiz(slot.id));
            var selfChecked = game.Progression.Data.selfCheckedModuleIds.Contains(slot.id);
            AddButton((English ? "My self-check: " : "Meine Selbsteinschätzung: ") + (selfChecked ? "✓" : "–"),
                () => game.Progression.SetSelfCheck(slot.id, !selfChecked));
            var eligibility = game.Progression.GetEligibility(slot.id);
            if (eligibility.MissingPrerequisiteIds.Length > 0)
                AddText((English ? "Build these modules first: " : "Zuerst diese Module bauen: ") + string.Join(", ", eligibility.MissingPrerequisiteIds), 21, Color.white);
            if (eligibility.NeedsQuiz) AddText(English ? "Complete the learning quiz first." : "Schliesse zuerst das Lernquiz ab.", 21, Color.white);
            if (eligibility.NeedsSelfCheck) AddText(English ? "Your self-check is still open." : "Deine Selbsteinschätzung ist noch offen.", 21, Color.white);
            if (eligibility.NeedsMasterEntry) AddText(English ? "The bachelor stage must be built first." : "Baue zuerst den Bachelorabschluss.", 21, Color.white);
            AddButton(eligibility.AlreadyPlaced ? (English ? "Already built" : "Bereits gebaut") : (English ? "Build module" : "Modul bauen"),
                () => { if (!game.Architecture.TryBuildModuleInFreeBay(slot.id, out var error)) ShowMessage(error); }, eligibility.Allowed && !game.Architecture.IsPositioned(slot.id));
        }

        private void RenderQuests()
        {
            var quests = game.Catalog.Document.quests ?? Array.Empty<QuestDefinition>();
            if (selectedQuestId == null)
            {
                foreach (var quest in quests)
                {
                    var questId = quest.id;
                    AddButton((game.Progression.Data.completedQuestIds.Contains(questId) ? "✓ " : "") + quest.title.Get(Language),
                        () => { selectedQuestId = questId; Refresh(); });
                }
                return;
            }
            var selected = Array.Find(quests, q => q.id == selectedQuestId);
            if (selected == null) return;
            AddButton(English ? "← All quests" : "← Alle Lernaufträge", () => { selectedQuestId = null; Refresh(); });
            AddText(selected.title.Get(Language), 28, Accent);
            AddText(selected.description.Get(Language), 24, Color.white);
            AddText(selected.instructions.Get(Language), 22, Color.white);
            if (!game.Progression.CanStartQuest(selected.id))
                AddText((English ? "Previous quests: " : "Vorherige Lernaufträge: ") + string.Join(", ", selected.prerequisiteQuestIds), 20, Color.white);
            AddButton(English ? "Start quest" : "Lernauftrag starten", () => game.StartQuest(selected.id), game.Progression.CanStartQuest(selected.id));
        }

        private void RenderQuiz()
        {
            var quiz = game.Quiz;
            AddButton(English ? "Close quiz" : "Quiz schliessen", () => quiz.Cancel());
            if (quiz.Phase == QuizPhase.Complete)
            {
                AddText(English ? "Learning goal reached" : "Lernziel erreicht", 30, Accent);
                AddText(English ? "You have correctly answered every question. Keep connecting the ideas to practice." : "Du hast jede Frage richtig beantwortet. Verknüpfe die Inhalte weiter mit der Praxis.", 24, Color.white);
                if (!string.IsNullOrEmpty(game.ActiveQuestId))
                {
                    var quest = Array.Find(game.Catalog.Document.quests, item => item.id == game.ActiveQuestId);
                    if (quest?.feedback != null) AddText(quest.feedback.Get(Language), 24, Color.white);
                }
                AddButton(English ? "Back to the house" : "Zurück zum Haus", () => quiz.Cancel());
                return;
            }
            var question = quiz.CurrentQuestion;
            AddText($"{quiz.MasteredCount}/{quiz.QuestionCount} " + (English ? "questions mastered" : "Fragen erarbeitet"), 20, Accent);
            foreach (var position in question.positions ?? Array.Empty<LocalizedText>())
                AddText(position.Get(Language), 22, Color.white);
            AddText(QuizPresentation.Prompt(question, Language), 27, Color.white);
            if (quiz.Phase == QuizPhase.Question)
            {
                for (var i = 0; i < quiz.OptionOrder.Count; i++)
                {
                    var option = i;
                    var button = AddButton($"{i + 1}. " + question.options[quiz.OptionOrder[i]].Get(Language), () => quiz.Answer(option));
                    answerButtons.Add(button);
                }
                if (answerButtons.Count > 0) EventSystem.current?.SetSelectedGameObject(answerButtons[0].gameObject);
            }
            else
            {
                AddText(quiz.LastAnswerCorrect ? (English ? "Correct" : "Richtig") : (English ? "Take another look" : "Schau noch einmal genauer hin"), 28, Accent);
                AddText((English ? "Your answer: " : "Deine Antwort: ") + question.options[quiz.SelectedOptionIndex].Get(Language), 21, Color.white);
                if (!quiz.LastAnswerCorrect) AddText((English ? "Best answer: " : "Beste Antwort: ") + question.options[question.correctIndex].Get(Language), 21, Color.white);
                AddText(QuizPresentation.Explanation(question, Language), 25, Color.white);
                var next = AddButton(English ? "Continue →" : "Weiter →", () => quiz.Continue());
                EventSystem.current?.SetSelectedGameObject(next.gameObject);
            }
        }

        private Color Accent => game.Progression?.Data.accessibility.highContrast == true ? Color.yellow : new Color(0.45f, 0.85f, 0.83f);

        private Text AddText(string text, int size, Color color)
        {
            var node = new GameObject("Text", typeof(RectTransform), typeof(Text));
            node.transform.SetParent(page, false);
            dynamicObjects.Add(node);
            var label = node.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = Mathf.RoundToInt(size * (game.Progression?.Data.accessibility.textScale ?? 1f));
            label.color = color;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private Button AddButton(string text, Action onClick, bool enabled = true)
        {
            var node = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            node.transform.SetParent(page, false);
            dynamicObjects.Add(node);
            node.GetComponent<LayoutElement>().minHeight = 60f * (game.Progression?.Data.accessibility.textScale ?? 1f);
            var buttonLayout = node.GetComponent<VerticalLayoutGroup>();
            buttonLayout.padding = new RectOffset(14, 14, 12, 12);
            buttonLayout.childForceExpandHeight = false;
            buttonLayout.childControlHeight = buttonLayout.childControlWidth = buttonLayout.childForceExpandWidth = true;
            var labelNode = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelNode.transform.SetParent(node.transform, false);
            var rect = labelNode.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14, 8);
            rect.offsetMax = new Vector2(-14, -8);
            var label = labelNode.GetComponent<Text>();
            label.font = font;
            label.fontSize = Mathf.RoundToInt(21 * (game.Progression?.Data.accessibility.textScale ?? 1f));
            label.color = Color.white;
            label.text = text;
            label.supportRichText = false;
            label.alignment = TextAnchor.MiddleLeft;
            var button = node.GetComponent<Button>();
            button.interactable = enabled;
            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.20f, 0.27f);
            colors.highlightedColor = new Color(0.18f, 0.32f, 0.41f);
            colors.selectedColor = new Color(0.18f, 0.37f, 0.40f);
            colors.pressedColor = new Color(0.08f, 0.15f, 0.2f);
            colors.disabledColor = new Color(0.11f, 0.13f, 0.15f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private void ShowMessage(string message) { if (status != null) status.text = message; }

        private void Update()
        {
            if (game.Quiz == null || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame && game.Quiz.Phase != QuizPhase.Idle) game.Quiz.Cancel();
            if (game.Quiz.Phase != QuizPhase.Question) return;
            var keys = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (var i = 0; i < Mathf.Min(keys.Length, answerButtons.Count); i++)
                if (Keyboard.current[keys[i]].wasPressedThisFrame) { game.Quiz.Answer(i); break; }
        }

        private void OnDestroy()
        {
            if (game?.Quiz != null) game.Quiz.Changed -= Refresh;
            if (game?.Progression != null) game.Progression.Changed -= Refresh;
            if (game != null) game.Message -= ShowMessage;
        }
    }
}
