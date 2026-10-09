using System.IO;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

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

        // ---- what it reserves while it waits ----------------------------------------------------

        private static ResearchNodeId FirstOpenNodeNeedingCompute(CompanySimulation simulation)
        {
            foreach (var standing in simulation.ResearchBoard())
            {
                if (standing.CanStart && standing.Node.PetaflopDaysRequired > 0.0)
                {
                    return standing.Node.Id;
                }
            }

            Assert.Fail("No node on day one asks for any compute, so there is nothing to measure.");
            return ResearchNodeId.None;
        }

        [Test]
        public void ANodeThatHasPaidItsComputeStopsClaimingTheFleet()
        {
            var simulation = Funded(accelerators: 4000);
            var node = FirstOpenNodeNeedingCompute(simulation);

            Assert.IsTrue(simulation.TryStartResearch(node, out var why), why);

            var project = simulation.State.ActiveResearch;

            Assert.IsTrue(simulation.ClusterIsBuildingSomething(),
                "A node that has just begun owes its whole petaflop-day bill and genuinely wants "
                + "the cluster.");

            // A fleet this size pays a node's bill in days, against a calendar asking for months.
            for (var day = 0; day < project.DurationDays && project.WantsCompute; day++)
            {
                simulation.Advance(1);
            }

            Assert.IsFalse(project.WantsCompute,
                "The cluster never finished paying, so this measures nothing.");
            Assert.IsFalse(project.IsComplete,
                "The calendar was supposed to still have months left on it.");

            Assert.IsFalse(simulation.ClusterIsBuildingSomething(),
                "A node waiting out its calendar can spend nothing, so it must reserve nothing. "
                + "While it did, customers were served on what was left of the fleet: the probe "
                + "measured 5,079 days out of 5,110 claimed, on every operator that did anything.");
        }

        [Test]
        public void AndTheCalendarStillTurnsWhileNothingIsClaimingTheCluster()
        {
            var simulation = Funded(accelerators: 4000);
            var node = FirstOpenNodeNeedingCompute(simulation);

            Assert.IsTrue(simulation.TryStartResearch(node, out var why), why);

            var project = simulation.State.ActiveResearch;
            var calendar = project.DurationDays;

            // **The half that cannot be left out.** Dropping the claim is one line; the day count
            // is advanced by the same pass that hands out the compute, so a slice of zero used to
            // mean the pass returned early and the node sat on an unchanging day counter for the
            // rest of the campaign. Without that repair this loop never ends.
            for (var day = 0; day < calendar + 30 && !simulation.State.HasResearch(node); day++)
            {
                simulation.Advance(1);
            }

            Assert.IsTrue(simulation.State.HasResearch(node),
                "The node never landed, so the calendar stopped turning the moment the cluster "
                + "owed it nothing.");
        }

        [Test]
        public void TheSameNodeTakesLessOfABiggerFleet()
        {
            // **The property, rather than a number.** An earlier version of this asserted the
            // share was under half the ceiling on one fleet size and was simply wrong about the
            // arithmetic twice. What the repair actually guarantees is a ratio: the node owes a
            // fixed quantity of petaflop-days, so the larger the cluster the smaller the part of
            // it that quantity represents. Against the version that took the whole share
            // whenever anything was in flight, both readings below are 0.70 and this fails.
            static double ShareWithFleet(int accelerators)
            {
                var simulation = Funded(accelerators);
                var node = FirstOpenNodeNeedingCompute(simulation);

                Assert.IsTrue(simulation.TryStartResearch(node, out var why), why);

                // Read before the clock turns: a large fleet settles a sixty petaflop-day node
                // inside its first day and there is nothing left to measure afterwards.
                return simulation.BuildingShareOfFleet();
            }

            var onASmallFleet = ShareWithFleet(1_000);
            var onALargeFleet = ShareWithFleet(16_000);

            Assert.Greater(onASmallFleet, 0.0,
                "A node that still owes petaflop-days is using some of the cluster.");

            Assert.Less(onALargeFleet, onASmallFleet,
                "The share is a ceiling, not a reservation. The same node owes the same sixty "
                + "petaflop-days whatever it is standing on, so sixteen times the fleet has to "
                + "mean a smaller part of it claimed and the rest left serving customers.");

            Assert.LessOrEqual(onASmallFleet, 0.7000001,
                "Nothing may ever claim more than the ceiling the player set.");
        }

        [Test]
        public void TheComputeScreenReadsTheShareFromTheSimulation()
        {
            // **A source guard, because an EditMode test has no panel to build the screen in.**
            // The row under the slider worked `1 - share` out by hand, which was the same number
            // until the share became a ceiling and then was not: it printed thirty per cent for
            // customers on days they had ninety-eight. The method it lives in has carried a
            // comment saying to read this from the simulation since the day it was written.
            var path = Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "UI", "GameShell.Compute.cs");

            Assert.IsTrue(File.Exists(path), path);
            var source = File.ReadAllText(path);

            Assert.IsTrue(source.Contains("simulation.BuildingShareOfFleet()"),
                "The cluster split panel has to ask the simulation what the fleet is doing.");

            // Narrowed on purpose: the reading beside the slider is legitimately `1 - share`,
            // because that one is reporting where the dial is set. What may not come back is the
            // row saying what the fleet is doing today, which is why the key it used is named.
            Assert.IsFalse(source.Contains("fleet.split_claimed"),
                "That row worked the split out by hand and printed thirty per cent for customers "
                + "on days they had ninety-eight.");
        }
    }
}
