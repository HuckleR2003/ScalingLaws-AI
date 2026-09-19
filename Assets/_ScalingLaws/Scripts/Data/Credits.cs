using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The people on the CREDITS sheet in the main menu.
    ///
    /// **Names, not keys.** A person's name is not translated, so these are plain strings and only
    /// the headings go through the phrase book. Written exactly the way each person asked to be
    /// written, nickname included: this is the one list in the game where a typo is an insult.
    ///
    /// Supporters of the Steam launch are added here and nowhere else, so the sheet and anything
    /// else that ever reads the list cannot disagree about who is on it.
    /// </summary>
    public static class Credits
    {
        /// <summary>Who made the game.</summary>
        public const string CreatedBy = "Marcin 'HCK' Firmuga";

        /// <summary>The studio name under which it is published.</summary>
        public const string Studio = "HCK Labs";

        /// <summary>
        /// People who played early builds and wrote down where they broke. Each of them changed
        /// what is in the game.
        /// </summary>
        public static readonly IReadOnlyList<string> Testers = new[]
        {
            "Natalka6456",
        };

        /// <summary>
        /// People who supported the Steam launch. Empty until the first one arrives, and the sheet
        /// says so rather than drawing an empty heading.
        /// </summary>
        public static readonly IReadOnlyList<string> Supporters = new string[0];
    }
}
