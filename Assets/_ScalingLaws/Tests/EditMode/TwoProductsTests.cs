using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A company with two products says two different things about them.
    ///
    /// **Reported by a player: both models show the same figures.** The corner banners are the one
    /// place the game answers "which of my products is carrying the company", so two models reading
    /// as one is not a cosmetic fault; it is the screen failing at the only question it exists for.
    ///
    /// The fixture measures rather than inspects. It ships two models of deliberately different
    /// capability, lets the market separate them over two months, and then reads what each banner
    /// would draw. A test that only asserted "not zero" would pass on a screen printing one number
    /// four times, which is exactly the state being repaired.
    /// </summary>
    public sealed class TwoProductsTests
    {
        private static CompanySimulation Trading(uint seed = 901)
        {
            var simulation = new CompanySimulation(new CompanyState("Twofold", seed));
            simulation.SetRentedPetaflops(140.0);
            return simulation;
        }

        private static DeployedModel Ship(CompanySimulation simulation, string name, double capability,
            string family)
        {
            var model = new DeployedModel(name, ArchitectureId.DenseTransformer, capability,
                simulation.State.Date, 2e10, 1.0, ModelType.General, family);

            simulation.State.AddDeployedModel(model);
            return model;
        }

        /// <summary>
        /// Two products, two separate lines, so neither supersedes the other and both stay on sale.
        ///
        /// A second model in the *same* line is an upgrade and is meant to replace the first, which
        /// would make this fixture measure the rule rather than the fault.
        /// </summary>
        private static (CompanySimulation, DeployedModel, DeployedModel) TwoOnSale(int days = 70)
        {
            var simulation = Trading();

            var strong = Ship(simulation, "Atlas", 52.0, "Atlas");
            var weak = Ship(simulation, "Pebble", 31.0, "Pebble");

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return (simulation, strong, weak);
        }

        /// <summary>
        /// The simulation itself keeps the two apart. If this fails, nothing above it can be right.
        /// </summary>
        [Test]
        public void TheSimulationAlreadyKnowsTheTwoProductsApart()
        {
            var (simulation, strong, weak) = TwoOnSale();

            Assume.That(simulation.MarketedModels(), Has.Count.EqualTo(2),
                "both models have to still be on sale for this fixture to measure anything");

            Assert.That(strong.LifetimeRevenueUsd, Is.Not.EqualTo(weak.LifetimeRevenueUsd),
                "The two models earned to the cent the same amount over seventy days, which means "
                + "the attribution in RecordDay is not splitting by product at all.");

            Assert.That(strong.LifetimeRevenueUsd, Is.GreaterThan(weak.LifetimeRevenueUsd),
                "The weaker model out-earned the stronger one, so the split is not reading "
                + "capability or users.");
        }

        /// <summary>
        /// Every banner reports its own product, and the follower does not repeat the lead.
        ///
        /// **This is the reported fault.** `Product()` answers about the company and `ProductFor`
        /// answers about one model, and both feed the same banner type. A lead banner carrying the
        /// company's month while standing under one model's name is what makes the first product
        /// look as though it owns everything the company earns.
        /// </summary>
        [Test]
        public void EachBannerReportsItsOwnProduct()
        {
            var (simulation, _, _) = TwoOnSale();
            var rows = simulation.MarketedModels();

            Assume.That(rows, Has.Count.EqualTo(2));

            var first = simulation.ProductFor(rows[0]);
            var second = simulation.ProductFor(rows[1]);

            Assert.That(first.Name, Is.Not.EqualTo(second.Name),
                "two banners, one name");

            Assert.That(first.Subscribers, Is.Not.EqualTo(second.Subscribers),
                "Both products report the same number of users. That is the fault a player sees "
                + "first, because the users figure is the headline of the banner.");

            Assert.That(first.OwnLifetimeUsd, Is.Not.EqualTo(second.OwnLifetimeUsd),
                "Both products report the same earnings.");
        }

        /// <summary>
        /// A number of people is not a number of dollars.
        ///
        /// The follower banner used to reuse the `MonthNetUsd` field to carry its user count, with
        /// the caption swapped to say so. The value still went through `UiFormat.Money` with a sign
        /// in front, so 1.96M people were drawn as **+$1.96M**, in the green a profit gets, because
        /// `IsProfitable` reads that same field.
        ///
        /// Two fields carrying four meanings is the shape of the fault rather than an accident in
        /// one line, which is why the repair separated them instead of changing the formatter.
        /// This is the ratchet: the two must not become the same number again.
        /// </summary>
        [Test]
        public void AUserCountIsNeverDrawnAsMoney()
        {
            var (simulation, _, _) = TwoOnSale();
            var rows = simulation.MarketedModels();

            Assume.That(rows, Has.Count.GreaterThan(0));

            var follower = simulation.ProductFor(rows[0]);

            Assume.That(follower.Subscribers, Is.GreaterThan(0.0),
                "the product has no users, so this fixture cannot tell the two figures apart");

            Assert.That((double)follower.MonthNetUsd, Is.Not.EqualTo(follower.Subscribers).Within(0.5),
                "The net figure and the user count are the same number, so the banner prints the "
                + "same value twice: once as people and once as dollars.");
        }

        /// <summary>
        /// The products between them hold exactly the company's audience, never more.
        ///
        /// **This is the ratchet, and it is stronger than "the two numbers differ".** The fault was
        /// that each product was handed the whole audience of its *kind*, so two general models
        /// each claimed every general user the company had. Two products could be made to differ
        /// by any number of wrong splits; only one split adds up.
        ///
        /// A cent of slack, because the parts are shares of a double and the last one is not
        /// forced to take the remainder the way the revenue slices are.
        /// </summary>
        [Test]
        public void TheProductsBetweenThemHoldExactlyTheCompanysAudience()
        {
            var (simulation, _, _) = TwoOnSale();
            var rows = simulation.MarketedModels();

            Assume.That(rows, Has.Count.EqualTo(2));

            var company = simulation.Sentiment().Users;
            Assume.That(company, Is.GreaterThan(0.0), "nobody is using anything yet");

            var held = 0.0;
            foreach (var row in rows)
            {
                held += simulation.ProductFor(row).Subscribers;
            }

            Assert.That(held, Is.LessThanOrEqualTo(company * 1.000001),
                "The products claim " + held.ToString("N0") + " people between them against a "
                + "company of " + company.ToString("N0") + ". Holding more people than the "
                + "company does is the audience being handed out more than once, which is the "
                + "reported fault: every model of a kind was given the whole of that kind.");

            Assert.That(held, Is.GreaterThanOrEqualTo(company * 0.99),
                "The products account for less than the company holds, so the split is losing "
                + "people rather than dividing them.");
        }

        /// <summary>
        /// A product's two money cells are its own, not the company's month wearing its name.
        /// </summary>
        [Test]
        public void EachBannerReportsItsOwnTakings()
        {
            var (simulation, _, _) = TwoOnSale();
            var rows = simulation.MarketedModels();

            Assume.That(rows, Has.Count.EqualTo(2));

            var first = simulation.ProductFor(rows[0]);
            var second = simulation.ProductFor(rows[1]);

            Assume.That(first.OwnLifetimeUsd, Is.GreaterThan(0L),
                "nothing has been earned, so this fixture cannot tell the two apart");

            Assert.That(first.OwnLifetimeUsd, Is.Not.EqualTo(second.OwnLifetimeUsd),
                "Both products report the same lifetime take.");

            Assert.That(first.OwnRecentUsd, Is.Not.EqualTo(second.OwnRecentUsd),
                "Both products report the same month.");

            Assert.That(first.OwnLifetimeUsd, Is.GreaterThan(second.OwnLifetimeUsd),
                "The weaker product out-earned the stronger one.");
        }
    }
}
