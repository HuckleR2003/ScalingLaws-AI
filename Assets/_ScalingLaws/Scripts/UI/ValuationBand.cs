using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The valuation band: what the company is worth, and how much of that is things you own.
    ///
    /// **Drawn in strips, because USS has no gradients.** Same reason the HUD accent is a baked
    /// texture and the time dial is `Painter2D`: the fade from the backed share into the rest has
    /// to happen across the bar rather than at a hard edge, and there is no other way to say it.
    ///
    /// It shared a file with `TornPlate` until the grants page stopped tearing its plates. That
    /// class is gone; this one is what the file is now.
    /// </summary>
    public sealed class ValuationBand : VisualElement
    {
        /// <summary>Strips across the band. Enough that the steps are invisible at any width.</summary>
        public const int Steps = 64;

        /// <summary>How far past the backed share the green takes to become blue.</summary>
        public const float FadeWidth = 0.10f;

        /// <summary>The colour of a price nobody has put anything behind yet.</summary>
        private static readonly Color Unbacked = new(0.18f, 0.34f, 0.62f);

        /// <summary>And of the part that is in the building.</summary>
        private static readonly Color Backed = new(0.20f, 0.56f, 0.42f);

        /// <summary>How far along the ramp the green actually reaches, 0 to 1.</summary>
        private float backed;

        public ValuationBand()
        {
            AddToClassList("vband");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>
        /// How much of the valuation the company's own assets cover.
        ///
        /// The band runs green as far as that share and stays blue past it, so a company whose
        /// price is mostly promise reads blue and one holding a warehouse reads green. Clamped,
        /// because a company worth less than its parts is a real state and the band should fill
        /// rather than overflow.
        /// </summary>
        public void SetBacked(double share)
        {
            backed = Mathf.Clamp01((float)share);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 4f || height < 2f || float.IsNaN(width))
            {
                return;
            }

            var painter = context.painter2D;
            var step = width / Steps;

            for (var index = 0; index < Steps; index++)
            {
                var along = index / (float)(Steps - 1);

                // **Green covers the backed share and nothing more.** The first pass divided by it,
                // which saturates the moment `along` passes it: a company whose assets cover three
                // per cent of its price drew ninety seven per cent green, the exact opposite of the
                // reading. Found by rendering it against a real campaign.
                //
                // The fade is over a tenth of the band so the edge is a boundary rather than a
                // seam, and the whole thing still reads as one bar rather than two.
                var mix = along <= backed
                    ? 1f
                    : Mathf.Clamp01(1f - (along - backed) / FadeWidth);

                painter.fillColor = Color.Lerp(Unbacked, Backed, mix);

                painter.BeginPath();
                painter.MoveTo(new Vector2(index * step, 0f));
                painter.LineTo(new Vector2((index + 1) * step + 1f, 0f));
                painter.LineTo(new Vector2((index + 1) * step + 1f, height));
                painter.LineTo(new Vector2(index * step, height));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
