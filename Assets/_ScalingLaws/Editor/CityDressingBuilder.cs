using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Everything standing on the land: bridges, houses, towers, the park and the sea.
    ///
    /// **Split from the terrain builder because they fail differently.** The heightmap is arithmetic
    /// that either produces the right shape or does not; this is placement, and placement is where a
    /// city stops looking like a heightmap. Keeping them apart means the land can be re-cut without
    /// disturbing anything standing on it.
    ///
    /// Everything here is a box, and that is deliberate for now. The author's next step is dropping
    /// real house assets in and comparing them against these, so what matters is that the footprints,
    /// heights and spacings are right — a wrong-sized real house is harder to spot than a wrong-sized
    /// grey box.
    /// </summary>
    public static class CityDressingBuilder
    {
        /// <summary>Fixed, so the same houses land on the same driveways every run.</summary>
        private const int Seed = 77712;

        private static System.Random random;
        private static Func<float, float, float> ground;

        public static void BuildScene(TerrainData data, string scenePath,
            Func<float, float, float> heightAt)
        {
            if (!ScalingLawsSceneBuilder.MayOverwriteScene(scenePath))
            {
                Debug.LogWarning($"Kept {scenePath} as it is. The terrain asset was still rebuilt.");
                return;
            }

            random = new System.Random(Seed);
            ground = heightAt;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Bayview";

            var terrain = terrainObject.GetComponent<Terrain>();

            // The camera sees the whole city, so the basemap has to reach further than the default
            // thousand metres or the far half renders as flat colour.
            terrain.basemapDistance = 2400f;
            terrain.heightmapPixelError = 2f;

            BuildSea();
            BuildBridges();
            BuildSuburb("greendale", 44, 0.34f, true);
            BuildSuburb("riverdale", 52, 0.26f, false);
            BuildFounderHome();
            BuildDowntown();
            BuildMidRise("media", 26, 14f, 34f);
            BuildMidRise("innovation", 22, 16f, 40f);
            BuildMidRise("civic", 12, 12f, 26f);
            BuildMidRise("port", 18, 8f, 16f);
            BuildPark();
            BuildMarkers();
            BuildLighting();
            BuildCamera();

            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[Scaling Laws] City scene written to {scenePath}.");
        }

        // ---- water ------------------------------------------------------------------------------

        private static void BuildSea()
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";

            // A Unity plane is ten metres across. A fifth over the map, so the edge is never in
            // frame from a corner.
            sea.transform.localScale =
                new Vector3(CityLayout.Size / 10f * 1.2f, 1f, CityLayout.Size / 10f * 1.2f);

            sea.transform.position =
                new Vector3(CityLayout.Size / 2f, CityLayout.SeaLevel, CityLayout.Size / 2f);

            UnityEngine.Object.DestroyImmediate(sea.GetComponent<MeshCollider>());

            sea.GetComponent<MeshRenderer>().sharedMaterial =
                Paint("Sea", new Color(0.07f, 0.20f, 0.32f), 0.92f, 0.1f);
        }

        // ---- bridges ------------------------------------------------------------------------------

        /// <summary>
        /// A deck, two abutments and a row of piers, per span.
        ///
        /// The deck is lifted clear of the water rather than laid at road height, because a crossing
        /// at water level is a causeway and the author asked for bridges. The approaches ramp from
        /// the land up to the deck so nothing has a step in it.
        /// </summary>
        private static void BuildBridges()
        {
            var group = new GameObject("Bridges").transform;

            foreach (var span in CityLayout.Bridges)
            {
                var bridge = new GameObject(span.Id).transform;
                bridge.SetParent(group, false);

                var from = new Vector3(span.From.X, span.DeckHeight, span.From.Z);
                var to = new Vector3(span.To.X, span.DeckHeight, span.To.Z);

                var middle = (from + to) * 0.5f;
                var length = Vector3.Distance(from, to);
                var facing = Quaternion.LookRotation((to - from).normalized, Vector3.up);

                var deck = Box(bridge, "Deck", middle, new Vector3(span.Width, 2.4f, length),
                    Paint("BridgeDeck", new Color(0.18f, 0.18f, 0.20f)));

                deck.transform.rotation = facing;

                // Parapets, which is most of what makes a slab read as a bridge.
                foreach (var side in new[] { -1f, 1f })
                {
                    var rail = Box(bridge, "Parapet",
                        middle + facing * new Vector3(side * (span.Width * 0.5f - 0.6f), 2.0f, 0f),
                        new Vector3(1.2f, 2.0f, length),
                        Paint("BridgeRail", new Color(0.40f, 0.40f, 0.42f)));

                    rail.transform.rotation = facing;
                }

                for (var pier = 1; pier <= span.Piers; pier++)
                {
                    var t = pier / (float)(span.Piers + 1);
                    var at = Vector3.Lerp(from, to, t);
                    var bed = ground(at.x, at.z);
                    var height = Mathf.Max(6f, span.DeckHeight - bed);

                    Box(bridge, $"Pier{pier}",
                        new Vector3(at.x, bed + height * 0.5f, at.z),
                        new Vector3(7f, height, 9f),
                        Paint("BridgePier", new Color(0.30f, 0.30f, 0.31f)));
                }

                // Approach ramps: from the deck down to whatever the land is doing at each end.
                foreach (var end in new[] { from, to })
                {
                    var outward = (end - middle).normalized;
                    var landing = end + outward * 26f;
                    var landHeight = ground(landing.x, landing.z);

                    var ramp = Box(bridge, "Approach",
                        new Vector3((end.x + landing.x) * 0.5f,
                            (span.DeckHeight + landHeight) * 0.5f,
                            (end.z + landing.z) * 0.5f),
                        new Vector3(span.Width, 2.4f, 54f),
                        Paint("BridgeDeck", new Color(0.18f, 0.18f, 0.20f)));

                    ramp.transform.rotation =
                        Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z), Vector3.up);
                }
            }
        }

        // ---- housing --------------------------------------------------------------------------------

        /// <summary>
        /// American suburb: houses set back from a lane, each with a driveway to it.
        ///
        /// **Placed along the roads rather than on a grid**, because that is what makes a suburb read
        /// as a suburb: the plots follow the curve of the lane, every driveway points at the road,
        /// and the gaps between them are uneven. A grid of houses is a housing estate in a strategy
        /// game; this is where somebody lives.
        /// </summary>
        private static void BuildSuburb(string districtId, int count, float lawnChance, bool grand)
        {
            var district = FindDistrict(districtId);
            if (district == null)
            {
                return;
            }

            var group = new GameObject($"Houses_{districtId}").transform;

            var lanes = new List<CityTerrainBuilder.Centreline>();

            foreach (var road in CityTerrainBuilder.RoadCentrelines())
            {
                if (road.Class != RoadClass.Lane)
                {
                    continue;
                }

                // Only the lanes inside this district.
                var inside = 0;
                foreach (var point in road.Points)
                {
                    if (Vector2.Distance(point, new Vector2(district.CentreX, district.CentreZ))
                        < district.Radius + 90f)
                    {
                        inside++;
                    }
                }

                if (inside > road.Points.Count / 3)
                {
                    lanes.Add(road);
                }
            }

            if (lanes.Count == 0)
            {
                return;
            }

            var placed = 0;
            var attempts = 0;

            while (placed < count && attempts < count * 12)
            {
                attempts++;

                var lane = lanes[random.Next(lanes.Count)];
                var index = random.Next(1, Mathf.Max(2, lane.Points.Count - 1));

                var here = lane.Points[index];
                var next = lane.Points[Mathf.Min(index + 1, lane.Points.Count - 1)];

                var along = (next - here).normalized;
                if (along.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                var side = random.Next(2) == 0 ? 1f : -1f;
                var out2 = new Vector2(-along.y, along.x) * side;

                // The setback is what makes the long driveway the author asked for.
                var setback = grand ? Range(34f, 46f) : Range(22f, 30f);
                var plot = here + out2 * setback;

                if (Vector2.Distance(plot, new Vector2(district.CentreX, district.CentreZ))
                    > district.Radius)
                {
                    continue;
                }

                var groundHeight = ground(plot.x, plot.y);

                if (groundHeight < CityLayout.SeaLevel + 3f)
                {
                    continue;
                }

                var facing = Quaternion.LookRotation(new Vector3(-out2.x, 0f, -out2.y), Vector3.up);

                BuildHouse(group, new Vector3(plot.x, groundHeight, plot.y), facing, grand,
                    lawnChance);

                // The driveway: a strip of asphalt from the kerb to the garage door.
                var kerb = here + out2 * 6f;
                var driveMiddle = (kerb + plot) / 2f;

                var drive = Box(group, "Driveway",
                    new Vector3(driveMiddle.x, groundHeight + 0.08f, driveMiddle.y),
                    new Vector3(4.2f, 0.16f, Vector2.Distance(kerb, plot)),
                    Paint("Driveway", new Color(0.20f, 0.20f, 0.21f)));

                drive.transform.rotation = facing;

                placed++;
            }
        }

        /// <summary>One house: body, roof, garage, and usually a lawn and a tree.</summary>
        private static void BuildHouse(Transform parent, Vector3 at, Quaternion facing, bool grand,
            float lawnChance)
        {
            var house = new GameObject(grand ? "Villa" : "House").transform;
            house.SetParent(parent, false);
            house.position = at;
            house.rotation = facing;

            var width = grand ? Range(13f, 17f) : Range(9f, 12f);
            var depth = grand ? Range(11f, 14f) : Range(8f, 10f);
            var storeys = grand ? (random.Next(10) > 5 ? 2 : 1) : 1;
            var height = 3.2f * storeys;

            var walls = new[]
            {
                new Color(0.86f, 0.84f, 0.78f), new Color(0.78f, 0.74f, 0.68f),
                new Color(0.70f, 0.72f, 0.74f), new Color(0.82f, 0.76f, 0.70f),
                new Color(0.62f, 0.66f, 0.62f)
            };

            Local(house, "Body", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth),
                Paint($"Wall{random.Next(walls.Length)}", walls[random.Next(walls.Length)]));

            // The roof is a flattened, slightly oversized box. A real pitched roof is a mesh, and a
            // box that overhangs reads as a roof from the only distance this is ever seen at.
            Local(house, "Roof", new Vector3(0f, height + 0.9f, 0f),
                new Vector3(width + 1.6f, 1.8f, depth + 1.6f),
                Paint("Roof", new Color(0.32f, 0.26f, 0.24f)));

            // The garage, offset to one side and facing the road, which is where the driveway ends.
            Local(house, "Garage", new Vector3(width * 0.5f + 2.6f, 1.6f, depth * 0.28f),
                new Vector3(5.4f, 3.2f, 6.2f),
                Paint("Garage", new Color(0.74f, 0.72f, 0.68f)));

            if (random.NextDouble() < lawnChance)
            {
                Local(house, "Hedge", new Vector3(0f, 0.7f, -depth * 0.5f - 4.5f),
                    new Vector3(width + 6f, 1.4f, 1.2f),
                    Paint("Hedge", new Color(0.20f, 0.34f, 0.19f)));
            }

            if (random.NextDouble() < 0.55)
            {
                var treeX = (random.Next(2) == 0 ? -1f : 1f) * (width * 0.5f + Range(3f, 6f));

                Local(house, "TreeTrunk", new Vector3(treeX, 1.6f, -depth * 0.3f),
                    new Vector3(0.6f, 3.2f, 0.6f),
                    Paint("Trunk", new Color(0.28f, 0.21f, 0.15f)));

                Local(house, "TreeCanopy", new Vector3(treeX, 4.6f, -depth * 0.3f),
                    new Vector3(4.4f, 4.0f, 4.4f),
                    Paint("Canopy", new Color(0.18f, 0.33f, 0.17f)));
            }
        }

        /// <summary>The founder's house, placed by hand so the author can find it every time.</summary>
        private static void BuildFounderHome()
        {
            var at = CityLayout.FounderHome;
            var height = ground(at.X, at.Z);

            var group = new GameObject("FounderHome").transform;
            group.position = new Vector3(at.X, height, at.Z);

            BuildHouse(group, new Vector3(at.X, height, at.Z),
                Quaternion.Euler(0f, 205f, 0f), false, 1f);

            // A marker above it, so it is findable in the hierarchy and in the scene view without
            // hunting through fifty identical boxes.
            var pin = Box(group, "FounderPin",
                new Vector3(at.X, height + 16f, at.Z), new Vector3(2f, 14f, 2f),
                Paint("FounderPin", new Color(0.90f, 0.72f, 0.24f)));

            pin.name = "FounderPin";
        }

        // ---- downtown --------------------------------------------------------------------------------

        /// <summary>
        /// The financial district: towers on the blocks between the grid streets.
        ///
        /// Heights fall off from the middle, which is what every real skyline does and what makes a
        /// cluster of boxes read as a downtown rather than as a bar chart.
        /// </summary>
        private static void BuildDowntown()
        {
            var district = FindDistrict("downtown");
            if (district == null)
            {
                return;
            }

            var group = new GameObject("Downtown").transform;

            var centre = new Vector2(district.CentreX, district.CentreZ);
            var placed = 0;
            var attempts = 0;

            while (placed < 46 && attempts < 900)
            {
                attempts++;

                var angle = random.NextDouble() * Math.PI * 2.0;
                var radius = (float)Math.Sqrt(random.NextDouble()) * district.Radius * 0.92f;

                var at = centre + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius);

                // Off the roads. A tower in the middle of a street is the one placement error that
                // is obvious from any distance.
                if (TooCloseToRoad(at, 24f))
                {
                    continue;
                }

                var groundHeight = ground(at.x, at.y);
                var falloff = 1f - radius / (district.Radius * 0.92f);

                var height = Mathf.Lerp(24f, 165f, falloff * falloff) * Range(0.72f, 1.28f);
                var footprint = Mathf.Clamp(height * 0.26f, 14f, 42f);

                var tint = 0.30f + (float)random.NextDouble() * 0.22f;

                Box(group, "Tower",
                    new Vector3(at.x, groundHeight + height * 0.5f, at.y),
                    new Vector3(footprint, height, footprint * Range(0.8f, 1.2f)),
                    Paint($"Tower{placed % 6}", new Color(tint, tint + 0.02f, tint + 0.06f), 0.35f));

                placed++;
            }
        }

        /// <summary>Lower buildings for the districts that are not the skyline.</summary>
        private static void BuildMidRise(string districtId, int count, float low, float high)
        {
            var district = FindDistrict(districtId);
            if (district == null)
            {
                return;
            }

            var group = new GameObject($"Blocks_{districtId}").transform;
            var centre = new Vector2(district.CentreX, district.CentreZ);

            var placed = 0;
            var attempts = 0;

            while (placed < count && attempts < count * 20)
            {
                attempts++;

                var angle = random.NextDouble() * Math.PI * 2.0;
                var radius = (float)Math.Sqrt(random.NextDouble()) * district.Radius * 0.88f;

                var at = centre + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius);

                if (TooCloseToRoad(at, 20f))
                {
                    continue;
                }

                var groundHeight = ground(at.x, at.y);

                if (groundHeight < CityLayout.SeaLevel + 2f)
                {
                    continue;
                }

                var height = Range(low, high);
                var tint = 0.34f + (float)random.NextDouble() * 0.18f;

                Box(group, "Block",
                    new Vector3(at.x, groundHeight + height * 0.5f, at.y),
                    new Vector3(Range(16f, 32f), height, Range(14f, 28f)),
                    Paint($"Block{placed % 5}", new Color(tint, tint, tint + 0.03f)));

                placed++;
            }
        }

        /// <summary>
        /// The park: a lake, trees around it and the open ground events are held on.
        ///
        /// It is the one district with no buildings on purpose — the author's note calls it the
        /// cheapest place in Bayview to be seen, and an empty lawn is what that means.
        /// </summary>
        private static void BuildPark()
        {
            var district = FindDistrict("park");
            if (district == null)
            {
                return;
            }

            var group = new GameObject("BayviewPark").transform;
            var centre = new Vector2(district.CentreX, district.CentreZ);

            var lake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lake.name = "Lake";
            lake.transform.SetParent(group, false);
            lake.transform.position =
                new Vector3(centre.x - 40f, district.GroundHeight + 0.3f, centre.y + 20f);

            lake.transform.localScale = new Vector3(150f, 0.4f, 110f);
            UnityEngine.Object.DestroyImmediate(lake.GetComponent<CapsuleCollider>());

            lake.GetComponent<MeshRenderer>().sharedMaterial =
                Paint("Lake", new Color(0.10f, 0.26f, 0.34f), 0.9f, 0.05f);

            // The open event ground, marked out so it reads as a place rather than as grass.
            Box(group, "EventGround",
                new Vector3(centre.x + 90f, district.GroundHeight + 0.1f, centre.y - 60f),
                new Vector3(120f, 0.2f, 90f),
                Paint("EventGround", new Color(0.42f, 0.40f, 0.33f)));

            for (var index = 0; index < 90; index++)
            {
                var angle = random.NextDouble() * Math.PI * 2.0;
                var radius = (float)Math.Sqrt(random.NextDouble()) * district.Radius * 0.95f;

                var at = centre + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius);

                // Not in the lake and not on the event ground.
                if (Vector2.Distance(at, new Vector2(centre.x - 40f, centre.y + 20f)) < 90f)
                {
                    continue;
                }

                if (TooCloseToRoad(at, 14f))
                {
                    continue;
                }

                var groundHeight = ground(at.x, at.y);
                var scale = Range(0.8f, 1.5f);

                Box(group, "Trunk",
                    new Vector3(at.x, groundHeight + 2f * scale, at.y),
                    new Vector3(0.8f, 4f * scale, 0.8f),
                    Paint("Trunk", new Color(0.28f, 0.21f, 0.15f)));

                Box(group, "Canopy",
                    new Vector3(at.x, groundHeight + 5.6f * scale, at.y),
                    new Vector3(6f * scale, 5f * scale, 6f * scale),
                    Paint("Canopy", new Color(0.18f, 0.33f, 0.17f)));
            }
        }

        // ---- scene furniture --------------------------------------------------------------------------

        private static void BuildMarkers()
        {
            var group = new GameObject("Districts").transform;

            foreach (var district in CityLayout.Districts)
            {
                var marker = new GameObject(district.DisplayName);
                marker.transform.SetParent(group, false);
                marker.transform.position = new Vector3(
                    district.CentreX,
                    ground(district.CentreX, district.CentreZ),
                    district.CentreZ);
            }
        }

        private static void BuildLighting()
        {
            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.18f;
            sun.color = new Color(1f, 0.96f, 0.90f);
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
        }

        private static void BuildCamera()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.08f, 0.13f);
            camera.farClipPlane = 7000f;
            camera.fieldOfView = 38f;

            // The reference's angle: from the south west, high enough to hold the whole city.
            // Pulled back and raised after the first render cut Riverdale off the bottom right.
            cameraObject.transform.rotation = Quaternion.Euler(36f, 36f, 0f);
            cameraObject.transform.position = new Vector3(-1150f, 2050f, -1250f);
        }

        // ---- helpers ------------------------------------------------------------------------------------

        private static DistrictDefinition FindDistrict(string id)
        {
            foreach (var district in CityLayout.Districts)
            {
                if (district.Id == id)
                {
                    return district;
                }
            }

            return null;
        }

        private static bool TooCloseToRoad(Vector2 at, float clearance)
        {
            foreach (var road in CityTerrainBuilder.RoadCentrelines())
            {
                CityTerrainBuilder.NearestOnPolyline(at.x, at.y, road.Points, out var distance);

                if (distance < road.Width * 0.5f + clearance)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Range(float low, float high) =>
            low + (float)random.NextDouble() * (high - low);

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, true);
            box.transform.position = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            return box;
        }

        /// <summary>A box positioned in its parent's frame, so it turns with the house it is part of.</summary>
        private static void Local(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        }

        private static readonly Dictionary<string, Material> Paints = new();

        /// <summary>
        /// One material per name, shared.
        ///
        /// The shader is looked up rather than named: a URP shader under the built-in pipeline draws
        /// magenta rather than failing, which is a bug that only shows up on screen.
        /// </summary>
        private static Material Paint(string name, Color colour, float smoothness = 0.15f,
            float metallic = 0f)
        {
            if (Paints.TryGetValue(name, out var cached) && cached != null)
            {
                return cached;
            }

            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            shader = shader != null ? shader : Shader.Find("Standard");

            var material = new Material(shader) { name = name };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", colour);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            Paints[name] = material;
            return material;
        }
    }
}
