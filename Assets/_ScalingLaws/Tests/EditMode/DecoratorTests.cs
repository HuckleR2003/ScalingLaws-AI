using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
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

        /// <summary>
        /// A stored piece can be put back, and putting it back makes it count again.
        ///
        /// **`TryPlaceFurniture` had no caller anywhere, including tests.** Found by the sweep before
        /// the 0.2.0 build: `TryStoreFurniture` takes a piece off the floor and nothing put one back,
        /// so the pair was half a mechanism. It was dormant rather than broken while
        /// `GameShell.FurnishingShopIsOpen` was false; the build mode is permanent now, so both
        /// halves are things a player does with the mouse.
        ///
        /// **Not deleted, and that is the point.** Removing the placing half would leave a shop that
        /// can store and cannot restore on the day it is switched back on, which is exactly the shape
        /// of `TryRemove` silently destroying fans: a dormant fault that only becomes a fault when a
        /// feature arrives. The rack store room learned this and has `TryStoreRack` beside
        /// `TryStandRack`; this is the same pair, now proven to work.
        /// </summary>
        [Test]
        public void AStoredPieceCanBePutBackOnTheFloor()
        {
            var simulation = Rich();
            simulation.TryBuyFurniture(FurnitureKind.Sofa, Zone);

            var item = simulation.State.Decor.Items.Single();
            var standing = simulation.State.Decor.MoraleBonus;

            Assert.That(simulation.TryStoreFurniture(item), Is.Empty);
            Assert.That(item.IsPlaced, Is.False);

            Assert.That(simulation.TryPlaceFurniture(item, Zone), Is.Empty,
                "a piece that was just taken off this floor cannot be put back on it");

            Assert.That(item.IsPlaced, Is.True);
            Assert.That(simulation.State.Decor.MoraleBonus, Is.EqualTo(standing),
                "the sofa is back on the floor and is worth what it was worth before");
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

        /// <summary>
        /// **And the screen that moves furniture asks that question.**
        ///
        /// The fixture above proves the rule works. It proved that while the build mode moved
        /// pieces through `DecorPlan` directly and never asked: `TryStoreFurniture` was a door
        /// nobody used, so a player could put every desk in the office into storage with six
        /// people sitting at them. The hiring cap is the one number the furniture can break, and
        /// it is the reason the rule exists at all.
        ///
        /// Read from the source because an EditMode test has no panel and a click is never
        /// dispatched, which is the same reason the office buttons were invisible for months
        /// while every test of that page passed.
        /// </summary>
        [Test]
        public void TheBuildModeAsksWhetherAPieceMayLeaveTheFloor()
        {
            var build = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "UI", "GameShell.Build.cs"));

            Assert.That(build, Does.Contain("WhyFurnitureCannotBeStored"),
                "The build mode takes pieces off the floor without asking whether they may go, "
                + "so the desk rule guards a door nobody walks through.");

            var rules = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "Simulation",
                "CompanySimulation.cs"));

            Assert.That(Regex.Matches(rules, @"Staff\.Desks - seats").Count, Is.EqualTo(1),
                "The seat arithmetic is written twice, so the screen and the simulation can "
                + "disagree about whether a desk may be taken away.");
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

        /// <summary>
        /// **This asserted the wrong thing and let a tier ship without a room.**
        ///
        /// It read `RoomCatalog.For(tier)` and checked the camera and the desk count on whatever
        /// came back. `For` falls back to the garage when a tier has no entry, and the garage has a
        /// perfectly good camera and four desks, so the tower was added to the office ladder in
        /// August, had no room of its own, and this passed on the garage's numbers every run.
        ///
        /// The fact worth holding is membership: every tier has an entry of its own. The properties
        /// are worth holding too, but only once it is the tier's own properties being read.
        /// </summary>
        [Test]
        public void EveryTierHasARoomToLookAt()
        {
            var known = new System.Collections.Generic.HashSet<OfficeTier>(RoomCatalog.Tiers);

            foreach (OfficeTier tier in System.Enum.GetValues(typeof(OfficeTier)))
            {
                Assert.That(known.Contains(tier), Is.True,
                    $"{tier} has no room of its own, so the office view falls back to the garage "
                    + "and a player who moved there is looking at somebody else's building.");

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
