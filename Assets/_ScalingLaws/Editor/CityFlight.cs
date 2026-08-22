using System;
using System.Collections.Generic;
using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Flies a camera over Bayview and writes every frame to disk.
    ///
    /// **For before-and-after videos of the map, which is why it is frames and not a recording.**
    /// The point of the shot is to run it, change something (more houses, more road, lamps along a
    /// pavement, a district reconnected), run it again, and put the two side by side. That only
    /// works if the second flight is identical to the first, and a real-time capture is not: it
    /// samples wherever the frame rate happened to land. Here frame 137 is the same position on
    /// every run forever, so the only difference between two flights is the map.
    ///
    /// It also means this works in batchmode with no display, which is the only way to render 480
    /// frames without watching them.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath . -executeMethod ScalingLaws.Editor.CityFlight.Fly -quit
    /// </code>
    ///
    /// The folder ends in a tilde so Unity does not import half a thousand PNGs as project assets.
    /// </summary>
    public static class CityFlight
    {
        private const string OutputFolder = "CityFlight~";
        private const string ScenePath = "Assets/_ScalingLaws/Scenes/City.unity";

        /// <summary>24fps for twenty seconds. Long enough to read the map, short enough to watch.</summary>
        public const int Frames = 480;

        public const int FramesPerSecond = 24;

        public const int Width = 1920;
        public const int Height = 1080;

        /// <summary>
        /// One moment in the flight: where the camera is, and what it is pointed at.
        ///
        /// **Height is above the ground, not above sea level.** Bayview has four hundred metres of
        /// relief and the suburbs sit on a shoulder of it, so an absolute height that clears the
        /// hills is an altitude that loses the houses, and one that frames the houses flies through
        /// the hill on the way to downtown.
        /// </summary>
        private readonly struct Beat
        {
            public Beat(string caption, float x, float z, float above, float lookX, float lookZ,
                float lookAbove = 0f)
            {
                Caption = caption;
                Ground = new Vector2(x, z);
                Above = above;
                Target = new Vector2(lookX, lookZ);
                TargetAbove = lookAbove;
            }

            public string Caption { get; }
            public Vector2 Ground { get; }
            public float Above { get; }
            public Vector2 Target { get; }
            public float TargetAbove { get; }
        }

        /// <summary>
        /// The three things the author asked to see, in the order that flies well.
        ///
        /// Riverdale first because the loop is a closed circle and reads immediately as a place
        /// somebody lives; the founder's house is inside that same loop, so arriving at it is a
        /// descent rather than a cut; then the long run north west across the water to the towers,
        /// which is the shot that shows whether the map is actually connected.
        ///
        /// The first and last beats are doubled. A Catmull-Rom spline needs a point before the
        /// start and after the end to know which way the curve is leaving and arriving, and without
        /// them the flight snaps into motion and stops dead.
        /// </summary>
        private static readonly List<Beat> Flight = new()
        {
            new Beat("approach", 1900f, 120f, 260f, 1610f, 405f),
            new Beat("approach", 1900f, 120f, 260f, 1610f, 405f),

            // Around the Riverdale loop. Two beats on opposite sides so the spline curves round it
            // rather than cutting the corner.
            new Beat("the loop, east", 1830f, 300f, 175f, 1610f, 405f),
            new Beat("the loop, north", 1650f, 620f, 150f, 1610f, 420f),
            new Beat("the loop, west", 1400f, 470f, 130f, 1620f, 430f),

            // Down to the house, low and close.
            new Beat("the house", 1520f, 560f, 96f, 1660f, 470f, 6f),
            new Beat("over the house", 1640f, 548f, 82f, 1668f, 458f, 4f),

            // Up and away, over the water.
            new Beat("leaving", 1560f, 700f, 190f, 1300f, 900f),
            new Beat("the bay", 1330f, 900f, 250f, 1080f, 1010f),

            // Downtown, rising over the towers.
            new Beat("downtown", 1180f, 1010f, 210f, 1000f, 1030f, 60f),
            new Beat("the towers", 1010f, 1180f, 260f, 1000f, 1030f, 70f),
            new Beat("away", 800f, 1400f, 420f, 1000f, 1030f, 40f),
            new Beat("away", 800f, 1400f, 420f, 1000f, 1030f, 40f)
        };

        [MenuItem("Scaling Laws/Fly over the city")]
        public static void Fly()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[Scaling Laws] No scene at {ScenePath}. Build the city first.");
                return;
            }

            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                Debug.LogError("[Scaling Laws] The city scene has no camera to fly.");
                return;
            }

            // Whatever the snapshot tool last left it as. The flight is a perspective shot.
            var restore = (camera.transform.position, camera.transform.rotation,
                camera.orthographic, camera.fieldOfView, camera.farClipPlane, camera.clearFlags);

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, CityLayout.Size * 2f);

            if (Directory.Exists(OutputFolder))
            {
                // A shorter second flight left the tail of the first one behind it, and the video
                // ended with frames from a map that no longer exists.
                Directory.Delete(OutputFolder, true);
            }

            Directory.CreateDirectory(OutputFolder);

            var terrain = Terrain.activeTerrain;

            if (terrain == null)
            {
                Debug.LogWarning("[Scaling Laws] No terrain, so the flight is at absolute height "
                    + "and will very likely go through a hill.");
            }

            var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var wasActive = RenderTexture.active;

            camera.targetTexture = target;

            try
            {
                for (var frame = 0; frame < Frames; frame++)
                {
                    Place(camera, terrain, frame / (float)(Frames - 1));

                    camera.Render();
                    RenderTexture.active = target;
                    shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    shot.Apply();

                    File.WriteAllBytes(
                        Path.Combine(OutputFolder, $"frame_{frame:0000}.png"),
                        shot.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = wasActive;
                camera.targetTexture = null;

                (camera.transform.position, camera.transform.rotation, camera.orthographic,
                    camera.fieldOfView, camera.farClipPlane, camera.clearFlags) = restore;

                UnityEngine.Object.DestroyImmediate(shot);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }

            Debug.Log($"[Scaling Laws] {Frames} frames in {OutputFolder}/. "
                + $"Make the video with:\n"
                + $"  ffmpeg -framerate {FramesPerSecond} -i {OutputFolder}/frame_%04d.png "
                + "-c:v libx264 -pix_fmt yuv420p -crf 18 bayview.mp4");
        }

        /// <summary>
        /// Puts the camera where it belongs at this point through the flight.
        ///
        /// **Eased rather than linear.** A camera that starts and stops at full speed reads as a
        /// slideshow of positions, and the whole reason to fly rather than cut is that the eye can
        /// follow the ground between two places.
        /// </summary>
        private static void Place(Camera camera, Terrain terrain, float through)
        {
            var eased = Smooth(Mathf.Clamp01(through));

            // The doubled beats at each end are the spline's control points and are not flown
            // through, so the travelled span is one shorter at each end.
            var span = Flight.Count - 3;
            var scaled = eased * span;
            var index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, span - 1);
            var t = scaled - index;

            var a = Flight[index];
            var b = Flight[index + 1];
            var c = Flight[index + 2];
            var d = Flight[index + 3];

            var ground = CatmullRom(a.Ground, b.Ground, c.Ground, d.Ground, t);
            var above = CatmullRom(a.Above, b.Above, c.Above, d.Above, t);

            var look = CatmullRom(a.Target, b.Target, c.Target, d.Target, t);
            var lookAbove = CatmullRom(a.TargetAbove, b.TargetAbove, c.TargetAbove, d.TargetAbove, t);

            var position = new Vector3(ground.x, GroundAt(terrain, ground) + above, ground.y);
            var at = new Vector3(look.x, GroundAt(terrain, look) + lookAbove, look.y);

            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((at - position).normalized, Vector3.up);
        }

        /// <summary>Which beat the flight is closest to at this point, for naming a still.</summary>
        private static string Nearest(float through)
        {
            var span = Flight.Count - 3;
            var scaled = Smooth(Mathf.Clamp01(through)) * span;
            var index = Mathf.Clamp(Mathf.RoundToInt(scaled) + 1, 1, Flight.Count - 1);

            return Flight[index].Caption;
        }

        /// <summary>The ground under a map point, or sea level when there is no terrain.</summary>
        private static float GroundAt(Terrain terrain, Vector2 point) =>
            terrain == null
                ? CityLayout.SeaLevel
                : terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + terrain.GetPosition().y;

        /// <summary>Ease in and out, so the flight has a start and an end rather than a cut.</summary>
        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) =>
            new(CatmullRom(a.x, b.x, c.x, d.x, t), CatmullRom(a.y, b.y, c.y, d.y, t));

        private static float CatmullRom(float a, float b, float c, float d, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return 0.5f * ((2f * b)
                + (-a + c) * t
                + (2f * a - 5f * b + 4f * c - d) * t2
                + (-a + 3f * b - 3f * c + d) * t3);
        }

        /// <summary>
        /// Six stills at the beats, for a quick look without rendering the whole flight.
        ///
        /// Four hundred and eighty frames is a couple of minutes and a gigabyte. Most of the time
        /// the question is only whether the path still clears the ground and still points at
        /// something, and six pictures answer that.
        /// </summary>
        [MenuItem("Scaling Laws/Check the flight path")]
        public static void CheckPath()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[Scaling Laws] No scene at {ScenePath}.");
                return;
            }

            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                Debug.LogError("[Scaling Laws] The city scene has no camera.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);

            // The stills are named after the beat they are nearest, so moving a beat renames a file
            // and the old one would sit there looking like part of this run. Only the stills go;
            // a rendered flight in the same folder is left alone.
            foreach (var stale in Directory.GetFiles(OutputFolder, "path_*.png"))
            {
                File.Delete(stale);
            }

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, CityLayout.Size * 2f);

            var terrain = Terrain.activeTerrain;
            var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var wasActive = RenderTexture.active;

            camera.targetTexture = target;

            const int stills = 6;

            for (var index = 0; index < stills; index++)
            {
                var through = index / (float)(stills - 1);
                Place(camera, terrain, through);

                camera.Render();
                RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();

                var name = $"path_{index}_{Nearest(through).Replace(' ', '_').Replace(",", string.Empty)}.png";

                File.WriteAllBytes(Path.Combine(OutputFolder, name), shot.EncodeToPNG());
            }

            RenderTexture.active = wasActive;
            camera.targetTexture = null;

            UnityEngine.Object.DestroyImmediate(shot);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);

            Debug.Log($"[Scaling Laws] {stills} stills along the path in {OutputFolder}/.");
        }
    }
}
