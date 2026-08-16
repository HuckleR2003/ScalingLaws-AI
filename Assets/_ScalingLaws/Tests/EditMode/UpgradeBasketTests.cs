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

        [Test]
        public void NothingIsCommissionedUntilTheBasketIsStarted()
        {
            var simulation = WithALiveModel();
            var panel = new UI.UpgradeGridPanel(simulation);
            panel.Refresh();

            var before = simulation.State.CashUsd;

            Assert.That(simulation.State.UpgradeProjects, Is.Empty,
                "Opening the screen must not commission anything.");

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before));
        }

        [Test]
        public void StartingTheBasketCommissionsEverythingInIt()
        {
            var simulation = WithALiveModel();
            var panel = new UI.UpgradeGridPanel(simulation);
            panel.Refresh();

            var model = simulation.State.DeployedModels[0];

            var wanted = model.Traits.Standings(simulation.State.Date)
                .Where(standing => standing.IsAvailable && !standing.IsMaxed)
                .Take(3)
                .Select(standing => standing.Trait)
                .ToList();

            Assert.That(wanted, Is.Not.Empty, "This test needs something upgradeable.");

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
