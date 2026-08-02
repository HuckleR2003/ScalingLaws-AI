using System;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What choosing an architecture actually changes. Four independent levers, so no architecture
    /// is a pure upgrade over another:
    ///   ParameterEfficiency     quality you get per parameter
    ///   ActiveParameterFraction share of parameters that burn FLOPs on each token
    ///   TrainingEfficiency      multiplier on cluster utilization during a run
    ///   InferenceCostMultiplier what a served token costs once the model is live
    /// </summary>
    public readonly struct ArchitectureDefinition
    {
        public ArchitectureDefinition(
            ArchitectureId id,
            string displayName,
            GameDate availableFrom,
            double parameterEfficiency,
            double activeParameterFraction,
            double trainingEfficiency,
            double inferenceCostMultiplier,
            double capabilityBonus,
            long adoptionCostUsd)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
            AvailableFrom = availableFrom;
            ParameterEfficiency = Math.Clamp(SimUnits.Finite(parameterEfficiency, 1.0), 0.25, 4.0);
            ActiveParameterFraction = Math.Clamp(SimUnits.Finite(activeParameterFraction, 1.0), 0.02, 1.0);
            TrainingEfficiency = Math.Clamp(SimUnits.Finite(trainingEfficiency, 1.0), 0.25, 2.0);
            InferenceCostMultiplier = Math.Clamp(SimUnits.Finite(inferenceCostMultiplier, 1.0), 0.1, 10.0);
            CapabilityBonus = Math.Clamp(SimUnits.Finite(capabilityBonus), 0.0, 20.0);
            AdoptionCostUsd = Math.Clamp(adoptionCostUsd, 0L, 5_000_000_000L);
        }

        public ArchitectureId Id { get; }
        public string DisplayName { get; }

        /// <summary>The day the technique becomes public. Nobody trains on it before this.</summary>
        public GameDate AvailableFrom { get; }

        public double ParameterEfficiency { get; }
        public double ActiveParameterFraction { get; }
        public double TrainingEfficiency { get; }
        public double InferenceCostMultiplier { get; }

        /// <summary>Flat capability added on top of the scaling-law result, on the 0 to 100 scale.</summary>
        public double CapabilityBonus { get; }

        /// <summary>One-off research spend to unlock the family for the company.</summary>
        public long AdoptionCostUsd { get; }

        public bool IsAvailableOn(GameDate date) => date.IsOnOrAfter(AvailableFrom);

        public override string ToString() => $"{DisplayName} (from {AvailableFrom})";
    }
}
