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
        public CountryDefinition(Country country, WorldRegion region, string displayName,
            double taxRate, double hardwarePriceMultiplier, double innovationMultiplier,
            double localCompetitionMultiplier, string note)
        {
            Country = country;
            Region = region;
            DisplayName = displayName ?? country.ToString();
            TaxRate = Math.Clamp(taxRate, 0.0, 0.6);
            HardwarePriceMultiplier = Math.Clamp(hardwarePriceMultiplier, 0.5, 2.0);
            InnovationMultiplier = Math.Clamp(innovationMultiplier, 0.5, 2.0);
            LocalCompetitionMultiplier = Math.Clamp(localCompetitionMultiplier, 0.2, 2.5);
            Note = note ?? string.Empty;
        }

        public Country Country { get; }
        public WorldRegion Region { get; }
        public string DisplayName { get; }

        /// <summary>Share of daily operating profit taken before it reaches the balance.</summary>
        public double TaxRate { get; }

        /// <summary>What accelerators cost here. Below one is cheap, above one is a supply problem.</summary>
        public double HardwarePriceMultiplier { get; }

        /// <summary>Research and training speed. Talent density, not patriotism.</summary>
        public double InnovationMultiplier { get; }

        /// <summary>How crowded the local market is. Above one and your brand counts for less.</summary>
        public double LocalCompetitionMultiplier { get; }

        public string Note { get; }

        public override string ToString() => $"{DisplayName} ({Region})";
    }

    public sealed class RegionDefinition
    {
        public RegionDefinition(WorldRegion region, string displayName, string blurb)
        {
            Region = region;
            DisplayName = displayName ?? region.ToString();
            Blurb = blurb ?? string.Empty;
        }

        public WorldRegion Region { get; }
        public string DisplayName { get; }
        public string Blurb { get; }
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
            new(WorldRegion.America, "America",
                "Where the accelerators ship first and where everyone else already is."),
            new(WorldRegion.Europe, "Europe",
                "Cheaper to run, slower to supply, and a regulator that will read what you publish."),
            new(WorldRegion.Asia, "Asia",
                "Closest to the factories. Silicon is cheap here and attention is not.")
        };

        private static readonly CountryDefinition[] Countries =
        {
            new(Country.UnitedStates, WorldRegion.America, "United States",
                0.21, 0.92, 1.15, 1.35, "First in line for every launch, and so is everybody else."),
            new(Country.Canada, WorldRegion.America, "Canada",
                0.26, 1.00, 1.05, 0.85, "The research is here. The capital is one border away."),
            new(Country.Brazil, WorldRegion.America, "Brazil",
                0.34, 1.18, 0.90, 0.60, "Almost nobody is competing for these users yet."),
            new(Country.Mexico, WorldRegion.America, "Mexico",
                0.30, 1.12, 0.92, 0.60, "Cheap to staff, close enough to ship to."),

            new(Country.UnitedKingdom, WorldRegion.Europe, "United Kingdom",
                0.25, 1.04, 1.10, 1.00, "Deep research bench, thin domestic market."),
            new(Country.Germany, WorldRegion.Europe, "Germany",
                0.30, 1.06, 1.08, 0.90, "Industrial customers who pay on time and read the contract."),
            new(Country.France, WorldRegion.Europe, "France",
                0.25, 1.05, 1.06, 0.90, "State money is available if the state likes you."),
            new(Country.Poland, WorldRegion.Europe, "Poland",
                0.19, 1.10, 0.98, 0.55, "Low tax, low payroll, hardware arrives late."),
            new(Country.Ireland, WorldRegion.Europe, "Ireland",
                0.13, 1.08, 1.00, 0.70, "The tax rate is the entire pitch."),
            new(Country.Switzerland, WorldRegion.Europe, "Switzerland",
                0.15, 1.10, 1.12, 0.65, "Expensive people, cheap taxes, quiet neighbours."),

            new(Country.Japan, WorldRegion.Asia, "Japan",
                0.30, 0.98, 1.08, 0.85, "Patient customers, punishing tax."),
            new(Country.SouthKorea, WorldRegion.Asia, "South Korea",
                0.24, 0.94, 1.10, 0.80, "Memory is made here, so memory is cheap here."),
            new(Country.Taiwan, WorldRegion.Asia, "Taiwan",
                0.20, 0.88, 1.05, 0.70, "The wafers start here. Nothing is closer to the source."),
            new(Country.Singapore, WorldRegion.Asia, "Singapore",
                0.17, 1.00, 1.05, 0.75, "A small market that everything in the region routes through."),
            new(Country.India, WorldRegion.Asia, "India",
                0.25, 1.14, 0.96, 0.65, "More engineers than anywhere, less silicon than anywhere."),
            new(Country.China, WorldRegion.Asia, "China",
                0.25, 1.20, 1.05, 1.20, "Enormous demand, and export controls on everything you need.")
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

            return new CountryDefinition(Country.None, region, Get(region).DisplayName,
                list.Average(c => c.TaxRate),
                list.Average(c => c.HardwarePriceMultiplier),
                list.Average(c => c.InnovationMultiplier),
                list.Average(c => c.LocalCompetitionMultiplier),
                Get(region).Blurb);
        }
    }
}
