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
    /// Marketing.
    ///
    /// The rule the whole system exists to obey: **it buys awareness and nothing else.** It does not
    /// touch capability, reliability or what an audience will pay, because those are what the product
    /// is. A bad product advertised hard gets tried and abandoned, which costs money twice.
    ///
    /// The version this replaced added a figure straight to reputation. That is not a marketing
    /// system, it is a reputation slider with a different label on it.
    /// </summary>
    public sealed class MarketingTests
    {
        private static CompanySimulation Fresh(uint seed = 300) =>
            new(new CompanyState("Adco", seed));

        private static MarketingCampaign Campaign(AudienceSegment target, int months,
            params MarketingChannel[] channels) =>
            new(channels, target, months, GameDate.Start);

        // ---- the catalog has to stay a set of trade-offs -------------------------------------

        [Test]
        public void NoChannelIsSimplyBetterThanAnother()
        {
            foreach (var mine in MarketingCatalog.All)
            {
                var dominated = false;

                foreach (var other in MarketingCatalog.All)
                {
                    if (other.Id == mine.Id)
                    {
                        continue;
                    }

                    // Cheaper and better on every axis at once. If any channel is ever like this,
                    // the choice between six tiles collapses to one.
                    if (other.DailyCostUsd <= mine.DailyCostUsd
                        && other.Reach >= mine.Reach
                        && other.Speed >= mine.Speed
                        && other.Persistence >= mine.Persistence
                        && other.Credibility >= mine.Credibility
                        && other.Volatility <= mine.Volatility)
                    {
                        dominated = true;
                    }
                }

                Assert.IsFalse(dominated,
                    $"{mine.DisplayName} is beaten on every axis at once by something cheaper, so "
                    + "nobody should ever pick it.");
            }
        }

        [Test]
        public void TelevisionIsBroadAndSlowAndSocialIsCheapAndFickle()
        {
            var tv = MarketingCatalog.Get(MarketingChannel.Television);
            var social = MarketingCatalog.Get(MarketingChannel.Social);
            var press = MarketingCatalog.Get(MarketingChannel.Press);

            Assert.Greater(tv.Reach, social.Reach);
            Assert.Less(tv.Speed, social.Speed, "Television takes weeks to land.");
            Assert.Less(social.DailyCostUsd, tv.DailyCostUsd);
            Assert.Greater(social.Volatility, tv.Volatility, "Social swings hardest.");

            Assert.Greater(press.Credibility, 0.0, "Press is the standing channel.");
            Assert.Less(social.Credibility, 0.0,
                "Shouting on social is the one channel that can cost you standing.");
        }

        [Test]
        public void ACampaignCannotRunMoreThanThreeChannels()
        {
            var greedy = Campaign(AudienceSegment.Consumer, 3,
                MarketingChannel.Social, MarketingChannel.Press, MarketingChannel.Radio,
                MarketingChannel.Television, MarketingChannel.Billboards);

            Assert.AreEqual(MarketingCatalog.MostChannelsAtOnce, greedy.Channels.Count,
                "Six at once makes the combination meaningless, which is the only reason to allow "
                + "more than one.");
        }

        [Test]
        public void TheSameChannelCannotBeBookedTwiceInOneCampaign()
        {
            var doubled = Campaign(AudienceSegment.Consumer, 1,
                MarketingChannel.Social, MarketingChannel.Social, MarketingChannel.Social);

            Assert.AreEqual(1, doubled.Channels.Count);
        }

        [Test]
        public void LongerBookingsAreCheaperPerDayAndOpenEndedIsDearest()
        {
            var one = Campaign(AudienceSegment.Consumer, 1, MarketingChannel.Television);
            var six = Campaign(AudienceSegment.Consumer, 6, MarketingChannel.Television);
            var open = Campaign(AudienceSegment.Consumer, 0, MarketingChannel.Television);

            Assert.Less(six.DailyCostUsd, one.DailyCostUsd,
                "Committing has to buy something or nobody would commit.");

            Assert.Greater(open.DailyCostUsd, one.DailyCostUsd,
                "Nobody sells an open contract at the price of a booked one.");

            Assert.IsTrue(open.IsOpenEnded);
            Assert.AreEqual(180, six.DaysBooked);
        }

        // ---- awareness ------------------------------------------------------------------------

        [Test]
        public void AnUnknownCompanyIsStillConsideredALittle()
        {
            Assert.AreEqual(Awareness.Floor, Awareness.Consideration(0.0), 1e-12,
                "Somebody always stumbles across a thing. Zero would delete a company that simply "
                + "has not advertised yet.");

            Assert.AreEqual(1.0, Awareness.Consideration(1.0), 1e-12,
                "And being famous is not worth more than being fully known.");
        }

        [Test]
        public void ACampaignMakesTheTargetAudienceAwareFasterThanTheOthers()
        {
            var simulation = Fresh();
            simulation.State.AddCampaign(new MarketingCampaign(
                new[] { MarketingChannel.Creators }, AudienceSegment.Developer, 3,
                simulation.State.Date));

            for (var day = 0; day < 60; day++)
            {
                simulation.AdvanceDay();
            }

            var targeted = simulation.State.Awareness.In(AudienceSegment.Developer);
            var bystander = simulation.State.Awareness.In(AudienceSegment.Enterprise);

            Assert.Greater(targeted, bystander,
                $"Developers reached {targeted:P0} and enterprise {bystander:P0}. If those are the "
                + "same, targeting does nothing and the audience chips are decoration.");

            Assert.Greater(bystander, 0.0,
                "Everybody hears something. A channel that reaches exactly one group turns targeting "
                + "into a lookup table.");
        }

        [Test]
        public void MarketingIsBilledEveryDayItRuns()
        {
            var simulation = Fresh();
            simulation.State.AddCampaign(new MarketingCampaign(
                new[] { MarketingChannel.Billboards }, AudienceSegment.Consumer, 2,
                simulation.State.Date));

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var month = Ledger.MonthKeyOf(simulation.State.Date);
            var spent = simulation.State.Ledger.MonthTotal(month, LedgerLine.Marketing)
                + simulation.State.Ledger.MonthTotal(month - 1, LedgerLine.Marketing);

            Assert.Greater(spent, 0L, "A campaign that costs nothing is not a decision.");
        }

        [Test]
        public void ABookedCampaignStopsCostingMoneyWhenItsTermRunsOut()
        {
            var simulation = Fresh();
            simulation.State.AddCampaign(new MarketingCampaign(
                new[] { MarketingChannel.Radio }, AudienceSegment.Creative, 1,
                simulation.State.Date));

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.IsEmpty(simulation.State.Campaigns,
                "A one month booking has to stop on its own, whether or not the player is looking at "
                + "the screen.");
        }

        /// <summary>
        /// The floor that keeps awareness a lever rather than a tax.
        ///
        /// A five year balance test caught this: the baseline player never advertises, so their
        /// awareness sat at the standing floor and every product they made was considered at fifteen
        /// percent strength forever. Being used is itself being known.
        /// </summary>
        [Test]
        public void BeingUsedCountsAsBeingKnown()
        {
            var quiet = new Awareness();
            var random = new DeterministicRandom(7);

            quiet.Advance(new List<MarketingCampaign>(), GameDate.Start, 0.0, random,
                _ => 0.40);

            Assert.GreaterOrEqual(quiet.In(AudienceSegment.Consumer), 0.40,
                "If two fifths of an audience is on the service then at least two fifths of them "
                + "have heard of it, whatever the advertising says.");
        }

        [Test]
        public void AwarenessFadesWhenNothingIsRunning()
        {
            var awareness = new Awareness();
            awareness.Set(AudienceSegment.Consumer, 0.9);

            var random = new DeterministicRandom(7);
            for (var day = 0; day < 30; day++)
            {
                awareness.Advance(new List<MarketingCampaign>(), GameDate.Start, 0.0, random);
            }

            Assert.Less(awareness.In(AudienceSegment.Consumer), 0.9,
                "Rented attention is rented.");
        }

        /// <summary>
        /// The claim that makes marketing worth building: two identical companies, same model, same
        /// price, and the one people have heard of holds more of the market.
        /// </summary>
        [Test]
        public void BeingKnownWinsUsersFromAnIdenticalUnknownCompany()
        {
            static double Play(bool advertise)
            {
                var simulation = new CompanySimulation(new CompanyState("Adco", 909));
                simulation.SetRentedPetaflops(80.0);

                simulation.State.AddDeployedModel(new DeployedModel(
                    "Subject", ArchitectureId.DenseTransformer, 50.0,
                    simulation.State.Date, 2e10, 1.0, ModelType.General));

                if (advertise)
                {
                    simulation.State.AddCampaign(new MarketingCampaign(
                        new[] { MarketingChannel.Television, MarketingChannel.Billboards },
                        AudienceSegment.Consumer, 6, simulation.State.Date));
                }

                for (var day = 0; day < 400; day++)
                {
                    simulation.AdvanceDay();
                }

                return simulation.Sentiment().Users;
            }

            var unknown = Play(false);
            var known = Play(true);

            Assert.Greater(known, unknown,
                $"Advertised held {known:N0} users and unadvertised {unknown:N0}. If those are equal "
                + "then awareness never reaches the market and the whole system is spending.");
        }

        [Test]
        public void MarketingNeverImprovesTheProductItself()
        {
            var simulation = Fresh();
            var model = new DeployedModel("Subject", ArchitectureId.DenseTransformer, 44.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General);

            simulation.State.AddDeployedModel(model);
            var capabilityBefore = model.EffectiveCapability(simulation.State.Date);

            simulation.State.AddCampaign(new MarketingCampaign(
                new[] { MarketingChannel.Television }, AudienceSegment.Consumer, 6,
                simulation.State.Date));

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.AreEqual(capabilityBefore, model.Capability, 1e-9,
                "Advertising must never make the model better. That is the one line this system is "
                + "not allowed to cross.");
        }

        // ---- persistence -----------------------------------------------------------------------

        [Test]
        public void CampaignsAndAwarenessSurviveASave()
        {
            var simulation = Fresh(11);
            simulation.State.AddCampaign(new MarketingCampaign(
                new[] { MarketingChannel.Press, MarketingChannel.Radio },
                AudienceSegment.Enterprise, 6, simulation.State.Date));

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(1, restored.Campaigns.Count, "A booking that vanishes on load is a refund.");
            Assert.AreEqual(2, restored.Campaigns[0].Channels.Count);
            Assert.AreEqual(AudienceSegment.Enterprise, restored.Campaigns[0].Target);

            Assert.AreEqual(simulation.State.Awareness.In(AudienceSegment.Enterprise),
                restored.Awareness.In(AudienceSegment.Enterprise), 1e-6);
        }

        [Test]
        public void AnOlderSaveHasNoCampaignsAndRebuildsItsAwareness()
        {
            var data = new SaveData { version = 21 };
            var upgraded = SaveMigration.UpgradeV21ToV22(data);

            Assert.AreEqual(22, upgraded.version);
            Assert.IsEmpty(upgraded.campaigns);
            Assert.IsEmpty(upgraded.awareness,
                "Awareness rebuilds on the first day from standing and from who is already being "
                + "served, both of which are recorded, so nothing has to be invented.");
        }
    }
}
