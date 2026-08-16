using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Photographs the city so somebody can look at it.
    ///
    /// A terrain that compiles and saves can still be a terrain with a district on a cliff, and
    /// there is no assertion that catches that. Two frames: the whole map from the south west at
    /// the angle the reference uses, and a top-down orthographic plate that reads like a plan.
    ///
    /// The folder ends in a tilde so Unity does not import the output as project assets.
    /// </summary>
    public static class CitySnapshot
    {
        private const string OutputFolder = "CityProof~";
        private const string ScenePath = "Assets/_ScalingLaws/Scenes/City.unity";

        [MenuItem("Scaling Laws/Snapshot the city")]
        public static void Snapshot()
        {
            Directory.CreateDirectory(OutputFolder);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"No scene at {ScenePath}.");
                return;
            }

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("The city scene has no camera.");
                return;
            }

            Shoot(camera, "city_view.png");

            // Straight down, orthographic, framed on the whole terrain. This is the frame that
            // shows whether the districts are where the catalog says they are.
            camera.orthographic = true;
            camera.orthographicSize = CityLayout.Size / 2f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.transform.position = new Vector3(
                CityLayout.Size / 2f,
                CityLayout.Height * 3f,
                CityLayout.Size / 2f);

            Shoot(camera, "city_plan.png");
        }

        private static void Shoot(Camera camera, string fileName)
        {
            var target = new RenderTexture(1600, 1600, 24) { antiAliasing = 4 };
            var wasActive = RenderTexture.active;
            var previous = camera.targetTexture;

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
