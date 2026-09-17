using System.Collections.Generic;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Removes the parts of a placeholder that a swap orphans.
    ///
    /// **A grey house is a wall box with a roof slab beside it, not one object.**
    /// <see cref="CityAssetSwapper"/> replaces the wall box — the only part described as a
    /// <see cref="CityProp"/> — and deliberately spares that box's siblings, so that a swap never
    /// deletes something nothing was standing in for. The reasoning is sound and the side effect was
    /// not noticed for a while: a real Kenney house has its own roof, so the old slab goes on
    /// hovering over the new model. Same for a lamp's head over a real lamp, a tree's trunk inside a
    /// real tree, and a bridge's railings along a deck that now carries railings of its own.
    /// Reported as big brown slabs lying over the suburbs.
    ///
    /// **Deletes only what is genuinely orphaned.** Each part names the kind it belonged to; if a
    /// placeholder of that kind is still standing next to it, the swap has not happened here and the
    /// part is still the only roof that house has. So this is safe to run on a freshly built scene
    /// as well as a swapped one, and safe to run twice.
    /// </summary>
    public static class CityLeftoverCleanup
    {
        /// <summary>A part left behind, what it belonged to, and how far away that would have been.</summary>
        private readonly struct Orphan
        {
            public Orphan(string name, float radius, params CityPropKind[] parents)
            {
                Name = name;
                Radius = radius;
                Parents = parents;
            }

            public string Name { get; }

            /// <summary>How far from the part its placeholder would sit, if it were still there.</summary>
            public float Radius { get; }

            public CityPropKind[] Parents { get; }
        }

        private static readonly Orphan[] Orphans =
        {
            new("Roof", 4f, CityPropKind.House, CityPropKind.Villa, CityPropKind.FounderHome),
            new("Trunk", 4f, CityPropKind.Tree),
            new("LampHead", 4f, CityPropKind.StreetLamp),

            // A parapet is offset to the edge of its deck, so it stands further from the deck's
            // centre than the other parts do from theirs — half the width of a thirty metre bridge.
            new("Parapet", 20f, CityPropKind.BridgeDeck),

            // The gallery's roof and entrance canopy are siblings of its hall, and the hall is the
            // only part of it described as a prop. Replacing the hall with wings left a 190 metre
            // slab hanging over them. The radius covers the hall's own footprint, so on a scene
            // where the gallery has not been rebuilt these are still its roof and stay put.
            new("GalleryRoof", 130f, CityPropKind.Mall),
            new("GalleryEntrance", 130f, CityPropKind.Mall)
        };

        [MenuItem("Scaling Laws/Clean up leftover placeholder parts")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Cleanup] No City.unity.");
                return;
            }

            // Every surviving placeholder, bucketed by kind, so "is the thing this belonged to still
            // here?" is a short lookup rather than a walk over the whole scene per part.
            var survivors = new Dictionary<CityPropKind, List<Vector3>>();

            foreach (var prop in Object.FindObjectsByType<CityProp>(FindObjectsSortMode.None))
            {
                if (!survivors.TryGetValue(prop.Kind, out var list))
                {
                    list = new List<Vector3>();
                    survivors[prop.Kind] = list;
                }

                list.Add(prop.transform.position);
            }

            var removed = 0;
            var kept = 0;

            foreach (var orphan in Orphans)
            {
                var doomed = new List<GameObject>();

                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (renderer.gameObject.name != orphan.Name)
                    {
                        continue;
                    }

                    if (StillStanding(survivors, orphan, renderer.transform.position))
                    {
                        kept++;
                        continue;
                    }

                    doomed.Add(renderer.gameObject);
                }

                foreach (var part in doomed)
                {
                    Object.DestroyImmediate(part);
                }

                removed += doomed.Count;
                Debug.Log($"[Cleanup] {orphan.Name}: {doomed.Count} removed.");
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Cleanup] {removed} orphaned parts removed, {kept} kept "
                + "(their placeholder is still standing, so they are still doing a job).");
        }

        private static bool StillStanding(Dictionary<CityPropKind, List<Vector3>> survivors,
            Orphan orphan, Vector3 at)
        {
            var squared = orphan.Radius * orphan.Radius;

            foreach (var kind in orphan.Parents)
            {
                if (!survivors.TryGetValue(kind, out var positions))
                {
                    continue;
                }

                foreach (var position in positions)
                {
                    // Horizontal only: a roof sits directly above its walls, and a lamp head above
                    // its post, so height is exactly the axis that must not count here.
                    var dx = position.x - at.x;
                    var dz = position.z - at.z;

                    if (dx * dx + dz * dz <= squared)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
