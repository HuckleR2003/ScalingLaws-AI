using System;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Quick travel: one rectangular tile per district, stacked in the top-left corner, each
    /// coloured like its row in <see cref="MapLegendPanel"/> so the two never disagree about what a
    /// colour means. Clicking a tile hands its ground centre to whatever the caller wants done with
    /// it — <see cref="CityMapController.FlyTo"/> in practice — rather than reaching for the camera
    /// itself, the same separation <see cref="MapFilterController"/> keeps between the panel and
    /// the pins it dims.
    ///
    /// The blurb every <see cref="DistrictDefinition"/> already carries is the tile's
    /// <see cref="InsightTip"/> rather than printed inline: eight tiles of a sentence each would be
    /// the tallest thing on the screen, and a hover is the same information a click away.
    /// </summary>
    public sealed class MapDistrictPanel : VisualElement
    {
        public MapDistrictPanel(Action<DistrictDefinition> onPicked)
        {
            AddToClassList("map-districts");

            var title = new Label(Loc.T("map.districts.title"));
            title.AddToClassList("map-districts__title");
            Add(title);

            foreach (var district in CityLayout.Districts)
            {
                var captured = district;

                var tile = new Button(() => onPicked(captured));
                tile.AddToClassList("map-districts__tile");

                var accent = new VisualElement();
                accent.AddToClassList("map-districts__accent");
                accent.style.backgroundColor = MapCategoryPalette.ColourFor(district.Category);
                tile.Add(accent);

                var icon = new VisualElement();
                icon.AddToClassList("map-districts__icon");
                icon.pickingMode = PickingMode.Ignore;

                if (MapIcons.Apply(icon, MapIcons.For(district.Category)))
                {
                    tile.Add(icon);
                }

                var label = new Label(district.DisplayName);
                label.AddToClassList("map-districts__label");
                tile.Add(label);

                InsightTip.Attach(tile, district.DisplayName, district.Blurb,
                    InsightTip.Placement.Above);

                Add(tile);
            }
        }
    }
}
