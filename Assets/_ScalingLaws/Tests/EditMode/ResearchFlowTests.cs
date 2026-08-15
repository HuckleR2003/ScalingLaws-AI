using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The whole research flow, from pressing BEGIN to owning what the node opened.
    ///
    /// **A node needs a calendar and a cluster, and only the calendar passes on its own.** That is
    /// the fact this fixture exists for. A company that puts its whole fleet on a training run
    /// reaches the end of a node's duration and stops, and the screen used to say "0 days left,
    /// 30% done" for the rest of the campaign. Nothing was broken and there was no way to tell that
    /// from a hang.
    /// </summary>
    public sealed class ResearchFlowTests
    {
        private static ResearchNodeId FirstOpenNode(CompanySimulation simulation)
        {
            foreach (var standing in simulation.ResearchBoard())
            {
                if (standing.CanStart)
                {
                    return standing.Node.Id;
                }
            }

            Assert.Fail("Nothing can be started on day one, so the tree opens with nothing to do.");
            return ResearchNodeId.None;
        }

        private static CompanySimulation Funded(int accelerators = 800)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 2_000_000_000;
            simulation.State.ResearchPoints = 2_000_000;
            simulation.SetRentedAccelerators(accelerators);
            return simulation;
        }

        // ---- pressing BEGIN ---------------------------------------------------------------------

        [Test]
        public void BeginningANodeChargesBothCurrenciesAndStartsIt()
        {
            var simulation = Funded();
            var node = FirstOpenNode(simulation);
            var definition = ResearchTree.Get(node);

            var cashBefore = simulation.State.CashUsd;
            var pointsBefore = simulation.State.ResearchPoints;

            Assert.IsTrue(simulation.TryStartResearch(node, out var why), why);

            Assert.IsNotNull(simulation.State.ActiveResearch);
            Assert.AreEqual(node, simulation.State.ActiveResearch.Node);

            Assert.Less(simulation.State.CashUsd, cashBefore, "The cash cost was never taken.");
            Assert.Less(simulation.State.ResearchPoints, pointsBefore,
                "The point cost was never taken, which would make research free for anybody who "
                + "had money.");

            Assert.AreEqual(ResearchBudget.CashCostOf(definition.CostUsd), cashBefore - simulation.State.CashUsd);
        }

        [Test]
        public void OnlyOneNodeRunsAtATime()
        {
            var simulation = Funded();
            Assert.IsTrue(simulation.TryStartResearch(FirstOpenNode(simulation), out _));

            var second = ResearchNodeId.None;
            foreach (var standing in simulation.ResearchBoard())
            {
                if (standing.Node.Id != simulation.State.ActiveResearch.Node && standing.CanStart)
                {
                    second = standing.Node.Id;
                    break;
                }
            }

            if (second == ResearchNodeId.None)
            {
                Assert.Pass("Only one node is open on day one, so there is nothing to double up.");
            }

            Assert.IsFalse(simulation.TryStartResearch(second, out var why));
            Assert.IsNotEmpty(why, "A refusal with no reason is a button that does nothing.");
        }

        [Test]
        public void ANodeNobodyCanAffordIsRefusedRatherThanStartedForFree()
        {
            var simulation = Funded();

            // The node is picked while the company can still afford it. CanStart reads the balance,
            // so asking after the money is gone finds nothing and tests nothing.
            var node = FirstOpenNode(simulation);

            simulation.State.ResearchPoints = 0.0;
            simulation.State.CashUsd = 1_000;

            Assert.IsFalse(simulation.TryStartResearch(node, out var why));
            Assert.IsNotEmpty(why);
            Assert.IsNull(simulation.State.ActiveResearch);
        }

        // ---- the days that show on the strip ----------------------------------------------------

        [Test]
        public void TheDayCountMovesAndTheBarMovesWithIt()
        {
            var simulation = Funded();
            Assert.IsTrue(simulation.TryStartResearch(FirstOpenNode(simulation), out _));

            var project = simulation.State.ActiveResearch;
            Assert.AreEqual(0, project.DaysCompleted);

            simulation.Advance(10);

            Assert.GreaterOrEqual(project.DaysCompleted, 10);
            Assert.Greater(project.Progress, 0.0, "Ten days in and the strip would still read zero.");
            Assert.Less(project.Progress, 1.0);
        }

        [Test]
        public void ANodeWithAClusterBehindItFinishesOnItsOwn()
        {
            var simulation = Funded(4000);
            var node = FirstOpenNode(simulation);
            Assert.IsTrue(simulation.TryStartResearch(node, out _));

            for (var day = 0; day < 1200 && simulation.State.ActiveResearch != null; day++)
            {
                simulation.State.CashUsd = 2_000_000_000;
                simulation.Advance(1);
            }

            Assert.IsNull(simulation.State.ActiveResearch, "It never finished.");
            Assert.IsTrue(simulation.State.HasResearch(node));
        }

        // ---- the stall, which is the reason this fixture exists ---------------------------------

        [Test]
        public void ANodeWithNoComputeBehindItSaysSoRatherThanReadingAsAHang()
        {
            // No fleet at all. The calendar still passes and the cluster never pays its share.
            var simulation = Funded(accelerators: 0);
            simulation.SetRentedPetaflops(0.0);

            var node = FirstOpenNode(simulation);
            var definition = ResearchTree.Get(node);

            if (definition.PetaflopDaysRequired <= 0.0)
            {
                Assert.Pass("This node asks for no compute, so it cannot stall.");
            }

            Assert.IsTrue(simulation.TryStartResearch(node, out _));

            var project = simulation.State.ActiveResearch;
            for (var day = 0; day < definition.DurationDays + 60; day++)
            {
                simulation.State.CashUsd = 2_000_000_000;
                simulation.Advance(1);
            }

            Assert.IsNotNull(simulation.State.ActiveResearch, "It should not have finished.");
            Assert.IsTrue(project.IsWaitingForCompute,
                "The calendar ran out and the cluster paid nothing, and the screen has no way of "
                + "saying so unless the project knows it.");

            Assert.Greater(project.PetaflopDaysRemaining, 0.0,
                "And it has to be able to say how much is still owed.");
        }

        [Test]
        public void AProjectStillMovingIsNotReportedAsWaiting()
        {
            var simulation = Funded(4000);
            Assert.IsTrue(simulation.TryStartResearch(FirstOpenNode(simulation), out _));

            var project = simulation.State.ActiveResearch;
            simulation.Advance(5);

            Assert.IsFalse(project.IsWaitingForCompute,
                "A node five days into a four month programme is not waiting on anything.");
        }

        [Test]
        public void ComputeArrivingLateStillFinishesTheNode()
        {
            // The promise the strip makes when it says free some capacity and this finishes on its
            // own. If that were not true the message would be a lie and cancelling would be the
            // only way out.
            var simulation = Funded(accelerators: 0);
            simulation.SetRentedPetaflops(0.0);

            var node = FirstOpenNode(simulation);
            if (ResearchTree.Get(node).PetaflopDaysRequired <= 0.0)
            {
                Assert.Pass("This node asks for no compute.");
            }

            Assert.IsTrue(simulation.TryStartResearch(node, out _));

            for (var day = 0; day < ResearchTree.Get(node).DurationDays + 30; day++)
            {
                simulation.State.CashUsd = 2_000_000_000;
                simulation.Advance(1);
            }

            Assert.IsTrue(simulation.State.ActiveResearch.IsWaitingForCompute);

            simulation.SetRentedAccelerators(4000);
            for (var day = 0; day < 900 && simulation.State.ActiveResearch != null; day++)
            {
                simulation.State.CashUsd = 2_000_000_000;
                simulation.Advance(1);
            }

            Assert.IsNull(simulation.State.ActiveResearch,
                "Renting a cluster has to actually clear the backlog.");
            Assert.IsTrue(simulation.State.HasResearch(node));
        }

        // ---- getting out ------------------------------------------------------------------------

        [Test]
        public void CancellingClearsTheSlotSoTheTreeIsNotBrickedByOneStuckNode()
        {
            var simulation = Funded(accelerators: 0);
            simulation.SetRentedPetaflops(0.0);

            Assert.IsTrue(simulation.TryStartResearch(FirstOpenNode(simulation), out _));
            Assert.IsTrue(simulation.TryCancelResearch(out _));

            Assert.IsNull(simulation.State.ActiveResearch);

            simulation.SetRentedAccelerators(2000);
            Assert.IsTrue(simulation.TryStartResearch(FirstOpenNode(simulation), out var why), why);
        }
    }
}
