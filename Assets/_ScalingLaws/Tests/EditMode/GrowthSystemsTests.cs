using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class GrowthSystemsTests
    {
        private static ModelBlueprint FirstModel() => new(
            "Muse 1", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.WebCrawl);

        private static CompanySimulation ShippedCompany(uint seed = 1234)
        {
            var state = new CompanyState("Prometheus AI", seed);

            // Trait upgrades sit behind technology tree nodes. These tests are about the upgrade
            // mechanics, not the gate, so the gate is opened directly.
            state.UnlockedResearch.Add(ResearchNodeId.EfficientAttention);
            state.UnlockedResearch.Add(ResearchNodeId.ScalingLaws);
            state.UnlockedResearch.Add(ResearchNodeId.MixtureOfExperts);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedAccelerators(500);
            simulation.TryStartTraining(FirstModel(), out _);
            simulation.Advance(40);
            simulation.TryReleaseModel(0, 1.0, out _);
            return simulation;
        }

        // ------------------------------------------------------------------ funding

        [Test]
        public void NobodyFundsALabThatHasShippedNothing()
        {
            var simulation = new CompanySimulation(new CompanyState("Nothing yet"));

            var availability = simulation.NextRoundAvailability();

            Assert.That(availability.Stage, Is.EqualTo(FundingStage.SeriesA));
            Assert.That(availability.IsAvailable, Is.False);
            Assert.That(availability.Refusal, Is.EqualTo(FundingRefusal.NeedsReleasedModels));
            Assert.That(simulation.TryOpenFundingRound(out var reason), Is.False);
            Assert.That(reason, Does.Contain("released model"));
        }

        [Test]
        public void ShippingOpensTheSeriesAAndSigningItDilutesTheFounders()
        {
            var simulation = ShippedCompany();
            var cashBefore = simulation.State.CashUsd;

            Assert.That(simulation.NextRoundAvailability().IsAvailable, Is.True);
            Assert.That(simulation.TryOpenFundingRound(out var openReason), Is.True, openReason);

            var offer = simulation.State.CurrentFundingOffer;
            Assert.That(offer.IsOpen, Is.True);
            Assert.That(offer.Stage, Is.EqualTo(FundingStage.SeriesA));
            Assert.That(offer.EquitySold, Is.GreaterThan(0.0));

            Assert.That(simulation.TryAcceptFundingOffer(out var signReason), Is.True, signReason);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cashBefore + offer.RaiseUsd));
            Assert.That(simulation.State.CapTable.FounderEquity, Is.LessThan(1.0));
            Assert.That(simulation.State.CapTable.FounderEquity,
                Is.EqualTo(1.0 - offer.EquitySold).Within(1e-9));
            Assert.That(simulation.State.CapTable.LastStage, Is.EqualTo(FundingStage.SeriesA));
            Assert.That(simulation.State.CurrentFundingOffer.IsOpen, Is.False);
        }

        [Test]
        public void TheSameCompanyIsWorthFarMoreInAHotMarketThanAColdOne()
        {
            // Identical fundamentals, four years apart. Only investor sentiment differs.
            const double capability = 55.0;
            const double frontier = 65.0;
            const long revenue = 40_000_000;

            var cold = FundingMarket.PreMoneyValuationUsd(
                GameDate.FromCalendar(2022, 3, 1), capability, frontier, revenue);
            var hot = FundingMarket.PreMoneyValuationUsd(
                GameDate.FromCalendar(2025, 6, 1), capability, frontier, revenue);

            Assert.That(hot, Is.GreaterThan(cold * 3),
                $"Cold market ${cold:N0} against hot market ${hot:N0}.");
            Assert.That(FundingCatalog.SentimentLabel(FundingCatalog.SentimentOn(GameDate.FromCalendar(2025, 6, 1))),
                Is.EqualTo("Frenzied"));
        }

        [Test]
        public void ValuationFallsAwaySteeplyFromTheFrontier()
        {
            var date = GameDate.FromCalendar(2024, 6, 1);
            var atParity = FundingMarket.PreMoneyValuationUsd(date, 60, 60, 0);
            var slightlyBehind = FundingMarket.PreMoneyValuationUsd(date, 48, 60, 0);
            var wellBehind = FundingMarket.PreMoneyValuationUsd(date, 30, 60, 0);

            Assert.That(slightlyBehind, Is.LessThan(atParity / 2), "Twenty percent behind is worth under half.");
            Assert.That(wellBehind, Is.LessThan(atParity / 10), "Half the frontier is worth a rounding error.");
        }

        [Test]
        public void ADownRoundCostsMoreOfTheCompanyForTheSameMoney()
        {
            var date = GameDate.FromCalendar(2026, 8, 1);

            var flat = FundingMarket.BuildOffer(
                FundingStage.SeriesB, date, 60, 70, 20_000_000, lastPostMoneyValuationUsd: 0);
            var down = FundingMarket.BuildOffer(
                FundingStage.SeriesB, date, 60, 70, 20_000_000, lastPostMoneyValuationUsd: 50_000_000_000);

            Assert.That(down.IsDownRound, Is.True);
            Assert.That(flat.IsDownRound, Is.False);
            Assert.That(down.EquitySold, Is.GreaterThan(flat.EquitySold));
            Assert.That(down.EquitySold,
                Is.EqualTo(flat.EquitySold * FundingCatalog.DownRoundPenalty).Within(1e-6));
        }

        [Test]
        public void ATermSheetLapsesIfItIsNotSigned()
        {
            var simulation = ShippedCompany();
            simulation.TryOpenFundingRound(out _);
            Assert.That(simulation.State.CurrentFundingOffer.IsOpen, Is.True);

            simulation.Advance(FundingCatalog.Get(FundingStage.SeriesA).OfferWindowDays + 2);

            Assert.That(simulation.State.CurrentFundingOffer.IsOpen, Is.False);
            Assert.That(simulation.TryAcceptFundingOffer(out var reason), Is.False);
            Assert.That(reason, Does.Contain("No term sheet"));
        }

        // ------------------------------------------------------------------ upgrades

        [Test]
        public void AFreshModelSitsExactlyAtMarketParAndScoresNoBonus()
        {
            var date = GameDate.FromCalendar(2024, 6, 1);
            var model = new DeployedModel("Par", ArchitectureId.SparseMixture, 50, date, 5e10, 1.0);

            Assert.That(model.EffectiveCapability(date), Is.EqualTo(50.0).Within(1e-9));
            Assert.That(model.BrandBonus(date), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(model.EfficiencyMultiplier(date), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(model.Traits.TotalShortfall(date), Is.Zero);
        }

        [Test]
        public void AModelNobodyMaintainsSlipsBelowParOnEveryAxis()
        {
            var released = GameDate.FromCalendar(2023, 1, 1);
            var model = new DeployedModel("Neglected", ArchitectureId.DenseTransformer, 45, released, 3e10, 1.0);
            var later = GameDate.FromCalendar(2025, 6, 1);

            Assert.That(model.EffectiveCapability(later), Is.LessThan(45.0));
            Assert.That(model.BrandBonus(later), Is.LessThan(0.0));
            Assert.That(model.EfficiencyMultiplier(later), Is.GreaterThan(1.0),
                "Never optimising means every token costs more than a rival's.");
            Assert.That(model.Traits.TotalShortfall(later), Is.GreaterThan(0));
        }

        [Test]
        public void AnUpgradeCostsCashUpFrontAndThenTimeAndCompute()
        {
            var simulation = ShippedCompany();
            var cashBefore = simulation.State.CashUsd;

            var grid = simulation.UpgradeGrid(0);
            Assert.That(grid.Count, Is.EqualTo(ModelTraitSetLimits.TraitCount),
                "The grid always shows every trait, available or not.");

            var levelBefore = simulation.State.DeployedModels[0].Traits.GetLevel(ModelTrait.Efficiency);
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Efficiency, out var reason), Is.True, reason);

            Assert.That(simulation.State.CashUsd, Is.LessThan(cashBefore));
            Assert.That(simulation.State.UpgradeProjects.Count, Is.EqualTo(1));
            Assert.That(simulation.State.DeployedModels[0].Traits.GetLevel(ModelTrait.Efficiency),
                Is.EqualTo(levelBefore), "The level does not move until the work is done.");

            simulation.Advance(120);

            Assert.That(simulation.State.UpgradeProjects, Is.Empty);
            Assert.That(simulation.State.DeployedModels[0].Traits.GetLevel(ModelTrait.Efficiency),
                Is.EqualTo(levelBefore + 1));
        }

        [Test]
        public void TheSameTraitCannotBeUpgradedTwiceAtOnce()
        {
            var simulation = ShippedCompany();
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out _), Is.True);
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out var reason), Is.False);
            Assert.That(reason, Does.Contain("already being worked on"));
        }

        [Test]
        public void OnlyThreeUpgradeProgrammesRunAtOnce()
        {
            var simulation = ShippedCompany();

            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out _), Is.True);
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Coding, out _), Is.True);
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Efficiency, out _), Is.True);
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.Latency, out var reason), Is.False);
            Assert.That(reason, Does.Contain("3 upgrade programmes"));
        }

        [Test]
        public void ATraitTheFieldHasNotSolvedYetCannotBeBought()
        {
            var simulation = ShippedCompany();

            // Tool use is not a solved problem in early 2022 at any price.
            Assert.That(simulation.TryStartUpgrade(0, ModelTrait.ToolUse, out var reason), Is.False);
            Assert.That(reason, Does.Contain("2023-06-01"));
        }

        [Test]
        public void UpgradesAndTrainingCompeteForTheSameCluster()
        {
            var alone = ShippedCompany();
            alone.TryStartTraining(new ModelBlueprint(
                "Muse 2", ArchitectureId.DenseTransformer, 30, 600, DatasetSource.WebCrawl), out _);
            alone.Advance(10);

            var shared = ShippedCompany();
            shared.TryStartTraining(new ModelBlueprint(
                "Muse 2", ArchitectureId.DenseTransformer, 30, 600, DatasetSource.WebCrawl), out _);
            shared.TryStartUpgrade(0, ModelTrait.Reasoning, out _);
            shared.Advance(10);

            Assert.That(shared.State.ActiveRun.Progress, Is.LessThan(alone.State.ActiveRun.Progress),
                "A run sharing the cluster with an upgrade programme must be slower.");
        }

        // ------------------------------------------------------------------ rivals

        [Test]
        public void RivalsShipOnTheirOwnAndTheFrontierClimbsBetweenLaunches()
        {
            var field = CompetitorField.CreateFromCatalog();
            var random = new DeterministicRandom(7);

            var atStart = field.FrontierCapability(GameDate.Start);
            for (var day = 0; day <= 900; day++)
            {
                field.Tick(new GameDate(day), 0.0, random);
            }

            var afterTwoYears = field.FrontierCapability(new GameDate(900));
            var tenDaysLater = field.FrontierCapability(new GameDate(910));

            Assert.That(atStart, Is.EqualTo(CompetitorField.IncumbentCapability).Within(0.001));
            Assert.That(afterTwoYears, Is.GreaterThan(atStart + 15.0));
            Assert.That(tenDaysLater, Is.GreaterThan(afterTwoYears),
                "Capability drifts up between launches because rivals run upgrades too.");
        }

        [Test]
        public void APatientLabSitsOutAHardwareTransitionOnPurpose()
        {
            var field = CompetitorField.CreateFromCatalog();
            var random = new DeterministicRandom(11);
            var sawSomebodyWait = false;

            for (var day = 0; day <= 1800; day++)
            {
                field.Tick(new GameDate(day), 0.0, random);
                if (field.LabsWaitingForHardware().Count > 0)
                {
                    sawSomebodyWait = true;
                    break;
                }
            }

            Assert.That(sawSomebodyWait, Is.True,
                "At least one patient lab must decide to wait for better silicon during the campaign.");

            var waiter = field.LabsWaitingForHardware()[0];
            Assert.That(waiter.AccumulatedDelayDays, Is.GreaterThan(0));
            Assert.That(waiter.WaitingFor, Is.Not.EqualTo(HardwareGenerationId.None));
        }

        [Test]
        public void TheRivalFieldIsDeterministicForAGivenSeed()
        {
            static double Run(uint seed)
            {
                var field = CompetitorField.CreateFromCatalog();
                var random = new DeterministicRandom(seed);
                for (var day = 0; day <= 1200; day++)
                {
                    field.Tick(new GameDate(day), 40.0, random);
                }

                return field.FrontierCapability(new GameDate(1200));
            }

            Assert.That(Run(99), Is.EqualTo(Run(99)));
        }

        // ------------------------------------------------------------------ intelligence

        [Test]
        public void ABetterDeskSeesFurtherAheadAndIsRightMoreOften()
        {
            Assert.That(IntelligenceService.Accuracy(IntelTier.ScoutingTeam),
                Is.GreaterThan(IntelligenceService.Accuracy(IntelTier.SupplyChainRumor)));
            Assert.That(IntelligenceService.LeadTimeDays(IntelTier.ScoutingTeam),
                Is.GreaterThan(IntelligenceService.LeadTimeDays(IntelTier.SupplyChainRumor)));
            Assert.That(IntelligenceService.MonthlyRetainerUsd(IntelTier.PublicNews), Is.Zero);
        }

        [Test]
        public void ADeskIsAlwaysMoreConfidentThanItIsAccurate()
        {
            var random = new DeterministicRandom(5);
            foreach (var tier in new[] { IntelTier.SupplyChainRumor, IntelTier.AnalystReport, IntelTier.ScoutingTeam })
            {
                var total = 0.0;
                for (var index = 0; index < 400; index++)
                {
                    total += IntelligenceService.StatedConfidence(tier, random);
                }

                Assert.That(total / 400.0, Is.GreaterThan(IntelligenceService.Accuracy(tier)),
                    $"{tier} should claim more than it delivers.");
            }
        }

        [Test]
        public void CheapIntelligenceIsWrongOftenEnoughToHurt()
        {
            var field = CompetitorField.CreateFromCatalog();
            var random = new DeterministicRandom(21);
            var wrong = 0;

            for (var index = 0; index < 400; index++)
            {
                var signal = IntelligenceService.Generate(
                    IntelTier.SupplyChainRumor, GameDate.FromCalendar(2023, 6, 1), field, random);
                if (!signal.IsCorrect)
                {
                    wrong++;
                }
            }

            Assert.That(wrong, Is.InRange(120, 220), $"{wrong} of 400 rumours were wrong.");
        }

        [Test]
        public void ARetainerIsBilledEveryDayWhetherOrNotANoteArrives()
        {
            var quiet = ShippedCompany();
            var paying = ShippedCompany();
            paying.SetIntelSubscription(IntelTier.ScoutingTeam);

            quiet.Advance(90);
            paying.Advance(90);

            Assert.That(paying.State.CashUsd, Is.LessThan(quiet.State.CashUsd));
            Assert.That(paying.State.Signals.Count, Is.GreaterThan(0));
            Assert.That(quiet.State.Signals, Is.Empty);
        }

        // ------------------------------------------------------------------ ranking

        [Test]
        public void TheBoardIsSortedAndTheCompanyAppearsOnIt()
        {
            var simulation = ShippedCompany();
            simulation.Advance(400);

            var board = simulation.Ranking();

            Assert.That(board.Count, Is.GreaterThan(1));
            for (var index = 1; index < board.Count; index++)
            {
                Assert.That(board[index].Score, Is.LessThanOrEqualTo(board[index - 1].Score));
                Assert.That(board[index].Position, Is.EqualTo(index + 1));
            }

            Assert.That(RankingBoard.PlayerPosition(board), Is.GreaterThan(0));
        }

        [Test]
        public void TheBoardScoreIsBuiltFromTheSameNumbersTheEconomyRunsOn()
        {
            // The complaint about Smartphone Tycoon was ratings that ignored the specifications.
            // The score here is a fixed function of capability, share and brand and cannot drift.
            var low = RankingBoard.Score(30, 0.05, 0.2);
            var better = RankingBoard.Score(60, 0.05, 0.2);
            var bestShare = RankingBoard.Score(60, 0.40, 0.2);

            Assert.That(better, Is.GreaterThan(low));
            Assert.That(bestShare, Is.GreaterThan(better));
            Assert.That(RankingBoard.Score(100, 1.0, 1.0), Is.EqualTo(100.0).Within(1e-9));
            Assert.That(RankingBoard.Score(0, 0, 0), Is.Zero);
        }
    }
}
