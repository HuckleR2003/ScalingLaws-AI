using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The lab the player takes over at the start. Explicit values, saved, never renumbered.
    /// </summary>
    public enum CompanyArchetype
    {
        Custom = 0,
        OpenSi = 1,
        Antropic = 2,
        DeepSearch = 3,
        HuggyFace = 4
    }

    /// <summary>
    /// One of the four labs on the opening screen, plus the blank slate.
    ///
    /// These are affectionate near misses of the labs that actually existed in 2022, and they exist
    /// so the first decision of a campaign already has a point of view. Each one starts the player
    /// somewhere different on the same map: money, reputation, what is already in the building.
    ///
    /// No art needed. Each carries an accent colour and a one or two character mark, which the tile
    /// renders directly, so the opening screen reads as four distinct companies with nothing to
    /// import.
    /// </summary>
    public readonly struct CompanyIdentityDefinition
    {
        public CompanyIdentityDefinition(
            CompanyArchetype archetype,
            string displayName,
            string mark,
            string accentHex,
            long startingCashUsd,
            double startingReputation,
            DatasetSource startingData,
            FounderTrait houseTrait,
            double operatingCostMultiplier = 1.0,
            double priceMultiplier = 1.0)
        {
            Archetype = archetype;
            StoredName = string.IsNullOrWhiteSpace(displayName) ? archetype.ToString() : displayName;
            Mark = string.IsNullOrWhiteSpace(mark) ? "?" : mark;
            AccentHex = string.IsNullOrWhiteSpace(accentHex) ? "#3A8ADC" : accentHex;
            StartingCashUsd = Math.Clamp(startingCashUsd, 1_000_000L, 1_000_000_000L);
            StartingReputation = Math.Clamp(SimUnits.Finite(startingReputation), 0.0, 1.0);
            StartingData = startingData;
            HouseTrait = houseTrait;
            OperatingCostMultiplier = Math.Clamp(SimUnits.Finite(operatingCostMultiplier, 1.0), 0.5, 2.0);
            PriceMultiplier = Math.Clamp(SimUnits.Finite(priceMultiplier, 1.0), 0.1, 5.0);
        }

        public CompanyArchetype Archetype { get; }

        private string StoredName { get; }

        /// <summary>
        /// What the tile is called.
        ///
        /// Four of the five are company names and stay exactly as written: a name is not translated.
        /// The fifth is not a name, it is the words "your own company", and that one reads from the
        /// book like everything else a player is shown.
        /// </summary>
        public string DisplayName =>
            Archetype == CompanyArchetype.Custom ? Loc.T("archetype.own.name") : StoredName;

        /// <summary>One or two characters drawn as the logo. No texture required.</summary>
        public string Mark { get; }

        public string AccentHex { get; }

        /// <summary>
        /// The stem this archetype's phrases hang off.
        ///
        /// **`DisplayName` deliberately stays a literal.** OpenSI, Antropic, DeepSearch and HuggyFace
        /// are names of companies and a name is not translated. Only the one that is not a name,
        /// the player's own company, reads from the book.
        /// </summary>
        private static string KeyFor(CompanyArchetype archetype) => archetype switch
        {
            CompanyArchetype.Antropic => "archetype.careful",
            CompanyArchetype.DeepSearch => "archetype.research",
            CompanyArchetype.HuggyFace => "archetype.open",
            CompanyArchetype.Custom => "archetype.own",
            _ => "archetype.shipfast"
        };

        public string Tagline => Loc.T(KeyFor(Archetype) + ".tag");

        /// <summary>The paragraph shown when the tile is picked. Sets the tone of the campaign.</summary>
        public string Opening => Loc.T(KeyFor(Archetype) + ".blurb");

        public long StartingCashUsd { get; }
        public double StartingReputation { get; }
        public DatasetSource StartingData { get; }

        /// <summary>A trait the house comes with, on top of the two the founder picks.</summary>
        public FounderTrait HouseTrait { get; }

        public double OperatingCostMultiplier { get; }

        /// <summary>Default price on the first model. Open labs undercut, closed labs do not.</summary>
        public double PriceMultiplier { get; }

        public override string ToString() => $"{DisplayName} ({Archetype})";
    }

    /// <summary>The ONE company identity library.</summary>
    /// <summary>
    /// Names for the people who write in looking for work.
    ///
    /// Deliberately plain and deliberately from everywhere, because the company can be registered in
    /// any of sixteen countries and a mailbox full of one nationality would be a claim the game is
    /// not making. Two lists crossed rather than one list of full names, so a few dozen entries give
    /// several hundred people without anybody noticing a repeat.
    /// </summary>
    public static class ApplicantNames
    {
        private static readonly string[] First =
        {
            "Aleksy", "Amara", "Anders", "Ayla", "Bea", "Caio", "Dara", "Eero", "Elif", "Fen",
            "Gita", "Hana", "Idris", "Ines", "Jonas", "Kaia", "Lars", "Lena", "Malik", "Mira",
            "Nadia", "Noor", "Omar", "Petra", "Rafa", "Rina", "Samir", "Sofia", "Tariq", "Vera",
            "Wen", "Yusuf", "Zara", "Bruno", "Cato", "Ilya"
        };

        private static readonly string[] Last =
        {
            "Adeyemi", "Bergstrom", "Castellan", "Dlamini", "Eriksen", "Farah", "Gallo", "Haddad",
            "Iversen", "Jansen", "Kowalczyk", "Lindqvist", "Moreau", "Nakamura", "Okafor", "Pereira",
            "Quintana", "Rossi", "Sandoval", "Tanaka", "Ueda", "Varga", "Weiss", "Xu", "Yilmaz",
            "Zielinski"
        };

        public static string Pick(DeterministicRandom random) =>
            First[random.NextInt(0, First.Length)] + " " + Last[random.NextInt(0, Last.Length)];
    }

    public static class CompanyIdentityCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        private static readonly CompanyIdentityDefinition[] Entries =
        {
            new(CompanyArchetype.OpenSi, "OpenSI", "OI", "#12B886",
                startingCashUsd: 14_000_000,
                startingReputation: 0.16,
                startingData: DatasetSource.WebCrawl,
                houseTrait: FounderTrait.SilverTongue,
                operatingCostMultiplier: 1.10,
                priceMultiplier: 1.0),

            new(CompanyArchetype.Antropic, "Antropic", "A", "#D9822B",
                startingCashUsd: 11_000_000,
                startingReputation: 0.10,
                startingData: DatasetSource.WebCrawl,
                houseTrait: FounderTrait.SafetyAdvocate,
                operatingCostMultiplier: 0.98,
                priceMultiplier: 1.15),

            new(CompanyArchetype.DeepSearch, "DeepSearch", "DS", "#4C6EF5",
                startingCashUsd: 16_000_000,
                startingReputation: 0.06,
                startingData: DatasetSource.WebCrawl,
                houseTrait: FounderTrait.Researcher,
                operatingCostMultiplier: 1.04,
                priceMultiplier: 1.0),

            new(CompanyArchetype.HuggyFace, "HuggyFace", "HF", "#F59F00",
                startingCashUsd: 9_000_000,
                startingReputation: 0.22,
                startingData: DatasetSource.WebCrawl | DatasetSource.CodeCorpus,
                houseTrait: FounderTrait.DataHoarder,
                operatingCostMultiplier: 0.94,
                priceMultiplier: 0.45),

            new(CompanyArchetype.Custom, "Your own company", "+", "#8A9BB0",
                startingCashUsd: 12_000_000,
                startingReputation: 0.05,
                startingData: DatasetSource.WebCrawl,
                houseTrait: FounderTrait.None,
                operatingCostMultiplier: 1.0,
                priceMultiplier: 1.0)
        };

        private static readonly Dictionary<CompanyArchetype, CompanyIdentityDefinition> ByArchetype = BuildIndex();

        public static IReadOnlyList<CompanyIdentityDefinition> All => Entries;

        /// <summary>The four labs shown as tiles, in order. The custom slate is offered separately.</summary>
        public static IEnumerable<CompanyIdentityDefinition> Tiles()
        {
            foreach (var entry in Entries)
            {
                if (entry.Archetype != CompanyArchetype.Custom)
                {
                    yield return entry;
                }
            }
        }

        public static CompanyIdentityDefinition Get(CompanyArchetype archetype) =>
            ByArchetype.TryGetValue(archetype, out var definition)
                ? definition
                : ByArchetype[CompanyArchetype.Custom];

        public static bool TryGet(CompanyArchetype archetype, out CompanyIdentityDefinition definition) =>
            ByArchetype.TryGetValue(archetype, out definition);

        private static Dictionary<CompanyArchetype, CompanyIdentityDefinition> BuildIndex()
        {
            var index = new Dictionary<CompanyArchetype, CompanyIdentityDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Archetype] = entry;
            }

            return index;
        }
    }
}
