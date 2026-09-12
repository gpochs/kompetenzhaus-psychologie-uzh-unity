using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;

namespace Kompetenzhaus.Competencies
{
    // A descriptive projection of chosen curriculum modules, never an assessment of a person.
    // No mutations, persistence, random values, quiz bonuses or model-generated scores.
    public static class CompetenceProjection
    {
        private static readonly string[][] RadarIds = {
            new[] { "Fa1", "Fa8", "Fa9" }, new[] { "Fa2", "Fa3", "Fa5" },
            new[] { "Fa4", "Fa6", "Fa7", "Fa10" }, new[] { "KI1", "KI2", "KI3" },
            new[] { "KI4", "KI5", "KI6" }, new[] { "Fu1", "Fu2", "Fu3" }
        };
        private static readonly string[] RadarNames = { "knowledge", "methods", "thinking", "aiCreation", "aiJudgement", "future" };
        private static readonly string[] RadarGerman = { "Fachwissen & Transfer", "Methoden, Daten & Diagnostik", "Denken, Ethik & Kommunikation", "Mit KI arbeiten & gestalten", "KI verstehen, prüfen & verantworten", "Future Skills (mit & ohne KI)" };
        private static readonly string[] RadarEnglish = { "Knowledge & transfer", "Methods, data & diagnostics", "Thinking, ethics & communication", "Working & creating with AI", "Understanding, auditing & owning AI", "Future skills (with & without AI)" };

        public static bool ValidateChoice(CompetenceChoice choice, out string error)
        {
            error = null;
            if (choice == null || !LegacyCompetenceRules.Modules.Any(rule => rule.moduleId == choice.moduleId))
            { error = "Unknown module for competency choice."; return false; }
            foreach (var pair in new[] { ("specialisation", choice.specialisationId), ("topic", choice.topicId), ("thesisQuestion", choice.thesisQuestionId) })
                if (!string.IsNullOrEmpty(pair.Item2) && !LegacyCompetenceRules.Options.Any(option => option.moduleId == choice.moduleId && option.kind == pair.Item1 && option.id == pair.Item2 &&
                    (pair.Item1 != "thesisQuestion" || option.parentId == choice.topicId)))
                { error = "This competency choice is not available for the selected module and topic."; return false; }
            return true;
        }

        public static CompetenceProfileSnapshot SimulateChoiceProfile(ContentCatalog catalog, ProgressionService progression,
            string bachelorDirection = null, string specialisationId = null, string optionalModuleCode = null)
        {
            if (!string.IsNullOrEmpty(bachelorDirection) && !new[] { "klin", "ekn", "swo" }.Contains(bachelorDirection)) throw new ArgumentException("Unknown Bachelor direction.");
            if (!string.IsNullOrEmpty(specialisationId) && !new[] { "DeNC", "HEA", "SEOP" }.Contains(specialisationId)) throw new ArgumentException("Unknown Master specialisation.");
            if (!string.IsNullOrEmpty(optionalModuleCode) && !(catalog.GetModule("wp")?.optionCodes ?? Array.Empty<string>()).Contains(optionalModuleCode)) throw new ArgumentException("Unknown elective option.");
            var original = progression.Data;
            var copy = new ProgressData
            {
                learningMode = original.learningMode, directMasterEntry = original.directMasterEntry,
                placedModuleIds = new List<string>(original.placedModuleIds),
                quizMasteredModuleIds = new List<string>(original.quizMasteredModuleIds),
                selfCheckedModuleIds = new List<string>(original.selfCheckedModuleIds),
                moduleChoices = original.moduleChoices.Select(choice => new ModuleChoice { slotId = choice.slotId, moduleCode = choice.moduleCode }).ToList(),
                competenceChoices = original.competenceChoices.Select(choice => new CompetenceChoice { moduleId = choice.moduleId, specialisationId = choice.specialisationId, topicId = choice.topicId, thesisQuestionId = choice.thesisQuestionId }).ToList(),
                preStageChecks = new List<int>(original.preStageChecks)
            };
            foreach (var moduleId in copy.placedModuleIds)
            {
                var topic = string.IsNullOrEmpty(bachelorDirection) ? null : LegacyCompetenceRules.Options.FirstOrDefault(option => option.moduleId == moduleId && option.kind == "topic" && option.directionId == bachelorDirection);
                var specialisation = string.IsNullOrEmpty(specialisationId) ? null : LegacyCompetenceRules.Options.FirstOrDefault(option => option.moduleId == moduleId && option.kind == "specialisation" && option.id == specialisationId);
                if (topic == null && specialisation == null) continue;
                var choice = copy.competenceChoices.FirstOrDefault(item => item.moduleId == moduleId);
                if (choice == null) { choice = new CompetenceChoice { moduleId = moduleId }; copy.competenceChoices.Add(choice); }
                if (topic != null) { choice.topicId = topic.id; if (moduleId == "BA") choice.thesisQuestionId = null; }
                if (specialisation != null) choice.specialisationId = specialisation.id;
            }
            if (!string.IsNullOrEmpty(optionalModuleCode) && copy.placedModuleIds.Contains("wp"))
            {
                copy.moduleChoices.RemoveAll(choice => choice.slotId == "wp");
                copy.moduleChoices.Add(new ModuleChoice { slotId = "wp", moduleCode = optionalModuleCode });
            }
            var preview = Create(catalog, new ProgressionService(catalog, copy, () => { throw new InvalidOperationException("Preview cannot persist."); }), copy.competenceChoices, copy.preStageChecks);
            preview.isPreview = true;
            return preview;
        }

        public static CompetenceProfileSnapshot Create(ContentCatalog catalog, ProgressionService progression,
            IEnumerable<CompetenceChoice> choices = null, IEnumerable<int> preStageChecks = null)
        {
            if (catalog == null || progression == null) throw new ArgumentNullException();
            var selection = new Dictionary<string, CompetenceChoice>(StringComparer.Ordinal);
            foreach (var choice in choices ?? Enumerable.Empty<CompetenceChoice>())
            {
                if (!ValidateChoice(choice, out var error)) throw new ArgumentException(error);
                if (!selection.TryAdd(choice.moduleId, choice)) throw new ArgumentException("Duplicate competency choice.");
            }
            var checks = new HashSet<int>(preStageChecks ?? Enumerable.Empty<int>());
            if (checks.Any(index => index < 0 || index >= LegacyCompetenceRules.PreStageChecks.Length)) throw new ArgumentException("Unknown pre-stage check.");
            var built = new HashSet<string>(progression.Data.placedModuleIds, StringComparer.Ordinal);
            var modules = catalog.Document.modules;
            var values = catalog.Document.competencies.ToDictionary(k => k.id, k => new CompetenceValue { id = k.id }, StringComparer.Ordinal);
            var actual = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var primary = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var references = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var volume = values.Keys.ToDictionary(id => id, id => new double[5]);
            var builtVolume = values.Keys.ToDictionary(id => id, id => new double[5]);
            var maxStages = values.Keys.ToDictionary(id => id, id => 0);
            foreach (var slot in modules)
            {
                var content = progression.GetContentForSlot(slot.id);
                var ids = new HashSet<string>(content.competencyIds ?? Array.Empty<string>());
                var main = new HashSet<string>(content.primaryCompetencyIds ?? Array.Empty<string>());
                if (built.Contains(slot.id) && selection.TryGetValue(slot.id, out var choice))
                    foreach (var option in SelectedOptions(choice)) foreach (var id in option.competencyIds) { ids.Add(id); main.Add(id); }
                actual[slot.id] = ids; primary[slot.id] = main;
                var reference = LegacyCompetenceRules.Modules.FirstOrDefault(rule => rule.moduleId == slot.id);
                var refIds = new HashSet<string>(reference?.maximumIds ?? slot.competencyIds ?? Array.Empty<string>());
                var refMain = new HashSet<string>(reference?.maximumPrimaryIds ?? slot.primaryCompetencyIds ?? Array.Empty<string>());
                references[slot.id] = refIds;
                // Preserve the original base-slot ECTS cap, even when an elective's actual ECTS differ.
                var weight = Math.Min(slot.ects, 8d);
                foreach (var id in refIds) if (values.TryGetValue(id, out var value))
                {
                    value.maximum += weight * (refMain.Contains(id) ? 2 : 1);
                    if (slot.stage >= 1 && slot.stage <= 4) volume[id][slot.stage] += weight;
                }
                foreach (var id in ids) if (values.TryGetValue(id, out var value))
                {
                    maxStages[id] = Math.Max(maxStages[id], slot.stage);
                    if (!built.Contains(slot.id)) continue;
                    value.score += weight * (main.Contains(id) ? 2 : 1);
                    if (slot.stage >= 1 && slot.stage <= 4) builtVolume[id][slot.stage] += weight;
                }
            }
            foreach (var index in checks) foreach (var id in LegacyCompetenceRules.PreStageChecks[index].competencyIds)
                if (values.TryGetValue(id, out var value) && value.maximum > 0) value.score = Math.Min(value.maximum, value.score + 0.025 * value.maximum);
            foreach (var value in values.Values)
            {
                value.ratio = value.maximum > 0 ? value.score / value.maximum : 0;
                value.percent = Round(value.ratio * 100);
                for (var stage = 1; stage <= 4; stage++)
                    if (volume[value.id][stage] > 0 && builtVolume[value.id][stage] / volume[value.id][stage] >= 0.4) value.stage = stage;
                var relevant = modules.Where(slot => actual[slot.id].Contains(value.id));
                value.builtModuleIds = relevant.Where(slot => built.Contains(slot.id)).OrderByDescending(slot => (primary[slot.id].Contains(value.id) ? 2 : 1) * slot.ects).Select(slot => slot.id).ToArray();
                value.availableModuleIds = relevant.Where(slot => !built.Contains(slot.id)).OrderByDescending(slot => progression.GetEligibility(slot.id).Allowed)
                    .ThenByDescending(slot => (primary[slot.id].Contains(value.id) ? 2 : 1) * slot.ects).Select(slot => slot.id).ToArray();
            }
            var choiceProfile = GetChoiceProfile(selection, built);
            var hasEvidence = built.Count > 0 || checks.Count > 0 || selection.Values.Any(c => !string.IsNullOrEmpty(c.specialisationId) || !string.IsNullOrEmpty(c.topicId) || !string.IsNullOrEmpty(c.thesisQuestionId));
            var careers = LegacyCompetenceRules.Careers.Select(rule =>
            {
                var weights = rule.weightIds.Select((id, i) => new { id, value = rule.weights[i] }).ToDictionary(x => x.id, x => x.value);
                var a = values.Values.Select(value => value.ratio).ToArray();
                var b = values.Values.Select(value => weights.TryGetValue(value.id, out var weight) ? weight : 0).ToArray();
                var meanA = a.Length > 0 ? a.Average() : 0; var meanB = b.Length > 0 ? b.Average() : 0;
                double numerator = 0, denominatorA = 0, denominatorB = 0;
                for (var i = 0; i < a.Length; i++) { var x = a[i] - meanA; var y = b[i] - meanB; numerator += x * y; denominatorA += x * x; denominatorB += y * y; }
                var correlation = denominatorA > 0 && denominatorB > 0 ? numerator / (Math.Sqrt(denominatorA) * Math.Sqrt(denominatorB)) : 0;
                var shapeFit = (correlation + 1) / 2;
                var choiceFit = ChoiceFit(rule, choiceProfile, selection, built, progression);
                var gaps = rule.targetIds.Select((id, i) => new CompetenceGap { competencyId = id, currentStage = values.TryGetValue(id, out var value) ? value.stage : 0,
                    targetStage = Math.Min(rule.targetStages[i], maxStages.TryGetValue(id, out var maxStage) ? maxStage : 0) })
                    .Where(gap => gap.currentStage < gap.targetStage).OrderByDescending(gap => gap.targetStage - gap.currentStage).ToArray();
                return new CareerProjection { id = rule.id, name = rule.name, description = rule.description, targetRadar = rule.targetRadar,
                    hasEvidence = hasEvidence, fitPercent = Math.Max(0, Math.Min(100, Round((choiceFit.HasValue ? 0.62 * choiceFit.Value + 0.38 * shapeFit : shapeFit) * 100))), gaps = gaps,
                    recommendedModuleIds = modules.Where(slot => !built.Contains(slot.id)).Select(slot => new { slot, weight = actual[slot.id].Sum(id => (weights.TryGetValue(id, out var w) ? w : 0) * (primary[slot.id].Contains(id) ? 2 : 1)) })
                        .Where(candidate => candidate.weight > 0).OrderByDescending(candidate => progression.GetEligibility(candidate.slot.id).Allowed).ThenByDescending(candidate => candidate.weight).Take(3).Select(candidate => candidate.slot.id).ToArray() };
            }).ToArray();
            var snapshot = new CompetenceProfileSnapshot
            {
                competencies = values.Values.ToArray(), choices = choiceProfile, careers = careers,
                fields = new[] { "fa", "ki", "fu" }.Select(field => Aggregate(field, null, catalog.Document.competencies.Where(k => k.fieldId == field).Select(k => k.id).ToArray(), values)).ToArray(),
                radar = RadarIds.Select((ids, i) => Aggregate(RadarNames[i], new LocalizedText { de = RadarGerman[i], en = RadarEnglish[i] }, ids, values)).ToArray(),
                credits = new[] { "bsc", "msc" }.Select(house => new DegreeCredits { houseId = house, selectedEcts = modules.Where(slot => slot.houseId == house && built.Contains(slot.id)).Sum(slot => (double)progression.GetContentForSlot(slot.id).ects) }).ToArray(),
                choiceOptions = LegacyCompetenceRules.Options, preStageOptions = LegacyCompetenceRules.PreStageChecks,
                semesters = LegacyCompetenceRules.Semesters.Select(term =>
                {
                    var result = new SemesterProjection { id = term.id };
                    foreach (var moduleId in term.moduleIds) if (built.Contains(moduleId) && catalog.TryGetModule(moduleId, out var slot))
                    {
                        result.fa += actual[moduleId].Count(id => id.StartsWith("Fa", StringComparison.Ordinal)) * slot.ects;
                        result.ki += actual[moduleId].Count(id => id.StartsWith("KI", StringComparison.Ordinal)) * slot.ects;
                        result.fu += actual[moduleId].Count(id => id.StartsWith("Fu", StringComparison.Ordinal)) * slot.ects;
                    }
                    return result;
                }).ToArray()
            };
            return snapshot;
        }

        private static IEnumerable<CompetenceChoiceOption> SelectedOptions(CompetenceChoice choice) => LegacyCompetenceRules.Options.Where(option => option.moduleId == choice.moduleId &&
            ((option.kind == "specialisation" && option.id == choice.specialisationId) || (option.kind == "topic" && option.id == choice.topicId) ||
            (option.kind == "thesisQuestion" && option.id == choice.thesisQuestionId && option.parentId == choice.topicId)));

        private static ChoiceProfile GetChoiceProfile(Dictionary<string, CompetenceChoice> selection, HashSet<string> built)
        {
            var bachelor = new[] { "klin", "ekn", "swo" }.Select(id => new CompetenceCount { id = id }).ToArray();
            var master = new[] { "DeNC", "HEA", "SEOP" }.Select(id => new CompetenceCount { id = id }).ToArray();
            foreach (var choice in selection.Values.Where(choice => built.Contains(choice.moduleId)))
                foreach (var option in SelectedOptions(choice))
                {
                    if (option.kind == "topic") { var count = bachelor.FirstOrDefault(c => c.id == option.directionId); if (count != null) count.count++; }
                    if (option.kind == "specialisation") { var count = master.FirstOrDefault(c => c.id == option.id); if (count != null) count.count++; }
                }
            var highest = bachelor.Max(count => count.count);
            return new ChoiceProfile { bachelorCounts = bachelor, masterCounts = master,
                bachelorDirection = highest >= 2 && bachelor.Count(count => count.count == highest) == 1 ? bachelor.First(count => count.count == highest).id : null,
                masterSpecialisation = master.FirstOrDefault(count => count.count >= 4)?.id };
        }

        private static double? ChoiceFit(CareerRule rule, ChoiceProfile profile, Dictionary<string, CompetenceChoice> choices, HashSet<string> built, ProgressionService progression)
        {
            double count = 0, matched = 0;
            if (!string.IsNullOrEmpty(rule.directionId)) { count++; if (profile.bachelorDirection == rule.directionId) matched++; }
            if (!string.IsNullOrEmpty(rule.specialisationId)) { count++; matched += Math.Min(1d, (profile.masterCounts.FirstOrDefault(c => c.id == rule.specialisationId)?.count ?? 0) / 4d); }
            if (!string.IsNullOrEmpty(rule.optionalModuleCode)) { count += 0.7; if (built.Contains("wp") && progression.GetSelectedCode("wp") == rule.optionalModuleCode) matched += 0.7; }
            if (count > 0 && built.Contains("BA") && choices.TryGetValue("BA", out var bachelor))
            {
                var question = SelectedOptions(bachelor).FirstOrDefault(option => option.kind == "thesisQuestion");
                if (question != null) { count += 0.7; if (question.careerIds.Contains(rule.id)) matched += 0.7; }
            }
            return count > 0 ? matched / count : (double?)null;
        }

        private static CompetenceAggregate Aggregate(string id, LocalizedText name, string[] ids, Dictionary<string, CompetenceValue> values)
        {
            var present = ids.Where(values.ContainsKey).Select(key => values[key]).ToArray();
            var score = present.Sum(value => value.score); var maximum = present.Sum(value => value.maximum);
            var ratio = maximum > 0 ? score / maximum : 0;
            return new CompetenceAggregate { id = id, name = name, competencyIds = ids, score = score, maximum = maximum, ratio = ratio, percent = Round(ratio * 100) };
        }
        // JavaScript Math.round on these nonnegative quantities, not C#'s banker's rounding.
        private static int Round(double value) => (int)Math.Floor(value + 0.5);
    }
}
