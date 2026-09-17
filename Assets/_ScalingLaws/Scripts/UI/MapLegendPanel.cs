using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The eight-category legend, doubling as the filter: click a row to pick it out on the map,
    /// click it again to go back to showing everything.
    ///
    /// Etap 5 of Docs/CITY_MAP_PLAN.md, the piece the author called "the most premium element of
    /// the UI" in the notes it was planned from. Rows and colours read from
    /// <see cref="MapCategoryPalette"/>, the same place <see cref="MapSitePin"/>'s dimming reads
    /// from, so a row and the pins it is meant to pick out cannot show two different greens.
    /// </summary>
    public sealed class MapLegendPanel : VisualElement
    {
        private readonly MapFilterState state;
        private readonly Dictionary<MapCategory, VisualElement> rows = new();

        public MapLegendPanel(MapFilterState filterState)
        {
            state = filterState;
            AddToClassList("map-legend");

            var title = new Label(Loc.T("map.filters.title"));
            title.AddToClassList("map-legend__title");
            Add(title);

            foreach (var category in MapCategoryPalette.All)
            {
                var captured = category;
                var row = new Button(() => state.Toggle(captured));
                row.AddToClassList("map-legend__row");

                var swatch = new VisualElement();
                swatch.AddToClassList("map-legend__swatch");
                swatch.style.backgroundColor = MapCategoryPalette.ColourFor(category);
                row.Add(swatch);

                var label = new Label(Loc.T(MapCategoryPalette.NameKey(category)));
                label.AddToClassList("map-legend__label");
                row.Add(label);

                rows[category] = row;
                Add(row);
            }

            state.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => state.Changed -= Refresh);

            Refresh();
        }

        private void Refresh()
        {
            foreach (var (category, row) in rows)
            {
                row.EnableInClassList("map-legend__row--on", state.Selected == category);
                row.EnableInClassList("map-legend__row--dim",
                    state.Selected.HasValue && state.Selected != category);
            }
        }
    }
}
