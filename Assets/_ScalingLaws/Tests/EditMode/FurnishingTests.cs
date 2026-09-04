using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Placing furniture by hand: the half of the build mode that can be tested without a scene.
    ///
    /// **The clicks cannot be tested and the rules can.** An EditMode test dispatches no pointer
    /// events, so which square a cursor lands on is the author's first click. What is testable is
    /// everything underneath: that a slot the player pointed at is the slot that gets used, that two
    /// things cannot stand in one place, and that a refused placement leaves the floor exactly as it
    /// was rather than losing a sofa.
    ///
    /// That last one is the reason this fixture exists. The basement shipped a lift that really
    /// removed the cabinet, which destroyed the fans bought for it, and the office uses the same
    /// grammar.
    /// </summary>
    public sealed class FurnishingTests
    {
        private static DecorZone Zone => new(0f, 0f, 8f, 6f);

        private static CompanySimulation Company(uint seed = 21)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.State.CashUsd = 5_000_000L;

            return simulation;
        }

        /// <summary>
        /// The grid the player clicks is the grid the plan fills.
        ///
        /// Two walks over one zone, and if they ever disagree the square somebody points at and the
        /// square something lands on are different squares - which nothing would report, because the
        /// plan would be right and the picture would be right and they would be about different
        /// places.
        /// </summary>
        [Test]
        public void EverySlotTheBuildModeOffersIsOneThePlanWillFill()
        {
            var plan = new DecorPlan();
            var slots = plan.AllSlots(Zone).ToList();

            Assert.Greater(slots.Count, 3, "A zone this size has more than three places to stand.");

            // Filling them one at a time uses each exactly once and never runs out early.
            for (var index = 0; index < slots.Count; index++)
            {
                var item = plan.Buy(FurnitureKind.Plant, Zone);

                Assert.IsTrue(item.IsPlaced,
                    $"The floor reported {slots.Count} places and refused number {index + 1}.");
            }

            Assert.AreEqual(slots.Count, plan.Placed.Count());

            // And the next one has nowhere to go rather than standing on somebody.
            var overflow = plan.Buy(FurnitureKind.Plant, Zone);
            Assert.IsFalse(overflow.IsPlaced, "A full floor took one more piece.");
        }

        /// <summary>A piece goes on the square it was pointed at, not on the first free one.</summary>
        [Test]
        public void APieceLandsOnTheSlotItWasPointedAt()
        {
            var plan = new DecorPlan();
            var slots = plan.AllSlots(Zone).ToList();

            var chosen = slots[5];

            var item = plan.Buy(FurnitureKind.Desk, Zone);
            plan.Store(item);

            Assert.IsTrue(plan.PlaceOn(item, chosen.x, chosen.z));

            Assert.AreEqual(chosen.x, item.X, 0.001f);
            Assert.AreEqual(chosen.z, item.Z, 0.001f);

            Assert.AreSame(item, plan.At(chosen.x, chosen.z),
                "The plan cannot find the piece on the square it just put it on.");

            Assert.IsNull(plan.At(slots[0].x, slots[0].z), "A square nothing is on reports something.");
        }

        /// <summary>
        /// Two things never stand in one place, and the refusal costs nothing.
        ///
        /// The plan has no way to represent two items at one position: the scene would draw them
        /// inside each other and `At` would answer with whichever it reached first.
        /// </summary>
        [Test]
        public void AnOccupiedSlotIsRefusedAndNothingIsLost()
        {
            var plan = new DecorPlan();
            var slots = plan.AllSlots(Zone).ToList();

            var first = plan.Buy(FurnitureKind.Sofa, Zone);
            var second = plan.Buy(FurnitureKind.Plant, Zone);

            plan.Store(second);

            Assert.IsFalse(plan.PlaceOn(second, first.X, first.Z),
                "Two pieces were allowed to stand in one place.");

            Assert.IsFalse(second.IsPlaced, "A refused placement stood the piece up anyway.");
            Assert.AreSame(first, plan.At(first.X, first.Z), "The refusal moved the piece already there.");

            Assert.AreEqual(2, plan.Items.Count, "A refused placement lost the piece.");
        }

        /// <summary>
        /// Storing keeps the piece and the money stays spent.
        ///
        /// The whole point of a store room: it is somewhere to put something you have already paid
        /// for while you decide, and quitting mid-decision must lose nobody anything.
        /// </summary>
        [Test]
        public void StoringKeepsThePieceAndSellingIsWhatLosesIt()
        {
            var simulation = Company();
            var state = simulation.State;

            Assert.IsEmpty(simulation.TryBuyFurniture(FurnitureKind.CoffeeBar, Zone));

            var item = state.Decor.Newest;
            Assert.IsNotNull(item);

            var spent = state.CashUsd;

            state.Decor.Store(item);

            Assert.AreEqual(1, state.Decor.Items.Count, "Storing lost the piece.");
            Assert.AreEqual(spent, state.CashUsd, "Storing refunded something.");
            Assert.IsFalse(item.IsPlaced);

            // And it can be stood up again wherever the player likes.
            var slot = state.Decor.AllSlots(Zone).First();
            Assert.IsTrue(state.Decor.PlaceOn(item, slot.x, slot.z));

            // Selling is the one that removes it, and it pays back less than it cost.
            var got = simulation.SellFurniture(item);

            Assert.Greater(got, 0.0);
            Assert.Less(got, FurnitureCatalog.Get(FurnitureKind.CoffeeBar).PriceUsd,
                "Furniture sold back for what it cost, which mints money out of a decision.");

            Assert.IsEmpty(state.Decor.Items, "The sold piece is still owned.");
        }

        /// <summary>
        /// Desks the player buys raise the hiring cap the same day.
        ///
        /// This is the one piece of furniture that touches the economy, and the author's whole reason
        /// for wanting the build mode was to test hiring against it.
        /// </summary>
        [Test]
        public void ADeskBoughtTodayCanBeHiredIntoToday()
        {
            var simulation = Company(33);
            var state = simulation.State;

            state.Staff.SetOffice(OfficeTier.Garage);

            var before = state.Staff.ExtraDesks;

            Assert.IsEmpty(simulation.TryBuyFurniture(FurnitureKind.Desk, Zone));

            Assert.Greater(state.Staff.ExtraDesks, before,
                "A desk bought and standing on the floor did not raise the seat count, so the "
                + "player has to wait a day to hire into it.");

            Assert.AreEqual(state.Decor.ExtraDesks, state.Staff.ExtraDesks,
                "The roster and the floor plan disagree about how many seats there are.");
        }

        /// <summary>The build mode's words exist in both languages.</summary>
        [Test]
        public void TheBuildRailSpeaksBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var key in new[]
                    {
                        "build.title", "build.done", "build.shop", "build.store", "build.store_empty",
                        "build.carrying", "build.stash", "build.no_floor", "plate.off_duty",
                        "plate.ceo_of"
                    })
                    {
                        Assert.AreNotEqual(key, Loc.T(key), $"{key} has no words in {language}.");
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }
    }
}
