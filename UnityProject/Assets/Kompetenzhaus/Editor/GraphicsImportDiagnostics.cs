using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kompetenzhaus.Editor
{
    // A bounded diagnostic for URP's three probe-baking unified shader assets.
    // Never changes package versions/source, scenes, game assets or graphics settings.
    public static class GraphicsImportDiagnostics
    {
        private static readonly string[] Paths =
        {
            "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/VirtualOffset/TraceVirtualOffset.urtshader",
            "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/DynamicGI/DynamicGISkyOcclusion.urtshader",
            "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/RenderingLayerMask/TraceRenderingLayerMask.urtshader"
        };

        public static void Inspect() => Execute(false);
        public static void RepairMissingShaderImports() => Execute(true);

        private static void Execute(bool repair)
        {
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                before = Paths.Select(InspectPath).ToArray()
            };
            var repaired = new List<string>();
            if (repair)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Use a graphics device for shader import verification; omit -nographics.");
                foreach (var item in report.before.Where(item => !item.hasComputeShader || !item.hasRayTracingShader))
                {
                    AssetDatabase.ImportAsset(item.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    repaired.Add(item.path);
                }
            }
            report.reimportedPaths = repaired.ToArray();
            report.after = Paths.Select(InspectPath).ToArray();
            report.allExpectedTypesPresent = report.after.All(item => item.hasComputeShader && item.hasRayTracingShader);
            var directory = Path.Combine(ProjectTools.ProjectRoot, "Logs", "qa-snapshots");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, repair ? "graphics-import-repair.json" : "graphics-import-inspect.json"), JsonUtility.ToJson(report, true));
            Debug.Log($"[Kompetenzhaus] Shader import inspection: device={report.graphicsDevice}, expectedTypes={report.allExpectedTypesPresent}, reimported={repaired.Count}.");
            if (repair && !report.allExpectedTypesPresent)
                throw new InvalidOperationException("Targeted shader import did not produce both expected asset types. Inspect the diagnostic report before further action.");
        }

        private static AssetReport InspectPath(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            return new AssetReport
            {
                path = path,
                importer = AssetImporter.GetAtPath(path)?.GetType().FullName ?? "missing",
                mainType = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName ?? "missing",
                importedTypes = assets.Where(asset => asset != null).Select(asset => asset.GetType().FullName).ToArray(),
                hasComputeShader = assets.Any(asset => asset is ComputeShader),
                hasRayTracingShader = assets.Any(asset => asset is RayTracingShader)
            };
        }

        [Serializable] private sealed class Report
        {
            public string unityVersion, graphicsDevice;
            public AssetReport[] before, after;
            public string[] reimportedPaths;
            public bool allExpectedTypesPresent;
        }
        [Serializable] private sealed class AssetReport
        {
            public string path, importer, mainType;
            public string[] importedTypes;
            public bool hasComputeShader, hasRayTracingShader;
        }
    }
}
