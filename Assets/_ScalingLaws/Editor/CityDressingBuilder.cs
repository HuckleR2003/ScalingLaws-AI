using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Everything standing on the land: streets, pavements, plots, houses, towers, the gallery,
    /// the parks and the bridges.
    ///
    /// **The suburbs are surveyed rather than sprinkled.** The first version dropped houses at
    /// random points along a curving lane and it read as a village. An American suburb is a
    /// subdivision: parallel streets at a fixed spacing, a collector along one edge, cul-de-sacs
    /// off it, and every plot the same width with every house the same distance back from the kerb.
    /// So this walks each block from <see cref="CityBlocks"/>, lays the streets, subdivides the
    /// frontage into lots and puts one house on each — which is the only way to get the regularity
    /// that makes a map look like somewhere real.
    ///
    /// Everything placed carries a <see cref="CityProp"/>: what it is, how big the space is, which
    /// district it is in. That is what makes a real asset a transform copy later rather than a
    /// redesign.
    /// </summary>
    public static class CityDressingBuilder
    {
        /// <summary>Fixed, so the same houses land on the same plots every run.</summary>
        private const int Seed = 77712;

        /// <summary>Metres of pavement either side of a residential street.</summary>
        /// <summary>
        /// Metres of pavement either side of a street.
        ///
        /// Narrowed from 2.2 after the plan render: at this map scale a wide pale strip either side
        /// of every road turned the whole city into a chalk drawing, and the pavements were reading
        /// louder than the roads they edge.
        /// </summary>
        private const float SidewalkWidth = 1.6f;

        /// <summary>Metres between street lamps. Real spacing is about this.</summary>
        private const float LampSpacing = 44f;

        /// <summary>How far a cul-de-sac runs off its collector, and how wide its bulb is.</summary>
        private const float CulDeSacLength = 90f;

        private const float CulDeSacRadius = 17f;

        private static System.Random random;
        private static Func<float, float, float> ground;
        private static Transform root;

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
            terrain.basemapDistance = 2400f;
            terrain.heightmapPixelError = 2f;

            root = new GameObject("City").transform;

            BuildSea();
            BuildArterialSidewalks();
            BuildBridges();

            var houses = 0;
            foreach (var block in CityBlocks.Residential)
            {
                houses += BuildSubdivision(block);
            }

            BuildFounderHome();

            var buildings = 0;
            foreach (var grid in CityBlocks.Grids)
            {
                buildings += BuildGrid(grid);
            }

            foreach (var mall in CityBlocks.Malls)
            {
                BuildMall(mall);
            }

            foreach (var park in CityBlocks.Parks)
            {
                BuildPark(park);
            }

            BuildMarkers();
            BuildLighting();
            BuildCamera();

            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[Scaling Laws] City dressed: {houses} houses on surveyed plots, "
                + $"{buildings} blocks and towers, {CityBlocks.Malls.Count} gallery, "
                + $"{CityBlocks.Parks.Count} parks, {CityLayout.Bridges.Count} bridges.");
        }

        // ---- water --------------------------------------------------------------------------------

        private static void BuildSea()
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            sea.transform.SetParent(root, true);

            sea.transform.localScale =
                new Vector3(CityLayout.Size / 10f * 1.2f, 1f, CityLayout.Size / 10f * 1.2f);

            sea.transform.position =
                new Vector3(CityLayout.Size / 2f, CityLayout.SeaLevel, CityLayout.Size / 2f);

            UnityEngine.Object.DestroyImmediate(sea.GetComponent<MeshCollider>());

            sea.GetComponent<MeshRenderer>().sharedMaterial =
                Paint("Sea", new Color(0.07f, 0.20f, 0.32f), 0.92f, 0.1f);
        }

        // ---- the streets in the data -------------------------------------------------------------

        /// <summary>
        /// Pavements and lamps down the arterial roads.
        ///
        /// The road surface itself is painted into the terrain by the terrain builder, so what is
        /// missing is everything that stands beside it. A road with no kerb, no pavement and no
        /// lighting reads as a track through a field however well it is surfaced.
        /// </summary>
        private static void BuildArterialSidewalks()
        {
            var group = new GameObject("Streets").transform;
            group.SetParent(root, true);

            foreach (var road in CityTerrainBuilder.RoadCentrelines())
            {
                if (road.Class == RoadClass.Lane)
                {
                    // Suburban lanes get theirs from the subdivision that owns them, so the kerb
                    // and the plots are surveyed together and cannot disagree.
                    continue;
                }

                Pavements(group, road.Points, road.Width, road.Class == RoadClass.Highway);
            }
        }

        /// <summary>Lays a pavement strip and a run of lamps down both sides of a centreline.</summary>
        private static void Pavements(Transform parent, IReadOnlyList<Vector2> points, float width,
            bool lampsBothSides)
        {
            var travelled = 0f;
            var nextLamp = LampSpacing * 0.5f;

            for (var index = 0; index < points.Count - 1; index++)
            {
                var a = points[index];
                var b = points[index + 1];

                var along = b - a;
                var length = along.magnitude;

                if (length < 0.1f)
                {
                    continue;
                }

                var direction = along / length;
                var across = new Vector2(-direction.y, direction.x);
                var middle = (a + b) * 0.5f;

                foreach (var side in new[] { -1f, 1f })
                {
                    var at = middle + across * side * (width * 0.5f + SidewalkWidth * 0.5f);
                    var height = ground(at.x, at.y);

                    if (height < CityLayout.SeaLevel + 1.5f)
                    {
                        continue;
                    }

                    var slab = Box(parent, "Sidewalk",
                        new Vector3(at.x, height + 0.14f, at.y),
                        new Vector3(SidewalkWidth, 0.28f, length + 0.4f),
                        Paint("Sidewalk", new Color(0.38f, 0.38f, 0.37f)));

                    slab.transform.rotation =
                        Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);

                    Describe(slab, CityPropKind.Sidewalk,
                        new Vector3(SidewalkWidth, 0.28f, length), string.Empty, 0);
                }

                travelled += length;

                while (travelled >= nextLamp)
                {
                    nextLamp += LampSpacing;

                    var sides = lampsBothSides ? new[] { -1f, 1f } : new[] { 1f };

                    foreach (var side in sides)
                    {
                        var at = middle + across * side * (width * 0.5f + SidewalkWidth + 0.4f);
                        Lamp(parent, at);
                    }
                }
            }
        }

        private static void Lamp(Transform parent, Vector2 at)
        {
            var height = ground(at.x, at.y);

            if (height < CityLayout.SeaLevel + 1.5f)
            {
                return;
            }

            var post = Box(parent, "StreetLamp",
                new Vector3(at.x, height + 4.2f, at.y),
                new Vector3(0.34f, 8.4f, 0.34f),
                Paint("LampPost", new Color(0.24f, 0.25f, 0.27f)));

            Describe(post, CityPropKind.StreetLamp, new Vector3(0.34f, 8.4f, 0.34f), string.Empty, 0);

            Box(parent, "LampHead",
                new Vector3(at.x, height + 8.5f, at.y),
                new Vector3(1.5f, 0.4f, 0.6f),
                Paint("LampHead", new Color(0.72f, 0.70f, 0.60f)));
        }

        // ---- subdivisions ---------------------------------------------------------------------------

        /// <summary>
        /// Surveys one subdivision: collector, streets, cul-de-sacs, then a house on every plot.
        ///
        /// The order is the order a developer would do it in, and it matters. The streets decide
        /// where the frontage is; the frontage decides where the plots are; the plots decide where
        /// the houses go. Placing houses first and drawing roads afterwards is what produced the
        /// village that this replaced.
        /// </summary>
        private static int BuildSubdivision(ResidentialBlock block)
        {
            var group = new GameObject($"Subdivision_{block.Id}").transform;
            group.SetParent(root, true);

            var centre = new Vector2(block.CentreX, block.CentreZ);
            var angle = block.RotationDegrees * Mathf.Deg2Rad;

            // Local axes: "along" runs down a street, "across" steps from one street to the next.
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-along.y, along.x);

            var halfWidth = block.Width * 0.5f;
            var halfDepth = block.Depth * 0.5f;

            const float streetWidth = 10f;
            var placed = 0;

            // The collector, along the western edge of the block. Every street hangs off it.
            var collectorAt = centre - across * (halfWidth + 14f);
            var collectorFrom = collectorAt - along * halfDepth;
            var collectorTo = collectorAt + along * halfDepth;

            StreetStrip(group, collectorFrom, collectorTo, 13f, true);

            var lanes = Mathf.Max(1, Mathf.FloorToInt(block.Width / block.StreetSpacing));

            for (var lane = 0; lane < lanes; lane++)
            {
                var offset = -halfWidth + block.StreetSpacing * (lane + 0.5f);
                var lineAt = centre + across * offset;

                var from = lineAt - along * halfDepth;
                var to = lineAt + along * halfDepth;

                StreetStrip(group, from, to, streetWidth, true);

                // Plots down both sides. Backs meet in the middle of the block, which is why the
                // spacing is two lot depths plus the road.
                foreach (var side in new[] { -1f, 1f })
                {
                    placed += Plots(group, block, from, to, along, across * side, streetWidth);
                }
            }

            for (var index = 0; index < block.CulDeSacs; index++)
            {
                var t = (index + 1f) / (block.CulDeSacs + 1f);
                var mouth = Vector2.Lerp(collectorFrom, collectorTo, t);
                var head = mouth - across * CulDeSacLength;

                StreetStrip(group, mouth, head, 9f, true);

                // The bulb: a disc of asphalt with plots facing into it.
                if (ground(head.x, head.y) < CityLayout.SeaLevel + 2.5f)
                {
                    continue;
                }

                var bulb = Cylinder(group, "CulDeSac", head, CulDeSacRadius * 2f, 0.24f,
                    Paint("Asphalt", new Color(0.17f, 0.17f, 0.19f)));

                Describe(bulb, CityPropKind.RoadSegment,
                    new Vector3(CulDeSacRadius * 2f, 0.24f, CulDeSacRadius * 2f), block.DistrictId, 0);

                for (var slot = 0; slot < 5; slot++)
                {
                    var spoke = Mathf.PI * (0.2f + 0.6f * slot / 4f) - Mathf.PI * 0.5f;
                    var facing = new Vector2(
                        Mathf.Cos(spoke) * -across.x + Mathf.Sin(spoke) * along.x,
                        Mathf.Cos(spoke) * -across.y + Mathf.Sin(spoke) * along.y);

                    var kerb = head + facing * (CulDeSacRadius + 1f);
                    var plot = head + facing * (CulDeSacRadius + block.Setback + 7f);

                    if (PlaceHouse(group, block, plot, kerb, -facing))
                    {
                        placed++;
                    }
                }
            }

            return placed;
        }

        /// <summary>
        /// Subdivides one side of one street into lots and puts a house on each.
        ///
        /// The lots are laid from one end at a fixed width, which is what gives a street its rhythm.
        /// A plot is skipped when the ground under it is water or too steep, and skipping leaves a
        /// gap rather than shuffling everything along — real subdivisions have gaps too, and
        /// shuffling would break the rhythm that is the entire point.
        /// </summary>
        private static int Plots(Transform parent, ResidentialBlock block, Vector2 from, Vector2 to,
            Vector2 along, Vector2 outward, float streetWidth)
        {
            var length = Vector2.Distance(from, to);
            var count = Mathf.FloorToInt(length / block.LotWidth);
            var placed = 0;

            for (var index = 0; index < count; index++)
            {
                var t = (index + 0.5f) * block.LotWidth / length;
                var frontage = Vector2.Lerp(from, to, t);

                var kerb = frontage + outward * (streetWidth * 0.5f + SidewalkWidth);
                var plot = kerb + outward * block.Setback;

                if (PlaceHouse(parent, block, plot, kerb, -outward))
                {
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// One house, its garage, its driveway and usually a tree, on a surveyed plot.
        ///
        /// Returns false when the plot is unbuildable, which is how the coastline and the hills get
        /// their ragged edges without anybody drawing them.
        /// </summary>
        private static bool PlaceHouse(Transform parent, ResidentialBlock block, Vector2 plot,
            Vector2 kerb, Vector2 facing)
        {
            var height = ground(plot.x, plot.y);

            if (height < CityLayout.SeaLevel + 3f)
            {
                return false;
            }

            // Refuse a plot the land is falling away under. Cheaper than terracing and it is what
            // gives the subdivision its irregular outer edge.
            var slope = Mathf.Abs(height - ground(plot.x + 12f, plot.y))
                + Mathf.Abs(height - ground(plot.x, plot.y + 12f));

            if (slope > 7f)
            {
                return false;
            }

            var rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);
            var variant = random.Next(6);

            var house = new GameObject(block.Grand ? "Villa" : "House").transform;
            house.SetParent(parent, false);
            house.position = new Vector3(plot.x, height, plot.y);
            house.rotation = rotation;

            var width = block.Grand ? Range(14f, 17f) : Range(10f, 13f);
            var depth = block.Grand ? Range(12f, 15f) : Range(9f, 11f);
            var storeys = block.Grand ? (random.Next(10) > 4 ? 2 : 1) : (random.Next(10) > 7 ? 2 : 1);
            var wallHeight = 3.2f * storeys;

            var walls = new[]
            {
                new Color(0.86f, 0.84f, 0.78f), new Color(0.78f, 0.74f, 0.68f),
                new Color(0.70f, 0.72f, 0.74f), new Color(0.82f, 0.76f, 0.70f),
                new Color(0.62f, 0.66f, 0.62f), new Color(0.74f, 0.68f, 0.62f)
            };

            var body = Local(house, "Body", new Vector3(0f, wallHeight * 0.5f, 0f),
                new Vector3(width, wallHeight, depth),
                Paint($"Wall{variant}", walls[variant]));

            Describe(body, block.Grand ? CityPropKind.Villa : CityPropKind.House,
                new Vector3(width, wallHeight, depth), block.DistrictId, variant);

            Local(house, "Roof", new Vector3(0f, wallHeight + 0.9f, 0f),
                new Vector3(width + 1.6f, 1.8f, depth + 1.6f),
                Paint("Roof", new Color(0.32f, 0.26f, 0.24f)));

            // The garage sits on the side the driveway comes up, which is what makes the driveway
            // lead somewhere rather than stop at a wall.
            var garageSide = random.Next(2) == 0 ? -1f : 1f;

            var garage = Local(house, "Garage",
                new Vector3(garageSide * (width * 0.5f + 2.8f), 1.6f, depth * 0.22f),
                new Vector3(5.6f, 3.2f, 6.4f),
                Paint("Garage", new Color(0.74f, 0.72f, 0.68f)));

            Describe(garage, CityPropKind.Garage, new Vector3(5.6f, 3.2f, 6.4f),
                block.DistrictId, variant);

            // The driveway: kerb to garage door, offset to the garage side.
            var doorLocal = new Vector3(garageSide * (width * 0.5f + 2.8f), 0f, -depth * 0.5f - 1f);
            var door = house.TransformPoint(doorLocal);
            var start = new Vector3(kerb.x, height, kerb.y)
                + (door - new Vector3(plot.x, height, plot.y)).normalized * 0.5f;

            var driveMiddle = (door + start) * 0.5f;
            var driveLength = Vector3.Distance(door, start);

            var drive = Box(parent, "Driveway",
                new Vector3(driveMiddle.x, height + 0.1f, driveMiddle.z),
                new Vector3(4.4f, 0.2f, Mathf.Max(3f, driveLength)),
                Paint("Driveway", new Color(0.21f, 0.21f, 0.22f)));

            drive.transform.rotation = Quaternion.LookRotation(
                new Vector3(door.x - start.x, 0f, door.z - start.z).normalized, Vector3.up);

            Describe(drive, CityPropKind.Driveway, new Vector3(4.4f, 0.2f, driveLength),
                block.DistrictId, 0);

            // Front lawn furniture. A hedge or a tree, never both, so the street has variety.
            if (random.NextDouble() < 0.45)
            {
                Local(house, "Hedge", new Vector3(0f, 0.6f, -depth * 0.5f - block.Setback * 0.55f),
                    new Vector3(width * 0.8f, 1.2f, 0.9f),
                    Paint("Hedge", new Color(0.20f, 0.34f, 0.19f)));
            }
            else if (random.NextDouble() < 0.6)
            {
                var treeX = -garageSide * (width * 0.5f + Range(2.5f, 4.5f));
                var tree = Local(house, "Tree", new Vector3(treeX, 4.4f, -depth * 0.25f),
                    new Vector3(4.6f, 4.2f, 4.6f),
                    Paint("Canopy", new Color(0.18f, 0.33f, 0.17f)));

                Describe(tree, CityPropKind.Tree, new Vector3(4.6f, 7f, 4.6f), block.DistrictId, 0);

                Local(house, "Trunk", new Vector3(treeX, 1.5f, -depth * 0.25f),
                    new Vector3(0.6f, 3f, 0.6f),
                    Paint("Trunk", new Color(0.28f, 0.21f, 0.15f)));
            }

            return true;
        }

        /// <summary>Asphalt, kerbs, pavements and lamps for one straight run of residential street.</summary>
        private static void StreetStrip(Transform parent, Vector2 from, Vector2 to, float width,
            bool pavements)
        {
            var along = to - from;
            var length = along.magnitude;

            if (length < 1f)
            {
                return;
            }

            var direction = along / length;
            var facing = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);

            // Cut into pieces so the surface follows the ground rather than spanning a hill.
            var pieces = Mathf.Max(1, Mathf.CeilToInt(length / 28f));

            for (var piece = 0; piece < pieces; piece++)
            {
                var t0 = piece / (float)pieces;
                var t1 = (piece + 1f) / pieces;

                var a = Vector2.Lerp(from, to, t0);
                var b = Vector2.Lerp(from, to, t1);
                var middle = (a + b) * 0.5f;

                var height = ground(middle.x, middle.y);

                // **Nothing is laid below the waterline.** A subdivision reaching the coast has
                // streets that stop at the beach, and skipping the wet pieces is what gives the
                // built-up area its ragged outer edge without anybody drawing one.
                if (height < CityLayout.SeaLevel + 1.5f)
                {
                    continue;
                }

                var slab = Box(parent, "Street",
                    new Vector3(middle.x, height + 0.12f, middle.y),
                    new Vector3(width, 0.24f, length / pieces + 0.3f),
                    Paint("Asphalt", new Color(0.17f, 0.17f, 0.19f)));

                slab.transform.rotation = facing;

                Describe(slab, CityPropKind.RoadSegment,
                    new Vector3(width, 0.24f, length / pieces), string.Empty, 0);
            }

            if (pavements)
            {
                Pavements(parent, new List<Vector2> { from, to }, width, false);
            }
        }

        // ---- the founder ------------------------------------------------------------------------------

        /// <summary>
        /// The founder's house, on a plot of its own with a pin over it.
        ///
        /// Placed by hand rather than picked out of the subdivision, because the author needs to find
        /// it every time to stand a real asset next to it.
        /// </summary>
        private static void BuildFounderHome()
        {
            var at = CityLayout.FounderHome;
            var height = ground(at.X, at.Z);

            var group = new GameObject("FounderHome").transform;
            group.SetParent(root, true);
            group.position = new Vector3(at.X, height, at.Z);

            var house = new GameObject("FounderHouse").transform;
            house.SetParent(group, false);
            house.rotation = Quaternion.Euler(0f, 205f, 0f);

            var body = Local(house, "Body", new Vector3(0f, 1.7f, 0f), new Vector3(12f, 3.4f, 10f),
                Paint("FounderWall", new Color(0.84f, 0.80f, 0.72f)));

            Describe(body, CityPropKind.FounderHome, new Vector3(12f, 3.4f, 10f), "riverdale", 0);

            Local(house, "Roof", new Vector3(0f, 4.3f, 0f), new Vector3(13.6f, 1.8f, 11.6f),
                Paint("Roof", new Color(0.32f, 0.26f, 0.24f)));

            var garage = Local(house, "Garage", new Vector3(8.4f, 1.6f, 2.2f),
                new Vector3(5.6f, 3.2f, 6.4f), Paint("Garage", new Color(0.74f, 0.72f, 0.68f)));

            Describe(garage, CityPropKind.Garage, new Vector3(5.6f, 3.2f, 6.4f), "riverdale", 0);

            var pin = Box(group, "FounderPin", new Vector3(at.X, height + 20f, at.Z),
                new Vector3(2f, 18f, 2f), Paint("FounderPin", new Color(0.92f, 0.74f, 0.24f)));

            Describe(pin, CityPropKind.StreetFurniture, new Vector3(2f, 18f, 2f), "riverdale", 0);
        }

        // ---- grids -------------------------------------------------------------------------------------

        /// <summary>
        /// Streets on a grid, and a building filling each block between them.
        ///
        /// Heights fall off from the middle when the block is a skyline, which is what every real
        /// downtown does and what stops a cluster of boxes reading as a bar chart.
        /// </summary>
        private static int BuildGrid(GridBlock grid)
        {
            var group = new GameObject($"Grid_{grid.Id}").transform;
            group.SetParent(root, true);

            var centre = new Vector2(grid.CentreX, grid.CentreZ);
            var angle = grid.RotationDegrees * Mathf.Deg2Rad;

            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-along.y, along.x);

            var columns = Mathf.Max(1, Mathf.FloorToInt(grid.Width / grid.BlockSize));
            var rows = Mathf.Max(1, Mathf.FloorToInt(grid.Depth / grid.BlockSize));

            const float streetWidth = 17f;
            var placed = 0;

            // The streets, both ways, before anything is put between them.
            for (var column = 0; column <= columns; column++)
            {
                var offset = -grid.Width * 0.5f + grid.BlockSize * column;
                var line = centre + across * offset;

                StreetStrip(group, line - along * grid.Depth * 0.5f,
                    line + along * grid.Depth * 0.5f, streetWidth, true);
            }

            for (var row = 0; row <= rows; row++)
            {
                var offset = -grid.Depth * 0.5f + grid.BlockSize * row;
                var line = centre + along * offset;

                StreetStrip(group, line - across * grid.Width * 0.5f,
                    line + across * grid.Width * 0.5f, streetWidth, true);
            }

            var maximumReach = Mathf.Max(grid.Width, grid.Depth) * 0.5f;

            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    var offsetAcross = -grid.Width * 0.5f + grid.BlockSize * (column + 0.5f);
                    var offsetAlong = -grid.Depth * 0.5f + grid.BlockSize * (row + 0.5f);

                    var blockCentre = centre + across * offsetAcross + along * offsetAlong;

                    // The gallery has its own block. Nothing else goes on top of it.
                    if (InsideAMall(blockCentre))
                    {
                        continue;
                    }

                    // One to three buildings per block, so the skyline is not a chessboard.
                    var perBlock = grid.Skyline ? random.Next(2, 4) : random.Next(1, 3);

                    for (var slot = 0; slot < perBlock; slot++)
                    {
                        var jitter = new Vector2(Range(-1f, 1f), Range(-1f, 1f))
                            * (grid.BlockSize * 0.22f);

                        var at = blockCentre + across * jitter.x + along * jitter.y;
                        var height = ground(at.x, at.y);

                        if (height < CityLayout.SeaLevel + 2f)
                        {
                            continue;
                        }

                        var reach = Vector2.Distance(at, centre) / Mathf.Max(1f, maximumReach);
                        var falloff = Mathf.Clamp01(1f - reach);

                        var tall = grid.Skyline
                            ? Mathf.Lerp(grid.LowBuilding, grid.HighBuilding, falloff * falloff)
                              * Range(0.72f, 1.24f)
                            : Range(grid.LowBuilding, grid.HighBuilding);

                        var footprint = grid.Skyline
                            ? Mathf.Clamp(tall * 0.24f, 15f, 40f)
                            : Range(18f, 32f);

                        var depth = footprint * Range(0.8f, 1.25f);
                        var tint = 0.30f + (float)random.NextDouble() * 0.22f;

                        var tower = Box(group, grid.Skyline ? "Tower" : "Block",
                            new Vector3(at.x, height + tall * 0.5f, at.y),
                            new Vector3(footprint, tall, depth),
                            Paint($"Build{placed % 7}",
                                new Color(tint, tint + 0.02f, tint + 0.06f), 0.35f));

                        tower.transform.rotation =
                            Quaternion.Euler(0f, grid.RotationDegrees + Range(-4f, 4f), 0f);

                        Describe(tower, grid.Skyline ? CityPropKind.Tower : CityPropKind.Block,
                            new Vector3(footprint, tall, depth), grid.DistrictId, placed % 7);

                        placed++;
                    }
                }
            }

            return placed;
        }

        private static bool InsideAMall(Vector2 at)
        {
            foreach (var mall in CityBlocks.Malls)
            {
                var span = Mathf.Max(mall.LotWidth, mall.BuildingWidth) * 0.6f;

                if (Vector2.Distance(at, new Vector2(mall.CentreX, mall.CentreZ)) < span)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- the gallery ---------------------------------------------------------------------------------

        /// <summary>
        /// The shopping gallery, its car park and the bays in it.
        ///
        /// **The car park is drawn bay by bay rather than as one grey rectangle**, because a lot with
        /// aisles and rows is instantly readable as a car park and a plain slab is a helipad. It is
        /// also the surface an expo actually uses: the marquees go on the lot, not in the shop.
        /// </summary>
        private static void BuildMall(MallSite mall)
        {
            var group = new GameObject($"Mall_{mall.Id}").transform;
            group.SetParent(root, true);

            var centre = new Vector2(mall.CentreX, mall.CentreZ);
            var angle = mall.RotationDegrees * Mathf.Deg2Rad;

            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-along.y, along.x);

            var height = ground(centre.x, centre.y);
            var facing = Quaternion.Euler(0f, mall.RotationDegrees, 0f);

            // The lot first, so the building sits on it rather than beside it.
            var lotCentre = centre - along * (mall.BuildingDepth * 0.5f + mall.LotDepth * 0.5f + 8f);

            var apron = Box(group, "ParkingApron",
                new Vector3(lotCentre.x, height + 0.1f, lotCentre.y),
                new Vector3(mall.LotWidth, 0.2f, mall.LotDepth),
                Paint("Asphalt", new Color(0.17f, 0.17f, 0.19f)));

            apron.transform.rotation = facing;

            Describe(apron, CityPropKind.ParkingRow,
                new Vector3(mall.LotWidth, 0.2f, mall.LotDepth), "downtown", 0);

            // Rows of bays, in pairs back to back with an aisle between each pair.
            const float bayDepth = 5.2f;
            const float aisle = 6.4f;
            var pitch = bayDepth * 2f + aisle;
            var rows = Mathf.Max(1, Mathf.FloorToInt(mall.LotDepth / pitch));

            for (var row = 0; row < rows; row++)
            {
                var offset = -mall.LotDepth * 0.5f + pitch * (row + 0.5f);

                foreach (var side in new[] { -1f, 1f })
                {
                    var at = lotCentre + along * (offset + side * bayDepth * 0.5f);
                    var stripe = Box(group, "ParkingRow",
                        new Vector3(at.x, height + 0.22f, at.y),
                        new Vector3(mall.LotWidth - 14f, 0.12f, bayDepth),
                        Paint("ParkingLine", new Color(0.46f, 0.46f, 0.44f)));

                    stripe.transform.rotation = facing;

                    Describe(stripe, CityPropKind.ParkingRow,
                        new Vector3(mall.LotWidth - 14f, 0.12f, bayDepth), "downtown", row);
                }
            }

            // Lamps down the middle aisle, which is what a real lot has.
            for (var lamp = 0; lamp < 6; lamp++)
            {
                var t = (lamp + 0.5f) / 6f;
                var at = lotCentre + along * (-mall.LotDepth * 0.5f + mall.LotDepth * t);
                Lamp(group, at);
            }

            // The gallery itself: a long low hall with a taller entrance block.
            var hall = Box(group, "Gallery",
                new Vector3(centre.x, height + mall.BuildingHeight * 0.5f, centre.y),
                new Vector3(mall.BuildingWidth, mall.BuildingHeight, mall.BuildingDepth),
                Paint("MallWall", new Color(0.56f, 0.56f, 0.58f), 0.4f));

            hall.transform.rotation = facing;

            Describe(hall, CityPropKind.Mall,
                new Vector3(mall.BuildingWidth, mall.BuildingHeight, mall.BuildingDepth),
                "downtown", 0);

            var entrance = Box(group, "GalleryEntrance",
                new Vector3(
                    centre.x - along.x * (mall.BuildingDepth * 0.5f),
                    height + mall.BuildingHeight * 0.72f,
                    centre.y - along.y * (mall.BuildingDepth * 0.5f)),
                new Vector3(mall.BuildingWidth * 0.32f, mall.BuildingHeight * 1.45f, 18f),
                Paint("MallGlass", new Color(0.36f, 0.52f, 0.62f), 0.75f, 0.2f));

            entrance.transform.rotation = facing;

            // A roof line, so a long box reads as a building rather than as a wall.
            var roof = Box(group, "GalleryRoof",
                new Vector3(centre.x, height + mall.BuildingHeight + 1.2f, centre.y),
                new Vector3(mall.BuildingWidth + 4f, 2.4f, mall.BuildingDepth + 4f),
                Paint("MallRoof", new Color(0.38f, 0.38f, 0.40f)));

            roof.transform.rotation = facing;
        }

        // ---- parks ------------------------------------------------------------------------------------------

        private static void BuildPark(ParkSite park)
        {
            var group = new GameObject($"Park_{park.Id}").transform;
            group.SetParent(root, true);

            var centre = new Vector2(park.CentreX, park.CentreZ);
            var height = ground(centre.x, centre.y);

            if (park.HasLake)
            {
                var lake = Cylinder(group, "Lake",
                    centre + new Vector2(-park.Radius * 0.28f, park.Radius * 0.16f),
                    park.Radius * 0.72f, 0.5f,
                    Paint("Lake", new Color(0.10f, 0.26f, 0.34f), 0.9f, 0.05f));

                lake.transform.position = new Vector3(
                    lake.transform.position.x, height + 0.25f, lake.transform.position.z);
            }

            if (park.HasEventGround)
            {
                var lawn = Box(group, "EventGround",
                    new Vector3(centre.x + park.Radius * 0.42f, height + 0.12f,
                        centre.y - park.Radius * 0.3f),
                    new Vector3(park.Radius * 0.8f, 0.24f, park.Radius * 0.62f),
                    Paint("EventGround", new Color(0.40f, 0.42f, 0.30f)));

                Describe(lawn, CityPropKind.StreetFurniture,
                    new Vector3(park.Radius * 0.8f, 0.24f, park.Radius * 0.62f), park.Id, 0);
            }

            // Two crossing paths, which is what turns a lawn into a park.
            foreach (var turn in new[] { 20f, 110f })
            {
                var radians = turn * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                StreetStrip(group, centre - direction * park.Radius * 0.92f,
                    centre + direction * park.Radius * 0.92f, 4.2f, false);
            }

            for (var index = 0; index < park.Trees; index++)
            {
                var spin = random.NextDouble() * Math.PI * 2.0;
                var reach = (float)Math.Sqrt(random.NextDouble()) * park.Radius * 0.96f;

                var at = centre + new Vector2(
                    (float)Math.Cos(spin) * reach, (float)Math.Sin(spin) * reach);

                if (park.HasLake &&
                    Vector2.Distance(at, centre + new Vector2(-park.Radius * 0.28f,
                        park.Radius * 0.16f)) < park.Radius * 0.42f)
                {
                    continue;
                }

                var treeHeight = ground(at.x, at.y);

                if (treeHeight < CityLayout.SeaLevel + 2f)
                {
                    continue;
                }

                var scale = Range(0.85f, 1.55f);

                Box(group, "Trunk", new Vector3(at.x, treeHeight + 2f * scale, at.y),
                    new Vector3(0.8f, 4f * scale, 0.8f),
                    Paint("Trunk", new Color(0.28f, 0.21f, 0.15f)));

                var canopy = Box(group, "Canopy",
                    new Vector3(at.x, treeHeight + 5.8f * scale, at.y),
                    new Vector3(6.2f * scale, 5.2f * scale, 6.2f * scale),
                    Paint("Canopy", new Color(0.18f, 0.33f, 0.17f)));

                Describe(canopy, CityPropKind.Tree,
                    new Vector3(6.2f * scale, 9f * scale, 6.2f * scale), park.Id, 0);
            }
        }

        // ---- bridges ----------------------------------------------------------------------------------------

        /// <summary>
        /// A finished crossing: deck in segments, parapets, piers to the river bed, and ramps that
        /// meet the land at whatever height it happens to be.
        ///
        /// **The deck is segmented and the ramps are pitched.** A single slab from bank to bank was
        /// the thing that read as a plank laid over a stream: a real bridge has a rise in the middle
        /// and its approaches slope, and both are what make it look like it carries a road.
        /// </summary>
        private static void BuildBridges()
        {
            var group = new GameObject("Bridges").transform;
            group.SetParent(root, true);

            foreach (var span in CityLayout.Bridges)
            {
                var bridge = new GameObject(span.Id).transform;
                bridge.SetParent(group, false);

                var from = new Vector2(span.From.X, span.From.Z);
                var to = new Vector2(span.To.X, span.To.Z);

                var along = (to - from).normalized;
                var length = Vector2.Distance(from, to);
                var facing = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y), Vector3.up);

                // The deck, in segments, with a slight camber so it rises to the middle.
                const int segments = 10;

                for (var segment = 0; segment < segments; segment++)
                {
                    var t = (segment + 0.5f) / segments;
                    var at = Vector2.Lerp(from, to, t);

                    var camber = Mathf.Sin(t * Mathf.PI) * 3.5f;
                    var deckHeight = span.DeckHeight + camber;

                    var slab = Box(bridge, "Deck",
                        new Vector3(at.x, deckHeight, at.y),
                        new Vector3(span.Width, 2.2f, length / segments + 0.6f),
                        Paint("BridgeDeck", new Color(0.19f, 0.19f, 0.21f)));

                    slab.transform.rotation = facing;

                    Describe(slab, CityPropKind.BridgeDeck,
                        new Vector3(span.Width, 2.2f, length / segments), string.Empty, 0);

                    foreach (var side in new[] { -1f, 1f })
                    {
                        var offset = new Vector3(-along.y, 0f, along.x)
                            * (side * (span.Width * 0.5f - 0.7f));

                        var rail = Box(bridge, "Parapet",
                            new Vector3(at.x, deckHeight + 1.7f, at.y) + offset,
                            new Vector3(1.1f, 1.9f, length / segments + 0.6f),
                            Paint("BridgeRail", new Color(0.44f, 0.44f, 0.46f)));

                        rail.transform.rotation = facing;
                    }
                }

                for (var pier = 1; pier <= span.Piers; pier++)
                {
                    var t = pier / (float)(span.Piers + 1);
                    var at = Vector2.Lerp(from, to, t);
                    var bed = ground(at.x, at.y);
                    var camber = Mathf.Sin(t * Mathf.PI) * 3.5f;
                    var tall = Mathf.Max(6f, span.DeckHeight + camber - bed);

                    var column = Box(bridge, $"Pier{pier}",
                        new Vector3(at.x, bed + tall * 0.5f, at.y),
                        new Vector3(6.5f, tall, 9f),
                        Paint("BridgePier", new Color(0.31f, 0.31f, 0.32f)));

                    column.transform.rotation = facing;

                    Describe(column, CityPropKind.BridgePier, new Vector3(6.5f, tall, 9f),
                        string.Empty, 0);
                }

                // Ramps: from the deck ends down to the land, pitched rather than stepped.
                foreach (var end in new[] { (Point: from, Direction: -along),
                                            (Point: to, Direction: along) })
                {
                    const int rampSegments = 5;
                    const float rampLength = 70f;

                    for (var segment = 0; segment < rampSegments; segment++)
                    {
                        var t0 = segment / (float)rampSegments;
                        var t1 = (segment + 1f) / rampSegments;

                        var a = end.Point + end.Direction * (rampLength * t0);
                        var b = end.Point + end.Direction * (rampLength * t1);
                        var middle = (a + b) * 0.5f;

                        var landHeight = ground(b.x, b.y);
                        var deckHeight = Mathf.Lerp(span.DeckHeight, landHeight, t1);

                        var slab = Box(bridge, "Approach",
                            new Vector3(middle.x, deckHeight, middle.y),
                            new Vector3(span.Width, 2.2f, rampLength / rampSegments + 0.6f),
                            Paint("BridgeDeck", new Color(0.19f, 0.19f, 0.21f)));

                        slab.transform.rotation = Quaternion.LookRotation(
                            new Vector3(end.Direction.x, 0f, end.Direction.y), Vector3.up);

                        Describe(slab, CityPropKind.BridgeDeck,
                            new Vector3(span.Width, 2.2f, rampLength / rampSegments),
                            string.Empty, 0);
                    }
                }
            }
        }

        // ---- scene furniture ----------------------------------------------------------------------------------

        private static void BuildMarkers()
        {
            var group = new GameObject("Districts").transform;
            group.SetParent(root, true);

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

            cameraObject.transform.rotation = Quaternion.Euler(36f, 36f, 0f);
            cameraObject.transform.position = new Vector3(-1150f, 2050f, -1250f);
        }

        // ---- helpers -------------------------------------------------------------------------------------------

        private static float Range(float low, float high) =>
            low + (float)random.NextDouble() * (high - low);

        private static void Describe(GameObject box, CityPropKind kind, Vector3 footprint,
            string district, int variant)
        {
            var prop = box.AddComponent<CityProp>();
            prop.Describe(kind, footprint, district, variant);
        }

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

        private static GameObject Local(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            return box;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector2 at,
            float diameter, float thickness, Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, true);
            cylinder.transform.position = new Vector3(at.x, ground(at.x, at.y) + 0.2f, at.y);
            cylinder.transform.localScale = new Vector3(diameter, thickness, diameter);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;

            UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<CapsuleCollider>());
            return cylinder;
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
