using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>Where the company is registered. Three regions, picked once, never changed.</summary>
    public enum WorldRegion
    {
        None = 0,
        America = 1,
        Europe = 2,
        Asia = 3
    }

    public enum Country
    {
        None = 0,
        UnitedStates = 1,
        Canada = 2,
        Brazil = 3,
        Mexico = 4,
        UnitedKingdom = 10,
        Germany = 11,
        France = 12,
        Poland = 13,
        Ireland = 14,
        Switzerland = 15,
        Japan = 20,
        SouthKorea = 21,
        Taiwan = 22,
        Singapore = 23,
        India = 24,
        China = 25
    }

    /// <summary>
    /// One country's standing on the four axes the player is shown. Every one of them is a trade:
    /// the places with the cheapest accelerators are the places with the most rivals, and the
    /// places with no rivals are the places where hardware arrives late and expensive.
    ///
    /// Tax is a share of daily operating profit. The others are multipliers on numbers that
    /// already exist, so a region never adds a rule, it only moves an existing one.
    /// </summary>
    public sealed class CountryDefinition
    {
        private readonly string stem;

        public CountryDefinition(Country country, WorldRegion region,
            double taxRate, double hardwarePriceMultiplier, double innovationMultiplier,
            double localCompetitionMultiplier, string stem = null)
        {
            Country = country;
            Region = region;

            // **The stem is an argument because one of these is not a country.** `Average` builds a
            // synthetic entry standing for a whole region, and it borrows the region's own words
            // rather than inventing a seventeenth set. Everything else takes its own.
            this.stem = string.IsNullOrEmpty(stem) ? KeyFor(country) : stem;

            TaxRate = Math.Clamp(taxRate, 0.0, 0.6);
            HardwarePriceMultiplier = Math.Clamp(hardwarePriceMultiplier, 0.5, 2.0);
            InnovationMultiplier = Math.Clamp(innovationMultiplier, 0.5, 2.0);
            LocalCompetitionMultiplier = Math.Clamp(localCompetitionMultiplier, 0.2, 2.5);
        }

        public Country Country { get; }
        public WorldRegion Region { get; }

        /// <summary>
        /// The phrase-book stem for a country.
        ///
        /// Written out rather than derived, same as every catalog here. A country name is not a
        /// place to be clever: this list is the first screen of a new campaign.
        /// </summary>
        private static string KeyFor(Country country) => country switch
        {
            Country.UnitedStates => "country.us",
            Country.Canada => "country.canada",
            Country.Brazil => "country.brazil",
            Country.Mexico => "country.mexico",
            Country.UnitedKingdom => "country.uk",
            Country.Germany => "country.germany",
            Country.France => "country.france",
            Country.Poland => "country.poland",
            Country.Ireland => "country.ireland",
            Country.Switzerland => "country.switzerland",
            Country.Japan => "country.japan",
            Country.SouthKorea => "country.southkorea",
            Country.Taiwan => "country.taiwan",
            Country.Singapore => "country.singapore",
            Country.India => "country.india",
            _ => "country.china"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(stem);

        /// <summary>Share of daily operating profit taken before it reaches the balance.</summary>
        public double TaxRate { get; }

        /// <summary>What accelerators cost here. Below one is cheap, above one is a supply problem.</summary>
        public double HardwarePriceMultiplier { get; }

        /// <summary>Research and training speed. Talent density, not patriotism.</summary>
        public double InnovationMultiplier { get; }

        /// <summary>How crowded the local market is. Above one and your brand counts for less.</summary>
        public double LocalCompetitionMultiplier { get; }

        public string Note => Loc.T(stem + ".note");

        public override string ToString() => $"{DisplayName} ({Region})";
    }

    public sealed class RegionDefinition
    {
        public RegionDefinition(WorldRegion region)
        {
            Region = region;
        }

        public WorldRegion Region { get; }

        /// <summary>
        /// The phrase-book stem for a region.
        ///
        /// Public because `WorldRegionCatalog.Average` hands it to a synthetic country standing for
        /// the whole region, which is how that row gets the region's name and blurb without a
        /// second copy of either.
        /// </summary>
        public static string StemFor(WorldRegion region) => region switch
        {
            WorldRegion.Europe => "region.europe",
            WorldRegion.Asia => "region.asia",
            _ => "region.america"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(StemFor(Region));

        public string Blurb => Loc.T(StemFor(Region) + ".note");
    }

    /// <summary>
    /// The regions, their countries, and the averages the map shows before a country is picked.
    /// Numbers are rounded from published corporate rates and from where the hardware actually
    /// ships first. They are coarse on purpose: this is a strategic choice, not a tax return.
    /// </summary>
    public static class WorldRegionCatalog
    {
        public const string CatalogVersion = "regions-2026-08-04";

        private static readonly RegionDefinition[] Regions =
        {
            // The words are `region.*` and `country.*` in the phrase book.
            new(WorldRegion.America),
            new(WorldRegion.Europe),
            new(WorldRegion.Asia)
        };

        private static readonly CountryDefinition[] Countries =
        {
            new(Country.UnitedStates, WorldRegion.America, 0.21, 0.92, 1.15, 1.35),
            new(Country.Canada, WorldRegion.America, 0.26, 1.00, 1.05, 0.85),
            new(Country.Brazil, WorldRegion.America, 0.34, 1.18, 0.90, 0.60),
            new(Country.Mexico, WorldRegion.America, 0.30, 1.12, 0.92, 0.60),

            new(Country.UnitedKingdom, WorldRegion.Europe, 0.25, 1.04, 1.10, 1.00),
            new(Country.Germany, WorldRegion.Europe, 0.30, 1.06, 1.08, 0.90),
            new(Country.France, WorldRegion.Europe, 0.25, 1.05, 1.06, 0.90),
            new(Country.Poland, WorldRegion.Europe, 0.19, 1.10, 0.98, 0.55),
            new(Country.Ireland, WorldRegion.Europe, 0.13, 1.08, 1.00, 0.70),
            new(Country.Switzerland, WorldRegion.Europe, 0.15, 1.10, 1.12, 0.65),

            new(Country.Japan, WorldRegion.Asia, 0.30, 0.98, 1.08, 0.85),
            new(Country.SouthKorea, WorldRegion.Asia, 0.24, 0.94, 1.10, 0.80),
            new(Country.Taiwan, WorldRegion.Asia, 0.20, 0.88, 1.05, 0.70),
            new(Country.Singapore, WorldRegion.Asia, 0.17, 1.00, 1.05, 0.75),
            new(Country.India, WorldRegion.Asia, 0.25, 1.14, 0.96, 0.65),
            new(Country.China, WorldRegion.Asia, 0.25, 1.20, 1.05, 1.20)
        };

        public static IReadOnlyList<RegionDefinition> All => Regions;

        public static RegionDefinition Get(WorldRegion region) =>
            Regions.FirstOrDefault(r => r.Region == region) ?? Regions[0];

        public static IReadOnlyList<CountryDefinition> AllCountries => Countries;

        public static IReadOnlyList<CountryDefinition> CountriesIn(WorldRegion region) =>
            Countries.Where(c => c.Region == region).ToArray();

        public static CountryDefinition Get(Country country) =>
            Countries.FirstOrDefault(c => c.Country == country) ?? Countries[0];

        /// <summary>The default country for a region, used until the player picks one themselves.</summary>
        public static Country FirstIn(WorldRegion region)
        {
            var list = CountriesIn(region);
            return list.Count > 0 ? list[0].Country : Country.UnitedStates;
        }

        /// <summary>
        /// What the map shows for a whole region: the plain average of its countries. Shown before
        /// a country is chosen so the three regions can be compared at a glance, and replaced by the
        /// exact figures the moment one is.
        /// </summary>
        public static CountryDefinition Average(WorldRegion region)
        {
            var list = CountriesIn(region);
            if (list.Count == 0)
            {
                return Countries[0];
            }

            return new CountryDefinition(Country.None, region,
                list.Average(c => c.TaxRate),
                list.Average(c => c.HardwarePriceMultiplier),
                list.Average(c => c.InnovationMultiplier),
                list.Average(c => c.LocalCompetitionMultiplier),
                RegionDefinition.StemFor(region));
        }
    }
}
