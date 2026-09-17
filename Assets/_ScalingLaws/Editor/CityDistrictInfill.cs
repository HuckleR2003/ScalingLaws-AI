using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Fills the empty ground inside every district block with buildings that face the street.
    ///
    /// **A city block is a ring of frontage, not a clump in the middle.** `CityDressingBuilder`
    /// drops one to three buildings near each block's centre and leaves the rest to grass, so the
    /// districts read as towers standing in a field with roads going past them. Real blocks are
    /// built out to the pavement on all four sides, and the inside of the block is what is left
    /// over — the opposite arrangement. This lays that frontage.
    ///
    /// **Nothing is placed without checking what is already there.** Every renderer already in the
    /// scene — roads, bridges, driveways, the buildings the original pass placed — goes into a
    /// grid of buckets first, and a candidate that would overlap any of them is dropped. That is
    /// what keeps an infill pass from being the thing that introduces the overlaps it was supposed
    /// to be tidying up, and it is why this can be run again after anything else moves.
    /// </summary>
    public static class CityDistrictInfill
    {
        private const string Commercial =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        /// <summary>Kerb to kerb of a district street, copied from `CityDressingBuilder.BuildGrid`.</summary>
        private const float StreetWidth = 17f;

        /// <summary>
        /// Kerb strip plus the gap a building keeps behind it.
        ///
        /// The pavement boxes sit from 8.5m to about 10.1m off the centreline, so anything under
        /// about two metres here puts a wall through a kerb.
        /// </summary>
        private const float Setback = 3.5f;

        /// <summary>
        /// Frontage width of one infill building.
        ///
        /// Narrow on purpose: a block edge holds three of these and only two of a wider one, and a
        /// street with three doors on it reads as a street where a street with two reads as a gap.
        /// </summary>
        private const float FrontageWidth = 20f;

        /// <summary>How far back from the street an infill building reaches.</summary>
        private const float BuildingDepth = 16f;

        /// <summary>Gap between neighbours on the same frontage.</summary>
        private const float Shoulder = 3f;

        private static int Tried;
        private static int RejectedWet;
        private static int RejectedBlocked;
        private static int RejectedNoStreet;
        private static int Corners;
        private static int Inner;

        /// <summary>Frontage of a corner building: narrower than a run's, so it fits between the last doors of two runs.</summary>
        private const float CornerWidth = 13f;

        /// <summary>The yard left between the frontage and the second row behind it.</summary>
        private const float InnerYard = 2f;

        [MenuItem("Scaling Laws/Fill district blocks with frontage")]
        public static void Run()
        {
            Tried = 0;
            RejectedWet = 0;
            RejectedBlocked = 0;
            RejectedNoStreet = 0;
            Corners = 0;
            Inner = 0;

            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Infill] No City.unity.");
                return;
            }

            var cityRoot = GameObject.Find("City");
            var existing = GameObject.Find("DistrictInfill");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var occupied = new Occupancy();
            occupied.TakeSceneAsItStands();

            var root = new GameObject("DistrictInfill").transform;
            if (cityRoot != null)
            {
                root.SetParent(cityRoot.transform, false);
            }

            var random = new System.Random(20260916);
            var placed = 0;

            foreach (var grid in CityBlocks.Grids)
            {
                var group = new GameObject($"Infill_{grid.Id}").transform;
                group.SetParent(root, false);

                placed += FillGrid(grid, group, occupied, random);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Infill] {placed} frontage buildings placed across {CityBlocks.Grids.Count} districts. "
                + $"{Tried} spots considered: {RejectedWet} too low, {RejectedBlocked} already occupied, "
                + $"{RejectedNoStreet} facing no street. Of those placed, {Corners} on block corners and {Inner} in second rows.");
        }

        private static int FillGrid(GridBlock grid, Transform group, Occupancy occupied,
            System.Random random)
        {
            var angle = grid.RotationDegrees * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-along.y, along.x);
            var centre = new Vector2(grid.CentreX, grid.CentreZ);

            var columns = Mathf.Max(1, Mathf.FloorToInt(grid.Width / grid.BlockSize));
            var rows = Mathf.Max(1, Mathf.FloorToInt(grid.Depth / grid.BlockSize));

            var models = PoolFor(grid);
            var placed = 0;

            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    var cellCentre = centre
                        + across * (-grid.Width * 0.5f + grid.BlockSize * (column + 0.5f))
                        + along * (-grid.Depth * 0.5f + grid.BlockSize * (row + 0.5f));

                    // Where the backs of the buildings sit: half a block, less the carriageway, the
                    // pavement, and half the building's own depth.
                    var frontage = grid.BlockSize * 0.5f
                        - (StreetWidth * 0.5f + Setback + BuildingDepth * 0.5f);

                    if (frontage <= 0f)
                    {
                        continue;
                    }

                    // The run stops where the frontage turning the corner begins, so the two do not
                    // try to stand on the same piece of ground.
                    var runHalf = grid.BlockSize * 0.5f
                        - (StreetWidth * 0.5f + Setback + BuildingDepth * 0.5f);

                    if (runHalf <= FrontageWidth * 0.5f)
                    {
                        continue;
                    }

                    foreach (var side in new[] { 0, 1, 2, 3 })
                    {
                        var outward = side switch
                        {
                            0 => across,
                            1 => -across,
                            2 => along,
                            _ => -along
                        };

                        var sideways = new Vector2(-outward.y, outward.x);

                        var slots = Mathf.Max(1,
                            Mathf.FloorToInt(runHalf * 2f / (FrontageWidth + Shoulder)));
                        var step = runHalf * 2f / slots;

                        for (var slot = 0; slot < slots; slot++)
                        {
                            var offset = -runHalf + step * (slot + 0.5f);
                            var at = cellCentre + outward * frontage + sideways * offset;

                            if (TryPlace(at, outward, models, group, occupied, random))
                            {
                                placed++;
                            }
                        }
                    }

                    // Sheds stand on their own yards: no corners and no second row in a district
                    // of halls.
                    if (grid.HighBuilding <= 25f)
                    {
                        continue;
                    }

                    // The corners. The four frontage runs stop short of each other, and every block
                    // was left with an empty square at each corner; a narrower building fits it,
                    // pushed out to the kerb so it clears the last door of both runs.
                    var corner = frontage + (BuildingDepth - CornerWidth) * 0.5f;

                    foreach (var (first, second) in new[] { (across, along), (across, -along), (-across, along), (-across, -along) })
                    {
                        if (TryPlace(cellCentre + first * corner + second * corner, first, models, group, occupied,
                                random, CornerWidth, CornerWidth * 0.4f))
                        {
                            placed++;
                            Corners++;
                        }
                    }

                    // A second row behind the frontage where the block is deep enough for a yard in
                    // between: one building behind each side, backing onto the frontage, the four
                    // standing round a courtyard in the middle of the block.
                    var inner = frontage - BuildingDepth - InnerYard;

                    if (inner < BuildingDepth * 0.5f + FrontageWidth * 0.5f)
                    {
                        continue;
                    }

                    foreach (var outward in new[] { across, -across, along, -along })
                    {
                        if (TryPlace(cellCentre + outward * inner, outward, models, group, occupied, random,
                                facesStreet: false))
                        {
                            placed++;
                            Inner++;
                        }
                    }
                }
            }

            return placed;
        }

        private static bool TryPlace(Vector2 at, Vector2 facing, IReadOnlyList<GameObject> models,
            Transform group, Occupancy occupied, System.Random random, float frontageWidth = FrontageWidth,
            float reachOverride = 0f, bool facesStreet = true)
        {
            Tried++;

            var height = CityTerrainBuilder.HeightAt(at.x, at.y);

            // Nothing is built below the waterline, the same rule every other builder here follows.
            if (height < CityLayout.SeaLevel + 2f)
            {
                RejectedWet++;
                return false;
            }

            var width = frontageWidth * (0.82f + (float)random.NextDouble() * 0.3f);

            // Checked on the building's depth, not on the square of its longest side. A frontage
            // building is wide and shallow; testing it as a square pushes its imaginary corners
            // across the pavement and into the kerb strip, which rejected almost everything on the
            // first two runs of this pass.
            //
            // Its neighbours along the same frontage need no test at all — they are laid out a
            // fixed step apart, so they cannot reach each other by construction.
            var reach = reachOverride > 0f ? reachOverride : BuildingDepth * 0.5f * 0.9f;

            if (!occupied.IsFree(at, reach))
            {
                RejectedBlocked++;
                return false;
            }

            // Frontage faces a street that is actually there. The grid's own numbers say where its
            // streets were drawn; the road network decides which were laid — the port's grid gave
            // way to one street behind its halls, and a grid street beside a highway was dropped —
            // and a shop front onto grass is not frontage.
            // Within a highway's width rather than a street's: where a grid's own street beside a highway
            // was dropped, its frontage faces the highway instead, and that is still a street front.
            if (facesStreet && !occupied.NearRoad(at + facing * (BuildingDepth * 0.5f + Setback + StreetWidth * 0.5f), StreetWidth * 1.6f))
            {
                RejectedNoStreet++;
                return false;
            }

            var radius = Mathf.Max(width, BuildingDepth) * 0.5f;

            var model = models[random.Next(models.Count)];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, group);

            instance.transform.position = new Vector3(at.x, height, at.y);
            instance.transform.rotation =
                Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);

            if (!TryMeasure(model, out var size) || size.x <= 0.001f)
            {
                Object.DestroyImmediate(instance);
                return false;
            }

            // Scaled uniformly to the frontage it has to fill. Uniform on purpose: squashing one
            // axis of a building to fit a plot is how a tower ends up looking sat on.
            var factor = width / size.x;
            instance.transform.localScale = Vector3.one * factor;

            // Sit it on the ground by its own measured base, because a model's pivot is its
            // author's business, not ours — the same lesson the first asset swap had to learn.
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                instance.transform.position += Vector3.up * (height - bounds.min.y);
            }

            occupied.Take(at, radius);
            return true;
        }

        /// <summary>
        /// Which models suit a district, read from what the district says about itself rather than
        /// from a list naming districts — a new district then gets sensible buildings for free.
        /// </summary>
        private static IReadOnlyList<GameObject> PoolFor(GridBlock grid)
        {
            string[] names;

            if (grid.Skyline)
            {
                names = new[]
                {
                    "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
                    "building-skyscraper-e", "building-l", "building-m", "building-i", "building-j"
                };
            }
            else if (grid.HighBuilding <= 25f)
            {
                // Low and wide: the port and anywhere else that is sheds rather than offices.
                names = new[] { "building-e", "building-c", "building-k", "building-a" };
            }
            else
            {
                names = new[]
                {
                    "building-a", "building-b", "building-d", "building-h",
                    "building-i", "building-j", "building-k", "building-l"
                };
            }

            var loaded = new List<GameObject>();

            foreach (var name in names)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Commercial + name + ".fbx");

                if (model != null)
                {
                    loaded.Add(model);
                }
            }

            return loaded;
        }

        private static bool TryMeasure(GameObject model, out Vector3 size)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);

            if (renderers.Length == 0)
            {
                size = Vector3.zero;
                return false;
            }

            var bounds = renderers[0].localBounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].localBounds);
            }

            size = bounds.size;
            return true;
        }

        /// <summary>
        /// What ground is already spoken for, in buckets, so asking is cheap enough to ask for
        /// every candidate.
        ///
        /// **Rectangles, not circles.** A circle round the longest dimension of a 460 metre kerb
        /// strip claims a quarter of the district; the first run of this pass placed 81 buildings
        /// because of it. A footprint is a rectangle and the honest test is against the rectangle.
        /// </summary>
        private sealed class Occupancy
        {
            private const float Cell = 40f;

            private readonly Dictionary<(int, int), List<Rect>> buckets = new();
            private readonly Dictionary<(int, int), List<Vector2>> roads = new();

            /// <summary>
            /// Everything already standing becomes an obstacle: roads, bridges, driveways, the
            /// buildings placed before this pass, the lot. Read off the scene rather than rebuilt
            /// from the data that drew it, so anything added by hand counts too.
            /// </summary>
            public void TakeSceneAsItStands()
            {
                var taken = 0;

                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    // The terrain is the ground, not an obstacle on it, and the flat rings under the
                    // map pins are paint rather than anything a building could hit.
                    if (renderer.GetComponent<Terrain>() != null)
                    {
                        continue;
                    }

                    var name = renderer.gameObject.name;
                    if (name is "Ring" or "Pin" or "PinHead" or "Water")
                    {
                        continue;
                    }

                    var bounds = renderer.bounds;

                    if (name.StartsWith("road-"))
                    {
                        var key = (Mathf.FloorToInt(bounds.center.x / Cell), Mathf.FloorToInt(bounds.center.z / Cell));
                        if (!roads.TryGetValue(key, out var tiles))
                        {
                            tiles = new List<Vector2>();
                            roads[key] = tiles;
                        }

                        tiles.Add(new Vector2(bounds.center.x, bounds.center.z));
                    }

                    // Something paper-thin and enormous is ground paint, not a thing in the way.
                    if (bounds.size.y < 0.3f && Mathf.Max(bounds.size.x, bounds.size.z) > 60f)
                    {
                        continue;
                    }

                    Take(new Rect(
                        bounds.center.x - bounds.extents.x * 0.85f,
                        bounds.center.z - bounds.extents.z * 0.85f,
                        bounds.size.x * 0.85f,
                        bounds.size.z * 0.85f));

                    taken++;
                }

                Debug.Log($"[Infill] {taken} existing objects counted as occupied ground.");
            }

            /// <summary>True when a road piece stands within the given distance of a point.</summary>
            public bool NearRoad(Vector2 at, float distance)
            {
                foreach (var key in Keys(new Rect(at.x - distance, at.y - distance, distance * 2f, distance * 2f)))
                {
                    if (roads.TryGetValue(key, out var tiles) && tiles.Any(tile => Vector2.Distance(tile, at) <= distance))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Take(Vector2 at, float radius) =>
                Take(new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f));

            public void Take(Rect rect)
            {
                foreach (var key in Keys(rect))
                {
                    if (!buckets.TryGetValue(key, out var list))
                    {
                        list = new List<Rect>();
                        buckets[key] = list;
                    }

                    list.Add(rect);
                }
            }

            /// <summary>True when a square footprint of this size, centred here, touches nothing.</summary>
            public bool IsFree(Vector2 at, float radius)
            {
                var candidate = new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f);

                foreach (var key in Keys(candidate))
                {
                    if (!buckets.TryGetValue(key, out var list))
                    {
                        continue;
                    }

                    foreach (var rect in list)
                    {
                        if (rect.Overlaps(candidate))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private static IEnumerable<(int, int)> Keys(Rect rect)
            {
                var minX = Mathf.FloorToInt(rect.xMin / Cell);
                var maxX = Mathf.FloorToInt(rect.xMax / Cell);
                var minY = Mathf.FloorToInt(rect.yMin / Cell);
                var maxY = Mathf.FloorToInt(rect.yMax / Cell);

                for (var x = minX; x <= maxX; x++)
                {
                    for (var y = minY; y <= maxY; y++)
                    {
                        yield return (x, y);
                    }
                }
            }
        }
    }
}
