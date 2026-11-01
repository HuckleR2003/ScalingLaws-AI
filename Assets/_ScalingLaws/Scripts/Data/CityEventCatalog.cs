using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The events Bayview holds, every one of them already named in a venue's own description on
    /// the map before any of this was built.
    /// </summary>
    public enum CityEventId
    {
        /// <summary>The year's big one. Lawns, marquees, everybody.</summary>
        FrontierExpo = 0,

        /// <summary>Racks, fabric and power. Indoors, and the trade comes to it.</summary>
        InfrastructureExpo = 1,

        /// <summary>Papers and posters. Small, and the people who read them matter.</summary>
        ResearchSummit = 2,

        /// <summary>Policy, safety and whoever has something to answer for.</summary>
        ResponsibleForum = 3,

        /// <summary>Makers, studios and the audience that follows them.</summary>
        CreatorFestival = 4,

        /// <summary>Money. The room where next year's funding is decided.</summary>
        CapitalForum = 5,

        /// <summary>The ceremony at the end of the year, and the one everybody films.</summary>
        ModelAwards = 6
    }

    /// <summary>
    /// One event: where it is held, when it runs, and how long before it the stands go on sale.
    ///
    /// **The venue assignments are not a new design decision.** Every one of these seven names was
    /// already written into a `MapSiteKind.EventVenue` blurb in <see cref="MapSiteCatalog"/>, each
    /// naming the venue it belongs to, and that prose is what this table is built from rather than
    /// a fresh allocation. `CityEventTests` reads the map's own descriptions and fails if an event
    /// is ever moved away from the venue whose blurb claims it.
    /// </summary>
    public readonly struct CityEventDefinition
    {
        public CityEventDefinition(CityEventId id, EventVenueId venue, int month, int runsForDays,
            int registrationOpensDaysBefore, AudienceSegment favours)
        {
            Id = id;
            Venue = venue;
            Month = Math.Clamp(month, 1, 12);
            RunsForDays = Math.Clamp(runsForDays, 1, 14);
            RegistrationOpensDaysBefore = Math.Clamp(registrationOpensDaysBefore, 7, 365);
            Favours = favours;
        }

        public CityEventId Id { get; }

        /// <summary>Where it is held. The venue carries the crowd, the regard and the stand count.</summary>
        public EventVenueId Venue { get; }

        /// <summary>Which month of the year it runs in. The same month every year.</summary>
        public int Month { get; }

        public int RunsForDays { get; }

        /// <summary>
        /// How far ahead the stands go on sale, and it is the whole decision this system creates.
        ///
        /// **Varied per event on purpose.** A player who learns one number stops reading the
        /// calendar, and the calendar is what they are supposed to be planning against. The awards
        /// open four months out and the Responsible AI Forum a month out, so the same lateness means
        /// different things at different events.
        /// </summary>
        public int RegistrationOpensDaysBefore { get; }

        /// <summary>
        /// Whose event it is, in terms the market already understands.
        ///
        /// Read two ways: it is why a lab selling to this audience wants the stand badly enough to
        /// bid for it, and it is why a company that sells to somebody else gets less out of being
        /// there. It is an existing <see cref="AudienceSegment"/> rather than a new list, because
        /// the game already has one mechanism for who a thing is for.
        /// </summary>
        public AudienceSegment Favours { get; }

        private static string KeyFor(CityEventId id) => id switch
        {
            CityEventId.FrontierExpo => "cityevent.frontierexpo",
            CityEventId.InfrastructureExpo => "cityevent.infraexpo",
            CityEventId.ResearchSummit => "cityevent.researchsummit",
            CityEventId.ResponsibleForum => "cityevent.responsibleforum",
            CityEventId.CreatorFestival => "cityevent.creatorfestival",
            CityEventId.CapitalForum => "cityevent.capitalforum",
            CityEventId.ModelAwards => "cityevent.modelawards",
            _ => "cityevent.frontierexpo"
        };

        /// <summary>Read from the book at access time. See the note on the venue definition.</summary>
        public string DisplayName => Loc.T(KeyFor(Id));

        /// <summary>What it is, for the news story and the window.</summary>
        public string Blurb => Loc.T(KeyFor(Id) + ".blurb");

        public override string ToString()
            => $"{Id} at {Venue}, month {Month}, opens {RegistrationOpensDaysBefore}d ahead";
    }

    /// <summary>
    /// The seven events, as pure data. `Docs/EVENTS_PLAN.md` is the design behind it.
    ///
    /// Two venues carry no event here and that is deliberate rather than unfinished: Terrace Park
    /// and Valley Plaza are described on the map as places a **launch** is held, which is a company
    /// holding its own event rather than taking a stand at somebody else's, and the plan document
    /// lists that as out of scope and the author's call. Assigning them a conference to fill the
    /// table would spend the two best addresses in the city on the wrong feature.
    /// </summary>
    public static class CityEventCatalog
    {
        public const string CatalogVersion = "cityevents-2026.10.24";

        private static readonly CityEventDefinition[] Entries =
        {
            // Spread across the year so something is always coming, and so a company that misses
            // one is never waiting long enough to forget the system exists.
            new(CityEventId.FrontierExpo, EventVenueId.BayviewPark,
                3, 3, 90, AudienceSegment.Consumer),

            new(CityEventId.InfrastructureExpo, EventVenueId.BayviewGallery,
                5, 2, 60, AudienceSegment.Enterprise),

            new(CityEventId.ResearchSummit, EventVenueId.MediaPlaza,
                6, 2, 45, AudienceSegment.Developer),

            new(CityEventId.CreatorFestival, EventVenueId.BayviewGallery,
                8, 3, 75, AudienceSegment.Creative),

            new(CityEventId.ResponsibleForum, EventVenueId.CivicSquare,
                9, 1, 30, AudienceSegment.Enterprise),

            new(CityEventId.CapitalForum, EventVenueId.MediaPlaza,
                11, 2, 50, AudienceSegment.Enterprise),

            new(CityEventId.ModelAwards, EventVenueId.BayviewPark,
                12, 1, 120, AudienceSegment.Consumer)
        };

        public static IReadOnlyList<CityEventDefinition> All => Entries;

        public static CityEventDefinition Get(CityEventId id)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    return entry;
                }
            }

            return Entries[0];
        }

        /// <summary>Everything held at one venue, in the order the year runs.</summary>
        public static IEnumerable<CityEventDefinition> At(EventVenueId venue)
        {
            foreach (var entry in Entries)
            {
                if (entry.Venue == venue)
                {
                    yield return entry;
                }
            }
        }
    }
}
