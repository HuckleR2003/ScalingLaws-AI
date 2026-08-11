using System;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The tokens-per-parameter belt: three zones and a marker that slides as the sliders move.
    ///
    /// This is the one number in the whole creator that a player has to feel rather than read. The
    /// hint sentence said "around twenty tokens per parameter" and that is true and useless, because
    /// nothing on the screen told you whether you were at four or at eighty. Drawn with Painter2D for
    /// the same reason the clock dial is: USS has no gradients.
    ///
    /// The zone edges come from <see cref="TrainingProfile.BandOnBelt"/>, which reads them off
    /// <see cref="TrainingProjection"/>. There is no second copy of the band in this file.
    /// </summary>
    public sealed class ScaleBelt : VisualElement
    {
        private static readonly Color Starved = new(0.42f, 0.24f, 0.24f);
        private static readonly Color Efficient = new(0.20f, 0.44f, 0.32f);
        private static readonly Color Spill = new(0.38f, 0.34f, 0.20f);
        private static readonly Color Marker = new(0.96f, 0.94f, 0.90f);
        private static readonly Color Edge = new(0.06f, 0.07f, 0.09f);

        private double position = 0.5;
        private bool inBand = true;

        public ScaleBelt()
        {
            AddToClassList("belt");
            generateVisualContent += Draw;
        }

        public void Set(TrainingProfile profile)
        {
            position = profile.BandPosition;
            inBand = profile.Profile == ShapeProfile.Balanced;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 4f || rect.height <= 4f)
            {
                return;
            }

            var painter = context.painter2D;
            var (from, to) = TrainingProfile.BandOnBelt();

            var trackTop = rect.height * 0.28f;
            var trackHeight = rect.height * 0.44f;

            Block(painter, 0f, (float)from * rect.width, trackTop, trackHeight, Starved);
            Block(painter, (float)from * rect.width, (float)to * rect.width, trackTop, trackHeight,
                Efficient);
            Block(painter, (float)to * rect.width, rect.width, trackTop, trackHeight, Spill);

            // The marker runs the full height so it reads against all three zones at once.
            var x = Mathf.Clamp((float)position * rect.width, 1.5f, rect.width - 1.5f);

            painter.strokeColor = Edge;
            painter.lineWidth = 5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, 0f));
            painter.LineTo(new Vector2(x, rect.height));
            painter.Stroke();

            painter.strokeColor = inBand ? Efficient * 2.2f : Marker;
            painter.lineWidth = 2.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, 0f));
            painter.LineTo(new Vector2(x, rect.height));
            painter.Stroke();
        }

        private static void Block(Painter2D painter, float left, float right, float top, float height,
            Color colour)
        {
            if (right - left <= 0.5f)
            {
                return;
            }

            painter.fillColor = colour;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left, top));
            painter.LineTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right, top + height));
            painter.LineTo(new Vector2(left, top + height));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
