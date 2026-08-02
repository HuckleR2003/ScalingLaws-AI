using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class CompanySimulationTests
    {
        private static ModelBlueprint FirstModel() => new(
            "Muse 1", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.WebCrawl);

        private static CompanySimulation NewCompany(int rentedAccelerators, uint seed = 1234)
        {
            var state = new CompanyState("Prometheus AI", seed);
            var simulation = new CompanySimulation(state);
            simulation.SetRentedAccelerators(rentedAccelerators);
            return simulation;
        }

        [Test]
        public void ANewCompanyStartsInJanuary2022WithSeedMoneyAndNothingElse()
        {
            var state = new CompanyState("Prometheus AI");

            Assert.That(state.Date, Is.EqualTo(GameDate.Start));
            Assert.That(state.CashUsd, Is.EqualTo(CompanyState.StartingCashUsd));
            Assert.That(state.ReleasedModelCount, Is.Zero);
            Assert.That(state.BestCapability, Is.Zero);
            Assert.That(state.HasArchitecture(ArchitectureId.DenseTransformer), Is.True);
            Assert.That(state.HasDataSource(DatasetSource.WebCrawl), Is.True);
            Assert.That(state.IsTierUnlocked(ComputeTier.RentedCloud), Is.True);
            Assert.That(state.IsTierUnlocked(ComputeTier.ColocatedServers), Is.False);
        }

        /// <summary>Trains the opening model and ships it the day it comes off the line.</summary>
        private static CompanySimulation TrainAndShipFirstModel(int rentedAccelerators = 500, uint seed = 1234)
        {
            var simulation = NewCompany(rentedAccelerators, seed);
            simulation.TryStartTraining(FirstModel(), out _);
            simulation.Advance(40);
            simulation.TryReleaseModel(0, 1.0, out _);
            return simulation;
        }

        [Test]
        public void AFinishedRunGoesToTheShelfRatherThanStraightToMarket()
        {
            var simulation = NewCompany(500);
            var projection = simulation.Project(FirstModel());

            Assert.That(projection.IsFeasible, Is.True, projection.BlockingReason);
            Assert.That(simulation.TryStartTraining(FirstModel(), out var reason), Is.True, reason);

            simulation.Advance(projection.TrainingDays + 3);

            Assert.That(simulation.State.ActiveRun, Is.Null);
            Assert.That(simulation.State.Shelf.Count, Is.EqualTo(1), "A finished run waits for a release decision.");
            Assert.That(simulation.State.ReleasedModelCount, Is.Zero);
            Assert.That(simulation.State.BestCapability, Is.Zero, "Nothing on the shelf counts as capability.");

            Assert.That(simulation.TryReleaseModel(0, 1.0, out var releaseReason), Is.True, releaseReason);
            Assert.That(simulation.State.Shelf, Is.Empty);
            Assert.That(simulation.State.ReleasedModelCount, Is.EqualTo(1));
            Assert.That(simulation.State.BestCapability, Is.GreaterThan(10.0));
            Assert.That(simulation.State.IsBankrupt, Is.False);
        }

        [Test]
        public void HoldingAModelOnTheShelfCostsCapabilityAsParMovesUnderIt()
        {
            var simulation = NewCompany(500);
            simulation.TryStartTraining(FirstModel(), out _);
            simulation.Advance(40);

            var shelved = simulation.State.Shelf[0];
            var shipNow = shelved.CapabilityIfReleasedOn(simulation.State.Date);

            simulation.Advance(500);
            var shipLater = shelved.CapabilityIfReleasedOn(simulation.State.Date);

            Assert.That(shelved.Capability, Is.EqualTo(shipNow).Within(0.001),
                "On the day it finishes there is no slippage yet.");
            Assert.That(shipLater, Is.LessThan(shipNow), "Par rises under a model that is not shipped.");
            Assert.That(shelved.DaysOnShelf(simulation.State.Date), Is.GreaterThan(400));
        }

        [Test]
        public void WhatComesOutOfTheRunIsNotTheProjectionItStartedWith()
        {
            // The projection is an estimate and is stored as one. Only the finished number ever
            // becomes the model's capability.
            var simulation = NewCompany(500, seed: 99);
            simulation.TryStartTraining(FirstModel(), out _);
            var projected = simulation.State.ActiveRun.ProjectedCapability;

            simulation.Advance(40);

            var measured = simulation.State.Shelf[0].Capability;
            Assert.That(measured, Is.Not.EqualTo(projected));
            Assert.That(measured, Is.EqualTo(projected).Within(6.0 * CompanySimulation.TrainingOutcomeStandardDeviation));
            Assert.That(simulation.State.Shelf[0].ProjectedCapability, Is.EqualTo(projected).Within(1e-9),
                "The projection is kept for the post mortem, next to the measurement, not instead of it.");
        }

        [Test]
        public void TwoCampaignsWithTheSameSeedRunIdentically()
        {
            var first = RunScriptedCampaign(4242);
            var second = RunScriptedCampaign(4242);
            var different = RunScriptedCampaign(9999);

            Assert.That(second.Cash, Is.EqualTo(first.Cash));
            Assert.That(second.Capability, Is.EqualTo(first.Capability));
            Assert.That(different.Capability, Is.Not.EqualTo(first.Capability));
        }

        private static (long Cash, double Capability) RunScriptedCampaign(uint seed)
        {
            var simulation = TrainAndShipFirstModel(500, seed);
            simulation.Advance(160);
            return (simulation.State.CashUsd, simulation.State.BestCapability);
        }

        [Test]
        public void AnIdleRentedClusterBurnsTheCompanyDown()
        {
            // Nothing trained, nothing served, twenty thousand accelerators on the meter.
            var simulation = NewCompany(20_000);

            simulation.Advance(60);

            Assert.That(simulation.State.IsBankrupt, Is.True);
            Assert.That(simulation.State.LifetimeRevenueUsd, Is.Zero);
        }

        [Test]
        public void ShippingOnceAndCoastingLosesTheMarket()
        {
            var simulation = TrainAndShipFirstModel();

            Assert.That(simulation.State.ReleasedModelCount, Is.EqualTo(1));

            var earlyShare = simulation.AdvanceDay().MarketShare;
            Assert.That(earlyShare, Is.GreaterThan(0.02), "A 2022 model should find customers in 2022.");

            // Three years of rivals shipping while the company ships nothing.
            simulation.Advance(1095);

            var lateShare = simulation.AdvanceDay();
            Assert.That(lateShare.MarketShare, Is.LessThan(earlyShare * 0.1),
                $"Share went from {earlyShare:P3} to {lateShare.MarketShare:P3}.");
            Assert.That(lateShare.CapabilityGap, Is.GreaterThan(20.0));
        }

        [Test]
        public void ARunCannotStartWithoutTheDataOrTheArchitecture()
        {
            var simulation = NewCompany(500);

            var unownedData = new ModelBlueprint(
                "Licensed", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.LicensedBooks);
            Assert.That(simulation.TryStartTraining(unownedData, out var dataReason), Is.False);
            Assert.That(dataReason, Does.Contain("does not own"));

            var unownedArchitecture = new ModelBlueprint(
                "Sparse", ArchitectureId.SparseMixture, 20, 400, DatasetSource.WebCrawl);
            Assert.That(simulation.TryStartTraining(unownedArchitecture, out var architectureReason), Is.False);
            Assert.That(architectureReason, Does.Contain("adopted"));
        }

        [Test]
        public void OnlyOneRunAtATime()
        {
            var simulation = NewCompany(500);

            Assert.That(simulation.TryStartTraining(FirstModel(), out _), Is.True);
            Assert.That(simulation.TryStartTraining(FirstModel(), out var reason), Is.False);
            Assert.That(reason, Does.Contain("already in flight"));
        }

        [Test]
        public void AddingComputeMidRunFinishesItSooner()
        {
            var patient = NewCompany(500);
            patient.TryStartTraining(FirstModel(), out _);
            patient.Advance(5);

            var reinforced = NewCompany(500);
            reinforced.TryStartTraining(FirstModel(), out _);
            reinforced.Advance(5);
            reinforced.SetRentedAccelerators(2000);
            reinforced.Advance(3);

            patient.Advance(3);

            Assert.That(reinforced.State.ActiveRun?.Progress ?? 1.0,
                Is.GreaterThan(patient.State.ActiveRun?.Progress ?? 0.0));
        }

        [Test]
        public void SellingAnAgedClusterReturnsFarLessThanItCost()
        {
            var state = new CompanyState("Late seller")
            {
                Date = GameDate.FromCalendar(2022, 10, 1),
                CashUsd = 200_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Placeholder", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));
            var simulation = new CompanySimulation(state);

            Assert.That(simulation.TryBuyHardware(
                HardwareGenerationId.AcceleratorH100, 512, ComputeTier.ColocatedServers, out var reason), Is.True, reason);

            var spent = 200_000_000 - state.CashUsd;

            state.Date = GameDate.FromCalendar(2026, 6, 1);
            Assert.That(simulation.TrySellHardware(0, 512, out var proceeds, out _), Is.True);

            Assert.That(proceeds, Is.LessThan(spent / 3), $"Recovered ${proceeds:N0} of ${spent:N0}.");
        }

        [Test]
        public void EventsAreQueuedInTheOrderTheyHappened()
        {
            var simulation = TrainAndShipFirstModel();

            var types = new List<CompanyEventType>();
            while (simulation.State.TryDequeueEvent(out var companyEvent))
            {
                types.Add(companyEvent.Type);
            }

            Assert.That(types, Does.Contain(CompanyEventType.TrainingStarted));
            Assert.That(types, Does.Contain(CompanyEventType.TrainingCompleted));
            Assert.That(types, Does.Contain(CompanyEventType.ModelReleased));
            Assert.That(types.IndexOf(CompanyEventType.TrainingStarted),
                Is.LessThan(types.IndexOf(CompanyEventType.TrainingCompleted)));
        }

        [Test]
        public void ASnapshotSeparatesCashFromWhatTheFleetIsWorth()
        {
            var state = new CompanyState("Balance sheet")
            {
                Date = GameDate.FromCalendar(2024, 1, 1),
                CashUsd = 200_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Placeholder", ArchitectureId.DenseTransformer, 30, state.Date, 1e10, 1.0));
            var simulation = new CompanySimulation(state);
            simulation.TryBuyHardware(HardwareGenerationId.AcceleratorH100, 256, ComputeTier.ColocatedServers, out _);

            var snapshot = new CompanySnapshot(state, simulation.Profile, simulation.Market);

            Assert.That(snapshot.CashUsd, Is.LessThan(200_000_000));
            Assert.That(snapshot.FleetResidualValueUsd, Is.GreaterThan(0));
            Assert.That(snapshot.NetWorthUsd, Is.EqualTo(snapshot.CashUsd + snapshot.FleetResidualValueUsd));
            Assert.That(snapshot.AcceleratorsInTransit, Is.EqualTo(256));
            Assert.That(snapshot.BestCapability, Is.EqualTo(30.0).Within(0.001),
                "A model released today sits exactly at market par, so its effective capability is its measured one.");
        }

        [Test]
        public void ABankruptCompanyStopsSimulating()
        {
            var simulation = NewCompany(20_000);
            simulation.Advance(60);
            Assert.That(simulation.State.IsBankrupt, Is.True);

            var dateAtFailure = simulation.State.Date;
            var cashAtFailure = simulation.State.CashUsd;

            simulation.Advance(100);

            Assert.That(simulation.State.Date, Is.EqualTo(dateAtFailure));
            Assert.That(simulation.State.CashUsd, Is.EqualTo(cashAtFailure));
        }
    }
}
