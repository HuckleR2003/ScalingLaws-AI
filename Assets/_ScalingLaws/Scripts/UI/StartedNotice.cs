using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// How loud a notice is.
    ///
    /// **Three, and the loud two are rationed.** The author asked for a gold one kept for occasions
    /// and a red one for things that happen to the company rather than by it. A notice system where
    /// everything is gold is a notice system where nothing is.
    /// </summary>
    public enum NoticeTone
    {
        /// <summary>Something the player did has begun or landed. Blue, three seconds.</summary>
        Standard = 0,

        /// <summary>An occasion: somebody joins, a campaign is booked, a grant level opens. Gold.</summary>
        Special = 1,

        /// <summary>
        /// Something happened to the company. Red, flashing white, two and a half times as long,
        /// and carrying the one button that takes the player to it.
        /// </summary>
        Alert = 2
    }

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

        /// <summary>An alert stays two and a half times as long, which is what was asked for.</summary>
        public const int AlertHoldMilliseconds = HoldMilliseconds * 5 / 2;

        /// <summary>And its bar runs for the same share of the longer hold.</summary>
        public const int AlertSweepMilliseconds = SweepMilliseconds * 5 / 2;

        /// <summary>How often an alert swaps between red and white.</summary>
        public const int FlashMilliseconds = 420;

        /// <summary>
        /// The gold ground, baked once.
        ///
        /// USS has no gradients, so a gradient is a texture stretched over the element, the same
        /// trick the bottom bar's accent line uses. A flat gold slab reads as a warning label, and
        /// this one is meant to read as an occasion.
        /// </summary>
        private static Texture2D specialGround;

        private readonly VisualElement host;

        private VisualElement frame;
        private Action pendingAction;

        public StartedNotice(VisualElement host) => this.host = host;

        public bool IsShowing => frame != null && frame.parent != null;

        /// <summary>The notice on screen, or null. For tests, which have no eyes.</summary>
        public VisualElement Frame => frame;

        /// <summary>
        /// Puts the notice up. Any notice already showing is replaced rather than queued.
        ///
        /// Replaced, because two of these would stack down the screen and the second one is always
        /// the one the player is waiting to read.
        /// </summary>
        public void Show(string headline, string note = null, NoticeTone tone = NoticeTone.Standard,
            string actionText = null, Action action = null)
        {
            Hide();

            var shown = new VisualElement();
            shown.AddToClassList("started");
            shown.EnableInClassList("started--special", tone == NoticeTone.Special);
            shown.EnableInClassList("started--alert", tone == NoticeTone.Alert);
            shown.pickingMode = PickingMode.Ignore;

            if (tone == NoticeTone.Special)
            {
                shown.style.backgroundImage = new StyleBackground(SpecialGround());
            }

            var title = new Label(headline);
            title.AddToClassList("started__title");
            title.pickingMode = PickingMode.Ignore;
            shown.Add(title);

            if (!string.IsNullOrEmpty(note))
            {
                var sub = new Label(note);
                sub.AddToClassList("started__note");
                sub.pickingMode = PickingMode.Ignore;
                shown.Add(sub);
            }

            var track = new VisualElement();
            track.AddToClassList("started__track");
            track.pickingMode = PickingMode.Ignore;

            var fill = new VisualElement();
            fill.AddToClassList("started__fill");
            fill.pickingMode = PickingMode.Ignore;
            track.Add(fill);

            shown.Add(track);

            // **The one control a notice may carry.** Everything else on it ignores the pointer, so
            // SAVE and MENU under it keep working; this button is the exception because taking the
            // player to what happened is the whole point of an alert.
            if (!string.IsNullOrEmpty(actionText) && action != null)
            {
                pendingAction = action;

                var go = new Button(RunAction) { text = actionText };
                go.AddToClassList("started__action");
                shown.Add(go);
            }

            host.Add(shown);
            frame = shown;

            var hold = HoldMilliseconds;

            if (tone == NoticeTone.Alert)
            {
                hold = AlertHoldMilliseconds;

                var sweep = new TimeValue(AlertSweepMilliseconds, TimeUnit.Millisecond);
                fill.style.transitionDuration = new List<TimeValue> { sweep, sweep };

                shown.schedule.Execute(() =>
                {
                    if (frame == shown)
                    {
                        shown.ToggleInClassList("started--flash");
                    }
                }).Every(FlashMilliseconds);
            }

            // Born narrow and released a frame later, so the width transition has somewhere to run
            // from. Same trick every arriving element in this project uses: USS has no keyframes,
            // so a transition between two states is the whole animation vocabulary.
            shown.schedule.Execute(() =>
            {
                fill.AddToClassList("started__fill--run");
                shown.AddToClassList("started--in");
            }).ExecuteLater(16);

            // Only takes down the notice it was scheduled for. If a newer one has replaced it,
            // closing now would cut the newer message short.
            shown.schedule.Execute(() =>
            {
                if (frame == shown)
                {
                    Hide();
                }
            }).ExecuteLater(hold);
        }

        /// <summary>What an alert's button calls. Public so a test can press it without a panel.</summary>
        public void RunAction()
        {
            var action = pendingAction;
            Hide();
            action?.Invoke();
        }

        public void Hide()
        {
            frame?.RemoveFromHierarchy();
            frame = null;
            pendingAction = null;
        }

        private static Texture2D SpecialGround()
        {
            if (specialGround != null)
            {
                return specialGround;
            }

            const int width = 256;

            var left = new Color(0.44f, 0.27f, 0.03f, 0.97f);
            var middle = new Color(0.22f, 0.14f, 0.02f, 0.97f);
            var right = new Color(0.50f, 0.34f, 0.05f, 0.97f);

            specialGround = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var x = 0; x < width; x++)
            {
                var along = x / (float)(width - 1);
                var colour = along < 0.5f
                    ? Color.Lerp(left, middle, along * 2f)
                    : Color.Lerp(middle, right, (along - 0.5f) * 2f);

                specialGround.SetPixel(x, 0, colour);
            }

            specialGround.Apply(false, false);
            return specialGround;
        }
    }
}
