using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The ONE data library, plus the blending rule.
    ///
    /// Blending draws from the best corpus first and works down until the run has the tokens it
    /// asked for. That is why a small licensed archive still helps a huge run: it fills the top of
    /// the mix. It is also why bolting raw crawl onto a clean mix drags the average down.
    /// </summary>
    public static class DatasetCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        private static readonly DatasetSourceDefinition[] Entries =
        {
            // The words are `corpus.*` in the phrase book.
            new(DatasetSource.WebCrawl,
                GameDate.FromCalendar(2022, 1, 1),
                tokenSupplyBillions: 1_800,
                qualityMultiplier: 0.80,
                acquisitionCostUsd: 0,
                requiredOwnedCapability: 0),

            new(DatasetSource.CuratedWeb,
                GameDate.FromCalendar(2022, 1, 1),
                tokenSupplyBillions: 3_000,
                qualityMultiplier: 1.00,
                acquisitionCostUsd: 2_000_000,
                requiredOwnedCapability: 0),

            new(DatasetSource.CodeCorpus,
                GameDate.FromCalendar(2022, 1, 1),
                tokenSupplyBillions: 800,
                qualityMultiplier: 1.10,
                acquisitionCostUsd: 3_000_000,
                requiredOwnedCapability: 0),

            new(DatasetSource.HumanFeedback,
                GameDate.FromCalendar(2022, 6, 1),
                tokenSupplyBillions: 60,
                qualityMultiplier: 1.30,
                acquisitionCostUsd: 20_000_000,
                requiredOwnedCapability: 0),

            new(DatasetSource.LicensedBooks,
                GameDate.FromCalendar(2023, 3, 1),
                tokenSupplyBillions: 400,
                qualityMultiplier: 1.18,
                acquisitionCostUsd: 12_000_000,
                requiredOwnedCapability: 0),

            new(DatasetSource.AcademicArchive,
                GameDate.FromCalendar(2023, 9, 1),
                tokenSupplyBillions: 300,
                qualityMultiplier: 1.15,
                acquisitionCostUsd: 8_000_000,
                requiredOwnedCapability: 0),

            // The volume unlock, and the reason mid-game runs stop being data limited. It needs a
            // model good enough to generate usable text, so it cannot carry a company that has not
            // shipped anything yet.
            new(DatasetSource.Synthetic,
                GameDate.FromCalendar(2024, 6, 1),
                tokenSupplyBillions: 20_000,
                qualityMultiplier: 0.95,
                acquisitionCostUsd: 6_000_000,
                requiredOwnedCapability: 40),

            new(DatasetSource.VideoAndAudio,
                GameDate.FromCalendar(2025, 1, 1),
                tokenSupplyBillions: 5_000,
                qualityMultiplier: 1.05,
                acquisitionCostUsd: 25_000_000,
                requiredOwnedCapability: 0)
        };

        private static readonly Dictionary<DatasetSource, DatasetSourceDefinition> ByFlag = BuildIndex();

        public static IReadOnlyList<DatasetSourceDefinition> All => Entries;

        /// <summary>The mix a brand new company starts with: whatever it can scrape for free.</summary>
        public const DatasetSource StartingSources = DatasetSource.WebCrawl;

        public static bool TryGet(DatasetSource flag, out DatasetSourceDefinition definition)
        {
            return ByFlag.TryGetValue(flag, out definition);
        }

        public static DatasetSourceDefinition Get(DatasetSource flag)
        {
            if (!ByFlag.TryGetValue(flag, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(flag), flag, "Unknown dataset source.");
            }

            return definition;
        }

        public static IEnumerable<DatasetSourceDefinition> AvailableOn(GameDate date, double bestOwnedCapability)
        {
            foreach (var entry in Entries)
            {
                if (entry.IsAvailableOn(date, bestOwnedCapability))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>Total licensing bill for a set of sources, ignoring availability.</summary>
        public static long TotalAcquisitionCost(DatasetSource mask)
        {
            var total = 0L;
            foreach (var entry in Entries)
            {
                if ((mask & entry.Flag) != 0)
                {
                    total += entry.AcquisitionCostUsd;
                }
            }

            return total;
        }

        /// <summary>
        /// Pours the owned corpora into one run. Sources unavailable on the date, or gated behind a
        /// capability the company has not reached, are silently skipped: they are not owned yet.
        /// </summary>
        public static DatasetBlend Blend(
            DatasetSource mask,
            double requestedTokensBillions,
            GameDate date,
            double bestOwnedCapability,
            double supplyMultiplier = 1.0)
        {
            var supplyScale = Math.Clamp(SimUnits.Finite(supplyMultiplier, 1.0), 0.5, 3.0);
            var requested = Math.Max(0.0, SimUnits.Finite(requestedTokensBillions));
            if (mask == DatasetSource.None || requested <= 0.0)
            {
                return DatasetBlend.Empty;
            }

            var usable = new List<DatasetSourceDefinition>();
            var cost = 0L;
            var totalSupply = 0.0;
            foreach (var entry in Entries)
            {
                if ((mask & entry.Flag) == 0 || !entry.IsAvailableOn(date, bestOwnedCapability))
                {
                    continue;
                }

                usable.Add(entry);
                cost += entry.AcquisitionCostUsd;
                totalSupply += entry.TokenSupplyBillions * supplyScale;
            }

            if (usable.Count == 0)
            {
                return DatasetBlend.Empty;
            }

            // Best tokens first: that is what a real data team does with a fixed token budget.
            usable.Sort(static (left, right) => right.QualityMultiplier.CompareTo(left.QualityMultiplier));

            var remaining = requested;
            var drawn = 0.0;
            var weightedQuality = 0.0;
            foreach (var entry in usable)
            {
                if (remaining <= 0.0)
                {
                    break;
                }

                var take = Math.Min(remaining, entry.TokenSupplyBillions * supplyScale);
                weightedQuality += take * entry.QualityMultiplier;
                drawn += take;
                remaining -= take;
            }

            var quality = drawn > 0.0 ? weightedQuality / drawn : 1.0;
            return new DatasetBlend(
                Math.Min(requested, totalSupply),
                quality,
                cost,
                usable.Count,
                totalSupply >= requested);
        }

        private static Dictionary<DatasetSource, DatasetSourceDefinition> BuildIndex()
        {
            var index = new Dictionary<DatasetSource, DatasetSourceDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Flag] = entry;
            }

            return index;
        }
    }
}
