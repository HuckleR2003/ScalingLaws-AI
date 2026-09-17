using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// DEBUG MODE: a campaign with every node researched and the bank full, for looking at screens.
    ///
    /// Two things about it are worth a fixture rather than a comment. The record of what a player
    /// has achieved lives outside the save and cannot be taken back, so a sandbox must never write
    /// to it. And a node marked as done that hands over nothing is the fault this project has now
    /// shipped eleven times, so the unlocking goes through the same granting a finished node does.
    /// </summary>
    public sealed class SandboxTests
    {
        private static CompanyState Fresh() =>
            CompanyState.FromOpeningChoice("Sandbox Labs", CompanyArchetype.Custom,
                FounderTrait.None, FounderTrait.None);

        [Test]
        public void ASandboxCampaignEarnsNothingHoweverWellItIsDoing()
        {
            var state = Fresh();
            state.CashUsd = 900_000_000L;

            Assert.IsNotEmpty(AchievementEvaluator.Satisfied(state, 0),
                "a campaign holding nine hundred million satisfies something, or this test proves nothing");

            state.IsSandbox = true;

            Assert.IsEmpty(AchievementEvaluator.Satisfied(state, 0),
                "the same campaign in a sandbox has to earn nothing at all");
        }

        [Test]
        public void UnlockingEverythingHandsOverWhatTheNodesPromised()
        {
            var state = Fresh();
            var simulation = new CompanySimulation(state);

            var corporaBefore = CountCorpora(state);
            var familiesBefore = state.AdoptedArchitectures.Count;

            simulation.UnlockEveryResearchNode();

            Assert.AreEqual(ResearchTree.All.Count, state.UnlockedResearch.Count,
                "every node in the tree is held");

            Assert.Greater(CountCorpora(state), corporaBefore,
                "a tree with every corpus node finished has to leave the company holding the corpora, "
                + "or this is the eleventh mechanism that reports itself done and delivers nothing");

            Assert.Greater(state.AdoptedArchitectures.Count, familiesBefore,
                "and the architecture families the same nodes open");
        }

        [Test]
        public void UnlockingEverythingTwiceChangesNothingTheSecondTime()
        {
            var state = Fresh();
            var simulation = new CompanySimulation(state);

            simulation.UnlockEveryResearchNode();
            var corpora = CountCorpora(state);
            var families = state.AdoptedArchitectures.Count;

            simulation.UnlockEveryResearchNode();

            Assert.AreEqual(corpora, CountCorpora(state));
            Assert.AreEqual(families, state.AdoptedArchitectures.Count,
                "a family adopted twice would be listed twice");
        }

        [Test]
        public void TheFlagSurvivesASaveBecauseALoadedSandboxIsStillOne()
        {
            var state = Fresh();
            state.IsSandbox = true;

            var back = RoundTrip(state);

            Assert.IsTrue(back.IsSandbox,
                "a sandbox that came back as an ordinary campaign would start earning achievements "
                + "on its first tick, which is exactly what this flag exists to prevent");
        }

        [Test]
        public void AnOrdinaryCampaignIsNotASandbox()
        {
            Assert.IsFalse(RoundTrip(Fresh()).IsSandbox);
        }

        [Test]
        public void AnOlderSaveIsNotASandbox()
        {
            var data = SaveStore.Capture(Fresh());
            data.version = 57;
            data.isSandbox = true; // whatever an older file happens to hold in the new field

            var upgraded = SaveMigration.UpgradeV57ToV58(data);

            Assert.AreEqual(58, upgraded.version);
            Assert.IsFalse(upgraded.isSandbox,
                "a v57 campaign was played in a game with no DEBUG MODE in it, so it was not one");
        }

        /// <summary>Through the json, like the game does, rather than through the object in memory.</summary>
        private static CompanyState RoundTrip(CompanyState state) =>
            SaveStore.Restore(SaveStore.Parse(UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state))));

        private static int CountCorpora(CompanyState state)
        {
            var held = 0;

            foreach (var corpus in DatasetCatalog.All)
            {
                if ((state.OwnedDataSources & corpus.Flag) == corpus.Flag)
                {
                    held++;
                }
            }

            return held;
        }
    }
}
