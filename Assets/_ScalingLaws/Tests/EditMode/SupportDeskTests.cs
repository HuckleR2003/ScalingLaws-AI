using NUnit.Framework;
using ScalingLaws.Data;
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

            Assert.That(desk.Quality(0), Is.EqualTo(1.0),
                "a company with no users has answered all of its post");
            Assert.That(desk.ServiceMultiplier(0), Is.GreaterThan(1.0));
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

            for (var day = 0; day < 30; day++)
            {
                desk.Advance(3_000_000.0, supportStaff: 0);
            }

            Assert.That(desk.BacklogHours, Is.GreaterThan(0.0));
            Assert.That(desk.Quality(0), Is.EqualTo(0.0));
            Assert.That(desk.ServiceMultiplier(0),
                Is.EqualTo(1.0 - SupportCatalog.WorstPenalty).Within(0.0001));
        }

        [Test]
        public void ADeskStaffedForItsAudienceKeepsTheBonus()
        {
            var desk = new SupportDesk();

            for (var day = 0; day < 60; day++)
            {
                desk.Advance(3_000_000.0, supportStaff: 8);
            }

            Assert.That(desk.AverageHours(8), Is.LessThan(SupportCatalog.AnsweredHours));
            Assert.That(desk.ServiceMultiplier(8),
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
                small.Advance(1_000_000.0, supportStaff: 3);
                large.Advance(10_000_000.0, supportStaff: 3);
            }

            Assert.That(small.Quality(3), Is.EqualTo(1.0));
            Assert.That(large.Quality(3), Is.LessThan(0.5));
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
                desk.Advance(3_000_000.0, supportStaff: 0);
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

            plain.Advance(3_000_000.0, supportStaff: 1);
            trained.Advance(3_000_000.0, supportStaff: 1);

            Assert.That(trained.BacklogHours, Is.LessThan(plain.BacklogHours),
                "the same post has to cost fewer hours once the desk has been trained on it");
        }

        [Test]
        public void ASavedDeskComesBackTheSameAndCannotBeGivenLevelsItNeverBought()
        {
            var desk = new SupportDesk();
            desk.Restore(low: 40.0, medium: 12.0, high: 3.0,
                deflection: 99, training: -4, agentLevel: 40, working: 99);

            Assert.That(desk.BacklogHoursOf(TicketClass.Low), Is.EqualTo(40.0));
            Assert.That(desk.DeflectionLevel, Is.EqualTo(3));
            Assert.That(desk.TrainingLevel, Is.EqualTo(0));
            Assert.That(desk.AgentLevel, Is.EqualTo(10));
            Assert.That(desk.AgentsWorking, Is.EqualTo(10));
        }
    }
}
