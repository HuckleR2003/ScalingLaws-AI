using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Gives every <see cref="MapSiteCatalog"/> entry a real building, chosen to suit what the site
    /// is, and makes it clickable.
    ///
    /// **A coloured pin is a label, not a place.** The twenty-six sites are the only things on this
    /// map a player is ever going to make a decision about — rent this office, buy that house, take
    /// a stake in that plant — and until now each was a stick in the ground. A serverless hall and
    /// a family home read identically. This stands the right kind of building under each pin, and
    /// hangs the catalog entry off it so a click can say what it is.
    ///
    /// The pin stays. It is what makes a site findable from across the map and what the legend
    /// filters; the building is what makes it somewhere.
    /// </summary>
    public static class CitySiteBuildings
    {
        private const string Commercial =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        private const string Suburban =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";

        /// <summary>
        /// What each kind of site should look like, and how wide it should be.
        ///
        /// Widths are chosen against the site's own surveyed radius where that means anything: a
        /// megasite server hall has a thirty metre radius and should not be a corner shop.
        /// </summary>
        private readonly struct Choice
        {
            public Choice(string folder, string[] models, float width)
            {
                Folder = folder;
                Models = models;
                Width = width;
            }

            public string Folder { get; }
            public string[] Models { get; }
            public float Width { get; }
        }

        private static Choice ChoiceFor(MapSiteKind kind, int tier) => kind switch
        {
            // Offices climb a ladder, so the building climbs with the tier.
            MapSiteKind.OfficeLease => new Choice(Commercial,
                tier >= 4 ? new[] { "building-skyscraper-b", "building-skyscraper-d" }
                : tier >= 2 ? new[] { "building-l", "building-m" }
                : new[] { "building-a", "building-h" },
                tier >= 4 ? 40f : tier >= 2 ? 32f : 26f),

            // A home is a house, and a better one is a bigger house.
            MapSiteKind.PropertyListing => new Choice(Suburban,
                tier >= 3 ? new[] { "building-type-n", "building-type-t" }
                : new[] { "building-type-f", "building-type-j" },
                tier >= 3 ? 26f : 20f),

            // Low, wide, windowless-looking: a shed full of racks.
            MapSiteKind.ServerFacility => new Choice(Commercial,
                new[] { "building-e", "building-k" },
                18f + Mathf.Clamp(tier, 0, 5) * 8f),

            // A hall big enough to hold a crowd.
            MapSiteKind.EventVenue => new Choice(Commercial,
                new[] { "building-n", "building-j" }, 46f),

            MapSiteKind.PowerPlantStake => new Choice(Commercial,
                new[] { "building-k", "building-e" }, 42f),

            MapSiteKind.JobAgency => new Choice(Commercial,
                new[] { "building-b", "building-d" }, 24f),

            // The civic pile: the tallest thing that is not an office tower.
            MapSiteKind.TaxOffice => new Choice(Commercial,
                new[] { "building-m" }, 34f),

            MapSiteKind.CarDealership => new Choice(Commercial,
                new[] { "building-c", "building-e" }, 28f),

            _ => new Choice(Commercial, new[] { "building-a" }, 24f)
        };

        [MenuItem("Scaling Laws/Stand a building on every map site")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Sites] No City.unity.");
                return;
            }

            var existing = GameObject.Find("SiteBuildings");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var cityRoot = GameObject.Find("City");
            var root = new GameObject("SiteBuildings").transform;
            if (cityRoot != null)
            {
                root.SetParent(cityRoot.transform, false);
            }

            var pins = IndexPins();
            var random = new System.Random(20260917);

            var built = 0;
            var cleared = 0;

            foreach (var site in MapSiteCatalog.All)
            {
                var choice = ChoiceFor(site.Kind, site.Tier);
                var model = Pick(choice, random);

                if (model == null)
                {
                    Debug.LogWarning($"[Sites] No model for {site.Id}.");
                    continue;
                }

                var height = CityTerrainBuilder.HeightAt(site.Position.X, site.Position.Z);

                if (height < CityLayout.SeaLevel + 1f)
                {
                    // The two power plants stand in the water on purpose and are a known open
                    // question; everything else being wet would be a bug worth hearing about.
                    Debug.LogWarning($"[Sites] {site.Id} is below the waterline ({height:0.0}m); skipped.");
                    continue;
                }

                var at = new Vector3(site.Position.X, height, site.Position.Z);

                // The site's building takes precedence over whatever generic filler landed here:
                // this is the one building on this spot that means something.
                cleared += ClearSpace(at, choice.Width, root);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                instance.name = site.DisplayName;
                instance.transform.position = at;

                // Turned to face the middle of its own district, so a row of sites does not all
                // stare the same way regardless of where the streets are — and, where the district
                // has a street grid, squared up to it, so a hall stands parallel to its street
                // instead of across the corner of its block.
                var position = new Vector2(site.Position.X, site.Position.Z);
                var facing = SquareToStreets(site.DistrictId, position, DistrictCentre(site.DistrictId) - position);

                instance.transform.rotation = facing.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up)
                    : Quaternion.identity;

                if (!TryMeasure(model, out var size) || size.x <= 0.001f)
                {
                    Object.DestroyImmediate(instance);
                    continue;
                }

                instance.transform.localScale = Vector3.one * (choice.Width / size.x);
                SitOnGround(instance, height);

                // What makes it clickable, and what tells the click which site it is.
                var box = instance.AddComponent<BoxCollider>();
                var bounds = WorldBounds(instance);
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                // Turned into the building's own frame a size can come out negative on an axis, which
                // a collider refuses with a warning on every load; a size is a size either way round.
                var size3 = instance.transform.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(size3.x), Mathf.Abs(size3.y), Mathf.Abs(size3.z));

                instance.AddComponent<MapSitePin>().Describe(site.Category, site.Kind, site.Id);

                // The pin that was already here is the same site, so it learns its id too — the
                // legend filter still drives off the pin, and a click on the pin should now be able
                // to answer the same question a click on the building does.
                if (pins.TryGetValue(site.DisplayName, out var pin))
                {
                    pin.Describe(site.Category, site.Kind, site.Id);
                    EditorUtility.SetDirty(pin);
                }

                built++;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Sites] {built} site buildings stood up, {cleared} generic buildings cleared "
                + "to make room.");
        }

        /// <summary>The pins already in the scene, by the name `MapSiteBuilder` gave them.</summary>
        private static Dictionary<string, MapSitePin> IndexPins()
        {
            var pins = new Dictionary<string, MapSitePin>();

            foreach (var pin in Object.FindObjectsByType<MapSitePin>(FindObjectsSortMode.None))
            {
                pins[pin.gameObject.name] = pin;
            }

            return pins;
        }

        /// <summary>
        /// Removes plain infill buildings standing where a site's building is about to go. Only
        /// touches things under the infill and gallery groups — never a road, a tree, or the pin.
        /// </summary>
        private static int ClearSpace(Vector3 at, float width, Transform siteRoot)
        {
            var reach = width * 0.75f;
            var doomed = new List<GameObject>();

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var root = renderer.transform.root;
                var underInfill = false;

                for (var t = renderer.transform; t != null; t = t.parent)
                {
                    if (t.name is "DistrictInfill" or "GalleryWings")
                    {
                        underInfill = true;
                        break;
                    }
                }

                if (!underInfill)
                {
                    continue;
                }

                var here = renderer.bounds.center;
                var dx = here.x - at.x;
                var dz = here.z - at.z;

                if (dx * dx + dz * dz < reach * reach)
                {
                    var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject)
                                   ?? renderer.gameObject;

                    if (!doomed.Contains(instance))
                    {
                        doomed.Add(instance);
                    }
                }
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
            }

            return doomed.Count;
        }

        /// <summary>
        /// A facing snapped to the nearest of the four directions of the street grid the site stands
        /// in: the district's grid whose rectangle holds it, or failing that its nearest. Left as it
        /// is in a district with no grid.
        /// </summary>
        private static Vector2 SquareToStreets(string districtId, Vector2 position, Vector2 facing)
        {
            GridBlock best = null;
            var bestScore = float.MaxValue;

            foreach (var grid in CityBlocks.Grids)
            {
                if (grid.DistrictId != districtId)
                {
                    continue;
                }

                var angle = grid.RotationDegrees * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);
                var offset = position - new Vector2(grid.CentreX, grid.CentreZ);
                var inside = Mathf.Abs(Vector2.Dot(offset, across)) <= grid.Width * 0.5f + 40f
                             && Mathf.Abs(Vector2.Dot(offset, along)) <= grid.Depth * 0.5f + 40f;
                var score = (inside ? 0f : 10000f) + offset.magnitude;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = grid;
                }
            }

            if (best == null || facing.sqrMagnitude < 1f)
            {
                return facing;
            }

            var radians = best.RotationDegrees * Mathf.Deg2Rad;
            var axes = new[]
            {
                new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)),
                new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians))
            };

            var chosen = facing;
            var closest = float.MinValue;

            foreach (var axis in axes)
            {
                foreach (var sign in new[] { 1f, -1f })
                {
                    var dot = Vector2.Dot(facing.normalized, axis * sign);
                    if (dot > closest)
                    {
                        closest = dot;
                        chosen = axis * sign;
                    }
                }
            }

            return chosen;
        }

        private static Vector2 DistrictCentre(string districtId)
        {
            foreach (var district in CityLayout.Districts)
            {
                if (district.Id == districtId)
                {
                    return new Vector2(district.CentreX, district.CentreZ);
                }
            }

            return new Vector2(CityLayout.Size * 0.5f, CityLayout.Size * 0.5f);
        }

        private static GameObject Pick(Choice choice, System.Random random)
        {
            var name = choice.Models[random.Next(choice.Models.Length)];
            return AssetDatabase.LoadAssetAtPath<GameObject>(choice.Folder + name + ".fbx");
        }

        private static void SitOnGround(GameObject instance, float groundY)
        {
            var bounds = WorldBounds(instance);
            instance.transform.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static Bounds WorldBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(instance.transform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
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
    }
}
