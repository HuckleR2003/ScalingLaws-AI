using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The eight map colours, in one place, read by the 3D pins and the legend chips alike.
    ///
    /// **One source, because two would drift.** `MapSiteBuilder` paints pins in the city scene and
    /// `MapLegendPanel` paints chips in the filter panel, and a legend whose colours do not match
    /// what is actually on the map is worse than no legend — it teaches the wrong thing with
    /// confidence. <see cref="MapCategory"/>'s own doc comment names the eight colours; this is the
    /// only place that turns those names into actual <see cref="Color"/> values.
    /// </summary>
    public static class MapCategoryPalette
    {
        public static Color ColourFor(MapCategory category) => category switch
        {
            MapCategory.Compute => new Color(0.10f, 0.78f, 0.80f),
            MapCategory.Business => new Color(0.22f, 0.42f, 0.92f),
            MapCategory.Research => new Color(0.56f, 0.26f, 0.86f),
            MapCategory.Events => new Color(0.95f, 0.66f, 0.10f),
            MapCategory.Media => new Color(0.86f, 0.20f, 0.74f),
            MapCategory.Energy => new Color(0.26f, 0.76f, 0.32f),
            MapCategory.Finance => new Color(0.85f, 0.70f, 0.16f),
            MapCategory.Regulation => new Color(0.86f, 0.22f, 0.22f),
            _ => Color.white
        };

        /// <summary>The phrase-book key for a category's name, e.g. "map.category.compute".</summary>
        public static string NameKey(MapCategory category) => category switch
        {
            MapCategory.Compute => "map.category.compute",
            MapCategory.Business => "map.category.business",
            MapCategory.Research => "map.category.research",
            MapCategory.Events => "map.category.events",
            MapCategory.Media => "map.category.media",
            MapCategory.Energy => "map.category.energy",
            MapCategory.Finance => "map.category.finance",
            MapCategory.Regulation => "map.category.regulation",
            _ => string.Empty
        };

        /// <summary>
        /// All eight, in the order the legend lists them.
        ///
        /// Compute first because it is the district a new company sees soonest (the office starts
        /// nowhere near it, but every campaign's first big purchase is compute); Regulation last
        /// because it is the one nobody goes looking for on purpose.
        /// </summary>
        public static readonly MapCategory[] All =
        {
            MapCategory.Compute,
            MapCategory.Business,
            MapCategory.Research,
            MapCategory.Events,
            MapCategory.Media,
            MapCategory.Energy,
            MapCategory.Finance,
            MapCategory.Regulation
        };
    }
}
