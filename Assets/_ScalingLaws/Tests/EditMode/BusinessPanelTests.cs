using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The price is not changed by brushing a control, and the figures beside it are real.
    ///
    /// **Three complaints, one screen.** Setting a subscription price was a live slider on a page a
    /// player opens to read; the four figures that say whether the price is working were on a
    /// different screen entirely; and nothing said which product any of it was about.
    ///
    /// Driven through the methods the buttons call, because an EditMode element has no panel and a
    /// click sent to a button is never dispatched. Same shape as `ManagementScreen.ShowDesk` and
    /// `FinanceReport.ShowDays`.
    /// </summary>
    public sealed class BusinessPanelTests
    {
        private sealed class Rig
        {
            public Rig(int models = 1)
            {
                Simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
                Simulation.State.CashUsd = 400_000_000L;
                Simulation.SetRentedPetaflops(400.0);

                for (var index = 0; index < models; index++)
                {
                    // Different capability and a line of its own, so the audience split has
                    // something to divide and the two products cannot read identically.
                    Simulation.State.AddToShelf(new TrainedModel(
                        "Subject " + (index + 1), ArchitectureId.DenseTransformer,
                        40.0 + index * 9.0, Simulation.State.Date, 2e10, 40.0 + index * 9.0,
                        family: "Line " + (index + 1)));

                    Assert.That(Simulation.TryReleaseModel(0, 1.0, out var why), Is.True, why);
                }

                // One day, so the market has actually split an audience between them.
                Simulation.AdvanceDay();

                Panel = new BusinessPricingPanel(
                    () => Simulation,
                    () => Panel.Refresh(),
                    (title, body, label, onYes) =>
                    {
                        Asked++;
                        LastBody = body;
                        if (AnswerYes)
                        {
                            onYes();
                        }
                    });

                Panel.Refresh();
            }

            public CompanySimulation Simulation { get; }
            public BusinessPricingPanel Panel { get; }

            public int Asked { get; private set; }
            public string LastBody { get; private set; } = string.Empty;
            public bool AnswerYes { get; set; } = true;

            public Slider Fee() =>
                Panel.Root.Query<Slider>(className: "bizprice__slider").ToList().FirstOrDefault();

            public double Price => Simulation.State.Monetization.SubscriptionPriceUsdPerMonth;
        }

        /// <summary>
        /// **This is the ratchet.** A live price slider on a page the player opens to read is one
        /// brush of the wheel away from repricing the whole company.
        /// </summary>
        [Test]
        public void TheFeeSliderIsLockedUntilChangeIsPressed()
        {
            var rig = new Rig();

            var slider = rig.Fee();
            Assert.That(slider, Is.Not.Null, "There is no price control on the pricing panel.");

            Assert.That(slider.enabledSelf, Is.False,
                "The price slider is live the moment the page opens.");

            rig.Panel.OpenTheLock();

            Assert.That(rig.Fee().enabledSelf, Is.True,
                "CHANGE was pressed and the slider is still refusing to move.");
        }

        [Test]
        public void MovingTheSliderChargesNothingUntilItIsApplied()
        {
            var rig = new Rig();
            var before = rig.Price;

            rig.Panel.OpenTheLock();
            rig.Panel.SetPrice(before + 30.0);

            Assert.That(rig.Price, Is.EqualTo(before).Within(1e-9),
                "Dragging the slider changed what every customer pays, before the player had "
                + "agreed to anything.");

            Assert.That(rig.Panel.PendingPriceUsdPerMonth, Is.EqualTo(before + 30.0).Within(0.5),
                "The panel is not holding what the slider says, so APPLY would commit the old "
                + "figure.");

            rig.Panel.Apply();

            Assert.That(rig.Price, Is.EqualTo(before + 30.0).Within(0.5),
                "APPLY was pressed and the company is still on the old price.");
        }

        /// <summary>
        /// **The warning does not depend on the picker.** The note that asked for this expected it
        /// only on ALL MODELS; the price is one company-wide rate, so it is always the whole
        /// catalogue and a warning that appeared sometimes would say the other times were safe.
        /// </summary>
        [Test]
        public void ApplyingAsksFirstEvenWithOneProductSelected()
        {
            var rig = new Rig(2);

            rig.Panel.SelectScope(1);
            rig.Panel.OpenTheLock();
            rig.Panel.SetPrice(110.0);
            rig.Panel.Apply();

            Assert.That(rig.Asked, Is.EqualTo(1),
                "One product was picked and the price moved for the whole company without a word.");

            Assert.That(rig.LastBody, Does.Contain("110").Or.Contain("$110"),
                "The card does not say what it is about to apply: " + rig.LastBody);
        }

        [Test]
        public void SayingNoLeavesThePriceAlone()
        {
            var rig = new Rig { AnswerYes = false };
            var before = rig.Price;

            rig.Panel.OpenTheLock();
            rig.Panel.SetPrice(before + 40.0);
            rig.Panel.Apply();

            Assert.That(rig.Asked, Is.EqualTo(1), "nothing asked");

            Assert.That(rig.Price, Is.EqualTo(before).Within(1e-9),
                "The player was asked, said no, and the price moved anyway.");
        }

        [Test]
        public void BackingOutShutsTheLockAndForgetsTheFigure()
        {
            var rig = new Rig();
            var before = rig.Price;

            rig.Panel.OpenTheLock();
            rig.Panel.SetPrice(before + 50.0);
            rig.Panel.Cancel();

            Assert.That(rig.Panel.IsUnlocked, Is.False, "The lock is still open after BACK.");

            Assert.That(rig.Panel.PendingPriceUsdPerMonth, Is.EqualTo(before).Within(1e-9),
                "The abandoned figure is still loaded, so the next CHANGE opens on a price the "
                + "player already walked away from.");

            Assert.That(rig.Price, Is.EqualTo(before).Within(1e-9), "BACK charged something.");
        }

        /// <summary>
        /// The four tiles are the official page's, not a second set written beside them.
        /// </summary>
        [Test]
        public void TheFourFiguresAreOnTheScreen()
        {
            var rig = new Rig();

            Assert.That(rig.Panel.Root.Query(className: "mg-kpi").ToList(), Has.Count.EqualTo(4),
                "The page shows a price with nothing to judge it against.");
        }

        /// <summary>
        /// **The picker has to actually re-read.** A dropdown that changes a field and redraws the
        /// same numbers is the shape this project keeps finding: a control that moves nothing.
        /// </summary>
        [Test]
        public void ThePickerChangesWhichProductTheFiguresAreAbout()
        {
            var rig = new Rig(2);

            rig.Panel.SelectScope(0);
            var first = Registered(rig);

            rig.Panel.SelectScope(1);
            var second = Registered(rig);

            Assert.That(rig.Panel.Scope, Is.EqualTo(1), "The picker did not take.");

            Assert.That(second, Is.Not.EqualTo(first),
                "Both products report " + first + " registered users, so the picker is changing a "
                + "caption and re-reading the same company.");
        }

        /// <summary>A scope pointing past the end of the list falls back rather than throwing.</summary>
        [Test]
        public void PickingAProductThatIsNoLongerOnSaleFallsBackToTheCompany()
        {
            var rig = new Rig(2);
            rig.Panel.SelectScope(1);

            // What withdrawing a product does to the list while this screen is open.
            foreach (var model in rig.Simulation.State.DeployedModels.ToList())
            {
                rig.Simulation.TryRetireModel(model, out _);
            }

            Assert.DoesNotThrow(() => rig.Panel.Refresh(),
                "Withdrawing a product while the picker is on it took the screen down.");

            Assert.That(rig.Panel.Scope, Is.EqualTo(BusinessPricingPanel.EverythingOnSale),
                "The picker is still pointing at a product that is not on sale.");
        }

        private static string Registered(Rig rig)
        {
            var tiles = rig.Panel.Root.Query(className: "mg-kpi").ToList();
            Assume.That(tiles, Is.Not.Empty, "no tiles to read");

            return tiles[0].Query<Label>(className: "mg-kpi__value").ToList()
                .FirstOrDefault()?.text ?? string.Empty;
        }
    }
}
