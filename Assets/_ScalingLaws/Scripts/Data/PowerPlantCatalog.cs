using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Which of the two sites a plant stands on. Written into saves; never renumber.</summary>
    public enum PowerPlantSite
    {
        /// <summary>Gas turbines by the river. Quick to build, expensive to run.</summary>
        Riverside = 0,

        /// <summary>A nuclear block on the coast. A decade to build, almost free to run.</summary>
        Coastal = 1
    }

    /// <summary>One buildable power station.</summary>
    public readonly struct PowerPlantDefinition
    {
        public PowerPlantDefinition(PowerPlantSite site, string key, long capexUsd,
            double megawatts, int buildDays, double fuelCostPerKilowattHourUsd,
            double dailyUpkeepUsd, GameDate earliest)
        {
            Site = site;
            Key = key;
            CapexUsd = Math.Max(0L, capexUsd);
            Megawatts = Math.Max(0.0, SimUnits.Finite(megawatts));
            BuildDays = Math.Max(1, buildDays);
            FuelCostPerKilowattHourUsd = Math.Max(0.0, SimUnits.Finite(fuelCostPerKilowattHourUsd));
            DailyUpkeepUsd = Math.Max(0.0, SimUnits.Finite(dailyUpkeepUsd));
            Earliest = earliest;
        }

        public PowerPlantSite Site { get; }

        /// <summary>The phrase-book stem. A literal, so the localisation guard can see it.</summary>
        public string Key { get; }

        public long CapexUsd { get; }

        public double Megawatts { get; }

        /// <summary>What it supplies, in the unit everything else in this game bills power in.</summary>
        public double Kilowatts => Megawatts * 1000.0;

        public int BuildDays { get; }

        /// <summary>Fuel, per kilowatt hour generated, whether or not anybody uses it.</summary>
        public double FuelCostPerKilowattHourUsd { get; }

        /// <summary>Staff, inspections and everything that does not scale with output.</summary>
        public double DailyUpkeepUsd { get; }

        /// <summary>The first date a company could commission it.</summary>
        public GameDate Earliest { get; }

        public string DisplayName => Loc.T(Key);

        public string Description => Loc.T(Key + ".note");
    }

    /// <summary>
    /// The two power stations a company can build, and why a lab would ever build one.
    ///
    /// **The ceiling is the product. The income is the consolation.** Measured over nine campaigns
    /// on 2026-09-13: a company that buys its own accelerators stops growing at 2,500 kW at the end
    /// of its third year, because that is what `ComputeTier.ColocatedServers` supplies and it is
    /// added once and never again. Past that there is one step, the $80M datacenter at 40,000 kW,
    /// and then nothing at all. A plant is what a company that wants to run a real frontier cluster
    /// has to own, the same way the real ones started signing for reactors.
    ///
    /// **Nobody should build one expecting it to pay for itself in electricity, and the card says
    /// so.** At $0.045 a kilowatt hour wholesale, the coastal block returns a little over $200M a
    /// year on a $14bn build. That is not a mistake in the numbers, it is what merchant generation
    /// actually earns, and pretending otherwise would make this the one purchase in the game with a
    /// guaranteed return. What it buys is the right to draw 1.1 GW, plus enough income to carry the
    /// site during the years the company is growing into it.
    ///
    /// ### The two, and neither is simply better
    ///
    /// Same output, opposite shapes. Gas is two and a half years and cheap to build, then $0.042 a
    /// kilowatt hour forever. Nuclear is nine years and costs nearly five times as much, then
    /// $0.009. A company that needs the power before 2030 has one choice and a company planning the
    /// endgame has the other, which is the spine of this game applied to a building.
    ///
    /// Capital costs are anchored on published figures: the EIA's capital cost estimates put a
    /// combined cycle plant near $1,100/kW and advanced nuclear near $6,700/kW, and the Vogtle
    /// units actually came in around $15,700/kW. Both entries here sit inside those bands once
    /// grid connection and land are in the number. Fuel is a gas price near $6/MMBtu at a heat
    /// rate of about 6,500 Btu/kWh, and a nuclear fuel cost in the region of a cent.
    /// </summary>
    public static class PowerPlantCatalog
    {
        public const string CatalogVersion = "plants-1";

        /// <summary>
        /// What a merchant generator gets for a kilowatt hour it puts on the grid.
        ///
        /// Deliberately far under every tariff the company pays: a wholesale price and a retail
        /// price are different things, and a plant that sold at the retail rate would make building
        /// one a money printer rather than a way to be allowed to draw a gigawatt.
        /// </summary>
        public const double WholesalePricePerKilowattHourUsd = 0.045;

        private static readonly PowerPlantDefinition[] Entries =
        {
            new(PowerPlantSite.Riverside, "plant.riverside",
                capexUsd: 3_000_000_000L,
                megawatts: 1_100.0,
                buildDays: 900,
                fuelCostPerKilowattHourUsd: 0.042,
                dailyUpkeepUsd: 42_000.0,
                earliest: GameDate.Start),

            new(PowerPlantSite.Coastal, "plant.coastal",
                capexUsd: 14_000_000_000L,
                megawatts: 1_100.0,
                buildDays: 3_300,
                fuelCostPerKilowattHourUsd: 0.009,
                dailyUpkeepUsd: 392_000.0,
                earliest: GameDate.Start)
        };

        public static IReadOnlyList<PowerPlantDefinition> All => Entries;

        public static PowerPlantDefinition Get(PowerPlantSite site)
        {
            foreach (var entry in Entries)
            {
                if (entry.Site == site)
                {
                    return entry;
                }
            }

            return Entries[0];
        }

        /// <summary>
        /// What one plant costs to run for a day, fuel at full output plus everything fixed.
        ///
        /// **Full output, not what was used.** A turbine spinning for a cluster that is half idle
        /// still burns, and a plant that only cost money when the company needed it would be a
        /// tariff with extra steps.
        /// </summary>
        public static double DailyRunningCostUsd(PowerPlantDefinition plant) =>
            plant.Kilowatts * SimUnits.HoursPerDay * plant.FuelCostPerKilowattHourUsd
            + plant.DailyUpkeepUsd;
    }
}
