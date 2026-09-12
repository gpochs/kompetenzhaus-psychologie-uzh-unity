using Kompetenzhaus.Content;
using Kompetenzhaus.Presentation;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Tests
{
    public sealed class CameraFramingTests
    {
        [TestCase(ViewMode.FirstPerson, "msc")]
        [TestCase(ViewMode.FirstPerson, "bsc")]
        [TestCase(ViewMode.FirstPerson, "all")]
        [TestCase(ViewMode.BirdView, "msc")]
        public void HouseSelectionShowsBirdViewBeforeFocusWithoutMovingPlayer(ViewMode initialMode, string houseId)
        {
            var root = new GameObject("Temporary house selection test");
            // Keep Awake from loading resources or the user's saved progress.
            root.SetActive(false);
            try
            {
                var catalog = new ContentCatalog(new ContentDocument
                {
                    schemaVersion = 1,
                    modules = new[] { new ModuleDefinition { id = "foundation" } }
                });
                var saves = 0;
                var progression = new ProgressionService(catalog, new ProgressData(), () => saves++);
                var game = root.AddComponent<GameBootstrap>();
                typeof(GameBootstrap).GetProperty(nameof(GameBootstrap.Progression)).SetValue(game, progression);
                var controls = root.AddComponent<CameraModeController>();
                game.cameraController = controls;
                var birdObject = new GameObject("Bird camera"); birdObject.transform.SetParent(root.transform, false);
                var firstPersonObject = new GameObject("First-person camera"); firstPersonObject.transform.SetParent(root.transform, false);
                var playerObject = new GameObject("Player"); playerObject.transform.SetParent(root.transform, false);
                controls.birdCamera = birdObject.AddComponent<Camera>();
                controls.firstPersonCamera = firstPersonObject.AddComponent<Camera>();
                controls.player = playerObject.AddComponent<CharacterController>();
                controls.worldReady = true;
                controls.Initialize(progression);
                controls.SetMode(initialMode);
                var playerPosition = new Vector3(-25f, 3.4f, 7f);
                playerObject.transform.position = playerPosition;
                saves = 0;
                var changes = 0;
                progression.Changed += () =>
                {
                    changes++;
                    Assert.That(controls.Mode, Is.EqualTo(ViewMode.BirdView));
                    Assert.That(progression.Data.viewMode, Is.EqualTo(ViewMode.BirdView));
                    Assert.That(progression.Data.selectedHouseId, Is.EqualTo(houseId));
                };
                var focusRequests = 0;
                game.HouseFocusRequested += selected =>
                {
                    focusRequests++;
                    Assert.That(selected, Is.EqualTo(houseId));
                    Assert.That(changes, Is.EqualTo(1));
                    Assert.That(birdObject.activeSelf, Is.True);
                    Assert.That(firstPersonObject.activeSelf, Is.False);
                    Assert.That(controls.player.enabled, Is.False);
                    Assert.That(playerObject.transform.position, Is.EqualTo(playerPosition));
                };

                game.SelectHouse(houseId);

                Assert.That(saves, Is.EqualTo(1));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(focusRequests, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0.6f)]
        [TestCase(1.6f)]
        public void CampusBoundsFitInsideWideAndNarrowViewports(float aspect)
        {
            var root = new GameObject("Temporary framing test");
            try
            {
                var controls = root.AddComponent<CameraModeController>();
                var cameraObject = new GameObject("Temporary bird camera");
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.aspect = aspect;
                controls.birdCamera = camera;
                var bounds = new Bounds(new Vector3(0f, 2f, 1f), new Vector3(80f, 14f, 40f));
                controls.FitBirdView(bounds);
                for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    var viewport = camera.WorldToViewportPoint(corner);
                    Assert.That(viewport.x, Is.InRange(0.01f, 0.99f));
                    Assert.That(viewport.y, Is.InRange(0.01f, 0.99f));
                    Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
                    Assert.That(viewport.z, Is.LessThan(camera.farClipPlane));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
