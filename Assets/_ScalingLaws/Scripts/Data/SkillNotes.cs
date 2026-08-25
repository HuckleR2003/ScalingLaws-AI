namespace ScalingLaws.Data
{
    /// <summary>
    /// What each of the seven founder skills is, for somebody who has never played this.
    ///
    /// **The creator asks for two hundred points to be spent across seven words and explains none of
    /// them.** `ShortEffect` says what a skill moves in three words, which is right for the row and
    /// useless for deciding: "cheaper inference" is not an argument to somebody who does not yet
    /// know that inference is the bill you pay every day forever.
    ///
    /// Same shape as <see cref="TechNotes"/> and the same register as the twenty six written for the
    /// screens: an everyday thing first, the game second. The `Affects` line is the one the interface
    /// prints in gold, because that is the half a player is actually choosing on.
    ///
    /// Plain strings and no UnityEngine, so this stays in `Data/`.
    /// </summary>
    public static class SkillNotes
    {
        /// <summary>Bump when the copy changes enough that a screenshot would be stale.</summary>
        public const string CatalogVersion = "2026.08.25";

        private static TechNotes.Note From(string stem) => new(
            Loc.T(stem + ".title"),
            Loc.T(stem + ".what"),
            Loc.T(stem + ".affects"),
            Loc.T(stem + ".high"),
            Loc.T(stem + ".low"));

        /// <summary>
        /// The note for a skill.
        ///
        /// Written as a switch rather than assembled from the enum name, because a key built by
        /// concatenation is invisible to the guard that checks every key exists. That has already
        /// cost this project one shipped screen full of raw keys.
        /// </summary>
        public static TechNotes.Note For(PlayerSkill skill) => skill switch
        {
            PlayerSkill.Development => From("skill.development"),
            PlayerSkill.Management => From("skill.management"),
            PlayerSkill.Teamwork => From("skill.teamwork"),
            PlayerSkill.Concept => From("skill.concept"),
            PlayerSkill.Software => From("skill.software"),
            PlayerSkill.DataEngineering => From("skill.data"),
            _ => From("skill.safety")
        };
    }
}
