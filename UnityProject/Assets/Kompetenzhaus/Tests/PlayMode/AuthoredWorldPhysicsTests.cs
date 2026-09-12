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
    // These tests load the actual assembled production scene and use its imported
    // meshes, generated colliders and original CharacterController. They never
    // substitute boxes for doors/stairs and never read or write a saved game.
    public sealed class AuthoredWorldPhysicsTests
    {
        private const string ScenePath = "Assets/Kompetenzhaus/Scenes/Kompetenzhaus.unity";
        private GameBootstrap app;
        private GameWorld world;
        private CharacterController player;
        private readonly List<MotionFrame> frames = new();
        private float verticalSpeed;
        private int sideCollisions;

        [UnitySetUp]
        public IEnumerator LoadRealScene()
        {
            Assert.That(Array.IndexOf(Environment.GetCommandLineArgs(), "-qaNoPersist"), Is.GreaterThanOrEqualTo(0),
                "Runtime QA requires -qaNoPersist before loading the real scene.");
            frames.Clear();
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();
            app = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            world = UnityEngine.Object.FindAnyObjectByType<GameWorld>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.StartupError, Is.Null.Or.Empty);
            Assert.That(world, Is.Not.Null, "WorldSetup.Assemble must have saved the actual authored world.");
            Assert.That(app.cameraController.worldReady, Is.True);
            player = app.cameraController.player;
            Assert.That(player.height, Is.EqualTo(1.75f).Within(.001f));
            Assert.That(player.radius, Is.EqualTo(.3f).Within(.001f));
            Assert.That(player.stepOffset, Is.EqualTo(.28f).Within(.001f));
            app.SetUiFocus(true); // QA drives Move directly; physical input and guide animation stay quiet.
            Assert.That(app.cameraController.SetMode(ViewMode.FirstPerson), Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator StoreMeasuredTrace()
        {
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "qa-runtime"));
            Directory.CreateDirectory(folder);
            var name = TestContext.CurrentContext.Test.MethodName;
            File.WriteAllText(Path.Combine(folder, name + ".json"), JsonUtility.ToJson(new MotionTrace
            {
                test = name, scene = ScenePath, playerHeight = player != null ? player.height : 0,
                playerRadius = player != null ? player.radius : 0, frames = frames.ToArray(),
                method = "Actual scene CharacterController.Move against runtime colliders; fixed-step trajectory, no substitute geometry."
            }, true));
            if (app != null) app.cameraController.SetMode(ViewMode.BirdView);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ImportedKitAndCameraModesRemainValidInRealScene()
        {
            Assert.That(world.assets.entries.Length, Is.EqualTo(22));
            foreach (var entry in world.assets.entries)
            {
                Assert.That(entry.model, Is.Not.Null, entry.id);
                var filters = entry.model.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThan(0), entry.id);
                foreach (var filter in filters)
                {
                    Assert.That(filter.sharedMesh, Is.Not.Null, entry.id);
                    Assert.That(filter.sharedMesh.isReadable, Is.True,
                        entry.id + ": WebGL runtime MeshCollider cooking requires readable imported geometry.");
                }
                foreach (var renderer in entry.model.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, entry.id);
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), entry.id);
                }
            }
            var guideNames = world.assets.Get("student-guide").GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.name).OrderBy(name => name).ToArray();
            var expected = new[] { "GuideBody", "LeftUpperArm", "RightUpperArm", "LeftForearm", "RightForearm",
                "LeftHand", "RightHand", "LeftLeg", "RightLeg", "LeftFoot", "RightFoot" }.OrderBy(name => name).ToArray();
            Assert.That(guideNames, Is.EqualTo(expected));
            Assert.That(app.cameraController.firstPersonCamera.gameObject.activeInHierarchy, Is.True);
            Assert.That(app.cameraController.birdCamera.gameObject.activeInHierarchy, Is.False);
            Assert.That(player.enabled, Is.True);
            app.cameraController.SetMode(ViewMode.BirdView);
            yield return null;
            Assert.That(player.enabled, Is.False);
            Assert.That(app.cameraController.birdCamera.gameObject.activeInHierarchy, Is.True);
            app.SelectHouse("msc");
            yield return null;
            Assert.That(app.Progression.Data.selectedHouseId, Is.EqualTo("msc"));
            Assert.That(app.cameraController.Mode, Is.EqualTo(ViewMode.BirdView));
            Assert.That(app.cameraController.SetMode(ViewMode.FirstPerson), Is.True);
            yield return null;
            Assert.That(player.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RealExteriorAndInteriorDoorsPassThePlayerWhileSolidWallStopsIt()
        {
            yield return BuildTestHouse("bsc", 0);
            var origin = GameWorld.HouseOrigin("bsc");
            var boundaries = world.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Boundary ")).ToArray();
            Assert.That(boundaries.Length, Is.GreaterThan(0));
            Assert.That(boundaries.All(t => t.GetComponentsInChildren<MeshCollider>().Length > 0), Is.True,
                "Door/wall boundaries must use the actual imported meshes.");
            Assert.That(boundaries.All(t => t.GetComponentsInChildren<BoxCollider>().Length == 0), Is.True,
                "A box collider would close a genuine portal opening.");
            Teleport(origin + new Vector3(2, .2f, -1.3f));
            yield return Settle("outside-door");
            yield return Walk(Vector3.forward, 3.3f, "exterior-door");
            Assert.That(player.transform.position.z, Is.GreaterThan(origin.z + 1.7f), "Cannot enter the real exterior doorway.");
            yield return Walk(Vector3.right, 4f, "interior-door");
            Assert.That(player.transform.position.x, Is.GreaterThan(origin.x + 5.6f), "Cannot pass the real interior doorway.");
            sideCollisions = 0;
            yield return Walk(Vector3.forward, 3.4f, "solid-wall");
            Assert.That(sideCollisions, Is.GreaterThan(0), "The solid wall should stop the original capsule.");
            Assert.That(player.transform.position.z, Is.LessThan(origin.z + 4f), "The capsule passed through a solid exterior wall.");
            Assert.That(player.transform.position.y, Is.InRange(origin.y - .08f, origin.y + .15f));
        }

        [UnityTest]
        public IEnumerator RealStairsClimbAndDescendInAllFourOrientations()
        {
            for (var quarter = 0; quarter < 4; quarter++)
            {
                var houseId = quarter % 2 == 0 ? "bsc" : "msc";
                yield return BuildTestHouse(houseId, quarter);
                var origin = GameWorld.HouseOrigin(houseId);
                var centre = origin + GameWorld.TileCentre(new GridTile(0, 1));
                var forward = Quaternion.Euler(0, quarter * 90, 0) * Vector3.forward;
                var stairs = world.GetComponentsInChildren<Transform>().Single(t => t.name == "staircase");
                Assert.That(stairs.GetComponentsInChildren<MeshCollider>().Length, Is.GreaterThan(0));
                // Below the upper ceiling, the ray must see the staircase itself,
                // not a full floor tile sealing the stairwell at 3.2 metres.
                var holeProbe = centre + Vector3.up * 3.35f;
                Assert.That(Physics.Raycast(holeProbe, Vector3.down, out var holeHit, 4f), Is.True);
                Assert.That(holeHit.point.y, Is.LessThan(origin.y + 3f), "Upper storey floor seals the actual stairwell.");
                var landingProbe = centre + forward * 1.65f + Vector3.up * 3.5f;
                Assert.That(Physics.Raycast(landingProbe, Vector3.down, out var landingHit, 1f), Is.True);
                Assert.That(landingHit.point.y, Is.EqualTo(origin.y + 3.2f).Within(.03f));
                Teleport(centre - forward * 2.8f + Vector3.up * .2f);
                yield return Settle("stair-start-" + quarter);
                yield return Walk(forward, 6f, "stair-up-" + quarter);
                Assert.That(Vector3.Dot(player.transform.position - centre, forward), Is.GreaterThan(2.7f),
                    "The controller could not reach the upper room in orientation " + quarter);
                Assert.That(player.transform.position.y, Is.InRange(origin.y + 3.15f, origin.y + 3.4f),
                    "The real capsule did not reach the 3.2 m upper floor in orientation " + quarter);
                yield return Walk(-forward, 6f, "stair-down-" + quarter);
                Assert.That(Vector3.Dot(player.transform.position - centre, forward), Is.LessThan(-2.4f));
                Assert.That(player.transform.position.y, Is.InRange(origin.y - .08f, origin.y + .15f),
                    "The real capsule did not return to the lower floor in orientation " + quarter);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRebuildUndoAndRecoveryDoNotLeaveInvisibleColliders()
        {
            yield return BuildTestHouse("bsc", 0);
            var before = world.transform.Find("Player architecture");
            var oldWall = before.GetComponentsInChildren<MeshCollider>().First();
            Assert.That(app.Architecture.TryCommit(state =>
            {
                var house = state.houses.Single(h => h.houseId == "bsc");
                house.connections.Clear(); house.rooms.Clear(); house.footprintTiles.Clear(); house.floors = 1;
            }, out var error), Is.True, error);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(oldWall == null, Is.True, "Removed architecture still owns a live collider.");
            Assert.That(world.transform.Find("Player architecture").GetComponentsInChildren<MeshCollider>().Length, Is.Zero);
            Assert.That(app.Architecture.Undo(out error), Is.True, error);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(world.transform.Find("Player architecture").GetComponentsInChildren<MeshCollider>().Length, Is.GreaterThan(0));
            var restored = JsonUtility.ToJson(app.Architecture.State);
            Assert.That(app.Architecture.TryCommit(state => state.houses[0].footprintTiles.Add(new GridTile(99, 99)), out error), Is.False);
            Assert.That(JsonUtility.ToJson(app.Architecture.State), Is.EqualTo(restored));
            Teleport(new Vector3(0, -4, 0));
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(player.transform.position, app.cameraController.entryPoint), Is.LessThan(.2f),
                "Falling outside the world did not restore the existing player to the entrance.");
        }

        private IEnumerator BuildTestHouse(string houseId, int quarter)
        {
            Assert.That(app.Architecture.TryCommit(state =>
            {
                state.houses = ArchitectureState.CreateDefault().houses;
                var house = state.houses.Single(h => h.houseId == houseId);
                house.floors = 2; house.facadeStyle = "solid";
                GridTile T(int x, int z, int floor = 0)
                {
                    var dx = x; var dz = z - 1;
                    for (var i = 0; i < quarter; i++) { var oldX = dx; dx = dz; dz = -oldX; }
                    return new GridTile(dx, dz + 1, floor);
                }
                house.footprintTiles.AddRange(new[] { T(0, 0), T(0, 1), T(0, 2), T(1, 0), T(0, 1, 1), T(0, 2, 1) });
                house.rooms.Add(new RoomDesign { id = "qa-lower", name = "QA lower room", tiles = new List<GridTile> { T(0, 0), T(0, 1) } });
                house.rooms.Add(new RoomDesign { id = "qa-peer", name = "QA peer room", tiles = new List<GridTile> { T(1, 0) } });
                house.rooms.Add(new RoomDesign { id = "qa-upper", name = "QA upper room", tiles = new List<GridTile> { T(0, 1, 1), T(0, 2, 1) } });
                house.connections.Add(new RoomConnection { id = "qa-door", kind = "door", fromRoomId = "qa-lower", toRoomId = "qa-peer", fromTile = T(0, 0), toTile = T(1, 0) });
                house.connections.Add(new RoomConnection { id = "qa-stair", kind = "stair", fromRoomId = "qa-lower", toRoomId = "qa-upper", fromTile = T(0, 1), toTile = T(0, 1, 1) });
            }, out var error), Is.True, error);
            yield return null;
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
        }

        private void Teleport(Vector3 position)
        {
            player.enabled = false; player.transform.position = position; player.enabled = true;
            verticalSpeed = 0; Physics.SyncTransforms();
        }

        private IEnumerator Walk(Vector3 direction, float distance, string stage)
        {
            var remaining = distance;
            while (remaining > .0001f)
            {
                var delta = Mathf.Min(remaining, app.cameraController.walkingSpeed * Time.fixedDeltaTime);
                Move(direction * delta, stage);
                remaining -= delta;
                yield return new WaitForFixedUpdate();
            }
            yield return Settle(stage + "-settled");
        }

        private IEnumerator Settle(string stage)
        {
            for (var i = 0; i < 15; i++) { Move(Vector3.zero, stage); yield return new WaitForFixedUpdate(); }
        }

        private void Move(Vector3 horizontal, string stage)
        {
            if (player.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.fixedDeltaTime;
            var flags = player.Move(horizontal + Vector3.up * (verticalSpeed * Time.fixedDeltaTime));
            if ((flags & CollisionFlags.Sides) != 0) sideCollisions++;
            frames.Add(new MotionFrame { stage = stage, position = player.transform.position, collisionFlags = (int)flags, grounded = player.isGrounded });
        }

        [Serializable] private sealed class MotionFrame { public string stage; public Vector3 position; public int collisionFlags; public bool grounded; }
        [Serializable] private sealed class MotionTrace { public string test, scene, method; public float playerHeight, playerRadius; public MotionFrame[] frames; }
    }
}
