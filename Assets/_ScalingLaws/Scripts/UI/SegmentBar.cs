using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A bar made of slanted segments that fill from the left.
    ///
    /// **The one piece of this interface the author liked enough to ask for it twice.** It was
    /// drawn once for the boot screen and nowhere else, and a shape that only exists for the three
    /// seconds before a game starts is a shape nobody sees. It is a component now and the two
    /// things a player waits weeks for, a training run and a research node, wear it.
    ///
    /// Painter2D because UI Toolkit has no skew. The project already knows that and works around it
    /// in one place with a square turned forty five degrees and clipped; that trick draws one
    /// diagonal cut and cannot draw a row of parallelograms, so these are four points each.
    ///
    /// Segments rather than a continuous fill, because a continuous fill at four per cent is a
    /// sliver nobody can see and a row of segments at four per cent is one lit block. The reading
    /// is coarse on purpose: this says roughly how far along, and the number beside it says exactly.
    /// </summary>
    public sealed class SegmentBar : VisualElement
    {
        /// <summary>Width of one segment along the bottom edge.</summary>
        public const float SegmentWidth = 30f;

        /// <summary>Gap between two segments, measured along the bottom edge.</summary>
        public const float Gap = 5f;

        /// <summary>How far the top edge of a segment leads its bottom edge.</summary>
        public const float Slant = 9f;

        private static readonly Color Unlit = new(0.11f, 0.15f, 0.21f);

        private int segments;
        private double filled;
        private Color accent = new(0.95f, 0.45f, 0.13f);

        public SegmentBar()
        {
            AddToClassList("segbar");
            generateVisualContent += Draw;
        }

        /// <summary>
        /// How much of the bar is lit, 0 to 1.
        ///
        /// Readable because a bar is a claim and a test has to be able to check it. The creator
        /// drew this full and green on a company with no model, in a field where nobody had
        /// released anything, and nothing could catch that until the fill could be read back.
        /// Same seam as `OpsPanel.BanIsArmed` and `ManagementScreen.ShowDesk`.
        /// </summary>
        public double Fill => filled;

        /// <summary>How much of the bar is lit, 0 to 1, and in what colour.</summary>
        public void Set(double fraction, Color colour)
        {
            filled = double.IsNaN(fraction) ? 0.0 : Math.Clamp(fraction, 0.0, 1.0);
            accent = colour;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// How many segments to draw, or zero to fit as many as the width takes.
        ///
        /// **A bar standing for a ladder has to draw one block per rung.** Fitted by width it
        /// drew twenty five blocks for a ladder of six, so a player counting the lit ones got
        /// a number that was not their level. Found by rendering it and looking: the bar was
        /// correct as a fraction and wrong as a count, and only one of those is what a ladder
        /// means. Zero is still right for the boot screen, where it stands for a fraction.
        /// </summary>
        public int Segments
        {
            get => segments;
            set
            {
                segments = Math.Max(0, value);
                MarkDirtyRepaint();
            }
        }

        /// <summary>How much of the bar is lit, in the default colour.</summary>
        public void Set(double fraction) => Set(fraction, accent);

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width <= 0f || height <= 0f)
            {
                return;
            }

            // The slant leans off the right-hand end, so the row is laid out inside a width that
            // already has room for it. Without this the last segment is clipped at the tip and
            // reads as a different shape from the rest.
            var usable = Mathf.Max(0f, width - Slant);
            // A stated count divides the row; otherwise the row takes as many as it fits.
            var count = segments > 0
                ? segments
                : Mathf.Max(1, Mathf.FloorToInt((usable + Gap) / (SegmentWidth + Gap)));

            var step = (usable + Gap) / count;
            var blockWidth = Mathf.Max(2f, step - Gap);

            var painter = context.painter2D;

            for (var index = 0; index < count; index++)
            {
                var left = index * step;

                // A segment counts as lit once the fill has passed its middle, so a bar at exactly
                // half reads as half rather than as one block more or less depending on rounding.
                var middle = (left + blockWidth * 0.5f) / Mathf.Max(1f, usable);
                var lit = middle <= filled;

                painter.fillColor = lit ? accent : Unlit;
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, height));
                painter.LineTo(new Vector2(left + blockWidth, height));
                painter.LineTo(new Vector2(left + blockWidth + Slant, 0f));
                painter.LineTo(new Vector2(left + Slant, 0f));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
