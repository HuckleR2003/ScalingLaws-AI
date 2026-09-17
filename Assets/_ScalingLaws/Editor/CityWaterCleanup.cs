using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Removes the carriageway, kerbs and street furniture that were laid across open water.
    ///
    /// **Roads are tiled from a centreline that does not know where the river is.** A road run
    /// crosses the bay because the town does; the crossing is a <see cref="CityLayout.Bridges"/>
    /// span drawn separately, with its own deck. The tiling pass laid ordinary tarmac along the
    /// centreline the whole way regardless, so between the banks there is a strip of road hanging at
    /// terrain height — under the bridge, or over nothing at all where no bridge was ever specified.
    /// In the Waterfront that reads as roads flying over the harbour and stopping in mid-air.
    ///
    /// **Only small pieces, and only by their centre.** A park's lawn is one big mesh whose corner
    /// may reach the shore, and deleting the lawn because of its corner would be a worse bug than
    /// the one being fixed. A road tile whose *middle* is over water is unambiguously flying.
    /// </summary>
    public static class CityWaterCleanup
    {
        /// <summary>
        /// Names that may be removed when they are over water. Everything else is left alone —
        /// bridges, piers, jetties, the park's own lake, and the two riverside power plants that
        /// are in the water on purpose.
        /// </summary>
        private static readonly string[] Removable =
        {
            "road-straight", "road-crossroad", "road-crossing", "Sidewalk",
            "traffic-light", "light-square", "light-curved", "driveway-long", "path-long"
        };

        /// <summary>Anything bigger than this across is a landform, not a piece of street.</summary>
        private const float PieceLimit = 60f;

        [MenuItem("Scaling Laws/Clear roads laid over water")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[WaterFix] No City.unity.");
                return;
            }

            var doomed = new List<GameObject>();
            var byName = new Dictionary<string, int>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var name = renderer.gameObject.name;

                if (!Removable.Any(name.Contains))
                {
                    continue;
                }

                var bounds = renderer.bounds;

                if (Mathf.Max(bounds.size.x, bounds.size.z) > PieceLimit)
                {
                    continue;
                }

                var ground = CityTerrainBuilder.HeightAt(bounds.center.x, bounds.center.z);

                // The same margin the builders themselves use for "this is the water's edge, do not
                // lay anything here" (`CityDressingBuilder.StreetStrip`). Ground sitting exactly at
                // the waterline is under the water plane as far as the eye is concerned, and the
                // first run of this pass left a road crossing the harbour on that technicality.
                if (ground >= CityLayout.SeaLevel + 1.5f)
                {
                    continue;
                }

                var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject)
                               ?? renderer.gameObject;

                if (doomed.Contains(instance))
                {
                    continue;
                }

                doomed.Add(instance);

                byName.TryGetValue(name, out var seen);
                byName[name] = seen + 1;
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
            }

            foreach (var pair in byName.OrderByDescending(entry => entry.Value))
            {
                Debug.Log($"[WaterFix] {pair.Key,-22} {pair.Value} removed from over the water.");
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[WaterFix] {doomed.Count} pieces of street removed from open water.");
        }
    }
}
