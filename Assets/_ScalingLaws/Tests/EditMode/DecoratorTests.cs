using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The furniture shop.
    ///
    /// **The thing being defended here is that buying furniture actually does something.** Six
    /// separate mechanisms in this project have shipped green and delivered nothing to the player,
    /// so every test below walks the whole path: money leaves, a piece stands up in the room, and a
    /// number the campaign reads changes. A test that only asserted the inventory grew would pass on
    /// a shop that is pure decoration in the worst sense.
    /// </summary>
    public sealed class DecoratorTests
    {
        /// <summary>The small hub's open floor, which is where the campaign's first sofa goes.</summary>
        private static readonly DecorZone Zone = new(1.6f, 6.9f, 8.0f, 3.6f);

        /// <summary>A floor with room for far more than anything below buys.</summary>
        private static readonly DecorZone Huge = new(0f, 0f, 40f, 40f);

        private static CompanySimulation Rich(long cash = 5_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        [Test]
        public void BuyingADeskRaisesTheHiringCap()
        {
            var simulation = Rich();
            var before = simulation.State.Staff.Desks;

            var problem = simulation.TryBuyFurniture(FurnitureKind.Desk, Zone);

            Assert.That(problem, Is.Empty, "A company with five million can afford a desk.");
            Assert.That(simulation.State.Staff.Desks, Is.EqualTo(before + 1),
                "A desk that does not seat anybody is a box the player paid for.");
        }

        [Test]
        public void BuyingTakesTheMoney()
        {
            var simulation = Rich();
            var piece = FurnitureCatalog.Get(FurnitureKind.Sofa);
            var before = simulation.State.CashUsd;

            simulation.TryBuyFurniture(FurnitureKind.Sofa, Zone);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - (long)piece.PriceUsd));
        }

        [Test]
        public void SomethingBoughtIsStandingUpStraightAway()
        {
            var simulation = Rich();
            simulation.TryBuyFurniture(FurnitureKind.Plant, Zone);

            var item = simulation.State.Decor.Items.Single();

            Assert.That(item.IsPlaced, Is.True,
                "Buying and then having to find it in a list is two decisions where there was one.");

            Assert.That(item.X, Is.GreaterThanOrEqualTo(Zone.X));
            Assert.That(item.X, Is.LessThanOrEqualTo(Zone.X + Zone.Width));
            Assert.That(item.Z, Is.GreaterThanOrEqualTo(Zone.Z));
            Assert.That(item.Z, Is.LessThanOrEqualTo(Zone.Z + Zone.Depth),
                "Furniture outside the zone stands on somebody's desk.");
        }

        [Test]
        public void NothingIsEverPlacedOnTopOfSomethingElse()
        {
            var simulation = Rich(200_000_000);

            for (var index = 0; index < 12; index++)
            {
                simulation.TryBuyFurniture(FurnitureKind.Plant, Zone);
            }

            var spots = simulation.State.Decor.Placed
                .Select(item => (item.X, item.Z))
                .ToList();

            Assert.That(spots.Distinct().Count(), Is.EqualTo(spots.Count),
                "Two pieces in one slot is one piece the player paid twice for.");
        }

        [Test]
        public void CannotBuyWhatTheCompanyCannotAfford()
        {
            var simulation = Rich(1_000);

            var problem = simulation.TryBuyFurniture(FurnitureKind.SleepPod, Zone);

            Assert.That(problem, Is.Not.Empty);
            Assert.That(simulation.State.Decor.Items, Is.Empty);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(1_000L));
        }

        [Test]
        public void SellingReturnsThirtyPerCent()
        {
            var simulation = Rich();
            var piece = FurnitureCatalog.Get(FurnitureKind.CoffeeBar);

            simulation.TryBuyFurniture(FurnitureKind.CoffeeBar, Zone);
            var afterBuying = simulation.State.CashUsd;

            var got = simulation.SellFurniture(simulation.State.Decor.Items.Single());

            Assert.That(got, Is.EqualTo(piece.PriceUsd * FurnitureCatalog.ResaleFraction).Within(1.0));
            Assert.That(simulation.State.CashUsd, Is.EqualTo(afterBuying + (long)got));
            Assert.That(simulation.State.Decor.Items, Is.Empty);
        }

        [Test]
        public void SellingTheSameThingTwiceMintsNothing()
        {
            var simulation = Rich();
            simulation.TryBuyFurniture(FurnitureKind.Sofa, Zone);

            var item = simulation.State.Decor.Items.Single();
            simulation.SellFurniture(item);
            var after = simulation.State.CashUsd;

            var second = simulation.SellFurniture(item);

            Assert.That(second, Is.EqualTo(0.0));
            Assert.That(simulation.State.CashUsd, Is.EqualTo(after));
        }

        [Test]
        public void StoredThingsDoNothing()
        {
            var simulation = Rich();
            simulation.TryBuyFurniture(FurnitureKind.Sofa, Zone);

            var item = simulation.State.Decor.Items.Single();
            var standing = simulation.State.Decor.MoraleBonus;

            simulation.TryStoreFurniture(item);

            Assert.That(standing, Is.GreaterThan(0.0), "A sofa on the floor has to be worth something.");
            Assert.That(simulation.State.Decor.MoraleBonus, Is.EqualTo(0.0),
                "A sofa in a box raises nobody's morale.");
        }

        [Test]
        public void ADeskSomebodyIsSittingAtCannotBeStored()
        {
            var simulation = Rich();

            // Fill the garage, then buy the seat that lets one more person in.
            var lease = OfficeCatalog.Get(simulation.State.Staff.Office).Desks;
            simulation.TryBuyFurniture(FurnitureKind.Desk, Zone);

            for (var index = 0; index < lease + 1; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 3, GameDate.Start));
            }

            var desk = simulation.State.Decor.Items.Single();
            var problem = simulation.TryStoreFurniture(desk);

            Assert.That(problem, Is.Not.Empty,
                "Storing that desk leaves somebody employed with nowhere to sit.");

            Assert.That(desk.IsPlaced, Is.True);
        }

        [Test]
        public void TheBonusesAreCapped()
        {
            var simulation = Rich(2_000_000_000);

            for (var index = 0; index < 40; index++)
            {
                simulation.TryBuyFurniture(FurnitureKind.Aquarium, Huge);
            }

            Assert.That(simulation.State.Decor.MoraleBonus,
                Is.EqualTo(FurnitureCatalog.MoraleCeiling).Within(1e-9),
                "Without a ceiling a rich player buys their way out of ever losing anybody.");
        }

        [Test]
        public void OwningNothingChangesNothing()
        {
            var plan = new DecorPlan();

            Assert.That(plan.ExtraDesks, Is.Zero);
            Assert.That(plan.MoraleBonus, Is.Zero);
            Assert.That(plan.ResearchBonus, Is.Zero);
            Assert.That(plan.InvestedUsd, Is.Zero);
        }

        [Test]
        public void EveryRoomLeavesSomewhereToPutThings()
        {
            foreach (OfficeTier tier in System.Enum.GetValues(typeof(OfficeTier)))
            {
                var room = RoomCatalog.For(tier);
                var zone = new DecorZone(room.DecorX, room.DecorZ, room.DecorWidth, room.DecorDepth);

                var plan = new DecorPlan();
                var placed = 0;

                for (var index = 0; index < 6; index++)
                {
                    if (plan.Buy(FurnitureKind.Plant, zone).IsPlaced)
                    {
                        placed++;
                    }
                }

                if (!room.AllowsFurniture)
                {
                    Assert.That(placed, Is.Zero,
                        $"{tier} says it has no room but placed something anyway.");
                    continue;
                }

                Assert.That(placed, Is.GreaterThanOrEqualTo(4),
                    $"{tier} opens the shop but has nowhere to stand four plants in.");
            }
        }

        [Test]
        public void EveryTierHasARoomToLookAt()
        {
            foreach (OfficeTier tier in System.Enum.GetValues(typeof(OfficeTier)))
            {
                var room = RoomCatalog.For(tier);

                Assert.That(room.CameraSize, Is.GreaterThan(0f), $"{tier} has no camera framing.");
                Assert.That(room.FixedDesks, Is.GreaterThan(0), $"{tier} seats nobody.");
            }
        }

        [Test]
        public void TheRoomSeatsAsManyPeopleAsTheLeaseSays()
        {
            foreach (OfficeTier tier in System.Enum.GetValues(typeof(OfficeTier)))
            {
                if (!RoomCatalog.For(tier).IsLoaded)
                {
                    continue;
                }

                Assert.That(RoomCatalog.For(tier).FixedDesks,
                    Is.GreaterThanOrEqualTo(OfficeCatalog.Get(tier).Desks == 0
                        ? 0
                        : System.Math.Min(OfficeCatalog.Get(tier).Desks, 20)),
                    $"{tier} promises more desks than the room the player is looking at has.");
            }
        }
    }
}
