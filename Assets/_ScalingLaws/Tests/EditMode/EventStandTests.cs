using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What a stand is worth, and the gate on the early look.
    ///
    /// The two halves of the venue table do different jobs and that is the thing worth holding:
    /// the crowd builds a following and the address builds regard. If those ever start moving
    /// together, the six venues become one venue with six names.
    /// </summary>
    public sealed class EventStandTests
    {
        private const uint Seed = 0x5CA1AB1E;

        /// <summary>
        /// The crowd drives the following and the address drives the regard.
        ///
        /// Bayview Park is seven times the crowd of Civic Square and ranks below it, so the two
        /// readings have to come out in opposite directions. **Asserting that they differ is the
        /// weak version of this test**: any two numbers differ, and only the right ordering says
        /// the split is wired the way the table was written.
        /// </summary>
        [Test]
        public void TheCrowdBuildsAFollowingAndTheAddressBuildsRegard()
        {
            var park = EventVenueId.BayviewPark;
            var square = EventVenueId.CivicSquare;

            Assert.That(EventStand.FansAt(park, StandTier.Small),
                Is.GreaterThan(EventStand.FansAt(square, StandTier.Small)),
                "A festival ground with seven times the crowd hands on no more of a following "
                + "than a civic square, so visitors are not reaching the fans.");

            Assert.That(EventStand.ReputationAt(square, StandTier.Small),
                Is.GreaterThan(EventStand.ReputationAt(park, StandTier.Small)),
                "The better regarded address earns no more regard than the festival ground, so "
                + "prestige is not reaching the reputation and the two figures are one figure.");
        }

        [Test]
        public void EveryTierUpIsWorthMoreOnBothAxes()
        {
            foreach (var venue in EventVenueCatalog.All)
            {
                Assert.That(EventStand.FansAt(venue.Id, StandTier.Large),
                    Is.GreaterThan(EventStand.FansAt(venue.Id, StandTier.Medium)));

                Assert.That(EventStand.ReputationAt(venue.Id, StandTier.Medium),
                    Is.GreaterThan(EventStand.ReputationAt(venue.Id, StandTier.Small)),
                    venue.Id + ": a bigger stand earns no more regard.");
            }
        }

        /// <summary>
        /// Going viral is about how many people saw it, not about how smart the room was.
        /// </summary>
        [Test]
        public void TheChanceOfGoingViralFollowsTheCrowdRatherThanThePrestige()
        {
            var park = EventVenueCatalog.Get(EventVenueId.BayviewPark);
            var plaza = EventVenueCatalog.Get(EventVenueId.ValleyPlaza);

            Assert.That(plaza.Prestige, Is.GreaterThan(park.Prestige),
                "This fixture needs the plaza to be the better address.");

            Assert.That(park.AverageVisitors, Is.GreaterThan(plaza.AverageVisitors),
                "And the park to be the bigger crowd.");

            Assert.That(EventStand.ViralChance(park.Id, StandTier.Small),
                Is.GreaterThan(EventStand.ViralChance(plaza.Id, StandTier.Small)),
                "The more highly regarded venue is likelier to be talked about than the bigger "
                + "crowd, so going viral is following the wrong figure.");
        }

        [Test]
        public void NothingEverGoesViralMoreOftenThanTheCeiling()
        {
            foreach (var venue in EventVenueCatalog.All)
            {
                foreach (var tier in new[] { StandTier.Small, StandTier.Medium, StandTier.Large })
                {
                    Assert.That(EventStand.ViralChance(venue.Id, tier),
                        Is.LessThanOrEqualTo(EventStand.MostViralChance),
                        venue.Id + " at " + tier + " is past the ceiling, so the biggest venue "
                        + "makes it close to certain and the result stops being a result.");
                }
            }
        }

        [Test]
        public void GoingViralIsWorthMoreThanNotAndTheOddsAreReported()
        {
            // Walk the years until this event's roll comes up viral, then compare that year
            // against one that did not.
            EventOutcome? viral = null;
            EventOutcome? quiet = null;

            for (var year = 2022; year <= 2040 && (viral == null || quiet == null); year++)
            {
                var outcome = EventStand.Run(CityEventId.FrontierExpo, year, StandTier.Large, Seed);

                if (outcome.WentViral)
                {
                    viral ??= outcome;
                }
                else
                {
                    quiet ??= outcome;
                }
            }

            Assert.That(viral, Is.Not.Null,
                "Nineteen years of the biggest event at the biggest stand and it never once went "
                + "viral, so the roll is not reachable.");

            Assert.That(quiet, Is.Not.Null,
                "And it went viral every single year, so it is not a chance.");

            Assert.That(viral.Value.Fans, Is.GreaterThan(quiet.Value.Fans),
                "Going viral handed on no more of a following than not going viral.");

            Assert.That(viral.Value.ViralShare, Is.GreaterThan(0.0),
                "The card cannot say what the odds were, so a 6% result and a 38% result read "
                + "identically to the player.");
        }

        [Test]
        public void TheSameYearRunsTheSameWayTwice()
        {
            var first = EventStand.Run(CityEventId.ModelAwards, 2027, StandTier.Medium, Seed);
            var again = EventStand.Run(CityEventId.ModelAwards, 2027, StandTier.Medium, Seed);

            Assert.That(again.WentViral, Is.EqualTo(first.WentViral));
            Assert.That(again.Fans, Is.EqualTo(first.Fans).Within(1e-9));
        }

        // ---- the early look, and the gate on it ------------------------------------------------

        private static CompanySimulation Company()
        {
            var state = new CompanyState("Prometheus AI", Seed)
            {
                Date = GameDate.FromCalendar(2025, 1, 10),
                CashUsd = 500_000_000
            };

            return new CompanySimulation(state);
        }

        /// <summary>
        /// **The membership shows the calendar; the tick box only asks to be interrupted.**
        ///
        /// The first version required both for the early view, which made the box a switch
        /// that hid events the player was paying to see: turning it off made something eighty
        /// days out vanish from the list. Reported on 2026-10-25. A preference that removes
        /// information is a paywall wearing a checkbox.
        /// </summary>
        [Test]
        public void TheMembershipShowsTheCalendarAndTheBoxOnlyAsksForTheNotice()
        {
            var simulation = Company();

            simulation.SetIntelSubscription(IntelTier.NationalPress, true);

            Assert.That(simulation.HasEventEarlyAccess, Is.True,
                "Paid for the desk and the calendar is still the public one, so the membership "
                + "is selling nothing until a box is found and ticked.");

            Assert.That(simulation.WantsEarlyEventNotice, Is.False,
                "Nobody asked to be interrupted and the game is going to interrupt them.");

            simulation.SetEventEarlyAccess(true);

            Assert.That(simulation.WantsEarlyEventNotice, Is.True);

            simulation.SetEventEarlyAccess(false);

            Assert.That(simulation.WantsEarlyEventNotice, Is.False,
                "The box was turned off and the notice is still coming.");

            Assert.That(simulation.HasEventEarlyAccess, Is.True,
                "Turning off the notice took the early calendar away with it, which is the "
                + "fault this test was written for.");
        }

        [Test]
        public void TheBoxCannotBeTickedWithoutTheMembershipBehindIt()
        {
            var simulation = Company();

            simulation.SetEventEarlyAccess(true);

            Assert.That(simulation.State.EventEarlyAccess, Is.False,
                "The flag was written without the membership behind it.");

            Assert.That(simulation.WantsEarlyEventNotice, Is.False);
        }

        [Test]
        public void CancellingTheMembershipTakesTheEarlyLookWithIt()
        {
            var simulation = Company();

            simulation.SetIntelSubscription(IntelTier.NationalPress, true);
            simulation.SetEventEarlyAccess(true);

            Assert.That(simulation.HasEventEarlyAccess, Is.True,
                "Paid for and switched on, and it still is not working.");

            simulation.SetIntelSubscription(IntelTier.NationalPress, false);

            Assert.That(simulation.HasEventEarlyAccess, Is.False,
                "The membership is gone and the head start is not, so a player can buy one month "
                + "of National Press and keep the advantage for fourteen years.");

            Assert.That(simulation.WantsEarlyEventNotice, Is.False,
                "And the notices are still coming after the desk was cancelled.");
        }

        /// <summary>
        /// The list is longer with the early look than without it, on a day where that is the
        /// whole of the difference.
        /// </summary>
        [Test]
        public void TheMemberSeesMoreOfTheYearThanEverybodyElse()
        {
            var member = Company();
            member.SetIntelSubscription(IntelTier.NationalPress, true);
            member.SetEventEarlyAccess(true);

            var outsider = Company();

            // A day chosen so at least one event sits inside the extra month.
            var early = EventSeason.EarlyAnnouncedOn(CityEventId.ResearchSummit, 2025);
            member.State.Date = early;
            outsider.State.Date = early;

            var seen = member.UpcomingEvents().Select(row => row.Id).ToList();
            var theirs = outsider.UpcomingEvents().Select(row => row.Id).ToList();

            Assert.That(seen, Has.Member(CityEventId.ResearchSummit),
                "The member cannot see the event on the day their own membership opens it.");

            Assert.That(theirs, Has.No.Member(CityEventId.ResearchSummit),
                "Everybody can already see it, so the membership is selling nothing.");
        }
    }
}
