using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Falling behind the frontier costs, and no longer erases.
    ///
    /// **This fixture is the guard on a decision taken 2026-11-01**, and both halves of it have to
    /// keep passing together or the decision has quietly been reversed in one direction or the
    /// other.
    ///
    /// A tester reported the late game as unsurvivable. The probe measured why: every funding stage
    /// required a share of a frontier the design intends to run away, so a scripted player fell
    /// under the Series A requirement of 55 per cent in its second year and was locked out of every
    /// round for the rest of the campaign. One round raised, ever, in fourteen years.
    ///
    /// The cost of being behind was already charged, once, in the price of money:
    /// <see cref="FundingMarket.PreMoneyValuationUsd"/> raises the story half of a valuation to the
    /// fourth power of parity. The gate charged for the same fact a second time, and the second
    /// charge was not a price, it was erasure. This repository had already settled that principle
    /// elsewhere, in those words.
    ///
    /// So the gate is gone and the fourth power stays. **Neither half is safe alone.** Remove the
    /// valuation penalty and falling behind becomes free, which breaks the spine. Put a parity gate
    /// back anywhere and the late game closes again.
    /// </summary>
    public sealed class FallingBehindCostsTests
    {
        private static ModelBlueprint Model() => new(
            "Muse 1", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.WebCrawl);

        /// <summary>A company that has shipped, so only parity is ever in question.</summary>
        private static CompanySimulation Shipped(uint seed = 4242)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", seed));
            simulation.SetRentedAccelerators(500);
            simulation.TryStartTraining(Model(), out _);
            simulation.Advance(40);
            simulation.TryReleaseModel(0, 1.0, out _);
            return simulation;
        }

        /// <summary>
        /// And then the calendar moves, which is the only way a company gets behind.
        ///
        /// **The first version of this fixture measured a company at 90 per cent of the frontier
        /// and called it behind.** A model shipped in February 2022 is competitive, because the
        /// frontier in 2022 is 31 and the field has barely started. Falling behind is something
        /// that happens over years, so the fixture has to let the years happen.
        /// </summary>
        private static CompanySimulation ShippedAndLeftAlone(int days)
        {
            var simulation = Shipped();
            simulation.Advance(days);
            return simulation;
        }

        /// <summary>
        /// The half that was the report: a company well behind the frontier can still raise.
        ///
        /// Forty four per cent is not a number picked to pass. It is what the Balanced style in the
        /// campaign probe actually held in 2026, measured, against a Series A requirement that used
        /// to be 55 per cent.
        /// </summary>
        [Test]
        public void ACompanyWellBehindTheFrontierCanStillOpenARound()
        {
            var simulation = ShippedAndLeftAlone(1100);

            Assert.That(simulation.State.IsBankrupt, Is.False,
                "The company went insolvent before the question could be asked, so this measures "
                + "nothing. Shorten the wait or give it a fleet it can afford.");

            var parity = simulation.State.BestCapability
                / System.Math.Max(1.0, simulation.Market.FrontierCapability);

            Assert.That(parity, Is.LessThan(0.55),
                $"This company sits at {parity:P0} of the frontier, which is above the gate that "
                + "used to exist, so the fixture is not measuring the case it was written for. "
                + "Shape the opening model so it lands behind the field.");

            var availability = simulation.NextRoundAvailability();

            Assert.That(availability.IsAvailable, Is.True,
                "A company that has shipped a product and sits behind the frontier was refused the "
                + "round: " + availability.Reason + ". Being behind is priced in the valuation, "
                + "not at the door, and a gate here locks the player out of capital exactly as "
                + "their need for it rises.");

            Assert.That(simulation.TryOpenFundingRound(out var why), Is.True, why);
        }

        /// <summary>
        /// The half that keeps the spine: the same money costs far more of the company.
        ///
        /// Measured through the one function that prices capital, at the same date, same revenue,
        /// same standing, so parity is the only thing that differs between the two readings.
        /// </summary>
        [Test]
        public void TheSameRoundCostsFarMoreOfACompanyThatIsBehind()
        {
            var date = GameDate.FromCalendar(2026, 1, 1);
            const double frontier = 77.0;
            const long revenue = 20_000_000L;

            // 44 per cent is the probe's measured parity in 2026; the other is a lab at the front.
            var behind = FundingMarket.PreMoneyValuationUsd(
                date, frontier * 0.44, frontier, revenue, 0.5, 100_000.0);

            var atTheFront = FundingMarket.PreMoneyValuationUsd(
                date, frontier, frontier, revenue, 0.5, 100_000.0);

            Assert.That(behind, Is.LessThan(atTheFront),
                "Falling behind the frontier cost the company nothing in the price of its own "
                + "equity, which is the spine of this game going missing.");

            var raise = FundingCatalog.Get(FundingStage.SeriesA).TargetRaiseUsd;

            var soldBehind = raise / (double)(behind + raise);
            var soldAhead = raise / (double)(atTheFront + raise);

            // **Measured, not chosen, and the first version of this asserted four times.**
            //
            // Four was wrong because it came from the story half of the valuation alone. The
            // valuation has a second half, revenue at a multiple of twenty, which does not read
            // parity at all, so at $20M of revenue the $75M story against $2B at parity is
            // diluted by $400M that both companies share. Measured at this revenue: 3.1 per cent
            // of the company against 1.0 per cent, which is three times and not twenty.
            //
            // **And the penalty fades as the company grows**: measured in the probe report at
            // 6.2x with no revenue, 2.6x at $20M, 1.4x at $100M and 1.0x at $1B. So parity is
            // priced severely while a lab is a story and not at all once it is a business.
            // Whether a large laggard should still be punished by capital is the author's call,
            // so it is recorded in Docs/TESTER_REPORTS.md and asserted nowhere.
            //
            // **Twice, not two and a half.** The first threshold here sat at 2.5 against a
            // measured 2.6, which is four per cent of headroom, and a guard with four per cent
            // of headroom goes red on a rounding change and gets deleted by whoever is in a
            // hurry. Two still catches the failure this exists for, which is the price of being
            // behind collapsing to nothing.
            Assert.That(soldBehind, Is.GreaterThan(soldAhead * 2.0),
                $"The same ${raise:N0} costs a company at 44 per cent of the frontier "
                + $"{soldBehind:P1} of itself and costs a frontier lab {soldAhead:P1}. That is "
                + "under twice the dilution, so being behind has stopped being a serious price, "
                + "and the gate that used to be at the door is not coming back to fix it.");
        }

        /// <summary>
        /// A large company behind the frontier pays something, and only a little.
        ///
        /// **Asked for by the author on 2026-11-01**, after the measurement showed that removing
        /// the gate left a billion-dollar laggard paying nothing at all for being behind: it sold
        /// 0.7 per cent of itself for a $250M round and so did a frontier lab. The revenue half of
        /// the valuation read no parity, so the fourth power on the story half faded to nothing as
        /// a company grew.
        ///
        /// Both bounds are the point. Below the lower one the penalty has gone again; above the
        /// upper one "a little" has quietly become the gate coming back through the valuation.
        /// </summary>
        [Test]
        public void ALargeCompanyBehindTheFrontierPaysSomethingAndOnlyALittle()
        {
            var date = GameDate.FromCalendar(2026, 1, 1);
            const double frontier = 77.0;
            const long revenue = 1_000_000_000L;
            const long round = 250_000_000L;

            var behind = FundingMarket.PreMoneyValuationUsd(
                date, frontier * 0.44, frontier, revenue, 0.5, 100_000.0);

            var atTheFront = FundingMarket.PreMoneyValuationUsd(
                date, frontier, frontier, revenue, 0.5, 100_000.0);

            var soldBehind = round / (double)(behind + round);
            var soldFront = round / (double)(atTheFront + round);
            var worse = soldBehind / soldFront;

            Assert.That(worse, Is.GreaterThan(1.10),
                $"A company on $1B of revenue at 44 per cent of the frontier sells {soldBehind:P2} "
                + $"of itself and a frontier lab sells {soldFront:P2}, which is {worse:0.00}x. "
                + "Falling behind costs a large company nothing, which is the state the author "
                + "asked to be changed.");

            Assert.That(worse, Is.LessThan(1.60),
                $"It is now {worse:0.00}x, which is not a little. A revenue discount this sharp is "
                + "the parity gate coming back in through the valuation, and the whole reason the "
                + "gate went is that erasure is not a price.");
        }

        /// <summary>
        /// And a company at the frontier is valued exactly as it was before any of this.
        ///
        /// **The neutral-option rule**, which every optional axis in this project is held to: an
        /// added term that is not exactly 1.0 at the neutral point retunes everything balanced
        /// before it existed. Asserted on the factor itself rather than through a valuation,
        /// because a product of large numbers can hide a one per cent drift.
        /// </summary>
        [Test]
        public void AFrontierLabIsPricedExactlyAsItWasBeforeTheRevenueDiscountExisted()
        {
            Assert.That(FundingCatalog.RevenueParityFactor(1.0), Is.EqualTo(1.0),
                "A company at the frontier is discounted for being at the frontier.");

            Assert.That(FundingCatalog.RevenueParityFactor(1.25), Is.EqualTo(1.0),
                "A company past the frontier is paid a bonus, which is a different mechanic "
                + "nobody asked for. The factor has to flatten at parity.");

            Assert.That(FundingCatalog.RevenueParityFactor(0.0),
                Is.EqualTo(FundingCatalog.RevenueParityFloor),
                "A company with no claim on the frontier should sit exactly on the floor.");

            foreach (var nonsense in new[] { double.NaN, double.PositiveInfinity, -3.0 })
            {
                var factor = FundingCatalog.RevenueParityFactor(nonsense);

                Assert.That(double.IsNaN(factor), Is.False, $"{nonsense} produced NaN.");

                Assert.That(factor, Is.InRange(FundingCatalog.RevenueParityFloor, 1.0),
                    $"{nonsense} produced {factor}, outside the band the factor is defined on.");
            }
        }

        /// <summary>
        /// And nothing else about the door moved.
        ///
        /// **Written because removing one requirement is exactly when the others go quietly with
        /// it.** A company with no product must still be refused, and the calendar must still hold:
        /// the decision was that parity is priced rather than gated, not that capital is free.
        /// </summary>
        [Test]
        public void TheRequirementsThatAreNotAboutParityStillHold()
        {
            var nothing = new CompanySimulation(new CompanyState("Nothing yet"));
            var availability = nothing.NextRoundAvailability();

            Assert.That(availability.IsAvailable, Is.False,
                "A lab that has shipped nothing was offered a Series A.");

            Assert.That(availability.Refusal, Is.EqualTo(FundingRefusal.NeedsReleasedModels),
                "It was refused for the wrong reason: " + availability.Reason);

            // The calendar gates are per stage and the later ones are dated. Growth opens in 2024,
            // and a 2022 company may not reach it however much revenue it is pretending to have.
            var early = FundingMarket.Evaluate(
                FundingStage.Growth, GameDate.FromCalendar(2022, 6, 1),
                10_000_000_000L, 9);

            Assert.That(early.IsAvailable, Is.False,
                "The Growth round opened in 2022, so a calendar gate went with the parity gate.");

            Assert.That(early.Refusal, Is.EqualTo(FundingRefusal.TooEarly),
                "Refused, but not by the calendar: " + early.Reason);
        }
    }
}
