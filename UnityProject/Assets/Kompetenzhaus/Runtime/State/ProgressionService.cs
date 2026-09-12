using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.Competencies;

namespace Kompetenzhaus.State
{
    public sealed class BuildEligibility
    {
        public bool Allowed => MissingPrerequisiteIds.Length == 0 && !NeedsSelfCheck && !NeedsQuiz && !NeedsMasterEntry;
        public string[] MissingPrerequisiteIds = Array.Empty<string>();
        public bool NeedsSelfCheck;
        public bool NeedsQuiz;
        public bool NeedsMasterEntry;
        public bool AlreadyPlaced;
    }

    public sealed class ProgressionService
    {
        public event Action Changed;
        public event Action<ModuleDefinition> ModulePlaced;
        private readonly ContentCatalog catalog;
        private readonly Action save;
        public ProgressData Data { get; }

        public ProgressionService(ContentCatalog catalog, ProgressStore store) : this(catalog, store.Data, store.Save) { }

        public ProgressionService(ContentCatalog catalog, ProgressData data, Action persist)
        {
            this.catalog = catalog;
            Data = data;
            save = persist ?? (() => { });
        }

        public string GetSelectedCode(string slotId)
        {
            var slot = catalog.GetModule(slotId) ?? throw new ArgumentException("Unknown slot.");
            var choice = Data.moduleChoices.Find(c => c.slotId == slotId);
            return choice != null && (slot.optionCodes ?? Array.Empty<string>()).Contains(choice.moduleCode)
                ? choice.moduleCode : slot.code;
        }

        public ModuleDefinition GetContentForSlot(string slotId)
        {
            var slot = catalog.GetModule(slotId) ?? throw new ArgumentException("Unknown slot.");
            return slot.optionCodes != null && slot.optionCodes.Length > 0
                ? catalog.GetOptionalModule(GetSelectedCode(slotId)) ?? slot : slot;
        }

        public bool SetModuleChoice(string slotId, string moduleCode)
        {
            var slot = catalog.GetModule(slotId);
            if (slot == null || Data.placedModuleIds.Contains(slotId) || !(slot.optionCodes ?? Array.Empty<string>()).Contains(moduleCode)) return false;
            var changed = GetSelectedCode(slotId) != moduleCode;
            if (!changed && Data.moduleChoices.Any(c => c.slotId == slotId && c.moduleCode == moduleCode)) return true;
            Data.moduleChoices.RemoveAll(c => c.slotId == slotId);
            Data.moduleChoices.Add(new ModuleChoice { slotId = slotId, moduleCode = moduleCode });
            if (changed)
            {
                Data.quizMasteredModuleIds.Remove(slotId);
                Data.selfCheckedModuleIds.Remove(slotId);
            }
            SaveAndNotify();
            return true;
        }

        public bool CanStartQuest(string questId)
        {
            var quest = Array.Find(catalog.Document.quests ?? Array.Empty<QuestDefinition>(), q => q.id == questId);
            if (quest == null) return false;
            if (Data.learningMode == LearningMode.Free) return true;
            return (quest.prerequisiteQuestIds ?? Array.Empty<string>()).All(prerequisiteId =>
            {
                if (Data.completedQuestIds.Contains(prerequisiteId)) return true;
                var prerequisite = Array.Find(catalog.Document.quests, q => q.id == prerequisiteId);
                return Data.directMasterEntry && catalog.Document.rules?.allowDirectMasterEntry == true &&
                    (quest.stageId == "3" || quest.stageId == "4") && (prerequisite?.stageId == "1" || prerequisite?.stageId == "2");
            });
        }

        public bool SetCompetenceChoice(CompetenceChoice choice, out string error)
        {
            if (!CompetenceProjection.ValidateChoice(choice, out error)) return false;
            if (!catalog.HasModule(choice.moduleId)) { error = "Unknown competency-choice module."; return false; }
            var candidate = new CompetenceChoice
            {
                moduleId = choice.moduleId, specialisationId = choice.specialisationId,
                topicId = choice.topicId, thesisQuestionId = choice.thesisQuestionId
            };
            Data.competenceChoices.RemoveAll(existing => existing.moduleId == choice.moduleId);
            if (!string.IsNullOrEmpty(candidate.specialisationId) || !string.IsNullOrEmpty(candidate.topicId) || !string.IsNullOrEmpty(candidate.thesisQuestionId))
                Data.competenceChoices.Add(candidate);
            SaveAndNotify();
            return true;
        }

        public bool SetPreStageChecks(IEnumerable<int> indexes, out string error)
        {
            var candidate = indexes?.ToList();
            if (candidate == null || candidate.Any(index => index < 0 || index > 3) || candidate.Distinct().Count() != candidate.Count)
            { error = "Invalid self-reported prior-learning selection."; return false; }
            Data.preStageChecks = candidate.OrderBy(index => index).ToList();
            SaveAndNotify();
            error = null;
            return true;
        }

        public BuildEligibility GetEligibility(string moduleId)
        {
            var module = catalog.GetModule(moduleId) ?? throw new ArgumentException("Unknown module.");
            var result = new BuildEligibility { AlreadyPlaced = Data.placedModuleIds.Contains(moduleId) };
            // Free/sandbox mode gives architectural freedom without curriculum prerequisites.
            if (result.AlreadyPlaced || Data.learningMode == LearningMode.Free) return result;
            result.MissingPrerequisiteIds = (module.prerequisiteIds ?? Array.Empty<string>())
                .Where(id => !Data.placedModuleIds.Contains(id)).ToArray();
            var rules = catalog.Document.rules;
            result.NeedsMasterEntry = rules != null && !string.IsNullOrWhiteSpace(rules.masterEntryPrerequisiteId) &&
                (rules.masterStageIds ?? Array.Empty<string>()).Contains(module.stageId) &&
                !(rules.allowDirectMasterEntry && Data.directMasterEntry) && !Data.placedModuleIds.Contains(rules.masterEntryPrerequisiteId);
            if (Data.learningMode == LearningMode.Serious)
            {
                // This is the player's self-report, never an academic completion certificate.
                result.NeedsSelfCheck = !Data.selfCheckedModuleIds.Contains(moduleId);
                result.NeedsQuiz = catalog.GetQuestions(GetContentForSlot(moduleId)).Length > 0 && !Data.quizMasteredModuleIds.Contains(moduleId);
            }
            return result;
        }

        public bool TryPlace(string moduleId, out BuildEligibility eligibility)
        {
            eligibility = GetEligibility(moduleId);
            if (!eligibility.Allowed || eligibility.AlreadyPlaced) return false;
            PreserveBuiltModuleChoice(moduleId);
            Data.placedModuleIds.Add(moduleId);
            SaveAndNotify();
            ModulePlaced?.Invoke(catalog.GetModule(moduleId));
            return true;
        }

        internal void CommitModulePlacement(string moduleId, ArchitectureState architecture)
        {
            var firstBuild = !Data.placedModuleIds.Contains(moduleId);
            if (firstBuild)
            {
                PreserveBuiltModuleChoice(moduleId);
                Data.placedModuleIds.Add(moduleId);
            }
            Data.architecture = architecture;
            SaveAndNotify();
            ModulePlaced?.Invoke(catalog.GetModule(moduleId));
        }

        public void SetSelfCheck(string moduleId, bool checkedByPlayer)
        {
            if (!catalog.HasModule(moduleId)) throw new ArgumentException("Unknown module.");
            SetMembership(Data.selfCheckedModuleIds, moduleId, checkedByPlayer);
            SaveAndNotify();
        }

        private void PreserveBuiltModuleChoice(string slotId)
        {
            var slot = catalog.GetModule(slotId);
            if (slot?.optionCodes == null || slot.optionCodes.Length == 0 || Data.moduleChoices.Any(c => c.slotId == slotId)) return;
            var code = GetSelectedCode(slotId);
            if (slot.optionCodes.Contains(code)) Data.moduleChoices.Add(new ModuleChoice { slotId = slotId, moduleCode = code });
        }

        public void MarkModuleQuizMastered(string moduleId)
        {
            if (!catalog.HasModule(moduleId)) throw new ArgumentException("Unknown module.");
            SetMembership(Data.quizMasteredModuleIds, moduleId, true);
            SaveAndNotify();
        }

        public void MarkQuestCompleted(string questId)
        {
            var quest = Array.Find(catalog.Document.quests ?? Array.Empty<QuestDefinition>(), q => q.id == questId);
            if (quest == null) throw new ArgumentException("Unknown quest.");
            if (!CanStartQuest(questId)) throw new InvalidOperationException("Complete prerequisite quests first.");
            if (Data.completedQuestIds.Contains(questId)) return;
            SetMembership(Data.completedQuestIds, questId, true);
            if (quest.reward != null)
            {
                Data.earnedExperience += Math.Max(0, quest.reward.experience);
                if (!string.IsNullOrWhiteSpace(quest.reward.cosmeticId)) SetMembership(Data.unlockedCosmeticIds, quest.reward.cosmeticId, true);
            }
            SaveAndNotify();
        }

        public void SaveAndNotify()
        {
            save();
            Changed?.Invoke();
        }

        private static void SetMembership(List<string> ids, string id, bool present)
        {
            if (present && !ids.Contains(id)) ids.Add(id);
            if (!present) ids.Remove(id);
        }
    }
}
