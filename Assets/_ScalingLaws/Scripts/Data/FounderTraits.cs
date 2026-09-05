using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Who the founder is. Two are chosen at the start of a campaign and they never change.
    /// Explicit values, written into saves, never renumbered.
    /// </summary>
    public enum FounderTrait
    {
        None = 0,
        SilverTongue = 1,
        Solopreneur = 2,
        Entrepreneur = 3,
        Researcher = 4,
        HardwareWhisperer = 5,
        DataHoarder = 6,
        SafetyAdvocate = 7,
        VentureDarling = 8
    }

    /// <summary>
    /// What a trait actually does. Every field here is a multiplier or an offset applied at a real
    /// point in the simulation, never a flat score. A trait that only reads well is decoration.
    ///
    /// Each one is a trade. There is no strictly best pick, and the two you take should pull the
    /// campaign in a direction rather than make it uniformly easier.
    /// </summary>
    public readonly struct FounderTraitDefinition
    {
        public FounderTraitDefinition(
            FounderTrait trait,
            double brandBonus = 0.0,
            double operatingCostMultiplier = 1.0,
            double researchDurationMultiplier = 1.0,
            double trainingThroughputMultiplier = 1.0,
            double hardwarePriceMultiplier = 1.0,
            double dataSupplyMultiplier = 1.0,
            double valuationMultiplier = 1.0,
            double reputationGainMultiplier = 1.0,
            int safetyHeadStart = 0)
        {
            Trait = trait;
            BrandBonus = Math.Clamp(SimUnits.Finite(brandBonus), -0.5, 0.5);
            OperatingCostMultiplier = Math.Clamp(SimUnits.Finite(operatingCostMultiplier, 1.0), 0.5, 2.0);
            ResearchDurationMultiplier = Math.Clamp(SimUnits.Finite(researchDurationMultiplier, 1.0), 0.5, 2.0);
            TrainingThroughputMultiplier = Math.Clamp(SimUnits.Finite(trainingThroughputMultiplier, 1.0), 0.5, 2.0);
            HardwarePriceMultiplier = Math.Clamp(SimUnits.Finite(hardwarePriceMultiplier, 1.0), 0.5, 2.0);
            DataSupplyMultiplier = Math.Clamp(SimUnits.Finite(dataSupplyMultiplier, 1.0), 0.5, 3.0);
            ValuationMultiplier = Math.Clamp(SimUnits.Finite(valuationMultiplier, 1.0), 0.5, 3.0);
            ReputationGainMultiplier = Math.Clamp(SimUnits.Finite(reputationGainMultiplier, 1.0), 0.5, 2.0);
            SafetyHeadStart = Math.Clamp(safetyHeadStart, 0, 4);
        }

        public FounderTrait Trait { get; }

        /// <summary>
        /// The stem for the three things written about this trait.
        ///
        /// Written out rather than built from the enum name, because a key made by concatenation is
        /// invisible to `LocalisationTests.EveryKeyTheInterfaceAsksForExists`.
        /// </summary>
        private static string KeyFor(FounderTrait trait) => trait switch
        {
            FounderTrait.SilverTongue => "trait.silvertongue",
            FounderTrait.Solopreneur => "trait.solopreneur",
            FounderTrait.Entrepreneur => "trait.entrepreneur",
            FounderTrait.Researcher => "trait.researcher",
            FounderTrait.HardwareWhisperer => "trait.hardwarewhisperer",
            FounderTrait.DataHoarder => "trait.datahoarder",
            FounderTrait.SafetyAdvocate => "trait.safetyadvocate",
            _ => "trait.venturedarling"
        };

        /// <summary>Read from the book at access time, never stored. See `PlayerSkillDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Trait));

        /// <summary>One line of character. Shown on the card, does nothing mechanically.</summary>
        public string Flavour => Loc.T(KeyFor(Trait) + ".flavour");

        /// <summary>
        /// The numbers, in plain words, for the card.
        ///
        /// **Still written rather than generated from the multipliers.** `researchDurationMultiplier
        /// 1.18` is what the field holds and "slower to finish anything" is what it means, and only
        /// one of those is worth reading on a card. A generator would also have to decide, for every
        /// language, which way round a multiplier under one reads.
        /// </summary>
        public string EffectSummary => Loc.T(KeyFor(Trait) + ".effect");

        public double BrandBonus { get; }
        public double OperatingCostMultiplier { get; }

        /// <summary>Applies to training runs, upgrades and family programmes alike.</summary>
        public double ResearchDurationMultiplier { get; }

        public double TrainingThroughputMultiplier { get; }
        public double HardwarePriceMultiplier { get; }
        public double DataSupplyMultiplier { get; }
        public double ValuationMultiplier { get; }
        public double ReputationGainMultiplier { get; }

        /// <summary>Levels of Safety every shipped model starts above market par.</summary>
        public int SafetyHeadStart { get; }

        public override string ToString() => $"{DisplayName}: {EffectSummary}";
    }

    /// <summary>The ONE founder trait library.</summary>
    public static class FounderTraitCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        /// <summary>Traits picked at the start. Two, so the choice is a combination and not a menu.</summary>
        public const int TraitsPerFounder = 2;

        private static readonly FounderTraitDefinition[] Entries =
        {
            new(FounderTrait.SilverTongue, brandBonus: 0.10, reputationGainMultiplier: 1.08, operatingCostMultiplier: 1.06),

            new(FounderTrait.Solopreneur, brandBonus: 0.04, operatingCostMultiplier: 0.86, researchDurationMultiplier: 1.18),

            new(FounderTrait.Entrepreneur, researchDurationMultiplier: 0.80, reputationGainMultiplier: 1.06, operatingCostMultiplier: 1.22),

            new(FounderTrait.Researcher, brandBonus: -0.06, trainingThroughputMultiplier: 1.14),

            new(FounderTrait.HardwareWhisperer, operatingCostMultiplier: 0.92, hardwarePriceMultiplier: 0.86, trainingThroughputMultiplier: 0.94),

            new(FounderTrait.DataHoarder, operatingCostMultiplier: 1.10, dataSupplyMultiplier: 1.30),

            new(FounderTrait.SafetyAdvocate, brandBonus: 0.07, trainingThroughputMultiplier: 0.92, safetyHeadStart: 2),

            new(FounderTrait.VentureDarling, brandBonus: -0.05, valuationMultiplier: 1.30, operatingCostMultiplier: 1.08)
        };

        private static readonly Dictionary<FounderTrait, FounderTraitDefinition> ByTrait = BuildIndex();

        public static IReadOnlyList<FounderTraitDefinition> All => Entries;

        public static bool TryGet(FounderTrait trait, out FounderTraitDefinition definition) =>
            ByTrait.TryGetValue(trait, out definition);

        public static FounderTraitDefinition Get(FounderTrait trait)
        {
            if (!ByTrait.TryGetValue(trait, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(trait), trait, "Unknown founder trait.");
            }

            return definition;
        }

        private static Dictionary<FounderTrait, FounderTraitDefinition> BuildIndex()
        {
            var index = new Dictionary<FounderTrait, FounderTraitDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Trait] = entry;
            }

            return index;
        }
    }
}
