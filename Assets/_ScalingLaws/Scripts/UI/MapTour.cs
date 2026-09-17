using System.Collections.Generic;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The SHOW button at the foot of the legend: a way through every place on the map one click at
    /// a time, for a player who does not yet know where anything is.
    ///
    /// **It follows the filter.** With a category picked it walks that category's places; with
    /// nothing picked it walks all of them. Showing the player a tax office while the map is dimmed
    /// down to compute would be the legend and the button disagreeing about what the map is showing.
    ///
    /// **It goes back to the first place after <see cref="IdleResetSeconds"/> of nothing.** The
    /// counter on the button is the only thing that says where the walk has got to, and a player who
    /// stopped a minute ago, looked around and came back has no idea that "7/22" was theirs. Going
    /// quiet is treated as putting it down.
    ///
    /// **No UnityEngine in here.** The order, the wrap at the end, the idle reset and the counter
    /// are the parts worth being sure about, and none of them need a camera or a scene to be true.
    /// </summary>
    public sealed class MapTour
    {
        /// <summary>Quiet for this long and the walk starts again from the first place.</summary>
        public const float IdleResetSeconds = 8f;

        private int next;
        private float idleSeconds;
        private bool walking;

        public MapTour()
        {
            Stops = StopsFor(null);
        }

        /// <summary>The category being walked, or null for the whole map.</summary>
        public MapCategory? Category { get; private set; }

        /// <summary>The places this walk visits, in catalog order.</summary>
        public IReadOnlyList<MapSiteDefinition> Stops { get; private set; }

        public int Count => Stops.Count;

        /// <summary>Which place the next click shows, counting from one. Zero when there are none.</summary>
        public int NextOrdinal => Count == 0 ? 0 : next + 1;

        /// <summary>
        /// The place to fly to, and the counter moves on. Null when this category has nowhere to go,
        /// which no category has today and which is still not a reason to throw at a player.
        /// </summary>
        public MapSiteDefinition Show()
        {
            if (Count == 0)
            {
                return null;
            }

            var stop = Stops[next];

            next = (next + 1) % Count;
            idleSeconds = 0f;
            walking = true;

            return stop;
        }

        /// <summary>
        /// Counts the quiet. True when the counter moved back to the first place and the button
        /// needs redrawing, which is the only thing the caller has to do about it.
        /// </summary>
        public bool Tick(float seconds)
        {
            if (!walking)
            {
                return false;
            }

            idleSeconds += seconds;

            if (idleSeconds < IdleResetSeconds)
            {
                return false;
            }

            walking = false;
            idleSeconds = 0f;

            if (next == 0)
            {
                // The walk ended on the last place, so the counter already reads 1. Nothing moved.
                return false;
            }

            next = 0;
            return true;
        }

        /// <summary>
        /// The legend picked a different category, so the walk starts again over the new set. True
        /// when the button's text changes as a result, which it does whenever the set is not the
        /// same one it was already showing from the start.
        /// </summary>
        public bool Focus(MapCategory? category)
        {
            var wasAtTheStart = !walking && next == 0;
            var sameSet = Category == category;

            Category = category;
            Stops = StopsFor(category);

            next = 0;
            idleSeconds = 0f;
            walking = false;

            return !(sameSet && wasAtTheStart);
        }

        private static IReadOnlyList<MapSiteDefinition> StopsFor(MapCategory? category)
        {
            if (category == null)
            {
                return MapSiteCatalog.All;
            }

            var stops = new List<MapSiteDefinition>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (site.Category == category)
                {
                    stops.Add(site);
                }
            }

            return stops;
        }
    }
}
