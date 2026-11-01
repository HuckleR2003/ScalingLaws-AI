using System;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Whether <c>TrainingRun.ActualTokensBillions</c> is a fact or a copy.
    ///
    /// **Found by the third reachability sweep on 2026-11-01**: the field is clamped in the
    /// constructor, written into the save, and read by nothing in the rules. The author was asked
    /// whether a player should see it and said honestly that he did not know, so it is settled by
    /// measurement instead of by taste.
    ///
    /// The reasoning the measurement is testing: <c>DatasetCatalog</c> sets
    /// <c>IsSufficient = totalSupply >= requested</c> and <c>AvailableTokensBillions =
    /// min(requested, totalSupply)</c>, and <c>TrainingPlanner</c> appends <c>plan.data_short</c>
    /// to the **blocking** reasons when the mix is insufficient. So a run whose mix cannot supply
    /// the tokens asked for cannot start at all, and for every run that can start the two figures
    /// are the same number.
    ///
    /// If that holds, the field is a duplicate of <c>Blueprint.TrainingTokensBillions</c> and is
    /// deleted. If it does not hold, it is real information about a model that came out worse than
    /// it was projected to, and it belongs on a screen. **This fixture stays either way**, because
    /// the next person to wonder should find the answer rather than the question.
    /// </summary>
    public sealed class ActualTokensCarryNoInformationTests
    {
        private static CompanySimulation Lab()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 90210));
            simulation.State.CashUsd = 400_000_000L;
            simulation.SetRentedAccelerators(900);
            return simulation;
        }

        /// <summary>
        /// Every run that starts trained on exactly the tokens it asked for.
        ///
        /// Swept across the token axis rather than asserted once, because one blueprint passing
        /// says nothing about a field that only ever differs at an edge.
        /// </summary>
        [Test]
        public void AnyRunThatCanStartTrainedOnExactlyTheTokensItAskedFor()
        {
            var started = 0;
            var refused = 0;

            foreach (var tokens in new[] { 50, 200, 400, 900, 2_000, 6_000, 20_000, 120_000 })
            {
                foreach (var parameters in new[] { 5, 20 })
                {
                    var simulation = Lab();

                    var blueprint = new ModelBlueprint(
                        $"Muse {tokens}", ArchitectureId.DenseTransformer,
                        parameters, tokens, DatasetSource.WebCrawl);

                    if (!simulation.TryStartTraining(blueprint, out _))
                    {
                        refused++;
                        continue;
                    }

                    started++;
                    var run = simulation.State.ActiveRun;

                    Assert.That(run, Is.Not.Null, "A run started and there is no run.");

                    // **Read off the mix rather than off the run**, because the field this fixture
                    // was written about has been deleted for being a copy of exactly this. What
                    // has to keep holding is the property that made it a copy: a run that starts
                    // trains on every token it asked for.
                    var supplied = simulation.Project(run.Blueprint).Blend.AvailableTokensBillions;

                    Assert.That(supplied,
                        Is.EqualTo(run.Blueprint.TrainingTokensBillions).Within(1e-6),
                        $"A run of {tokens}B tokens on {parameters}B parameters started on a mix "
                        + $"supplying {supplied}B. If a run can start short of what it asked for "
                        + "then the model comes out worse than projected for a reason the player "
                        + "is never told, the figure deleted on 2026-11-01 was information after "
                        + "all, and the conclusion in Docs/TESTER_REPORTS.md is wrong.");
                }
            }

            Assert.That(started, Is.GreaterThan(4),
                $"Only {started} of {started + refused} blueprints could start, which is too few "
                + "for this sweep to have measured the field at all.");
        }

        /// <summary>
        /// And a mix that is short really does refuse, which is the other half of the argument.
        ///
        /// Without this the fixture above could be passing because nothing short was ever tried.
        /// </summary>
        [Test]
        public void AMixThatCannotSupplyTheTokensRefusesTheRunRatherThanShrinkingIt()
        {
            var simulation = Lab();

            // Far past what the opening crawl can supply, whatever the supply scaling does by date.
            var greedy = new ModelBlueprint(
                "Muse Greedy", ArchitectureId.DenseTransformer, 20, 50_000_000,
                DatasetSource.WebCrawl);

            var projection = simulation.Project(greedy);

            Assume.That(projection.Blend.IsSufficient, Is.False,
                "The opening crawl supplied fifty trillion tokens, so this is not the short case.");

            Assert.That(simulation.TryStartTraining(greedy, out var why), Is.False,
                "A run was started on a data mix that cannot supply the tokens it asked for. That "
                + "is the case ActualTokensBillions would exist to record, so if this ever passes "
                + "the field has to come back and be shown.");

            Assert.That(why, Is.Not.Empty, "Refused with nothing said.");
        }
    }
}
