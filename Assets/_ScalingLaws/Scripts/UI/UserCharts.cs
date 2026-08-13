using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A light line chart with dotted markers and an optional filled area, drawn to the reference the
    /// author supplied: pale grid, thin coloured line, a dot on every reading.
    ///
    /// One element serves both charts. Registered users is an area because it is a stock and the
    /// filled shape reads as accumulation; online users is a bare line because it is a rate and
    /// filling it would suggest a total.
    /// </summary>
    public sealed class LineChart : VisualElement
    {
        private static readonly Color Grid = new(0.89f, 0.89f, 0.90f);
        private static readonly Color Axis = new(0.72f, 0.72f, 0.74f);

        private double[] values = Array.Empty<double>();
        private Color line = new(0.29f, 0.68f, 0.90f);
        private bool filled;

        public LineChart()
        {
            AddToClassList("lchart");
            generateVisualContent += Draw;
        }

        public void Set(IReadOnlyList<double> series, Color colour, bool fill)
        {
            values = new double[series?.Count ?? 0];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = series[index];
            }

            line = colour;
            filled = fill;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 8f || rect.height <= 8f)
            {
                return;
            }

            var painter = context.painter2D;

            const float left = 4f;
            var right = rect.width - 4f;
            const float top = 6f;
            var bottom = rect.height - 6f;

            // Four horizontal rules, like the reference. They are the only grid: vertical lines at
            // this width turn the chart into graph paper.
            painter.strokeColor = Grid;
            painter.lineWidth = 1f;

            for (var step = 0; step <= 4; step++)
            {
                var y = top + (bottom - top) * step / 4f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, y));
                painter.LineTo(new Vector2(right, y));
                painter.Stroke();
            }

            if (values.Length < 2)
            {
                return;
            }

            // Scaled to the data's own range rather than to zero, which is what makes a flat period
            // followed by a jump look like the reference instead of like a flat line.
            var low = double.MaxValue;
            var high = double.MinValue;

            foreach (var value in values)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }

            var span = Math.Max(1.0, high - low);
            low -= span * 0.12;
            high += span * 0.12;
            span = high - low;

            var points = new Vector2[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                var x = left + (right - left) * index / (values.Length - 1f);
                var y = (float)(bottom - (values[index] - low) / span * (bottom - top));
                points[index] = new Vector2(x, Mathf.Clamp(y, top, bottom));
            }

            if (filled)
            {
                painter.fillColor = new Color(line.r, line.g, line.b, 0.28f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(points[0].x, bottom));

                foreach (var point in points)
                {
                    painter.LineTo(point);
                }

                painter.LineTo(new Vector2(points[^1].x, bottom));
                painter.ClosePath();
                painter.Fill();
            }

            painter.strokeColor = line;
            painter.lineWidth = 1.8f;
            painter.BeginPath();
            painter.MoveTo(points[0]);

            for (var index = 1; index < points.Length; index++)
            {
                painter.LineTo(points[index]);
            }

            painter.Stroke();

            // A dot on every reading, which is what makes the reference read as measurements rather
            // than as a trend line. Skipped when the points are too close to tell apart.
            if ((right - left) / values.Length >= 6f)
            {
                painter.fillColor = Color.white;
                painter.strokeColor = line;
                painter.lineWidth = 1.6f;

                foreach (var point in points)
                {
                    painter.BeginPath();
                    painter.Arc(point, 2.6f, 0f, 360f);
                    painter.Fill();

                    painter.BeginPath();
                    painter.Arc(point, 2.6f, 0f, 360f);
                    painter.Stroke();
                }
            }

            painter.strokeColor = Axis;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left, bottom));
            painter.LineTo(new Vector2(right, bottom));
            painter.Stroke();
        }
    }
}
