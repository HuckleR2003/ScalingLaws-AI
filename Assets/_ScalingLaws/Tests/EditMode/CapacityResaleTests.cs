using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Selling the part of the fleet nothing is using.
    ///
    /// **This fixture exists mostly to hold one promise.** The spine of this game is that capital
    /// spent too early is a loss, and a feature that hands money back for idle capacity is the
    /// shape of thing that quietly undoes it. It does not undo it here, and the reason is
    /// arithmetic rather than tuning: capacity resells below what it costs to hold, so a company
    /// that rents more than it needs recovers at most half of what it overspent.
    ///
    /// `RentingTwiceWhatYouNeedIsStillWorseThanRentingWhatYouNeed` is that promise as a test. If it
    /// ever goes red, the mechanic has become a rebate on over-buying and the price fraction is
    /// what to look at first.
    /// </summary>
    [TestFixture]
    public sealed class CapacityResaleTests
    {
        private const int TopLevel = CapacityResale.MostLevels;

        private static CompanySimulation Company(double petaflops, int level = 0)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2023, 9, 1),
                CashUsd = 400_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(petaflops);

            if (level > 0)
            {
                state.UnlockedResearch.Add(ResearchNodeId.SpotCapacityDesk);
                state.CapacityResaleLevel = level;
            }

            return simulation;
        }

        // ---- the ladder --------------------------------------------------------------------

        [Test]
        public void TheLadderClimbsAndTheTopIsTheTop()
        {
            Assert.AreEqual((0.0, 0.0), CapacityResale.BandAt(0),
                "Level zero sells something, so a company that bought nothing is already trading.");

            for (var level = 1; level <= TopLevel; level++)
            {
                var (low, high) = CapacityResale.BandAt(level);
                var (lowBelow, highBelow) = CapacityResale.BandAt(level - 1);

                Assert.That(low, Is.GreaterThan(lowBelow), $"Level {level} has no better floor.");
                Assert.That(high, Is.GreaterThan(highBelow), $"Level {level} has no better ceiling.");
                Assert.That(high, Is.GreaterThan(low), $"Level {level} is a figure, not a band.");
            }

            Assert.AreEqual(CapacityResale.BandAt(TopLevel), CapacityResale.BandAt(TopLevel + 40),
                "Asking past the top climbs further instead of answering the top.");
        }

        /// <summary>
        /// The cap is a share of the whole fleet, and that is the property that bounds this.
        ///
        /// **A percentage of surplus would be unbounded.** The campaign probe measured capacity at
        /// 3,082 times demand, so 30% of the surplus is a second business rather than a rebate.
        /// 30% of the fleet resold at half price returns 15% of the bill and can never return more
        /// than the bill itself.
        /// </summary>
        [Test]
        public void TheMostItCanEverTakeOffTheBillIsWellUnderTheBill()
        {
            var most = CapacityResale.BillShareAt(TopLevel, 1.0);

            Assert.That(most, Is.GreaterThan(0.10),
                "The finished ladder takes almost nothing off the bill, so nobody would climb it.");

            Assert.That(most, Is.LessThan(0.25),
                "The finished ladder takes more than a quarter of the fleet bill, which is not a "
                + "trim on a fixed cost any more.");

            Assert.That(CapacityResale.ResalePriceFraction, Is.LessThan(1.0),
                "Capacity resells for what it costs to hold or more, which makes renting more than "
                + "you need a way to earn money. That is the one thing this must never be.");
        }

        /// <summary>
        /// A shortage is when somebody will take spare capacity, and a glut is when nobody will.
        /// </summary>
        [Test]
        public void ItPaysBestWhenAcceleratorsAreShort()
        {
            var tight = CapacityResale.ShareOn(TopLevel, 1.0);
            var loose = CapacityResale.ShareOn(TopLevel, 0.0);

            Assert.That(tight, Is.GreaterThan(loose),
                "Scarcity does not move where in the band today lands, so the desk is a standing "
                + "order rather than a market and the timing goes out of the decision.");

            Assert.AreEqual(CapacityResale.BandAt(TopLevel).Low, loose, 1e-9);
            Assert.AreEqual(CapacityResale.BandAt(TopLevel).High, tight, 1e-9);
        }

        // ---- the promise -------------------------------------------------------------------

        /// <summary>
        /// Renting twice what you need is still worse than renting what you need.
        ///
        /// **The spine, as a measurement, and the first version of this test was wrong.** It
        /// compared a quarter of a petaflop against a half, and at that size the company cannot
        /// serve all the demand it has, so the bigger fleet won on revenue for reasons that have
        /// nothing to do with the resale desk. Renting more genuinely does help a company that is
        /// short of capacity; that is not the claim.
        ///
        /// The claim is about a company that already serves everybody it can, where more capacity
        /// buys nothing and costs rent. Measured over six months with the ladder finished on both:
        /// $269,977,787 against $139,535,969. At half price the desk recovers half of what was
        /// overspent when the idle cap binds and less when the ladder cap does, so there is no
        /// fleet size at which this comes out ahead.
        /// </summary>
        [Test]
        public void RentingTwiceWhatYouNeedIsStillWorseThanRentingWhatYouNeed()
        {
            var rightSized = Company(4_000.0, TopLevel);
            var overRented = Company(8_000.0, TopLevel);

            for (var day = 0; day < 180; day++)
            {
                rightSized.AdvanceDay();
                overRented.AdvanceDay();
            }

            Assert.That(overRented.State.LastQuality.Utilisation, Is.LessThan(0.1),
                "Both companies have to be able to serve everything they have, or this measures "
                + "whether capacity helps rather than whether the desk pays for over-buying.");

            Assert.That(overRented.CapacitySalesLastMonthUsd(),
                Is.GreaterThan(rightSized.CapacitySalesLastMonthUsd()),
                "The over-rented company did not out-sell the right-sized one, so the test passes "
                + "without the desk ever being the thing under test.");

            Assert.That(overRented.State.CashUsd, Is.LessThan(rightSized.State.CashUsd),
                "Renting twice what the company needs came out ahead of renting what it needs. "
                + "The resale desk has become a rebate on over-buying; look at "
                + "CapacityResale.ResalePriceFraction first.");
        }

        /// <summary>
        /// The desk takes a real bite out of the fixed cost, inside the band the author asked for.
        ///
        /// His brief was 4% to 20% off server spend at the top of the ladder, and the arithmetic
        /// lands at 14.7%: a 29.4% share of the fleet at today's scarcity, resold at half price.
        /// Measured end to end over six months on an idle fleet, $247,046,582 without the desk
        /// against $269,977,787 with it.
        ///
        /// **Both ends are asserted.** Too little and nobody climbs a ladder that costs 4,320
        /// research points; too much and it stops being a trim on a fixed cost.
        /// </summary>
        [Test]
        public void TheFinishedDeskTakesTheBiteOutOfTheBillTheAuthorAskedFor()
        {
            var without = Company(4_000.0);
            var with = Company(4_000.0, TopLevel);

            for (var day = 0; day < 180; day++)
            {
                without.AdvanceDay();
                with.AdvanceDay();
            }

            var spentWithout = 400_000_000L - without.State.CashUsd;
            var spentWith = 400_000_000L - with.State.CashUsd;

            Assert.That(spentWithout, Is.GreaterThan(0L), "Nothing was spent, so nothing is saved.");

            var saved = (spentWithout - spentWith) / (double)spentWithout;

            Assert.That(saved, Is.GreaterThan(0.04),
                $"The finished ladder saved {saved:P1} of what the company spent, which is under "
                + "the 4% the brief asked for, and it costs 4,320 research points to get there.");

            Assert.That(saved, Is.LessThan(0.20),
                $"The finished ladder saved {saved:P1}, over the 20% the brief set as the top. "
                + "Past that it stops being a trim on a fixed cost.");
        }

        /// <summary>
        /// A busy company sells nothing, however many levels it bought.
        ///
        /// The honest answer rather than a penalty: there is nothing spare. It is also what stops
        /// the ladder being free money for a company that is doing well.
        /// </summary>
        [Test]
        public void ABusyCompanyHasNothingToSell()
        {
            var busy = Company(0.1, TopLevel);

            for (var day = 0; day < 120; day++)
            {
                busy.AdvanceDay();
            }

            Assert.That(busy.State.LastQuality.Utilisation, Is.GreaterThan(0.95),
                "This company has room to spare, so it is not the case under test.");

            Assert.That(busy.IdleShareOfFleet(), Is.LessThan(0.01),
                "A cluster at over 90% load is reported as having a tenth of itself idle.");

            Assert.That(busy.CapacitySoldShareToday(), Is.LessThan(0.01),
                "A company serving nearly everybody it can is still placing a tenth of its fleet "
                + "with somebody else, which it does not have to give.");
        }

        /// <summary>
        /// What is sold is gone for the day.
        ///
        /// **Without this the ladder is a discount with no downside**, and it is the half that makes
        /// the desk a decision: a company living off resale has no headroom when demand jumps.
        /// </summary>
        [Test]
        public void SoldCapacityIsNotAvailableToServeWith()
        {
            var keeping = Company(4_000.0);
            var selling = Company(4_000.0, TopLevel);

            for (var day = 0; day < 60; day++)
            {
                keeping.AdvanceDay();
                selling.AdvanceDay();
            }

            Assert.That(selling.CapacitySoldShareToday(), Is.GreaterThan(0.0),
                "An idle fleet placed none of itself, so nothing is being measured.");

            Assert.That(selling.State.LastQuality.Capacity,
                Is.LessThan(keeping.State.LastQuality.Capacity),
                "The company sold a share of its fleet and can still serve with all of it, so the "
                + "sale costs it nothing and there is no decision here.");
        }

        /// <summary>
        /// An idle fleet is worth something, and the books say which line it came in on.
        /// </summary>
        [Test]
        public void AnIdleFleetEarnsAndItLandsOnItsOwnLine()
        {
            var idle = Company(4_000.0, TopLevel);

            for (var day = 0; day < 40; day++)
            {
                idle.AdvanceDay();
            }

            Assert.That(idle.CapacitySalesLastMonthUsd(), Is.GreaterThan(0L),
                "A fleet at almost no load earned nothing from a finished ladder.");

            Assert.That(idle.CapacitySalesYesterdayUsd(), Is.GreaterThan(0L),
                "Yesterday is the other figure the panel draws and it is empty.");

            var month = Ledger.MonthKeyOf(idle.State.Date);
            Assert.That(idle.State.Ledger.MonthTotal(month, LedgerLine.CapacitySales),
                Is.GreaterThan(0L),
                "The takings reached the bank and not the books, so the finance page cannot "
                + "explain where the money came from.");
        }

        /// <summary>
        /// Nothing is sold without somebody to sell it, and the refusal is a sentence.
        /// </summary>
        [Test]
        public void WithoutTheDeskNothingIsSoldAndTheLadderRefuses()
        {
            var simulation = Company(4_000.0);
            simulation.State.CapacityResaleLevel = TopLevel;

            Assert.That(simulation.CapacitySoldShareToday(), Is.EqualTo(0.0),
                "A company with levels and no desk is trading, so the node opens nothing.");

            Assert.IsFalse(simulation.TryUpgradeCapacityResale(out var why));
            Assert.IsNotNull(why, "A refusal with no sentence is a button that does nothing.");
        }

        /// <summary>
        /// Points are the real price, and the ladder stops at the top rather than taking money.
        /// </summary>
        [Test]
        public void TheLadderCostsPointsAndStopsWhenItIsFinished()
        {
            var simulation = Company(4_000.0);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.SpotCapacityDesk);

            simulation.State.ResearchPoints = 0.0;
            Assert.IsFalse(simulation.TryUpgradeCapacityResale(out _),
                "A company with a full bank and no research bought a level anyway.");

            simulation.State.ResearchPoints = 1_000_000.0;

            for (var level = 0; level < TopLevel; level++)
            {
                Assert.IsTrue(simulation.TryUpgradeCapacityResale(out var why),
                    $"Level {level + 1} was refused: {why}");
            }

            var cash = simulation.State.CashUsd;
            Assert.IsFalse(simulation.TryUpgradeCapacityResale(out _),
                "A finished ladder sold another level.");
            Assert.AreEqual(cash, simulation.State.CashUsd, "A refused upgrade took the money.");
        }

        /// <summary>
        /// The level and the month of takings both survive a save.
        /// </summary>
        [Test]
        public void TheLevelAndTheMonthSurviveASave()
        {
            var simulation = Company(4_000.0, 3);

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var earned = simulation.CapacitySalesLastMonthUsd();
            Assert.That(earned, Is.GreaterThan(0L), "Nothing was recorded, so nothing is proven.");

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));
            var after = new CompanySimulation(restored);

            Assert.AreEqual(3, restored.CapacityResaleLevel, "The ladder was not saved.");
            Assert.AreEqual(earned, after.CapacitySalesLastMonthUsd(),
                "The month of takings did not survive, so the panel reports an empty month to a "
                + "company that has been selling all month.");
        }

        /// <summary>
        /// The idle reading is about the serving slice nothing asked for, not about the whole fleet.
        ///
        /// **`TrainingComputeShare` is a reservation rather than a consumption.** A reading of
        /// `1 - utilisation` would call a cluster that is flat out training almost entirely idle,
        /// and then sell it. This is the fault this project has found four times in other guises.
        /// </summary>
        [Test]
        public void IdleIsNeverMoreThanTheFleetAndNeverLessThanNothing()
        {
            var simulation = Company(4_000.0, TopLevel);

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();

                var idle = simulation.IdleShareOfFleet();
                Assert.That(idle, Is.InRange(0.0, 1.0), $"Idle share out of range on day {day}.");

                var sold = simulation.CapacitySoldShareToday();
                Assert.That(sold, Is.InRange(0.0, 1.0), $"Sold share out of range on day {day}.");
                Assert.That(sold, Is.LessThanOrEqualTo(idle + 1e-9),
                    $"Day {day}: more of the fleet was sold than was idle to sell.");
            }
        }
    }
}
