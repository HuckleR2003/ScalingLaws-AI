using System;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// One corpus the company can license or build. Supply is a hard ceiling: a blueprint that asks
    /// for more tokens than the owned mix can supply does not get to train on tokens that do not exist.
    /// </summary>
    public readonly struct DatasetSourceDefinition
    {
        public DatasetSourceDefinition(
            DatasetSource flag,
            GameDate availableFrom,
            double tokenSupplyBillions,
            double qualityMultiplier,
            long acquisitionCostUsd,
            double requiredOwnedCapability)
        {
            Flag = flag;
            AvailableFrom = availableFrom;
            TokenSupplyBillions = Math.Clamp(SimUnits.Finite(tokenSupplyBillions), 1.0, 1_000_000.0);
            QualityMultiplier = Math.Clamp(SimUnits.Finite(qualityMultiplier, 1.0), 0.4, 1.6);
            AcquisitionCostUsd = Math.Clamp(acquisitionCostUsd, 0L, 5_000_000_000L);
            RequiredOwnedCapability = Math.Clamp(SimUnits.Finite(requiredOwnedCapability), 0.0, 100.0);
        }

        public DatasetSource Flag { get; }

        private static string KeyFor(DatasetSource flag) => flag switch
        {
            DatasetSource.CuratedWeb => "corpus.curated",
            DatasetSource.CodeCorpus => "corpus.code",
            DatasetSource.HumanFeedback => "corpus.feedback",
            DatasetSource.LicensedBooks => "corpus.books",
            DatasetSource.AcademicArchive => "corpus.academic",
            DatasetSource.Synthetic => "corpus.synthetic",
            DatasetSource.VideoAndAudio => "corpus.video",
            _ => "corpus.crawl"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Flag));
        public GameDate AvailableFrom { get; }

        /// <summary>Billions of tokens this corpus can contribute to a single run.</summary>
        public double TokenSupplyBillions { get; }

        /// <summary>Multiplier on effective training tokens. Above 1.0 means a token here teaches more.</summary>
        public double QualityMultiplier { get; }

        /// <summary>One-off licensing or pipeline cost. Paid per source, not per token.</summary>
        public long AcquisitionCostUsd { get; }

        /// <summary>
        /// Capability the company must already have shipped before this corpus can be produced.
        /// Synthetic data is the case that matters: you cannot generate it without a good model.
        /// </summary>
        public double RequiredOwnedCapability { get; }

        public bool IsAvailableOn(GameDate date, double bestOwnedCapability)
        {
            return date.IsOnOrAfter(AvailableFrom) && bestOwnedCapability >= RequiredOwnedCapability;
        }

        public override string ToString() => $"{DisplayName} ({TokenSupplyBillions:N0}B tokens, q{QualityMultiplier:0.00})";
    }
}
