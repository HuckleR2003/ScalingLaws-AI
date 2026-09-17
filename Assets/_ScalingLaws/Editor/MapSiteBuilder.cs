using System;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Stands a coloured pin, and where it has one, a footprint ring, for every entry in
    /// <see cref="MapSiteCatalog"/>.
    ///
    /// **A pin, not a building.** Etap 4 of Docs/CITY_MAP_PLAN.md is the catalog; drawing real
    /// buildings on these 25 positions is later work, and guessing at their shape now would be
    /// exactly the mistake CityDressingBuilder's own notes warn against — houses sprinkled before
    /// the streets that should have decided where they go. What a pin buys today is honest: it
    /// proves a position is where the catalog says it is and lets two pins that are too close to
    /// each other actually be seen doing it, which <see cref="MapSiteCatalogTests"/> cannot check
    /// from numbers alone.
    ///
    /// Colour follows <see cref="MapCategoryPalette"/>, the one place the eight map colours are
    /// defined — the legend's chips and a rival's compute district read the same green because
    /// they read the same method, not two copies of the same eight numbers.
    /// </summary>
    public static class MapSiteBuilder
    {
        /// <summary>Base pin height, in metres. Roughly the FounderHome pin CityDressingBuilder already draws.</summary>
        private const float BasePinHeight = 13f;

        /// <summary>Extra metres per tier, so a higher rung on a site's own ladder stands taller.</summary>
        private const float TierStep = 2.4f;

        private const float PinWidth = 1.8f;
        private const float FootprintThickness = 0.22f;

        public static void Build(Transform root, Func<float, float, float> ground)
        {
            var group = new GameObject("MapSites").transform;
            group.SetParent(root, true);

            foreach (var site in MapSiteCatalog.All)
            {
                var height = ground(site.Position.X, site.Position.Z);
                var colour = MapCategoryPalette.ColourFor(site.Category);

                var pinHeight = BasePinHeight + Mathf.Clamp(site.Tier, 0, 5) * TierStep;

                var site3d = new GameObject(site.DisplayName).transform;
                site3d.SetParent(group, true);
                site3d.position = new Vector3(site.Position.X, height, site.Position.Z);

                site3d.gameObject.AddComponent<MapSitePin>().Describe(site.Category, site.Kind);

                var pin = CityDressingBuilder.Box(site3d, "Pin",
                    new Vector3(site.Position.X, height + pinHeight * 0.5f, site.Position.Z),
                    new Vector3(PinWidth, pinHeight, PinWidth),
                    CityDressingBuilder.Paint($"MapSite_{site.Category}", colour, 0.25f));

                CityDressingBuilder.Describe(pin, CityPropKind.SiteMarker,
                    new Vector3(PinWidth, pinHeight, PinWidth), site.DistrictId, (int)site.Kind);

                // A dimmer marker head, so a pin reads from above (the plan shot) and not only from
                // the side (the flight). Same idea as CityDressingBuilder's FounderPin, smaller.
                CityDressingBuilder.Box(site3d, "PinHead",
                    new Vector3(site.Position.X, height + pinHeight + 1.1f, site.Position.Z),
                    new Vector3(PinWidth * 2.4f, 1.6f, PinWidth * 2.4f),
                    CityDressingBuilder.Paint($"MapSite_{site.Category}", colour, 0.25f));

                if (site.Radius > 0f)
                {
                    var ring = CityDressingBuilder.Cylinder(site3d, "Footprint",
                        new Vector2(site.Position.X, site.Position.Z),
                        site.Radius * 2f, FootprintThickness,
                        CityDressingBuilder.Paint($"MapSiteGround_{site.Category}", Dim(colour), 0.05f));

                    CityDressingBuilder.Describe(ring, CityPropKind.SiteMarker,
                        new Vector3(site.Radius * 2f, FootprintThickness, site.Radius * 2f),
                        site.DistrictId, (int)site.Kind);
                }
            }

            Debug.Log($"[Scaling Laws] {MapSiteCatalog.All.Count} map site pins placed.");
        }

        /// <summary>A footprint ring reads better dimmer than its pin, or it competes with the pin over it.</summary>
        private static Color Dim(Color colour) => Color.Lerp(colour, new Color(0.05f, 0.06f, 0.08f), 0.55f);
    }
}
