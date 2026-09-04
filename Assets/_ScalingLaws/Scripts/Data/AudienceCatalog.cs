using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Who is actually buying tokens. The market is not one crowd.</summary>
    public enum AudienceSegment
    {
        None = 0,

        /// <summary>People asking questions. Enormous, price sensitive, mostly on the free tier.</summary>
        Consumer = 1,

        /// <summary>Engineers. Small in 2022, and the first segment to discover it would pay.</summary>
        Developer = 2,

        /// <summary>Companies replacing back office work. Slow to arrive, hardest to lose.</summary>
        Enterprise = 3,

        /// <summary>Writing, images, marketing copy. Steady and never large.</summary>
        Creative = 4,

        /// <summary>Models given a machine and left to run it. Does not exist yet in 2022.</summary>
        Agentic = 5
    }

    /// <summary>
    /// One segment: how many of them there are over time, and what they will pay.
    ///
    /// Weights are raw and normalised at read time rather than hand balanced to sum to one. Balancing
    /// five curves by hand across fifteen years is the kind of arithmetic that quietly stops summing
    /// after the third edit, and a market that sums to 0.93 is a market where nine percent of demand
    /// silently does not exist.
    /// </summary>
    public sealed class AudienceSegmentDefinition
    {
        public AudienceSegmentDefinition(AudienceSegment segment,
            double willingnessToPay, double adoptionRatePerDay, double brandWeight,
            double servingCostWeight, double tokensPerUserPerDay,
            (int Year, double Weight)[] anchors,
            double reservationCapability = 0.0, double intensityGrowthPerYear = 1.0)
        {
            TokensPerUserPerDay = Math.Max(1.0, tokensPerUserPerDay);
            AdoptionRatePerDay = Math.Clamp(adoptionRatePerDay, 0.002, 0.5);
            BrandWeight = Math.Clamp(brandWeight, 0.0, 2.5);
            ReservationCapability = Math.Clamp(reservationCapability, 0.0, 100.0);
            IntensityGrowthPerYear = Math.Clamp(intensityGrowthPerYear, 1.0, 2.0);
            ServingCostWeight = Math.Clamp(servingCostWeight, 0.0, 2.0);
            Segment = segment;

            WillingnessToPay = Math.Clamp(willingnessToPay, 0.25, 4.0);
            Anchors = anchors ?? Array.Empty<(int, double)>();
        }

        public AudienceSegment Segment { get; }
        private static string KeyFor(AudienceSegment segment) => segment switch
        {
            AudienceSegment.Developer => "audience.developer",
            AudienceSegment.Enterprise => "audience.enterprise",
            AudienceSegment.Creative => "audience.creative",
            AudienceSegment.Agentic => "audience.agentic",
            _ => "audience.consumer"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Segment));
        public string Description => Loc.T(KeyFor(Segment) + ".desc");

        /// <summary>
        /// How much more than the baseline this segment will pay before it walks. A developer paying
        /// 1.2 does not mean they are charged more; it means a price that loses half the consumers
        /// loses far fewer of them.
        /// </summary>
        public double WillingnessToPay { get; }

        /// <summary>
        /// How much of the gap to its preferred product a segment closes in one day.
        ///
        /// This is the switching friction, and it is the only thing that makes segments behave
        /// differently in kind rather than in degree. Developers re-evaluate in weeks. An enterprise
        /// contract does not care what shipped on Tuesday and will not care for a year.
        /// </summary>
        public double AdoptionRatePerDay { get; }

        /// <summary>How much this segment cares who built it. Consumers care most, developers least.</summary>
        public double BrandWeight { get; }

        /// <summary>How much an expensive-to-serve model is punished here. Support buyers notice.</summary>
        public double ServingCostWeight { get; }

        /// <summary>
        /// Tokens one person in this audience gets through in a day.
        ///
        /// This is what turns a token pool into a number of people, and it is the reason a developer
        /// is not worth the same as a consumer. Somebody asking a few questions a day and an agent
        /// running unsupervised for hours are both "a user", and they are two orders of magnitude
        /// apart. Counting them as one thing would make the whole audience system lie.
        /// </summary>
        public double TokensPerUserPerDay { get; }

        /// <summary>
        /// How many people that many tokens represents. The pool is in billions per day, which is
        /// the unit the rest of the market speaks, so the conversion lives here rather than at every
        /// call site where it could be got wrong once and never noticed.
        /// </summary>
        public double UsersFor(double billionTokensPerDay) =>
            Math.Max(0.0, billionTokensPerDay) * SimUnits.TokensPerBillion / TokensPerUserPerDay;

        /// <summary>
        /// How many people that much demand represents in a given year. This is the one the game
        /// uses; the year free version above is the 2022 baseline and is kept for the curve tests.
        /// </summary>
        public double UsersFor(double billionTokensPerDay, int year) =>
            Math.Max(0.0, billionTokensPerDay) * SimUnits.TokensPerBillion
            / Math.Max(1.0, IntensityIn(year));

        /// <summary>Raw size at each anchor year. Read through <see cref="AudienceCatalog"/>.</summary>
        public (int Year, double Weight)[] Anchors { get; }

        /// <summary>
        /// How good a model has to be before this audience considers it worth using at all.
        ///
        /// This is the outside option, and it is the reason a market can be partly unserved. An
        /// enterprise in 2022 was not choosing between vendors, it was choosing not to buy, and no
        /// amount of price cutting moved it. Scored on the same capability scale as everything else,
        /// so the bar and the products are always comparable.
        /// </summary>
        public double ReservationCapability { get; }

        /// <summary>
        /// How much more one of these people gets through each year.
        ///
        /// Without this, users are token demand divided by a constant, and since demand grows by
        /// orders of magnitude across the game the user count grows with it. That produced sixty two
        /// billion users by 2035, which is not a number to show anybody. People do not merely arrive,
        /// they also use it far more heavily than they did, and that is most of the growth in tokens.
        /// </summary>
        public double IntensityGrowthPerYear { get; }

        /// <summary>Tokens one of these people gets through per day in a given year.</summary>
        public double IntensityIn(int year) =>
            TokensPerUserPerDay * Math.Pow(IntensityGrowthPerYear, Math.Max(0, year - 2022));

        /// <summary>
        /// Size in the given year, interpolated between anchors and held flat outside them. Linear
        /// between anchors on purpose: the anchors are close enough together that a smoother curve
        /// would be inventing detail nobody can defend.
        /// </summary>
        public double WeightIn(int year)
        {
            if (Anchors.Length == 0)
            {
                return 0.0;
            }

            if (year <= Anchors[0].Year)
            {
                return Anchors[0].Weight;
            }

            for (var index = 1; index < Anchors.Length; index++)
            {
                if (year > Anchors[index].Year)
                {
                    continue;
                }

                var (fromYear, fromWeight) = Anchors[index - 1];
                var (toYear, toWeight) = Anchors[index];
                var span = Math.Max(1, toYear - fromYear);
                var t = (year - fromYear) / (double)span;
                return fromWeight + (toWeight - fromWeight) * t;
            }

            return Anchors[^1].Weight;
        }

        public override string ToString() => $"{DisplayName} (pays {WillingnessToPay:0.00}x)";
    }

    /// <summary>
    /// The ONE audience library. Who exists, when, and what they are worth.
    ///
    /// The shape of these curves is the reason model type is a timing decision rather than a
    /// preference. Coding is a rounding error in 2022 and the largest paying segment by 2025.
    /// Agentic work does not exist until the models can hold a task down, and then it takes over.
    /// A player who researches the agent type in 2023 has bought a market that has no customers in
    /// it, which is the same mistake as buying accelerators a year early.
    ///
    /// Numbers are shaped from public adoption history through 2026 and are projections after that.
    /// They are deliberately coarse. Nothing here should be read as a forecast.
    /// </summary>
    public static class AudienceCatalog
    {
        public const string CatalogVersion = "audience-2026-08-11";

        /// <summary>Last year the curves say anything. Past this the final anchor holds.</summary>
        public const int HorizonYear = 2036;

        private static readonly AudienceSegmentDefinition[] Entries =
        {
            new(AudienceSegment.Consumer, willingnessToPay: 1.00, adoptionRatePerDay: 0.045, brandWeight: 1.35,
                servingCostWeight: 0.55, tokensPerUserPerDay: 12_000,
                new (int, double)[]
                {
                    (2022, 30), (2023, 62), (2024, 74), (2026, 82),
                    (2029, 88), (2032, 92), (2036, 95)
                },
                reservationCapability: 6.0, intensityGrowthPerYear: 1.28),

            new(AudienceSegment.Developer, willingnessToPay: 1.20, adoptionRatePerDay: 0.090, brandWeight: 0.55,
                servingCostWeight: 0.35, tokensPerUserPerDay: 180_000,
                new (int, double)[]
                {
                    (2022, 4), (2023, 14), (2024, 26), (2025, 34), (2027, 40),
                    (2030, 42), (2036, 44)
                },
                reservationCapability: 11.0, intensityGrowthPerYear: 1.32),

            new(AudienceSegment.Enterprise, willingnessToPay: 1.65, adoptionRatePerDay: 0.012, brandWeight: 1.15,
                servingCostWeight: 0.25, tokensPerUserPerDay: 900_000,
                new (int, double)[]
                {
                    (2022, 3), (2024, 9), (2026, 20), (2028, 30), (2031, 38), (2036, 44)
                },
                reservationCapability: 26.0, intensityGrowthPerYear: 1.24),

            new(AudienceSegment.Creative, willingnessToPay: 1.05, adoptionRatePerDay: 0.060, brandWeight: 1.00,
                servingCostWeight: 0.85, tokensPerUserPerDay: 40_000,
                new (int, double)[]
                {
                    (2022, 6), (2023, 12), (2025, 16), (2028, 17), (2036, 18)
                },
                reservationCapability: 9.0, intensityGrowthPerYear: 1.26),

            new(AudienceSegment.Agentic, willingnessToPay: 2.10, adoptionRatePerDay: 0.030, brandWeight: 0.70,
                servingCostWeight: 1.30, tokensPerUserPerDay: 3_000_000,
                new (int, double)[]
                {
                    (2022, 0), (2024, 1), (2026, 6), (2028, 18), (2030, 34), (2033, 52), (2036, 64)
                },
                reservationCapability: 44.0, intensityGrowthPerYear: 1.18)
        };

        private static readonly Dictionary<AudienceSegment, AudienceSegmentDefinition> BySegment = BuildIndex();

        public static IReadOnlyList<AudienceSegmentDefinition> All => Entries;

        public static AudienceSegmentDefinition Get(AudienceSegment segment) =>
            BySegment.TryGetValue(segment, out var found) ? found : Entries[0];

        /// <summary>
        /// Each segment's share of the market on a date, summing to exactly one. This is the only
        /// way shares should ever be read: the raw weights are sizes, not fractions.
        /// </summary>
        public static double ShareOf(AudienceSegment segment, GameDate date)
        {
            var year = date.Year;
            var total = 0.0;

            foreach (var entry in Entries)
            {
                total += entry.WeightIn(year);
            }

            return total <= 0.0 ? 0.0 : Get(segment).WeightIn(year) / total;
        }

        /// <summary>Every share on a date, in catalog order. One pass instead of five.</summary>
        public static double[] SharesOn(GameDate date)
        {
            var year = date.Year;
            var shares = new double[Entries.Length];
            var total = 0.0;

            for (var index = 0; index < Entries.Length; index++)
            {
                shares[index] = Entries[index].WeightIn(year);
                total += shares[index];
            }

            if (total <= 0.0)
            {
                return shares;
            }

            for (var index = 0; index < shares.Length; index++)
            {
                shares[index] /= total;
            }

            return shares;
        }

        /// <summary>
        /// How fast the whole market is growing relative to 2022. Segment shares say who is buying;
        /// this says how many of them there are, and the two are separate on purpose.
        /// </summary>
        /// <summary>
        /// What one person, averaged across the whole market, gets through in a day.
        ///
        /// Weighted by how large each audience is on the date, because an average that ignored the
        /// mix would drift as the market reshapes itself even with every audience unchanged.
        /// </summary>
        public static double AverageTokensPerUserPerDay(GameDate date)
        {
            var shares = SharesOn(date);
            var total = 0.0;

            for (var index = 0; index < Entries.Length && index < shares.Length; index++)
            {
                total += shares[index] * Entries[index].IntensityIn(date.Year);
            }

            return Math.Max(1.0, total);
        }

        public static double MarketSizeIndex(GameDate date)
        {
            var total = 0.0;
            foreach (var entry in Entries)
            {
                total += entry.WeightIn(date.Year);
            }

            var baseline = 0.0;
            foreach (var entry in Entries)
            {
                baseline += entry.WeightIn(2022);
            }

            return baseline <= 0.0 ? 1.0 : total / baseline;
        }

        private static Dictionary<AudienceSegment, AudienceSegmentDefinition> BuildIndex()
        {
            var map = new Dictionary<AudienceSegment, AudienceSegmentDefinition>();
            foreach (var entry in Entries)
            {
                map[entry.Segment] = entry;
            }

            return map;
        }
    }
}
