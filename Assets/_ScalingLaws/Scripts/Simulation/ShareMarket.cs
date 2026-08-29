using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What a rival's stock is worth, how much of it exists, and what holding it pays.
    ///
    /// **The price is derived, never stored.** It is a function of the lab's own fundamentals on a
    /// date plus a deterministic wave, so it replays identically, needs no migration, and cannot
    /// drift out of step with the company it is supposed to be about. Only the player's holdings
    /// and what they paid are saved, because those are the only parts the player changed.
    ///
    /// **Nothing here is investment advice and nothing here is a real company.** These are the
    /// parody labs the rest of the game already runs on, priced off their in-game capability.
    /// </summary>
    public static class ShareMarket
    {
        /// <summary>
        /// What a share costs before any of the lab's own standing is applied.
        ///
        /// **Calibrated by rendering the board, not by taste.** At 6.0 every company on it priced
        /// between $0.71 and $0.90, which is a column of numbers that all look the same and says
        /// nothing about which company is worth more. At 18 a young lab is around $10 and a leader
        /// is in the forties, which is a spread the eye can read down.
        /// </summary>
        public const double BasePriceUsd = 18.0;

        /// <summary>Broker's cut, charged on the way in and on the way out.</summary>
        public const double CommissionRate = 0.025;

        /// <summary>
        /// How long the market takes to price a new release in.
        ///
        /// Long enough that the chart ramps rather than steps, short enough that a release still
        /// reads as the event that moved the company.
        /// </summary>
        public const int PricingInDays = 45;

        /// <summary>
        /// What a holding pays each month, as a share of what it is currently worth.
        ///
        /// **Deliberately small.** 0.35% a month is a little over 4% a year, which is a reason to
        /// hold rather than a reason to stop building models. A dividend that competes with running
        /// the company would turn the whole game into a savings account, and this project's spine
        /// is that capital left sitting still is capital losing a race.
        /// </summary>
        public const double MonthlyYield = 0.0035;

        /// <summary>A lab in trouble pays nothing, however much of it you hold.</summary>
        public const double PaysDividendAboveBrand = 0.25;

        /// <summary>Past this you control the company and can move to take it over.</summary>
        public const double ControlThreshold = 0.50;

        /// <summary>
        /// The most of a bought company's following that survives the purchase.
        ///
        /// **Nobody keeps all of it and that is the point.** A quarter of the people who liked them
        /// liked them for not being you. Buying a rival is buying most of something rather than
        /// adding all of it, which is what stops acquisition from being strictly better than
        /// building.
        /// </summary>
        public const double TransferShare = 0.75;

        /// <summary>How much of the float the market will actually let go on any one day.</summary>
        public const double TradableShare = 0.35;

        /// <summary>
        /// How many shares of a lab exist.
        ///
        /// Hashed from the lab so it is stable for a campaign and different between companies, and
        /// coarse on purpose: the figure exists to make a percentage meaningful, not to be
        /// modelled. Between 120 and 400 million.
        /// </summary>
        public static long SharesOutstanding(CompetitorId lab)
        {
            unchecked
            {
                var value = ((uint)lab * 2654435761u) ^ 0x9E3779B9u;
                value ^= value >> 13;
                value *= 2246822519u;
                value ^= value >> 16;

                return 120_000_000L + (long)(value % 281_000_000u);
            }
        }

        /// <summary>
        /// What one share costs today.
        ///
        /// Capability is the ground: a lab shipping better models is worth more. Standing is a
        /// multiplier on top, because the market pays for a company people have heard of. The wave
        /// is small and deterministic, and it is there so the chart has something to be a chart
        /// about rather than to make timing the entry the point of the screen.
        /// </summary>
        public static double PriceUsd(CompetitorId lab, double capability, double brand,
            GameDate date)
        {
            var ground = BasePriceUsd * Math.Pow(Math.Max(0.0, capability) / 45.0 + 0.35, 1.35);
            // The floor is high and the swing is smaller than it was. Brand on these labs sits
            // nearer 0.05 than 0.5 for most of a campaign, so a term that leaned hard on it pushed
            // the whole board into pennies.
            var standing = 0.70 + 1.20 * Math.Clamp(SimUnits.Finite(brand, 0.0), 0.0, 1.5);

            var price = ground * standing * Wave(lab, date);

            return Math.Clamp(SimUnits.Finite(price, BasePriceUsd), 0.35, 4000.0);
        }

        /// <summary>
        /// The wiggle, from three periods that do not divide into each other.
        ///
        /// Three incommensurate periods rather than one, because a single sine reads as a machine
        /// and a player watching the chart notices within a minute. Amplitude stays under a fifth
        /// so a rally is never large enough to beat actually running the company.
        /// </summary>
        private static double Wave(CompetitorId lab, GameDate date)
        {
            var phase = (uint)lab * 0.7391;
            var day = date.DayIndex;

            // Four terms, and the fast ones carry real weight. The first version put a fiftieth
            // of the range on its quickest term, so ninety days rendered as one smooth arc: a
            // machine drawing a shape rather than a price anybody traded.
            var slow = Math.Sin(day / 137.0 + phase) * 0.055;
            var middle = Math.Sin(day / 29.3 + phase * 2.1) * 0.045;
            var quick = Math.Sin(day / 8.7 + phase * 3.7) * 0.032;
            var fast = Math.Sin(day / 3.1 + phase * 5.3) * 0.019;

            return 1.0 + slow + middle + quick + fast;
        }

        /// <summary>
        /// What a lab's share costs on a date, from the published record.
        ///
        /// **The one entry point.** The board, the chart and the ticket all come through here, so
        /// they cannot disagree, and the history a chart draws is the same function today's price
        /// comes from rather than a second implementation of it.
        ///
        /// `standingMultiplier` is anything the player has done to them. It is passed in rather
        /// than looked up because this type knows about companies and not about the campaign.
        /// </summary>
        public static double PriceOn(CompetitorId lab, GameDate date,
            double standingMultiplier = 1.0)
        {
            Read(lab, date, out var capability, out var brand);

            return PriceUsd(lab, capability,
                brand * SimUnits.Finite(standingMultiplier, 1.0), date);
        }

        /// <summary>
        /// How good a lab is understood to be on a date, with its newest release eased in.
        ///
        /// **A release is priced in over weeks, not in a day.** The first version stepped straight
        /// onto the new figure and the rendered chart had a vertical cliff in it: honest data drawn
        /// as something that reads like a rendering fault. Markets re-rate a company over however
        /// long it takes people to agree the new thing is actually better.
        ///
        /// One pass over the catalog rather than `BestPerCompetitorOn`, which allocates a list and
        /// a dictionary every call and is asked for ninety prices each time a chart is drawn.
        /// </summary>
        private static void Read(CompetitorId lab, GameDate date,
            out double capability, out double brand)
        {
            var newest = default(CompetitorRelease);
            var previous = default(CompetitorRelease);
            var found = 0;

            foreach (var entry in CompetitorCatalog.All)
            {
                if (entry.Competitor != lab || !entry.IsLiveOn(date))
                {
                    continue;
                }

                if (found == 0 || entry.ReleaseDate.DayIndex > newest.ReleaseDate.DayIndex)
                {
                    previous = newest;
                    newest = entry;
                    found++;
                }
                else if (found == 1 || entry.ReleaseDate.DayIndex > previous.ReleaseDate.DayIndex)
                {
                    previous = entry;
                    found++;
                }
            }

            if (found == 0)
            {
                capability = 0.0;
                brand = 0.0;
                return;
            }

            var since = date.DayIndex - newest.ReleaseDate.DayIndex;
            var eased = Math.Clamp(since / (double)PricingInDays, 0.0, 1.0);

            // Smoothstep rather than a straight ramp, so the line leaves the old level and arrives
            // at the new one without a corner at either end.
            eased = eased * eased * (3.0 - 2.0 * eased);

            var fromCapability = found > 1 ? previous.Capability : newest.Capability * 0.72;
            var fromBrand = found > 1 ? previous.BrandStrength : newest.BrandStrength * 0.72;

            capability = fromCapability + (newest.Capability - fromCapability) * eased;
            brand = fromBrand + (newest.BrandStrength - fromBrand) * eased;
        }

        /// <summary>What a parcel costs, commission included.</summary>
        public static long CostOf(long shares, double priceUsd) =>
            (long)Math.Ceiling(Math.Max(0L, shares) * Math.Max(0.0, priceUsd)
                * (1.0 + CommissionRate));

        /// <summary>What selling a parcel returns, commission deducted.</summary>
        public static long ProceedsOf(long shares, double priceUsd) =>
            (long)Math.Floor(Math.Max(0L, shares) * Math.Max(0.0, priceUsd)
                * (1.0 - CommissionRate));

        /// <summary>
        /// What the rest of a company would cost once you already hold some of it.
        ///
        /// **A premium, and it climbs with how much you already have.** The last shares are the
        /// dearest, because the people still holding them know exactly how much you need them.
        /// Without that, creeping to 49% and then buying the rest at list price would make a
        /// takeover cheaper than the shares it is made of.
        /// </summary>
        public static long BuyoutCostUsd(CompetitorId lab, long held, double priceUsd)
        {
            var outstanding = SharesOutstanding(lab);
            var remaining = Math.Max(0L, outstanding - held);

            var owned = outstanding > 0 ? held / (double)outstanding : 0.0;
            var premium = 1.25 + 0.85 * Math.Clamp(owned, 0.0, 1.0);

            return (long)Math.Ceiling(remaining * Math.Max(0.0, priceUsd) * premium);
        }
    }
}
