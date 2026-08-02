using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The founder's traits folded into one set of multipliers.
    ///
    /// Combined rather than looked up every time, so a trait can never be applied twice by accident
    /// and every consumer reads a single number. Multipliers compose; offsets add.
    /// </summary>
    public readonly struct FounderProfile
    {
        private readonly FounderTrait[] traits;

        public FounderProfile(params FounderTrait[] chosen)
        {
            // Two picked by the player plus at most one that comes with the company they took over.
            const int capacity = FounderTraitCatalog.TraitsPerFounder + 1;
            var kept = new List<FounderTrait>(capacity);

            BrandBonus = 0.0;
            OperatingCostMultiplier = 1.0;
            ResearchDurationMultiplier = 1.0;
            TrainingThroughputMultiplier = 1.0;
            HardwarePriceMultiplier = 1.0;
            DataSupplyMultiplier = 1.0;
            ValuationMultiplier = 1.0;
            ReputationGainMultiplier = 1.0;
            SafetyHeadStart = 0;

            if (chosen != null)
            {
                foreach (var trait in chosen)
                {
                    if (trait == FounderTrait.None
                        || kept.Contains(trait)
                        || kept.Count >= capacity
                        || !FounderTraitCatalog.TryGet(trait, out var definition))
                    {
                        continue;
                    }

                    kept.Add(trait);

                    BrandBonus += definition.BrandBonus;
                    OperatingCostMultiplier *= definition.OperatingCostMultiplier;
                    ResearchDurationMultiplier *= definition.ResearchDurationMultiplier;
                    TrainingThroughputMultiplier *= definition.TrainingThroughputMultiplier;
                    HardwarePriceMultiplier *= definition.HardwarePriceMultiplier;
                    DataSupplyMultiplier *= definition.DataSupplyMultiplier;
                    ValuationMultiplier *= definition.ValuationMultiplier;
                    ReputationGainMultiplier *= definition.ReputationGainMultiplier;
                    SafetyHeadStart += definition.SafetyHeadStart;
                }
            }

            traits = kept.ToArray();

            BrandBonus = Math.Clamp(BrandBonus, -0.5, 0.5);
            SafetyHeadStart = Math.Clamp(SafetyHeadStart, 0, 4);
        }

        public IReadOnlyList<FounderTrait> Traits => traits ?? Array.Empty<FounderTrait>();

        public double BrandBonus { get; }
        public double OperatingCostMultiplier { get; }
        public double ResearchDurationMultiplier { get; }
        public double TrainingThroughputMultiplier { get; }
        public double HardwarePriceMultiplier { get; }
        public double DataSupplyMultiplier { get; }
        public double ValuationMultiplier { get; }
        public double ReputationGainMultiplier { get; }
        public int SafetyHeadStart { get; }

        public bool Has(FounderTrait trait)
        {
            if (traits == null)
            {
                return false;
            }

            foreach (var owned in traits)
            {
                if (owned == trait)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Scales a day count by the founder's research speed, never below one day.</summary>
        public int ScaleDuration(int days) =>
            Math.Max(1, (int)Math.Round(days * ResearchDurationMultiplier));

        /// <summary>A founder with no traits picked. Every multiplier neutral.</summary>
        public static FounderProfile Neutral => new(Array.Empty<FounderTrait>());

        public override string ToString() =>
            traits == null || traits.Length == 0 ? "no traits" : string.Join(" + ", traits);
    }
}
