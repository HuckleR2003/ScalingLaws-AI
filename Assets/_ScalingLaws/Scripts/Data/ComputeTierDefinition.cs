using System;
using System.Text;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The cost structure and the gate of one compute tier.
    ///
    /// Renting is expensive per FLOP and free to walk away from. Owning is cheap per FLOP and
    /// impossible to walk away from: the rack fee, the power bill and the depreciation all keep
    /// running on an idle cluster. That asymmetry is the whole point of the tier ladder.
    /// </summary>
    public readonly struct ComputeTierDefinition
    {
        public ComputeTierDefinition(
            ComputeTier tier,
            int leadTimeDays,
            double capitalPriceMultiplier,
            double powerCostPerKilowattHourUsd,
            double housingCostPerKilowattMonthUsd,
            double maintenanceRatePerYear,
            long facilityCapexUsd,
            double powerCapacityKilowatts,
            long requiredCashUsd,
            int requiredReleasedModels,
            long requiredLifetimeRevenueUsd,
            GameDate earliestDate)
        {
            Tier = tier;

            LeadTimeDays = Math.Clamp(leadTimeDays, 0, 1500);
            CapitalPriceMultiplier = Math.Clamp(SimUnits.Finite(capitalPriceMultiplier, 1.0), 0.5, 2.0);
            PowerCostPerKilowattHourUsd = Math.Clamp(SimUnits.Finite(powerCostPerKilowattHourUsd), 0.0, 2.0);
            HousingCostPerKilowattMonthUsd = Math.Clamp(SimUnits.Finite(housingCostPerKilowattMonthUsd), 0.0, 2000.0);
            MaintenanceRatePerYear = Math.Clamp(SimUnits.Finite(maintenanceRatePerYear), 0.0, 0.5);
            FacilityCapexUsd = Math.Clamp(facilityCapexUsd, 0L, 100_000_000_000L);
            PowerCapacityKilowatts = Math.Clamp(SimUnits.Finite(powerCapacityKilowatts), 0.0, 5_000_000.0);
            RequiredCashUsd = Math.Max(0L, requiredCashUsd);
            RequiredReleasedModels = Math.Max(0, requiredReleasedModels);
            RequiredLifetimeRevenueUsd = Math.Max(0L, requiredLifetimeRevenueUsd);
            EarliestDate = earliestDate;
        }

        public ComputeTier Tier { get; }
        /// <summary>
        /// Written out rather than built from the enum name, because a key made by concatenation is
        /// invisible to `LocalisationTests.EveryKeyTheInterfaceAsksForExists`.
        /// </summary>
        private static string KeyFor(ComputeTier tier) => tier switch
        {
            ComputeTier.RentedCloud => "tier.rented",
            ComputeTier.ColocatedServers => "tier.colocated",
            _ => "tier.owned"
        };

        /// <summary>Read from the book at access time, never stored. See `PlayerSkillDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Tier));

        public string Description => Loc.T(KeyFor(Tier) + ".about");

        /// <summary>Days between paying and the hardware producing its first FLOP.</summary>
        public int LeadTimeDays { get; }

        /// <summary>What the company pays against catalog list price. Volume buyers pay less.</summary>
        public double CapitalPriceMultiplier { get; }

        public double PowerCostPerKilowattHourUsd { get; }

        /// <summary>Rack space rent per installed kilowatt per month. Zero once you own the building.</summary>
        public double HousingCostPerKilowattMonthUsd { get; }

        /// <summary>Yearly maintenance as a share of capital spent. Failed accelerators are not free.</summary>
        public double MaintenanceRatePerYear { get; }

        /// <summary>One-off cost of the facility itself, before a single accelerator is bought.</summary>
        public long FacilityCapexUsd { get; }

        /// <summary>Installed power the tier can deliver. The real ceiling on a large cluster.</summary>
        public double PowerCapacityKilowatts { get; }

        public long RequiredCashUsd { get; }
        public int RequiredReleasedModels { get; }
        public long RequiredLifetimeRevenueUsd { get; }
        public GameDate EarliestDate { get; }

        public bool IsRented => Tier == ComputeTier.RentedCloud;

        /// <summary>
        /// Checks the gate and, when it is shut, says exactly what is missing. The reason string is
        /// built from unmet requirements only, so it never tells the player to do something they
        /// have already done.
        /// </summary>
        public ComputeTierStatus Evaluate(GameDate date, long cashUsd, int releasedModels, long lifetimeRevenueUsd)
        {
            var missing = new StringBuilder();

            if (date.IsBefore(EarliestDate))
            {
                Append(missing, $"not before {EarliestDate}");
            }

            if (cashUsd < RequiredCashUsd)
            {
                Append(missing, $"cash {FormatUsd(RequiredCashUsd)} (have {FormatUsd(cashUsd)})");
            }

            if (releasedModels < RequiredReleasedModels)
            {
                Append(missing, $"{RequiredReleasedModels} released model(s) (have {releasedModels})");
            }

            if (lifetimeRevenueUsd < RequiredLifetimeRevenueUsd)
            {
                Append(missing, $"lifetime revenue {FormatUsd(RequiredLifetimeRevenueUsd)} (have {FormatUsd(lifetimeRevenueUsd)})");
            }

            return missing.Length == 0
                ? ComputeTierStatus.Unlocked(Tier)
                : new ComputeTierStatus(Tier, false, $"Needs {missing}.");
        }

        private static void Append(StringBuilder builder, string requirement)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(requirement);
        }

        private static string FormatUsd(long amount)
        {
            return amount switch
            {
                >= 1_000_000_000 => $"${amount / 1_000_000_000.0:0.##}B",
                >= 1_000_000 => $"${amount / 1_000_000.0:0.##}M",
                >= 1_000 => $"${amount / 1_000.0:0.##}k",
                _ => $"${amount}"
            };
        }

        public override string ToString() => $"{DisplayName} (lead {LeadTimeDays}d)";
    }
}
