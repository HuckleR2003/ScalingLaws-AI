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
    ///
    /// **A second `Camera.Render()` in one batchmode process is unreliable on at least one real
    /// machine, and it fails silently.** Measured 2026-09-15: the first shot always lands. The
    /// second — orthographic, after the first was perspective — sometimes ends the process right
    /// after the first shot's own log line, no exception, no crash report, exit code present but
    /// meaningless. Reopening the scene between the two shots did not fix it either; only running
    /// each shot as its own `-executeMethod` invocation did, every time it was tried. Nobody has
    /// proven which piece of driver or render-target state a second `Render()` is inheriting stale,
    /// only that avoiding a second one avoids the fault.
    ///
    /// So there are three ways in: <see cref="Snapshot"/> for the menu, where a human clicking twice
    /// is unaffected because each click is its own editor tick; <see cref="SnapshotView"/> and
    /// <see cref="SnapshotPlan"/> for scripted or batchmode use, one process each, which is the
    /// combination that has actually worked every time it has been tried from the command line.
    /// </summary>
    public static class CitySnapshot
    {
        private const string OutputFolder = "CityProof~";
        private const string ScenePath = "Assets/_ScalingLaws/Scenes/City.unity";

        /// <summary>
        /// Both shots. Reliable when clicked from an open editor; not guaranteed from
        /// <c>-executeMethod</c> in batchmode — see the class notes. Scripted callers should invoke
        /// <see cref="SnapshotView"/> and <see cref="SnapshotPlan"/> as two separate processes
        /// instead.
        /// </summary>
        [MenuItem("Scaling Laws/Snapshot the city")]
        public static void Snapshot()
        {
            Directory.CreateDirectory(OutputFolder);

            var camera = OpenAndFindCamera();
            if (camera == null)
            {
                return;
            }

            Shoot(camera, "city_view.png");

            camera = OpenAndFindCamera();
            if (camera == null)
            {
                return;
            }

            AimPlan(camera);
            Shoot(camera, "city_plan.png");
        }

        /// <summary>The whole map from the south west, at the angle the reference uses. One process, one shot.</summary>
        [MenuItem("Scaling Laws/Snapshot the city (view only)")]
        public static void SnapshotView()
        {
            Directory.CreateDirectory(OutputFolder);

            var camera = OpenAndFindCamera();
            if (camera != null)
            {
                Shoot(camera, "city_view.png");
            }
        }

        /// <summary>
        /// Straight down, orthographic, framed on the whole terrain — the frame that shows whether
        /// the districts are where the catalog says they are. One process, one shot.
        /// </summary>
        [MenuItem("Scaling Laws/Snapshot the city (plan only)")]
        public static void SnapshotPlan()
        {
            Directory.CreateDirectory(OutputFolder);

            var camera = OpenAndFindCamera();
            if (camera == null)
            {
                return;
            }

            AimPlan(camera);
            Shoot(camera, "city_plan.png");
        }

        private static void AimPlan(Camera camera)
        {
            camera.orthographic = true;
            camera.orthographicSize = CityLayout.Size / 2f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.transform.position = new Vector3(
                CityLayout.Size / 2f,
                CityLayout.Height * 3f,
                CityLayout.Size / 2f);
        }

        private static Camera OpenAndFindCamera()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"No scene at {ScenePath}.");
                return null;
            }

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("The city scene has no camera.");
            }

            return camera;
        }

        private static void Shoot(Camera camera, string fileName)
        {
            var target = new RenderTexture(1600, 1600, 24) { antiAliasing = 4 };
            var wasActive = RenderTexture.active;
            var previous = camera.targetTexture;

            camera.targetTexture = target;
            camera.Render();
            GL.Flush();

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
