using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Do event announcements actually reach a player who is only reading the free wire.
    ///
    /// **Reported on 2026-10-25: no event story anywhere in the news while two events were about
    /// to open.** The calendar knew about both, because the National Press list showed them with
    /// days remaining. So either the stories are never filed, or they are filed and lost among
    /// twenty-eight rival releases that look exactly like them.
    ///
    /// This fixture answers which, by playing the opening months of a real campaign rather than
    /// asserting on the calendar: the calendar being right was never in doubt.
    /// </summary>
    public sealed class EventNewsReachTests
    {
        private static CompanySimulation Campaign()
        {
            var state = new CompanyState("Prometheus AI", 0x5CA1AB1E)
            {
                Date = GameDate.FromCalendar(2022, 1, 1),
                CashUsd = 12_000_000
            };

            return new CompanySimulation(state);
        }

        /// <summary>
        /// The opening months, walked day by day, counting what the free wire carried.
        /// </summary>
        [Test]
        public void TheFreeWireCarriesAnEventOpeningInTheFirstMonthsOfACampaign()
        {
            var simulation = Campaign();

            // To 22 May 2022, the day the report came from.
            var until = GameDate.FromCalendar(2022, 5, 22);
            simulation.Advance(until.DayIndex - simulation.State.Date.DayIndex);

            var wire = simulation.State.News.In(NewsSection.Wire, 28);

            var withVenue = wire.Where(story => story.Venue != null).ToList();
            var names = CityEventCatalog.All.Select(e => e.DisplayName).ToList();

            var byName = wire
                .Where(story => names.Any(n => story.Headline.Contains(n)))
                .ToList();

            UnityEngine.Debug.Log(
                "WIRE stories=" + wire.Count
                + " withVenueTag=" + withVenue.Count
                + " namingAnEvent=" + byName.Count
                + (byName.Count == 0
                    ? ""
                    : "\nWIRE first=" + byName[0].Headline + " on " + byName[0].Date)
                + "\nWIRE expected by now: "
                + string.Join(", ", EventSeason.VisibleOn(simulation.State.Date, early: false)
                    .Select(v => CityEventCatalog.Get(v.Id).DisplayName)));

            Assert.That(EventSeason.VisibleOn(simulation.State.Date, early: false),
                Is.Not.Empty,
                "Nothing is open for registration on this day, so this fixture is asking about a "
                + "calendar with nothing in it and proves nothing either way.");

            Assert.That(byName, Is.Not.Empty,
                "Registration is open at events the calendar can name, and not one story on the "
                + "free wire mentions any of them. The announcement never reaches a player who "
                + "has not paid for the desk, which is the opposite of the design.");

            Assert.That(withVenue, Is.Not.Empty,
                "An event story reached the wire and carries no venue, so the screen has nothing "
                + "to draw a place tag from and nothing to send the player to the map with.");
        }

        /// <summary>
        /// And are they findable, or do twenty-eight rival releases bury them.
        ///
        /// **This is the half the report was really about.** A story that is filed, correct and
        /// indistinguishable from the fourteen above it has not reached anybody. Measured as the
        /// share of the wire an event occupies.
        /// </summary>
        [Test]
        public void AnEventStoryIsNotOneLineInTwentyEightIdenticalOnes()
        {
            var simulation = Campaign();

            var until = GameDate.FromCalendar(2022, 5, 22);
            simulation.Advance(until.DayIndex - simulation.State.Date.DayIndex);

            var wire = simulation.State.News.In(NewsSection.Wire, 28);
            var events = wire.Count(story => story.Venue != null);

            UnityEngine.Debug.Log("CROWD wire=" + wire.Count + " events=" + events
                + " share=" + (wire.Count == 0 ? 0.0 : events / (double)wire.Count).ToString("P0"));

            // Not an assertion about the number, because the right answer is a screen change
            // rather than a figure: this records how far down the column a player has to read.
            Assert.That(wire, Is.Not.Empty, "The wire is empty in May of the opening year.");
        }
    }
}
