using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Renders the two hub scenes to PNGs so somebody can look at them.
    ///
    /// A room that compiles, saves and passes its tests can still be a room with a wall through the
    /// middle of it, and there is no assertion that catches that. Writing the frames out and opening
    /// them has caught more faults in this project than any other check.
    ///
    /// The folder ends in a tilde so Unity does not import the output as project assets.
    /// </summary>
    public static class HubSnapshot
    {
        private const string OutputFolder = "HubProof~";

        [MenuItem("Scaling Laws/Snapshot the hubs")]
        public static void Snapshot()
        {
            Directory.CreateDirectory(OutputFolder);

            Shoot("Assets/_ScalingLaws/Scenes/SmallHub.unity", "small_hub.png");
            Shoot("Assets/_ScalingLaws/Scenes/BigHub.unity", "big_hub.png");
        }

        private static void Shoot(string scenePath, string fileName)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"No scene at {scenePath}.");
                return;
            }

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning($"{scenePath} has no camera.");
                return;
            }

            var target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var previous = camera.targetTexture;
            var wasActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            shot.Apply();

            RenderTexture.active = wasActive;
            camera.targetTexture = previous;

            var path = Path.Combine(OutputFolder, fileName);
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);

            Debug.Log($"[Scaling Laws] Wrote {path}.");
        }
    }
}
