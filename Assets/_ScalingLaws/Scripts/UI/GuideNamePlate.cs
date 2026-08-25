using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The torn black plate his name sits on.
    ///
    /// A rectangle behind a name reads as a caption. A plate with ripped edges reads as something
    /// stuck onto the screen, which is the register the whole tutorial is written in: a cousin
    /// talking over your shoulder rather than a system message.
    ///
    /// Drawn rather than styled because the edges are irregular, and USS has neither a clip path nor
    /// a mask. Same reason the mark and the dial are drawn.
    ///
    /// **The tear is deterministic, not random.** It is derived from the vertex index, so the plate
    /// looks identical on every rebuild and in every screenshot. A shape that reshuffles itself each
    /// time the strip repaints is a shape that flickers.
    /// </summary>
    public sealed class GuideNamePlate : VisualElement
    {
        /// <summary>How far the edge wanders, as a share of the plate's height.</summary>
        private const float Bite = 0.16f;

        private const int Teeth = 9;

        public GuideNamePlate()
        {
            AddToClassList("guide__plate");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 8f || height < 8f)
            {
                return;
            }

            var painter = context.painter2D;
            var bite = height * Bite;

            painter.fillColor = new Color(0.02f, 0.025f, 0.035f, 0.93f);
            painter.BeginPath();

            // Along the top, left to right, with the edge stepping up and down.
            painter.MoveTo(new Vector2(0f, Wander(0)));

            for (var index = 1; index <= Teeth; index++)
            {
                painter.LineTo(new Vector2(width * index / Teeth, Wander(index)));
            }

            // Down the right, which is torn as well, or it reads as a plate somebody trimmed.
            painter.LineTo(new Vector2(width - bite * 0.4f, height * 0.5f));
            painter.LineTo(new Vector2(width, height - Wander(Teeth + 3)));

            // Back along the bottom.
            for (var index = Teeth - 1; index >= 0; index--)
            {
                painter.LineTo(new Vector2(width * index / Teeth, height - Wander(index + 5)));
            }

            painter.LineTo(new Vector2(bite * 0.4f, height * 0.5f));
            painter.ClosePath();
            painter.Fill();

            float Wander(int index)
            {
                // A fixed zigzag with a longer beat laid over it, so the edge is irregular without
                // being noisy and without needing a random source.
                var step = index % 3 == 0 ? 1f : index % 3 == 1 ? 0.15f : 0.62f;
                var beat = index % 4 == 0 ? 0.35f : 0f;
                return bite * (step + beat) * 0.7f;
            }
        }
    }
}
