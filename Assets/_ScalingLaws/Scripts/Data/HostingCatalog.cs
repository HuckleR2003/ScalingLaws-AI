using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    public enum HostingPackage
    {
        /// <summary>Reserved capacity sized to a million ordinary accounts.</summary>
        Standard = 0,

        /// <summary>Fewer people, held to a much tighter response time.</summary>
        LowLatency = 1,

        /// <summary>Bulk capacity at a discount, on shared iron that queues under load.</summary>
        Bulk = 2
    }

    /// <summary>
    /// One rentable block of hosting. Bought in whole units and they stack.
    ///
    /// The slider rents raw petaflops out of a shared pool: cheapest per unit, and it queues like
    /// everything else in that pool. A package is a contract for reserved capacity, so it costs more
    /// per petaflop and behaves better when the fleet is busy. That is the whole trade and it is why
    /// three packages exist rather than one bigger slider.
    /// </summary>
    public readonly struct HostingPackageDefinition
    {
        public HostingPackageDefinition(HostingPackage id,
            double petaflops, long monthlyCostUsd, double reservedQuality, int unitCap)
        {
            Id = id;
            Petaflops = Math.Max(0.0, petaflops);
            MonthlyCostUsd = Math.Max(0L, monthlyCostUsd);
            ReservedQuality = Math.Clamp(reservedQuality, 0.0, 1.0);
            UnitCap = Math.Max(1, unitCap);
        }

        public HostingPackage Id { get; }

        private static string KeyFor(HostingPackage id) => id switch
        {
            HostingPackage.LowLatency => "hosting.edge",
            HostingPackage.Bulk => "hosting.bulk",
            _ => "hosting.growth"
        };

        /// <summary>
        /// Read from the book at access time, never stored.
        ///
        /// A catalog built at type load keeps whatever language it was built in. See
        /// `PrecisionDefinition` for the version of this note with the screenshot behind it.
        /// </summary>
        public string DisplayName => Loc.T(KeyFor(Id));

        /// <summary>What the hosting company would say it is for.</summary>
        public string Pitch => Loc.T(KeyFor(Id) + ".pitch");

        public double Petaflops { get; }
        public long MonthlyCostUsd { get; }

        /// <summary>
        /// How much of this block counts as reserved rather than shared. One means it holds its
        /// response time right up to the point it is full; zero means it queues like the pool.
        /// </summary>
        public double ReservedQuality { get; }

        /// <summary>How many of these one company may hold. Nothing scales for ever.</summary>
        public int UnitCap { get; }

        public long DailyCostUsd => (long)Math.Round(MonthlyCostUsd / 30.0);

        public override string ToString() => $"{DisplayName} ({Petaflops:N0} PF)";
    }

    /// <summary>
    /// The three packages the hosting page sells, alongside the raw slider.
    ///
    /// Deliberately not a ladder where each is strictly better. Standard is the sensible default,
    /// low latency buys experience rather than volume, and bulk buys volume at the cost of
    /// experience. A player who takes bulk to chase a big audience and then wonders why they cannot
    /// keep it has made a real mistake rather than hit a hidden rule.
    /// </summary>
    public static class HostingCatalog
    {
        public const string CatalogVersion = "hosting-2026-08-13";

        /// <summary>Roughly what a million ordinary consumer accounts get through in a day.</summary>
        public const double PetaflopsPerMillionConsumers = 40.0;

        private static readonly HostingPackageDefinition[] Entries =
        {
            // The words are `hosting.*` in the phrase book.
            new(HostingPackage.Standard,
                petaflops: 40.0, monthlyCostUsd: 320_000, reservedQuality: 0.75, unitCap: 40),

            new(HostingPackage.LowLatency,
                petaflops: 20.0, monthlyCostUsd: 300_000, reservedQuality: 1.0, unitCap: 20),

            new(HostingPackage.Bulk,
                petaflops: 120.0, monthlyCostUsd: 640_000, reservedQuality: 0.0, unitCap: 25)
        };

        public static IReadOnlyList<HostingPackageDefinition> All => Entries;

        public static HostingPackageDefinition Get(HostingPackage id)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    return entry;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown hosting package.");
        }

        /// <summary>How many everyday accounts a block of this size comfortably covers.</summary>
        public static double CoversAccounts(double petaflops) =>
            Math.Max(0.0, petaflops) / PetaflopsPerMillionConsumers * 1_000_000.0;
    }
}
