using System;
using System.IO;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.Presentation;
using Kompetenzhaus.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kompetenzhaus.Editor
{
    public static class ProjectTools
    {
        public const string ScenePath = "Assets/Kompetenzhaus/Scenes/Kompetenzhaus.unity";
        public const string ResourcePath = "Assets/Kompetenzhaus/Resources/kompetenzhaus-content.json";
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        private static string RepositoryRoot
        {
            get
            {
                var arguments = Environment.GetCommandLineArgs();
                var sourceArgument = Array.IndexOf(arguments, "-sourceRoot");
                if (sourceArgument >= 0 && sourceArgument + 1 < arguments.Length)
                    return Path.GetFullPath(arguments[sourceArgument + 1]);
                var parent = Directory.GetParent(ProjectRoot);
                if (parent != null && File.Exists(Path.Combine(parent.FullName, "content", "kompetenzhaus-content.json"))) return parent.FullName;
                throw new InvalidOperationException("Pass -sourceRoot <repository path> when launching this project through a short drive alias.");
            }
        }

        [MenuItem("Kompetenzhaus/Sync content")]
        public static void SyncContent()
        {
            var source = Path.Combine(RepositoryRoot, "content", "kompetenzhaus-content.json");
            if (!File.Exists(source)) throw new FileNotFoundException("Run the repository content exporter first.", source);
            var json = File.ReadAllText(source);
            var catalog = ContentCatalog.FromJson(json);
            FrameworkV2ContentSync.SyncFromRepository(RepositoryRoot, catalog);
            Directory.CreateDirectory(Path.GetDirectoryName(ResourcePath));
            File.WriteAllText(ResourcePath, json);
            AssetDatabase.ImportAsset(ResourcePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kompetenzhaus] Synced {catalog.Document.modules.Length} module slots and {catalog.Document.quests.Length} quests.");
        }

        [MenuItem("Kompetenzhaus/Create development scaffold scene")]
        public static void CreateScaffoldScene()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists. This command never replaces a reviewed scene.");
            SyncContent();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var controllers = new GameObject("Campus Camera Controller").AddComponent<CameraModeController>();
            var bird = new GameObject("Bird Camera", typeof(Camera), typeof(AudioListener));
            bird.tag = "MainCamera";
            controllers.birdCamera = bird.GetComponent<Camera>();
            controllers.birdCamera.orthographic = true;
            controllers.birdCamera.orthographicSize = 18f;
            controllers.birdCamera.backgroundColor = new Color(0.10f, 0.16f, 0.22f);
            controllers.birdCamera.clearFlags = CameraClearFlags.SolidColor;
            controllers.FitBirdView(new Bounds(Vector3.zero, new Vector3(24f, 12f, 18f)));

            var player = new GameObject("Player", typeof(CharacterController));
            controllers.player = player.GetComponent<CharacterController>();
            controllers.player.height = 1.75f;
            controllers.player.radius = 0.3f;
            controllers.player.center = new Vector3(0f, 0.875f, 0f);
            controllers.player.stepOffset = 0.25f;
            controllers.player.slopeLimit = 45f;
            controllers.player.enabled = false;
            var firstPerson = new GameObject("First Person Camera", typeof(Camera), typeof(AudioListener));
            firstPerson.tag = "MainCamera";
            firstPerson.transform.SetParent(player.transform, false);
            firstPerson.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            controllers.firstPersonCamera = firstPerson.GetComponent<Camera>();
            controllers.firstPersonCamera.fieldOfView = 65f;
            firstPerson.SetActive(false);
            // Art/layout review owns adding the campus, colliders and entry point, then enabling this flag.
            controllers.worldReady = false;

            var application = new GameObject("Kompetenzhaus").AddComponent<GameBootstrap>();
            application.cameraController = controllers;
            var bridge = new GameObject("KompetenzhausBridge").AddComponent<KompetenzhausBridge>();
            bridge.game = application;
            var hud = new GameObject("Development UI").AddComponent<PrototypeHud>();
            hud.game = application;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigureWebPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kompetenzhaus] Development scene saved. Final campus composition is intentionally pending.");
        }

        public static void ConfigureWebPlayer()
        {
            PlayerSettings.companyName = "Kompetenzhaus Psychologie UZH";
            PlayerSettings.productName = "Kompetenzhaus Psychologie UZH Unity";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 512;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Kompetenzhaus/Validate and save")]
        public static void ValidateAndSave()
        {
            SyncContent();
            var catalog = ContentCatalog.LoadResource();
            var report = new ValidationReport
            {
                editorVersion = Application.unityVersion,
                schemaVersion = catalog.Document.schemaVersion,
                modules = catalog.Document.modules.Length,
                bachelorModules = catalog.ModulesForHouse("bsc").Count(),
                masterModules = catalog.ModulesForHouse("msc").Count(),
                quizBanks = catalog.Document.quizBanks.Length,
                uniqueQuestions = catalog.Document.quizBanks.Sum(bank => bank.questions.Length),
                quests = catalog.Document.quests.Length,
                questQuestions = catalog.Document.quests.Sum(quest => quest.questions.Length),
                sceneExists = File.Exists(ScenePath),
                webSupportInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)
            };
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Logs"));
            File.WriteAllText(Path.Combine(ProjectRoot, "Logs", "content-validation.json"), JsonUtility.ToJson(report, true));
            AssetDatabase.SaveAssets();
            Debug.Log("[Kompetenzhaus] VALIDATED " + JsonUtility.ToJson(report));
        }

        public static void ImportAndSave()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateAndSave();
            Debug.Log("[Kompetenzhaus] IMPORT_AND_COMPILE_COMPLETE");
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string editorVersion;
            public int schemaVersion, modules, bachelorModules, masterModules, quizBanks, uniqueQuestions, quests, questQuestions;
            public bool sceneExists, webSupportInstalled;
        }
    }

    public static class BuildGame
    {
        [MenuItem("Kompetenzhaus/Build WebGL locally")]
        public static void Web()
        {
            if (!File.Exists(ProjectTools.ScenePath)) throw new InvalidOperationException("Create and review the game scene before building.");
            ProjectTools.SyncContent();
            ProjectTools.ConfigureWebPlayer();
            var arguments = Environment.GetCommandLineArgs();
            var outputArgument = Array.IndexOf(arguments, "-buildOutput");
            var output = outputArgument >= 0 && outputArgument + 1 < arguments.Length
                ? arguments[outputArgument + 1] : Path.Combine(ProjectTools.ProjectRoot, "Build", "WebGL");
            output = Path.GetFullPath(output);
            var allowedRoot = ProjectTools.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build output must remain inside this Unity project. Deployment is a separate action.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectTools.ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Web build failed: {report.summary.result}, errors: {report.summary.totalErrors}.");
            Debug.Log($"[Kompetenzhaus] WEB_BUILD_COMPLETE bytes={report.summary.totalSize}");
        }
    }
}
