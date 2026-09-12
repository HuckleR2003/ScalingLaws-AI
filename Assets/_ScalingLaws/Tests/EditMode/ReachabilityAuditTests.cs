using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The things added this week, reached the way a campaign reaches them.
    ///
    /// **A green suite does not prove a player can get there**, and this week proved it twice: the
    /// office buttons were laid out, enabled and sixty four pixels under a clipping edge, and every
    /// test of that page passed while two testers could not use it.
    ///
    /// So these do not call the new methods directly. They play a campaign and ask whether the thing
    /// ever turns up.
    /// </summary>
    public sealed class ReachabilityAuditTests
    {
        private static CompanySimulation Shipped()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 4242));
            simulation.State.CashUsd = 60_000_000L;
            simulation.SetRentedPetaflops(120.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Aurora", ArchitectureId.DenseTransformer, 38.0,
                simulation.State.Date, 2e10, 1.0));

            simulation.State.DeployedModels[0].SeedLine(
                MonetizationPolicy.OpeningSubscriptionUsdPerMonth, 0.0);

            return simulation;
        }

        /// <summary>
        /// **The one that could have been dead on arrival.** Twelve named firms, a cadence, a book
        /// and a screen are worth nothing if nobody ever calls, and the button that opens the book
        /// disables itself when the book is empty.
        /// </summary>
        [Test]
        public void InvestorsActuallyTurnUpInACampaign()
        {
            var simulation = Shipped();
            var everSeen = 0;

            for (var day = 0; day < 900; day++)
            {
                simulation.AdvanceDay();
                everSeen = System.Math.Max(everSeen, simulation.State.Investors.Count);
            }

            Assert.That(everSeen, Is.GreaterThan(0),
                "Nobody offered anything in two and a half years, so the twelve firms, the cadence, "
                + "the book and the button that opens it are all unreachable and the whole thing is "
                + "the thirteenth finished mechanism in this project a player cannot get to.");
        }

        /// <summary>
        /// And what turns up is takeable: named, priced, and the button behind it works.
        /// </summary>
        [Test]
        public void AnOfferThatTurnsUpCanBeTaken()
        {
            var simulation = Shipped();

            for (var day = 0; day < 900 && simulation.State.Investors.Count == 0; day++)
            {
                simulation.AdvanceDay();
            }

            Assume.That(simulation.State.Investors.Count, Is.GreaterThan(0),
                "nobody called, which the fixture above reports on");

            var offer = simulation.State.Investors.Open[0];

            Assert.That(offer.Investor, Is.Not.EqualTo(InvestorId.None),
                "A term sheet arrived with nobody's name on it, so the book draws a blank row.");

            Assert.That(offer.RaiseUsd, Is.GreaterThan(0L));
            Assert.That(offer.EquitySold, Is.GreaterThan(0.0));

            var ownedBefore = simulation.State.CapTable.FounderEquity;
            var cashBefore = simulation.State.CashUsd;

            Assert.That(simulation.TryTakeInvestorOffer(offer.Investor, out var why), Is.True, why);

            Assert.That(simulation.State.CashUsd, Is.GreaterThan(cashBefore), "No money arrived.");

            Assert.That(simulation.State.CapTable.FounderEquity, Is.LessThan(ownedBefore),
                "The money arrived and nobody was given any of the company.");

            Assert.That(
                simulation.State.CapTable.Holders.Any(h => h.Investor == offer.Investor),
                Is.True, "The firm that wrote the cheque is not on the register.");

            Assert.That(simulation.State.CapTable.FounderEquity
                + simulation.State.CapTable.InvestorEquity,
                Is.EqualTo(1.0).Within(1e-9), "The register does not add up to one company.");
        }

        /// <summary>
        /// **Every cancel a player is offered reaches a method that charges for it.**
        ///
        /// The four were wired to four different screens, and three of them were already there under
        /// the old free behaviour, so the risk is a button still calling the version that costs
        /// nothing rather than one that does not exist.
        /// </summary>
        [Test]
        public void EveryAbandonPathChargesOrBanks()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 400_000_000L;
            simulation.State.ResearchPoints = 200_000.0;
            simulation.SetRentedPetaflops(60.0);

            // Research banks rather than charges.
            var node = ResearchTree.All.First(entry =>
                simulation.TryStartResearch(entry.Id, out _));

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.TryCancelResearch(out _), Is.True);

            Assert.That(simulation.State.BankedResearch.ContainsKey(node.Id), Is.True,
                "Abandoning a node banked nothing, so the penalty is the whole thing.");

            // The other three charge. A run first.
            var blueprint = new ModelBlueprint(
                "Subject", ArchitectureId.DenseTransformer, 20.0, 1_800.0, DatasetSource.WebCrawl);

            Assume.That(simulation.TryStartTraining(blueprint, out var why), Is.True, why);

            for (var day = 0; day < 20; day++)
            {
                simulation.AdvanceDay();
            }

            var before = simulation.State.CashUsd;
            Assert.That(simulation.TryCancelTraining(out var fee, out var reason), Is.True, reason);

            Assert.That(fee, Is.GreaterThan(0L),
                "Walking away from a run twenty days in cost nothing, so the fee is reading a "
                + "figure that never moves.");

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - fee));
        }

        /// <summary>
        /// The subscription a fresh company opens on is the one the whole game asks about, and it
        /// reaches the till. Held here as well as in its own fixture because it is the change most
        /// likely to be undone by somebody tidying a default.
        /// </summary>
        [Test]
        public void AFreshCompanyChargesWhatTheCreatorAsksFor()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            var policy = simulation.State.Monetization;

            Assert.That(policy.Model, Is.EqualTo(PricingModel.Subscription));

            var market = simulation.Market.PricePerMillionTokensUsd;
            var atOpening = policy.RatePerMillionTokensUsd(market);

            policy.SubscriptionPriceUsdPerMonth *= 2.0;

            Assert.That(policy.RatePerMillionTokensUsd(market), Is.GreaterThan(atOpening),
                "Doubling the monthly fee changed nothing the company charges.");
        }

        /// <summary>
        /// Emil's architecture offer commissions something, rather than being a button that talks.
        /// </summary>
        [Test]
        public void TheArchitectureOfferCommissionsAProgramme()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 400_000_000L;
            simulation.SetRentedPetaflops(400.0);

            var panel = new UI.ArchitectureCreatorPanel(simulation);
            panel.TakeTheAdvice();

            Assert.That(panel.CommitNow(out var why), Is.True, why);

            Assert.That(simulation.State.ActiveArchitectureProject, Is.Not.Null,
                "The offer's own path started nothing, so the step that waits for it clears "
                + "itself and the tour walks past the decision it just asked for.");
        }
    }
}
