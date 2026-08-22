using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Picking several upgrades and commissioning them together.
    ///
    /// **The old screen commissioned a programme the instant a card was clicked**, so there was
    /// nothing to test beyond one call. The basket is a decision the player builds up and can back
    /// out of, which means there is now a state that can be wrong: money spent on something never
    /// confirmed, a basket that survives a change of model, a partial failure reported as success.
    /// </summary>
    public sealed class UpgradeBasketTests
    {
        private static CompanySimulation WithALiveModel(long cash = 200_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = cash;
            simulation.SetRentedPetaflops(400.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, capability: 40.0,
                releaseDate: simulation.State.Date, activeParameterCount: 8.0,
                priceMultiplier: 1.0));

            return simulation;
        }

        private static UI.UpgradeGridPanel Screen(CompanySimulation simulation) =>
            new(simulation, (_, _) => { }, () => { });

        [Test]
        public void NothingIsCommissionedByOpeningTheScreen()
        {
            var simulation = WithALiveModel();
            var before = simulation.State.CashUsd;

            var panel = Screen(simulation);
            panel.Refresh();

            Assert.That(simulation.State.UpgradeProjects, Is.Empty,
                "Opening the screen must not commission anything.");

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before));

            Assert.That(panel.Chosen, Is.Empty,
                "A freshly opened basket is empty, so the green button has nothing to carry.");
        }

        /// <summary>
        /// The basket leaves this screen and arrives at the planner intact.
        ///
        /// **This is the join that has failed before.** The model type was chosen on one screen,
        /// passed nowhere, and every model released was general for the whole campaign with 244
        /// tests green. A basket that is built here and lost on the way to the planner would ship a
        /// version containing none of the work the player picked, and nothing would say so.
        /// </summary>
        [Test]
        public void TheBasketReachesThePlannerAndStillCommissionsNothing()
        {
            var simulation = WithALiveModel();
            var model = simulation.State.DeployedModels[0];

            var wanted = model.Traits.Standings(simulation.State.Date)
                .Where(standing => standing.IsAvailable && !standing.IsMaxed)
                .Take(3)
                .Select(standing => standing.Trait)
                .ToList();

            Assert.That(wanted, Is.Not.Empty, "This test needs something upgradeable.");

            var planner = new UI.ReleasePlanPanel(simulation, _ => { }, () => { });
            planner.Open(0, wanted);

            Assert.That(planner.ModelIndex, Is.EqualTo(0));
            Assert.That(planner.Basket, Is.EquivalentTo(wanted));

            Assert.That(planner.VersionName, Is.Not.Empty,
                "A blank name would leave SHIP disabled on a screen that offers no other way on.");

            Assert.That(simulation.State.UpgradeProjects, Is.Empty,
                "Planning a release is not commissioning it. Nothing starts until SHIP.");
        }

        /// <summary>
        /// The difference bars survive a reading that is below zero.
        ///
        /// **Brand is measured against the market, so a model behind par reads negative**, and the
        /// first version of this scaled the bar by the after value. At -25.8% that is a denominator
        /// of almost nothing, so a two point improvement drew as a full-width bar: the panel
        /// reported a total transformation for a marginal gain, on the one row where the player is
        /// most likely to be worried. Nothing failed. It was found by rendering the screen.
        /// </summary>
        [Test]
        public void ADifferenceBarStillMeansSomethingWhenTheReadingIsNegative()
        {
            var (held, gained) = UI.UpgradeGridPanel.BarWidths(-28.6, -25.8);

            Assert.That(held, Is.EqualTo(0.0).Within(1e-9),
                "There is nothing built up yet when the reading is below zero.");

            Assert.That(gained, Is.LessThan(0.2),
                "Two and a half points against a scale of twenty eight is a sliver, not a full bar.");

            Assert.That(gained, Is.GreaterThan(0.0), "And it is still an improvement.");
        }

        [Test]
        public void ADifferenceBarReadsAsMostlyBuiltWhenTheGainIsSmall()
        {
            var (held, gained) = UI.UpgradeGridPanel.BarWidths(47.8, 49.2);

            Assert.That(held, Is.GreaterThan(0.8), "Nearly all of this was already there.");
            Assert.That(gained, Is.LessThan(0.1));

            Assert.That(held + gained,
                Is.LessThanOrEqualTo(UI.UpgradeGridPanel.BarCeiling + 1e-9),
                "The two halves together must never run past the end of their own track.");
        }

        [Test]
        public void CommissioningTheBasketStartsOneProgrammePerUpgrade()
        {
            var simulation = WithALiveModel();
            var model = simulation.State.DeployedModels[0];

            var wanted = model.Traits.Standings(simulation.State.Date)
                .Where(standing => standing.IsAvailable && !standing.IsMaxed)
                .Take(3)
                .Select(standing => standing.Trait)
                .ToList();

            foreach (var trait in wanted)
            {
                simulation.TryStartUpgrade(0, trait, out var reason);
                Assert.That(reason, Is.Empty, reason);
            }

            Assert.That(simulation.State.UpgradeProjects.Count, Is.EqualTo(wanted.Count),
                "Three picked programmes have to become three programmes.");
        }

        [Test]
        public void TheCalendarIsTheLongestProgrammeNotTheSumOfThem()
        {
            var simulation = WithALiveModel();
            var model = simulation.State.DeployedModels[0];

            var standings = model.Traits.Standings(simulation.State.Date)
                .Where(standing => standing.IsAvailable && !standing.IsMaxed)
                .Take(2)
                .ToList();

            Assert.That(standings.Count, Is.EqualTo(2));

            foreach (var standing in standings)
            {
                simulation.TryStartUpgrade(0, standing.Trait, out _);
            }

            var longest = standings.Max(standing => standing.UpgradeDays);

            // The cluster runs them side by side. Their compute competes, which is what actually
            // slows them, and that is carried by the petaflop-day clock rather than the calendar.
            foreach (var project in simulation.State.UpgradeProjects)
            {
                Assert.That(project.DurationDays, Is.LessThanOrEqualTo(longest),
                    "No programme should take longer than the longest one picked.");
            }
        }

        [Test]
        public void DaysRemainingCountsDown()
        {
            var simulation = WithALiveModel();
            var model = simulation.State.DeployedModels[0];

            var standing = model.Traits.Standings(simulation.State.Date)
                .First(entry => entry.IsAvailable && !entry.IsMaxed);

            simulation.TryStartUpgrade(0, standing.Trait, out _);

            var project = simulation.State.UpgradeProjects[0];
            var before = project.DaysRemaining;

            simulation.AdvanceDay();

            Assert.That(project.DaysRemaining, Is.EqualTo(before - 1),
                "The banner counts this down, so it has to move.");
        }
    }
}
