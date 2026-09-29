using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// How the desk is doing, as a face.
    ///
    /// **A percentage is a fact and a face is a verdict**, and the satisfaction figure needs both.
    /// A player reading "72%" beside no scale cannot tell whether that is a good desk having a bad
    /// week or a company about to lose its audience; a mouth that has turned down says it before
    /// the number is read, which is the whole reason a status page in any product of this kind puts
    /// one there.
    ///
    /// Drawn rather than styled, and not an emoji: USS has no arcs, and a font glyph would be a
    /// different shape on every machine and would carry whatever colour the fallback face decided.
    /// Same reason the dial, the brand mark and the world map are drawn here.
    ///
    /// **Nothing about it is animated and nothing about it is random.** The curve is a pure
    /// function of the quality handed in, so the same desk draws the same face in a screenshot
    /// taken a week apart, and a panel rebuilt every day does not make the face twitch.
    /// </summary>
    public sealed class SupportFace : VisualElement
    {
        /// <summary>
        /// Where the mouth stops being a smile and starts being a line.
        ///
        /// Deliberately high. The desk answers everything up to two days and the quality reading
        /// only starts falling past that, so a face that stayed cheerful into the middle of the
        /// range would be reassuring a player whose audience is already leaving.
        /// </summary>
        private const float Content = 0.85f;

        /// <summary>Where it stops being a line and starts being a frown.</summary>
        private const float Unhappy = 0.55f;

        private readonly float quality;

        public SupportFace(double quality)
        {
            this.quality = Mathf.Clamp01((float)quality);

            AddToClassList("sup__face");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>
        /// The three tones, so the colour and the mouth cannot tell different stories.
        ///
        /// Written out rather than interpolated: a face halfway between green and red is an
        /// ambiguous face, and the bands are where the mouth changes anyway.
        /// </summary>
        public Color Tone => ToneFor(quality);

        /// <summary>
        /// The same three tones, for anything else that draws the desk's satisfaction.
        ///
        /// **One reading, because two would drift.** The official page prints the figure with a
        /// bar behind it and this draws a face, and a bar that is amber beside a face that is
        /// still smiling is the disagreement with a date on it. This project has had four copies
        /// of one set of heat thresholds saying different things about the same cabinet.
        /// </summary>
        public static Color ToneFor(double quality)
        {
            var clamped = Mathf.Clamp01((float)quality);

            return clamped >= Content
                ? UiParts.Good
                : clamped >= Unhappy
                    ? new Color(0.84f, 0.64f, 0.26f)
                    : UiParts.Bad;
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width < 10f || height < 10f)
            {
                return;
            }

            var painter = context.painter2D;
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            var radius = Mathf.Min(width, height) * 0.5f - 1.5f;
            var tone = Tone;

            painter.fillColor = new Color(tone.r, tone.g, tone.b, 0.18f);
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Fill();

            painter.strokeColor = tone;
            painter.lineWidth = Mathf.Max(1.6f, radius * 0.10f);
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Stroke();

            var eye = radius * 0.13f;
            var eyeUp = centre.y - radius * 0.28f;

            painter.fillColor = tone;

            foreach (var side in new[] { -1f, 1f })
            {
                painter.BeginPath();
                painter.Arc(new Vector2(centre.x + side * radius * 0.34f, eyeUp), eye, 0f, 360f);
                painter.Fill();
            }

            DrawMouth(painter, centre, radius, tone);
        }

        /// <summary>
        /// The mouth, as one quadratic through three points.
        ///
        /// **The middle point is the only thing that moves**, from below the corners at a happy
        /// desk to above them at an abandoned one, passing through a flat line on the way. That is
        /// one number rather than three shapes, so there is no band where the face is drawn by a
        /// different piece of code and no chance of a mouth that does not meet its own corners.
        /// </summary>
        private void DrawMouth(Painter2D painter, Vector2 centre, float radius, Color tone)
        {
            var mouth = centre.y + radius * 0.22f;
            var reach = radius * 0.42f;

            // +1 at the worst, -1 at the best. Screen space runs downwards, so a negative bend
            // lifts the middle of the curve and draws a smile.
            var bend = Mathf.Lerp(1f, -1f, Mathf.InverseLerp(0f, 1f, quality));

            var left = new Vector2(centre.x - reach, mouth);
            var right = new Vector2(centre.x + reach, mouth);
            var control = new Vector2(centre.x, mouth + bend * radius * 0.52f);

            painter.strokeColor = tone;
            painter.lineWidth = Mathf.Max(1.6f, radius * 0.11f);
            painter.lineCap = LineCap.Round;

            painter.BeginPath();
            painter.MoveTo(left);
            painter.QuadraticCurveTo(control, right);
            painter.Stroke();
        }
    }
}
