using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The market split by what people are being sold rather than by who they are.
    ///
    /// The Foundation panel draws these numbers, so they have to be true before anything is drawn.
    /// A demographic readout that cannot be wrong is a decoration, and the whole point of this one
    /// is that a player looks at it and decides what to build next.
    /// </summary>
    public sealed class MarketByTypeTests
    {
        private static CompanySimulation RunFor(int days, uint seed = 909)
        {
            var state = new CompanyState("Chartco", seed);
            var simulation = new CompanySimulation(state);

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        private static DeployedModel Ship(CompanySimulation simulation, string name, ModelType type,
            double capability, double price = 1.0)
        {
            var model = new DeployedModel(name, ArchitectureId.DenseTransformer, capability,
                simulation.State.Date, 2e10, price, type);

            simulation.State.AddDeployedModel(model);
            return model;
        }

        [Test]
        public void EveryTypeIsListedAndNobodyIsLostBetweenThem()
        {
            var simulation = RunFor(600);
            var breakdown = simulation.MarketByType();

            Assert.AreEqual(ModelTypeCatalog.All.Count, breakdown.Types.Count,
                "Every type has to appear, even one nobody is selling, or the list silently shrinks.");

            var summed = breakdown.Types.Sum(type => type.TotalUsers);
            Assert.AreEqual(breakdown.TotalUsersOverall, summed, breakdown.TotalUsersOverall * 1e-9 + 1e-6,
                "The types have to add up to the whole market. Anything else is users appearing or "
                + "vanishing between two readouts of the same standing.");
        }

        [Test]
        public void TheListIsSortedByHowManyPeopleAreInIt()
        {
            var breakdown = RunFor(900).MarketByType();

            for (var index = 1; index < breakdown.Types.Count; index++)
            {
                Assert.GreaterOrEqual(breakdown.Types[index - 1].TotalUsers,
                    breakdown.Types[index].TotalUsers,
                    "The panel reads top down, so the biggest category has to be first.");
            }
        }

        [Test]
        public void SharesInsideACategoryAddUpToThatCategory()
        {
            var breakdown = RunFor(900).MarketByType();

            foreach (var standing in breakdown.Types)
            {
                if (standing.TotalUsers <= 0.0)
                {
                    continue;
                }

                var shares = 0.0;
                for (var owner = 0; owner < standing.OwnerUsers.Count; owner++)
                {
                    var share = standing.ShareOf(owner);
                    Assert.GreaterOrEqual(share, 0.0, $"{standing.Type} owner {owner}");
                    shares += share;
                }

                Assert.AreEqual(1.0, shares, 1e-9, $"{standing.Type} does not add up to one market.");
            }
        }

        [Test]
        public void NobodyEverHoldsANegativeOrImpossibleNumberOfUsers()
        {
            var simulation = RunFor(1500);
            var breakdown = simulation.MarketByType();

            Assert.Greater(breakdown.TotalUsersOverall, 0.0, "A live market has people in it.");

            foreach (var standing in breakdown.Types)
            {
                Assert.IsFalse(double.IsNaN(standing.TotalUsers), standing.Type.ToString());
                Assert.GreaterOrEqual(standing.TotalUsers, 0.0, standing.Type.ToString());

                foreach (var held in standing.OwnerUsers)
                {
                    Assert.IsFalse(double.IsNaN(held));
                    Assert.GreaterOrEqual(held, 0.0);
                    Assert.LessOrEqual(held, standing.TotalUsers + 1e-6);
                }
            }
        }

        /// <summary>
        /// The reason the third axis exists, proved by changing exactly one thing.
        ///
        /// Two identical models, same capability, same price, same day, shipped into the same world.
        /// One is general, one is coding. If the type did not reach the market, these would land in
        /// the same place, and the whole split would be decoration.
        /// </summary>
        [Test]
        public void TheSameModelLandsDifferentlyDependingOnWhatItIsFor()
        {
            static (double Coding, double Overall) Play(ModelType type)
            {
                var simulation = RunFor(1200, 909);
                Ship(simulation, "Subject", type, 55.0);

                for (var day = 0; day < 500; day++)
                {
                    simulation.AdvanceDay();
                }

                var breakdown = simulation.MarketByType();
                breakdown.TryGetType(ModelType.Coding, out var coding);
                return (coding.PlayerShare, breakdown.OverallShareOf(0));
            }

            var general = Play(ModelType.General);
            var coding = Play(ModelType.Coding);

            Assert.Greater(coding.Coding, general.Coding * 1.5,
                "A model built for coding has to do meaningfully better inside coding than the same "
                + "model built for everybody. Otherwise the type never reaches the market.");

            Assert.Greater(general.Overall, coding.Overall,
                "And it has to cost something. A general model reaches more people overall, which is "
                + "the trade the player is making when they specialise.");
        }

        [Test]
        public void UsersAreDerivedFromTokensRatherThanInvented()
        {
            // One audience, checked by hand: the pool it holds divided by what one of its people
            // gets through. If these ever disagree, users became a second number that can drift.
            var consumer = AudienceCatalog.Get(AudienceSegment.Consumer);
            var expected = 1.0 * SimUnits.TokensPerBillion / consumer.TokensPerUserPerDay;

            Assert.AreEqual(expected, consumer.UsersFor(1.0), 1e-6);
            Assert.AreEqual(0.0, consumer.UsersFor(-5.0), 1e-9, "Negative demand is not negative people.");
        }

        [Test]
        public void AnAgentUserIsWorthFarMoreTokensThanAConsumer()
        {
            var consumer = AudienceCatalog.Get(AudienceSegment.Consumer).TokensPerUserPerDay;
            var developer = AudienceCatalog.Get(AudienceSegment.Developer).TokensPerUserPerDay;
            var agentic = AudienceCatalog.Get(AudienceSegment.Agentic).TokensPerUserPerDay;

            Assert.Greater(developer, consumer * 5.0,
                "A developer running a model all day is not one casual question a day.");
            Assert.Greater(agentic, developer * 5.0,
                "An unsupervised agent is not a developer either.");
        }

        [Test]
        public void TheStandingSurvivesASaveWithItsTypeAxisIntact()
        {
            var simulation = RunFor(900, 4321);
            Ship(simulation, "Coder", ModelType.Coding, 48.0);

            for (var day = 0; day < 200; day++)
            {
                simulation.AdvanceDay();
            }

            var before = simulation.MarketByType();
            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));
            var after = new CompanySimulation(restored).MarketByType();

            Assert.AreEqual(before.Types.Count, after.Types.Count);

            for (var index = 0; index < before.Types.Count; index++)
            {
                Assert.AreEqual(before.Types[index].Type, after.Types[index].Type,
                    "The order changed, so the standing was not restored as written.");
                Assert.AreEqual(before.Types[index].TotalUsers, after.Types[index].TotalUsers,
                    before.Types[index].TotalUsers * 1e-6 + 1e-6);
                Assert.AreEqual(before.Types[index].PlayerUsers, after.Types[index].PlayerUsers,
                    before.Types[index].PlayerUsers * 1e-6 + 1e-6);
            }
        }

        /// <summary>
        /// A file written before the type axis existed cannot say what its users were being sold, so
        /// it is dropped and rebuilt rather than assigned to a type somebody guessed at.
        /// </summary>
        [Test]
        public void AnOlderStandingIsRebuiltRatherThanGuessedAt()
        {
            var data = new SaveData { version = 13 };
            data.segmentShares.Add(0.5);
            data.segmentOwnerCount = 3;
            data.segmentTypeCount = 2;

            var upgraded = SaveMigration.UpgradeV13ToV14(data);

            Assert.AreEqual(14, upgraded.version);
            Assert.IsEmpty(upgraded.segmentShares);
            Assert.AreEqual(0, upgraded.segmentOwnerCount);
            Assert.IsNotEmpty(SaveMigration.LastMigrationNotes);
        }

        [Test]
        public void ModelTypesUnlockAsOneChainFromEasiestToHardest()
        {
            // The player should never be able to skip to the agent line. Each step is a real gate,
            // and every gate costs calendar as well as money.
            var chain = new[]
            {
                ResearchNodeId.ConversationalModels,
                ResearchNodeId.CodingModels,
                ResearchNodeId.AutomationModels,
                ResearchNodeId.AgenticWorkstation
            };

            for (var index = 1; index < chain.Length; index++)
            {
                var node = ResearchTree.Get(chain[index]);
                Assert.Contains(chain[index - 1], node.Prerequisites.ToArray(),
                    $"{node.DisplayName} does not require the type before it, so the line is not a chain.");

                Assert.GreaterOrEqual(node.EarliestDate.DayIndex,
                    ResearchTree.Get(chain[index - 1]).EarliestDate.DayIndex,
                    $"{node.DisplayName} opens before the node it depends on.");

                Assert.Greater(node.DurationDays, 0,
                    "Money alone must never buy a type. Every unlock costs calendar.");
            }
        }
    }
}
