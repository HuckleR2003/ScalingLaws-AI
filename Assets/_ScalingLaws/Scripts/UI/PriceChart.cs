using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A price line, drawn.
    ///
    /// **Painter2D rather than a stack of elements.** USS has no way to draw a diagonal, so the
    /// alternative is one thin rotated element per segment, which is ninety elements for ninety
    /// days and a layout pass on every repaint. Same reason the time dial and the world map are
    /// drawn rather than built.
    ///
    /// The scale is the series' own range with a margin, never zero-based. Ninety days of a share
    /// that moved between nineteen and twenty-one is a flat line on a zero-based axis, and the
    /// whole point of the chart is the shape of that movement.
    /// </summary>
    public sealed class PriceChart : VisualElement
    {
        /// <summary>How much headroom above and below the range, as a share of it.</summary>
        public const float Margin = 0.18f;

        private readonly List<double> series;

        public PriceChart(List<double> series)
        {
            this.series = series ?? new List<double>();

            generateVisualContent += Draw;
        }

        /// <summary>The low and high the axis is drawn against, after the margin.</summary>
        public static void Bounds(IReadOnlyList<double> values, out double low, out double high)
        {
            low = double.MaxValue;
            high = double.MinValue;

            foreach (var value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    continue;
                }

                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }

            if (low > high)
            {
                low = 0.0;
                high = 1.0;
                return;
            }

            var span = high - low;

            // A perfectly flat series has no range to pad, and dividing by it would put the line
            // at infinity. Half a unit either side keeps it in the middle of the frame.
            if (span <= double.Epsilon)
            {
                low -= 0.5;
                high += 0.5;
                return;
            }

            low -= span * Margin;
            high += span * Margin;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (series.Count < 2)
            {
                return;
            }

            var width = contentRect.width;
            var height = contentRect.height;

            if (width <= 1f || height <= 1f)
            {
                return;
            }

            Bounds(series, out var low, out var high);

            var span = (float)(high - low);

            if (span <= 0f)
            {
                return;
            }

            var painter = context.painter2D;

            painter.strokeColor = new Color(0.36f, 0.78f, 0.95f, 1f);
            painter.lineWidth = 2.2f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            painter.BeginPath();

            for (var index = 0; index < series.Count; index++)
            {
                var x = width * index / (series.Count - 1f);
                var y = height * (1f - (float)((series[index] - low) / span));

                if (index == 0)
                {
                    painter.MoveTo(new Vector2(x, y));
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }

            painter.Stroke();

            // The last point again, as a dot, because the eye needs somewhere to land and the
            // right-hand end of a line is where "now" is.
            var lastX = width;
            var lastY = height * (1f - (float)((series[^1] - low) / span));

            painter.fillColor = new Color(0.62f, 0.90f, 1f, 1f);
            painter.BeginPath();
            painter.Arc(new Vector2(lastX - 3f, lastY), 3.4f, 0f, 360f);
            painter.Fill();
        }
    }
}
