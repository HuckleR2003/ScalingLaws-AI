namespace ScalingLaws.Data
{
    /// <summary>How a rival feels about you, as a band rather than a number.</summary>
    public enum RelationBand
    {
        /// <summary>Actively looking for a way to cost you something.</summary>
        Rivalry = 0,

        /// <summary>Competing against your interests on purpose.</summary>
        Hostile = 1,

        /// <summary>Cooling. Nothing has happened yet and something is going to.</summary>
        Tense = 2,

        /// <summary>Competitive and professional. Where everybody starts.</summary>
        Neutral = 3,

        /// <summary>Would take the call, and might say yes.</summary>
        Friendly = 4
    }

    /// <summary>
    /// The scale a relation is measured on, and the words for it.
    ///
    /// **In `Data/` rather than in `Simulation/` because the offers catalogue has to name a band.**
    /// `Data/` may not depend on `Simulation/` and never has; the same move was made for
    /// `ResearchDirection` when `ArchitectureCeiling` needed to name a direction. What is left in
    /// `Simulation/RivalRelations` is the part that holds state and decides things, which is where
    /// that belongs.
    ///
    /// `RivalRelations` forwards every member of this, so no existing caller had to move.
    /// </summary>
    public static class RelationScale
    {
        /// <summary>The scale. Symmetric on purpose: being liked is as reachable as being hated.</summary>
        public const double Worst = -100.0;
        public const double Best = 100.0;

        /// <summary>Where every lab starts. Competitive, professional, no history.</summary>
        public const double Start = 0.0;

        /// <summary>
        /// Where the cousin's company starts, because he is family and this is not a cold call.
        /// </summary>
        public const double CousinBaseline = 70.0;

        /// <summary>How much a relation returns toward neutral each day.</summary>
        public const double DriftPerDay = 0.035;

        // ---- the bands, and they are what the interface shows ------------------------------------
        public const double FriendlyAbove = 40.0;

        // **Under `Start`, and it has to be.** `Neutral` is documented as where everybody starts and
        // it was set at 5.0 against a start of 0.0, so every lab in the game was drawn as `Tense`
        // on day one: fourteen companies the player had never touched, all reading as cooling.
        // Found by an offer that asks for Neutral and could not be made to anybody, ever.
        public const double NeutralAbove = -5.0;
        public const double TenseAbove = -35.0;
        public const double HostileAbove = -70.0;

        /// <summary>Where this particular lab sits when nothing has happened with it.</summary>
        public static double BaselineFor(CompetitorId lab) =>
            lab == CompetitorId.ESolutions ? CousinBaseline : Start;

        public static RelationBand BandFor(double value) =>
            value >= FriendlyAbove ? RelationBand.Friendly
            : value >= NeutralAbove ? RelationBand.Neutral
            : value >= TenseAbove ? RelationBand.Tense
            : value >= HostileAbove ? RelationBand.Hostile
            : RelationBand.Rivalry;

        public static string NameOf(RelationBand band) => band switch
        {
            RelationBand.Friendly => Loc.T("relation.friendly"),
            RelationBand.Neutral => Loc.T("relation.neutral"),
            RelationBand.Tense => Loc.T("relation.tense"),
            RelationBand.Hostile => Loc.T("relation.hostile"),
            _ => Loc.T("relation.rivalry")
        };

        /// <summary>What a band means for how that lab behaves, in one sentence.</summary>
        public static string NoteFor(RelationBand band) => band switch
        {
            RelationBand.Friendly => Loc.T("relation.friendly.note"),
            RelationBand.Neutral => Loc.T("relation.neutral.note"),
            RelationBand.Tense => Loc.T("relation.tense.note"),
            RelationBand.Hostile => Loc.T("relation.hostile.note"),
            _ => Loc.T("relation.rivalry.note")
        };
    }
}
