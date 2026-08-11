using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Product lines.
    ///
    /// The rule they exist to carry: one line is one product, so only its strongest live model reaches
    /// the market. Without it a company raised its standing by never withdrawing anything, because
    /// every live model added its own score to the same bucket and shipping often beat shipping well.
    /// </summary>
    public sealed class ModelFamilyTests
    {
        private static CompanySimulation RunFor(int days, uint seed = 616)
        {
            var simulation = new CompanySimulation(new CompanyState("Lineco", seed));
            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        private static DeployedModel Ship(CompanySimulation simulation, string name, double capability,
            string family = null)
        {
            var model = new DeployedModel(name, ArchitectureId.DenseTransformer, capability,
                simulation.State.Date, 2e10, 1.0, ModelType.General, family);

            simulation.State.AddDeployedModel(model);
            return model;
        }

        [Test]
        public void AModelThatJoinsNoLineStartsOneNamedAfterItself()
        {
            var model = new DeployedModel("Prometheus", ArchitectureId.DenseTransformer, 40.0,
                GameDate.FromCalendar(2023, 1, 1), 2e10, 1.0);

            Assert.AreEqual("Prometheus", model.Family,
                "A model belonging to nothing could never be superseded, and the line picker would "
                + "have stayed empty however many models were released.");
        }

        [Test]
        public void TwoModelsInOneLineAreOneProductAndTheWeakerStopsCompeting()
        {
            var simulation = RunFor(600);

            Ship(simulation, "Prometheus 1", 40.0, "Prometheus");
            var withOne = Users(simulation, 250);

            var simulationTwo = RunFor(600);
            Ship(simulationTwo, "Prometheus 1", 40.0, "Prometheus");
            Ship(simulationTwo, "Prometheus 2", 40.0, "Prometheus");
            var withTwo = Users(simulationTwo, 250);

            Assert.AreEqual(withOne, withTwo, withOne * 0.02 + 1.0,
                $"One model held {withOne:N0} and two models in the same line held {withTwo:N0}. "
                + "A buyer choosing between your last two releases is not two chances at their "
                + "business, so a second model in the same line must not add standing.");
        }

        [Test]
        public void TwoSeparateLinesAreTwoProductsAndBothCompete()
        {
            var one = RunFor(600);
            Ship(one, "Prometheus", 40.0, "Prometheus");
            var single = Users(one, 250);

            var two = RunFor(600);
            Ship(two, "Prometheus", 40.0, "Prometheus");
            Ship(two, "Chronos", 40.0, "Chronos");
            var pair = Users(two, 250);

            Assert.Greater(pair, single * 1.05,
                $"One line held {single:N0} and two separate lines held {pair:N0}. Two genuinely "
                + "different products do reach more people, which is the trade against the line "
                + "rule above.");
        }

        [Test]
        public void TheStrongerModelInALineIsTheOneOnSale()
        {
            var simulation = RunFor(600);

            var weak = Ship(simulation, "Prometheus 1", 30.0, "Prometheus");
            var strong = Ship(simulation, "Prometheus 2", 62.0, "Prometheus");

            var entrants = simulation.SegmentStandings();
            Assert.IsNotNull(entrants, "Standings must build with two models in one line.");

            // Measured through the market: the line performs as the strong model, not the weak one.
            var withBoth = Users(simulation, 250);

            var alone = RunFor(600);
            Ship(alone, "Prometheus 2", 62.0, "Prometheus");
            var strongOnly = Users(alone, 250);

            Assert.AreEqual(strongOnly, withBoth, strongOnly * 0.02 + 1.0,
                $"The line held {withBoth:N0} with an old weak model still listed and {strongOnly:N0} "
                + "without it. The weaker one must not drag the line down or prop it up.");

            Assert.Greater(strong.Capability, weak.Capability);
        }

        [Test]
        public void ALineSurvivesASaveWithItsMembersIntact()
        {
            var simulation = RunFor(500);
            Ship(simulation, "Prometheus 1", 38.0, "Prometheus");
            Ship(simulation, "Prometheus 2", 55.0, "Prometheus");
            Ship(simulation, "Chronos", 44.0, "Chronos");

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(3, restored.DeployedModels.Count);

            for (var index = 0; index < 3; index++)
            {
                Assert.AreEqual(simulation.State.DeployedModels[index].Family,
                    restored.DeployedModels[index].Family,
                    "A line that does not survive a save silently splits into separate products.");
            }
        }

        /// <summary>
        /// Found while adding lines, and older than them.
        ///
        /// v14 never wrote the model type into the active run at all, so saving during training and
        /// reloading turned a coding model into a general one. Nothing failed and nothing said so.
        /// </summary>
        [Test]
        public void ARunInFlightRemembersWhatItIsBuilding()
        {
            var simulation = RunFor(400);
            simulation.State.UnlockedResearch.Add(
                ModelTypeCatalog.Get(ModelType.Coding).Requires);

            var blueprint = new ModelBlueprint("Coder", ArchitectureId.DenseTransformer, 12.0, 240.0,
                DatasetSource.None, ModelType.Coding, "Coder");

            var data = SaveStore.Capture(simulation.State);
            data.hasActiveRun = true;
            data.activeRun.blueprintName = blueprint.Name;
            data.activeRun.architecture = (int)blueprint.Architecture;
            data.activeRun.parameterCountBillions = blueprint.ParameterCountBillions;
            data.activeRun.trainingTokensBillions = blueprint.TrainingTokensBillions;
            data.activeRun.dataSources = (int)blueprint.DataSources;
            data.activeRun.modelType = (int)blueprint.Type;
            data.activeRun.family = blueprint.Family;
            data.activeRun.petaflopDaysRequired = 1000.0;

            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(data)));

            Assert.IsNotNull(restored.ActiveRun, "The run itself has to come back.");
            Assert.AreEqual(ModelType.Coding, restored.ActiveRun.Blueprint.Type,
                "A run in flight came back as a general model, so months of training silently "
                + "changed what was being built.");
            Assert.AreEqual("Coder", restored.ActiveRun.Blueprint.Family);
        }

        [Test]
        public void AnOlderFileGivesEveryModelALineOfItsOwn()
        {
            var data = new SaveData { version = 14 };
            data.models.Add(new DeployedModelData { name = "Old one", family = null });
            data.models.Add(new DeployedModelData { name = "Old two", family = null });

            var upgraded = SaveMigration.UpgradeV14ToV15(data);

            Assert.AreEqual(15, upgraded.version);
            Assert.IsNotEmpty(SaveMigration.LastMigrationNotes);

            foreach (var model in upgraded.models)
            {
                Assert.AreEqual(string.Empty, model.family,
                    "Grouping old models by name would look tidier and would be a guess, and it "
                    + "would withdraw models the player still has on sale.");
            }
        }

        [Test]
        public void EveryBlueprintEditKeepsTheLine()
        {
            // Same rule the type lives under. A single omission would move a successor out of its own
            // line the moment the player renamed it.
            var blueprint = new ModelBlueprint("Prometheus 2", ArchitectureId.DenseTransformer, 20.0,
                400.0, DatasetSource.None, ModelType.General, "Prometheus");

            Assert.AreEqual("Prometheus", blueprint.WithName("Renamed").Family);
            Assert.AreEqual("Prometheus", blueprint.WithParameters(40.0).Family);
            Assert.AreEqual("Prometheus", blueprint.WithTokens(900.0).Family);
            Assert.AreEqual("Prometheus",
                blueprint.WithArchitecture(ArchitectureId.SparseMixture).Family);
            Assert.AreEqual("Prometheus", blueprint.WithType(ModelType.Coding).Family);
            Assert.AreEqual("Prometheus", blueprint.WithDataSources(DatasetSource.None).Family);
            Assert.AreEqual("Chronos", blueprint.WithFamily("Chronos").Family);
        }

        private static double Users(CompanySimulation simulation, int days)
        {
            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation.Sentiment().Users;
        }
    }
}
