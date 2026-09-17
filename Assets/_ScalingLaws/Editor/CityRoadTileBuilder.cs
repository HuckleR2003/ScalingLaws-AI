using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Lays real road tiles along the centrelines <see cref="CityTerrainBuilder"/> already computed,
    /// instead of stretching one tile to cover a whole segment the way <see cref="CityAssetSwapper"/>
    /// does for a single object on a single plot.
    ///
    /// **Every Kenney road model measures exactly 1x1x0.02.** Not metres — an abstract tile unit
    /// meant to be scaled uniformly to whatever width a road actually is. Scaling `road-straight` by
    /// a road's own `Width` (11, 16 or 26 depending on class) makes one tile exactly as wide as the
    /// road and exactly as long as it is wide, so stepping forward by that same distance tiles the
    /// road with no gap and no overlap, for any of the three widths, from one number.
    ///
    /// **Highways and streets only, on purpose.** Suburban lanes are drawn by
    /// <see cref="CityDressingBuilder.BuildSubdivision"/> from its own collector-and-plots survey,
    /// not from <see cref="CityLayout.Roads"/> directly — <c>BuildArterialSidewalks</c> already skips
    /// `RoadClass.Lane` for the same reason. Tiling from the raw `RoadRun` polyline here would lay a
    /// second, disagreeing road under the one the subdivision already built.
    ///
    /// Adds to the already-swapped `City.unity` rather than going through `CityDressingBuilder.BuildScene`,
    /// because that scene now holds real prefab instances and `MayOverwriteScene` will refuse to
    /// regenerate it — correctly. This only ever adds objects to what is already there.
    /// </summary>
    public static class CityRoadTileBuilder
    {
        private const string TilePath =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/road-straight.fbx";

        /// <summary>The tile's own footprint, measured directly rather than assumed.</summary>
        private const float TileSize = 1f;

        /// <summary>
        /// **The road runs along the tile's local X, not its Z — rendered and confirmed 2026-09-16.**
        /// A single `road-straight` at identity rotation, photographed from above with a marker on
        /// each axis, shows kerb, lane, centre line, lane, kerb stacked across Z, every band running
        /// the full length of X. So X is "along the road" and Z is "across it".
        ///
        /// `Quaternion.LookRotation` aims local **+Z**, which is the wrong axis for this tile: it
        /// laid every tile with the road's cross-section pointing down the road, so the painted lines
        /// ran across the carriageway instead of along it. Reported by Gosia with a drawing of what a
        /// road should look like next to what this was doing, which is what finally pinned it down.
        /// Hence the extra quarter turn, and hence width scaling Z while length scales X.
        /// </summary>
        private static readonly Quaternion AlongX = Quaternion.Euler(0f, -90f, 0f);

        /// <summary>
        /// How much road one tile covers, along the direction of travel.
        ///
        /// Free to be short now. The paint on this tile is uniform along X — the same bands from one
        /// end to the other — so tiles butt together invisibly at any length, and a short one only
        /// costs tile count. Short is what a curve wants: every tile is a rigid plate, and the less
        /// road one plate has to cover, the less it has to disagree with the curve under it.
        /// </summary>
        private const float RoadTileLength = 8f;

        [MenuItem("Scaling Laws/Tile roads (test, spine only)")]
        public static void TileSpineOnly()
        {
            Run(onlyRoadId: "spine");
        }

        [MenuItem("Scaling Laws/Tile roads (all highways and streets)")]
        public static void TileAll()
        {
            Run(onlyRoadId: null);
        }

        /// <summary>
        /// The width <see cref="CityDressingBuilder.BuildGrid"/> uses for every grid street, copied
        /// rather than derived — that method has no public constant to read, only a local `const`.
        /// </summary>
        private const float GridStreetWidth = 17f;

        /// <summary>
        /// Tiles the streets inside every <see cref="CityBlocks.Grids"/> block (downtown, media,
        /// innovation, civic, port) — found only after the first intersection render, which showed
        /// a dense grid still in flat grey directly under the newly tiled "spine" crossing it.
        ///
        /// **Not the same roads <see cref="TileAll"/> covers.** Those come from
        /// <see cref="CityLayout.Roads"/>. This dense grid is generated on the fly inside
        /// `CityDressingBuilder.BuildGrid`, straight from a `GridBlock`'s width, depth and block
        /// size, and never touches `CityLayout.Roads` at all — the same shape of gap as suburban
        /// lanes not coming from `CityLayout.Roads` either, just not noticed until this render
        /// showed a diagonal highway crossing streets it apparently had nothing to do with.
        /// </summary>
        [MenuItem("Scaling Laws/Tile grid streets (downtown etc.)")]
        public static void TileGridStreets()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Scaling Laws] No City.unity to tile grid streets in.");
                return;
            }

            if (CityRoadNetwork.Supersedes("Scaling Laws"))
            {
                return;
            }

            var tile = AssetDatabase.LoadAssetAtPath<GameObject>(TilePath);
            if (tile == null)
            {
                Debug.LogError($"[Scaling Laws] Could not load {TilePath}.");
                return;
            }

            var cityRoot = GameObject.Find("City");
            var existing = GameObject.Find("GridStreetTiles");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var parent = new GameObject("GridStreetTiles").transform;
            if (cityRoot != null)
            {
                parent.SetParent(cityRoot.transform, false);
            }

            // Same step as the main roads. These lines are dead straight so a longer tile would do
            // no harm here, but one number is easier to reason about than two.
            var step = RoadTileLength;
            var placed = 0;

            foreach (var grid in CityBlocks.Grids)
            {
                var group = new GameObject($"Grid_{grid.Id}").transform;
                group.SetParent(parent, true);

                var angle = grid.RotationDegrees * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);

                var columns = Mathf.Max(1, Mathf.FloorToInt(grid.Width / grid.BlockSize));
                var rows = Mathf.Max(1, Mathf.FloorToInt(grid.Depth / grid.BlockSize));
                var centre = new Vector2(grid.CentreX, grid.CentreZ);

                for (var column = 0; column <= columns; column++)
                {
                    var offset = -grid.Width * 0.5f + grid.BlockSize * column;
                    var line = centre + across * offset;

                    placed += TileStraightLine(group, tile,
                        line - along * grid.Depth * 0.5f, line + along * grid.Depth * 0.5f, step);
                }

                for (var row = 0; row <= rows; row++)
                {
                    var offset = -grid.Depth * 0.5f + grid.BlockSize * row;
                    var line = centre + along * offset;

                    placed += TileStraightLine(group, tile,
                        line - across * grid.Width * 0.5f, line + across * grid.Width * 0.5f, step);
                }
            }

            var removed = RemoveOldSurfaceUnderTiles(parent);

            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Scaling Laws] {placed} grid-street tiles placed across "
                + $"{CityBlocks.Grids.Count} districts, {removed} old grey boxes removed.");
        }

        /// <summary>One straight run, tiled in fixed steps — no smoothing needed, the line already is one.</summary>
        private static int TileStraightLine(Transform parent, GameObject tile, Vector2 from, Vector2 to,
            float step)
        {
            var length = Vector2.Distance(from, to);

            if (length < 0.01f)
            {
                return 0;
            }

            var direction = (to - from) / length;
            var widthScale = GridStreetWidth / TileSize;
            var lengthScale = step / TileSize;
            var placed = 0;

            for (var travelled = step * 0.5f; travelled < length; travelled += step)
            {
                var at = from + direction * travelled;
                var height = CityTerrainBuilder.HeightAt(at.x, at.y);

                // BuildGrid's own StreetStrip skips pieces below the waterline, which is what gives
                // a district its ragged edge without anybody drawing one — matched here, or a tile
                // lands somewhere the street it is replacing was never actually drawn.
                if (height < CityLayout.SeaLevel + 1.5f)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(tile, parent);
                instance.transform.position = new Vector3(at.x, height, at.y);
                instance.transform.rotation =
                    Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up) * AlongX;
                instance.transform.localScale = new Vector3(lengthScale, 1f, widthScale);

                placed++;
            }

            return placed;
        }

        private const string CrossroadPath =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/road-crossroad.fbx";

        /// <summary>
        /// Finds every point where two different roads' smoothed centrelines actually cross, and
        /// drops a real crossroad tile there instead of two straight tiles overlapping at whatever
        /// angle they happen to meet at.
        ///
        /// **Geometric, not looked up.** Nothing in <see cref="CityLayout"/> names which roads cross
        /// which — the data is centrelines, not a junction list — so this walks every pair of roads'
        /// point-to-point segments and tests each pair against each other, the standard way to find
        /// where two polylines meet. Run after <see cref="TileAll"/>, never before: it removes
        /// straight tiles near a crossing, and there is nothing to remove on an untiled road.
        /// </summary>
        [MenuItem("Scaling Laws/Tile intersections")]
        public static void TileIntersections()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Scaling Laws] No City.unity to tile intersections in.");
                return;
            }

            if (CityRoadNetwork.Supersedes("Scaling Laws"))
            {
                return;
            }

            var crossroadTile = AssetDatabase.LoadAssetAtPath<GameObject>(CrossroadPath);
            if (crossroadTile == null)
            {
                Debug.LogError($"[Scaling Laws] Could not load {CrossroadPath}.");
                return;
            }

            var tilesRoot = GameObject.Find("RoadTiles");
            if (tilesRoot == null)
            {
                Debug.LogError("[Scaling Laws] No \"RoadTiles\" group. Run Tile roads (all "
                    + "highways and streets) first.");
                return;
            }

            var existing = GameObject.Find("RoadCrossings");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var crossingsGroup = new GameObject("RoadCrossings").transform;
            crossingsGroup.SetParent(tilesRoot.transform.parent, false);

            var centrelines = CityTerrainBuilder.RoadCentrelines();
            var roads = CityLayout.Roads;

            var roadIndices = new List<int>();
            for (var index = 0; index < roads.Count; index++)
            {
                if (roads[index].Class != RoadClass.Lane)
                {
                    roadIndices.Add(index);
                }
            }

            var crossings = new List<(Vector2 At, float Width)>();

            for (var a = 0; a < roadIndices.Count; a++)
            {
                for (var b = a + 1; b < roadIndices.Count; b++)
                {
                    var pointsA = centrelines[roadIndices[a]].Points;
                    var pointsB = centrelines[roadIndices[b]].Points;
                    var width = Mathf.Max(roads[roadIndices[a]].Width, roads[roadIndices[b]].Width);

                    for (var i = 0; i < pointsA.Count - 1; i++)
                    {
                        for (var j = 0; j < pointsB.Count - 1; j++)
                        {
                            if (SegmentsIntersect(pointsA[i], pointsA[i + 1], pointsB[j], pointsB[j + 1],
                                out var at))
                            {
                                crossings.Add((at, width));
                            }
                        }
                    }
                }
            }

            // Two curves rarely cross exactly once as two straight-segment sweeps see it — a shared
            // stretch of nearly-parallel road can register several points a metre apart. One tile
            // per real crossing: merge anything within half a tile of a crossing already kept.
            var merged = new List<(Vector2 At, float Width)>();
            foreach (var crossing in crossings)
            {
                var isNew = true;
                for (var index = 0; index < merged.Count; index++)
                {
                    if (Vector2.Distance(merged[index].At, crossing.At) < merged[index].Width * 0.5f)
                    {
                        isNew = false;
                        break;
                    }
                }

                if (isNew)
                {
                    merged.Add(crossing);
                }
            }

            var placedCount = 0;

            foreach (var (at, width) in merged)
            {
                var height = CityTerrainBuilder.HeightAt(at.x, at.y);
                var scale = width / TileSize;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(crossroadTile, crossingsGroup);
                instance.transform.position = new Vector3(at.x, height, at.y);
                instance.transform.localScale = new Vector3(scale, 1f, scale);
                placedCount++;

                RemoveTilesNear(tilesRoot.transform, at, width * 0.62f);
            }

            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Scaling Laws] {placedCount} intersections tiled "
                + $"({crossings.Count} raw crossing points merged down to {merged.Count}).");
        }

        private static void RemoveTilesNear(Transform tilesRoot, Vector2 at, float radius)
        {
            var toRemove = new List<GameObject>();

            foreach (Transform roadGroup in tilesRoot)
            {
                foreach (Transform tileInstance in roadGroup)
                {
                    var position = new Vector2(tileInstance.position.x, tileInstance.position.z);
                    if (Vector2.Distance(position, at) <= radius)
                    {
                        toRemove.Add(tileInstance.gameObject);
                    }
                }
            }

            foreach (var go in toRemove)
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Standard 2D segment intersection, both parameters clamped to the segment itself.</summary>
        private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4,
            out Vector2 point)
        {
            point = Vector2.zero;

            var d1 = p2 - p1;
            var d2 = p4 - p3;
            var denom = d1.x * d2.y - d1.y * d2.x;

            if (Mathf.Abs(denom) < 1e-6f)
            {
                return false;
            }

            var t = ((p3.x - p1.x) * d2.y - (p3.y - p1.y) * d2.x) / denom;
            var u = ((p3.x - p1.x) * d1.y - (p3.y - p1.y) * d1.x) / denom;

            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                return false;
            }

            point = p1 + t * d1;
            return true;
        }

        private static void Run(string onlyRoadId)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Scaling Laws] No City.unity to tile roads in.");
                return;
            }

            if (CityRoadNetwork.Supersedes("Scaling Laws"))
            {
                return;
            }

            var tile = AssetDatabase.LoadAssetAtPath<GameObject>(TilePath);
            if (tile == null)
            {
                Debug.LogError($"[Scaling Laws] Could not load {TilePath}.");
                return;
            }

            var cityRoot = GameObject.Find("City");
            var groupName = onlyRoadId == null ? "RoadTiles" : $"RoadTiles_{onlyRoadId}";

            // Re-running this is meant to be safe: without this, a second pass adds a second set of
            // tiles on top of the first rather than replacing it, because nothing here otherwise
            // remembers that a group with this name already exists.
            var existing = GameObject.Find(groupName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var parent = new GameObject(groupName).transform;

            if (cityRoot != null)
            {
                parent.SetParent(cityRoot.transform, false);
            }

            var centrelines = CityTerrainBuilder.RoadCentrelines();
            var roads = CityLayout.Roads;

            var placed = 0;
            var skippedLanes = 0;

            for (var index = 0; index < roads.Count; index++)
            {
                var run = roads[index];

                if (run.Class == RoadClass.Lane)
                {
                    skippedLanes++;
                    continue;
                }

                if (onlyRoadId != null && run.Id != onlyRoadId)
                {
                    continue;
                }

                placed += TileOneRoad(parent, centrelines[index], run, tile);
            }

            var removed = RemoveOldSurfaceUnderTiles(parent);

            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Scaling Laws] {placed} road tiles placed, {removed} old grey road boxes "
                + "removed from under them"
                + (onlyRoadId != null ? $" on \"{onlyRoadId}\"." : $", {skippedLanes} lane runs skipped (subdivision draws those)."));
        }

        /// <summary>
        /// <summary>
        /// **Square tiles, stepped by the road's own width — corrected 2026-09-16.** An earlier
        /// version capped every tile's length at a fixed 6m to fight a curve seam (see the note
        /// below), which for a 26m highway meant a tile 26m wide and only 6m long: four times wider
        /// than it runs. Laid one after another that reads as a ladder of crossbars, not a road —
        /// exactly what it was reported as. A road tile has to be at least as long as it is wide, or
        /// it stops looking like a strip of road at all, and that matters more than the curve seam
        /// the short step was fighting.
        ///
        /// **The curve seam this trades back in.** A square tile scaled to a 26m highway is a
        /// 26m-long rigid plate; the smoothed centreline can turn enough over 26m that two square
        /// plates no longer meet edge to edge. Measured and accepted the first time this exact
        /// tradeoff was made (the original "spine" test, before the 6m cap existed): a barely
        /// visible notch at one sharp point, not a systemic problem. Kenney's own curve tiles are
        /// not the fix — they are built for a fixed grid with entries on cardinal sides, and this
        /// road is a free curve, not a grid cell.
        /// </summary>
        private static int TileOneRoad(Transform parent, CityTerrainBuilder.Centreline centreline,
            RoadRun run, GameObject tile)
        {
            var group = new GameObject($"Road_{run.Id}").transform;
            group.SetParent(parent, true);

            var points = centreline.Points;
            var width = run.Width;
            var widthScale = width / TileSize;
            var step = RoadTileLength;
            var lengthScale = step / TileSize;

            var travelled = 0f;
            var nextTile = step * 0.5f;
            var placed = 0;

            for (var index = 0; index < points.Count - 1; index++)
            {
                var a = points[index];
                var b = points[index + 1];
                var segment = Vector2.Distance(a, b);

                if (segment < 0.01f)
                {
                    continue;
                }

                var direction = (b - a) / segment;

                while (travelled + segment >= nextTile)
                {
                    var t = (nextTile - travelled) / segment;
                    var at = Vector2.Lerp(a, b, t);
                    var heightT = (index + t) / (points.Count - 1);
                    var height = centreline.HeightAt(heightT);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(tile, group);
                    instance.transform.position = new Vector3(at.x, height, at.y);
                    instance.transform.rotation =
                        Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up) * AlongX;
                    instance.transform.localScale = new Vector3(lengthScale, 1f, widthScale);

                    placed++;
                    nextTile += step;
                }

                travelled += segment;
            }

            return placed;
        }

        /// <summary>
        /// Deletes every `RoadSegment` box within one tile-width of a real tile just placed.
        ///
        /// **Position, not identity.** `CityProp` on a road box carries no road id — every segment
        /// from every run was described with an empty district and no other handle — so the only way
        /// to say "the box this tile replaced" is where the two actually sit. A box within half its
        /// own width of a tile centre is that tile's box and nothing on a different road happens to
        /// land there, because the survey that placed both agrees on where the road is.
        /// </summary>
        private static int RemoveOldSurfaceUnderTiles(Transform tilesRoot)
        {
            var tilePositions = new List<(Vector3 Position, float Reach)>();

            foreach (Transform roadGroup in tilesRoot)
            {
                foreach (Transform tileInstance in roadGroup)
                {
                    var reach = tileInstance.localScale.x * TileSize * 0.75f;
                    tilePositions.Add((tileInstance.position, reach));
                }
            }

            var removed = 0;

            foreach (var prop in Object.FindObjectsByType<CityProp>(FindObjectsSortMode.None))
            {
                if (prop.Kind != CityPropKind.RoadSegment)
                {
                    continue;
                }

                foreach (var (position, reach) in tilePositions)
                {
                    if (Vector3.Distance(prop.transform.position, position) <= reach)
                    {
                        Object.DestroyImmediate(prop.gameObject);
                        removed++;
                        break;
                    }
                }
            }

            return removed;
        }
    }
}
