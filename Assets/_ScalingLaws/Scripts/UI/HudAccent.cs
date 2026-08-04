using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The one accent the bottom interface is built from: warm coral on the left, wine red through
    /// the middle, pale violet on the right.
    ///
    /// It lives in one place because the whole point of it is that the contour of the time dial and
    /// the bar along the bottom edge are visibly the same line continued. Two copies of these three
    /// colours would drift apart the first time one of them was tuned.
    ///
    /// USS has no gradient, so it is baked into a one pixel tall texture and stretched. That costs
    /// 256 pixels of memory and works on every element that takes a background image, which is more
    /// than a shader would give and considerably less trouble.
    /// </summary>
    public static class HudAccent
    {
        public static readonly Color Left = new(0.886f, 0.306f, 0.243f);
        public static readonly Color Middle = new(0.369f, 0.086f, 0.157f);
        public static readonly Color Right = new(0.698f, 0.620f, 0.886f);

        private const int Width = 256;

        private static Texture2D barTexture;

        /// <summary>The accent at a point across the interface, 0 on the left edge, 1 on the right.</summary>
        public static Color At(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? Color.Lerp(Left, Middle, t * 2f)
                : Color.Lerp(Middle, Right, (t - 0.5f) * 2f);
        }

        /// <summary>A one pixel tall strip of the gradient, built once and shared.</summary>
        public static Texture2D BarTexture()
        {
            if (barTexture != null)
            {
                return barTexture;
            }

            barTexture = new Texture2D(Width, 1, TextureFormat.RGBA32, false)
            {
                name = "HudAccent",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var x = 0; x < Width; x++)
            {
                barTexture.SetPixel(x, 0, At(x / (float)(Width - 1)));
            }

            barTexture.Apply();
            return barTexture;
        }

        /// <summary>
        /// A ramp between two arbitrary colours, for bars that are not part of the bottom accent.
        /// The creator figures use their own blue and violet so they read as readouts rather than
        /// as another piece of the interface chrome.
        /// </summary>
        public static void PaintRamp(VisualElement element, Color from, Color to)
        {
            var texture = new Texture2D(Width, 1, TextureFormat.RGBA32, false)
            {
                name = "HudRamp",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var x = 0; x < Width; x++)
            {
                texture.SetPixel(x, 0, Color.Lerp(from, to, x / (float)(Width - 1)));
            }

            texture.Apply();
            element.style.backgroundImage = new StyleBackground(texture);
        }

        /// <summary>
        /// Paints part of the gradient onto an element. An element covering the middle third of the
        /// screen gets the middle third of the gradient, so several separate strips still read as
        /// one line crossing the whole interface.
        ///
        /// The slice is baked into its own small texture rather than cropped at draw time, because
        /// UI Toolkit stretches a background image to fill and has no way to show a region of one.
        /// </summary>
        public static void PaintSlice(VisualElement element, float from, float to)
        {
            var texture = new Texture2D(Width, 1, TextureFormat.RGBA32, false)
            {
                name = $"HudAccent {from:0.00}-{to:0.00}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var x = 0; x < Width; x++)
            {
                texture.SetPixel(x, 0, At(Mathf.Lerp(from, to, x / (float)(Width - 1))));
            }

            texture.Apply();
            element.style.backgroundImage = new StyleBackground(texture);
        }
    }
}
