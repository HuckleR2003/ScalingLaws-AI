using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Which map categories are picked out from the rest.
    ///
    /// **A set rather than one category.** The legend was one-of-eight, so a player comparing where
    /// the compute halls sit against where the power comes from had to keep one of the two in their
    /// head. Ticking several is the question they were actually asking.
    ///
    /// **On its own, with no UnityEngine in it**, so the rules (ticking a lit row clears it, ticking
    /// a second row adds to the first, an empty set means the whole map is lit) are facts this file
    /// can be wrong about on its own, checked without a scene, a panel or a pin.
    /// </summary>
    public sealed class MapFilterState
    {
        private readonly HashSet<MapCategory> picked = new();

        /// <summary>The categories ticked, in the legend's own order. Empty means the whole map.</summary>
        public IReadOnlyList<MapCategory> Picked =>
            MapCategoryPalette.All.Where(picked.Contains).ToList();

        /// <summary>True while at least one category is ticked.</summary>
        public bool Filtering => picked.Count > 0;

        /// <summary>
        /// The one category ticked, or null when none or several are.
        ///
        /// Kept because the walk through the places (<see cref="MapTour"/>) and its tests were
        /// written against one category, and a single tick is still the common case.
        /// </summary>
        public MapCategory? Selected => picked.Count == 1 ? picked.First() : null;

        public event Action Changed;

        /// <summary>True when this category is ticked.</summary>
        public bool IsPicked(MapCategory category) => picked.Contains(category);

        /// <summary>Ticks a category, or unticks it when it is already ticked.</summary>
        public void Toggle(MapCategory category)
        {
            if (!picked.Remove(category))
            {
                picked.Add(category);
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            if (picked.Count == 0)
            {
                return;
            }

            picked.Clear();
            Changed?.Invoke();
        }

        /// <summary>True when nothing is ticked, or this is one of the categories that is.</summary>
        public bool IsEmphasised(MapCategory category) => picked.Count == 0 || picked.Contains(category);
    }
}
