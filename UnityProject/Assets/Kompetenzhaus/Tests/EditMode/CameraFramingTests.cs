using Kompetenzhaus.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Tests
{
    public sealed class CameraFramingTests
    {
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
