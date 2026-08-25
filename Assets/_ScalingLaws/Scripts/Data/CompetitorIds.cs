namespace ScalingLaws.Data
{
    /// <summary>
    /// The labs the player is up against. Explicit values, written into saves, never renumbered.
    /// </summary>
    public enum CompetitorId
    {
        None = 0,
        OpenAi = 1,
        Anthropic = 2,
        GoogleDeepMind = 3,
        MetaAi = 4,
        MistralAi = 5,
        DeepSeek = 6,
        XAi = 7,
        AlibabaQwen = 8,

        /// <summary>
        /// A small lab starting from nothing at the same time as the player.
        ///
        /// Everybody else here is already a going concern by the time they appear. This one begins in
        /// 2022 with something barely usable, improves slowly and is left behind, which is what
        /// happened to most attempts. A field where every rival is a success story is a field where
        /// the player's own struggle looks like a personal failing.
        /// </summary>
        Groq = 9,

        // Four labs added 2026-08-15 to make the middle of the board a real place rather than a
        // gap between the frontier and the one struggling startup. Three of them are here because
        // of how they end: the field needed companies the player can watch fall over, and watch
        // *why*.
        //
        // **These names are the real companies the arcs are drawn from, and they are deliberately
        // not what the player sees.** The displayed names are parodies and live on the dossier;
        // keeping the real one here is what documents where an arc came from, so somebody reading
        // this file in a year can check the history rather than guess at it.
        //
        // Their histories are in LabDossiers.

        /// <summary>Open image generation. Enormous reach, no way to charge for it, and lawsuits.</summary>
        StabilityAi = 10,

        /// <summary>A genuine challenger that was hollowed out by a hiring announcement in one day.</summary>
        InflectionAi = 11,

        /// <summary>The European bid. Right about the problem, out-scaled on the answer.</summary>
        AlephAlpha = 12,

        /// <summary>The survivor. Never chased the frontier, never had to.</summary>
        Cohere = 13,

        /// <summary>
        /// Emil's shop. The one company on this board that is not drawn from a real one.
        ///
        /// He is the cousin who walks the player through their first hour, and a permanent character
        /// with no place in the world is a voice from nowhere. Small, steady, mid-table, never near
        /// the frontier and still trading at the end: a cousin who turned out to be secretly winning
        /// would make the favour he does in the tutorial read as charity.
        ///
        /// Everything about it carries `isProjection`, because it is invented and the honesty flag
        /// is about not passing invention off as record.
        /// </summary>
        ESolutions = 14
    }
}
