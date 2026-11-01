using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The venues, the events held at them, and the stand ladder.
    ///
    /// `Docs/EVENTS_PLAN.md` is the design. Two of these fixtures hold properties the whole feature
    /// stands on rather than values that happen to be in the table today, and both would pass
    /// silently if somebody tuned the numbers without reading them.
    /// </summary>
    public sealed class CityEventTests
    {
        /// <summary>
        /// The words each venue's own map description uses for the events it claims.
        ///
        /// **This is the join that stops the assignment being invented.** All seven of these names
        /// were written into `MapSiteCatalog` blurbs before any of this was built, each naming the
        /// venue it belongs to, and `CityEventCatalog` is supposed to be a transcription of that
        /// rather than a fresh allocation. The fixture reads the map's own prose, so moving an event
        /// to a different venue fails here until the map is changed to match.
        /// </summary>
        private static readonly Dictionary<CityEventId, string> NamedInTheMapBlurb = new()
        {
            [CityEventId.FrontierExpo] = "Frontier Expo",
            [CityEventId.ModelAwards] = "Model Awards",
            [CityEventId.InfrastructureExpo] = "Infrastructure Expo",
            [CityEventId.CreatorFestival] = "Creator Intelligence",
            [CityEventId.ResponsibleForum] = "Responsible AI Forum",
            [CityEventId.ResearchSummit] = "Research Summit",
            [CityEventId.CapitalForum] = "Capital & AI"
        };

        private static MapSiteDefinition MapSite(string id)
            => MapSiteCatalog.All.First(site => site.Id == id);

        [Test]
        public void EveryVenueIsAPinThatAlreadyStandsOnTheMap()
        {
            foreach (var venue in EventVenueCatalog.All)
            {
                var matches = MapSiteCatalog.All.Where(site => site.Id == venue.MapSiteId).ToList();

                Assert.That(matches.Count, Is.EqualTo(1),
                    venue.Id + " names map site '" + venue.MapSiteId + "', which the map has "
                    + matches.Count + " of. The figures and the pin have to be the same place.");

                Assert.That(matches[0].Kind, Is.EqualTo(MapSiteKind.EventVenue),
                    venue.Id + " points at a pin that is a " + matches[0].Kind
                    + " rather than a venue.");
            }
        }

        [Test]
        public void EveryVenueOnTheMapHasFiguresBehindIt()
        {
            var pins = MapSiteCatalog.All
                .Where(site => site.Kind == MapSiteKind.EventVenue)
                .ToList();

            foreach (var pin in pins)
            {
                Assert.That(EventVenueCatalog.ForMapSite(pin.Id), Is.Not.Null,
                    "The map draws '" + pin.Id + "' as an event venue and no row in "
                    + "EventVenueCatalog says how many people come to it, so clicking it can only "
                    + "ever say nothing. A pin with no system behind it is the state this feature "
                    + "exists to leave.");
            }

            Assert.That(EventVenueCatalog.All.Count, Is.EqualTo(pins.Count),
                "The two lists are different lengths, so one of them has a place the other does "
                + "not.");
        }

        /// <summary>
        /// The property the whole venue table stands on: the biggest crowd is not the best address.
        ///
        /// **Same shape as `NoChannelIsSimplyBetterThanAnother`.** The moment one venue is both the
        /// largest and the most highly regarded, every other venue is a worse version of it and six
        /// places collapse into one. Visitors buy a following and prestige buys regard, and a player
        /// has to be choosing between those rather than reading a ladder.
        /// </summary>
        [Test]
        public void TheBiggestCrowdIsNotAlsoTheBestAddress()
        {
            var busiest = EventVenueCatalog.All
                .OrderByDescending(venue => venue.AverageVisitors)
                .First();

            var grandest = EventVenueCatalog.All
                .OrderByDescending(venue => venue.Prestige)
                .First();

            Assert.That(busiest.Id, Is.Not.EqualTo(grandest.Id),
                busiest.Id + " has both the largest crowd and the highest prestige, so there is no "
                + "reason to go anywhere else and the other five venues are decoration.");
        }

        [Test]
        public void NoVenueIsSimplyBetterThanAnother()
        {
            foreach (var left in EventVenueCatalog.All)
            {
                foreach (var right in EventVenueCatalog.All)
                {
                    if (left.Id == right.Id)
                    {
                        continue;
                    }

                    var cheaperOrSame = left.SmallStandCostUsd <= right.SmallStandCostUsd;
                    var betterOnEverything = left.AverageVisitors >= right.AverageVisitors
                        && left.Prestige >= right.Prestige
                        && left.Stands >= right.Stands;

                    Assert.That(cheaperOrSame && betterOnEverything, Is.False,
                        left.Id + " is at least as cheap as " + right.Id + " and at least as good "
                        + "on the crowd, the prestige and the room, so nobody would ever pick "
                        + right.Id + ".");
                }
            }
        }

        /// <summary>
        /// The stand ladder, both halves, and the second one is the half that makes it a decision.
        ///
        /// Each tier up is worth more and each tier up is **worse value per dollar**: 2.56 times the
        /// result for four times the money at the top. That is the same trade the dear marketing
        /// channels make. If value per dollar ever stopped falling, the large stand would be
        /// strictly correct and the three tiles the author asked for would be one tile.
        /// </summary>
        [Test]
        public void EachStandTierIsStrongerAndWorseValueThanTheOneBelow()
        {
            var tiers = new[] { StandTier.Small, StandTier.Medium, StandTier.Large };

            foreach (var venue in EventVenueCatalog.All)
            {
                for (var step = 1; step < tiers.Length; step++)
                {
                    var below = tiers[step - 1];
                    var above = tiers[step];

                    Assert.That(EventVenueCatalog.EffectMultiple(above),
                        Is.GreaterThan(EventVenueCatalog.EffectMultiple(below)),
                        above + " is not worth more than " + below + ".");

                    Assert.That(EventVenueCatalog.StandCostUsd(venue.Id, above),
                        Is.GreaterThan(EventVenueCatalog.StandCostUsd(venue.Id, below)),
                        above + " at " + venue.Id + " does not cost more than " + below + ".");

                    Assert.That(EventVenueCatalog.EffectPerDollar(venue.Id, above),
                        Is.LessThan(EventVenueCatalog.EffectPerDollar(venue.Id, below)),
                        above + " at " + venue.Id + " buys at least as much per dollar as "
                        + below + ", so the big stand is simply the right answer and the ladder "
                        + "has stopped being a decision.");
                }
            }
        }

        [Test]
        public void TheLargeStandIsFourTimesTheMoneyForTwoAndAHalfTimesTheResult()
        {
            Assert.That(EventVenueCatalog.EffectMultiple(StandTier.Large),
                Is.EqualTo(2.56).Within(1e-9),
                "The author's figure is about 60% more per tier, which compounds to 2.56.");

            Assert.That(EventVenueCatalog.CostMultiple(StandTier.Large),
                Is.EqualTo(4.0).Within(1e-9),
                "And 100% more per tier, which compounds to 4.");
        }

        [Test]
        public void EveryEventIsHeldWhereItsOwnVenueSaysItIs()
        {
            foreach (var definition in CityEventCatalog.All)
            {
                Assert.That(NamedInTheMapBlurb.ContainsKey(definition.Id), Is.True,
                    definition.Id + " is in the catalogue and this fixture does not know which "
                    + "words to look for on the map. Add it above with the phrase its venue's "
                    + "blurb uses, or say why the venue does not name it.");

                var venue = EventVenueCatalog.Get(definition.Venue);
                var blurb = MapSite(venue.MapSiteId).DecisionBlurb;

                Assert.That(blurb, Does.Contain(NamedInTheMapBlurb[definition.Id]),
                    definition.Id + " is held at " + definition.Venue + ", whose map description "
                    + "does not mention it. The venue assignments are a transcription of the map's "
                    + "own prose, so this is either the wrong venue or a change the map has not "
                    + "been told about. The blurb reads: " + blurb);
            }
        }

        [Test]
        public void TheRegistrationWindowsAreNotAllTheSame()
        {
            var windows = CityEventCatalog.All
                .Select(definition => definition.RegistrationOpensDaysBefore)
                .Distinct()
                .Count();

            Assert.That(windows, Is.GreaterThanOrEqualTo(4),
                "The windows are the one thing that makes being early a decision. If they are all "
                + "the same number a player learns it once and stops reading the calendar.");
        }

        [Test]
        public void TheYearHasEventsSpreadThroughItRatherThanBunched()
        {
            var months = CityEventCatalog.All
                .Select(definition => definition.Month)
                .Distinct()
                .OrderBy(month => month)
                .ToList();

            Assert.That(months.Count, Is.GreaterThanOrEqualTo(5),
                "Everything is in " + months.Count + " month(s) of the year, so a company that "
                + "misses the run waits long enough to forget the system is there.");

            Assert.That(months.First(), Is.LessThanOrEqualTo(4),
                "Nothing happens in the first third of the year.");

            Assert.That(months.Last(), Is.GreaterThanOrEqualTo(9),
                "Nothing happens in the last third of the year.");
        }

        /// <summary>
        /// Both catalogs read the phrase book at access time, in both languages.
        ///
        /// **These are the twentieth and twenty-first catalogs in this project**, and nineteen of
        /// the first nineteen were caught storing player-facing English after the rule had been
        /// written down twice. The keys here are built from a stem in `KeyFor`, which makes them
        /// invisible to the guard that reads literals, so this is the fixture that shape of key
        /// exists to be covered by.
        /// </summary>
        [Test]
        public void EveryVenueAndEventHasItsWordsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var venue in EventVenueCatalog.All)
                    {
                        foreach (var (what, text) in new[]
                        {
                            ("name", venue.DisplayName),
                            ("organiser", venue.OrganiserName),
                            ("blurb", venue.Blurb),
                            ("closing comment", venue.ClosingComment)
                        })
                        {
                            Assert.That(text, Is.Not.Empty,
                                venue.Id + " has no " + what + " in " + language);

                            Assert.That(text, Does.Not.StartWith("venue."),
                                venue.Id + "'s " + what + " came back as its own key in "
                                + language + ", which is what a missing phrase renders as: "
                                + text);
                        }
                    }

                    foreach (var definition in CityEventCatalog.All)
                    {
                        Assert.That(definition.DisplayName, Is.Not.Empty,
                            definition.Id + " has no name in " + language);

                        Assert.That(definition.DisplayName, Does.Not.StartWith("cityevent."),
                            definition.Id + " renders as its own key in " + language);

                        Assert.That(definition.Blurb, Does.Not.StartWith("cityevent."),
                            definition.Id + "'s blurb renders as its own key in " + language);
                    }

                    foreach (var tier in new[] { StandTier.Small, StandTier.Medium, StandTier.Large })
                    {
                        Assert.That(EventVenueCatalog.TierName(tier), Does.Not.StartWith("stand."),
                            tier + " renders as its own key in " + language);

                        Assert.That(EventVenueCatalog.TierNote(tier), Does.Not.StartWith("stand."),
                            tier + "'s note renders as its own key in " + language);
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
