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

        /// <summary>The gallery, which is not a district and so is aimed at by hand.</summary>
        [MenuItem("Scaling Laws/Look at/The gallery")]
        public static void Gallery() => ShootAt(new Vector2(790f, 1300f), 170f, "gallery");

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
            camera.transform.rotation = Quaternion.Euler(38f, 40f, 0f);
            camera.transform.position = target - camera.transform.forward * 1500f;

            var rt = new RenderTexture(1500, 1000, 24) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            GL.Flush();

            RenderTexture.active = rt;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            shot.Apply();

            var path = Path.Combine("CityProof~", $"look_{file}.png");
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Debug.Log($"[Look] Wrote {path}");
        }
    }
}
