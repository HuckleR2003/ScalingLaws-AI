using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The ONE trait library. Pure data plus lookups.
    ///
    /// Read the ExpectationRiseDays column as "how fast this rots". Efficiency and Latency rot
    /// fastest, which is deliberate: a company can lead on raw capability and still be losing share
    /// because it has not optimised in a year. Ecosystem rots slowest, so it compounds.
    /// </summary>
    public static class ModelTraitCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        private static readonly ModelTraitDefinition[] Entries =
        {
            new(ModelTrait.Reasoning, "Reasoning",
                "Multi-step problems. The most expensive points on the board and the ones buyers notice first.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.90, brandPerLevel: 0.004, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 1_800_000, costGrowthPerLevel: 1.62,
                petaflopDaysPerLevel: 220, daysPerLevel: 18, expectationRiseDays: 260),

            new(ModelTrait.Knowledge, "Knowledge",
                "Breadth of recall. Cheap to buy, and the first thing a rival matches.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.50, brandPerLevel: 0.002, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 900_000, costGrowthPerLevel: 1.48,
                petaflopDaysPerLevel: 140, daysPerLevel: 12, expectationRiseDays: 220),

            new(ModelTrait.Coding, "Coding",
                "Code generation and repair. The single highest paying segment in the whole market.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.70, brandPerLevel: 0.006, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 1_400_000, costGrowthPerLevel: 1.55,
                petaflopDaysPerLevel: 180, daysPerLevel: 15, expectationRiseDays: 200),

            new(ModelTrait.Multilingual, "Multilanguage",
                "Languages beyond English. Opens regions rather than raising the ceiling.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.30, brandPerLevel: 0.009, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 700_000, costGrowthPerLevel: 1.42,
                petaflopDaysPerLevel: 110, daysPerLevel: 14, expectationRiseDays: 300),

            new(ModelTrait.Multimodal, "Multimodal",
                "Images, audio and video in and out. Expensive to train, and buyers assume it by 2025.",
                GameDate.FromCalendar(2023, 3, 1),
                capabilityPerLevel: 0.40, brandPerLevel: 0.011, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 2_600_000, costGrowthPerLevel: 1.58,
                petaflopDaysPerLevel: 320, daysPerLevel: 24, expectationRiseDays: 240),

            new(ModelTrait.ContextLength, "Context length",
                "How much the model can hold at once. Sells to enterprises, costs memory to serve.",
                GameDate.FromCalendar(2022, 6, 1),
                capabilityPerLevel: 0.30, brandPerLevel: 0.010, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 1_100_000, costGrowthPerLevel: 1.50,
                petaflopDaysPerLevel: 130, daysPerLevel: 16, expectationRiseDays: 210),

            new(ModelTrait.Safety, "Safety",
                "Refusals that land in the right place. Invisible when it works. The only defence against an incident.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.20, brandPerLevel: 0.014, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 1_200_000, costGrowthPerLevel: 1.45,
                petaflopDaysPerLevel: 90, daysPerLevel: 20, expectationRiseDays: 190),

            new(ModelTrait.Latency, "Speed",
                "Time to first token. Buyers feel this before they read any benchmark.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.0, brandPerLevel: 0.018, efficiencyPerLevel: 0.03,
                baseUpgradeCostUsd: 800_000, costGrowthPerLevel: 1.44,
                petaflopDaysPerLevel: 70, daysPerLevel: 11, expectationRiseDays: 150),

            new(ModelTrait.Efficiency, "Optimisation",
                "Cost per served token. Buys no headlines and decides whether the company survives a price war.",
                GameDate.FromCalendar(2022, 1, 1),
                capabilityPerLevel: 0.0, brandPerLevel: 0.002, efficiencyPerLevel: 0.075,
                baseUpgradeCostUsd: 1_000_000, costGrowthPerLevel: 1.40,
                petaflopDaysPerLevel: 100, daysPerLevel: 13, expectationRiseDays: 140),

            new(ModelTrait.ToolUse, "Tool use",
                "Calling things that are not the model. The whole agent market runs on this.",
                GameDate.FromCalendar(2023, 6, 1),
                capabilityPerLevel: 0.60, brandPerLevel: 0.012, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 2_000_000, costGrowthPerLevel: 1.56,
                petaflopDaysPerLevel: 200, daysPerLevel: 19, expectationRiseDays: 180),

            new(ModelTrait.Ecosystem, "Ecosystem",
                "SDKs, integrations, everyone else building on top of you. Slow to grow and slow to lose.",
                GameDate.FromCalendar(2022, 3, 1),
                capabilityPerLevel: 0.0, brandPerLevel: 0.030, efficiencyPerLevel: 0.0,
                baseUpgradeCostUsd: 1_600_000, costGrowthPerLevel: 1.52,
                petaflopDaysPerLevel: 0, daysPerLevel: 30, expectationRiseDays: 420)
        };

        private static readonly Dictionary<ModelTrait, ModelTraitDefinition> ByTrait = BuildIndex();

        public static IReadOnlyList<ModelTraitDefinition> All => Entries;

        public static ModelTraitDefinition Get(ModelTrait trait)
        {
            if (!ByTrait.TryGetValue(trait, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(trait), trait, "Unknown model trait.");
            }

            return definition;
        }

        public static bool TryGet(ModelTrait trait, out ModelTraitDefinition definition)
        {
            return ByTrait.TryGetValue(trait, out definition);
        }

        public static IEnumerable<ModelTraitDefinition> AvailableOn(GameDate date)
        {
            foreach (var entry in Entries)
            {
                if (entry.IsAvailableOn(date))
                {
                    yield return entry;
                }
            }
        }

        private static Dictionary<ModelTrait, ModelTraitDefinition> BuildIndex()
        {
            var index = new Dictionary<ModelTrait, ModelTraitDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Trait] = entry;
            }

            return index;
        }
    }
}
