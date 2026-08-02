using System;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The result of pouring a set of corpora into one run: how many tokens actually came out, how
    /// good the average token was, and what the mix cost to obtain.
    /// </summary>
    public readonly struct DatasetBlend
    {
        public DatasetBlend(
            double availableTokensBillions,
            double qualityMultiplier,
            long acquisitionCostUsd,
            int sourceCount,
            bool isSufficient)
        {
            AvailableTokensBillions = Math.Max(0.0, SimUnits.Finite(availableTokensBillions));
            QualityMultiplier = Math.Clamp(SimUnits.Finite(qualityMultiplier, 1.0), 0.4, 1.6);
            AcquisitionCostUsd = Math.Max(0L, acquisitionCostUsd);
            SourceCount = Math.Max(0, sourceCount);
            IsSufficient = isSufficient;
        }

        /// <summary>Tokens the mix can supply to this run, in billions.</summary>
        public double AvailableTokensBillions { get; }

        /// <summary>
        /// Weighted quality of the tokens actually drawn. Adding a bulk low-quality corpus on top of
        /// a small clean one raises volume and lowers this number, which is the real tradeoff.
        /// </summary>
        public double QualityMultiplier { get; }

        public long AcquisitionCostUsd { get; }
        public int SourceCount { get; }

        /// <summary>False when the blueprint asked for more tokens than the mix can supply.</summary>
        public bool IsSufficient { get; }

        public static DatasetBlend Empty => new(0.0, 1.0, 0L, 0, false);
    }
}
