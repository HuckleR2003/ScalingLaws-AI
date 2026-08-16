using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The locked halves of the SCALE sliders, attacked rather than admired.
    ///
    /// **A lock that only exists in the panel is not a lock.** The parameter ceiling was reported as
    /// broken because the blueprint was built before the clamp ran, and the fix was in the panel —
    /// which means the simulation would still accept an illegal blueprint if anything else built
    /// one. A save edited by hand, an older file loaded forward, a future screen: all of them reach
    /// the same door. So these tests do not touch the panel at all. They build blueprints past the
    /// ceiling directly and check the company refuses them.
    /// </summary>
    public sealed class CreatorLockTests
    {
        private static CompanySimulation Ready(long cash = 5_000_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = cash;
            simulation.SetRentedPetaflops(4000.0);
            return simulation;
        }

        /// <summary>The billions a fraction of the slider's log travel lands on.</summary>
        private static double BillionsAt(double fraction, double lowLog, double highLog) =>
            Math.Pow(10.0, lowLog + (highLog - lowLog) * fraction);

        // ---- parameters -----------------------------------------------------------------------

        [Test]
        public void ACompanyWithNoResearchCannotTrainPastTheParameterCeiling()
        {
            var simulation = Ready();
            var ceiling = simulation.ParameterCeilingBillions();

            var legal = new ModelBlueprint("Legal", ArchitectureId.DenseTransformer,
                ceiling * 0.95, 400.0, DatasetSource.WebCrawl);

            var illegal = legal.WithParameters(ceiling * 1.5);

            Assert.That(simulation.Project(legal).IsFeasible, Is.True,
                "A run under the ceiling has to be allowed.");

            Assert.That(simulation.Project(illegal).IsFeasible, Is.False,
                "A blueprint past the ceiling has to be refused by the company, not only by the "
                + "slider that usually builds it.");
        }

        [Test]
        public void TheRefusalNamesWhatWouldLiftIt()
        {
            var simulation = Ready();

            var illegal = new ModelBlueprint("Too big", ArchitectureId.DenseTransformer,
                simulation.ParameterCeilingBillions() * 3.0, 400.0, DatasetSource.WebCrawl);

            var projection = simulation.Project(illegal);

            Assert.That(projection.BlockingReason, Is.Not.Empty);
            Assert.That(projection.BlockingReason.ToLowerInvariant(),
                Does.Contain("research").Or.Contain("supervis").Or.Contain("cap"),
                $"A dead end that does not say what opens it: {projection.BlockingReason}");
        }

        [Test]
        public void EachRungActuallyRaisesTheParameterCeiling()
        {
            var simulation = Ready();
            var previous = simulation.ParameterCeilingBillions();

            foreach (var (node, _) in ScaleCeiling.Ladder)
            {
                simulation.State.UnlockedResearch.Add(node);
                var now = simulation.ParameterCeilingBillions();

                Assert.That(now, Is.GreaterThan(previous),
                    $"{node} is on the ladder and did not move the ceiling.");

                previous = now;
            }
        }

        // ---- tokens ---------------------------------------------------------------------------

        [Test]
        public void TheTokenLadderOnlyEverOpensUp()
        {
            var opened = TokenCeiling.FractionFor(_ => false);

            Assert.That(opened, Is.EqualTo(TokenCeiling.BaseFraction).Within(1e-9),
                "A company with nothing researched gets exactly the base fraction.");

            var everything = TokenCeiling.FractionFor(_ => true);

            Assert.That(everything, Is.EqualTo(1.0).Within(1e-9),
                "A company that has researched the whole data line gets the whole slider, or the "
                + "top of it is permanently dead and reads as a bug.");
        }

        [Test]
        public void EveryTokenRungIsReachableAndNamed()
        {
            var held = new System.Collections.Generic.HashSet<ResearchNodeId>();
            var seen = 0;

            while (TokenCeiling.TryNextRung(held.Contains, out var node, out var fraction))
            {
                Assert.That(node, Is.Not.EqualTo(ResearchNodeId.None));
                Assert.That(ResearchTree.Get(node).DisplayName, Is.Not.Empty,
                    $"{node} is on the ladder and has no name to show on the lock.");

                Assert.That(fraction, Is.GreaterThan(TokenCeiling.FractionFor(held.Contains)));

                held.Add(node);
                seen++;

                Assert.That(seen, Is.LessThanOrEqualTo(TokenCeiling.Ladder.Length),
                    "The ladder walk did not terminate.");
            }

            Assert.That(seen, Is.EqualTo(TokenCeiling.Ladder.Length),
                "Some rung cannot be reached by researching the ones before it.");
        }

        [Test]
        public void TheTokenCeilingSitsWhereTheSliderSaysItDoes()
        {
            // The panel maps the fraction onto the slider's log travel. If these two ever disagree
            // the shaded part of the bar stops matching the value it refuses.
            const double lowLog = 1.0;
            const double highLog = 5.0;

            var atBase = BillionsAt(TokenCeiling.BaseFraction, lowLog, highLog);
            var atFull = BillionsAt(1.0, lowLog, highLog);

            Assert.That(atBase, Is.LessThan(atFull));
            Assert.That(atBase, Is.GreaterThan(Math.Pow(10.0, lowLog)),
                "Half the travel has to be a usable number of tokens, not the bottom of the range.");
        }

        // ---- precision ---------------------------------------------------------------------------

        [Test]
        public void TheWholePrecisionLadderIsMonotonic()
        {
            var rungs = new[]
            {
                TrainingPrecision.Float64, TrainingPrecision.Float32,
                TrainingPrecision.BFloat16, TrainingPrecision.Float8
            };

            for (var index = 1; index < rungs.Length; index++)
            {
                var below = TrainingChoiceCatalog.Get(rungs[index - 1]);
                var above = TrainingChoiceCatalog.Get(rungs[index]);

                Assert.That(above.Throughput, Is.GreaterThan(below.Throughput),
                    $"{rungs[index]} is above {rungs[index - 1]} and is not faster.");

                Assert.That(above.Instability, Is.GreaterThanOrEqualTo(below.Instability),
                    $"{rungs[index]} is faster than {rungs[index - 1]} and costs nothing for it.");
            }
        }

        [Test]
        public void ACompanyWithNoResearchCanStillTrainSomething()
        {
            var simulation = Ready();

            // The whole point of the fourth rung: gating all three modern widths must not leave a
            // new company unable to start a run at all.
            var blueprint = new ModelBlueprint("First", ArchitectureId.DenseTransformer,
                4.0, 80.0, DatasetSource.WebCrawl);

            Assert.That(blueprint.Precision, Is.EqualTo(TrainingPrecision.Float64));

            var projection = simulation.Project(blueprint);

            Assert.That(projection.IsFeasible, Is.True,
                $"A brand new company cannot train anything: {projection.BlockingReason}");
        }

        [Test]
        public void EveryGatedWidthIsRefusedUntilItIsBought()
        {
            foreach (var precision in new[]
            {
                TrainingPrecision.Float32, TrainingPrecision.BFloat16, TrainingPrecision.Float8
            })
            {
                var simulation = Ready();

                var blueprint = new ModelBlueprint("Subject", ArchitectureId.DenseTransformer,
                    4.0, 80.0, DatasetSource.WebCrawl).WithPrecision(precision);

                Assert.That(simulation.Project(blueprint).IsFeasible, Is.False,
                    $"{precision} was allowed without its research.");

                simulation.State.UnlockedResearch.Add(TrainingChoiceCatalog.GateFor(precision));

                // FP8 also needs the silicon, so it can still be blocked for a different reason on
                // an early date. What must not happen is the research being ignored.
                var after = simulation.Project(blueprint);

                if (!after.IsFeasible)
                {
                    Assert.That(after.BlockingReason.ToLowerInvariant(),
                        Does.Not.Contain("research"),
                        $"{precision} is researched and still refused for want of research.");
                }
            }
        }

        // ---- the whole run ------------------------------------------------------------------------

        [Test]
        public void ARunTakesAboutAsLongAsTheCreatorPromised()
        {
            var simulation = Ready();

            var blueprint = new ModelBlueprint("Subject", ArchitectureId.DenseTransformer,
                12.0, 240.0, DatasetSource.WebCrawl);

            var projection = simulation.Project(blueprint);
            Assert.That(projection.IsFeasible, Is.True, projection.BlockingReason);

            var promised = projection.TrainingDays;
            Assert.That(promised, Is.GreaterThan(0));

            Assert.That(simulation.TryStartTraining(blueprint, out var reason), Is.True, reason);

            var days = 0;
            while (simulation.State.ActiveRun != null && days < promised * 4 + 40)
            {
                simulation.AdvanceDay();
                days++;
            }

            Assert.That(simulation.State.ActiveRun, Is.Null, "The run never finished.");

            // Within a fifth. The projection cannot be exact — the fleet is billed and re-profiled
            // daily — but a creator that says eleven weeks and delivers four days is the bug this
            // whole alignment exists to prevent.
            Assert.That(days, Is.InRange(promised * 0.8, promised * 1.25 + 2),
                $"The creator promised {promised} days and the run took {days}.");
        }
    }
}
