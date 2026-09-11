using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A model goes on sale on terms the player chose, not on whatever was left over.
    ///
    /// **Reported: clicking a model on the release screen shipped it, on the spot.** Two hundred days
    /// of training and most of the company's cash went on sale in one click, at whatever price the
    /// company happened to be charging, with nothing in between. The tutorial tells the player at
    /// step 44 to click it and set something, and there was nothing to set.
    ///
    /// The card is driven through `Release`, which is what its button calls: an EditMode element has
    /// no panel, so a click sent to a button is never dispatched.
    /// </summary>
    public sealed class ReleaseConfirmTests
    {
        private static CompanySimulation WithAShelvedModel()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 200_000_000L;
            simulation.SetRentedPetaflops(200.0);

            simulation.State.AddToShelf(new TrainedModel("Subject",
                ArchitectureId.DenseTransformer, 40.0, simulation.State.Date, 2e10, 40.0));

            return simulation;
        }

        private static (ReleaseConfirmDialog Card, VisualElement Host) Open(
            CompanySimulation simulation)
        {
            var host = new VisualElement();
            var card = new ReleaseConfirmDialog(() => simulation, _ => { }, () => { });
            card.Show(host, 0);
            return (card, host);
        }

        /// <summary>
        /// **This is the ratchet.** Opening the card must not be the same event as shipping.
        /// </summary>
        [Test]
        public void OpeningTheCardDoesNotPutTheModelOnSale()
        {
            var simulation = WithAShelvedModel();

            Open(simulation);

            Assert.That(simulation.State.Shelf, Has.Count.EqualTo(1),
                "The model left the shelf just for being looked at.");

            Assert.That(simulation.MarketedModels(), Is.Empty,
                "It is already on sale and the player has not agreed to anything yet.");
        }

        [Test]
        public void TheCardCarriesTheThreeDecisionsAndAPictureOfTheModel()
        {
            var simulation = WithAShelvedModel();
            var (card, host) = Open(simulation);

            Assert.That(host.Q(className: "die--release"), Is.Not.Null,
                "No silicon plate. The tile it came from was a title and two grey lines, which is "
                + "the whole reason this screen was rebuilt.");

            // A fresh company gives nothing away, so the card opens with the tier off and one
            // slider. **That is the design rather than an omission**: "how generous" is a question
            // that only exists once the answer to "at all?" is yes, and a slider sitting at zero
            // says neither.
            Assert.That(host.Query<Slider>(className: "relship__slider").ToList(),
                Has.Count.EqualTo(1),
                "The free tier is off and its size is being asked for anyway.");

            Assert.That(host.Q<Button>(className: "relship__toggle"), Is.Not.Null,
                "Nothing switches the free tier on, which is one of the three things the report "
                + "asked to be settable.");

            card.SetFreeTier(true);

            Assert.That(host.Query<Slider>(className: "relship__slider").ToList(),
                Has.Count.EqualTo(2),
                "The tier is on and there is no control for how much of one.");
        }

        [Test]
        public void ShippingPutsItOnSaleOnTheTermsTheCardShows()
        {
            var simulation = WithAShelvedModel();
            var (card, _) = Open(simulation);

            Assert.That(card.Release(out var why), Is.True, why);

            Assert.That(simulation.State.Shelf, Is.Empty, "the model is still on the shelf");
            Assert.That(simulation.MarketedModels(), Has.Count.EqualTo(1));

            Assert.That(simulation.State.Monetization.SubscriptionPriceUsdPerMonth,
                Is.EqualTo(card.PriceUsdPerMonth).Within(0.001),
                "The company is charging something other than what the card said it would.");
        }

        /// <summary>
        /// The version line is seeded inside `TryReleaseModel` from the policy, so the policy has to
        /// be written first. Setting it afterwards would put the model on sale at the old figure and
        /// then change what everybody pays a frame later.
        /// </summary>
        [Test]
        public void TheModelsOpeningPriceIsTheOneOnTheCard()
        {
            var simulation = WithAShelvedModel();
            simulation.State.Monetization.SubscriptionPriceUsdPerMonth = 19.0;

            var host = new VisualElement();
            var card = new ReleaseConfirmDialog(() => simulation, _ => { }, () => { });
            card.Show(host, 0);

            // What the slider calls. An EditMode element has no panel, so setting `value` sends a
            // change event nothing dispatches and the callback behind the control never runs.
            card.SetPrice(47.0);

            Assert.That(card.Release(out var why), Is.True, why);

            var model = simulation.MarketedModels().Single().Model;

            Assert.That(model.Line.EffectivePriceUsdPerMonth(), Is.EqualTo(47.0).Within(0.5),
                "The model shipped at " + model.Line.EffectivePriceUsdPerMonth()
                + " against the " + card.PriceUsdPerMonth + " on the card, so the price was "
                + "applied after the release rather than before it.");
        }

        /// <summary>
        /// Off means nothing given away, not a slider left at its own minimum. Those read the same on
        /// screen and are different facts in the market.
        /// </summary>
        [Test]
        public void SwitchingTheFreeTierOffGivesNothingAway()
        {
            var simulation = WithAShelvedModel();
            simulation.State.Monetization.FreeTierTokensPerUserPerDay = 5000.0;

            var host = new VisualElement();
            var card = new ReleaseConfirmDialog(() => simulation, _ => { }, () => { });
            card.Show(host, 0);

            Assume.That(card.FreeTokensPerDay, Is.GreaterThan(0.0),
                "the card opened with the free tier already off, so this measures nothing");

            Assert.That(host.Q<Button>(className: "relship__toggle"), Is.Not.Null,
                "there is no switch to press");

            // What the switch calls. A `Button.clicked` event cannot be raised from a test and an
            // EditMode element has no panel to dispatch a click through.
            card.SetFreeTier(false);

            Assert.That(card.FreeTokensPerDay, Is.EqualTo(0.0),
                "The switch is off and the card still intends to give tokens away.");

            Assert.That(card.Release(out var why), Is.True, why);

            Assert.That(simulation.State.Monetization.FreeTierTokensPerUserPerDay,
                Is.EqualTo(0.0),
                "The company shipped with a free tier the player switched off.");
        }
    }
}
