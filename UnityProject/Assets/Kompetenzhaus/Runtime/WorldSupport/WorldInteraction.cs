using System;
using Kompetenzhaus.Content;
using Kompetenzhaus.Gameplay;
using UnityEngine;

namespace Kompetenzhaus.WorldSupport
{
    public enum WorldTargetStatus { Unavailable, Available, Recommended, Completed }
    // Attach to an authored target or one of its collider parents. Configure exactly one id.
    [DisallowMultipleComponent]
    public sealed class WorldInteraction : MonoBehaviour
    {
        public string moduleId;
        public string questId;
        public LocalizedText label = new LocalizedText();

        public bool IsValid(GameBootstrap game)
        {
            if (game?.Catalog == null || string.IsNullOrEmpty(moduleId) == string.IsNullOrEmpty(questId)) return false;
            return !string.IsNullOrEmpty(moduleId) ? game.Catalog.HasModule(moduleId)
                : Array.Exists(game.Catalog.Document.quests, quest => quest.id == questId);
        }

        public string GetLabel(GameBootstrap game)
        {
            var language = game?.Progression?.Data.language ?? "de";
            var custom = label?.Get(language);
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            if (game?.Catalog == null) return "";
            if (!string.IsNullOrEmpty(moduleId) && game.Catalog.HasModule(moduleId))
                return game.Progression.GetContentForSlot(moduleId).title?.Get(language) ?? "";
            return Array.Find(game.Catalog.Document.quests, quest => quest.id == questId)?.title?.Get(language) ?? "";
        }

        public bool Activate(GameBootstrap game)
        {
            if (!IsValid(game)) return false;
            return !string.IsNullOrEmpty(moduleId) ? game.StartModuleQuiz(moduleId) : game.StartQuest(questId);
        }

        public WorldTargetStatus GetStatus(GameBootstrap game, string recommendedQuestId = null)
        {
            if (!IsValid(game) || game.Progression == null) return WorldTargetStatus.Unavailable;
            if (!string.IsNullOrEmpty(moduleId))
            {
                if (game.Catalog.GetQuestions(game.Progression.GetContentForSlot(moduleId)).Length == 0) return WorldTargetStatus.Unavailable;
                return game.Progression.Data.quizMasteredModuleIds.Contains(moduleId) ? WorldTargetStatus.Completed : WorldTargetStatus.Available;
            }
            if (game.Progression.Data.completedQuestIds.Contains(questId)) return WorldTargetStatus.Completed;
            if (!game.Progression.CanStartQuest(questId)) return WorldTargetStatus.Unavailable;
            recommendedQuestId ??= JourneyDirector.Create(game.Catalog, game.Progression).questId;
            return questId == recommendedQuestId ? WorldTargetStatus.Recommended : WorldTargetStatus.Available;
        }

        public string GetHint(GameBootstrap game, bool firstPerson)
        {
            var english = game?.Progression?.Data.language == "en";
            var status = GetStatus(game);
            var labelText = GetLabel(game);
            if (status == WorldTargetStatus.Unavailable || (!string.IsNullOrEmpty(questId) && !game.Progression.CanStartQuest(questId)))
            {
                if (!string.IsNullOrEmpty(questId))
                {
                    var quest = Array.Find(game.Catalog.Document.quests, item => item.id == questId);
                    var prerequisite = Array.Find(quest?.prerequisiteQuestIds ?? Array.Empty<string>(), id => !game.Progression.Data.completedQuestIds.Contains(id));
                    var title = Array.Find(game.Catalog.Document.quests, item => item.id == prerequisite)?.title?.Get(game.Progression.Data.language);
                    if (!string.IsNullOrEmpty(title)) return labelText + (english ? " · First: " : " · Zuerst: ") + title;
                }
                return labelText + (english ? " · No task available yet" : " · Noch keine Aufgabe verfügbar");
            }
            var action = status == WorldTargetStatus.Completed ? (english ? "Revisit" : "Erneut prüfen")
                : !string.IsNullOrEmpty(questId) ? (english ? "Explore assignment" : "Auftrag erkunden") : (english ? "Practise" : "Üben");
            return (firstPerson ? "E · " : english ? "Click · " : "Klicken · ") + action + " · " + labelText;
        }
    }
}
