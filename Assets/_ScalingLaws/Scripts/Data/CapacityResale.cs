using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Selling the part of the fleet nothing is using today.
    ///
    /// **The measurement this exists for.** The campaign probe reports serving load at 0% for eight
    /// consecutive years, with capacity between 200 and 3,082 times demand, while the fleet bill ran
    /// at $5,806,735 a day in January 2024 against 4% utilisation. A fixed cost that large, which no
    /// screen in the game showed, is the single biggest thing that kills a company here. Nothing
    /// about that is a balance accident; what was missing was any way to do something about it.
    ///
    /// **Three properties keep this from being a rebate on over-buying**, and the first one is
    /// arithmetic rather than tuning:
    ///
    /// 1. **Capacity resells at half what it costs to rent**, so over-renting always loses money.
    ///    When the idle cap is what binds, the company recovers exactly half of what it overspent;
    ///    when the ladder cap binds, it recovers less. There is no fleet size at which buying more
    ///    than you need comes out ahead, and that is provable rather than measured.
    /// 2. **The cap is a share of the whole fleet, never of the surplus.** A percentage of surplus
    ///    is unbounded: 30% of a 3,082x surplus is a second business. 30% of the fleet resold at
    ///    half price returns 15% of the bill and can never return more than the bill.
    /// 3. **What is sold is gone for the day.** Capacity sold is capacity the company cannot serve
    ///    with, so a company living off resale has no headroom when demand jumps. That is the
    ///    decision this mechanic is, and without it the ladder would be a discount with no downside.
    /// </summary>
    public static class CapacityResale
    {
        /// <summary>How far the ladder climbs.</summary>
        public const int MostLevels = 8;

        /// <summary>
        /// What a buyer pays, as a share of what the same capacity costs the company to hold.
        ///
        /// **This single number is what protects the spine, so it must stay below one.** The
        /// company is reselling somebody else's hardware at short notice to a buyer who knows it is
        /// short notice, and nobody in that position gets the list price.
        /// </summary>
        public const double ResalePriceFraction = 0.50;

        /// <summary>
        /// The least and the most of the whole fleet that may be sold in a day, by level.
        ///
        /// The author's ladder, extended to the maximum he asked for. It is a band rather than a
        /// figure because an outside buyer is not a standing order: some days there is somebody who
        /// needs a cluster this afternoon and some days there is not.
        /// </summary>
        public static (double Low, double High) BandAt(int level) => level switch
        {
            <= 0 => (0.0, 0.0),
            1 => (0.01, 0.03),
            2 => (0.02, 0.05),
            3 => (0.03, 0.07),
            4 => (0.04, 0.09),
            5 => (0.05, 0.10),
            6 => (0.08, 0.16),
            7 => (0.11, 0.22),
            _ => (0.15, 0.30)
        };

        /// <summary>
        /// Where in the band today lands, from how short of accelerators the world is.
        ///
        /// **Scarcity rather than a dice roll, and it is the same number the rental price reads.**
        /// A shortage is exactly when somebody will take spare capacity off your hands, and a glut
        /// is exactly when nobody will. So the years this pays best are 2023 to 2026, which is the
        /// window the author wanted it to help, and it pays least in the late game when everybody
        /// is sitting on idle silicon. That makes it a question of timing rather than a discount,
        /// which is the shape every other decision in this game has.
        /// </summary>
        public static double ShareOn(int level, double scarcity)
        {
            var (low, high) = BandAt(level);
            var appetite = Math.Clamp(double.IsNaN(scarcity) ? 0.0 : scarcity, 0.0, 1.0);

            return low + (high - low) * appetite;
        }

        /// <summary>
        /// Research points for the next level.
        ///
        /// **Dear on purpose, and dearer than any other ladder in the game.** The author's words
        /// were that this is expensive and then becomes genuinely good, which is the right shape for
        /// something that ends up taking a sixth of a fleet bill off the biggest cost a company has.
        /// The whole ladder is 4,320 points and $2,640,000.
        /// </summary>
        public static int PointsFor(int nextLevel) => 120 * Math.Max(1, nextLevel);

        /// <summary>The bill that comes with the points.</summary>
        public static long CashFor(int nextLevel) =>
            60_000L + 60_000L * Math.Max(1, nextLevel);

        /// <summary>
        /// What a company at this level would take off its fleet bill, if it were wholly idle.
        ///
        /// For the panel, and for working out whether a level is worth buying before buying it.
        /// A company that is busy sells less than this, and one that is busy everywhere sells none.
        /// </summary>
        public static double BillShareAt(int level, double scarcity) =>
            ShareOn(level, scarcity) * ResalePriceFraction;
    }
}
