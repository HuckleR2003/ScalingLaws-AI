using System;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// A rival model going live. The player never sees the rival's internals, only what the market
    /// sees: how good it is, how much trust the name carries, and what it charges.
    /// </summary>
    public readonly struct CompetitorRelease
    {
        public CompetitorRelease(
            CompetitorId competitor,
            string displayName,
            GameDate releaseDate,
            double capability,
            double brandStrength,
            double priceMultiplier,
            bool isProjection)
        {
            Competitor = competitor;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? competitor.ToString() : displayName;
            ReleaseDate = releaseDate;
            Capability = Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0);
            BrandStrength = Math.Clamp(SimUnits.Finite(brandStrength), 0.0, 1.0);
            PriceMultiplier = Math.Clamp(SimUnits.Finite(priceMultiplier, 1.0), 0.05, 20.0);
            IsProjection = isProjection;
        }

        public CompetitorId Competitor { get; }
        public string DisplayName { get; }
        public GameDate ReleaseDate { get; }

        /// <summary>Capability on the same 0 to 100 scale the player's models are scored on.</summary>
        public double Capability { get; }

        /// <summary>How much the name alone is worth in the demand split, 0 to 1.</summary>
        public double BrandStrength { get; }

        /// <summary>Price relative to the market average for that day.</summary>
        public double PriceMultiplier { get; }

        /// <summary>True for entries past the point where real releases are known. Labelled, never hidden.</summary>
        public bool IsProjection { get; }

        public bool IsLiveOn(GameDate date) => date.IsOnOrAfter(ReleaseDate);

        public override string ToString() => $"{DisplayName} ({ReleaseDate}, cap {Capability:0.0})";
    }
}
