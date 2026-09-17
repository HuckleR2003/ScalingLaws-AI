using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// What makes River Works' halls look like what their leases say they are.
    ///
    /// The difference between the halls on the bank and the ones behind the streets is the whole
    /// point of the district — the river does the cooling for the first and charges rent for it —
    /// and a hall is a box either way. So a hall on the water gets a pump house on the bank, two
    /// intake pipes running down the bank and out to an intake under a small jetty, and an outfall
    /// pipe at its other end with the warm water foaming where it meets the river. A hall behind the
    /// streets gets rows of chillers on its roof instead, because that is where its heat goes.
    ///
    /// Everything is built from primitives in the scene's own flat colours, into one group that is
    /// thrown away and built again on every run.
    /// </summary>
    public static class CityRiverCooling
    {
        private const string District = "riverworks";

        /// <summary>A hall this close to the water, measured out from its back wall, takes its cooling from the river.</summary>
        private const float OnTheWater = 45f;

        private const float PipeWidth = 1.5f;
        private const float PipeGap = 2.6f;

        /// <summary>How far past the water's edge the intakes reach, where the river is deep enough to draw from.</summary>
        private const float IntakeReach = 18f;

        [MenuItem("Scaling Laws/Cool the River Works halls")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var city = GameObject.Find("City")?.transform;
            var halls = city?.Find("SiteBuildings");

            if (!scene.IsValid() || halls == null)
            {
                Debug.LogError("[Cooling] No City.unity with SiteBuildings.");
                return;
            }

            var old = city.Find("RiverWorksCooling");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var root = new GameObject("RiverWorksCooling").transform;
            root.SetParent(city, false);

            var onWater = 0;
            var chilled = 0;

            foreach (var site in MapSiteCatalog.All.Where(s => s.DistrictId == District && s.Kind == MapSiteKind.ServerFacility))
            {
                var hall = halls.Find(site.DisplayName);
                if (hall == null)
                {
                    Debug.LogWarning($"[Cooling] {site.DisplayName} has no building.");
                    continue;
                }

                var frame = Measure(hall);
                var group = new GameObject(site.DisplayName).transform;
                group.SetParent(root, false);

                if (TowardsWater(frame, out var towards, out var shore))
                {
                    CoolFromTheRiver(frame, towards, shore, group);
                    onWater++;
                }
                else
                {
                    PutChillersOnTheRoof(frame, group);
                    chilled++;
                }
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Cooling] {onWater} halls piped to the river, {chilled} with chillers on the roof.");
        }

        // ---- measuring a hall --------------------------------------------------------------------

        /// <summary>A hall's footprint in its own axes: centre, the two axes, half-extents, and its roof.</summary>
        private readonly struct Hall
        {
            public Hall(Vector2 centre, Vector2 right, Vector2 forward, float halfWidth, float halfDepth, float floor, float roof)
            {
                Centre = centre;
                Right = right;
                Forward = forward;
                HalfWidth = halfWidth;
                HalfDepth = halfDepth;
                Floor = floor;
                Roof = roof;
            }

            public Vector2 Centre { get; }
            public Vector2 Right { get; }
            public Vector2 Forward { get; }
            public float HalfWidth { get; }
            public float HalfDepth { get; }
            public float Floor { get; }
            public float Roof { get; }
        }

        private static Hall Measure(Transform hall)
        {
            var right = new Vector2(hall.right.x, hall.right.z).normalized;
            var forward = new Vector2(hall.forward.x, hall.forward.z).normalized;
            var origin = new Vector2(hall.position.x, hall.position.z);

            float minR = float.MaxValue, maxR = float.MinValue, minF = float.MaxValue, maxF = float.MinValue;
            float floor = float.MaxValue, roof = float.MinValue;

            foreach (var filter in hall.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var bounds = filter.sharedMesh.bounds;

                for (var corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var world = filter.transform.TransformPoint(local);
                    var offset = new Vector2(world.x, world.z) - origin;

                    minR = Mathf.Min(minR, Vector2.Dot(offset, right));
                    maxR = Mathf.Max(maxR, Vector2.Dot(offset, right));
                    minF = Mathf.Min(minF, Vector2.Dot(offset, forward));
                    maxF = Mathf.Max(maxF, Vector2.Dot(offset, forward));
                    floor = Mathf.Min(floor, world.y);
                    roof = Mathf.Max(roof, world.y);
                }
            }

            var centre = origin + right * ((minR + maxR) * 0.5f) + forward * ((minF + maxF) * 0.5f);
            return new Hall(centre, right, forward, (maxR - minR) * 0.5f, (maxF - minF) * 0.5f, floor, roof);
        }

        /// <summary>
        /// Which way from the hall the river is, if it is near: out from the back or the front wall,
        /// whichever reaches water first within <see cref="OnTheWater"/> metres, and how far.
        /// </summary>
        private static bool TowardsWater(Hall hall, out Vector2 towards, out float shore)
        {
            towards = Vector2.zero;
            shore = float.MaxValue;

            foreach (var direction in new[] { -hall.Forward, hall.Forward })
            {
                for (var d = 0f; d <= OnTheWater; d += 1f)
                {
                    var at = hall.Centre + direction * (hall.HalfDepth + d);
                    if (CityTerrainBuilder.HeightAt(at.x, at.y) < CityLayout.SeaLevel)
                    {
                        if (d < shore)
                        {
                            shore = d;
                            towards = direction;
                        }

                        break;
                    }
                }
            }

            return shore < float.MaxValue;
        }

        // ---- the river side ----------------------------------------------------------------------

        private static void CoolFromTheRiver(Hall hall, Vector2 towards, float shore, Transform group)
        {
            var steel = CityDressingBuilder.Paint("CoolingPipe", new Color(0.50f, 0.56f, 0.62f), 0.45f, 0.55f);
            var concrete = CityDressingBuilder.Paint("PumpHouse", new Color(0.64f, 0.64f, 0.61f));
            var roofing = CityDressingBuilder.Paint("PumpHouseRoof", new Color(0.30f, 0.33f, 0.36f));
            var foam = CityDressingBuilder.Paint("OutfallFoam", new Color(0.86f, 0.94f, 0.96f), 0.6f);
            var dark = CityDressingBuilder.Paint("IntakeGrille", new Color(0.14f, 0.16f, 0.18f));

            var side = new Vector2(-towards.y, towards.x);
            var wall = hall.Centre + towards * hall.HalfDepth;

            // Intakes from the upstream third of the wall, the outfall from the far end: warm water
            // goes back in below where the cold is drawn, or the hall would drink its own heat.
            var intakeAt = wall + side * (hall.HalfWidth * 0.4f);
            var outfallAt = wall - side * (hall.HalfWidth * 0.7f);

            var reach = shore + IntakeReach;

            // The pump house stands on the bank top, where the ground starts to fall away.
            var bankTop = 3f;
            for (var d = 3f; d < shore; d += 1f)
            {
                var at = intakeAt + towards * d;
                if (CityTerrainBuilder.HeightAt(at.x, at.y) < hall.Floor - 1.2f)
                {
                    break;
                }

                bankTop = d;
            }

            var pumpCentre = intakeAt + towards * Mathf.Max(4f, bankTop - 3f);
            var pumpGround = CityTerrainBuilder.HeightAt(pumpCentre.x, pumpCentre.y);
            Block(group, "PumpHouse", pumpCentre, pumpGround + 2.6f, towards, new Vector3(9f, 5.2f, 7f), concrete);
            Block(group, "PumpHouseRoof", pumpCentre, pumpGround + 5.45f, towards, new Vector3(9.8f, 0.5f, 7.8f), roofing);

            foreach (var lane in new[] { -0.5f, 0.5f })
            {
                var start = intakeAt + side * (lane * PipeGap);
                Pipe(group, start, towards, reach, PipeWidth, steel);
            }

            // The jetty over the intakes, on piles, with the intake heads under it.
            var jetty = intakeAt + towards * reach;
            var deck = CityLayout.SeaLevel + 1.4f;
            Block(group, "IntakeJetty", jetty, deck, towards, new Vector3(9f, 0.6f, 8f), concrete);

            foreach (var (x, z) in new[] { (-3.8f, -3.3f), (3.8f, -3.3f), (-3.8f, 3.3f), (3.8f, 3.3f) })
            {
                var pile = jetty + side * x + towards * z;
                var bed = Mathf.Min(CityLayout.SeaLevel - 1f, CityTerrainBuilder.HeightAt(pile.x, pile.y));
                Column(group, "JettyPile", pile, bed, deck, 0.7f, concrete);
            }

            foreach (var lane in new[] { -0.5f, 0.5f })
            {
                var head = jetty + side * (lane * PipeGap);
                Column(group, "IntakeHead", head, CityLayout.SeaLevel - 0.8f, CityLayout.SeaLevel + 0.35f, 2.6f, dark);
            }

            // The outfall: one pipe, a shorter run, and the warm water frothing where it leaves.
            var outfallReach = shore + 6f;
            Pipe(group, outfallAt, towards, outfallReach, PipeWidth * 1.2f, steel);

            var mouth = outfallAt + towards * (outfallReach + 3f);
            var froth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            froth.name = "OutfallFoam";
            froth.transform.SetParent(group, true);
            froth.transform.position = new Vector3(mouth.x, CityLayout.SeaLevel + 0.08f, mouth.y);
            froth.transform.localScale = new Vector3(9f, 0.04f, 9f);
            froth.GetComponent<MeshRenderer>().sharedMaterial = foam;
            Object.DestroyImmediate(froth.GetComponent<Collider>());
        }

        /// <summary>
        /// A pipe laid from a point out along a direction: on the ground on land, just clear of the
        /// surface over water, in straight lengths that follow the bank down.
        /// </summary>
        private static void Pipe(Transform group, Vector2 start, Vector2 direction, float length, float width, Material material)
        {
            const float piece = 3f;
            var points = new List<Vector3>();

            for (var d = 0f; d <= length + 0.01f; d += piece)
            {
                var at = start + direction * Mathf.Min(d, length);
                var ground = CityTerrainBuilder.HeightAt(at.x, at.y);
                var surface = Mathf.Max(ground, CityLayout.SeaLevel + 0.3f);
                points.Add(new Vector3(at.x, surface + width * 0.5f + 0.1f, at.y));
            }

            for (var index = 0; index < points.Count - 1; index++)
            {
                var a = points[index];
                var b = points[index + 1];
                var span = b - a;

                var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                segment.name = "CoolingPipe";
                segment.transform.SetParent(group, true);
                segment.transform.position = (a + b) * 0.5f;
                segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, span.normalized);

                // A cylinder is two units tall; the extra fifth of a metre closes the joint on a bend.
                segment.transform.localScale = new Vector3(width, span.magnitude * 0.5f + 0.1f, width);
                segment.GetComponent<MeshRenderer>().sharedMaterial = material;
                Object.DestroyImmediate(segment.GetComponent<Collider>());
            }
        }

        // ---- the streets side ----------------------------------------------------------------------

        /// <summary>Rows of chillers on the roof, as many as the roof holds with a walkway round them.</summary>
        private static void PutChillersOnTheRoof(Hall hall, Transform group)
        {
            var casing = CityDressingBuilder.Paint("Chiller", new Color(0.74f, 0.76f, 0.78f), 0.3f, 0.2f);
            var fan = CityDressingBuilder.Paint("ChillerFan", new Color(0.13f, 0.14f, 0.16f));

            const float unit = 4.2f;
            const float spacing = 6f;

            var columns = Mathf.Clamp(Mathf.FloorToInt((hall.HalfWidth * 2f - 6f) / spacing), 1, 6);
            var rows = Mathf.Clamp(Mathf.FloorToInt((hall.HalfDepth * 2f - 6f) / spacing), 1, 3);

            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    var at = hall.Centre
                             + hall.Right * ((column - (columns - 1) * 0.5f) * spacing)
                             + hall.Forward * ((row - (rows - 1) * 0.5f) * spacing);

                    Block(group, "Chiller", at, hall.Roof + 0.9f, hall.Forward, new Vector3(unit, 1.8f, unit), casing);

                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    disc.name = "ChillerFan";
                    disc.transform.SetParent(group, true);
                    disc.transform.position = new Vector3(at.x, hall.Roof + 1.83f, at.y);
                    disc.transform.localScale = new Vector3(unit * 0.72f, 0.03f, unit * 0.72f);
                    disc.GetComponent<MeshRenderer>().sharedMaterial = fan;
                    Object.DestroyImmediate(disc.GetComponent<Collider>());
                }
            }
        }

        // ---- primitives ----------------------------------------------------------------------------

        private static void Block(Transform group, string name, Vector2 at, float centreHeight, Vector2 facing, Vector3 size, Material material)
        {
            var box = CityDressingBuilder.Box(group, name, new Vector3(at.x, centreHeight, at.y), size, material);
            box.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);
        }

        private static void Column(Transform group, string name, Vector2 at, float bottom, float top, float width, Material material)
        {
            var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = name;
            column.transform.SetParent(group, true);
            column.transform.position = new Vector3(at.x, (bottom + top) * 0.5f, at.y);
            column.transform.localScale = new Vector3(width, Mathf.Max(0.05f, (top - bottom) * 0.5f), width);
            column.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(column.GetComponent<Collider>());
        }
    }
}
