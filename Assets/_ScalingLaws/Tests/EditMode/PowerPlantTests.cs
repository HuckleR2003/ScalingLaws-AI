using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Power stations: the ceiling they lift, the money they make, and the money they cost.
    ///
    /// **Built because a measurement, not a hunch.** Nine scripted campaigns on 2026-09-13 showed a
    /// company that buys its own accelerators stopping dead at 2,500 kW at the end of its third
    /// year and staying there for eleven, because that is what `ComputeTier.ColocatedServers`
    /// supplies and it is added once and never again. The refusal read
    /// "Draws 12,162 kW, the site provides 2,500 kW" every month and nothing else in the game
    /// mentioned that a ceiling existed.
    ///
    /// The first fixture below is therefore the one that matters: the same purchase, refused before
    /// the station and accepted after it.
    /// </summary>
    public sealed class PowerPlantTests
    {
        private static CompanySimulation Company(long cash = 20_000_000_000L)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2024, 1, 1),
                CashUsd = cash
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            return new CompanySimulation(state);
        }

        // ---- the ceiling, which is the product --------------------------------------------------

        [Test]
        public void AStationLiftsTheCeilingThatStoppedTheFleetGrowing()
        {
            var simulation = Company();

            var tier = ComputeTierCatalog.Get(ComputeTier.ColocatedServers);
            var card = HardwareCatalog.Get(HardwareGenerationId.AcceleratorH100);

            // Comfortably more than the colocated site supplies, and comfortably less than a
            // station does. Derived from the two capacities rather than typed, so a change to
            // either one moves the test with it instead of silently making it vacuous.
            var units = (int)(tier.PowerCapacityKilowatts / card.PowerKilowatts) + 400;

            Assert.That(simulation.TryBuyHardware(card.Id, units, ComputeTier.ColocatedServers,
                out var refused), Is.False,
                "This purchase has to be over the site's supply, or the fixture proves nothing.");

            Assert.That(refused, Does.Contain("2,500").Or.Contain("2 500").Or.Contain("kW"));

            Assert.That(simulation.TryBuildPowerPlant(PowerPlantSite.Riverside, out var why),
                Is.True, why);

            // Still refused while it is being built. Nine years of waiting is the decision.
            Assert.That(simulation.TryBuyHardware(card.Id, units, ComputeTier.ColocatedServers,
                out _), Is.False, "A station under construction supplies nothing.");

            simulation.State.Date = simulation.State.Date.AddDays(
                PowerPlantCatalog.Get(PowerPlantSite.Riverside).BuildDays + 1);

            Assert.That(simulation.TryBuyHardware(card.Id, units, ComputeTier.ColocatedServers,
                out var accepted), Is.True, accepted);
        }

        [Test]
        public void TheCeilingIsOneReadingAndTheStationIsInIt()
        {
            var simulation = Company();
            var before = simulation.SitePowerCapacityKilowatts();

            simulation.TryBuildPowerPlant(PowerPlantSite.Riverside, out _);

            Assert.That(simulation.SitePowerCapacityKilowatts(), Is.EqualTo(before),
                "A site being built supplies nothing.");

            simulation.State.Date = simulation.State.Date.AddDays(
                PowerPlantCatalog.Get(PowerPlantSite.Riverside).BuildDays);

            Assert.That(simulation.SitePowerCapacityKilowatts(),
                Is.EqualTo(before + PowerPlantCatalog.Get(PowerPlantSite.Riverside).Kilowatts)
                    .Within(0.001));

            // And the profile the fleet screen reads has to agree with the till.
            Assert.That(simulation.Profile.PowerCapacityKilowatts,
                Is.EqualTo(simulation.SitePowerCapacityKilowatts()).Within(0.001),
                "The number printed on the screen and the number the purchase is refused over "
                + "must be the same number.");
        }

        // ---- what it costs, and what it earns ---------------------------------------------------

        [Test]
        public void CommissioningChargesTheWholeCapexOnTheDay()
        {
            var simulation = Company();
            var plant = PowerPlantCatalog.Get(PowerPlantSite.Coastal);
            var before = simulation.State.CashUsd;

            Assert.That(simulation.TryBuildPowerPlant(PowerPlantSite.Coastal, out var why),
                Is.True, why);

            Assert.That(before - simulation.State.CashUsd, Is.EqualTo(plant.CapexUsd));
            Assert.That(simulation.State.Power.IsBuilding(PowerPlantSite.Coastal,
                simulation.State.Date), Is.True);
        }

        [Test]
        public void ASecondStationCannotStandOnTheSameGround()
        {
            var simulation = Company();

            Assert.That(simulation.TryBuildPowerPlant(PowerPlantSite.Riverside, out _), Is.True);

            var cash = simulation.State.CashUsd;

            Assert.That(simulation.TryBuildPowerPlant(PowerPlantSite.Riverside, out var why),
                Is.False);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cash),
                "A refused commission must not move money.");

            Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void ACompanyWithNoStationPaysAndEarnsNothingForOne()
        {
            var simulation = Company(cash: 400_000_000L);
            var books = simulation.State.Ledger;
            var month = MonthKey(simulation.State.Date);

            simulation.Advance(3);

            Assert.That(books.MonthTotal(month, LedgerLine.PowerGeneration), Is.Zero);
            Assert.That(books.MonthTotal(month, LedgerLine.PowerPlantUpkeep), Is.Zero,
                "Nothing about this feature may touch a company that never bought into it.");
        }

        [Test]
        public void ARunningStationEarnsAndCostsEveryDay()
        {
            var simulation = Company();
            simulation.TryBuildPowerPlant(PowerPlantSite.Riverside, out _);

            simulation.State.Date = simulation.State.Date.AddDays(
                PowerPlantCatalog.Get(PowerPlantSite.Riverside).BuildDays + 1);

            var month = MonthKey(simulation.State.Date);
            simulation.Advance(2);

            var books = simulation.State.Ledger;

            Assert.That(books.MonthTotal(month, LedgerLine.PowerGeneration), Is.GreaterThan(0L),
                "A station that generated nothing anybody paid for would be a hole in the books.");

            Assert.That(books.MonthTotal(month, LedgerLine.PowerPlantUpkeep), Is.GreaterThan(0L),
                "And it burns whether or not the cluster that needed it was ever built. That is "
                + "the timing pressure, applied to a building.");
        }

        /// <summary>
        /// A station is not an investment, and the arithmetic has to keep saying so.
        ///
        /// **This is the guard on the one thing that would break the spine.** There is no
        /// guaranteed profit anywhere in this game; a plant that paid back on power alone inside a
        /// campaign would be exactly that, bought once and collected from for fourteen years.
        /// </summary>
        [Test]
        public void NeitherStationPaysForItselfOnElectricityInsideACampaign()
        {
            foreach (var plant in PowerPlantCatalog.All)
            {
                var yearly = plant.Kilowatts * SimUnits.HoursPerDay * 365.0
                    * PowerPlantCatalog.WholesalePricePerKilowattHourUsd
                    - PowerPlantCatalog.DailyRunningCostUsd(plant) * 365.0;

                Assert.That(yearly, Is.GreaterThan(0.0),
                    $"{plant.Key} loses money simply by running, which makes it a trap rather "
                    + "than a trade.");

                var years = plant.CapexUsd / yearly;

                Assert.That(years, Is.GreaterThan(14.0),
                    $"{plant.Key} returns its capex in {years:0.0} years of power sales. The game "
                    + "is fourteen years long. What a station buys is the right to draw a "
                    + "gigawatt, and the day it also buys its own price back it is the only "
                    + "guaranteed return in the game.");
            }
        }

        [Test]
        public void PowerSoldIsWorthLessThanPowerBought()
        {
            foreach (var tier in ComputeTierCatalog.All)
            {
                if (tier.IsRented)
                {
                    continue;
                }

                Assert.That(PowerPlantCatalog.WholesalePricePerKilowattHourUsd,
                    Is.LessThan(tier.PowerCostPerKilowattHourUsd),
                    "Wholesale and retail are different prices. A station selling at the rate the "
                    + "company buys at would be a money printer.");
            }
        }

        /// <summary>
        /// The catalog rule every list in this game is held to: no entry is simply better.
        /// </summary>
        [Test]
        public void NeitherStationIsSimplyBetterThanTheOther()
        {
            var gas = PowerPlantCatalog.Get(PowerPlantSite.Riverside);
            var nuclear = PowerPlantCatalog.Get(PowerPlantSite.Coastal);

            Assert.That(gas.CapexUsd, Is.LessThan(nuclear.CapexUsd));
            Assert.That(gas.BuildDays, Is.LessThan(nuclear.BuildDays));
            Assert.That(gas.FuelCostPerKilowattHourUsd,
                Is.GreaterThan(nuclear.FuelCostPerKilowattHourUsd));

            Assert.That(PowerPlantCatalog.DailyRunningCostUsd(gas),
                Is.GreaterThan(PowerPlantCatalog.DailyRunningCostUsd(nuclear)),
                "Cheap and quick to build has to be dear to run, or the other one is decoration.");
        }

        // ---- the self-supplied half -------------------------------------------------------------

        /// <summary>
        /// Power the company burns itself is credited at the tariff it did not pay, and that tariff
        /// is read back off the bill rather than named here.
        /// </summary>
        [Test]
        public void SelfSuppliedPowerIsWorthTheTariffAndSurplusIsWorthWholesale()
        {
            var estate = new PowerEstate();
            var ready = GameDate.Start;
            estate.TryCommission(PowerPlantSite.Riverside, ready);

            var plant = PowerPlantCatalog.Get(PowerPlantSite.Riverside);
            var generatedKwh = plant.Kilowatts * SimUnits.HoursPerDay;

            // Nobody is drawing anything: everything goes to the grid.
            Assert.That(estate.DailyValueUsd(ready, 0.0, 0.0),
                Is.EqualTo(generatedKwh * PowerPlantCatalog.WholesalePricePerKilowattHourUsd)
                    .Within(0.01));

            // Drawing exactly what it makes, at twenty cents a unit: worth twenty cents a unit.
            var billed = generatedKwh * 0.20;

            Assert.That(estate.DailyValueUsd(ready, plant.Kilowatts, billed),
                Is.EqualTo(billed).Within(0.01),
                "Self supply is worth what it replaced, and what it replaced is on the bill.");

            Assert.That(estate.DailyValueUsd(ready, plant.Kilowatts, billed),
                Is.GreaterThan(estate.DailyValueUsd(ready, 0.0, 0.0)),
                "Which is more than selling it, or nobody would ever run a cluster on their own "
                + "generation.");
        }

        // ---- the save ---------------------------------------------------------------------------

        [Test]
        public void TheStationsSurviveASave()
        {
            var simulation = Company();
            simulation.TryBuildPowerPlant(PowerPlantSite.Riverside, out _);
            simulation.TryBuildPowerPlant(PowerPlantSite.Coastal, out _);

            var restored = SaveStore.Restore(SaveStore.Parse(
                JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.That(restored.Power.Count, Is.EqualTo(2));

            foreach (var plant in PowerPlantCatalog.All)
            {
                Assert.That(restored.Power.ReadyDate(plant.Site)?.DayIndex,
                    Is.EqualTo(simulation.State.Power.ReadyDate(plant.Site)?.DayIndex),
                    $"{plant.Key}: the ready date is the whole record. Losing it hands the player "
                    + "a station that is either free or never finishes.");
            }
        }

        [Test]
        public void AFileFromBeforeStationsExistedOwnsNone()
        {
            var legacy = SaveStore.Capture(Company().State);
            legacy.version = 54;
            legacy.powerPlantSites = null;
            legacy.powerPlantReadyDays = null;

            var upgraded = SaveStore.Parse(JsonUtility.ToJson(legacy));

            Assert.That(upgraded, Is.Not.Null);
            Assert.That(upgraded.version, Is.EqualTo(SaveData.CurrentVersion));

            var state = SaveStore.Restore(upgraded);

            Assert.That(state.Power.Count, Is.Zero,
                "A v54 campaign could not commission one, so handing it a station now would be "
                + "inventing a three billion dollar decision nobody made.");
        }

        // ---- the words --------------------------------------------------------------------------

        [Test]
        public void EveryStationHasItsWordsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (Language language in Enum.GetValues(typeof(Language)))
                {
                    Loc.Current = language;

                    foreach (var plant in PowerPlantCatalog.All)
                    {
                        Assert.That(plant.DisplayName, Is.Not.EqualTo(plant.Key),
                            $"{language}: {plant.Key}");

                        Assert.That(plant.Description, Is.Not.EqualTo(plant.Key + ".note"),
                            $"{language}: {plant.Key}.note");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }

        private static int MonthKey(GameDate date) => date.Year * 12 + date.Month - 1;
    }
}
