using System;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What one upgradeable trait costs and what it buys. Four separate effects, so no trait is
    /// simply better than another:
    ///   CapabilityPerLevel  raw quality, the thing benchmarks measure
    ///   BrandPerLevel       whether buyers pick you for reasons other than quality
    ///   EfficiencyPerLevel  multiplicative cut in what a served token costs
    ///   LatencyPerLevel     multiplicative cut in response time, which buyers feel directly
    /// </summary>
    public readonly struct ModelTraitDefinition
    {
        public ModelTraitDefinition(
            ModelTrait trait,
            string displayName,
            string description,
            GameDate availableFrom,
            double capabilityPerLevel,
            double brandPerLevel,
            double efficiencyPerLevel,
            long baseUpgradeCostUsd,
            double costGrowthPerLevel,
            double petaflopDaysPerLevel,
            int daysPerLevel,
            int expectationRiseDays)
        {
            Trait = trait;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? trait.ToString() : displayName;
            Description = description ?? string.Empty;
            AvailableFrom = availableFrom;
            CapabilityPerLevel = Math.Clamp(SimUnits.Finite(capabilityPerLevel), 0.0, 5.0);
            BrandPerLevel = Math.Clamp(SimUnits.Finite(brandPerLevel), 0.0, 0.2);
            EfficiencyPerLevel = Math.Clamp(SimUnits.Finite(efficiencyPerLevel), 0.0, 0.3);
            BaseUpgradeCostUsd = Math.Clamp(baseUpgradeCostUsd, 0L, 10_000_000_000L);
            CostGrowthPerLevel = Math.Clamp(SimUnits.Finite(costGrowthPerLevel, 1.5), 1.0, 4.0);
            PetaflopDaysPerLevel = Math.Max(0.0, SimUnits.Finite(petaflopDaysPerLevel));
            DaysPerLevel = Math.Clamp(daysPerLevel, 1, 400);
            ExpectationRiseDays = Math.Clamp(expectationRiseDays, 30, 2000);
        }

        public ModelTrait Trait { get; }
        public string DisplayName { get; }
        public string Description { get; }

        /// <summary>Nobody can build this before the field knows how.</summary>
        public GameDate AvailableFrom { get; }

        public double CapabilityPerLevel { get; }
        public double BrandPerLevel { get; }

        /// <summary>Share of serving cost removed per level, applied multiplicatively.</summary>
        public double EfficiencyPerLevel { get; }

        public long BaseUpgradeCostUsd { get; }
        public double CostGrowthPerLevel { get; }

        /// <summary>Post-training compute one level consumes. Upgrades compete with the next run.</summary>
        public double PetaflopDaysPerLevel { get; }

        public int DaysPerLevel { get; }

        /// <summary>
        /// How fast the market comes to expect another level of this as table stakes. Short means
        /// the trait rots quickly and has to be revisited. Efficiency is the shortest, which is why
        /// a company that never optimises falls behind even while its capability climbs.
        /// </summary>
        public int ExpectationRiseDays { get; }

        public bool IsAvailableOn(GameDate date) => date.IsOnOrAfter(AvailableFrom);

        /// <summary>Cash to go from <paramref name="currentLevel"/> to the next one.</summary>
        public long UpgradeCostUsd(int currentLevel)
        {
            var level = Math.Clamp(currentLevel, 0, ModelTraitSetLimits.MaximumLevel);
            return SimUnits.ToDollars(BaseUpgradeCostUsd * Math.Pow(CostGrowthPerLevel, level));
        }

        /// <summary>Compute the next level consumes. Grows more slowly than the cash bill.</summary>
        public double UpgradePetaflopDays(int currentLevel)
        {
            var level = Math.Clamp(currentLevel, 0, ModelTraitSetLimits.MaximumLevel);
            return PetaflopDaysPerLevel * Math.Pow(1.35, level);
        }

        public int UpgradeDays(int currentLevel)
        {
            var level = Math.Clamp(currentLevel, 0, ModelTraitSetLimits.MaximumLevel);
            return (int)Math.Round(DaysPerLevel * Math.Pow(1.15, level));
        }

        /// <summary>
        /// The level buyers treat as normal on a given day. Being under it is a penalty, not a
        /// missed bonus: this is the mechanism that makes periodic optimisation compulsory rather
        /// than optional.
        /// </summary>
        public int ExpectedLevelOn(GameDate date)
        {
            if (!IsAvailableOn(date))
            {
                return 0;
            }

            var elapsed = date.DayIndex - AvailableFrom.DayIndex;
            return Math.Clamp(elapsed / ExpectationRiseDays, 0, ModelTraitSetLimits.MaximumLevel);
        }

        public override string ToString() => $"{DisplayName} (from {AvailableFrom})";
    }

    /// <summary>Shared limits, kept out of the struct so the struct can reference them in clamps.</summary>
    public static class ModelTraitSetLimits
    {
        public const int MaximumLevel = 12;
        public const int TraitCount = 11;
    }
}
