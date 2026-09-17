using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Finds everything standing over water that has no business being there.
    ///
    /// **Bridges belong over water; a park lawn does not.** Several builders lay their geometry from
    /// a shape — a park's event ground, its paths, a subdivision's streets — and each stops at the
    /// waterline by sampling the terrain at the *middle* of a piece. A piece whose middle is dry and
    /// whose end is not therefore gets drawn, and hangs over the river. That is what the pale wedge
    /// sticking into the water below the Civic Center is.
    ///
    /// This reports rather than deletes: what to do about a given family of objects is a decision,
    /// and some of them (the bridges, the piers, the deliberate riverside power plants) are correct.
    /// </summary>
    public static class CityWaterAudit
    {
        /// <summary>Names that are supposed to be over water, and are not findings.</summary>
        private static readonly string[] Expected =
        {
            "road-bridge", "bridge-pillar", "BridgePier", "Parapet", "Water", "Terrain"
        };

        [MenuItem("Scaling Laws/Audit things standing in water")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Water] No City.unity.");
                return;
            }

            var findings = new Dictionary<string, (int Count, float Deepest)>();
            var expected = 0;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.GetComponent<Terrain>() != null)
                {
                    continue;
                }

                var bounds = renderer.bounds;

                // The ground under the object's own footprint, sampled at its centre and its four
                // corners — an object is only honestly "over water" if the land under it is gone,
                // and a single centre sample is exactly the mistake that put it there.
                var lowest = float.MaxValue;

                foreach (var corner in Corners(bounds))
                {
                    lowest = Mathf.Min(lowest, CityTerrainBuilder.HeightAt(corner.x, corner.y));
                }

                if (lowest >= CityLayout.SeaLevel)
                {
                    continue;
                }

                var name = renderer.gameObject.name;

                if (Expected.Any(name.Contains))
                {
                    expected++;
                    continue;
                }

                findings.TryGetValue(name, out var seen);
                findings[name] = (seen.Count + 1,
                    seen.Count == 0 ? lowest : Mathf.Min(seen.Deepest, lowest));
            }

            foreach (var pair in findings.OrderByDescending(entry => entry.Value.Count))
            {
                Debug.Log($"[Water] {pair.Key,-26} {pair.Value.Count,5}  "
                    + $"lowest ground under one of them: {pair.Value.Deepest:0.0}m "
                    + $"(waterline {CityLayout.SeaLevel:0}m)");
            }

            Debug.Log($"[Water] {findings.Values.Sum(v => v.Count)} objects over water that should not be, "
                + $"in {findings.Count} families. {expected} bridge parts ignored as correct.");
        }

        private static IEnumerable<Vector2> Corners(Bounds bounds)
        {
            yield return new Vector2(bounds.center.x, bounds.center.z);
            yield return new Vector2(bounds.min.x, bounds.min.z);
            yield return new Vector2(bounds.min.x, bounds.max.z);
            yield return new Vector2(bounds.max.x, bounds.min.z);
            yield return new Vector2(bounds.max.x, bounds.max.z);
        }
    }
}
