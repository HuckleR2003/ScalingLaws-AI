using System.Collections.Generic;
using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The land Bayview sits on: hills, coast, bay, river, and a levelled pad under every district.
    ///
    /// **Second version, drawn against the author's reference rather than invented.** The first was
    /// eight circles and two straight channels, and the render said so. What changed:
    ///
    /// Water is a spline carrying a half-width and a depth at every control point, so the bay opens
    /// into a basin at its mouth and narrows where the bridges cross it, and the river thins as it
    /// climbs into the hills. Straight segments of constant width are what made the first pass read
    /// as two canals.
    ///
    /// The coast is pushed about by three octaves of noise before anything is measured against it,
    /// so the shoreline has inlets and headlands rather than being an offset curve.
    ///
    /// Roads are cut into the heightmap as shallow shelves rather than laid on top, which is what
    /// stops a highway floating over a valley.
    ///
    /// Deterministic from a fixed seed: two runs produce the same coastline.
    /// </summary>
    public static class CityTerrainBuilder
    {
        private const string ScenesFolder = "Assets/_ScalingLaws/Scenes";
        private const string DataFolder = "Assets/_ScalingLaws/Terrain";

        public const string ScenePath = ScenesFolder + "/City.unity";
        private const string TerrainDataPath = DataFolder + "/BayviewTerrain.asset";

        private const int Seed = 20260816;

        /// <summary>Flat ground kept past a district's radius before the land starts to move.</summary>
        private const float PadMargin = 55f;

        /// <summary>How far past the pad the ground eases back to whatever it would have been.</summary>
        private const float BlendWidth = 190f;

        /// <summary>Metres of shoulder either side of a road that is levelled with it.</summary>
        private const float RoadShoulder = 9f;

        /// <summary>How far past the shoulder a road's cutting blends out.</summary>
        private const float RoadBlend = 34f;

        [MenuItem("Scaling Laws/Build the city")]
        public static void Build()
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(DataFolder);

            centrelines = null;

            var data = BuildTerrainData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaling Laws] Bayview terrain: {CityLayout.Size}m square, "
                + $"{CityLayout.Districts.Count} districts, {CityLayout.Roads.Count} roads, "
                + $"{CityLayout.Bridges.Count} bridges.");

            CityDressingBuilder.BuildScene(data, ScenePath, HeightAt);
        }

        // ---- the heightmap -------------------------------------------------------------------------

        private static TerrainData BuildTerrainData()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);

            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = CityLayout.HeightmapResolution;
            data.size = new Vector3(CityLayout.Size, CityLayout.Height, CityLayout.Size);
            data.SetDetailResolution(512, 16);

            var resolution = data.heightmapResolution;
            var heights = new float[resolution, resolution];

            // Sampled once and reused by the splatmap: the height is the expensive part, and the two
            // passes have to agree about where the water is or the sand lands in the sea.
            var metres = new float[resolution, resolution];

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var worldX = x / (float)(resolution - 1) * CityLayout.Size;
                    var worldZ = y / (float)(resolution - 1) * CityLayout.Size;

                    var height = HeightAt(worldX, worldZ);
                    metres[y, x] = height;

                    // Heightmap is indexed [z, x]. The one thing about Unity terrain that catches
                    // everybody exactly once.
                    heights[y, x] = Mathf.Clamp01(height / CityLayout.Height);
                }
            }

            data.SetHeights(0, 0, heights);
            data.terrainLayers = Layers();
            PaintSplat(data, metres);

            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// The land, in the order the ground was actually made.
        ///
        /// Hills, then the pads levelled into them, then the roads cut across, and the water carved
        /// through everything last. **Water wins**, because a district that ends up wet is a layout
        /// mistake to fix by moving the district, not by quietly filling in the sea underneath it.
        /// </summary>
        public static float HeightAt(float x, float z)
        {
            var height = NaturalHeight(x, z);
            height = FlattenDistricts(x, z, height);
            height = CutRoads(x, z, height);
            return CarveWater(x, z, height);
        }

        /// <summary>
        /// Hills around the north and east, a coastal shelf falling away south and west.
        ///
        /// The ridge is a bent radial rather than a plane, so the high ground curls around the city
        /// the way it does on the reference instead of running off one corner. Value noise from a
        /// fixed hash rather than Mathf.PerlinNoise, which is not promised identical across Unity
        /// versions — terrain that changes shape on an upgrade is terrain whose districts no longer
        /// sit on their pads.
        /// </summary>
        private static float NaturalHeight(float x, float z)
        {
            var u = x / CityLayout.Size;
            var v = z / CityLayout.Size;

            var arc = Mathf.Sqrt((u - 0.06f) * (u - 0.06f) * 0.72f + (v - 0.10f) * (v - 0.10f));
            var ridge = Mathf.Clamp01((arc - 0.46f) / 0.58f);

            var basement = CityLayout.SeaLevel + 10f + ridge * ridge * 330f;

            var rolling =
                Noise(u * 2.3f + 11.7f, v * 2.3f + 4.1f) * 42f
                + Noise(u * 5.9f, v * 5.9f) * 17f
                + Noise(u * 13.1f, v * 13.1f) * 6f;

            // Foothills only bite where the ridge already is, so the coastal shelf stays flat.
            return basement + rolling * (0.35f + 0.65f * ridge);
        }

        /// <summary>
        /// Levels each district and eases the ground back into the hills around it.
        ///
        /// Smoothstep rather than a straight ramp: a plateau with a conical skirt reads as a mesa,
        /// and one that eases out reads as a valley floor.
        /// </summary>
        private static float FlattenDistricts(float x, float z, float height)
        {
            foreach (var district in CityLayout.Districts)
            {
                var distance = Distance(x, z, district.CentreX, district.CentreZ);
                var pad = district.Radius + PadMargin;

                if (distance <= pad)
                {
                    return district.GroundHeight;
                }

                if (distance <= pad + BlendWidth)
                {
                    var t = Mathf.SmoothStep(1f, 0f, (distance - pad) / BlendWidth);
                    height = Mathf.Lerp(height, district.GroundHeight, t);
                }
            }

            return height;
        }

        /// <summary>
        /// Cuts every road into the ground as a shallow shelf.
        ///
        /// The shelf takes the height of its own smoothed centreline, so a highway between districts
        /// at different heights climbs rather than stepping. Without this the roads are painted
        /// stripes floating over hills, which is the fastest way to make a generated city look it.
        /// </summary>
        private static float CutRoads(float x, float z, float height)
        {
            foreach (var road in RoadCentrelines())
            {
                var half = road.Width * 0.5f + RoadShoulder;
                var along = NearestOnPolyline(x, z, road.Points, out var distance);

                if (distance > half + RoadBlend)
                {
                    continue;
                }

                var surface = road.HeightAt(along);

                if (distance <= half)
                {
                    height = surface;
                    continue;
                }

                var t = Mathf.SmoothStep(1f, 0f, (distance - half) / RoadBlend);
                height = Mathf.Lerp(height, surface, t);
            }

            return height;
        }

        /// <summary>
        /// The bay and the river, cut through everything else.
        ///
        /// Half-width and depth are carried per control point and interpolated along the run, so the
        /// bay is a basin at the mouth and a channel at the bridges. The banks are noised before the
        /// distance is measured, which gives the coast inlets and headlands rather than a clean
        /// offset curve.
        /// </summary>
        private static float CarveWater(float x, float z, float height)
        {
            var deepest = 0f;
            var bed = height;

            foreach (var run in CityLayout.Water)
            {
                if (!NearestOnWater(x, z, run, out var distance, out var halfWidth, out var depth))
                {
                    continue;
                }

                var wobble =
                    (Noise(x * 0.0042f + 31.7f, z * 0.0042f + 12.3f) - 0.5f) * 96f
                    + (Noise(x * 0.0115f, z * 0.0115f) - 0.5f) * 42f
                    + (Noise(x * 0.031f + 7.1f, z * 0.031f + 3.3f) - 0.5f) * 14f;

                var edge = Mathf.Max(12f, halfWidth + wobble);

                if (distance > edge)
                {
                    continue;
                }

                // One at the centreline, zero at the bank, smoothed so the bed is a dish rather
                // than a trough with vertical sides.
                var across = Mathf.Clamp01(1f - distance / edge);
                var strength = across * across * (3f - 2f * across);

                if (strength <= deepest)
                {
                    continue;
                }

                deepest = strength;
                bed = CityLayout.SeaLevel - depth * strength;
            }

            return deepest <= 0f ? height : Mathf.Min(height, bed);
        }

        // ---- the splatmap -----------------------------------------------------------------------

        /// <summary>
        /// Six surfaces, chosen by what the ground is doing rather than by where it is.
        ///
        /// Steep is rock, near the waterline is sand, high and gentle is forest, a road is asphalt,
        /// a district is concrete. That way the painting cannot disagree with the shape, which is
        /// what happens the moment anybody paints to a hand-drawn mask.
        /// </summary>
        private static void PaintSplat(TerrainData data, float[,] metres)
        {
            var resolution = CityLayout.SplatResolution;
            data.alphamapResolution = resolution;

            var layers = data.terrainLayers.Length;
            var map = new float[resolution, resolution, layers];

            var step = CityLayout.Size / (resolution - 1);
            var heightStep = CityLayout.Size / (CityLayout.HeightmapResolution - 1);

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var worldX = x * step;
                    var worldZ = y * step;

                    var height = SampleMetres(metres, worldX, worldZ);
                    var slope = SlopeAt(metres, worldX, worldZ, heightStep);

                    var weights = new float[layers];
                    weights[1] = 1f;

                    var aboveSea = height - CityLayout.SeaLevel;
                    if (aboveSea > -6f && aboveSea < 9f)
                    {
                        weights[0] = Mathf.SmoothStep(1.4f, 0f, Mathf.Abs(aboveSea) / 9f);
                    }

                    if (slope > 0.18f)
                    {
                        weights[2] = Mathf.Clamp01((slope - 0.18f) / 0.38f) * 1.8f;
                    }

                    if (height > 140f && slope < 0.34f)
                    {
                        weights[3] = Mathf.Clamp01((height - 140f) / 120f) * 1.2f;
                    }

                    // **No concrete disc under a district.**
                    //
                    // The first render painted every district as a grey circle and that is exactly
                    // what it looked like: eight coasters on a lawn. A real district is read from
                    // its roads and its buildings, not from a stain on the ground. What is left is
                    // a faint dusting that only bites inside the pad, broken up by noise so its
                    // edge is never a circle.
                    foreach (var district in CityLayout.Districts)
                    {
                        var distance = Distance(worldX, worldZ, district.CentreX, district.CentreZ);
                        var pad = district.Radius + PadMargin;

                        if (distance >= pad)
                        {
                            continue;
                        }

                        var grain = Noise(worldX * 0.017f + 5.3f, worldZ * 0.017f + 9.1f);
                        var inward = Mathf.SmoothStep(0f, 1f, 1f - distance / pad);

                        weights[5] = Mathf.Max(weights[5], inward * (0.25f + grain * 0.55f));
                    }

                    // Asphalt last and heaviest, so nothing else shows through a road.
                    foreach (var road in RoadCentrelines())
                    {
                        NearestOnPolyline(worldX, worldZ, road.Points, out var distance);
                        var half = road.Width * 0.5f;

                        if (distance < half + 6f)
                        {
                            weights[4] = Mathf.Max(weights[4],
                                Mathf.SmoothStep(3f, 0f, Mathf.Max(0f, distance - half) / 6f));
                        }
                    }

                    var total = 0f;
                    for (var layer = 0; layer < layers; layer++)
                    {
                        total += weights[layer];
                    }

                    for (var layer = 0; layer < layers; layer++)
                    {
                        map[y, x, layer] = weights[layer] / total;
                    }
                }
            }

            data.SetAlphamaps(0, 0, map);
        }

        private static float SampleMetres(float[,] metres, float worldX, float worldZ)
        {
            var last = CityLayout.HeightmapResolution - 1;
            var x = Mathf.Clamp(Mathf.RoundToInt(worldX / CityLayout.Size * last), 0, last);
            var y = Mathf.Clamp(Mathf.RoundToInt(worldZ / CityLayout.Size * last), 0, last);
            return metres[y, x];
        }

        /// <summary>Metres of rise per metre travelled, from the four neighbouring samples.</summary>
        private static float SlopeAt(float[,] metres, float worldX, float worldZ, float step)
        {
            var east = SampleMetres(metres, worldX + step, worldZ);
            var west = SampleMetres(metres, worldX - step, worldZ);
            var north = SampleMetres(metres, worldX, worldZ + step);
            var south = SampleMetres(metres, worldX, worldZ - step);

            var dx = (east - west) / (2f * step);
            var dz = (north - south) / (2f * step);

            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static TerrainLayer[] Layers() => new[]
        {
            Layer("Sand", new Color(0.76f, 0.70f, 0.55f), 14f),
            Layer("Grass", new Color(0.29f, 0.38f, 0.24f), 22f),
            Layer("Rock", new Color(0.40f, 0.39f, 0.37f), 30f),
            Layer("Forest", new Color(0.17f, 0.26f, 0.16f), 26f),
            Layer("Asphalt", new Color(0.16f, 0.16f, 0.18f), 12f),
            Layer("Concrete", new Color(0.44f, 0.44f, 0.43f), 18f)
        };

        /// <summary>
        /// One flat-colour layer, created once and reused.
        ///
        /// Flat colour because the project has no ground photographs, and a missing texture renders
        /// white — worse than a colour that is at least the right colour. In Docs/NeededGraphics.md.
        /// </summary>
        private static TerrainLayer Layer(string name, Color colour, float tile)
        {
            var path = $"{DataFolder}/{name}.terrainlayer";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

            if (existing != null)
            {
                existing.tileSize = new Vector2(tile, tile);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var texture = new Texture2D(16, 16) { name = name + "Texture" };

            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    // Per-pixel jitter, so a flat colour does not read as plastic up close.
                    var jitter = (Hash(x, y) - 0.5f) * 0.06f;
                    texture.SetPixel(x, y, new Color(
                        Mathf.Clamp01(colour.r + jitter),
                        Mathf.Clamp01(colour.g + jitter),
                        Mathf.Clamp01(colour.b + jitter)));
                }
            }

            texture.Apply();
            AssetDatabase.CreateAsset(texture, $"{DataFolder}/{name}Texture.asset");

            var layer = new TerrainLayer
            {
                diffuseTexture = texture,
                tileSize = new Vector2(tile, tile)
            };

            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        // ---- road centrelines ----------------------------------------------------------------------

        /// <summary>
        /// A road, smoothed, with its surface height sampled and averaged along it.
        ///
        /// Built once per terrain build and cached: both passes walk every road for every sample,
        /// and re-smoothing a polyline a million times is the difference between a build that takes
        /// seconds and one that takes minutes.
        /// </summary>
        public sealed class Centreline
        {
            public Centreline(RoadRun run)
            {
                Width = run.Width;
                Class = run.Class;
                Points = Smooth(run.Points);

                heights = new float[Points.Count];

                for (var index = 0; index < Points.Count; index++)
                {
                    heights[index] = FlattenDistricts(Points[index].x, Points[index].y,
                        NaturalHeight(Points[index].x, Points[index].y));
                }

                // Six passes of neighbour averaging, so a road does not inherit every bump the land
                // under it happens to have.
                for (var pass = 0; pass < 6; pass++)
                {
                    var smoothed = (float[])heights.Clone();

                    for (var index = 1; index < heights.Length - 1; index++)
                    {
                        smoothed[index] =
                            (heights[index - 1] + heights[index] * 2f + heights[index + 1]) * 0.25f;
                    }

                    heights = smoothed;
                }
            }

            private float[] heights;

            public float Width { get; }
            public RoadClass Class { get; }
            public IReadOnlyList<Vector2> Points { get; }

            /// <summary>The road surface at a position along the line, in metres.</summary>
            public float HeightAt(float t)
            {
                if (heights.Length == 0)
                {
                    return CityLayout.SeaLevel;
                }

                var scaled = Mathf.Clamp(t * (heights.Length - 1), 0f, heights.Length - 1);
                var index = Mathf.FloorToInt(scaled);
                var next = Mathf.Min(index + 1, heights.Length - 1);

                return Mathf.Lerp(heights[index], heights[next], scaled - index);
            }
        }

        private static List<Centreline> centrelines;

        public static IReadOnlyList<Centreline> RoadCentrelines()
        {
            if (centrelines != null)
            {
                return centrelines;
            }

            centrelines = new List<Centreline>();

            foreach (var run in CityLayout.Roads)
            {
                centrelines.Add(new Centreline(run));
            }

            return centrelines;
        }

        // ---- geometry ------------------------------------------------------------------------------

        /// <summary>
        /// Catmull-Rom through the control points, about one sample every eight metres.
        ///
        /// Catmull-Rom rather than Chaikin because it passes through every point it is given, and
        /// those points are where the bridges land.
        /// </summary>
        public static List<Vector2> Smooth(IReadOnlyList<MapPoint> points)
        {
            var raw = new List<Vector2>(points.Count);

            foreach (var point in points)
            {
                raw.Add(new Vector2(point.X, point.Z));
            }

            if (raw.Count < 3)
            {
                return raw;
            }

            var output = new List<Vector2>();

            for (var index = 0; index < raw.Count - 1; index++)
            {
                var p0 = raw[Mathf.Max(index - 1, 0)];
                var p1 = raw[index];
                var p2 = raw[index + 1];
                var p3 = raw[Mathf.Min(index + 2, raw.Count - 1)];

                var steps = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(p1, p2) / 8f));

                for (var step = 0; step < steps; step++)
                {
                    output.Add(CatmullRom(p0, p1, p2, p3, step / (float)steps));
                }
            }

            output.Add(raw[^1]);
            return output;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return 0.5f * (
                2f * p1
                + (p2 - p0) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Distance to a polyline, and how far along it the nearest point is, 0 to 1.</summary>
        public static float NearestOnPolyline(float x, float z, IReadOnlyList<Vector2> points,
            out float distance)
        {
            distance = float.MaxValue;
            var best = 0f;

            if (points.Count < 2)
            {
                return 0f;
            }

            for (var index = 0; index < points.Count - 1; index++)
            {
                var a = points[index];
                var b = points[index + 1];

                var t = SegmentT(x, z, a, b);
                var closest = Vector2.Lerp(a, b, t);
                var d = Vector2.Distance(new Vector2(x, z), closest);

                if (d < distance)
                {
                    distance = d;
                    best = (index + t) / (points.Count - 1);
                }
            }

            return best;
        }

        private static bool NearestOnWater(float x, float z, WaterRun run,
            out float distance, out float halfWidth, out float depth)
        {
            distance = float.MaxValue;
            halfWidth = 0f;
            depth = 0f;

            var points = run.Points;

            for (var index = 0; index < points.Count - 1; index++)
            {
                var a = new Vector2(points[index].At.X, points[index].At.Z);
                var b = new Vector2(points[index + 1].At.X, points[index + 1].At.Z);

                var t = SegmentT(x, z, a, b);
                var closest = Vector2.Lerp(a, b, t);
                var d = Vector2.Distance(new Vector2(x, z), closest);

                if (d >= distance)
                {
                    continue;
                }

                distance = d;
                halfWidth = Mathf.Lerp(points[index].HalfWidth, points[index + 1].HalfWidth, t);
                depth = Mathf.Lerp(points[index].Depth, points[index + 1].Depth, t);
            }

            return distance < float.MaxValue;
        }

        private static float SegmentT(float x, float z, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSquared = ab.sqrMagnitude;

            if (lengthSquared <= 0f)
            {
                return 0f;
            }

            var ap = new Vector2(x - a.x, z - a.y);
            return Mathf.Clamp01(Vector2.Dot(ap, ab) / lengthSquared);
        }

        private static float Distance(float x, float z, float toX, float toZ)
        {
            var dx = x - toX;
            var dz = z - toZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---- noise ----------------------------------------------------------------------------------

        private static float Noise(float x, float y)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var xf = x - xi;
            var yf = y - yi;

            var u = xf * xf * (3f - 2f * xf);
            var v = yf * yf * (3f - 2f * yf);

            var a = Hash(xi, yi);
            var b = Hash(xi + 1, yi);
            var c = Hash(xi, yi + 1);
            var d = Hash(xi + 1, yi + 1);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + Seed);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
