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

        /// <summary>30fps for thirty seconds. Long enough to read the map, short enough to watch.</summary>
        public const int Frames = 900;

        public const int FramesPerSecond = 30;

        // 1600x900 rather than full HD, and it is not only about file size.
        //
        // **Nine hundred renders in one editor tick ran the GPU out of memory at frame 220.**
        // `-executeMethod` never yields to a frame boundary, so every render, resolve and readback
        // queues against the same device with nothing forcing it to drain, and 4x MSAA at full HD
        // was enough to reach `E_OUTOFMEMORY` and take the editor down with it. Smaller buffers,
        // 2x MSAA, an explicit flush after every frame and a fresh target every `RecycleEvery`
        // frames between them keep it inside what the driver will hold.
        //
        // It also matches the interface tour, which matters because the two are cut together.
        public const int Width = 1600;
        public const int Height = 900;

        /// <summary>How often the render target is thrown away and remade.</summary>
        private const int RecycleEvery = 60;

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
                float lookAbove = 0f, float dwell = 1f, float roll = 0f)
            {
                Caption = caption;
                Ground = new Vector2(x, z);
                Above = above;
                Target = new Vector2(lookX, lookZ);
                TargetAbove = lookAbove;
                Dwell = Mathf.Max(0.05f, dwell);
                Roll = roll;
            }

            public string Caption { get; }
            public Vector2 Ground { get; }
            public float Above { get; }
            public Vector2 Target { get; }
            public float TargetAbove { get; }

            /// <summary>
            /// How much of the running time the leg *into* this beat is worth.
            ///
            /// **This is the whole difference between a flight and a pan.** With one weight for
            /// every leg, a low pass down a street and the long haul across the bay took the same
            /// number of seconds, so the street crawled and the bay felt hurried. A number under one
            /// is a fast pass; over one is a reveal the eye is given time to read.
            /// </summary>
            public float Dwell { get; }

            /// <summary>
            /// Bank, in degrees. Positive rolls right.
            ///
            /// A camera that turns without banking reads as a drone on rails. Twelve degrees through
            /// a curve is enough to feel flown and little enough that the horizon is not a gimmick.
            /// </summary>
            public float Roll { get; }
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
            // Held: the spline needs a point before the start to know which way the curve leaves.
            new Beat("hold", 1960f, 60f, 330f, 1610f, 405f, dwell: 1f),
            new Beat("approach", 1930f, 90f, 300f, 1610f, 405f, dwell: 1.5f),

            // Riverdale. A fast low run round the loop: three legs, all under one, so the estate
            // goes past at the speed the author asked for rather than at survey pace.
            new Beat("the loop, east", 1855f, 250f, 132f, 1650f, 380f, dwell: 0.65f, roll: 9f),
            new Beat("the loop, north", 1690f, 600f, 108f, 1600f, 430f, dwell: 0.6f, roll: 13f),
            new Beat("the loop, west", 1395f, 480f, 118f, 1630f, 430f, dwell: 0.6f, roll: 10f),

            // The house. The one place the flight slows down, because it is the player's.
            new Beat("the house", 1530f, 566f, 92f, 1662f, 472f, 6f, dwell: 1.5f, roll: -6f),
            new Beat("over the house", 1648f, 546f, 74f, 1670f, 456f, 4f, dwell: 1.3f),

            // Out over the water, climbing. A long leg, and it is allowed to take its time.
            new Beat("leaving", 1600f, 720f, 200f, 1280f, 920f, dwell: 1.4f, roll: -11f),
            new Beat("the bay", 1360f, 940f, 268f, 1060f, 1020f, dwell: 1.2f, roll: -7f),

            // Downtown. Slow: the towers are the payoff and the eye needs time on them.
            new Beat("downtown", 1190f, 1000f, 205f, 1000f, 1030f, 62f, dwell: 1.5f),
            new Beat("the towers", 1030f, 1170f, 250f, 1000f, 1030f, 72f, dwell: 1.4f, roll: 8f),

            // North west across the park to the second estate, fast again.
            new Beat("the park", 860f, 1330f, 300f, 720f, 1500f, 20f, dwell: 0.8f, roll: 10f),
            new Beat("greendale, east", 560f, 1580f, 150f, 360f, 1620f, dwell: 0.6f, roll: 12f),
            new Beat("greendale, west", 250f, 1600f, 128f, 330f, 1660f, dwell: 0.6f, roll: 8f),

            // And out, high enough that the last frame is the whole map.
            new Beat("the whole map", 420f, 1180f, 560f, 980f, 1080f, dwell: 1.6f),
            new Beat("hold", 520f, 940f, 700f, 1000f, 1040f, dwell: 1f)
        };

        /// <summary>
        /// Where each beat falls through the running time, from the dwells.
        ///
        /// Built once and cached: `Place` runs nine hundred times a flight and the answer never
        /// changes. The first and last entries are the spline's control points and are not flown,
        /// so they carry no time.
        /// </summary>
        private static float[] Milestones
        {
            get
            {
                if (milestones != null)
                {
                    return milestones;
                }

                var legs = Flight.Count - 3;
                var marks = new float[legs + 1];
                var total = 0f;

                for (var leg = 0; leg < legs; leg++)
                {
                    total += Flight[leg + 2].Dwell;
                    marks[leg + 1] = total;
                }

                for (var index = 0; index <= legs; index++)
                {
                    marks[index] /= total;
                }

                milestones = marks;
                return milestones;
            }
        }

        private static float[] milestones;

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

            var target = NewTarget();
            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var wasActive = RenderTexture.active;

            camera.targetTexture = target;

            try
            {
                for (var frame = 0; frame < Frames; frame++)
                {
                    if (frame > 0 && frame % RecycleEvery == 0)
                    {
                        RenderTexture.active = null;
                        camera.targetTexture = null;
                        target.Release();
                        UnityEngine.Object.DestroyImmediate(target);

                        target = NewTarget();
                        camera.targetTexture = target;
                    }

                    Place(camera, terrain, frame / (float)(Frames - 1));

                    camera.Render();
                    RenderTexture.active = target;
                    shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    shot.Apply();

                    // Nothing else in this loop reaches a frame boundary, so this is the only thing
                    // telling the driver it may drain what it is holding.
                    GL.Flush();

                    File.WriteAllBytes(
                        Path.Combine(OutputFolder, $"frame_{frame:0000}.png"),
                        shot.EncodeToPNG());

                    if (frame % 100 == 0)
                    {
                        Debug.Log($"[Scaling Laws] flight frame {frame} of {Frames}.");
                    }
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
            // Eased only at the very ends. Easing the whole path was what made every leg the same
            // speed regardless of its dwell: the curve was doing the pacing and the weights had
            // nothing left to do.
            var eased = Ends(Mathf.Clamp01(through));

            var (index, t) = Leg(eased);

            var a = Flight[index];
            var b = Flight[index + 1];
            var c = Flight[index + 2];
            var d = Flight[index + 3];

            // Smooth within a leg as well, so the joins between beats are not corners.
            var soft = Smooth(t);

            var ground = CatmullRom(a.Ground, b.Ground, c.Ground, d.Ground, soft);
            var above = CatmullRom(a.Above, b.Above, c.Above, d.Above, soft);

            var look = CatmullRom(a.Target, b.Target, c.Target, d.Target, soft);
            var lookAbove = CatmullRom(a.TargetAbove, b.TargetAbove, c.TargetAbove, d.TargetAbove, soft);
            var roll = CatmullRom(a.Roll, b.Roll, c.Roll, d.Roll, soft);

            var position = new Vector3(ground.x, GroundAt(terrain, ground) + above, ground.y);
            var at = new Vector3(look.x, GroundAt(terrain, look) + lookAbove, look.y);

            var forward = (at - position).normalized;

            camera.transform.position = position;

            // Bank around the direction of travel rather than around world up, or the horizon
            // shears instead of tilting.
            camera.transform.rotation =
                Quaternion.AngleAxis(roll, forward) * Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>Which leg the flight is on at this point through, and how far along it.</summary>
        private static (int Index, float T) Leg(float through)
        {
            var marks = Milestones;

            for (var index = 0; index < marks.Length - 1; index++)
            {
                if (through > marks[index + 1] && index < marks.Length - 2)
                {
                    continue;
                }

                var from = marks[index];
                var span = Mathf.Max(marks[index + 1] - from, 1e-6f);

                return (index, Mathf.Clamp01((through - from) / span));
            }

            return (marks.Length - 2, 1f);
        }

        /// <summary>
        /// Eases the first and last tenth only.
        ///
        /// A smoothstep across the whole flight is a camera that accelerates for ten seconds and
        /// decelerates for ten, which flattens every dwell in the table above.
        /// </summary>
        private static float Ends(float t)
        {
            const float lip = 0.10f;

            if (t < lip)
            {
                return Smooth(t / lip) * lip;
            }

            if (t > 1f - lip)
            {
                return 1f - lip + Smooth((t - (1f - lip)) / lip) * lip;
            }

            return t;
        }

        /// <summary>Which beat the flight is closest to at this point, for naming a still.</summary>
        private static string Nearest(float through)
        {
            var (index, t) = Leg(Ends(Mathf.Clamp01(through)));
            return Flight[index + (t < 0.5f ? 1 : 2)].Caption;
        }

        private static RenderTexture NewTarget()
        {
            var target = new RenderTexture(Width, Height, 24) { antiAliasing = 2 };
            target.Create();
            return target;
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
            var target = NewTarget();
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
