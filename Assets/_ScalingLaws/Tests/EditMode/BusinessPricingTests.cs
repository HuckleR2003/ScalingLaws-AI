using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The one price the game asks the player for is the one the company charges.
    ///
    /// **Reported as a display complaint and it is a wiring one.** The BUSINESS page opened on a
    /// price per token, and the note said a player never meets a token price anywhere else in the
    /// game: the creator asks for a monthly subscription, the release card asks for a monthly
    /// subscription, and nothing else ever mentions tokens. That is true, and the reason it is true
    /// is worse than the presentation.
    ///
    /// `MonetizationPolicy.Model` opened on `PayPerToken`, and `RatePerMillionTokensUsd` reads
    /// `SubscriptionPriceUsdPerMonth` only on a subscription. So the figure the player set when
    /// shipping their first model reached the version line, was printed back to them on the release
    /// screen as what people pay, and **did not enter the till at all**. The company charged the
    /// market rate times one, whatever the card said.
    /// </summary>
    public sealed class BusinessPricingTests
    {
        private static CompanySimulation Fresh()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 200_000_000L;
            simulation.SetRentedPetaflops(200.0);
            return simulation;
        }

        /// <summary>
        /// **This is the ratchet.** A control the player is given that moves no number in the
        /// simulation is the failure this project has now hit twelve times, and this one was on the
        /// busiest decision in the game.
        /// </summary>
        [Test]
        public void ThePriceAFreshCompanyIsAskedForReachesTheTill()
        {
            var simulation = Fresh();
            var policy = simulation.State.Monetization;
            var market = simulation.Market.PricePerMillionTokensUsd;

            policy.SubscriptionPriceUsdPerMonth = 20.0;
            var atTwenty = policy.RatePerMillionTokensUsd(market);

            policy.SubscriptionPriceUsdPerMonth = 80.0;
            var atEighty = policy.RatePerMillionTokensUsd(market);

            Assert.That(atEighty, Is.GreaterThan(atTwenty),
                "Quadrupling the subscription price changed nothing the company charges. The "
                + "creator and the release card ask for this figure and nothing else, so on a "
                + "fresh campaign the only price control in the game is decorative.");
        }

        /// <summary>
        /// The same thing said end to end, through the card the player actually presses.
        /// </summary>
        [Test]
        public void ShippingAtAPriceChargesThatPrice()
        {
            var simulation = Fresh();

            simulation.State.AddToShelf(new TrainedModel("Subject",
                ArchitectureId.DenseTransformer, 40.0, simulation.State.Date, 2e10, 40.0));

            var host = new VisualElement();
            var card = new ReleaseConfirmDialog(() => simulation, _ => { }, () => { });
            card.Show(host, 0);
            card.SetPrice(120.0);

            Assert.That(card.Release(out var why), Is.True, why);

            var market = simulation.Market.PricePerMillionTokensUsd;
            var charged = simulation.State.Monetization.RatePerMillionTokensUsd(market);

            Assert.That(charged, Is.GreaterThan(0.0),
                "The company shipped a model at $120 a month and charges nothing per token, so "
                + "the release card's price never reached the revenue calculation.");

            simulation.State.Monetization.SubscriptionPriceUsdPerMonth = 10.0;

            Assert.That(simulation.State.Monetization.RatePerMillionTokensUsd(market),
                Is.LessThan(charged),
                "Dropping the price twelvefold did not lower what the company charges.");
        }

        /// <summary>
        /// A subscription left alone becomes an expensive one, and the game notices.
        ///
        /// **This is the spine applied to the price, and it arrived with the default change.** The
        /// market rate halves about every year while a monthly fee does not move, so a company that
        /// never revisits the BUSINESS page is charging twice the market inside a year and past
        /// `ModelScandals.PricyAbove` well before that. Metered pricing tracks the market and never
        /// does this, which is the trade between the two and the reason both exist.
        ///
        /// Written as arithmetic rather than as a campaign so it says the mechanism rather than a
        /// seed: the market price on the right-hand side is simply where the decay puts it.
        /// </summary>
        [Test]
        public void ASubscriptionLeftAloneDriftsIntoOverchargingAndThatIsAStory()
        {
            var policy = new MonetizationPolicy();
            var opening = MarketModel.InitialPricePerMillionTokensUsd;

            Assert.That(policy.RelativePrice(opening), Is.EqualTo(1.0).Within(1e-9),
                "A company that has touched nothing does not open at par.");

            // Roughly where a year of decay leaves the market.
            var afterAYear = policy.RelativePrice(opening / 2.0);

            Assert.That(afterAYear, Is.EqualTo(2.0).Within(1e-9),
                "The fee is fixed and the market halved, so the company is charging twice the "
                + "market and this is the number that says so.");

            Assert.That(afterAYear, Is.GreaterThan(ModelScandals.PricyAbove),
                "Standing still no longer costs anything, so the BUSINESS page is a screen a "
                + "player never has to open twice.");

            var metered = new MonetizationPolicy { Model = PricingModel.PayPerToken };

            Assert.That(metered.RelativePrice(opening / 2.0), Is.EqualTo(1.0).Within(1e-9),
                "Metered pricing stopped following the market down, which is the only reason to "
                + "take it over a subscription.");
        }

        /// <summary>
        /// **The other three pricing models still work, and one of them has to stay reachable.**
        /// Changing the opening choice must not delete the choice.
        /// </summary>
        [Test]
        public void MeteredAndFreeStillDoWhatTheySay()
        {
            var simulation = Fresh();
            var policy = simulation.State.Monetization;
            var market = simulation.Market.PricePerMillionTokensUsd;

            policy.Model = PricingModel.PayPerToken;
            policy.PaidPriceMultiplier = 2.0;

            Assert.That(policy.RatePerMillionTokensUsd(market),
                Is.EqualTo(market * 2.0).Within(0.0001),
                "Metered pricing stopped tracking the market rate.");

            policy.Model = PricingModel.FreeOnly;

            Assert.That(policy.RatePerMillionTokensUsd(market), Is.EqualTo(0.0),
                "A company giving everything away is charging for it.");

            Assert.That(policy.FreeShareOfTokens, Is.EqualTo(1.0),
                "Free only is serving something it expects to be paid for.");
        }
    }
}
