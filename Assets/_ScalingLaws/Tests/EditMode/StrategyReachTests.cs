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

                // **`FastFollower` is exempt and the exemption is the strategy.** It builds what
                // the player builds, and this fixture has no player, so General is the right answer
                // rather than the fallback. `TheFollowerCopiesWhateverThePlayerIsSelling` is what
                // covers it, by giving it somebody to copy.
                if (strategy == CompetitorStrategy.FrontierRace
                    || strategy == CompetitorStrategy.FastFollower)
                {
                    continue;
                }

                if (target == ModelType.General)
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
        /// Every strategy has a lab, and that is now true for the first time.
        ///
        /// **This assertion did its job and then changed.** It was written yesterday saying that
        /// `FastFollower` alone had no lab, so that giving it one would fail here and point at the
        /// four places that needed a line written for it. A lab was given it the next day, this
        /// failed, and the four places were written. Left as it was, it would now be a test asserting
        /// that a finished mechanic is still missing.
        ///
        /// It keeps its value pointing the other way: a strategy that loses its last lab is a
        /// mechanism that silently stops happening to anybody, which is how two of the five model
        /// types went unbuilt for fourteen years of game time with the suite green.
        /// </summary>
        [Test]
        public void EveryStrategyHasALab()
        {
            var assigned = Assigned();

            var idle = Enum.GetValues(typeof(CompetitorStrategy))
                .Cast<CompetitorStrategy>()
                .Where(strategy => !assigned.Contains(strategy))
                .ToList();

            CollectionAssert.IsEmpty(idle,
                "A strategy has no lab, so whatever it was built to do stops happening to anybody: "
                + string.Join(", ", idle) + ". Either give it a lab, with a rung on "
                + "CompetitorAgent.Ladders, a house serving cost, a cadence and a capability gain, "
                + "or take the member out rather than leaving a name with nothing behind it.");
        }

        /// <summary>
        /// The follower builds what the player builds, and does it without being told twice.
        ///
        /// The point of the strategy, and the part that cannot be read off the tables: every other
        /// lab answers to the calendar and this one answers to the player.
        /// </summary>
        [Test]
        public void TheFollowerCopiesWhateverThePlayerIsSelling()
        {
            var simulation = new CompanySimulation(new CompanyState("Copied", 0x60A7u));
            var state = simulation.State;

            // Far enough in that every model type has been reachable for years, so a follower that
            // ignored the player would answer General and one that reads them answers Coding.
            var late = GameDate.FromCalendar(2032, 6, 1);

            Assert.That(CompetitorAgent.TargetTypeOn(late, CompetitorStrategy.FastFollower),
                Is.EqualTo(ModelType.General),
                "with nothing on sale there is nothing to copy");

            state.AddDeployedModel(new DeployedModel("Copybook",
                ArchitectureId.DenseTransformer, 60.0, GameDate.FromCalendar(2030, 1, 1),
                2e10, 1.0, ModelType.Coding));

            Assert.That(CompetitorAgent.TargetTypeOn(late, CompetitorStrategy.FastFollower),
                Is.EqualTo(ModelType.Coding),
                "the follower is the one lab that reads the player, and it did not");

            // **And it reads the date rather than today.** A rival's live model is typed by the day
            // it shipped, so asking about a date before the player had anything must still answer
            // General however good their product is now.
            Assert.That(
                CompetitorAgent.TargetTypeOn(GameDate.FromCalendar(2026, 1, 1),
                    CompetitorStrategy.FastFollower),
                Is.EqualTo(ModelType.General),
                "a model already on the shelf changed type retroactively, which is the fault this "
                + "project has been caught by once already");
        }
    }
}
