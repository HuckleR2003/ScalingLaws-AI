using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class ComputeTierGateTests
    {
        [Test]
        public void EveryTierIsVisibleFromDayOneEvenWhenShut()
        {
            var ladder = ComputeTierCatalog.EvaluateAll(GameDate.Start, 0, 0, 0);

            Assert.That(ladder.Count, Is.EqualTo(ComputeTierCatalog.All.Count));
            Assert.That(ladder.Count, Is.EqualTo(3));
            Assert.That(ladder.Exists(status => status.Tier == ComputeTier.OwnDatacenter), Is.True,
                "A locked tier must still appear on the ladder so the player can plan toward it.");
        }

        [Test]
        public void RentingIsOpenOnTheFirstDayWithNothingInTheBank()
        {
            var rented = ComputeTierCatalog.Get(ComputeTier.RentedCloud)
                .Evaluate(GameDate.Start, 0, 0, 0);

            Assert.That(rented.IsUnlocked, Is.True);
            Assert.That(rented.LockReason, Is.Empty);
            Assert.That(ComputeTierCatalog.Get(ComputeTier.RentedCloud).LeadTimeDays, Is.Zero);
        }

        /// <summary>
        /// **Cash, and deliberately nothing else, since 17 October 2026.**
        ///
        /// It used to want a shipped model as well, and that is what the test above this one was
        /// called. Two testers in one week pressed BUY in the parts shop on a company the tutorial
        /// had just handed a cellar to, and were refused with a sentence about releasing a model,
        /// which is a different subject from buying four accelerators.
        ///
        /// The assertion is kept rather than deleted and turned round: a company with the money
        /// and no product **is** unlocked. That way putting the model gate back is a failure here
        /// rather than a quiet return of the thing two people tripped on.
        /// </summary>
        [Test]
        public void ColocationNeedsCashAndNothingElse()
        {
            var definition = ComputeTierCatalog.Get(ComputeTier.ColocatedServers);

            var broke = definition.Evaluate(GameDate.Start, 1_000_000, 1, 0);
            var noModel = definition.Evaluate(GameDate.Start, 50_000_000, 0, 0);
            var ready = definition.Evaluate(GameDate.Start, 50_000_000, 1, 0);

            Assert.That(broke.IsUnlocked, Is.False);
            Assert.That(broke.LockReason, Does.Contain("cash"));

            Assert.That(noModel.IsUnlocked, Is.True,
                "A company with the money and no product on sale is shut out of owning silicon "
                + "again, which is the refusal two testers read as the shop being broken.");

            Assert.That(ready.IsUnlocked, Is.True);
        }

        [Test]
        public void TheDatacenterIsGatedOnRevenueAndOnTheCalendar()
        {
            var definition = ComputeTierCatalog.Get(ComputeTier.OwnDatacenter);

            var tooEarly = definition.Evaluate(
                GameDate.FromCalendar(2023, 1, 1), 500_000_000, 5, 1_000_000_000);
            var noRevenue = definition.Evaluate(
                GameDate.FromCalendar(2025, 1, 1), 500_000_000, 5, 1_000_000);
            var ready = definition.Evaluate(
                GameDate.FromCalendar(2025, 1, 1), 500_000_000, 5, 1_000_000_000);

            Assert.That(tooEarly.IsUnlocked, Is.False);
            Assert.That(tooEarly.LockReason, Does.Contain("2024-01-01"));
            Assert.That(noRevenue.IsUnlocked, Is.False);
            Assert.That(noRevenue.LockReason, Does.Contain("lifetime revenue"));
            Assert.That(ready.IsUnlocked, Is.True);
        }

        /// <summary>
        /// The reason string must never tell the player to do something already done.
        ///
        /// Measured on the datacenter rather than on colocation, because colocation has one
        /// requirement left and a sentence listing one thing cannot list the wrong one. The
        /// claim needs a gate with several, and this one has three.
        /// </summary>
        [Test]
        public void ALockReasonOnlyNamesWhatIsActuallyMissing()
        {
            // Five models and a billion earned, and only the money is short.
            var reason = ComputeTierCatalog.Get(ComputeTier.OwnDatacenter)
                .Evaluate(GameDate.FromCalendar(2025, 1, 1), 1_000_000, 5, 1_000_000_000)
                .LockReason;

            Assert.That(reason, Does.Contain("cash"));
            Assert.That(reason, Does.Not.Contain("released model"));
            Assert.That(reason, Does.Not.Contain("lifetime revenue"));
            Assert.That(reason, Does.Not.Contain("not before"));
        }

        /// <summary>
        /// A shut tier refuses at the till and the money stays where it was.
        ///
        /// On the datacenter, which a fresh company misses on all three counts. It used to be
        /// written against colocation, and a fresh company now clears that gate on its opening
        /// twelve million, so the test was measuring an open door.
        /// </summary>
        [Test]
        public void BuyingIntoALockedTierIsRefusedWithTheGateReason()
        {
            var state = new CompanyState("Gatekeeper");
            var simulation = new CompanySimulation(state);

            var bought = simulation.TryBuyHardware(
                HardwareGenerationId.AcceleratorA100, 8, ComputeTier.OwnDatacenter, out var reason);

            Assert.That(bought, Is.False);
            Assert.That(reason, Does.Contain("cash"));
            Assert.That(state.CashUsd, Is.EqualTo(CompanyState.StartingCashUsd), "A refused purchase must not move money.");
        }

        [Test]
        public void HardwareCannotBeBoughtBeforeItShips()
        {
            var state = new CompanyState("Time traveller")
            {
                CashUsd = 500_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Placeholder", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            var bought = simulation.TryBuyHardware(
                HardwareGenerationId.AcceleratorB200, 8, ComputeTier.ColocatedServers, out var reason);

            Assert.That(bought, Is.False);
            Assert.That(reason, Does.Contain("2025-01-15"));
        }

        [Test]
        public void ColocationPowerCapacityCapsTheClusterSize()
        {
            var state = new CompanyState("Power hungry")
            {
                Date = GameDate.FromCalendar(2023, 1, 1),
                CashUsd = 5_000_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Placeholder", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            var capacity = ComputeTierCatalog.Get(ComputeTier.ColocatedServers).PowerCapacityKilowatts;
            var overSized = (int)(capacity / 0.7) + 500;

            var bought = simulation.TryBuyHardware(
                HardwareGenerationId.AcceleratorH100, overSized, ComputeTier.ColocatedServers, out var reason);

            Assert.That(bought, Is.False);
            Assert.That(reason, Does.Contain("kW"));
        }

        [Test]
        public void OrderedHardwareCostsMoneyNowAndArrivesLater()
        {
            var state = new CompanyState("Patient buyer")
            {
                Date = GameDate.FromCalendar(2023, 1, 1),
                CashUsd = 100_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Placeholder", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            Assert.That(simulation.TryBuyHardware(
                HardwareGenerationId.AcceleratorH100, 64, ComputeTier.ColocatedServers, out _), Is.True);

            Assert.That(state.CashUsd, Is.LessThan(100_000_000));
            Assert.That(simulation.Profile.AcceleratorCount, Is.Zero, "Nothing is online on the day of purchase.");
            Assert.That(simulation.Profile.AcceleratorsInTransit, Is.EqualTo(64));

            simulation.Advance(46);

            Assert.That(simulation.Profile.AcceleratorCount, Is.EqualTo(64));
            Assert.That(simulation.Profile.AcceleratorsInTransit, Is.Zero);
        }

        [Test]
        public void StarvedAcceleratorsRunAtAFractionOfTheirRating()
        {
            var market = MarketModel.Evaluate(GameDate.FromCalendar(2023, 6, 1));

            var starved = new ComputePool();
            starved.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 256,
                GameDate.FromCalendar(2023, 1, 1), 30_000, 0));

            var balanced = new ComputePool();
            balanced.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 256,
                GameDate.FromCalendar(2023, 1, 1), 30_000, 0));
            balanced.AddAsset(new HardwareAsset(
                HardwareGenerationId.CpuGenoa, ComputeTier.ColocatedServers, 32,
                GameDate.FromCalendar(2023, 1, 1), 11_000, 0));
            balanced.AddAsset(new HardwareAsset(
                HardwareGenerationId.MemoryDdr5, ComputeTier.ColocatedServers, 32,
                GameDate.FromCalendar(2023, 1, 1), 2_600, 0));
            balanced.AddAsset(new HardwareAsset(
                HardwareGenerationId.NetworkIb400, ComputeTier.ColocatedServers, 4,
                GameDate.FromCalendar(2023, 1, 1), 48_000, 0));

            var starvedProfile = starved.BuildProfile(GameDate.FromCalendar(2023, 6, 1), market);
            var balancedProfile = balanced.BuildProfile(GameDate.FromCalendar(2023, 6, 1), market);

            Assert.That(starvedProfile.RawPetaflops, Is.EqualTo(balancedProfile.RawPetaflops).Within(0.01));
            Assert.That(starvedProfile.BalanceFactor, Is.EqualTo(ComputePool.MinimumBalanceFactor));
            Assert.That(balancedProfile.BalanceFactor, Is.EqualTo(1.0));
            Assert.That(balancedProfile.EffectivePetaflops, Is.GreaterThan(starvedProfile.EffectivePetaflops * 3.0));
        }

        /// <summary>Builds a company that has cleared every gate on the datacenter except paying for it.</summary>
        private static CompanySimulation ReadyForADatacenter()
        {
            var state = new CompanyState("Site owner")
            {
                Date = GameDate.FromCalendar(2025, 1, 1),
                CashUsd = 85_000_000,
                LifetimeRevenueUsd = 1_000_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "First", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));
            state.AddDeployedModel(new DeployedModel(
                "Second", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            simulation.UnlockResearchNode(ResearchNodeId.DatacenterProgramme);
            return simulation;
        }

        /// <summary>
        /// **The tier asked for eighty million in the bank and cost eighty million to commission.**
        /// Paying for it therefore dropped the company under its own gate in the same tick, so the
        /// ladder went back to demanding the money that had just left and a site already under
        /// construction reported itself shut. Francisco paid for one and the game never admitted it.
        /// </summary>
        [Test]
        public void ACommissionedDatacenterStaysOpenAfterItsOwnPriceLeavesTheBank()
        {
            var simulation = ReadyForADatacenter();
            var state = simulation.State;

            Assert.That(simulation.TryOrderDatacenter(out var why), Is.True, why);
            Assert.That(state.CashUsd, Is.LessThan(
                ComputeTierCatalog.Get(ComputeTier.OwnDatacenter).RequiredCashUsd),
                "The commission has to leave the company under the cash gate, or this proves nothing.");

            var row = state.ComputeTierLadder()
                .Find(status => status.Tier == ComputeTier.OwnDatacenter);

            Assert.That(row.IsUnlocked, Is.True,
                "A datacenter that has been paid for must not report itself locked.");
            Assert.That(row.LockReason, Is.Empty);
        }

        /// <summary>
        /// The second half of the same fault, and the one that cost the money. The compute page
        /// picks which tier its hardware cards buy into by asking <see cref="CompanyState.IsTierUnlocked"/>,
        /// so once the cash gate shut again every purchase went quietly into colocation instead,
        /// where the site provides 2,500 kW rather than 40,000. From the player chair the button
        /// simply did nothing.
        /// </summary>
        [Test]
        public void TheComputePageStillBuysIntoADatacenterThatHasBeenPaidFor()
        {
            var simulation = ReadyForADatacenter();
            var state = simulation.State;

            Assert.That(simulation.TryOrderDatacenter(out var why), Is.True, why);
            state.CashUsd = 1_000_000;

            Assert.That(state.IsTierUnlocked(ComputeTier.OwnDatacenter), Is.True,
                "The purchase page reads this to choose a tier, so a false here sells into colocation.");
        }

        /// <summary>
        /// The research requirement used to live only on the screen: <c>TryOrderDatacenter</c> read
        /// the catalog straight and never asked the tree. A gate that one caller can walk around is
        /// a suggestion, which is the shape this project has already shipped more than once.
        /// </summary>
        [Test]
        public void CommissioningADatacenterNeedsTheResearchAndNotOnlyTheMoney()
        {
            var state = new CompanyState("Rich and unread")
            {
                Date = GameDate.FromCalendar(2025, 1, 1),
                CashUsd = 500_000_000,
                LifetimeRevenueUsd = 1_000_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "First", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));
            state.AddDeployedModel(new DeployedModel(
                "Second", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);

            Assert.That(simulation.TryOrderDatacenter(out var why), Is.False,
                "Money alone must not commission a site the company has not worked out how to run.");
            Assert.That(why, Is.Not.Empty);
            Assert.That(state.CashUsd, Is.EqualTo(500_000_000),
                "A refused commission must not move money.");
            Assert.That(state.DatacenterOrdered, Is.False);
        }
    }
}
