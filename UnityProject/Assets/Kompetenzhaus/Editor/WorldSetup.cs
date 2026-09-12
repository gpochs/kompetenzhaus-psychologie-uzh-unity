using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kompetenzhaus.World;
using Kompetenzhaus.WorldSupport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kompetenzhaus.Editor
{
    // All asset and scene serialization uses the Editor API. This method has an
    // explicit review guard and never runs during ordinary compilation/tests.
    public static class WorldSetup
    {
        public const string ArtRoot = "Assets/Kompetenzhaus/Art";
        [Serializable] private sealed class Reviews { public Review function; public Review form; }
        [Serializable] private sealed class Review { public string status; public string approvedBy; }
        private static readonly string[] RequiredModels = {
            "wall-window", "wall-door", "wall-solid", "wall-partition", "floor-tile", "roof-flat", "roof-gabled", "balcony", "staircase",
            "evidence-table", "research-desk", "discussion-sofa", "courtyard-tree", "student-guide",
            "bookshelf", "lamp", "noticeboard", "chair", "bench", "acoustic-divider", "achievement-display", "research-poster"
        };
        public static string SourceRoot
        {
            get
            {
                var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-sourceRoot");
                if (index >= 0 && index + 1 < args.Length) return Path.GetFullPath(args[index + 1]);
                var parent = Directory.GetParent(ProjectTools.ProjectRoot);
                if (parent != null && Directory.Exists(Path.Combine(parent.FullName, "art"))) return parent.FullName;
                throw new InvalidOperationException("Pass -sourceRoot when using the U: project alias.");
            }
        }

        [MenuItem("Kompetenzhaus/Assemble reviewed world")]
        public static void Assemble()
        {
            RequireReviews();
            FrameworkV2ContentSync.SyncAndExportQaProfiles();
            foreach (var id in RequiredModels)
                if (!File.Exists(ArtRoot + "/Models/" + id + ".fbx")) throw new FileNotFoundException("Reviewed Blender export missing: " + id);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var library = PrepareLibrary();
            PrepareAudio(); SyncWebTemplate();
            if (!File.Exists(ProjectTools.ScenePath)) ProjectTools.CreateScaffoldScene();
            var scene = EditorSceneManager.OpenScene(ProjectTools.ScenePath);
            if (UnityEngine.Object.FindAnyObjectByType<GameWorld>() != null)
                throw new InvalidOperationException("A world already exists. Use a deliberate scene edit; this command never replaces it.");
            var app = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (app == null || app.cameraController == null) throw new InvalidOperationException("Scaffold app/cameras missing.");
            var developmentHud = UnityEngine.Object.FindAnyObjectByType<UI.PrototypeHud>();
            if (developmentHud != null) UnityEngine.Object.DestroyImmediate(developmentHud.gameObject);
            var root = new GameObject("Kompetenzhaus authored campus");
            var world = root.AddComponent<GameWorld>(); world.game = app; world.assets = library;
            var lightObject = new GameObject("Sunlight");
            var sun = lightObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f;
            sun.color = new Color(1f, .94f, .84f); sun.shadows = LightShadows.Soft; sun.shadowStrength = .65f;
            sun.shadowBias = .03f; sun.shadowNormalBias = .15f; sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            world.sun = sun;
            var audio = root.AddComponent<WorldAudio>();
            audio.uiHover = Clip("ui-hover"); audio.uiSelect = Clip("ui-select"); audio.uiUnavailable = Clip("ui-unavailable");
            audio.buildPlace = Clip("build-place"); audio.questComplete = Clip("quest-complete"); audio.milestone = Clip("milestone");
            audio.footstepA = Clip("footstep-a"); audio.footstepB = Clip("footstep-b"); audio.courtyardAmbience = Clip("courtyard-ambience");
            root.AddComponent<WorldInteractor>();
            var camera = app.cameraController;
            camera.entryPoint = new Vector3(0, .15f, -18); camera.worldReady = false;
            camera.birdCamera.backgroundColor = new Color(.77f, .82f, .81f);
            camera.birdCamera.farClipPlane = 220; camera.birdCamera.nearClipPlane = .1f;
            camera.firstPersonCamera.backgroundColor = camera.birdCamera.backgroundColor;
            camera.firstPersonCamera.farClipPlane = 200; camera.firstPersonCamera.nearClipPlane = .08f;
            camera.player.stepOffset = .28f; camera.player.skinWidth = .035f;
            camera.FitBirdView(new Bounds(new Vector3(0, 2, 0), new Vector3(80, 12, 40)));
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.72f, .77f, .8f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.78f, .82f, .8f); RenderSettings.fogStartDistance = 95; RenderSettings.fogEndDistance = 180;
            QualitySettings.shadowDistance = 70; QualitySettings.antiAliasing = 2;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Kompetenzhaus] REVIEWED_WORLD_ASSEMBLED. Runtime verification is still required.");
        }

        private static void RequireReviews()
        {
            var reviews = JsonUtility.FromJson<Reviews>(File.ReadAllText(Path.Combine(SourceRoot, "art/review/milestone-reviews.json")));
            foreach (var review in new[] { reviews.function, reviews.form })
                if (review == null || review.status != "approved" || string.IsNullOrWhiteSpace(review.approvedBy))
                    throw new InvalidOperationException("Actual Function and Form review records are required before world assembly.");
        }

        private static WorldAssetLibrary PrepareLibrary()
        {
            var materialManifestPath = Path.Combine(SourceRoot, "art/models/material-manifest.json");
            var materialManifest = JsonUtility.FromJson<MaterialManifest>(File.ReadAllText(materialManifestPath));
            if (materialManifest == null || materialManifest.schema != "kompetenzhaus.materials.v1" || materialManifest.materials == null)
                throw new InvalidOperationException("The reviewed Blender material manifest is missing or invalid.");
            var authoredMaterials = materialManifest.materials.ToDictionary(m => m.name, StringComparer.Ordinal);
            Directory.CreateDirectory(ArtRoot + "/Prefabs"); Directory.CreateDirectory(ArtRoot + "/Materials");
            var libraryPath = ArtRoot + "/WorldAssets.asset";
            var library = AssetDatabase.LoadAssetAtPath<WorldAssetLibrary>(libraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<WorldAssetLibrary>(); AssetDatabase.CreateAsset(library, libraryPath); }
            var entries = new List<WorldAssetLibrary.Entry>();
            foreach (var id in RequiredModels)
            {
                var modelPath = ArtRoot + "/Models/" + id + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                // Players build MeshColliders as the user's architecture changes.
                // The Editor can access non-readable meshes; a Web player cannot.
                importer.isReadable = true; importer.meshCompression = ModelImporterMeshCompression.Low;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) throw new InvalidOperationException("FBX import failed: " + id);
                var instance = new GameObject(id);
                var geometry = UnityEngine.Object.Instantiate(model, instance.transform); geometry.name = id + " geometry";
                if (id == "staircase") NormalizeStairHeading(geometry);
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        var source = materials[i]; var name = source != null ? source.name : "GameMat_Stone";
                        if (!authoredMaterials.TryGetValue(name, out var authored))
                            throw new InvalidOperationException("Imported material has no authored definition: " + name);
                        materials[i] = AuthoredMaterial(authored);
                    }
                    renderer.sharedMaterials = materials;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, ArtRoot + "/Prefabs/" + id + ".prefab");
                UnityEngine.Object.DestroyImmediate(instance);
                entries.Add(new WorldAssetLibrary.Entry { id = id, model = prefab });
            }
            library.entries = entries.ToArray();
            library.ground = Material("Campus lawn", new Color(.48f, .56f, .41f), .1f);
            library.path = Material("Limestone paving", new Color(.68f, .66f, .57f), .15f);
            library.ivory = Material("Ivory site", new Color(.83f, .82f, .73f), .2f);
            library.navy = Material("Campus navy", new Color(.07f, .17f, .23f), .32f);
            library.copper = Material("Campus copper", new Color(.71f, .43f, .23f), .5f);
            library.foliage = Material("Sage foliage", new Color(.42f, .53f, .35f), .1f);
            library.labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EditorUtility.SetDirty(library); return library;
        }

        [MenuItem("Kompetenzhaus/Refresh reviewed model library")]
        public static void RefreshReviewedModels()
        {
            RequireReviews();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PrepareLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kompetenzhaus] REVIEWED_MODELS_REFRESHED. Existing scene and player saves retained.");
        }

        public static void PrepareRuntimeMeshAccess()
        {
            foreach (var id in RequiredModels)
            {
                var importer = AssetImporter.GetAtPath(ArtRoot + "/Models/" + id + ".fbx") as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Reviewed model missing: " + id);
                if (importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static void NormalizeStairHeading(GameObject geometry)
        {
            // Measure the imported top tread instead of assuming that Blender +Y
            // became Unity +Z. The runtime landing is always on local positive Z.
            float sum = 0; int count = 0;
            foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>())
            {
                var vertices = filter.sharedMesh.vertices; var triangles = filter.sharedMesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i]]);
                    var b = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    var c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    var centre = (a + b + c) / 3;
                    if (centre.y < 3.17f || centre.y > 3.25f || Mathf.Abs(centre.x) > .74f) continue;
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.y) < .9f) continue;
                    sum += centre.z; count++;
                }
            }
            if (count == 0 || Mathf.Abs(sum / count) < .5f)
                throw new InvalidOperationException("Cannot verify imported stair orientation from the 3.2 m top tread.");
            if (sum < 0) geometry.transform.localRotation = Quaternion.Euler(0, 180, 0) * geometry.transform.localRotation;
            Debug.Log($"[Kompetenzhaus] Measured imported stair top from {count} triangles; normalized to positive Z.");
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            name = string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
            var path = ArtRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", name.Contains("Copper") ? .5f : 0);
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }

        [Serializable] private sealed class MaterialManifest
        {
            public string schema;
            public MaterialDefinition[] materials;
        }
        [Serializable] private sealed class MaterialDefinition
        {
            public string name, baseColorSrgb, emissionColorSrgb;
            public float roughness, metallic, opacity, emissionStrength, sheenWeight;
        }
        private static Material AuthoredMaterial(MaterialDefinition source)
        {
            if (string.IsNullOrWhiteSpace(source.name) || !ColorUtility.TryParseHtmlString(source.baseColorSrgb, out var color) ||
                !ColorUtility.TryParseHtmlString(source.emissionColorSrgb, out var emission) ||
                source.roughness < 0 || source.roughness > 1 || source.metallic < 0 || source.metallic > 1 ||
                source.opacity < 0 || source.opacity > 1 || source.emissionStrength < 0)
                throw new InvalidOperationException("Invalid authored material: " + source.name);
            color.a = source.opacity;
            var material = Material(source.name, color, 1 - source.roughness);
            material.SetFloat("_Metallic", source.metallic);
            material.SetColor("_EmissionColor", emission * source.emissionStrength);
            if (source.emissionStrength > 0) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            var transparent = source.opacity < .999f;
            material.SetFloat("_Surface", transparent ? 1 : 0);
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // URP Lit has no Blender cloth-sheen input. The authored base colour and
            // roughness are retained; no unsupported shader equivalence is claimed.
            EditorUtility.SetDirty(material); return material;
        }

        private static void PrepareAudio()
        {
            Directory.CreateDirectory(ArtRoot + "/Audio");
            foreach (var file in Directory.GetFiles(Path.Combine(SourceRoot, "art/audio"), "*.wav"))
                File.Copy(file, ArtRoot + "/Audio/" + Path.GetFileName(file), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in Directory.GetFiles(ArtRoot + "/Audio", "*.wav"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .7f;
                importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
            }
        }
        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(ArtRoot + "/Audio/" + name + ".wav");

        [MenuItem("Kompetenzhaus/Sync web shell")]
        public static void SyncWebTemplate()
        {
            var destination = Path.Combine(Application.dataPath, "WebGLTemplates/Kompetenzhaus");
            Directory.CreateDirectory(destination);
            foreach (var file in new[] { "index.html", "shell.css", "shell.js", "bridge-contract.mjs", "framework-view.mjs", "shell-locale.mjs" })
                File.Copy(Path.Combine(SourceRoot, "web-template", file), Path.Combine(destination, file), true);
            var templateData = Path.Combine(destination, "TemplateData");
            Directory.CreateDirectory(templateData);
            File.Copy(Path.Combine(SourceRoot, "art/references/two-houses-concept.png"), Path.Combine(templateData, "cover.png"), true);
            foreach (var relative in new[] { "content/kompetenzhaus-content.json", "content/module-learning-design.json", "companion/published.json", "companion/context-catalog.json" })
            {
                var target = Path.Combine(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(SourceRoot, relative), target, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PlayerSettings.WebGL.template = "PROJECT:Kompetenzhaus"; AssetDatabase.SaveAssets();
        }
    }
}
