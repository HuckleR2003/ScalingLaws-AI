using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class SavePersistenceTests
    {
        private static CompanyState BuildCampaign()
        {
            var state = new CompanyState("Prometheus AI", 777)
            {
                Date = GameDate.FromCalendar(2024, 3, 15),
                CashUsd = 87_500_000,
                Reputation = 0.42,
                TrainingComputeShare = 0.55,
                OwnedDataSources = DatasetSource.WebCrawl | DatasetSource.CuratedWeb | DatasetSource.CodeCorpus,
                LifetimeRevenueUsd = 310_000_000,
                LifetimeOperatingCostUsd = 190_000_000,
                LifetimeCapitalSpentUsd = 44_000_000,
                DatacenterOrdered = true,
                DatacenterReadyDate = GameDate.FromCalendar(2025, 1, 9),
                DaysInDebt = 3
            };

            state.AdoptedArchitectures.Add(ArchitectureId.EfficientAttention);
            state.AdoptedArchitectures.Add(ArchitectureId.SparseMixture);

            state.Pool.SetRentedPetaflops(1_480.0);
            state.Pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 512,
                GameDate.FromCalendar(2023, 5, 1), 33_000, 45));
            state.Pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.CpuGenoa, ComputeTier.ColocatedServers, 64,
                GameDate.FromCalendar(2023, 5, 1), 11_000, 45));

            state.AddDeployedModel(new DeployedModel(
                "Muse 1", ArchitectureId.DenseTransformer, 24.5, GameDate.FromCalendar(2022, 4, 2), 2e10, 1.0));
            state.AddDeployedModel(new DeployedModel(
                "Muse 2", ArchitectureId.SparseMixture, 48.25, GameDate.FromCalendar(2023, 11, 20), 5e10, 0.6));

            var run = new TrainingRun(
                new ModelBlueprint("Muse 3", ArchitectureId.SparseMixture, 400, 8000,
                    DatasetSource.WebCrawl | DatasetSource.CuratedWeb),
                GameDate.FromCalendar(2024, 3, 1),
                120_000,
                57.5,
                8_000,
                0);
            run.Contribute(41_000, 6_200_000);
            state.ActiveRun = run;

            return state;
        }

        [Test]
        public void ACampaignSurvivesARoundTripThroughJson()
        {
            var original = BuildCampaign();
            var json = JsonUtility.ToJson(SaveStore.Capture(original));
            var restored = SaveStore.Restore(SaveStore.Parse(json));

            Assert.That(restored.CompanyName, Is.EqualTo(original.CompanyName));
            Assert.That(restored.Date, Is.EqualTo(original.Date));
            Assert.That(restored.CashUsd, Is.EqualTo(original.CashUsd));
            Assert.That(restored.Reputation, Is.EqualTo(original.Reputation).Within(1e-9));
            Assert.That(restored.TrainingComputeShare, Is.EqualTo(original.TrainingComputeShare).Within(1e-9));
            Assert.That(restored.OwnedDataSources, Is.EqualTo(original.OwnedDataSources));
            Assert.That(restored.LifetimeRevenueUsd, Is.EqualTo(original.LifetimeRevenueUsd));
            Assert.That(restored.DatacenterOrdered, Is.True);
            Assert.That(restored.DatacenterReadyDate, Is.EqualTo(original.DatacenterReadyDate));
            Assert.That(restored.AdoptedArchitectures, Is.EquivalentTo(original.AdoptedArchitectures));
            Assert.That(restored.Pool.RentedPetaflops, Is.EqualTo(1_480.0).Within(1e-9));
            Assert.That(restored.Pool.Assets.Count, Is.EqualTo(2));
            Assert.That(restored.ReleasedModelCount, Is.EqualTo(2));
            Assert.That(restored.DeployedModels[1].Capability, Is.EqualTo(48.25).Within(1e-9));
            Assert.That(restored.BestCapability, Is.EqualTo(original.BestCapability).Within(1e-9),
                "Effective capability is par relative, so it has to survive the round trip too.");
        }

        [Test]
        public void PurchaseDatesAndLeadTimesSurviveTheRoundTrip()
        {
            // These two fields are the whole reason the save format moved to v2. If they do not
            // survive, depreciation is meaningless on a reloaded campaign.
            var original = BuildCampaign();
            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original))));

            var before = original.Pool.Assets[0];
            var after = restored.Pool.Assets[0];

            Assert.That(after.GenerationId, Is.EqualTo(before.GenerationId));
            Assert.That(after.Units, Is.EqualTo(before.Units));
            Assert.That(after.PurchaseDate, Is.EqualTo(before.PurchaseDate));
            Assert.That(after.CommissionDate, Is.EqualTo(before.CommissionDate));
            Assert.That(after.PurchasePricePerUnitUsd, Is.EqualTo(before.PurchasePricePerUnitUsd));

            Assert.That(HardwareValuation.ResidualValueUsd(after, restored.Date),
                Is.EqualTo(HardwareValuation.ResidualValueUsd(before, original.Date)));
        }

        [Test]
        public void ARunInFlightResumesWhereItStopped()
        {
            var original = BuildCampaign();
            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original))));

            Assert.That(restored.ActiveRun, Is.Not.Null);
            Assert.That(restored.ActiveRun.Blueprint.Name, Is.EqualTo("Muse 3"));
            Assert.That(restored.ActiveRun.Blueprint.ParameterCountBillions, Is.EqualTo(400).Within(1e-9));
            Assert.That(restored.ActiveRun.PetaflopDaysCompleted, Is.EqualTo(41_000).Within(1e-6));
            Assert.That(restored.ActiveRun.Progress, Is.EqualTo(original.ActiveRun.Progress).Within(1e-9));
            Assert.That(restored.ActiveRun.ProjectedCapability, Is.EqualTo(57.5).Within(1e-9));
        }

        [Test]
        public void TheRandomStreamResumesSoTheCampaignStaysReplayable()
        {
            var original = BuildCampaign();
            original.Random.NextUInt();
            original.Random.NextUInt();

            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original))));

            Assert.That(restored.Random.State, Is.EqualTo(original.Random.State));
            Assert.That(restored.Random.NextUInt(), Is.EqualTo(original.Random.NextUInt()));
        }

        [Test]
        public void AVersionOneSaveIsUpgradedIntoDatedHardwareBatches()
        {
            var legacy = new SaveDataV1
            {
                version = 1,
                companyName = "Legacy Labs",
                dayIndex = GameDate.FromCalendar(2024, 1, 1).DayIndex,
                cashUsd = 25_000_000,
                randomState = 4242,
                reputation = 0.31,
                ownedDataSources = (int)(DatasetSource.WebCrawl | DatasetSource.CuratedWeb),
                lifetimeRevenueUsd = 60_000_000,
                lifetimeOperatingCostUsd = 41_000_000,
                rentedAccelerators = 300,
                ownedAccelerators = 1_024,
                models = new List<DeployedModelData>
                {
                    new()
                    {
                        name = "Legacy 1",
                        architecture = (int)ArchitectureId.DenseTransformer,
                        capability = 33.0,
                        releaseDayIndex = GameDate.FromCalendar(2023, 2, 1).DayIndex,
                        activeParameterCount = 3e10,
                        priceMultiplier = 1.0
                    }
                }
            };

            var upgraded = SaveStore.Parse(JsonUtility.ToJson(legacy));

            Assert.That(upgraded.rentedPetaflops, Is.GreaterThan(0.0),
                "The v4 to v5 step has to convert the old unit count into contracted capacity.");
            Assert.That(SaveMigration.LastDetectedVersion, Is.EqualTo(1));
            Assert.That(upgraded, Is.Not.Null);
            Assert.That(upgraded.version, Is.EqualTo(SaveData.CurrentVersion));

            // The point of the migration: a bare count becomes a batch that depreciation can read.
            Assert.That(upgraded.assets.Count, Is.EqualTo(1));
            Assert.That(upgraded.assets[0].units, Is.EqualTo(1_024));
            Assert.That(upgraded.assets[0].purchaseDayIndex, Is.LessThan(upgraded.dayIndex),
                "A reconstructed batch must be older than the save, or it has lost no value at all.");
            Assert.That(upgraded.assets[0].generationId, Is.EqualTo((int)HardwareGenerationId.AcceleratorH100),
                "January 2024 should reconstruct as the part that was current then.");
            Assert.That(upgraded.rentedAccelerators, Is.EqualTo(300));
            Assert.That(upgraded.models.Count, Is.EqualTo(1));
            Assert.That(SaveMigration.LastMigrationNotes, Is.Not.Empty,
                "The upgrade invented a purchase date and has to say so.");
        }

        [Test]
        public void AnUpgradedLegacySaveIsImmediatelyPlayable()
        {
            var legacy = new SaveDataV1
            {
                version = 1,
                companyName = "Legacy Labs",
                dayIndex = GameDate.FromCalendar(2024, 1, 1).DayIndex,
                cashUsd = 25_000_000,
                randomState = 4242,
                ownedDataSources = (int)DatasetSource.WebCrawl,
                ownedAccelerators = 512,
                rentedAccelerators = 0
            };

            var state = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(legacy)));
            var simulation = new CompanySimulation(state);

            Assert.That(simulation.Profile.AcceleratorCount, Is.EqualTo(512));
            Assert.That(simulation.Profile.ResidualValueUsd, Is.GreaterThan(0));
            Assert.That(simulation.Profile.DailyDepreciationUsd, Is.GreaterThan(0.0),
                "The reconstructed batch must age like any other.");

            simulation.Advance(30);
            Assert.That(state.Date, Is.EqualTo(GameDate.FromCalendar(2024, 1, 31)));
        }

        [Test]
        public void AFileFromTheFutureIsRefusedRatherThanGuessedAt()
        {
            var fromTheFuture = JsonUtility.ToJson(new SaveData { version = SaveData.CurrentVersion + 99 });

            Assert.That(SaveStore.Parse(fromTheFuture), Is.Null);
            Assert.That(SaveStore.Parse("{ not json at all"), Is.Null);
            Assert.That(SaveStore.Parse(string.Empty), Is.Null);
        }

        [Test]
        public void SanitizeClampsEverythingAHandEditedFileCouldContain()
        {
            var poisoned = new SaveData
            {
                companyName = "   ",
                dayIndex = int.MaxValue,
                reputation = 42.0,
                trainingComputeShare = double.NaN,
                lifetimeRevenueUsd = -5,
                rentedAccelerators = -100,
                ownedDataSources = int.MaxValue,
                adoptedArchitectures = new List<int> { 999, (int)ArchitectureId.SparseMixture },
                assets = new List<HardwareAssetData>
                {
                    new() { generationId = 12345, tier = 1, units = 10 },
                    new() { generationId = (int)HardwareGenerationId.AcceleratorH100, tier = (int)ComputeTier.ColocatedServers, units = 0 },
                    new() { generationId = (int)HardwareGenerationId.AcceleratorH100, tier = (int)ComputeTier.ColocatedServers, units = 8, pricePerUnitUsd = -1 }
                },
                models = new List<DeployedModelData>
                {
                    new() { name = null, architecture = (int)ArchitectureId.DenseTransformer, capability = 500.0, priceMultiplier = 0.0 }
                },
                hasActiveRun = true,
                activeRun = new TrainingRunData { architecture = 999 }
            };

            var safe = SaveStore.Sanitize(poisoned);

            Assert.That(safe.companyName, Is.EqualTo("Newco"));
            Assert.That(safe.dayIndex, Is.EqualTo(GameDate.MaximumDayIndex));
            Assert.That(safe.reputation, Is.EqualTo(1.0));
            Assert.That(safe.trainingComputeShare, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(safe.lifetimeRevenueUsd, Is.Zero);
            Assert.That(safe.rentedAccelerators, Is.Zero);
            Assert.That(safe.adoptedArchitectures, Is.EquivalentTo(new[] { (int)ArchitectureId.SparseMixture }));
            Assert.That(safe.assets.Count, Is.EqualTo(1), "Unknown hardware and empty batches are dropped.");
            Assert.That(safe.assets[0].pricePerUnitUsd, Is.Zero);
            Assert.That(safe.models[0].name, Is.EqualTo("Untitled model"));
            Assert.That(safe.models[0].capability, Is.EqualTo(100.0));
            Assert.That(safe.models[0].priceMultiplier, Is.EqualTo(0.05).Within(1e-9));
            Assert.That(safe.hasActiveRun, Is.False, "A run with an unknown architecture cannot be resumed.");

            var knownSources = 0;
            foreach (var source in DatasetCatalog.All)
            {
                knownSources |= (int)source.Flag;
            }

            Assert.That(safe.ownedDataSources, Is.EqualTo(knownSources), "Unknown data flags are stripped.");
        }

        [Test]
        public void SanitizeSurvivesANullPayload()
        {
            var safe = SaveStore.Sanitize(null);

            Assert.That(safe, Is.Not.Null);
            Assert.That(safe.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(safe.assets, Is.Empty);
            Assert.That(safe.models, Is.Empty);
        }
    }
}
