using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Improving a model before it ships.
    ///
    /// **A finished run waiting to be released is when a real lab does its post-training work**, and
    /// until now it was the one state the upgrade screen could not see: traits were conjured at the
    /// moment of release and did not exist before it, so a company whose only model was on the shelf
    /// opened UPGRADE and found an empty screen.
    ///
    /// The tests that matter here are the two that could silently lose the player money: work that
    /// does not reach the released model, and work that does not survive a save.
    /// </summary>
    public sealed class ShelfUpgradeTests
    {
        private static CompanySimulation WithAShelvedModel(long cash = 200_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = cash;
            simulation.SetRentedPetaflops(400.0);

            simulation.State.AddToShelf(new TrainedModel(
                "Subject", ArchitectureId.DenseTransformer, 40.0, simulation.State.Date,
                8.0, 40.0));

            return simulation;
        }

        private static ModelTrait FirstUpgradeable(CompanySimulation simulation) =>
            simulation.State.Shelf[0].Traits.Standings(simulation.State.Date)
                .First(standing => standing.IsAvailable && !standing.IsMaxed)
                .Trait;

        [Test]
        public void AShelvedModelHasTraitsToWorkOn()
        {
            var simulation = WithAShelvedModel();

            Assert.That(simulation.State.Shelf[0].Traits, Is.Not.Null,
                "Without a trait set there is nothing for the screen to show.");

            Assert.That(simulation.State.Shelf[0].Traits.Standings(simulation.State.Date),
                Is.Not.Empty);
        }

        [Test]
        public void WorkCanBeCommissionedBeforeRelease()
        {
            var simulation = WithAShelvedModel();
            var trait = FirstUpgradeable(simulation);

            Assert.That(simulation.TryStartUpgrade(0, trait, out var reason, onShelf: true),
                Is.True, reason);

            Assert.That(simulation.State.UpgradeProjects.Single().OnShelf, Is.True);
        }

        [Test]
        public void AFinishedProgrammeReachesTheShelvedModel()
        {
            var simulation = WithAShelvedModel();
            var trait = FirstUpgradeable(simulation);

            var before = simulation.State.Shelf[0].Traits.GetLevel(trait);
            simulation.TryStartUpgrade(0, trait, out _, onShelf: true);

            for (var day = 0; day < 400 && simulation.State.UpgradeProjects.Count > 0; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.UpgradeProjects, Is.Empty, "The programme never finished.");
            Assert.That(simulation.State.Shelf[0].Traits.GetLevel(trait), Is.EqualTo(before + 1),
                "The work finished and did not land on the model that was paid for.");
        }

        [Test]
        public void TheWorkShipsWithTheModel()
        {
            var simulation = WithAShelvedModel();
            var trait = FirstUpgradeable(simulation);

            simulation.TryStartUpgrade(0, trait, out _, onShelf: true);

            for (var day = 0; day < 400 && simulation.State.UpgradeProjects.Count > 0; day++)
            {
                simulation.AdvanceDay();
            }

            var upgraded = simulation.State.Shelf[0].Traits.GetLevel(trait);
            var released = simulation.State.Shelf[0].Release(simulation.State.Date, 1.0);

            Assert.That(released.Traits.GetLevel(trait), Is.EqualTo(upgraded),
                "Releasing has to carry the work across, or the player paid for nothing.");
        }

        [Test]
        public void ShelfAndDeployedProgrammesDoNotBlockEachOther()
        {
            var simulation = WithAShelvedModel();

            simulation.State.AddDeployedModel(new DeployedModel(
                "Other", ArchitectureId.DenseTransformer, capability: 30.0,
                releaseDate: simulation.State.Date, activeParameterCount: 6.0,
                priceMultiplier: 1.0));

            var trait = FirstUpgradeable(simulation);

            Assert.That(simulation.TryStartUpgrade(0, trait, out _, onShelf: true), Is.True);

            // Index zero in the other list is a different model, and must not be considered busy.
            Assert.That(simulation.TryStartUpgrade(0, trait, out var reason), Is.True, reason);

            Assert.That(simulation.State.UpgradeProjects.Count, Is.EqualTo(2));
        }

        [Test]
        public void WorkBoughtOnTheShelfSurvivesASave()
        {
            var simulation = WithAShelvedModel();
            var trait = FirstUpgradeable(simulation);

            simulation.TryStartUpgrade(0, trait, out _, onShelf: true);

            for (var day = 0; day < 400 && simulation.State.UpgradeProjects.Count > 0; day++)
            {
                simulation.AdvanceDay();
            }

            var expected = simulation.State.Shelf[0].Traits.GetLevel(trait);
            Assert.That(expected, Is.GreaterThan(0), "This test needs something to have changed.");

            var reloaded = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.That(reloaded.Shelf[0].Traits.GetLevel(trait), Is.EqualTo(expected),
                "A save that drops this loses money the player already spent.");
        }

        [Test]
        public void AnApprovedProgrammeInFlightSurvivesASave()
        {
            var simulation = WithAShelvedModel();
            simulation.TryStartUpgrade(0, FirstUpgradeable(simulation), out _, onShelf: true);

            var reloaded = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.That(reloaded.UpgradeProjects.Single().OnShelf, Is.True,
                "A reloaded programme that forgets which list it belongs to upgrades a stranger.");
        }
    }
}
