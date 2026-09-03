using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class MarketModelTests
    {
        [Test]
        public void PricePerTokenOnlyEverFalls()
        {
            var previous = double.MaxValue;
            for (var year = 2022; year <= 2030; year++)
            {
                var price = MarketModel.PriceOn(GameDate.FromCalendar(year, 1, 1));
                Assert.That(price, Is.LessThanOrEqualTo(previous), $"Price rose in {year}.");
                Assert.That(price, Is.GreaterThanOrEqualTo(MarketModel.PriceFloorPerMillionTokensUsd));
                previous = price;
            }
        }

        [Test]
        public void DemandGrowsFastThenFlattens()
        {
            var y2022 = MarketModel.DemandOn(GameDate.FromCalendar(2022, 1, 1));
            var y2024 = MarketModel.DemandOn(GameDate.FromCalendar(2024, 1, 1));
            var y2026 = MarketModel.DemandOn(GameDate.FromCalendar(2026, 1, 1));
            var y2030 = MarketModel.DemandOn(GameDate.FromCalendar(2030, 1, 1));

            Assert.That(y2024, Is.GreaterThan(y2022 * 50.0), "2023 and 2024 were the explosion.");
            Assert.That(y2026, Is.GreaterThan(y2024));
            Assert.That(y2030, Is.LessThan(MarketModel.DemandCeilingBillionTokensPerDay));
            Assert.That(y2030 / y2026, Is.LessThan(y2026 / y2024), "Growth must decelerate, not compound forever.");
        }

        [Test]
        public void TheFrontierKeepsClimbingAndNeverWaitsForThePlayer()
        {
            var checkpoints = new[]
            {
                GameDate.FromCalendar(2022, 6, 1),
                GameDate.FromCalendar(2023, 6, 1),
                GameDate.FromCalendar(2024, 6, 1),
                GameDate.FromCalendar(2025, 6, 1),
                GameDate.FromCalendar(2026, 6, 1),
                GameDate.FromCalendar(2029, 6, 1)
            };

            var previous = 0.0;
            foreach (var date in checkpoints)
            {
                var frontier = CompetitorCatalog.FrontierCapabilityOn(date);
                Assert.That(frontier, Is.GreaterThan(previous), $"Frontier stalled at {date}.");
                previous = frontier;
            }

            // Past the last tabulated release the projection takes over rather than flatlining.
            var afterTable = CompetitorCatalog.FrontierCapabilityOn(CompetitorCatalog.LastKnownRelease.AddDays(730));
            var atTable = CompetitorCatalog.FrontierCapabilityOn(CompetitorCatalog.LastKnownRelease);
            Assert.That(afterTable - atTable,
                Is.EqualTo(2.0 * CompetitorCatalog.ProjectedCapabilityGainPerYear).Within(0.5));
        }

        [Test]
        public void ScarcityPeaksInTheShortageAndEasesAfterwards()
        {
            var early = MarketModel.ScarcityOn(GameDate.FromCalendar(2022, 1, 1));
            var peak = MarketModel.ScarcityOn(GameDate.FromCalendar(2023, 6, 1));
            var later = MarketModel.ScarcityOn(GameDate.FromCalendar(2026, 6, 1));

            Assert.That(peak, Is.EqualTo(1.0).Within(0.001));
            Assert.That(early, Is.LessThan(peak));
            Assert.That(later, Is.LessThan(peak));
            Assert.That(MarketModel.ScarcityOn(GameDate.FromCalendar(2035, 1, 1)), Is.InRange(0.0, 1.0));
        }

        [Test]
        public void CloudsRentTheFrontierPartButOnlyAfterALag()
        {
            // H100 ships in October 2022, so it cannot be rented that month.
            var atLaunch = MarketModel.RentableGenerationOn(GameDate.FromCalendar(2022, 10, 15));
            var halfAYearOn = MarketModel.RentableGenerationOn(GameDate.FromCalendar(2023, 5, 1));

            Assert.That(atLaunch, Is.EqualTo(HardwareGenerationId.AcceleratorA100));
            Assert.That(halfAYearOn, Is.EqualTo(HardwareGenerationId.AcceleratorH100));
        }

        [Test]
        public void RentalPricesRiseWithTheShortage()
        {
            var calm = MarketModel.RentPricePerPetaflopHourUsd(HardwareGenerationId.AcceleratorH100, 0.0);
            var crunch = MarketModel.RentPricePerPetaflopHourUsd(HardwareGenerationId.AcceleratorH100, 1.0);

            Assert.That(crunch, Is.GreaterThan(calm * 2.0));
        }

        [Test]
        public void BetterRecipesMakeTheSameFlopsGoFurther()
        {
            // **The published trend, measured on its own.** What efficiency actually is on a given
            // day is the trend plus whatever the world is doing to it, and this assertion is about
            // the doubling law rather than about the weather in 2026.
            var atStart = MarketModel.BaseAlgorithmicEfficiencyOn(GameDate.Start);
            var fourYearsOn = MarketModel.BaseAlgorithmicEfficiencyOn(GameDate.FromCalendar(2026, 1, 1));

            Assert.That(atStart, Is.EqualTo(1.0).Within(0.001));
            Assert.That(fourYearsOn, Is.EqualTo(16.0).Within(0.5));
            Assert.That(MarketModel.AlgorithmicEfficiencyOn(GameDate.FromCalendar(2040, 1, 1)),
                Is.LessThanOrEqualTo(MarketModel.MaximumAlgorithmicEfficiency));

            // And the world does reach it, or splitting the two would have quietly disconnected
            // the calendar from the one curve nobody would notice going missing. Reasoning models
            // land in September 2024 and are worth thirty per cent while the window is open.
            var during = GameDate.FromCalendar(2024, 11, 1);

            Assert.That(MarketModel.AlgorithmicEfficiencyOn(during),
                Is.GreaterThan(MarketModel.BaseAlgorithmicEfficiencyOn(during)),
                "The world calendar no longer reaches algorithmic efficiency.");
        }

        [Test]
        public void ShareIsSplitAgainstRealRivalsAndNeverHandedOver()
        {
            var market = MarketModel.Evaluate(GameDate.FromCalendar(2024, 6, 1));
            var strong = new[]
            {
                new DeployedModel("Strong", ArchitectureId.SparseMixture, 58, GameDate.FromCalendar(2024, 5, 1), 5e10, 1.0)
            };
            var stale = new[]
            {
                new DeployedModel("Stale", ArchitectureId.DenseTransformer, 21, GameDate.FromCalendar(2022, 6, 1), 5e10, 1.0)
            };

            var strongShare = MarketShareModel.PlayerShare(strong, 0.5, market);
            var staleShare = MarketShareModel.PlayerShare(stale, 0.5, market);
            var noModelShare = MarketShareModel.PlayerShare(new DeployedModel[0], 0.5, market);

            Assert.That(strongShare, Is.GreaterThan(staleShare * 5.0));
            Assert.That(strongShare, Is.LessThan(1.0), "Rivals always keep some of the market.");
            Assert.That(noModelShare, Is.Zero);
        }

        [Test]
        public void CuttingPriceBuysShareAndNothingElseDoes()
        {
            var market = MarketModel.Evaluate(GameDate.FromCalendar(2024, 6, 1));
            var listPrice = new[]
            {
                new DeployedModel("List", ArchitectureId.SparseMixture, 45, GameDate.FromCalendar(2024, 5, 1), 5e10, 1.0)
            };
            var discounted = new[]
            {
                new DeployedModel("Discount", ArchitectureId.SparseMixture, 45, GameDate.FromCalendar(2024, 5, 1), 5e10, 0.25)
            };

            Assert.That(MarketShareModel.PlayerShare(discounted, 0.4, market),
                Is.GreaterThan(MarketShareModel.PlayerShare(listPrice, 0.4, market)));
        }

        [Test]
        public void AModelNobodyReplacesQuietlyStopsBeingChosen()
        {
            var model = new DeployedModel(
                "Evergreen", ArchitectureId.DenseTransformer, 46, GameDate.FromCalendar(2023, 3, 1), 5e10, 1.0);
            var fleet = new[] { model };

            var atLaunch = MarketShareModel.PlayerShare(fleet, 0.5, MarketModel.Evaluate(GameDate.FromCalendar(2023, 4, 1)));
            var threeYearsOn = MarketShareModel.PlayerShare(fleet, 0.5, MarketModel.Evaluate(GameDate.FromCalendar(2026, 4, 1)));

            Assert.That(threeYearsOn, Is.LessThan(atLaunch * 0.2),
                $"Share went from {atLaunch:P2} to {threeYearsOn:P2}; a frozen model must decay.");
        }
    }
}
