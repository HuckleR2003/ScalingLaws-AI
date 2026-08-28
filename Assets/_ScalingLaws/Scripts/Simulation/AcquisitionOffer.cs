using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Somebody wants to buy the company.
    ///
    /// **This is the one offer in the game that ends it.** Accepting is a way to finish a campaign
    /// on purpose, with a number attached, rather than by going bankrupt or by closing the window,
    /// and that is the reason it exists: a tycoon with no exit has no ending except failure.
    ///
    /// It expires. An offer that sat open forever would be a floor under every later decision, and
    /// the player would stop taking risks knowing the parachute never closes.
    /// </summary>
    public sealed class AcquisitionOffer
    {
        public const int OpenForDays = 45;

        public AcquisitionOffer(CompetitorId from, GameDate madeOn, long amountUsd,
            double valuationMultiple)
        {
            From = from;
            MadeOn = madeOn;
            AmountUsd = Math.Max(0L, amountUsd);
            ValuationMultiple = SimUnits.Finite(valuationMultiple, 1.0);
        }

        public CompetitorId From { get; }
        public GameDate MadeOn { get; }
        public long AmountUsd { get; }

        /// <summary>What they are paying against the company's own book value.</summary>
        public double ValuationMultiple { get; }

        public int DaysElapsed { get; private set; }
        public bool HasLapsed => DaysElapsed >= OpenForDays;
        public int DaysLeft => Math.Max(0, OpenForDays - DaysElapsed);

        public void Advance() => DaysElapsed = Math.Min(OpenForDays, DaysElapsed + 1);

        public void Restore(int daysElapsed) =>
            DaysElapsed = Math.Clamp(daysElapsed, 0, OpenForDays);
    }

    /// <summary>
    /// What the company is worth, and who would want it.
    ///
    /// **Blocked outright while a state loan is outstanding**, and that is a rule rather than a
    /// discount. A government that has put a sovereign compute programme into a company is not
    /// going to watch it be sold to a competitor, and the alternative reading, letting the player
    /// take ten billion of public money and then sell the company, would make the state programme
    /// the strongest move in the game by a wide margin.
    /// </summary>
    public static class Acquisitions
    {
        /// <summary>Yearly revenue is worth this much of a company on top of what it owns.</summary>
        public const double RevenueMultiple = 4.5;

        /// <summary>Every fan is worth about this much to somebody buying the brand.</summary>
        public const double UsdPerFan = 12.0;

        /// <summary>Nobody bids for a company under this, so no offer is generated.</summary>
        public const long InterestFloorUsd = 400_000_000;

        /// <summary>The band a bid lands in against book value. Never below one.</summary>
        public const double WorstMultiple = 1.05;
        public const double BestMultiple = 2.40;

        /// <summary>Chance per day that somebody who could bid, does.</summary>
        public const double ChancePerDay = 0.0016;

        /// <summary>How long after a refusal before anybody tries again.</summary>
        public const int QuietDaysAfterRefusal = 540;

        /// <summary>What turning them down costs. Small: a refusal is not an insult.</summary>
        public const double RelationCostOfRefusal = -4.0;

        /// <summary>
        /// What the company is worth on paper.
        ///
        /// Cash, plus the fleet at what it would actually fetch, plus a multiple of what it earns,
        /// plus the following. **Reputation is not in it directly** and that is on purpose: it is
        /// already inside the revenue and the fan count, and counting it a third time would let a
        /// well-liked company that sells nothing be worth more than one that sells.
        /// </summary>
        public static long BookValueUsd(long cashUsd, long fleetResaleUsd, long annualRevenueUsd,
            double fans)
        {
            var earnings = (long)Math.Max(0.0, annualRevenueUsd * RevenueMultiple);
            var following = (long)Math.Max(0.0, SimUnits.Finite(fans, 0.0) * UsdPerFan);

            var total = Math.Max(0L, cashUsd) + Math.Max(0L, fleetResaleUsd) + earnings + following;

            return Math.Max(0L, total);
        }

        /// <summary>
        /// The multiple a particular bidder puts on it.
        ///
        /// Better when the buyer is behind and the company being bought is ahead, because that is
        /// when an acquisition is buying a position rather than an asset. A lab that is already
        /// winning has less reason to pay a premium and the number says so.
        /// </summary>
        public static double MultipleFor(double playerCapability, double bidderCapability,
            double roll)
        {
            var behind = Math.Clamp(
                (playerCapability - bidderCapability) / 20.0, -0.5, 0.5) + 0.5;

            var band = WorstMultiple + (BestMultiple - WorstMultiple) * behind;
            var jitter = (Math.Clamp(roll, 0.0, 1.0) - 0.5) * 0.25;

            return SimUnits.Finite(Math.Clamp(band + jitter, WorstMultiple, BestMultiple),
                WorstMultiple);
        }
    }
}
