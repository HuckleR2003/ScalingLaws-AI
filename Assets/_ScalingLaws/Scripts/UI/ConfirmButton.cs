using System;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A button that asks once before it does the thing.
    ///
    /// First press turns it amber and changes the word to "are you sure"; the second press acts. A
    /// pointer that leaves it, or any other control being pressed, puts it back to resting, so a
    /// half-armed button is never left lying about for a later stray click to finish.
    ///
    /// **Its own class because this is the third place that needed it.** Cancelling a training run
    /// and abandoning an architecture programme both arm before they fire, and both wrote the
    /// arming by hand. Asked for on 2026-10-25 as a general thing rather than a one-off for the
    /// tutorial, in those words: "a confirming button, we will probably use it somewhere again".
    ///
    /// It is one `Button`, never two swapped over, so the control under the cursor on the second
    /// press is the same element that was under it on the first. Replacing an element between the
    /// press and the release is the fault that broke the first playtest of the tour.
    /// </summary>
    public sealed class ConfirmButton : Button
    {
        private readonly string restingKey;
        private readonly string armedKey;
        private readonly Action act;

        private bool armed;

        /// <param name="restingKey">Phrase-book key for what it says before it is pressed.</param>
        /// <param name="armedKey">Phrase-book key for the question. Defaults to "are you sure".</param>
        /// <param name="act">Run on the second press, never on the first.</param>
        public ConfirmButton(string restingKey, Action act, string armedKey = "confirm.sure")
        {
            this.restingKey = restingKey ?? string.Empty;
            this.armedKey = armedKey ?? "confirm.sure";
            this.act = act;

            AddToClassList("confirm");
            Retext();

            clicked += () =>
            {
                if (!armed)
                {
                    Arm(true);

                    return;
                }

                Arm(false);
                this.act?.Invoke();
            };

            // **Disarmed on the way out.** A button left asking a question the player walked away
            // from is a button whose next press does something they have stopped thinking about.
            RegisterCallback<MouseLeaveEvent>(_ => Arm(false));
        }

        /// <summary>Whether it is currently asking. The seam a test presses, since a click is not dispatched.</summary>
        public bool IsArmed => armed;

        /// <summary>Presses it the way the mouse does, for a fixture with no panel.</summary>
        public void Press()
        {
            if (!armed)
            {
                Arm(true);

                return;
            }

            Arm(false);
            act?.Invoke();
        }

        /// <summary>Puts it back to resting without acting.</summary>
        public void Disarm() => Arm(false);

        private void Arm(bool value)
        {
            if (armed == value)
            {
                return;
            }

            armed = value;
            Retext();
        }

        private void Retext()
        {
            // Read at every change rather than captured, for the reason every catalog here follows:
            // the language can change while the control is on screen.
            text = Loc.T(armed ? armedKey : restingKey);

            EnableInClassList("confirm--armed", armed);
        }
    }
}
