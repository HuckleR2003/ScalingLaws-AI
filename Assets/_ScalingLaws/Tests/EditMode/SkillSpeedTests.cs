using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Development's second effect: a run goes faster, and the model is no better for it.
    ///
    /// **Both halves matter and the second one is the point.** Asked for on 2026-10-25 because the
    /// skill's only effect was the spread of a finished run around its projection, which at 0.65
    /// is the second largest number in `SkillSet` and is invisible to a player: they see one
    /// model, never a distribution.
    ///
    /// What it must not buy is capability. That comes out of the scaling law applied to the
    /// compute and the corpus, and the honesty flag covers it.
    /// </summary>
    public sealed class SkillSpeedTests
    {
        private static CompanySimulation Company(int development)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2024, 1, 1),
                CashUsd = 2_000_000_000
            };

            state.Skills.SetLevel(PlayerSkill.Development, development);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(300.0);

            return simulation;
        }

        [Test]
        public void AStrongDeveloperFinishesTheSameRunSooner()
        {
            var weak = Company(0);
            var strong = Company(100);

            Assert.That(strong.TrainingThroughputMultiplier(),
                Is.GreaterThan(weak.TrainingThroughputMultiplier()),
                "A founder who is good at this moves no more work through the same silicon, so "
                + "the skill's new effect does not reach the one method the projection and the "
                + "day both read.");
        }

        /// <summary>
        /// **And it is a quarter of the skill, not all of it.** One level already buys the spread;
        /// letting it buy a full share of the calendar as well is the same point paid twice.
        /// </summary>
        [Test]
        public void TheSpeedIsASmallShareOfTheSkillRatherThanAllOfIt()
        {
            var full = Company(100).State.Skills.TrainingSpeedMultiplier();
            var spread = Company(100).State.Skills.TrainingSpreadMultiplier();

            Assert.That(full, Is.LessThan(1.3).And.GreaterThan(1.0),
                "At level 100 the speed multiplier is " + full.ToString("0.000")
                + ", which is either doing nothing or doing the work of two skills.");

            Assert.That(1.0 - spread, Is.GreaterThan(full - 1.0),
                "The speed effect is larger than the spread effect, so the thing this skill was "
                + "always about has become the smaller half of it.");
        }

        [Test]
        public void SpeedDoesNotBuyCapability()
        {
            var weak = Company(0);
            var strong = Company(100);

            // Near the opening ceiling, trained compute-optimally, on a small fleet. **The run
            // has to be long for this test to mean anything**: the projection answers in whole
            // days, and a fifth off a four day run rounds to the same four days. That exact
            // shape shipped on the precision cards a month ago.
            var blueprint = new ModelBlueprint("Aurora", ArchitectureId.DenseTransformer,
                30.0, 600.0, DatasetSource.WebCrawl);

            var slow = weak.Project(blueprint);
            var fast = strong.Project(blueprint);

            // Both arms have to be real runs, or the two zeros of a blocked projection would
            // pass the capability half of this test while measuring nothing at all.
            Assert.That(slow.IsFeasible && fast.IsFeasible, Is.True,
                "Neither of these is a run the company can start, so the comparison below is "
                + "between two refusals: " + slow.BlockingReason + " / " + fast.BlockingReason);

            Assert.That(fast.TrainingDays, Is.LessThan(slow.TrainingDays),
                "The same blueprint takes the same calendar whoever is running the lab.");

            Assert.That(fast.ProjectedCapability,
                Is.EqualTo(slow.ProjectedCapability).Within(0.01),
                "A founder skill moved the score the model comes out at. Capability is the "
                + "scaling law applied to compute and corpus, and the card for this skill says "
                + "in as many words that it does not touch it.");
        }
    }
}
