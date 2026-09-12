#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kompetenzhaus.State;
using Kompetenzhaus.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kompetenzhaus.RuntimeTests
{
    public sealed class ProductionSceneMetricsTests
    {
        [UnityTest]
        public IEnumerator CaptureThreeActualProductionStates()
        {
            var args = Environment.GetCommandLineArgs();
            Assert.That(Array.IndexOf(args, "-qaNoPersist"), Is.GreaterThanOrEqualTo(0));
            var sourceIndex = Array.IndexOf(args, "-sourceRoot");
            Assert.That(sourceIndex, Is.GreaterThanOrEqualTo(0));
            var sourceRoot = Path.GetFullPath(args[sourceIndex + 1]);
            var brief = JsonUtility.FromJson<Brief>(File.ReadAllText(Path.Combine(sourceRoot, "art/review/room-brief.json")));
            var samples = new List<ProductionMetricsReporter.Sample>();
            foreach (var scenario in new[] { "empty-start", "inhabited-house-pair", "64-bays-per-house" })
            {
                yield return SceneManager.LoadSceneAsync("Assets/Kompetenzhaus/Scenes/Kompetenzhaus.unity", LoadSceneMode.Single);
                yield return null;
                var app = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
                var world = UnityEngine.Object.FindAnyObjectByType<GameWorld>();
                Assert.That(app.StartupError, Is.Null.Or.Empty); Assert.That(world, Is.Not.Null);
                app.SetUiFocus(true); app.Progression.Data.learningMode = LearningMode.Free;
                if (scenario != "empty-start")
                {
                    var dense = scenario == "64-bays-per-house";
                    Assert.That(app.Architecture.TryCommit(state =>
                    {
                        foreach (var house in state.houses)
                        {
                            house.floors = dense ? 4 : 1;
                            for (var floor = 0; floor < house.floors; floor++)
                            {
                                var room = new RoomDesign { id = house.houseId + "-room-" + floor, name = "Lernraum / Learning room " + (floor + 1) };
                                for (var x = -2; x <= 1; x++) for (var z = -2; z <= (dense ? 1 : 0); z++)
                                {
                                    house.footprintTiles.Add(new GridTile(x, z, floor));
                                    room.tiles.Add(new GridTile(x, z, floor));
                                }
                                house.rooms.Add(room);
                            }
                            if (dense) for (var floor = 0; floor < 3; floor++)
                                house.connections.Add(new RoomConnection { id = house.houseId + "-stairs-" + floor, kind = "stair",
                                    fromRoomId = house.houseId + "-room-" + floor, toRoomId = house.houseId + "-room-" + (floor + 1),
                                    fromTile = new GridTile(floor - 1, -1, floor), toTile = new GridTile(floor - 1, -1, floor + 1) });
                        }
                    }, out var error), Is.True, error);
                    foreach (var houseId in new[] { "bsc", "msc" })
                    foreach (var module in app.Catalog.Document.modules.Where(module => module.houseId == houseId).Take(dense ? int.MaxValue : 6))
                        Assert.That(app.Architecture.TryBuildModuleInFreeBay(module.id, out error), Is.True, module.id + ": " + error);
                }
                app.cameraController.SetMode(ViewMode.BirdView); world.SetArchitectureView(3, true); world.FocusHouse("all");
                yield return null;
                yield return null;
                var sample = ProductionMetricsReporter.Capture(scenario, app, world, app.cameraController.birdCamera, brief.performanceBudget.maxSceneTriangles);
                Assert.That(sample.activeMeshLod0Triangles, Is.GreaterThan(0));
                Assert.That(sample.meshes.Where(mesh => mesh.assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).All(mesh => mesh.cpuReadable), Is.True,
                    "Imported runtime collider source meshes must remain readable in player builds; built-in box primitives use BoxCollider instead.");
                samples.Add(sample);
                ProductionMetricsReporter.Write(sourceRoot, samples);
            }
            Assert.That(samples.Count, Is.EqualTo(3));
            LogAssert.NoUnexpectedReceived();
        }
        [Serializable] private sealed class Brief { public Budget performanceBudget; }
        [Serializable] private sealed class Budget { public int maxSceneTriangles; }
    }
}
#endif
