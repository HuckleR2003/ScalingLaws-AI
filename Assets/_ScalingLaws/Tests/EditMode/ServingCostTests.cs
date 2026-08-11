using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Size has to cost something after the training bill is paid.
    ///
    /// Until 2026-08-11 it did not. <c>InferenceFlopPerToken</c> scaled with active parameters and
    /// billed the player, but the market's burden term dropped size entirely, so a ten times larger
    /// model was exactly as cheap in the audience's eyes. That made the Scale stage consequence free
    /// past the moment the run finished, and made the panel's own warning that an oversized model
    /// would be expensive to serve later a false statement.
    /// </summary>
    public sealed class ServingCostTests
    {
        [Test]
        public void TheReferenceSizeIsExactlyNeutral()
        {
            // Same discipline as ReachFactor and ToleranceFactor: the baseline must sit at exactly one
            // forever, so adding this term is not a silent rebalance of everything tuned before it.
            Assert.AreEqual(1.0,
                MarketShareModel.SizeBurden(MarketShareModel.ReferenceActiveParameters), 1e-12,
                "A twenty billion parameter model is the reference and must score exactly 1.0.");
        }

        [Test]
        public void BiggerCostsMoreAndSmallerCostsLessWithoutException()
        {
            var sizes = new[] { 1e8, 1e9, 5e9, 2e10, 1e11, 5e11, 2e12 };
            var previous = 0.0;

            foreach (var size in sizes)
            {
                var burden = MarketShareModel.SizeBurden(size);

                Assert.Greater(burden, previous,
                    $"{size:0.0e0} parameters did not cost more than the size below it.");

                previous = burden;
            }

            Assert.Less(MarketShareModel.SizeBurden(1e9), 1.0, "A one billion model is cheap to serve.");
            Assert.Greater(MarketShareModel.SizeBurden(4e11), 1.0, "A four hundred billion model is not.");
        }

        [Test]
        public void NoSizeHoweverAbsurdProducesANonsenseBurden()
        {
            foreach (var size in new[] { 0.0, -1e12, double.NaN, double.PositiveInfinity, 1e30 })
            {
                var burden = MarketShareModel.SizeBurden(size);

                Assert.IsFalse(double.IsNaN(burden), $"{size} produced NaN");
                Assert.Greater(burden, 0.0, size.ToString());
                Assert.LessOrEqual(burden, 8.0, size.ToString());
            }
        }

        /// <summary>
        /// The consequence, measured through the real market rather than asserted on the formula.
        ///
        /// Two models identical in every way the audience can see, capability, price, type, age and
        /// architecture, differing only in how many parameters have to run for every token. The smaller
        /// one has to end up holding more of an audience that cares what serving costs.
        /// </summary>
        [Test]
        public void TheSmallerOfTwoIdenticalModelsWinsTheCostSensitiveAudience()
        {
            static double Play(double activeParameters)
            {
                var simulation = new CompanySimulation(new CompanyState("Sizeco", 808));
                for (var day = 0; day < 500; day++)
                {
                    simulation.AdvanceDay();
                }

                simulation.State.AddDeployedModel(new DeployedModel(
                    "Subject", ArchitectureId.DenseTransformer, 58.0,
                    simulation.State.Date, activeParameters, 1.0, ModelType.General));

                for (var day = 0; day < 400; day++)
                {
                    simulation.AdvanceDay();
                }

                // Autonomous work is the audience that pays the most attention to serving cost.
                return simulation.State.Segments.PlayerShareIn(AudienceSegment.Agentic);
            }

            var lean = Play(5e9);
            var heavy = Play(8e11);

            Assert.Greater(lean, heavy,
                $"A 5B model held {lean:P2} and an 800B model held {heavy:P2} of the same audience at "
                + "the same capability and the same price. If these are equal, size still costs "
                + "nothing and the Scale stage ends at the training bill.");
        }

        /// <summary>
        /// And it must not be a free win either. The larger model is only being punished on cost, so
        /// an audience that barely cares about cost should not separate them nearly as much.
        /// </summary>
        [Test]
        public void AnAudienceThatDoesNotCareAboutCostBarelySeparatesThem()
        {
            var agentic = AudienceCatalog.Get(AudienceSegment.Agentic).ServingCostWeight;
            var enterprise = AudienceCatalog.Get(AudienceSegment.Enterprise).ServingCostWeight;

            Assert.Greater(agentic, enterprise,
                "This test assumes autonomous work is more cost sensitive than enterprise. If that "
                + "changed in the catalog, the test above is measuring the wrong audience.");
        }
    }
}
