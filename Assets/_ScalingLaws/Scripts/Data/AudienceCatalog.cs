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
        public AudienceSegmentDefinition(AudienceSegment segment, string displayName, string description,
            double willingnessToPay, (int Year, double Weight)[] anchors)
        {
            Segment = segment;
            DisplayName = displayName ?? segment.ToString();
            Description = description ?? string.Empty;
            WillingnessToPay = Math.Clamp(willingnessToPay, 0.25, 4.0);
            Anchors = anchors ?? Array.Empty<(int, double)>();
        }

        public AudienceSegment Segment { get; }
        public string DisplayName { get; }
        public string Description { get; }

        /// <summary>
        /// How much more than the baseline this segment will pay before it walks. A developer paying
        /// 1.2 does not mean they are charged more; it means a price that loses half the consumers
        /// loses far fewer of them.
        /// </summary>
        public double WillingnessToPay { get; }

        /// <summary>Raw size at each anchor year. Read through <see cref="AudienceCatalog"/>.</summary>
        public (int Year, double Weight)[] Anchors { get; }

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
        public const string CatalogVersion = "audience-2026-08-04";

        /// <summary>Last year the curves say anything. Past this the final anchor holds.</summary>
        public const int HorizonYear = 2036;

        private static readonly AudienceSegmentDefinition[] Entries =
        {
            new(AudienceSegment.Consumer, "Consumer",
                "People asking questions. The largest crowd by a distance and the least willing to pay "
                + "for any of it.",
                willingnessToPay: 1.00,
                new (int, double)[]
                {
                    (2022, 30), (2023, 62), (2024, 74), (2026, 82),
                    (2029, 88), (2032, 92), (2036, 95)
                }),

            new(AudienceSegment.Developer, "Developers",
                "Engineers writing code. Almost nobody in 2022, and the first group to find out it "
                + "would happily pay.",
                willingnessToPay: 1.20,
                new (int, double)[]
                {
                    (2022, 4), (2023, 14), (2024, 26), (2025, 34), (2027, 40),
                    (2030, 42), (2036, 44)
                }),

            new(AudienceSegment.Enterprise, "Enterprise",
                "Companies replacing work that used to be done by a department. Slow to arrive, "
                + "expensive to win, and almost impossible to lose once won.",
                willingnessToPay: 1.65,
                new (int, double)[]
                {
                    (2022, 3), (2024, 9), (2026, 20), (2028, 30), (2031, 38), (2036, 44)
                }),

            new(AudienceSegment.Creative, "Creative",
                "Writing, images and marketing. Arrived early, never grew the way the others did.",
                willingnessToPay: 1.05,
                new (int, double)[]
                {
                    (2022, 6), (2023, 12), (2025, 16), (2028, 17), (2036, 18)
                }),

            new(AudienceSegment.Agentic, "Autonomous",
                "Models given a machine and a task and left alone with both. Does not exist until a "
                + "model can hold a job down for an hour without supervision.",
                willingnessToPay: 2.10,
                new (int, double)[]
                {
                    (2022, 0), (2024, 1), (2026, 6), (2028, 18), (2030, 34), (2033, 52), (2036, 64)
                })
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
