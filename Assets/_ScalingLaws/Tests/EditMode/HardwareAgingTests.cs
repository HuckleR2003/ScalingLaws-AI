using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class HardwareAgingTests
    {
        [Test]
        public void CatalogEntriesAreInternallyConsistent()
        {
            foreach (var generation in HardwareCatalog.All)
            {
                if (generation.Class == HardwareClass.Accelerator)
                {
                    Assert.That(generation.PetaflopsPerUnit, Is.GreaterThan(0.0), $"{generation.Id} produces no FLOPs.");
                    Assert.That(generation.MemoryGigabytes, Is.GreaterThan(0), $"{generation.Id} has no memory.");
                    Assert.That(generation.AcceleratorsServed, Is.Zero, $"{generation.Id} should not feed other accelerators.");
                }
                else
                {
                    Assert.That(generation.AcceleratorsServed, Is.GreaterThan(0), $"{generation.Id} feeds nothing.");
                    Assert.That(generation.PetaflopsPerUnit, Is.Zero, $"{generation.Id} should not produce FLOPs.");
                }

                Assert.That(generation.LaunchPriceUsd, Is.GreaterThan(0));
                Assert.That(generation.PowerKilowatts, Is.GreaterThan(0.0));
            }
        }

        [Test]
        public void EveryProjectionIsLabelledAndEveryShippedProductIsNot()
        {
            // Same honesty rule as an estimated sensor reading: a roadmap guess must never be
            // indistinguishable from a shipped part.
            foreach (var generation in HardwareCatalog.All)
            {
                var isFuture = generation.ReleaseDate > GameDate.FromCalendar(2026, 8, 2);
                Assert.That(generation.IsProjection, Is.EqualTo(isFuture),
                    $"{generation.Id} ships {generation.ReleaseDate} but IsProjection is {generation.IsProjection}.");
            }
        }

        [Test]
        public void FrontierTracksTheCalendar()
        {
            HardwareCatalog.TryGetFrontier(GameDate.FromCalendar(2022, 1, 1), HardwareClass.Accelerator, out var early);
            HardwareCatalog.TryGetFrontier(GameDate.FromCalendar(2023, 1, 1), HardwareClass.Accelerator, out var afterHopper);
            HardwareCatalog.TryGetFrontier(GameDate.FromCalendar(2025, 6, 1), HardwareClass.Accelerator, out var afterBlackwell);

            Assert.That(early.Id, Is.EqualTo(HardwareGenerationId.AcceleratorA100));
            Assert.That(afterHopper.Id, Is.EqualTo(HardwareGenerationId.AcceleratorH100));
            Assert.That(afterBlackwell.Id, Is.EqualTo(HardwareGenerationId.AcceleratorB200));
        }

        [Test]
        public void ValueFallsWithAgeEvenWithNothingNewOnTheMarket()
        {
            // The last entry in the catalog, so nothing newer can muddy the measurement.
            var purchase = GameDate.FromCalendar(2029, 1, 1);
            var generation = HardwareCatalog.Get(HardwareGenerationId.AcceleratorNext);

            var atPurchase = HardwareValuation.ResidualValuePerUnitUsd(
                generation.Id, generation.LaunchPriceUsd, purchase, purchase);
            var oneHalfLifeLater = HardwareValuation.ResidualValuePerUnitUsd(
                generation.Id, generation.LaunchPriceUsd, purchase, purchase.AddDays(generation.ValueHalfLifeDays));

            Assert.That(atPurchase, Is.EqualTo(generation.LaunchPriceUsd).Within(1.0));
            Assert.That(oneHalfLifeLater, Is.LessThan(atPurchase * 0.55));
        }

        [Test]
        public void BuyingAtLaunchAndHoldingThroughThreeSuccessorsCostsMostOfTheCapital()
        {
            // This is the lesson the game is built around, expressed as a number.
            var launchDay = GameDate.FromCalendar(2022, 10, 1);
            var fourYearsOn = GameDate.FromCalendar(2026, 6, 1);

            var residual = HardwareValuation.ResidualValuePerUnitUsd(
                HardwareGenerationId.AcceleratorH100, 30_000, launchDay, fourYearsOn);

            Assert.That(residual, Is.LessThan(30_000 * 0.30), $"Residual was ${residual:N0}.");
            Assert.That(residual, Is.GreaterThanOrEqualTo(30_000 * HardwareValuation.ScrapValueFraction));
        }

        [Test]
        public void WaitingForTheSuccessorBeatsBuyingTheOutgoingPart()
        {
            var settlementDay = GameDate.FromCalendar(2026, 6, 1);

            // Same money, same settlement day. One buyer took the outgoing part at launch, the other
            // waited two years and bought the part that replaced it.
            var earlyBuyer = HardwareValuation.ResidualValuePerUnitUsd(
                HardwareGenerationId.AcceleratorH100, 30_000, GameDate.FromCalendar(2022, 10, 1), settlementDay);
            var patientBuyer = HardwareValuation.ResidualValuePerUnitUsd(
                HardwareGenerationId.AcceleratorB200, 30_000, GameDate.FromCalendar(2025, 1, 15), settlementDay);

            Assert.That(patientBuyer, Is.GreaterThan(earlyBuyer * 1.5),
                $"Early ${earlyBuyer:N0} against patient ${patientBuyer:N0}.");
        }

        [Test]
        public void ASuccessorThatShippedBeforeThePurchaseCannotTakeValueTwice()
        {
            // Buying an H100 in 2025 already reflects that Blackwell exists. It must not be
            // penalised again for a launch that had happened before the money moved.
            var lateBuy = GameDate.FromCalendar(2025, 6, 1);
            var oneYearOn = lateBuy.AddDays(365);

            var residual = HardwareValuation.ResidualValuePerUnitUsd(
                HardwareGenerationId.AcceleratorH100, 30_000, lateBuy, oneYearOn);
            var timeOnlyFloor = 30_000 * 0.5;

            Assert.That(residual, Is.GreaterThan(timeOnlyFloor * 0.6), $"Residual was ${residual:N0}.");
        }

        [Test]
        public void DepreciationIsChargedEveryDayAndIsNeverNegative()
        {
            var asset = new HardwareAsset(
                HardwareGenerationId.AcceleratorH100,
                ComputeTier.ColocatedServers,
                100,
                GameDate.FromCalendar(2023, 1, 1),
                30_000,
                45);

            var daily = HardwareValuation.DailyDepreciationUsd(asset, GameDate.FromCalendar(2023, 6, 1));

            Assert.That(daily, Is.GreaterThan(0.0));
            Assert.That(HardwareValuation.DailyDepreciationUsd(asset, GameDate.FromCalendar(2030, 1, 1)),
                Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void AnOldFleetLosesGroundOnPerformancePerDollar()
        {
            var index2026 = HardwareValuation.PerformancePerDollarIndex(
                HardwareGenerationId.AcceleratorA100, GameDate.FromCalendar(2026, 6, 1));

            Assert.That(index2026, Is.LessThan(0.75), $"A100 index in 2026 was {index2026:0.00}.");
        }

        [Test]
        public void HardwareInTransitIsPaidForButProducesNothing()
        {
            var purchase = GameDate.FromCalendar(2024, 1, 1);
            var asset = new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 64, purchase, 30_000, 45);

            Assert.That(asset.IsOnline(purchase), Is.False);
            Assert.That(asset.IsInTransit(purchase), Is.True);
            Assert.That(asset.DaysUntilOnline(purchase), Is.EqualTo(45));
            Assert.That(asset.IsOnline(purchase.AddDays(45)), Is.True);

            // Value starts falling from the day the money left, not the day the crates arrived.
            Assert.That(HardwareValuation.DailyDepreciationUsd(asset, purchase.AddDays(10)), Is.GreaterThan(0.0));
        }

        [Test]
        public void ScalingEfficiencyOnlyBitesOnLargeClusters()
        {
            Assert.That(ComputePool.ScalingEfficiency(128), Is.EqualTo(1.0));
            Assert.That(ComputePool.ScalingEfficiency(256), Is.EqualTo(1.0));
            Assert.That(ComputePool.ScalingEfficiency(4096), Is.LessThan(1.0));
            Assert.That(ComputePool.ScalingEfficiency(200_000),
                Is.GreaterThanOrEqualTo(ComputePool.MinimumScalingEfficiency));
        }
    }
}
