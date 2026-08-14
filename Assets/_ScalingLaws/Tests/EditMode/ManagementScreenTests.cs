using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The official page and the management desk.
    ///
    /// **This fixture exists because of a documented failure in this project**: a mechanism complete
    /// in `Simulation/` with no working control in `UI/` passes every test and is unreachable, and
    /// that shipped twice. These tests build the real screen, click the real tabs and read the text
    /// that actually lands in the tree, so a screen that throws or renders empty fails here rather
    /// than in a screenshot.
    ///
    /// The other claim they hold is that the page reports rather than computes. Every figure on it
    /// has to come out of the simulation, because a dashboard with its own arithmetic is a second
    /// simulation with a prettier font, and the two will disagree eventually.
    ///
    /// **What this fixture cannot reach.** An EditMode test has no panel, so a click sent to a
    /// button is never dispatched. The tabs are asserted to exist and the switch behind them is
    /// driven through `ShowDesk`, which is the same method the tabs call. The one link left uncovered
    /// is therefore the two lambdas in `BuildTabs`, both visible on one line each. The screen is
    /// reached from the corner banner rather than from the bottom bar, so the PlayMode sweep over the
    /// category slots does not walk it either.
    /// </summary>
    public sealed class ManagementScreenTests
    {
        private static ManagementScreen Screen(CompanySimulation simulation) =>
            new(simulation, () => { }, () => { }, () => { }, () => { });

        private static CompanySimulation Selling(uint seed = 404, int days = 40)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(80.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Atlas One", ArchitectureId.DenseTransformer, 48.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        /// <summary>Every piece of text in the tree, so a test can ask what the player would read.</summary>
        private static List<string> Words(VisualElement root)
        {
            var found = new List<string>();

            void Walk(VisualElement element)
            {
                if (element is Label label && !string.IsNullOrEmpty(label.text))
                {
                    found.Add(label.text);
                }

                if (element is Button button && !string.IsNullOrEmpty(button.text))
                {
                    found.Add(button.text);
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

        private static Button ButtonSaying(VisualElement root, string text)
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

        // ---- the empty case, which is the one a new player sees first ---------------------------

        [Test]
        public void WithNothingReleasedThePageSaysSoAndOffersTheWayOut()
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", 12));
            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsTrue(Says(screen.Root, "NOTHING ON SALE"),
                "A company with no product has to be told that, not shown a page of zeroes.");

            Assert.IsNotNull(ButtonSaying(screen.Root, "GO TO RELEASE"),
                "A dead end with no way out of it is the bug that made commercialise unreachable.");
        }

        [Test]
        public void TheEmptyPageDoesNotPretendThereIsNothingToShipWhenSomethingIsOnTheShelf()
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", 13));
            simulation.SetRentedPetaflops(40.0);

            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsTrue(Says(screen.Root, "No model has been released"),
                "With an empty shelf the page should say the run has not happened, not that a "
                + "release is waiting.");
        }

        // ---- the page ----------------------------------------------------------------------------

        [Test]
        public void TheHeroNamesTheModelTheSimulationIsActuallySelling()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            var flagship = simulation.Flagship();
            Assert.IsNotNull(flagship, "The setup released a model, so something has to be on sale.");

            Assert.IsTrue(Says(screen.Root, flagship.Name.ToUpperInvariant()),
                $"The page has to name {flagship.Name}. A page that names a different model from the "
                + "banner is two answers to one question.");
        }

        [Test]
        public void TheStatusLineRepeatsWhatTheFleetMeasuredRatherThanAnOpinion()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsTrue(Says(screen.Root, simulation.State.LastQuality.Headline),
                "The public status line is yesterday's measured load. If it can disagree with the "
                + "fleet then it is decoration.");
        }

        [Test]
        public void ThePriceOnThePageIsThePriceTheCompanyCharges()
        {
            var simulation = Selling();
            simulation.State.Monetization.SubscriptionPriceUsdPerMonth = 42.0;

            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsTrue(Says(screen.Root, "42"),
                "A plan card showing a number the billing does not use is worse than no plan card.");
        }

        [Test]
        public void EveryReviewIsAboutSomethingTheSimulationKnows()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            foreach (var heading in new[] { "On the speed", "On the price", "On how current it is" })
            {
                Assert.IsTrue(Says(screen.Root, heading),
                    $"'{heading}' is missing. Each review answers 'why is my satisfaction that "
                    + "number', so dropping one takes an explanation away rather than a decoration.");
            }

            Assert.IsTrue(Says(screen.Root, simulation.State.LastStandingChange.Headline),
                "The overall review is the standing's own largest mover, in its own words.");
        }

        // ---- the desk ------------------------------------------------------------------------------

        [Test]
        public void TheManagementTabOpensADifferentScreenFromTheOfficialPage()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsTrue(Says(screen.Root, "WHAT PEOPLE SAY"));
            Assert.IsFalse(Says(screen.Root, "WHO IS USING IT"),
                "Both halves at once is the wall of numbers the tabs exist to avoid.");

            Assert.IsNotNull(ButtonSaying(screen.Root, "MANAGEMENT"),
                "There is no way to reach the second half.");

            screen.ShowDesk(true);

            Assert.IsTrue(Says(screen.Root, "WHO IS USING IT"),
                "Opening the desk has to actually change the screen.");

            Assert.IsFalse(Says(screen.Root, "WHAT PEOPLE SAY"));
        }

        [Test]
        public void TheDeskCountsTheSamePeopleTheMarketDoes()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsNotNull(ButtonSaying(screen.Root, "MANAGEMENT"), "No tab to reach the desk.");
            screen.ShowDesk(true);

            var expected = UiFormat.Count(simulation.Product().Subscribers);

            Assert.IsTrue(Says(screen.Root, expected),
                $"The desk should report {expected} registered, which is what the market holds. A "
                + "dashboard that recounts the users is a second simulation.");
        }

        [Test]
        public void EveryAudienceGetsARowAndTheLeaderIsNamed()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsNotNull(ButtonSaying(screen.Root, "MANAGEMENT"), "No tab to reach the desk.");
            screen.ShowDesk(true);

            foreach (var audience in AudienceCatalog.All)
            {
                Assert.IsTrue(Says(screen.Root, audience.DisplayName),
                    $"{audience.DisplayName} has no row. An overall share hides exactly the case the "
                    + "segmented market was built to expose: nowhere at all in the audience that is "
                    + "about to be the largest one.");
            }

            var standings = simulation.SegmentStandings();
            var named = false;

            foreach (var standing in standings)
            {
                var leader = standing.LeaderIndex == 0 ? "you" : standing.LeaderName;
                named |= Says(screen.Root, leader);
            }

            Assert.IsTrue(named, "No leader is named anywhere, so the table cannot say who to chase.");
        }

        [Test]
        public void TheFieldIsOrderedAndTheCompanyIsInIt()
        {
            var simulation = Selling();
            var screen = Screen(simulation);
            screen.Refresh();

            Assert.IsNotNull(ButtonSaying(screen.Root, "MANAGEMENT"), "No tab to reach the desk.");
            screen.ShowDesk(true);

            Assert.IsTrue(Says(screen.Root, "(you)"),
                "Listing the rivals without the player is a ranking the player is not in.");
        }

        /// <summary>
        /// The screen has to survive the states a real campaign passes through, not only the tidy one
        /// the other tests build. A page that throws renders nothing, which looks exactly like a hang.
        /// </summary>
        [Test]
        public void ItBuildsOnDayOneAndAfterADecade()
        {
            var young = new CompanySimulation(new CompanyState("Adco", 77));
            Assert.DoesNotThrow(() => Screen(young).Refresh(), "Day one throws.");

            var old = Selling(78, 3650);
            var screen = Screen(old);
            Assert.DoesNotThrow(() => screen.Refresh(), "Ten years in throws.");

            Assert.IsNotNull(ButtonSaying(screen.Root, "MANAGEMENT"));
            Assert.DoesNotThrow(() => screen.ShowDesk(true), "The desk throws after ten years.");
        }
    }
}
