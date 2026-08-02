namespace ScalingLaws.Data
{
    /// <summary>
    /// Rounds in the order they are raised. Explicit values, written into saves, never renumbered.
    /// </summary>
    public enum FundingStage
    {
        None = 0,
        Seed = 1,
        SeriesA = 2,
        SeriesB = 3,
        SeriesC = 4,
        SeriesD = 5,
        Growth = 6,
        PublicOffering = 7
    }

    /// <summary>Why an offer is not on the table.</summary>
    public enum FundingRefusal
    {
        None = 0,
        AlreadyRaised = 1,
        NeedsCapability = 2,
        NeedsRevenue = 3,
        NeedsReleasedModels = 4,
        TooEarly = 5,
        RoundAlreadyOpen = 6,
        Insolvent = 7
    }
}
