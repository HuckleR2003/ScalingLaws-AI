using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Reputation and fans.
    ///
    /// Reputation already existed as one number nudged from five scattered places, and it stays one
    /// number: a second would disagree with the first within a week. What is new is that the daily
    /// change is assembled from named drivers, so each can be moved on its own and the interface can
    /// say which one is responsible.
    ///
    /// Each test below changes exactly one input. A driver that cannot move the outcome on its own is
    /// decoration, and this fixture exists to stop one being added.
    /// </summary>
    public sealed class StandingTests
    {
        private static StandingChange Neutral() => Standing.Today(
            marketShare: 0.0, servedBillions: 0.0, freeTierGenerosity: 0.0,
            daysSinceLastRelease: 0, priceMultiplier: 1.0, marketingIntensity: 0.0,
            reputationGainMultiplier: 1.0);

        [Test]
        public void ACompanyDoingNothingSlowlyFades()
        {
            var change = Neutral();

            Assert.Less(change.Total, 0.0,
                "Standing has to cost something to keep. Otherwise a company that stops trying keeps "
                + "the reputation it earned five years ago forever.");

            Assert.AreEqual(-Standing.DailyDrift, change.Total, 1e-12,
                "With every driver at rest the only movement is the drift.");
        }

        [Test]
        public void ServingPeopleWellIsTheStrongestThingACompanyCanDo()
        {
            var serving = Standing.Today(0.5, 100.0, 0.0, 0, 1.0, 0.0, 1.0);
            var marketing = Standing.Today(0.0, 0.0, 0.0, 0, 1.0, 1.0, 1.0);

            Assert.Greater(serving.Service, 0.0);
            Assert.Greater(serving.Service, marketing.Marketing * 3.0,
                "Marketing at full tilt must not rival actually serving people, or the game says "
                + "advertising is a substitute for a product.");
        }

        [Test]
        public void AGenerousFreeTierBuysGoodwillAndAMeanOneDoesNot()
        {
            var mean = Standing.Today(0.0, 0.0, 0.0, 0, 1.0, 0.0, 1.0);
            var generous = Standing.Today(0.0, 0.0, 1.0, 0, 1.0, 0.0, 1.0);

            Assert.AreEqual(0.0, mean.FreeTier, 1e-12);
            Assert.Greater(generous.FreeTier, 0.0);
            Assert.Greater(generous.Total, mean.Total);
        }

        [Test]
        public void ALineNobodyRefreshesDragsTheCompanyDown()
        {
            var fresh = Standing.Today(0.0, 0.0, 0.0, 30, 1.0, 0.0, 1.0);
            var ageing = Standing.Today(0.0, 0.0, 0.0, Standing.FreshDays + 200, 1.0, 0.0, 1.0);
            var abandoned = Standing.Today(0.0, 0.0, 0.0, Standing.StaleDays + 500, 1.0, 0.0, 1.0);

            Assert.AreEqual(0.0, fresh.ModelAge, 1e-12, "Eight months is not yet stale.");
            Assert.Less(ageing.ModelAge, 0.0);
            Assert.Less(abandoned.ModelAge, ageing.ModelAge);

            Assert.AreEqual(-Standing.StaleLoss, abandoned.ModelAge, 1e-12,
                "Staleness has a floor, or a company left running overnight loses everything.");
        }

        [Test]
        public void PriceCutsAreLikedAndPriceRisesAreResented()
        {
            var par = Standing.Today(0.0, 0.0, 0.0, 0, 1.0, 0.0, 1.0);
            var cheap = Standing.Today(0.0, 0.0, 0.0, 0, 0.5, 0.0, 1.0);
            var dear = Standing.Today(0.0, 0.0, 0.0, 0, 2.0, 0.0, 1.0);

            Assert.AreEqual(0.0, par.Price, 1e-12, "A price at par is not an opinion either way.");
            Assert.Greater(cheap.Price, 0.0);
            Assert.Less(dear.Price, 0.0);

            Assert.AreEqual(cheap.Price, -dear.Price, 1e-12,
                "Halving and doubling are the same distance, so they are worth the same in opposite "
                + "directions. Anything else is a hidden thumb on the scale.");
        }

        /// <summary>
        /// The founder lifts what a company earns and never what it loses. A founder who is good with
        /// people does not make an ageing product line age more slowly.
        /// </summary>
        [Test]
        public void ACharismaticFounderCannotSlowDownDecay()
        {
            var plain = Standing.Today(0.4, 50.0, 1.0, Standing.StaleDays, 1.0, 1.0, 1.0);
            var charming = Standing.Today(0.4, 50.0, 1.0, Standing.StaleDays, 1.0, 1.0, 2.0);

            Assert.Greater(charming.Service, plain.Service);
            Assert.AreEqual(plain.ModelAge, charming.ModelAge, 1e-12);
            Assert.AreEqual(plain.Drift, charming.Drift, 1e-12);
        }

        [Test]
        public void TheHeadlineNamesWhicheverDriverIsActuallyDoingTheMost()
        {
            var stale = Standing.Today(0.0, 0.0, 0.0, Standing.StaleDays, 1.0, 0.0, 1.0);
            StringAssert.Contains("ageing", stale.Headline);

            var serving = Standing.Today(1.0, 100.0, 0.0, 0, 1.0, 0.0, 1.0);
            StringAssert.Contains("serving", serving.Headline);

            Assert.AreEqual("fading quietly", Neutral().Headline,
                "With nothing else happening, the drift is the honest answer.");
        }

        // ---- fans ------------------------------------------------------------------------

        [Test]
        public void AnUnknownCompanyHasNoFansHoweverManyUsersItHas()
        {
            Assert.AreEqual(0.0, Standing.FanTarget(10_000_000.0, 0.0), 1e-9,
                "Nobody follows a company they have never heard of, at any size.");
        }

        [Test]
        public void BeingBetterRegardedIsWorthMoreThanProportionally()
        {
            var modest = Standing.FanTarget(1_000_000.0, 0.3);
            var admired = Standing.FanTarget(1_000_000.0, 0.6);

            Assert.Greater(admired, modest * 3.0,
                "Twice the regard has to be worth much more than twice the following, or fans are "
                + "just users multiplied by a number and carry no meaning of their own.");
        }

        [Test]
        public void FansArriveFasterThanTheyLeave()
        {
            var gained = Standing.AdvanceFans(0.0, 100_000.0);
            var lost = 100_000.0 - Standing.AdvanceFans(100_000.0, 0.0);

            Assert.Greater(gained, lost,
                "Fans that evaporate as fast as they arrive are a second user count, not a stock "
                + "worth building.");
        }

        [Test]
        public void AFanBaseNeverGoesNegativeOrBecomesNonsense()
        {
            foreach (var target in new[] { -5.0, 0.0, double.NaN, double.PositiveInfinity })
            {
                var fans = Standing.AdvanceFans(1000.0, target);
                Assert.IsFalse(double.IsNaN(fans), target.ToString());
                Assert.GreaterOrEqual(fans, 0.0, target.ToString());
            }
        }

        // ---- through the real simulation ---------------------------------------------------

        [Test]
        public void AShippingCompanyBuildsAFollowingAndAnIdleOneDoesNot()
        {
            static (double Fans, double Reputation) Play(bool ship)
            {
                // Enough capacity to actually serve somebody, and cheap enough to survive nine hundred
                // days on twelve million. Both halves matter and both were wrong first time: renting
                // 150 PF bankrupted the company, and renting nothing meant it served zero tokens, so
                // the service driver never fired and reputation could only fall.
                var simulation = new CompanySimulation(new CompanyState("Fanco", 808));
                simulation.SetRentedPetaflops(20.0);

                // Five hundred days, not nine hundred. At nine hundred a single unrefreshed model has
                // been on sale for two and a half years, staleness outweighs the credit for serving,
                // and both companies sit at zero reputation. That is the simulation being right and
                // the test asking the wrong question.
                for (var day = 0; day < 500; day++)
                {
                    if (ship && day == 30)
                    {
                        simulation.State.AddDeployedModel(new DeployedModel(
                            "Muse", ArchitectureId.DenseTransformer, 45.0,
                            simulation.State.Date, 2e10, 1.0, ModelType.General));

                        simulation.State.LastReleaseDate = simulation.State.Date;
                    }

                    simulation.AdvanceDay();
                }

                return (simulation.State.Fans, simulation.State.Reputation);
            }

            var shipped = Play(true);
            var idle = Play(false);

            Assert.Greater(shipped.Fans, idle.Fans,
                $"Shipping built {shipped.Fans:N0} fans and doing nothing built {idle.Fans:N0}. A "
                + "following has to come from serving people.");

            Assert.Greater(shipped.Reputation, idle.Reputation);
        }

        [Test]
        public void FansAndTheReleaseDateSurviveASave()
        {
            var simulation = new CompanySimulation(new CompanyState("Saveco", 12));
            simulation.SetRentedPetaflops(20.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Muse", ArchitectureId.DenseTransformer, 50.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            for (var day = 0; day < 400; day++)
            {
                simulation.AdvanceDay();
            }

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(simulation.State.Fans, restored.Fans, 1e-6,
                "A following that resets on load is not a stock.");

            Assert.AreEqual(simulation.State.LastReleaseDate.DayIndex,
                restored.LastReleaseDate.DayIndex,
                "Losing the release date makes a fresh line look abandoned the moment it is loaded.");
        }

        [Test]
        public void AnOlderSaveRebuildsItsFollowingRatherThanBeingHandedOne()
        {
            var data = new SaveData { version = 17 };
            data.models.Add(new DeployedModelData { name = "Old", releaseDayIndex = 500 });

            var upgraded = SaveMigration.UpgradeV17ToV18(data);

            Assert.AreEqual(18, upgraded.version);
            Assert.AreEqual(0.0, upgraded.fans, 1e-9,
                "A following is earned day by day and a v17 file has no record of those days.");

            Assert.AreEqual(500, upgraded.lastReleaseDayIndex,
                "The release date was recorded, so reading it is not a guess.");
        }
    }
}
