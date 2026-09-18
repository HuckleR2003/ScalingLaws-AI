using System.Collections.Generic;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>How big an office is, as the map's OFFICES FOR RENT panel sorts them.</summary>
    public enum OfficeSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    /// <summary>
    /// The offices on the map a company could rent, filtered by size and walked one at a time.
    ///
    /// **Asked for as its own option under the map filters**: the legend's BUSINESS row lumps the
    /// offices in with houses, car dealers and rival headquarters, and a player looking for the next
    /// place to move into has one question, which is "where are the offices my size".
    ///
    /// **No UnityEngine in here**, the same split as <see cref="MapTour"/>: which size an office is,
    /// the order they come in and the wrap at the end are facts worth checking without a scene.
    /// </summary>
    public sealed class OfficeListing
    {
        /// <summary>Up to this many desks is small: the first rooms that are not the house.</summary>
        public const int SmallUpTo = 20;

        /// <summary>Up to this many desks is medium. Past it is a floor in a tower or bigger.</summary>
        public const int MediumUpTo = 60;

        private readonly HashSet<OfficeSize> picked = new();
        private int next;

        public OfficeListing()
        {
            Stops = StopsFor(picked);
        }

        /// <summary>The offices this walk visits, smallest first.</summary>
        public IReadOnlyList<MapSiteDefinition> Stops { get; private set; }

        public int Count => Stops.Count;

        /// <summary>Which office the next click shows, counting from one. Zero when there are none.</summary>
        public int NextOrdinal => Count == 0 ? 0 : next + 1;

        /// <summary>True when this size is ticked. Nothing ticked means every size.</summary>
        public bool IsPicked(OfficeSize size) => picked.Contains(size);

        /// <summary>Ticks a size, or unticks it, and starts the walk again over what is left.</summary>
        public void Toggle(OfficeSize size)
        {
            if (!picked.Remove(size))
            {
                picked.Add(size);
            }

            Stops = StopsFor(picked);
            next = 0;
        }

        /// <summary>The office to fly to, and the counter moves on. Null when there is none.</summary>
        public MapSiteDefinition Next()
        {
            if (Count == 0)
            {
                return null;
            }

            var stop = Stops[next];
            next = (next + 1) % Count;
            return stop;
        }

        /// <summary>
        /// How many desks an office site holds: its tier's own figure, or the announced figure for
        /// the two past the top of the ladder, which have no tier yet.
        /// </summary>
        public static int DesksOf(MapSiteDefinition site)
        {
            if (site == null)
            {
                return 0;
            }

            if (site.Tier > 0 && OfficeCatalog.TryGet((OfficeTier)site.Tier, out var office))
            {
                return office.Desks;
            }

            // The announced ones are matched by name: the site `office.soon.tower` is the announced
            // office named `office.soon.tower.name`. Read from the catalog so the panel and the
            // premises page quote the same number of desks.
            foreach (var entry in OfficeCatalog.ComingSoon)
            {
                if (entry.NameKey == site.Id + ".name")
                {
                    return entry.Desks;
                }
            }

            return 0;
        }

        public static OfficeSize SizeOf(MapSiteDefinition site)
        {
            var desks = DesksOf(site);

            if (desks <= SmallUpTo)
            {
                return OfficeSize.Small;
            }

            return desks <= MediumUpTo ? OfficeSize.Medium : OfficeSize.Large;
        }

        private static IReadOnlyList<MapSiteDefinition> StopsFor(HashSet<OfficeSize> sizes)
        {
            var stops = new List<MapSiteDefinition>();

            foreach (var site in MapSiteCatalog.OfKind(MapSiteKind.OfficeLease))
            {
                if (sizes.Count == 0 || sizes.Contains(SizeOf(site)))
                {
                    stops.Add(site);
                }
            }

            stops.Sort((left, right) => DesksOf(left).CompareTo(DesksOf(right)));
            return stops;
        }
    }
}
