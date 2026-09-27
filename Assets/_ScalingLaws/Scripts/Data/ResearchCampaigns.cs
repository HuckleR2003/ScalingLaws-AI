using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// How long a joint research campaign runs for.
    ///
    /// **Three terms and none of them is best.** A short one is cheap to be wrong about and pays
    /// little; a long one is the better rate and locks the money up through a year in which the
    /// frontier moves twice. That is the same trade the marketing contracts make, and it is what
    /// stops the longest term being the only answer.
    /// </summary>
    public enum CampaignTerm
    {
        Quarter = 0,
        Half = 1,
        Year = 2
    }

    /// <summary>
    /// The joint research campaign: what it costs a day, what it pays a day, and who is in it.
    ///
    /// **Modelled on the Frontier Model Forum and its AI Safety Fund**, where four labs that compete
    /// on everything else put more than ten million dollars into one pot with philanthropic
    /// partners. The shape worth taking from it is not the money, it is that the pot is bigger than
    /// any member's share of it, which is the entire argument for joining one.
    ///
    /// The author asked for this by name and gave the shape: a campaign that runs for a while and
    /// **pays points day by day rather than in a lump at the end**, costing two to three times less
    /// than reaching the same place alone. The numbers below are his brief turned into constants and
    /// they are to be measured with `DeepCampaignProbe` before they are believed.
    /// </summary>
    public static class ResearchCampaignCatalog
    {
        /// <summary>Points a day at the base rate, before anybody else joins.</summary>
        public const double PointsPerDay = 14.0;

        /// <summary>What a day of it costs one member running it alone.</summary>
        public const long CostPerDayUsd = 42_000;

        /// <summary>Days in each term.</summary>
        public static int DaysIn(CampaignTerm term) => term switch
        {
            CampaignTerm.Quarter => 90,
            CampaignTerm.Half => 180,
            _ => 365
        };

        /// <summary>
        /// A longer term is a better rate, which is what makes it a decision rather than a default.
        /// </summary>
        public static double RateFor(CampaignTerm term) => term switch
        {
            CampaignTerm.Quarter => 1.00,
            CampaignTerm.Half => 1.08,
            _ => 1.18
        };

        public static string KeyFor(CampaignTerm term) => term switch
        {
            CampaignTerm.Quarter => "campaign.term.quarter",
            CampaignTerm.Half => "campaign.term.half",
            _ => "campaign.term.year"
        };

        /// <summary>
        /// What the points are multiplied by with this many labs in the room, the player included.
        ///
        /// **Sublinear, and it has to be.** Two labs are worth 2.2 of one rather than 2, and three
        /// are worth 3.0 rather than 3.3, because a room of people who mostly already know the same
        /// things does not scale. If it were linear a consortium would simply be the correct answer
        /// at every size and the choice of who to ask would stop mattering.
        /// </summary>
        public static double PointsMultiplier(int members) => members switch
        {
            <= 1 => 1.0,
            2 => 2.2,
            _ => 3.0
        };

        /// <summary>
        /// What each member pays, as a share of running it alone.
        ///
        /// The cost is split and then some, because a shared programme is genuinely cheaper to run
        /// than two separate ones, which is the other half of why anybody signs.
        /// </summary>
        public static double CostShare(int members) => members switch
        {
            <= 1 => 1.0,
            2 => 0.60,
            _ => 0.45
        };

        /// <summary>
        /// How much further the money goes: the points multiplier over what each member pays.
        ///
        /// At two members that is 2.2 over 0.6, which is **about three and two thirds**, and it is
        /// the figure the whole mechanic is judged on. The author asked for two to three times, and
        /// this is that measured from the other side: alone you would have to spend three and a half
        /// times as much to reach the same place.
        /// </summary>
        public static double ValueMultiple(int members) =>
            PointsMultiplier(members) / CostShare(members);

        /// <summary>
        /// Leaving early costs a share of what is left on the term.
        ///
        /// **Without it, joining and walking out on the last profitable day is the dominant line**,
        /// and every one of these would be taken and abandoned. It is a share rather than a flat fee
        /// so that walking out on day one of a year costs more than walking out in month eleven.
        /// </summary>
        public const double BreakFeeShare = 0.55;

        /// <summary>The alliance level a lab has to be at before it can be asked into one.</summary>
        public const int NeedsAllianceLevel = 2;

        /// <summary>Most labs in one room, the player included. Past this it is a conference.</summary>
        public const int MostMembers = 3;

        public static IReadOnlyList<CampaignTerm> Terms { get; } = new[]
        {
            CampaignTerm.Quarter, CampaignTerm.Half, CampaignTerm.Year
        };
    }
}
