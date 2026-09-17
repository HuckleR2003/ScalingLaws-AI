using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Photographs one place in the city, close enough to judge it.
    ///
    /// <see cref="CitySnapshot"/> takes the whole map, which is the wrong tool for asking whether a
    /// driveway meets its road or a junction's lights stand where they should. Each entry here aims
    /// at a district by name and writes a file named after it.
    ///
    /// **Orthographic, at an angle, and that is not a style choice.** A perspective camera over this
    /// scene ends the batchmode process with no crash report and exit code 127, reproducibly, once
    /// the city has this much in it; the same view orthographic renders every time. The tilt keeps
    /// it readable as a place rather than a plan.
    ///
    /// **Run without `-nographics`.** That flag disables the graphics device, and every render in
    /// this project fails under it — silently blank at best, a segfault at worst. See
    /// `unity-batchmode-render-crashes` in the working notes.
    /// </summary>
    public static class CityLook
    {
        [MenuItem("Scaling Laws/Look at/Downtown")]
        public static void Downtown() => Shoot("downtown", 220f);

        [MenuItem("Scaling Laws/Look at/Greendale (suburb)")]
        public static void Greendale() => Shoot("greendale", 200f);

        [MenuItem("Scaling Laws/Look at/Riverdale (suburb)")]
        public static void Riverdale() => Shoot("riverdale", 200f);

        [MenuItem("Scaling Laws/Look at/Media District")]
        public static void Media() => Shoot("media", 180f);

        [MenuItem("Scaling Laws/Look at/Innovation District")]
        public static void Innovation() => Shoot("innovation", 180f);

        [MenuItem("Scaling Laws/Look at/Civic Center")]
        public static void Civic() => Shoot("civic", 160f);

        [MenuItem("Scaling Laws/Look at/Waterfront and Port")]
        public static void Port() => Shoot("port", 200f);

        [MenuItem("Scaling Laws/Look at/Bayview Park")]
        public static void Park() => Shoot("park", 200f);

        [MenuItem("Scaling Laws/Look at/River Works")]
        public static void RiverWorks() => Shoot("riverworks", 150f);

        /// <summary>Midtown, which is not a district of its own but downtown grown west towards the media strip.</summary>
        [MenuItem("Scaling Laws/Look at/Midtown")]
        public static void Midtown() => ShootAt(new Vector2(601f, 884f), 190f, "midtown");

        /// <summary>Silicon Valley from over the bay, looking inland at the waterfront and the bench behind it.</summary>
        [MenuItem("Scaling Laws/Look at/Silicon Valley")]
        public static void SiliconValley() => ShootFrom(new Vector2(680f, -640f), 430f, "silicon", 32f, 105f);

        /// <summary>The central plaza, the Own Tower and the Research Campus, close.</summary>
        [MenuItem("Scaling Laws/Look at/Silicon Valley plaza")]
        public static void SiliconPlaza() => ShootFrom(new Vector2(640f, -720f), 190f, "silicon_plaza", 34f, 115f);

        /// <summary>The gate from the port and the headquarters along the north half of the boulevard.</summary>
        [MenuItem("Scaling Laws/Look at/Silicon Valley north")]
        public static void SiliconNorth() => ShootFrom(new Vector2(600f, -330f), 200f, "silicon_north", 34f, 125f);

        /// <summary>
        /// Any close look from the command line: <c>-at x:z:size:tilt:bearing[:name]</c>, for checking one
        /// building or one sign without adding a menu entry for it.
        /// </summary>
        public static void At()
        {
            var args = System.Environment.GetCommandLineArgs();
            var index = System.Array.IndexOf(args, "-at");
            if (index < 0 || index + 1 >= args.Length)
            {
                Debug.LogError("[Look] -at x:z:size:tilt:bearing[:name] is required.");
                return;
            }

            var parts = args[index + 1].Split(':');
            float Read(int part) => float.Parse(parts[part], System.Globalization.CultureInfo.InvariantCulture);
            var name = parts.Length > 5 ? parts[5] : $"at_{parts[0]}_{parts[1]}";
            ShootFrom(new Vector2(Read(0), Read(1)), Read(2), name, Read(3), Read(4));
        }

        /// <summary>The gallery, which is not a district and so is aimed at by hand.</summary>
        [MenuItem("Scaling Laws/Look at/The gallery")]
        public static void Gallery() => ShootAt(new Vector2(790f, 1300f), 170f, "gallery");

        /// <summary>
        /// Every district straight from above, north up, one file each (`plan_&lt;district&gt;.png`).
        ///
        /// The angled views are for judging how a place looks; these are for checking whether a road
        /// meets the road it should, which a tilt hides behind the buildings.
        /// </summary>
        [MenuItem("Scaling Laws/Look at/Every district from above")]
        public static void Plans()
        {
            Directory.CreateDirectory("CityProof~");

            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Look] No City.unity.");
                return;
            }

            // From the command line, `-districts port,civic` picks some: a batchmode process renders a
            // handful of these and then dies without a word, so a long list is split across runs.
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-districts");
            var wanted = at >= 0 && at + 1 < args.Length ? args[at + 1].Split(',') : null;

            // `-spots x:z:size;x:z:size` photographs exact places instead, for a close look at one junction.
            var spots = System.Array.IndexOf(args, "-spots");
            if (spots >= 0 && spots + 1 < args.Length)
            {
                foreach (var spot in args[spots + 1].Split(';'))
                {
                    var parts = spot.Split(':');
                    var x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                    var z = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    var size = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

                    Photograph(new Vector2(x, z), size, $"spot_{x:0}_{z:0}", Quaternion.Euler(90f, 0f, 0f), 1000, 1000);
                }

                return;
            }

            foreach (var district in CityLayout.Districts)
            {
                if (wanted != null && System.Array.IndexOf(wanted, district.Id) < 0)
                {
                    continue;
                }

                Photograph(new Vector2(district.CentreX, district.CentreZ), 260f, $"plan_{district.Id}",
                    Quaternion.Euler(90f, 0f, 0f), 1200, 1200);
            }
        }

        private static void Shoot(string districtId, float size)
        {
            foreach (var district in CityLayout.Districts)
            {
                if (district.Id == districtId)
                {
                    ShootAt(new Vector2(district.CentreX, district.CentreZ), size, districtId);
                    return;
                }
            }

            Debug.LogError($"[Look] No district called '{districtId}'.");
        }

        private static void ShootAt(Vector2 where, float size, string file)
        {
            Directory.CreateDirectory("CityProof~");

            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Look] No City.unity.");
                return;
            }

            Photograph(where, size, $"look_{file}", Quaternion.Euler(38f, 40f, 0f), 1500, 1000);
        }

        /// <summary>Like <see cref="ShootAt"/>, from a chosen tilt and compass bearing.</summary>
        private static void ShootFrom(Vector2 where, float size, string file, float tilt, float bearing)
        {
            Directory.CreateDirectory("CityProof~");

            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Look] No City.unity.");
                return;
            }

            Photograph(where, size, $"look_{file}", Quaternion.Euler(tilt, bearing, 0f), 1500, 1000);
        }

        private static void Photograph(Vector2 where, float size, string file, Quaternion aim, int width, int height)
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("[Look] No camera in the scene.");
                return;
            }

            var target = new Vector3(where.x, CityTerrainBuilder.HeightAt(where.x, where.y), where.y);

            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.farClipPlane = 8000f;
            camera.transform.rotation = aim;
            camera.transform.position = target - camera.transform.forward * 1500f;

            var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            GL.Flush();

            RenderTexture.active = rt;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            shot.Apply();

            var path = Path.Combine("CityProof~", $"{file}.png");
            File.WriteAllBytes(path, shot.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(shot);
            rt.Release();

            Debug.Log($"[Look] Wrote {path}");
        }
    }
}
