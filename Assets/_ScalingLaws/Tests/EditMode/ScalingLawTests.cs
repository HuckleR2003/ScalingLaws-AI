using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class ScalingLawTests
    {
        [Test]
        public void ComputeOptimalRunsLandNearTwentyTokensPerParameter()
        {
            // The whole design lesson of the game. If this ratio drifts, the fit constants are wrong.
            var budgets = new[] { 1e22, 1e23, 5.88e23, 1e25, 1e26 };

            foreach (var budget in budgets)
            {
                var ratio = ScalingLaw.OptimalTokensPerParameter(budget);
                Assert.That(ratio, Is.InRange(12.0, 32.0),
                    $"Budget {budget:E1} FLOP gave {ratio:0.0} tokens per parameter.");
            }
        }

        [Test]
        public void ChinchillaBudgetReproducesRoughlySeventyBillionParameters()
        {
            // 70B parameters on 1.4T tokens is the published compute-optimal point for this budget.
            var flop = ScalingLaw.TrainingFlop(70e9, 1.4e12);
            var parameters = ScalingLaw.OptimalParameters(flop);
            var tokens = ScalingLaw.OptimalTokens(flop);

            Assert.That(parameters, Is.InRange(4e10, 1.1e11), $"Got {parameters:E2} parameters.");
            Assert.That(tokens, Is.InRange(8e11, 2.4e12), $"Got {tokens:E2} tokens.");
        }

        [Test]
        public void LossFallsWithScaleAndNeverBreaksTheFloor()
        {
            var small = ScalingLaw.Loss(1e9, 2e10);
            var medium = ScalingLaw.Loss(7e10, 1.4e12);
            var large = ScalingLaw.Loss(5e11, 1e13);

            Assert.That(medium, Is.LessThan(small));
            Assert.That(large, Is.LessThan(medium));
            Assert.That(large, Is.GreaterThan(ScalingLaw.IrreducibleLoss));
        }

        [Test]
        public void TenTimesTheComputeBuysAConstantCapabilityStep()
        {
            // Capability is linear in log reducible loss on purpose: every generation costs the same
            // multiple of compute, which is what makes the treadmill honest.
            var steps = new double[3];
            var budget = 1e22;

            for (var index = 0; index < steps.Length; index++)
            {
                var before = ScalingLaw.BestCapabilityForBudget(budget);
                var after = ScalingLaw.BestCapabilityForBudget(budget * 10.0);
                steps[index] = after - before;
                budget *= 10.0;
            }

            // The fit constants put this at 10.0 points, from reducible loss falling as
            // C^-(alpha*beta/(alpha+beta)). Ten times the compute for one step is the treadmill.
            foreach (var step in steps)
            {
                Assert.That(step, Is.EqualTo(10.0).Within(0.5), $"A ten times budget increase gave {step:0.00} points.");
            }

            Assert.That(Math.Abs(steps[0] - steps[2]), Is.LessThan(0.1), "The step size must stay flat across scales.");
        }

        [Test]
        public void ShapeEfficiencyPeaksAtTheOptimumAndPunishesBothMistakes()
        {
            var flop = 1e24;
            var optimalParameters = ScalingLaw.OptimalParameters(flop);
            var optimalTokens = ScalingLaw.OptimalTokens(flop);

            var atOptimum = ScalingLaw.ShapeEfficiency(optimalParameters, optimalTokens);

            // Same FLOP budget, spent on a model that is far too big for its data, and then on one
            // that is far too small.
            var undertrained = ScalingLaw.ShapeEfficiency(optimalParameters * 8.0, optimalTokens / 8.0);
            var overtrained = ScalingLaw.ShapeEfficiency(optimalParameters / 8.0, optimalTokens * 8.0);

            Assert.That(atOptimum, Is.GreaterThan(0.99));
            Assert.That(undertrained, Is.LessThan(atOptimum));
            Assert.That(overtrained, Is.LessThan(atOptimum));
        }

        [Test]
        public void SparseActivationCutsTheBillWithoutCuttingTheParameterCount()
        {
            const double parameters = 4e11;
            const double tokens = 8e12;

            var dense = ScalingLaw.TrainingPetaflopDays(parameters, tokens);
            var sparse = ScalingLaw.TrainingPetaflopDays(parameters, tokens, 0.25);

            Assert.That(sparse, Is.EqualTo(dense * 0.25).Within(dense * 0.001));

            // Quality depends on the parameters that exist, not on how many fire per token.
            Assert.That(ScalingLaw.Capability(parameters, tokens), Is.GreaterThan(0.0));
        }

        [Test]
        public void CapabilityStaysInsideItsScale()
        {
            Assert.That(ScalingLaw.Capability(1e6, 1e6), Is.InRange(0.0, 100.0));
            Assert.That(ScalingLaw.Capability(1e15, 1e17), Is.InRange(0.0, 100.0));
            Assert.That(ScalingLaw.CapabilityFromLoss(double.NaN), Is.InRange(0.0, 100.0));
            Assert.That(ScalingLaw.CapabilityFromLoss(0.0), Is.EqualTo(100.0));
        }

        [Test]
        public void PetaflopDayConversionMatchesTheDefinition()
        {
            Assert.That(SimUnits.FlopPerPetaflopDay, Is.EqualTo(8.64e19).Within(1e15));
            Assert.That(SimUnits.FlopToPetaflopDays(8.64e19), Is.EqualTo(1.0).Within(1e-9));

            // GPT-3 sized run: 175B parameters on 300B tokens is roughly 3600 petaflop/s-days.
            var petaflopDays = ScalingLaw.TrainingPetaflopDays(175e9, 300e9);
            Assert.That(petaflopDays, Is.InRange(3000.0, 4200.0), $"Got {petaflopDays:N0} PF-days.");
        }
    }
}
