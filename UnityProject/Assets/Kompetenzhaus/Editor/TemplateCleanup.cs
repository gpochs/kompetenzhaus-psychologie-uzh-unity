using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Kompetenzhaus.Editor
{
    public static class TemplateCleanup
    {
        private static AddAndRemoveRequest request;
        private static double deadline;

        // Asynchronous UPM operation: launch the editor directly WITHOUT -quit.
        public static void RemoveUnusedVersionControl()
        {
            request = Client.AddAndRemove(packagesToRemove: new[] { "com.unity.collab-proxy" });
            deadline = EditorApplication.timeSinceStartup + 300d;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                Debug.LogError("[Kompetenzhaus] UPM cleanup timed out.");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success)
            {
                Debug.LogError("[Kompetenzhaus] UPM cleanup failed: " + request.Error?.message);
                EditorApplication.Exit(1);
                return;
            }
            if (request.Result.Any(package => package.name == "com.unity.collab-proxy"))
            {
                Debug.LogError("[Kompetenzhaus] Version-control package is still present.");
                EditorApplication.Exit(1);
                return;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kompetenzhaus] UNUSED_VERSION_CONTROL_REMOVED");
            EditorApplication.Exit(0);
        }
    }
}
