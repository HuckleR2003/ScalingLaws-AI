using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The eight-category legend, doubling as the filter: tick a row to pick its places out on the
    /// map, tick a second to add it, tick a lit row again to drop it.
    ///
    /// Etap 5 of Docs/CITY_MAP_PLAN.md, the piece the author called "the most premium element of
    /// the UI" in the notes it was planned from. Rows and colours read from
    /// <see cref="MapCategoryPalette"/>, the same place <see cref="MapSitePin"/>'s dimming reads
    /// from, so a row and the pins it is meant to pick out cannot show two different greens.
    ///
    /// **Tick boxes rather than a lit row.** The panel used to show which category was picked only by
    /// going slightly brighter, which is invisible next to seven rows of colour. A box that is either
    /// empty or filled with a tick says it at a glance, and it is also the control that makes several
    /// categories at once look possible, which they now are.
    /// </summary>
    public sealed class MapLegendPanel : VisualElement
    {
        /// <summary>Pointing down: the rows underneath are showing.</summary>
        private const string ExpandedGlyph = "▾";

        /// <summary>Pointing sideways: the panel is collapsed to its header bar.</summary>
        private const string CollapsedGlyph = "▸";

        /// <summary>
        /// The tick, drawn rather than typed. Montserrat has no check mark, so a "✓" would reach the
        /// screen only through whatever fallback font the platform happens to offer, or as a box.
        /// </summary>
        private static readonly Color TickInk = new(0.047f, 0.063f, 0.086f);

        private readonly MapFilterState state;
        private readonly Dictionary<MapCategory, VisualElement> rows = new();
        private readonly Dictionary<MapCategory, VisualElement> ticks = new();
        private readonly VisualElement body;
        private readonly Label toggleGlyph;
        private readonly Button clear;
        private readonly Button tour;
        private bool collapsed;

        /// <param name="filterState">Which categories are ticked, shared with the pins.</param>
        /// <param name="showNextPlace">
        /// Called when the player asks to be shown the next place. Null leaves the button off the
        /// panel altogether, which is what a test with no camera in it gets: a button that flies
        /// nothing anywhere is worse than no button.
        /// </param>
        public MapLegendPanel(MapFilterState filterState, System.Action showNextPlace = null)
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

                // The box comes first, because it is the part that answers "is this one on?" and a
                // column of boxes down the left edge reads as a list of switches rather than a key.
                var tick = new VisualElement();
                tick.AddToClassList("map-legend__tick");

                var mark = new VisualElement();
                mark.AddToClassList("map-legend__tick-mark");
                mark.pickingMode = PickingMode.Ignore;
                mark.style.width = 14;
                mark.style.height = 14;
                mark.generateVisualContent += context => DrawTick(context, tick);
                tick.Add(mark);

                row.Add(tick);
                ticks[category] = tick;

                // The category's icon, or its plain colour until there is one.
                var swatch = new VisualElement();
                swatch.AddToClassList("map-legend__swatch");
                swatch.style.backgroundColor = MapCategoryPalette.ColourFor(category);

                if (MapIcons.Apply(swatch, MapIcons.For(category)))
                {
                    swatch.AddToClassList("map-legend__swatch--icon");
                }

                row.Add(swatch);

                var label = new Label(Loc.T(MapCategoryPalette.NameKey(category)));
                label.AddToClassList("map-legend__label");
                row.Add(label);

                rows[category] = row;
                body.Add(row);
            }

            // **One way back to the whole map.** With eight boxes to untick, a player who ticked
            // four of them and wants the city back should not have to remember which four.
            clear = new Button(() => state.Clear());
            clear.AddToClassList("map-legend__clear");
            clear.text = Loc.T("map.filters.all");
            body.Add(clear);

            // Under the eight rows, inside the body, so folding the legend away folds this away too.
            // It belongs to the categories above it: it walks whichever of them are ticked.
            if (showNextPlace != null)
            {
                tour = new Button(showNextPlace);
                tour.AddToClassList("map-legend__tour");
                body.Add(tour);
            }

            state.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => state.Changed -= Refresh);

            Refresh();
        }

        /// <summary>
        /// Writes the counter on the SHOW button, e.g. "SHOW 3/22". Does nothing when the panel was
        /// built without one.
        /// </summary>
        public void SetTourCaption(int ordinal, int count)
        {
            if (tour == null)
            {
                return;
            }

            tour.text = Loc.T("map.tour.show", ordinal, count);
            tour.SetEnabled(count > 0);
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
                var picked = state.IsPicked(category);

                row.EnableInClassList("map-legend__row--on", picked);
                row.EnableInClassList("map-legend__row--dim", state.Filtering && !picked);
                ticks[category].EnableInClassList("map-legend__tick--on", picked);
                ticks[category][0].MarkDirtyRepaint();
            }

            // Off rather than hidden: a control that does nothing is worse than one that is plainly
            // not needed yet, and the row keeps the panel's height steady either way.
            clear.SetEnabled(state.Filtering);
        }

        private static void DrawTick(MeshGenerationContext context, VisualElement box)
        {
            if (!box.ClassListContains("map-legend__tick--on"))
            {
                return;
            }

            var area = context.visualElement.contentRect;
            var painter = context.painter2D;

            painter.strokeColor = TickInk;
            painter.lineWidth = 2.4f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(new Vector2(area.width * 0.14f, area.height * 0.52f));
            painter.LineTo(new Vector2(area.width * 0.40f, area.height * 0.78f));
            painter.LineTo(new Vector2(area.width * 0.88f, area.height * 0.24f));
            painter.Stroke();
        }
    }
}
