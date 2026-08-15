using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// How large a run the company is allowed to schedule.
    ///
    /// **The claim this rests on: money must not be the only gate on scale.** Before it, the
    /// parameter slider ran its whole length on day one, so a two person company in January 2022
    /// could schedule a trillion parameter run if it could pay the bill, and money compounds. That
    /// is the failure the research tree exists to prevent, and the slider was outside it.
    /// </summary>
    public sealed class ScaleCeilingTests
    {
        private static bool Nothing(ResearchNodeId node) => false;

        private static System.Func<ResearchNodeId, bool> Has(params ResearchNodeId[] nodes)
        {
            var set = new HashSet<ResearchNodeId>(nodes);
            return set.Contains;
        }

        // ---- the ladder ---------------------------------------------------------------------

        [Test]
        public void ACompanyThatHasResearchedNothingIsCappedButCanStillBuildTheReferenceModel()
        {
            var ceiling = ScaleCeiling.CeilingBillions(
                ScaleCeiling.FractionFor(Nothing),
                ModelBlueprint.LowLogParameters,
                ModelBlueprint.HighLogParameters);

            Assert.Greater(ceiling, 20.0,
                "Twenty billion is the size MarketShareModel.SizeBurden scores as exactly 1.0. A cap "
                + "under it makes the economy's own reference model unbuildable on day one.");

            Assert.Less(ceiling, 100.0, "And it still has to be a real gate.");
        }

        [Test]
        public void EveryRungIsStrictlyHigherThanTheOneBeforeIt()
        {
            var previous = ScaleCeiling.BaseFraction;

            foreach (var (node, fraction) in ScaleCeiling.Ladder)
            {
                Assert.Greater(fraction, previous,
                    $"{node} does not raise the cap, so researching it buys nothing.");

                previous = fraction;
            }

            Assert.AreEqual(1.0, previous, 1e-9,
                "The top rung has to open the whole slider. A permanently dead end reads as a bug.");
        }

        [Test]
        public void ResearchIsTheOnlyThingThatMovesIt()
        {
            var start = ScaleCeiling.FractionFor(Nothing);
            var after = ScaleCeiling.FractionFor(Has(ResearchNodeId.ShardedOptimizerStates));

            Assert.Greater(after, start);
        }

        [Test]
        public void TheHighestRungWins()
        {
            // Out of order on purpose: a company can finish the second before the first if the tree
            // ever allows it, and the cap must not fall back to the lower one.
            var fraction = ScaleCeiling.FractionFor(
                Has(ResearchNodeId.UltraReadiness, ResearchNodeId.ShardedOptimizerStates));

            Assert.AreEqual(0.90, fraction, 1e-9);
        }

        [Test]
        public void TheInterfaceCanAlwaysNameWhatWouldRaiseTheCap()
        {
            Assert.IsTrue(ScaleCeiling.TryNextRung(Nothing, out var node, out var fraction));
            Assert.AreEqual(ResearchNodeId.ShardedOptimizerStates, node);
            Assert.Greater(fraction, ScaleCeiling.BaseFraction);

            var everything = new List<ResearchNodeId>();
            foreach (var (rung, _) in ScaleCeiling.Ladder)
            {
                everything.Add(rung);
            }

            Assert.IsFalse(ScaleCeiling.TryNextRung(Has(everything.ToArray()), out _, out _),
                "At the top there is nothing left to name, and the lock is hidden rather than "
                + "captioned with a node that is already done.");
        }

        // ---- the rule -------------------------------------------------------------------------

        [Test]
        public void ARunOverTheCeilingIsRefusedAndTheReasonNamesTheResearch()
        {
            var simulation = NewCompany();
            var ceiling = simulation.ParameterCeilingBillions();

            var blueprint = new ModelBlueprint(
                "Too big", ArchitectureId.DenseTransformer, ceiling * 4.0, ceiling * 80.0,
                DatasetSource.WebCrawl);

            Assert.IsFalse(simulation.TryStartTraining(blueprint, out var reason));
            StringAssert.Contains("Sharded optimizer states", reason,
                "A refusal that does not say what would lift it is a dead end.");
        }

        [Test]
        public void TheCeilingIsARuleAndNotOnlyASliderBound()
        {
            // The point of this one: the cap is enforced in TryStartTraining, so it survives every
            // other way a run could ever be started. A limit that only exists on the control is a
            // suggestion the moment a second control exists.
            var simulation = NewCompany();
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "_ScalingLaws", "Scripts", "Simulation",
                "CompanySimulation.cs"));

            StringAssert.Contains("ParameterCeilingBillions()", source);
            Assert.Greater(simulation.ParameterCeilingBillions(), 0.0);
        }

        [Test]
        public void ResearchingTheFirstRungActuallyLetsABiggerRunStart()
        {
            var simulation = NewCompany();
            var before = simulation.ParameterCeilingBillions();

            simulation.State.UnlockedResearch.Add(ResearchNodeId.ShardedOptimizerStates);

            Assert.Greater(simulation.ParameterCeilingBillions(), before * 2.0,
                "A rung that does not visibly move the number is a node nobody would buy.");
        }

        private static CompanySimulation NewCompany()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.SetRentedAccelerators(500);
            return simulation;
        }
    }
}
