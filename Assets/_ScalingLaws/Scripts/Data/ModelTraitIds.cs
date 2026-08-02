namespace ScalingLaws.Data
{
    /// <summary>
    /// The upgradeable properties of a shipped model. Eleven of them, the same shape as the OS
    /// upgrade grid in Smartphone Tycoon, but each one wired to something the simulation actually
    /// computes. The loudest complaint about that game was that ratings ignored the specifications;
    /// here every level moves capability, brand or serving cost, and nothing is decorative.
    ///
    /// Explicit values, written into saves, never renumbered.
    /// </summary>
    public enum ModelTrait
    {
        Reasoning = 0,
        Knowledge = 1,
        Coding = 2,
        Multilingual = 3,
        Multimodal = 4,
        ContextLength = 5,
        Safety = 6,
        Latency = 7,
        Efficiency = 8,
        ToolUse = 9,
        Ecosystem = 10
    }

    /// <summary>How a competitor lab plays. Decides whether it races, waits or undercuts.</summary>
    public enum CompetitorStrategy
    {
        /// <summary>Ships the biggest model it can afford, as soon as it can. Never waits.</summary>
        FrontierRace = 0,

        /// <summary>Waits for the next accelerator generation, then leapfrogs. Patient and dangerous.</summary>
        PatientScaler = 1,

        /// <summary>Chases the cheapest token on the market. Undercuts rather than out-scales.</summary>
        CostLeader = 2,

        /// <summary>Releases open weights. Low price, wide reach, weaker margins.</summary>
        OpenWeights = 3,

        /// <summary>Sells to enterprises. Leans on safety, context and ecosystem over raw capability.</summary>
        EnterpriseFocus = 4,

        /// <summary>Watches the player and copies whatever just worked, a few months late.</summary>
        FastFollower = 5
    }

    /// <summary>What the company is willing to pay for advance warning, and how often it is right.</summary>
    public enum IntelTier
    {
        /// <summary>Free. Whatever is already public. Arrives with the news, not before it.</summary>
        PublicNews = 0,

        /// <summary>Cheap chatter from the supply chain. Often right, sometimes badly wrong.</summary>
        SupplyChainRumor = 1,

        /// <summary>A bought analyst note. Decent lead time, decent accuracy.</summary>
        AnalystReport = 2,

        /// <summary>A dedicated scouting team on retainer. Long lead time, and still not certain.</summary>
        ScoutingTeam = 3
    }
}
