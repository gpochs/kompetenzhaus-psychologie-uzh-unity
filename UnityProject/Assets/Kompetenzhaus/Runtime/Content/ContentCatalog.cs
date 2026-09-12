using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kompetenzhaus.Content
{
    public sealed class ContentCatalog
    {
        public ContentDocument Document { get; }
        private readonly Dictionary<string, ModuleDefinition> modules;
        private readonly Dictionary<string, QuizBankDefinition> quizBanks;
        private readonly Dictionary<string, ModuleDefinition> options;

        public ContentCatalog(ContentDocument document)
        {
            if (document == null || document.schemaVersion != 1)
                throw new ArgumentException("Unsupported or missing curriculum schema. Expected schemaVersion 1.");
            Document = document;
            modules = Index(document.modules, m => m.id, "module");
            quizBanks = Index(document.quizBanks, b => b.id, "quiz bank");
            options = Index(document.optionalModules, m => m.code, "optional module code");
            if (modules.Count == 0) throw new ArgumentException("Curriculum contains no modules.");
            foreach (var module in modules.Values)
            {
                foreach (var prerequisite in module.prerequisiteIds ?? Array.Empty<string>())
                    if (!modules.ContainsKey(prerequisite))
                        throw new ArgumentException($"Module {module.id} references missing prerequisite {prerequisite}.");
                ValidateQuestions(GetQuestions(module), module.id);
            }
            foreach (var quest in document.quests ?? Array.Empty<QuestDefinition>())
                ValidateQuestions(quest.questions, quest.id);
        }

        public static ContentCatalog LoadResource(string resourceName = "kompetenzhaus-content")
        {
            var resource = Resources.Load<TextAsset>(resourceName);
            if (resource == null) throw new InvalidOperationException("Curriculum asset missing. Run Kompetenzhaus > Sync content in the Editor.");
            return FromJson(resource.text);
        }

        public static ContentCatalog FromJson(string json) => new ContentCatalog(JsonUtility.FromJson<ContentDocument>(json));
        public bool TryGetModule(string id, out ModuleDefinition module) => modules.TryGetValue(id ?? "", out module);
        public ModuleDefinition GetModule(string id) => modules.TryGetValue(id ?? "", out var module) ? module : null;
        public bool HasModule(string id) => id != null && modules.ContainsKey(id);
        public ModuleDefinition GetOptionalModule(string code) => code != null && options.TryGetValue(code, out var option) ? option : null;
        public IEnumerable<ModuleDefinition> ModulesForHouse(string houseId) =>
            modules.Values.Where(module => houseId == "all" || module.houseId == houseId);

        public QuizQuestion[] GetQuestions(ModuleDefinition module)
        {
            if (module.questions != null && module.questions.Length > 0) return module.questions;
            return module.quizBankId != null && quizBanks.TryGetValue(module.quizBankId, out var bank)
                ? bank.questions ?? Array.Empty<QuizQuestion>() : Array.Empty<QuizQuestion>();
        }

        private static Dictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> key, string label) where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var value in values ?? Enumerable.Empty<T>())
            {
                if (value == null || string.IsNullOrWhiteSpace(key(value))) throw new ArgumentException($"Missing {label} id.");
                if (!result.TryAdd(key(value), value)) throw new ArgumentException($"Duplicate {label} id: {key(value)}.");
            }
            return result;
        }

        public static void ValidateQuestions(IEnumerable<QuizQuestion> questions, string owner)
        {
            foreach (var question in questions ?? Enumerable.Empty<QuizQuestion>())
            {
                if (question == null || question.options == null || question.options.Length < 2 ||
                    question.correctIndex < 0 || question.correctIndex >= question.options.Length)
                    throw new ArgumentException($"Invalid quiz options in {owner}.");
                if (question.explanation == null || string.IsNullOrWhiteSpace(question.explanation.de))
                    throw new ArgumentException($"Missing explanation for question {question.id} in {owner}.");
            }
        }
    }
}
