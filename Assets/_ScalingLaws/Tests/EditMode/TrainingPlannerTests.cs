using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class TrainingPlannerTests
    {
        private static ComputeProfile RentedFleet(int units, GameDate date, out MarketConditions market)
        {
            market = MarketModel.Evaluate(date);
            var pool = new ComputePool();
            pool.SetRentedAcceleratorEquivalent(units, market.RentableGeneration);
            return pool.BuildProfile(date, market);
        }

        [Test]
        public void AFirstModelIn2022IsAffordableAndTakesWeeksNotYears()
        {
            var profile = RentedFleet(500, GameDate.Start, out var market);
            var blueprint = new ModelBlueprint(
                "Muse 1", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.WebCrawl);

            var projection = TrainingPlanner.Project(blueprint, profile, market, 0.0);

            Assert.That(projection.IsFeasible, Is.True, projection.BlockingReason);
            Assert.That(projection.TrainingDays, Is.InRange(3, 40), $"Run took {projection.TrainingDays} days.");
            Assert.That(projection.ProjectedCapability, Is.InRange(14.0, 30.0),
                $"Capability was {projection.ProjectedCapability:0.0}.");
            Assert.That(projection.TotalCashCostUsd, Is.LessThan(CompanyState.StartingCashUsd));
        }

        [Test]
        public void ARunWithNoDataMixIsRefusedBeforeAnythingIsSpent()
        {
            var profile = RentedFleet(500, GameDate.Start, out var market);
            var blueprint = new ModelBlueprint(
                "Blank", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.None);

            var projection = TrainingPlanner.Project(blueprint, profile, market, 0.0);

            Assert.That(projection.IsFeasible, Is.False);
            Assert.That(projection.BlockingReason, Does.Contain("data"));
        }

        [Test]
        public void AskingForMoreTokensThanTheMixHoldsIsBlockedAndSaysByHowMuch()
        {
            var profile = RentedFleet(2000, GameDate.Start, out var market);

            // Open crawl carries 1800B tokens. Asking for 6000B is asking for data that does not exist.
            var blueprint = new ModelBlueprint(
                "Thirsty", ArchitectureId.DenseTransformer, 300, 6000, DatasetSource.WebCrawl);

            var projection = TrainingPlanner.Project(blueprint, profile, market, 0.0);

            Assert.That(projection.IsFeasible, Is.False);
            Assert.That(projection.BlockingReason, Does.Contain("data mix supplies"));
            Assert.That(projection.Blend.IsSufficient, Is.False);
            Assert.That(projection.Blend.AvailableTokensBillions, Is.EqualTo(1800.0).Within(0.5));
        }

        [Test]
        public void AModelTooLargeForTheFleetMemoryIsBlocked()
        {
            var profile = RentedFleet(8, GameDate.Start, out var market);
            var blueprint = new ModelBlueprint(
                "Oversized", ArchitectureId.DenseTransformer, 500, 1000, DatasetSource.WebCrawl);

            var projection = TrainingPlanner.Project(blueprint, profile, market, 0.0);

            Assert.That(projection.IsFeasible, Is.False);
            Assert.That(projection.BlockingReason, Does.Contain("memory"));
            Assert.That(projection.MemoryRequiredGigabytes, Is.GreaterThan(projection.MemoryAvailableGigabytes));
        }

        [Test]
        public void AnArchitectureThatDoesNotExistYetCannotBeUsed()
        {
            var profile = RentedFleet(500, GameDate.Start, out var market);
            var blueprint = new ModelBlueprint(
                "Ahead of its time", ArchitectureId.ReasoningMixture, 20, 400, DatasetSource.WebCrawl);

            var projection = TrainingPlanner.Project(blueprint, profile, market, 0.0);

            Assert.That(projection.IsFeasible, Is.False);
            Assert.That(projection.BlockingReason, Does.Contain("2025-02-01"));
        }

        [Test]
        public void BetterDataBeatsMoreDataAtTheSameTokenCount()
        {
            var date = GameDate.FromCalendar(2024, 1, 1);
            var profile = RentedFleet(2000, date, out var market);

            var crawlOnly = new ModelBlueprint(
                "Crawl", ArchitectureId.DenseTransformer, 40, 800, DatasetSource.WebCrawl);
            var curated = crawlOnly
                .WithName("Curated")
                .WithDataSources(DatasetSource.CuratedWeb | DatasetSource.CodeCorpus | DatasetSource.LicensedBooks);

            var crawlProjection = TrainingPlanner.Project(crawlOnly, profile, market, 0.0);
            var curatedProjection = TrainingPlanner.Project(curated, profile, market, 0.0);

            Assert.That(curatedProjection.ProjectedCapability, Is.GreaterThan(crawlProjection.ProjectedCapability));
            Assert.That(curatedProjection.TrainingPetaflopDays,
                Is.EqualTo(crawlProjection.TrainingPetaflopDays).Within(0.01),
                "Data quality changes the result, not the compute bill.");
        }

        [Test]
        public void TheOptimalShapeBeatsBothMistakesOnTheSameBudget()
        {
            var date = GameDate.FromCalendar(2024, 1, 1);
            var profile = RentedFleet(4000, date, out var market);
            var data = DatasetSource.WebCrawl | DatasetSource.CuratedWeb | DatasetSource.CodeCorpus
                | DatasetSource.LicensedBooks | DatasetSource.AcademicArchive;

            var optimal = TrainingPlanner.OptimalBlueprintForBudget("Balanced", ArchitectureId.DenseTransformer, 5_000, data);
            var tooBig = optimal.WithName("Too big")
                .WithParameters(optimal.ParameterCountBillions * 5.0)
                .WithTokens(optimal.TrainingTokensBillions / 5.0);
            var tooSmall = optimal.WithName("Too small")
                .WithParameters(optimal.ParameterCountBillions / 5.0)
                .WithTokens(optimal.TrainingTokensBillions * 5.0);

            var balanced = TrainingPlanner.Project(optimal, profile, market, 0.0);
            var big = TrainingPlanner.Project(tooBig, profile, market, 0.0);
            var small = TrainingPlanner.Project(tooSmall, profile, market, 0.0);

            Assert.That(balanced.IsFeasible, Is.True, balanced.BlockingReason);
            Assert.That(big.IsFeasible, Is.True, big.BlockingReason);
            Assert.That(small.IsFeasible, Is.True, small.BlockingReason);
            Assert.That(balanced.ShapeEfficiency, Is.GreaterThan(0.98));
            Assert.That(balanced.ProjectedCapability, Is.GreaterThan(big.ProjectedCapability));
            Assert.That(balanced.ProjectedCapability, Is.GreaterThan(small.ProjectedCapability));
            Assert.That(big.IsUndertrained, Is.True);
            Assert.That(small.IsOvertrained, Is.True);
            Assert.That(balanced.TokensPerParameter, Is.InRange(12.0, 32.0));
        }

        [Test]
        public void SparseMixtureBuysTheSameCapabilityForLessCompute()
        {
            var date = GameDate.FromCalendar(2024, 6, 1);
            var profile = RentedFleet(4000, date, out var market);
            var data = DatasetSource.WebCrawl | DatasetSource.CuratedWeb | DatasetSource.CodeCorpus;

            var dense = new ModelBlueprint("Dense", ArchitectureId.DenseTransformer, 200, 4000, data);
            var sparse = dense.WithName("Sparse").WithArchitecture(ArchitectureId.SparseMixture);

            var denseProjection = TrainingPlanner.Project(dense, profile, market, 0.0);
            var sparseProjection = TrainingPlanner.Project(sparse, profile, market, 0.0);

            Assert.That(sparseProjection.TrainingPetaflopDays, Is.LessThan(denseProjection.TrainingPetaflopDays * 0.3));
            Assert.That(sparseProjection.TrainingDays, Is.LessThan(denseProjection.TrainingDays));

            // The saving is not free: quality per parameter is lower.
            Assert.That(sparseProjection.ProjectedCapability, Is.LessThan(denseProjection.ProjectedCapability));
        }

        [Test]
        public void ProjectingAtFullFleetWhenOnlyHalfIsTrainingWouldLie()
        {
            var profile = RentedFleet(1000, GameDate.FromCalendar(2023, 6, 1), out var market);
            var blueprint = new ModelBlueprint(
                "Split", ArchitectureId.DenseTransformer, 40, 800, DatasetSource.WebCrawl);

            var wholeFleet = TrainingPlanner.Project(blueprint, profile, market, 0.0);
            var halfFleet = TrainingPlanner.Project(blueprint, profile, market, 0.0, 0.5);

            Assert.That(halfFleet.TrainingDays, Is.GreaterThan(wholeFleet.TrainingDays));
            Assert.That(halfFleet.ProjectedCapability, Is.EqualTo(wholeFleet.ProjectedCapability).Within(0.001),
                "Splitting the fleet changes the calendar, not the model.");
        }

        [Test]
        public void TheSameRunGetsCheaperAsTrainingRecipesImprove()
        {
            var blueprint = new ModelBlueprint(
                "Fixed shape", ArchitectureId.DenseTransformer, 70, 1400, DatasetSource.WebCrawl);

            var early = RentedFleet(2000, GameDate.FromCalendar(2022, 6, 1), out var earlyMarket);
            var late = RentedFleet(2000, GameDate.FromCalendar(2026, 6, 1), out var lateMarket);

            var earlyProjection = TrainingPlanner.Project(blueprint, early, earlyMarket, 0.0);
            var lateProjection = TrainingPlanner.Project(blueprint, late, lateMarket, 0.0);

            Assert.That(lateProjection.ProjectedCapability, Is.GreaterThan(earlyProjection.ProjectedCapability + 5.0),
                "Four years of better recipes must be worth real capability on the same run.");
        }
    }
}
