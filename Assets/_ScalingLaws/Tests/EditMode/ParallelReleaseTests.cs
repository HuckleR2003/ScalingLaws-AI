using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// More than one release in engineering at once, from the author's list of 2026-09-18: two need
    /// an office of level one and five people, three need level two and fifteen.
    /// </summary>
    public sealed class ParallelReleaseTests
    {
        private static CompanySimulation Company(int models)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2024, 1, 1),
                CashUsd = 5_000_000_000L
            };

            for (var index = 0; index < models; index++)
            {
                state.AddDeployedModel(new DeployedModel(
                    "Aurora " + index, ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));
            }

            var simulation = new CompanySimulation(state).LearnedToRent();
            simulation.SetRentedPetaflops(4_000.0);

            return simulation;
        }

        /// <summary>
        /// Fills the roster to a headcount. Contractors, because they need no desk: a roster refuses
        /// a seated hire when the lease is full, and a loop waiting for a refused hire to land never
        /// ends. That is how the first version of this fixture hung the whole suite.
        /// </summary>
        private static void Staff(CompanySimulation simulation, int people)
        {
            while (simulation.State.Staff.Headcount < people)
            {
                var added = simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 3,
                    GameDate.Start, "Contractor", PlayerSkill.Development, HireSource.Remote, 40.0));

                Assert.That(added, Is.True, "The roster refused a contractor.");
            }
        }

        private static void Move(CompanySimulation simulation, OfficeTier tier)
        {
            Assert.That(simulation.TryMoveOffice(tier, out var why), Is.True, why);
        }

        private static void Plan(CompanySimulation simulation, int model)
        {
            Assert.That(simulation.CanPlanRelease(model, out var busy), Is.True, busy);
            Assert.That(simulation.TryStartUpgrades(model, new[] { ModelTrait.Reasoning }, out var why),
                Is.True, why);

            simulation.State.UpgradeProjects[^1].PlannedVersionName = "v" + model;
        }

        [Test]
        public void AGarageRunsOneReleaseAtATime()
        {
            var simulation = Company(2);
            Staff(simulation, 20);

            Assert.That(simulation.ReleasePlanSlots(), Is.EqualTo(1),
                "People without premises are not a second team.");

            Plan(simulation, 0);

            Assert.That(simulation.CanPlanRelease(1, out var why), Is.False);
            Assert.That(why, Does.Contain("1").And.Contain("5"),
                "The refusal has to say what the second team needs.");
        }

        [Test]
        public void LevelOneAndFivePeopleRunTwo()
        {
            var simulation = Company(3);
            Move(simulation, OfficeTier.Loft);
            Staff(simulation, 4);

            Assert.That(simulation.ReleasePlanSlots(), Is.EqualTo(1), "Four people is not five.");

            Staff(simulation, 5);
            Assert.That(simulation.ReleasePlanSlots(), Is.EqualTo(2));

            Plan(simulation, 0);
            Plan(simulation, 1);

            Assert.That(simulation.CanPlanRelease(2, out _), Is.False, "Two slots, two plans.");
        }

        [Test]
        public void LevelTwoAndFifteenPeopleRunThree()
        {
            var simulation = Company(3);
            Move(simulation, OfficeTier.Floor);
            Staff(simulation, 14);

            Assert.That(simulation.ReleasePlanSlots(), Is.EqualTo(2),
                "Fourteen people at level two is still the second rung.");

            Staff(simulation, 15);
            Assert.That(simulation.ReleasePlanSlots(), Is.EqualTo(3));

            Plan(simulation, 0);
            Plan(simulation, 1);
            Plan(simulation, 2);
        }

        [Test]
        public void OneModelNeverTakesTwoPlansAtOnceWhateverTheSlots()
        {
            var simulation = Company(1);
            Move(simulation, OfficeTier.Floor);
            Staff(simulation, 15);

            Plan(simulation, 0);

            Assert.That(simulation.CanPlanRelease(0, out _), Is.False,
                "Two versions of one product racing to the shelf is what the one-plan rule stopped.");
        }
    }
}
