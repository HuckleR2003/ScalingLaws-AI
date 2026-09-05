using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What one round asks for and what it typically writes. The gates are traction gates: nobody
    /// funds a lab that has shipped nothing, and nobody funds a Series C on a Series A story.
    /// </summary>
    public readonly struct FundingStageDefinition
    {
        public FundingStageDefinition(
            FundingStage stage,
            long targetRaiseUsd,
            double requiredCapabilityRatio,
            long requiredAnnualRevenueUsd,
            int requiredReleasedModels,
            GameDate earliestDate,
            int offerWindowDays)
        {
            Stage = stage;
            TargetRaiseUsd = Math.Clamp(targetRaiseUsd, 0L, 500_000_000_000L);
            RequiredCapabilityRatio = Math.Clamp(SimUnits.Finite(requiredCapabilityRatio), 0.0, 1.5);
            RequiredAnnualRevenueUsd = Math.Max(0L, requiredAnnualRevenueUsd);
            RequiredReleasedModels = Math.Max(0, requiredReleasedModels);
            EarliestDate = earliestDate;
            OfferWindowDays = Math.Clamp(offerWindowDays, 7, 365);
        }

        public FundingStage Stage { get; }

        /// <summary>
        /// Written out rather than built from the enum name, because a key made by concatenation is
        /// invisible to `LocalisationTests.EveryKeyTheInterfaceAsksForExists`.
        /// </summary>
        private static string KeyFor(FundingStage stage) => stage switch
        {
            FundingStage.SeriesA => "funding.stage.a",
            FundingStage.SeriesB => "funding.stage.b",
            FundingStage.SeriesC => "funding.stage.c",
            FundingStage.SeriesD => "funding.stage.d",
            FundingStage.Growth => "funding.stage.growth",
            _ => "funding.stage.ipo"
        };

        /// <summary>Read from the book at access time, never stored. See `PlayerSkillDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Stage));

        /// <summary>What a round of this size normally writes, before the valuation adjusts it.</summary>
        public long TargetRaiseUsd { get; }

        /// <summary>
        /// Capability the company must hold as a fraction of the frontier. This is the gate that
        /// matters: money follows proximity to the frontier, not absolute numbers.
        /// </summary>
        public double RequiredCapabilityRatio { get; }

        public long RequiredAnnualRevenueUsd { get; }
        public int RequiredReleasedModels { get; }
        public GameDate EarliestDate { get; }

        /// <summary>Days the term sheet stays open before it is withdrawn.</summary>
        public int OfferWindowDays { get; }

        public override string ToString() => $"{DisplayName} (~${TargetRaiseUsd / 1_000_000.0:N0}M)";
    }

    /// <summary>
    /// The ONE funding library, plus the sentiment curve that decides what money costs.
    ///
    /// Sentiment is the AI investment cycle, and it is the single biggest lever on the campaign.
    /// Raising in 2022 sells a third of the company for pocket change. Raising in mid 2025 sells
    /// eight percent for a fortune. Same company, same models, four times the valuation, purely
    /// because of when the term sheet was signed. That is the timing lesson applied to capital.
    /// </summary>
    public static class FundingCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        /// <summary>Dollars of valuation per dollar of annual revenue run rate, at neutral sentiment.</summary>
        public const double RevenueMultiple = 20.0;

        /// <summary>Valuation a pre-revenue lab sitting exactly on the frontier can command.</summary>
        public const double FrontierParityValuationUsd = 2_000_000_000.0;

        /// <summary>
        /// How sharply valuation falls away from the frontier. Fourth power: being 80 percent of the
        /// frontier is worth 41 percent of parity, being half is worth 6 percent.
        /// </summary>
        public const double CapabilityValuationExponent = 4.0;

        /// <summary>A round priced below the previous one costs this much extra dilution.</summary>
        public const double DownRoundPenalty = 1.45;

        private static readonly (GameDate Date, double Value)[] SentimentKeyframes =
        {
            (GameDate.FromCalendar(2022, 1, 1), 0.55),
            (GameDate.FromCalendar(2022, 11, 30), 0.70),
            (GameDate.FromCalendar(2023, 6, 1), 1.60),
            (GameDate.FromCalendar(2024, 6, 1), 1.90),
            (GameDate.FromCalendar(2025, 6, 1), 2.20),
            (GameDate.FromCalendar(2026, 6, 1), 1.50),
            (GameDate.FromCalendar(2028, 1, 1), 1.00)
        };

        private static readonly FundingStageDefinition[] Entries =
        {
            new(FundingStage.SeriesA,
                targetRaiseUsd: 25_000_000,
                requiredCapabilityRatio: 0.55,
                requiredAnnualRevenueUsd: 0,
                requiredReleasedModels: 1,
                earliestDate: GameDate.Start,
                offerWindowDays: 90),

            new(FundingStage.SeriesB,
                targetRaiseUsd: 80_000_000,
                requiredCapabilityRatio: 0.65,
                requiredAnnualRevenueUsd: 10_000_000,
                requiredReleasedModels: 1,
                earliestDate: GameDate.Start,
                offerWindowDays: 75),

            new(FundingStage.SeriesC,
                targetRaiseUsd: 250_000_000,
                requiredCapabilityRatio: 0.75,
                requiredAnnualRevenueUsd: 80_000_000,
                requiredReleasedModels: 2,
                earliestDate: GameDate.Start,
                offerWindowDays: 60),

            new(FundingStage.SeriesD,
                targetRaiseUsd: 700_000_000,
                requiredCapabilityRatio: 0.80,
                requiredAnnualRevenueUsd: 300_000_000,
                requiredReleasedModels: 2,
                earliestDate: GameDate.FromCalendar(2023, 6, 1),
                offerWindowDays: 60),

            new(FundingStage.Growth,
                targetRaiseUsd: 2_000_000_000,
                requiredCapabilityRatio: 0.85,
                requiredAnnualRevenueUsd: 1_000_000_000,
                requiredReleasedModels: 3,
                earliestDate: GameDate.FromCalendar(2024, 1, 1),
                offerWindowDays: 45),

            new(FundingStage.PublicOffering,
                targetRaiseUsd: 6_000_000_000,
                requiredCapabilityRatio: 0.85,
                requiredAnnualRevenueUsd: 3_000_000_000,
                requiredReleasedModels: 4,
                earliestDate: GameDate.FromCalendar(2026, 1, 1),
                offerWindowDays: 45)
        };

        private static readonly Dictionary<FundingStage, FundingStageDefinition> ByStage = BuildIndex();

        public static IReadOnlyList<FundingStageDefinition> All => Entries;

        public static FundingStageDefinition Get(FundingStage stage)
        {
            if (!ByStage.TryGetValue(stage, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown funding stage.");
            }

            return definition;
        }

        public static bool TryGet(FundingStage stage, out FundingStageDefinition definition)
        {
            return ByStage.TryGetValue(stage, out definition);
        }

        /// <summary>The round that follows one already closed.</summary>
        public static FundingStage NextStageAfter(FundingStage stage)
        {
            return stage switch
            {
                FundingStage.None => FundingStage.SeriesA,
                FundingStage.Seed => FundingStage.SeriesA,
                FundingStage.SeriesA => FundingStage.SeriesB,
                FundingStage.SeriesB => FundingStage.SeriesC,
                FundingStage.SeriesC => FundingStage.SeriesD,
                FundingStage.SeriesD => FundingStage.Growth,
                FundingStage.Growth => FundingStage.PublicOffering,
                _ => FundingStage.None
            };
        }

        /// <summary>
        /// How much investors are willing to pay for the same company on a given day. Piecewise
        /// linear between keyframes, flat outside.
        /// </summary>
        public static double SentimentOn(GameDate date)
        {
            var first = SentimentKeyframes[0];
            if (date <= first.Date)
            {
                return first.Value;
            }

            for (var index = 1; index < SentimentKeyframes.Length; index++)
            {
                var previous = SentimentKeyframes[index - 1];
                var current = SentimentKeyframes[index];
                if (date > current.Date)
                {
                    continue;
                }

                var span = current.Date.DayIndex - previous.Date.DayIndex;
                if (span <= 0)
                {
                    return current.Value;
                }

                var t = (date.DayIndex - previous.Date.DayIndex) / (double)span;
                return previous.Value + (current.Value - previous.Value) * t;
            }

            return SentimentKeyframes[SentimentKeyframes.Length - 1].Value;
        }

        /// <summary>Plain label for the sentiment number, for the term sheet screen.</summary>
        public static string SentimentLabel(double sentiment)
        {
            return sentiment switch
            {
                >= 2.0 => Loc.T("funding.appetite.frenzied"),
                >= 1.5 => Loc.T("funding.appetite.hot"),
                >= 1.0 => Loc.T("funding.appetite.steady"),
                >= 0.7 => Loc.T("funding.appetite.cautious"),
                _ => Loc.T("funding.appetite.closed")
            };
        }

        private static Dictionary<FundingStage, FundingStageDefinition> BuildIndex()
        {
            var index = new Dictionary<FundingStage, FundingStageDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Stage] = entry;
            }

            return index;
        }
    }
}
