using System;
using UnityEngine;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The two row shapes that more than one screen draws.
    ///
    /// They live here rather than being written twice because a bar that is four pixels tall on one
    /// screen and six on another reads as two different widgets, and nobody would ever notice the
    /// drift in review. The styles were already shared; only the construction was not.
    /// </summary>
    public static class UiParts
    {
        private static readonly Color DefaultFill = new(0.36f, 0.62f, 0.88f);

        /// <summary>A caption, a slim proportion bar, and the figure it represents.</summary>
        public static VisualElement ThinBarRow(string label, string value, double fraction,
            Color? fill = null)
        {
            var row = new VisualElement();
            row.AddToClassList("thin-bar");

            var caption = new Label(label);
            caption.AddToClassList("thin-bar__label");
            row.Add(caption);

            var track = new VisualElement();
            track.AddToClassList("thin-bar__track");

            var bar = new VisualElement();
            bar.AddToClassList("thin-bar__fill");

            var clamped = double.IsNaN(fraction) ? 0.0 : Math.Clamp(fraction, 0.0, 1.0);
            bar.style.width = Length.Percent((float)(clamped * 100.0));
            bar.style.backgroundColor = fill ?? DefaultFill;
            track.Add(bar);

            row.Add(track);

            var amount = new Label(value);
            amount.AddToClassList("thin-bar__value");
            row.Add(amount);

            return row;
        }

        /// <summary>A name on the left and a figure on the right, sharing one line.</summary>
        public static VisualElement StatLine(string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("rnow-stat");

            var label = new Label(name);
            label.AddToClassList("rnow-stat__name");
            row.Add(label);

            var figure = new Label(value);
            figure.AddToClassList("rnow-stat__value");
            row.Add(figure);

            return row;
        }
        /// <summary>
        /// Puts an "(i)" after a section heading, in a row, without the caller building the row.
        ///
        /// **The heading is already in the tree when this is called**, so the row is inserted where
        /// the heading was and the heading is moved into it. Doing it the other way round means
        /// every one of fourteen call sites has to remember to add the row instead of the label, and
        /// the one that forgets loses its explanation silently.
        ///
        /// Only for headings carrying a word a player was not born knowing. A badge on ON THE
        /// PAYROLL saying it lists the staff is noise, and noise is what teaches somebody to stop
        /// clicking the badges that matter.
        /// </summary>
        public static void ExplainHeading(Label heading, TechNotes.Note note)
        {
            if (heading == null)
            {
                return;
            }

            // **Deferred until the heading is actually in a panel, and that is the whole trick.**
            // Every call site builds the label, styles it, and adds it to its panel afterwards, so
            // at the moment this is called the heading has no parent and there is nowhere to put the
            // row. The first version read `heading.parent`, found null, returned quietly, and
            // fourteen screens went on rendering exactly as they had before with nothing to say
            // anything was missing. Only counting the badges found it.
            heading.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var parent = heading.parent;

                if (parent == null || parent.ClassListContains("headrow"))
                {
                    return;
                }

                var at = parent.IndexOf(heading);

                var row = new VisualElement();
                row.AddToClassList("headrow");

                heading.RemoveFromHierarchy();
                row.Add(heading);
                row.Add(InsightTip.InfoBadge(note.Title,
                    new InsightTip.Reading(note.What, note.Affects, note.High, note.Low)));

                parent.Insert(at, row);
            });
        }
        /// <summary>
        /// A row of the words this screen uses, each with its own "(i)", directly under the strap.
        ///
        /// **For screens built out of cards rather than sections.** RANKING and RELEASE have no
        /// headings to hang an explanation on: they are a list of rows and a list of tiles. Rather
        /// than invent a section title so a badge has somewhere to live, the terms themselves are
        /// the row, which is also the honest shape: these are the words on this page that a player
        /// may not know, and here they are.
        /// </summary>
        /// <summary>
        /// The terms and the page's own sentence on one line.
        ///
        /// The strap under a page title is usually a paragraph, and a paragraph plus a row of
        /// defined terms is two bands of small grey text saying related things. On the board where
        /// the terms *are* the sentence, they belong on one line and the strap has to be short
        /// enough to sit on it.
        /// </summary>
        public static void ExplainInline(VisualElement page, string strap,
            params TechNotes.Note[] notes)
        {
            if (page == null || notes == null || notes.Length == 0)
            {
                return;
            }

            var row = TermsRow(notes);
            row.AddToClassList("terms--inline");

            if (!string.IsNullOrEmpty(strap))
            {
                var sentence = new Label(strap);
                sentence.AddToClassList("terms__strap");
                row.Add(sentence);
            }

            page.Insert(System.Math.Min(2, page.childCount), row);
        }

        private static VisualElement TermsRow(TechNotes.Note[] notes)
        {
            var row = new VisualElement();
            row.AddToClassList("terms");

            foreach (var note in notes)
            {
                var chip = new VisualElement();
                chip.AddToClassList("terms__chip");

                var word = new Label(note.Title);
                word.AddToClassList("terms__word");

                chip.Add(word);
                chip.Add(InsightTip.InfoBadge(note.Title,
                    new InsightTip.Reading(note.What, note.Affects, note.High, note.Low),
                    InsightTip.Placement.Above));

                row.Add(chip);
            }

            return row;
        }

        public static void ExplainPage(VisualElement page, params TechNotes.Note[] notes)
        {
            if (page == null || notes == null || notes.Length == 0)
            {
                return;
            }

            var row = new VisualElement();
            row.AddToClassList("terms");

            foreach (var note in notes)
            {
                var chip = new VisualElement();
                chip.AddToClassList("terms__chip");

                var word = new Label(note.Title);
                word.AddToClassList("terms__word");

                chip.Add(word);
                chip.Add(InsightTip.InfoBadge(note.Title,
                    new InsightTip.Reading(note.What, note.Affects, note.High, note.Low),
                    InsightTip.Placement.Above));

                row.Add(chip);
            }

            // Third, so it lands under the title and the strap and above the banner photograph.
            page.Insert(System.Math.Min(2, page.childCount), row);
        }
    }
}
