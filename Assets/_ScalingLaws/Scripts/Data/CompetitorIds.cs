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
        AlibabaQwen = 8
    }
}
