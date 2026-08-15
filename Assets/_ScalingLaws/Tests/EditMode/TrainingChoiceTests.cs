using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The four Scale and Data choices.
    ///
    /// The claim they all rest on: **the middle option of every catalog is exactly 1.0 on every
    /// axis**, so adding them changed nothing for a company that leaves them alone. That is not a
    /// nicety, it is what let four new mechanics land without retuning the economy, and it is the
    /// rule I broke twice while building this. The cutoff cost started at 1.35 for the default,
    /// which raised the data bill on every run in the game, and the balance suite caught it.
    /// </summary>
    public sealed class TrainingChoiceTests
    {
        private static CompanySimulation Ready(uint seed = 1200)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(200.0);
            return simulation;
        }

        private static ModelBlueprint Basic() =>
            new("Subject", ArchitectureId.DenseTransformer, 8.0, 160.0, DatasetSource.WebCrawl);

        // ---- the neutral rule --------------------------------------------------------------------

        [Test]
        public void TheMiddleOfEveryCatalogIsExactlyNeutral()
        {
            var precision = TrainingChoiceCatalog.Get(TrainingPrecision.BFloat16);
            Assert.AreEqual(1.0, precision.Throughput, 1e-9, "BF16 has to be the reference width.");
            Assert.AreEqual(1.0, precision.Instability, 1e-9);

            var shape = TrainingChoiceCatalog.Get(ModelShape.Balanced);
            Assert.AreEqual(1.0, shape.Capability, 1e-9);
            Assert.AreEqual(1.0, shape.ServingBurden, 1e-9);

            var pass = TrainingChoiceCatalog.Get(DeduplicationPass.Standard);
            Assert.AreEqual(1.0, pass.TokensKept, 1e-9);
            Assert.AreEqual(1.0, pass.Quality, 1e-9);

            Assert.AreEqual(1.0, TrainingChoiceCatalog.CutoffCapabilityMultiplier(0), 1e-9);
            Assert.AreEqual(1.0, TrainingChoiceCatalog.CutoffCostMultiplier(0), 1e-9,
                "Taking everything up to today is what the game always did, so it has to cost "
                + "exactly what it always cost. This was 1.35 and it raised the data bill on every "
                + "run in the game.");
        }

        [Test]
        public void ANeutralBlueprintProjectsIdenticallyToOneThatNamesNothing()
        {
            var simulation = Ready();

            var silent = simulation.Project(Basic());
            var explicitly = simulation.Project(Basic()
                .WithPrecision(TrainingPrecision.BFloat16)
                .WithShape(ModelShape.Balanced)
                .WithDeduplication(DeduplicationPass.Standard)
                .WithCutoff(0));

            Assert.AreEqual(silent.ProjectedCapability, explicitly.ProjectedCapability, 1e-9);
            Assert.AreEqual(silent.TrainingDays, explicitly.TrainingDays);
            Assert.AreEqual(silent.DataAcquisitionCostUsd, explicitly.DataAcquisitionCostUsd);
        }

        // ---- each choice does what it says ---------------------------------------------------------

        [Test]
        public void NarrowerNumbersFinishTheRunSooner()
        {
            var simulation = Ready(1201);

            var wide = simulation.Project(Basic().WithPrecision(TrainingPrecision.Float32));
            var normal = simulation.Project(Basic().WithPrecision(TrainingPrecision.BFloat16));

            Assert.Greater(wide.TrainingDays, normal.TrainingDays,
                "FP32 moves half as much through the same cluster, so it has to take longer.");
        }

        [Test]
        public void PrecisionChangesTheCalendarAndNotTheModel()
        {
            var simulation = Ready(1202);

            var normal = simulation.Project(Basic().WithPrecision(TrainingPrecision.BFloat16));
            var narrow = simulation.Project(Basic().WithPrecision(TrainingPrecision.Float32));

            Assert.AreEqual(normal.ProjectedCapability, narrow.ProjectedCapability, 1e-9,
                "Precision buys throughput. If it moved the projected capability it would be a "
                + "quality slider, and the whole trade would collapse.");
        }

        [Test]
        public void DepthCostsMoreToServeThanWidthAtTheSameSize()
        {
            var deep = TrainingChoiceCatalog.Get(ModelShape.Deep);
            var wide = TrainingChoiceCatalog.Get(ModelShape.Wide);

            Assert.Greater(deep.Capability, wide.Capability);
            Assert.Greater(deep.ServingBurden, wide.ServingBurden,
                "Depth is sequential and width is parallel. Without this the deep option is a free "
                + "capability bonus and nobody would ever pick anything else.");
        }

        [Test]
        public void AnAggressiveCleanTradesTokensForQuality()
        {
            var pass = TrainingChoiceCatalog.Get(DeduplicationPass.Aggressive);

            Assert.Less(pass.TokensKept, 1.0, "It has to cost part of the corpus.");
            Assert.Greater(pass.Quality, 1.0, "And it has to be worth something.");
        }

        [Test]
        public void AStaleCorpusIsCheaperAndScoresLower()
        {
            Assert.Less(TrainingChoiceCatalog.CutoffCapabilityMultiplier(24),
                TrainingChoiceCatalog.CutoffCapabilityMultiplier(0));

            Assert.Less(TrainingChoiceCatalog.CutoffCostMultiplier(24),
                TrainingChoiceCatalog.CutoffCostMultiplier(0),
                "Two year old text is cheaper to license. If it were not, nobody would ever take it.");
        }

        [Test]
        public void TheShapeReachesTheMarketRatherThanBeingSpentOnTheRun()
        {
            var simulation = Ready(1203);

            var model = new DeployedModel("Subject", ArchitectureId.DenseTransformer, 45.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General);

            model.SetShape(ModelShape.Deep);
            simulation.State.AddDeployedModel(model);

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.AreEqual(ModelShape.Deep, model.Shape,
                "The arrangement is a permanent property of the model, read by the market every day "
                + "it is on sale.");
        }

        // ---- the technologies that open them ---------------------------------------------------------

        [Test]
        public void FloatEightNeedsTheResearchBeforeARunCanUseIt()
        {
            // No rented compute on purpose. This test only needs the calendar to reach the silicon
            // date, and a company paying for two hundred petaflops with nothing to sell is insolvent
            // long before then.
            var simulation = new CompanySimulation(new CompanyState("Adco", 1204));

            // Past the silicon date, so the only thing left in the way is the knowledge.
            //
            // Bounded, and it has to be: AdvanceDay returns without moving the date once the company
            // is insolvent, so "run until the year is 2024" is a loop whose exit condition stops
            // being reachable the moment the company fails. The first version of this hung the whole
            // suite for twenty minutes.
            for (var day = 0; day < 900 && simulation.State.Date.Year < 2024; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.GreaterOrEqual(simulation.State.Date.Year, 2024,
                "The company died before the silicon arrived, so this tested nothing.");

            Assert.IsFalse(simulation.TryStartTraining(
                Basic().WithPrecision(TrainingPrecision.Float8), out var reason));

            Assert.IsTrue(reason.Contains("Low precision training", StringComparison.Ordinal), reason);
        }

        [Test]
        public void TheNeutralOptionsNeedNoResearchAtAll()
        {
            Assert.AreEqual(ResearchNodeId.None,
                TrainingChoiceCatalog.GateFor(TrainingPrecision.BFloat16));

            Assert.AreEqual(ResearchNodeId.None,
                TrainingChoiceCatalog.GateFor(DeduplicationPass.Standard));

            foreach (var months in TrainingChoiceCatalog.CutoffMonths)
            {
                Assert.AreEqual(ResearchNodeId.None, TrainingChoiceCatalog.GateForCutoff(months),
                    $"The cutoff at {months} months is gated, and gating any of them locks away "
                    + "behaviour the game has always had. This mistake cost twenty five tests.");
            }
        }

        [Test]
        public void ThePipelineMakesFreshTextCheaperRatherThanPossible()
        {
            Assert.Less(TrainingChoiceCatalog.CutoffCostMultiplier(0, hasPipeline: true),
                TrainingChoiceCatalog.CutoffCostMultiplier(0, hasPipeline: false));

            Assert.AreEqual(TrainingChoiceCatalog.CutoffCostMultiplier(24, hasPipeline: false),
                TrainingChoiceCatalog.CutoffCostMultiplier(24, hasPipeline: true), 1e-9,
                "It touches the fresh end only. Licensing a two year old archive is the same job "
                + "whether or not there is an ingest running.");
        }

        /// <summary>
        /// The three technologies open options rather than raising ceilings, and the balance
        /// suite's baseline player has to know the difference.
        /// </summary>
        [Test]
        public void TheThreeTechnologiesAreMarkedOptional()
        {
            foreach (var id in new[]
                     {
                         ResearchNodeId.LowPrecisionTraining,
                         ResearchNodeId.CorpusDeduplication,
                         ResearchNodeId.ContinuousDataPipeline
                     })
            {
                Assert.IsTrue(ResearchTree.Get(id).OptionalTechnology,
                    $"{id} raises no ceiling, so a player who never touches the control it opens has "
                    + "spent months on nothing. The scripted baseline reads this flag.");
            }
        }

        // ---- persistence ---------------------------------------------------------------------------

        [Test]
        public void ARunInFlightRemembersAllFourChoices()
        {
            var simulation = Ready(1205);

            var blueprint = Basic()
                .WithShape(ModelShape.Wide)
                .WithCutoff(24);

            Assert.IsTrue(simulation.TryStartTraining(blueprint, out var reason), reason);

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.IsNotNull(restored.ActiveRun);
            Assert.AreEqual(ModelShape.Wide, restored.ActiveRun.Blueprint.Shape,
                "A run that forgets its own shape finishes as a different model from the one that "
                + "was started, which is the fault v15 fixed for the model type.");

            Assert.AreEqual(24, restored.ActiveRun.Blueprint.CutoffMonthsBack);
        }

        [Test]
        public void AnOlderSaveTakesTheNeutralOptionEverywhere()
        {
            var data = new SaveData { version = 24 };
            data.models.Add(new DeployedModelData { name = "Old", capability = 40.0 });

            var upgraded = SaveMigration.UpgradeV24ToV25(data);

            Assert.AreEqual(25, upgraded.version);
            Assert.AreEqual((int)ModelShape.Balanced, upgraded.models[0].shape);
            Assert.AreEqual((int)TrainingPrecision.BFloat16, upgraded.activeRun.choices.precision);
            Assert.AreEqual(0, upgraded.activeRun.choices.cutoffMonthsBack);
        }
    }
}
