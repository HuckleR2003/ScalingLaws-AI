using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Builds the houses on subdivisions added to <see cref="CityBlocks.Residential"/> after the city
    /// was dressed.
    ///
    /// The original subdivisions were surveyed by <see cref="CityDressingBuilder"/> as grey boxes and
    /// swapped for Kenney models afterwards, a chain that only runs on a whole new scene. A subdivision
    /// added later gets the same result in one step: the same plots along the same streets — the
    /// street lines are the ones <see cref="CityRoadNetwork"/> lays from the same brief — and the same
    /// house the swap left behind: a `House` (or `Villa`) plot turned to face its street, holding the
    /// house model, a small garage model to one side and usually a tree. That shape is what the road
    /// network reads to lay each driveway from the garage door to the kerb, so rebuild the network
    /// after this.
    ///
    /// A subdivision that already has its `Subdivision_&lt;id&gt;` group is left alone, so this can be run
    /// again safely after adding another brief.
    /// </summary>
    public static class CitySuburbGrowth
    {
        private const string Suburban = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";

        /// <summary>Street and pavement, as the original surveyor measured them.</summary>
        private const float StreetWidth = 10f;
        private const float SidewalkWidth = 1.6f;

        /// <summary>The garage model is a villa model at a garage's size, the way the swap left it.</summary>
        private const float GarageScale = 3.83f;

        /// <summary>How far either side of a long subdivision's middle cross street a plot stays empty: the road, its pavement and a garage.</summary>
        private const float MiddleClearance = 18f;

        [MenuItem("Scaling Laws/Densify/Build houses on new subdivisions")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var city = GameObject.Find("City")?.transform;

            if (!scene.IsValid() || city == null)
            {
                Debug.LogError("[Suburbs] No City.unity with a City root.");
                return;
            }

            var houses = Load("building-type-a", "building-type-b", "building-type-c", "building-type-d",
                "building-type-e", "building-type-f");
            var villas = Load("building-type-h", "building-type-i", "building-type-j");
            var garage = Load("building-type-h").FirstOrDefault();
            var trees = Load("tree-large", "tree-small");

            // Houses already standing, and road tiles, as obstacles a new plot must keep clear of.
            var standing = city.GetComponentsInChildren<Transform>()
                .Where(t => t.name is "House" or "Villa" or "FounderHouse")
                .Select(t => new Vector2(t.position.x, t.position.z)).ToList();
            var roads = city.Find("RoadNetwork") is { } network
                ? network.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds).ToList()
                : new List<Bounds>();

            // `-rebuild id,id` on the command line takes those subdivisions' houses down first, for
            // when a brief has been moved.
            var args = System.Environment.GetCommandLineArgs();
            var flag = System.Array.IndexOf(args, "-rebuild");
            if (flag >= 0 && flag + 1 < args.Length)
            {
                foreach (var id in args[flag + 1].Split(','))
                {
                    var old = city.Find($"Subdivision_{id}");
                    if (old != null)
                    {
                        Object.DestroyImmediate(old.gameObject);
                    }
                }

                standing = city.GetComponentsInChildren<Transform>()
                    .Where(t => t.name is "House" or "Villa" or "FounderHouse")
                    .Select(t => new Vector2(t.position.x, t.position.z)).ToList();

                // Their old streets are still laid and will move with the rebuild: they are no
                // obstacle to the plots that replace them.
                var rebuilt = CityBlocks.Residential.Where(block => args[flag + 1].Split(',').Contains(block.Id)).ToList();
                roads = roads.Where(box => !rebuilt.Any(block => Inside(block, new Vector2(box.center.x, box.center.z), 30f))).ToList();
            }

            var random = new System.Random(20260917);
            var report = new List<string>();

            foreach (var block in CityBlocks.Residential)
            {
                if (city.Find($"Subdivision_{block.Id}") != null)
                {
                    continue;
                }

                var group = new GameObject($"Subdivision_{block.Id}").transform;
                group.SetParent(city, false);

                var angle = block.RotationDegrees * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);
                var centre = new Vector2(block.CentreX, block.CentreZ);
                var streets = Mathf.Max(1, Mathf.FloorToInt(block.Width / block.StreetSpacing));
                var built = 0;
                var refused = 0;

                for (var street = 0; street < streets; street++)
                {
                    var line = centre + across * (-block.Width * 0.5f + block.StreetSpacing * (street + 0.5f));
                    var from = line - along * block.Depth * 0.5f;
                    var count = Mathf.FloorToInt(block.Depth / block.LotWidth);

                    foreach (var side in new[] { -1f, 1f })
                    {
                        var outward = across * side;

                        for (var lot = 0; lot < count; lot++)
                        {
                            // The middle cross street of a long subdivision runs through here: no house.
                            var alongFromMiddle = (lot + 0.5f) * block.LotWidth - block.Depth * 0.5f;
                            if (block.Depth >= 240f && Mathf.Abs(alongFromMiddle) < MiddleClearance)
                            {
                                continue;
                            }

                            var frontage = from + along * ((lot + 0.5f) * block.LotWidth);
                            var kerb = frontage + outward * (StreetWidth * 0.5f + SidewalkWidth);
                            var plot = kerb + outward * block.Setback;

                            if (!Buildable(plot, standing, roads))
                            {
                                refused++;
                                continue;
                            }

                            House(group, block, plot, -outward, block.Grand ? villas : houses, garage, trees, random);
                            standing.Add(plot);
                            built++;
                        }
                    }
                }

                report.Add($"{block.Id}: {built} houses, {refused} plots refused (water, slope, or already taken)");
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log(report.Count == 0
                ? "[Suburbs] Every subdivision already has its houses."
                : "[Suburbs] " + string.Join("\n[Suburbs] ", report) + "\n[Suburbs] Now rebuild the road network, which lays their driveways.");
        }

        private static bool Inside(ResidentialBlock block, Vector2 at, float margin)
        {
            var angle = block.RotationDegrees * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-along.y, along.x);
            var offset = at - new Vector2(block.CentreX, block.CentreZ);
            return Mathf.Abs(Vector2.Dot(offset, across)) <= block.Width * 0.5f + margin
                   && Mathf.Abs(Vector2.Dot(offset, along)) <= block.Depth * 0.5f + margin;
        }

        /// <summary>The plot rules the original surveyor used: dry, not falling away, and not on top of anything.</summary>
        private static bool Buildable(Vector2 plot, List<Vector2> standing, List<Bounds> roads)
        {
            var height = CityTerrainBuilder.HeightAt(plot.x, plot.y);

            if (height < CityLayout.SeaLevel + 3f)
            {
                return false;
            }

            var slope = Mathf.Abs(height - CityTerrainBuilder.HeightAt(plot.x + 12f, plot.y))
                        + Mathf.Abs(height - CityTerrainBuilder.HeightAt(plot.x, plot.y + 12f));

            if (slope > 7f)
            {
                return false;
            }

            if (standing.Any(other => Vector2.Distance(other, plot) < 16f))
            {
                return false;
            }

            return !roads.Any(box => plot.x > box.min.x - 9f && plot.x < box.max.x + 9f
                                     && plot.y > box.min.z - 9f && plot.y < box.max.z + 9f);
        }

        private static void House(Transform group, ResidentialBlock block, Vector2 plot, Vector2 facing,
            IReadOnlyList<GameObject> models, GameObject garageModel, IReadOnlyList<GameObject> trees, System.Random random)
        {
            var height = CityTerrainBuilder.HeightAt(plot.x, plot.y);
            var house = new GameObject(block.Grand ? "Villa" : "House").transform;
            house.SetParent(group, false);
            house.position = new Vector3(plot.x, height, plot.y);
            house.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);

            var width = block.Grand ? Range(random, 14f, 17f) : Range(random, 10f, 13f);
            var model = models[random.Next(models.Count)];
            var body = Place(house, model, Vector3.zero, width / Mathf.Max(0.01f, Measure(model).x));

            var garageSide = random.Next(2) == 0 ? -1f : 1f;
            if (garageModel != null)
            {
                Place(house, garageModel, new Vector3(garageSide * (width * 0.5f + 3.2f), 0f, 2.8f), GarageScale);
            }

            if (trees.Count > 0 && random.Next(10) < 7)
            {
                var tree = Place(house, trees[random.Next(trees.Count)],
                    new Vector3(-garageSide * (width * 0.5f + 4.5f), 0f, -3.4f), Range(random, 14f, 19f));
                tree.localRotation = Quaternion.Euler(0f, random.Next(360), 0f);
            }

            // Stood on the ground by its own measured base.
            var bounds = body.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds)
                .Aggregate((a, b) => { a.Encapsulate(b); return a; });
            body.position += Vector3.up * (height - bounds.min.y);
        }

        private static Transform Place(Transform parent, GameObject model, Vector3 local, float scale)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.transform.localPosition = local;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;
            return instance.transform;
        }

        private static List<GameObject> Load(params string[] names) => names
            .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(Suburban + name + ".fbx"))
            .Where(model => model != null)
            .ToList();

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

        private static float Range(System.Random random, float from, float to) =>
            from + (float)random.NextDouble() * (to - from);
    }
}
