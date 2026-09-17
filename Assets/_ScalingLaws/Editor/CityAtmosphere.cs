using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Plants the woods the roads between districts run through.
    ///
    /// **The long drives are the emptiest thing on this map.** Bayview's districts are dense now and
    /// the ground between them is mown grass for a kilometre at a stretch — which is what makes the
    /// arterial roads read as lines on a diagram rather than as a road going somewhere. Trees along
    /// them cost nothing to place, nothing to maintain, and do more for how the map feels than any
    /// building would.
    ///
    /// **Thinned towards the towns and thickened away from them.** A wood that stops dead at a
    /// district boundary looks drawn; one that thins out as the houses start looks like a town that
    /// grew into it. The density falls off inside a district's own radius.
    /// </summary>
    public static class CityAtmosphere
    {
        private const string Suburban =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";

        /// <summary>Metres along a road between one rank of trees and the next.</summary>
        private const float Spacing = 13f;

        /// <summary>Nearest a trunk stands to the middle of the carriageway.</summary>
        private const float NearVerge = 22f;

        /// <summary>Furthest out the wood reaches from the road.</summary>
        private const float FarVerge = 95f;

        /// <summary>Trees are small; this is all the room one needs to itself.</summary>
        private const float TreeRoom = 7f;

        [MenuItem("Scaling Laws/Plant woods along the roads")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Woods] No City.unity.");
                return;
            }

            var models = new List<GameObject>();
            foreach (var name in new[] { "tree-large", "tree-small" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Suburban + name + ".fbx");

                if (model != null)
                {
                    models.Add(model);
                }
            }

            if (models.Count == 0)
            {
                Debug.LogError("[Woods] No tree models.");
                return;
            }

            var existing = GameObject.Find("Woods");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var cityRoot = GameObject.Find("City");
            var root = new GameObject("Woods").transform;
            if (cityRoot != null)
            {
                root.SetParent(cityRoot.transform, false);
            }

            var taken = Occupied();
            var random = new System.Random(20260918);
            var planted = 0;

            var centrelines = CityTerrainBuilder.RoadCentrelines();
            var roads = CityLayout.Roads;

            for (var index = 0; index < roads.Count; index++)
            {
                if (roads[index].Class == RoadClass.Lane)
                {
                    continue;
                }

                planted += PlantAlong(centrelines[index].Points, root, models, taken, random);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Woods] {planted} trees planted along {roads.Count} roads.");
        }

        private static int PlantAlong(IReadOnlyList<Vector2> points, Transform root,
            IReadOnlyList<GameObject> models, HashSet<(int, int)> taken, System.Random random)
        {
            var planted = 0;
            var travelled = 0f;
            var next = Spacing;

            for (var index = 0; index < points.Count - 1; index++)
            {
                var a = points[index];
                var b = points[index + 1];
                var run = Vector2.Distance(a, b);

                if (run < 0.01f)
                {
                    continue;
                }

                var direction = (b - a) / run;
                var side = new Vector2(-direction.y, direction.x);

                while (travelled + run >= next)
                {
                    var at = Vector2.Lerp(a, b, (next - travelled) / run);

                    // Several ranks each side, a different number each time and each jittered along
                    // the road, so the edge of the wood is ragged rather than a hedge.
                    foreach (var hand in new[] { -1f, 1f })
                    {
                        var ranks = random.Next(3, 7);

                        for (var rank = 0; rank < ranks; rank++)
                        {
                            var out1 = NearVerge + (FarVerge - NearVerge)
                                * (float)random.NextDouble();

                            var wobble = ((float)random.NextDouble() - 0.5f) * Spacing;
                            var spot = at + side * (hand * out1) + direction * wobble;

                            if (TryPlant(spot, root, models, taken, random))
                            {
                                planted++;
                            }
                        }
                    }

                    next += Spacing;
                }

                travelled += run;
            }

            return planted;
        }

        private static bool TryPlant(Vector2 at, Transform root, IReadOnlyList<GameObject> models,
            HashSet<(int, int)> taken, System.Random random)
        {
            var height = CityTerrainBuilder.HeightAt(at.x, at.y);

            if (height < CityLayout.SeaLevel + 2f)
            {
                return false;
            }

            // Thinner where a town has already grown: full density out in open country, falling to
            // nothing by the middle of a district.
            var chance = 1f;

            foreach (var district in CityLayout.Districts)
            {
                var reach = Vector2.Distance(at, new Vector2(district.CentreX, district.CentreZ));

                if (reach < district.Radius)
                {
                    chance = Mathf.Min(chance, reach / district.Radius);
                }
            }

            if ((float)random.NextDouble() > chance)
            {
                return false;
            }

            var key = Key(at);

            if (taken.Contains(key))
            {
                return false;
            }

            taken.Add(key);

            var model = models[random.Next(models.Count)];
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(model, root);

            tree.transform.position = new Vector3(at.x, height, at.y);
            tree.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

            // Trees are the one thing on this map that should not all be the same size.
            var scale = 14f + (float)random.NextDouble() * 12f;
            tree.transform.localScale = Vector3.one * scale;

            return true;
        }

        /// <summary>
        /// The ground already spoken for, as a coarse grid of taken cells.
        ///
        /// A tree needs so little room that a cell the size of one is accurate enough, and a set of
        /// cells is far cheaper to ask than a list of rectangles when the answer is wanted tens of
        /// thousands of times.
        /// </summary>
        private static HashSet<(int, int)> Occupied()
        {
            var taken = new HashSet<(int, int)>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.GetComponent<Terrain>() != null)
                {
                    continue;
                }

                var bounds = renderer.bounds;

                // **Ground paint is not an obstacle.** The bay's water plane, a park lawn, the
                // gallery's parking apron and the flat rings under the map pins are each hundreds of
                // metres across and a few centimetres thick. Counting them filled every cell on the
                // map and the first run of this planted exactly nothing.
                if (bounds.size.y < 0.6f && Mathf.Max(bounds.size.x, bounds.size.z) > 60f)
                {
                    continue;
                }

                for (var x = bounds.min.x; x <= bounds.max.x + TreeRoom; x += TreeRoom)
                {
                    for (var z = bounds.min.z; z <= bounds.max.z + TreeRoom; z += TreeRoom)
                    {
                        taken.Add(Key(new Vector2(x, z)));
                    }
                }
            }

            return taken;
        }

        private static (int, int) Key(Vector2 at) =>
            (Mathf.FloorToInt(at.x / TreeRoom), Mathf.FloorToInt(at.y / TreeRoom));
    }
}
