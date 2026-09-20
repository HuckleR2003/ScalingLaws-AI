using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// One tester: the name exactly as they asked to be written, what they did in a phrase-book key,
    /// and an optional icon under Resources.
    /// </summary>
    public readonly struct Tester
    {
        public Tester(string name, string roleKey, string icon = null)
        {
            Name = name;
            RoleKey = roleKey;
            Icon = icon;
        }

        /// <summary>Not translated. A person's name is the one string here that never changes.</summary>
        public string Name { get; }

        /// <summary>Phrase-book key for what they did, so the line reads in both languages.</summary>
        public string RoleKey { get; }

        /// <summary>Path under Resources to a small picture drawn left of the name, or null.</summary>
        public string Icon { get; }
    }

    /// <summary>
    /// The people on the CREDITS sheet and under SETTINGS in the main menu.
    ///
    /// **Names, not keys.** A person's name is not translated, so names are plain strings and only
    /// what they did goes through the phrase book. Written exactly the way each person asked to be
    /// written, nickname included: this is the one list in the game where a typo is an insult.
    ///
    /// Supporters of the Steam launch are added here and nowhere else, so every screen that reads
    /// the list agrees about who is on it.
    /// </summary>
    public static class Credits
    {
        /// <summary>Who made the game.</summary>
        public const string CreatedBy = "Marcin 'HCK' Firmuga";

        /// <summary>The studio name under which it is published.</summary>
        public const string Studio = "HCK Labs";

        /// <summary>
        /// People who played early builds and wrote down where they broke, in the order they joined.
        /// Each of them changed what is in the game.
        /// </summary>
        public static readonly IReadOnlyList<Tester> Testers = new[]
        {
            new Tester("MiNatix", "tester.natalka", "Testers/minatix"),
            new Tester("Francisco T", "tester.francisco"),
        };

        /// <summary>
        /// People who supported the Steam launch. Empty until the first one arrives, and the sheet
        /// says so rather than drawing an empty heading.
        /// </summary>
        public static readonly IReadOnlyList<string> Supporters = new string[0];
    }
}
