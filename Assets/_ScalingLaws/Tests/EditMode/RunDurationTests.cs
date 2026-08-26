using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The number the creator quotes is the number the calendar actually spends.
    ///
    /// **Reported from a playtest: a run quoted at 21 days finished in 4.** That is the safety
    /// stage's length on its own, which says the compute clock was emptying almost immediately and
    /// the only thing left holding the run open was the stage. A player who plans a campaign around
    /// a quoted duration and gets a fifth of it is not being given a decision, they are being given
    /// a number that means nothing.
    ///
    /// Nothing in the suite measured elapsed days against the quote before this, which is how the
    /// gap survived: every part was individually correct and the join was never weighed.
    /// </summary>
    public sealed class RunDurationTests
    {
        private static CompanySimulation Lab(double petaflops, long cash = 500_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 99));
            simulation.State.CashUsd = cash;
            simulation.SetRentedPetaflops(petaflops);
            return simulation;
        }

        private static ModelBlueprint Plan(double parameters, double tokensPerParameter) =>
            new("Subject", ArchitectureId.DenseTransformer, parameters, tokensPerParameter,
                DatasetSource.WebCrawl);

        /// <summary>Runs the clock until the run lands, and says how many days that took.</summary>
        private static int DaysToFinish(CompanySimulation simulation, int limit = 900)
        {
            for (var day = 1; day <= limit; day++)
            {
                simulation.AdvanceDay();

                if (simulation.State.ActiveRun == null)
                {
                    return day;
                }
            }

            return -1;
        }

        /// <summary>
        /// The headline. A quote of N days is a run of about N days.
        ///
        /// A tolerance rather than an equality, because the fleet is billed and re-profiled daily and
        /// the run is allowed to drift a little either way. A fifth of the quote is not drift.
        /// </summary>
        [Test]
        public void ARunTakesRoughlyAsLongAsTheCreatorSaidItWould()
        {
            var simulation = Lab(petaflops: 60.0);
            var blueprint = Plan(parameters: 7.0, tokensPerParameter: 20.0);

            var projection = simulation.Project(blueprint);
            Assert.IsTrue(projection.IsFeasible, projection.BlockingReason);
            Assert.That(projection.TrainingDays, Is.GreaterThan(6),
                "This fixture needs a run long enough for a shortfall to be visible.");

            Assert.IsTrue(simulation.TryStartTraining(blueprint, out var why), why);

            var actual = DaysToFinish(simulation);

            Assert.That(actual, Is.GreaterThan(0), "The run never finished.");
            Assert.That(actual, Is.EqualTo(projection.TrainingDays).Within(0.25 * projection.TrainingDays),
                $"Quoted {projection.TrainingDays} days, took {actual}. The creator's duration is the "
                + "only figure a player can plan a campaign around.");
        }

        /// <summary>
        /// The same on a fast cluster, where the compute clock empties almost at once.
        ///
        /// **The safety stage is a floor and the quote knows it.** Measured on 4000 PF: the run
        /// lands on exactly the stage, and the creator quotes exactly the stage plus the day of
        /// compute, so the two agree. Extra silicon shortening a run is one of the few decisions
        /// this game is about, and it does not make the quoted duration a fiction.
        ///
        /// The assertion is therefore "never faster than the stage" rather than "slower than it".
        /// Demanding the latter would be demanding the floor not work.
        /// </summary>
        [Test]
        public void AFastFleetLandsOnTheSafetyStageAndTheQuoteSaysSo()
        {
            var simulation = Lab(petaflops: 4000.0);
            var blueprint = Plan(parameters: 7.0, tokensPerParameter: 20.0);

            var projection = simulation.Project(blueprint);
            Assert.IsTrue(projection.IsFeasible, projection.BlockingReason);

            var stage = SafetyPlan.For(blueprint, 0).ExtraDays;

            Assert.IsTrue(simulation.TryStartTraining(blueprint, out var why), why);
            var actual = DaysToFinish(simulation);

            Assert.That(actual, Is.GreaterThanOrEqualTo(stage),
                $"Finished in {actual} days and the safety stage alone is {stage}. The stage is "
                + "work that no amount of silicon can hurry, so it is a floor.");

            Assert.That(actual, Is.EqualTo(projection.TrainingDays).Within(0.25 * projection.TrainingDays),
                $"Quoted {projection.TrainingDays}, took {actual}.");
        }

        /// <summary>
        /// The banner's countdown reaches zero on the day the run lands, not before.
        ///
        /// **Also reported: "it stops at 0 days and then days keep passing".** Two clocks with one
        /// of them on screen is worse than one clock, because the player is watching a number that
        /// has already finished while the thing it describes has not.
        /// </summary>
        [Test]
        public void TheCountdownDoesNotSitAtZeroWhileTheRunKeepsGoing()
        {
            var simulation = Lab(petaflops: 4000.0);
            var blueprint = Plan(parameters: 7.0, tokensPerParameter: 20.0);

            Assert.IsTrue(simulation.TryStartTraining(blueprint, out var why), why);

            var zeroed = 0;

            for (var day = 1; day <= 400; day++)
            {
                simulation.AdvanceDay();

                var run = simulation.State.ActiveRun;
                if (run == null)
                {
                    break;
                }

                if (run.DaysRemaining(simulation.RunPetaflopDaysPerDay()) <= 0)
                {
                    zeroed++;
                }
            }

            Assert.That(zeroed, Is.LessThanOrEqualTo(1),
                $"The countdown read zero on {zeroed} days while the run was still in flight.");
        }
    }
}
