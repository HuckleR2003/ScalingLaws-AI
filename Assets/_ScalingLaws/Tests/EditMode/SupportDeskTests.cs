using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The desk has to make growth cost something without making it impossible, and the author gave
    /// the two figures it is judged on: an abandoned queue costs a fifth of how the product is
    /// experienced, an answered one adds eight per cent. Everything here is measured against those.
    /// </summary>
    public sealed class SupportDeskTests
    {
        [Test]
        public void AnEmptyDeskIsNotAFailingDesk()
        {
            var desk = new SupportDesk();

            Assert.That(desk.Quality(), Is.EqualTo(1.0),
                "a company with no users has answered all of its post");
            Assert.That(desk.ServiceMultiplier(), Is.GreaterThan(1.0));
        }

        [Test]
        public void MorePeopleServedMeansMorePostButNotInProportion()
        {
            var desk = new SupportDesk();

            var small = desk.TicketsPerDay(1_000_000.0);
            var large = desk.TicketsPerDay(30_000_000.0);

            Assert.That(small, Is.GreaterThan(0.0));
            Assert.That(large, Is.GreaterThan(small));
            Assert.That(large, Is.EqualTo(small * System.Math.Sqrt(30.0)).Within(0.001),
                "thirty times the audience asks about five and a half times as much, because most "
                + "of what it asks is the question the last million already asked");
            Assert.That(large, Is.LessThan(small * 30.0),
                "a linear desk would need more staff than the largest office has desks");
        }

        /// <summary>
        /// The whole point of the mechanic: a desk that is not staffed for its audience falls
        /// behind, and the product is judged on it.
        /// </summary>
        [Test]
        public void ADeskLeftUnstaffedFallsBehindUntilThePenaltyIsFull()
        {
            var desk = new SupportDesk();

            for (var day = 0; day < 400; day++)
            {
                desk.Advance(3_000_000.0, supportPeople: 0);
            }

            Assert.That(desk.BacklogHours, Is.GreaterThan(0.0));
            Assert.That(desk.Quality(), Is.LessThan(0.001),
                "an abandoned queue approaches zero rather than landing on it exactly");
            Assert.That(desk.ServiceMultiplier(),
                Is.EqualTo(1.0 - SupportCatalog.WorstPenalty).Within(0.0001));
        }

        [Test]
        public void ADeskStaffedForItsAudienceKeepsTheBonus()
        {
            var desk = new SupportDesk();

            for (var day = 0; day < 60; day++)
            {
                desk.Advance(3_000_000.0, supportPeople: 8);
            }

            Assert.That(desk.AverageHours(8), Is.LessThan(SupportCatalog.AnsweredHours));
            Assert.That(desk.ServiceMultiplier(),
                Is.EqualTo(1.0 + SupportCatalog.BestBonus).Within(0.0001));
        }

        /// <summary>
        /// Growth has to outrun a fixed desk, or hiring would be a one-off purchase rather than
        /// something the player keeps deciding.
        /// </summary>
        [Test]
        public void ADeskThatWasEnoughLastYearIsNotEnoughAtTenTimesTheAudience()
        {
            var small = new SupportDesk();
            var large = new SupportDesk();

            for (var day = 0; day < 60; day++)
            {
                small.Advance(1_000_000.0, supportPeople: 3);
                large.Advance(10_000_000.0, supportPeople: 3);
            }

            Assert.That(small.Quality(), Is.EqualTo(1.0));
            Assert.That(large.Quality(), Is.LessThan(0.5));
        }

        [Test]
        public void PeopleTakeTheOutageFirstAndTheAgentsOnlyTakeTheOrdinaryPost()
        {
            var desk = new SupportDesk();
            for (var level = 0; level < 4; level++)
            {
                desk.RecordUpgrade(SupportCatalog.Upgrade.Agents);
            }

            desk.SetAgentsWorking(4);

            for (var day = 0; day < 20; day++)
            {
                desk.Advance(3_000_000.0, supportPeople: 0);
            }

            Assert.That(desk.BacklogHoursOf(TicketClass.High), Is.GreaterThan(0.0),
                "agents must never answer an outage, however many of them there are");
            Assert.That(desk.BacklogHoursOf(TicketClass.Low),
                Is.LessThan(desk.BacklogHoursOf(TicketClass.Medium)),
                "what the agents are for is the ordinary post, and they should be ahead on it");
        }

        [Test]
        public void EveryWorkingAgentCostsTheFleetAndAnIdleOneCostsNothing()
        {
            var desk = new SupportDesk();
            Assert.That(desk.UsersOwedToAgents, Is.EqualTo(0.0));

            for (var level = 0; level < 3; level++)
            {
                desk.RecordUpgrade(SupportCatalog.Upgrade.Agents);
            }

            desk.SetAgentsWorking(3);
            Assert.That(desk.UsersOwedToAgents,
                Is.EqualTo(3.0 * SupportCatalog.AgentUsersEquivalent).Within(0.001));

            desk.SetAgentsWorking(0);
            Assert.That(desk.UsersOwedToAgents, Is.EqualTo(0.0),
                "switching an agent off frees its compute the same day");
        }

        [Test]
        public void AgentsCannotBeSwitchedOnWithoutASeatToSitIn()
        {
            var desk = new SupportDesk();

            desk.SetAgentsWorking(10);
            Assert.That(desk.AgentsWorking, Is.EqualTo(0));

            desk.RecordUpgrade(SupportCatalog.Upgrade.Agents);
            desk.SetAgentsWorking(10);
            Assert.That(desk.AgentsWorking, Is.EqualTo(1), "one level is one seat");
        }

        [Test]
        public void TheLaddersStopWhereTheCatalogSaysTheyStop()
        {
            var desk = new SupportDesk();

            for (var attempt = 0; attempt < 20; attempt++)
            {
                desk.RecordUpgrade(SupportCatalog.Upgrade.Deflection);
                desk.RecordUpgrade(SupportCatalog.Upgrade.ContinuousTraining);
                desk.RecordUpgrade(SupportCatalog.Upgrade.Agents);
            }

            Assert.That(desk.DeflectionLevel, Is.EqualTo(3));
            Assert.That(desk.TrainingLevel, Is.EqualTo(3));
            Assert.That(desk.AgentLevel, Is.EqualTo(10));
            Assert.That(desk.AgentSlots, Is.EqualTo(10));
        }

        /// <summary>
        /// The untouched desk has to be exactly the desk everything else was balanced against, or
        /// adding these three ladders quietly retunes every campaign that ignores them.
        /// </summary>
        [Test]
        public void TheUntouchedLaddersChangeNothing()
        {
            Assert.That(SupportCatalog.DeflectionAt(0), Is.EqualTo(0.0));
            Assert.That(SupportCatalog.TrainingAt(0), Is.EqualTo(0.0));
            Assert.That(SupportCatalog.AgentSlotsAt(0), Is.EqualTo(0));
        }

        [Test]
        public void DeflectionTakesPostAwayAndTrainingTakesTimeAway()
        {
            var plain = new SupportDesk();
            var deflected = new SupportDesk();
            deflected.RecordUpgrade(SupportCatalog.Upgrade.Deflection);
            deflected.RecordUpgrade(SupportCatalog.Upgrade.Deflection);
            deflected.RecordUpgrade(SupportCatalog.Upgrade.Deflection);

            Assert.That(deflected.TicketsPerDay(3_000_000.0),
                Is.LessThan(plain.TicketsPerDay(3_000_000.0)));

            var trained = new SupportDesk();
            trained.RecordUpgrade(SupportCatalog.Upgrade.ContinuousTraining);
            trained.RecordUpgrade(SupportCatalog.Upgrade.ContinuousTraining);
            trained.RecordUpgrade(SupportCatalog.Upgrade.ContinuousTraining);

            plain.Advance(3_000_000.0, supportPeople: 1);
            trained.Advance(3_000_000.0, supportPeople: 1);

            Assert.That(trained.BacklogHours, Is.LessThan(plain.BacklogHours),
                "the same post has to cost fewer hours once the desk has been trained on it");
        }

        [Test]
        public void ASavedDeskComesBackTheSameAndCannotBeGivenLevelsItNeverBought()
        {
            var desk = new SupportDesk();
            desk.Restore(low: 40.0, medium: 12.0, high: 3.0, judgedHours: 70.0,
                deflection: 99, training: -4, agentLevel: 40, working: 99);

            Assert.That(desk.BacklogHoursOf(TicketClass.Low), Is.EqualTo(40.0));
            Assert.That(desk.DeflectionLevel, Is.EqualTo(3));
            Assert.That(desk.TrainingLevel, Is.EqualTo(0));
            Assert.That(desk.AgentLevel, Is.EqualTo(10));
            Assert.That(desk.AgentsWorking, Is.EqualTo(10));
        }

        /// <summary>
        /// **The failure this smoothing was added for, kept as a test.**
        ///
        /// Judging the instant queue meant a company that shipped its first model on Monday was
        /// reported as abandoning its post on Tuesday: one day of arrivals against a founder
        /// working alone is already past four days of waiting, so the market took a fifth off a
        /// product that was two days old. `ShippingOnceAndCoastingLosesTheMarket` caught it by
        /// finding half the share it expected on day one.
        /// </summary>
        [Test]
        public void AProductTwoDaysOldIsNotJudgedForItsFirstDayOfPost()
        {
            var desk = new SupportDesk();

            desk.Advance(3_000_000.0, supportPeople: SupportCatalog.FounderShare);

            Assert.That(desk.BacklogHours, Is.GreaterThan(0.0), "the post did arrive");
            Assert.That(desk.AverageHours(SupportCatalog.FounderShare),
                Is.GreaterThan(SupportCatalog.AnsweredHours),
                "and one founder cannot get through it, which is true and is not the question");
            Assert.That(desk.Quality(), Is.EqualTo(1.0),
                "a month of evidence is what a customer forms an opinion out of, not one day");
        }

        /// <summary>
        /// And the other direction, which is what makes hiring worth doing: a desk that was
        /// abandoned and is then staffed climbs back over about a month rather than instantly.
        /// </summary>
        [Test]
        public void HiringAfterABadMonthIsFeltWithinAboutAMonth()
        {
            var desk = new SupportDesk();

            for (var day = 0; day < 400; day++)
            {
                desk.Advance(3_000_000.0, supportPeople: 0.0);
            }

            Assert.That(desk.Quality(), Is.LessThan(0.001),
                "an abandoned queue approaches zero rather than landing on it exactly");

            for (var day = 0; day < 30; day++)
            {
                desk.Advance(3_000_000.0, supportPeople: 8.0);
            }

            var afterAMonth = desk.Quality();
            Assert.That(afterAMonth, Is.GreaterThan(0.5),
                "a month of answering has to be visible");
            Assert.That(afterAMonth, Is.LessThan(1.0),
                "and it should not wipe the record of the year that came before it");
        }

        /// <summary>
        /// **The ratchet for an afternoon lost to a struct.**
        ///
        /// `ServiceQuality` is a struct and `CompanyState.LastQuality` starts as `default`, which
        /// does not run the constructor, so the support multiplier read back as zero. The market
        /// then served a company on its first morning at a fifth of the attractiveness it had
        /// earned, before a single ticket had arrived.
        /// </summary>
        [Test]
        public void ADefaultServiceQualityIsACompanyWithNoDeskRatherThanADeadOne()
        {
            var untouched = default(ServiceQuality);

            Assert.That(untouched.SupportMultiplier, Is.EqualTo(0.0),
                "this is what default(struct) does, and it is why the reader has to be careful");
            Assert.That(untouched.ExperienceMultiplier, Is.EqualTo(1.0),
                "a company with no desk to judge is experienced exactly as its cluster is");
        }

        [Test]
        public void AnAbandonedDeskReachesTheMarketAndAnAnsweredOneIsWorthMore()
        {
            var abandoned = new ServiceQuality(10.0, 1000.0, 0.0, 1.0 - SupportCatalog.WorstPenalty);
            var answered = new ServiceQuality(10.0, 1000.0, 0.0, 1.0 + SupportCatalog.BestBonus);

            Assert.That(abandoned.Reliability, Is.EqualTo(answered.Reliability),
                "the cluster is the same in both; only the desk differs");
            Assert.That(abandoned.ExperienceMultiplier, Is.LessThan(0.81));
            Assert.That(answered.ExperienceMultiplier, Is.GreaterThan(1.07));
        }

        /// <summary>
        /// The call that says the post has started is made once in a campaign, and surviving a save
        /// is the half of that which matters.
        ///
        /// **Support is the only system here that starts without being started.** Nothing is bought
        /// and nothing is clicked; enough people are being served that some of them write in. So the
        /// announcement is the only thing that ever tells a player the desk exists, and a flag
        /// rebuilt from the desk rather than saved would ring the phone again on the first tick
        /// after every load, for the rest of the campaign.
        /// </summary>
        [Test]
        public void TheFirstTicketIsAnnouncedOnceAndSurvivesASave()
        {
            var simulation = Serving();

            Assert.That(Announcements(simulation), Is.Zero,
                "nothing has been served yet, so there is no post to announce");

            var said = 0;

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
                said += Announcements(simulation);
            }

            Assert.That(said, Is.EqualTo(1),
                "the desk opened once, so it is announced once");
            Assert.IsTrue(simulation.State.SupportAnnounced);

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.IsTrue(back.SupportAnnounced,
                "a reloaded campaign has already been told");

            var reloaded = new CompanySimulation(back);
            reloaded.SetRentedPetaflops(80.0);

            var again = 0;

            for (var day = 0; day < 60; day++)
            {
                reloaded.AdvanceDay();
                again += Announcements(reloaded);
            }

            Assert.That(again, Is.Zero,
                "reloading must not ring the phone about a queue the player has been working for "
                + "months");
        }

        /// <summary>
        /// A v63 campaign that already had post is not rung up about its first ticket.
        ///
        /// The least flattering reading available, and the right one: the alternative is a company
        /// three years in being told the post has started, which reads as the game having lost its
        /// place. A v63 file with an empty desk genuinely has not taken one, so it is left to be
        /// announced on the day it does.
        /// </summary>
        [Test]
        public void AnOlderDeskWithPostCountsAsAlreadyToldAndAnEmptyOneDoesNot()
        {
            var busy = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            busy.version = 63;
            busy.supportLowHours = 14.0;

            var upgraded = SaveMigration.UpgradeV63ToV64(busy);

            Assert.That(upgraded.version, Is.EqualTo(64));
            Assert.IsTrue(upgraded.supportAnnounced);
            StringAssert.Contains("v63 to v64", SaveMigration.LastMigrationNotes);

            var quiet = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            quiet.version = 63;

            Assert.IsFalse(SaveMigration.UpgradeV63ToV64(quiet).supportAnnounced,
                "an empty desk has its first ticket still ahead of it");
        }

        /// <summary>A company with a model on sale and a cluster to serve it from.</summary>
        private static CompanySimulation Serving(uint seed = 404)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(80.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Atlas One", ArchitectureId.DenseTransformer, 48.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            return simulation;
        }

        /// <summary>Drains the queue and counts what it said about the desk opening.</summary>
        private static int Announcements(CompanySimulation simulation)
        {
            var said = 0;

            while (simulation.State.TryDequeueEvent(out var companyEvent))
            {
                if (companyEvent.Type == CompanyEventType.FirstSupportTicket)
                {
                    said++;
                }
            }

            return said;
        }
    }
}
