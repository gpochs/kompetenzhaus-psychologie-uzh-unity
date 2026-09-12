using System;
using System.IO;
using System.Linq;
using Kompetenzhaus.Competencies;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using UnityEditor;
using UnityEngine;

namespace Kompetenzhaus.Editor
{
    public static class FrameworkV2ContentSync
    {
        private static readonly string[] Names = { FrameworkV2Catalog.FrameworkResource, FrameworkV2Catalog.CareersResource, FrameworkV2Catalog.LearningResource };

        public static void SyncFromRepository(string repositoryRoot, ContentCatalog curriculum)
        {
            var sourceDirectory = Path.Combine(Path.GetFullPath(repositoryRoot), "content");
            var values = Names.Select(name => File.ReadAllText(Path.Combine(sourceDirectory, name + ".json"))).ToArray();
            // Validate the complete source set before writing any resource. A malformed
            // proposal or cross-version mix never replaces a working runtime dataset.
            var validated = FrameworkV2Catalog.FromJson(values[0], values[1], values[2], curriculum);
            const string resourceDirectory = "Assets/Kompetenzhaus/Resources";
            var absoluteDirectory = Path.Combine(Application.dataPath, "Kompetenzhaus", "Resources");
            Directory.CreateDirectory(absoluteDirectory);
            for (var index = 0; index < Names.Length; index++)
            {
                var target = resourceDirectory + "/" + Names[index] + ".json";
                var absoluteTarget = Path.Combine(absoluteDirectory, Names[index] + ".json");
                if (File.Exists(absoluteTarget) && File.ReadAllText(absoluteTarget) == values[index]) continue;
                File.WriteAllText(absoluteTarget, values[index]);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            }
            Debug.Log($"[Kompetenzhaus] Framework V2 synced: {validated.Framework.competencies.Length} competencies, " +
                $"{validated.Framework.competencies.Sum(item => item.criteria.Length)} criteria, {validated.Careers.roles.Length} roles.");
        }

        // Pure DTO fixtures for inspecting the semantic browser shell. No PlayerPrefs,
        // scene creation, asset models, academic evidence or player progress are changed.
        public static void SyncAndExportQaProfiles()
        {
            ProjectTools.SyncContent();
            var curriculum = ContentCatalog.LoadResource();
            var framework = FrameworkV2Catalog.LoadResources(curriculum);
            var directory = Path.Combine(ProjectTools.ProjectRoot, "Logs", "qa-snapshots");
            Directory.CreateDirectory(directory);
            var empty = new ProgressData();
            Export("framework-v2-empty", empty);
            var all = new ProgressData();
            all.placedModuleIds.AddRange(curriculum.Document.modules.Select(item => item.id));
            Export("framework-v2-all-modules", all);
            var allEnglish = new ProgressData { language = "en" };
            allEnglish.placedModuleIds.AddRange(curriculum.Document.modules.Select(item => item.id));
            Export("framework-v2-all-modules-en", allEnglish);
            var mentoring = new ProgressData();
            mentoring.placedModuleIds.AddRange(new[] { "wp", "s01c" });
            mentoring.moduleChoices.Add(new ModuleChoice { slotId = "wp", moduleCode = "06SM200-511" });
            mentoring.moduleChoices.Add(new ModuleChoice { slotId = "s01c", moduleCode = "10SMSTS-505" });
            Export("framework-v2-selected-options", mentoring);
            AssetDatabase.SaveAssets();
            void Export(string name, ProgressData data)
            {
                var profile = FrameworkV2Projection.Create(framework, curriculum, new ProgressionService(curriculum, data, () => { }));
                File.WriteAllText(Path.Combine(directory, name + ".json"), JsonUtility.ToJson(new QaEnvelope { frameworkProfile = profile }, true));
            }
            Debug.Log("[Kompetenzhaus] Framework V2 QA profiles exported; no personal assessment records exist.");
        }

        // Export the current imported resources in English without synchronising or
        // writing curriculum assets. This entry point is for browser localisation QA.
        public static void ExportEnglishQaProfile()
        {
            var curriculum = ContentCatalog.LoadResource();
            var framework = FrameworkV2Catalog.LoadResources(curriculum);
            var data = new ProgressData { language = "en" };
            data.placedModuleIds.AddRange(curriculum.Document.modules.Select(item => item.id));
            var profile = FrameworkV2Projection.Create(framework, curriculum, new ProgressionService(curriculum, data, () => { }));
            var directory = Path.Combine(ProjectTools.ProjectRoot, "Logs", "qa-snapshots");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "framework-v2-all-modules-en.json"),
                JsonUtility.ToJson(new QaEnvelope { frameworkProfile = profile }, true));
            Debug.Log("[Kompetenzhaus] English Framework V2 QA profile exported from unchanged resources.");
        }
        [Serializable] private sealed class QaEnvelope
        {
            public bool isSyntheticQaState = true;
            public FrameworkV2Profile frameworkProfile;
        }
    }
}
