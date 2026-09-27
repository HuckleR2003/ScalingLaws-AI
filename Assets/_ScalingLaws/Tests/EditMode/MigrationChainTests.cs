using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A save from a shipped build opens in the current one, all the way through.
    ///
    /// **Every migration step had a test and the chain of them had none.** Each step is checked
    /// alone, on a file with exactly the fields that step cares about, which is the right way to
    /// test a step and says nothing about what happens when five of them run in order over a real
    /// campaign. That is the only thing a player mid-campaign cares about on the day a build lands.
    ///
    /// This is also the fault `SaveMigration` exists because of. Baka Bake Bakery stamped the
    /// version without reading it, so the field was decorative and there was no upgrade path at
    /// all; it worked perfectly and did nothing, which is the worst kind of bug. A chain of green
    /// steps is not a green chain, the same way a chain of green links is not.
    /// </summary>
    public sealed class MigrationChainTests
    {
        /// <summary>A campaign with something in every drawer, so the walk has work to do.</summary>
        private static CompanySimulation Played(uint seed = 606)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.State.CashUsd = 90_000_000;
            simulation.State.ResearchPoints = 2_400;
            simulation.SetRentedPetaflops(300.0);
            simulation.LearnedToRent();

            simulation.State.AddDeployedModel(new DeployedModel(
                "Atlas One", ArchitectureId.DenseTransformer, 44.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General, family: "Atlas"));

            simulation.State.Staff.SetOffice(OfficeTier.Loft);

            simulation.State.Staff.Add(new Hire(
                PositionCatalog.Get(PlayerSkill.Support).Role, 3, simulation.State.Date, "Ada",
                PlayerSkill.Support, HireSource.Remote, 90.0));

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        /// <summary>
        /// The one that matters: a file written by the shipped build opens in this one.
        ///
        /// **v62 is 0.5.0**, the version players are on today, and the chain from there is five
        /// steps: the support desk, the first ticket, the research the player asked for, the
        /// relations, and the record of what was done with other labs. The campaign has to come out
        /// the other end with its money, its model and its research still in it.
        /// </summary>
        [Test]
        public void ASaveFromTheShippedBuildOpensInThisOne()
        {
            var simulation = Played();
            var data = SaveStore.Capture(simulation.State);

            var cash = data.cashUsd;
            var points = data.researchPoints;
            var nodes = data.unlockedResearch.Count;
            var models = data.models.Count;

            // Stamped back to what 0.5.0 wrote. The fields added since stay on the object, which is
            // what a real file would not have, but every step has to tolerate that anyway: the
            // runner deserialises one shape and walks it forward.
            data.version = 62;

            var json = UnityEngine.JsonUtility.ToJson(data);
            var opened = SaveStore.Parse(json);

            Assert.IsNotNull(opened, "a 0.5.0 campaign no longer opens at all");
            Assert.That(opened.version, Is.EqualTo(SaveData.CurrentVersion),
                "the chain stopped short, which is the fault this whole system exists because of");

            Assert.That(opened.cashUsd, Is.EqualTo(cash), "the money did not survive the walk");
            Assert.That(opened.researchPoints, Is.EqualTo(points));
            Assert.That(opened.unlockedResearch.Count, Is.EqualTo(nodes));
            Assert.That(opened.models.Count, Is.EqualTo(models));

            var state = SaveStore.Restore(opened);

            Assert.That(state.DeployedModels.Count, Is.EqualTo(models),
                "it parsed and then would not load");

            Assert.That(state.CompanyName, Is.EqualTo("Adco"));
        }

        /// <summary>
        /// And it still runs afterwards. Opening is half of it.
        ///
        /// A migrated campaign that loads and then throws on its first tick is a campaign the player
        /// has lost, and the difference is invisible to a test that only parses.
        /// </summary>
        [Test]
        public void AndTheMigratedCampaignStillPlays()
        {
            var data = SaveStore.Capture(Played().State);
            data.version = 62;

            var state = SaveStore.Restore(
                SaveStore.Parse(UnityEngine.JsonUtility.ToJson(data)));

            var simulation = new CompanySimulation(state);

            Assert.DoesNotThrow(() =>
            {
                for (var day = 0; day < 60; day++)
                {
                    simulation.AdvanceDay();
                }
            }, "a campaign opened from 0.5.0 falls over once the clock starts");

            Assert.That(simulation.State.Date.DayIndex, Is.GreaterThan(0));
        }

        /// <summary>
        /// Every version between the oldest shape and today walks to the end.
        ///
        /// **Cheap and it covers the step nobody thought about.** Five steps were added in three
        /// days here; the one that breaks the chain is the one written while somebody was thinking
        /// about something else.
        /// </summary>
        [Test]
        public void EveryVersionFromEveryPointInHistoryReachesTheCurrentOne()
        {
            var broken = new System.Collections.Generic.List<string>();

            for (var from = 40; from < SaveData.CurrentVersion; from++)
            {
                var data = SaveStore.Capture(new CompanyState("Adco", 11u));
                data.version = from;

                var opened = SaveStore.Parse(UnityEngine.JsonUtility.ToJson(data));

                if (opened == null || opened.version != SaveData.CurrentVersion)
                {
                    broken.Add($"v{from} -> {(opened == null ? "unreadable" : opened.version.ToString())}");
                }
            }

            CollectionAssert.IsEmpty(broken,
                "These versions do not reach the current one, so a campaign saved at them is lost: "
                + string.Join(", ", broken));
        }
    }
}
