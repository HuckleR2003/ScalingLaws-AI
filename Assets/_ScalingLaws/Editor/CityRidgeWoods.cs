using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The pine woods on the shoulder behind Greendale, and the road that slaloms down them to the
    /// water.
    ///
    /// **Why here.** The author asked for more ground behind Greendale and Media with a believable
    /// drop to the sea, a long winding road and a thick forest of tall trees. Behind Media there is
    /// no room left — four subdivisions reach the western edge — but north of Greendale the plateau
    /// ends at 96 m, the mountain climbs to 225 m at the corner and the ground falls to the bay at
    /// 40 m. That is the drop, already there; this tool puts a road on it and plants it.
    ///
    /// **Four passes, in this order**, because each needs the one before it:
    ///   1. `Grade` cuts a shelf under the road, to the road's own smoothed profile.
    ///   2. `Pave` lays the road on that same profile, tile by tile (not through `CityRoadNetwork`,
    ///      see the note on `Pave`).
    ///   3. `Plant` paints the forest floor and puts the pines in.
    ///   4. `Houses` drops a handful of wide houses along the road.
    /// `Run` does all four. Rerun it after anything moves the ground here.
    /// </summary>
    public static class CityRidgeWoods
    {
        /// <summary>The group everything this tool makes hangs from, so a rerun can clear it.</summary>
        private const string GroupName = "RidgeWoods";

        /// <summary>The road in <see cref="CityLayout.Roads"/> this tool builds around.</summary>
        private const string RoadId = "greendale_ridge";

        /// <summary>How wide the shelf under the road is cut, in metres either side of the centre.</summary>
        private const float ShelfHalfWidth = 13f;

        /// <summary>How far past the shelf the cut blends back into the mountain.</summary>
        private const float ShelfBlend = 46f;

        /// <summary>Trees stay this far off the road, so the drive is not a tunnel.</summary>
        private const float RoadClearance = 17f;

        /// <summary>And this far off anything else that already stands on the map.</summary>
        private const float BuiltClearance = 26f;

        /// <summary>Roughly one pine per this many square metres, before the thinning noise.</summary>
        private const float TreeSpacing = 9.5f;

        private static readonly string PineFolder =
            "Assets/_ScalingLaws/ThirdParty/Kenney/NatureKit/Models/FBX format/";

        /// <summary>The wood's own patch of map: x from, z from, x to, z to.</summary>
        private static readonly Rect Wood = Rect.MinMaxRect(0f, 1660f, 470f, 2048f);

        /// <summary>How wide the forest road is, kerb to kerb. Narrower than a street on purpose.</summary>
        private const float RoadWidth = 9.5f;

        /// <summary>How much road one tile covers along the way.</summary>
        private const float TileStep = 6f;

        private const string RoadTile =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/road-straight.fbx";

        /// <summary>The tile model faces along X; every road builder here turns it this way.</summary>
        private static readonly Quaternion AlongX = Quaternion.Euler(0f, -90f, 0f);

        /// <summary>The steepest the finished road is allowed to climb. A hard mountain road, not a wall.</summary>
        private const float SteepestGrade = 0.11f;

        /// <summary>
        /// The height the road runs at, point by point along its smoothed line.
        ///
        /// **Not simply the ground.** Following the terrain gave a staircase — 55% between two tiles
        /// where the line crosses the fall of the slope — because the mountain is steeper across the
        /// road than along it. So the ground under the centreline is read once, smoothed hard, and
        /// then walked forwards and backwards until no step between two points is steeper than
        /// <see cref="SteepestGrade"/>. The shelf is cut to this and the tiles are laid on it, so the
        /// two cannot disagree.
        /// </summary>
        private static List<float> Profile(List<Vector2> line, Terrain[] terrains)
        {
            var heights = line.Select(point => Sample(terrains, point.x, point.y)).ToList();

            for (var pass = 0; pass < 12; pass++)
            {
                var smoothed = new List<float>(heights);

                for (var index = 1; index < heights.Count - 1; index++)
                {
                    smoothed[index] = (heights[index - 1] + heights[index] * 2f + heights[index + 1]) / 4f;
                }

                heights = smoothed;
            }

            // Forwards, then backwards: one pass only fixes climbs, the other only descents.
            for (var pass = 0; pass < 3; pass++)
            {
                for (var index = 1; index < heights.Count; index++)
                {
                    var reach = Vector2.Distance(line[index - 1], line[index]) * SteepestGrade;
                    heights[index] = Mathf.Clamp(heights[index], heights[index - 1] - reach, heights[index - 1] + reach);
                }

                for (var index = heights.Count - 2; index >= 0; index--)
                {
                    var reach = Vector2.Distance(line[index], line[index + 1]) * SteepestGrade;
                    heights[index] = Mathf.Clamp(heights[index], heights[index + 1] - reach, heights[index + 1] + reach);
                }
            }

            return heights;
        }

        /// <summary>The profile's height at a distance along the line, between its points.</summary>
        private static float ProfileAt(List<Vector2> line, List<float> profile, float along)
        {
            var travelled = 0f;

            for (var index = 0; index < line.Count - 1; index++)
            {
                var length = Vector2.Distance(line[index], line[index + 1]);

                if (along <= travelled + length || index == line.Count - 2)
                {
                    var t = length < 0.001f ? 0f : Mathf.Clamp01((along - travelled) / length);
                    return Mathf.Lerp(profile[index], profile[index + 1], t);
                }

                travelled += length;
            }

            return profile[^1];
        }

        [MenuItem("Scaling Laws/Ridge woods/Everything")]
        public static void Run()
        {
            Grade();
            Pave();
            Plant();
            Houses();
        }

        /// <summary>
        /// Lays the road itself, tile by tile along the graded shelf.
        ///
        /// **Not left to `CityRoadNetwork`.** That builder is right about the city: it refuses a road
        /// that meets nothing, cuts side streets away from junctions on steep ground, and drops what
        /// it cannot make a junction for — and by those rules a lane that climbs a mountain, touches
        /// the suburb once and ends at a view over the water is all of the things it refuses. It is
        /// also, on purpose, none of the things it is protecting: no junctions, no houses fronting it,
        /// no traffic. So the road is stated here, at the width of a forest road rather than a street.
        /// </summary>
        [MenuItem("Scaling Laws/Ridge woods/1b. Lay the forest road")]
        public static void Pave()
        {
            var scene = OpenScene();
            if (!scene.IsValid())
            {
                return;
            }

            var road = CityLayout.Roads.FirstOrDefault(run => run.Id == RoadId);
            if (road == null)
            {
                Debug.LogWarning($"[Ridge woods] No road called {RoadId} in CityLayout.");
                return;
            }

            var tile = AssetDatabase.LoadAssetAtPath<GameObject>(RoadTile);
            if (tile == null)
            {
                Debug.LogWarning("[Ridge woods] No road tile model.");
                return;
            }

            var group = Group(scene, clear: false);
            var existing = group.transform.Find("RidgeRoad");

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var holder = new GameObject("RidgeRoad");
            holder.transform.SetParent(group.transform, false);

            // The same smoothing the terrain builder uses on its own centrelines, so the bends are
            // curves rather than the corners of a polyline.
            var line = CityTerrainBuilder.Smooth(road.Points);
            var terrains = Terrains();
            var profile = Profile(line, terrains);
            var placed = 0;
            var run = 0f;

            for (var index = 0; index < line.Count - 1; index++)
            {
                var from = line[index];
                var to = line[index + 1];
                var span = to - from;
                var length = span.magnitude;

                if (length < 0.01f)
                {
                    continue;
                }

                var direction = span / length;

                for (var travelled = 0f; travelled < length; travelled += TileStep)
                {
                    var at = from + direction * travelled;
                    var height = ProfileAt(line, profile, run + travelled);

                    if (height < CityLayout.SeaLevel + 1.5f)
                    {
                        continue;
                    }

                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(tile, holder.transform);

                    piece.name = $"RidgeRoad_{placed:000}";
                    piece.transform.position = new Vector3(at.x, height + 0.08f, at.y);
                    piece.transform.rotation =
                        Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up) * AlongX;

                    // A little longer than the step, so the bends do not open gaps between tiles.
                    piece.transform.localScale = new Vector3(TileStep * 1.25f, 1f, RoadWidth);

                    placed++;
                }

                run += length;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // What the drive is actually like: the steepest step between two tiles. A staircase shows
            // up here rather than in a screenshot a day later.
            var steepest = 0f;
            var previous = float.NaN;

            foreach (Transform piece in holder.transform)
            {
                if (!float.IsNaN(previous))
                {
                    steepest = Mathf.Max(steepest, Mathf.Abs(piece.position.y - previous) / TileStep);
                }

                previous = piece.position.y;
            }

            Debug.Log($"[Ridge woods] Laid {placed} road tiles along {line.Count} smoothed points; " +
                      $"steepest step between two tiles {steepest * 100f:0}%.");
        }

        [MenuItem("Scaling Laws/Ridge woods/1. Cut the shelf for the road")]
        public static void Grade()
        {
            var scene = OpenScene();
            if (!scene.IsValid())
            {
                return;
            }

            var road = CityLayout.Roads.FirstOrDefault(run => run.Id == RoadId);
            if (road == null)
            {
                Debug.LogWarning($"[Ridge woods] No road called {RoadId} in CityLayout.");
                return;
            }

            var line = CityTerrainBuilder.Smooth(road.Points);
            var profile = Profile(line, Terrains());
            var moved = 0;

            foreach (var terrain in Terrains())
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

                        if (!Wood.Contains(new Vector2(worldX, worldZ)))
                        {
                            continue;
                        }

                        var along = NearestOn(line, new Vector2(worldX, worldZ), out var distance);

                        if (distance > ShelfHalfWidth + ShelfBlend)
                        {
                            continue;
                        }

                        // The height the road runs at here — the same profile the tiles are laid on,
                        // so the shelf and the road cannot disagree.
                        var wanted = ProfileAt(line, profile, along);
                        var current = heights[y, x] * data.size.y + origin.y;

                        var pull = distance <= ShelfHalfWidth
                            ? 1f
                            : Mathf.SmoothStep(1f, 0f, (distance - ShelfHalfWidth) / ShelfBlend);

                        var next = Mathf.Lerp(current, wanted, pull);

                        if (Mathf.Abs(next - current) > 0.05f)
                        {
                            moved++;
                        }

                        heights[y, x] = Mathf.Clamp01((next - origin.y) / data.size.y);
                    }
                }

                data.SetHeights(0, 0, heights);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Ridge woods] Shelf cut: {moved} terrain points moved along {line.Count} bends.");
        }

        [MenuItem("Scaling Laws/Ridge woods/2. Paint the forest floor and plant it")]
        public static void Plant()
        {
            var scene = OpenScene();
            if (!scene.IsValid())
            {
                return;
            }

            var group = Holder(Group(scene, clear: false), "Pines");
            var road = CityLayout.Roads.FirstOrDefault(run => run.Id == RoadId);
            var line = road == null
                ? new List<Vector2>()
                : road.Points.Select(point => new Vector2(point.X, point.Z)).ToList();

            PaintForestFloor();

            var pines = LoadPines();
            if (pines.Count == 0)
            {
                Debug.LogWarning("[Ridge woods] No pine models found; planted nothing.");
                return;
            }

            var built = Occupied();
            var terrains = Terrains();
            var planted = 0;
            var random = new System.Random(20260918);

            for (var z = Wood.yMin; z < Wood.yMax; z += TreeSpacing)
            {
                for (var x = Wood.xMin; x < Wood.xMax; x += TreeSpacing)
                {
                    var spot = new Vector2(
                        x + (float)random.NextDouble() * TreeSpacing,
                        z + (float)random.NextDouble() * TreeSpacing);

                    var height = Sample(terrains, spot.x, spot.y);

                    // Nothing in the water, nothing on the beach, nothing on a cliff.
                    if (height < CityLayout.SeaLevel + 3f || Slope(terrains, spot.x, spot.y) > 0.9f)
                    {
                        continue;
                    }

                    if (line.Count > 1)
                    {
                        NearestOn(line, spot, out var toRoad);

                        if (toRoad < RoadClearance)
                        {
                            continue;
                        }
                    }

                    if (built.Any(bounds => bounds.Contains(spot)))
                    {
                        continue;
                    }

                    // Thinner at the edges of the wood, so it fades into the grass instead of
                    // stopping at a line.
                    var edge = Mathf.Min(
                        Mathf.InverseLerp(Wood.xMin, Wood.xMin + 90f, spot.x),
                        Mathf.InverseLerp(Wood.yMin, Wood.yMin + 110f, spot.y));

                    if (random.NextDouble() > Mathf.Lerp(0.45f, 1f, edge))
                    {
                        continue;
                    }

                    var pine = pines[random.Next(pines.Count)];
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(pine, group.transform);

                    tree.name = $"Pine_{planted:000}";
                    tree.transform.position = new Vector3(spot.x, height - 0.3f, spot.y);
                    tree.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

                    // Kenney's pines are about one and a half metres tall in their own file (measured:
                    // 1.37 to 1.55); the city around them is in metres, and the author asked for tall ones.
                    var scale = Mathf.Lerp(10f, 15f, (float)random.NextDouble());
                    tree.transform.localScale = new Vector3(scale, scale * Mathf.Lerp(0.9f, 1.25f,
                        (float)random.NextDouble()), scale);

                    planted++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Ridge woods] Planted {planted} pines from {pines.Count} models.");
        }

        [MenuItem("Scaling Laws/Ridge woods/3. Houses along the road")]
        public static void Houses()
        {
            var scene = OpenScene();
            if (!scene.IsValid())
            {
                return;
            }

            var road = CityLayout.Roads.FirstOrDefault(run => run.Id == RoadId);
            if (road == null)
            {
                return;
            }

            var group = Holder(Group(scene, clear: false), "RidgeHouses");
            var terrains = Terrains();
            var line = road.Points.Select(point => new Vector2(point.X, point.Z)).ToList();
            var models = new[] { "building-type-j", "building-type-l", "building-type-n", "building-type-q" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/{name}.fbx"))
                .Where(model => model != null)
                .ToList();

            if (models.Count == 0)
            {
                Debug.LogWarning("[Ridge woods] No house models found.");
                return;
            }

            var random = new System.Random(9182026);
            var placed = 0;

            // Every so often along the road, on whichever side is gentler: a house in a clearing, not
            // a row. The forest is the point; the houses are for scale and for somebody living there.
            for (var t = 0.12f; t < 0.95f; t += 0.14f)
            {
                var at = PointAlong(line, t, out var direction);
                var side = random.Next(2) == 0 ? 1f : -1f;
                var across = new Vector2(-direction.y, direction.x) * side;
                var spot = at + across * Mathf.Lerp(26f, 34f, (float)random.NextDouble());

                var height = Sample(terrains, spot.x, spot.y);

                if (height < CityLayout.SeaLevel + 4f || Slope(terrains, spot.x, spot.y) > 0.42f)
                {
                    continue;
                }

                var model = models[random.Next(models.Count)];
                var house = (GameObject)PrefabUtility.InstantiatePrefab(model, group.transform);

                house.name = $"RidgeHouse_{placed:00}";
                house.transform.position = new Vector3(spot.x, height - 0.2f, spot.y);
                house.transform.rotation = Quaternion.LookRotation(
                    new Vector3(-across.x, 0f, -across.y), Vector3.up);

                var scale = Mathf.Lerp(4.6f, 5.6f, (float)random.NextDouble());
                house.transform.localScale = new Vector3(scale * 1.25f, scale, scale);

                var prop = house.AddComponent<CityProp>();
                prop.Describe(CityPropKind.House, new Vector3(18f, 9f, 14f), "greendale", placed);

                placed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Ridge woods] Placed {placed} houses along the road.");
        }

        /// <summary>
        /// Forest floor under the wood and sand where it meets the water, written straight onto the
        /// terrain's own splat map. The layers are the ones `CityTerrainBuilder` makes: 0 sand,
        /// 1 grass, 2 rock, 3 forest, 4 asphalt, 5 concrete.
        /// </summary>
        private static void PaintForestFloor()
        {
            foreach (var terrain in Terrains())
            {
                var data = terrain.terrainData;
                var origin = terrain.transform.position;
                var resolution = data.alphamapResolution;
                var layers = data.terrainLayers.Length;

                if (layers < 5)
                {
                    continue;
                }

                var map = data.GetAlphamaps(0, 0, resolution, resolution);
                var step = data.size.x / (resolution - 1);
                var touched = 0;

                for (var y = 0; y < resolution; y++)
                {
                    var worldZ = origin.z + y * step;

                    for (var x = 0; x < resolution; x++)
                    {
                        var worldX = origin.x + x * step;

                        if (!Wood.Contains(new Vector2(worldX, worldZ)))
                        {
                            continue;
                        }

                        // Leave the road and anything already concrete alone: those weights are what
                        // make a road look like a road from above.
                        var asphalt = map[y, x, 4];
                        var concrete = layers > 5 ? map[y, x, 5] : 0f;

                        if (asphalt > 0.25f || concrete > 0.4f)
                        {
                            continue;
                        }

                        var height = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + origin.y;

                        if (height < CityLayout.SeaLevel + 2f)
                        {
                            continue;
                        }

                        var grain = Mathf.PerlinNoise(worldX * 0.03f, worldZ * 0.03f);
                        var forest = Mathf.Lerp(0.55f, 0.95f, grain);

                        map[y, x, 3] = forest;
                        map[y, x, 1] = 1f - forest - asphalt - concrete > 0f
                            ? 1f - forest - asphalt - concrete
                            : 0f;
                        map[y, x, 0] = 0f;
                        map[y, x, 2] = 0f;

                        var total = 0f;
                        for (var layer = 0; layer < layers; layer++)
                        {
                            total += map[y, x, layer];
                        }

                        if (total > 0f)
                        {
                            for (var layer = 0; layer < layers; layer++)
                            {
                                map[y, x, layer] /= total;
                            }
                        }

                        touched++;
                    }
                }

                data.SetAlphamaps(0, 0, map);
                Debug.Log($"[Ridge woods] Forest floor painted on {terrain.name}: {touched} splat points.");
            }
        }

        private static List<GameObject> LoadPines()
        {
            var names = new[]
            {
                "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD",
                "tree_pineTallA_detailed", "tree_pineTallC_detailed",
                "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundA", "tree_pineRoundC"
            };

            return names
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{PineFolder}{name}.fbx"))
                .Where(model => model != null)
                .ToList();
        }

        /// <summary>Footprints of everything already standing in the wood, with a margin around it.</summary>
        private static List<Rect> Occupied()
        {
            var rects = new List<Rect>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var bounds = renderer.bounds;

                if (bounds.size.x > 600f || bounds.size.z > 600f)
                {
                    continue;
                }

                if (bounds.max.x < Wood.xMin || bounds.min.x > Wood.xMax ||
                    bounds.max.z < Wood.yMin || bounds.min.z > Wood.yMax)
                {
                    continue;
                }

                rects.Add(Rect.MinMaxRect(
                    bounds.min.x - BuiltClearance, bounds.min.z - BuiltClearance,
                    bounds.max.x + BuiltClearance, bounds.max.z + BuiltClearance));
            }

            return rects;
        }

        private static UnityEngine.SceneManagement.Scene OpenScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogWarning("[Ridge woods] No city scene.");
            }

            return scene;
        }

        /// <summary>One phase's own sub-group, emptied so the phase can be run again.</summary>
        private static GameObject Holder(GameObject group, string name)
        {
            // Anything an older run of this tool left loose in the group, before the phases had
            // holders of their own. Without this, the last run's pines are still standing and every
            // spot for a new one reads as taken.
            for (var index = group.transform.childCount - 1; index >= 0; index--)
            {
                var child = group.transform.GetChild(index);

                if (child.name.StartsWith("Pine_") || child.name.StartsWith("RidgeHouse_"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            var existing = group.transform.Find(name);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var holder = new GameObject(name);
            holder.transform.SetParent(group.transform, false);
            return holder;
        }

        private static GameObject Group(UnityEngine.SceneManagement.Scene scene, bool clear = true)
        {
            var city = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "City");
            var parent = city != null ? city.transform : null;
            var existing = parent == null ? null : parent.Find(GroupName);

            if (existing != null && clear)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                return existing.gameObject;
            }

            var group = new GameObject(GroupName);
            group.transform.SetParent(parent, false);
            return group;
        }

        private static Terrain[] Terrains() =>
            Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

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

            return -1f;
        }

        private static float Slope(Terrain[] terrains, float x, float z)
        {
            var west = Sample(terrains, x - 8f, z);
            var east = Sample(terrains, x + 8f, z);
            var south = Sample(terrains, x, z - 8f);
            var north = Sample(terrains, x, z + 8f);

            return Mathf.Max(Mathf.Abs(east - west), Mathf.Abs(north - south)) / 16f;
        }

        /// <summary>Distance along the line to the nearest point on it, and how far off it we are.</summary>
        private static float NearestOn(List<Vector2> line, Vector2 point, out float distance)
        {
            var best = float.MaxValue;
            var bestAlong = 0f;
            var travelled = 0f;

            for (var index = 0; index < line.Count - 1; index++)
            {
                var from = line[index];
                var to = line[index + 1];
                var span = to - from;
                var length = span.magnitude;

                if (length < 0.001f)
                {
                    continue;
                }

                var t = Mathf.Clamp01(Vector2.Dot(point - from, span) / (length * length));
                var on = from + span * t;
                var away = Vector2.Distance(point, on);

                if (away < best)
                {
                    best = away;
                    bestAlong = travelled + length * t;
                }

                travelled += length;
            }

            distance = best;
            return bestAlong;
        }

        private static Vector2 PointAtDistance(List<Vector2> line, float along)
        {
            var travelled = 0f;

            for (var index = 0; index < line.Count - 1; index++)
            {
                var from = line[index];
                var to = line[index + 1];
                var length = Vector2.Distance(from, to);

                if (along <= travelled + length || index == line.Count - 2)
                {
                    var t = length < 0.001f ? 0f : Mathf.Clamp01((along - travelled) / length);
                    return Vector2.Lerp(from, to, t);
                }

                travelled += length;
            }

            return line[^1];
        }

        private static Vector2 PointAlong(List<Vector2> line, float fraction, out Vector2 direction)
        {
            var total = 0f;

            for (var index = 0; index < line.Count - 1; index++)
            {
                total += Vector2.Distance(line[index], line[index + 1]);
            }

            var at = PointAtDistance(line, total * fraction);
            var ahead = PointAtDistance(line, total * fraction + 12f);

            direction = (ahead - at).sqrMagnitude < 0.001f ? Vector2.up : (ahead - at).normalized;
            return at;
        }
    }
}
