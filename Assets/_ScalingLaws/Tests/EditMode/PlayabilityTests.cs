using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Is the game playable.
    ///
    /// Every other test asks whether a rule behaves correctly. These ask whether the rules together
    /// make a game somebody can win, and whether standing still loses. A simulation can be perfectly
    /// consistent and completely unplayable, and the only way to find out is to play it.
    ///
    /// <see cref="ScriptedOperator"/> is a plain competent player, not an optimal one: it ships,
    /// raises when offered, keeps optimising, and scales its fleet with its bank balance. If that
    /// player cannot survive four years, the balance is wrong, not the player.
    /// </summary>
    public sealed class PlayabilityTests
    {
        /// <summary>
        /// A deliberately ordinary strategy. No lookahead, no intel, no clever timing. It exists to
        /// establish the floor: whatever a thoughtful human does should beat this, and this should
        /// not go bankrupt.
        /// </summary>
        private sealed class ScriptedOperator
        {
            private readonly CompanySimulation simulation;
            private readonly CompanyState state;
            private int modelNumber;

            public ScriptedOperator(CompanySimulation simulation)
            {
                this.simulation = simulation;
                state = simulation.State;
            }

            private DayReport lastReport;

            public int ModelsShipped { get; private set; }
            public int RoundsRaised { get; private set; }
            public int UpgradesCommissioned { get; private set; }

            public void Run(int days)
            {
                // Serving pays the bills, so the split leans that way rather than sitting at the
                // research-heavy default.
                state.TrainingComputeShare = 0.55;

                for (var day = 0; day < days && !state.IsBankrupt; day++)
                {
                    Decide();
                    lastReport = simulation.AdvanceDay();
                }
            }

            private void Decide()
            {
                ShipAnythingFinished();
                SizeTheFleet();
                RaiseWhenOffered();
                PushTheTree();
                AdoptBetterArchitectures();
                BuyDataWhenAffordable();
                KeepOptimising();
                StartARunWhenIdle();
            }

            /// <summary>Ship the day it is done. Holding is an advanced move and this player is not.</summary>
            private void ShipAnythingFinished()
            {
                while (state.Shelf.Count > 0)
                {
                    if (!simulation.TryReleaseModel(0, 1.0, out _))
                    {
                        break;
                    }

                    ModelsShipped++;
                }
            }

            /// <summary>
            /// Follow demand, not the bank balance.
            ///
            /// This is the single insight that separates a company that compounds from one that does
            /// not. Serving capacity is what converts market share into revenue, so a fleet sized to
            /// cash rather than to demand starves itself: too small to serve, therefore too poor to
            /// grow. Grow while there is demand going unserved and the money holds; shrink when the
            /// account is going the wrong way.
            /// </summary>
            private void SizeTheFleet()
            {
                var rented = state.Pool.RentedPetaflops;
                if (rented <= 0.0)
                {
                    simulation.SetRentedPetaflops(120.0);
                    return;
                }

                var dailyCost = Math.Max(1.0, simulation.Profile.DailyOperatingCostUsd);
                var runwayDays = state.CashUsd / dailyCost;
                var costPerPetaflopDay = dailyCost / rented;

                // Only grow while the business is both turning customers away and making money on
                // the ones it keeps. Growing into a negative margin is how a fleet kills a company.
                var target = rented;
                if (lastReport.UnservedBillionTokens > 0.0 && lastReport.CashFlowUsd > 0 && runwayDays > 240)
                {
                    target = rented * 1.12 + 10.0;
                }
                else if (runwayDays < 150)
                {
                    // Only runway triggers a cut. Negative cash flow during a training run is what
                    // investing looks like; shrinking the fleet then is how a company talks itself
                    // out of ever growing.
                    target = rented * 0.92;
                }

                // Never hold more capacity than the balance sheet can carry for two quarters.
                var affordable = state.CashUsd / Math.Max(1.0, costPerPetaflopDay * 180.0);
                target = Math.Clamp(target, 60.0, Math.Max(60.0, Math.Min(60_000.0, affordable)));

                if (Math.Abs(target - rented) > rented * 0.02)
                {
                    simulation.SetRentedPetaflops(target);
                }
            }

            private void RaiseWhenOffered()
            {
                if (state.CurrentFundingOffer.IsOpen)
                {
                    if (simulation.TryAcceptFundingOffer(out _))
                    {
                        RoundsRaised++;
                    }

                    return;
                }

                if (simulation.NextRoundAvailability().IsAvailable)
                {
                    simulation.TryOpenFundingRound(out _);
                }
            }

            /// <summary>
            /// Buy the newer architecture families as they are published.
            ///
            /// This is not optional flavour. A sparse mixture fires a quarter of its parameters per
            /// token, so it serves at roughly a quarter of the cost. A company still on dense
            /// transformers in 2025 is running a 39 percent gross margin against a rival's 88, and
            /// the difference eats it alive long before capability does.
            /// </summary>
            /// <summary>
            /// Always be researching something. The tree gates every architecture, corpus, trait and
            /// compute tier in the game, and the calendar cost of a node cannot be bought out of, so
            /// an idle research org is the most expensive thing a company can have.
            /// </summary>
            private void PushTheTree()
            {
                if (state.ActiveResearch != null)
                {
                    return;
                }

                ResearchNodeId best = ResearchNodeId.None;
                var cheapest = long.MaxValue;

                foreach (var standing in simulation.ResearchBoard())
                {
                    if (!standing.CanStart || standing.Node.CostUsd >= cheapest)
                    {
                        continue;
                    }

                    // The baseline follows the ladder. It never touches the Scale or Data controls,
                    // so researching a technology that only opens one of them is months and millions
                    // spent on an option it will not use. Cheapest-first was a fair description of a
                    // competent player while every node raised a ceiling; it stopped being one the
                    // day the tree gained options.
                    if (standing.Node.OptionalTechnology)
                    {
                        continue;
                    }

                    // Leave enough behind to keep the lights on for a year.
                    if (standing.Node.CostUsd > state.CashUsd * 0.4)
                    {
                        continue;
                    }

                    cheapest = standing.Node.CostUsd;
                    best = standing.Node.Id;
                }

                if (best != ResearchNodeId.None)
                {
                    simulation.TryStartResearch(best, out _);
                }
            }

            private void AdoptBetterArchitectures()
            {
                foreach (var definition in ArchitectureCatalog.AvailableOn(state.Date))
                {
                    if (state.HasArchitecture(definition.Id))
                    {
                        continue;
                    }

                    // Two and a half times the sticker price, not a flat threshold. Sparse mixtures
                    // arrive in December 2023 and cost nine million; a company that happens to be
                    // mid-run that month never buys them and is dead by 2025 without knowing why.
                    if (definition.AdoptionCostUsd * 2.5 > state.CashUsd)
                    {
                        continue;
                    }

                    simulation.TryAdoptArchitecture(definition.Id, out _);
                    return;
                }
            }

            private void BuyDataWhenAffordable()
            {
                // Buy early. Data supply, not money, is what caps a run once the fleet is any size,
                // and a company that waits until it is rich has already wasted a generation.
                if (state.CashUsd < 12_000_000)
                {
                    return;
                }

                foreach (var source in new[]
                {
                    DatasetSource.CuratedWeb,
                    DatasetSource.CodeCorpus,
                    DatasetSource.LicensedBooks,
                    DatasetSource.AcademicArchive,
                    DatasetSource.Synthetic
                })
                {
                    if (!state.HasDataSource(source))
                    {
                        simulation.TryAcquireDataSource(source, out _);
                        return;
                    }
                }
            }

            /// <summary>
            /// Keep the newest model level with the market. This is the rule that separates a company
            /// that survives from one that leads on paper and dies on margin.
            /// </summary>
            private void KeepOptimising()
            {
                if (state.DeployedModels.Count == 0 || state.UpgradeProjects.Count >= 2)
                {
                    return;
                }

                var index = state.DeployedModels.Count - 1;
                foreach (var standing in simulation.UpgradeGrid(index))
                {
                    if (!standing.IsBehindMarket || !standing.IsAvailable)
                    {
                        continue;
                    }

                    if (standing.UpgradeCostUsd > state.CashUsd / 6)
                    {
                        continue;
                    }

                    if (simulation.TryStartUpgrade(index, standing.Trait, out _))
                    {
                        UpgradesCommissioned++;
                    }

                    return;
                }
            }

            private void StartARunWhenIdle()
            {
                if (state.ActiveRun != null || state.Shelf.Count > 0)
                {
                    return;
                }

                // Size the run to the money, then rent enough capacity to finish it in five months.
                // Sizing it to the fleet instead is the trap: a small fleet produces a small model,
                // which earns little, which keeps the fleet small.
                const int targetDays = 150;
                var market = simulation.Market;
                // Keep a reserve. The next architecture family is the single highest return purchase
                // available and a company that spends its last dollar on compute never makes it.
                // Reserve for the next family, but never let the reserve freeze the company: a rule
                // that blocks every run until an unaffordable purchase is affordable blocks forever.
                var reserve = Math.Min(NextArchitectureCostUsd() * 2.5, state.CashUsd * 0.5);
                var spendable = state.CashUsd - reserve;
                var cashBudget = Math.Min(state.CashUsd * 0.22, Math.Max(0, spendable) * 0.6);
                if (cashBudget < 500_000)
                {
                    return;
                }

                var rawPetaflopDays = cashBudget / Math.Max(1.0, market.RentPricePerPetaflopDayUsd);
                var neededPetaflops = rawPetaflopDays / targetDays;
                if (state.Pool.RentedPetaflops < neededPetaflops)
                {
                    simulation.SetRentedPetaflops(neededPetaflops);
                }

                var profile = simulation.Profile;
                if (profile.EffectivePetaflops <= 0.0)
                {
                    return;
                }

                var budget = profile.EffectivePetaflops * state.TrainingComputeShare * targetDays;
                var architecture = BestArchitecture();
                var blueprint = TrainingPlanner.OptimalBlueprintForBudget(
                    $"Muse {++modelNumber}", architecture, budget, state.OwnedDataSources);

                // A compute-optimal shape can also ask for a model larger than the company knows how
                // to hold together. A player meets that as a slider that stops; the operator meets
                // it as a refusal from TryStartTraining, so it has to do what the player does and
                // build the largest run it is allowed to.
                //
                // Without this the whole campaign dies on day one: the first compute-optimal shape
                // is over the opening cap, every run is refused, nothing ships, and the company is
                // insolvent by August 2024 having researched one node. That is the operator being
                // unable to use a control, not the ceiling being wrong.
                var ceiling = simulation.ParameterCeilingBillions();
                if (blueprint.ParameterCountBillions > ceiling)
                {
                    blueprint = blueprint
                        .WithParameters(ceiling)
                        .WithTokens(Math.Max(ModelBlueprint.MinimumTokenBillions, ceiling * 20.0));
                }

                // A compute-optimal shape can ask for more tokens than the company owns. Cap the run
                // at the supply and rebuild the shape around it rather than halving blindly, which
                // stalls forever once the budget outgrows the corpora.
                var supply = OwnedTokenSupplyBillions();
                if (blueprint.TrainingTokensBillions > supply * 0.95)
                {
                    var tokens = Math.Max(ModelBlueprint.MinimumTokenBillions, supply * 0.95);
                    blueprint = blueprint.WithTokens(tokens).WithParameters(Math.Max(0.1, tokens / 20.0));
                }

                var projection = simulation.Project(blueprint);
                if (!projection.IsFeasible)
                {
                    modelNumber--;
                    return;
                }

                // Do not start a run the company cannot pay for.
                if (projection.ComputeCashCostUsd > state.CashUsd * 0.6)
                {
                    modelNumber--;
                    return;
                }

                if (!simulation.TryStartTraining(blueprint, out _))
                {
                    modelNumber--;
                }
            }

            /// <summary>Sticker price of the cheapest family the company has not adopted yet.</summary>
            private long NextArchitectureCostUsd()
            {
                foreach (var definition in ArchitectureCatalog.AvailableOn(state.Date))
                {
                    if (!state.HasArchitecture(definition.Id))
                    {
                        return definition.AdoptionCostUsd;
                    }
                }

                return 0;
            }

            private double OwnedTokenSupplyBillions()
            {
                var supply = 0.0;
                foreach (var definition in DatasetCatalog.All)
                {
                    if (state.HasDataSource(definition.Flag)
                        && definition.IsAvailableOn(state.Date, state.BestCapability))
                    {
                        supply += definition.TokenSupplyBillions;
                    }
                }

                return supply;
            }

            private ArchitectureId BestArchitecture()
            {
                var best = ArchitectureId.DenseTransformer;
                var bestValue = double.MaxValue;

                foreach (var id in state.AdoptedArchitectures)
                {
                    if (!state.TryGetArchitecture(id, out var definition) || !definition.IsAvailableOn(state.Date))
                    {
                        continue;
                    }

                    if (definition.ActiveParameterFraction < bestValue)
                    {
                        bestValue = definition.ActiveParameterFraction;
                        best = id;
                    }
                }

                return best;
            }
        }

        private static CompanySimulation NewGame(uint seed = 4242)
        {
            var state = new CompanyState("Prometheus AI", seed);
            return new CompanySimulation(state);
        }

        [Test]
        public void AnOrdinaryCompetentPlayerSurvivesFourYears()
        {
            var simulation = NewGame();
            var operatorBot = new ScriptedOperator(simulation);

            operatorBot.Run(1460);

            Assert.That(simulation.State.IsBankrupt, Is.False,
                $"Went under on {simulation.State.Date} with {operatorBot.ModelsShipped} model(s) shipped.");
            Assert.That(operatorBot.ModelsShipped, Is.GreaterThanOrEqualTo(3),
                "Four years should fit several model generations.");
            Assert.That(simulation.State.LifetimeRevenueUsd, Is.GreaterThan(0),
                "A shipped model has to earn something.");
            Assert.That(simulation.State.BestCapability, Is.GreaterThan(30.0),
                $"Capability stalled at {simulation.State.BestCapability:0.0}.");
        }

        [Test]
        public void ThatPlayerStaysWithinReachOfTheFrontier()
        {
            var simulation = NewGame();
            new ScriptedOperator(simulation).Run(1460);

            var gap = simulation.Market.FrontierCapability - simulation.State.BestCapability;

            // Behind is expected and fine. Hopeless is not: the game has to stay winnable from here.
            Assert.That(gap, Is.LessThan(25.0),
                $"Best {simulation.State.BestCapability:0.0} against frontier {simulation.Market.FrontierCapability:0.0}.");
        }

        /// <summary>
        /// The difficulty band. An ordinary player should finish four years in the race and not in
        /// front of it: still behind the frontier, still short of dominating the market, still
        /// solvent. If this test starts passing trivially the game has gone soft; if it starts
        /// failing on the low side the game has gone unfair.
        /// </summary>
        [Test]
        public void FourYearsLeavesTheBaselinePlayerCompetitiveAndNotDominant()
        {
            var simulation = NewGame();
            var bot = new ScriptedOperator(simulation);
            bot.Run(1460);

            var report = simulation.AdvanceDay();
            var gap = report.FrontierCapability - simulation.State.BestCapability;
            var context =
                $"cap {simulation.State.BestCapability:F1}, frontier {report.FrontierCapability:F1}, "
                + $"share {report.MarketShare:P1}, cash {simulation.State.CashUsd:N0}, "
                + $"models {bot.ModelsShipped}, rounds {bot.RoundsRaised}, "
                + $"research {simulation.State.UnlockedResearch.Count}/{ResearchTree.All.Count}";

            Assert.That(simulation.State.IsBankrupt, Is.False, context);
            Assert.That(gap, Is.GreaterThan(-8.0),
                $"An ordinary player should not be comfortably ahead of the whole field. {context}");
            Assert.That(report.MarketShare, Is.LessThan(0.75),
                $"An ordinary player should not own the market. {context}");
            Assert.That(simulation.State.UnlockedResearch.Count, Is.LessThan(ResearchTree.All.Count),
                $"Four years must not be enough to finish the technology tree. {context}");
            Assert.That(simulation.State.HasResearch(ResearchNodeId.ArtificialSuperintelligence), Is.False,
                $"The end game must stay out of reach in the first four years. {context}");
        }

        /// <summary>
        /// The campaign does not end when the first difficulty band does. Year five is where the
        /// reference timeline runs out, specialised audiences matter, and a weak save or a slowly
        /// compounding number tends to reveal itself. A competent no-lookahead player must still be
        /// solvent, participating in the market and able to react to the next decision.
        /// </summary>
        [Test]
        public void FiveYearsLeaveTheBaselinePlayerSolventAndInTheRace()
        {
            var simulation = NewGame();
            var bot = new ScriptedOperator(simulation);
            bot.Run(1826);

            var report = simulation.AdvanceDay();
            var gap = report.FrontierCapability - simulation.State.BestCapability;
            var context =
                $"cap {simulation.State.BestCapability:F1}, frontier {report.FrontierCapability:F1}, "
                + $"share {report.MarketShare:P1}, cash {simulation.State.CashUsd:N0}, "
                + $"models {bot.ModelsShipped}, rounds {bot.RoundsRaised}, "
                + $"research {simulation.State.UnlockedResearch.Count}/{ResearchTree.All.Count}";

            Assert.That(simulation.State.IsBankrupt, Is.False, context);
            Assert.That(bot.ModelsShipped, Is.GreaterThanOrEqualTo(4),
                $"Five years should contain several deliberate model generations. {context}");
            Assert.That(simulation.State.LifetimeRevenueUsd, Is.GreaterThan(0L),
                $"A surviving company must still have a business, not only a credit line. {context}");
            Assert.That(gap, Is.LessThan(30.0),
                $"The frontier may move past an ordinary player, but it cannot become unreachable. {context}");
            Assert.That(report.MarketShare, Is.InRange(0.001, 0.90),
                $"Year five must neither erase the player nor hand them the whole market. {context}");
        }

        [Test]
        public void ThePlayerEndsUpOnTheBoard()
        {
            var simulation = NewGame();
            new ScriptedOperator(simulation).Run(1460);

            var board = simulation.Ranking();
            var position = RankingBoard.PlayerPosition(board);

            Assert.That(position, Is.GreaterThan(0), "The company should appear in the standings at all.");
            Assert.That(position, Is.LessThanOrEqualTo(board.Count));
        }

        [Test]
        public void ShippingOnceAndDoingNothingElseIsPunished()
        {
            // The control case. Same start, same seed, no maintenance after the first model.
            var passive = NewGame();
            passive.SetRentedAccelerators(500);
            passive.TryStartTraining(
                new ModelBlueprint("Only model", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.WebCrawl),
                out _);
            passive.Advance(40);
            passive.TryReleaseModel(0, 1.0, out _);
            passive.Advance(1400);

            var active = NewGame();
            new ScriptedOperator(active).Run(1440);

            Assert.That(active.State.BestCapability, Is.GreaterThan(passive.State.BestCapability + 10.0),
                "Playing well has to beat playing once by a wide margin.");
            Assert.That(active.State.LifetimeRevenueUsd, Is.GreaterThan(passive.State.LifetimeRevenueUsd));
        }

        [Test]
        public void RaisingCapitalIsWhatMakesTheLaterYearsPossible()
        {
            var funded = NewGame();
            new ScriptedOperator(funded).Run(1460);

            Assert.That(funded.State.CapTable.RoundCount, Is.GreaterThan(0),
                "A company that ships should reach at least a Series A inside four years.");
            Assert.That(funded.State.CapTable.FounderEquity, Is.LessThan(1.0));
            Assert.That(funded.State.CapTable.TotalRaisedUsd, Is.GreaterThan(0));
        }

        [Test]
        public void AHouseFamilyBeatsTheDenseBaselineItWasBuiltFrom()
        {
            var simulation = NewGame(seed: 31);
            var state = simulation.State;
            state.CashUsd = 400_000_000;
            simulation.SetRentedAccelerators(3000);

            var weights = new Dictionary<ResearchDirection, double>
            {
                [ResearchDirection.Sparsity] = 1.0,
                [ResearchDirection.Throughput] = 0.3,
                [ResearchDirection.Quality] = 0.4,
                [ResearchDirection.Serving] = 0.5,
                [ResearchDirection.Reasoning] = 0.2
            };

            // The directions are capped by research now, and this programme leans past the base in
            // three of them. A player who wanted these weights would have walked the ladders first,
            // so the fixture does too: it opens exactly the rungs the weights need and no more.
            //
            // Derived rather than listed, so changing a weight above cannot silently re-break this.
            // That is a better model of a player than an exemption from the rule would be, and it
            // is the treatment ScaleCeiling already needed on the parameter slider.
            foreach (var (direction, wanted) in weights)
            {
                foreach (var (node, fraction) in ArchitectureCeiling.Ladders[direction])
                {
                    if (ArchitectureCeiling.FractionFor(direction, state.HasResearch) < wanted)
                    {
                        state.UnlockedResearch.Add(node);
                    }

                    if (fraction >= wanted)
                    {
                        break;
                    }
                }
            }

            var blueprint = new ArchitectureBlueprint(
                "House sparse 1",
                ArchitectureId.CustomFamilyA,
                ArchitectureId.None,
                weights[ResearchDirection.Sparsity],
                weights[ResearchDirection.Throughput],
                weights[ResearchDirection.Quality],
                weights[ResearchDirection.Serving],
                weights[ResearchDirection.Reasoning],
                researchBudgetUsd: 200_000_000,
                durationDays: 500);

            var projection = simulation.ProjectArchitecture(blueprint);
            Assert.That(projection.IsFeasible, Is.True, projection.BlockingReason);
            Assert.That(simulation.TryStartArchitectureProgramme(blueprint, out var reason), Is.True, reason);

            simulation.Advance(600);

            Assert.That(state.ActiveArchitectureProject, Is.Null, "The programme should have landed by now.");
            Assert.That(state.CustomArchitectures.ContainsKey(ArchitectureId.CustomFamilyA), Is.True);

            var family = state.CustomArchitectures[ArchitectureId.CustomFamilyA];
            var baseline = ArchitectureCatalog.Baseline;

            Assert.That(family.ActiveParameterFraction, Is.LessThan(baseline.ActiveParameterFraction),
                "A sparsity led programme has to cut the compute bill.");
            Assert.That(state.HasArchitecture(ArchitectureId.CustomFamilyA), Is.True,
                "A finished family is adopted automatically; the company designed it.");
        }

        [Test]
        public void ACheapRushedProgrammeIsALotteryAndAFundedOneIsNot()
        {
            var date = GameDate.FromCalendar(2024, 6, 1);

            var rushed = new ArchitectureBlueprint(
                "Rushed", ArchitectureId.CustomFamilyA, ArchitectureId.None,
                1.0, 0.2, 0.2, 0.2, 0.2, 3_000_000, 70);
            var funded = new ArchitectureBlueprint(
                "Funded", ArchitectureId.CustomFamilyA, ArchitectureId.None,
                1.0, 0.2, 0.2, 0.2, 0.2, 900_000_000, 700);

            var rushedProjection = ArchitectureDesigner.Project(rushed, date, null, long.MaxValue, 0);
            var fundedProjection = ArchitectureDesigner.Project(funded, date, null, long.MaxValue, 0);

            Assert.That(rushedProjection.Variance, Is.GreaterThan(fundedProjection.Variance * 2.0),
                "Underfunding has to show up as risk, not just as a worse average.");
            Assert.That(fundedProjection.Expected.ActiveParameterFraction,
                Is.LessThan(rushedProjection.Expected.ActiveParameterFraction),
                "More money and more time has to reach further.");
        }

        [Test]
        public void AFamilyPlateausAcrossGenerations()
        {
            var date = GameDate.FromCalendar(2025, 1, 1);
            var clean = new ArchitectureBlueprint(
                "Gen", ArchitectureId.CustomFamilyB, ArchitectureId.None,
                1.0, 0.3, 0.3, 0.3, 0.3, 300_000_000, 500);

            var firstGeneration = ArchitectureDesigner.Project(clean, date, null, long.MaxValue, 0);

            var iterated = clean.WithBaseFamily(ArchitectureId.DenseTransformer);
            var thirdGeneration = ArchitectureDesigner.Project(iterated, date, null, long.MaxValue, 3);

            Assert.That(thirdGeneration.Expected.ActiveParameterFraction,
                Is.GreaterThan(firstGeneration.Expected.ActiveParameterFraction),
                "A third iteration of the same lineage has to reach less far than a clean sheet.");
        }

        [Test]
        public void SavingMidCampaignAndLoadingItBackContinuesTheSameCompany()
        {
            var original = NewGame();
            new ScriptedOperator(original).Run(900);

            var json = JsonUtility.ToJson(SaveStore.Capture(original.State));
            var restored = SaveStore.Restore(SaveStore.Parse(json));

            Assert.That(restored.Date, Is.EqualTo(original.State.Date));
            Assert.That(restored.CashUsd, Is.EqualTo(original.State.CashUsd));
            Assert.That(restored.ReleasedModelCount, Is.EqualTo(original.State.ReleasedModelCount));
            Assert.That(restored.BestCapability, Is.EqualTo(original.State.BestCapability).Within(1e-6));
            Assert.That(restored.CapTable.FounderEquity,
                Is.EqualTo(original.State.CapTable.FounderEquity).Within(1e-9));
            Assert.That(restored.CustomArchitectures.Count,
                Is.EqualTo(original.State.CustomArchitectures.Count));

            // And it has to keep running rather than merely load.
            var resumed = new CompanySimulation(restored);
            resumed.Advance(120);
            Assert.That(restored.Date, Is.EqualTo(original.State.Date.AddDays(120)));
        }

        [Test]
        public void AYearFourSaveRunsIdenticallyThroughYearFive()
        {
            var original = NewGame(seed: 901);
            new ScriptedOperator(original).Run(1460);

            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original.State))));
            var resumed = new CompanySimulation(restored);

            original.Advance(366);
            resumed.Advance(366);

            Assert.That(restored.Date, Is.EqualTo(original.State.Date));
            Assert.That(restored.CashUsd, Is.EqualTo(original.State.CashUsd));
            Assert.That(restored.LifetimeRevenueUsd, Is.EqualTo(original.State.LifetimeRevenueUsd));
            Assert.That(restored.LifetimeOperatingCostUsd, Is.EqualTo(original.State.LifetimeOperatingCostUsd));
            Assert.That(restored.LifetimeTaxPaidUsd, Is.EqualTo(original.State.LifetimeTaxPaidUsd));
            Assert.That(restored.LifetimeFinesUsd, Is.EqualTo(original.State.LifetimeFinesUsd));
            Assert.That(restored.BestCapability, Is.EqualTo(original.State.BestCapability).Within(1e-9));
            Assert.That(restored.Rivals.FrontierCapability(restored.Date),
                Is.EqualTo(original.State.Rivals.FrontierCapability(original.State.Date)).Within(1e-9));
        }

        [Test]
        public void TheWholeCampaignStaysDeterministicUnderAScriptedPlayer()
        {
            static (long Cash, double Capability, int Models) Play(uint seed)
            {
                var simulation = NewGame(seed);
                var bot = new ScriptedOperator(simulation);
                bot.Run(1100);
                return (simulation.State.CashUsd, simulation.State.BestCapability, bot.ModelsShipped);
            }

            Assert.That(Play(777), Is.EqualTo(Play(777)));
        }

        [Test]
        public void EveryRailScreenHasSomethingToShowFromTheFirstDay()
        {
            // A new campaign must not open on an empty or throwing screen anywhere.
            var simulation = NewGame();
            var state = simulation.State;

            Assert.That(simulation.Project(new ModelBlueprint(
                "Opening", ArchitectureId.DenseTransformer, 10, 200, DatasetSource.WebCrawl)).IsFeasible,
                Is.False, "With no compute rented the opening projection should explain itself, not crash.");

            simulation.SetRentedAccelerators(400);
            Assert.That(simulation.Project(new ModelBlueprint(
                "Opening", ArchitectureId.DenseTransformer, 10, 200, DatasetSource.WebCrawl)).IsFeasible, Is.True);

            Assert.That(simulation.UpgradeGrid(0), Is.Empty, "No models yet, so no grid.");
            Assert.That(simulation.Ranking(), Is.Not.Null);
            Assert.That(simulation.NextRoundAvailability().Reason, Is.Not.Empty);
            Assert.That(state.FirstFreeArchitectureSlot(), Is.EqualTo(ArchitectureId.CustomFamilyA));
            Assert.That(simulation.ProjectArchitecture(
                ArchitectureBlueprint.Default(ArchitectureId.CustomFamilyA)).IsFeasible, Is.True);
            Assert.That(state.ComputeTierLadder().Count, Is.EqualTo(3));
        }
    }
}
