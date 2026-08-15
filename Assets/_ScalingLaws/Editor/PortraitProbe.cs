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
    }
}
