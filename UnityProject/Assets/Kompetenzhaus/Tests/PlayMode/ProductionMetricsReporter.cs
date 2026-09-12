#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Kompetenzhaus.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Kompetenzhaus.RuntimeTests
{
    // Editor-only observation: no GameObject, material, mesh or save-state mutation.
    internal static class ProductionMetricsReporter
    {
        public static Sample Capture(string name, GameBootstrap app, GameWorld world, Camera camera, int budget)
        {
            var meshes = new Dictionary<Mesh, MeshMetric>();
            var textures = new Dictionary<Texture, TextureMetric>();
            var renderers = new List<RendererMetric>();
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            long activeTriangles = 0, frustumTriangles = 0, textUpperBound = 0, frustumTextUpperBound = 0;
            long activeLineTriangles = 0, frustumLineTriangles = 0;
            int otherUnknown = 0, textRendererCount = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != app.gameObject.scene || !renderer.gameObject.activeInHierarchy ||
                    !renderer.enabled || renderer.forceRenderingOff) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                var inFrustum = (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 && GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
                long triangles = 0;
                string coverage;
                if (mesh != null)
                {
                    if (!meshes.TryGetValue(mesh, out var metric))
                    {
                        long indexElements = 0;
                        for (var sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var count = (long)mesh.GetIndexCount(sub);
                            indexElements += count;
                            if (mesh.GetTopology(sub) == MeshTopology.Triangles) triangles += count / 3;
                        }
                        long vertices = 0;
                        for (var stream = 0; stream < mesh.vertexBufferCount; stream++) vertices += (long)mesh.vertexCount * mesh.GetVertexBufferStride(stream);
                        metric = new MeshMetric { id = meshes.Count + 1, name = mesh.name, assetPath = AssetDatabase.GetAssetPath(mesh),
                            lod0Triangles = triangles, vertexCount = mesh.vertexCount, vertexBufferPayloadBytes = vertices,
                            referencedLod0IndexElementBytes = indexElements * (mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4),
                            cpuReadable = mesh.isReadable, profilerNativeBytes = NativeBytes(mesh) };
                        meshes.Add(mesh, metric);
                    }
                    triangles = metric.lod0Triangles;
                    metric.instances++;
                    if (inFrustum) metric.frustumCandidates++;
                    activeTriangles += triangles;
                    if (inFrustum) frustumTriangles += triangles;
                    coverage = "measured-mesh-lod0";
                }
                else if (renderer is LineRenderer line)
                {
                    var baked = new Mesh();
                    try
                    {
                        if (line.positionCount >= 2) line.BakeMesh(baked, camera, false);
                        for (var sub = 0; sub < baked.subMeshCount; sub++)
                            if (baked.GetTopology(sub) == MeshTopology.Triangles) triangles += (long)baked.GetIndexCount(sub) / 3;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(baked); }
                    activeLineTriangles += triangles;
                    if (inFrustum) frustumLineTriangles += triangles;
                    coverage = "measured-line-bake-for-camera";
                }
                else if (renderer.TryGetComponent<TextMesh>(out var text))
                {
                    // TextMesh exposes no public MeshFilter. Count a conservative
                    // two triangles per UTF-16 character (including spaces/markup),
                    // and keep this bound distinct from measured mesh triangles.
                    var bound = (text.text?.Length ?? 0) * 2L;
                    textUpperBound += bound; if (inFrustum) frustumTextUpperBound += bound;
                    textRendererCount++; coverage = "textmesh-character-upper-bound";
                }
                else { otherUnknown++; coverage = "unmeasured-renderer"; }
                renderers.Add(new RendererMetric { name = renderer.name, rendererType = renderer.GetType().Name,
                    coverage = coverage, meshTriangles = triangles, frustumCandidate = inFrustum });
                foreach (var material in renderer.sharedMaterials.Where(material => material != null))
                foreach (var property in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(property);
                    if (texture == null || textures.ContainsKey(texture)) continue;
                    var path = AssetDatabase.GetAssetPath(texture);
                    textures.Add(texture, new TextureMetric { id = textures.Count + 1, name = texture.name,
                        assetPath = path, type = texture.GetType().Name, width = texture.width, height = texture.height,
                        category = texture is RenderTexture ? "render-target" : string.IsNullOrEmpty(path) ? "runtime-or-builtin" : "asset",
                        profilerNativeBytes = NativeBytes(texture) });
                }
            }
            return new Sample { name = name, frame = Time.frameCount, architecture = JsonUtility.ToJson(app.Architecture.State),
                architectureSha256 = Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(app.Architecture.State))),
                placedModules = app.Progression.Data.placedModuleIds.ToArray(),
                bscBays = app.Architecture.State.houses.Single(h => h.houseId == "bsc").footprintTiles.Count,
                mscBays = app.Architecture.State.houses.Single(h => h.houseId == "msc").footprintTiles.Count,
                camera = camera.name, cameraMode = app.cameraController.Mode.ToString(), showRoofs = world.showRoofs, visibleFloor = world.visibleFloor,
                screenWidth = Screen.width, screenHeight = Screen.height, cameraPixelWidth = camera.pixelWidth, cameraPixelHeight = camera.pixelHeight,
                activeRendererInstances = renderers.Count, activeMeshLod0Triangles = activeTriangles,
                frustumCandidateMeshLod0Triangles = frustumTriangles, textRendererCount = textRendererCount,
                activeLineTriangles = activeLineTriangles, frustumCandidateLineTriangles = frustumLineTriangles,
                textMeshTriangleUpperBound = textUpperBound, frustumTextMeshTriangleUpperBound = frustumTextUpperBound,
                unmeasuredOtherRenderers = otherUnknown, geometryInventoryComplete = otherUnknown == 0,
                geometryTriangleUpperBound = otherUnknown == 0 ? activeTriangles + activeLineTriangles + textUpperBound : -1,
                triangleBudget = budget, measuredMeshBudgetExceeded = activeTriangles > budget,
                conservativeBudgetExceeded = otherUnknown == 0 && activeTriangles + activeLineTriangles + textUpperBound > budget,
                uniqueMeshVertexBufferPayloadBytes = meshes.Values.Sum(m => m.vertexBufferPayloadBytes),
                uniqueMeshReferencedLod0IndexElementBytes = meshes.Values.Sum(m => m.referencedLod0IndexElementBytes),
                meshProfilerNativeBytes = meshes.Values.Any(m => m.profilerNativeBytes < 0) ? -1 : meshes.Values.Sum(m => m.profilerNativeBytes),
                textureProfilerNativeBytes = textures.Values.Any(t => t.profilerNativeBytes < 0) ? -1 : textures.Values.Sum(t => t.profilerNativeBytes),
                meshes = meshes.Values.ToArray(), textures = textures.Values.ToArray(), renderers = renderers.ToArray() };
        }

        public static void Write(string sourceRoot, List<Sample> samples)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var modelFiles = Files(projectRoot, "Assets/Kompetenzhaus/Art/Models", new[] { ".fbx", ".glb" });
            var textureFiles = Files(projectRoot, "Assets/Kompetenzhaus/Art", new[] { ".png", ".jpg", ".jpeg", ".tga", ".exr", ".hdr", ".tif", ".tiff", ".psd", ".ktx2" });
            var result = new Report { recordedAt = DateTime.UtcNow.ToString("o"), editorVersion = Application.unityVersion,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), sceneSha256 = Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, "Kompetenzhaus/Scenes/Kompetenzhaus.unity"))),
                importedModelArtifactBytes = modelFiles.Sum(file => file.bytes), ownTextureArtifactBytes = textureFiles.Sum(file => file.bytes),
                modelFiles = modelFiles, ownTextureFiles = textureFiles, samples = samples.ToArray(),
                modelAuditSha256 = Hash(File.ReadAllBytes(Path.Combine(sourceRoot, "art/models/asset-audit.json"))) };
            var json = JsonUtility.ToJson(result, true);
            // JsonUtility has no nullable long fields. Unknown native-memory values
            // are serialized as JSON null, never as a fabricated zero-byte result.
            json = Regex.Replace(json, "\"(profilerNativeBytes|meshProfilerNativeBytes|textureProfilerNativeBytes|geometryTriangleUpperBound)\"\\s*:\\s*-1\\b", "\"$1\": null");
            var directory = Path.Combine(projectRoot, "Logs", "qa-runtime"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "production-metrics.json"), json);
        }

        private static long NativeBytes(UnityEngine.Object obj)
        {
            try { var value = Profiler.GetRuntimeMemorySizeLong(obj); return value > 0 ? value : -1; }
            catch { return -1; }
        }
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private static Artifact[] Files(string root, string relative, string[] extensions)
        {
            var directory = Path.Combine(root, relative);
            return Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant())).OrderBy(file => file)
                .Select(file => new Artifact { path = Path.GetRelativePath(root, file).Replace('\\', '/'), bytes = new FileInfo(file).Length, sha256 = Hash(File.ReadAllBytes(file)) }).ToArray() : Array.Empty<Artifact>();
        }
        [Serializable] internal sealed class MeshMetric { public int id, vertexCount, instances, frustumCandidates; public string name, assetPath; public bool cpuReadable; public long lod0Triangles, vertexBufferPayloadBytes, referencedLod0IndexElementBytes, profilerNativeBytes; }
        [Serializable] internal sealed class TextureMetric { public int id, width, height; public string name, assetPath, type, category; public long profilerNativeBytes; }
        [Serializable] internal sealed class RendererMetric { public string name, rendererType, coverage; public long meshTriangles; public bool frustumCandidate; }
        [Serializable] internal sealed class Artifact { public string path, sha256; public long bytes; }
        [Serializable] internal sealed class Sample
        {
            public string name, architecture, architectureSha256, camera, cameraMode;
            public string[] placedModules;
            public int frame, bscBays, mscBays, visibleFloor, screenWidth, screenHeight, cameraPixelWidth, cameraPixelHeight, activeRendererInstances, textRendererCount, unmeasuredOtherRenderers, triangleBudget;
            public bool showRoofs, geometryInventoryComplete, measuredMeshBudgetExceeded, conservativeBudgetExceeded;
            public long activeMeshLod0Triangles, frustumCandidateMeshLod0Triangles, textMeshTriangleUpperBound, frustumTextMeshTriangleUpperBound,
                activeLineTriangles, frustumCandidateLineTriangles,
                geometryTriangleUpperBound, uniqueMeshVertexBufferPayloadBytes, uniqueMeshReferencedLod0IndexElementBytes, meshProfilerNativeBytes, textureProfilerNativeBytes;
            public MeshMetric[] meshes; public TextureMetric[] textures; public RendererMetric[] renderers;
        }
        [Serializable] private sealed class Report
        {
            public string schema = "kompetenzhaus.production-metrics.v1", status = "measured-editor-scene-not-browser-performance-approval";
            public string recordedAt, editorVersion, graphicsApi, sceneSha256, modelAuditSha256;
            public string triangleMethod = "Every active enabled renderer in the loaded production scene, LOD0 index counts per mesh instance. LineRenderer geometry is measured via temporary BakeMesh for the recorded camera; the observer mesh is excluded from memory inventory. Frustum counts are candidate geometry, not GPU draws or occlusion results. TextMesh uses a separately labelled character upper bound.";
            public string memoryMethod = "Mesh buffer payload and referenced LOD0 index elements are not total RAM. Profiler values are actual Editor native-memory observations; unavailable values are null. Textures cover material references only, not every URP render target or browser resource.";
            public string artifactMethod = "Exact imported source-model file bytes and own image files under Unity Art, separate from loaded memory and WebGL transfer bytes.";
            public string persistence = "-qaNoPersist; synthetic valid game designs only; no saved-game read/write.";
            public long importedModelArtifactBytes, ownTextureArtifactBytes;
            public Artifact[] modelFiles, ownTextureFiles; public Sample[] samples;
        }
    }
}
#endif
