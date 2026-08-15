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

            /// <summary>What pressing it does, in the fewest words that stay true.</summary>
            public string Action { get; }
        }

        public static readonly Shortcut[] All =
        {
            new("SPACE", "Pause or resume"),
            new("1", "Speed x1"),
            new("2", "Speed x2"),
            new("3", "Speed x3")
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
