using System;
using System.Collections.Generic;
using System.Linq;
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
        MultiSite = 4,

        /// <summary>
        /// Added 2026-08-28, between Campus and MultiSite on the ladder and after both in the enum.
        ///
        /// **Appended rather than inserted.** These values are written into saves, so renumbering
        /// MultiSite would move an existing campaign into a building it never leased. Order on
        /// screen comes from the catalog array, which nothing reads numerically.
        /// </summary>
        Tower = 5
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
            long purchasePriceUsd,
            GameDate earliestDate,
            int level = 0,
            string art = "")
        {
            Tier = tier;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? tier.ToString() : displayName;
            Description = description ?? string.Empty;
            Level = Math.Max(0, level);
            Art = art ?? string.Empty;

            // Zero is legal now and it means something: at home there is nowhere for anybody else to
            // sit, so the first hire is not a purchase, it is a move.
            Desks = Math.Clamp(desks, 0, 5000);
            MonthlyRentUsd = Math.Clamp(monthlyRentUsd, 0L, 500_000_000L);
            FitOutCostUsd = Math.Clamp(fitOutCostUsd, 0L, 5_000_000_000L);
            EffectivenessMultiplier = Math.Clamp(SimUnits.Finite(effectivenessMultiplier, 1.0), 0.5, 1.6);
            RequiredCashUsd = Math.Max(0L, requiredCashUsd);
            PurchasePriceUsd = Math.Max(0L, purchasePriceUsd);
            EarliestDate = earliestDate;
        }

        public OfficeTier Tier { get; }
        public string DisplayName { get; }

        /// <summary>
        /// Where it sits on the ladder, shown as LVL 0, LVL 1 and so on.
        ///
        /// An explicit number rather than the position in the array, because the chooser only shows
        /// the tiers that have a place built for them and the numbering has to stay put when the
        /// rest arrive.
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// The picture of the place, under Resources/Offices, or empty while it is not drawn.
        ///
        /// **This is what the ladder is becoming.** An office used to be a desk count with a rent on
        /// it. Each one is turning into somewhere the company physically is, which is why the
        /// chooser is a row of photographs rather than a table, and why a tier with no picture is
        /// not offered yet rather than being offered with a grey square.
        /// </summary>
        public string Art { get; }

        /// <summary>True once there is a place to move into. Tiers without one stay off the screen.</summary>
        public bool HasPlace => Art.Length > 0;
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

        /// <summary>
        /// What it costs to own the place outright, or zero where nobody will sell.
        ///
        /// **Roughly ten years of rent**, which is the number that makes it a real decision rather
        /// than an obvious one: a company that will still be here in a decade should buy, and a
        /// company that is not sure should not tie up the capital. Owning ends the rent entirely and
        /// the money never comes back, which is the trade.
        /// </summary>
        public long PurchasePriceUsd { get; }

        public bool CanBeBought => PurchasePriceUsd > 0L;
        public GameDate EarliestDate { get; }

        public long DailyRentUsd => SimUnits.ToDollars(MonthlyRentUsd / 30.4375);

        public override string ToString() => $"{DisplayName}: {Desks} desks at ${MonthlyRentUsd:N0}/month";
    }

    /// <summary>The ONE office library.</summary>
    public static class OfficeCatalog
    {
        public const string CatalogVersion = "2026.08.22";

        /// <summary>
        /// What a furnished move costs on top of the fit-out.
        ///
        /// **Deliberately less than the pieces are worth.** The pack below lists at $43,000, so
        /// taking the standard fit-out saves about nine per cent against buying the same things one
        /// at a time. That is what makes the tick a real option rather than a tax on anybody who
        /// forgets to untick it: the saving is the price of not choosing.
        /// </summary>
        public const long FurnishedPackUsd = 39_000L;

        /// <summary>
        /// What arrives with a furnished move.
        ///
        /// **No desks, on purpose.** Desks are what caps hiring, so a pack that included them would
        /// be a change to the economy dressed as a convenience. Everything here moves morale and
        /// research and nothing else, which keeps the option out of the balance-critical path.
        /// </summary>
        public static readonly IReadOnlyList<FurnitureKind> FurnishedPack = new[]
        {
            FurnitureKind.CoffeeBar,
            FurnitureKind.Sofa,
            FurnitureKind.Bookshelf,
            FurnitureKind.Whiteboard,
            FurnitureKind.Plant,
            FurnitureKind.Plant
        };

        /// <summary>What the pack would cost bought piece by piece.</summary>
        public static double FurnishedPackListUsd =>
            FurnishedPack.Sum(kind => FurnitureCatalog.Get(kind).PriceUsd);

        private static readonly OfficeDefinition[] Entries =
        {
            // The three that have a place built for them. Desks, rent and level are the author's
            // figures from the chooser mock, not derived: a house with nowhere to sit, then two
            // hubs that cost real money for a modest number of desks. Rent per desk is deliberately
            // steep, because what the move buys is the right to have anybody at all.
            new(OfficeTier.Garage, "House",
                "The room you started in. There is nowhere for a second person to sit, so everything "
                + "that gets built here gets built by you.",
                desks: 0,
                monthlyRentUsd: 4_000,
                fitOutCostUsd: 0,
                effectivenessMultiplier: 0.85,
                requiredCashUsd: 0,
                purchasePriceUsd: 0,
                earliestDate: GameDate.Start,
                level: 0,
                art: "office_house"),

            new(OfficeTier.Loft, "Small office hub",
                "Ten desks and a lease. The first month the company is somewhere rather than "
                + "somebody.",
                desks: 10,
                monthlyRentUsd: 210_000,
                fitOutCostUsd: 350_000,
                effectivenessMultiplier: 1.0,
                requiredCashUsd: 3_000_000,
                purchasePriceUsd: 24_500_000,
                earliestDate: GameDate.Start,
                level: 1,
                art: "office_smallhub"),

            new(OfficeTier.Floor, "Big company hub",
                "Twenty desks, a proper server closet, and the first month anybody has to ask who "
                + "owns something.",
                desks: 20,
                monthlyRentUsd: 300_000,
                fitOutCostUsd: 2_400_000,
                effectivenessMultiplier: 1.08,
                requiredCashUsd: 25_000_000,
                purchasePriceUsd: 35_000_000,
                earliestDate: GameDate.Start,
                level: 2,
                art: "office_bighub"),

            // Still in the catalog and not yet on the chooser: no place has been built for either,
            // and offering a move to somewhere with no picture is offering a move to nowhere.
            new(OfficeTier.Campus, "Campus",
                "Purpose built, well equipped, and expensive enough that the rent shows up in the "
                + "monthly numbers whether or not the desks are full.",
                desks: 50,
                monthlyRentUsd: 620_000,
                fitOutCostUsd: 18_000_000,
                effectivenessMultiplier: 1.18,
                requiredCashUsd: 150_000_000,
                purchasePriceUsd: 0,
                earliestDate: GameDate.FromCalendar(2023, 6, 1),
                level: 3),

            new(OfficeTier.Tower, "Tower floor",
                "Two floors of a building with your name in the lobby. The rent is a number the "
                + "board asks about, and the desks fill faster than anybody plans for.",
                desks: 125,
                monthlyRentUsd: 1_450_000,
                fitOutCostUsd: 34_000_000,
                effectivenessMultiplier: 1.22,
                requiredCashUsd: 380_000_000,
                purchasePriceUsd: 0,
                earliestDate: GameDate.FromCalendar(2024, 1, 1),
                level: 4),

            new(OfficeTier.MultiSite, "Multiple sites",
                "Three time zones and a travel budget. More people than any one room can hold, at the "
                + "price of nobody being in the same room.",
                desks: 200,
                monthlyRentUsd: 2_400_000,
                fitOutCostUsd: 70_000_000,
                effectivenessMultiplier: 1.12,
                requiredCashUsd: 800_000_000,
                purchasePriceUsd: 0,
                earliestDate: GameDate.FromCalendar(2025, 1, 1),
                level: 5)
        };

        private static readonly Dictionary<OfficeTier, OfficeDefinition> ByTier = BuildIndex();

        /// <summary>
        /// Places that are announced and not built.
        ///
        /// **Deliberately not `OfficeTier` members and deliberately not in `Entries`.** Those values
        /// are written into saves, so adding two the player can never occupy would put two numbers
        /// into the format that mean nothing, forever, to buy a caption. These are a separate small
        /// list that only the chooser reads, they carry no economics, and deleting them is deleting
        /// one field.
        ///
        /// They exist because a ladder that stops without saying so reads as a ladder you have
        /// finished climbing. Showing where it goes is the difference between a player who knows
        /// there is more coming and one who concludes the game ended at the top floor.
        /// </summary>
        public readonly struct AnnouncedOffice
        {
            public AnnouncedOffice(string nameKey, string noteKey, int desks)
            {
                NameKey = nameKey;
                NoteKey = noteKey;
                Desks = desks;
            }

            public string NameKey { get; }
            public string NoteKey { get; }

            /// <summary>What it would hold, so the ladder reads as a ladder rather than as a rumour.</summary>
            public int Desks { get; }

            public string DisplayName => Loc.T(NameKey);
            public string Note => Loc.T(NoteKey);
        }

        private static readonly AnnouncedOffice[] Announced =
        {
            new("office.soon.tower.name", "office.soon.tower.note", 320),
            new("office.soon.campus.name", "office.soon.campus.note", 500)
        };

        public static IReadOnlyList<AnnouncedOffice> ComingSoon => Announced;

        public static IReadOnlyList<OfficeDefinition> All => Entries;

        /// <summary>
        /// The tiers the chooser offers, which is the ones with somewhere to move into.
        ///
        /// Separate from <see cref="All"/> on purpose. A saved company can be sitting in a tier that
        /// has no picture yet, and it has to keep working; what it must not do is appear as an
        /// option to a company that is not already there.
        /// </summary>
        public static List<OfficeDefinition> Places()
        {
            var places = new List<OfficeDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                if (entry.HasPlace)
                {
                    places.Add(entry);
                }
            }

            return places;
        }

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
