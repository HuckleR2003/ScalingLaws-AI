using System;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// One hardware generation as the market sees it: when it ships, what it costs, what it burns,
    /// and how fast its resale value falls. Immutable, clamped on construction, safe to copy.
    /// </summary>
    public readonly struct HardwareGeneration
    {
        public HardwareGeneration(
            HardwareGenerationId id,
            HardwareClass hardwareClass,
            string vendorName,
            string displayName,
            GameDate releaseDate,
            double petaflopsPerUnit,
            int acceleratorsServed,
            long launchPriceUsd,
            double powerKilowatts,
            int memoryGigabytes,
            double utilizationCeiling,
            int valueHalfLifeDays,
            bool isProjection)
        {
            Id = id;
            Class = hardwareClass;
            VendorName = string.IsNullOrWhiteSpace(vendorName) ? "Unknown" : vendorName;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
            ReleaseDate = releaseDate;
            PetaflopsPerUnit = Math.Max(0.0, SimUnits.Finite(petaflopsPerUnit));
            AcceleratorsServed = Math.Max(0, acceleratorsServed);
            LaunchPriceUsd = Math.Clamp(launchPriceUsd, 1L, 10_000_000L);
            PowerKilowatts = Math.Clamp(SimUnits.Finite(powerKilowatts), 0.001, 100.0);
            MemoryGigabytes = Math.Max(0, memoryGigabytes);
            UtilizationCeiling = Math.Clamp(SimUnits.Finite(utilizationCeiling), 0.05, 0.95);
            ValueHalfLifeDays = Math.Clamp(valueHalfLifeDays, 90, 5000);
            IsProjection = isProjection;
        }

        public HardwareGenerationId Id { get; }
        public HardwareClass Class { get; }
        public string VendorName { get; }
        public string DisplayName { get; }

        /// <summary>The day it can first be ordered. Buying before this is not possible at any price.</summary>
        public GameDate ReleaseDate { get; }

        /// <summary>Dense BF16 throughput of a single unit. Zero for everything that is not an accelerator.</summary>
        public double PetaflopsPerUnit { get; }

        /// <summary>How many accelerators one support unit can keep fed. Zero for accelerators themselves.</summary>
        public int AcceleratorsServed { get; }

        public long LaunchPriceUsd { get; }
        public double PowerKilowatts { get; }

        /// <summary>On-package memory per unit. This is the hard cap on how large a model can be trained.</summary>
        public int MemoryGigabytes { get; }

        /// <summary>
        /// Best model-FLOPs utilization this silicon reaches on a well-written stack. Real clusters
        /// land under it; nothing lands above it. Only accelerators use this.
        /// </summary>
        public double UtilizationCeiling { get; }

        /// <summary>Days for resale value to halve with no successor on the market.</summary>
        public int ValueHalfLifeDays { get; }

        /// <summary>
        /// True when the entry is a forward projection rather than a shipped product. Same honesty
        /// rule as PC Workman's estimated sensor readings: projections are labelled, never quietly
        /// mixed in with measured facts.
        /// </summary>
        public bool IsProjection { get; }

        public bool IsAvailableOn(GameDate date) => date.IsOnOrAfter(ReleaseDate);

        /// <summary>Petaflops per dollar at launch price. The number that makes old silicon hurt.</summary>
        public double PetaflopsPerDollar => LaunchPriceUsd <= 0 ? 0.0 : PetaflopsPerUnit / LaunchPriceUsd;

        /// <summary>Petaflops per kilowatt. Power, not price, is what caps a datacenter.</summary>
        public double PetaflopsPerKilowatt => PowerKilowatts <= 0.0 ? 0.0 : PetaflopsPerUnit / PowerKilowatts;

        public override string ToString() => $"{VendorName} {DisplayName} ({ReleaseDate})";
    }
}
