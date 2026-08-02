using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Does every number in the game stay sane, everywhere, for the whole campaign.
    ///
    /// The other suites check that individual rules are right. This one checks that nothing anywhere
    /// produces a NaN, a negative where a negative is nonsense, a value outside its own declared
    /// range, or a dangling reference between two catalogs. It is the suite that catches the class
    /// of bug where a system works perfectly on its own and quietly corrupts a neighbour.
    /// </summary>
    public sealed class ConsistencyTests
    {
        private static void AssertFinite(double value, string what)
        {
            Assert.That(double.IsNaN(value), Is.False, $"{what} was NaN.");
            Assert.That(double.IsInfinity(value), Is.False, $"{what} was infinite.");
        }

        // ------------------------------------------------------------------ catalogs

        [Test]
        public void EveryCatalogEntryIsWellFormed()
        {
            foreach (var entry in ArchitectureCatalog.All)
            {
                Assert.That(entry.ActiveParameterFraction, Is.InRange(0.02, 1.0), $"{entry.Id} active fraction.");
                Assert.That(entry.ParameterEfficiency, Is.GreaterThan(0.0), $"{entry.Id} parameter efficiency.");
                Assert.That(entry.TrainingEfficiency, Is.GreaterThan(0.0), $"{entry.Id} training efficiency.");
                Assert.That(entry.InferenceCostMultiplier, Is.GreaterThan(0.0), $"{entry.Id} inference cost.");
                Assert.That(entry.DisplayName, Is.Not.Empty);
            }

            foreach (var entry in DatasetCatalog.All)
            {
                Assert.That(entry.TokenSupplyBillions, Is.GreaterThan(0.0), $"{entry.Flag} supply.");
                Assert.That(entry.QualityMultiplier, Is.InRange(0.4, 1.6), $"{entry.Flag} quality.");
                Assert.That(entry.AcquisitionCostUsd, Is.GreaterThanOrEqualTo(0L));
            }

            foreach (var entry in ModelTraitCatalog.All)
            {
                Assert.That(entry.DisplayName, Is.Not.Empty);
                Assert.That(entry.ExpectationRiseDays, Is.GreaterThan(0));
                Assert.That(entry.UpgradeCostUsd(0), Is.GreaterThan(0L), $"{entry.Trait} level 0 cost.");
                Assert.That(entry.UpgradeDays(ModelTraitSetLimits.MaximumLevel), Is.LessThan(1000));
            }

            foreach (var entry in FounderTraitCatalog.All)
            {
                Assert.That(entry.DisplayName, Is.Not.Empty);
                Assert.That(entry.EffectSummary, Is.Not.Empty, $"{entry.Trait} has no stated effect.");

                var isNeutral = entry.BrandBonus == 0.0
                    && Math.Abs(entry.OperatingCostMultiplier - 1.0) < 1e-9
                    && Math.Abs(entry.ResearchDurationMultiplier - 1.0) < 1e-9
                    && Math.Abs(entry.TrainingThroughputMultiplier - 1.0) < 1e-9
                    && Math.Abs(entry.HardwarePriceMultiplier - 1.0) < 1e-9
                    && Math.Abs(entry.DataSupplyMultiplier - 1.0) < 1e-9
                    && Math.Abs(entry.ValuationMultiplier - 1.0) < 1e-9
                    && Math.Abs(entry.ReputationGainMultiplier - 1.0) < 1e-9
                    && entry.SafetyHeadStart == 0;
                Assert.That(isNeutral, Is.False, $"{entry.Trait} does nothing mechanically.");
            }
        }

        [Test]
        public void EveryFounderTraitIsATradeAndNotAFreeBonus()
        {
            // A trait that only helps is not a decision. Each must carry at least one cost.
            foreach (var entry in FounderTraitCatalog.All)
            {
                var hasCost = entry.BrandBonus < 0.0
                    || entry.OperatingCostMultiplier > 1.0
                    || entry.ResearchDurationMultiplier > 1.0
                    || entry.TrainingThroughputMultiplier < 1.0
                    || entry.HardwarePriceMultiplier > 1.0;

                Assert.That(hasCost, Is.True, $"{entry.Trait} is a free bonus with no downside.");
            }
        }

        [Test]
        public void EveryOpeningTileIsDistinctAndPlayable()
        {
            var tiles = new List<CompanyIdentityDefinition>(CompanyIdentityCatalog.Tiles());
            Assert.That(tiles.Count, Is.EqualTo(4), "The opening screen shows exactly four labs.");

            var names = new HashSet<string>();
            var marks = new HashSet<string>();
            foreach (var identity in tiles)
            {
                Assert.That(names.Add(identity.DisplayName), Is.True, $"Duplicate name {identity.DisplayName}.");
                Assert.That(marks.Add(identity.Mark), Is.True, $"Duplicate mark {identity.Mark}.");
                Assert.That(identity.Opening, Is.Not.Empty, $"{identity.DisplayName} has no opening text.");
                Assert.That(identity.StartingCashUsd, Is.InRange(5_000_000L, 40_000_000L));
                Assert.That(identity.StartingReputation, Is.InRange(0.0, 0.5));
                Assert.That(identity.StartingData, Is.Not.EqualTo(DatasetSource.None),
                    $"{identity.DisplayName} starts with no data at all.");
                Assert.That(identity.HouseTrait, Is.Not.EqualTo(FounderTrait.None),
                    $"{identity.DisplayName} has no house character.");
            }

            var custom = CompanyIdentityCatalog.Get(CompanyArchetype.Custom);
            Assert.That(custom.HouseTrait, Is.EqualTo(FounderTrait.None), "The blank slate carries no house trait.");
        }

        // ------------------------------------------------------------------ research tree

        [Test]
        public void TheTreeHasNoDanglingPrerequisitesAndNoCycles()
        {
            foreach (var node in ResearchTree.All)
            {
                foreach (var prerequisite in node.Prerequisites)
                {
                    Assert.That(ResearchTree.TryGet(prerequisite, out _), Is.True,
                        $"{node.Id} requires {prerequisite}, which does not exist.");
                }
            }

            // Every node has to be reachable by repeatedly taking whatever is fully satisfied.
            var unlocked = new HashSet<ResearchNodeId> { ResearchTree.StartingNode };
            var progress = true;
            while (progress)
            {
                progress = false;
                foreach (var node in ResearchTree.All)
                {
                    if (unlocked.Contains(node.Id))
                    {
                        continue;
                    }

                    var ready = true;
                    foreach (var prerequisite in node.Prerequisites)
                    {
                        if (!unlocked.Contains(prerequisite))
                        {
                            ready = false;
                            break;
                        }
                    }

                    if (ready)
                    {
                        unlocked.Add(node.Id);
                        progress = true;
                    }
                }
            }

            Assert.That(unlocked.Count, Is.EqualTo(ResearchTree.All.Count),
                "Some node is unreachable, which means a cycle or an orphan.");
            Assert.That(unlocked, Does.Contain(ResearchNodeId.ArtificialSuperintelligence),
                "The end game has to be reachable.");
        }

        [Test]
        public void APrerequisiteNeverUnlocksLaterThanTheNodeThatNeedsIt()
        {
            foreach (var node in ResearchTree.All)
            {
                foreach (var prerequisite in node.Prerequisites)
                {
                    var parent = ResearchTree.Get(prerequisite);
                    Assert.That(parent.EarliestDate, Is.LessThanOrEqualTo(node.EarliestDate),
                        $"{node.Id} opens before its prerequisite {prerequisite}, which can never be satisfied on time.");
                    Assert.That((int)parent.Era, Is.LessThanOrEqualTo((int)node.Era),
                        $"{node.Id} is in an earlier era than its prerequisite {prerequisite}.");
                }
            }
        }

        [Test]
        public void EveryGatedThingHasExactlyOneGate()
        {
            foreach (var architecture in ArchitectureCatalog.All)
            {
                if (architecture.Id == ArchitectureId.DenseTransformer)
                {
                    continue;
                }

                Assert.That(ResearchTree.GateForArchitecture(architecture.Id),
                    Is.Not.EqualTo(ResearchNodeId.None),
                    $"{architecture.Id} can be adopted without any research.");
            }

            foreach (var source in DatasetCatalog.All)
            {
                if (source.Flag == DatasetCatalog.StartingSources)
                {
                    continue;
                }

                Assert.That(ResearchTree.GateForData(source.Flag), Is.Not.EqualTo(ResearchNodeId.None),
                    $"{source.Flag} can be bought without any research.");
            }

            Assert.That(ResearchTree.GateForTier(ComputeTier.RentedCloud), Is.EqualTo(ResearchNodeId.None),
                "Renting must never be gated; it is the opening move.");
            Assert.That(ResearchTree.GateForTier(ComputeTier.ColocatedServers),
                Is.EqualTo(ResearchNodeId.ScalingLaws));
            Assert.That(ResearchTree.GateForTier(ComputeTier.OwnDatacenter),
                Is.EqualTo(ResearchNodeId.DatacenterProgramme));
        }

        [Test]
        public void TheEndGameIsAnnouncedLongBeforeItCanBeReached()
        {
            var asi = ResearchTree.Get(ResearchNodeId.ArtificialSuperintelligence);

            Assert.That(asi.HasWarning, Is.True, "The last node must warn the player before they commit.");
            Assert.That(asi.EarliestDate, Is.GreaterThan(GameDate.FromCalendar(2027, 1, 1)));
            Assert.That(asi.CostUsd, Is.GreaterThan(1_000_000_000L));

            // Visible from day one on the research board even though nothing about it is reachable.
            var simulation = new CompanySimulation(new CompanyState("Day one"));
            var board = simulation.ResearchBoard();
            var found = board.Find(entry => entry.Node.Id == ResearchNodeId.ArtificialSuperintelligence);

            Assert.That(found.Node.Id, Is.EqualTo(ResearchNodeId.ArtificialSuperintelligence),
                "The end game node has to be on the board from the first day.");
            Assert.That(found.CanStart, Is.False);
            Assert.That(found.BlockedReason, Is.Not.Empty);
        }

        // ------------------------------------------------------------------ rivals

        [Test]
        public void EveryRivalLabShipsAndStaysInsideItsRanges()
        {
            var field = CompetitorField.CreateFromCatalog();
            var random = new DeterministicRandom(3);

            Assert.That(field.Agents.Count, Is.EqualTo(8), "All eight labs exist from the start.");

            for (var day = 0; day <= 2200; day++)
            {
                var date = new GameDate(day);
                field.Tick(date, 45.0, random);

                var frontier = field.FrontierCapability(date);
                AssertFinite(frontier, "frontier");
                Assert.That(frontier, Is.InRange(0.0, 100.0), $"Frontier out of range on {date}.");

                foreach (var rival in field.LiveModels(date))
                {
                    AssertFinite(rival.Capability, $"{rival.Competitor} capability");
                    Assert.That(rival.Capability, Is.InRange(0.0, 100.0));
                    Assert.That(rival.BrandStrength, Is.InRange(0.0, 1.0));
                    Assert.That(rival.PriceMultiplier, Is.InRange(0.05, 20.0));
                    Assert.That(rival.DisplayName, Is.Not.Empty);
                }
            }

            var shipped = 0;
            foreach (var agent in field.Agents)
            {
                if (agent.HasShipped)
                {
                    shipped++;
                }
            }

            Assert.That(shipped, Is.EqualTo(8), "Every lab should have shipped something inside six years.");
        }

        [Test]
        public void RivalsNeverStallForeverEvenPastTheReferenceTable()
        {
            var field = CompetitorField.CreateFromCatalog();
            var random = new DeterministicRandom(17);

            for (var day = 0; day <= 3000; day++)
            {
                field.Tick(new GameDate(day), 60.0, random);
            }

            var atEnd = field.FrontierCapability(new GameDate(3000));
            var earlier = field.FrontierCapability(new GameDate(1800));

            Assert.That(atEnd, Is.GreaterThan(earlier),
                "Past the end of the reference table the frontier still has to move.");
            Assert.That(atEnd, Is.LessThanOrEqualTo(100.0));
        }

        // ------------------------------------------------------------------ our side

        [Test]
        public void AFullCampaignWithFoundersNeverProducesAnImpossibleNumber()
        {
            foreach (var archetype in new[]
            {
                CompanyArchetype.OpenSi,
                CompanyArchetype.Antropic,
                CompanyArchetype.DeepSearch,
                CompanyArchetype.HuggyFace
            })
            {
                var state = CompanyState.FromOpeningChoice(
                    "Soak", archetype, FounderTrait.Entrepreneur, FounderTrait.DataHoarder, 909);
                var simulation = new CompanySimulation(state);
                simulation.SetRentedPetaflops(220.0);

                simulation.TryStartTraining(new ModelBlueprint(
                    "Soak 1", ArchitectureId.DenseTransformer, 16, 320, state.OwnedDataSources), out _);

                for (var day = 0; day < 2200 && !state.IsBankrupt; day++)
                {
                    if (state.Shelf.Count > 0)
                    {
                        simulation.TryReleaseModel(0, state.DefaultPriceMultiplier, out _);
                    }

                    var report = simulation.AdvanceDay();

                    AssertFinite(report.MarketShare, $"{archetype} share");
                    AssertFinite(report.ServedBillionTokens, $"{archetype} served");
                    AssertFinite(report.BestCapability, $"{archetype} capability");
                    Assert.That(report.MarketShare, Is.InRange(0.0, 1.0));
                    Assert.That(report.ServedBillionTokens, Is.LessThanOrEqualTo(report.DemandedBillionTokens + 1e-6));
                    Assert.That(report.RevenueUsd, Is.GreaterThanOrEqualTo(0L));
                    Assert.That(report.OperatingCostUsd, Is.GreaterThanOrEqualTo(0L));
                    Assert.That(report.BestCapability, Is.InRange(0.0, 100.0));

                    var profile = simulation.Profile;
                    AssertFinite(profile.EffectivePetaflops, $"{archetype} effective petaflops");
                    Assert.That(profile.EffectivePetaflops, Is.LessThanOrEqualTo(profile.RawPetaflops + 1e-6));
                    Assert.That(profile.ResidualValueUsd, Is.GreaterThanOrEqualTo(0L));
                }

                foreach (var model in state.DeployedModels)
                {
                    var capability = model.EffectiveCapability(state.Date);
                    AssertFinite(capability, $"{archetype} model capability");
                    Assert.That(capability, Is.InRange(0.0, 100.0));
                    Assert.That(model.EfficiencyMultiplier(state.Date), Is.InRange(0.05, 4.0));
                    Assert.That(model.PriceMultiplier, Is.InRange(0.05, 10.0));
                }
            }
        }

        [Test]
        public void FounderTraitsActuallyMoveTheSimulation()
        {
            static long RunCost(FounderTrait first, FounderTrait second)
            {
                var state = CompanyState.FromOpeningChoice("Test", CompanyArchetype.Custom, first, second, 5);
                var simulation = new CompanySimulation(state);
                simulation.SetRentedPetaflops(200.0);
                simulation.Advance(120);
                return state.LifetimeOperatingCostUsd;
            }

            var lean = RunCost(FounderTrait.Solopreneur, FounderTrait.HardwareWhisperer);
            var expensive = RunCost(FounderTrait.Entrepreneur, FounderTrait.VentureDarling);

            Assert.That(lean, Is.LessThan(expensive),
                "A lean founder pair has to cost visibly less to run than an expensive one.");

            var neutral = CompanyState.FromOpeningChoice(
                "Neutral", CompanyArchetype.Custom, FounderTrait.None, FounderTrait.None, 5);
            Assert.That(neutral.Founder.OperatingCostMultiplier, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(neutral.Founder.Traits, Is.Empty);
        }

        [Test]
        public void AHouseTraitStacksOnTopOfTheTwoThePlayerPicked()
        {
            var state = CompanyState.FromOpeningChoice(
                "Careful", CompanyArchetype.Antropic, FounderTrait.Researcher, FounderTrait.Solopreneur);

            Assert.That(state.Founder.Traits.Count, Is.EqualTo(3),
                "Two picked plus the house trait the company came with.");
            Assert.That(state.Founder.Has(FounderTrait.SafetyAdvocate), Is.True);
            Assert.That(state.Founder.SafetyHeadStart, Is.EqualTo(2));
            Assert.That(state.Archetype, Is.EqualTo(CompanyArchetype.Antropic));
            Assert.That(state.CashUsd, Is.EqualTo(
                CompanyIdentityCatalog.Get(CompanyArchetype.Antropic).StartingCashUsd));
        }

        [Test]
        public void TheOpeningChoiceSurvivesASaveAndReload()
        {
            var original = CompanyState.FromOpeningChoice(
                "HuggyFace", CompanyArchetype.HuggyFace, FounderTrait.SilverTongue, FounderTrait.Researcher, 42);
            var simulation = new CompanySimulation(original);
            simulation.TryStartResearch(ResearchNodeId.CuratedCorpora, out _);
            simulation.Advance(30);

            var json = UnityEngine.JsonUtility.ToJson(Persistence.SaveStore.Capture(original));
            var restored = Persistence.SaveStore.Restore(Persistence.SaveStore.Parse(json));

            Assert.That(restored.Archetype, Is.EqualTo(CompanyArchetype.HuggyFace));
            Assert.That(restored.Founder.Traits, Is.EquivalentTo(original.Founder.Traits));
            Assert.That(restored.DefaultPriceMultiplier,
                Is.EqualTo(original.DefaultPriceMultiplier).Within(1e-9));
            Assert.That(restored.UnlockedResearch, Is.EquivalentTo(original.UnlockedResearch));
            Assert.That(restored.ActiveResearch?.Node, Is.EqualTo(original.ActiveResearch?.Node));
            Assert.That(restored.Founder.OperatingCostMultiplier,
                Is.EqualTo(original.Founder.OperatingCostMultiplier).Within(1e-9));
        }

        [Test]
        public void NothingCanBeUnlockedWithoutItsResearch()
        {
            // A date where every technique below is already public, so the only thing that can be
            // refusing is the research gate itself.
            var state = new CompanyState("Gated")
            {
                Date = GameDate.FromCalendar(2023, 1, 1),
                CashUsd = 5_000_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Placeholder", ArchitectureId.DenseTransformer, 30, state.Date, 1e10, 1.0));
            var simulation = new CompanySimulation(state);

            Assert.That(simulation.TryAdoptArchitecture(ArchitectureId.EfficientAttention, out var archReason),
                Is.False);
            Assert.That(archReason, Does.Contain("research"));

            Assert.That(simulation.TryAcquireDataSource(DatasetSource.CuratedWeb, out var dataReason), Is.False);
            Assert.That(dataReason, Does.Contain("research"));

            Assert.That(state.IsTierUnlocked(ComputeTier.ColocatedServers), Is.False,
                "Owning hardware needs the scaling laws worked out, not just money.");
            Assert.That(state.IsTierUnlocked(ComputeTier.RentedCloud), Is.True,
                "Renting is never gated.");

            // And with the research done, the same calls succeed.
            state.UnlockedResearch.Add(ResearchNodeId.CuratedCorpora);
            state.UnlockedResearch.Add(ResearchNodeId.ScalingLaws);
            Assert.That(simulation.TryAcquireDataSource(DatasetSource.CuratedWeb, out _), Is.True);
            Assert.That(state.IsTierUnlocked(ComputeTier.ColocatedServers), Is.True);
        }
    }
}
