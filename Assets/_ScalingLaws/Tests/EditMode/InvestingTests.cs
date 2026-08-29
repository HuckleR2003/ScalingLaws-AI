using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Owning shares in rivals, the monthly cheque, and buying a company outright.
    ///
    /// The two that matter most are the ones holding the economy's spine in place. A dividend that
    /// outgrows running the company turns the whole game into a savings account, and a takeover
    /// that hands over everything makes buying strictly better than building.
    /// </summary>
    public sealed class InvestingTests
    {
        private static CompanySimulation Company(long cash = 20_000_000_000L)
        {
            var state = new CompanyState("Test Lab", 4242);
            state.CashUsd = cash;
            return new CompanySimulation(state);
        }

        // ---- the market ---------------------------------------------------------------------------

        /// <summary>
        /// Nothing about the board is stored, so two companies on the same day see one market.
        ///
        /// This is what lets the chart draw ninety days of history without a single number in the
        /// save file, and it is the property that breaks the moment somebody caches a price.
        /// </summary>
        [Test]
        public void ThePriceOfALabIsTheSameForEverybodyOnTheSameDay()
        {
            var one = Company();
            var two = Company();

            foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
            {
                Assert.That(one.SharePriceOf(lab), Is.EqualTo(two.SharePriceOf(lab)).Within(1e-9),
                    $"{lab} is priced differently for two companies looking on the same day.");
            }
        }

        /// <summary>Every price stays a price: finite, positive, and not an astronomical number.</summary>
        [Test]
        public void NoPriceEverLeavesItsBand()
        {
            var simulation = Company();

            for (var day = 0; day < 5000; day += 137)
            {
                simulation.Advance(137);

                foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
                {
                    var price = simulation.SharePriceOf(lab);

                    Assert.That(double.IsNaN(price) || double.IsInfinity(price), Is.False);
                    Assert.That(price, Is.GreaterThan(0.0));
                    Assert.That(price, Is.LessThan(5000.0));
                }
            }
        }

        /// <summary>The chart has something to draw: the line is not flat over three months.</summary>
        [Test]
        public void ThePriceActuallyMoves()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            var seen = Enumerable.Range(0, 90)
                .Select(day => Math.Round(
                    ShareMarket.PriceUsd(lab, 40.0, 0.5, new GameDate(day)), 3))
                .Distinct()
                .Count();

            Assert.That(seen, Is.GreaterThan(40),
                "Ninety days produced almost no distinct prices, so the chart is a straight line.");
        }

        // ---- trading ------------------------------------------------------------------------------

        [Test]
        public void BuyingCostsCashAndLeavesAHoldingWorthRoughlyWhatItCost()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            var before = simulation.State.CashUsd;

            Assert.That(simulation.TryBuyShares(lab, 1_000_000, out var cost, out var why),
                Is.True, why);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - cost));
            Assert.That(simulation.SharesHeldIn(lab), Is.EqualTo(1_000_000));
            Assert.That(simulation.SpentOnSharesIn(lab), Is.EqualTo(cost));

            // Worth a little less than it cost, because the commission is charged both ways. A
            // holding that was instantly worth what was paid for it would make trading free.
            Assert.That(simulation.ValueOfHoldingIn(lab), Is.LessThan(cost));
            Assert.That(simulation.ValueOfHoldingIn(lab), Is.GreaterThan(cost * 0.9));
        }

        /// <summary>Selting part of a holding leaves the rest still saying what it cost.</summary>
        [Test]
        public void APartialSaleLeavesTheRestOfTheHoldingCosted()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            simulation.TryBuyShares(lab, 1_000_000, out var cost, out _);
            simulation.TrySellShares(lab, 400_000, out _, out _);

            Assert.That(simulation.SharesHeldIn(lab), Is.EqualTo(600_000));

            Assert.That(simulation.SpentOnSharesIn(lab),
                Is.EqualTo((long)(cost * 0.6)).Within(cost / 100),
                "The remainder reads as though it cost nothing, or as though it cost the lot.");
        }

        [Test]
        public void NobodySellsMoreOfACompanyThanIsOnOffer()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            var outstanding = ShareMarket.SharesOutstanding(lab);

            Assert.That(simulation.TryBuyShares(lab, outstanding, out _, out var why), Is.False);
            Assert.That(why, Is.Not.Empty);

            Assert.That(simulation.SharesAvailableIn(lab), Is.LessThan(outstanding),
                "The whole company is on the market, so control is one click rather than a run.");
        }

        // ---- the cheque ---------------------------------------------------------------------------

        /// <summary>
        /// The dividend is a reason to hold and never a reason to stop building.
        ///
        /// **This is the spine wearing a test.** Capital left sitting still has to lose a race in
        /// this game, so a year of dividends must stay far below what the same money does inside
        /// the company. Four percent a year is a rounding error against a training run.
        /// </summary>
        [Test]
        public void TheDividendIsSmallEnoughToStayADecision()
        {
            var yearly = ShareMarket.MonthlyYield * 12.0;

            Assert.That(yearly, Is.LessThan(0.08),
                "A holding pays more than eight percent a year, which makes doing nothing a "
                + "strategy and this game is built on the opposite.");

            Assert.That(yearly, Is.GreaterThan(0.01),
                "It pays so little that holding shares is never worth the screen.");
        }

        [Test]
        public void HoldingSharesPaysOnTheFirstOfTheMonth()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            simulation.Advance(400);
            simulation.TryBuyShares(lab, 20_000_000, out _, out _);

            var before = simulation.State.CashUsd;
            var paid = false;

            for (var day = 0; day < 62 && !paid; day++)
            {
                var was = simulation.State.CashUsd;
                simulation.Advance(1);

                if (simulation.State.CashUsd > was)
                {
                    paid = true;
                }
            }

            Assert.That(paid, Is.True,
                "Two months went by holding twenty million shares and nothing was ever paid.");

            Assert.That(before, Is.GreaterThan(0L));
        }

        // ---- buying the company ---------------------------------------------------------------------

        [Test]
        public void ACompanyCannotBeBoughtWithoutControlFirst()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            Assert.That(simulation.CanTakeOver(lab, out _, out var why), Is.False);
            Assert.That(why, Is.Not.Empty, "A refusal with no sentence reads as a bug.");
        }

        /// <summary>
        /// The last shares are the dearest.
        ///
        /// Without the premium, creeping to 49% and buying the rest at list price would make a
        /// takeover cheaper than the shares it is made of, and everybody would do it that way.
        /// </summary>
        [Test]
        public void TheRestOfACompanyCostsMoreThanTheSharesAreListedAt()
        {
            var lab = CompetitorId.OpenAi;
            var outstanding = ShareMarket.SharesOutstanding(lab);
            var held = (long)(outstanding * 0.5);

            var buyout = ShareMarket.BuyoutCostUsd(lab, held, 20.0);
            var listed = (outstanding - held) * 20.0;

            Assert.That(buyout, Is.GreaterThan(listed));
        }

        /// <summary>
        /// Buying a rival hands over most of what they had, and never all of it.
        ///
        /// A quarter of the people who liked them liked them for not being you. Take all of it and
        /// acquisition becomes strictly better than out-building anybody.
        /// </summary>
        [Test]
        public void NobodyKeepsEverythingTheyBought()
        {
            Assert.That(ShareMarket.TransferShare, Is.LessThan(1.0));
            Assert.That(ShareMarket.TransferShare, Is.GreaterThan(0.4));
        }

        [Test]
        public void BuyingACompanyTakesTheirNewestModelAndSomeOfTheirFollowing()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            // Far enough in that the lab has shipped something to be bought.
            simulation.Advance(900);

            simulation.State.Shareholdings[lab] =
                (long)(ShareMarket.SharesOutstanding(lab) * 0.55);

            Assert.That(simulation.CanTakeOver(lab, out var cost, out var why), Is.True, why);

            simulation.State.CashUsd = cost * 2;

            var models = simulation.State.DeployedModels.Count;
            var fans = simulation.State.Fans;

            Assert.That(simulation.TryTakeOver(lab, out var failure), Is.True, failure);

            Assert.That(simulation.State.AcquiredLabs.Contains(lab), Is.True);

            Assert.That(simulation.State.DeployedModels.Count, Is.GreaterThan(models),
                "Their newest model did not join the fleet, so the purchase bought a number.");

            Assert.That(simulation.State.Fans, Is.GreaterThanOrEqualTo(fans));

            Assert.That(simulation.SharesAvailableIn(lab), Is.EqualTo(0L),
                "A company that has been bought is still trading.");
        }

        // ---- the save -------------------------------------------------------------------------------

        /// <summary>
        /// Holdings survive a save, and the market does not need to.
        ///
        /// The prices are derived from each lab's standing on the day, so a reload sees the same
        /// board it left. What has to come back is what the player bought and what they paid.
        /// </summary>
        [Test]
        public void SharesSurviveASaveAndTheBoardIsUnchanged()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            simulation.Advance(500);
            simulation.TryBuyShares(lab, 3_000_000, out var cost, out _);

            var priceBefore = simulation.SharePriceOf(lab);

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));
            var reloaded = new CompanySimulation(restored);

            Assert.That(reloaded.SharesHeldIn(lab), Is.EqualTo(3_000_000));
            Assert.That(reloaded.SpentOnSharesIn(lab), Is.EqualTo(cost));

            Assert.That(reloaded.SharePriceOf(lab), Is.EqualTo(priceBefore).Within(1e-9),
                "The board moved across a save, so the chart is not reproducible.");
        }
    }
}
