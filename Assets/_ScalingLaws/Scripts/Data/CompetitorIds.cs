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
        Groq = 9
    }
}
