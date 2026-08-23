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
            // displayName and description are the English originals the phrase book was built
            // from, and they are deliberately not stored. Loc holds both languages and falls back
            // to English itself, so a second copy here would only be somewhere for the two to
            // disagree. They stay as parameters because they document what each row is.
            _ = displayName;
            _ = description;
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

        /// <summary>
        /// The trait's name, in whatever language the player reads.
        ///
        /// **Read at access time rather than stored**, because the language can change while the
        /// game is running and a name captured when the catalog was built would leave eleven English
        /// headings on an otherwise Polish upgrade screen. These eleven names are the parameters
        /// that screen exists to change, so they are the last place to leave untranslated.
        ///
        /// The English strings above stay as the fallback: `Loc.T` returns them when a key is
        /// missing, so a trait added tomorrow reads correctly before anybody writes its Polish.
        /// </summary>
        public string DisplayName => Loc.T($"trait.{KeyFor(Trait)}.name");

        public string Description => Loc.T($"trait.{KeyFor(Trait)}.desc");

        /// <summary>
        /// The stem of the phrase-book key for a trait.
        ///
        /// Written out rather than derived from the enum name, because four of them do not match:
        /// the enum says `ContextLength`, `Latency`, `ToolUse` and `Multilingual` while the player
        /// reads context, speed, tool use and multilanguage. Deriving would produce keys nobody
        /// would think to look for.
        /// </summary>
        private static string KeyFor(ModelTrait trait) => trait switch
        {
            ModelTrait.Reasoning => "reasoning",
            ModelTrait.Knowledge => "knowledge",
            ModelTrait.Coding => "coding",
            ModelTrait.Multilingual => "multilingual",
            ModelTrait.Multimodal => "multimodal",
            ModelTrait.ContextLength => "context",
            ModelTrait.Safety => "safety",
            ModelTrait.Latency => "latency",
            ModelTrait.Efficiency => "efficiency",
            ModelTrait.ToolUse => "tooluse",
            _ => "ecosystem"
        };

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
