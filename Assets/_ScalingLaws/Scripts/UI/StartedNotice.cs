using System;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A short announcement across the top of the screen that something long has just begun.
    ///
    /// **Written because money left the company and nothing said so.** Commissioning post-training
    /// work charges immediately, publishes the new version immediately, and then moves the player
    /// to the official page, where the progress banner does not live: it only draws in the office.
    /// A playtest reported it as the upgrade completing instantly, which is exactly what it looks
    /// like from that seat.
    ///
    /// It leaves on its own. A notice that has to be dismissed is a second click for something the
    /// player did not ask about, and one that stays is a banner they stop reading.
    /// </summary>
    public sealed class StartedNotice
    {
        /// <summary>How long it stays up, in milliseconds.</summary>
        public const int HoldMilliseconds = 3000;

        /// <summary>How long the bar takes to run. Short of the hold, so it finishes on screen.</summary>
        public const int SweepMilliseconds = 2200;

        private readonly VisualElement host;

        private VisualElement frame;

        public StartedNotice(VisualElement host) => this.host = host;

        public bool IsShowing => frame != null && frame.parent != null;

        /// <summary>
        /// Puts the notice up. Any notice already showing is replaced rather than queued.
        ///
        /// Replaced, because two of these would stack down the screen and the second one is always
        /// the one the player is waiting to read.
        /// </summary>
        public void Show(string headline, string note = null)
        {
            Hide();

            frame = new VisualElement();
            frame.AddToClassList("started");
            frame.pickingMode = PickingMode.Ignore;

            var title = new Label(headline);
            title.AddToClassList("started__title");
            frame.Add(title);

            if (!string.IsNullOrEmpty(note))
            {
                var sub = new Label(note);
                sub.AddToClassList("started__note");
                frame.Add(sub);
            }

            var track = new VisualElement();
            track.AddToClassList("started__track");

            var fill = new VisualElement();
            fill.AddToClassList("started__fill");
            track.Add(fill);

            frame.Add(track);
            host.Add(frame);

            // Born narrow and released a frame later, so the width transition has somewhere to run
            // from. Same trick every arriving element in this project uses: USS has no keyframes,
            // so a transition between two states is the whole animation vocabulary.
            frame.schedule.Execute(() =>
            {
                fill.AddToClassList("started__fill--run");
                frame.AddToClassList("started--in");
            }).ExecuteLater(16);

            frame.schedule.Execute(Hide).ExecuteLater(HoldMilliseconds);
        }

        public void Hide()
        {
            frame?.RemoveFromHierarchy();
            frame = null;
        }
    }
}
