using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A version ships when the work that produces it lands, not when it is paid for.
    ///
    /// **Reported plainly**: the new version was on the market although the engineering was not
    /// finished. The screen is called PLAN THE RELEASE and it published the release on the click
    /// that commissioned the programme, so the weeks of work ran behind a version people could
    /// already buy.
    /// </summary>
    public sealed class PlannedReleaseTests
    {
        private static CompanySimulation Company()
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2024, 1, 1),
                CashUsd = 2_000_000_000L
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(4_000.0);

            return simulation;
        }

        /// <summary>Commissions a programme the way the release planner does, and plans a version on it.</summary>
        private static ModelUpgradeProject Plan(CompanySimulation simulation, string version)
        {
            Assert.That(simulation.TryStartUpgrades(0, new[] { ModelTrait.Reasoning }, out var why),
                Is.True, why);

            var project = simulation.State.UpgradeProjects[^1];

            project.PlannedVersionName = version;
            project.PlannedPriceUsdPerMonth = 42.0;
            project.PlannedFreeTokensPerDay = 1_000.0;

            return project;
        }

        [Test]
        public void APlannedVersionIsNotOnTheMarketWhileItIsStillBeingBuilt()
        {
            var simulation = Company();
            var line = simulation.State.DeployedModels[0].Line;
            var before = line.Versions.Count;

            var project = Plan(simulation, "Aurora 2");

            Assert.That(line.Versions.Count, Is.EqualTo(before),
                "Commissioning the work must not publish the version the work is for.");

            Assert.That(project.IsComplete, Is.False);

            // A day is not a programme.
            simulation.Advance(1);

            Assert.That(line.Versions.Count, Is.EqualTo(before),
                "Nor must one day of it.");
        }

        [Test]
        public void ItShipsOnTheDayTheWorkLands()
        {
            var simulation = Company();
            var line = simulation.State.DeployedModels[0].Line;
            var before = line.Versions.Count;

            Plan(simulation, "Aurora 2");

            // Long enough for any programme in the catalogue, and the assertion is that it landed
            // rather than that it landed on a particular day.
            simulation.Advance(400);

            Assert.That(simulation.State.UpgradeProjects, Is.Empty, "The programme should be done.");

            Assert.That(line.Versions.Count, Is.EqualTo(before + 1),
                "And the version it was commissioned for is on the market now.");

            Assert.That(line.Versions[^1].Name, Is.EqualTo("Aurora 2"));
        }

        /// <summary>
        /// The price travels with the version, because a plan that took effect first was not a plan.
        /// </summary>
        [Test]
        public void ThePriceLandsWithTheVersionAndNotBeforeIt()
        {
            var simulation = Company();
            var was = simulation.State.Monetization.SubscriptionPriceUsdPerMonth;

            Plan(simulation, "Aurora 2");

            Assert.That(simulation.State.Monetization.SubscriptionPriceUsdPerMonth,
                Is.EqualTo(was),
                "Naming a future version must not reprice the one people are paying for today.");

            simulation.Advance(400);

            Assert.That(simulation.State.Monetization.SubscriptionPriceUsdPerMonth,
                Is.EqualTo(42.0).Within(0.001));

            Assert.That(simulation.State.Monetization.FreeTierTokensPerUserPerDay,
                Is.EqualTo(1_000.0).Within(0.001));
        }

        [Test]
        public void OnlyOneReleaseCanBePlannedAtATime()
        {
            var simulation = Company();

            Assert.That(simulation.State.ReleaseProgrammeInFlight, Is.False,
                "Nothing is planned on a company that has commissioned nothing.");

            Plan(simulation, "Aurora 2");

            Assert.That(simulation.State.ReleaseProgrammeInFlight, Is.True,
                "A named version waiting on engineering is what blocks the next plan.");

            simulation.Advance(400);

            Assert.That(simulation.State.ReleaseProgrammeInFlight, Is.False,
                "And the block lifts the day it ships, or the screen is dead forever.");
        }

        /// <summary>
        /// Work on the shelf names no version, so it blocks no release. That split is deliberate.
        /// </summary>
        [Test]
        public void WorkOnAShelvedModelDoesNotBlockARelease()
        {
            var simulation = Company();

            simulation.State.AddToShelf(new TrainedModel(
                "Muse", ArchitectureId.DenseTransformer, 40.0, simulation.State.Date, 8.0, 40.0));

            Assert.That(simulation.TryStartUpgrades(0, new[] { ModelTrait.Reasoning },
                out var why, onShelf: true), Is.True, why);

            Assert.That(simulation.State.ReleaseProgrammeInFlight, Is.False,
                "A programme on a model nobody can buy names no version and blocks no release.");
        }

        /// <summary>
        /// The plan is causal, so it has to survive a save. Eighth time in this project.
        /// </summary>
        [Test]
        public void ThePlannedVersionSurvivesASave()
        {
            var simulation = Company();
            Plan(simulation, "Aurora 2");

            var restored = SaveStore.Restore(SaveStore.Parse(
                JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.That(restored.UpgradeProjects.Count, Is.EqualTo(1));

            var project = restored.UpgradeProjects[0];

            Assert.That(project.PlannedVersionName, Is.EqualTo("Aurora 2"),
                "Dropping this would take a release the player named and paid for.");

            Assert.That(project.PlannedPriceUsdPerMonth, Is.EqualTo(42.0).Within(0.001));
            Assert.That(project.PlannedFreeTokensPerDay, Is.EqualTo(1_000.0).Within(0.001));
            Assert.That(restored.ReleaseProgrammeInFlight, Is.True);
        }

        [Test]
        public void AProgrammeFromBeforeTheRuleHasNoReleaseWaitingOnIt()
        {
            var simulation = Company();
            Plan(simulation, "Aurora 2");

            var legacy = SaveStore.Capture(simulation.State);
            legacy.version = 55;

            var upgraded = SaveStore.Parse(JsonUtility.ToJson(legacy));
            Assert.That(upgraded, Is.Not.Null);
            Assert.That(upgraded.version, Is.EqualTo(SaveData.CurrentVersion));

            var state = SaveStore.Restore(upgraded);

            Assert.That(state.UpgradeProjects.Count, Is.EqualTo(1));
            Assert.That(state.UpgradeProjects[0].HasPlannedRelease, Is.False,
                "In v55 the version was published on the click that commissioned the work, so a "
                + "v55 programme has already shipped its version. Planning one now would publish "
                + "the same version a second time.");
        }

        [Test]
        public void EveryWordTheBlockedButtonUsesExistsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (Language language in System.Enum.GetValues(typeof(Language)))
                {
                    Loc.Current = language;

                    Assert.That(Loc.T("upgrade.team_busy"), Is.Not.EqualTo("upgrade.team_busy"));
                    Assert.That(Loc.T("upgrade.one_release_note"),
                        Is.Not.EqualTo("upgrade.one_release_note"));
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
