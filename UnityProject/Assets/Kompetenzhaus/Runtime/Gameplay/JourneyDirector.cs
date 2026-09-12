using System;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;

namespace Kompetenzhaus.Gameplay
{
    // Guidance is derived from existing progress. It never grants rewards, changes a
    // prerequisite, or turns an optional route into a gate for free construction.
    [Serializable]
    public sealed class JourneySnapshot
    {
        public string houseId, questId, title, purpose, instructions, location, actionLabel;
        public int completed, total;
        public bool complete, designProposal = true, prerequisiteDetour;
    }

    public static class JourneyDirector
    {
        public static JourneySnapshot Create(ContentCatalog catalog, ProgressionService progression)
        {
            if (catalog == null || progression == null) return new JourneySnapshot();
            var data = progression.Data;
            var house = data.selectedHouseId == "msc" || (data.selectedHouseId == "all" && data.directMasterEntry) ? "msc" : "bsc";
            var english = data.language == "en";
            var all = catalog.Document.quests ?? Array.Empty<QuestDefinition>();
            var route = all.Where(q => q.type == "main" && HouseFor(q) == house).ToArray();
            var result = new JourneySnapshot
            {
                houseId = house,
                total = route.Length,
                completed = route.Count(q => data.completedQuestIds.Contains(q.id)),
                actionLabel = english ? "Explore the assignment" : "Auftrag erkunden"
            };
            result.complete = result.total > 0 && result.completed == result.total;
            if (result.complete)
            {
                result.title = english ? "Your next question" : "Deine nächste Frage";
                result.purpose = english ? "Revisit a decision, explore a side assignment, or keep designing your house."
                    : "Prüfe eine Entscheidung erneut, erkunde einen Nebenauftrag oder gestalte dein Haus weiter.";
                return result;
            }
            var next = route.FirstOrDefault(q => !data.completedQuestIds.Contains(q.id) && progression.CanStartQuest(q.id));
            if (next == null)
            {
                // A guided Master route can legitimately require an earlier BSc quest.
                // Direct Master entry is handled by CanStartQuest and therefore skips it.
                var blocked = route.FirstOrDefault(q => !data.completedQuestIds.Contains(q.id));
                next = FindAvailablePrerequisite(blocked, all, progression, new System.Collections.Generic.HashSet<string>());
                result.prerequisiteDetour = next != null && HouseFor(next) != house;
            }
            if (next == null) return result;
            result.questId = next.id;
            result.title = next.title?.Get(data.language) ?? "";
            result.purpose = next.description?.Get(data.language) ?? "";
            result.instructions = next.instructions?.Get(data.language) ?? "";
            result.location = next.worldLocation;
            return result;
        }

        private static QuestDefinition FindAvailablePrerequisite(QuestDefinition quest, QuestDefinition[] quests,
            ProgressionService progression, System.Collections.Generic.HashSet<string> visited)
        {
            if (quest == null || !visited.Add(quest.id)) return null;
            foreach (var id in quest.prerequisiteQuestIds ?? Array.Empty<string>())
            {
                if (progression.Data.completedQuestIds.Contains(id)) continue;
                var prerequisite = Array.Find(quests, item => item.id == id);
                if (prerequisite == null) continue;
                if (progression.CanStartQuest(id)) return prerequisite;
                var earlier = FindAvailablePrerequisite(prerequisite, quests, progression, visited);
                if (earlier != null) return earlier;
            }
            return null;
        }

        public static string HouseFor(QuestDefinition quest) => quest?.stageId == "3" || quest?.stageId == "4" ? "msc" : "bsc";

        // Each interaction belongs to an actual reviewed prop. Root world composition
        // decides its position; this mapping does not reserve students' building lots.
        public static string StationAssetFor(QuestDefinition quest)
        {
            return quest?.worldLocation switch
            {
                "archive" => "bookshelf",
                "methods-workshop" => "research-desk",
                "bachelor-capstone" => "research-poster",
                "decision-room" => "evidence-table",
                "research-tower" => "research-poster",
                "study-studio" => "research-desk",
                "cognition-gallery" => "acoustic-divider",
                "measurement-workshop" => "evidence-table",
                "team-forum" => "discussion-sofa",
                "health-garden" => "bench",
                "agent-observatory" => "research-desk",
                _ => "evidence-table"
            };
        }
    }
}
