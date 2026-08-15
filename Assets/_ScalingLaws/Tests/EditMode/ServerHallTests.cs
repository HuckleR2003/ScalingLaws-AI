using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The server hall floor and the four racks that stand on it.
    ///
    /// **Written before the scene exists, on purpose.** A placement system built inside a scene can
    /// only be checked by looking at it, and every layout fault in this project was found by looking
    /// rather than by a test for exactly that reason. The grid is data; the scene will be a view of
    /// it.
    ///
    /// The claim the whole thing rests on: **cooling is what stops four racks being a ladder.**
    /// Without it the answer is always the dearest rack affordable, which is the failure the hosting
    /// packages were designed around and the same one this has to avoid.
    /// </summary>
    public sealed class ServerHallTests
    {
        // ---- the catalog has to stay a set of trades ------------------------------------------

        [Test]
        public void NoRackIsSimplyBetterThanAnother()
        {
            foreach (var mine in ServerRackCatalog.All)
            {
                var dominated = false;

                foreach (var other in ServerRackCatalog.All)
                {
                    if (other.Id == mine.Id)
                    {
                        continue;
                    }

                    // Cheaper per slot, better cooled, and cheaper to keep. Any rack like this makes
                    // the other three decoration.
                    if (other.PricePerSlotUsd <= mine.PricePerSlotUsd
                        && other.CoolingCapacityKilowatts >= mine.CoolingCapacityKilowatts
                        && other.MonthlyUpkeepUsd <= mine.MonthlyUpkeepUsd
                        && other.Slots >= mine.Slots)
                    {
                        dominated = true;
                    }
                }

                Assert.IsFalse(dominated,
                    $"{mine.DisplayName} is beaten on every axis at once, so nobody would ever "
                    + "choose it.");
            }
        }

        [Test]
        public void TheCheapRackIsCheapestPerSlotAndTheDenseOneFitsTheMost()
        {
            var open = ServerRackCatalog.Get(ServerRack.OpenFrame);
            var immersion = ServerRackCatalog.Get(ServerRack.Immersion);

            Assert.Less(open.PricePerSlotUsd, immersion.PricePerSlotUsd,
                "The cheap answer has to actually be the cheap answer.");

            Assert.Greater(immersion.Slots, open.Slots * 4,
                "And the dear one has to buy floor space, which is what a hall runs out of.");

            Assert.Greater(immersion.MonthlyUpkeepUsd, open.MonthlyUpkeepUsd * 10,
                "Immersion needs somebody who knows what they are doing, every month.");
        }

        // ---- heat -------------------------------------------------------------------------------

        [Test]
        public void ARackInsideItsRatingDeliversEverything()
        {
            Assert.AreEqual(1.0, ServerRackCatalog.ThrottleFactor(5.0, 14.0), 1e-9);
            Assert.AreEqual(1.0, ServerRackCatalog.ThrottleFactor(14.0, 14.0), 1e-9,
                "Sitting at the rating is normal engineering, not a fault.");
        }

        [Test]
        public void AnOverfilledRackThrottlesRatherThanFailing()
        {
            var badly = ServerRackCatalog.ThrottleFactor(40.0, 14.0);

            Assert.Less(badly, 1.0, "Too much heat has to cost something.");
            Assert.GreaterOrEqual(badly, ServerRackCatalog.WorstThrottle,
                "And it has to throttle rather than stop. A rack that dies is a bug report; a rack "
                + "at half speed is a decision the player made.");
        }

        [Test]
        public void TheSameAcceleratorsInACheapRackDeliverLess()
        {
            var cheap = new ServerHall(2, 2);
            var proper = new ServerHall(2, 2);

            // Sixteen accelerators. Four open frames hold exactly that and cook; two high density
            // racks hold it comfortably.
            for (var column = 0; column < 2; column++)
            {
                for (var row = 0; row < 2; row++)
                {
                    Assert.IsTrue(cheap.TryPlace(column, row, ServerRack.OpenFrame, out _));
                }
            }

            Assert.IsTrue(proper.TryPlace(0, 0, ServerRack.HighDensity, out _));
            Assert.IsTrue(proper.TryPlace(1, 0, ServerRack.HighDensity, out _));

            cheap.Stock(16);
            proper.Stock(16);

            Assert.AreEqual(16, cheap.HousedAccelerators);
            Assert.AreEqual(16, proper.HousedAccelerators);

            // Two and a half kilowatts each, which is a real accelerator's order of magnitude.
            var cheapOutput = cheap.Output(1.0, 2.5);
            var properOutput = proper.Output(1.0, 2.5);

            Assert.Less(cheapOutput.Petaflops, properOutput.Petaflops,
                "Same silicon, same count, worse cooling, less work done. If these are equal then "
                + "cooling is decoration and the cheapest rack always wins.");

            Assert.Greater(cheapOutput.ThrottledRacks, 0);
            Assert.IsTrue(properOutput.IsHealthy);
        }

        [Test]
        public void ThePowerIsDrawnEvenWhenTheWorkIsNot()
        {
            var hall = new ServerHall(1, 1);
            Assert.IsTrue(hall.TryPlace(0, 0, ServerRack.OpenFrame, out _));
            hall.Stock(4);

            var output = hall.Output(1.0, 4.0);

            Assert.Less(output.Petaflops, 4.0, "It is well past its cooling, so it throttles.");
            Assert.GreaterOrEqual(output.DrawKilowatts, 16.0,
                "And the bill is for the heat, which is the whole cost of getting this wrong.");
        }

        [Test]
        public void AnEmptyRackStillDrawsItsIdle()
        {
            var hall = new ServerHall(1, 1);
            hall.TryPlace(0, 0, ServerRack.Immersion, out _);

            Assert.Greater(hall.Output(1.0, 2.5).DrawKilowatts, 0.0,
                "Fans and pumps do not care whether anything is plugged in.");
        }

        // ---- the floor ---------------------------------------------------------------------------

        [Test]
        public void TheDefaultHallHasTheSquaresTheDesignAskedFor()
        {
            var hall = new ServerHall();

            Assert.GreaterOrEqual(hall.SquareCount, 30);
            Assert.LessOrEqual(hall.SquareCount, 40);
        }

        [Test]
        public void OneRackToASquareAndNothingOutsideTheFloor()
        {
            var hall = new ServerHall(3, 3);

            Assert.IsTrue(hall.TryPlace(1, 1, ServerRack.Enclosed, out _));

            Assert.IsFalse(hall.TryPlace(1, 1, ServerRack.Immersion, out var taken),
                "Placing over something would silently destroy it and whatever was in it.");

            Assert.IsTrue(taken.Contains("already"));

            Assert.IsFalse(hall.TryPlace(9, 9, ServerRack.Enclosed, out var offFloor));
            Assert.IsTrue(offFloor.Contains("not on the floor"));

            Assert.AreEqual(1, hall.RackCount);
            Assert.AreEqual(8, hall.FreeSquares);
        }

        [Test]
        public void SellingARackGivesBackWhatWasInIt()
        {
            var hall = new ServerHall(2, 2);
            hall.TryPlace(0, 0, ServerRack.HighDensity, out _);
            hall.Stock(10);

            Assert.IsTrue(hall.TryRemove(0, 0, out var rack, out var freed, out _));

            Assert.AreEqual(ServerRack.HighDensity, rack);
            Assert.AreEqual(10, freed,
                "The accelerators are the expensive half. Selling a rack is not a decision to scrap "
                + "what it held.");

            Assert.IsTrue(hall.IsEmpty(0, 0));
        }

        [Test]
        public void AFleetLargerThanTheFloorLeavesSomeInTheYard()
        {
            var hall = new ServerHall(2, 1);
            hall.TryPlace(0, 0, ServerRack.OpenFrame, out _);
            hall.TryPlace(1, 0, ServerRack.OpenFrame, out _);

            var housed = hall.Stock(50);

            Assert.AreEqual(8, housed, "Two open frames hold four each and no more.");
            Assert.AreEqual(8, hall.HousedAccelerators);
            Assert.AreEqual(8, hall.TotalSlots);
        }

        [Test]
        public void TheFloorSurvivesARoundTrip()
        {
            var hall = new ServerHall(4, 4);
            hall.TryPlace(0, 0, ServerRack.Immersion, out _);
            hall.TryPlace(3, 2, ServerRack.OpenFrame, out _);
            hall.Stock(20);

            var racks = new System.Collections.Generic.List<int>();
            var counts = new System.Collections.Generic.List<int>();
            hall.Capture(racks, counts);

            var back = new ServerHall(4, 4);
            back.Restore(racks, counts);

            Assert.AreEqual(ServerRack.Immersion, back.At(0, 0).Rack);
            Assert.AreEqual(ServerRack.OpenFrame, back.At(3, 2).Rack);
            Assert.AreEqual(hall.HousedAccelerators, back.HousedAccelerators);
            Assert.AreEqual(hall.MonthlyUpkeepUsd, back.MonthlyUpkeepUsd);
        }

        [Test]
        public void ACorruptSaveCannotPutSomethingImpossibleOnTheFloor()
        {
            var hall = new ServerHall(2, 2);

            hall.Restore(
                new System.Collections.Generic.List<int> { 99, -4, 2, 1 },
                new System.Collections.Generic.List<int> { 5, 5, -3, 4 });

            Assert.AreEqual(ServerRack.None, hall.At(0, 0).Rack, "99 is not a rack.");
            Assert.AreEqual(ServerRack.None, hall.At(1, 0).Rack, "Neither is minus four.");
            Assert.AreEqual(ServerRack.Enclosed, hall.At(0, 1).Rack);
            Assert.GreaterOrEqual(hall.At(0, 1).Accelerators, 0, "No negative counts.");
        }
    }
}
