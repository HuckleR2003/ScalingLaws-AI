using System;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A line that says something happened, for a couple of seconds, and then is gone.
    ///
    /// **Built because SAVE said nothing at all.** Pressing it wrote the file and changed not one
    /// pixel, so the only way to find out whether a campaign had been saved was to quit and look,
    /// which is the one moment a player cannot afford to be wrong about. The autosave was worse: it
    /// happened on a timer nobody could see.
    ///
    /// It mounts on the panel root rather than beside whatever asked for it, for the same reason
    /// <see cref="InsightTip"/> does: the thing that raised it is usually about to be rebuilt, and a
    /// message living inside a rebuilt page disappears with it. `UiBootstrap.Prepare` sets
    /// <see cref="Host"/>, which is the one function every document already calls.
    ///
    /// One at a time. Two of these stacking is a notification centre, which is a different feature
    /// and not one this game needs.
    /// </summary>
    public static class Toast
    {
        /// <summary>How long a line stays up, in milliseconds.</summary>
        public const long HoldMilliseconds = 2200;

        /// <summary>The panel root. Set once by <see cref="UiBootstrap"/>.</summary>
        public static VisualElement Host { get; set; }

        private static VisualElement current;

        /// <summary>
        /// Puts a line up. A null or empty message takes down whatever is showing, so a caller
        /// never has to reason about whether something is already there.
        /// </summary>
        public static void Show(string message, bool warning = false)
        {
            Hide();

            if (Host == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            var line = new Label(message);
            line.AddToClassList("toast");
            line.EnableInClassList("toast--warn", warning);

            // **It must never eat a click.** It sits over the top bar, which is where SAVE and MENU
            // are, and an element that takes pointer events there would make the two buttons a
            // player presses most dead for two seconds after every save. That is the exact fault
            // this project shipped twice with invisible overlays.
            line.pickingMode = PickingMode.Ignore;

            Host.Add(line);
            current = line;

            line.schedule.Execute(() =>
            {
                if (current == line)
                {
                    Hide();
                }
            }).StartingIn(HoldMilliseconds);
        }

        /// <summary>Takes down whatever is showing, if anything is.</summary>
        public static void Hide()
        {
            if (current == null)
            {
                return;
            }

            current.RemoveFromHierarchy();
            current = null;
        }

        /// <summary>Whether a line is up. For tests, which have no eyes.</summary>
        public static bool IsShowing => current != null;

        /// <summary>What the line currently says, or empty. For tests.</summary>
        public static string Text => current is Label label ? label.text : string.Empty;

        /// <summary>
        /// Forgets the host and anything showing.
        ///
        /// Static state outlives a panel, and a test that left a toast mounted would hand the next
        /// one a host that is no longer in any document. Same reason `InsightTip` has one.
        /// </summary>
        public static void Reset()
        {
            current = null;
            Host = null;
        }
    }
}
