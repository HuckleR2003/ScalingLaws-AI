using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The ONE compute tier library.
    ///
    /// Rough shape of the ladder, for a three year hold on well-utilized hardware:
    ///   rented cloud     highest cost per FLOP, zero capital, cancellable any day
    ///   colocation       around a third of rented, 45 day lead time, rack fee runs while idle
    ///   own datacenter   cheapest power, no rack fee, 300 day build and a facility bill up front
    ///
    /// The gates are deliberately visible from day one. A locked tier still reports its full cost
    /// structure so the player can plan toward it.
    /// </summary>
    public static class ComputeTierCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        private static readonly ComputeTierDefinition[] Entries =
        {
            new(ComputeTier.RentedCloud,
                leadTimeDays: 0,
                capitalPriceMultiplier: 1.0,
                powerCostPerKilowattHourUsd: 0.0,
                housingCostPerKilowattMonthUsd: 0.0,
                maintenanceRatePerYear: 0.0,
                facilityCapexUsd: 0,
                powerCapacityKilowatts: 5_000_000.0,
                requiredCashUsd: 0,
                requiredReleasedModels: 0,
                requiredLifetimeRevenueUsd: 0,
                earliestDate: GameDate.Start),

            new(ComputeTier.ColocatedServers,
                leadTimeDays: 45,
                capitalPriceMultiplier: 1.0,
                powerCostPerKilowattHourUsd: 0.14,
                housingCostPerKilowattMonthUsd: 180.0,
                maintenanceRatePerYear: 0.04,
                facilityCapexUsd: 0,
                powerCapacityKilowatts: 2_500.0,
                requiredCashUsd: 5_000_000,
                requiredReleasedModels: 1,
                requiredLifetimeRevenueUsd: 0,
                earliestDate: GameDate.Start),

            new(ComputeTier.OwnDatacenter,
                leadTimeDays: 300,
                capitalPriceMultiplier: 0.92,
                powerCostPerKilowattHourUsd: 0.055,
                housingCostPerKilowattMonthUsd: 0.0,
                maintenanceRatePerYear: 0.03,
                facilityCapexUsd: 80_000_000,
                powerCapacityKilowatts: 40_000.0,
                requiredCashUsd: 80_000_000,
                requiredReleasedModels: 2,
                requiredLifetimeRevenueUsd: 200_000_000,
                earliestDate: GameDate.FromCalendar(2024, 1, 1))
        };

        private static readonly Dictionary<ComputeTier, ComputeTierDefinition> ByTier = BuildIndex();

        public static IReadOnlyList<ComputeTierDefinition> All => Entries;

        public static ComputeTierDefinition Get(ComputeTier tier)
        {
            if (!ByTier.TryGetValue(tier, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown compute tier.");
            }

            return definition;
        }

        public static bool TryGet(ComputeTier tier, out ComputeTierDefinition definition)
        {
            return ByTier.TryGetValue(tier, out definition);
        }

        /// <summary>
        /// The full ladder with each gate evaluated. Always returns every tier, locked ones included.
        /// </summary>
        public static List<ComputeTierStatus> EvaluateAll(
            GameDate date,
            long cashUsd,
            int releasedModels,
            long lifetimeRevenueUsd)
        {
            var statuses = new List<ComputeTierStatus>(Entries.Length);
            foreach (var entry in Entries)
            {
                statuses.Add(entry.Evaluate(date, cashUsd, releasedModels, lifetimeRevenueUsd));
            }

            return statuses;
        }

        private static Dictionary<ComputeTier, ComputeTierDefinition> BuildIndex()
        {
            var index = new Dictionary<ComputeTier, ComputeTierDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Tier] = entry;
            }

            return index;
        }
    }
}
