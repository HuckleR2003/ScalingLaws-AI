using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The terrace park above Innovation District: three long reflecting pools on a levelled shelf
    /// cut into the hill, a pavilion at one end, a fountain at the other, and steps down to the
    /// district's streets.
    ///
    /// **Why this, and why here.** The author liked the Silicon Valley plaza — the long pool between
    /// lawns — and asked for a bigger park like it on a hill, with the ground straightened for it, as
    /// a place for events. North of Innovation the district's 72 m pad meets a ridge that climbs to
    /// 113 m within 160 m and holds nothing; a shelf at 92 m there sits twenty metres over the
    /// district with the bay behind it. It is inside Innovation's own radius, so the map site belongs
    /// to a district the way every other one does.
    ///
    /// Run it after anything that moves the ground here. It clears its own group first.
    /// </summary>
    public static class CityHillPark
    {
        private const string GroupName = "HillPark";

        /// <summary>The shelf: centre, size and the height it is levelled to.</summary>
        private static readonly Vector2 Centre = new(1570f, 1675f);
        private static readonly Vector2 Size = new(220f, 130f);
        private const float Level = 92f;

        /// <summary>How far past the shelf the ground blends back into the hill.</summary>
        private const float Blend = 42f;

        private const float PoolLength = 150f;
        private const float PoolWidth = 10f;
        private static readonly float[] PoolRows = { -30f, 0f, 30f };
        private static readonly float[] WalkRows = { -45f, -15f, 15f, 45f };

        private static Material Water => CityDressingBuilder.Paint("SvWater", new Color(0.18f, 0.42f, 0.56f), 0.95f, 0.1f);
        private static Material StoneDark => CityDressingBuilder.Paint("ParkStoneDark", new Color(0.40f, 0.39f, 0.37f), 0.15f);
        private static Material Stone => CityDressingBuilder.Paint("ParkStone", new Color(0.72f, 0.70f, 0.66f), 0.12f);
        private static Material White => CityDressingBuilder.Paint("ParkWhite", new Color(0.92f, 0.92f, 0.90f), 0.3f);
        private static Material Champagne => CityDressingBuilder.Paint("ParkChampagne", new Color(0.80f, 0.74f, 0.62f), 0.5f, 0.4f);
        private static Material Timber => CityDressingBuilder.Paint("ParkTimber", new Color(0.46f, 0.33f, 0.22f), 0.2f);

        private const string TreeModel =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/tree-large.fbx";

        [MenuItem("Scaling Laws/Hill park/Build the terrace park")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity", OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Hill park] No city scene.");
                return;
            }

            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LevelShelf(terrains);

            var city = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "City");
            var existing = city == null ? null : city.transform.Find(GroupName);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var park = new GameObject(GroupName).transform;
            park.SetParent(city == null ? null : city.transform, false);

            // Three pools, each with a darker rim, laid along the shelf's long side.
            foreach (var row in PoolRows)
            {
                var z = Centre.y + row;
                CityDressingBuilder.Box(park, "PoolRim", new Vector3(Centre.x, Level + 0.3f, z),
                    new Vector3(PoolLength + 2f, 0.6f, PoolWidth + 2f), StoneDark);
                CityDressingBuilder.Box(park, "Pool", new Vector3(Centre.x, Level + 0.45f, z),
                    new Vector3(PoolLength, 0.6f, PoolWidth), Water);
            }

            // The walks between and outside the pools, with benches facing the water.
            foreach (var row in WalkRows)
            {
                var z = Centre.y + row;
                CityDressingBuilder.Box(park, "Walk", new Vector3(Centre.x, Level + 0.06f, z),
                    new Vector3(PoolLength + 24f, 0.12f, 7f), Stone);

                for (var x = Centre.x - PoolLength * 0.5f + 10f; x < Centre.x + PoolLength * 0.5f; x += 22f)
                {
                    CityDressingBuilder.Box(park, "Bench", new Vector3(x, Level + 0.45f, z + (row < 0 ? 2.8f : -2.8f)),
                        new Vector3(3f, 0.8f, 0.9f), Timber);
                }
            }

            // A cross-walk at each end tying the rows together.
            foreach (var end in new[] { -1f, 1f })
            {
                CityDressingBuilder.Box(park, "EndWalk",
                    new Vector3(Centre.x + end * (PoolLength * 0.5f + 12f), Level + 0.06f, Centre.y),
                    new Vector3(8f, 0.12f, 98f), Stone);
            }

            // The pavilion at the west end: where a stage or a marquee would go.
            var west = Centre.x - PoolLength * 0.5f - 30f;
            CityDressingBuilder.Box(park, "PavilionFloor", new Vector3(west, Level + 0.4f, Centre.y),
                new Vector3(24f, 0.8f, 40f), Stone);
            CityDressingBuilder.Box(park, "Pavilion", new Vector3(west - 4f, Level + 4.8f, Centre.y),
                new Vector3(12f, 8f, 30f), White);
            CityDressingBuilder.Box(park, "PavilionRoof", new Vector3(west, Level + 9.2f, Centre.y),
                new Vector3(26f, 0.9f, 42f), Champagne);

            foreach (var side in new[] { -1f, 1f })
            {
                CityDressingBuilder.Box(park, "PavilionColumn", new Vector3(west + 10f, Level + 4.8f, Centre.y + side * 18f),
                    new Vector3(1.2f, 8.8f, 1.2f), White);
            }

            // The fountain at the east end, round, so the axis ends on something.
            var east = new Vector2(Centre.x + PoolLength * 0.5f + 28f, Centre.y);
            Disc(park, "FountainRim", east, Level + 0.35f, 12f, 0.7f, StoneDark);
            Disc(park, "Fountain", east, Level + 0.5f, 10.5f, 0.7f, Water);
            Disc(park, "FountainJet", east, Level + 2.2f, 1.2f, 3.6f, White);

            PlantEdges(park);
            Steps(park, terrains);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Hill park] Terrace park built at ({Centre.x:0}, {Centre.y:0}), level {Level:0} m, " +
                      $"{PoolRows.Length} pools of {PoolLength:0} m.");
        }

        /// <summary>Levels the shelf and blends its edges back into the hill.</summary>
        private static void LevelShelf(Terrain[] terrains)
        {
            var half = Size * 0.5f;
            var moved = 0;

            foreach (var terrain in terrains)
            {
                var data = terrain.terrainData;
                var origin = terrain.transform.position;
                var resolution = data.heightmapResolution;
                var step = data.size.x / (resolution - 1);
                var heights = data.GetHeights(0, 0, resolution, resolution);

                for (var y = 0; y < resolution; y++)
                {
                    var worldZ = origin.z + y * step;

                    for (var x = 0; x < resolution; x++)
                    {
                        var worldX = origin.x + x * step;

                        // Distance outside the shelf's rectangle; zero inside it.
                        var dx = Mathf.Max(0f, Mathf.Abs(worldX - Centre.x) - half.x);
                        var dz = Mathf.Max(0f, Mathf.Abs(worldZ - Centre.y) - half.y);
                        var outside = Mathf.Sqrt(dx * dx + dz * dz);

                        if (outside > Blend)
                        {
                            continue;
                        }

                        var current = heights[y, x] * data.size.y + origin.y;
                        var pull = Mathf.SmoothStep(1f, 0f, outside / Blend);
                        var next = Mathf.Lerp(current, Level, pull);

                        heights[y, x] = Mathf.Clamp01((next - origin.y) / data.size.y);
                        moved++;
                    }
                }

                data.SetHeights(0, 0, heights);
            }

            Debug.Log($"[Hill park] Shelf levelled at {Level:0} m: {moved} terrain points.");
        }

        /// <summary>Trees along the long edges, so the shelf reads as a park and not as a car park.</summary>
        private static void PlantEdges(Transform park)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TreeModel);
            if (model == null)
            {
                return;
            }

            // Scaled to twelve metres whatever size the model file happens to be.
            var height = 0.01f;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                height = Mathf.Max(height, filter.sharedMesh.bounds.max.y * filter.transform.lossyScale.y);
            }

            var scale = 12f / height;

            foreach (var edge in new[] { -1f, 1f })
            {
                var z = Centre.y + edge * (Size.y * 0.5f - 6f);

                for (var x = Centre.x - Size.x * 0.5f + 8f; x < Centre.x + Size.x * 0.5f; x += 12f)
                {
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(model, park);
                    tree.name = "ParkTree";
                    tree.transform.position = new Vector3(x, Level, z);
                    tree.transform.rotation = Quaternion.Euler(0f, x * 37f % 360f, 0f);
                    tree.transform.localScale = Vector3.one * scale;
                }
            }
        }

        /// <summary>
        /// Stone steps down the south face to Innovation's streets: the way up for anybody on foot.
        /// Each step sits on the blended slope, so the flight follows the ground rather than floating.
        /// </summary>
        private static void Steps(Transform park, Terrain[] terrains)
        {
            var top = Centre.y - Size.y * 0.5f;

            for (var z = top; z > top - Blend - 4f; z -= 3f)
            {
                var ground = Sample(terrains, Centre.x, z);
                CityDressingBuilder.Box(park, "Step", new Vector3(Centre.x, ground + 0.2f, z),
                    new Vector3(14f, 0.5f, 3.2f), Stone);
            }
        }

        /// <summary>A round slab at a stated height; the shared helper always sits on the ground instead.</summary>
        private static void Disc(Transform parent, string name, Vector2 at, float centreY, float diameter,
            float thickness, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(parent, true);
            disc.transform.position = new Vector3(at.x, centreY, at.y);
            disc.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);
            disc.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>());
        }

        private static float Sample(Terrain[] terrains, float x, float z)
        {
            foreach (var terrain in terrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;

                if (x < origin.x || x > origin.x + size.x || z < origin.z || z > origin.z + size.z)
                {
                    continue;
                }

                return terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
            }

            return Level;
        }
    }
}
