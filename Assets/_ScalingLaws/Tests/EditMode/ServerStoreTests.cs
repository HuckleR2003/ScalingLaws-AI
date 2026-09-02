using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Buying a cabinet, carrying it, standing it up, moving it and putting it back.
    ///
    /// **The claim under all of it: rearranging the room is free and losing something is not
    /// possible.** A build mode is a place to change your mind, and every one of these tests is a
    /// way the old single-call `TryPlaceRack` could have taken money or hardware off a player who
    /// did.
    ///
    /// Written before the basement scene exists, same as `ServerHallTests` was. The floor is data;
    /// the scene will be a view of it.
    /// </summary>
    public sealed class ServerStoreTests
    {
        /// <summary>A company that already has the basement and the four cabinets it comes with.</summary>
        private static CompanySimulation Started(long cash = 5_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 1234));
            simulation.State.CashUsd = cash;

            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);

            return simulation;
        }

        // ---- the floor's geometry ---------------------------------------------------------------
        //
        // The editor builder writes a marker per square, the runtime stage stands a cabinet on
        // each, and the interface projects the same points to find the square under the cursor.
        // None of the three can see the others, so the arithmetic they share is checked here.

        [Test]
        public void EverySquareTheFloorDefinesIsInsideTheRoomAndClearOfTheWalls()
        {
            for (var row = 0; row < BasementFloor.Rows; row++)
            {
                for (var column = 0; column < BasementFloor.Columns; column++)
                {
                    var x = BasementFloor.CentreX(column);
                    var z = BasementFloor.CentreZ(row);
                    var half = BasementFloor.SquareSize / 2f;

                    Assert.Greater(x - half, 0f, $"square {column},{row} runs through the back wall");
                    Assert.Greater(z - half, 0f, $"square {column},{row} runs through the side wall");

                    Assert.Less(x + half, BasementFloor.RoomWidth,
                        $"square {column},{row} runs off the floor");

                    Assert.Less(z + half, BasementFloor.RoomDepth,
                        $"square {column},{row} runs off the floor");
                }
            }
        }

        [Test]
        public void NoTwoSquaresShareACentreOrAMarkerName()
        {
            var centres = new HashSet<(float, float)>();
            var names = new HashSet<string>();

            for (var row = 0; row < BasementFloor.Rows; row++)
            {
                for (var column = 0; column < BasementFloor.Columns; column++)
                {
                    Assert.IsTrue(
                        centres.Add((BasementFloor.CentreX(column), BasementFloor.CentreZ(row))),
                        $"square {column},{row} sits on top of another one");

                    Assert.IsTrue(names.Add(BasementFloor.MarkerName(column, row)),
                        $"square {column},{row} shares a marker name with another square");
                }
            }

            Assert.AreEqual(BasementFloor.SquareCount, centres.Count);
        }

        [Test]
        public void TheFloorAndTheHallAgreeOnHowManySquaresThereAre()
        {
            var simulation = Started();

            Assert.AreEqual(BasementFloor.Columns, simulation.State.Hall.Columns);
            Assert.AreEqual(BasementFloor.Rows, simulation.State.Hall.Rows);

            Assert.AreEqual(BasementFloor.SquareCount, simulation.State.Hall.SquareCount,
                "a floor with more squares than the hall is squares nothing can ever stand on");
        }

        [Test]
        public void TheCameraLooksAtTheMiddleOfTheGridRatherThanTheMiddleOfTheRoom()
        {
            // The grid is centred in the room today, so these coincide. The test exists because
            // they will not if a stairwell or a plant room ever takes a bite out of one end, and
            // at that point the camera has to follow the squares rather than the walls.
            var first = BasementFloor.CentreX(0);
            var last = BasementFloor.CentreX(BasementFloor.Columns - 1);

            Assert.AreEqual((first + last) / 2f, BasementFloor.FocusX, 0.001f);
            Assert.Greater(BasementFloor.FocusX, 0f);
            Assert.Less(BasementFloor.FocusX, BasementFloor.RoomWidth);
        }

        // ---- the store room on its own ------------------------------------------------------

        [Test]
        public void TakingFromAnEmptyStoreFailsRatherThanGoingNegative()
        {
            var store = new ServerStock();

            Assert.IsFalse(store.TryTake(ServerRack.Enclosed));
            Assert.IsFalse(store.TryTakeFan());
            Assert.AreEqual(0, store.CountOf(ServerRack.Enclosed));
            Assert.AreEqual(0, store.Fans);
            Assert.IsTrue(store.IsEmpty);
        }

        [Test]
        public void TheStoreRoomSurvivesBeingWrittenOutAndReadBack()
        {
            var store = new ServerStock();
            store.Add(ServerRack.Immersion, 2);
            store.Add(ServerRack.OpenFrame);
            store.AddFans(5);

            var kinds = new List<int>();
            var counts = new List<int>();
            store.Capture(kinds, counts, out var fans);

            var read = new ServerStock();
            read.Restore(kinds, counts, fans);

            Assert.AreEqual(2, read.CountOf(ServerRack.Immersion));
            Assert.AreEqual(1, read.CountOf(ServerRack.OpenFrame));
            Assert.AreEqual(0, read.CountOf(ServerRack.HighDensity));
            Assert.AreEqual(5, read.Fans);
            Assert.AreEqual(store.ValueUsd, read.ValueUsd);
        }

        [Test]
        public void ARackKindThatIsNotInTheCatalogIsDroppedOnLoadRatherThanStored()
        {
            var read = new ServerStock();

            read.Restore(new List<int> { 99, (int)ServerRack.Enclosed },
                new List<int> { 4, 1 }, savedFans: 0);

            Assert.AreEqual(1, read.RackCount, "an edited save must not invent a fifth kind of rack");
            Assert.AreEqual(1, read.CountOf(ServerRack.Enclosed));
        }

        // ---- buying is one decision and placing is another -----------------------------------

        [Test]
        public void ABoughtCabinetWaitsInTheStoreRoomUntilItIsStoodUp()
        {
            var simulation = Started();
            var before = simulation.State.Hall.RackCount;

            Assert.IsTrue(simulation.TryBuyRack(ServerRack.HighDensity, out var why), why);

            Assert.AreEqual(1, simulation.State.Warehouse.CountOf(ServerRack.HighDensity));
            Assert.AreEqual(before, simulation.State.Hall.RackCount,
                "buying must not put anything on the floor by itself");

            Assert.IsTrue(simulation.TryStandRack(3, 3, ServerRack.HighDensity, out why), why);

            Assert.AreEqual(0, simulation.State.Warehouse.CountOf(ServerRack.HighDensity));
            Assert.AreEqual(before + 1, simulation.State.Hall.RackCount);
            Assert.AreEqual(ServerRack.HighDensity, simulation.State.Hall.At(3, 3).Rack);
        }

        [Test]
        public void StandingSomethingTheCompanyDoesNotOwnIsRefused()
        {
            var simulation = Started();

            Assert.IsFalse(simulation.TryStandRack(3, 3, ServerRack.Immersion, out var why));
            Assert.IsNotEmpty(why);
            Assert.IsTrue(simulation.State.Hall.IsEmpty(3, 3));
        }

        [Test]
        public void ARefusedPlacementPutsTheCabinetBackRatherThanLosingIt()
        {
            var simulation = Started();
            Assert.IsTrue(simulation.TryBuyRack(ServerRack.OpenFrame, out var why), why);

            // Square 0,0 is one of the four the basement came with, so this placement is refused.
            Assert.IsFalse(simulation.TryStandRack(0, 0, ServerRack.OpenFrame, out _));

            Assert.AreEqual(1, simulation.State.Warehouse.CountOf(ServerRack.OpenFrame),
                "a rack the floor refused is still a rack the company paid for");
        }

        [Test]
        public void BuyingIsTheOnlyThingThatCostsMoney()
        {
            var simulation = Started();
            var afterOpening = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryBuyRack(ServerRack.Enclosed, out var why), why);
            var afterBuying = simulation.State.CashUsd;

            Assert.AreEqual(ServerRackCatalog.Get(ServerRack.Enclosed).PriceUsd,
                afterOpening - afterBuying);

            Assert.IsTrue(simulation.TryStandRack(3, 3, ServerRack.Enclosed, out why), why);
            Assert.IsTrue(simulation.TryMoveRack(3, 3, 3, 2, out why), why);
            Assert.IsTrue(simulation.TryStoreRack(3, 2, out why), why);
            Assert.IsTrue(simulation.TryStandRack(2, 2, ServerRack.Enclosed, out why), why);

            Assert.AreEqual(afterBuying, simulation.State.CashUsd,
                "standing, moving and storing a cabinet must all be free");
        }

        // ---- moving and storing ----------------------------------------------------------------

        [Test]
        public void MovingACabinetCarriesItsFansWithIt()
        {
            var simulation = Started();

            Assert.IsTrue(simulation.TryFitFan(0, 0, out var why), why);
            Assert.IsTrue(simulation.TryFitFan(0, 0, out why), why);
            Assert.AreEqual(2, simulation.State.Hall.At(0, 0).Fans);

            Assert.IsTrue(simulation.TryMoveRack(0, 0, 3, 3, out why), why);

            Assert.AreEqual(2, simulation.State.Hall.At(3, 3).Fans,
                "a fan is bought for a cabinet, so it travels with the cabinet");

            Assert.AreEqual(0, simulation.State.Hall.At(0, 0).Fans);
            Assert.IsTrue(simulation.State.Hall.IsEmpty(0, 0));
        }

        [Test]
        public void MovingOntoAnOccupiedSquareLeavesBothCabinetsWhereTheyWere()
        {
            var simulation = Started();

            Assert.IsFalse(simulation.TryMoveRack(0, 0, 1, 0, out var why));
            Assert.IsNotEmpty(why);

            Assert.IsFalse(simulation.State.Hall.IsEmpty(0, 0));
            Assert.IsFalse(simulation.State.Hall.IsEmpty(1, 0));
        }

        [Test]
        public void StoringACabinetReturnsItAndItsFansToTheStoreRoom()
        {
            var simulation = Started();

            Assert.IsTrue(simulation.TryFitFan(0, 0, out var why), why);
            Assert.IsTrue(simulation.TryStoreRack(0, 0, out why), why);

            Assert.AreEqual(1, simulation.State.Warehouse.CountOf(ServerRack.Enclosed));
            Assert.AreEqual(1, simulation.State.Warehouse.Fans,
                "the fan was paid for and must not be destroyed by picking the cabinet up");

            Assert.IsTrue(simulation.State.Hall.IsEmpty(0, 0));
        }

        [Test]
        public void RefittingAStoredFanCostsNothingBecauseItWasAlreadyBought()
        {
            var simulation = Started();

            Assert.IsTrue(simulation.TryFitFan(0, 0, out var why), why);
            var afterBuyingTheFan = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryStoreRack(0, 0, out why), why);
            Assert.IsTrue(simulation.TryStandRack(0, 0, ServerRack.Enclosed, out why), why);
            Assert.IsTrue(simulation.TryFitFan(0, 0, out why), why);

            Assert.AreEqual(afterBuyingTheFan, simulation.State.CashUsd,
                "rearranging the room must not charge for cooling the company already owns");

            Assert.AreEqual(0, simulation.State.Warehouse.Fans);
            Assert.AreEqual(1, simulation.State.Hall.At(0, 0).Fans);
        }

        [Test]
        public void PullingAFanPutsItInTheStoreRoomRatherThanThrowingItAway()
        {
            var simulation = Started();

            Assert.IsTrue(simulation.TryFitFan(0, 0, out var why), why);
            Assert.IsTrue(simulation.TryStoreFan(0, 0));

            Assert.AreEqual(1, simulation.State.Warehouse.Fans);
            Assert.AreEqual(0, simulation.State.Hall.At(0, 0).Fans);
        }

        [Test]
        public void StoringFromAnEmptySquareFailsAndChangesNothing()
        {
            var simulation = Started();
            var held = simulation.State.Warehouse.RackCount;

            Assert.IsFalse(simulation.TryStoreRack(3, 3, out var why));
            Assert.IsNotEmpty(why);
            Assert.AreEqual(held, simulation.State.Warehouse.RackCount);
        }

        // ---- selling it back -------------------------------------------------------------------

        [Test]
        public void SellingACabinetPaysMoneyInRatherThanTakingItOut()
        {
            var simulation = Started();
            Assert.IsTrue(simulation.TryBuyRack(ServerRack.Enclosed, out var why), why);

            var beforeSelling = simulation.State.CashUsd;
            Assert.IsTrue(simulation.TrySellRack(ServerRack.Enclosed, out why), why);

            Assert.Greater(simulation.State.CashUsd, beforeSelling,
                "PostCash reads the direction off the ledger line, so a sale posted against an "
                + "expense line spends the proceeds instead of banking them");

            Assert.AreEqual(CompanySimulation.RackResaleUsd(ServerRack.Enclosed),
                simulation.State.CashUsd - beforeSelling);

            Assert.AreEqual(0, simulation.State.Warehouse.CountOf(ServerRack.Enclosed));
        }

        [Test]
        public void BuyingAndSellingTheSameCabinetLosesMoney()
        {
            var simulation = Started();
            var opening = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryBuyRack(ServerRack.Immersion, out var why), why);
            Assert.IsTrue(simulation.TrySellRack(ServerRack.Immersion, out why), why);

            var lost = opening - simulation.State.CashUsd;
            var price = ServerRackCatalog.Get(ServerRack.Immersion).PriceUsd;

            Assert.Greater(lost, 0,
                "a store room that empties at list price is a place to park capital, and buying "
                + "the wrong cabinet stops being a mistake");

            Assert.Less(lost, price,
                "and it must not be so punishing that nobody ever corrects a mistake");

            // The author's call: worth under sixty per cent of new.
            Assert.Less(CompanySimulation.RackResaleFraction, 0.60);
        }

        [Test]
        public void ACabinetOnTheFloorCannotBeSoldWithoutPickingItUpFirst()
        {
            var simulation = Started();
            var opening = simulation.State.CashUsd;

            // Four cabinets are standing and the store room is empty, so there is nothing to sell.
            Assert.IsFalse(simulation.TrySellRack(ServerRack.Enclosed, out var why));
            Assert.IsNotEmpty(why);

            Assert.AreEqual(opening, simulation.State.CashUsd);
            Assert.AreEqual(4, simulation.State.Hall.RackCount,
                "selling what the player is looking at is one misclick from deleting a working rack");
        }

        [Test]
        public void AFanTakenOutOfACabinetCanBeSoldOnTheSameTerms()
        {
            var simulation = Started();

            Assert.IsTrue(simulation.TryFitFan(0, 0, out var why), why);
            Assert.IsTrue(simulation.TryStoreFan(0, 0));

            var beforeSelling = simulation.State.CashUsd;
            Assert.IsTrue(simulation.TrySellFan());

            Assert.Greater(simulation.State.CashUsd, beforeSelling);
            Assert.AreEqual(0, simulation.State.Warehouse.Fans);
            Assert.IsFalse(simulation.TrySellFan(), "there was only one");
        }

        // ---- the old one-call path still works -------------------------------------------------

        [Test]
        public void BuyingAndPlacingInOneCallStillLeavesNothingInTheStoreRoom()
        {
            var simulation = Started();

            Assert.IsTrue(simulation.TryBuyRack(ServerRack.OpenFrame, out var why), why);
            Assert.IsTrue(simulation.TryStandRack(3, 3, ServerRack.OpenFrame, out why), why);

            Assert.AreEqual(ServerRack.OpenFrame, simulation.State.Hall.At(3, 3).Rack);
            Assert.IsTrue(simulation.State.Warehouse.IsEmpty,
                "the one-call path is a buy followed by a stand, so nothing may be left behind");
        }
    }
}
