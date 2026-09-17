using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Counts objects in the city scene by name, for the parts of a placeholder that no swap ever
    /// claimed.
    ///
    /// **A swapped house leaves its roof behind.** `CityAssetSwapper` deletes the box it replaced
    /// but deliberately keeps that box's siblings, because "a house carries its own roof and garage
    /// and deleting the lot would take the parts nothing was swapped for". Only the wall box was
    /// ever described as a <see cref="UI.CityProp"/>, so the roof slab and the garage block survive
    /// the swap and go on sitting exactly where the grey house used to be — over the top of the real
    /// model that replaced it.
    /// </summary>
    public static class LeftoverAudit
    {
        [MenuItem("Scaling Laws/Audit leftover placeholder parts")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Leftover] No City.unity.");
                return;
            }

            var counts = new Dictionary<string, int>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var name = renderer.gameObject.name;
                counts.TryGetValue(name, out var seen);
                counts[name] = seen + 1;
            }

            foreach (var pair in counts.OrderByDescending(entry => entry.Value).Take(24))
            {
                Debug.Log($"[Leftover] {pair.Key,-28} {pair.Value}");
            }

            Debug.Log($"[Leftover] {counts.Count} distinct names, "
                + $"{counts.Values.Sum()} rendered objects in the scene.");
        }
    }
}
