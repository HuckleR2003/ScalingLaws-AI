using System.Collections.Generic;
using System.Linq;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Counts what is still a grey box in `City.unity`, by kind, with the footprint range of each.
    ///
    /// **The list of what is left to do, read off the scene rather than remembered.** Every
    /// placeholder carries a <see cref="CityProp"/> describing what it stands for and how big it is;
    /// the ones a swap or a tiling pass has already replaced are gone. So whatever this still finds
    /// is, by definition, the work not yet done — and the footprints tell you which tool the job
    /// wants before anybody opens the scene to look.
    /// </summary>
    public static class CityPropAudit
    {
        [MenuItem("Scaling Laws/Audit remaining grey boxes")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Audit] No City.unity.");
                return;
            }

            var byKind = new Dictionary<CityPropKind, List<CityProp>>();

            foreach (var prop in Object.FindObjectsByType<CityProp>(FindObjectsSortMode.None))
            {
                if (!byKind.TryGetValue(prop.Kind, out var list))
                {
                    list = new List<CityProp>();
                    byKind[prop.Kind] = list;
                }

                list.Add(prop);
            }

            var total = 0;

            foreach (var pair in byKind.OrderByDescending(entry => entry.Value.Count))
            {
                var footprints = pair.Value.Select(p => p.Footprint).ToList();
                var widths = footprints.Select(f => f.x).ToList();
                var lengths = footprints.Select(f => f.z).ToList();

                total += pair.Value.Count;

                Debug.Log($"[Audit] {pair.Key}: {pair.Value.Count}  "
                    + $"width {widths.Min():0.#}-{widths.Max():0.#}m, "
                    + $"length {lengths.Min():0.#}-{lengths.Max():0.#}m");
            }

            Debug.Log($"[Audit] TOTAL still grey: {total}");
        }
    }
}
