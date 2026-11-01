using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The thick yellow chevron beside Emil's head that folds him away and brings him back.
    ///
    /// **Drawn rather than typed, and that is the standing rule here rather than a preference.** A
    /// chevron taken from a font is a different shape, weight and colour on every machine that
    /// falls back to a different face, which is the same reason the time dial, the brand mark, the
    /// city map, the torn name plate and the support face are all `Painter2D`. USS has no arcs and
    /// no polygons.
    ///
    /// It is two strokes meeting at a point, with round caps and a round join, so the corner is a
    /// corner at any size rather than a notch. The only thing that changes between up and down is
    /// the sign on the vertical, which is why there is one path and not two: two drawings of one
    /// shape is two chances for the down arrow to be a slightly different thickness.
    /// </summary>
    public sealed class GuideFoldArrow : VisualElement
    {
        /// <summary>Stroke width as a share of the box, so it stays thick at any size.</summary>
        private const float Weight = 0.17f;

        /// <summary>How much of the box the chevron spans, leaving a margin to breathe in.</summary>
        private const float Span = 0.62f;

        private bool pointsUp;

        public GuideFoldArrow()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>
        /// Up means "bring him back", down means "fold him away".
        ///
        /// Set from the overlay rather than read from it, because the arrow is rebuilt with the
        /// strip and the fold outlives the strip: the strip is rebuilt whenever the step changes,
        /// and a player who folded Emil on step four has not asked for him again on step five.
        /// </summary>
        public bool PointsUp
        {
            get => pointsUp;
            set
            {
                if (pointsUp == value)
                {
                    return;
                }

                pointsUp = value;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 6f || height < 6f)
            {
                return;
            }

            var size = Mathf.Min(width, height);
            var half = size * Span * 0.5f;
            var middleX = width * 0.5f;
            var middleY = height * 0.5f;

            // A chevron is wider than it is tall, or it reads as an arrowhead rather than as a
            // direction. Two thirds of the horizontal reach on the vertical.
            var rise = half * 0.66f;
            var tip = pointsUp ? middleY - rise * 0.5f : middleY + rise * 0.5f;
            var tail = pointsUp ? middleY + rise * 0.5f : middleY - rise * 0.5f;

            var painter = context.painter2D;

            painter.lineWidth = Mathf.Max(2f, size * Weight);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.strokeColor = new Color(1f, 0.82f, 0.22f);

            painter.BeginPath();
            painter.MoveTo(new Vector2(middleX - half, tail));
            painter.LineTo(new Vector2(middleX, tip));
            painter.LineTo(new Vector2(middleX + half, tail));
            painter.Stroke();
        }
    }
}
