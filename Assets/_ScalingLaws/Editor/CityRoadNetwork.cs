using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Lays Bayview's whole road network in one pass, as a network: every crossing, tee and corner
    /// found, given the right junction piece, and every road cut exactly to the edge of it.
    ///
    /// **Why this replaced four tools.** The arterials, the district grids, the suburban lanes and
    /// the junctions were each laid by a separate pass that knew nothing about the others. So where
    /// an arterial crossed a district street, two strips of tarmac simply overlapped in an X; where a
    /// grid line ran into the harbour, a stub of it was left standing on the far bank; and nothing
    /// anywhere knew what a tee or a corner was. A road network is a graph, and the only way to get
    /// the junctions right is to build it as one.
    ///
    /// **Bridges are found, not stated.** <see cref="CityLayout.Bridges"/> places each crossing by
    /// hand, and measured against the roads they are 40 to 75 metres off the carriageway and at a
    /// different angle — so no stated bridge actually met its road. Here a bridge is wherever an
    /// arterial's own centreline leaves dry land and comes back to it, which cannot miss.
    ///
    /// **Every piece was photographed before its rotation was written down** (see
    /// <see cref="KenneyModelPreview"/>): road-straight runs along local X; road-bridge along local Z;
    /// road-intersection is open at ±X and +Z and closed at −Z; road-bend joins +X to +Z;
    /// road-end-round is open at −X; a traffic light's lamps face +X.
    ///
    /// **The suburbs are surveyed from their brief, not read off the scene.** Each subdivision's
    /// streets ran parallel to its own collector, so not one of them met it, and the subdivisions
    /// overlap one another at different angles. Here each gets a cross street at both ends of its
    /// streets, closing them into a ladder, and where two overlap the bigger one keeps the ground.
    ///
    /// **A road that meets another at an angle is bent to meet it square** over its last few tens of
    /// metres, because every junction piece in the kit is a right angle.
    /// </summary>
    public static class CityRoadNetwork
    {
        private const string ScenePath = "Assets/_ScalingLaws/Scenes/City.unity";
        private const string Kit = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";
        private const string SuburbanKit = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";

        /// <summary>Target length of one straight tile; each run is divided into whole tiles of about this.</summary>
        private const float TileLength = 8f;

        /// <summary>Vertical scale of flat pieces: 0.02 on Kenney's grid, so kerbs stand 16 cm.</summary>
        private const float SurfaceScale = 8f;

        /// <summary>Vertical scale of bridge pieces: their 0.52 of railing comes out about 1.35 m.</summary>
        private const float RailScale = 2.6f;

        private const float Lift = 0.04f;

        /// <summary>The margin every builder here uses for "this is the water's edge".</summary>
        private const float WaterMargin = 1.5f;

        /// <summary>Stretches of water shorter than this are a wet patch the road rides over, not a river.</summary>
        private const float ShortestCrossing = 15f;

        /// <summary>Deck above the waterline. Low on purpose: a city bridge, not a viaduct, so its approaches stay gentle.</summary>
        private const float DeckClearance = 8f;

        private const float RampLength = 50f;
        private const float ShortestRamp = 14f;
        private const float PierSpacing = 40f;

        /// <summary>A bridge piece is used where the road stands this far above the ground under it.</summary>
        private const float BridgeElevation = 2.5f;

        private const float GridStreetWidth = 17f;
        private const float Densify = 4f;
        private const float TrafficLightScale = 13f;

        /// <summary>A dead-end district street shorter than a block is the overhang past the last cross street.</summary>
        private const float GridSpur = 140f;

        /// <summary>A district street with no junction on it at all is a fragment the water left behind.</summary>
        private const float ShortestGridPiece = 140f;

        private const float LaneSpur = 20f;
        private const float ShortestLanePiece = 40f;

        /// <summary>A highway running on past its last junction for less than this is a stub, not a road somewhere.</summary>
        private const float ArterialSpur = 60f;

        // The subdivision's own measurements, as CityDressingBuilder surveyed the plots to them.
        private const float CollectorWidth = 13f;
        private const float SuburbStreetWidth = 10f;
        private const float CulDeSacWidth = 9f;
        private const float CollectorOffset = 14f;
        private const float CulDeSacLength = 90f;
        private const float CulDeSacHead = 34f;

        /// <summary>
        /// How far past the ends of a subdivision's streets its cross street runs. The last plot's
        /// house stops at least half a lot short of the end, so this clears it by about ten metres.
        /// </summary>
        private const float CrossStreetGap = 12f;

        /// <summary>Where a road meets a junction further off square than this, its approach is bent.</summary>
        private const float SquareWithin = 5f;

        /// <summary>A district street this close alongside a highway, or a bigger district's street, is the same street twice.</summary>
        private const float AlongsideDistance = 24f;
        private const float AlongsideAngle = 32f;

        /// <summary>Shorter than this, running alongside is a junction or a near miss rather than a duplicate street.</summary>
        private const float ShortestAlongside = 50f;

        /// <summary>Furthest a district street that stops short of another road is carried on to meet it.</summary>
        private const float LongestReach = 60f;

        private const string PortGrid = "port_core";

        /// <summary>
        /// Where the port bridge crosses, as a distance along the port grid's streets from its middle:
        /// just west of the halls on the far bank, where the river is at its narrowest for them.
        /// The west road turns onto that line where it reaches it.
        /// </summary>
        private const float PortBridgeAlong = -120f;

        /// <summary>
        /// The port's one street, square across from its middle: behind the halls, on the land side.
        /// The grid's own waterfront street ran underneath them.
        /// </summary>
        private const float PortStreetAcross = -215f;
        /// <summary>How far the port street runs west of the port's middle, towards the river mouth and the container terminal.</summary>
        private const float PortStreetWest = 255f;

        /// <summary>How far it runs east, up the river past the halls to the warehouses.</summary>
        private const float PortStreetEast = 300f;

        /// <summary>How far before and after that corner the turn is spread.</summary>
        private const float PortTurnReach = 52f;

        private const float DrivewayWidth = 4.4f;
        private const float DrivewayPiece = 5.6f;

        /// <summary>Furthest a driveway runs looking for its street: a Greendale villa stands thirteen metres back.</summary>
        private const float DrivewayReach = 24f;

        /// <summary>Thirty metres square: the least an outdoor stage and its crowd need.</summary>
        private const float SmallestEventLawn = 900f;

        /// <summary>A house further than this from every road is not waiting for a driveway; it has lost its street.</summary>
        private const float OrphanedPlot = 35f;

        /// <summary>
        /// Steepest ground a junction piece is laid on, as rise per metre. The pieces are flat: on a
        /// steeper slope one side of the piece is buried and the other stands off the ground.
        /// </summary>
        private const float SteepestJunction = 0.15f;

        /// <summary>A highway inside a subdivision ring for less than this has only clipped a corner of it.</summary>
        private const float CornerChord = 120f;

        /// <summary>How far a smaller subdivision's street is held back from a bigger one's corners and cul-de-sac mouths.</summary>
        private const float JunctionClearance = 24f;

        /// <summary>Turns a rotation that aims local +Z into one that aims local +X — road-straight's axis.</summary>
        private static readonly Quaternion AlongX = Quaternion.Euler(0f, -90f, 0f);

        private static readonly string[] OldGroups =
        {
            "RoadTiles", "GridStreetTiles", "RoadCrossings", "TrafficLights", "RoadNetwork", "Bridges"
        };

        // ---- entry points -------------------------------------------------------------------------

        [MenuItem("Scaling Laws/Road network/Audit (changes nothing)")]
        public static void Audit() => Run(apply: false);

        [MenuItem("Scaling Laws/Road network/Rebuild")]
        public static void Rebuild() => Run(apply: true);

        /// <summary>
        /// True, with an error in the console, when the open city's roads were laid by this network.
        ///
        /// For the tools it replaced to ask first: run again, CityRoadTileBuilder would lay its
        /// overlapping tiles back over the junctions, CityGroundStage would throw away the
        /// driveways and lay nothing in their place, CityPropsStage would add a second set of
        /// traffic lights facing the wrong way, and CityWaterCleanup would take the bridges' ramps
        /// for roads in the water.
        /// </summary>
        public static bool Supersedes(string tool)
        {
            if (GameObject.Find("City")?.transform.Find("RoadNetwork") == null)
            {
                return false;
            }

            Debug.LogError($"[{tool}] This city's roads are laid by Scaling Laws/Road network/Rebuild now, "
                + "and this older tool would undo part of it. Nothing was changed.");
            return true;
        }

        private static void Run(bool apply)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Network] No City.unity.");
                return;
            }

            var city = GameObject.Find("City");
            if (city == null)
            {
                Debug.LogError("[Network] No City root.");
                return;
            }

            var report = new Report();
            var before = SceneRoadRects();
            report.Line($"Before: {CountRectCrossings(before)} places in the scene where two strips of road "
                + $"cross with no junction piece; {BuildingsOnRoads(before).Values.Sum()} buildings stand "
                + "with their middle on a road.");

            var suburbs = Suburbs();
            var buildings = new Buildings(city.transform);
            var ways = CollectWays(suburbs, report, out var bulbs);

            FindBridges(ways, report);
            KeepHighwaysAboveGround(ways, report);
            Probe("before junctions", ways, null, report);

            // Before any pruning: a street that stops just short of another road is a dead end only
            // until it is carried on, and pruning would cut it back first.
            ReachAcross(ways, FindJunctions(ways), report);

            var junctions = Settle(ways, buildings, report);

            if (KeepJunctionsOffSteepGround(junctions, report))
            {
                junctions = Settle(ways, buildings, report);
            }

            Probe("settled", ways, junctions, report);

            if (SquareApproaches(junctions, report))
            {
                junctions = Settle(ways, buildings, report);
                Probe("squared", ways, junctions, report);
            }

            ShapeRamps(ways, junctions);
            BlendIntoJunctions(junctions);

            report.Line($"Junctions: {junctions.Count(j => j.Shape == Shape.Cross)} crossroads, "
                + $"{junctions.Count(j => j.Shape == Shape.Tee)} tees, "
                + $"{junctions.Count(j => j.Shape == Shape.Bend)} corners.");

            if (!apply)
            {
                report.Flush("[Network][Audit]");
                return;
            }

            foreach (var name in OldGroups)
            {
                var old = city.transform.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var groundStage = city.transform.Find("GroundStage");
            foreach (var name in new[] { "Lanes", "Bridges" })
            {
                var old = groundStage != null ? groundStage.Find(name) : null;
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var layer = new Layer(city.transform);
            Lay(ways, junctions, bulbs, layer, report);
            PlaceTrafficLights(junctions, ways, layer, report);

            var remaining = FindRectCrossings(layer.Rects);
            report.Line($"After: {remaining.Count} places where two strips cross with no junction piece.");

            foreach (var (point, roads) in remaining)
            {
                report.Line($"  still crossing at ({point.x:0}, {point.y:0}): {roads}");
            }

            ClearWhatStandsOnTheRoad(layer, report);
            ClearOverlappingSubdivisions(suburbs, report);
            ClearStreetFurniture(layer, report);
            FitGroundFeatures(layer, report);
            LayDriveways(layer, report);
            RepaintAsphalt(city.transform, layer, report);

            EditorSceneManager.SaveScene(scene);
            report.Flush("[Network]");
        }

        /// <summary>
        /// Carries a district street that stops just short of another road on to meet it.
        ///
        /// Two grids laid out separately end where their own rectangles end, and where they come
        /// close the street of one stops tens of metres short of the other's — River Works' cross
        /// street ends thirty-six metres below downtown's southern street, a gap no driver would
        /// leave. Only a street's own end is carried on, never an end the water or a bigger road cut:
        /// those stop where they stop for a reason. It must meet the other road at a real angle, not
        /// run along beside it, and cross no water on the way.
        /// </summary>
        private static bool ReachAcross(List<Way> ways, List<Junction> junctions, Report report)
        {
            var index = new SegmentIndex(ways);
            var joined = junctions.Where(j => j.Shape != Shape.None).SelectMany(j => j.Touches).ToList();
            var square = Mathf.Cos(50f * Mathf.Deg2Rad);
            var reached = false;

            foreach (var way in ways.Where(w => w.Kind == Kind.Grid).ToList())
            {
                foreach (var atEnd in new[] { false, true })
                {
                    var s = atEnd ? way.Length : 0f;
                    var range = way.RangeAt(s, 0.5f);

                    if (range == null || (atEnd ? range.To < way.Length - 0.5f : range.From > 0.5f)
                        || joined.Any(t => t.Way == way && Mathf.Abs(t.S - s) < 3f))
                    {
                        continue;
                    }

                    var start = way.PointAt(s);
                    var outward = atEnd ? way.TangentAt(s) : -way.TangentAt(s);
                    var finish = start + outward * LongestReach;
                    var nearest = float.MaxValue;

                    foreach (var other in index.Near(start + outward * (LongestReach * 0.5f), LongestReach))
                    {
                        if (other == way)
                        {
                            continue;
                        }

                        for (var segment = 0; segment < other.Points.Count - 1; segment++)
                        {
                            var a = other.Points[segment];
                            var b = other.Points[segment + 1];

                            if (!Intersect(start, finish, a, b, out _, out var t, out var u))
                            {
                                continue;
                            }

                            var os = other.Arc[segment] + u * (other.Arc[segment + 1] - other.Arc[segment]);
                            var distance = t * LongestReach;

                            if (distance > 0.5f && distance < nearest
                                && other.RangeAt(os, 0f) != null && !other.OnCrossing(os, 10f)
                                && Mathf.Abs(Vector2.Dot(outward, (b - a).normalized)) <= square)
                            {
                                nearest = distance;
                            }
                        }
                    }

                    if (nearest == float.MaxValue)
                    {
                        continue;
                    }

                    var dry = true;
                    for (var d = 0f; d <= nearest && dry; d += 4f)
                    {
                        dry = IsDry(start + outward * d);
                    }

                    if (!dry)
                    {
                        continue;
                    }

                    var steps = Mathf.Max(1, Mathf.CeilToInt(nearest / Densify));
                    var points = new List<Vector2>();
                    var heights = new List<float>();

                    for (var step = 0; step <= steps; step++)
                    {
                        var point = start + outward * (nearest * step / steps);
                        points.Add(point);
                        heights.Add(CityTerrainBuilder.HeightAt(point.x, point.y));
                    }

                    if (!atEnd)
                    {
                        points.Reverse();
                        heights.Reverse();
                    }

                    way.Replace(s, s, points, heights);
                    reached = true;
                    report.Line($"  {way.Id}: carried {nearest:0} m on from ({start.x:0}, {start.y:0}) to meet the road ahead");
                }
            }

            return reached;
        }

        /// <summary>
        /// Takes the side streets off a highway where it runs down a slope too steep for a junction.
        ///
        /// Every junction piece is flat, and the spine comes down off the Greendale hill at more than
        /// one in two: a tee laid there has one kerb buried in the hill and the other in mid-air, and
        /// from above it reads as a hole in the road. The side street stops short of the highway
        /// instead, as a street on a hillside does when it cannot meet the road below.
        /// </summary>
        private static bool KeepJunctionsOffSteepGround(List<Junction> junctions, Report report)
        {
            var changed = false;

            foreach (var junction in junctions.Where(j => j.Shape != Shape.None))
            {
                var highway = junction.Touches
                    .Where(t => t.Way.Kind == Kind.Arterial)
                    .OrderByDescending(t => t.Way.Width)
                    .FirstOrDefault();

                if (highway == null)
                {
                    continue;
                }

                var grade = junction.Touches.Max(t =>
                    Mathf.Abs(t.Way.HeightAt(Mathf.Min(t.Way.Length, t.S + 8f)) - t.Way.HeightAt(Mathf.Max(0f, t.S - 8f))) / 16f);

                if (grade <= SteepestJunction)
                {
                    continue;
                }

                var reach = junction.Largest * 0.5f + 10f;

                foreach (var touch in junction.Touches.Where(t => t.Way != highway.Way))
                {
                    CutOut(touch.Way, Mathf.Max(0f, touch.S - reach), Mathf.Min(touch.Way.Length, touch.S + reach));
                    changed = true;
                }

                report.Line($"  junction at ({junction.At.x:0}, {junction.At.y:0}) on a {grade * 100f:0}% slope of "
                    + $"{highway.Way.Id}: its side streets stop short instead");
            }

            return changed;
        }

        /// <summary>
        /// Lifts a highway's surface wherever the ground has come up over it.
        ///
        /// A highway is laid on its own smoothed profile, which is also what the terrain builder cut
        /// into the ground for it — except where a road later in the layout crosses it, because that
        /// road's cut is made afterwards and wins. Where one of the suburban loops crosses the spine
        /// the ground stands a metre or two above the spine's profile, and the tiles vanish into a
        /// bank. The rise is spread over the neighbouring stretch rather than stepped, so the road
        /// climbs over it instead of jumping; nothing is lowered, and bridges keep their decks.
        /// </summary>
        private static void KeepHighwaysAboveGround(List<Way> ways, Report report)
        {
            const float spread = 14f;

            foreach (var way in ways.Where(w => w.Kind == Kind.Arterial))
            {
                var count = way.Points.Count;
                var under = new float[count];

                // Sampled between the points too: they are several metres apart, and a bank is narrow.
                for (var index = 0; index < count - 1; index++)
                {
                    for (var step = 0; step <= 4; step++)
                    {
                        var s = Mathf.Lerp(way.Arc[index], way.Arc[index + 1], step / 4f);
                        if (way.OnCrossing(s, 0f))
                        {
                            continue;
                        }

                        var point = way.PointAt(s);
                        var rise = CityTerrainBuilder.HeightAt(point.x, point.y) - way.HeightAt(s);
                        var nearest = step <= 2 ? index : index + 1;
                        under[nearest] = Mathf.Max(under[nearest], rise);
                    }
                }

                if (under.All(rise => rise < 0.05f))
                {
                    continue;
                }

                // The largest rise within reach of each point, then averaged over the same reach: never
                // less than the rise at any point, and without a step anywhere.
                var widest = new float[count];

                for (var index = 0; index < count; index++)
                {
                    for (var other = 0; other < count; other++)
                    {
                        if (Mathf.Abs(way.Arc[other] - way.Arc[index]) <= spread)
                        {
                            widest[index] = Mathf.Max(widest[index], under[other]);
                        }
                    }
                }

                var raised = 0f;

                for (var index = 0; index < count; index++)
                {
                    var sum = 0f;
                    var samples = 0;

                    for (var other = 0; other < count; other++)
                    {
                        if (Mathf.Abs(way.Arc[other] - way.Arc[index]) <= spread)
                        {
                            sum += widest[other];
                            samples++;
                        }
                    }

                    var lift = sum / samples;
                    way.Heights[index] += lift;
                    raised = Mathf.Max(raised, lift);
                }

                report.Line($"  {way.Id}: raised by up to {raised:0.0} m where the ground stands above its profile");
            }
        }

        /// <summary>Finds the junctions and prunes what the water and the grids left over, until nothing changes.</summary>
        private static List<Junction> Settle(List<Way> ways, Buildings buildings, Report report)
        {
            for (var pass = 0; pass < 8; pass++)
            {
                if (!Prune(ways, FindJunctions(ways), buildings, report))
                {
                    break;
                }
            }

            return FindJunctions(ways);
        }

        /// <summary>
        /// With `-probe x:z:radius` on the command line, writes into the report every road end and
        /// junction near that point at each stage — to see why a corner did not join without guessing.
        /// </summary>
        private static void Probe(string stage, List<Way> ways, List<Junction> junctions, Report report)
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-probe");
            if (at < 0 || at + 1 >= args.Length)
            {
                return;
            }

            foreach (var spot in args[at + 1].Split(';'))
            {
                var parts = spot.Split(':').Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                ProbeAt(stage, new Vector2(parts[0], parts[1]), parts[2], ways, junctions, report);
            }
        }

        private static void ProbeAt(string stage, Vector2 centre, float radius, List<Way> ways, List<Junction> junctions, Report report)
        {
            report.Line($"PROBE [{stage}] around ({centre.x:0}, {centre.y:0}):");

            foreach (var way in ways)
            {
                foreach (var range in way.Ranges)
                {
                    foreach (var (label, s) in new[] { ("from", range.From), ("to", range.To) })
                    {
                        var point = way.PointAt(s);
                        if (Vector2.Distance(point, centre) <= radius)
                        {
                            report.Line($"  end {way.Id} {label} s={s:0.0}/{way.Length:0.0} at ({point.x:0.0}, {point.y:0.0})");
                        }
                    }
                }
            }

            foreach (var junction in junctions ?? new List<Junction>())
            {
                if (Vector2.Distance(junction.At, centre) > radius)
                {
                    continue;
                }

                var touches = string.Join(", ", junction.Touches.Select(t => $"{t.Way.Id}@{t.S:0.0}"));
                var arms = string.Join(", ", junction.Arms.Select(a =>
                    $"{a.Way.Id}->{Mathf.Atan2(a.Direction.y, a.Direction.x) * Mathf.Rad2Deg:0}deg slot{a.Slot}"));
                report.Line($"  junction {junction.Shape} at ({junction.At.x:0.0}, {junction.At.y:0.0}) touches [{touches}] arms [{arms}]");
            }
        }

        // ---- ways ----------------------------------------------------------------------------------

        private enum Kind
        {
            Arterial,
            Grid,
            Lane
        }

        private sealed class Span
        {
            public float From;
            public float To;
        }

        /// <summary>One road as a polyline, with the parts of it that exist on land and the parts that are bridge.</summary>
        private sealed class Way
        {
            public string Id;
            public Kind Kind;
            public float Width;

            /// <summary>The district grid or subdivision a street belongs to, and how big that is.</summary>
            public string Group = string.Empty;
            public float GroupArea;

            /// <summary>A subdivision street's precedence: 0 for the biggest subdivision. Everything else is never outranked.</summary>
            public int Rank = int.MaxValue;

            public readonly List<Vector2> Points = new();
            public readonly List<float> Heights = new();
            public readonly List<Span> Ranges = new();
            public readonly List<Span> Crossings = new();
            public float[] Arc = Array.Empty<float>();

            public float Length => Arc.Length == 0 ? 0f : Arc[^1];

            public void Measure()
            {
                Arc = new float[Points.Count];
                for (var index = 1; index < Points.Count; index++)
                {
                    Arc[index] = Arc[index - 1] + Vector2.Distance(Points[index - 1], Points[index]);
                }
            }

            /// <summary>
            /// Swaps the stretch between two distances along the road for a new line, keeping every
            /// range and crossing beyond it where it was on the ground.
            /// </summary>
            public void Replace(float from, float to, IReadOnlyList<Vector2> points, IReadOnlyList<float> heights)
            {
                var keptPoints = new List<Vector2>();
                var keptHeights = new List<float>();
                var tail = new List<(Vector2 Point, float Height)>();

                for (var index = 0; index < Points.Count; index++)
                {
                    if (Arc[index] < from - 0.01f)
                    {
                        keptPoints.Add(Points[index]);
                        keptHeights.Add(Heights[index]);
                    }
                    else if (Arc[index] > to + 0.01f)
                    {
                        tail.Add((Points[index], Heights[index]));
                    }
                }

                var before = Length;
                keptPoints.AddRange(points);
                keptHeights.AddRange(heights);

                foreach (var (point, height) in tail)
                {
                    keptPoints.Add(point);
                    keptHeights.Add(height);
                }

                Points.Clear();
                Points.AddRange(keptPoints);
                Heights.Clear();
                Heights.AddRange(keptHeights);
                Measure();

                var shift = Length - before;

                foreach (var span in Ranges.Concat(Crossings))
                {
                    if (span.From > to)
                    {
                        span.From += shift;
                    }

                    if (span.To >= to)
                    {
                        span.To += shift;
                    }
                }
            }

            private int SegmentAt(float s)
            {
                if (s <= 0f)
                {
                    return 0;
                }

                var low = 0;
                var high = Arc.Length - 2;

                while (low < high)
                {
                    var middle = (low + high + 1) / 2;
                    if (Arc[middle] <= s)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                return Mathf.Clamp(low, 0, Arc.Length - 2);
            }

            public Vector2 PointAt(float s)
            {
                var index = SegmentAt(s);
                var run = Arc[index + 1] - Arc[index];
                var t = run > 0.0001f ? Mathf.Clamp01((s - Arc[index]) / run) : 0f;
                return Vector2.Lerp(Points[index], Points[index + 1], t);
            }

            public float HeightAt(float s)
            {
                var index = SegmentAt(s);
                var run = Arc[index + 1] - Arc[index];
                var t = run > 0.0001f ? Mathf.Clamp01((s - Arc[index]) / run) : 0f;
                return Mathf.Lerp(Heights[index], Heights[index + 1], t);
            }

            public Vector2 TangentAt(float s)
            {
                var before = PointAt(Mathf.Max(0f, s - 2f));
                var after = PointAt(Mathf.Min(Length, s + 2f));
                var direction = after - before;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            }

            public Span RangeAt(float s, float tolerance)
            {
                foreach (var range in Ranges)
                {
                    if (s >= range.From - tolerance && s <= range.To + tolerance)
                    {
                        return range;
                    }
                }

                return null;
            }

            public bool OnCrossing(float s, float margin)
            {
                foreach (var crossing in Crossings)
                {
                    if (s > crossing.From - margin && s < crossing.To + margin)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>Nearest point on the line, as distance along it and distance from it.</summary>
            public (float S, float Distance) Project(Vector2 q)
            {
                var bestS = 0f;
                var best = float.MaxValue;

                for (var index = 0; index < Points.Count - 1; index++)
                {
                    var a = Points[index];
                    var b = Points[index + 1];
                    var ab = b - a;
                    var lengthSquared = ab.sqrMagnitude;
                    var t = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / lengthSquared) : 0f;
                    var distance = Vector2.Distance(q, a + ab * t);

                    if (distance < best)
                    {
                        best = distance;
                        bestS = Arc[index] + t * Mathf.Sqrt(lengthSquared);
                    }
                }

                return (bestS, best);
            }
        }

        private sealed class Bulb
        {
            public Vector2 At;
            public float Size;
        }

        private static List<Way> CollectWays(List<Suburb> suburbs, Report report, out List<Bulb> bulbs)
        {
            var ways = new List<Way>();
            bulbs = new List<Bulb>();

            var centrelines = CityTerrainBuilder.RoadCentrelines();
            var excluded = new List<string>();

            for (var index = 0; index < CityLayout.Roads.Count; index++)
            {
                var run = CityLayout.Roads[index];

                // The suburban loops are painted into the terrain and never had a subdivision laid
                // along them; tiling one would drive a road through the middle of the houses.
                if (run.Class == RoadClass.Lane)
                {
                    excluded.Add(run.Id + " (loop painted on the terrain, houses stand on it)");
                    continue;
                }

                // A street inside a district grid predates the grid and runs across its blocks at an
                // angle, a few metres from the grid's own streets. The grid is the district's street
                // plan now; laying both is what put two parallel roads side by side downtown.
                if (run.Class == RoadClass.Street && InsideAGrid(run.Points))
                {
                    excluded.Add(run.Id + " (inside a district grid, duplicates its streets)");
                    continue;
                }

                // The southern link was drawn from the port to the civic centre along a river that
                // was later widened over it: its port end is under water, its middle runs a hundred
                // metres out along the waterline on a "bridge" parallel to the bank, and its civic end
                // comes ashore under the civic gardens' event lawn. The port now has its own bridge.
                if (run.Id == "southroad")
                {
                    excluded.Add(run.Id + " (the river runs along it; the port is reached by its own bridge)");
                    continue;
                }

                var centreline = centrelines[index];
                var way = new Way { Id = run.Id, Kind = Kind.Arterial, Width = run.Width };

                for (var point = 0; point < centreline.Points.Count; point++)
                {
                    way.Points.Add(centreline.Points[point]);
                    way.Heights.Add(centreline.HeightAt(point / (float)Mathf.Max(1, centreline.Points.Count - 1)));
                }

                way.Measure();

                if (way.Id == "westroad")
                {
                    CarryOverToThePort(way, report);
                }

                ways.Add(way);
            }

            foreach (var grid in CityBlocks.Grids)
            {
                if (grid.Id == PortGrid)
                {
                    ways.Add(PortStreet(grid));
                    continue;
                }

                var angle = grid.RotationDegrees * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);
                var centre = new Vector2(grid.CentreX, grid.CentreZ);

                var columns = Mathf.Max(1, Mathf.FloorToInt(grid.Width / grid.BlockSize));
                var rows = Mathf.Max(1, Mathf.FloorToInt(grid.Depth / grid.BlockSize));
                var streets = new List<Way>();

                for (var column = 0; column <= columns; column++)
                {
                    var line = centre + across * (-grid.Width * 0.5f + grid.BlockSize * column);
                    streets.Add(Straight($"{grid.Id}_c{column}", Kind.Grid, GridStreetWidth,
                        line - along * grid.Depth * 0.5f, line + along * grid.Depth * 0.5f));
                }

                for (var row = 0; row <= rows; row++)
                {
                    var line = centre + along * (-grid.Depth * 0.5f + grid.BlockSize * row);
                    streets.Add(Straight($"{grid.Id}_r{row}", Kind.Grid, GridStreetWidth,
                        line - across * grid.Width * 0.5f, line + across * grid.Width * 0.5f));
                }

                foreach (var street in streets)
                {
                    street.Group = grid.Id;
                    street.GroupArea = grid.Width * grid.Depth;
                }

                ways.AddRange(streets);
            }

            for (var rank = 0; rank < suburbs.Count; rank++)
            {
                foreach (var street in SuburbStreets(suburbs[rank], bulbs))
                {
                    street.Rank = rank;
                    ways.Add(street);
                }
            }

            foreach (var way in ways)
            {
                way.Ranges.Add(new Span { From = 0f, To = way.Length });
            }

            report.Line($"Ways: {ways.Count(w => w.Kind == Kind.Arterial)} arterials, "
                + $"{ways.Count(w => w.Kind == Kind.Grid)} district streets, "
                + $"{ways.Count(w => w.Kind == Kind.Lane)} suburban streets, {bulbs.Count} cul-de-sac heads.");

            foreach (var reason in excluded)
            {
                report.Line($"  left out: {reason}");
            }

            GiveWayToBiggerSubdivisions(ways, suburbs, report);
            DropStreetsAlongside(ways, report);
            AddAccessStreets(ways, report);

            return ways;
        }

        private static bool InsideAGrid(IReadOnlyList<MapPoint> points) =>
            CityBlocks.Grids.Any(grid =>
                points.Count(point => InsideGrid(grid, new Vector2(point.X, point.Z)))
                >= Mathf.CeilToInt(points.Count * 0.7f));

        private static bool InsideAGrid(Vector2 point) => CityBlocks.Grids.Any(grid => InsideGrid(grid, point));

        private static bool InsideGrid(GridBlock grid, Vector2 point)
        {
            var angle = grid.RotationDegrees * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-along.y, along.x);
            var offset = point - new Vector2(grid.CentreX, grid.CentreZ);

            return Mathf.Abs(Vector2.Dot(offset, across)) <= grid.Width * 0.5f + 20f
                   && Mathf.Abs(Vector2.Dot(offset, along)) <= grid.Depth * 0.5f + 20f;
        }

        private static Way Straight(string id, Kind kind, float width, Vector2 from, Vector2 to)
        {
            var way = new Way { Id = id, Kind = kind, Width = width };
            var length = Vector2.Distance(from, to);
            var steps = Mathf.Max(1, Mathf.CeilToInt(length / Densify));

            for (var step = 0; step <= steps; step++)
            {
                var point = Vector2.Lerp(from, to, step / (float)steps);
                way.Points.Add(point);
                way.Heights.Add(CityTerrainBuilder.HeightAt(point.x, point.y));
            }

            way.Measure();
            return way;
        }

        // ---- suburbs -------------------------------------------------------------------------------

        /// <summary>
        /// One subdivision's ground in its own frame — "along" down its streets, "across" from one
        /// street to the next — measured the way <see cref="CityDressingBuilder"/> surveyed it.
        /// </summary>
        private sealed class Suburb
        {
            public Suburb(ResidentialBlock block)
            {
                Block = block;
                Centre = new Vector2(block.CentreX, block.CentreZ);

                var angle = block.RotationDegrees * Mathf.Deg2Rad;
                Along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Across = new Vector2(-Along.y, Along.x);

                Streets = Mathf.Max(1, Mathf.FloorToInt(block.Width / block.StreetSpacing));
                Collector = -block.Width * 0.5f - CollectorOffset;
                LastStreet = StreetAt(Streets - 1);
                End = block.Depth * 0.5f + CrossStreetGap;
            }

            public ResidentialBlock Block { get; }
            public Vector2 Centre { get; }
            public Vector2 Along { get; }
            public Vector2 Across { get; }
            public int Streets { get; }

            /// <summary>How far across the collector runs, and the street furthest from it.</summary>
            public float Collector { get; }
            public float LastStreet { get; }

            /// <summary>How far along the two cross streets run.</summary>
            public float End { get; }

            public float StreetAt(int street) => -Block.Width * 0.5f + Block.StreetSpacing * (street + 0.5f);

            public Vector2 World(float along, float across) => Centre + Along * along + Across * across;

            /// <summary>A point in this frame: x along the streets, y across them.</summary>
            public Vector2 Local(Vector2 point)
            {
                var offset = point - Centre;
                return new Vector2(Vector2.Dot(offset, Along), Vector2.Dot(offset, Across));
            }

            /// <summary>Inside the ring the collector, the last street and the two cross streets make.</summary>
            public bool Inside(Vector2 point, float margin)
            {
                var local = Local(point);
                return Mathf.Abs(local.x) <= End + margin
                       && local.y >= Collector - margin && local.y <= LastStreet + margin;
            }
        }

        /// <summary>Every subdivision, biggest first: where two overlap, the bigger one keeps the ground.</summary>
        private static List<Suburb> Suburbs() => CityBlocks.Residential
            .Select(block => new Suburb(block))
            .OrderByDescending(suburb => suburb.Block.Width * suburb.Block.Depth)
            .ToList();

        /// <summary>
        /// A subdivision's streets as its brief describes them, and the two its surveyor left out.
        ///
        /// The generator ran every street parallel to the collector and stopped each at the edge of
        /// the block, so not one of them met anything: a comb with no spine. A cross street just
        /// past each end of the streets joins all of them and the collector into a ladder.
        /// </summary>
        private static List<Way> SuburbStreets(Suburb suburb, List<Bulb> bulbs)
        {
            var id = suburb.Block.Id;
            var streets = new List<Way>
            {
                Straight($"{id}_collector", Kind.Lane, CollectorWidth,
                    suburb.World(-suburb.End, suburb.Collector), suburb.World(suburb.End, suburb.Collector))
            };

            for (var street = 0; street < suburb.Streets; street++)
            {
                var across = suburb.StreetAt(street);
                streets.Add(Straight($"{id}_street{street}", Kind.Lane, SuburbStreetWidth,
                    suburb.World(-suburb.End, across), suburb.World(suburb.End, across)));
            }

            foreach (var end in new[] { -suburb.End, suburb.End })
            {
                streets.Add(Straight($"{id}_cross{(end < 0f ? "_start" : "_end")}", Kind.Lane, SuburbStreetWidth,
                    suburb.World(end, suburb.Collector), suburb.World(end, suburb.LastStreet)));
            }

            for (var index = 0; index < suburb.Block.CulDeSacs; index++)
            {
                var along = Mathf.Lerp(-suburb.Block.Depth * 0.5f, suburb.Block.Depth * 0.5f,
                    (index + 1f) / (suburb.Block.CulDeSacs + 1f));
                var head = suburb.World(along, suburb.Collector - CulDeSacLength);

                streets.Add(Straight($"{id}_culdesac{index}", Kind.Lane, CulDeSacWidth,
                    suburb.World(along, suburb.Collector), head));

                if (CityTerrainBuilder.HeightAt(head.x, head.y) >= CityLayout.SeaLevel + 2.5f)
                {
                    bulbs.Add(new Bulb { At = head, Size = CulDeSacHead });
                }
            }

            foreach (var street in streets)
            {
                street.Group = id;
                street.GroupArea = suburb.Block.Width * suburb.Block.Depth;
            }

            return streets;
        }
        /// <summary>
        /// Cuts out of every subdivision's ring of streets the roads that have no business inside it:
        /// the streets of a smaller subdivision laid over it at another angle, and the highways drawn
        /// straight through its houses. What is left of each ends on the ring, and meets it as a tee.
        ///
        /// Two exceptions keep the junctions this makes buildable. A highway that only clips a corner
        /// of the ring stays whole, and the corner it cuts off is taken away instead, so the ring's
        /// streets tee into the highway rather than a highway ending ten metres from a corner. And a
        /// smaller subdivision's street is held back from the bigger ring's corners and cul-de-sac
        /// mouths: arriving on top of one, it makes a five-way knot no junction piece can be.
        /// </summary>
        private static void GiveWayToBiggerSubdivisions(List<Way> ways, List<Suburb> suburbs, Report report)
        {
            var grazes = new List<(Way Highway, Suburb Suburb)>();

            foreach (var way in ways)
            {
                if (way.Kind == Kind.Grid)
                {
                    continue;
                }

                for (var rank = 0; rank < Mathf.Min(way.Rank, suburbs.Count); rank++)
                {
                    foreach (var (from, to) in InsideIntervals(way, suburbs[rank]))
                    {
                        if (to - from < 0.5f)
                        {
                            continue;
                        }

                        var passesThrough = from > 1f && to < way.Length - 1f;

                        if (way.Kind == Kind.Arterial && passesThrough && to - from < CornerChord)
                        {
                            grazes.Add((way, suburbs[rank]));
                            continue;
                        }

                        CutOut(way, from, to);
                        report.Count(way.Kind == Kind.Arterial
                            ? "subdivisions: highway stretches cut out from among the houses"
                            : "subdivisions: streets of a smaller subdivision cut back to a bigger one's");
                    }
                }
            }

            foreach (var (highway, suburb) in grazes)
            {
                CutOffCorner(ways.Where(w => w.Group == suburb.Block.Id), highway, suburb, report);
            }

            foreach (var way in ways.Where(w => w.Kind == Kind.Lane))
            {
                for (var rank = 0; rank < Mathf.Min(way.Rank, suburbs.Count); rank++)
                {
                    KeepClearOfJunctions(way, suburbs[rank], report);
                }
            }
        }

        /// <summary>
        /// Takes away the part of a subdivision's streets on the far side of a highway that cuts
        /// across a corner of it, measured from the subdivision's middle.
        /// </summary>
        private static void CutOffCorner(IEnumerable<Way> streets, Way highway, Suburb suburb, Report report)
        {
            foreach (var street in streets)
            {
                var marked = new List<(float From, float To)>();

                for (var s = 0f; s <= street.Length; s += 2f)
                {
                    var point = street.PointAt(s);
                    var (hs, distance) = highway.Project(point);

                    if (distance > CornerChord)
                    {
                        continue;
                    }

                    var near = highway.PointAt(hs);
                    var tangent = highway.TangentAt(hs);

                    if (Side(tangent, point - near) == Side(tangent, suburb.Centre - near))
                    {
                        continue;
                    }

                    if (marked.Count > 0 && s - marked[^1].To <= 2.01f)
                    {
                        marked[^1] = (marked[^1].From, s);
                    }
                    else
                    {
                        marked.Add((s, s));
                    }
                }

                foreach (var (from, to) in marked)
                {
                    CutOut(street, from, to);
                    report.Line($"  {street.Id}: {to - from:0} m beyond {highway.Id}, which cuts the corner, taken away");
                }
            }
        }

        private static float Side(Vector2 direction, Vector2 offset) =>
            Mathf.Sign(direction.x * offset.y - direction.y * offset.x);

        /// <summary>
        /// Holds a smaller subdivision's street back from where the bigger one's own streets meet:
        /// the ring's corners, where its streets end on its cross streets, and its cul-de-sac mouths.
        /// </summary>
        private static void KeepClearOfJunctions(Way way, Suburb bigger, Report report)
        {
            var points = new List<Vector2>();

            foreach (var along in new[] { -bigger.End, bigger.End })
            {
                points.Add(bigger.World(along, bigger.Collector));

                for (var street = 0; street < bigger.Streets; street++)
                {
                    points.Add(bigger.World(along, bigger.StreetAt(street)));
                }
            }

            for (var index = 0; index < bigger.Block.CulDeSacs; index++)
            {
                var along = Mathf.Lerp(-bigger.Block.Depth * 0.5f, bigger.Block.Depth * 0.5f,
                    (index + 1f) / (bigger.Block.CulDeSacs + 1f));
                points.Add(bigger.World(along, bigger.Collector));
            }

            var marked = new List<(float From, float To)>();

            for (var s = 0f; s <= way.Length; s += 2f)
            {
                var point = way.PointAt(s);

                if (way.RangeAt(s, 0f) == null || !points.Any(p => Vector2.Distance(p, point) < JunctionClearance))
                {
                    continue;
                }

                if (marked.Count > 0 && s - marked[^1].To <= 2.01f)
                {
                    marked[^1] = (marked[^1].From, s);
                }
                else
                {
                    marked.Add((s, s));
                }
            }

            foreach (var (from, to) in marked)
            {
                CutOut(way, Mathf.Max(0f, from - 8f), Mathf.Min(way.Length, to + 8f));
                report.Count("subdivisions: streets held back from a bigger subdivision's corners");
            }
        }

        /// <summary>The stretches of a road inside a subdivision's ring, as distances along the road.</summary>
        private static List<(float From, float To)> InsideIntervals(Way way, Suburb suburb)
        {
            var found = new List<(float From, float To)>();

            for (var index = 0; index < way.Points.Count - 1; index++)
            {
                var a = suburb.Local(way.Points[index]);
                var b = suburb.Local(way.Points[index + 1]);
                var enter = 0f;
                var leave = 1f;

                if (!Clip(a.x, b.x, -suburb.End, suburb.End, ref enter, ref leave)
                    || !Clip(a.y, b.y, suburb.Collector, suburb.LastStreet, ref enter, ref leave))
                {
                    continue;
                }

                var run = way.Arc[index + 1] - way.Arc[index];
                var from = way.Arc[index] + enter * run;
                var to = way.Arc[index] + leave * run;

                if (found.Count > 0 && from - found[^1].To < 0.05f)
                {
                    found[^1] = (found[^1].From, to);
                }
                else
                {
                    found.Add((from, to));
                }
            }

            return found;
        }

        /// <summary>Narrows [enter, leave] to the part of a segment whose coordinate lies between low and high.</summary>
        private static bool Clip(float start, float end, float low, float high, ref float enter, ref float leave)
        {
            var delta = end - start;

            if (Mathf.Abs(delta) < 0.0001f)
            {
                return start >= low && start <= high;
            }

            var first = (low - start) / delta;
            var second = (high - start) / delta;

            if (first > second)
            {
                (first, second) = (second, first);
            }

            enter = Mathf.Max(enter, first);
            leave = Mathf.Min(leave, second);
            return enter <= leave;
        }

        /// <summary>
        /// Drops the stretches of a district street that run alongside a highway, or alongside a
        /// bigger district's street, nearly parallel and a few tens of metres off it.
        ///
        /// Downtown's east street and the spine run side by side twenty-odd metres apart, and the
        /// civic grid's north street shadows downtown's south one: two roads where a city has one,
        /// with a strip of grass between them no building fits on.
        /// </summary>
        private static void DropStreetsAlongside(List<Way> ways, Report report)
        {
            var index = new SegmentIndex(ways);
            var parallel = Mathf.Cos(AlongsideAngle * Mathf.Deg2Rad);

            foreach (var way in ways.Where(w => w.Kind == Kind.Grid))
            {
                var marked = new List<(float From, float To)>();

                for (var s = 0f; s <= way.Length; s += Densify)
                {
                    var point = way.PointAt(s);
                    var tangent = way.TangentAt(s);
                    var alongside = false;

                    foreach (var other in index.Near(point, 60f))
                    {
                        var outranks = other.Kind == Kind.Arterial
                                       || (other.Kind == Kind.Grid && other.Group != way.Group
                                           && other.GroupArea > way.GroupArea);
                        if (!outranks)
                        {
                            continue;
                        }

                        var (os, distance) = other.Project(point);

                        // Beside the other road, not beyond its end: a street drawn on from the end of
                        // another, as Midtown's are from downtown's, continues it rather than doubling it.
                        var otherRange = other.RangeAt(os, 0f);

                        if (distance <= (way.Width + other.Width) * 0.5f + AlongsideDistance
                            && otherRange != null && os > otherRange.From + 3f && os < otherRange.To - 3f
                            && Mathf.Abs(Vector2.Dot(tangent, other.TangentAt(os))) >= parallel)
                        {
                            alongside = true;
                            break;
                        }
                    }

                    if (!alongside)
                    {
                        continue;
                    }

                    if (marked.Count > 0 && s - marked[^1].To <= Densify + 0.01f)
                    {
                        marked[^1] = (marked[^1].From, s);
                    }
                    else
                    {
                        marked.Add((s, s));
                    }
                }

                foreach (var (from, to) in marked)
                {
                    // A street meeting another road at a shallow angle runs beside it only briefly,
                    // and two grids that merely come close at a corner do too: a junction or a near
                    // miss, not a second road.
                    if (to - from < ShortestAlongside)
                    {
                        continue;
                    }

                    CutOut(way, Mathf.Max(0f, from - Densify), Mathf.Min(way.Length, to + Densify));
                    report.Line($"  {way.Id}: {to - from:0} m running alongside a bigger road, dropped");
                }
            }
        }

        // ---- access streets ------------------------------------------------------------------------

        /// <summary>
        /// Streets joining a district grown later to the roads already around it.
        ///
        /// A grid is a closed ring of its own streets; one laid onto empty land touches nothing, and
        /// River Works came out as an island a hundred metres from the west road and eighty below
        /// Midtown. Each access leaves a point on one of the grid's streets, given in the grid's own
        /// frame (along its long streets, across them), and runs straight on in the direction given
        /// until it meets another road.
        /// </summary>
        private static readonly (string Grid, float Along, float Across, float TowardsAlong, float TowardsAcross)[] Accesses =
        {
            // The middle cross street, on past the inland street and up into Midtown.
            ("riverworks_core", 0f, 70f, 0f, 1f),

            // From the middle of the western cross street, out to the west road.
            ("riverworks_core", -140f, 0f, -1f, 0f)
        };

        private const float LongestAccess = 260f;

        private static void AddAccessStreets(List<Way> ways, Report report)
        {
            foreach (var (gridId, along, across, towardsAlong, towardsAcross) in Accesses)
            {
                var grid = CityBlocks.Grids.FirstOrDefault(g => g.Id == gridId);
                if (grid == null)
                {
                    continue;
                }

                var (centre, alongAxis, acrossAxis) = Frame(grid);
                var start = centre + alongAxis * along + acrossAxis * across;
                var direction = (alongAxis * towardsAlong + acrossAxis * towardsAcross).normalized;
                var finish = start + direction * LongestAccess;
                var nearest = float.MaxValue;

                foreach (var other in ways.Where(w => w.Group != gridId))
                {
                    for (var segment = 0; segment < other.Points.Count - 1; segment++)
                    {
                        if (Intersect(start, finish, other.Points[segment], other.Points[segment + 1], out _, out var t, out _)
                            && t * LongestAccess > 1f)
                        {
                            nearest = Mathf.Min(nearest, t * LongestAccess);
                        }
                    }
                }

                if (nearest == float.MaxValue)
                {
                    report.Line($"  WARNING: the access from {gridId} at ({start.x:0}, {start.y:0}) meets no road within {LongestAccess:0} m");
                    continue;
                }

                var access = Straight($"{gridId}_access_{Accesses.ToList().IndexOf((gridId, along, across, towardsAlong, towardsAcross))}",
                    Kind.Grid, GridStreetWidth, start, start + direction * nearest);

                access.Group = gridId;
                access.GroupArea = grid.Width * grid.Depth;
                access.Ranges.Add(new Span { From = 0f, To = access.Length });
                ways.Add(access);

                report.Line($"  {access.Id}: {nearest:0} m from ({start.x:0}, {start.y:0}) to the road it meets");
            }
        }

        // ---- the port ------------------------------------------------------------------------------

        /// <summary>
        /// Takes the west road over the river to the port, instead of into it.
        ///
        /// The port stands on the far bank, and the west road was drawn running on down the near one
        /// until the river took it: the district the city's servers live in had no road to it at
        /// all. The road now bends, where it comes down towards the river, onto a line square to the
        /// port's street and runs straight across — just west of the halls, not through them — to
        /// the street behind them. The bridge itself is found the way every other one is.
        /// </summary>
        private static void CarryOverToThePort(Way westroad, Report report)
        {
            var port = CityBlocks.Grids.FirstOrDefault(grid => grid.Id == PortGrid);
            if (port == null)
            {
                return;
            }

            var (centre, along, across) = Frame(port);

            // Where the road, heading down towards the port, reaches the bridge's line.
            float? turn = null;

            for (var index = 0; index < westroad.Points.Count - 1 && turn == null; index++)
            {
                var before = Vector2.Dot(westroad.Points[index] - centre, along) - PortBridgeAlong;
                var after = Vector2.Dot(westroad.Points[index + 1] - centre, along) - PortBridgeAlong;

                if (before < 0f && after >= 0f && Vector2.Dot(westroad.Points[index] - centre, across) > 0f)
                {
                    turn = Mathf.Lerp(westroad.Arc[index], westroad.Arc[index + 1], -before / (after - before));
                }
            }

            if (turn == null || turn.Value < PortTurnReach)
            {
                report.Line("  WARNING: the west road never comes down to the port; left as it was.");
                return;
            }

            var corner = westroad.PointAt(turn.Value);
            var leave = turn.Value - PortTurnReach;
            var from = westroad.PointAt(leave);
            var height = westroad.HeightAt(leave);

            var cornerAcross = Vector2.Dot(corner - centre, across);
            var landing = corner - across * PortTurnReach;
            var street = corner - across * (cornerAcross - PortStreetAcross);

            // A cubic with both handles two thirds of the way to the corner: a round turn from the
            // road's heading onto the bridge's, with no kink at either end.
            var route = new List<Vector2>();
            var steps = Mathf.CeilToInt(PortTurnReach * 2f / Densify);

            for (var step = 1; step <= steps; step++)
            {
                route.Add(Bezier(from, from + (corner - from) * (2f / 3f), landing + (corner - landing) * (2f / 3f),
                    landing, step / (float)steps));
            }

            steps = Mathf.CeilToInt(Vector2.Distance(landing, street) / Densify);

            for (var step = 1; step <= steps; step++)
            {
                route.Add(Vector2.Lerp(landing, street, step / (float)steps));
            }

            var kept = westroad.Arc.Count(s => s < leave);
            westroad.Points.RemoveRange(kept, westroad.Points.Count - kept);
            westroad.Heights.RemoveRange(kept, westroad.Heights.Count - kept);
            westroad.Points.Add(from);
            westroad.Heights.Add(height);

            foreach (var point in route)
            {
                westroad.Points.Add(point);
                westroad.Heights.Add(CityTerrainBuilder.HeightAt(point.x, point.y));
            }

            westroad.Measure();

            report.Line($"Port: the west road turns at ({corner.x:0}, {corner.y:0}) and crosses to the "
                + $"port street at ({street.x:0}, {street.y:0}).");
        }

        /// <summary>
        /// The port's one street, in place of its grid.
        ///
        /// The port grid was centred where the river now runs: of its streets, two are under water,
        /// the rows cross the river, and the one on the far bank runs straight underneath the halls
        /// that stand along the shore. What the port needs is a street behind those halls.
        /// </summary>
        private static Way PortStreet(GridBlock port)
        {
            var (centre, along, across) = Frame(port);
            var line = centre + across * PortStreetAcross;
            var street = Straight($"{port.Id}_street", Kind.Grid, GridStreetWidth,
                line - along * PortStreetWest, line + along * PortStreetEast);

            street.Group = port.Id;
            street.GroupArea = port.Width * port.Depth;
            return street;
        }

        private static (Vector2 Centre, Vector2 Along, Vector2 Across) Frame(GridBlock grid)
        {
            var angle = grid.RotationDegrees * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return (new Vector2(grid.CentreX, grid.CentreZ), along, new Vector2(-along.y, along.x));
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        // ---- bridges -------------------------------------------------------------------------------

        /// <summary>
        /// Wherever an arterial's own centreline leaves the land and comes back, that stretch is a
        /// bridge. A road that runs into the water and never comes out is cut at the shore instead.
        /// District and suburban streets are never bridged; they are cut at the water.
        /// </summary>
        private static void FindBridges(List<Way> ways, Report report)
        {
            var bridged = 0;
            var cut = 0;

            foreach (var way in ways)
            {
                var wet = new List<(float From, float To)>();
                var inWater = false;
                var start = 0f;

                for (var index = 0; index < way.Points.Count; index++)
                {
                    var dry = IsDry(way.Points[index]);

                    if (!dry && !inWater)
                    {
                        inWater = true;
                        start = index == 0 ? 0f : Shoreline(way, index - 1, index);
                    }
                    else if (dry && inWater)
                    {
                        inWater = false;
                        wet.Add((start, Shoreline(way, index, index - 1)));
                    }
                }

                if (inWater)
                {
                    wet.Add((start, way.Length));
                }

                foreach (var (from, to) in wet)
                {
                    var touchesStart = from <= 0.01f;
                    var touchesEnd = to >= way.Length - 0.01f;

                    if (way.Kind == Kind.Arterial && !touchesStart && !touchesEnd)
                    {
                        if (to - from < ShortestCrossing)
                        {
                            continue;
                        }

                        way.Crossings.Add(new Span { From = from, To = to });
                        bridged++;
                        report.Line($"  bridge on {way.Id}: {to - from:0} m of water, "
                            + $"from ({way.PointAt(from).x:0}, {way.PointAt(from).y:0}) "
                            + $"to ({way.PointAt(to).x:0}, {way.PointAt(to).y:0})");
                        continue;
                    }

                    if (to - from < ShortestCrossing && !touchesStart && !touchesEnd)
                    {
                        continue;
                    }

                    CutOut(way, from, to);
                    cut++;
                }
            }

            report.Line($"Water: {bridged} bridges found on the arterials' own lines, "
                + $"{cut} stretches of road cut back to the shore.");
        }

        private static bool IsDry(Vector2 point) =>
            CityTerrainBuilder.HeightAt(point.x, point.y) >= CityLayout.SeaLevel + WaterMargin;

        /// <summary>The exact distance along the road at which the shore is, found by halving between a dry point and a wet one.</summary>
        private static float Shoreline(Way way, int dryIndex, int wetIndex)
        {
            var dry = way.Arc[dryIndex];
            var wet = way.Arc[wetIndex];

            for (var step = 0; step < 14; step++)
            {
                var middle = (dry + wet) * 0.5f;
                if (IsDry(way.PointAt(middle)))
                {
                    dry = middle;
                }
                else
                {
                    wet = middle;
                }
            }

            return dry;
        }

        private static void CutOut(Way way, float from, float to)
        {
            var replacement = new List<Span>();

            foreach (var range in way.Ranges)
            {
                if (to <= range.From || from >= range.To)
                {
                    replacement.Add(range);
                    continue;
                }

                if (from > range.From)
                {
                    replacement.Add(new Span { From = range.From, To = from });
                }

                if (to < range.To)
                {
                    replacement.Add(new Span { From = to, To = range.To });
                }
            }

            way.Ranges.Clear();
            way.Ranges.AddRange(replacement);
        }

        /// <summary>
        /// Lifts each bridge's deck clear of the water and gives it approaches, stopping every
        /// approach short of the nearest junction so no junction ends up halfway up a ramp.
        /// </summary>
        private static void ShapeRamps(List<Way> ways, List<Junction> junctions)
        {
            foreach (var way in ways)
            {
                if (way.Crossings.Count == 0)
                {
                    continue;
                }

                var stops = junctions
                    .Where(j => j.Shape != Shape.None)
                    .SelectMany(j => j.Touches.Where(t => t.Way == way).Select(t => (t.S, Half: j.Largest * 0.5f)))
                    .ToList();

                var original = way.Heights.ToArray();

                foreach (var crossing in way.Crossings)
                {
                    var range = way.RangeAt((crossing.From + crossing.To) * 0.5f, 0f);
                    var floor = range?.From ?? 0f;
                    var ceiling = range?.To ?? way.Length;

                    var before = Mathf.Min(RampLength, crossing.From - floor - 1f);
                    var after = Mathf.Min(RampLength, ceiling - crossing.To - 1f);

                    foreach (var (s, half) in stops)
                    {
                        if (s < crossing.From)
                        {
                            before = Mathf.Min(before, crossing.From - (s + half + 6f));
                        }
                        else if (s > crossing.To)
                        {
                            after = Mathf.Min(after, s - half - 6f - crossing.To);
                        }
                    }

                    before = Mathf.Max(ShortestRamp, before);
                    after = Mathf.Max(ShortestRamp, after);

                    var deck = Mathf.Max(CityLayout.SeaLevel + DeckClearance,
                        Mathf.Max(way.HeightAt(crossing.From), way.HeightAt(crossing.To)) + 2f);

                    var span = crossing.To - crossing.From;
                    var camber = Mathf.Min(3f, span * 0.02f);

                    for (var index = 0; index < way.Points.Count; index++)
                    {
                        var s = way.Arc[index];

                        if (s >= crossing.From && s <= crossing.To)
                        {
                            way.Heights[index] = deck + Mathf.Sin(Mathf.PI * (s - crossing.From) / span) * camber;
                        }
                        else if (s >= crossing.From - before && s < crossing.From)
                        {
                            var t = (s - (crossing.From - before)) / before;
                            way.Heights[index] = Mathf.Lerp(original[index], deck, t * t * (3f - 2f * t));
                        }
                        else if (s > crossing.To && s <= crossing.To + after)
                        {
                            var t = (crossing.To + after - s) / after;
                            way.Heights[index] = Mathf.Lerp(original[index], deck, t * t * (3f - 2f * t));
                        }
                    }

                    crossing.From -= before;
                    crossing.To += after;
                }
            }
        }

        /// <summary>
        /// Eases a street's surface into the height of the junction it meets over the last 24 metres.
        ///
        /// A district street follows the levelled ground; the arterial crossing it follows its own
        /// smoothed line, and the two can disagree by a metre or more. Without this the junction
        /// piece sits at the arterial's height and the street's last tile stops a step below it.
        /// </summary>
        private static void BlendIntoJunctions(List<Junction> junctions)
        {
            const float reach = 24f;

            foreach (var junction in junctions.Where(j => j.Shape != Shape.None))
            {
                foreach (var touch in junction.Touches.Where(t => t.Way.Kind != Kind.Arterial))
                {
                    var way = touch.Way;

                    for (var index = 0; index < way.Points.Count; index++)
                    {
                        var distance = Mathf.Abs(way.Arc[index] - touch.S);
                        if (distance < reach)
                        {
                            way.Heights[index] = Mathf.Lerp(way.Heights[index], junction.Height, 1f - distance / reach);
                        }
                    }
                }
            }
        }

        // ---- junctions -----------------------------------------------------------------------------

        private enum Shape
        {
            None,
            Cross,
            Tee,
            Bend
        }

        private sealed class Touch
        {
            public Way Way;
            public float S;
        }

        private sealed class Arm
        {
            public Way Way;
            public Vector2 Direction;
            public int Slot;
        }

        private sealed class Junction
        {
            public Vector2 At;
            public float Height;
            public readonly List<Touch> Touches = new();
            public readonly List<Arm> Arms = new();
            public Shape Shape;
            public Vector2 U;
            public float ExtentU;
            public float ExtentV;
            public readonly bool[] Slots = new bool[4];
            public readonly float[] SlotWidth = new float[4];

            public float Largest => Mathf.Max(ExtentU, ExtentV);

            public Vector2 V => new(-U.y, U.x);

            public Vector2 SlotDirection(int slot) => slot switch
            {
                0 => U,
                1 => -U,
                2 => V,
                _ => -V
            };

            /// <summary>How far the junction piece reaches along a direction, edge to edge.</summary>
            public float ExtentAlong(Vector2 direction) =>
                Mathf.Abs(Vector2.Dot(direction, U)) >= Mathf.Abs(Vector2.Dot(direction, V)) ? ExtentU : ExtentV;
        }

        private sealed class Candidate
        {
            public Vector2 At;
            public readonly List<Touch> Touches = new();
        }

        private static List<Junction> FindJunctions(List<Way> ways)
        {
            var candidates = new List<Candidate>();
            var index = new SegmentIndex(ways);

            // Crossings: where two roads' lines actually intersect.
            foreach (var (a, i, b, j) in index.Pairs())
            {
                if (!Intersect(a.Points[i], a.Points[i + 1], b.Points[j], b.Points[j + 1], out var at, out var ta, out var tb))
                {
                    continue;
                }

                var sa = a.Arc[i] + ta * (a.Arc[i + 1] - a.Arc[i]);
                var sb = b.Arc[j] + tb * (b.Arc[j + 1] - b.Arc[j]);

                if (a.RangeAt(sa, 1f) == null || b.RangeAt(sb, 1f) == null
                    || a.OnCrossing(sa, 0f) || b.OnCrossing(sb, 0f))
                {
                    continue;
                }

                var candidate = new Candidate { At = at };
                candidate.Touches.Add(new Touch { Way = a, S = sa });
                candidate.Touches.Add(new Touch { Way = b, S = sb });
                candidates.Add(candidate);
            }

            // Ends: a road that stops on another road's line is a tee; one that stops at another's end is a corner.
            foreach (var way in ways)
            {
                foreach (var range in way.Ranges)
                {
                    foreach (var s in new[] { range.From, range.To })
                    {
                        var point = way.PointAt(s);

                        foreach (var other in index.Near(point, 30f))
                        {
                            if (other == way)
                            {
                                continue;
                            }

                            var reach = (way.Width + other.Width) * 0.5f + 4f;
                            var (os, distance) = other.Project(point);

                            if (distance > reach || other.RangeAt(os, 1f) == null || other.OnCrossing(os, 0f))
                            {
                                continue;
                            }

                            var candidate = new Candidate { At = other.PointAt(os) };
                            candidate.Touches.Add(new Touch { Way = way, S = s });
                            candidate.Touches.Add(new Touch { Way = other, S = os });
                            candidates.Add(candidate);
                        }
                    }
                }
            }

            // Everything found within half a road's width of itself is one junction.
            var parent = Enumerable.Range(0, candidates.Count).ToArray();
            int FindRoot(int i) => parent[i] == i ? i : parent[i] = FindRoot(parent[i]);

            for (var i = 0; i < candidates.Count; i++)
            {
                var widthI = candidates[i].Touches.Max(t => t.Way.Width);

                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var reach = Mathf.Max(6f, Mathf.Max(widthI, candidates[j].Touches.Max(t => t.Way.Width)) * 0.5f);

                    if (Vector2.Distance(candidates[i].At, candidates[j].At) <= reach)
                    {
                        parent[FindRoot(i)] = FindRoot(j);
                    }
                }
            }

            var junctions = new List<Junction>();

            foreach (var family in Enumerable.Range(0, candidates.Count).GroupBy(FindRoot))
            {
                var members = family.Select(i => candidates[i]).ToList();
                var junction = new Junction
                {
                    At = new Vector2(members.Average(m => m.At.x), members.Average(m => m.At.y))
                };

                foreach (var touch in members.SelectMany(m => m.Touches))
                {
                    var existing = junction.Touches.FirstOrDefault(t =>
                        t.Way == touch.Way && Mathf.Abs(t.S - touch.S) < Mathf.Max(8f, touch.Way.Width));

                    if (existing == null)
                    {
                        junction.Touches.Add(new Touch { Way = touch.Way, S = touch.S });
                    }
                }

                junctions.Add(junction);
            }

            foreach (var junction in junctions)
            {
                Snap(junction);
            }

            // Measured again once every road has been snapped: a snap that lengthens a road at its
            // start moves every distance along it, and a junction further down that road measured
            // before it would otherwise sit ten metres from where the road now ends — which pruning
            // then reads as a dead-end stub and cuts off, parting a corner that had met.
            foreach (var junction in junctions)
            {
                foreach (var touch in junction.Touches)
                {
                    touch.S = touch.Way.Project(junction.At).S;
                }

                Classify(junction);
            }

            return junctions;
        }

        /// <summary>
        /// Brings every road that ends at a junction to exactly the junction's point — extended if
        /// it stopped short, trimmed if it ran past — so the gap or overlap at the joint is zero
        /// before any tile is laid.
        /// </summary>
        private static void Snap(Junction junction)
        {
            var tolerance = junction.Touches.Max(t => t.Way.Width) * 0.5f + 5f;

            foreach (var touch in junction.Touches)
            {
                var way = touch.Way;

                // Re-measured from the junction itself every time, never carried over: an earlier
                // junction on the same road may have lengthened it at the start and moved every
                // distance along it.
                var (s, gap) = way.Project(junction.At);
                touch.S = s;

                var range = way.RangeAt(s, tolerance);
                if (range == null)
                {
                    continue;
                }

                if (Mathf.Abs(s - range.To) < tolerance)
                {
                    var last = way.Points[^1];

                    if (range.To >= way.Length - 0.01f && s >= way.Length - 0.01f
                        && gap > 0.3f && gap < tolerance * 2f
                        && Vector2.Dot(junction.At - last, way.TangentAt(way.Length)) > gap * 0.5f)
                    {
                        way.Points.Add(junction.At);
                        way.Heights.Add(way.Heights[^1]);
                        way.Measure();
                        touch.S = way.Length;
                    }

                    range.To = Mathf.Max(range.From, touch.S);
                }
                else if (Mathf.Abs(s - range.From) < tolerance)
                {
                    var first = way.Points[0];

                    if (range.From <= 0.01f && s <= 0.01f
                        && gap > 0.3f && gap < tolerance * 2f
                        && Vector2.Dot(junction.At - first, -way.TangentAt(0f)) > gap * 0.5f)
                    {
                        way.Points.Insert(0, junction.At);
                        way.Heights.Insert(0, way.Heights[0]);
                        way.Measure();

                        var shift = way.Arc[1];

                        foreach (var other in way.Ranges.Where(other => other != range))
                        {
                            other.From += shift;
                            other.To += shift;
                        }

                        range.To += shift;

                        foreach (var crossing in way.Crossings)
                        {
                            crossing.From += shift;
                            crossing.To += shift;
                        }

                        touch.S = 0f;
                    }

                    range.From = Mathf.Min(range.To, touch.S);
                }
            }
        }

        private static void Classify(Junction junction)
        {
            foreach (var touch in junction.Touches)
            {
                var range = touch.Way.RangeAt(touch.S, 1.5f);
                if (range == null)
                {
                    continue;
                }

                if (range.To - touch.S > 2f)
                {
                    AddArm(junction, touch.Way, touch.Way.TangentAt(Mathf.Min(touch.Way.Length, touch.S + 2f)));
                }

                if (touch.S - range.From > 2f)
                {
                    AddArm(junction, touch.Way, -touch.Way.TangentAt(Mathf.Max(0f, touch.S - 2f)));
                }
            }

            if (junction.Arms.Count < 2)
            {
                junction.Shape = Shape.None;
                return;
            }

            // The piece's axis: whichever arm leaves the least bending for the rest, a road's bending
            // weighed by the square of its width — a highway is not bent round to suit a cul-de-sac,
            // and two streets already square to each other are not bent to suit a third on the skew.
            // A road passing through weighs four times over: bending it means an S on both sides of
            // the junction, where a road that ends there only curves in, and usually has the room.
            Arm anchor = null;
            var cheapest = float.MaxValue;

            foreach (var candidate in junction.Arms)
            {
                var cost = 0f;

                foreach (var arm in junction.Arms)
                {
                    var along = Mathf.Abs(Vector2.Dot(arm.Direction, candidate.Direction));
                    var across = Mathf.Abs(arm.Direction.x * candidate.Direction.y - arm.Direction.y * candidate.Direction.x);
                    var through = junction.Arms.Count(other => other.Way == arm.Way) >= 2 ? 4f : 1f;
                    cost += through * arm.Way.Width * arm.Way.Width * Mathf.Acos(Mathf.Clamp01(Mathf.Max(along, across)));
                }

                if (cost < cheapest - 0.001f || (cost < cheapest + 0.001f && candidate.Way.Width > anchor.Way.Width))
                {
                    cheapest = cost;
                    anchor = candidate;
                }
            }

            junction.U = anchor.Direction;
            var owner = new Arm[4];

            // Each arm takes the free slot nearest its heading, the anchor's road first and then the
            // widest. Two roads never share a slot: one that loses its nearest takes the next, and
            // SquareApproaches bends it round to meet it.
            foreach (var arm in junction.Arms
                         .OrderByDescending(a => a.Way == anchor.Way)
                         .ThenByDescending(a => a.Way.Width))
            {
                arm.Slot = -1;

                foreach (var slot in Enumerable.Range(0, 4)
                             .OrderByDescending(slot => Vector2.Dot(arm.Direction, junction.SlotDirection(slot))))
                {
                    if (owner[slot] == null && Vector2.Dot(arm.Direction, junction.SlotDirection(slot)) > -0.2f)
                    {
                        arm.Slot = slot;
                        owner[slot] = arm;
                        break;
                    }
                }

                if (arm.Slot < 0)
                {
                    continue;
                }

                junction.Slots[arm.Slot] = true;
                junction.SlotWidth[arm.Slot] = Mathf.Max(junction.SlotWidth[arm.Slot], arm.Way.Width);
            }

            var alongU = Mathf.Max(junction.SlotWidth[0], junction.SlotWidth[1]);
            var alongV = Mathf.Max(junction.SlotWidth[2], junction.SlotWidth[3]);

            junction.ExtentU = alongV > 0f ? alongV : alongU;
            junction.ExtentV = alongU > 0f ? alongU : alongV;

            var count = junction.Slots.Count(taken => taken);

            junction.Shape = count switch
            {
                4 => Shape.Cross,
                3 => Shape.Tee,
                2 when (junction.Slots[0] && junction.Slots[1]) || (junction.Slots[2] && junction.Slots[3]) => Shape.None,
                2 => Shape.Bend,
                _ => Shape.None
            };

            // An arterial's surface is smoothed along its whole length, so where one is part of the
            // junction its height is the one everything else meets.
            var arterial = junction.Touches
                .Where(t => t.Way.Kind == Kind.Arterial)
                .OrderByDescending(t => t.Way.Width)
                .FirstOrDefault();

            junction.Height = arterial != null
                ? arterial.Way.HeightAt(arterial.S)
                : junction.Touches.Average(t => t.Way.HeightAt(t.S));
        }

        private static void AddArm(Junction junction, Way way, Vector2 direction)
        {
            foreach (var arm in junction.Arms)
            {
                // The same road touching twice is one arm. Two different roads leaving in nearly the
                // same direction are two arms: one of them is bent round to a free slot.
                if (Vector2.Dot(arm.Direction, direction) > (arm.Way == way ? 0.9f : 0.98f))
                {
                    if (way.Width > arm.Way.Width)
                    {
                        arm.Way = way;
                        arm.Direction = direction;
                    }

                    return;
                }
            }

            junction.Arms.Add(new Arm { Way = way, Direction = direction });
        }

        // ---- pruning -------------------------------------------------------------------------------

        /// <summary>
        /// Removes what does not belong to the network: a district street's dead-end overhang past
        /// its last cross street, a fragment the water cut off with no junction on it, a stub of
        /// suburban street too short to be a street. Returns true when anything changed, so the
        /// junctions can be looked for again — trimming a stub turns a crossroads into a tee.
        /// </summary>
        private static bool Prune(List<Way> ways, List<Junction> junctions, Buildings buildings, Report report)
        {
            var changed = false;

            foreach (var way in ways)
            {
                // A highway joined to nothing at all goes wherever it is long: it is a road the water
                // cut both ends off, and a highway that leads nowhere is not a highway.
                var (spur, shortest) = way.Kind switch
                {
                    Kind.Grid => (GridSpur, ShortestGridPiece),
                    Kind.Lane => (LaneSpur, ShortestLanePiece),
                    _ => (ArterialSpur, float.MaxValue)
                };

                var positions = junctions
                    .Where(j => j.Shape != Shape.None)
                    .SelectMany(j => j.Touches)
                    .Where(t => t.Way == way)
                    .Select(t => t.S)
                    .ToList();

                foreach (var range in way.Ranges.ToList())
                {
                    var inside = positions.Where(s => s >= range.From - 1.5f && s <= range.To + 1.5f).OrderBy(s => s).ToList();
                    var endJoined = inside.Any(s => Mathf.Abs(s - range.To) < 3f);
                    var startJoined = inside.Any(s => Mathf.Abs(s - range.From) < 3f);

                    if (inside.Count == 0)
                    {
                        if (range.To - range.From < shortest)
                        {
                            way.Ranges.Remove(range);
                            report.Count(way.Kind == Kind.Arterial
                                ? $"pruned: highway pieces joined to nothing ({way.Id})"
                                : "pruned: isolated fragments");
                            changed = true;
                        }

                        continue;
                    }

                    if (!endJoined && (range.To - inside[^1] < spur || !Serves(way, inside[^1], range.To, buildings)))
                    {
                        range.To = inside[^1];
                        report.Count("pruned: dead-end stubs");
                        changed = true;
                    }

                    if (!startJoined && (inside[0] - range.From < spur || !Serves(way, inside[0], range.From, buildings)))
                    {
                        range.From = inside[0];
                        report.Count("pruned: dead-end stubs");
                        changed = true;
                    }

                    if (range.To - range.From < 0.5f)
                    {
                        way.Ranges.Remove(range);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Whether a dead-end stretch of district street or highway leads to anything: a building
        /// within twenty-five metres of its kerb anywhere past its first forty metres, which belong
        /// to the junction it leaves and whatever stands on its corners. A street that serves nothing is a leftover — the port grid's
        /// north-bank street running off along the river, the highway stubs past the last turn.
        /// Suburban streets always serve the houses they were surveyed for.
        /// </summary>
        private static bool Serves(Way way, float junction, float end, Buildings buildings)
        {
            if (way.Kind == Kind.Lane)
            {
                return true;
            }

            // A highway that stops in the middle of a district grid serves nothing the grid's own
            // streets do not: the east road ran eighty metres into a downtown block and stopped.
            if (way.Kind == Kind.Arterial && InsideAGrid(way.PointAt(end)))
            {
                return false;
            }

            var direction = Mathf.Sign(end - junction);
            var length = Mathf.Abs(end - junction);

            for (var travelled = 40f; travelled <= length; travelled += 6f)
            {
                if (buildings.Near(way.PointAt(junction + direction * travelled), way.Width * 0.5f + 25f))
                {
                    return true;
                }
            }

            return length <= 40f;
        }

        /// <summary>The footprint of every building in the city, to ask what a street leads to.</summary>
        private sealed class Buildings
        {
            private const float Cell = 64f;
            private readonly Dictionary<(int, int), List<Bounds>> cells = new();

            public Buildings(Transform city)
            {
                foreach (var renderer in city.GetComponentsInChildren<MeshRenderer>())
                {
                    var parent = renderer.transform.parent;
                    if (!renderer.gameObject.name.StartsWith("building-") && (parent == null || parent.name != "SiteBuildings"))
                    {
                        continue;
                    }

                    var bounds = renderer.bounds;
                    var key = (Floor(bounds.center.x), Floor(bounds.center.z));

                    if (!cells.TryGetValue(key, out var list))
                    {
                        list = new List<Bounds>();
                        cells[key] = list;
                    }

                    list.Add(bounds);
                }
            }

            private static int Floor(float value) => Mathf.FloorToInt(value / Cell);

            public bool Near(Vector2 point, float radius)
            {
                for (var x = Floor(point.x - radius - Cell); x <= Floor(point.x + radius + Cell); x++)
                {
                    for (var y = Floor(point.y - radius - Cell); y <= Floor(point.y + radius + Cell); y++)
                    {
                        if (!cells.TryGetValue((x, y), out var list))
                        {
                            continue;
                        }

                        foreach (var bounds in list)
                        {
                            var dx = Mathf.Max(0f, Mathf.Abs(point.x - bounds.center.x) - bounds.extents.x);
                            var dz = Mathf.Max(0f, Mathf.Abs(point.y - bounds.center.z) - bounds.extents.z);

                            if (dx * dx + dz * dz <= radius * radius)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        // ---- squaring ------------------------------------------------------------------------------

        /// <summary>
        /// Bends every road that meets a junction off square round to meet it square.
        ///
        /// Every junction piece in the kit is a right angle. A street arriving thirty degrees off it
        /// runs its tiles across the corner of the piece and leaves a wedge of grass on the far side
        /// — the overlaps the crossing count finds. Real streets curve into a junction rather than
        /// meeting it on the skew, so each such arm's first stretch is replaced by a curve that
        /// leaves the junction square and joins the road's own line further out.
        /// </summary>
        private static bool SquareApproaches(List<Junction> junctions, Report report)
        {
            var edits = new List<(Way Way, float From, float To, List<Vector2> Points)>();
            var real = junctions.Where(j => j.Shape != Shape.None).ToList();

            foreach (var junction in real)
            {
                foreach (var arm in junction.Arms.Where(a => a.Slot >= 0))
                {
                    var wanted = junction.SlotDirection(arm.Slot);
                    var off = Vector2.Angle(arm.Direction, wanted);

                    if (off < SquareWithin || off > 75f)
                    {
                        continue;
                    }

                    var way = arm.Way;
                    var touch = junction.Touches
                        .Where(t => t.Way == way)
                        .OrderBy(t => Vector2.Distance(way.PointAt(t.S), junction.At))
                        .FirstOrDefault();
                    var range = touch != null ? way.RangeAt(touch.S, 1.5f) : null;

                    if (range == null)
                    {
                        continue;
                    }

                    var outward = Vector2.Dot(arm.Direction, way.TangentAt(touch.S)) > 0f ? 1f : -1f;

                    // Long enough to turn gently; never past the road's end, onto a bridge, or more
                    // than halfway to the road's next junction, whose own arm may be turning too.
                    var room = outward > 0f ? range.To - touch.S : touch.S - range.From;

                    foreach (var other in real.Where(other => other != junction))
                    {
                        foreach (var gap in other.Touches.Where(t => t.Way == way).Select(t => (t.S - touch.S) * outward))
                        {
                            if (gap > 0f)
                            {
                                room = Mathf.Min(room, Mathf.Min(gap * 0.5f, gap - other.Largest * 0.5f - 6f));
                            }
                        }
                    }

                    foreach (var crossing in way.Crossings)
                    {
                        var gap = outward > 0f ? crossing.From - touch.S : touch.S - crossing.To;
                        if (gap > 0f)
                        {
                            room = Mathf.Min(room, gap - 4f);
                        }
                    }

                    var reach = Mathf.Min(14f + off * 0.45f, room);

                    if (reach < junction.Largest * 0.5f + 8f)
                    {
                        report.Line($"  left on the skew: {way.Id} meets the junction at ({junction.At.x:0}, {junction.At.y:0}) {off:0} degrees off square, {room:0} m to bend in");
                        continue;
                    }

                    var far = touch.S + reach * outward;
                    var end = way.PointAt(far);
                    var heading = way.TangentAt(far) * outward;
                    var handle = reach * 0.45f;
                    var steps = Mathf.Max(4, Mathf.CeilToInt(reach / 2f));
                    var curve = new List<Vector2>();

                    for (var step = 0; step <= steps; step++)
                    {
                        curve.Add(Bezier(junction.At, junction.At + wanted * handle, end - heading * handle, end,
                            step / (float)steps));
                    }

                    if (outward < 0f)
                    {
                        curve.Reverse();
                    }

                    edits.Add((way, Mathf.Min(touch.S, far), Mathf.Max(touch.S, far), curve));
                }
            }

            var applied = 0;

            // Furthest along each road first, so replacing one stretch never moves another still to come.
            foreach (var road in edits.GroupBy(edit => edit.Way))
            {
                var way = road.Key;
                var limit = float.MaxValue;

                foreach (var edit in road.OrderByDescending(edit => edit.From))
                {
                    if (edit.To > limit + 0.01f)
                    {
                        continue;
                    }

                    var heights = edit.Points
                        .Select((_, i) => way.HeightAt(Mathf.Lerp(edit.From, edit.To, i / (float)(edit.Points.Count - 1))))
                        .ToList();

                    way.Replace(edit.From, edit.To, edit.Points, heights);
                    limit = edit.From;
                    applied++;
                }
            }

            report.Line($"Squaring: {applied} approaches bent round to meet their junction square.");
            return applied > 0;
        }

        // ---- laying --------------------------------------------------------------------------------

        private readonly struct Rect2
        {
            public Rect2(Vector2 centre, Vector2 axis, float halfLength, float halfWidth, bool junction, string owner = null)
            {
                Owner = owner;
                Centre = centre;
                Axis = axis;
                HalfLength = halfLength;
                HalfWidth = halfWidth;
                Junction = junction;
            }

            public Vector2 Centre { get; }
            public Vector2 Axis { get; }
            public float HalfLength { get; }
            public float HalfWidth { get; }
            public bool Junction { get; }

            /// <summary>The road a strip was laid for, to name it in the report.</summary>
            public string Owner { get; }

            public bool Contains(Vector2 point, float margin)
            {
                var offset = point - Centre;
                var normal = new Vector2(-Axis.y, Axis.x);
                return Mathf.Abs(Vector2.Dot(offset, Axis)) <= HalfLength + margin
                       && Mathf.Abs(Vector2.Dot(offset, normal)) <= HalfWidth + margin;
            }
        }

        private sealed class Layer
        {
            public Layer(Transform city)
            {
                Root = new GameObject("RoadNetwork").transform;
                Root.SetParent(city, false);
                Streets = Group("Streets");
                Junctions = Group("Junctions");
                Ends = Group("Ends");
                Bridges = Group("Bridges");
                Piers = Group("Piers");
                Lights = Group("TrafficLights");

                Straight = Load("road-straight");
                Bridge = Load("road-bridge");
                Crossroad = Load("road-crossroad");
                Tee = Load("road-intersection");
                Bend = Load("road-bend");
                End = Load("road-end-round");
                Pillar = Load("bridge-pillar-wide");
                Light = Load("traffic-light");
            }

            public Transform Root { get; }
            public Transform Streets { get; }
            public Transform Junctions { get; }
            public Transform Ends { get; }
            public Transform Bridges { get; }
            public Transform Piers { get; }
            public Transform Lights { get; }

            public GameObject Straight { get; }
            public GameObject Bridge { get; }
            public GameObject Crossroad { get; }
            public GameObject Tee { get; }
            public GameObject Bend { get; }
            public GameObject End { get; }
            public GameObject Pillar { get; }
            public GameObject Light { get; }

            public readonly List<Rect2> Rects = new();

            private Transform Group(string name)
            {
                var group = new GameObject(name).transform;
                group.SetParent(Root, false);
                return group;
            }

            private static GameObject Load(string name)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".fbx");
                if (model == null)
                {
                    throw new InvalidOperationException($"Missing road model {name}.");
                }

                return model;
            }

            public GameObject Place(GameObject model, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = scale;
                return instance;
            }
        }

        private static void Lay(List<Way> ways, List<Junction> junctions, List<Bulb> bulbs, Layer layer, Report report)
        {
            var real = junctions.Where(j => j.Shape != Shape.None).ToList();
            var cuts = new Dictionary<Way, List<(float From, float To)>>();

            foreach (var way in ways)
            {
                cuts[way] = new List<(float, float)>();
            }

            // Junction pieces, and the stretch of every road they stand on. Cut per road that touches
            // the junction, not per arm: a narrow street sharing an arm's direction with a wider one
            // lost that arm to the wider road, and would otherwise run its tiles straight across the
            // junction piece.
            foreach (var junction in real)
            {
                PlaceJunction(junction, layer, report);

                foreach (var touch in junction.Touches)
                {
                    var half = junction.ExtentAlong(touch.Way.TangentAt(touch.S)) * 0.5f;
                    cuts[touch.Way].Add((touch.S - half, touch.S + half));
                }
            }

            // Dead ends: a rounded end, or a cul-de-sac head where the subdivision drew one.
            foreach (var way in ways)
            {
                foreach (var range in way.Ranges)
                {
                    foreach (var atEnd in new[] { false, true })
                    {
                        var s = atEnd ? range.To : range.From;

                        if (real.Any(j => j.Touches.Any(t => t.Way == way && Mathf.Abs(t.S - s) < 3f)))
                        {
                            continue;
                        }

                        var point = way.PointAt(s);
                        var outward = atEnd ? way.TangentAt(s) : -way.TangentAt(s);
                        var bulb = bulbs.FirstOrDefault(b => Vector2.Distance(b.At, point) < 6f);
                        var size = bulb?.Size ?? way.Width;

                        var centre = bulb != null ? bulb.At : point - outward * (size * 0.5f);
                        var height = way.HeightAt(s);

                        layer.Place(layer.End, layer.Ends,
                            new Vector3(centre.x, height + Lift, centre.y),
                            Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y), Vector3.up) * AlongX,
                            new Vector3(size, SurfaceScale, bulb != null ? size : way.Width));

                        layer.Rects.Add(new Rect2(centre, outward, size * 0.5f, (bulb != null ? size : way.Width) * 0.5f, true));

                        var reach = bulb != null ? Vector2.Distance(bulb.At, point) + size * 0.5f : size;
                        var inward = Mathf.Min(reach, size);
                        cuts[way].Add(atEnd ? (s - inward, s + 0.01f) : (s - 0.01f, s + inward));
                        report.Count(bulb != null ? "ends: cul-de-sac heads" : "ends: rounded dead ends");

                        if (bulb == null)
                        {
                            report.Line($"  dead end: {way.Id} at ({point.x:0}, {point.y:0})");
                        }
                    }
                }
            }

            foreach (var way in ways)
            {
                foreach (var range in way.Ranges)
                {
                    foreach (var (from, to) in Free(range, cuts[way]))
                    {
                        LayRun(way, from, to, layer, report);
                    }
                }

                PlacePiers(way, layer, report);
            }
        }

        private static IEnumerable<(float From, float To)> Free(Span range, List<(float From, float To)> cuts)
        {
            var cursor = range.From;

            foreach (var (from, to) in cuts.Where(c => c.To > range.From && c.From < range.To).OrderBy(c => c.From))
            {
                if (from > cursor + 0.2f)
                {
                    yield return (cursor, Mathf.Min(from, range.To));
                }

                cursor = Mathf.Max(cursor, to);
            }

            if (range.To > cursor + 0.2f)
            {
                yield return (cursor, range.To);
            }
        }

        /// <summary>
        /// One free stretch of road, divided into a whole number of equal tiles so it ends exactly
        /// where the junction or the next stretch begins — no gap, no overlap — each tile pitched
        /// to the slope under it so a ramp is a ramp and not a flight of steps.
        /// </summary>
        private static void LayRun(Way way, float from, float to, Layer layer, Report report)
        {
            var length = to - from;
            var count = Mathf.Max(1, Mathf.RoundToInt(length / TileLength));
            var step = length / count;

            for (var index = 0; index < count; index++)
            {
                var s = from + step * (index + 0.5f);
                var point = way.PointAt(s);
                var tangent = way.TangentAt(s);

                var h0 = way.HeightAt(s - step * 0.5f);
                var h1 = way.HeightAt(s + step * 0.5f);
                var height = (h0 + h1) * 0.5f;
                var forward = new Vector3(tangent.x, (h1 - h0) / step, tangent.y).normalized;
                var pitched = Quaternion.LookRotation(forward, Vector3.up);

                var ground = CityTerrainBuilder.HeightAt(point.x, point.y);
                var elevated = way.OnCrossing(s, 0.01f) && height - ground > BridgeElevation;

                // A tile is a straight plank between its two ends. Where the ground bends upward
                // under it — the edge of a levelled pad — the grass rises through the middle of the
                // plank and reads as a hole in the road, so the plank is lifted clear of it.
                if (!elevated)
                {
                    var bulge = 0f;

                    for (var quarter = 1; quarter <= 3; quarter++)
                    {
                        var q = quarter * 0.25f;
                        var under = way.PointAt(s + step * (q - 0.5f));
                        bulge = Mathf.Max(bulge, CityTerrainBuilder.HeightAt(under.x, under.y) - Mathf.Lerp(h0, h1, q));
                    }

                    height += bulge;
                }

                // On a curve, square-ended tiles open a wedge on the outside of the bend between one
                // and the next. Each is lengthened by just enough to close it; the overlap on the
                // inside is the same surface lying on itself, and does not show.
                var turn = Vector2.Angle(way.TangentAt(s - step * 0.5f), way.TangentAt(s + step * 0.5f)) * Mathf.Deg2Rad;
                var cover = step * 1.004f + way.Width * Mathf.Tan(Mathf.Min(turn, 1f) * 0.5f);

                if (elevated)
                {
                    layer.Place(layer.Bridge, layer.Bridges, new Vector3(point.x, height + Lift, point.y),
                        pitched, new Vector3(way.Width, RailScale, cover));
                    report.Count("tiles: bridge");
                }
                else
                {
                    layer.Place(layer.Straight, layer.Streets, new Vector3(point.x, height + Lift, point.y),
                        pitched * AlongX, new Vector3(cover, SurfaceScale, way.Width));
                    report.Count("tiles: straight");
                }

                layer.Rects.Add(new Rect2(point, tangent, step * 0.5f, way.Width * 0.5f, false, way.Id));
            }
        }

        private static void PlaceJunction(Junction junction, Layer layer, Report report)
        {
            var up = junction.Height + Lift + 0.01f;
            var at = new Vector3(junction.At.x, up, junction.At.y);
            Quaternion rotation;
            GameObject model;

            switch (junction.Shape)
            {
                case Shape.Cross:
                    model = layer.Crossroad;
                    rotation = Quaternion.LookRotation(Flat(junction.U), Vector3.up) * AlongX;
                    report.Count("junctions: crossroads");
                    break;

                case Shape.Tee:
                {
                    // The piece is closed on its local −Z; aim that at the one direction with no road.
                    var missing = Enumerable.Range(0, 4).First(slot => !junction.Slots[slot]);
                    rotation = Quaternion.LookRotation(Flat(-junction.SlotDirection(missing)), Vector3.up);
                    model = layer.Tee;
                    report.Count("junctions: tees");
                    break;
                }

                default:
                {
                    // The bend joins its local +X to its local +Z. Aim +Z along one arm; if +X then
                    // points away from the other arm, aim +Z along the other one instead.
                    var taken = Enumerable.Range(0, 4).Where(slot => junction.Slots[slot]).ToArray();
                    var a = junction.SlotDirection(taken[0]);
                    var b = junction.SlotDirection(taken[1]);
                    rotation = Quaternion.LookRotation(Flat(b), Vector3.up);

                    if (Vector2.Dot(Across(rotation), a) < 0.5f)
                    {
                        rotation = Quaternion.LookRotation(Flat(a), Vector3.up);
                    }

                    model = layer.Bend;
                    report.Count("junctions: corners");
                    break;
                }
            }

            var localX = Across(rotation);
            var scale = new Vector3(junction.ExtentAlong(localX), SurfaceScale,
                junction.ExtentAlong(new Vector2(-localX.y, localX.x)));

            layer.Place(model, layer.Junctions, at, rotation, scale);
            layer.Rects.Add(new Rect2(junction.At, localX, scale.x * 0.5f, scale.z * 0.5f, true));
        }

        private static Vector3 Flat(Vector2 direction) => new(direction.x, 0f, direction.y);

        private static Vector2 Across(Quaternion rotation)
        {
            var right = rotation * Vector3.right;
            return new Vector2(right.x, right.z).normalized;
        }

        private static void PlacePiers(Way way, Layer layer, Report report)
        {
            foreach (var crossing in way.Crossings)
            {
                for (var s = crossing.From + PierSpacing * 0.5f; s < crossing.To; s += PierSpacing)
                {
                    var point = way.PointAt(s);
                    var top = way.HeightAt(s);
                    var bed = CityTerrainBuilder.HeightAt(point.x, point.y);
                    var tall = top - bed - 0.2f;

                    if (tall < 4f)
                    {
                        continue;
                    }

                    var tangent = way.TangentAt(s);

                    // bridge-pillar-wide is 0.14 x 0.5 x 0.14 on Kenney's grid, standing on its base.
                    layer.Place(layer.Pillar, layer.Piers, new Vector3(point.x, bed, point.y),
                        Quaternion.LookRotation(Flat(tangent), Vector3.up),
                        new Vector3(way.Width * 0.22f / 0.14f, tall / 0.5f, 7f / 0.14f));

                    report.Count("bridge piers");
                }
            }
        }

        /// <summary>
        /// A light at the near-right corner of every arm of every junction a street or highway
        /// meets, facing the traffic coming up that arm — its lamps are on its local +X.
        /// Suburban junctions get none: nobody puts traffic lights where two cul-de-sacs meet.
        /// </summary>
        private static void PlaceTrafficLights(List<Junction> junctions, List<Way> ways, Layer layer, Report report)
        {
            foreach (var junction in junctions)
            {
                if (junction.Shape is not (Shape.Cross or Shape.Tee))
                {
                    continue;
                }

                if (junction.Arms.Max(a => a.Way.Width) < 16f)
                {
                    continue;
                }

                for (var slot = 0; slot < 4; slot++)
                {
                    if (!junction.Slots[slot])
                    {
                        continue;
                    }

                    var outward = junction.SlotDirection(slot);
                    var right = new Vector2(-outward.y, outward.x);
                    var along = junction.ExtentAlong(outward) * 0.5f;
                    var aside = junction.ExtentAlong(right) * 0.5f;

                    var spot = junction.At + outward * (along + 3f) + right * (aside + 3f);
                    var ground = CityTerrainBuilder.HeightAt(spot.x, spot.y);

                    if (ground < CityLayout.SeaLevel + WaterMargin)
                    {
                        continue;
                    }

                    layer.Place(layer.Light, layer.Lights, new Vector3(spot.x, ground, spot.y),
                        Quaternion.LookRotation(Flat(outward), Vector3.up) * AlongX,
                        Vector3.one * TrafficLightScale);

                    report.Count("traffic lights");
                }
            }
        }

        /// <summary>
        /// Everything standing on the carriageway that should not be.
        ///
        /// **Most of it was there before this rebuild.** Measured against the old road tiles, 79
        /// buildings already stood with their middle on a road: the subdivision generator ran each
        /// cul-de-sac stem out across plots, and the subdivisions overlap. A garage in the middle of
        /// a street is never right, whatever put it there.
        ///
        /// A building goes when any part of it is on a road, not only its middle — the middle test
        /// left houses with half their floor on the tarmac. It goes with its garage and hedge, since
        /// a garage standing alone where a house was is its own small artefact.
        ///
        /// **A map site and the founder's house are never removed**, only reported: each is the one
        /// building on its spot that means something, and moving it is a decision about the game.
        /// </summary>
        private static void ClearWhatStandsOnTheRoad(Layer layer, Report report)
        {
            var index = new RectIndex(layer.Rects);
            var doomed = new HashSet<GameObject>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var name = renderer.gameObject.name;
                var isTree = name.StartsWith("tree-");
                var isHedge = name == "Hedge";
                var isDriveway = name.StartsWith("driveway-");

                if (!isTree && !isHedge && !isDriveway)
                {
                    continue;
                }

                // A driveway is meant to meet the kerb; only a piece lying out on the carriageway goes.
                var margin = isTree ? 1.5f : isDriveway ? -1f : 0.2f;
                var centre = renderer.bounds.center;

                if (!index.Contains(new Vector2(centre.x, centre.z), margin))
                {
                    continue;
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? renderer.gameObject;
                if (doomed.Add(root))
                {
                    report.Count(isTree ? "cleared: trees standing on a road"
                        : isDriveway ? "cleared: driveway pieces lying on a road"
                        : "cleared: hedges in a road");
                }
            }

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.gameObject.name.StartsWith("building-") || !OnARoad(renderer.bounds, index))
                {
                    continue;
                }

                var building = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? renderer.gameObject;

                if (building.GetComponentInParent<UI.MapSitePin>() != null || IsTheFounders(building.transform))
                {
                    report.Line($"  WARNING: '{building.transform.parent?.name}/{building.name}' at "
                        + $"({building.transform.position.x:0}, {building.transform.position.z:0}) is partly on a road — "
                        + "a map site or the founder's house, left in place.");
                    continue;
                }

                // A house, its garage and its hedge are one plot: all of it goes together.
                var parent = building.transform.parent;
                var plot = parent != null && parent.name is "House" or "Villa" ? parent.gameObject : building;

                if (doomed.Add(plot))
                {
                    report.Count(plot == building ? $"cleared: {building.name} standing on a road"
                        : "cleared: suburban plots with a house or garage on a road");
                }
            }

            Destroy(doomed);
        }

        /// <summary>
        /// Whether a building's footprint reaches a road: its middle, or any of four points set in
        /// from its corners. Set in, because a turned building's bounding box is larger than the
        /// building; at a third of the way out they stay on its floor.
        /// </summary>
        private static bool OnARoad(Bounds bounds, RectIndex index)
        {
            var centre = new Vector2(bounds.center.x, bounds.center.z);
            var reach = new Vector2(bounds.extents.x, bounds.extents.z) * 0.33f;

            return index.Contains(centre, 0f)
                   || index.Contains(centre + new Vector2(reach.x, reach.y), 0f)
                   || index.Contains(centre + new Vector2(-reach.x, reach.y), 0f)
                   || index.Contains(centre + new Vector2(reach.x, -reach.y), 0f)
                   || index.Contains(centre - reach, 0f);
        }

        private static bool IsTheFounders(Transform transform)
        {
            for (var at = transform; at != null; at = at.parent)
            {
                if (at.name == "FounderHome")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Where two subdivisions overlap, the smaller one's houses that stand inside the bigger
        /// one's ring of streets or on one of its houses; and any plot the clearing left with a
        /// garage but no house.
        ///
        /// The streets of the smaller subdivision are already cut back to the bigger one's ring, so
        /// a house of it left inside the ring faces a street that is no longer there, at an angle to
        /// every house around it.
        /// </summary>
        private static void ClearOverlappingSubdivisions(List<Suburb> suburbs, Report report)
        {
            var city = GameObject.Find("City")?.transform;
            if (city == null)
            {
                return;
            }

            var plots = new List<(Transform Plot, int Rank, Bounds Floor)>();
            var siteGroup = city.Find("SiteBuildings");
            var sites = siteGroup == null
                ? new List<Bounds>()
                : siteGroup.Cast<Transform>().Select(site => Footprint(new[] { site.gameObject })).ToList();
            var doomed = new HashSet<GameObject>();

            for (var rank = 0; rank < suburbs.Count; rank++)
            {
                var group = city.Find($"Subdivision_{suburbs[rank].Block.Id}");
                if (group == null)
                {
                    continue;
                }

                foreach (Transform plot in group)
                {
                    if (plot.name is not ("House" or "Villa"))
                    {
                        continue;
                    }

                    var buildings = plot.Cast<Transform>().Where(child => child.name.StartsWith("building-")).ToList();

                    // The house stands on the plot's own point; a garage stands off to one side of it.
                    if (!buildings.Any(child => new Vector2(child.localPosition.x, child.localPosition.z).magnitude < 1f))
                    {
                        doomed.Add(plot.gameObject);
                        report.Count("subdivisions: plots left with a garage and no house");
                        continue;
                    }

                    var floor = Footprint(buildings.Select(child => child.gameObject));
                    var at = new Vector2(plot.position.x, plot.position.z);

                    if (sites.Any(site => Overlaps(site, floor, 0.75f)))
                    {
                        doomed.Add(plot.gameObject);
                        report.Count("subdivisions: houses standing on a map site's building");
                        continue;
                    }

                    if (Enumerable.Range(0, rank).Any(bigger => suburbs[bigger].Inside(at, 4f)))
                    {
                        doomed.Add(plot.gameObject);
                        report.Count("subdivisions: houses of a smaller subdivision inside a bigger one's streets");
                        continue;
                    }

                    plots.Add((plot, rank, floor));
                }
            }

            for (var i = 0; i < plots.Count; i++)
            {
                for (var j = i + 1; j < plots.Count; j++)
                {
                    var (a, rankA, floorA) = plots[i];
                    var (b, rankB, floorB) = plots[j];

                    if (rankA == rankB || doomed.Contains(a.gameObject) || doomed.Contains(b.gameObject)
                        || !Overlaps(floorA, floorB, 0.8f))
                    {
                        continue;
                    }

                    doomed.Add(rankA > rankB ? a.gameObject : b.gameObject);
                    report.Count("subdivisions: houses of a smaller subdivision standing on a bigger one's");
                }
            }

            Destroy(doomed);
        }

        private static bool Overlaps(Bounds a, Bounds b, float shrink) =>
            Mathf.Abs(a.center.x - b.center.x) < (a.extents.x + b.extents.x) * shrink
            && Mathf.Abs(a.center.z - b.center.z) < (a.extents.z + b.extents.z) * shrink;

        /// <summary>
        /// The grey kerb strips and the lamps the placeholder roads were dressed with.
        ///
        /// Every road tile carries its own pavement and kerb, so a separate strip is a second kerb
        /// beside the first where a road is, and a grey line across the grass where one was taken
        /// away — the thin lines left all over the port and the media district. A street lamp is kept
        /// only where it still stands at the side of a road.
        /// </summary>
        private static void ClearStreetFurniture(Layer layer, Report report)
        {
            var index = new RectIndex(layer.Rects);
            var city = GameObject.Find("City")?.transform;
            var doomed = new HashSet<GameObject>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject.name == "Sidewalk" && doomed.Add(renderer.gameObject))
                {
                    report.Count("cleared: kerb strips (the road tiles have their own)");
                }
            }

            if (city != null)
            {
                foreach (Transform group in city)
                {
                    if (group.name != "Streets" && !group.name.StartsWith("Subdivision_") && !group.name.StartsWith("Grid_"))
                    {
                        continue;
                    }

                    foreach (Transform lamp in group)
                    {
                        if (lamp.name is not ("light-square" or "light-curved"))
                        {
                            continue;
                        }

                        var at = new Vector2(lamp.position.x, lamp.position.z);

                        if (index.Contains(at, 0.5f))
                        {
                            doomed.Add(lamp.gameObject);
                            report.Count("cleared: street lamps standing in a road");
                        }
                        else if (!index.Contains(at, 7f))
                        {
                            doomed.Add(lamp.gameObject);
                            report.Count("cleared: street lamps beside a road that is no longer there");
                        }
                    }
                }
            }

            Destroy(doomed);
        }

        /// <summary>
        /// Takes the asphalt the terrain was painted with off everywhere a road is not.
        ///
        /// The terrain builder paints every road in the layout onto the ground, including the ones
        /// this network leaves out or cuts away — the suburban loops, the streets inside the grids,
        /// the stubs the river took. Under a laid road the paint is hidden; anywhere else it is a dark
        /// band through the grass leading nowhere, like the rings through both suburbs. Removing the
        /// asphalt weight and renormalising what is left gives exactly the ground the builder would
        /// have painted with no road there.
        /// </summary>
        private static void RepaintAsphalt(Transform city, Layer layer, Report report)
        {
            var terrain = city.GetComponentInChildren<Terrain>() ?? Object.FindFirstObjectByType<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;

            if (data == null)
            {
                report.Line("  WARNING: no terrain found; the asphalt paint was left as it was.");
                return;
            }

            var asphalt = Array.FindIndex(data.terrainLayers, painted => painted != null && painted.name == "Asphalt");
            if (asphalt < 0)
            {
                report.Line("  WARNING: the terrain has no Asphalt layer; nothing repainted.");
                return;
            }

            var index = new RectIndex(layer.Rects);
            var resolution = data.alphamapResolution;
            var layers = data.alphamapLayers;
            var map = data.GetAlphamaps(0, 0, resolution, resolution);
            var origin = terrain.transform.position;
            var repainted = 0;

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var weight = map[y, x, asphalt];
                    if (weight <= 0.001f)
                    {
                        continue;
                    }

                    var worldX = origin.x + x / (float)(resolution - 1) * data.size.x;
                    var worldZ = origin.z + y / (float)(resolution - 1) * data.size.z;

                    // A texel is about four metres; keep the paint that a road tile covers anyway.
                    if (index.Contains(new Vector2(worldX, worldZ), 2f))
                    {
                        continue;
                    }

                    var rest = 1f - weight;
                    map[y, x, asphalt] = 0f;

                    for (var other = 0; other < layers; other++)
                    {
                        if (other != asphalt)
                        {
                            map[y, x, other] = rest > 0.001f ? map[y, x, other] / rest : (other == 1 ? 1f : 0f);
                        }
                    }

                    repainted++;
                }
            }

            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            report.Line($"Ground: asphalt paint taken off {repainted} texels of grass with no road on them.");
        }

        private static Bounds Footprint(IEnumerable<GameObject> parts)
        {
            Bounds? bounds = null;

            foreach (var renderer in parts.SelectMany(part => part.GetComponentsInChildren<MeshRenderer>()))
            {
                if (bounds == null)
                {
                    bounds = renderer.bounds;
                }
                else
                {
                    var grown = bounds.Value;
                    grown.Encapsulate(renderer.bounds);
                    bounds = grown;
                }
            }

            return bounds ?? new Bounds();
        }

        private static void Destroy(IEnumerable<GameObject> doomed)
        {
            foreach (var go in doomed.ToList())
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// Keeps the parks' flat features off the roads and out of the water.
        ///
        /// An event lawn is a raised slab, so a street running under one simply disappears beneath
        /// the grass for a hundred metres — the civic gardens' lawn was laid across a street, and
        /// the park's hangs out over the bay. Each is cut down to the largest rectangle of it that is
        /// on dry land and clear of every road. A park lake the bay has since mostly taken is
        /// a darker disc floating on the sea, and goes.
        /// </summary>
        private static void FitGroundFeatures(Layer layer, Report report)
        {
            const float cell = 2f;
            var index = new RectIndex(layer.Rects);
            var doomed = new List<GameObject>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var feature = renderer.transform;

                if (feature.name == "Lake")
                {
                    var middle = new Vector2(feature.position.x, feature.position.z);
                    var reach = feature.lossyScale.x * 0.3f;
                    var wet = Enumerable.Range(0, 8)
                        .Select(i => middle + new Vector2(Mathf.Cos(i * Mathf.PI * 0.25f), Mathf.Sin(i * Mathf.PI * 0.25f)) * reach)
                        .Append(middle)
                        .Count(point => !IsDry(point));

                    if (wet >= 3)
                    {
                        doomed.Add(feature.gameObject);
                        report.Count("grounds: park lakes standing in the sea");
                    }

                    continue;
                }

                if (feature.name != "EventGround")
                {
                    continue;
                }

                var bounds = renderer.bounds;
                var columns = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x / cell));
                var rows = Mathf.Max(1, Mathf.RoundToInt(bounds.size.z / cell));
                var free = new bool[rows, columns];
                var blocked = 0;

                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var at = new Vector2(bounds.min.x + (column + 0.5f) * cell, bounds.min.z + (row + 0.5f) * cell);
                        free[row, column] = IsDry(at) && !index.Contains(at, 2f);
                        blocked += free[row, column] ? 0 : 1;
                    }
                }

                if (blocked == 0)
                {
                    continue;
                }

                var (top, left, height, width) = LargestClearRectangle(free);

                // Too little left to hold an event is not a lawn cut down but a slab on the water.
                if (height * width * cell * cell < SmallestEventLawn)
                {
                    doomed.Add(feature.gameObject);
                    report.Line($"  event lawn at ({bounds.center.x:0}, {bounds.center.z:0}) removed: less than "
                        + $"{SmallestEventLawn:0} m² of it is dry and clear of roads");
                    continue;
                }

                var middleX = bounds.min.x + (left + width * 0.5f) * cell;
                var middleZ = bounds.min.z + (top + height * 0.5f) * cell;
                var centre = new Vector3(middleX,
                    CityTerrainBuilder.HeightAt(middleX, middleZ) + feature.localScale.y * 0.5f, middleZ);

                feature.position = centre;
                feature.localScale = new Vector3(width * cell, feature.localScale.y, height * cell);

                report.Line($"  event lawn cut to {width * cell:0} x {height * cell:0} m clear of roads and water, "
                    + $"centred at ({centre.x:0}, {centre.z:0})");
            }

            Destroy(doomed);
        }

        /// <summary>The largest all-true rectangle in a grid, as its first row and column and its size.</summary>
        internal static (int Top, int Left, int Height, int Width) LargestClearRectangle(bool[,] free)
        {
            var rows = free.GetLength(0);
            var columns = free.GetLength(1);
            var heights = new int[columns];
            var best = (Top: 0, Left: 0, Height: 0, Width: 0);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    heights[column] = free[row, column] ? heights[column] + 1 : 0;
                }

                // Largest rectangle under the histogram of runs ending on this row.
                var stack = new Stack<int>();

                for (var column = 0; column <= columns; column++)
                {
                    var current = column == columns ? 0 : heights[column];

                    while (stack.Count > 0 && heights[stack.Peek()] >= current)
                    {
                        var tall = heights[stack.Pop()];
                        var left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                        var wide = column - left;

                        if (tall * wide > best.Height * best.Width)
                        {
                            best = (row - tall + 1, left, tall, wide);
                        }
                    }

                    stack.Push(column);
                }
            }

            return best;
        }

        /// <summary>
        /// A concrete driveway for every suburban house, from its garage door straight out to the
        /// street in front of it.
        ///
        /// The generator ran each driveway from the kerb to a door it had put on the house's back
        /// wall, so every one crossed its own plot on the diagonal, under the house and out behind
        /// it — the grey cross beside each house seen from above. And with the network rebuilt some
        /// houses no longer have the street they were built facing; those get no driveway rather
        /// than one leading into a garden.
        /// </summary>
        private static void LayDriveways(Layer layer, Report report)
        {
            var city = GameObject.Find("City")?.transform;
            var parent = city != null ? city.Find("GroundStage/Driveways") : null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SuburbanKit + "driveway-long.fbx");

            if (parent == null || model == null)
            {
                report.Line("  WARNING: no GroundStage/Driveways or no driveway model; driveways left as they were.");
                return;
            }

            while (parent.childCount > 0)
            {
                Object.DestroyImmediate(parent.GetChild(0).gameObject);
            }

            var index = new RectIndex(layer.Rects);
            var laid = 0;
            var facingNothing = 0;
            var orphaned = new List<GameObject>();

            foreach (Transform group in city)
            {
                if (!group.name.StartsWith("Subdivision_") && group.name != "FounderHome")
                {
                    continue;
                }

                foreach (Transform plot in group)
                {
                    var buildings = plot.Cast<Transform>().Where(child => child.name.StartsWith("building-")).ToList();
                    var garage = buildings
                        .Where(child => new Vector2(child.localPosition.x, child.localPosition.z).magnitude >= 1f)
                        .OrderBy(child => child.localPosition.sqrMagnitude)
                        .FirstOrDefault();

                    if (garage == null)
                    {
                        continue;
                    }

                    var forward = new Vector2(plot.forward.x, plot.forward.z).normalized;
                    var door = FrontOf(garage, forward);

                    // Out along the way the house faces until the first road.
                    var reach = -1f;

                    for (var step = 0.5f; step <= DrivewayReach; step += 0.5f)
                    {
                        if (index.Contains(door + forward * step, 0f))
                        {
                            reach = step;
                            break;
                        }
                    }

                    if (reach < 0f && group.name != "FounderHome"
                        && !index.Contains(new Vector2(plot.position.x, plot.position.z), OrphanedPlot))
                    {
                        // No street in front and none anywhere near: a house around a cul-de-sac
                        // head the rebuild took away, standing in a field.
                        orphaned.Add(plot.gameObject);
                        continue;
                    }

                    if (reach < 1.5f)
                    {
                        facingNothing += reach < 0f ? 1 : 0;
                        continue;
                    }

                    var strip = new GameObject("Driveway").transform;
                    strip.SetParent(parent, false);

                    var length = reach + 0.6f;
                    var pieces = Mathf.Max(1, Mathf.RoundToInt(length / DrivewayPiece));
                    var piece = length / pieces;

                    for (var n = 0; n < pieces; n++)
                    {
                        var from = door + forward * (piece * n);
                        var to = door + forward * (piece * (n + 1));
                        var middle = (from + to) * 0.5f;
                        var rise = CityTerrainBuilder.HeightAt(to.x, to.y) - CityTerrainBuilder.HeightAt(from.x, from.y);

                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, strip);
                        instance.transform.SetPositionAndRotation(
                            new Vector3(middle.x, CityTerrainBuilder.HeightAt(middle.x, middle.y) + 0.03f, middle.y),
                            Quaternion.LookRotation(new Vector3(forward.x, rise / piece, forward.y), Vector3.up));

                        // driveway-long is 0.36 wide and 0.4 long on Kenney's grid, along its local Z.
                        instance.transform.localScale = new Vector3(DrivewayWidth / 0.36f, DrivewayWidth / 0.36f, piece / 0.4f);
                    }

                    laid++;
                }
            }

            Destroy(orphaned);

            report.Line($"Driveways: {laid} laid from garage door to street; {facingNothing} houses face no street "
                + $"within {DrivewayReach:0} m and got none; {orphaned.Count} houses with no street within "
                + $"{OrphanedPlot:0} m at all taken away.");
        }

        /// <summary>The middle of a garage's front wall: the point of its model furthest along the way the house faces.</summary>
        private static Vector2 FrontOf(Transform garage, Vector2 forward)
        {
            var centre = new Vector2(garage.position.x, garage.position.z);
            var furthest = 0f;

            foreach (var filter in garage.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var local = filter.sharedMesh.bounds;

                for (var corner = 0; corner < 8; corner++)
                {
                    var point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var world = filter.transform.TransformPoint(point);
                    furthest = Mathf.Max(furthest, Vector2.Dot(new Vector2(world.x, world.z) - centre, forward));
                }
            }

            return centre + forward * furthest;
        }

        // ---- measuring crossings ---------------------------------------------------------------------

        /// <summary>The road pieces already in the scene, as rectangles on the ground.</summary>
        private static List<Rect2> SceneRoadRects()
        {
            var rects = new List<Rect2>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var name = renderer.gameObject.name;
                var transform = renderer.transform;
                var junction = name is "road-crossroad" or "road-intersection" or "road-bend";

                if (!junction && name is not ("road-straight" or "road-bridge"))
                {
                    continue;
                }

                var axisVector = name == "road-bridge" ? transform.forward : transform.right;
                var axis = new Vector2(axisVector.x, axisVector.z).normalized;
                var scale = transform.lossyScale;
                var length = name == "road-bridge" ? scale.z : scale.x;
                var width = name == "road-bridge" ? scale.x : scale.z;

                rects.Add(new Rect2(new Vector2(transform.position.x, transform.position.z), axis,
                    Mathf.Abs(length) * 0.5f, Mathf.Abs(width) * 0.5f, junction));
            }

            return rects;
        }

        /// <summary>
        /// Buildings whose footprint's middle is on a road, by model. Site buildings count too, so
        /// they are reported — they are never removed automatically.
        /// </summary>
        private static Dictionary<string, int> BuildingsOnRoads(List<Rect2> rects)
        {
            var index = new RectIndex(rects);
            var found = new Dictionary<string, int>();
            var seen = new HashSet<GameObject>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.gameObject.name.StartsWith("building-"))
                {
                    continue;
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? renderer.gameObject;
                if (!seen.Add(root))
                {
                    continue;
                }

                var bounds = renderer.bounds;
                if (index.Contains(new Vector2(bounds.center.x, bounds.center.z), 0f))
                {
                    found.TryGetValue(renderer.gameObject.name, out var count);
                    found[renderer.gameObject.name] = count + 1;
                }
            }

            return found;
        }

        private static int CountRectCrossings(List<Rect2> rects) => FindRectCrossings(rects).Count;

        private static List<(Vector2 At, string Roads)> FindRectCrossings(List<Rect2> rects)
        {
            var strips = rects.Where(r => !r.Junction).ToList();
            var joints = rects.Where(r => r.Junction).ToList();
            var index = new RectIndex(strips);
            var found = new List<(Vector2 At, string Roads)>();

            foreach (var a in strips)
            {
                foreach (var b in index.Near(a.Centre, a.HalfLength + a.HalfWidth))
                {
                    if (Mathf.Abs(Vector2.Dot(a.Axis, b.Axis)) > 0.8f || !Overlap(a, b))
                    {
                        continue;
                    }

                    var point = (a.Centre + b.Centre) * 0.5f;

                    if (joints.Any(j => j.Contains(point, 4f)) || found.Any(f => Vector2.Distance(f.At, point) < 20f))
                    {
                        continue;
                    }

                    found.Add((point, $"{a.Owner ?? "?"} x {b.Owner ?? "?"}"));
                }
            }

            return found;
        }

        private static bool Overlap(Rect2 a, Rect2 b)
        {
            foreach (var axis in new[] { a.Axis, new Vector2(-a.Axis.y, a.Axis.x), b.Axis, new Vector2(-b.Axis.y, b.Axis.x) })
            {
                var distance = Mathf.Abs(Vector2.Dot(b.Centre - a.Centre, axis));
                var reach = Radius(a, axis) + Radius(b, axis);

                if (distance > reach - 0.3f)
                {
                    return false;
                }
            }

            return true;
        }

        private static float Radius(Rect2 rect, Vector2 axis) =>
            rect.HalfLength * Mathf.Abs(Vector2.Dot(rect.Axis, axis))
            + rect.HalfWidth * Mathf.Abs(Vector2.Dot(new Vector2(-rect.Axis.y, rect.Axis.x), axis));

        private static bool Intersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4,
            out Vector2 point, out float t, out float u)
        {
            point = default;
            t = u = 0f;

            var r = p2 - p1;
            var s = p4 - p3;
            var denominator = r.x * s.y - r.y * s.x;

            if (Mathf.Abs(denominator) < 0.0001f)
            {
                return false;
            }

            var q = p3 - p1;
            t = (q.x * s.y - q.y * s.x) / denominator;
            u = (q.x * r.y - q.y * r.x) / denominator;

            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                return false;
            }

            point = p1 + r * t;
            return true;
        }

        // ---- indexes -------------------------------------------------------------------------------

        private sealed class SegmentIndex
        {
            private const float Cell = 48f;
            private readonly Dictionary<(int, int), List<(Way Way, int Segment)>> cells = new();

            public SegmentIndex(List<Way> ways)
            {
                foreach (var way in ways)
                {
                    for (var index = 0; index < way.Points.Count - 1; index++)
                    {
                        var a = way.Points[index];
                        var b = way.Points[index + 1];

                        for (var x = Floor(Mathf.Min(a.x, b.x)); x <= Floor(Mathf.Max(a.x, b.x)); x++)
                        {
                            for (var y = Floor(Mathf.Min(a.y, b.y)); y <= Floor(Mathf.Max(a.y, b.y)); y++)
                            {
                                if (!cells.TryGetValue((x, y), out var list))
                                {
                                    list = new List<(Way, int)>();
                                    cells[(x, y)] = list;
                                }

                                list.Add((way, index));
                            }
                        }
                    }
                }
            }

            private static int Floor(float value) => Mathf.FloorToInt(value / Cell);

            public IEnumerable<(Way A, int I, Way B, int J)> Pairs()
            {
                var seen = new HashSet<(Way, int, Way, int)>();

                foreach (var list in cells.Values)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        for (var j = i + 1; j < list.Count; j++)
                        {
                            var a = list[i];
                            var b = list[j];

                            if (a.Way == b.Way)
                            {
                                continue;
                            }

                            if (seen.Add((a.Way, a.Segment, b.Way, b.Segment)))
                            {
                                yield return (a.Way, a.Segment, b.Way, b.Segment);
                            }
                        }
                    }
                }
            }

            public IEnumerable<Way> Near(Vector2 point, float radius)
            {
                var found = new HashSet<Way>();

                for (var x = Floor(point.x - radius); x <= Floor(point.x + radius); x++)
                {
                    for (var y = Floor(point.y - radius); y <= Floor(point.y + radius); y++)
                    {
                        if (cells.TryGetValue((x, y), out var list))
                        {
                            foreach (var entry in list)
                            {
                                found.Add(entry.Way);
                            }
                        }
                    }
                }

                return found;
            }
        }

        private sealed class RectIndex
        {
            private const float Cell = 32f;
            private readonly Dictionary<(int, int), List<Rect2>> cells = new();

            public RectIndex(IEnumerable<Rect2> rects)
            {
                foreach (var rect in rects)
                {
                    var reach = rect.HalfLength + rect.HalfWidth;

                    for (var x = Floor(rect.Centre.x - reach); x <= Floor(rect.Centre.x + reach); x++)
                    {
                        for (var y = Floor(rect.Centre.y - reach); y <= Floor(rect.Centre.y + reach); y++)
                        {
                            if (!cells.TryGetValue((x, y), out var list))
                            {
                                list = new List<Rect2>();
                                cells[(x, y)] = list;
                            }

                            list.Add(rect);
                        }
                    }
                }
            }

            private static int Floor(float value) => Mathf.FloorToInt(value / Cell);

            public IEnumerable<Rect2> Near(Vector2 point, float radius)
            {
                var found = new HashSet<Rect2>();

                for (var x = Floor(point.x - radius); x <= Floor(point.x + radius); x++)
                {
                    for (var y = Floor(point.y - radius); y <= Floor(point.y + radius); y++)
                    {
                        if (cells.TryGetValue((x, y), out var list))
                        {
                            foreach (var rect in list)
                            {
                                found.Add(rect);
                            }
                        }
                    }
                }

                return found;
            }

            public bool Contains(Vector2 point, float margin) =>
                Near(point, 1f).Any(rect => rect.Contains(point, margin));
        }

        // ---- report --------------------------------------------------------------------------------

        private sealed class Report
        {
            private readonly List<string> lines = new();
            private readonly Dictionary<string, int> counts = new();

            public void Line(string text) => lines.Add(text);

            public void Count(string key)
            {
                counts.TryGetValue(key, out var seen);
                counts[key] = seen + 1;
            }

            public void Flush(string prefix)
            {
                foreach (var line in lines)
                {
                    Debug.Log($"{prefix} {line}");
                }

                foreach (var pair in counts.OrderBy(p => p.Key))
                {
                    Debug.Log($"{prefix} {pair.Key}: {pair.Value}");
                }
            }
        }
    }
}
