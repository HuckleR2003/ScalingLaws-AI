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
        private const float DimAmount = 0.78f;

        private static readonly Color DimTarget = new(0.045f, 0.055f, 0.075f);

        private readonly MapFilterState state = new();
        private readonly Dictionary<MapCategory, List<Material>> materialsByCategory = new();
        private readonly Dictionary<Material, Color> originalColours = new();

        private void Start()
        {
            var document = GetComponent<UIDocument>();
            UiBootstrap.Prepare(document.rootVisualElement, null);

            var panel = new MapLegendPanel(state);
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

            CollectMaterials();
            state.Changed += ApplyFilter;
        }

        private void OnDestroy()
        {
            state.Changed -= ApplyFilter;
        }

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
