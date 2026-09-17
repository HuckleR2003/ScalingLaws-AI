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
        /// <summary>Pointing down: the rows underneath are showing.</summary>
        private const string ExpandedGlyph = "▾";

        /// <summary>Pointing sideways: the panel is collapsed to its header bar.</summary>
        private const string CollapsedGlyph = "▸";

        private readonly MapFilterState state;
        private readonly Dictionary<MapCategory, VisualElement> rows = new();
        private readonly VisualElement body;
        private readonly Label toggleGlyph;
        private bool collapsed;

        public MapLegendPanel(MapFilterState filterState)
        {
            state = filterState;
            AddToClassList("map-legend");

            // A header the panel keeps even when collapsed, so there is always something on
            // screen to click to bring the rows back — a legend that can vanish with no trace
            // of itself would need a second control somewhere else just to undo the first.
            var header = new VisualElement();
            header.AddToClassList("map-legend__header");
            Add(header);

            var title = new Label(Loc.T("map.filters.title"));
            title.AddToClassList("map-legend__title");
            header.Add(title);

            toggleGlyph = new Label(ExpandedGlyph);
            toggleGlyph.pickingMode = PickingMode.Ignore;

            var toggle = new Button(ToggleCollapsed);
            toggle.AddToClassList("map-legend__toggle");
            toggle.Add(toggleGlyph);
            header.Add(toggle);

            body = new VisualElement();
            body.AddToClassList("map-legend__body");
            Add(body);

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
                body.Add(row);
            }

            state.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => state.Changed -= Refresh);

            Refresh();
        }

        private void ToggleCollapsed()
        {
            collapsed = !collapsed;
            body.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            toggleGlyph.text = collapsed ? CollapsedGlyph : ExpandedGlyph;
            EnableInClassList("map-legend--collapsed", collapsed);
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
