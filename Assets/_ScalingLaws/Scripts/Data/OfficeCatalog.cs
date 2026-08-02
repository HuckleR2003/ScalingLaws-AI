using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Where the company works. Explicit values, saved, never renumbered.</summary>
    public enum OfficeTier
    {
        Garage = 0,
        Loft = 1,
        Floor = 2,
        Campus = 3,
        MultiSite = 4
    }

    /// <summary>
    /// One office. Desks, rent, and how well people work in it.
    ///
    /// Borrowed from the tycoon games this follows: office location and workspace upgrades change
    /// how fast and how well things get built, not just how many people fit. The trap they all have
    /// and this keeps is that rent is fixed and headcount is not, so a company that upgrades early
    /// pays for empty desks and one that upgrades late cannot hire the person it needs this month.
    /// </summary>
    public readonly struct OfficeDefinition
    {
        public OfficeDefinition(
            OfficeTier tier,
            string displayName,
            string description,
            int desks,
            long monthlyRentUsd,
            long fitOutCostUsd,
            double effectivenessMultiplier,
            long requiredCashUsd,
            GameDate earliestDate)
        {
            Tier = tier;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? tier.ToString() : displayName;
            Description = description ?? string.Empty;
            Desks = Math.Clamp(desks, 1, 5000);
            MonthlyRentUsd = Math.Clamp(monthlyRentUsd, 0L, 500_000_000L);
            FitOutCostUsd = Math.Clamp(fitOutCostUsd, 0L, 5_000_000_000L);
            EffectivenessMultiplier = Math.Clamp(SimUnits.Finite(effectivenessMultiplier, 1.0), 0.5, 1.6);
            RequiredCashUsd = Math.Max(0L, requiredCashUsd);
            EarliestDate = earliestDate;
        }

        public OfficeTier Tier { get; }
        public string DisplayName { get; }
        public string Description { get; }

        /// <summary>Hard cap on headcount. No desk, no hire.</summary>
        public int Desks { get; }

        public long MonthlyRentUsd { get; }

        /// <summary>One-off cost of moving in. Paid on the day the lease is signed.</summary>
        public long FitOutCostUsd { get; }

        /// <summary>
        /// How much of each person's contribution actually lands. A garage is cramped and a campus
        /// is well equipped, and both beat a floor nobody wanted to move into.
        /// </summary>
        public double EffectivenessMultiplier { get; }

        public long RequiredCashUsd { get; }
        public GameDate EarliestDate { get; }

        public long DailyRentUsd => SimUnits.ToDollars(MonthlyRentUsd / 30.4375);

        public override string ToString() => $"{DisplayName}: {Desks} desks at ${MonthlyRentUsd:N0}/month";
    }

    /// <summary>The ONE office library.</summary>
    public static class OfficeCatalog
    {
        public const string CatalogVersion = "2026.08.03";

        private static readonly OfficeDefinition[] Entries =
        {
            new(OfficeTier.Garage, "Garage",
                "Four desks, one whiteboard and a router that everyone has learned not to touch.",
                desks: 4,
                monthlyRentUsd: 4_000,
                fitOutCostUsd: 0,
                effectivenessMultiplier: 0.85,
                requiredCashUsd: 0,
                earliestDate: GameDate.Start),

            new(OfficeTier.Loft, "Loft",
                "Enough room to argue in without booking anything. The last office where everyone knows "
                + "what everyone else is working on.",
                desks: 14,
                monthlyRentUsd: 42_000,
                fitOutCostUsd: 350_000,
                effectivenessMultiplier: 1.0,
                requiredCashUsd: 3_000_000,
                earliestDate: GameDate.Start),

            new(OfficeTier.Floor, "Office floor",
                "A proper lease with a proper server closet. Also the first month anybody has to ask "
                + "who owns something.",
                desks: 40,
                monthlyRentUsd: 190_000,
                fitOutCostUsd: 2_400_000,
                effectivenessMultiplier: 1.08,
                requiredCashUsd: 25_000_000,
                earliestDate: GameDate.Start),

            new(OfficeTier.Campus, "Campus",
                "Purpose built, well equipped, and expensive enough that the rent shows up in the "
                + "monthly numbers whether or not the desks are full.",
                desks: 120,
                monthlyRentUsd: 900_000,
                fitOutCostUsd: 18_000_000,
                effectivenessMultiplier: 1.18,
                requiredCashUsd: 150_000_000,
                earliestDate: GameDate.FromCalendar(2023, 6, 1)),

            new(OfficeTier.MultiSite, "Multiple sites",
                "Three time zones and a travel budget. More people than any one room can hold, at the "
                + "price of nobody being in the same room.",
                desks: 400,
                monthlyRentUsd: 4_200_000,
                fitOutCostUsd: 70_000_000,
                effectivenessMultiplier: 1.12,
                requiredCashUsd: 800_000_000,
                earliestDate: GameDate.FromCalendar(2024, 6, 1))
        };

        private static readonly Dictionary<OfficeTier, OfficeDefinition> ByTier = BuildIndex();

        public static IReadOnlyList<OfficeDefinition> All => Entries;

        public static OfficeDefinition Get(OfficeTier tier) =>
            ByTier.TryGetValue(tier, out var definition) ? definition : ByTier[OfficeTier.Garage];

        public static bool TryGet(OfficeTier tier, out OfficeDefinition definition) =>
            ByTier.TryGetValue(tier, out definition);

        private static Dictionary<OfficeTier, OfficeDefinition> BuildIndex()
        {
            var index = new Dictionary<OfficeTier, OfficeDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Tier] = entry;
            }

            return index;
        }
    }
}
