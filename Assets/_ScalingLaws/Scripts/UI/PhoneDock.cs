using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The top of the phone, poking out from under the bottom bar.
    ///
    /// **When the tour ended the phone left the game entirely.** Emil is a permanent character, the
    /// only one in it, and after the last step there was no way to reach him at all — a player who
    /// pressed "I'll take it from here" in the first minute had skipped the tutorial permanently
    /// with no route back to it. That is a dead end reached by one click.
    ///
    /// So it is docked rather than dismissed: a sliver above the intelligence and marketing slots,
    /// which rises when the cursor finds it and rings him when it is pressed. It is deliberately the
    /// same handset the opening call arrives on, because this is where the messages, the rivals and
    /// the rest of the correspondence are going to live, and a second phone-shaped thing would be a
    /// second place to look for them.
    /// </summary>
    public sealed class PhoneDock
    {
        /// <summary>How much of the handset sits above the bar when it is resting.</summary>
        public const int RestingPeek = 26;

        /// <summary>And how much when the cursor is on it. Enough to read as a phone.</summary>
        public const int HoveredPeek = 76;

        private readonly VisualElement host;
        private readonly Func<GuideProgress> progress;
        private readonly Action ring;

        private VisualElement dock;

        public PhoneDock(VisualElement host, Func<GuideProgress> progress, Action ring)
        {
            this.host = host;
            this.progress = progress;
            this.ring = ring;
        }

        /// <summary>True while the sliver is on screen.</summary>
        public bool IsShowing => dock != null && dock.parent != null;

        /// <summary>
        /// Shows or hides the dock to match where the player is with the tour.
        ///
        /// Only once the conversation is over, whichever way it ended. While he is talking the strip
        /// is already on screen and a second handset under it would be the same character twice.
        /// </summary>
        public void Refresh()
        {
            var state = progress();

            var wanted = state != null
                && (state.Stage == GuideStage.Finished || state.Stage == GuideStage.Paused);

            if (!wanted)
            {
                Hide();
                return;
            }

            if (IsShowing)
            {
                // Already up. Rebuilding it every repaint would restart the rise animation and eat
                // the click, which is the fault that took the whole tutorial down once.
                return;
            }

            Build(state);
        }

        public void Hide()
        {
            dock?.RemoveFromHierarchy();
            dock = null;
        }

        private void Build(GuideProgress state)
        {
            dock = new VisualElement();
            dock.AddToClassList("phonedock");

            var handset = new Button(() => ring?.Invoke());
            handset.AddToClassList("phonedock__handset");

            // The same picture the opening call uses, so the sliver reads as the top of that phone
            // rather than as a new widget. Missing art leaves the drawn body, same rule as the call.
            var art = Resources.Load<Texture2D>("Others/phone");

            if (art != null)
            {
                handset.style.backgroundImage = new StyleBackground(art);
                handset.AddToClassList("phonedock__handset--art");
            }

            var notch = new VisualElement();
            notch.AddToClassList("phonedock__notch");
            handset.Add(notch);

            var caption = new Label(state.Stage == GuideStage.Paused
                ? Loc.T("phone.dock.waiting")
                : Loc.T("phone.dock.idle"));

            caption.AddToClassList("phonedock__caption");
            handset.Add(caption);

            // A paused tour has something waiting in it, so the dot is on. Finished has nothing
            // pending and the dot would be a notification for a message nobody sent.
            if (state.Stage == GuideStage.Paused)
            {
                var dot = new VisualElement();
                dot.AddToClassList("phonedock__dot");
                handset.Add(dot);
            }

            handset.tooltip = Loc.T("phone.dock.tooltip", GuideScript.CousinName);

            handset.RegisterCallback<MouseEnterEvent>(_ =>
                handset.style.bottom = HoveredPeek - handset.resolvedStyle.height);

            handset.RegisterCallback<MouseLeaveEvent>(_ =>
                handset.style.bottom = RestingPeek - handset.resolvedStyle.height);

            // The resting position needs a measured height, which is not available until the
            // element has been laid out. Set on the first geometry pass rather than guessed.
            handset.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (handset.resolvedStyle.height > 0f)
                {
                    handset.style.bottom = RestingPeek - handset.resolvedStyle.height;
                }
            });

            dock.Add(handset);
            host.Add(dock);
        }
    }
}
