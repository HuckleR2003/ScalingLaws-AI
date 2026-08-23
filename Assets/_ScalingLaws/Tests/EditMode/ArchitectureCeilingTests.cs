using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// How far the company knows how to push each research direction.
    ///
    /// **The five direction sliders used to run their whole length on day one**, which said a two
    /// person company in January 2022 already knew how to build a well routed sparse mixture and
    /// simply chose not to. Money and taste were the only gates, and money compounds. That is the
    /// exact failure the research tree exists to prevent, sitting on the controls the architecture
    /// screen is made of.
    /// </summary>
    public sealed class ArchitectureCeilingTests
    {
        private static CompanySimulation Company()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 900_000_000;
            simulation.SetRentedPetaflops(3_000.0);
            return simulation;
        }

        private static ArchitectureBlueprint Leaning(ResearchDirection direction, double weight)
        {
            double Of(ResearchDirection which) => which == direction ? weight : 0.1;

            return new ArchitectureBlueprint(
                "House family 1",
                ArchitectureId.CustomFamilyA,
                ArchitectureId.None,
                Of(ResearchDirection.Sparsity),
                Of(ResearchDirection.Throughput),
                Of(ResearchDirection.Quality),
                Of(ResearchDirection.Serving),
                Of(ResearchDirection.Reasoning),
                researchBudgetUsd: 120_000_000,
                durationDays: 420);
        }

        [Test]
        public void ACompanyThatHasResearchedNothingCanOnlyLeanSlightly()
        {
            foreach (var direction in ArchitectureCeiling.Ladders.Keys)
            {
                Assert.That(ArchitectureCeiling.FractionFor(direction, _ => false),
                    Is.EqualTo(ArchitectureCeiling.BaseFraction).Within(1e-9),
                    $"{direction} starts open before anything has been researched.");
            }
        }

        /// <summary>
        /// **The rule is enforced where programmes start, not where the slider is drawn.**
        ///
        /// A cap that lives only in the interface is a suggestion the moment a second way to
        /// commit exists, and this project has shipped exactly that six times.
        /// </summary>
        [Test]
        public void APrgrammePushedPastTheCeilingIsRefused()
        {
            var simulation = Company();

            var refused = simulation.TryStartArchitectureProgramme(
                Leaning(ResearchDirection.Sparsity, 1.0), out var reason);

            Assert.That(refused, Is.False, "Full sparsity is not something a new lab knows how to do.");
            Assert.That(reason, Does.Contain("Sparsity"), "The refusal has to name what is over the line.");
            Assert.That(simulation.State.ActiveArchitectureProject, Is.Null);
        }

        [Test]
        public void ResearchingTheLadderOpensTheWholeSlider()
        {
            var simulation = Company();

            foreach (var (node, _) in ArchitectureCeiling.Ladders[ResearchDirection.Sparsity])
            {
                simulation.State.UnlockedResearch.Add(node);
            }

            Assert.That(
                ArchitectureCeiling.FractionFor(ResearchDirection.Sparsity, simulation.State.HasResearch),
                Is.EqualTo(1.0).Within(1e-9));

            Assert.That(
                simulation.TryStartArchitectureProgramme(
                    Leaning(ResearchDirection.Sparsity, 1.0), out var reason),
                Is.True, reason);
        }

        [Test]
        public void OpeningOneDirectionDoesNotOpenTheOthers()
        {
            var simulation = Company();

            foreach (var (node, _) in ArchitectureCeiling.Ladders[ResearchDirection.Sparsity])
            {
                simulation.State.UnlockedResearch.Add(node);
            }

            Assert.That(
                ArchitectureCeiling.FractionFor(ResearchDirection.Reasoning, simulation.State.HasResearch),
                Is.EqualTo(ArchitectureCeiling.BaseFraction).Within(1e-9),
                "Learning to route a sparse model teaches nothing about reasoning.");
        }

        /// <summary>
        /// Every ladder climbs, ends at exactly one, and starts above the base.
        ///
        /// A rung at or below the base does nothing and reads to the player as a node that lied.
        /// A ladder that stops short leaves the top of a slider permanently dead, which reads as
        /// a bug rather than as a limit.
        /// </summary>
        [Test]
        public void EveryLadderClimbsFromTheBaseToTheWholeSlider()
        {
            foreach (var (direction, ladder) in ArchitectureCeiling.Ladders)
            {
                Assert.That(ladder.Length, Is.GreaterThan(0), $"{direction} has no ladder.");

                Assert.That(ladder[0].Fraction, Is.GreaterThan(ArchitectureCeiling.BaseFraction),
                    $"{direction}: the first rung has to open something.");

                Assert.That(ladder[^1].Fraction, Is.EqualTo(1.0).Within(1e-9),
                    $"{direction}: the top of the slider is never reachable.");

                for (var index = 1; index < ladder.Length; index++)
                {
                    Assert.That(ladder[index].Fraction, Is.GreaterThan(ladder[index - 1].Fraction),
                        $"{direction}: rung {index} does not open more than the one below it.");
                }
            }
        }

        /// <summary>
        /// Every node named by a ladder is a node that exists, and it is dated after the thing it
        /// needs. `ConsistencyTests` holds the second half for the whole tree; this holds the first
        /// half for the ten added with this screen, because a ladder can name a node that was
        /// deleted and nothing else would notice.
        /// </summary>
        [Test]
        public void EveryRungNamesARealNode()
        {
            foreach (var (direction, ladder) in ArchitectureCeiling.Ladders)
            {
                foreach (var (node, _) in ladder)
                {
                    Assert.That(ResearchTree.All.Any(entry => entry.Id == node), Is.True,
                        $"{direction} is gated behind {node}, which is not in the tree.");
                }
            }
        }

        /// <summary>
        /// The interface has something to print on the locked part of every track.
        ///
        /// A grey bar with no explanation on it reads as a rendering fault, which is what the
        /// parameter slider taught this project the first time it was capped.
        /// </summary>
        [Test]
        public void ALockedTrackCanAlwaysNameWhatWouldOpenIt()
        {
            foreach (var direction in ArchitectureCeiling.Ladders.Keys)
            {
                Assert.That(
                    ArchitectureCeiling.TryNextRung(direction, _ => false, out var node, out var fraction),
                    Is.True, $"{direction} is capped with nothing to point the player at.");

                Assert.That(node, Is.Not.EqualTo(ResearchNodeId.None));
                Assert.That(fraction, Is.GreaterThan(ArchitectureCeiling.BaseFraction));
            }
        }
    }
}
