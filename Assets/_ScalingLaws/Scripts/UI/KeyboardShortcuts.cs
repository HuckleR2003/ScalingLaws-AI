using System;
using ScalingLaws.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Every key the game listens to, in one table.
    ///
    /// It is a file of its own rather than four `if` statements in the shell's `Update` because a
    /// shortcut nobody can find is not a shortcut. <see cref="All"/> is the single list, and the
    /// interface reads it to say what a control does, so a key can never be bound in one place and
    /// described in another.
    ///
    /// **It refuses to fire while the player is typing.** The company name and every model name are
    /// text fields, and a game that jumps to double speed when somebody types "GPT-3" in a name box
    /// is a game with a haunted clock. That check is the reason this needs the panel root, and it is
    /// the only reason.
    /// </summary>
    public sealed class KeyboardShortcuts
    {
        /// <summary>One binding, and the words the interface uses for it.</summary>
        public readonly struct Shortcut
        {
            public Shortcut(string keyName, string action)
            {
                KeyName = keyName;
                Action = action;
            }

            /// <summary>How the key is printed, e.g. "SPACE".</summary>
            public string KeyName { get; }

            /// <summary>
            /// Phrase-book key for what pressing it does.
            ///
            /// A key rather than the sentence, for the reason every catalog in this project
            /// already follows: a table built once at type load keeps whatever language it was
            /// built in, and this one is static.
            /// </summary>
            public string Action { get; }
        }

        /// <summary>
        /// Every binding, and the words for it.
        ///
        /// **The action text is a key now, not a sentence.** The comment above this class has
        /// said since it was written that the interface reads this table so a key cannot be bound
        /// in one place and described in another. Nothing in `UI/` ever read it: the only caller
        /// anywhere was a test. So the four keys the game had were undiscoverable, which is the
        /// half of "a player without a mouse cannot play" that adding more keys would have made
        /// worse rather than better.
        /// </summary>
        public static readonly Shortcut[] All =
        {
            new("SPACE", "keys.pause"),
            new("1", "keys.slow"),
            new("2", "keys.normal"),
            new("3", "keys.fast"),
            new("\u2191 \u2193", "keys.scroll"),
            new("PGUP PGDN", "keys.page"),
            new("HOME END", "keys.ends"),
            new("ESC", "keys.menu")
        };

        /// <summary>
        /// What SPACE resumes to.
        ///
        /// Normal rather than whatever was running last: a player who paused at triple speed to read
        /// something usually wants to read the next few days too, and the three keys are right there
        /// if they do not.
        /// </summary>
        public const SimSpeed DefaultResumeSpeed = SimSpeed.Normal;

        private readonly VisualElement root;
        private readonly Func<SimSpeed> readSpeed;
        private readonly Action<SimSpeed> setSpeed;

        private SimSpeed lastRunning = DefaultResumeSpeed;

        public KeyboardShortcuts(VisualElement root, Func<SimSpeed> readSpeed, Action<SimSpeed> setSpeed)
        {
            this.root = root;
            this.readSpeed = readSpeed ?? (() => SimSpeed.Paused);
            this.setSpeed = setSpeed;
        }

        /// <summary>Called once a frame by the shell. Does nothing at all while a field has focus.</summary>
        public void Poll()
        {
            if (setSpeed == null || IsTyping())
            {
                return;
            }

            var next = Resolve(
                readSpeed(),
                Input.GetKeyDown(KeyCode.Space),
                Pressed(KeyCode.Alpha1, KeyCode.Keypad1),
                Pressed(KeyCode.Alpha2, KeyCode.Keypad2),
                Pressed(KeyCode.Alpha3, KeyCode.Keypad3));

            if (next.HasValue)
            {
                setSpeed(next.Value);
            }
        }

        /// <summary>
        /// What the keys mean, given what the clock is doing. Null for "nothing was pressed".
        ///
        /// Separated from <see cref="Poll"/> because `Input` cannot be driven from a test, and the
        /// interesting half of this is not the reading of the keyboard: it is that SPACE has to
        /// remember what to go back to. That half is testable and this is what makes it so.
        /// </summary>
        public SimSpeed? Resolve(SimSpeed current, bool space, bool one, bool two, bool three)
        {
            if (current != SimSpeed.Paused)
            {
                lastRunning = current;
            }

            if (space)
            {
                return current == SimSpeed.Paused ? Resume() : SimSpeed.Paused;
            }

            if (one)
            {
                return SimSpeed.Slow;
            }

            if (two)
            {
                return SimSpeed.Normal;
            }

            return three ? SimSpeed.Fast : null;
        }

        // ---- scrolling the open page ------------------------------------------------------

        /// <summary>How fast a held arrow moves the page, in pixels a second.</summary>
        public const float ScrollPerSecond = 1100f;

        /// <summary>
        /// How much of the window a screenful moves, as a share of it.
        ///
        /// Under one deliberately. A jump of exactly one window loses the line the reader
        /// stopped on every single time; leaving a sliver of the last screen on the new one is
        /// what makes a long page readable with a keyboard.
        /// </summary>
        public const float ScreenfulShare = 0.85f;

        /// <summary>
        /// Where the page should be scrolled to, or null when nothing was pressed.
        ///
        /// Pure, and split from the polling for the same reason <see cref="Resolve"/> is: `Input`
        /// cannot be driven from a test, and the interesting half is not the reading of the
        /// keyboard. It is the clamping, the order the keys are answered in, and the fact that a
        /// page which already fits must return null rather than zero, so holding an arrow over a
        /// short page does not fight whatever else wanted the keyboard.
        ///
        /// The arrows are held keys and the other four are pressed once, which is why the caller
        /// reads them differently and why the arrows are the only ones scaled by the frame.
        /// </summary>
        public static float? ResolveScroll(bool up, bool down, bool pageUp, bool pageDown,
            bool home, bool end, float offset, float viewport, float content, float deltaSeconds)
        {
            var scrollable = Math.Max(0f, content - viewport);

            if (scrollable <= 0f)
            {
                return null;
            }

            if (home)
            {
                return 0f;
            }

            if (end)
            {
                return scrollable;
            }

            var screenful = viewport * ScreenfulShare;
            var moved = 0f;

            if (pageUp)
            {
                moved -= screenful;
            }

            if (pageDown)
            {
                moved += screenful;
            }

            // Clamped, because a frame that took a quarter of a second (a scene load, an alt-tab)
            // would otherwise throw the page the length of the screen in one step.
            var step = ScrollPerSecond * Math.Clamp(deltaSeconds, 0f, 0.05f);

            if (up)
            {
                moved -= step;
            }

            if (down)
            {
                moved += step;
            }

            if (moved == 0f)
            {
                return null;
            }

            var wanted = Math.Clamp(offset + moved, 0f, scrollable);

            // Already against the stop. Returning the same offset would be a write a frame that
            // changes nothing, every frame, for as long as the key is held.
            return Math.Abs(wanted - offset) < 0.01f ? null : wanted;
        }

        /// <summary>
        /// Reads the scrolling keys and answers where the page should go. Null while the keyboard
        /// belongs to something else.
        /// </summary>
        public float? PollScroll(float offset, float viewport, float content, float deltaSeconds)
        {
            if (IsTyping() || IsAdjustingAControl())
            {
                return null;
            }

            return ResolveScroll(
                Input.GetKey(KeyCode.UpArrow),
                Input.GetKey(KeyCode.DownArrow),
                Input.GetKeyDown(KeyCode.PageUp),
                Input.GetKeyDown(KeyCode.PageDown),
                Input.GetKeyDown(KeyCode.Home),
                Input.GetKeyDown(KeyCode.End),
                offset, viewport, content, deltaSeconds);
        }

        /// <summary>
        /// True while the arrows belong to a control rather than to the page.
        ///
        /// **A slider is driven with the arrows and so is a dropdown**, and both are everywhere in
        /// this game: the parameter slider, the five architecture directions, the rent meter, the
        /// model picker. Scrolling the page out from under somebody nudging a price is worse than
        /// not having the shortcut at all, and it is the sort of thing that reads as the game
        /// being broken rather than as two features disagreeing.
        ///
        /// Separate from <see cref="IsTyping"/> because the speed keys have no such conflict: a
        /// slider does nothing with the 2 key.
        ///
        /// **`ScrollView` is deliberately not in this list, and the scroller is made unfocusable to
        /// match.** UI Toolkit gives a focused scroller its own arrow handling, so leaving both in
        /// place would mean two things moving one page by different amounts depending on where the
        /// player last clicked. One mechanism per subject: the shell scrolls the page and the
        /// scroller does not.
        /// </summary>
        private bool IsAdjustingAControl()
        {
            var element = root?.focusController?.focusedElement as VisualElement;

            while (element != null)
            {
                if (element is Slider or SliderInt or DropdownField)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        /// <summary>
        /// The speed SPACE goes back to.
        ///
        /// If the game was paused by something other than the player, the last running speed is
        /// whatever it was before that, which is the reading a player expects.
        /// </summary>
        private SimSpeed Resume() =>
            lastRunning == SimSpeed.Paused ? DefaultResumeSpeed : lastRunning;

        private static bool Pressed(KeyCode key, KeyCode alternate) =>
            Input.GetKeyDown(key) || Input.GetKeyDown(alternate);

        /// <summary>
        /// True while the keyboard belongs to a text field rather than to the game.
        ///
        /// It walks up rather than testing the focused element itself, because focus lands on the
        /// input inside a `TextField` and not on the field. Testing for `TextElement` instead would
        /// look right and disable every shortcut permanently: `Button` derives from `TextElement`
        /// too, so one click anywhere would have been enough.
        /// </summary>
        private bool IsTyping()
        {
            var element = root?.focusController?.focusedElement as VisualElement;

            while (element != null)
            {
                if (element is TextField)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }
    }
}
