using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What a day of rented capacity costs, and how worried to be about it.
    ///
    /// **The playtest asked for this by name and the reason is in the ask.** The rent slider is the
    /// one control in the creator that can quietly end a campaign: it bills every day whether or not
    /// anything is training, and nothing on screen said whether the number under it was normal or
    /// ruinous. A figure with no scale behind it is not a warning.
    ///
    /// Four bands, and they are advice rather than rules — nothing here refuses anything. The white
    /// tick sits at the figure Emil actually recommends, so the player has a mark to aim at rather
    /// than a colour to interpret.
    ///
    /// Drawn with Painter2D because it is four zones and a tick, and USS has no gradients.
    /// </summary>
    public sealed class SpendMeter : VisualElement
    {
        /// <summary>What he tells you to stay under. The white line.</summary>
        public const double AdvisedUsdPerDay = 100_000;

        /// <summary>Where the bands start. Below the first one there is nothing to say.</summary>
        public const double WarnAbove = 150_000;
        public const double HeavyAbove = 300_000;
        public const double SevereAbove = 600_000;

        /// <summary>The right-hand end of the track. Past this the bar is simply full.</summary>
        private const double FullScale = 1_000_000;

        private static readonly Color Calm = new(0.42f, 0.70f, 0.55f);
        private static readonly Color Warn = new(0.89f, 0.75f, 0.27f);
        private static readonly Color Heavy = new(0.91f, 0.55f, 0.24f);
        private static readonly Color Severe = new(0.85f, 0.31f, 0.29f);
        private static readonly Color Ruin = new(0.48f, 0.29f, 0.68f);

        private double perDay;

        public SpendMeter()
        {
            AddToClassList("spend");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>The daily bill. Repaints only when the drawn bar would move.</summary>
        public double PerDayUsd
        {
            get => perDay;
            set
            {
                var safe = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;

                if (Math.Abs(safe - perDay) < 1.0)
                {
                    return;
                }

                perDay = safe;
                MarkDirtyRepaint();
            }
        }

        /// <summary>The colour this figure earns. Public so the readout beside it can match.</summary>
        public static Color ToneFor(double usdPerDay) =>
            usdPerDay > SevereAbove ? Ruin
            : usdPerDay > HeavyAbove ? Severe
            : usdPerDay > WarnAbove ? Heavy
            : usdPerDay > AdvisedUsdPerDay ? Warn
            : Calm;

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 8f || height < 3f)
            {
                return;
            }

            var painter = context.painter2D;

            // The empty track, so the bar reads as a proportion rather than as a floating block.
            painter.fillColor = new Color(1f, 1f, 1f, 0.06f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, 0f));
            painter.LineTo(new Vector2(width, 0f));
            painter.LineTo(new Vector2(width, height));
            painter.LineTo(new Vector2(0f, height));
            painter.ClosePath();
            painter.Fill();

            var filled = (float)Math.Clamp(perDay / FullScale, 0.0, 1.0) * width;

            if (filled > 1f)
            {
                painter.fillColor = ToneFor(perDay);
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, 0f));
                painter.LineTo(new Vector2(filled, 0f));
                painter.LineTo(new Vector2(filled, height));
                painter.LineTo(new Vector2(0f, height));
                painter.ClosePath();
                painter.Fill();
            }

            // The advice, as a line. Drawn over the fill so it stays visible once the bar passes it,
            // which is exactly when it matters.
            var advised = (float)(AdvisedUsdPerDay / FullScale) * width;

            painter.strokeColor = new Color(1f, 1f, 1f, 0.82f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(advised, -2f));
            painter.LineTo(new Vector2(advised, height + 2f));
            painter.Stroke();
        }
    }
}
