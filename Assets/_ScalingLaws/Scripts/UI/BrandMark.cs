using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The company's mark, drawn from its name.
    ///
    /// **There is no logo file for the player's company and there cannot be one.** Every rival has a
    /// drawn mark in `Resources/Labs`, because there are thirteen of them and they are authored. The
    /// player types their company name at the creator and the game has to have something to put on a
    /// browser tab, a chip and a product page four seconds later.
    ///
    /// So the mark is derived: a ring, a cut, and the initial, with the hue taken from a hash of the
    /// name. Two things follow from that and both matter. The same company always gets the same mark
    /// in the same colour, on every screen and after a reload, because nothing is stored and nothing
    /// is random. And a company called anything at all has a mark, including one called "x".
    ///
    /// Drawn rather than styled because it is a ring with a gap in it, and USS has no arcs. Same
    /// reason <see cref="HudTimeDial"/> is drawn.
    ///
    /// **The arrows beside it in the creator are deliberately dead.** Choosing a mark is a feature
    /// this does not have yet; showing where it will live is the difference between a decision the
    /// player knows is coming and one they never find out about.
    /// </summary>
    public sealed class BrandMark : VisualElement
    {
        /// <summary>The gap in the ring, in degrees. A closed ring reads as a loading spinner.</summary>
        private const float Gap = 62f;

        private const int Segments = 40;

        private string company = string.Empty;
        private Label initial;

        public BrandMark()
        {
            AddToClassList("mark");
            pickingMode = PickingMode.Ignore;

            initial = new Label();
            initial.AddToClassList("mark__letter");
            initial.pickingMode = PickingMode.Ignore;
            Add(initial);

            generateVisualContent += Draw;
        }

        /// <summary>The company this mark belongs to. Setting it repaints and re-letters.</summary>
        public string Company
        {
            get => company;
            set
            {
                var trimmed = (value ?? string.Empty).Trim();

                if (trimmed == company)
                {
                    return;
                }

                company = trimmed;
                initial.text = InitialOf(company);
                initial.style.color = Ink(company);

                MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// The letter on the mark.
        ///
        /// The first letter of the first word that has one, so "  4chan" reads as C rather than as a
        /// space, and a name made entirely of punctuation still gets something rather than an empty
        /// ring that looks like a failed load.
        /// </summary>
        public static string InitialOf(string name)
        {
            foreach (var character in name ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return char.ToUpperInvariant(character).ToString();
                }
            }

            return "?";
        }

        /// <summary>
        /// The mark's colour, from the name.
        ///
        /// A fixed hash rather than <c>string.GetHashCode</c>, which is randomised per process on
        /// modern runtimes: the same company would come out a different colour every launch, which
        /// is exactly the kind of fault nobody reports and everybody notices.
        /// </summary>
        public static Color Ink(string name)
        {
            unchecked
            {
                var hash = 2166136261u;

                foreach (var character in name ?? string.Empty)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                // Kept off the reds, which the interface reserves for penalties and alarms, and off
                // full saturation, which no brand mark in this palette uses.
                var hue = (hash % 1000u) / 1000f * 0.62f + 0.14f;
                return Color.HSVToRGB(hue, 0.46f, 0.92f);
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 4f || height < 4f)
            {
                return;
            }

            var painter = context.painter2D;
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            var radius = Mathf.Min(width, height) * 0.42f;
            var ink = Ink(company);

            // The ring, with a gap. Stroked as segments rather than as one arc so the ends can be
            // faded, which is what stops it reading as a progress indicator that is stuck.
            painter.lineWidth = Mathf.Max(2f, radius * 0.17f);
            painter.lineCap = LineCap.Round;

            var sweep = 360f - Gap;

            for (var index = 0; index < Segments; index++)
            {
                var from = Gap * 0.5f + sweep * index / Segments;
                var to = Gap * 0.5f + sweep * (index + 1) / Segments;

                // Faded at both ends of the sweep, solid through the middle.
                var along = (index + 0.5f) / Segments;
                var strength = Mathf.SmoothStep(0.25f, 1f, 1f - Mathf.Abs(along - 0.5f) * 2f);

                painter.strokeColor = new Color(ink.r, ink.g, ink.b, strength);
                painter.BeginPath();
                painter.Arc(centre, radius, from, to);
                painter.Stroke();
            }

            // One short stroke across the gap, which is what turns a broken ring into a mark.
            painter.lineWidth = Mathf.Max(2f, radius * 0.13f);
            painter.strokeColor = new Color(ink.r, ink.g, ink.b, 0.85f);
            painter.BeginPath();
            painter.MoveTo(centre + Angle(-Gap * 0.42f) * (radius * 1.16f));
            painter.LineTo(centre + Angle(Gap * 0.42f) * (radius * 0.52f));
            painter.Stroke();
        }

        private static Vector2 Angle(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
