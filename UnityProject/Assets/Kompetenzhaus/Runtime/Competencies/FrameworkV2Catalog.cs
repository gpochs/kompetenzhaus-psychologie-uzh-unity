using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using UnityEngine;

namespace Kompetenzhaus.Competencies
{
    public sealed class FrameworkV2Catalog
    {
        public const string FrameworkResource = "competency-framework";
        public const string CareersResource = "career-profiles";
        public const string LearningResource = "module-learning-design";
        public FrameworkV2Definition Framework { get; }
        public FrameworkV2CareersDefinition Careers { get; }
        public FrameworkV2LearningDesign Learning { get; }
        private readonly Dictionary<string, FrameworkV2ModuleDesign> slots;
        private readonly Dictionary<string, FrameworkV2ModuleDesign> options;

        public FrameworkV2Catalog(FrameworkV2Definition framework, FrameworkV2CareersDefinition careers,
            FrameworkV2LearningDesign learning, ContentCatalog curriculum = null)
        {
            Framework = framework ?? throw new ArgumentNullException(nameof(framework));
            Careers = careers ?? throw new ArgumentNullException(nameof(careers));
            Learning = learning ?? throw new ArgumentNullException(nameof(learning));
            Require(framework.schemaVersion == 2 && framework.schema == "kompetenzhaus.competency-framework", "Unsupported competency framework schema.");
            Require(careers.schemaVersion == 2 && careers.schema == "kompetenzhaus.career-profiles", "Unsupported career profile schema.");
            Require(learning.schemaVersion == 1 && learning.schema == "kompetenzhaus.module-learning-design", "Unsupported module learning design schema.");
            Require(learning.status == "design-proposal", "Learning designs must retain their proposal status.");
            Require(!string.IsNullOrWhiteSpace(framework.version) && careers.frameworkVersion == framework.version && learning.frameworkVersion == framework.version,
                "Framework, careers and module designs must have the same framework version.");
            var domains = Ids(framework.domains, item => item.id, "domain");
            var contexts = Ids(framework.contexts, item => item.id, "context");
            var milestones = Ids(framework.studyMilestones, item => item.id, "study milestone");
            Require(framework.levelDefinitions != null && framework.levelDefinitions.Select(item => item.level).OrderBy(value => value).SequenceEqual(new[] { 1, 2, 3 }),
                "The framework requires exactly three task levels, I, II and III.");
            var competencies = Ids(framework.competencies, item => item.id, "competency");
            Require(competencies.Count > 0, "The framework has no competencies.");
            var criterionOwners = new Dictionary<string, string>();
            foreach (var competency in framework.competencies)
            {
                Require(domains.Contains(competency.domainId), "Unknown domain for " + competency.id);
                Require(competency.criteria != null && competency.criteria.Length > 0, "Missing criteria for " + competency.id);
                foreach (var criterion in competency.criteria)
                {
                    Require(criterion != null && !string.IsNullOrWhiteSpace(criterion.id), "Missing criterion id.");
                    Require(criterionOwners.TryAdd(criterion.id, competency.id), "A criterion may have only one canonical owner: " + criterion.id);
                }
                Require(competency.levels != null && competency.levels.Select(item => item.level).OrderBy(value => value).SequenceEqual(new[] { 1, 2, 3 }),
                    "Missing task-level descriptions for " + competency.id);
            }
            Ids(framework.futureLenses, item => item.id, "future perspective");
            foreach (var competency in framework.competencies)
            {
                var integration = competency.aiIntegration;
                if (integration == null) continue;
                ValidateReferences(integration.relatedCriterionIds, criterionOwners.Keys, "AI integration criterion");
                Require(!string.IsNullOrWhiteSpace(integration.possibleRole?.de) && !string.IsNullOrWhiteSpace(integration.learnerResponsibility?.de) &&
                    !string.IsNullOrWhiteSpace(integration.assessmentFocus?.de) && !string.IsNullOrWhiteSpace(integration.exampleTask?.de),
                    "AI integration needs a role, learner responsibility, assessment focus and example task for " + competency.id);
            }
            foreach (var lens in framework.futureLenses)
            {
                ValidateReferences(lens.competencyIds, competencies, "future perspective competency");
                ValidateReferences(lens.criterionIds, criterionOwners.Keys, "future perspective criterion");
                ValidateReferences(lens.contextIds, contexts, "future perspective context");
                Require(lens.criterionIds.All(id => lens.competencyIds.Contains(criterionOwners[id])), "A future perspective must name every criterion's owner.");
            }
            Ids(careers.roles, item => item.id, "career role");
            foreach (var role in careers.roles)
            {
                Ids(role.facets, item => item.id, "career facet");
                var withinRole = new HashSet<string>();
                foreach (var facet in role.facets)
                {
                    facet.contextIds ??= Array.Empty<string>();
                    ValidateReferences(facet.criterionIds, criterionOwners.Keys, "career criterion");
                    ValidateReferences(facet.contextIds, contexts, "career context");
                    Require(!facet.optionalContext || facet.contextIds.Length > 0, "An optional career context needs explicit context ids.");
                    foreach (var id in facet.criterionIds)
                        Require(withinRole.Add(id), "A criterion appears twice within role " + role.id + ": " + id);
                }
            }
            Ids(learning.modules, item => item.id, "module design");
            slots = new Dictionary<string, FrameworkV2ModuleDesign>();
            options = new Dictionary<string, FrameworkV2ModuleDesign>();
            var objectiveIds = new HashSet<string>();
            foreach (var design in learning.modules)
            {
                Require(design.kind == "slot" || design.kind == "option", "Unknown module design kind.");
                Require(design.baseline != null && design.proposal != null, "Missing baseline or proposal for " + design.id);
                Require(milestones.Contains(design.baseline.stageId), "Unknown study milestone for " + design.id);
                Require(!string.IsNullOrWhiteSpace(design.code), "Missing module code for " + design.id);
                Require(design.proposal.status == "design-proposal", "Module opportunities must retain their proposal status.");
                Require(design.proposal.objectives != null && design.proposal.objectives.Length > 0, "Missing objectives for " + design.id);
                if (design.kind == "slot") Require(slots.TryAdd(design.id, design), "Duplicate slot design.");
                else
                {
                    Require(design.parentSlotIds != null && design.parentSlotIds.Length > 0 && design.parentSlotIds.Distinct().Count() == design.parentSlotIds.Length,
                        "An optional design needs unique parent slots.");
                    Require(options.TryAdd(design.code, design), "Duplicate optional module design code.");
                }
                foreach (var objective in design.proposal.objectives)
                {
                    Require(objective != null && !string.IsNullOrWhiteSpace(objective.id) && objectiveIds.Add(objective.id), "Missing or duplicate objective id.");
                    Require(criterionOwners.ContainsKey(objective.criterionId), "Unknown objective criterion: " + objective.criterionId);
                    Require(objective.targetLevel >= 1 && objective.targetLevel <= 3, "An opportunity must target task level I, II or III.");
                    ValidateReferences(objective.contextIds, contexts, "objective context");
                    Require(objective.contextIds.Length > 0, "An objective needs an explicit task context.");
                    Require(!string.IsNullOrWhiteSpace(objective.text?.de) && !string.IsNullOrWhiteSpace(objective.task?.de) &&
                        !string.IsNullOrWhiteSpace(objective.evidence?.de) && !string.IsNullOrWhiteSpace(objective.successCriteria?.de),
                        "An opportunity needs its objective, task, proposed evidence and success criteria: " + objective.id);
                }
            }
            if (curriculum != null) ValidateCurriculum(curriculum);
        }

        public void ValidateCurriculum(ContentCatalog curriculum)
        {
            foreach (var slot in curriculum.Document.modules)
            {
                Require(slots.ContainsKey(slot.id), "No learning design for curriculum slot " + slot.id);
                var design = slots[slot.id];
                Require(design.code == slot.code && design.baseline.houseId == slot.houseId && design.baseline.stageId == slot.stageId &&
                    Math.Abs(design.baseline.ects - slot.ects) < .001f, "A slot design must preserve its curriculum identity and credits: " + slot.id);
                foreach (var code in slot.optionCodes ?? Array.Empty<string>())
                    Require(code == slot.code || (options.TryGetValue(code, out var choice) && choice.parentSlotIds.Contains(slot.id)),
                        "No matching optional learning design for " + slot.id + "/" + code);
            }
            foreach (var design in Learning.modules)
            {
                if (design.kind == "slot") Require(curriculum.HasModule(design.id), "Unknown curriculum slot in learning designs.");
                else
                {
                    Require(design.parentSlotIds != null && design.parentSlotIds.Length > 0, "An optional design needs parent slots.");
                    var option = curriculum.GetOptionalModule(design.code);
                    Require(option != null && Math.Abs(option.ects - design.baseline.ects) < .001f &&
                        option.houseId == design.baseline.houseId && option.stageId == design.baseline.stageId,
                        "An optional design must preserve its own baseline identity and credits: " + design.code);
                    foreach (var id in design.parentSlotIds)
                        Require(curriculum.HasModule(id) && curriculum.GetModule(id).optionCodes.Contains(design.code), "Optional design references an incompatible slot.");
                }
            }
        }

        public FrameworkV2ModuleDesign Resolve(ModuleDefinition slot, string selectedCode)
        {
            if (slot == null || !slots.TryGetValue(slot.id, out var design)) throw new ArgumentException("Missing slot learning design.");
            if ((slot.optionCodes ?? Array.Empty<string>()).Contains(selectedCode) && options.TryGetValue(selectedCode, out var option) && option.parentSlotIds.Contains(slot.id))
                return option; // Replace the slot objectives; never add both designs.
            if (selectedCode != slot.code) throw new ArgumentException("No learning design for selected option " + selectedCode);
            return design;
        }

        public static FrameworkV2Catalog FromJson(string framework, string careers, string learning, ContentCatalog curriculum = null) => new(
            JsonUtility.FromJson<FrameworkV2Definition>(framework), JsonUtility.FromJson<FrameworkV2CareersDefinition>(careers),
            JsonUtility.FromJson<FrameworkV2LearningDesign>(learning), curriculum);

        public static FrameworkV2Catalog LoadResources(ContentCatalog curriculum = null) => FromJson(ReadResource(FrameworkResource),
            ReadResource(CareersResource), ReadResource(LearningResource), curriculum);

        private static string ReadResource(string name)
        {
            var asset = Resources.Load<TextAsset>(name);
            if (asset == null) throw new InvalidOperationException("Missing " + name + ". Run Kompetenzhaus > Sync content.");
            return asset.text;
        }
        private static HashSet<string> Ids<T>(IEnumerable<T> values, Func<T, string> id, string label) where T : class
        {
            Require(values != null, "Missing " + label + " collection.");
            var found = new HashSet<string>();
            foreach (var item in values) Require(item != null && !string.IsNullOrWhiteSpace(id(item)) && found.Add(id(item)), "Missing or duplicate " + label + " id.");
            return found;
        }
        private static void ValidateReferences(string[] ids, IEnumerable<string> validIds, string label)
        {
            Require(ids != null && ids.Distinct().Count() == ids.Length, "Missing or duplicate " + label + " references.");
            var valid = new HashSet<string>(validIds);
            Require(ids.All(valid.Contains), "Unknown " + label + " reference.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    }
}
