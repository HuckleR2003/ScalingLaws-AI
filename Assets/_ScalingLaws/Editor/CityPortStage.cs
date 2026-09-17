using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The port as a port: a container terminal towards the river mouth and warehouses upstream.
    ///
    /// The district was six halls in a row on the south bank with a street behind them, which is a
    /// business park, not a port. Gosia asked for the port to grow along the water by half again
    /// and more, the big port on the far side of the bridge, out towards the sea — so west of the
    /// bridge there is now a paved terminal out to a quay on the river, two ship-to-shore cranes,
    /// stacks of containers and a ship alongside; and east of the halls, up the river, three
    /// warehouses with their own quay and crane.
    ///
    /// Laid in the port grid's frame: "along" runs down the port street, "across" from the street
    /// towards the river. Everything is built new into one group on every run; the halls, the sites
    /// and the street are not touched.
    /// </summary>
    public static class CityPortStage
    {
        private const string Commercial = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        /// <summary>The port street's line, across from the port's middle, and the kerb-side edge of the paving.</summary>
        private const float Street = -215f;
        private const float PavingStart = Street + 14f;

        // The two works, as stretches along the port street.
        private const float TerminalFrom = -255f;
        private const float TerminalTo = -148f;
        private const float WarehousesFrom = 140f;
        private const float WarehousesTo = 300f;

        private const float SampleStep = 6f;

        private static System.Random random;

        [MenuItem("Scaling Laws/Port/Build the terminal and the warehouses")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var city = GameObject.Find("City")?.transform;
            var port = CityBlocks.Grids.FirstOrDefault(grid => grid.Id == "port_core");

            if (!scene.IsValid() || city == null || port == null)
            {
                Debug.LogError("[Port] No City.unity, City root or port grid.");
                return;
            }

            var old = city.Find("PortWorks");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            random = new System.Random(20260917);

            var root = new GameObject("PortWorks").transform;
            root.SetParent(city, false);

            var frame = new Frame(port);
            var cleared = ClearTheGround(city, frame);

            var terminal = new GameObject("ContainerTerminal").transform;
            terminal.SetParent(root, false);
            var containers = BuildTerminal(frame, terminal);

            var warehouses = new GameObject("Warehouses").transform;
            warehouses.SetParent(root, false);
            var halls = BuildWarehouses(frame, warehouses);

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Port] Terminal: {containers} containers, 3 cranes, a ship alongside. Warehouses: {halls} halls "
                + $"and a crane. {cleared} trees and stray buildings cleared from the works.");
        }

        // ---- the frame -----------------------------------------------------------------------------

        private readonly struct Frame
        {
            public Frame(GridBlock grid)
            {
                var angle = grid.RotationDegrees * Mathf.Deg2Rad;
                Centre = new Vector2(grid.CentreX, grid.CentreZ);
                Along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Across = new Vector2(-Along.y, Along.x);
            }

            public Vector2 Centre { get; }
            public Vector2 Along { get; }
            public Vector2 Across { get; }

            public Vector2 World(float along, float across) => Centre + Along * along + Across * across;

            public Vector2 Local(Vector2 point)
            {
                var offset = point - Centre;
                return new Vector2(Vector2.Dot(offset, Along), Vector2.Dot(offset, Across));
            }

            public Quaternion Facing(Vector2 direction) =>
                Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);
        }

        /// <summary>How far across, from the paving's edge towards the river, the land still holds: the quay line at this point along.</summary>
        private static float Shore(Frame frame, float along)
        {
            for (var across = PavingStart; across < 200f; across += 1f)
            {
                var at = frame.World(along, across);
                if (CityTerrainBuilder.HeightAt(at.x, at.y) < CityLayout.SeaLevel + 0.2f)
                {
                    return across;
                }
            }

            return 200f;
        }

        /// <summary>
        /// Woods and generic infill standing where the works go. The halls, the map sites and the
        /// roads are never touched.
        /// </summary>
        private static int ClearTheGround(Transform city, Frame frame)
        {
            var doomed = new List<GameObject>();

            foreach (var groupName in new[] { "Woods", "DistrictInfill" })
            {
                var group = city.Find(groupName);
                if (group == null)
                {
                    continue;
                }

                foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>())
                {
                    var local = frame.Local(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z));
                    var inWorks = local.y > Street - 10f
                                  && ((local.x > TerminalFrom - 15f && local.x < TerminalTo + 10f)
                                      || (local.x > WarehousesFrom - 10f && local.x < WarehousesTo + 15f));

                    if (!inWorks)
                    {
                        continue;
                    }

                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? renderer.gameObject;
                    if (!doomed.Contains(root))
                    {
                        doomed.Add(root);
                    }
                }
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
            }

            return doomed.Count;
        }

        // ---- the container terminal ----------------------------------------------------------------

        /// <summary>How far past the furthest point of the bank the terminal's quay is built out into the river mouth.</summary>
        private const float PierReach = 30f;

        private static int BuildTerminal(Frame frame, Transform group)
        {
            var concrete = CityDressingBuilder.Paint("PortConcrete", new Color(0.56f, 0.56f, 0.54f));
            var kerb = CityDressingBuilder.Paint("QuayEdge", new Color(0.70f, 0.69f, 0.66f));

            // The land between the bridge and the mouth is narrow, so the terminal is what ports
            // build there: a quay run straight out past the bank, filled in behind.
            var quay = Enumerable.Range(0, Mathf.FloorToInt((TerminalTo - TerminalFrom) / SampleStep) + 1)
                .Max(step => Shore(frame, TerminalFrom + step * SampleStep)) + PierReach;

            var top = CityLayout.SeaLevel + 3f;
            for (var along = TerminalFrom; along <= TerminalTo; along += SampleStep)
            {
                for (var across = PavingStart; across < quay; across += 4f)
                {
                    var at = frame.World(along, across);
                    top = Mathf.Max(top, CityTerrainBuilder.HeightAt(at.x, at.y) + 0.15f);
                }
            }

            // One solid block from the river bed up: a filled quay, not a slab on stilts.
            var bottom = CityLayout.SeaLevel - 3f;
            var depth = quay - PavingStart;
            Slab(frame, group, "TerminalQuay", (TerminalFrom + TerminalTo) * 0.5f, PavingStart + depth * 0.5f,
                bottom, top, TerminalTo - TerminalFrom + SampleStep, depth, concrete);
            Slab(frame, group, "QuayEdge", (TerminalFrom + TerminalTo) * 0.5f, quay + 0.6f,
                bottom, top + 0.5f, TerminalTo - TerminalFrom + SampleStep, 1.2f, kerb);

            foreach (var along in new[] { TerminalFrom + 22f, TerminalFrom + 56f, TerminalFrom + 90f })
            {
                Crane(frame, group, along, quay, top, "CraneYellow", new Color(0.96f, 0.72f, 0.12f));
            }

            // Container blocks behind the cranes' reach: rows of forty-footers, one to four high,
            // with a lane every second row for the straddle carriers.
            var placed = 0;
            var row = 0;

            for (var across = PavingStart + 8f; across + 6f < quay - 26f; across += 8.5f, row++)
            {
                if (row % 3 == 2)
                {
                    continue;
                }

                for (var along = TerminalFrom + 6f; along + 12f < TerminalTo; along += 13f)
                {
                    if (random.NextDouble() < 0.1)
                    {
                        continue;
                    }

                    var height = 1 + random.Next(4);

                    for (var tier = 0; tier < height; tier++)
                    {
                        foreach (var lane in new[] { 0f, 2.7f })
                        {
                            Container(frame, group, along + 6f, across + lane, top + 1.3f + tier * 2.6f);
                            placed++;
                        }
                    }
                }
            }

            Ship(frame, group, (TerminalFrom + TerminalTo) * 0.5f, quay + 9f, ref placed);
            return placed;
        }

        /// <summary>A block of ground-works in the port's frame: centred along and across, from one height to another.</summary>
        private static void Slab(Frame frame, Transform group, string name, float along, float across,
            float bottom, float top, float alongSize, float acrossSize, Material material)
        {
            var at = frame.World(along, across);
            var box = CityDressingBuilder.Box(group, name, new Vector3(at.x, (bottom + top) * 0.5f, at.y),
                new Vector3(alongSize, top - bottom, acrossSize), material);
            box.transform.rotation = frame.Facing(frame.Across);
        }

        /// <summary>
        /// A paved band in strips down a stretch, each strip level with the highest ground under its
        /// own part of the band, so the paving follows the lie of the land instead of standing on it as
        /// a table. Returns the highest strip.
        /// </summary>
        private static float PaveBand(Frame frame, Transform group, float from, float to,
            System.Func<float, (float From, float To)> band, Material material)
        {
            var highest = float.MinValue;

            for (var along = from; along <= to; along += SampleStep)
            {
                var (near, far) = band(along);
                var top = CityLayout.SeaLevel + 1.2f;

                for (var across = near; across <= far; across += 3f)
                {
                    var at = frame.World(along, across);
                    top = Mathf.Max(top, CityTerrainBuilder.HeightAt(at.x, at.y) + 0.12f);
                }

                Slab(frame, group, "PortPaving", along, (near + far) * 0.5f, top - 2.5f, top, SampleStep + 0.3f, far - near, material);
                highest = Mathf.Max(highest, top);
            }

            return highest;
        }

        private static readonly Color[] ContainerColours =
        {
            new(0.72f, 0.18f, 0.14f), new(0.16f, 0.35f, 0.66f), new(0.18f, 0.52f, 0.30f),
            new(0.90f, 0.52f, 0.12f), new(0.56f, 0.58f, 0.60f), new(0.86f, 0.86f, 0.82f),
            new(0.46f, 0.24f, 0.52f)
        };

        private static void Container(Frame frame, Transform group, float along, float across, float centreHeight)
        {
            var colour = ContainerColours[random.Next(ContainerColours.Length)];
            var paint = CityDressingBuilder.Paint($"Container{System.Array.IndexOf(ContainerColours, colour)}", colour, 0.2f);
            var at = frame.World(along, across);

            var box = CityDressingBuilder.Box(group, "Container", new Vector3(at.x, centreHeight, at.y),
                new Vector3(2.5f, 2.55f, 12.2f), paint);
            box.transform.rotation = frame.Facing(frame.Along);
        }

        /// <summary>
        /// A ship-to-shore crane: two portal frames straddling the quay, a girder running out over
        /// the water as the boom, a counter-jib behind, and the operator's cab under the boom.
        /// </summary>
        private static void Crane(Frame frame, Transform group, float along, float shore, float ground, string paintName, Color colour)
        {
            var paint = CityDressingBuilder.Paint(paintName, colour, 0.3f);
            var cab = CityDressingBuilder.Paint("CraneCab", new Color(0.88f, 0.89f, 0.9f), 0.3f);

            const float legHeight = 30f;
            const float gauge = 16f;
            const float span = 14f;

            var crane = new GameObject("Crane").transform;
            crane.SetParent(group, false);

            foreach (var side in new[] { -span * 0.5f, span * 0.5f })
            {
                foreach (var leg in new[] { shore - 4f, shore - 4f - gauge })
                {
                    Part(frame, crane, along + side, leg, ground + legHeight * 0.5f, new Vector3(1.3f, legHeight, 1.3f), paint);
                }

                // The portal's cross beam, low, at the legs' knees.
                Part(frame, crane, along + side, shore - 4f - gauge * 0.5f, ground + 12f, new Vector3(1f, 1.2f, gauge), paint);
            }

            // The boom out over the water and the counter-jib back over the terminal, one girder.
            Part(frame, crane, along, shore + 12f, ground + legHeight + 1f, new Vector3(span + 1.3f, 2.4f, 62f), paint);

            // Its stays: a raised frame over the legs.
            Part(frame, crane, along, shore - 4f - gauge * 0.5f, ground + legHeight + 7f, new Vector3(span + 1.3f, 1.2f, 3f), paint);

            Part(frame, crane, along, shore + 2f, ground + legHeight - 2.6f, new Vector3(3.2f, 2.6f, 3.4f), cab);
        }

        /// <summary>A cargo ship alongside: hull, a white superstructure at the stern, and a deck load of containers.</summary>
        private static void Ship(Frame frame, Transform group, float along, float across, ref int containers)
        {
            var hull = CityDressingBuilder.Paint("ShipHull", new Color(0.18f, 0.22f, 0.32f), 0.25f);
            var boot = CityDressingBuilder.Paint("ShipBoot", new Color(0.62f, 0.16f, 0.14f), 0.25f);
            var bridge = CityDressingBuilder.Paint("ShipBridge", new Color(0.92f, 0.92f, 0.9f), 0.3f);

            const float length = 78f;
            const float beam = 13f;

            var ship = new GameObject("Ship").transform;
            ship.SetParent(group, false);

            var waterline = CityLayout.SeaLevel;
            // Sizes are (along the quay, up, across it): the ship lies parallel to the quay.
            Part(frame, ship, along, across, waterline - 0.6f, new Vector3(length - 1f, 1.6f, beam - 0.4f), boot);
            Part(frame, ship, along, across, waterline + 2.2f, new Vector3(length, 4f, beam), hull);

            // The bow, narrowed in two steps so it does not read as a barge.
            Part(frame, ship, along + length * 0.5f + 3f, across, waterline + 2.2f, new Vector3(6f, 4f, beam * 0.66f), hull);
            Part(frame, ship, along + length * 0.5f + 7f, across, waterline + 2.2f, new Vector3(3f, 4f, beam * 0.3f), hull);

            var stern = along - length * 0.5f + 8f;
            Part(frame, ship, stern, across, waterline + 9f, new Vector3(10f, 9.6f, beam - 1f), bridge);
            Part(frame, ship, stern, across, waterline + 14.6f, new Vector3(6f, 1.6f, beam + 2f), bridge);

            for (var bay = along - length * 0.5f + 20f; bay < along + length * 0.5f - 8f; bay += 13f)
            {
                var stack = 1 + random.Next(3);

                for (var tier = 0; tier < stack; tier++)
                {
                    foreach (var row in new[] { -3.9f, -1.3f, 1.3f, 3.9f })
                    {
                        Container(frame, ship, bay, across + row, waterline + 5.5f + tier * 2.6f);
                        containers++;
                    }
                }
            }
        }

        // ---- the warehouses ------------------------------------------------------------------------

        private static int BuildWarehouses(Frame frame, Transform group)
        {
            var asphalt = CityDressingBuilder.Paint("PortYard", new Color(0.40f, 0.41f, 0.43f));
            var concrete = CityDressingBuilder.Paint("PortConcrete", new Color(0.56f, 0.56f, 0.54f));
            var kerb = CityDressingBuilder.Paint("QuayEdge", new Color(0.70f, 0.69f, 0.66f));

            var models = new[] { "building-k", "building-e" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(Commercial + name + ".fbx"))
                .Where(model => model != null)
                .ToList();

            // Three halls along the street, each on its own yard.
            const float yardDepth = 64f;
            var slot = (WarehousesTo - WarehousesFrom) / 3f;
            var halls = 0;

            for (var index = 0; index < 3; index++)
            {
                var from = WarehousesFrom + slot * index + 3f;
                var to = from + slot - 6f;
                var yard = PaveBand(frame, group, from, to, _ => (PavingStart, PavingStart + yardDepth), asphalt);

                var model = models[index % models.Count];
                var size = Measure(model);
                var scale = (to - from - 6f) / size.x;
                var hallDepth = size.z * scale;

                var at = frame.World((from + to) * 0.5f, PavingStart + yardDepth - 4f - hallDepth * 0.5f);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                instance.name = "Warehouse";
                instance.transform.SetPositionAndRotation(new Vector3(at.x, yard, at.y), frame.Facing(-frame.Across));
                instance.transform.localScale = Vector3.one * scale;

                // Stood on its yard by its own measured base: a model's pivot is its author's business.
                var bounds = instance.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds)
                    .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                instance.transform.position += Vector3.up * (yard - bounds.min.y);
                halls++;
            }

            // The quay upstream: a band along the water's edge, the crane, and boxes waiting on it.
            const float quayDepth = 34f;
            var quayTop = PaveBand(frame, group, WarehousesFrom, WarehousesTo,
                along => (Shore(frame, along) - quayDepth, Shore(frame, along) + 1.5f), concrete);

            for (var along = WarehousesFrom; along <= WarehousesTo; along += SampleStep)
            {
                var shore = Shore(frame, along);
                Slab(frame, group, "QuayEdge", along, shore + 2.1f, CityLayout.SeaLevel - 3f, quayTop - 0.1f, SampleStep + 0.3f, 1.2f, kerb);
            }

            var craneAlong = WarehousesTo - 40f;
            Crane(frame, group, craneAlong, Shore(frame, craneAlong) + 1.5f, quayTop, "CraneRed", new Color(0.82f, 0.2f, 0.16f));

            for (var along = WarehousesFrom + 12f; along < WarehousesTo - 70f; along += 13f)
            {
                var shore = Shore(frame, along);
                var height = 1 + random.Next(2);

                for (var tier = 0; tier < height; tier++)
                {
                    Container(frame, group, along, shore - quayDepth + 6f, quayTop + 1.3f + tier * 2.6f);
                }
            }

            return halls;
        }

        // ---- parts ---------------------------------------------------------------------------------

        private static void Part(Frame frame, Transform parent, float along, float across, float centreHeight, Vector3 size, Material paint)
        {
            var at = frame.World(along, across);
            var box = CityDressingBuilder.Box(parent, "Part", new Vector3(at.x, centreHeight, at.y), size, paint);
            box.transform.rotation = frame.Facing(frame.Across);
        }

        private static Vector3 Measure(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var bounds = renderers[0].localBounds;

            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.localBounds);
            }

            return bounds.size;
        }
    }
}
