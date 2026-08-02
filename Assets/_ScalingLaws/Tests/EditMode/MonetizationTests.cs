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
    /// How the company charges, how much it gives away, and what it spends being noticed.
    /// </summary>
    public sealed class MonetizationTests
    {
        private static CompanySimulation Live(long cash, GameDate date, double capability = 50.0)
        {
            var state = new CompanyState("Seller", 63)
            {
                Date = date,
                CashUsd = cash
            };
            state.AddDeployedModel(new DeployedModel(
                "Flagship", ArchitectureId.SparseMixture, capability, date, 2e10, 1.0));
            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(600.0);
            return simulation;
        }

        [Test]
        public void ACompanyStartsMeteredAtTheMarketRateWithNoFreeTier()
        {
            var policy = new CompanyState("Default").Monetization;

            Assert.That(policy.Model, Is.EqualTo(PricingModel.PayPerToken));
            Assert.That(policy.PaidPriceMultiplier, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(policy.FreeTierTokensPerUserPerDay, Is.Zero);
            Assert.That(policy.TotalMarketingDailyUsd, Is.Zero);
            Assert.That(policy.FreeShareOfTokens, Is.EqualTo(MonetizationCatalog.BaseFreeShare).Within(1e-9));
        }

        [Test]
        public void ASubscriptionSetsItsOwnRateAndIgnoresTheMarket()
        {
            var policy = new MonetizationPolicy { Model = PricingModel.Subscription };
            policy.SubscriptionPriceUsdPerMonth = 20.0;

            var cheapMarket = policy.RatePerMillionTokensUsd(0.5);
            var expensiveMarket = policy.RatePerMillionTokensUsd(18.0);

            Assert.That(cheapMarket, Is.EqualTo(expensiveMarket).Within(1e-9),
                "A fee you set does not move when the market does. That is the whole trade.");

            var metered = new MonetizationPolicy { Model = PricingModel.PayPerToken };
            Assert.That(metered.RatePerMillionTokensUsd(18.0),
                Is.GreaterThan(metered.RatePerMillionTokensUsd(0.5)),
                "Metered pricing has to follow the market down.");
        }

        [Test]
        public void FreeOnlyEarnsNothingAtAll()
        {
            var policy = new MonetizationPolicy { Model = PricingModel.FreeOnly };

            Assert.That(policy.RatePerMillionTokensUsd(12.0), Is.Zero);
            Assert.That(policy.FreeShareOfTokens, Is.EqualTo(1.0));
            Assert.That(policy.ReachMultiplier, Is.GreaterThan(1.0), "It does buy reach.");
        }

        [Test]
        public void AGenerousFreeTierBuysReachAndGivesAwayMostOfWhatItServes()
        {
            var mean = new MonetizationPolicy { FreeTierTokensPerUserPerDay = 0.0 };
            var generous = new MonetizationPolicy
            {
                FreeTierTokensPerUserPerDay = MonetizationCatalog.GenerousFreeTierTokensPerDay
            };

            Assert.That(generous.ReachMultiplier, Is.GreaterThan(mean.ReachMultiplier));
            Assert.That(generous.FreeShareOfTokens, Is.GreaterThan(0.6),
                "At maximum generosity most of what is served has to earn nothing.");
            Assert.That(mean.FreeShareOfTokens, Is.LessThan(0.15));
        }

        [Test]
        public void TheFreeTierTrapIsReal()
        {
            // The strategic point of the whole system. The generous company reaches more people and
            // can still end up with less money, because serving capacity does not care which kind of
            // token it is producing.
            var stingy = Live(200_000_000, GameDate.FromCalendar(2024, 6, 1));
            var generous = Live(200_000_000, GameDate.FromCalendar(2024, 6, 1));
            generous.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            stingy.Advance(365);
            generous.Advance(365);

            Assert.That(generous.State.LifetimeFreeTokensBillions,
                Is.GreaterThan(stingy.State.LifetimeFreeTokensBillions * 3.0),
                "A generous tier has to give away far more.");
            Assert.That(generous.State.LifetimeRevenueUsd, Is.LessThan(stingy.State.LifetimeRevenueUsd),
                "And on identical capacity it has to earn less doing it.");
        }

        [Test]
        public void GivingTokensAwayStillCostsTheSameToServe()
        {
            var simulation = Live(200_000_000, GameDate.FromCalendar(2024, 6, 1));
            simulation.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            var report = simulation.AdvanceDay();

            Assert.That(simulation.State.FreeTokensServedBillions, Is.GreaterThan(0.0));
            Assert.That(report.OperatingCostUsd, Is.GreaterThan(0L),
                "The fleet bills for free tokens exactly as it bills for paid ones.");
        }

        [Test]
        public void UndercuttingTheMarketWinsShareAndChargingOverItLosesShare()
        {
            var cheap = Live(200_000_000, GameDate.FromCalendar(2024, 6, 1));
            cheap.State.Monetization.PaidPriceMultiplier = 0.35;

            var dear = Live(200_000_000, GameDate.FromCalendar(2024, 6, 1));
            dear.State.Monetization.PaidPriceMultiplier = 2.5;

            var cheapShare = cheap.AdvanceDay().MarketShare;
            var dearShare = dear.AdvanceDay().MarketShare;

            Assert.That(cheapShare, Is.GreaterThan(dearShare));
        }

        [Test]
        public void MarketingCostsMoneyEveryDayAndCompanySpendCompounds()
        {
            var quiet = Live(300_000_000, GameDate.FromCalendar(2024, 1, 1));
            var loud = Live(300_000_000, GameDate.FromCalendar(2024, 1, 1));
            loud.State.Monetization.CompanyMarketingDailyUsd = 45_000;

            var reputationBefore = loud.State.Reputation;
            quiet.Advance(365);
            loud.Advance(365);

            Assert.That(loud.State.Reputation, Is.GreaterThan(reputationBefore),
                "Company marketing has to build something.");
            Assert.That(loud.State.Reputation, Is.GreaterThan(quiet.State.Reputation));
            Assert.That(loud.State.LifetimeOperatingCostUsd,
                Is.GreaterThan(quiet.State.LifetimeOperatingCostUsd),
                "And it has to show up as a cost.");
        }

        [Test]
        public void ModelAwarenessEvaporatesWhenTheSpendingStops()
        {
            var simulation = Live(400_000_000, GameDate.FromCalendar(2024, 1, 1));
            simulation.State.Monetization.ModelMarketingDailyUsd = 180_000;
            simulation.Advance(180);

            var peak = simulation.State.Monetization.ModelAwareness;
            Assert.That(peak, Is.GreaterThan(0.0));

            simulation.State.Monetization.ModelMarketingDailyUsd = 0;
            simulation.Advance(240);

            Assert.That(simulation.State.Monetization.ModelAwareness, Is.LessThan(peak * 0.2),
                "Bought attention is rented, not owned.");
        }

        [Test]
        public void MarketingSaturates()
        {
            var small = MonetizationCatalog.EffectFor(50_000, 0.002) * 50_000;
            var large = MonetizationCatalog.EffectFor(5_000_000, 0.002) * 5_000_000;

            Assert.That(large, Is.GreaterThan(small));
            Assert.That(large, Is.LessThan(small * 100.0),
                "A hundred times the budget must not buy a hundred times the attention.");
        }

        [Test]
        public void PricingAndMarketingSurviveASaveAndReload()
        {
            var simulation = Live(200_000_000, GameDate.FromCalendar(2024, 6, 1));
            var policy = simulation.State.Monetization;
            policy.Model = PricingModel.Subscription;
            policy.SubscriptionPriceUsdPerMonth = 45.0;
            policy.FreeTierTokensPerUserPerDay = 90_000;
            policy.CompanyMarketingDailyUsd = 45_000;
            policy.ModelMarketingDailyUsd = 180_000;
            simulation.Advance(120);

            var original = simulation.State;
            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original))));

            Assert.That(restored.Monetization.Model, Is.EqualTo(PricingModel.Subscription));
            Assert.That(restored.Monetization.SubscriptionPriceUsdPerMonth, Is.EqualTo(45.0).Within(1e-9));
            Assert.That(restored.Monetization.FreeTierTokensPerUserPerDay, Is.EqualTo(90_000.0).Within(1e-9));
            Assert.That(restored.Monetization.CompanyMarketingDailyUsd, Is.EqualTo(45_000L));
            Assert.That(restored.Monetization.ModelAwareness,
                Is.EqualTo(original.Monetization.ModelAwareness).Within(1e-9));
            Assert.That(restored.LifetimeFreeTokensBillions,
                Is.EqualTo(original.LifetimeFreeTokensBillions).Within(1e-6));
        }

        [Test]
        public void EveryCampaignIsPricedAndDescribed()
        {
            foreach (var campaign in MonetizationCatalog.All)
            {
                Assert.That(campaign.DailyBudgetUsd, Is.GreaterThan(0L));
                Assert.That(campaign.EffectPerDay, Is.GreaterThan(0.0));
                Assert.That(campaign.Description, Is.Not.Empty);
                Assert.That(campaign.Kind, Is.Not.EqualTo(CampaignKind.None));
            }
        }
    }
}
