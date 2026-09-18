using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The drawing the model creator is laid out on: a faint grid, a few draughtsman's marks, the
    /// name of what each stage is deciding, and the author's own places written small in the margins.
    ///
    /// **It is held to this project's own art-direction rule rather than to what a blueprint looks
    /// like.** Anything under the interface here is evenly dark with no bright focal point and no
    /// text competing with the screen's own. A real blueprint is the opposite: white line work on
    /// saturated blue, covered in writing. So the grid is drawn at a few per cent of white and every
    /// word on it is dimmer than the dimmest caption on any panel. The test the rule states is the
    /// one to re-run after any change here: put white uppercase over the top corner and a price at
    /// the bottom, and read both.
    ///
    /// **The words brighten when the cursor comes near them.** Dim enough to ignore while reading a
    /// panel, and findable on purpose by anybody who moves the mouse across the empty half of the
    /// page. That is the whole reason they can be as faint as they are.
    ///
    /// **Drawn, not a texture.** Same answer as the world map, the clock dial and the torn name
    /// plate: nothing to license, nothing to keep in step with a palette, and it is sharp at any
    /// window size.
    /// </summary>
    public sealed class BlueprintGround : VisualElement
    {
        /// <summary>How close the cursor has to come before a word lifts, in pixels.</summary>
        public const float NearDistance = 50f;

        /// <summary>The major grid, in pixels. The minor one is an eighth of it.</summary>
        private const float MajorGrid = 96f;

        private const float MinorGrid = 16f;

        private static readonly Color MajorLine = new(0.47f, 0.78f, 1f, 0.055f);
        private static readonly Color MinorLine = new(0.47f, 0.78f, 1f, 0.028f);
        private static readonly Color MarkLine = new(0.59f, 0.78f, 1f, 0.16f);

        private readonly VisualElement marks = new();
        private readonly List<Label> words = new();

        public BlueprintGround()
        {
            AddToClassList("bpg");

            // It can never take a click. It sits under every panel on the page and an element that
            // ate pointer events there would be an invisible hole in the creator, which is a fault
            // this project has already shipped twice.
            pickingMode = PickingMode.Ignore;

            generateVisualContent += Draw;

            marks.AddToClassList("bpg__marks");
            marks.pickingMode = PickingMode.Ignore;
            Add(marks);
        }

        /// <summary>
        /// What this stage is deciding, and the small ones. Called whenever the creator changes page.
        ///
        /// The words are rebuilt rather than retexted, because a stage carries a different number of
        /// them, and they are few enough that rebuilding six labels costs nothing.
        /// </summary>
        public void Show(int stage)
        {
            marks.Clear();
            words.Clear();

            var plan = BlueprintNotes.For(stage);

            // **The callouts are keys and the small ones are not.** A sentence naming what a page
            // decides is read by whoever is playing, so it belongs in the phrase book like every
            // other sentence here. A city and a nickname are called what they are called in both
            // languages, and translating a studio name is how a credit stops being a credit.
            foreach (var callout in plan.Callouts)
            {
                Place(callout, "bpg__callout", translate: true);
            }

            foreach (var small in plan.Small)
            {
                Place(small, "bpg__small", translate: false);
            }

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Lifts whatever the cursor is near. Fed from the creator, which is where the pointer
        /// events land: this element ignores them by design.
        /// </summary>
        public void PointerAt(Vector2 position)
        {
            foreach (var word in words)
            {
                var box = word.worldBound;
                var near = box.Contains(position)
                           || DistanceTo(box, position) <= NearDistance;

                word.EnableInClassList("bpg__word--near", near);
            }
        }

        private void Place(BlueprintNotes.Note note, string className, bool translate)
        {
            var label = new Label(translate ? Loc.T(note.Text) : note.Text)
            {
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList("bpg__word");
            label.AddToClassList(className);

            label.style.position = Position.Absolute;

            if (note.FromRight)
            {
                label.style.right = note.X;
            }
            else
            {
                label.style.left = note.X;
            }

            if (note.FromBottom)
            {
                label.style.bottom = note.Y;
            }
            else
            {
                label.style.top = note.Y;
            }

            marks.Add(label);
            words.Add(label);
        }

        private static float DistanceTo(Rect box, Vector2 point)
        {
            var dx = Mathf.Max(box.xMin - point.x, 0f, point.x - box.xMax);
            var dy = Mathf.Max(box.yMin - point.y, 0f, point.y - box.yMax);

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Two grids and four corner marks. The minor grid is half the weight of the major one, which
        /// is what makes it read as a drawing rather than as a table.
        /// </summary>
        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width <= 1f || height <= 1f)
            {
                return;
            }

            var painter = context.painter2D;
            painter.lineWidth = 1f;

            Grid(painter, width, height, MinorGrid, MinorLine);
            Grid(painter, width, height, MajorGrid, MajorLine);

            // Corner ticks, the one flourish. A sheet with a border reads as a sheet.
            painter.strokeColor = MarkLine;
            painter.BeginPath();

            const float arm = 22f;
            const float inset = 10f;

            foreach (var (x, y, dx, dy) in new[]
                     {
                         (inset, inset, 1f, 1f),
                         (width - inset, inset, -1f, 1f),
                         (inset, height - inset, 1f, -1f),
                         (width - inset, height - inset, -1f, -1f)
                     })
            {
                painter.MoveTo(new Vector2(x, y + dy * arm));
                painter.LineTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x + dx * arm, y));
            }

            painter.Stroke();
        }

        private static void Grid(Painter2D painter, float width, float height, float step, Color colour)
        {
            painter.strokeColor = colour;
            painter.BeginPath();

            for (var x = step; x < width; x += step)
            {
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, height));
            }

            for (var y = step; y < height; y += step)
            {
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(width, y));
            }

            painter.Stroke();
        }
    }

    /// <summary>
    /// What is written on the drawing, per stage: two or three callouts naming the section, and a
    /// handful of the author's own places and names in the margins.
    ///
    /// **They are the author's, chosen by him, and they are deliberately few.** Three or four a
    /// stage over eight stages is thirty-odd places for something that is meant to be found rather
    /// than read, and a background that carries thirty at once is a scrapbook.
    ///
    /// Everything here is a literal and none of it is translated. A city is called what it is called
    /// in both languages, and a nickname is a nickname.
    /// </summary>
    public static class BlueprintNotes
    {
        public readonly struct Note
        {
            public Note(string text, float x, float y, bool fromRight = false, bool fromBottom = false)
            {
                Text = text;
                X = x;
                Y = y;
                FromRight = fromRight;
                FromBottom = fromBottom;
            }

            public string Text { get; }
            public float X { get; }
            public float Y { get; }
            public bool FromRight { get; }
            public bool FromBottom { get; }
        }

        public readonly struct Sheet
        {
            public Sheet(Note[] callouts, Note[] small)
            {
                Callouts = callouts;
                Small = small;
            }

            /// <summary>What this page is deciding. Two at most, in the margins.</summary>
            public Note[] Callouts { get; }

            /// <summary>The small ones. Never behind a panel: the gutters only.</summary>
            public Note[] Small { get; }
        }

        /// <summary>
        /// **Everything lives in the band along the bottom**, which is where a real drawing puts its
        /// title block and, more to the point, the only part of this page that is reliably empty.
        ///
        /// The first pass put the words down the left and right gutters and the render showed why
        /// that was wrong: the creator fills the window, the monitor took the left third, and every
        /// word was behind a panel. A background nobody can see is not subtle, it is absent.
        /// </summary>
        private static readonly Sheet[] Sheets =
        {
            // 0 BRANDING: the page where the player decides who they are, so it carries the names.
            new(new[]
                {
                    new Note("bp.sheet1", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet1.note", 240f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("HuckleR", 700f, 44f, fromBottom: true),
                    new Note("MarcinPF", 860f, 44f, fromBottom: true),
                    new Note("Kematex", 1030f, 44f, fromBottom: true),
                    new Note("OpenSource", 1190f, 44f, fromBottom: true)
                }),

            // 1 FOUNDATION
            new(new[]
                {
                    new Note("bp.sheet2", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet2.note", 260f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("Radom", 700f, 44f, fromBottom: true),
                    new Note("HuckHub", 830f, 44f, fromBottom: true),
                    new Note("build-in-public", 1000f, 44f, fromBottom: true)
                }),

            // 2 SCALE
            new(new[]
                {
                    new Note("bp.sheet3", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet3.note", 240f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("Krakow", 700f, 44f, fromBottom: true),
                    new Note("Zakopane", 830f, 44f, fromBottom: true),
                    new Note("Nowy Sacz", 990f, 44f, fromBottom: true),
                    new Note("never git --force", 1150f, 44f, fromBottom: true)
                }),

            // 3 DATA
            new(new[]
                {
                    new Note("bp.sheet4", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet4.note", 240f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("Venlo", 780f, 44f, fromBottom: true),
                    new Note("Raalte", 890f, 44f, fromBottom: true),
                    new Note("Kacia & Lusia <3", 1010f, 44f, fromBottom: true),
                    new Note("doggos", 1200f, 44f, fromBottom: true)
                }),

            // 4 COMPUTE
            new(new[]
                {
                    new Note("bp.sheet5", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet5.note", 240f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("Oss", 760f, 44f, fromBottom: true),
                    new Note("Almelo", 850f, 44f, fromBottom: true),
                    new Note("Schijndel", 970f, 44f, fromBottom: true),
                    new Note("Hertogenbosch", 1110f, 44f, fromBottom: true)
                }),

            // 5 SAFETY
            new(new[]
                {
                    new Note("bp.sheet6", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet6.note", 280f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("Anglia, przedszkole", 760f, 44f, fromBottom: true),
                    new Note("Limanowa", 970f, 44f, fromBottom: true)
                }),

            // 6 REVIEW
            new(new[]
                {
                    new Note("bp.sheet7", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet7.note", 250f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("Amsterdam", 700f, 44f, fromBottom: true),
                    new Note("Nowy Targ", 860f, 44f, fromBottom: true),
                    new Note("Kematex", 1010f, 44f, fromBottom: true)
                }),

            // 7 AFTER THE RUN
            new(new[]
                {
                    new Note("bp.sheet8", 26f, 44f, fromBottom: true),
                    new Note("bp.sheet8.note", 270f, 44f, fromBottom: true)
                },
                new[]
                {
                    new Note("HCK Labs", 720f, 44f, fromBottom: true),
                    new Note("PC Workman", 860f, 44f, fromBottom: true),
                    new Note("Baka Bake Bakery", 1020f, 44f, fromBottom: true)
                })
        };

        public static Sheet For(int stage) => Sheets[Math.Clamp(stage, 0, Sheets.Length - 1)];

        /// <summary>How many sheets there are. The creator has to have exactly this many stages.</summary>
        public static int Count => Sheets.Length;
    }
}
