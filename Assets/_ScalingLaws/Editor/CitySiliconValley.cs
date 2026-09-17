using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Silicon Valley: the frontier labs' headquarters on the southern bay shore, built one building
    /// at a time.
    ///
    /// **Designed, not filled.** Every other district is a grid the infill scatters kit buildings
    /// over, which is right for a town and wrong for the one place on the map meant to look like
    /// money. Here each address has its own architecture, chosen so the district reads from the map
    /// camera by silhouette alone: the crown tower at the head of the central plaza, the wide campus
    /// stepping down to the water, the glass ring around its garden, the brick factories kept at the
    /// gate from the port, and between them the two trophy buildings nobody rents yet.
    ///
    /// **The walk it is laid out for**, north to south: the gate from the port with its pylons and
    /// converted warehouses, corporate blocks along the boulevard, the major headquarters, the
    /// central plaza whose reflecting pool runs from the Own Tower's forecourt across the boulevard
    /// and down to a marina, the Research Campus on the water, the canopy headquarters and the
    /// research pavilions, and a waterfront park at the far end. A promenade follows the shore the
    /// whole way.
    ///
    /// Positions come from <see cref="MapSiteCatalog"/>, never from here, so a pin and its building
    /// cannot drift apart; this file owns only what each address looks like. Run after the road
    /// network, which it reads so nothing stands on a street, and rerun freely: it replaces its own
    /// group and touches nothing else.
    /// </summary>
    public static class CitySiliconValley
    {
        public const string DistrictId = "silicon";

        private const string GroupName = "SiliconValley";
        private const string Suburban = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";
        private const string RoadKit = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";
        private const string Commercial = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        /// <summary>Storey height the glass facades are banded at.</summary>
        private const float Storey = 4f;

        /// <summary>The central plaza's axis: the line the Own Tower, the pool and the marina share.</summary>
        private const float PlazaAxisZ = -690f;

        private static Transform root;
        private static Transform greenery;
        private static Random random;
        private static List<Bounds> roadBoxes;
        private static List<Vector2> junctions;
        private static readonly List<(Vector2 Centre, Vector2 Right, Vector2 Forward, float HalfWidth, float HalfDepth)> Footprints = new();
        private static readonly List<string> Conflicts = new();
        private static readonly Dictionary<string, int> Counts = new();
        private static Font font;
        private static GameObject[] trees;
        private static GameObject lamp;

        [MenuItem("Scaling Laws/Silicon Valley/Build the district")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var city = GameObject.Find("City");

            if (!scene.IsValid() || city == null)
            {
                Debug.LogError("[Valley] No City.unity with a City root.");
                return;
            }

            var old = city.transform.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            root = new GameObject(GroupName).transform;
            root.SetParent(city.transform, false);
            greenery = Child(root, "Greenery");

            random = new Random(20260917);
            Footprints.Clear();
            Conflicts.Clear();
            Counts.Clear();
            ReadRoads(city.transform);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            trees = new[] { "tree-large", "tree-small" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(Suburban + name + ".fbx"))
                .Where(model => model != null).ToArray();
            lamp = AssetDatabase.LoadAssetAtPath<GameObject>(RoadKit + "light-curved.fbx");

            var sites = MapSiteCatalog.All.Where(site => site.DistrictId == DistrictId)
                .ToDictionary(site => site.Id);

            // The two trophies and the plaza first: they decide the axis everything else keeps clear of.
            BuildOwnTower(sites["office.soon.tower"]);
            BuildCentralPlaza();
            BuildResearchCampus(sites["office.soon.campus"]);

            BuildCrownTower(sites["hq.silicon.1"]);
            BuildWideCampus(sites["hq.silicon.2"]);
            BuildCourtyardRing(sites["hq.silicon.3"]);
            BuildPavilions(sites["hq.silicon.4"]);
            BuildSteppedOffice(sites["hq.silicon.5"]);
            BuildCanopyHeadquarters(sites["hq.silicon.6"]);
            BuildFinnedMidRise(sites["hq.silicon.7"], light: false);
            BuildBenchTower(sites["hq.silicon.8"]);
            BuildSawtoothFactory(sites["hq.silicon.9"]);
            BuildBrickAndGlass(sites["hq.silicon.10"]);
            BuildFinnedMidRise(sites["hq.silicon.11"], light: true);
            BuildWarehouse(sites["hq.silicon.12"]);

            BuildNeighbours();
            BuildGateway();
            BuildPromenade();
            BuildWaterfrontPark();
            BuildStreetscape();

            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Valley] Built: {string.Join(", ", Counts.Select(pair => $"{pair.Key} {pair.Value}"))}.");
            Debug.Log(Conflicts.Count == 0
                ? "[Valley] Nothing stands on a street."
                : $"[Valley] {Conflicts.Count} footprints touch a street:\n  " + string.Join("\n  ", Conflicts));
        }

        // ---- materials -----------------------------------------------------------------------------

        private static Material Glass => CityDressingBuilder.Paint("SvGlass", new Color(0.40f, 0.56f, 0.66f), 0.85f, 0.3f);
        private static Material GlassDark => CityDressingBuilder.Paint("SvGlassDark", new Color(0.19f, 0.27f, 0.34f), 0.9f, 0.3f);
        private static Material GlassWarm => CityDressingBuilder.Paint("SvGlassWarm", new Color(0.50f, 0.44f, 0.36f), 0.85f, 0.35f);
        private static Material GlassLight => CityDressingBuilder.Paint("SvGlassLight", new Color(0.66f, 0.80f, 0.86f), 0.85f, 0.2f);
        private static Material Frame => CityDressingBuilder.Paint("SvFrame", new Color(0.88f, 0.89f, 0.90f), 0.35f);
        private static Material Champagne => CityDressingBuilder.Paint("SvChampagne", new Color(0.80f, 0.74f, 0.62f), 0.5f, 0.4f);
        private static Material Gold => CityDressingBuilder.Paint("SvGold", new Color(0.86f, 0.69f, 0.32f), 0.7f, 0.75f);
        private static Material Steel => CityDressingBuilder.Paint("SvSteel", new Color(0.30f, 0.32f, 0.35f), 0.5f, 0.6f);
        private static Material Concrete => CityDressingBuilder.Paint("SvConcrete", new Color(0.74f, 0.73f, 0.70f), 0.1f);
        private static Material Stone => CityDressingBuilder.Paint("SvStone", new Color(0.58f, 0.56f, 0.52f), 0.15f);
        private static Material StoneDark => CityDressingBuilder.Paint("SvStoneDark", new Color(0.56f, 0.55f, 0.53f), 0.15f);
        private static Material Brick => CityDressingBuilder.Paint("SvBrick", new Color(0.56f, 0.30f, 0.23f), 0.1f);
        private static Material BrickDark => CityDressingBuilder.Paint("SvBrickDark", new Color(0.40f, 0.21f, 0.17f), 0.1f);
        private static Material BrickLight => CityDressingBuilder.Paint("SvBrickLight", new Color(0.68f, 0.45f, 0.34f), 0.1f);
        private static Material Lawn => CityDressingBuilder.Paint("SvLawn", new Color(0.38f, 0.56f, 0.30f), 0.05f);
        private static Material Water => CityDressingBuilder.Paint("SvWater", new Color(0.18f, 0.42f, 0.56f), 0.95f, 0.1f);
        private static Material Timber => CityDressingBuilder.Paint("SvTimber", new Color(0.62f, 0.46f, 0.31f), 0.2f);
        private static Material White => CityDressingBuilder.Paint("SvWhite", new Color(0.95f, 0.95f, 0.94f), 0.3f);
        private static Material Lantern => CityDressingBuilder.Paint("SvLantern", new Color(0.98f, 0.90f, 0.66f), 0.6f, 0.1f);

        // ---- the trophies --------------------------------------------------------------------------

        /// <summary>
        /// The Own Tower: a bronze shaft with a companion volume, a sky garden two thirds of the way
        /// up and a gold lantern on top, on a stone platform above the boulevard with a grand stair
        /// and a reflecting pool down the middle of its forecourt.
        /// </summary>
        private static void BuildOwnTower(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 76f, 56f, out _);

            Band(building, "Podium", new Vector3(0f, 0f, 0f), 70f, 50f, 18f, GlassWarm, Champagne, 6f);
            Box(building, "Lobby", new Vector3(0f, 6f, 25.2f), new Vector3(40f, 12f, 0.6f), GlassLight);
            Box(building, "PodiumCornice", new Vector3(0f, 18.4f, 0f), new Vector3(71f, 0.8f, 51f), Gold);

            // The main shaft, broken by the sky garden.
            Band(building, "Shaft", new Vector3(6f, 18f, -4f), 34f, 34f, 102f, GlassWarm, Champagne);
            Box(building, "SkyGardenFloor", new Vector3(6f, 120.8f, -4f), new Vector3(34f, 0.4f, 34f), Lawn);
            Box(building, "SkyGardenCore", new Vector3(6f, 124f, -4f), new Vector3(24f, 8f, 24f), GlassDark);
            for (var corner = 0; corner < 4; corner++)
            {
                var x = corner % 2 == 0 ? -15.5f : 15.5f;
                var z = corner < 2 ? -15.5f : 15.5f;
                Box(building, "SkyGardenColumn", new Vector3(6f + x, 124f, -4f + z), new Vector3(1.2f, 8f, 1.2f), Gold);
                SmallTree(building, new Vector3(6f + x * 0.62f, 120.4f, -4f + z * 0.62f), 5f);
            }

            Band(building, "UpperShaft", new Vector3(6f, 128f, -4f), 34f, 34f, 42f, GlassWarm, Champagne);

            // The companion: lower, set back, so the tower reads as two volumes rather than a slab.
            Band(building, "Companion", new Vector3(-21f, 18f, -12f), 20f, 22f, 122f, GlassWarm, Champagne);
            Box(building, "CompanionCap", new Vector3(-21f, 140.6f, -12f), new Vector3(21f, 1.2f, 23f), Gold);

            // The crown: a gold frame around a lit lantern, corner spires and a mast.
            var crown = Child(building, "Crown", new Vector3(6f, 170f, -4f));
            Box(crown, "Lantern", new Vector3(0f, 9f, 0f), new Vector3(22f, 18f, 22f), Lantern);
            FrameCage(crown, new Vector3(0f, 0f, 0f), 27f, 20f, 27f, 1.1f, Gold);
            foreach (var (x, z) in Corners(13.5f))
            {
                Box(crown, "Spire", new Vector3(x, 21f, z), new Vector3(1.1f, 22f, 1.1f), Gold);
            }

            Cylinder(crown, "Mast", new Vector3(0f, 20f, 0f), 1.6f, 34f, Steel);

            Forecourt(building, site, 25f, 70f, pool: true, grand: true, sign: null);
            Pin(building, site);
        }

        /// <summary>
        /// The Research Campus: a round glass pavilion with three planted wings fanning out behind it
        /// to the water, lawns between the wings running down to a deck on the shore.
        /// </summary>
        private static void BuildResearchCampus(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 150f, 130f, out _);

            Cylinder(building, "Pavilion", new Vector3(0f, 0f, 20f), 30f, 14f, GlassLight);
            Cylinder(building, "PavilionRoof", new Vector3(0f, 14.2f, 20f), 38f, 1.2f, White);
            Cylinder(building, "PavilionTrim", new Vector3(0f, 13.9f, 20f), 38.8f, 1.2f, Gold);

            foreach (var yaw in new[] { -38f, 0f, 38f })
            {
                var wing = Child(building, "Wing", new Vector3(0f, 0f, 20f), yaw);
                Band(wing, "Glass", new Vector3(0f, 0f, -60f), 20f, 80f, 22f, Glass, Frame);
                Box(wing, "GreenRoof", new Vector3(0f, 23.0f, -60f), new Vector3(18f, 0.5f, 76f), Lawn);
                Box(wing, "RoofTrim", new Vector3(0f, 22.75f, -60f), new Vector3(20.6f, 0.3f, 80.6f), Gold);
            }

            foreach (var yaw in new[] { -19f, 19f })
            {
                var lawn = Child(building, "Lawn", new Vector3(0f, 0f, 20f), yaw);
                Box(lawn, "Grass", new Vector3(0f, 0.2f, -66f), new Vector3(22f, 0.4f, 70f), Lawn);

                for (var step = 0; step < 4; step++)
                {
                    SmallTree(lawn, new Vector3(0f, 0.4f, -40f - step * 16f), 9f);
                }
            }

            Box(building, "Entrance", new Vector3(0f, 0.15f, 48f), new Vector3(48f, 0.3f, 22f), Stone);
            Box(building, "Canopy", new Vector3(0f, 7f, 42f), new Vector3(24f, 0.8f, 10f), White);
            Box(building, "CanopyEdge", new Vector3(0f, 7.5f, 47f), new Vector3(24.4f, 0.3f, 0.4f), Gold);

            Box(building, "Deck", new Vector3(0f, 0.2f, -70f), new Vector3(110f, 0.4f, 12f), Timber);
            Pin(building, site);
        }

        // ---- the headquarters ----------------------------------------------------------------------

        /// <summary>Slot one: a glass shaft with corner fins, two setbacks and a lit crown with blades and a spire.</summary>
        private static void BuildCrownTower(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 56f, 40f, out _);

            Band(building, "Podium", Vector3.zero, 56f, 40f, 16f, GlassDark, Frame);
            Box(building, "PodiumGarden", new Vector3(0f, 16.85f, -6f), new Vector3(50f, 0.4f, 26f), Lawn);

            Band(building, "Shaft", new Vector3(0f, 16f, -4f), 30f, 30f, 110f, Glass, Frame);
            foreach (var (x, z) in Corners(15.4f))
            {
                Box(building, "Fin", new Vector3(x, 71f, -4f + z), new Vector3(1.4f, 110f, 1.4f), Frame);
            }

            Band(building, "Setback", new Vector3(0f, 126f, -4f), 26f, 26f, 16f, Glass, Frame);
            Band(building, "Top", new Vector3(0f, 142f, -4f), 20f, 20f, 14f, Glass, Frame);

            var crown = Child(building, "Crown", new Vector3(0f, 156f, -4f));
            Box(crown, "Lantern", new Vector3(0f, 6f, 0f), new Vector3(12f, 12f, 12f), Lantern);
            foreach (var (x, z) in Corners(9.5f))
            {
                Box(crown, "Blade", new Vector3(x, 13f, z), new Vector3(1.2f, 26f, 5f), Frame, x * z > 0 ? 45f : -45f);
            }

            Cylinder(crown, "Spire", new Vector3(0f, 12f, 0f), 1.3f, 28f, Frame);

            Box(building, "Canopy", new Vector3(0f, 7f, 25f), new Vector3(22f, 0.8f, 10f), Steel);
            Cylinder(building, "CanopyPost", new Vector3(-9f, 0f, 29f), 0.6f, 7f, Steel);
            Cylinder(building, "CanopyPost", new Vector3(9f, 0f, 29f), 0.6f, 7f, Steel);
            FacadeName(building, site.DisplayName, new Vector3(0f, 12f, 20.3f), 2.8f);

            Forecourt(building, site, 20f, 60f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot two: three long wings stepping down to the water, green roofs, glass bridges between.</summary>
        private static void BuildWideCampus(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 150f, 110f, out _);

            var wings = new[] { (x: 0f, z: 38f, w: 140f, h: 20f), (x: -10f, z: 0f, w: 130f, h: 16f), (x: -4f, z: -38f, w: 120f, h: 12f) };

            foreach (var (x, z, w, h) in wings)
            {
                Band(building, "Wing", new Vector3(x, 0f, z), w, 22f, h, Glass, Frame);
                Box(building, "GreenRoof", new Vector3(x, h + 0.85f, z), new Vector3(w - 3f, 0.5f, 19f), Lawn);
                Box(building, "Skylight", new Vector3(x, h + 1.4f, z), new Vector3(w * 0.7f, 0.8f, 2.4f), GlassLight);
            }

            foreach (var x in new[] { -40f, 30f })
            {
                Box(building, "Bridge", new Vector3(x, 10.5f, 19f), new Vector3(8f, 5f, 16f), GlassLight);
                Box(building, "Bridge", new Vector3(x + 12f, 8.5f, -19f), new Vector3(8f, 5f, 16f), GlassLight);
            }

            foreach (var z in new[] { 19f, -19f })
            {
                Box(building, "Court", new Vector3(-6f, 0.2f, z), new Vector3(118f, 0.4f, 14f), Lawn);
                for (var step = 0; step < 4; step++)
                {
                    SmallTree(building, new Vector3(-55f + step * 30f + (z > 0 ? 8f : 0f), 0.4f, z), 7f);
                }
            }

            Box(building, "Terrace", new Vector3(-4f, 0.2f, -55f), new Vector3(120f, 0.4f, 10f), Timber);
            Forecourt(building, site, 49f, 40f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot three: a glass ring around a garden, open to the bay behind and bridged across the gap.</summary>
        private static void BuildCourtyardRing(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 90f, 90f, out _);

            Band(building, "Front", new Vector3(0f, 0f, 36f), 90f, 18f, 28f, Glass, Frame);
            Band(building, "Left", new Vector3(-36f, 0f, 0f), 18f, 54f, 28f, Glass, Frame);
            Band(building, "Right", new Vector3(36f, 0f, 0f), 18f, 54f, 28f, Glass, Frame);
            Band(building, "BackLeft", new Vector3(-28f, 0f, -36f), 34f, 18f, 28f, Glass, Frame);
            Band(building, "BackRight", new Vector3(28f, 0f, -36f), 34f, 18f, 28f, Glass, Frame);
            Box(building, "Bridge", new Vector3(0f, 17f, -36f), new Vector3(22f, 8f, 16f), GlassLight);

            Box(building, "Garden", new Vector3(0f, 0.2f, 0f), new Vector3(54f, 0.4f, 54f), Lawn);
            Box(building, "PoolRim", new Vector3(0f, 0.35f, 0f), new Vector3(18f, 0.7f, 18f), Stone);
            Box(building, "Pool", new Vector3(0f, 0.5f, 0f), new Vector3(16f, 0.7f, 16f), Water);

            foreach (var (x, z) in Corners(17f))
            {
                SmallTree(building, new Vector3(x, 0.4f, z), 8f);
            }

            for (var slat = 0; slat < 7; slat++)
            {
                Box(building, "Pergola", new Vector3(-36f + slat * 12f, 29.2f, 36f), new Vector3(1f, 0.6f, 15f), Timber);
            }

            Forecourt(building, site, 45f, 30f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot four: five pavilions in an arc under deep white roofs, joined by covered walks round a lawn.</summary>
        private static void BuildPavilions(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 130f, 110f, out _);
            var arc = new[] { new Vector2(-50f, -8f), new Vector2(-26f, 18f), new Vector2(0f, 28f), new Vector2(26f, 18f), new Vector2(50f, -8f) };

            for (var index = 0; index < arc.Length; index++)
            {
                var at = arc[index];
                var yaw = Mathf.Atan2(at.x, at.y + 60f) * Mathf.Rad2Deg;
                var pavilion = Child(building, "Pavilion", new Vector3(at.x, 0f, at.y), yaw);

                Box(pavilion, "Glass", new Vector3(0f, 4.5f, 0f), new Vector3(24f, 9f, 16f), GlassLight);
                Box(pavilion, "Roof", new Vector3(0f, 9.5f, 0f), new Vector3(32f, 1f, 24f), White);

                if (index == 0)
                {
                    continue;
                }

                var previous = arc[index - 1];
                var middle = (previous + at) * 0.5f;
                var run = Vector2.Distance(previous, at);
                var walkYaw = Mathf.Atan2(at.x - previous.x, at.y - previous.y) * Mathf.Rad2Deg;
                var walk = Child(building, "Walk", new Vector3(middle.x, 0f, middle.y), walkYaw);
                Box(walk, "Roof", new Vector3(0f, 4.6f, 0f), new Vector3(4f, 0.4f, run), Timber);
                Cylinder(walk, "Post", new Vector3(-1.6f, 0f, 0f), 0.4f, 4.4f, Steel);
                Cylinder(walk, "Post", new Vector3(1.6f, 0f, 0f), 0.4f, 4.4f, Steel);
            }

            Box(building, "Lawn", new Vector3(0f, 0.2f, -30f), new Vector3(80f, 0.4f, 44f), Lawn);
            for (var step = 0; step < 6; step++)
            {
                SmallTree(building, new Vector3(-50f + step * 20f, 0.4f, -50f), 9f);
            }

            Forecourt(building, site, 42f, 20f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot five: a stepped block with a garden on every setback.</summary>
        private static void BuildSteppedOffice(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 60f, 40f, out _);
            var tiers = new[] { (w: 60f, d: 40f, h: 14f), (w: 50f, d: 32f, h: 12f), (w: 40f, d: 24f, h: 12f), (w: 28f, d: 16f, h: 12f) };
            var floor = 0f;

            for (var index = 0; index < tiers.Length; index++)
            {
                var (w, d, h) = tiers[index];
                Band(building, "Tier", new Vector3(0f, floor, -2f * index), w, d, h, Glass, Frame);
                floor += h;

                if (index + 1 < tiers.Length)
                {
                    var next = tiers[index + 1];
                    Box(building, "Terrace", new Vector3(0f, floor + 0.75f, -2f * index), new Vector3(w - 1f, 0.4f, d - 1f), Lawn);
                    SmallTree(building, new Vector3(-(next.w * 0.5f) - 2f, floor + 0.4f, -2f * index + d * 0.5f - 3f), 5f);
                    SmallTree(building, new Vector3(next.w * 0.5f + 2f, floor + 0.4f, -2f * index + d * 0.5f - 3f), 5f);
                }
            }

            Box(building, "RoofPlant", new Vector3(0f, floor + 1.5f, -8f), new Vector3(12f, 3f, 8f), Concrete);
            Forecourt(building, site, 20f, 18f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot six: a glass block lifted on columns over its plaza, under a canopy reaching out towards the boulevard.</summary>
        private static void BuildCanopyHeadquarters(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 50f, 34f, out _);

            Box(building, "Lobby", new Vector3(0f, 3f, -2f), new Vector3(38f, 6f, 20f), GlassDark);
            foreach (var x in new[] { -20f, 0f, 20f })
            {
                foreach (var z in new[] { -13f, 13f })
                {
                    Cylinder(building, "Column", new Vector3(x, 0f, z), 1.4f, 6f, Frame);
                }
            }

            Band(building, "Block", new Vector3(0f, 6f, 0f), 50f, 34f, 34f, Glass, Frame);
            Box(building, "Canopy", new Vector3(0f, 13f, 30f), new Vector3(44f, 1.5f, 26f), White);
            Cylinder(building, "CanopyColumn", new Vector3(-16f, 0f, 40f), 0.9f, 12.3f, Frame);
            Cylinder(building, "CanopyColumn", new Vector3(16f, 0f, 40f), 0.9f, 12.3f, Frame);

            Box(building, "PoolRim", new Vector3(0f, 0.35f, 52f), new Vector3(32f, 0.7f, 12f), Stone);
            Box(building, "Pool", new Vector3(0f, 0.5f, 52f), new Vector3(30f, 0.7f, 10f), Water);
            for (var jet = 0; jet < 5; jet++)
            {
                Cylinder(building, "Jet", new Vector3(-12f + jet * 6f, 0.8f, 52f), 0.5f, 3f, White);
            }

            Forecourt(building, site, 17f, 50f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slots seven and eleven: a glass mid-rise, dark with vertical fins or light with bands and a crown ring.</summary>
        private static void BuildFinnedMidRise(MapSiteDefinition site, bool light)
        {
            var building = Site(site, "valley_bench", 36f, 28f, out _);

            if (light)
            {
                Band(building, "Block", Vector3.zero, 32f, 32f, 44f, GlassLight, Frame);
                FrameCage(building, new Vector3(0f, 44f, 0f), 30f, 5f, 30f, 0.8f, Frame);
                Box(building, "Penthouse", new Vector3(0f, 46f, -4f), new Vector3(16f, 4f, 12f), GlassDark);
            }
            else
            {
                Box(building, "Block", new Vector3(0f, 28f, 0f), new Vector3(36f, 56f, 28f), GlassDark);
                for (var fin = 0; fin <= 12; fin++)
                {
                    var x = -18f + fin * 3f;
                    Box(building, "Fin", new Vector3(x, 28f, 14.6f), new Vector3(0.5f, 56f, 1.2f), Frame);
                    Box(building, "Fin", new Vector3(x, 28f, -14.6f), new Vector3(0.5f, 56f, 1.2f), Frame);
                }

                Box(building, "Cap", new Vector3(0f, 56.4f, 0f), new Vector3(37f, 0.8f, 29f), Frame);
                Box(building, "Penthouse", new Vector3(0f, 59.5f, -3f), new Vector3(20f, 6f, 14f), Frame);
            }

            Forecourt(building, site, light ? 16f : 14f, 14f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot eight: a kit tower on a glass podium, given a steel lantern so it does not read as downtown's.</summary>
        private static void BuildBenchTower(MapSiteDefinition site)
        {
            var building = Site(site, "valley_bench", 44f, 44f, out _);

            Band(building, "Podium", Vector3.zero, 44f, 44f, 8f, GlassDark, Frame);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Commercial + "building-skyscraper-c.fbx");
            var top = 8f;

            if (model != null)
            {
                var tower = (GameObject)PrefabUtility.InstantiatePrefab(model, building);
                tower.transform.localRotation = Quaternion.identity;
                var size = Measure(model);
                var scale = 92f / Mathf.Max(0.01f, size.y);
                tower.transform.localScale = Vector3.one * scale;
                tower.transform.localPosition = Vector3.zero;

                var bounds = WorldBounds(tower);
                tower.transform.position += Vector3.up * (building.position.y + 8f - bounds.min.y);
                var centre = building.InverseTransformPoint(WorldBounds(tower).center);
                tower.transform.localPosition -= new Vector3(centre.x, 0f, centre.z);
                top = building.InverseTransformPoint(WorldBounds(tower).max).y;
            }

            FrameCage(building, new Vector3(0f, top, 0f), 22f, 12f, 22f, 0.9f, Steel);
            Box(building, "Lantern", new Vector3(0f, top + 5f, 0f), new Vector3(14f, 10f, 14f), Lantern);
            Forecourt(building, site, 22f, 14f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot nine: a brick sawtooth factory kept at the port gate, with a glass box set into one end and its chimney.</summary>
        private static void BuildSawtoothFactory(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 62f, 36f, out _);

            Box(building, "Hall", new Vector3(-8f, 7f, 0f), new Vector3(46f, 14f, 36f), Brick);
            for (var ridge = 0; ridge < 5; ridge++)
            {
                var z = -14.4f + ridge * 7.2f;
                Box(building, "Ridge", new Vector3(-8f, 14f, z), new Vector3(44f, 5f, 5f), Steel, 0f, 45f);
                Box(building, "RoofLight", new Vector3(-8f, 16.2f, z + 1.8f), new Vector3(42f, 0.4f, 1.6f), GlassLight);
            }

            for (var pier = 0; pier <= 6; pier++)
            {
                var x = -31f + pier * 7.66f;
                Box(building, "Pier", new Vector3(x, 7f, 18.3f), new Vector3(1.6f, 14f, 0.8f), BrickDark);
                Box(building, "Pier", new Vector3(x, 7f, -18.3f), new Vector3(1.6f, 14f, 0.8f), BrickDark);

                if (pier < 6)
                {
                    Box(building, "Window", new Vector3(x + 3.83f, 6f, 18.2f), new Vector3(5f, 7f, 0.4f), GlassDark);
                    Box(building, "Window", new Vector3(x + 3.83f, 6f, -18.2f), new Vector3(5f, 7f, 0.4f), GlassDark);
                }
            }

            Band(building, "Addition", new Vector3(23f, 0f, 0f), 16f, 30f, 22f, Glass, Frame);
            Cylinder(building, "Chimney", new Vector3(-28f, 0f, -12f), 3.2f, 32f, BrickDark);
            Cylinder(building, "ChimneyCap", new Vector3(-28f, 32f, -12f), 3.8f, 1f, Steel);

            Forecourt(building, site, 18f, 14f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot ten: brick below with punched windows, glass above, a pergola on the roof.</summary>
        private static void BuildBrickAndGlass(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 50f, 30f, out _);

            Box(building, "Base", new Vector3(0f, 6f, 0f), new Vector3(50f, 12f, 30f), BrickLight);
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 7; column++)
                {
                    var x = -21f + column * 7f;
                    var y = 3.5f + row * 5f;
                    Box(building, "Window", new Vector3(x, y, 15.1f), new Vector3(3.6f, 3f, 0.4f), GlassDark);
                    Box(building, "Window", new Vector3(x, y, -15.1f), new Vector3(3.6f, 3f, 0.4f), GlassDark);
                }
            }

            Band(building, "Upper", new Vector3(0f, 12f, -1f), 46f, 26f, 20f, Glass, Frame);
            for (var slat = 0; slat < 9; slat++)
            {
                Box(building, "Pergola", new Vector3(-20f + slat * 5f, 33f, -1f), new Vector3(0.8f, 0.5f, 20f), Timber);
            }

            Cylinder(building, "PergolaPost", new Vector3(-20f, 32f, -10f), 0.5f, 1f, Timber);
            Forecourt(building, site, 15f, 30f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        /// <summary>Slot twelve: a gabled brick warehouse at the gate, loading doors and a timber water tower on the roof.</summary>
        private static void BuildWarehouse(MapSiteDefinition site)
        {
            var building = Site(site, "valley_boulevard", 70f, 34f, out _);

            Box(building, "Hall", new Vector3(0f, 6f, 0f), new Vector3(70f, 12f, 34f), BrickLight);
            Box(building, "Roof", new Vector3(0f, 12f, 0f), new Vector3(71f, 12f, 12f), Steel, 0f, 45f);

            for (var door = 0; door < 5; door++)
            {
                Box(building, "Door", new Vector3(-28f + door * 14f, 3f, 17.2f), new Vector3(7f, 6f, 0.4f), GlassDark);
            }

            var tower = Child(building, "WaterTower", new Vector3(24f, 12f, -6f));
            foreach (var (x, z) in Corners(2.4f))
            {
                Box(tower, "Leg", new Vector3(x, 5f, z), new Vector3(0.4f, 10f, 0.4f), Timber);
            }

            Cylinder(tower, "Tank", new Vector3(0f, 10f, 0f), 7f, 6f, Timber);
            Cylinder(tower, "Cap", new Vector3(0f, 16f, 0f), 7.6f, 0.8f, Steel);

            Forecourt(building, site, 17f, 14f, pool: false, grand: false, sign: site.DisplayName);
            Pin(building, site);
        }

        // ---- the ground between the buildings -----------------------------------------------------

        /// <summary>
        /// The central plaza, from the boulevard to the water on the Own Tower's axis: a long pool with
        /// jets at the boulevard end, rows of trees either side, a lattice sculpture, two café
        /// pavilions, and a pier with boats where it meets the bay.
        /// </summary>
        private static void BuildCentralPlaza()
        {
            var plaza = Child(root, "CentralPlaza");
            var east = BoulevardX(PlazaAxisZ) - 22f;
            var shore = ShoreX(PlazaAxisZ);
            var west = shore + 6f;
            var length = east - west;
            var middle = (east + west) * 0.5f;
            const float width = 130f;

            // Paved in strips across the axis, each on its own ground, so a slight fall to the water
            // is a set of shallow steps rather than a slab in the air.
            for (var x = west; x < east; x += 12f)
            {
                var centre = new Vector2(x + 6f, PlazaAxisZ);
                var ground = MaxGround(centre, 6f, width * 0.5f);
                Box(plaza, "Paving", new Vector3(centre.x, ground - 0.2f, centre.y), new Vector3(12.6f, 0.8f, width), Stone);
            }

            var level = MaxGround(new Vector2(middle, PlazaAxisZ), length * 0.5f, 8f);
            Box(plaza, "PoolRim", new Vector3(middle + 4f, level + 0.3f, PlazaAxisZ), new Vector3(length - 34f, 0.6f, 12f), StoneDark);
            Box(plaza, "Pool", new Vector3(middle + 4f, level + 0.45f, PlazaAxisZ), new Vector3(length - 36f, 0.6f, 10f), Water);

            for (var jet = 0; jet < 7; jet++)
            {
                Cylinder(plaza, "Jet", new Vector3(east - 26f, level + 0.6f, PlazaAxisZ - 9f + jet * 3f), 0.5f, 4f, White);
            }

            foreach (var side in new[] { -1f, 1f })
            {
                for (var x = west + 20f; x < east - 8f; x += 14f)
                {
                    foreach (var offset in new[] { 26f, 46f })
                    {
                        Tree(new Vector2(x, PlazaAxisZ + side * offset), 16f);
                    }
                }

                var cafe = new Vector2(west + 30f, PlazaAxisZ + side * 58f);
                var cafeGround = MaxGround(cafe, 8f, 6f);
                Box(plaza, "Cafe", new Vector3(cafe.x, cafeGround + 2.5f, cafe.y), new Vector3(16f, 5f, 10f), GlassLight);
                Box(plaza, "CafeRoof", new Vector3(cafe.x, cafeGround + 5.4f, cafe.y), new Vector3(20f, 0.8f, 14f), White);
            }

            // The lattice: a steel cube frame stood on one corner beside the pool.
            var sculptureAt = new Vector2(middle - length * 0.18f, PlazaAxisZ + 17f);
            var sculptureGround = MaxGround(sculptureAt, 5f, 5f);
            Cylinder(plaza, "SculptureBase", new Vector3(sculptureAt.x, sculptureGround, sculptureAt.y), 9f, 0.8f, StoneDark);
            var lattice = Child(plaza, "Lattice", new Vector3(sculptureAt.x, sculptureGround + 10.6f, sculptureAt.y));
            lattice.localRotation = Quaternion.Euler(35.26f, 45f, 0f);
            FrameCage(lattice, new Vector3(0f, -6f, 0f), 12f, 12f, 12f, 0.7f, Steel);

            // The pier and the boats at the end of the axis.
            var pierLength = 70f;
            Box(plaza, "Pier", new Vector3(shore - pierLength * 0.5f + 4f, CityLayout.SeaLevel + 1.4f, PlazaAxisZ),
                new Vector3(pierLength, 0.6f, 7f), Timber);
            for (var pile = 0; pile < 8; pile++)
            {
                foreach (var side in new[] { -3f, 3f })
                {
                    Cylinder(plaza, "Pile", new Vector3(shore - 4f - pile * 9f, CityLayout.SeaLevel - 3f, PlazaAxisZ + side), 0.6f, 4.4f, Timber);
                }
            }

            for (var boat = 0; boat < 5; boat++)
            {
                var side = boat % 2 == 0 ? 1f : -1f;
                Boat(plaza, new Vector3(shore - 14f - boat * 12f, CityLayout.SeaLevel, PlazaAxisZ + side * 9f));
            }

            Claim(new Vector2(middle, PlazaAxisZ), Vector2.right, Vector2.up, length * 0.5f, width * 0.5f);
            Count("central plaza", 1);
        }

        /// <summary>The gate from the port: two lit pylons either side of the boulevard, planted beds and a stone wall.</summary>
        private static void BuildGateway()
        {
            var gate = Child(root, "Gateway");
            const float z = -70f;
            var centre = BoulevardX(z);

            foreach (var side in new[] { -1f, 1f })
            {
                var at = new Vector2(centre + side * 30f, z);
                var ground = MaxGround(at, 4f, 4f);
                Box(gate, "Pylon", new Vector3(at.x, ground + 13f, at.y), new Vector3(5f, 26f, 5f), Steel);
                Box(gate, "PylonGlass", new Vector3(at.x, ground + 22f, at.y), new Vector3(5.4f, 7f, 5.4f), Lantern);
                Box(gate, "Bed", new Vector3(at.x + side * 14f, ground + 0.4f, at.y), new Vector3(20f, 0.8f, 16f), Lawn);
                Box(gate, "Wall", new Vector3(at.x + side * 14f, ground + 1f, at.y - 9f), new Vector3(22f, 2f, 1.2f), Stone);

                for (var step = 0; step < 3; step++)
                {
                    Tree(new Vector2(at.x + side * (8f + step * 7f), at.y + 4f), 12f);
                }
            }

            Count("gateway", 1);
        }

        /// <summary>
        /// A paved promenade along the whole shore: railing on the water side, lamps, and trees on the
        /// land side wherever a building has left room.
        /// </summary>
        private static void BuildPromenade()
        {
            var walk = Child(root, "Promenade");
            Vector2? last = null;
            var travelled = 0f;
            var laid = 0;

            for (var z = -110f; z >= -1160f; z -= 7f)
            {
                var shore = ShoreX(z);
                if (float.IsNaN(shore))
                {
                    last = null;
                    continue;
                }

                var here = new Vector2(shore + 13f, z);

                if (last.HasValue)
                {
                    var along = here - last.Value;
                    var middle = (here + last.Value) * 0.5f;
                    var yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
                    var ground = MaxGround(middle, 6f, 4f);
                    var segment = Child(walk, "Segment", new Vector3(middle.x, ground, middle.y), yaw);

                    Box(segment, "Paving", new Vector3(0f, -0.2f, 0f), new Vector3(11f, 0.8f, along.magnitude + 0.2f), Stone);
                    // The water is west: whichever way this segment turned, put the railing on that side.
                    var waterSide = -5.4f * Mathf.Sign(Mathf.Cos(yaw * Mathf.Deg2Rad));
                    Box(segment, "Railing", new Vector3(waterSide, 0.8f, 0f), new Vector3(0.3f, 1.2f, along.magnitude + 0.2f), Steel);

                    travelled += along.magnitude;
                    laid++;

                    if (travelled % 35f < along.magnitude)
                    {
                        Lamp(new Vector2(middle.x + 4.8f, middle.y), Vector2.left);
                    }

                    if (travelled % 21f < along.magnitude)
                    {
                        Tree(new Vector2(middle.x + 11f, middle.y), 15f);
                    }
                }

                last = here;
            }

            Count("promenade segments", laid);
        }

        /// <summary>Lawn and trees south of the pavilions, where the boulevard turns up to the bench.</summary>
        private static void BuildWaterfrontPark()
        {
            var planted = 0;

            for (var z = -1110f; z >= -1180f; z -= 16f)
            {
                var shore = ShoreX(z);
                if (float.IsNaN(shore))
                {
                    continue;
                }

                for (var x = shore + 28f; x < BoulevardX(-1100f) - 10f; x += 18f)
                {
                    if (Tree(new Vector2(x + (float)random.NextDouble() * 6f, z + (float)random.NextDouble() * 6f), 18f))
                    {
                        planted++;
                    }
                }
            }

            Count("park trees", planted);
        }

        /// <summary>
        /// The smaller offices between the addresses, where the district needs density rather than a
        /// landmark: the same glass-and-frame language as the headquarters, varied in size, glass,
        /// facade and roof so no two neighbours are the same building. Two old kit buildings stay at
        /// the port gate, where the waterfront was still industrial.
        /// </summary>
        private static void BuildNeighbours()
        {
            var plan = new (float X, float Z, float Width, float Depth, float Height, int Style, string Street)[]
            {
                (590f, -170f, 40f, 28f, 24f, 0, "valley_boulevard"),
                (600f, -240f, 34f, 26f, 32f, 1, "valley_boulevard"),
                (470f, -255f, 36f, 24f, 16f, 2, "valley_boulevard"),
                (720f, -385f, 30f, 24f, 26f, 3, "valley_bench"),
                (890f, -350f, 30f, 24f, 36f, 0, "valley_bench"),
                (900f, -540f, 28f, 22f, 30f, 1, "valley_bench"),
                (890f, -760f, 32f, 26f, 40f, 3, "valley_bench"),
                (895f, -935f, 24f, 20f, 24f, 0, "valley_bench"),
                (890f, -1010f, 38f, 28f, 28f, 1, "valley_bench"),
                (730f, -1062f, 36f, 24f, 18f, 3, "valley_bench"),
                (690f, -205f, 30f, 24f, 20f, 2, "valley_boulevard")
            };

            var neighbours = Child(root, "Neighbours");
            var built = 0;

            foreach (var (x, z, width, depth, height, style, street) in plan)
            {
                var at = new Vector2(x, z);
                var node = Place(neighbours, "Office", at, Toward(at, street), width, depth);

                switch (style)
                {
                    case 0:
                        Band(node, "Block", Vector3.zero, width, depth, height, GlassLight, Frame);
                        Box(node, "Plant", new Vector3(0f, height + 2f, -depth * 0.2f), new Vector3(width * 0.4f, 3f, depth * 0.3f), Concrete);
                        break;

                    case 1:
                        Box(node, "Block", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), GlassDark);
                        for (var fin = 0f; fin <= width; fin += 3f)
                        {
                            Box(node, "Fin", new Vector3(fin - width * 0.5f, height * 0.5f, depth * 0.5f + 0.5f), new Vector3(0.45f, height, 1f), Frame);
                        }

                        Box(node, "Cap", new Vector3(0f, height + 0.4f, 0f), new Vector3(width + 1f, 0.8f, depth + 1f), Frame);
                        break;

                    case 2:
                        Box(node, "Base", new Vector3(0f, 4f, 0f), new Vector3(width, 8f, depth), BrickLight);
                        Band(node, "Upper", new Vector3(0f, 8f, -1f), width - 4f, depth - 3f, height - 8f, Glass, Frame);
                        break;

                    default:
                        Band(node, "Block", Vector3.zero, width, depth, height * 0.6f, Glass, Frame);
                        Band(node, "Setback", new Vector3(0f, height * 0.6f, -depth * 0.2f), width * 0.7f, depth * 0.6f, height * 0.4f, Glass, Frame);
                        Box(node, "RoofGarden", new Vector3(0f, height * 0.6f + 0.75f, depth * 0.3f), new Vector3(width - 1f, 0.4f, depth * 0.38f), Lawn);
                        SmallTree(node, new Vector3(-width * 0.3f, height * 0.6f + 0.95f, depth * 0.33f), 5f);
                        SmallTree(node, new Vector3(width * 0.3f, height * 0.6f + 0.95f, depth * 0.33f), 5f);
                        break;
                }

                built++;
            }

            // The two left from when the gate was a working waterfront.
            foreach (var (name, x, z, width) in new[] { ("building-e", 400f, -118f, 30f), ("building-k", 575f, -128f, 34f) })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Commercial + name + ".fbx");
                if (model == null)
                {
                    continue;
                }

                var size = Measure(model);
                var scale = width / Mathf.Max(0.01f, size.x);
                var at = new Vector2(x, z);
                var node = Place(neighbours, name, at, Toward(at, "valley_boulevard"), width, size.z * scale);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, node);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.localPosition = Vector3.zero;
                var bounds = WorldBounds(instance);
                instance.transform.position += Vector3.up * (node.position.y - bounds.min.y);
                var centre = node.InverseTransformPoint(WorldBounds(instance).center);
                instance.transform.localPosition -= new Vector3(centre.x, 0f, centre.z);
                built++;
            }

            Count("neighbouring offices", built);
        }

        /// <summary>Trees and lamps along both sides of the district's own streets, clear of junctions, drives and buildings.</summary>
        private static void BuildStreetscape()
        {
            var planted = 0;
            var lit = 0;
            var centrelines = CityTerrainBuilder.RoadCentrelines();

            for (var index = 0; index < CityLayout.Roads.Count; index++)
            {
                var run = CityLayout.Roads[index];
                if (!run.Id.StartsWith("valley_"))
                {
                    continue;
                }

                var points = centrelines[index].Points;
                var travelled = 0f;

                for (var p = 0; p < points.Count - 1; p++)
                {
                    var a = points[p];
                    var b = points[p + 1];
                    var segment = Vector2.Distance(a, b);
                    if (segment < 0.01f)
                    {
                        continue;
                    }

                    var direction = (b - a) / segment;
                    var side = new Vector2(-direction.y, direction.x);

                    for (var s = 0f; s < segment; s += 1f)
                    {
                        var along = travelled + s;
                        var at = a + direction * s;

                        if (at.y > -60f || junctions.Any(junction => Vector2.Distance(junction, at) < 22f))
                        {
                            continue;
                        }

                        if (Mathf.Repeat(along, 17f) < 1f)
                        {
                            foreach (var hand in new[] { -1f, 1f })
                            {
                                if (Tree(at + side * hand * (run.Width * 0.5f + 7f), 15f))
                                {
                                    planted++;
                                }
                            }
                        }

                        if (Mathf.Repeat(along + 8f, 34f) < 1f)
                        {
                            foreach (var hand in new[] { -1f, 1f })
                            {
                                if (Lamp(at + side * hand * (run.Width * 0.5f + 2.5f), -side * hand))
                                {
                                    lit++;
                                }
                            }
                        }
                    }

                    travelled += segment;
                }
            }

            Count("street trees", planted);
            Count("street lamps", lit);
        }

        // ---- sites -----------------------------------------------------------------------------------

        /// <summary>
        /// The frame an address is built in: at its catalog position, turned to face its street, raised
        /// to the highest ground under its footprint, on a stone plinth down to the lowest.
        /// </summary>
        private static Transform Site(MapSiteDefinition site, string street, float width, float depth, out float plinth)
        {
            var at = new Vector2(site.Position.X, site.Position.Z);
            var node = Place(root, site.DisplayName, at, Toward(at, street), width, depth);
            plinth = node.position.y - MinGround(at, node, width * 0.5f + 2f, depth * 0.5f + 2f);
            return node;
        }

        private static Transform Place(Transform parent, string name, Vector2 at, Vector2 facing, float width, float depth)
        {
            var forward = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.up;
            var right = new Vector2(forward.y, -forward.x);
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.rotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y), Vector3.up);

            var highest = float.MinValue;
            var lowest = float.MaxValue;

            foreach (var point in Samples(at, right, forward, width * 0.5f, depth * 0.5f))
            {
                var ground = CityTerrainBuilder.HeightAt(point.x, point.y);
                highest = Mathf.Max(highest, ground);
                lowest = Mathf.Min(lowest, ground);
            }

            node.position = new Vector3(at.x, highest + 0.1f, at.y);

            if (highest - lowest > 0.4f)
            {
                var height = highest - lowest + 1.6f;
                Box(node, "Plinth", new Vector3(0f, -height * 0.5f + 0.1f, 0f), new Vector3(width + 4f, height, depth + 4f), Stone);
            }

            Claim(at, right, forward, width * 0.5f, depth * 0.5f);
            return node;
        }

        /// <summary>
        /// Clicks on the building open its card: one box collider round everything it is made of, and
        /// the pin component that tells the click which site it is.
        /// </summary>
        private static void Pin(Transform building, MapSiteDefinition site)
        {
            var bounds = WorldBounds(building.gameObject);
            var box = building.gameObject.AddComponent<BoxCollider>();
            box.center = building.InverseTransformPoint(bounds.center);
            var size = building.InverseTransformVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            building.gameObject.AddComponent<MapSitePin>().Describe(site.Category, site.Kind, site.Id);
            Count(site.Kind == MapSiteKind.RivalHeadquarters ? "headquarters" : "trophy offices", 1);
        }

        /// <summary>
        /// Paving from a building's front to its street, planted either side, with a monument sign at
        /// the kerb. A grand forecourt climbs to a raised platform on a stair and carries a pool on its axis.
        /// </summary>
        private static void Forecourt(Transform building, MapSiteDefinition site, float front, float width,
            bool pool, bool grand, string sign)
        {
            var origin = new Vector2(building.position.x, building.position.z);
            var forward = new Vector2(building.forward.x, building.forward.z).normalized;
            var right = new Vector2(building.right.x, building.right.z).normalized;

            var kerb = KerbDistance(origin, forward, front);
            var depth = kerb - front - 3f;
            if (depth < 4f)
            {
                return;
            }

            var court = Child(building, "Forecourt");
            var startGround = building.position.y;
            var climbed = false;
            var stairRun = 0f;

            for (var z = front; z < front + depth; z += 6f)
            {
                var slice = Mathf.Min(6f, front + depth - z);
                var centre = origin + forward * (z + slice * 0.5f);
                var ground = Mathf.Min(MaxGround(centre, right, forward, width * 0.5f, slice * 0.5f), startGround);
                var local = ground - building.position.y;

                // Where the plinth stands above the plaza, the plaza climbs to it in steps.
                if (!climbed && building.position.y - ground > 1.2f)
                {
                    var rise = building.position.y - ground;
                    var steps = Mathf.CeilToInt(rise / 0.8f);
                    stairRun = 1.4f * steps + 2f;

                    for (var step = 0; step < steps; step++)
                    {
                        var height = rise * (step + 1) / steps;
                        Box(court, "Stair", new Vector3(0f, local + height * 0.5f - 0.1f, front + 1.4f * (steps - step)),
                            new Vector3(width, height, 1.5f), Stone);
                    }

                    climbed = true;
                    continue;
                }

                Box(court, "Paving", new Vector3(0f, local - 0.2f, z + slice * 0.5f), new Vector3(width, 0.8f, slice + 0.6f), Stone);
            }

            for (var z = front + 6f; z < front + depth - 4f; z += 12f)
            {
                foreach (var hand in new[] { -1f, 1f })
                {
                    var spot = origin + forward * z + right * hand * (width * 0.5f - 4f);
                    Tree(spot, 13f);
                }
            }

            if (pool && depth - stairRun > 26f)
            {
                var poolLength = depth - stairRun - 16f;
                var poolCentre = front + stairRun + 8f + poolLength * 0.5f;
                var ground = MaxGround(origin + forward * poolCentre, right, forward, 6f, poolLength * 0.5f) - building.position.y;
                Box(court, "PoolRim", new Vector3(0f, ground + 0.3f, poolCentre), new Vector3(12f, 0.6f, poolLength + 2f), StoneDark);
                Box(court, "Pool", new Vector3(0f, ground + 0.45f, poolCentre), new Vector3(10f, 0.6f, poolLength), Water);
            }

            if (!string.IsNullOrEmpty(sign))
            {
                var at = front + depth - 3f;
                var ground = CityTerrainBuilder.HeightAt((origin + forward * at + right * (width * 0.25f)).x,
                    (origin + forward * at + right * (width * 0.25f)).y) - building.position.y;
                Box(court, "Sign", new Vector3(width * 0.25f, ground + 1.1f, at), new Vector3(12f, 2.2f, 1f), StoneDark);
                var text = Child(court, "SignText", new Vector3(width * 0.25f, ground + 1.1f, at + 0.52f), 180f);
                Text(text, sign, 0.9f);
            }
        }

        // ---- building parts ------------------------------------------------------------------------

        /// <summary>
        /// A glass volume banded at every storey: the glass box, a frame slab at each floor line, and
        /// a cap. What makes a block of colour read as an office building from the map camera.
        /// </summary>
        private static void Band(Transform parent, string name, Vector3 baseCentre, float width, float depth,
            float height, Material glass, Material frame, float storey = Storey)
        {
            var part = Child(parent, name, baseCentre);
            Box(part, "Glass", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), glass);

            var floors = Mathf.Max(1, Mathf.RoundToInt(height / storey));
            for (var floor = 1; floor < floors; floor++)
            {
                Box(part, "Floor", new Vector3(0f, height * floor / floors, 0f), new Vector3(width + 0.5f, 0.45f, depth + 0.5f), frame);
            }

            Box(part, "Cap", new Vector3(0f, height + 0.3f, 0f), new Vector3(width + 0.8f, 0.6f, depth + 0.8f), frame);
        }

        /// <summary>The twelve edges of a box as bars: a lantern cage, a crown ring, the sculpture.</summary>
        private static void FrameCage(Transform parent, Vector3 baseCentre, float width, float height, float depth,
            float bar, Material material)
        {
            var cage = Child(parent, "Cage", baseCentre);

            foreach (var y in new[] { 0f, height })
            {
                foreach (var z in new[] { -depth * 0.5f, depth * 0.5f })
                {
                    Box(cage, "Bar", new Vector3(0f, y, z), new Vector3(width + bar, bar, bar), material);
                }

                foreach (var x in new[] { -width * 0.5f, width * 0.5f })
                {
                    Box(cage, "Bar", new Vector3(x, y, 0f), new Vector3(bar, bar, depth + bar), material);
                }
            }

            foreach (var (x, z) in Corners(width * 0.5f, depth * 0.5f))
            {
                Box(cage, "Bar", new Vector3(x, height * 0.5f, z), new Vector3(bar, height, bar), material);
            }
        }

        private static void Boat(Transform parent, Vector3 at)
        {
            var boat = Child(parent, "Boat", at, (float)random.NextDouble() * 10f - 5f);
            Box(boat, "Hull", new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1.6f, 10f), White);
            Box(boat, "Deck", new Vector3(0f, 1.35f, -1f), new Vector3(2.2f, 1.1f, 3.4f), Timber);
            Cylinder(boat, "Mast", new Vector3(0f, 1.3f, 1f), 0.25f, 12f, Frame);
        }

        private static void FacadeName(Transform parent, string name, Vector3 at, float letter)
        {
            var node = Child(parent, "Name", at, 180f);
            Text(node, name, letter);
        }

        private static void Text(Transform node, string text, float letter)
        {
            var mesh = node.gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = letter * 0.1f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.97f, 0.97f, 0.95f);
            node.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void SmallTree(Transform parent, Vector3 local, float scale)
        {
            if (trees.Length == 0)
            {
                return;
            }

            var tree = (GameObject)PrefabUtility.InstantiatePrefab(trees[random.Next(trees.Length)], parent);
            tree.transform.localPosition = local;
            tree.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            tree.transform.localScale = Vector3.one * scale;
        }

        /// <summary>A tree on open ground, unless the spot is a street, a footprint or the water.</summary>
        private static bool Tree(Vector2 at, float scale)
        {
            if (trees.Length == 0 || OnARoad(at, 3f) || InAFootprint(at, 1f))
            {
                return false;
            }

            var ground = CityTerrainBuilder.HeightAt(at.x, at.y);
            if (ground < CityLayout.SeaLevel + 1.5f)
            {
                return false;
            }

            var tree = (GameObject)PrefabUtility.InstantiatePrefab(trees[random.Next(trees.Length)], greenery);
            tree.transform.position = new Vector3(at.x, ground, at.y);
            tree.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            tree.transform.localScale = Vector3.one * (scale * (0.85f + (float)random.NextDouble() * 0.3f));
            return true;
        }

        private static bool Lamp(Vector2 at, Vector2 facing)
        {
            if (lamp == null || OnARoad(at, 0.5f) || InAFootprint(at, 0.5f))
            {
                return false;
            }

            var ground = CityTerrainBuilder.HeightAt(at.x, at.y);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(lamp, greenery);
            instance.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);
            instance.transform.localScale = Vector3.one * (9f / Mathf.Max(0.01f, Measure(lamp).y));
            instance.transform.position = new Vector3(at.x, ground, at.y);
            return true;
        }

        private static Transform Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material,
            float yaw = 0f, float roll = 0f)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = centre;
            cube.transform.localRotation = Quaternion.Euler(roll, yaw, 0f);
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube.transform;
        }

        /// <summary>A cylinder standing on <paramref name="baseCentre"/>.</summary>
        private static Transform Cylinder(Transform parent, string name, Vector3 baseCentre, float diameter, float height,
            Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = baseCentre + Vector3.up * (height * 0.5f);
            cylinder.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cylinder.transform;
        }

        private static Transform Child(Transform parent, string name, Vector3 local = default, float yaw = 0f)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = local;
            node.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return node;
        }

        private static IEnumerable<(float X, float Z)> Corners(float half) => Corners(half, half);

        private static IEnumerable<(float X, float Z)> Corners(float halfX, float halfZ)
        {
            yield return (-halfX, -halfZ);
            yield return (halfX, -halfZ);
            yield return (-halfX, halfZ);
            yield return (halfX, halfZ);
        }

        // ---- the land and the streets ----------------------------------------------------------------

        /// <summary>The road tiles' boxes and the junction pieces' centres, read off the network in the scene.</summary>
        private static void ReadRoads(Transform city)
        {
            roadBoxes = new List<Bounds>();
            junctions = new List<Vector2>();
            var network = city.Find("RoadNetwork");

            if (network == null)
            {
                Debug.LogWarning("[Valley] No road network in the scene: lay it first, or buildings may stand on streets.");
                return;
            }

            foreach (var name in new[] { "Streets", "Junctions", "Ends", "Bridges" })
            {
                var group = network.Find(name);
                if (group == null)
                {
                    continue;
                }

                foreach (Transform tile in group)
                {
                    if (tile.position.z > 200f)
                    {
                        continue;
                    }

                    var renderers = tile.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length == 0)
                    {
                        continue;
                    }

                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    roadBoxes.Add(bounds);

                    if (name == "Junctions")
                    {
                        junctions.Add(new Vector2(tile.position.x, tile.position.z));
                    }
                }
            }
        }

        private static bool OnARoad(Vector2 at, float margin) => roadBoxes.Any(box =>
            at.x > box.min.x - margin && at.x < box.max.x + margin && at.y > box.min.z - margin && at.y < box.max.z + margin);

        private static bool InAFootprint(Vector2 at, float margin) => Footprints.Any(footprint =>
        {
            var offset = at - footprint.Centre;
            return Mathf.Abs(Vector2.Dot(offset, footprint.Right)) < footprint.HalfWidth + margin
                   && Mathf.Abs(Vector2.Dot(offset, footprint.Forward)) < footprint.HalfDepth + margin;
        });

        /// <summary>Records a footprint, and whether any of it lies on a street.</summary>
        private static void Claim(Vector2 centre, Vector2 right, Vector2 forward, float halfWidth, float halfDepth)
        {
            Footprints.Add((centre, right, forward, halfWidth, halfDepth));

            var touching = Samples(centre, right, forward, halfWidth, halfDepth).Count(point => OnARoad(point, 0f));
            if (touching > 0)
            {
                Conflicts.Add($"footprint at ({centre.x:0}, {centre.y:0}), {halfWidth * 2f:0} x {halfDepth * 2f:0} m: {touching} sample points on a street");
            }
        }

        private static IEnumerable<Vector2> Samples(Vector2 centre, Vector2 right, Vector2 forward, float halfWidth, float halfDepth)
        {
            for (var u = -halfWidth; u <= halfWidth + 0.01f; u += 4f)
            {
                for (var v = -halfDepth; v <= halfDepth + 0.01f; v += 4f)
                {
                    yield return centre + right * u + forward * v;
                }
            }
        }

        private static float MaxGround(Vector2 centre, float halfX, float halfZ) =>
            MaxGround(centre, Vector2.right, Vector2.up, halfX, halfZ);

        private static float MaxGround(Vector2 centre, Vector2 right, Vector2 forward, float halfWidth, float halfDepth) =>
            Samples(centre, right, forward, halfWidth, halfDepth).Max(point => CityTerrainBuilder.HeightAt(point.x, point.y));

        private static float MinGround(Vector2 centre, Transform frame, float halfWidth, float halfDepth)
        {
            var right = new Vector2(frame.right.x, frame.right.z);
            var forward = new Vector2(frame.forward.x, frame.forward.z);
            return Samples(centre, right, forward, halfWidth, halfDepth).Min(point => CityTerrainBuilder.HeightAt(point.x, point.y));
        }

        /// <summary>How far ahead of a point, along a direction, the nearest road tile begins.</summary>
        private static float KerbDistance(Vector2 origin, Vector2 forward, float start)
        {
            for (var distance = start; distance < 160f; distance += 1f)
            {
                if (OnARoad(origin + forward * distance, 1f))
                {
                    return distance;
                }
            }

            return start + 30f;
        }

        /// <summary>The direction from a point to the nearest point of a named road.</summary>
        private static Vector2 Toward(Vector2 at, string roadId)
        {
            var index = CityLayout.Roads.ToList().FindIndex(run => run.Id == roadId);
            if (index < 0)
            {
                return Vector2.up;
            }

            var points = CityTerrainBuilder.RoadCentrelines()[index].Points;
            var best = points[0];
            var bestDistance = float.MaxValue;

            for (var p = 0; p < points.Count - 1; p++)
            {
                var a = points[p];
                var b = points[p + 1];
                var ab = b - a;
                var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude) : 0f;
                var closest = a + ab * t;
                var distance = Vector2.Distance(closest, at);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = closest;
                }
            }

            return (best - at).normalized;
        }

        /// <summary>The boulevard's centreline X at a given Z, from its smoothed line.</summary>
        private static float BoulevardX(float z)
        {
            var index = CityLayout.Roads.ToList().FindIndex(run => run.Id == "valley_boulevard");
            var points = CityTerrainBuilder.RoadCentrelines()[index].Points;

            for (var p = 0; p < points.Count - 1; p++)
            {
                var a = points[p];
                var b = points[p + 1];

                if ((a.y - z) * (b.y - z) <= 0f && Mathf.Abs(a.y - b.y) > 0.001f)
                {
                    return Mathf.Lerp(a.x, b.x, (z - a.y) / (b.y - a.y));
                }
            }

            return points[points.Count - 1].x;
        }

        /// <summary>Where land begins going east along a line of Z, or NaN where there is none to find.</summary>
        private static float ShoreX(float z)
        {
            for (var x = 150f; x < 900f; x += 2f)
            {
                if (CityTerrainBuilder.HeightAt(x, z) >= CityLayout.SeaLevel + 0.6f)
                {
                    return x;
                }
            }

            return float.NaN;
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

        private static Bounds WorldBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;

            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static void Count(string what, int howMany)
        {
            Counts.TryGetValue(what, out var count);
            Counts[what] = count + howMany;
        }
    }
}
