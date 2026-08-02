using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One trait as the upgrade grid needs to show it.</summary>
    public readonly struct TraitStanding
    {
        public TraitStanding(
            ModelTrait trait,
            int level,
            int expectedLevel,
            long upgradeCostUsd,
            double upgradePetaflopDays,
            int upgradeDays,
            bool isAvailable)
        {
            Trait = trait;
            Level = Math.Clamp(level, 0, ModelTraitSetLimits.MaximumLevel);
            ExpectedLevel = Math.Clamp(expectedLevel, 0, ModelTraitSetLimits.MaximumLevel);
            UpgradeCostUsd = Math.Max(0L, upgradeCostUsd);
            UpgradePetaflopDays = Math.Max(0.0, SimUnits.Finite(upgradePetaflopDays));
            UpgradeDays = Math.Max(1, upgradeDays);
            IsAvailable = isAvailable;
        }

        public ModelTrait Trait { get; }
        public int Level { get; }

        /// <summary>What buyers treat as normal today. The grid shows this so nobody has to guess.</summary>
        public int ExpectedLevel { get; }

        public long UpgradeCostUsd { get; }
        public double UpgradePetaflopDays { get; }
        public int UpgradeDays { get; }
        public bool IsAvailable { get; }

        /// <summary>Levels below what the market expects. Zero or more.</summary>
        public int Shortfall => Math.Max(0, ExpectedLevel - Level);

        public bool IsBehindMarket => Shortfall > 0;
        public bool IsMaxed => Level >= ModelTraitSetLimits.MaximumLevel;

        public override string ToString() => $"{Trait} L{Level}/exp {ExpectedLevel}";
    }

    /// <summary>
    /// The upgrade levels a single model carries. Mutable, owned by a <see cref="DeployedModel"/>.
    ///
    /// Three separate effects come out of here and they do not overlap: capability moves the quality
    /// number, brand moves how buyers pick between equal models, and efficiency moves the serving
    /// bill. A company can lead on all three, on one, or on none, and each is a different business.
    /// </summary>
    public sealed class ModelTraitSet
    {
        private readonly int[] levels = new int[ModelTraitSetLimits.TraitCount];

        /// <summary>Brand lost per level of shortfall against what the market expects.</summary>
        public const double ShortfallBrandPenalty = 0.022;

        /// <summary>
        /// Levels of shortfall past which a trait stops getting worse. Two, not four: across the
        /// eight traits that carry capability weight, four levels came to 15.6 points, which is more
        /// than the gap between a 2022 model and a 2024 one. That is not a slide, it is a cliff, and
        /// it made a shipped model worthless inside two years for no decision the player made. The
        /// age term in the demand split already handles obsolescence.
        /// </summary>
        public const int MaximumShortfallCounted = 2;

        public int GetLevel(ModelTrait trait)
        {
            var index = (int)trait;
            return index < 0 || index >= levels.Length ? 0 : levels[index];
        }

        public void SetLevel(ModelTrait trait, int level)
        {
            var index = (int)trait;
            if (index >= 0 && index < levels.Length)
            {
                levels[index] = Math.Clamp(level, 0, ModelTraitSetLimits.MaximumLevel);
            }
        }

        public bool Increment(ModelTrait trait)
        {
            var current = GetLevel(trait);
            if (current >= ModelTraitSetLimits.MaximumLevel)
            {
                return false;
            }

            SetLevel(trait, current + 1);
            return true;
        }

        public int TotalLevels
        {
            get
            {
                var total = 0;
                foreach (var level in levels)
                {
                    total += level;
                }

                return total;
            }
        }

        /// <summary>
        /// Levels above what the market expects today. Negative when behind, floored at
        /// <see cref="MaximumShortfallCounted"/> so a forgotten model decays without hitting zero
        /// twice over (the age term in the demand split is already punishing it).
        /// </summary>
        public int Advantage(ModelTraitDefinition definition, GameDate date)
        {
            var delta = GetLevel(definition.Trait) - definition.ExpectedLevelOn(date);
            return Math.Max(-MaximumShortfallCounted, delta);
        }

        /// <summary>
        /// Capability the upgrades add or cost, measured against the market par of the day. A model
        /// exactly at par scores zero here: par is not an achievement, it is the entry fee.
        /// </summary>
        public double CapabilityBonus(GameDate date)
        {
            var bonus = 0.0;
            foreach (var definition in ModelTraitCatalog.All)
            {
                bonus += Advantage(definition, date) * definition.CapabilityPerLevel;
            }

            return bonus;
        }

        /// <summary>
        /// Brand earned above par, minus brand lost below it. Goes negative, which is the entire
        /// point: standing still is not neutral, because par moves every month.
        /// </summary>
        public double BrandBonus(GameDate date)
        {
            var bonus = 0.0;
            foreach (var definition in ModelTraitCatalog.All)
            {
                var advantage = Advantage(definition, date);
                bonus += advantage >= 0
                    ? advantage * definition.BrandPerLevel
                    : advantage * ShortfallBrandPenalty;
            }

            return bonus;
        }

        /// <summary>
        /// Multiplier on serving cost per token, also measured against par. Being level with
        /// everyone else costs what everyone else pays. Falling behind on Optimisation makes every
        /// token more expensive than a rival's, which is how a price war is actually lost.
        /// </summary>
        public double EfficiencyMultiplier(GameDate date)
        {
            var multiplier = 1.0;
            foreach (var definition in ModelTraitCatalog.All)
            {
                if (definition.EfficiencyPerLevel <= 0.0)
                {
                    continue;
                }

                multiplier *= Math.Pow(1.0 - definition.EfficiencyPerLevel, Advantage(definition, date));
            }

            return Math.Clamp(multiplier, 0.05, 4.0);
        }

        /// <summary>Total levels the model is short across every trait. The staleness number.</summary>
        public int TotalShortfall(GameDate date)
        {
            var shortfall = 0;
            foreach (var definition in ModelTraitCatalog.All)
            {
                shortfall += Math.Max(0, definition.ExpectedLevelOn(date) - GetLevel(definition.Trait));
            }

            return shortfall;
        }

        /// <summary>Everything the upgrade grid needs, in catalog order.</summary>
        public List<TraitStanding> Standings(GameDate date)
        {
            var standings = new List<TraitStanding>(ModelTraitSetLimits.TraitCount);
            foreach (var definition in ModelTraitCatalog.All)
            {
                var level = GetLevel(definition.Trait);
                standings.Add(new TraitStanding(
                    definition.Trait,
                    level,
                    definition.ExpectedLevelOn(date),
                    definition.UpgradeCostUsd(level),
                    definition.UpgradePetaflopDays(level),
                    definition.UpgradeDays(level),
                    definition.IsAvailableOn(date)));
            }

            return standings;
        }

        public ModelTraitSet Clone()
        {
            var copy = new ModelTraitSet();
            Array.Copy(levels, copy.levels, levels.Length);
            return copy;
        }

        public int[] ToArray() => (int[])levels.Clone();

        public static ModelTraitSet FromArray(IReadOnlyList<int> source)
        {
            var set = new ModelTraitSet();
            if (source == null)
            {
                return set;
            }

            var count = Math.Min(source.Count, ModelTraitSetLimits.TraitCount);
            for (var index = 0; index < count; index++)
            {
                set.levels[index] = Math.Clamp(source[index], 0, ModelTraitSetLimits.MaximumLevel);
            }

            return set;
        }

        /// <summary>
        /// A freshly trained model, level with the market on every trait. It has no advantage and no
        /// deficit on the day it ships. From there the levels stay put and par keeps rising, so
        /// doing nothing is a slow slide rather than a stable position.
        /// </summary>
        public static ModelTraitSet AtMarketPar(GameDate date)
        {
            var set = new ModelTraitSet();
            foreach (var definition in ModelTraitCatalog.All)
            {
                set.SetLevel(definition.Trait, definition.ExpectedLevelOn(date));
            }

            return set;
        }
    }
}
