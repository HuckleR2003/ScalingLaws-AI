using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>The six places in Bayview an event is actually held at.</summary>
    public enum EventVenueId
    {
        /// <summary>The long pool between the trophy towers. The best address, not the biggest room.</summary>
        ValleyPlaza = 0,

        /// <summary>Pools on a terrace cut into the hill above Innovation.</summary>
        TerracePark = 1,

        /// <summary>A square sized for a crowd rather than a festival.</summary>
        CivicSquare = 2,

        /// <summary>Lawns, a lake and the event ground. The biggest crowd in the city.</summary>
        BayviewPark = 3,

        /// <summary>The city's second indoor venue, so the gallery is not hosting everything.</summary>
        MediaPlaza = 4,

        /// <summary>Indoor halls with a car park behind them that takes the marquees.</summary>
        BayviewGallery = 5
    }

    /// <summary>
    /// How big a stand the company takes. Three sizes, and the large one is deliberately poor value.
    /// </summary>
    public enum StandTier
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    /// <summary>
    /// One venue: where it is, how many people come, how well it is thought of, and how many stands
    /// it has room for.
    ///
    /// **It carries the id of the pin that already exists rather than its own position.** The six
    /// `MapSiteKind.EventVenue` entries in <see cref="MapSiteCatalog"/> were drawn against a map the
    /// author has furnished by hand, and a second copy of a coordinate here is a second chance to
    /// mark the wrong spot on the map while every test stays green. `MapSiteId` is the join, and a
    /// test fails if it ever names a site that is not there or is not a venue.
    /// </summary>
    public readonly struct EventVenueDefinition
    {
        public EventVenueDefinition(EventVenueId id, string mapSiteId, double averageVisitors,
            double prestige, int stands)
        {
            Id = id;
            MapSiteId = mapSiteId ?? string.Empty;

            // Clamp in the constructor, the standing rule here: nothing downstream should have to
            // guard against a venue with no visitors or a prestige outside the scale.
            AverageVisitors = Math.Clamp(averageVisitors, 1.0, 1_000_000.0);
            Prestige = Math.Clamp(prestige, 0.0, 1.0);
            Stands = Math.Clamp(stands, 1, 64);
        }

        public EventVenueId Id { get; }

        /// <summary>The `MapSiteCatalog` entry this venue is, so the pin and the figures agree.</summary>
        public string MapSiteId { get; }

        /// <summary>
        /// Who comes through in a normal year.
        ///
        /// **Drives the following, not the regard.** More people means more fans arrive and a better
        /// chance the stand is talked about afterwards; it buys very little reputation on its own,
        /// because being seen by a crowd is not the same as being thought well of.
        /// </summary>
        public double AverageVisitors { get; }

        /// <summary>
        /// How well the address is thought of, 0 to 1, with Valley Plaza at the top.
        ///
        /// **Drives the regard, and multiplies the following.** This is the figure that separates a
        /// festival ground from a civic square, and it is why the biggest crowd in the table is not
        /// the best place to be seen.
        /// </summary>
        public double Prestige { get; }

        /// <summary>
        /// How many stands the place has room for, which is what makes an auction possible at all.
        ///
        /// Civic Square holds two, so a second interested lab is already a problem; Bayview Park
        /// holds eight and is usually just a purchase. The venue's size is the scarcity.
        /// </summary>
        public int Stands { get; }

        private static string KeyFor(EventVenueId id) => id switch
        {
            EventVenueId.ValleyPlaza => "venue.valleyplaza",
            EventVenueId.TerracePark => "venue.terracepark",
            EventVenueId.CivicSquare => "venue.civicsquare",
            EventVenueId.BayviewPark => "venue.bayviewpark",
            EventVenueId.MediaPlaza => "venue.mediaplaza",
            EventVenueId.BayviewGallery => "venue.bayviewgallery",
            _ => "venue.valleyplaza"
        };

        /// <summary>
        /// Read from the book at access time, never stored.
        ///
        /// **A catalog built at type load keeps whatever language it was built in.** Nineteen
        /// catalogs in this project have been caught storing player-facing English, the last of them
        /// after this file's own rule had been written down twice, so this one is built that way from
        /// the first line. The arms above are literals rather than a stem plus the enum name,
        /// because `LocalisationTests` can only read literals.
        /// </summary>
        public string DisplayName => Loc.T(KeyFor(Id));

        /// <summary>Whose event it is. The cyan line under the name.</summary>
        public string OrganiserName => Loc.T(KeyFor(Id) + ".organiser");

        /// <summary>What the place is, in a sentence, for the window.</summary>
        public string Blurb => Loc.T(KeyFor(Id) + ".blurb");

        /// <summary>
        /// One line said from the stand afterwards, so two events at one venue do not read alike.
        /// </summary>
        public string ClosingComment => Loc.T(KeyFor(Id) + ".closing");

        /// <summary>
        /// What a small stand costs here, derived from the two figures above rather than listed.
        ///
        /// **One number per venue, not two, so the price cannot drift from the place.** Weighted so
        /// prestige carries real money without overtaking footfall: the park is the dearest place in
        /// the city because the crowd is the thing being bought, and Civic Square is cheap and well
        /// regarded, which is exactly the trade a company chasing reputation rather than reach wants
        /// to find.
        /// </summary>
        public long SmallStandCostUsd
            => (long)Math.Round(AverageVisitors * (0.5 + Prestige) * 1.4);

        public override string ToString()
            => $"{Id} ({AverageVisitors:N0} visitors, prestige {Prestige:0.00}, {Stands} stands)";
    }

    /// <summary>
    /// The six venues and the stand ladder, as pure data.
    ///
    /// `Docs/EVENTS_PLAN.md` is the design and the argument; this is the table it settled on. The
    /// venues are the six pins already on the map, so adding a seventh means adding a
    /// `MapSiteKind.EventVenue` entry first and then a row here, in that order.
    /// </summary>
    public static class EventVenueCatalog
    {
        public const string CatalogVersion = "venues-2026.10.24";

        /// <summary>
        /// Each tier up is worth about 60% more. The author's figure.
        /// </summary>
        public const double EffectPerTier = 1.6;

        /// <summary>
        /// Each tier up costs 100% more. The author's figure, and the reason the ladder is a
        /// decision.
        /// </summary>
        public const double CostPerTier = 2.0;

        private static readonly EventVenueDefinition[] Entries =
        {
            new(EventVenueId.ValleyPlaza, "event.valleyplaza", 42_000, 1.00, 3),
            new(EventVenueId.TerracePark, "event.hillpark", 18_000, 0.86, 3),
            new(EventVenueId.CivicSquare, "event.civicsquare", 12_000, 0.80, 2),
            new(EventVenueId.BayviewPark, "event.park", 85_000, 0.72, 8),
            new(EventVenueId.MediaPlaza, "event.mediaplaza", 24_000, 0.68, 4),
            new(EventVenueId.BayviewGallery, "event.gallery", 31_000, 0.62, 5)
        };

        public static IReadOnlyList<EventVenueDefinition> All => Entries;

        public static EventVenueDefinition Get(EventVenueId id)
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

        /// <summary>The venue a map pin is, or null for a pin that is not a venue.</summary>
        public static EventVenueDefinition? ForMapSite(string mapSiteId)
        {
            if (string.IsNullOrEmpty(mapSiteId))
            {
                return null;
            }

            foreach (var entry in Entries)
            {
                if (entry.MapSiteId == mapSiteId)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// How much more a tier is worth than a small stand: 1.00, 1.60, 2.56.
        /// </summary>
        public static double EffectMultiple(StandTier tier)
            => Math.Pow(EffectPerTier, StepsAbove(tier));

        /// <summary>
        /// How much more a tier costs than a small stand: 1, 2, 4.
        /// </summary>
        public static double CostMultiple(StandTier tier)
            => Math.Pow(CostPerTier, StepsAbove(tier));

        /// <summary>What that stand costs at that venue.</summary>
        public static long StandCostUsd(EventVenueId venue, StandTier tier)
            => (long)Math.Round(Get(venue).SmallStandCostUsd * CostMultiple(tier));

        /// <summary>
        /// Effect bought per dollar, which **falls** with every tier and is meant to.
        ///
        /// Exposed because it is the property the ladder is judged on rather than an internal
        /// detail: the large stand buys 2.56 times the result for four times the money, so it is
        /// the expensive way to get a result now. If this ever stopped falling, the large stand
        /// would be strictly correct and the three tiles would be one tile. There is a test.
        /// </summary>
        public static double EffectPerDollar(EventVenueId venue, StandTier tier)
        {
            var cost = StandCostUsd(venue, tier);

            return cost <= 0L ? 0.0 : EffectMultiple(tier) / cost;
        }

        private static string KeyForTier(StandTier tier) => tier switch
        {
            StandTier.Small => "stand.small",
            StandTier.Medium => "stand.medium",
            StandTier.Large => "stand.large",
            _ => "stand.small"
        };

        /// <summary>Read from the book at access time, for the reason written on the venue.</summary>
        public static string TierName(StandTier tier) => Loc.T(KeyForTier(tier));

        /// <summary>What taking that size of stand actually gets you, in a few words.</summary>
        public static string TierNote(StandTier tier) => Loc.T(KeyForTier(tier) + ".note");

        private static int StepsAbove(StandTier tier) => tier switch
        {
            StandTier.Small => 0,
            StandTier.Medium => 1,
            StandTier.Large => 2,
            _ => 0
        };
    }
}
