using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The room around the cabinets: its heat budget, the two square cooler, overclocking, and the
    /// number of people the silicon keeps served.
    ///
    /// **Asked for after a playtest** in which ten cabinets in Emil's basement "did next to nothing"
    /// and the screen gave no way to tell why. Every test here is one of the questions the author
    /// asked of the room: is it too hot, what fixes it, what does it buy, how many people is that.
    /// </summary>
    public sealed class RoomClimateTests
    {
        private static readonly HardwareGeneration H100 =
            HardwareCatalog.Get(HardwareGenerationId.AcceleratorH100);

        private static readonly HardwareGeneration B200 =
            HardwareCatalog.Get(HardwareGenerationId.AcceleratorB200);

        private static ServerHall Stuffed(ServerRack rack, int cabinets)
        {
            var hall = new ServerHall();
            var placed = 0;

            for (var row = 0; row < hall.Rows && placed < cabinets; row++)
            {
                for (var column = 0; column < hall.Columns && placed < cabinets; column++)
                {
                    Assert.IsTrue(hall.TryPlace(column, row, rack, out var why), why);
                    placed++;
                }
            }

            hall.Fill(hall.TotalSlots);
            return hall;
        }

        // ---- the heat budget ------------------------------------------------------------------

        [Test]
        public void TheReadingHasFourStatesInOrder()
        {
            Assert.That(ServerRackCatalog.ClimateOf(0.3), Is.EqualTo(ServerRackCatalog.RoomClimateState.Cool));
            Assert.That(ServerRackCatalog.ClimateOf(0.7), Is.EqualTo(ServerRackCatalog.RoomClimateState.Comfortable));
            Assert.That(ServerRackCatalog.ClimateOf(0.95), Is.EqualTo(ServerRackCatalog.RoomClimateState.Warm));
            Assert.That(ServerRackCatalog.ClimateOf(1.2), Is.EqualTo(ServerRackCatalog.RoomClimateState.Overheating));

            Assert.That(ServerRackCatalog.RoomCoolingFactor(0.99), Is.EqualTo(1.0),
                "A room inside its budget must not cost a cabinet anything.");
            Assert.That(ServerRackCatalog.RoomCoolingFactor(50.0), Is.EqualTo(ServerRackCatalog.RoomFactorFloor),
                "However hot, the cabinets keep some air, or the room becomes a switch.");
        }

        [Test]
        public void EmilsGiftFitsTheBasementWithoutACooler()
        {
            // Four enclosed cabinets of the part a 2023 company would own: the start has to work.
            var hall = Stuffed(ServerRack.Enclosed, CompanySimulation.BasementRacks);
            var climate = hall.Climate(H100.PowerKilowatts);

            Assert.That(climate.State, Is.Not.EqualTo(ServerRackCatalog.RoomClimateState.Overheating),
                $"The gift overheats on its own ({climate.HeatKilowatts:0.0} of {climate.CoolingKilowatts:0.0} kW), "
                + "so the first room a player sees is already broken.");
        }

        [Test]
        public void AFullCellarOverheatsAndACoolerBringsTheWorkBack()
        {
            var hall = Stuffed(ServerRack.HighDensity, 10);

            var hot = hall.Climate(B200.PowerKilowatts);
            Assert.That(hot.State, Is.EqualTo(ServerRackCatalog.RoomClimateState.Overheating),
                "Ten dense cabinets of hot silicon in a basement with no air handling must overheat. "
                + "That is the limit the author asked for in place of the power cap.");

            var before = hall.Output(B200.PetaflopsPerUnit, B200.PowerKilowatts);

            var placed = 0;
            for (var column = 0; column + 1 < hall.Columns; column += 2)
            {
                if (hall.TryPlaceCooler(column, hall.Rows - 1, out _))
                {
                    placed++;
                }
            }

            Assert.That(placed, Is.GreaterThan(0), "The fixture left no floor for a cooler.");

            var after = hall.Output(B200.PetaflopsPerUnit, B200.PowerKilowatts);

            Assert.That(after.Climate.Ratio, Is.LessThan(before.Climate.Ratio));
            Assert.That(after.Petaflops, Is.GreaterThan(before.Petaflops * 1.05),
                "Coolers went in and the cabinets did no more work, so the room's heat is decoration.");
            Assert.That(after.DrawKilowatts, Is.GreaterThan(before.DrawKilowatts),
                "A cooler is paid for in power as well as floor.");
        }

        // ---- the cooler -------------------------------------------------------------------------

        [Test]
        public void ACoolerStandsOnTwoSquares()
        {
            var hall = new ServerHall();
            var free = hall.FreeSquares;

            Assert.IsTrue(hall.TryPlaceCooler(2, 0, out var why), why);

            Assert.That(hall.FreeSquares, Is.EqualTo(free - 2));
            Assert.IsTrue(hall.IsCooler(2, 0));
            Assert.IsTrue(hall.IsCooler(3, 0));
            Assert.IsFalse(hall.IsCooler(4, 0));

            Assert.IsFalse(hall.TryPlace(3, 0, ServerRack.Enclosed, out _),
                "A cabinet was stood on the cooler's second square.");
            Assert.IsFalse(hall.TryPlaceCooler(hall.Columns - 1, 1, out _),
                "A cooler hung off the edge of the room.");

            Assert.IsTrue(hall.TryMoveCooler(3, 0, 3, 0, out why), why);
            Assert.IsTrue(hall.IsCooler(4, 0), "Moving a cooler one square to the right failed.");

            Assert.IsTrue(hall.TryRemoveCooler(4, 0, out why), why);
            Assert.That(hall.CoolerCount, Is.Zero);
        }

        [Test]
        public void BuyingAndSellingACoolerGoesThroughTheBooks()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 1234));
            simulation.State.CashUsd = 1_000_000;
            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);

            var cash = simulation.State.CashUsd;
            Assert.IsTrue(simulation.TryBuildCooler(0, 3, out why), why);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(cash - ServerRackCatalog.RoomCoolerPriceUsd));

            Assert.IsTrue(simulation.TrySellCooler(1, 3, out why), why);
            Assert.That(simulation.State.CashUsd, Is.GreaterThan(cash - ServerRackCatalog.RoomCoolerPriceUsd));
            Assert.That(simulation.State.Hall.CoolerCount, Is.Zero);
        }

        // ---- overclock --------------------------------------------------------------------------

        [Test]
        public void OverclockBuysWorkInACoolRoomAndIsRefusedInAWarmOne()
        {
            var hall = Stuffed(ServerRack.Enclosed, 1);
            var stock = hall.Output(H100.PetaflopsPerUnit, H100.PowerKilowatts);

            Assert.IsTrue(hall.TrySetOverclock(0, 0, 2, H100.PowerKilowatts, null, out var why), why);
            var tuned = hall.Output(H100.PetaflopsPerUnit, H100.PowerKilowatts);

            Assert.That(tuned.Petaflops, Is.GreaterThan(stock.Petaflops * 1.15));
            Assert.That(tuned.Climate.HeatKilowatts, Is.GreaterThan(stock.Climate.HeatKilowatts));

            var warm = Stuffed(ServerRack.HighDensity, 10);
            Assert.IsFalse(warm.TrySetOverclock(0, 0, 1, B200.PowerKilowatts, null, out _),
                "An overheating room let a cabinet be pushed harder.");

            // Backing off is never refused.
            Assert.IsTrue(hall.TrySetOverclock(0, 0, 0, H100.PowerKilowatts, null, out why), why);
        }

        [Test]
        public void AnOverheatingRoomSwitchesOverclocksOff()
        {
            var hall = Stuffed(ServerRack.Enclosed, 1);
            Assert.IsTrue(hall.TrySetOverclock(0, 0, 2, B200.PowerKilowatts, null, out var why), why);

            // Then the room fills around it.
            for (var column = 1; column < hall.Columns; column++)
            {
                for (var row = 0; row < 2; row++)
                {
                    hall.TryPlace(column, row, ServerRack.HighDensity, out _);
                }
            }

            hall.Fill(hall.TotalSlots);
            var climate = hall.Climate(B200.PowerKilowatts);

            Assert.That(climate.State, Is.EqualTo(ServerRackCatalog.RoomClimateState.Overheating));
            Assert.IsFalse(climate.OverclocksRunning,
                "Overclocks kept running in an overheating room, which makes the heat worse by rule.");
            Assert.That(hall.OverclockAt(0, 0), Is.EqualTo(2),
                "The setting was forgotten; it should wait for the room to cool, not reset.");
        }

        // ---- power and people -------------------------------------------------------------------

        [Test]
        public void TheBiggerTheBuildingTheCheaperThePower()
        {
            var colocated = ComputeTierCatalog.Get(ComputeTier.ColocatedServers).PowerCostPerKilowattHourUsd;
            var owned = ComputeTierCatalog.Get(ComputeTier.OwnDatacenter).PowerCostPerKilowattHourUsd;

            Assert.That(ComputePoolTariff.DomesticUsd, Is.GreaterThan(colocated),
                "The basement pays a house's rate, above any contract.");
            Assert.That(colocated, Is.GreaterThan(owned),
                "An own datacenter must buy power cheaper than a colocation cage.");
        }

        [Test]
        public void TheRoomSaysHowManyPeopleItKeepsServed()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 1234));

            var perPetaflop = simulation.UsersPerPetaflop();
            Assert.That(perPetaflop, Is.GreaterThan(0.0));

            // Emil's four cabinets full of H100s, in people. The number the author asked to see.
            var hall = Stuffed(ServerRack.Enclosed, CompanySimulation.BasementRacks);
            var output = hall.Output(H100.PetaflopsPerUnit, H100.PowerKilowatts);
            var people = output.Petaflops * perPetaflop;

            TestContext.WriteLine($"Emil's basement, 32 x H100: {output.Petaflops:0.0} PF, about {people:N0} people.");
            Assert.That(people, Is.GreaterThan(1_000),
                "A full basement keeps fewer than a thousand people served, which makes it a toy.");
        }

        // ---- the file ---------------------------------------------------------------------------

        [Test]
        public void CoolersAndOverclocksSurviveASave()
        {
            var state = new CompanyState("Prometheus AI", 1234);
            state.HasServerRoom = true;
            Assert.IsTrue(state.Hall.TryPlace(0, 0, ServerRack.Enclosed, out var why), why);
            Assert.IsTrue(state.Hall.TryPlaceCooler(2, 3, out why), why);
            Assert.IsTrue(state.Hall.TrySetOverclock(0, 0, 1, H100.PowerKilowatts, null, out why), why);

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state))));

            Assert.IsTrue(back.Hall.IsCooler(3, 3));
            Assert.That(back.Hall.CoolerCount, Is.EqualTo(1));
            Assert.That(back.Hall.OverclockAt(0, 0), Is.EqualTo(1));
        }

        [Test]
        public void AnOlderRoomComesInWithNoCoolersAndSaysSo()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 60;
            data.hallCoolers = null;
            data.hallOverclock = null;

            var upgraded = SaveMigration.UpgradeV60ToV61(data);

            Assert.That(upgraded.version, Is.EqualTo(61));
            Assert.That(upgraded.hallCoolers, Is.Empty);
            Assert.That(upgraded.hallOverclock, Is.Empty);
            StringAssert.Contains("v60 to v61", SaveMigration.LastMigrationNotes);
        }
    }
}
