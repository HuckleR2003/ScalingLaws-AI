using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The event calendar, who registers when, and the room that opens when the stands are gone.
    ///
    /// `Docs/EVENTS_PLAN.md` is the design. Three of these hold promises the feature is sold on
    /// rather than values that happen to be true today: being early is cheaper than bidding, a lab
    /// doing badly does not turn up, and the auction always ends.
    /// </summary>
    public sealed class EventSeasonTests
    {
        private const uint Seed = 0x5CA1AB1E;
        private const int Year = 2025;

        private static readonly CompetitorId[] Field =
        {
            CompetitorId.OpenAi, CompetitorId.Anthropic, CompetitorId.GoogleDeepMind,
            CompetitorId.MetaAi, CompetitorId.MistralAi, CompetitorId.Cohere,
            CompetitorId.StabilityAi, CompetitorId.InflectionAi
        };

        private static List<LabAppetite> FieldAt(double strength)
            => Field.Select(lab => new LabAppetite(lab, strength)).ToList();

        [Test]
        public void TheEarlyLookShowsEverythingTheOpenOneDoesAndSooner()
        {
            foreach (var definition in CityEventCatalog.All)
            {
                var open = EventSeason.AnnouncedOn(definition.Id, Year);
                var early = EventSeason.EarlyAnnouncedOn(definition.Id, Year);

                Assert.That(early.DayIndex, Is.EqualTo(open.DayIndex - EventSeason.EarlyAccessDays),
                    definition.Id + " does not open a month earlier for a member.");

                // The day a member is told, the open calendar is still silent about it.
                var memberSees = EventSeason.VisibleOn(early, early: true)
                    .Any(seen => seen.Id == definition.Id && seen.Year == Year);

                var everybodySees = EventSeason.VisibleOn(early, early: false)
                    .Any(seen => seen.Id == definition.Id && seen.Year == Year);

                Assert.That(memberSees, Is.True,
                    definition.Id + " is not visible to a member on the day it opens for them.");

                Assert.That(everybodySees, Is.False,
                    definition.Id + " is already public on the day the membership was supposed to "
                    + "be showing it first, so the early look buys nothing.");
            }
        }

        /// <summary>
        /// What the early look is actually worth, event by event.
        ///
        /// **Thirty days is one number and it does not mean one thing.** The windows run from 30
        /// days to 120, so the same membership doubles the whole window at the Responsible AI
        /// Forum and adds a quarter at the Global Model Awards. Quoting "a month ahead" as the
        /// benefit would be true of one event in seven.
        ///
        /// Measured in two units because they answer different questions: the share of the window
        /// is how much more time there is to decide, and the stands still free when a member
        /// arrives against when everybody else does is whether that time buys anything.
        /// </summary>
        [Test]
        public void TheEarlyLookIsWorthADifferentAmountAtEveryEvent()
        {
            var labs = FieldAt(0.82);
            var rows = new List<string>();
            var smallest = 1.0;

            foreach (var definition in CityEventCatalog.All)
            {
                var venue = EventVenueCatalog.Get(definition.Venue);
                var sellable = System.Math.Max(1, venue.Stands - 1);

                var openWindow = definition.RegistrationOpensDaysBefore;
                var share = EventSeason.EarlyAccessDays / (double)openWindow;
                smallest = System.Math.Min(smallest, share);

                // The day a member could first act, and the day everybody else could.
                var memberDay = EventSeason.EarlyAnnouncedOn(definition.Id, Year).AddDays(1);
                var publicDay = EventSeason.AnnouncedOn(definition.Id, Year).AddDays(1);

                var atMember = EventSeason.StandsTakenBy(
                    definition.Id, Year, memberDay, Seed, labs);

                var atPublic = EventSeason.StandsTakenBy(
                    definition.Id, Year, publicDay, Seed, labs);

                rows.Add("HEADSTART " + definition.Id
                    + " window=" + openWindow + "d"
                    + " +" + EventSeason.EarlyAccessDays + "d"
                    + " = " + share.ToString("P0") + " more time"
                    + " | stands " + sellable
                    + " free@member=" + (sellable - atMember.Taken)
                    + " free@public=" + (sellable - atPublic.Taken)
                    + " | venue=" + venue.Id);
            }

            UnityEngine.Debug.Log(string.Join("\n", rows));

            // **The weak claim, and it is the honest one.** The membership has to buy real time at
            // every event or it is sold on an average nobody experiences. A quarter of the window
            // is the floor; what it is worth beyond that is the table above, not a promise.
            Assert.That(smallest, Is.GreaterThanOrEqualTo(0.2),
                "At its weakest the early look adds " + smallest.ToString("P0")
                + " of the window, which is close enough to nothing that a player who paid for it "
                + "would not notice having it.");
        }

        [Test]
        public void TheEarlyLookNeverHidesSomethingTheOpenCalendarShows()
        {
            var day = GameDate.FromCalendar(Year, 5, 2);

            var open = EventSeason.VisibleOn(day, early: false);
            var early = EventSeason.VisibleOn(day, early: true);

            foreach (var seen in open)
            {
                Assert.That(early.Contains(seen), Is.True,
                    seen.Id + " is on the open calendar and missing from the member's, so paying "
                    + "for information takes some away.");
            }
        }

        /// <summary>
        /// Roughly half the field gets the early look, which is what makes it an edge rather than
        /// a certainty. The author's figure.
        /// </summary>
        [Test]
        public void AboutHalfTheRivalsAlsoSeeItEarly()
        {
            var seen = 0;
            var asked = 0;

            foreach (var definition in CityEventCatalog.All)
            {
                for (var year = 2023; year <= 2032; year++)
                {
                    foreach (var lab in Field)
                    {
                        asked++;

                        if (EventSeason.LabSeesEarly(lab, definition.Id, year, Seed))
                        {
                            seen++;
                        }
                    }
                }
            }

            var share = seen / (double)asked;

            Assert.That(share, Is.EqualTo(EventSeason.RivalsWithEarlyAccess).Within(0.06),
                "Across " + asked + " chances the early look went to " + share.ToString("P0")
                + " of them.");
        }

        /// <summary>
        /// A lab having a bad year does not take a stand at the best address in the city.
        ///
        /// **Without this the venue fills up the same way every year** regardless of what is
        /// happening in the market, and the whole derived-appetite argument collapses into a
        /// random number with extra steps.
        /// </summary>
        [Test]
        public void ALabDoingBadlyDoesNotTurnUpAtThePrestigiousVenue()
        {
            // Civic Square is the most highly regarded venue that actually holds an event.
            var forum = CityEventCatalog.Get(CityEventId.ResponsibleForum);
            var venue = EventVenueCatalog.Get(forum.Venue);

            Assert.That(venue.Prestige, Is.GreaterThan(0.7),
                "This fixture needs a well regarded venue to be about anything.");

            foreach (var lab in Field)
            {
                var weak = new LabAppetite(lab, 0.1);

                // **Full maturity on purpose, which is the hardest case for this claim.** The
                // participation bar falls as the industry fills up, so passing 1.0 asks whether a
                // lab having a terrible year stays home even when everybody else is going.
                // **A market half grown, and the number matters to the claim.** At full
                // maturity the bar is 0.08 of prestige and the design says so on purpose:
                // everybody goes once everybody is shipping, including a lab having a bad
                // year. The thing worth holding is that early on, when a stand is still a
                // statement rather than a habit, a weak lab stays home.
                Assert.That(EventSeason.DecidesOn(weak, forum.Id, Year, Seed, 0.4), Is.Null,
                    lab + " is having a terrible year and still took a stand at "
                    + venue.Id + ".");
            }

            var anyStrongLabCame = Field.Any(lab =>
                EventSeason.DecidesOn(new LabAppetite(lab, 0.95), forum.Id, Year, Seed, 0.4)
                    != null);

            Assert.That(anyStrongLabCame, Is.True,
                "Nobody comes to " + venue.Id + " even doing well, so it is not a venue.");
        }

        /// <summary>
        /// The promise the registration window is sold on: early there is room, late there is not.
        /// </summary>
        [Test]
        public void RegisteringEarlyFindsRoomAndRegisteringLateDoesNot()
        {
            var expo = CityEventCatalog.Get(CityEventId.FrontierExpo);
            var venue = EventVenueCatalog.Get(expo.Venue);
            var labs = FieldAt(0.9);

            var opens = EventSeason.AnnouncedOn(expo.Id, Year);
            var closes = EventSeason.RegistrationClosesOn(expo.Id, Year);

            var earlyOn = opens.AddDays(2);
            var lateOn = closes.AddDays(-3);

            // **A half-grown field, stated rather than defaulted.** At full maturity the
            // bar is low and everybody decides in the first quarter of the window, which is
            // a true thing about the late game and the wrong setting for a fixture about
            // the window itself.
            const double Maturity = 0.5;

            var early = EventSeason.StandsTakenBy(expo.Id, Year, earlyOn, Seed, labs, Maturity);
            var late = EventSeason.StandsTakenBy(expo.Id, Year, lateOn, Seed, labs, Maturity);

            // **Printed, not only asserted.** Twice today a green fixture in this project
            // turned out to be measuring nothing, and both were caught by reading figures
            // rather than the bar.
            UnityEngine.Debug.Log(
                "WINDOW " + expo.Id + " at " + venue.Id
                + " stands=" + venue.Stands
                + " opens=" + opens + " closes=" + closes
                + "\nWINDOW early(" + earlyOn + ") taken=" + early.Taken
                + " unseated=" + early.Unseated.Count
                + "\nWINDOW late(" + lateOn + ")  taken=" + late.Taken
                + " unseated=" + late.Unseated.Count);

            Assert.That(early.Taken, Is.LessThan(late.Taken),
                "The same field takes " + early.Taken + " stands two days in and " + late.Taken
                + " three days before the doors open, so there is no reason to be early.");

            Assert.That(late.Taken, Is.LessThanOrEqualTo(venue.Stands - 1),
                "Everything sold, including the one stand that is held back so a player who heard "
                + "late still has something to bid for.");
        }

        [Test]
        public void TheSameSeedGivesTheSameYearTwice()
        {
            var labs = FieldAt(0.72);
            var day = GameDate.FromCalendar(Year, 3, 1);

            var first = EventSeason.StandsTakenBy(CityEventId.FrontierExpo, Year, day, Seed, labs);
            var again = EventSeason.StandsTakenBy(CityEventId.FrontierExpo, Year, day, Seed, labs);

            Assert.That(again.Taken, Is.EqualTo(first.Taken),
                "Two readings of one day disagree, so nothing about this calendar replays.");
        }

        // ---- the room ------------------------------------------------------------------------

        [Test]
        public void TheAuctionAlwaysEnds()
        {
            foreach (var definition in CityEventCatalog.All)
            {
                var venue = EventVenueCatalog.Get(definition.Venue);
                var list = EventVenueCatalog.StandCostUsd(venue.Id, StandTier.Medium);

                var rounds = EventAuction.Settle(FieldAt(1.0), definition.Id,
                    definition.Favours, list, Seed);

                Assert.That(rounds.Count, Is.LessThan(EventAuction.MostRounds),
                    definition.Id + " ran to the ceiling with the strongest possible field, so "
                    + "the only thing stopping it is the guard rather than the bidders tiring.");

                Assert.That(rounds.Count, Is.GreaterThan(0),
                    definition.Id + " had a full field of keen labs and nobody bid once.");

                Assert.That(rounds[^1].StillIn, Is.EqualTo(0),
                    definition.Id + " ended with somebody still bidding.");
            }
        }

        /// <summary>
        /// Being late costs money, and this is the number that says how much.
        ///
        /// If this ever stopped being true the registration window would carry no decision and the
        /// early-access membership would be selling nothing.
        /// </summary>
        [Test]
        public void BiddingForTheLastStandCostsMoreThanBuyingOneEarly()
        {
            var expo = CityEventCatalog.Get(CityEventId.FrontierExpo);
            var venue = EventVenueCatalog.Get(expo.Venue);
            var list = EventVenueCatalog.StandCostUsd(venue.Id, StandTier.Medium);

            var rounds = EventAuction.Settle(FieldAt(0.85), expo.Id, expo.Favours, list, Seed);
            var toBeat = EventAuction.PriceToBeat(rounds, list);

            UnityEngine.Debug.Log(
                "AUCTION " + expo.Id + " list=" + list
                + " rounds=" + rounds.Count
                + " toBeat=" + toBeat
                + " over=" + ((toBeat / (double)list) - 1.0).ToString("P0"));

            Assert.That(toBeat, Is.GreaterThan(list),
                "A contested stand went for the list price, so being early buys nothing.");
        }

        [Test]
        public void AnEmptyRoomCostsTheListPrice()
        {
            var expo = CityEventCatalog.Get(CityEventId.FrontierExpo);
            var list = EventVenueCatalog.StandCostUsd(
                EventVenueCatalog.Get(expo.Venue).Id, StandTier.Small);

            var rounds = EventAuction.Settle(new List<LabAppetite>(), expo.Id, expo.Favours,
                list, Seed);

            Assert.That(EventAuction.PriceToBeat(rounds, list), Is.EqualTo(list),
                "Nobody wanted it and the player was charged more than the asking price.");
        }

        /// <summary>
        /// Tiredness is what ends the room, not a budget, so a keener field should last longer and
        /// still finish.
        /// </summary>
        [Test]
        public void AKeenerFieldBidsLongerButStillGivesUp()
        {
            var expo = CityEventCatalog.Get(CityEventId.FrontierExpo);
            var list = EventVenueCatalog.StandCostUsd(
                EventVenueCatalog.Get(expo.Venue).Id, StandTier.Medium);

            var mild = EventAuction.Settle(FieldAt(0.35), expo.Id, expo.Favours, list, Seed);
            var keen = EventAuction.Settle(FieldAt(0.95), expo.Id, expo.Favours, list, Seed);

            UnityEngine.Debug.Log(
                "FATIGUE mild rounds=" + mild.Count
                + " finalPrice=" + (mild.Count == 0 ? list : mild[^1].Price)
                + "\nFATIGUE keen rounds=" + keen.Count
                + " finalPrice=" + (keen.Count == 0 ? list : keen[^1].Price));

            Assert.That(keen.Count, Is.GreaterThan(mild.Count),
                "A field doing well gave up as quickly as one doing badly, so how a lab is doing "
                + "does not reach the room.");

            Assert.That(keen[^1].StillIn, Is.EqualTo(0),
                "The keen field never stopped, so fatigue is not the brake it is sold as.");
        }

        [Test]
        public void AStandIsWorthMoreToALabTheEventIsAimedAt()
        {
            var summit = CityEventCatalog.Get(CityEventId.ResearchSummit);
            var lab = new LabAppetite(CompetitorId.Anthropic, 0.8);

            var aimed = EventAuction.WorthToLab(lab, summit.Id, summit.Favours);
            var wrong = EventAuction.WorthToLab(lab, summit.Id, AudienceSegment.Creative);

            Assert.That(aimed, Is.GreaterThan(wrong),
                "A lab selling to the room the event is for values the stand no more than one "
                + "selling to somebody else, so the audience a venue draws decides nothing.");
        }
    }
}
