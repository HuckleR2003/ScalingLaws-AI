using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A block of colour with a torn edge, to put words on.
    ///
    /// **USS has no clip path and no mask**, so a shape that is not a rounded rectangle has to be
    /// drawn. `GuideNamePlate` already draws one of these for the cousin's name and this is the same
    /// idea made reusable: the grants page wanted five of them in three colours.
    ///
    /// **The tear is derived from the vertex index, never random.** A shape that reshuffles itself on
    /// every repaint flickers, and the grants page repaints on every simulated day.
    ///
    /// Drawn behind its own children rather than around them: a painted shape cannot be a container
    /// that lays anything out, so the caller puts a label inside and this fills the space behind it.
    /// </summary>
    public sealed class TornPlate : VisualElement
    {
        /// <summary>How far the edge wanders, as a share of the plate's height.</summary>
        public const float Bite = 0.14f;

        /// <summary>Steps along each torn edge. Odd, so the two ends are not mirror images.</summary>
        public const int Teeth = 7;

        private readonly Color fill;

        /// <summary>
        /// Which edges are torn.
        ///
        /// The left and right ends on a band, so it reads as a strip of something torn off a sheet;
        /// all four would read as a sticker and cost the words their margins.
        /// </summary>
        public TornPlate(Color fill)
        {
            this.fill = fill;

            AddToClassList("torn");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 6f || height < 6f || float.IsNaN(width))
            {
                return;
            }

            var painter = context.painter2D;
            var bite = height * Bite;

            painter.fillColor = fill;
            painter.BeginPath();

            painter.MoveTo(new Vector2(Wander(0, bite), 0f));

            // Along the top, straight: the words sit against it and a wandering top edge crowds
            // whichever line happens to be nearest it.
            painter.LineTo(new Vector2(width - Wander(1, bite), 0f));

            // Down the right, torn.
            for (var tooth = 0; tooth <= Teeth; tooth++)
            {
                var along = tooth / (float)Teeth;

                painter.LineTo(new Vector2(
                    width - Wander(tooth + 2, bite),
                    along * height));
            }

            painter.LineTo(new Vector2(Wander(3, bite), height));

            // And back up the left.
            for (var tooth = Teeth; tooth >= 0; tooth--)
            {
                var along = tooth / (float)Teeth;

                painter.LineTo(new Vector2(
                    Wander(tooth + 5, bite),
                    along * height));
            }

            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>
        /// How far in this step of the edge sits.
        ///
        /// A fixed pattern keyed on the step, so the same plate is the same shape every repaint. The
        /// numbers are arbitrary and only have to look unplanned.
        /// </summary>
        private static float Wander(int step, float bite)
        {
            var pattern = step % 5 switch
            {
                0 => 0.15f,
                1 => 0.85f,
                2 => 0.35f,
                3 => 1.00f,
                _ => 0.55f
            };

            return pattern * bite;
        }
    }

    /// <summary>
    /// The band under the asset rows: blue on the left, green on the right.
    ///
    /// **USS has no gradients**, which is why the bottom bar's accent is a baked texture and the
    /// clock's contour is forty eight short segments. This is the second of those: a run of vertical
    /// strips, each a step further along the ramp, which is cheap and needs no asset.
    ///
    /// Blue to green because that is the reading, not because it is pretty: the left of the band is
    /// what the company is priced at and the right is how much of that is actually in the building.
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
