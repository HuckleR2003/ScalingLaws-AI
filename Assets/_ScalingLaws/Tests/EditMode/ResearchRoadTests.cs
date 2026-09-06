using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What the research board paints in red when a player clicks a node they cannot start.
    ///
    /// **The blocked reason named the next step and called it the answer.** "Needs Sharded optimizer
    /// states" is true and it is a third of the truth when that node needs two of its own, and the
    /// player found that out by clicking each one in turn across a board of fifty nine. The road is
    /// computed here instead, in `Data/`, so the screen and the card cannot disagree about it and so
    /// the rule can be tested without a panel.
    ///
    /// The interface half is deliberately not tested here: an EditMode test dispatches no clicks.
    /// What is held is the thing a click asks for.
    /// </summary>
    public sealed class ResearchRoadTests
    {
        private static bool Nothing(ResearchNodeId id) => false;

        [Test]
        public void ANodeWithNothingInFrontOfItHasAnEmptyRoad()
        {
            var start = ResearchTree.Get(ResearchTree.StartingNode);

            Assert.That(start.Prerequisites, Is.Empty,
                "the starting node is the one every campaign begins holding; it cannot need anything");

            Assert.That(ResearchTree.MissingPrerequisites(start.Id, Nothing), Is.Empty);
        }

        /// <summary>
        /// The road is the node's own prerequisites and stops there.
        ///
        /// **The first version of this walked the whole chain and this fixture rejected it.** With a
        /// transitive walk, `Continuous oversight` lit sixteen of the fifty nine nodes at once, which
        /// is a quarter of the board in one colour. The deeper rungs are not lost: a red node is
        /// still a node, so clicking it paints its own prerequisites and the board walks the player
        /// back one step at a time.
        /// </summary>
        [Test]
        public void TheRoadIsTheNodesOwnPrerequisitesAndStopsThere()
        {
            // A node deep enough to have a grandparent, found rather than named, so the test does
            // not go stale the first time the tree is re-ordered.
            var withGrandparent = ResearchTree.All
                .Where(node => node.Prerequisites.Count > 0
                    && node.Prerequisites.Any(p => ResearchTree.Get(p).Prerequisites.Count > 0))
                .ToList();

            Assert.That(withGrandparent, Is.Not.Empty, "no node in the tree has a grandparent");

            var deep = withGrandparent[0];
            var road = ResearchTree.MissingPrerequisites(deep.Id, Nothing);
            var direct = deep.Prerequisites;

            Assert.That(road, Is.EquivalentTo(direct),
                "with nothing held, the road is exactly the node's own prerequisites");

            var grandparents = direct
                .SelectMany(p => ResearchTree.Get(p).Prerequisites)
                .Where(g => !direct.Contains(g))
                .ToList();

            Assert.That(grandparents, Is.Not.Empty, "this node was chosen for having grandparents");

            foreach (var grandparent in grandparents)
            {
                Assert.That(road, Has.No.Member(grandparent),
                    $"{ResearchTree.Get(grandparent).DisplayName} is two rungs away and would be a "
                    + "second colour on the board rather than the next thing to do");
            }
        }

        [Test]
        public void APrerequisiteAlreadyHeldIsNotOnTheRoad()
        {
            var node = ResearchTree.All.First(entry => entry.Prerequisites.Count > 0);
            var held = node.Prerequisites[0];

            var road = ResearchTree.MissingPrerequisites(node.Id, id => id == held);

            Assert.That(road, Has.No.Member(held),
                "a node the company already has is not something it must do first");
        }

        /// <summary>
        /// The red never covers the board.
        ///
        /// This is the reason the road is transitive and still readable: if the deepest node in the
        /// tree lit half of it, one colour would stop being information. Measured rather than
        /// asserted by eye, and it fails if the tree ever grows a chain long enough to make the
        /// highlight useless.
        /// </summary>
        [Test]
        public void TheLongestRoadIsStillSomethingAPlayerCanRead()
        {
            var worst = 0;
            var worstName = string.Empty;

            foreach (var node in ResearchTree.All)
            {
                var length = ResearchTree.MissingPrerequisites(node.Id, Nothing).Count;
                if (length > worst)
                {
                    worst = length;
                    worstName = node.DisplayName;
                }
            }

            Assert.That(worst, Is.GreaterThan(0), "no node has any prerequisites at all");
            Assert.That(worst, Is.LessThanOrEqualTo(12),
                $"{worstName} would light {worst} nodes red at once, "
                + "which is a wall rather than a direction");
        }

        [Test]
        public void TheRoadNeverRepeatsANode()
        {
            foreach (var node in ResearchTree.All)
            {
                var road = ResearchTree.MissingPrerequisites(node.Id, Nothing);

                Assert.That(road, Is.Unique,
                    $"{node.DisplayName} lists the same prerequisite twice, so two paths through the "
                    + "tree meet and the walk is counting the meeting point once per path");
            }
        }

        /// <summary>
        /// The road agrees with the simulation's own verdict on whether a node can be started.
        ///
        /// Two answers to one question is the fault this project has hit repeatedly. If the board
        /// paints a road for a node the simulation considers startable, or paints nothing for one it
        /// refuses on prerequisites, the player is being shown a second opinion.
        /// </summary>
        [Test]
        public void AnEmptyRoadNeverContradictsTheSimulation()
        {
            var simulation = new CompanySimulation(new CompanyState("Road", 0x0Au));

            foreach (var standing in simulation.ResearchBoard())
            {
                var road = ResearchTree.MissingPrerequisites(
                    standing.Node.Id, simulation.State.HasResearch);

                if (road.Count == 0)
                {
                    continue;
                }

                Assert.That(standing.CanStart, Is.False,
                    $"{standing.Node.DisplayName} is startable and the board would still paint "
                    + $"{road.Count} nodes red in front of it");
            }
        }
    }
}
