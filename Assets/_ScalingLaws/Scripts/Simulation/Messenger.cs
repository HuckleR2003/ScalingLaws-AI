using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The thread with Emil, kept between sessions.
    ///
    /// **A transcript, not a derivation, and that distinction is the whole reason it is saved.** What
    /// he says about his own company is read off the board on the day he says it: his share price
    /// against where it stood three months earlier. Re-deriving that line on load would answer with
    /// today's board, so a message sent in 2023 would silently rewrite itself into a 2027 opinion.
    /// The same reasoning that made `DeployedModel.LifetimeRevenueUsd` a record rather than a sum.
    ///
    /// The day is stored beside every line because the interface prints it, and because a thread with
    /// no dates in it is a wall of text rather than a history. It is the campaign day, which is what
    /// the player counts in, not a calendar date.
    ///
    /// **`MostKept` is a cap on the file, not a feature.** A fourteen year campaign that rings him
    /// every week would otherwise carry two thousand lines through every save and load; the oldest
    /// fall off the top the way they would in any messenger.
    /// </summary>
    public sealed class Messenger
    {
        /// <summary>How much of the thread survives. Oldest first out.</summary>
        public const int MostKept = 80;

        /// <summary>One message. Immutable, because a transcript that can be edited is not one.</summary>
        public readonly struct Line
        {
            public Line(int day, bool mine, string text)
            {
                Day = Math.Max(0, day);
                Mine = mine;
                Text = text ?? string.Empty;
            }

            /// <summary>Campaign day it was sent on, counting the first day as 1.</summary>
            public int Day { get; }

            /// <summary>Sent by the player rather than by him.</summary>
            public bool Mine { get; }

            public string Text { get; }
        }

        private readonly List<Line> lines = new();

        public IReadOnlyList<Line> Lines => lines;

        public int Count => lines.Count;

        /// <summary>Has anything ever been said. Drives the empty state rather than a separate flag.</summary>
        public bool IsEmpty => lines.Count == 0;

        /// <summary>
        /// Adds a message, on the day it was said.
        /// </summary>
        public void Say(GameDate on, bool mine, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            lines.Add(new Line(on.DayIndex + 1, mine, text));

            // From the front, so the newest are the ones kept. A messenger that dropped the newest
            // message once it was full would be the wrong way round in the only way that matters.
            while (lines.Count > MostKept)
            {
                lines.RemoveAt(0);
            }
        }

        /// <summary>
        /// Puts a loaded thread back.
        ///
        /// Clamped and truncated on the way in like everything else that comes off disk: an edited
        /// file must not be able to make the phone allocate a million labels.
        /// </summary>
        public void Restore(IEnumerable<Line> restored)
        {
            lines.Clear();

            if (restored == null)
            {
                return;
            }

            foreach (var line in restored)
            {
                if (!string.IsNullOrWhiteSpace(line.Text))
                {
                    lines.Add(line);
                }
            }

            while (lines.Count > MostKept)
            {
                lines.RemoveAt(0);
            }
        }
    }
}
