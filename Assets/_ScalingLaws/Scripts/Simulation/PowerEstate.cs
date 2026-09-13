using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the company owns that makes electricity, and what that is worth on a given day.
    ///
    /// **It holds dates, not decisions.** Money moves in
    /// <see cref="CompanySimulation.TryBuildPowerPlant"/>, the same split the cabinet store and the
    /// decor plan already keep: the estate knows which sites are under construction and which are
    /// running, and nothing here can charge anybody for anything.
    ///
    /// A site is commissioned once. There is no second gas plant on the same river, and allowing
    /// one would turn the ceiling into a slider bought with money, which is the failure the whole
    /// research tree exists to prevent.
    /// </summary>
    public sealed class PowerEstate
    {
        private readonly Dictionary<PowerPlantSite, GameDate> readyOn = new();

        /// <summary>Every site the company has paid for, running or not, with the day it opens.</summary>
        public IReadOnlyDictionary<PowerPlantSite, GameDate> Commissioned => readyOn;

        public int Count => readyOn.Count;

        public bool Owns(PowerPlantSite site) => readyOn.ContainsKey(site);

        /// <summary>Whether the site is paid for and still being built on this date.</summary>
        public bool IsBuilding(PowerPlantSite site, GameDate on) =>
            readyOn.TryGetValue(site, out var ready) && ready.DayIndex > on.DayIndex;

        /// <summary>Whether the site is generating on this date.</summary>
        public bool IsOnline(PowerPlantSite site, GameDate on) =>
            readyOn.TryGetValue(site, out var ready) && ready.DayIndex <= on.DayIndex;

        /// <summary>The day a site opens, or null when the company has not bought it.</summary>
        public GameDate? ReadyDate(PowerPlantSite site) =>
            readyOn.TryGetValue(site, out var ready) ? ready : null;

        /// <summary>
        /// Records a commissioned site. Refuses a second one on the same ground.
        /// </summary>
        public bool TryCommission(PowerPlantSite site, GameDate ready)
        {
            if (readyOn.ContainsKey(site))
            {
                return false;
            }

            readyOn[site] = ready;
            return true;
        }

        /// <summary>
        /// Everything generating on this date, in kilowatts.
        ///
        /// **A site under construction supplies nothing.** Counting it would let a player buy the
        /// ceiling and the cluster in the same week, and the nine years the coastal block takes are
        /// the whole reason it is a different decision from the gas plant.
        /// </summary>
        public double CapacityKilowatts(GameDate on)
        {
            var total = 0.0;

            foreach (var pair in readyOn)
            {
                if (pair.Value.DayIndex <= on.DayIndex)
                {
                    total += PowerPlantCatalog.Get(pair.Key).Kilowatts;
                }
            }

            return total;
        }

        /// <summary>Fuel and fixed costs for everything running today.</summary>
        public double DailyRunningCostUsd(GameDate on)
        {
            var total = 0.0;

            foreach (var pair in readyOn)
            {
                if (pair.Value.DayIndex <= on.DayIndex)
                {
                    total += PowerPlantCatalog.DailyRunningCostUsd(PowerPlantCatalog.Get(pair.Key));
                }
            }

            return total;
        }

        /// <summary>
        /// What today's generation is worth to the company, in dollars.
        ///
        /// Two halves, and they are priced differently on purpose:
        ///
        /// - **Power the company uses itself** is worth what it would otherwise have paid for it.
        ///   That price is not a constant here; it is `billedUsd / drawnKilowattHours`, read back
        ///   off the bill the fleet actually raised, so there is exactly one opinion in the game
        ///   about what a kilowatt hour costs a lab.
        /// - **Power nobody used** goes to the grid at
        ///   <see cref="PowerPlantCatalog.WholesalePricePerKilowattHourUsd"/>, which is a third of
        ///   the cheapest tariff the company pays. Selling at the retail rate would make a plant a
        ///   money printer.
        ///
        /// The running cost is charged whatever happens, so a plant built years ahead of the
        /// cluster it is for loses money every day until the cluster arrives. That is the timing
        /// pressure this game is made of, applied to a building.
        /// </summary>
        public double DailyValueUsd(GameDate on, double drawnKilowatts, double billedElectricityUsd)
        {
            var capacity = CapacityKilowatts(on);

            if (capacity <= 0.0)
            {
                return 0.0;
            }

            var generated = capacity * SimUnits.HoursPerDay;
            var drawn = Math.Max(0.0, SimUnits.Finite(drawnKilowatts)) * SimUnits.HoursPerDay;

            var selfUsed = Math.Min(generated, drawn);
            var surplus = generated - selfUsed;

            // What the fleet is actually being charged, per kilowatt hour. Derived rather than
            // named, because the tariff differs by tier and by what the room has researched, and a
            // second copy of any of those would be a rate changed in one place and quoted from the
            // other.
            var tariff = drawn > 0.0
                ? Math.Max(0.0, SimUnits.Finite(billedElectricityUsd)) / drawn
                : 0.0;

            return selfUsed * tariff
                + surplus * PowerPlantCatalog.WholesalePricePerKilowattHourUsd;
        }

        // ---- the save ----------------------------------------------------------------------------

        public void Capture(List<int> sites, List<int> readyDays)
        {
            sites.Clear();
            readyDays.Clear();

            foreach (var pair in readyOn)
            {
                sites.Add((int)pair.Key);
                readyDays.Add(pair.Value.DayIndex);
            }
        }

        public void Restore(IReadOnlyList<int> sites, IReadOnlyList<int> readyDays)
        {
            readyOn.Clear();

            if (sites == null || readyDays == null)
            {
                return;
            }

            for (var index = 0; index < sites.Count && index < readyDays.Count; index++)
            {
                if (!Enum.IsDefined(typeof(PowerPlantSite), sites[index]))
                {
                    continue;
                }

                readyOn[(PowerPlantSite)sites[index]] =
                    new GameDate(Math.Max(0, readyDays[index]));
            }
        }
    }
}
