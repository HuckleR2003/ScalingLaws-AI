using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The Scale stage's readouts. A player picks a model shape from these four bars and one badge, so
    /// a wrong number here is a wrong decision rather than a cosmetic fault.
    /// </summary>
    public sealed class ScaleReadoutTests
    {
        /// <summary>
        /// The belt and the badge have to agree, because they sit next to each other. Both read the
        /// band off <see cref="TrainingProjection"/>, and this asserts they still land together.
        /// </summary>
        [Test]
        public void TheBandOnTheBeltIsTheSameBandTheBadgeNames()
        {
            var (from, to) = TrainingProfile.BandOnBelt();

            Assert.Less(from, to, "The band has to have width or the belt has no middle zone.");
            Assert.Greater(from, 0.0, "The undertrained zone has to be visible.");
            Assert.Less(to, 1.0, "The spill zone has to be visible.");

            // A ratio just inside each edge has to land inside the drawn band, and just outside has to
            // land outside it. That is the whole contract between the two.
            var insideLow = TrainingProfile.PositionOnBelt(TrainingProjection.UndertrainedBelow * 1.01);
            var insideHigh = TrainingProfile.PositionOnBelt(TrainingProjection.OvertrainedAbove * 0.99);
            var outsideLow = TrainingProfile.PositionOnBelt(TrainingProjection.UndertrainedBelow * 0.99);

            Assert.Greater(insideLow, from);
            Assert.Less(insideHigh, to);
            Assert.Less(outsideLow, from);
        }

        [Test]
        public void OptimalSitsDeadCentreAndTheScaleIsSymmetricInMultiples()
        {
            Assert.AreEqual(0.5, TrainingProfile.PositionOnBelt(1.0), 1e-9,
                "Compute optimal is the reference, so it belongs in the middle.");

            // Half optimal and twice optimal are the same distance from the middle. On a linear scale
            // the entire undertrained half would squash into the first few pixels.
            var half = 0.5 - TrainingProfile.PositionOnBelt(0.5);
            var twice = TrainingProfile.PositionOnBelt(2.0) - 0.5;

            Assert.AreEqual(half, twice, 1e-9, "The belt is logarithmic or it is misleading.");
        }

        [Test]
        public void TheMarkerNeverLeavesTheBeltHoweverAbsurdTheShape()
        {
            foreach (var ratio in new[] { -5.0, 0.0, 1e-9, 1e9, double.NaN, double.PositiveInfinity })
            {
                var position = TrainingProfile.PositionOnBelt(ratio);

                Assert.IsFalse(double.IsNaN(position), $"ratio {ratio} produced NaN");
                Assert.GreaterOrEqual(position, 0.0, $"ratio {ratio}");
                Assert.LessOrEqual(position, 1.0, $"ratio {ratio}");
            }
        }

        [Test]
        public void EveryRatioGetsExactlyOneProfileWordAndTheOrderMakesSense()
        {
            var oversized = ProfileAt(0.1);
            var hungry = ProfileAt(0.45);
            var balanced = ProfileAt(1.0);
            var rich = ProfileAt(2.5);
            var lean = ProfileAt(20.0);

            Assert.AreEqual(ShapeProfile.Oversized, oversized);
            Assert.AreEqual(ShapeProfile.ComputeHungry, hungry);
            Assert.AreEqual(ShapeProfile.Balanced, balanced);
            Assert.AreEqual(ShapeProfile.DataRich, rich);
            Assert.AreEqual(ShapeProfile.Lean, lean);
        }

        /// <summary>
        /// Training efficiency and budget efficiency are deliberately different numbers. A perfectly
        /// shaped run that also bought a corpus it did not need converts its compute beautifully and
        /// still wasted money, and the second bar is the only place that shows.
        /// </summary>
        [Test]
        public void BuyingDataYouDoNotNeedShowsUpInBudgetButNotInTraining()
        {
            var lean = Fake(shapeEfficiency: 1.0, computeCost: 1_000_000, dataCost: 0);
            var wasteful = Fake(shapeEfficiency: 1.0, computeCost: 1_000_000, dataCost: 1_000_000);

            Assert.AreEqual(lean.TrainingEfficiency, wasteful.TrainingEfficiency, 1e-9,
                "The shape did not change, so the compute conversion must not change either.");

            Assert.Less(wasteful.BudgetEfficiency, lean.BudgetEfficiency,
                "Money spent on data the run did not need has to land somewhere.");

            Assert.AreEqual(0.5, wasteful.BudgetEfficiency, 1e-9,
                "Half the money went to data, so half the budget is converting.");
        }

        [Test]
        public void ARunThatDoesNotFitMemorySaysSoBeforeAnythingElse()
        {
            var profile = Fake(shapeEfficiency: 1.0, computeCost: 1000, dataCost: 0,
                memoryNeeded: 900.0, memoryAvailable: 400.0);

            Assert.IsFalse(profile.Fits);
            Assert.IsNotEmpty(profile.Notes);
            StringAssert.Contains("will not start", profile.Notes[0],
                "Memory is the note that stops the run, so it goes first. Everything else is advice.");
        }

        [Test]
        public void ThereIsAlwaysSomethingToSayAboutAShape()
        {
            foreach (var ratio in new[] { 0.1, 0.45, 1.0, 2.5, 20.0 })
            {
                var profile = FakeRatio(ratio);
                Assert.IsNotEmpty(profile.Notes,
                    $"ratio {ratio} produced an empty notes panel, which reads as a broken screen.");
            }
        }

        [Test]
        public void NothingInTheReadoutCanLeaveItsScale()
        {
            var profile = Fake(shapeEfficiency: 12.0, computeCost: -50, dataCost: -50,
                memoryNeeded: double.NaN, memoryAvailable: 0.0);

            Assert.AreEqual(1.0, profile.TrainingEfficiency, 1e-9, "Efficiency is a fraction.");
            Assert.GreaterOrEqual(profile.BudgetEfficiency, 0.0);
            Assert.LessOrEqual(profile.BudgetEfficiency, 1.0);
            Assert.IsFalse(double.IsNaN(profile.MemoryPressure));
        }

        private static ShapeProfile ProfileAt(double ratio) => FakeRatio(ratio).Profile;

        private static TrainingProfile FakeRatio(double ratio) =>
            Fake(shapeEfficiency: 0.8, computeCost: 1_000_000, dataCost: 0, ratio: ratio);

        /// <summary>
        /// A projection built by hand rather than planned, so each readout can be moved one at a time.
        /// Planning a real run couples every field to every other one and proves nothing about which
        /// input the bar is actually reading.
        /// </summary>
        private static TrainingProfile Fake(double shapeEfficiency, long computeCost, long dataCost,
            double memoryNeeded = 100.0, double memoryAvailable = 400.0, double ratio = 1.0)
        {
            var projection = new TrainingProjection(
                new ModelBlueprint("Subject", ArchitectureId.DenseTransformer, 20.0, 400.0,
                    DatasetSource.None, ModelType.General),
                isFeasible: true,
                blockingReason: string.Empty,
                projectedLoss: 2.0,
                projectedCapability: 50.0,
                shapeEfficiency: shapeEfficiency,
                tokensPerParameter: 20.0 * ratio,
                optimalTokensPerParameter: 20.0,
                trainingPetaflopDays: 1000.0,
                effectivePetaflops: 10.0,
                trainingDays: 100,
                computeCashCostUsd: computeCost,
                computeEconomicCostUsd: computeCost,
                dataAcquisitionCostUsd: dataCost,
                memoryRequiredGigabytes: memoryNeeded,
                memoryAvailableGigabytes: memoryAvailable,
                blend: default);

            return TrainingProfile.Read(projection);
        }
    }
}
