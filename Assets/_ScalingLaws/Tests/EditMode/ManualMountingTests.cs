using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Cards go into cabinets by hand, stay where they are put, and come out when clicked.
    ///
    /// **Reported three times** in one form or another: the parts mount themselves, the cabinets
    /// fill with whatever, nothing can be taken out. All three were one pass in `ServerHall.Stock`
    /// that spread every loose card across the free slots on every tick, so a card pulled out was
    /// back a fraction of a second later. These tests are the ratchet on that.
    /// </summary>
    public sealed class ManualMountingTests
    {
        private static CompanySimulation RoomWith(params (HardwareGenerationId Id, int Units)[] cards)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 1234));
            simulation.State.CashUsd = 50_000_000;
            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);

            foreach (var (id, units) in cards)
            {
                simulation.State.Pool.AddAsset(new HardwareAsset(
                    id, ComputeTier.ColocatedServers, units, simulation.State.Date, 10_000, 0));
            }

            return simulation;
        }

        [Test]
        public void NothingMountsItself()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorH100, 12));
            simulation.Advance(5);

            Assert.That(simulation.State.Hall.HousedAccelerators, Is.Zero,
                "Owned cards went into the cabinets without anybody putting them there.");
            Assert.That(simulation.InStoreOf(HardwareGenerationId.AcceleratorH100), Is.EqualTo(12));
        }

        [Test]
        public void AFittedCardIsTheCardThatWasChosenAndItStays()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorH100, 4),
                (HardwareGenerationId.AcceleratorA100, 4));

            Assert.IsTrue(simulation.TryFitCard(0, 0, HardwareGenerationId.AcceleratorA100, out var why), why);
            simulation.Advance(3);

            var cards = simulation.State.Hall.CardsIn(0, 0, out var unknown);

            Assert.That(unknown, Is.Zero);
            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(cards[0].Generation, Is.EqualTo(HardwareGenerationId.AcceleratorA100),
                "The player picked an A100 and the cabinet holds something else.");
            Assert.That(simulation.InStoreOf(HardwareGenerationId.AcceleratorA100), Is.EqualTo(3));
            Assert.That(simulation.InStoreOf(HardwareGenerationId.AcceleratorH100), Is.EqualTo(4));
        }

        [Test]
        public void APulledCardStaysOut()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorH100, 8));
            Assert.That(RoomHands.FitEverything(simulation), Is.EqualTo(8));

            var hall = simulation.State.Hall;
            var (column, row) = FirstCabinetWithCards(hall);

            Assert.IsTrue(simulation.TryPullCard(column, row, HardwareGenerationId.AcceleratorH100,
                out var why), why);
            simulation.Advance(10);

            Assert.That(hall.HousedAccelerators, Is.EqualTo(7),
                "A card taken out went back in by itself, which is the bug this fixture exists for.");
            Assert.That(simulation.InStoreOf(HardwareGenerationId.AcceleratorH100), Is.EqualTo(1));
        }

        [Test]
        public void FittingWithAnEmptyStoreIsRefusedAndMovesNothing()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorH100, 2));
            var hall = simulation.State.Hall;

            Assert.IsTrue(simulation.TryFitCard(0, 0, out var why), why);
            Assert.IsTrue(simulation.TryFitCard(0, 0, out why), why);

            Assert.IsFalse(simulation.TryFitCard(1, 0, out _),
                "A card was fitted with nothing in the store.");
            Assert.That(hall.At(0, 0).Accelerators, Is.EqualTo(2),
                "Fitting into one cabinet took a card out of another behind the player's back.");
        }

        [Test]
        public void SellingAGenerationTakesItsCardsOffTheFloor()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorA100, 6));
            RoomHands.FitEverything(simulation);

            var index = simulation.State.Pool.Assets.Count - 1;
            Assert.IsTrue(simulation.TrySellHardware(index, 6, out _, out var why), why);
            simulation.Advance(1);

            Assert.That(simulation.State.Hall.HousedAccelerators, Is.Zero,
                "Sold cards are still standing in the cabinets.");
        }

        [Test]
        public void SellingSomeCardsSpansBatchesAndLeavesTheCabinetsAlone()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorA100, 3),
                (HardwareGenerationId.AcceleratorA100, 4));

            Assert.IsTrue(simulation.TryFitCard(0, 0, HardwareGenerationId.AcceleratorA100, out var why), why);
            Assert.IsTrue(simulation.TryFitCard(0, 0, HardwareGenerationId.AcceleratorA100, out why), why);

            var quoted = simulation.SaleValueOfCards(HardwareGenerationId.AcceleratorA100, 5);
            var cash = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TrySellCards(HardwareGenerationId.AcceleratorA100, 5,
                out var proceeds, out why), why);

            Assert.That(proceeds, Is.EqualTo(quoted), "The dialog quoted one figure and paid another.");
            Assert.That(simulation.State.CashUsd, Is.EqualTo(cash + proceeds),
                "The sale did not reach the books.");

            simulation.Advance(1);
            Assert.That(simulation.OnlineUnitsOf(HardwareGenerationId.AcceleratorA100), Is.EqualTo(2),
                "Five of seven were sold across two batches.");
            Assert.That(simulation.State.Hall.At(0, 0).Accelerators, Is.EqualTo(2),
                "Selling what was in the store took cards out of a cabinet.");
        }

        [Test]
        public void EachCabinetRunsOnTheCardsInItNotOnTheFleetAverage()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorA100, 8),
                (HardwareGenerationId.AcceleratorB200, 8));

            for (var index = 0; index < 8; index++)
            {
                Assert.IsTrue(simulation.TryFitCard(0, 0, HardwareGenerationId.AcceleratorA100, out var why), why);
                Assert.IsTrue(simulation.TryFitCard(1, 0, HardwareGenerationId.AcceleratorB200, out why), why);
            }

            var hall = simulation.State.Hall;
            var (averagePf, averageKw) = simulation.HallPerAccelerator();

            var old = hall.CabinetPetaflops(0, 0, averagePf, averageKw, simulation.Room);
            var fresh = hall.CabinetPetaflops(1, 0, averagePf, averageKw, simulation.Room);

            Assert.That(fresh, Is.GreaterThan(old * 3.0),
                "A cabinet of B200s and a cabinet of A100s delivered alike, so the room is pricing "
                + "the fleet's average rather than the cards the player put in each.");
            Assert.That(hall.HeatRatio(1, 0, averageKw, simulation.Room),
                Is.GreaterThan(hall.HeatRatio(0, 0, averageKw, simulation.Room)));
        }

        [Test]
        public void MovingACabinetTakesItsCardsWithIt()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorH100, 3));
            RoomHands.FitEverything(simulation);

            var hall = simulation.State.Hall;
            var (column, row) = FirstCabinetWithCards(hall);
            var held = hall.At(column, row).Accelerators;

            Assert.IsTrue(simulation.TryMoveRack(column, row, 2, 3, out var why), why);
            simulation.Advance(1);

            Assert.That(hall.At(2, 3).Accelerators, Is.EqualTo(held));
            Assert.That(hall.CardsIn(2, 3, out _)[0].Generation,
                Is.EqualTo(HardwareGenerationId.AcceleratorH100));
        }

        [Test]
        public void UnrecordedCardsAreNamedNewestFirstAndNeverInvented()
        {
            var hall = new ServerHall(4, 4);
            Assert.IsTrue(hall.TryPlace(0, 0, ServerRack.Enclosed, out var why), why);
            hall.Fill(8);

            var owned = new Dictionary<HardwareGenerationId, int>
            {
                [HardwareGenerationId.AcceleratorA100] = 3,
                [HardwareGenerationId.AcceleratorH100] = 2
            };

            Assert.That(hall.Stock(owned), Is.EqualTo(5),
                "More cards stand on the floor than the company owns.");

            Assert.That(hall.HousedOf(HardwareGenerationId.AcceleratorH100), Is.EqualTo(2),
                "The newest cards owned should be the ones named first.");
            Assert.That(hall.HousedOf(HardwareGenerationId.AcceleratorA100), Is.EqualTo(3));
            hall.CardsIn(0, 0, out var unknown);
            Assert.That(unknown, Is.Zero);
        }

        [Test]
        public void WhichCardsStandWhereSurvivesASave()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorA100, 2),
                (HardwareGenerationId.AcceleratorH100, 2));

            Assert.IsTrue(simulation.TryFitCard(1, 0, HardwareGenerationId.AcceleratorA100, out var why), why);
            Assert.IsTrue(simulation.TryFitCard(1, 0, HardwareGenerationId.AcceleratorH100, out why), why);

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            var cards = back.Hall.CardsIn(1, 0, out var unknown);

            Assert.That(unknown, Is.Zero);
            Assert.That(cards, Has.Count.EqualTo(2));
            Assert.That(back.Hall.HousedOf(HardwareGenerationId.AcceleratorA100), Is.EqualTo(1));
            Assert.That(back.Hall.HousedOf(HardwareGenerationId.AcceleratorH100), Is.EqualTo(1));
        }

        [Test]
        public void AnOlderRoomKeepsItsCardsAndSaysHowTheyAreNamed()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 61;
            data.hallCards = null;

            var upgraded = SaveMigration.UpgradeV61ToV62(data);

            Assert.That(upgraded.version, Is.EqualTo(62));
            Assert.That(upgraded.hallCards, Is.Empty);
            StringAssert.Contains("v61 to v62", SaveMigration.LastMigrationNotes);
        }

        private static (int Column, int Row) FirstCabinetWithCards(ServerHall hall)
        {
            foreach (var square in hall.Occupied())
            {
                if (square.Accelerators > 0)
                {
                    return (square.Column, square.Row);
                }
            }

            Assert.Fail("No cabinet holds a card.");
            return (-1, -1);
        }
    }
}
