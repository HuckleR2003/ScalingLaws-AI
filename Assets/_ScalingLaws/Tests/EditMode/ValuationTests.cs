using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What a company is worth to somebody who has to write the cheque.
    ///
    /// **Reported: the valuation reads like a cheat.** A lab that ships one good model in its first
    /// year is priced in billions while it holds twenty million dollars and has never invoiced
    /// anybody, and three to six months later, once rivals have shipped, the same company collapses
    /// to a fraction of that. Both halves of that are the same fault: the price was almost entirely
    /// a story about one model, and a story is worth what the teller is trusted for.
    ///
    /// The numbers in these tests are the measurement, not a preference. They were taken from the
    /// formula before it changed and they are here so the next change to it is a decision.
    /// </summary>
    public sealed class ValuationTests
    {
        private static readonly GameDate EarlyDays = GameDate.FromCalendar(2022, 6, 1);

        /// <summary>
        /// **The headline fault, stated as a number.** A pre-revenue company with one model at the
        /// frontier, no reputation and no following, on day one hundred and fifty.
        /// </summary>
        [Test]
        public void AnUnknownLabWithOneGoodModelIsNotWorthBillions()
        {
            var worth = FundingMarket.PreMoneyValuationUsd(
                EarlyDays,
                bestCapability: 42.0,
                frontierCapability: 42.0,
                annualRevenueRunRateUsd: 0L,
                reputation: 0.08,
                fans: 0.0);

            Assert.That(worth, Is.LessThan(400_000_000L),
                "A lab nobody has heard of, with no revenue and no following, is priced at "
                + UiMoney(worth) + ". Measured at $1,236,036,036 on these same inputs before "
                + "the trust term went in, which is the figure the report called a cheat.");

            Assert.That(worth, Is.GreaterThan(0L),
                "A company with a frontier model is worth nothing at all, which is the opposite "
                + "mistake and would make the whole funding ladder unreachable.");
        }

        /// <summary>
        /// **The other half: proving it has to pay.** The same model, at a company people have heard
        /// of and stayed with.
        /// </summary>
        [Test]
        public void BeingTrustedIsWorthMoreThanBeingClever()
        {
            var unknown = FundingMarket.PreMoneyValuationUsd(
                EarlyDays, 42.0, 42.0, 0L, reputation: 0.08, fans: 0.0);

            var known = FundingMarket.PreMoneyValuationUsd(
                EarlyDays, 42.0, 42.0, 0L, reputation: 0.72, fans: 400_000.0);

            Assert.That(known, Is.GreaterThan(unknown * 3L),
                "Reputation and a following barely moved the price, so the thing the report asked "
                + "investors to read is not being read: " + UiMoney(unknown) + " against "
                + UiMoney(known) + ".");
        }

        /// <summary>
        /// **Revenue is not a story and is not discounted.** A company being paid by real customers
        /// has already proved the thing reputation is a proxy for.
        /// </summary>
        [Test]
        public void RevenueCountsInFullWhoeverYouAre()
        {
            var quiet = FundingMarket.PreMoneyValuationUsd(
                EarlyDays, 20.0, 42.0, 40_000_000L, reputation: 0.08, fans: 0.0);

            var loud = FundingMarket.PreMoneyValuationUsd(
                EarlyDays, 20.0, 42.0, 40_000_000L, reputation: 0.72, fans: 400_000.0);

            var sentiment = FundingCatalog.SentimentOn(EarlyDays);
            var revenueAlone = 40_000_000L * FundingCatalog.RevenueMultiple * sentiment;

            Assert.That(quiet, Is.GreaterThan(revenueAlone * 0.9),
                "A company with $40M of run rate is priced under what its revenue alone is worth, "
                + "so being unknown is being charged twice.");

            Assert.That(loud, Is.GreaterThan(quiet),
                "Reputation stopped mattering once there was revenue, which is the opposite of "
                + "what it should do.");
        }

        /// <summary>
        /// **The collapse the report describes, and how far it is allowed to go.**
        ///
        /// A company that stands still while the frontier moves must lose value: that is the spine
        /// of this game and it is not a fault. What was a fault is the size of the drop, because
        /// almost the whole price was a story about one model and nothing else held it up.
        /// </summary>
        [Test]
        public void FallingBehindCostsValueWithoutErasingTheCompany()
        {
            const double Frontier = 42.0;

            var atPar = FundingMarket.PreMoneyValuationUsd(
                EarlyDays, Frontier, Frontier, 30_000_000L, 0.5, 120_000.0);

            // Six months of a moving frontier against a company that shipped nothing.
            var behind = FundingMarket.PreMoneyValuationUsd(
                GameDate.FromCalendar(2022, 12, 1), Frontier, Frontier * 1.55, 30_000_000L,
                0.5, 120_000.0);

            Assert.That(behind, Is.LessThan(atPar),
                "Standing still while the frontier moved cost nothing, which is the spine of this "
                + "game not working.");

            Assert.That(behind, Is.GreaterThan(atPar / 6L),
                "The company lost more than five sixths of its value in six months without doing "
                + "anything wrong: " + UiMoney(atPar) + " to " + UiMoney(behind) + ". Revenue and "
                + "a following are supposed to be the floor under that.");
        }

        private static string UiMoney(long value) => "$" + value.ToString("N0");
    }
}
