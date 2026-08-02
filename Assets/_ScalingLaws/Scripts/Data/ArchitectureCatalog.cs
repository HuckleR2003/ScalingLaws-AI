using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The ONE architecture library. Pure data plus lookups.
    ///
    /// Calibration note: sparse mixtures trade quality per parameter for a large cut in FLOPs per
    /// token, so on a fixed compute budget they win. Reasoning models buy real capability with a
    /// serving bill that can sink a company whose price per token is already at the market floor.
    /// </summary>
    public static class ArchitectureCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        private static readonly ArchitectureDefinition[] Entries =
        {
            new(ArchitectureId.DenseTransformer, "Dense transformer",
                GameDate.FromCalendar(2022, 1, 1),
                parameterEfficiency: 1.00,
                activeParameterFraction: 1.00,
                trainingEfficiency: 1.00,
                inferenceCostMultiplier: 1.00,
                capabilityBonus: 0.0,
                adoptionCostUsd: 0),

            new(ArchitectureId.EfficientAttention, "Efficient attention",
                GameDate.FromCalendar(2022, 11, 1),
                parameterEfficiency: 1.05,
                activeParameterFraction: 1.00,
                trainingEfficiency: 1.15,
                inferenceCostMultiplier: 0.85,
                capabilityBonus: 0.0,
                adoptionCostUsd: 1_500_000),

            new(ArchitectureId.SparseMixture, "Sparse mixture of experts",
                GameDate.FromCalendar(2023, 12, 1),
                parameterEfficiency: 0.85,
                activeParameterFraction: 0.25,
                trainingEfficiency: 0.90,
                inferenceCostMultiplier: 0.80,
                capabilityBonus: 0.0,
                adoptionCostUsd: 9_000_000),

            new(ArchitectureId.LongContextMixture, "Long context mixture",
                GameDate.FromCalendar(2024, 9, 1),
                parameterEfficiency: 0.90,
                activeParameterFraction: 0.22,
                trainingEfficiency: 0.88,
                inferenceCostMultiplier: 0.95,
                capabilityBonus: 1.5,
                adoptionCostUsd: 22_000_000),

            new(ArchitectureId.ReasoningMixture, "Reasoning mixture",
                GameDate.FromCalendar(2025, 2, 1),
                parameterEfficiency: 0.95,
                activeParameterFraction: 0.22,
                trainingEfficiency: 0.85,
                inferenceCostMultiplier: 2.60,
                capabilityBonus: 6.0,
                adoptionCostUsd: 55_000_000),

            new(ArchitectureId.HybridStateSpace, "Hybrid state space",
                GameDate.FromCalendar(2026, 6, 1),
                parameterEfficiency: 1.10,
                activeParameterFraction: 0.30,
                trainingEfficiency: 1.05,
                inferenceCostMultiplier: 0.70,
                capabilityBonus: 2.0,
                adoptionCostUsd: 90_000_000)
        };

        private static readonly Dictionary<ArchitectureId, ArchitectureDefinition> ById = BuildIndex();

        public static IReadOnlyList<ArchitectureDefinition> All => Entries;

        public static ArchitectureDefinition Get(ArchitectureId id)
        {
            if (!ById.TryGetValue(id, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown architecture.");
            }

            return definition;
        }

        public static bool TryGet(ArchitectureId id, out ArchitectureDefinition definition)
        {
            return ById.TryGetValue(id, out definition);
        }

        public static IEnumerable<ArchitectureDefinition> AvailableOn(GameDate date)
        {
            foreach (var entry in Entries)
            {
                if (entry.IsAvailableOn(date))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>The starting architecture. Always available, always free.</summary>
        public static ArchitectureDefinition Baseline => Get(ArchitectureId.DenseTransformer);

        /// <summary>The six custom slots, in order. Not architectures, just addresses.</summary>
        public static readonly ArchitectureId[] CustomSlots =
        {
            ArchitectureId.CustomFamilyA,
            ArchitectureId.CustomFamilyB,
            ArchitectureId.CustomFamilyC,
            ArchitectureId.CustomFamilyD,
            ArchitectureId.CustomFamilyE,
            ArchitectureId.CustomFamilyF
        };

        public static bool IsCustomSlot(ArchitectureId id) => (int)id >= 1001 && (int)id <= 1006;

        /// <summary>The catalog seen as a source, for anything with no company attached.</summary>
        public static IArchitectureSource AsSource { get; } = new CatalogSource();

        private sealed class CatalogSource : IArchitectureSource
        {
            public bool TryGetArchitecture(ArchitectureId id, out ArchitectureDefinition definition) =>
                TryGet(id, out definition);
        }

        /// <summary>
        /// The best published family on a given date, judged by how much compute it saves. Used as
        /// the ceiling on what in-house research can reach: a lab cannot invent 2026 techniques in
        /// 2022 however much it spends.
        /// </summary>
        public static ArchitectureDefinition FrontierOn(GameDate date)
        {
            var best = Baseline;
            foreach (var entry in Entries)
            {
                if (!entry.IsAvailableOn(date))
                {
                    continue;
                }

                if (entry.ActiveParameterFraction < best.ActiveParameterFraction
                    || entry.CapabilityBonus > best.CapabilityBonus)
                {
                    best = entry;
                }
            }

            return best;
        }

        private static Dictionary<ArchitectureId, ArchitectureDefinition> BuildIndex()
        {
            var index = new Dictionary<ArchitectureId, ArchitectureDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Id] = entry;
            }

            return index;
        }
    }
}
