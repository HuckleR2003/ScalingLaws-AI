using UnityEditor;
using UnityEngine;

namespace ScalingLaws.EditorTools
{
    /// <summary>
    /// Renders every portrait and checks something is actually in the frame.
    ///
    /// **A render texture that never got a camera pass is a flat rectangle of the clear colour**, and
    /// on a dark plate that is indistinguishable from a portrait of somebody standing in shadow. The
    /// only honest check is to read the pixels back and look at the spread.
    /// </summary>
    public static class PortraitProbe
    {
        [MenuItem("Scaling Laws/Characters/Probe portraits")]
        public static void Probe()
        {
            var studio = new ScalingLaws.UI.PortraitStudio();
            if (!studio.Open())
            {
                Debug.LogError("PORTRAIT no looks found");
                return;
            }

            var flat = 0;
            var lines = "";

            for (var index = 0; index < studio.LookCount; index++)
            {
                // Two passes: skinning and animation settle a frame behind the first render.
                studio.RenderNow();
                studio.RenderNow();

                var read = new Texture2D(studio.Texture.width, studio.Texture.height,
                    TextureFormat.RGB24, false);

                var was = RenderTexture.active;
                RenderTexture.active = studio.Texture;
                read.ReadPixels(new Rect(0, 0, studio.Texture.width, studio.Texture.height), 0, 0);
                read.Apply();
                RenderTexture.active = was;

                var pixels = read.GetPixels();
                var min = 1f;
                var max = 0f;

                foreach (var pixel in pixels)
                {
                    var value = pixel.grayscale;
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }

                var spread = max - min;
                var lit = spread > 0.08f;
                flat += lit ? 0 : 1;

                lines += $"\n  {(lit ? "OK  " : "FLAT")} {studio.LookName} spread {spread:0.000}";

                // Written out so the frames can be looked at rather than only measured. Every
                // layout fault in this project was found by looking.
                System.IO.Directory.CreateDirectory("PortraitProof~");
                System.IO.File.WriteAllBytes(
                    $"PortraitProof~/{studio.LookName}.png", read.EncodeToPNG());

                Object.DestroyImmediate(read);
                studio.StepLook(1);
            }

            studio.Close();

            Debug.Log($"PORTRAIT {(flat == 0 ? "ALL LIVE" : flat + " FLAT")}{lines}");
        }

        /// <summary>
        /// Every pair of glasses on one face, written out to be looked at.
        ///
        /// **The probe above steps the look and never touches the glasses**, so fourteen frames
        /// proved the faces render and none of them could answer the only question anybody has
        /// asked about the glasses, which is whether they are on the nose or inside the skull.
        /// A tester reported the second. This is how that gets measured: render it and look.
        ///
        /// Runs from the command line, so it works without opening the editor:
        ///   Unity.exe -batchmode -projectPath . -executeMethod
        ///     ScalingLaws.EditorTools.PortraitProbe.ProbeGlasses -quit
        /// </summary>
        [MenuItem("Scaling Laws/Characters/Probe glasses")]
        public static void ProbeGlasses()
        {
            var studio = new ScalingLaws.UI.PortraitStudio();

            if (!studio.Open())
            {
                Debug.LogError("GLASSES no looks found");
                return;
            }

            System.IO.Directory.CreateDirectory("PortraitProof~");

            var lines = "";

            // **Three faces, not one.** The packs are built at different scales: one has its head
            // bone at 2.24m and another at 1.53m, and the placement is a fraction of that height,
            // so a pair that sits correctly on one face proves nothing about the next one.
            var looks = Mathf.Min(3, studio.LookCount);

            for (var face = 0; face < looks; face++)
            {
            for (var index = 0; index < studio.GlassesCount; index++)
            {
                // Two passes, for the reason the look probe states: skinning settles a frame late.
                studio.RenderNow();
                studio.RenderNow();

                var read = new Texture2D(studio.Texture.width, studio.Texture.height,
                    TextureFormat.RGB24, false);

                var was = RenderTexture.active;
                RenderTexture.active = studio.Texture;
                read.ReadPixels(new Rect(0, 0, studio.Texture.width, studio.Texture.height), 0, 0);
                read.Apply();
                RenderTexture.active = was;

                System.IO.File.WriteAllBytes(
                    $"PortraitProof~/glasses_{index}_{studio.LookName}.png", read.EncodeToPNG());

                lines += $"\n  glasses {index} on {studio.LookName}";

                Object.DestroyImmediate(read);
                studio.StepGlasses(1);
            }

                studio.StepLook(1);
            }

            studio.Close();

            Debug.Log($"GLASSES {looks * studio.GlassesCount} frames{lines}");
        }
    }
}
