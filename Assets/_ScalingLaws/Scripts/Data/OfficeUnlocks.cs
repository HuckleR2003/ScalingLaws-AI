namespace ScalingLaws.Data
{
    /// <summary>
    /// Which research a company needs before it may move into a given office.
    ///
    /// **One table, read by the rule and by the screen.** The premises page has to draw a locked
    /// row and say which node opens it, and the simulation has to refuse the move; two copies of
    /// that mapping is a screen that names one node while the till waits on another.
    ///
    /// A tier with no node here is not gated. That is deliberate rather than an omission: the only
    /// offices a player can reach are the ones with a place built for them
    /// (<see cref="OfficeCatalog.Places"/>), and gating a tier nobody can move into would put a
    /// node on the board that opens a door that is not there.
    /// <c>OfficeUnlockTests.EveryPlaceOnTheChooserHasAResearchBehindIt</c> is what keeps the two
    /// lists together when the next office is built.
    /// </summary>
    public static class OfficeUnlocks
    {
        /// <summary>The node that opens a tier, or <see cref="ResearchNodeId.None"/> when none does.</summary>
        public static ResearchNodeId RequiredFor(OfficeTier tier) => tier switch
        {
            OfficeTier.Loft => ResearchNodeId.LeaseASmallHub,
            OfficeTier.Floor => ResearchNodeId.LeaseABigHub,

            // The garage is where a campaign starts. Everything else has no place built for it
            // yet and is therefore unreachable for a reason that has nothing to do with research.
            _ => ResearchNodeId.None
        };

        /// <summary>True when the tier is gated at all. The garage never is.</summary>
        public static bool IsGated(OfficeTier tier) => RequiredFor(tier) != ResearchNodeId.None;
    }
}
