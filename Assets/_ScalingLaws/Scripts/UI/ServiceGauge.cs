using System;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The load dial: a thick arc that fills with utilisation, drawn to the author's reference.
    ///
    /// Painter2D because USS has no arcs, the same as the clock. The arc is a stroked path rather than
    /// a filled wedge, which is what gives it the flat ends and even thickness of the reference rather
    /// than the pie shape a filled arc produces.
    /// </summary>
    public sealed class ServiceGauge : VisualElement
    {
        private static readonly Color Track = new(0.90f, 0.90f, 0.91f);
        private static readonly Color Stable = new(0.24f, 0.72f, 0.20f);
        private static readonly Color Unstable = new(0.83f, 0.72f, 0.12f);
        private static readonly Color Critical = new(0.80f, 0.24f, 0.16f);

        private double utilisation;
        private ServiceStatus status = ServiceStatus.Stable;

        public ServiceGauge()
        {
            AddToClassList("gauge");
            generateVisualContent += Draw;
        }

        public static Color ColourFor(ServiceStatus value) => value switch
        {
            ServiceStatus.Critical => Critical,
            ServiceStatus.Unstable => Unstable,
            _ => Stable
        };

        public void Set(ServiceQuality quality)
        {
            utilisation = Math.Clamp(quality.Utilisation, 0.0, 1.0);
            status = quality.Status;
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

            // A half disc sitting on its flat edge, like the reference: the centre is at the bottom
            // and the arc sweeps the top half from left to right.
            var centre = new Vector2(rect.width / 2f, rect.height * 0.86f);
            var radius = Math.Min(rect.width / 2f, rect.height * 0.86f) - 4f;
            var thickness = Mathf.Max(6f, radius * 0.38f);

            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Butt;

            painter.strokeColor = Track;
            painter.BeginPath();
            painter.Arc(centre, radius - thickness / 2f, 180f, 360f);
            painter.Stroke();

            if (utilisation <= 0.0)
            {
                return;
            }

            painter.strokeColor = ColourFor(status);
            painter.BeginPath();
            painter.Arc(centre, radius - thickness / 2f, 180f, 180f + (float)(utilisation * 180.0));
            painter.Stroke();
        }
    }

    /// <summary>
    /// The three band scale beside the dial, with an arrow at the current band.
    ///
    /// It exists because a colour alone is not a reading. The dial says how full the cluster is; this
    /// says what that means, and the two together are what let a player act before the number is bad
    /// rather than after.
    /// </summary>
    public sealed class ServiceScale : VisualElement
    {
        private readonly Label critical = new("Critical");
        private readonly Label unstable = new("Unstable");
        private readonly Label stable = new("Stable");
        private readonly VisualElement bar = new();
        private readonly Label arrow = new("←");

        public ServiceScale()
        {
            AddToClassList("gauge-scale");

            var words = new VisualElement();
            words.AddToClassList("gauge-scale__words");

            critical.AddToClassList("gauge-scale__word");
            critical.AddToClassList("gauge-scale__word--critical");
            unstable.AddToClassList("gauge-scale__word");
            unstable.AddToClassList("gauge-scale__word--unstable");
            stable.AddToClassList("gauge-scale__word");
            stable.AddToClassList("gauge-scale__word--stable");

            words.Add(critical);
            words.Add(unstable);
            words.Add(stable);
            Add(words);

            bar.AddToClassList("gauge-scale__bar");
            Add(bar);

            arrow.AddToClassList("gauge-scale__arrow");
            Add(arrow);
        }

        public void Set(ServiceStatus status)
        {
            // The arrow sits against the band that is true now. Top is critical, bottom is stable,
            // which is the order on the reference and the order people read a severity scale in.
            var offset = status switch
            {
                ServiceStatus.Critical => 0f,
                ServiceStatus.Unstable => 33f,
                _ => 66f
            };

            arrow.style.top = Length.Percent(offset);

            critical.EnableInClassList("gauge-scale__word--on", status == ServiceStatus.Critical);
            unstable.EnableInClassList("gauge-scale__word--on", status == ServiceStatus.Unstable);
            stable.EnableInClassList("gauge-scale__word--on", status == ServiceStatus.Stable);
        }
    }
}
