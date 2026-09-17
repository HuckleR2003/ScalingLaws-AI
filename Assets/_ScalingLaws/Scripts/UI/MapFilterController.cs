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
