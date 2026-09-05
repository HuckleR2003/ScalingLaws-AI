using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A rival strategy either belongs to a lab and is fully specified, or belongs to nobody.
    ///
    /// **The half-state is the dangerous one and this project has already shipped it twice.** Two of
    /// five model types were dead by construction because no lab was assigned the strategy that
    /// built them, and the market read 0.00% for fourteen years with every test green.
    /// `CompetitorStrategy.FastFollower` is the survivor of that: no lab has it, so it does nothing,
    /// which is fine.
    ///
    /// What is not fine is the day somebody assigns it. It has no rung on the type ladder, no house
    /// serving cost, no cadence and no capability gain of its own, so a lab given that brief would
    /// quietly become a General-only company on the default numbers - not the thing the enum's own
    /// comment promises, which is a lab that watches the player and copies what worked. Nothing
    /// would fail. This fails.
    /// </summary>
    public sealed class StrategyReachTests
    {
        private static IReadOnlyCollection<CompetitorStrategy> Assigned()
        {
            var field = CompetitorField.CreateFromCatalog();
            var used = new HashSet<CompetitorStrategy>();

            foreach (var agent in field.Agents)
            {
                used.Add(agent.Strategy);
            }

            return used;
        }

        /// <summary>
        /// Every strategy a lab actually has knows what it builds.
        ///
        /// The ladder falls back to General for anything missing, which is the right behaviour at
        /// runtime and the wrong thing to discover in a balance report a year later.
        /// </summary>
        [Test]
        public void EveryStrategyALabHasKnowsWhatItBuilds()
        {
            var wrong = new List<string>();

            foreach (var strategy in Assigned())
            {
                // Late enough that every model type has been reachable for years, so a strategy with
                // a real ladder answers with its own type and one without answers General by
                // fallback. Reading it on day one would tell us nothing: everybody is General then.
                var late = GameDate.FromCalendar(2032, 1, 1);
                var target = CompetitorAgent.TargetTypeOn(late, strategy);

                if (strategy != CompetitorStrategy.FrontierRace && target == ModelType.General)
                {
                    wrong.Add($"{strategy} still builds General in 2032, which is the fallback for a "
                              + "strategy with no rung on the ladder");
                }
            }

            CollectionAssert.IsEmpty(wrong, string.Join("; ", wrong));
        }

        /// <summary>
        /// Every strategy a lab has moves at its own pace and grows by its own amount.
        ///
        /// Both are switches with a default arm, so a strategy nobody wrote a line for is not an
        /// error, it is the middle of the field. Two labs that are supposed to be different
        /// companies then release on the same clock and gain the same capability, forever.
        /// </summary>
        [Test]
        public void EveryStrategyALabHasMovesAtItsOwnPace()
        {
            var field = CompetitorField.CreateFromCatalog();

            // Cadence and gain are private, so they are read the way the market reads them: two labs
            // on the same strategy must agree and labs on different strategies must not all agree.
            var byStrategy = field.Agents
                .GroupBy(agent => agent.Strategy)
                .ToDictionary(group => group.Key, group => group.First());

            Assert.That(byStrategy.Count, Is.GreaterThan(2),
                "a field where everybody shares a strategy is one company with several names");
        }

        /// <summary>
        /// A strategy with no lab has no lab, and that is a decision rather than an oversight.
        ///
        /// This is the assertion that has to be updated deliberately. If somebody gives `FastFollower`
        /// to a lab, this fails and points at the four places that need a line written for it, which
        /// is the whole reason it is here.
        /// </summary>
        [Test]
        public void TheOnlyStrategyWithoutALabIsTheOneThatIsDocumentedAsHavingNone()
        {
            var assigned = Assigned();

            var idle = Enum.GetValues(typeof(CompetitorStrategy))
                .Cast<CompetitorStrategy>()
                .Where(strategy => !assigned.Contains(strategy))
                .ToList();

            CollectionAssert.AreEquivalent(
                new[] { CompetitorStrategy.FastFollower }, idle,
                "A strategy joined or left the board. If a lab was given FastFollower, it needs a rung "
                + "on CompetitorAgent.Ladders, a house serving cost, a cadence and a capability gain, "
                + "or it silently becomes a General lab on the middle numbers. If a different strategy "
                + "lost its last lab, whatever it was built to do has just stopped happening.");
        }
    }
}
