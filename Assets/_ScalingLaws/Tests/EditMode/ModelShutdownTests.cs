using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Taking a model off sale, and the archive that lists what has been taken off.
    ///
    /// **Until this went in, the only thing in the game that could retire a model was a safety
    /// incident.** A player could put a weak line on the market and had no way at all to withdraw
    /// it, which is another mechanism that existed in the simulation and had no control anywhere.
    /// </summary>
    public sealed class ModelShutdownTests
    {
        private static CompanySimulation Fresh(uint seed = 800)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(90.0);
            return simulation;
        }

        private static DeployedModel Ship(CompanySimulation simulation, string name, double capability,
            ModelType type = ModelType.General, string family = "")
        {
            var model = new DeployedModel(name, ArchitectureId.DenseTransformer, capability,
                simulation.State.Date, 2e10, 1.0, type, family);

            simulation.State.AddDeployedModel(model);
            return model;
        }

        private static ManagementScreen Screen(CompanySimulation simulation) =>
            new(simulation, () => { }, () => { }, () => { }, () => { });

        private static List<string> Words(VisualElement root)
        {
            var found = new List<string>();

            void Walk(VisualElement element)
            {
                switch (element)
                {
                    case Label label when !string.IsNullOrEmpty(label.text):
                        found.Add(label.text);
                        break;
                    case Button button when !string.IsNullOrEmpty(button.text):
                        found.Add(button.text);
                        break;
                }

                foreach (var child in element.Children())
                {
                    Walk(child);
                }
            }

            Walk(root);
            return found;
        }

        private static bool Says(VisualElement root, string fragment) =>
            Words(root).Exists(text => text.Contains(fragment));

        // ---- the mechanism -----------------------------------------------------------------------

        [Test]
        public void AModelCanBeTakenOffSale()
        {
            var simulation = Fresh();
            var model = Ship(simulation, "Atlas One", 42.0);

            for (var day = 0; day < 60; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.IsTrue(simulation.TryRetireModel(model, out var reason), reason);
            Assert.IsTrue(model.IsRetired);
            Assert.IsFalse(model.IsLiveOn(simulation.State.Date));
            Assert.AreEqual(simulation.State.Date.DayIndex, model.RetiredOn.DayIndex);
        }

        [Test]
        public void AWithdrawnModelStopsEarningAndStopsCompeting()
        {
            var simulation = Fresh(801);
            var model = Ship(simulation, "Atlas One", 44.0);

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            var earnedWhileSelling = model.LifetimeRevenueUsd;
            Assert.Greater(earnedWhileSelling, 0L, "The setup has to sell something first.");

            simulation.TryRetireModel(model, out _);

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.AreEqual(earnedWhileSelling, model.LifetimeRevenueUsd,
                "A withdrawn product cannot go on taking money.");

            Assert.IsNull(simulation.Flagship(),
                "With its only model withdrawn the company is selling nothing.");
        }

        [Test]
        public void WithdrawingIsNotReversible()
        {
            var simulation = Fresh(802);
            var model = Ship(simulation, "Atlas One", 40.0);
            simulation.AdvanceDay();

            Assert.IsTrue(simulation.TryRetireModel(model, out _));
            Assert.IsFalse(simulation.TryRetireModel(model, out var reason),
                "Withdrawing twice has to be refused, not silently repeated.");

            Assert.IsTrue(reason.Contains("already off sale"));
        }

        /// <summary>
        /// An upgrade programme is work being paid for. Letting the product it improves vanish
        /// underneath it would leave the programme running against nothing.
        /// </summary>
        [Test]
        public void AModelWithAnUpgradeRunningCannotBeWithdrawn()
        {
            var simulation = Fresh(803);
            var model = Ship(simulation, "Atlas One", 40.0);

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            // Whichever trait this company can actually start, rather than a named one: most are
            // gated behind research a day-thirty company has not done, and an Assert.Ignore here
            // would leave the rule with no coverage at all while still reading green.
            var started = false;
            var lastWhy = "no traits at all";

            foreach (ModelTrait trait in System.Enum.GetValues(typeof(ModelTrait)))
            {
                if (simulation.TryStartUpgrade(0, trait, out lastWhy))
                {
                    started = true;
                    break;
                }
            }

            Assert.IsTrue(started,
                $"No upgrade could be started, so the guard below is untested. Last refusal: {lastWhy}");

            Assert.IsFalse(simulation.TryRetireModel(model, out var reason));
            Assert.IsTrue(reason.Contains("upgrade"), reason);
            Assert.IsFalse(model.IsRetired);
        }

        [Test]
        public void WithdrawingIsPrintedInTheNews()
        {
            var simulation = Fresh(804);
            var model = Ship(simulation, "Atlas One", 40.0);

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            simulation.TryRetireModel(model, out _);

            Assert.IsTrue(simulation.State.News.In(NewsSection.Premieres, 6)
                    .Exists(story => story.Headline.Contains("finishes a run")
                        || story.Body.Contains("withdrawn")),
                "A product leaving the market is news, the same as one arriving.");
        }

        [Test]
        public void TheWithdrawalDateSurvivesASave()
        {
            var simulation = Fresh(805);
            var model = Ship(simulation, "Atlas One", 40.0);

            for (var day = 0; day < 25; day++)
            {
                simulation.AdvanceDay();
            }

            simulation.TryRetireModel(model, out _);
            var when = model.RetiredOn.DayIndex;

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(1, restored.DeployedModels.Count,
                "A withdrawn model stays in the list. Removing it would delete the history.");

            Assert.IsTrue(restored.DeployedModels[0].IsRetired);
            Assert.AreEqual(when, restored.DeployedModels[0].RetiredOn.DayIndex);
        }

        // ---- the archive --------------------------------------------------------------------------

        [Test]
        public void TheArchiveDistinguishesOnSaleFromSupersededFromRetired()
        {
            var simulation = Fresh(806);

            var old = Ship(simulation, "Atlas One", 30.0, ModelType.General, "Atlas");
            Ship(simulation, "Atlas Two", 55.0, ModelType.General, "Atlas");
            var gone = Ship(simulation, "Sonnet One", 35.0, ModelType.Coding, "Sonnet");

            simulation.AdvanceDay();
            simulation.TryRetireModel(gone, out _);

            var words = new Dictionary<string, string>();
            foreach (var record in simulation.ModelHistory())
            {
                words[record.Model.Name] = record.StateWord;
            }

            Assert.AreEqual("ON SALE", words["Atlas Two"]);
            Assert.AreEqual("SUPERSEDED", words[old.Name],
                "One line is one product. A player shown a list of live models would otherwise "
                + "expect all of them to be earning and be wrong about most of them.");

            Assert.AreEqual("RETIRED", words["Sonnet One"]);
        }

        [Test]
        public void TheArchiveTabListsEveryModelWithItsEarnings()
        {
            var simulation = Fresh(807);
            Ship(simulation, "Atlas One", 44.0, ModelType.General, "Atlas");

            for (var day = 0; day < 90; day++)
            {
                simulation.AdvanceDay();
            }

            var screen = Screen(simulation);
            screen.ShowArchive();

            Assert.IsTrue(Says(screen.Root, "ATLAS ONE"), "The model is not in its own archive.");
            Assert.IsTrue(Says(screen.Root, "EARNED"), "No earnings column.");
            Assert.IsTrue(Says(screen.Root, "SHIPPED"), "No count of what has been shipped.");
        }

        [Test]
        public void TheArchiveOpensWithNothingShippedAndSaysSo()
        {
            var screen = Screen(Fresh(808));
            Assert.DoesNotThrow(() => screen.ShowArchive());

            Assert.IsTrue(Says(screen.Root, "NOTHING SHIPPED YET"),
                "The archive is the one tab worth opening with nothing on sale, so it has to survive "
                + "that and say something useful.");
        }

        /// <summary>
        /// The control the author asked for: red shutdown on the left, amber upgrade in the middle,
        /// the live figure on the right, and shutdown only present while there is something to shut.
        /// </summary>
        [Test]
        public void TheControlBarOffersShutdownOnlyWhileTheModelIsOnSale()
        {
            var simulation = Fresh(809);
            var model = Ship(simulation, "Atlas One", 44.0);

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var screen = Screen(simulation);
            screen.ShowArchive();

            Assert.IsTrue(Says(screen.Root, "SHUTDOWN"), "No way to take it off sale.");
            Assert.IsTrue(Says(screen.Root, "UPGRADE"));
            Assert.IsTrue(Says(screen.Root, "ACTIVE"), "The bar has to say who is on it.");

            simulation.TryRetireModel(model, out _);
            screen.ShowArchive();

            Assert.IsFalse(Says(screen.Root, "SHUTDOWN"),
                "A withdrawn model offers nothing. The decision has been taken and there is no "
                + "putting it back.");

            Assert.IsTrue(Says(screen.Root, "WITHDRAWN"));
        }

        /// <summary>
        /// Two clicks, because the control sits next to UPGRADE and the action cannot be undone.
        /// </summary>
        [Test]
        public void ShuttingDownTakesTwoClicks()
        {
            var simulation = Fresh(810);
            var model = Ship(simulation, "Atlas One", 44.0);

            for (var day = 0; day < 20; day++)
            {
                simulation.AdvanceDay();
            }

            var screen = Screen(simulation);
            screen.ShowArchive();

            var stop = FindButton(screen.Root, "SHUTDOWN");
            Assert.IsNotNull(stop);

            InvokeShutdown(screen, model);
            Assert.IsFalse(model.IsRetired, "One click must only arm it.");
            Assert.IsTrue(Says(screen.Root, "CONFIRM SHUTDOWN"), "And it has to say that it has.");

            InvokeShutdown(screen, model);
            Assert.IsTrue(model.IsRetired, "The second click commits.");
        }

        /// <summary>
        /// An EditMode test has no panel, so a click sent to a button is never dispatched. The bar is
        /// asserted to exist above; the two step behaviour behind it is driven through the same
        /// method the button calls.
        /// </summary>
        private static void InvokeShutdown(ManagementScreen screen, DeployedModel model) =>
            screen.RequestShutdown(model);

        private static Button FindButton(VisualElement root, string text)
        {
            Button found = null;

            void Walk(VisualElement element)
            {
                if (element is Button button && button.text == text)
                {
                    found ??= button;
                }

                foreach (var child in element.Children())
                {
                    Walk(child);
                }
            }

            Walk(root);
            return found;
        }

        // ---- the corner stack ------------------------------------------------------------------------

        [Test]
        public void EveryProductOnSaleGetsItsOwnEntryAndSupersededOnesDoNot()
        {
            var simulation = Fresh(811);

            Ship(simulation, "Atlas One", 30.0, ModelType.General, "Atlas");
            Ship(simulation, "Atlas Two", 55.0, ModelType.General, "Atlas");
            Ship(simulation, "Sonnet One", 40.0, ModelType.Coding, "Sonnet");

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            var marketed = simulation.MarketedModels();

            Assert.AreEqual(2, marketed.Count,
                "Two lines are two products. The third model is superseded inside its own line and "
                + "is not on sale, so it does not get a banner.");

            Assert.AreEqual("Atlas Two", marketed[0].Model.Name, "Strongest first.");
        }

        [Test]
        public void AProductBannerReportsItsOwnModelRatherThanTheCompany()
        {
            var simulation = Fresh(812);
            Ship(simulation, "Atlas One", 46.0, ModelType.General, "Atlas");
            Ship(simulation, "Sonnet One", 40.0, ModelType.Coding, "Sonnet");

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            var marketed = simulation.MarketedModels();
            Assert.AreEqual(2, marketed.Count);

            var first = simulation.ProductFor(marketed[0]);
            var second = simulation.ProductFor(marketed[1]);

            Assert.AreEqual(marketed[0].Model.Name, first.Name);
            Assert.AreEqual(marketed[1].Model.Name, second.Name);

            Assert.AreNotEqual(first.MonthEarningsUsd, second.MonthEarningsUsd,
                "Two banners printing the same company-wide figure would be the same number twice "
                + "with different names on it.");
        }
    }
}
