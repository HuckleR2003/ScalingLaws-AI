using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Rebuilds the ground under the finished city and takes everything standing on it along.
    ///
    /// The terrain is generated once and the city is then built, dressed and tidied on top of it
    /// over many passes; running <see cref="CityTerrainBuilder.Build"/> again would throw all of
    /// that away. This rebuilds only the terrain asset, then moves every house, tree, lamp and
    /// building up or down by exactly how much the ground under it moved. What the new ground
    /// cannot hold — a house the eased bank has left on a slope, a tree the shore now reaches —
    /// is taken away and counted. Roads are not touched here: rebuild the road network afterwards,
    /// which lays them on the new ground and relays the driveways and the asphalt paint too.
    /// </summary>
    public static class CityGroundReshape
    {
        /// <summary>A house or a building left on ground steeper than this (about 20 degrees) is taken away.</summary>
        private const float SteepestPlot = 0.36f;

        [MenuItem("Scaling Laws/Reshape the ground under the city")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var city = GameObject.Find("City");

            if (!scene.IsValid() || city == null)
            {
                Debug.LogError("[Ground] No City.unity with a City root.");
                return;
            }

            var units = new List<Transform>();
            Collect(city.transform, units);

            // Measured on the ground as it is, before a single height changes.
            var before = units.Select(unit => CityTerrainBuilder.HeightAt(unit.position.x, unit.position.z)).ToList();

            CityTerrainBuilder.BuildTerrainData();
            AssetDatabase.SaveAssets();
            CityTerrainBuilder.EnsureSouthTerrain();

            var moved = 0;
            var largest = 0f;
            var doomed = new List<GameObject>();
            var reasons = new Dictionary<string, int>();

            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                var at = new Vector2(unit.position.x, unit.position.z);
                var ground = CityTerrainBuilder.HeightAt(at.x, at.y);
                var change = ground - before[index];

                if (Mathf.Abs(change) > 0.02f)
                {
                    unit.position += Vector3.up * change;
                    moved++;
                    largest = Mathf.Max(largest, Mathf.Abs(change));
                }

                if (Protected(unit))
                {
                    continue;
                }

                string reason = null;

                if (ground < CityLayout.SeaLevel + 0.3f && Mathf.Abs(change) > 0.02f)
                {
                    reason = "now at the water's edge or in it";
                }
                else if (IsBuilding(unit) && Slope(at) > SteepestPlot)
                {
                    reason = "a building now on a slope";
                }

                if (reason == null)
                {
                    continue;
                }

                doomed.Add(unit.gameObject);
                reasons.TryGetValue(reason, out var count);
                reasons[reason] = count + 1;
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
            }

            SeatSlabs(city.transform);

            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Ground] Terrain rebuilt. {units.Count} things standing on it, {moved} moved, "
                + $"the most by {largest:0.0} m.");

            foreach (var pair in reasons)
            {
                Debug.Log($"[Ground] taken away, {pair.Key}: {pair.Value}");
            }

            Debug.Log("[Ground] Now rebuild the road network: Scaling Laws/Road network/Rebuild.");
        }

        [MenuItem("Scaling Laws/Seat the flat slabs on the ground")]
        public static void SeatSlabsOnly()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var city = GameObject.Find("City");

            if (!scene.IsValid() || city == null)
            {
                Debug.LogError("[Ground] No City.unity with a City root.");
                return;
            }

            SeatSlabs(city.transform);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Refits the flat slabs — the gallery's car park and its parking rows, the event lawns — to
        /// the ground now under them.
        ///
        /// Moving a slab by the change at its middle is right for a house and wrong for a car park
        /// forty metres across: where the ground under it has tilted, one side ends up in the air and
        /// the gallery's car park hung over the new slope below Greendale like a shelf. A slab is
        /// raised to the highest ground under its footprint and deepened down to the lowest, so it
        /// sits on the land as a level platform with no daylight under any edge — on level ground, as
        /// under the gallery once its lot was levelled, that is simply a thin slab again.
        ///
        /// A slab too big for its level ground is cut down first: the gallery's car park is 250 by 170
        /// metres and its far corner reaches up the Greendale hill, so raised whole it stood twenty
        /// metres tall. It is shrunk to the largest part of itself lying on ground level with its
        /// middle, and its parking rows are relaid across what is left.
        /// </summary>
        private static void SeatSlabs(Transform city)
        {
            var renderers = city.GetComponentsInChildren<MeshRenderer>().Select(renderer => renderer.transform).ToList();
            var laid = new List<(Transform Box, float Top)>();

            // A car park runs up to the buildings it serves, never under them.
            var units = new List<Transform>();
            Collect(city, units);
            var buildings = units
                .Select(unit => unit.GetComponentsInChildren<Renderer>().Select(r => r.bounds).ToList())
                .Where(parts => parts.Count > 0)
                .Select(parts => parts.Aggregate((a, b) => { a.Encapsulate(b); return a; }))
                .Where(bounds => bounds.size.y >= 6f && Mathf.Min(bounds.size.x, bounds.size.z) >= 6f
                                 && Mathf.Max(bounds.size.x, bounds.size.z) < 120f)
                .ToList();

            foreach (var slab in renderers.Where(box => box.name is "ParkingApron" or "EventGround"))
            {
                FitToLevelGround(slab, slab.name == "ParkingApron" ? buildings : new List<Bounds>());
                laid.Add((slab, Seat(slab)));
            }

            // The old rows were spaced along the wrong axis and lay almost on top of one another in a
            // diagonal smear across the car park: they are taken up and relaid, one pattern per apron.
            var rows = renderers.Where(box => box.name == "ParkingRow").ToList();
            var relaid = 0;

            foreach (var (apron, top) in laid.Where(slab => slab.Box.name == "ParkingApron"))
            {
                var template = rows.FirstOrDefault(row => Covers(apron, new Vector2(row.position.x, row.position.z)));
                if (template != null)
                {
                    relaid += LayParkingRows(apron, top, template);
                }
            }

            foreach (var row in rows)
            {
                Object.DestroyImmediate(row.gameObject);
            }

            Debug.Log($"[Ground] {laid.Count} flat slabs refitted to the ground under them, {rows.Count} parking rows taken up and {relaid} relaid.");
        }

        /// <summary>Stripes of marked bays across a car park, sixteen metres apart with a margin all round.</summary>
        private static int LayParkingRows(Transform apron, float top, Transform template)
        {
            const float spacing = 16f;
            const float margin = 10f;

            var width = apron.lossyScale.x - margin * 2f;
            var depth = apron.lossyScale.z - margin * 2f;
            var count = Mathf.FloorToInt(depth / spacing) + 1;

            if (width < 10f || count < 1)
            {
                return 0;
            }

            var first = -(count - 1) * spacing * 0.5f;

            for (var index = 0; index < count; index++)
            {
                var copy = Object.Instantiate(template.gameObject, template.parent);
                copy.name = "ParkingRow";

                var row = copy.transform;
                var middle = apron.position + apron.forward * (first + index * spacing);
                row.rotation = apron.rotation;

                var parentScale = row.lossyScale.x / Mathf.Max(0.0001f, row.localScale.x);
                row.localScale = new Vector3(width, 0.1f, template.lossyScale.z) / Mathf.Max(0.0001f, parentScale);
                row.position = new Vector3(middle.x, top + 0.05f, middle.z);
            }

            return count;
        }

        /// <summary>
        /// Cuts a slab down to the largest rectangle of itself that lies within a metre and a half of
        /// the ground at its middle and three metres clear of the given buildings.
        /// </summary>
        private static void FitToLevelGround(Transform slab, List<Bounds> buildings)
        {
            const float cell = 4f;

            var centre = new Vector2(slab.position.x, slab.position.z);
            var right = new Vector2(slab.right.x, slab.right.z).normalized;
            var forward = new Vector2(slab.forward.x, slab.forward.z).normalized;
            var level = CityTerrainBuilder.HeightAt(centre.x, centre.y);

            var columns = Mathf.Max(1, Mathf.RoundToInt(slab.lossyScale.x / cell));
            var rows = Mathf.Max(1, Mathf.RoundToInt(slab.lossyScale.z / cell));
            var cellX = slab.lossyScale.x / columns;
            var cellZ = slab.lossyScale.z / rows;
            var free = new bool[rows, columns];

            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    var at = centre + right * (-slab.lossyScale.x * 0.5f + (c + 0.5f) * cellX)
                             + forward * (-slab.lossyScale.z * 0.5f + (r + 0.5f) * cellZ);
                    free[r, c] = Mathf.Abs(CityTerrainBuilder.HeightAt(at.x, at.y) - level) <= 1.5f
                                 && !buildings.Any(bounds => at.x > bounds.min.x - 3f && at.x < bounds.max.x + 3f
                                                             && at.y > bounds.min.z - 3f && at.y < bounds.max.z + 3f);
                }
            }

            var (top, left, height, width) = CityRoadNetwork.LargestClearRectangle(free);

            if (width * height == rows * columns || width * cellX < 12f || height * cellZ < 12f)
            {
                return;
            }

            var middle = centre + right * (-slab.lossyScale.x * 0.5f + (left + width * 0.5f) * cellX)
                         + forward * (-slab.lossyScale.z * 0.5f + (top + height * 0.5f) * cellZ);
            var parentX = slab.lossyScale.x / Mathf.Max(0.0001f, slab.localScale.x);
            var parentZ = slab.lossyScale.z / Mathf.Max(0.0001f, slab.localScale.z);

            slab.position = new Vector3(middle.x, slab.position.y, middle.y);
            slab.localScale = new Vector3(width * cellX / parentX, slab.localScale.y, height * cellZ / parentZ);
        }

        /// <summary>Raises a slab to the highest ground under it and deepens it to the lowest; returns its top.</summary>
        private static float Seat(Transform slab)
        {
            var right = new Vector2(slab.right.x, slab.right.z) * (slab.lossyScale.x * 0.5f);
            var forward = new Vector2(slab.forward.x, slab.forward.z) * (slab.lossyScale.z * 0.5f);
            var centre = new Vector2(slab.position.x, slab.position.z);

            var highest = float.MinValue;
            var lowest = float.MaxValue;

            for (var u = -1f; u <= 1.001f; u += 0.25f)
            {
                for (var v = -1f; v <= 1.001f; v += 0.25f)
                {
                    var at = centre + right * u + forward * v;
                    var ground = CityTerrainBuilder.HeightAt(at.x, at.y);
                    highest = Mathf.Max(highest, ground);
                    lowest = Mathf.Min(lowest, ground);
                }
            }

            var surface = highest + 0.12f;
            var bottom = Mathf.Min(surface - 0.3f, lowest - 0.3f);
            var parentScale = slab.lossyScale.y / Mathf.Max(0.0001f, slab.localScale.y);

            slab.position = new Vector3(slab.position.x, (surface + bottom) * 0.5f, slab.position.z);
            slab.localScale = new Vector3(slab.localScale.x, (surface - bottom) / Mathf.Max(0.0001f, parentScale), slab.localScale.z);
            return surface;
        }

        private static bool Covers(Transform box, Vector2 point)
        {
            var offset = new Vector3(point.x - box.position.x, 0f, point.y - box.position.z);
            return Mathf.Abs(Vector3.Dot(offset, box.right)) <= box.lossyScale.x * 0.5f
                   && Mathf.Abs(Vector3.Dot(offset, box.forward)) <= box.lossyScale.z * 0.5f;
        }

        private static bool InsideGroup(Transform transform, params string[] names)
        {
            for (var at = transform; at != null; at = at.parent)
            {
                if (names.Contains(at.name))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The things that stand on the ground on their own: a model, a primitive, a pin, or a house
        /// plot with its garage and hedge. Groups are walked into; the road network is left for its
        /// own rebuild, and the terrain and the sea are not standing on anything.
        /// </summary>
        private static void Collect(Transform node, List<Transform> units)
        {
            foreach (Transform child in node)
            {
                if (child.name is "RoadNetwork" or "Sea" || child.GetComponent<Terrain>() != null)
                {
                    continue;
                }

                var standsAlone = PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject)
                                  || child.GetComponent<MeshRenderer>() != null
                                  || child.GetComponent<UI.MapSitePin>() != null
                                  || child.name is "House" or "Villa" or "FounderHouse";

                if (standsAlone)
                {
                    units.Add(child);
                }
                else
                {
                    Collect(child, units);
                }
            }
        }

        /// <summary>Map sites, their buildings and the founder's house are moved with the ground but never taken away.</summary>
        private static bool Protected(Transform unit)
        {
            for (var at = unit; at != null; at = at.parent)
            {
                if (at.name is "SiteBuildings" or "MapSites" or "FounderHome" || at.GetComponent<UI.MapSitePin>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBuilding(Transform unit) =>
            unit.name is "House" or "Villa" || unit.name.StartsWith("building-");

        /// <summary>Rise per metre across eight metres either way, the steeper of the two directions.</summary>
        private static float Slope(Vector2 at)
        {
            const float reach = 8f;

            var east = CityTerrainBuilder.HeightAt(at.x + reach, at.y);
            var west = CityTerrainBuilder.HeightAt(at.x - reach, at.y);
            var north = CityTerrainBuilder.HeightAt(at.x, at.y + reach);
            var south = CityTerrainBuilder.HeightAt(at.x, at.y - reach);

            return Mathf.Max(Mathf.Abs(east - west), Mathf.Abs(north - south)) / (reach * 2f);
        }
    }
}
