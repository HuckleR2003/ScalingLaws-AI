using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The ONE place hardware loses value. Two forces, and the second is the one that hurts.
    ///
    /// 1. Time. Resale value halves every ValueHalfLifeDays with nothing new on the market.
    /// 2. Successors. Every newer part of the same class that ships knocks a further quarter off,
    ///    phased in over <see cref="SuccessorRampDays"/> rather than all at once on launch day.
    ///
    /// Buying at launch and holding through three successor launches costs roughly eighty percent of
    /// the capital. That is not a punishment bolted onto the game, it is what the second-hand market
    /// for accelerators actually does, and it is the reason renting early is a real strategy rather
    /// than a beginner's crutch.
    /// </summary>
    public static class HardwareValuation
    {
        /// <summary>Share of remaining value each newer generation takes away once fully phased in.</summary>
        public const double SuccessorPenalty = 0.25;

        /// <summary>Days for a new launch to finish repricing the previous generation.</summary>
        public const int SuccessorRampDays = 180;

        /// <summary>Scrap value floor as a share of purchase price. Somebody always wants cheap silicon.</summary>
        public const double ScrapValueFraction = 0.08;

        /// <summary>Resale value of one unit today, given what it cost and what has shipped since.</summary>
        public static double ResidualValuePerUnitUsd(
            HardwareGenerationId generationId,
            long purchasePricePerUnitUsd,
            GameDate purchaseDate,
            GameDate asOf)
        {
            if (purchasePricePerUnitUsd <= 0 || !HardwareCatalog.TryGet(generationId, out var generation))
            {
                return 0.0;
            }

            var ageDays = Math.Max(0, asOf.DayIndex - purchaseDate.DayIndex);
            var timeFactor = Math.Pow(0.5, ageDays / (double)generation.ValueHalfLifeDays);
            var successorFactor = SuccessorFactor(generation, purchaseDate, asOf);

            var value = purchasePricePerUnitUsd * timeFactor * successorFactor;
            var floor = purchasePricePerUnitUsd * ScrapValueFraction;
            return Math.Max(floor, value);
        }

        /// <summary>Resale value of a whole batch.</summary>
        public static long ResidualValueUsd(HardwareAsset asset, GameDate asOf)
        {
            var perUnit = ResidualValuePerUnitUsd(
                asset.GenerationId,
                asset.PurchasePricePerUnitUsd,
                asset.PurchaseDate,
                asOf);
            return SimUnits.ToDollars(perUnit * asset.Units);
        }

        /// <summary>
        /// Value lost across a single day. This is the number that quietly drains a company that
        /// bought too much too early, and it is charged whether the cluster is busy or idle.
        /// </summary>
        public static double DailyDepreciationUsd(HardwareAsset asset, GameDate asOf)
        {
            var today = ResidualValuePerUnitUsd(
                asset.GenerationId,
                asset.PurchasePricePerUnitUsd,
                asset.PurchaseDate,
                asOf);
            var tomorrow = ResidualValuePerUnitUsd(
                asset.GenerationId,
                asset.PurchasePricePerUnitUsd,
                asset.PurchaseDate,
                asOf.AddDays(1));
            return Math.Max(0.0, (today - tomorrow) * asset.Units);
        }

        /// <summary>
        /// Petaflops per dollar of this generation against the best part money can buy today.
        /// One means the fleet is current. Below one is the tax on having bought early.
        /// </summary>
        public static double PerformancePerDollarIndex(HardwareGenerationId generationId, GameDate asOf)
        {
            if (!HardwareCatalog.TryGet(generationId, out var generation)
                || generation.Class != HardwareClass.Accelerator)
            {
                return 1.0;
            }

            if (!HardwareCatalog.TryGetFrontier(asOf, HardwareClass.Accelerator, out var frontier)
                || frontier.PetaflopsPerDollar <= 0.0)
            {
                return 1.0;
            }

            return Math.Clamp(generation.PetaflopsPerDollar / frontier.PetaflopsPerDollar, 0.0, 4.0);
        }

        private static double SuccessorFactor(HardwareGeneration generation, GameDate purchaseDate, GameDate asOf)
        {
            var factor = 1.0;
            foreach (var candidate in HardwareCatalog.OfClass(generation.Class))
            {
                if (candidate.Id == generation.Id || candidate.ReleaseDate <= generation.ReleaseDate)
                {
                    continue;
                }

                // A launch that had already happened on the day of purchase was priced into what
                // was paid, so it cannot take value away a second time.
                if (candidate.ReleaseDate <= purchaseDate || asOf <= candidate.ReleaseDate)
                {
                    continue;
                }

                var ramp = Math.Clamp((asOf.DayIndex - candidate.ReleaseDate.DayIndex) / (double)SuccessorRampDays, 0.0, 1.0);
                factor *= 1.0 - SuccessorPenalty * ramp;
            }

            return Math.Clamp(factor, 0.0, 1.0);
        }
    }
}
