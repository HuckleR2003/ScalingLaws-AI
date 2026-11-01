using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The opening trade: money the founder has against things they already know how to do.
    ///
    /// These hold the shape and the consistency of the ladder. Whether the trade is *balanced* is
    /// a different question answered by a different kind of test, and it lives in
    /// `PlayabilityTests`, which plays both ends for four years.
    /// </summary>
    public sealed class FoundingTradeTests
    {
        [Test]
        public void TheDefaultPositionIsTheGameThatWasAlreadyBalanced()
        {
            Assert.That(FoundingTrade.NodesFor(FoundingTrade.DefaultCashUsd), Is.Empty,
                "A player who never touches this control has to start exactly where every "
                + "campaign before it started.");
        }

        [Test]
        public void TheFloorBuysTheWholeLadderAndNotADollarMore()
        {
            Assert.That(FoundingTrade.NodesFor(FoundingTrade.LeastCashUsd),
                Is.EquivalentTo(FoundingTrade.Ladder));

            Assert.That(FoundingTrade.LeastCashUsd + FoundingTrade.LadderTotalUsd,
                Is.EqualTo(FoundingTrade.DefaultCashUsd),
                "The floor is derived from the ladder. If these drift apart there is a stretch of "
                + "slider where moving the handle changes nothing, which reads as a broken control.");
        }

        [Test]
        public void GivingUpMoreMoneyNeverTakesKnowledgeAway()
        {
            var previous = 0;

            for (var cash = FoundingTrade.DefaultCashUsd;
                 cash >= FoundingTrade.LeastCashUsd;
                 cash -= 100_000)
            {
                var known = FoundingTrade.NodesFor(cash).Count;

                Assert.That(known, Is.GreaterThanOrEqualTo(previous),
                    $"Opening on {cash:N0} knows less than opening on more.");

                previous = known;
            }
        }

        /// <summary>
        /// **Nothing is granted before the world has it.** The honesty flag runs through this whole
        /// project: a founder who turns up knowing a 2023 technique in January 2022 is the game
        /// inventing history rather than letting the player start ahead of it.
        /// </summary>
        [Test]
        public void EveryRungExistsOnTheDayTheCampaignStarts()
        {
            foreach (var id in FoundingTrade.Ladder)
            {
                var node = ResearchTree.Get(id);

                Assert.That(node.IsAvailableOn(GameDate.Start), Is.True,
                    $"{id} is not published on {GameDate.Start}, so a founder cannot already "
                    + "know it.");
            }
        }

        /// <summary>
        /// A rung whose prerequisite is not also on the ladder leaves the company holding a node it
        /// could never have reached, which `ConsistencyTests` is right to treat as a broken tree.
        /// </summary>
        [Test]
        public void TheLadderIsPrerequisiteClosedAndInOrder()
        {
            // Seeded with what every company already owns, or the first rung reads as unreachable.
            var seen = new System.Collections.Generic.HashSet<ResearchNodeId>
            {
                ResearchTree.StartingNode
            };

            foreach (var id in FoundingTrade.Ladder)
            {
                foreach (var prerequisite in ResearchTree.Get(id).Prerequisites)
                {
                    Assert.That(seen, Does.Contain(prerequisite),
                        $"{id} needs {prerequisite}, which is not earlier on the ladder. A founder "
                        + "would open holding a node they could not have reached.");
                }

                seen.Add(id);
            }
        }

        /// <summary>
        /// **The rung that justifies the control has to arrive early.** Lifting the parameter
        /// ceiling is the one head start a player sees on day one. If it sat at the bottom of the
        /// ladder the trade would only pay for somebody who pushed the slider all the way down,
        /// and every position above that would be a worse version of not touching it.
        /// </summary>
        [Test]
        public void TheCeilingLiftIsInTheFirstHalfOfTheLadder()
        {
            var index = FoundingTrade.Ladder
                .ToList()
                .IndexOf(ResearchNodeId.ShardedOptimizerStates);

            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                "The ladder no longer contains the node that lifts the parameter ceiling, which "
                + "was the whole reason to spend founding capital on knowledge.");

            Assert.That(index, Is.LessThan(FoundingTrade.Ladder.Count / 2 + 1),
                $"It sits at rung {index + 1} of {FoundingTrade.Ladder.Count}, so most of the "
                + "slider buys nothing a player can see.");
        }

        [Test]
        public void NothingOutsideTheSliderIsReachable()
        {
            Assert.That(FoundingTrade.NodesFor(FoundingTrade.DefaultCashUsd + 50_000_000), Is.Empty,
                "More cash than the default must not read as owing knowledge back.");

            Assert.That(FoundingTrade.NodesFor(0), Is.EquivalentTo(FoundingTrade.Ladder),
                "Below the floor is the floor, not an unbounded head start.");
        }

        /// <summary>
        /// Opening a company is the one path both the creator and the sweep take, so what it hands
        /// over has to match what the slider advertises.
        /// </summary>
        [Test]
        public void OpeningACompanyHandsOverExactlyWhatTheLadderSays()
        {
            var state = new CompanyState("Prometheus AI");
            FoundingTrade.Open(state, FoundingTrade.LeastCashUsd);

            Assert.That(state.CashUsd, Is.EqualTo(FoundingTrade.LeastCashUsd));

            foreach (var id in FoundingTrade.Ladder)
            {
                Assert.That(state.UnlockedResearch, Does.Contain(id),
                    $"The slider promised {id} and the company does not have it.");
            }
        }

        [Test]
        public void OpeningAtTheDefaultChangesNothingAtAll()
        {
            var untouched = new CompanyState("Prometheus AI");

            var opened = new CompanyState("Prometheus AI");
            FoundingTrade.Open(opened, FoundingTrade.DefaultCashUsd);

            Assert.That(opened.CashUsd, Is.EqualTo(untouched.CashUsd));
            Assert.That(opened.UnlockedResearch.Count, Is.EqualTo(untouched.UnlockedResearch.Count));
        }
    }
}
