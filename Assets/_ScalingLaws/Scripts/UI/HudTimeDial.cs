using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The half disc that rises out of the left end of the bottom bar and holds the date and time.
    ///
    /// Drawn rather than styled because the one thing it has to do is carry the accent gradient
    /// around a curve, and USS has neither arcs nor gradients. Painter2D has arcs but only a single
    /// stroke colour, so the contour is stroked as a run of short segments, each one a step further
    /// along the gradient. Close up that is a gradient; there is no seam to find at this size.
    ///
    /// The arc is also the day clock: the filled portion is how far into the day the sim has run,
    /// which is the same number the bar along the bottom edge shows.
    /// </summary>
    public sealed class HudTimeDial : VisualElement
    {
        private const int Segments = 48;
        private const float ContourWidth = 2f;

        private static readonly Color Plate = new(0.055f, 0.058f, 0.070f, 0.94f);
        private static readonly Color Spent = new(1f, 1f, 1f, 0.10f);

        private float progress;

        public HudTimeDial()
        {
            AddToClassList("hud-dial");
            generateVisualContent += Draw;
        }

        /// <summary>How far into the day, 0 to 1. Repaints only when the drawn arc would change.</summary>
        public float Progress
        {
            get => progress;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Abs(clamped - progress) < 0.004f)
                {
                    return;
                }

                progress = clamped;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            var painter = context.painter2D;
            var centre = new Vector2(rect.width / 2f, rect.height);
            var radius = Mathf.Min(rect.width / 2f, rect.height) - ContourWidth;

            // The plate first, so the contour sits on its edge rather than under it.
            painter.fillColor = Plate;
            painter.BeginPath();
            painter.MoveTo(centre);
            AppendArc(painter, centre, radius, 180f, 360f);
            painter.ClosePath();
            painter.Fill();

            // How much of the day has gone, swept from the left end of the arc.
            if (progress > 0.001f)
            {
                painter.fillColor = Spent;
                painter.BeginPath();
                painter.MoveTo(centre);
                AppendArc(painter, centre, radius * 0.92f, 180f, 180f + 180f * progress);
                painter.ClosePath();
                painter.Fill();
            }

            painter.lineWidth = ContourWidth;
            painter.lineCap = LineCap.Butt;

            for (var index = 0; index < Segments; index++)
            {
                var from = 180f + 180f * index / Segments;
                var to = 180f + 180f * (index + 1) / Segments;

                // The dial sits at the far left of the interface, so it only ever uses the first
                // slice of the accent: coral into wine, never as far as the violet.
                painter.strokeColor = HudAccent.At(0.02f + 0.16f * (index / (float)Segments));
                painter.BeginPath();
                painter.MoveTo(PointOn(centre, radius, from));
                AppendArc(painter, centre, radius, from, to);
                painter.Stroke();
            }
        }

        private static void AppendArc(Painter2D painter, Vector2 centre, float radius, float fromDegrees,
            float toDegrees)
        {
            var steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(toDegrees - fromDegrees) / 4f));
            for (var step = 0; step <= steps; step++)
            {
                var angle = Mathf.Lerp(fromDegrees, toDegrees, step / (float)steps);
                painter.LineTo(PointOn(centre, radius, angle));
            }
        }

        private static Vector2 PointOn(Vector2 centre, float radius, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(
                centre.x + Mathf.Cos(radians) * radius,
                centre.y + Mathf.Sin(radians) * radius);
        }

        /// <summary>
        /// A clock reading derived from how far the day has run. The simulation has no hour hand,
        /// so this is the day bar said a second way rather than a separate piece of state.
        /// </summary>
        public static string ClockText(double dayProgress)
        {
            var minutes = (int)Math.Round(Math.Clamp(dayProgress, 0.0, 0.9999) * 24.0 * 60.0);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }
}
