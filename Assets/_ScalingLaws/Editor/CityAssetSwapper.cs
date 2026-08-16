using System.Collections.Generic;
using System.Linq;
using ScalingLaws.UI;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Swaps the grey boxes in the city for real assets.
    ///
    /// **This is the whole reason every placed box carries a <see cref="CityProp"/>.** A box is not
    /// a placeholder to be deleted and redone — it is a surveyed position with a footprint, a facing
    /// and a name, so bringing in a house pack is a transform copy rather than a redesign. Drop the
    /// prefabs into the list, press the button, and four hundred and forty-eight houses become four
    /// hundred and forty-eight houses.
    ///
    /// Two rules make this safe to run against a pack nobody has checked:
    ///
    /// It **scales to the surveyed footprint** rather than trusting the asset's own size. An asset
    /// authored at ten times scale, or in inches, is the normal case rather than the exception, and
    /// a suburb where the houses overlap their own driveways is what happens when nobody checks.
    ///
    /// It **reports the fit** rather than only doing it. A model that had to be squashed to sixty
    /// per cent to fit its plot is a model that will look wrong, and the console says so with a
    /// count per kind, so the pack can be judged before the scene is saved.
    /// </summary>
    public sealed class CityAssetSwapper : EditorWindow
    {
        /// <summary>Beyond this much scaling, the fit is reported as bad rather than merely done.</summary>
        public const float BadFit = 0.35f;

        [SerializeField] private List<CityPropKind> kinds = new();
        [SerializeField] private List<GameObject> prefabs = new();

        [SerializeField] private bool matchFootprint = true;
        [SerializeField] private bool keepTheBox;

        private Vector2 scroll;

        [MenuItem("Scaling Laws/Swap city assets")]
        public static void Open()
        {
            var window = GetWindow<CityAssetSwapper>(true, "Swap city assets");
            window.minSize = new Vector2(430f, 380f);

            if (window.kinds.Count == 0)
            {
                // The kinds worth swapping first, in the order somebody would actually do it.
                foreach (var kind in new[]
                {
                    CityPropKind.House, CityPropKind.Villa, CityPropKind.Garage,
                    CityPropKind.Tree, CityPropKind.StreetLamp, CityPropKind.FounderHome
                })
                {
                    window.kinds.Add(kind);
                    window.prefabs.Add(null);
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Every box the city builder placed knows what it is and how big its space is. Put a "
                + "prefab against a kind and press Swap; the prefab is dropped onto each box with "
                + "the same position and facing, and scaled to the surveyed footprint.",
                MessageType.Info);

            matchFootprint = EditorGUILayout.Toggle(
                new GUIContent("Scale to footprint",
                    "Off only if the pack is already authored at the right size. It usually is not."),
                matchFootprint);

            keepTheBox = EditorGUILayout.Toggle(
                new GUIContent("Keep the grey box",
                    "Leaves the box under the asset, so a bad fit is obvious. Handy on a first pass."),
                keepTheBox);

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (var index = 0; index < kinds.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();

                kinds[index] = (CityPropKind)EditorGUILayout.EnumPopup(kinds[index],
                    GUILayout.Width(150f));

                prefabs[index] = (GameObject)EditorGUILayout.ObjectField(prefabs[index],
                    typeof(GameObject), false);

                if (GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    kinds.RemoveAt(index);
                    prefabs.RemoveAt(index);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add a row"))
            {
                kinds.Add(CityPropKind.None);
                prefabs.Add(null);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Swap", GUILayout.Height(32f)))
            {
                Swap();
            }
        }

        private void Swap()
        {
            var mapping = new Dictionary<CityPropKind, GameObject>();

            for (var index = 0; index < kinds.Count; index++)
            {
                if (kinds[index] != CityPropKind.None && prefabs[index] != null)
                {
                    mapping[kinds[index]] = prefabs[index];
                }
            }

            if (mapping.Count == 0)
            {
                Debug.LogWarning("[Scaling Laws] Nothing to swap: no prefab is set against a kind.");
                return;
            }

            var props = FindObjectsByType<CityProp>(FindObjectsSortMode.None);
            var swapped = new Dictionary<CityPropKind, int>();
            var poorFits = new Dictionary<CityPropKind, int>();

            foreach (var prop in props)
            {
                if (!mapping.TryGetValue(prop.Kind, out var prefab))
                {
                    continue;
                }

                var box = prop.transform;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, box.parent);
                instance.transform.SetPositionAndRotation(box.position, box.rotation);

                if (matchFootprint)
                {
                    var fit = FitTo(instance, prop.Footprint);

                    if (fit < BadFit || fit > 1f / BadFit)
                    {
                        poorFits.TryGetValue(prop.Kind, out var count);
                        poorFits[prop.Kind] = count + 1;
                    }
                }

                swapped.TryGetValue(prop.Kind, out var done);
                swapped[prop.Kind] = done + 1;

                if (keepTheBox)
                {
                    continue;
                }

                // The box goes, but its children do not: a house carries its own roof and garage,
                // and deleting the lot would take the parts nothing was swapped for.
                foreach (Transform child in box)
                {
                    child.SetParent(box.parent, true);
                }

                DestroyImmediate(box.gameObject);
            }

            if (swapped.Count == 0)
            {
                Debug.LogWarning("[Scaling Laws] Nothing matched. Is the city scene open?");
                return;
            }

            foreach (var pair in swapped.OrderByDescending(entry => entry.Value))
            {
                poorFits.TryGetValue(pair.Key, out var bad);

                var note = bad > 0
                    ? $"  ({bad} needed heavy scaling — check the pack's units)"
                    : string.Empty;

                Debug.Log($"[Scaling Laws] {pair.Key}: {pair.Value} swapped.{note}");
            }
        }

        /// <summary>
        /// Scales an instance so its bounds fill the surveyed footprint.
        ///
        /// Returns the factor used, so the caller can say when a pack is authored at a wildly
        /// different scale. Uniform rather than per-axis: squashing a model to a plot's exact
        /// proportions is how a house ends up looking like it was sat on.
        /// </summary>
        private static float FitTo(GameObject instance, Vector3 footprint)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return 1f;
            }

            var bounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            if (bounds.size.x <= 0.001f || bounds.size.z <= 0.001f)
            {
                return 1f;
            }

            // The horizontal fit decides it. Height follows, because a house that is the right
            // width and a storey too tall reads far better than one squashed to the plot.
            var factor = Mathf.Min(footprint.x / bounds.size.x, footprint.z / bounds.size.z);

            instance.transform.localScale *= factor;

            // Sit it on the ground.
            //
            // The surveyed position is the middle of the plot at ground level, and an asset's pivot
            // is anybody's guess — feet, centre, or the corner of whatever the artist started with.
            // Measuring the scaled bounds and lifting by however far the bottom is below the plot
            // is the only way that works for a pack nobody has inspected.
            var scaled = instance.GetComponentsInChildren<Renderer>();
            var sat = scaled[0].bounds;

            for (var index = 1; index < scaled.Length; index++)
            {
                sat.Encapsulate(scaled[index].bounds);
            }

            instance.transform.position += Vector3.up * (instance.transform.position.y - sat.min.y);

            return factor;
        }
    }
}
