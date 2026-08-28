using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>What the company is being written about this month.</summary>
    public enum ScandalKind
    {
        None = 0,

        /// <summary>The price went somewhere people are willing to write about.</summary>
        Pricing = 1,

        /// <summary>The free tier was cut, and the people who lost it noticed.</summary>
        FreeTierCut = 2,

        /// <summary>The service has been slow for long enough that it is a story.</summary>
        Reliability = 3,

        /// <summary>Nothing has shipped for a very long time and somebody said so.</summary>
        Stagnation = 4,

        /// <summary>A model is on sale with the safety work visibly skipped.</summary>
        Corners = 5
    }

    /// <summary>
    /// The press noticing something the company already did.
    ///
    /// **Every scandal is caused by a decision the player made and can undo.** None of them is a
    /// dice roll against a company doing nothing wrong: the price really is high, the free tier
    /// really was cut, the fleet really is at ninety five per cent. That is the difference between
    /// a mechanic that teaches and one that only punishes, and it is the same rule the news desk
    /// has followed since it was written: translate, never invent.
    ///
    /// **One a month at most.** Without the timeout a bad quarter files the same story thirty times
    /// and the wire becomes unreadable exactly when the player most needs to read it.
    /// </summary>
    public static class ModelScandals
    {
        /// <summary>How long the desk waits before it will run another story of this kind.</summary>
        public const int QuietDays = 30;

        /// <summary>What a story takes off reputation. Blunt, and it recovers.</summary>
        public const double ReputationCost = 0.045;

        /// <summary>Price against the market before anybody writes about it.</summary>
        public const double PricyAbove = 1.75;

        /// <summary>Load that has to be sustained before slowness is a story.</summary>
        public const double StrainedAbove = 0.93;

        /// <summary>Days without a release before somebody writes the obituary.</summary>
        public const int StagnantDays = 1_100;

        /// <summary>Reputation below which a company is too small to be worth the column.</summary>
        public const double BeneathNotice = 0.18;

        /// <summary>
        /// Which story today is, or none.
        ///
        /// Ordered, and the order is a judgment about what a reader cares about most: safety before
        /// money, money before speed, and the slow decline last because it is the least surprising.
        /// Only one runs, because a month with three scandals in it is a company nobody would still
        /// be buying from and the simulation does not model that.
        /// </summary>
        public static ScandalKind Today(double reputation, double priceAgainstMarket,
            bool freeTierJustCut, double sustainedLoad, int daysSinceRelease, bool cornersCut)
        {
            if (reputation < BeneathNotice)
            {
                return ScandalKind.None;
            }

            if (cornersCut)
            {
                return ScandalKind.Corners;
            }

            if (freeTierJustCut)
            {
                return ScandalKind.FreeTierCut;
            }

            if (SimUnits.Finite(priceAgainstMarket, 1.0) > PricyAbove)
            {
                return ScandalKind.Pricing;
            }

            if (SimUnits.Finite(sustainedLoad, 0.0) > StrainedAbove)
            {
                return ScandalKind.Reliability;
            }

            if (daysSinceRelease > StagnantDays)
            {
                return ScandalKind.Stagnation;
            }

            return ScandalKind.None;
        }

        /// <summary>
        /// What it costs, scaled by how visible the company is.
        ///
        /// A company nobody has heard of is not damaged much by a story nobody reads, and a
        /// household name is. Without this the first bad month of a young company would cost it
        /// proportionally more than the same month costs the market leader, which is backwards.
        /// </summary>
        public static double CostFor(ScandalKind kind, double reputation)
        {
            if (kind == ScandalKind.None)
            {
                return 0.0;
            }

            var visibility = Math.Clamp(SimUnits.Finite(reputation, 0.0), 0.0, 1.0);

            return SimUnits.Finite(ReputationCost * (0.35 + 0.65 * visibility), 0.0);
        }

        public static string HeadlineKey(ScandalKind kind) => kind switch
        {
            ScandalKind.Pricing => "scandal.pricing",
            ScandalKind.FreeTierCut => "scandal.freetier",
            ScandalKind.Reliability => "scandal.reliability",
            ScandalKind.Stagnation => "scandal.stagnation",
            ScandalKind.Corners => "scandal.corners",
            _ => "scandal.none"
        };
    }
}
