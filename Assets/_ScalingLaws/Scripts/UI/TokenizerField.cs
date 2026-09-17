using ScalingLaws.Data;
using ScalingLaws.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The field of squares under the tokenizer picker: the corpus, packed.
    ///
    /// **A texture, not seven hundred elements.** The field is 48 by 16 cells and it animates, so a
    /// grid of `VisualElement`s would be 768 boxes laid out and repainted on a page that is already
    /// the busiest in the game. One point-filtered texture is a few thousand pixels written per
    /// frame and nothing for the layout to do.
    ///
    /// **Every cell's character comes from its index, never from a dice.** Same rule
    /// `GuideNamePlate` learned: anything derived from randomness at paint time reshuffles on every
    /// repaint, and a field that reshuffles reads as broken rather than as alive.
    ///
    /// **It honours the reduce-motion setting.** That is an accessibility setting in this project
    /// rather than a performance one, so with it on the field snaps to its new state instead of
    /// travelling to it.
    /// </summary>
    public sealed class TokenizerField : VisualElement
    {
        /// <summary>Cells across and down. The author's reference picture, near enough.</summary>
        public const int Columns = 96;

        public const int Rows = 8;

        /// <summary>Pixels a cell takes, of which the last is the gap between cells.</summary>
        private const int CellPixels = 3;

        /// <summary>How long the whole field takes to arrive at a new state, in seconds.</summary>
        public const float TravelSeconds = 2.6f;

        /// <summary>Milliseconds between repaints. Thirty a second is plenty for a field of squares.</summary>
        private const long TickMilliseconds = 33;

        private static readonly Color Background = new(0.039f, 0.047f, 0.063f);

        private readonly Texture2D texture;
        private readonly Color32[] pixels;
        private readonly Color[] current;
        private readonly float[] rank;
        private readonly float[] delay;

        private TokenizerKind kind = TokenizerKind.OffTheShelf;
        private int adaptation;
        private float sinceChange = TravelSeconds;
        private float clock;

        public TokenizerField()
        {
            AddToClassList("tok-field");

            texture = new Texture2D(Columns * CellPixels, Rows * CellPixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            pixels = new Color32[texture.width * texture.height];
            current = new Color[Columns * Rows];
            rank = new float[Columns * Rows];
            delay = new float[Columns * Rows];

            for (var index = 0; index < rank.Length; index++)
            {
                // Two hashes of the same index: one decides what kind of cell this is, the other
                // when it turns. Deterministic, so the field is the same field every repaint.
                rank[index] = Fraction(index * 12.9898f);

                var column = index % Columns;
                var row = index / Columns;
                delay[index] = (column / (float)Columns) * 1.15f
                               + (row / (float)Rows) * 0.55f
                               + Fraction(index * 78.233f) * 0.18f;

                current[index] = Background;
            }

            Paint(instant: true);

            schedule.Execute(() =>
            {
                clock += TickMilliseconds / 1000f;
                sinceChange += TickMilliseconds / 1000f;
                Paint(instant: false);
            }).Every(TickMilliseconds);
        }

        /// <summary>
        /// Shows a rung and how far the corpus has been adapted to it. The field travels there
        /// rather than cutting, which is the whole point of it.
        /// </summary>
        public void Show(TokenizerKind rung, int adaptationLevel)
        {
            var settled = rung == kind && adaptationLevel == adaptation;

            kind = rung;
            adaptation = Mathf.Clamp(adaptationLevel, 0, TokenizerCatalog.AdaptationLevels - 1);

            if (!settled)
            {
                sinceChange = 0f;
            }

            Paint(GameSettings.ReduceMotion || settled);
        }

        /// <summary>
        /// The colour a cell wants to be, from what it is and what the clock is doing.
        ///
        /// The top rung cycles and the third blinks, which is the author's brief. Both are read off
        /// the cell's own index so no two cells are ever in step, and neither is a dice.
        /// </summary>
        private Color Wanted(int index)
        {
            // How far the money has pulled this field towards the rung above: half a rung at the
            // top of the bar, which is the rule the ladder itself is built on.
            var share = adaptation / (float)(TokenizerCatalog.AdaptationLevels - 1);
            var reach = share * 0.5f;

            var tier = (int)kind;
            var promoted = tier < 3 && rank[index] < reach;
            var shown = promoted ? tier + 1 : tier;
            var phase = clock + Fraction(index * 37.719f) * 6.2f;

            switch (shown)
            {
                case 0:
                    // Nothing chosen: graphite, with a few lit cells along the bottom to say the
                    // field is a field rather than a dead panel.
                    if (rank[index] > 0.93f && index >= (Rows - 1) * Columns)
                    {
                        return new Color(0.91f, 0.93f, 0.96f);
                    }

                    return rank[index] > 0.72f
                        ? new Color(0.184f, 0.204f, 0.235f)
                        : new Color(0.133f, 0.149f, 0.173f);

                case 1:
                    if (rank[index] > 0.78f)
                    {
                        return new Color(0.561f, 0.890f, 0.671f);
                    }

                    if (rank[index] > 0.30f)
                    {
                        return new Color(0.247f, 0.663f, 0.420f);
                    }

                    // The dark cells take on the violet of the rung above as the money goes in, and
                    // at the top of the bar most of them do. Asked for by name.
                    var pull = rank[index] / 0.30f < share * 1.1f
                        ? 0.5f + 0.5f * Mathf.Sin(phase * 2.2f)
                        : 0f;

                    return Color.Lerp(new Color(0.145f, 0.286f, 0.227f),
                        new Color(0.478f, 0.306f, 0.761f), pull);

                case 2:
                    if (rank[index] > 0.965f)
                    {
                        // A handful blink white, off the same clock, never together.
                        var blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * 1.7f)), 12f);
                        return Color.Lerp(new Color(0.561f, 0.890f, 0.671f), Color.white, blink);
                    }

                    if (rank[index] > 0.62f)
                    {
                        return new Color(0.627f, 0.424f, 0.878f);
                    }

                    return rank[index] > 0.28f
                        ? new Color(0.349f, 0.788f, 0.541f)
                        : new Color(0.227f, 0.184f, 0.345f);

                default:
                    // The top rung: every cell walking the same wheel of colour at its own pace.
                    return Wheel(phase * 0.28f + rank[index] * 0.6f);
            }
        }

        /// <summary>Four stops around a wheel, which is enough to read as colour moving.</summary>
        private static Color Wheel(float at)
        {
            var stops = new[]
            {
                new Color(0.878f, 0.333f, 0.420f),
                new Color(0.310f, 0.690f, 0.910f),
                new Color(0.349f, 0.788f, 0.541f),
                new Color(0.690f, 0.424f, 0.878f)
            };

            var position = Fraction(at) * stops.Length;
            var first = Mathf.FloorToInt(position);

            return Color.Lerp(stops[first % stops.Length], stops[(first + 1) % stops.Length],
                position - first);
        }

        private void Paint(bool instant)
        {
            for (var index = 0; index < current.Length; index++)
            {
                var wanted = Wanted(index);

                if (instant)
                {
                    current[index] = wanted;
                }
                else
                {
                    // Nothing moves until this cell's own moment has come, which is what makes the
                    // change sweep across the field instead of happening to all of it at once.
                    var started = sinceChange - delay[index] * TravelSeconds * 0.55f;
                    if (started <= 0f)
                    {
                        continue;
                    }

                    // An exponential approach rather than a straight line, so a cell that has just
                    // turned moves quickly and then settles. Framed on the tick rather than on the
                    // frame, because this is repainted on its own clock.
                    const float perSecond = 5.5f;
                    var step = 1f - Mathf.Exp(-perSecond * (TickMilliseconds / 1000f));

                    current[index] = Color.Lerp(current[index], wanted, step);
                }

                Write(index, current[index]);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);

            style.backgroundImage = new StyleBackground(texture);
        }

        /// <summary>One cell into the pixel buffer, with its gap on the right and underneath.</summary>
        private void Write(int index, Color colour)
        {
            var column = index % Columns;
            var row = index / Columns;

            // The texture's origin is the bottom left and the field reads top down, so the row is
            // flipped here rather than everywhere a cell is decided.
            var originX = column * CellPixels;
            var originY = (Rows - 1 - row) * CellPixels;

            var lit = (Color32)colour;
            var gap = (Color32)Background;

            for (var y = 0; y < CellPixels; y++)
            {
                for (var x = 0; x < CellPixels; x++)
                {
                    var last = x == CellPixels - 1 || y == 0;
                    pixels[(originY + y) * texture.width + originX + x] = last ? gap : lit;
                }
            }
        }

        private static float Fraction(float of)
        {
            var value = Mathf.Sin(of) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
