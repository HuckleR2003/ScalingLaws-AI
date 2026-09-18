using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Wires <see cref="MapLegendPanel"/> onto a live <see cref="UIDocument"/> and dims every pin
    /// whose category is not the one picked.
    ///
    /// **Dims the eight shared materials, not the twenty-six pins.** Every pin of one category
    /// shares one material — the same object <see cref="MapCategoryPalette"/> was written to keep
    /// from drifting into two colours — so picking a category is eight colour writes rather than a
    /// walk over every renderer in the scene, and every pin sharing that material updates in the
    /// same frame because it is the same object.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapFilterController : MonoBehaviour
    {
        /// <summary>How far towards near-black a dimmed material's colour is pulled.</summary>
        private const float DimAmount = 0.9f;

        private static readonly Color DimTarget = new(0.045f, 0.055f, 0.075f);

        /// <summary>
        /// How high the camera ends up when the SHOW button takes the player to a place. Low enough
        /// that a single building fills a useful part of the frame, high enough to keep its street
        /// and its neighbours in shot, so the answer is "here, and this is what is around it".
        /// </summary>
        private const float TourHeight = 440f;

        private readonly MapFilterState state = new();
        private readonly MapTour tour = new();
        private readonly Dictionary<MapCategory, List<Material>> materialsByCategory = new();
        private readonly Dictionary<Material, Color> originalColours = new();
        private readonly Dictionary<string, MapSitePin> pinsById = new();

        private MapLegendPanel legend;
        private CityMapController mapView;
        private MapSiteSelection siteSelection;

        private void Start()
        {
            var document = GetComponent<UIDocument>();
            UiBootstrap.Prepare(document.rootVisualElement, null);

            var panel = new MapLegendPanel(state, ShowNextPlace);
            legend = panel;
            panel.style.position = Position.Absolute;
            panel.style.top = 16;
            panel.style.right = 16;

            document.rootVisualElement.Add(panel);

            // Not this component's concern in the strict sense — it owns the legend, not the
            // camera — but this is the one UIDocument the city scene has, and a control scheme
            // with no on-screen trace of itself is not discoverable. One label costs less than a
            // second overlay.
            var hint = new Label(Loc.T("map.controls.hint"));
            hint.AddToClassList("map-controls-hint");
            document.rootVisualElement.Add(hint);

            // Same reasoning as the hint above: one UIDocument, so the quick-travel panel lives
            // here too rather than starting a third overlay object for it.
            var mapCamera = FindFirstObjectByType<CityMapController>();
            mapView = mapCamera;

            if (mapCamera != null)
            {
                var districts = new MapDistrictPanel(district =>
                    mapCamera.FlyTo(new Vector2(district.CentreX, district.CentreZ), district.GroundHeight));
                districts.style.position = Position.Absolute;
                districts.style.top = 16;
                districts.style.left = 16;

                document.rootVisualElement.Add(districts);
            }

            // The card, and the thing that drives it. Built here for the same reason the district
            // panel is: this is the city scene's one UIDocument, and a second overlay object to
            // hold one panel would be a second thing to keep in step.
            var selection = mapCamera == null
                ? null
                : mapCamera.GetComponent<MapSiteSelection>()
                  ?? mapCamera.gameObject.AddComponent<MapSiteSelection>();

            var card = new MapSiteCard(() => selection?.Deselect());
            card.style.position = Position.Absolute;
            card.style.right = 16;
            card.style.bottom = 16;

            document.rootVisualElement.Add(card);

            selection?.Use(card);
            siteSelection = selection;

            CollectMaterials();

            // **The part that made ticking a category visible.** See `MapSiteBeacons`: dimming the other
            // pins was not enough to find anything from the height this map is read at.
            if (mapCamera != null)
            {
                var beacons = mapCamera.GetComponent<MapSiteBeacons>()
                              ?? mapCamera.gameObject.AddComponent<MapSiteBeacons>();

                beacons.Use(state, pinsById.Values);
            }
            state.Changed += ApplyFilter;
            state.Changed += RefocusTour;

            legend.SetTourCaption(tour.NextOrdinal, tour.Count);
        }

        private void OnDestroy()
        {
            state.Changed -= ApplyFilter;
            state.Changed -= RefocusTour;
        }

        private void Update()
        {
            // The walk through the places goes back to the first one when the player leaves it
            // alone, and the button is the only thing that shows where it had got to.
            if (tour.Tick(Time.unscaledDeltaTime))
            {
                legend.SetTourCaption(tour.NextOrdinal, tour.Count);
            }
        }

        /// <summary>
        /// The SHOW button: fly to the next place in the walk, open its card, and move the counter
        /// on. The card as well as the flight, because a building the player has never seen before
        /// arriving in the middle of the screen does not say what it is.
        /// </summary>
        private void ShowNextPlace()
        {
            var stop = tour.Show();

            legend.SetTourCaption(tour.NextOrdinal, tour.Count);

            if (stop == null || mapView == null)
            {
                return;
            }

            mapView.FlyTo(new Vector2(stop.Position.X, stop.Position.Z),
                GroundHeightFor(stop), TourHeight);

            if (pinsById.TryGetValue(stop.Id, out var pin))
            {
                siteSelection?.Select(pin);
            }
        }

        private void RefocusTour()
        {
            if (tour.FocusMany(state.Picked))
            {
                legend.SetTourCaption(tour.NextOrdinal, tour.Count);
            }
        }

        /// <summary>
        /// How high the ground is under a place: its district's levelled height, the same reading
        /// the quick-travel panel aims a district jump with. Zero for a place whose district has
        /// gone, which is a broken catalog rather than something to aim around.
        /// </summary>
        private static float GroundHeightFor(MapSiteDefinition site) =>
            CityLayout.DistrictById(site.DistrictId)?.GroundHeight ?? 0f;

        /// <summary>
        /// Finds every <see cref="MapSitePin"/>, and for each, every material its renderers use —
        /// the pin itself and, where the site has one, its footprint ring — captured once so
        /// dimming and undimming are always relative to what a colour actually was, not to a
        /// category's raw palette entry (the footprint ring is deliberately dimmer than its pin to
        /// start with, and re-deriving both from the same palette colour would erase that).
        /// </summary>
        private void CollectMaterials()
        {
            foreach (var pin in FindObjectsByType<MapSitePin>(FindObjectsSortMode.None))
            {
                // The same walk answers the other question the map asks of the pins: which object
                // in the scene is the place the catalog calls `cars.port`. The SHOW button needs it
                // and there is no reason to walk every pin twice to find out.
                if (!string.IsNullOrEmpty(pin.SiteId))
                {
                    pinsById[pin.SiteId] = pin;
                }

                if (!materialsByCategory.TryGetValue(pin.Category, out var list))
                {
                    list = new List<Material>();
                    materialsByCategory[pin.Category] = list;
                }

                foreach (var renderer in pin.GetComponentsInChildren<MeshRenderer>())
                {
                    var material = renderer.sharedMaterial;

                    if (material == null || originalColours.ContainsKey(material))
                    {
                        continue;
                    }

                    list.Add(material);
                    originalColours[material] = ReadColour(material);
                }
            }
        }

        private void ApplyFilter()
        {
            foreach (var (category, materials) in materialsByCategory)
            {
                var emphasised = state.IsEmphasised(category);

                foreach (var material in materials)
                {
                    var original = originalColours[material];
                    var colour = emphasised ? original : Color.Lerp(original, DimTarget, DimAmount);
                    WriteColour(material, colour);
                }
            }
        }

        private static Color ReadColour(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        }

        private static void WriteColour(Material material, Color colour)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", colour);
            }
        }
    }
}
