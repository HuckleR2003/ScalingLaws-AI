using System;
using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The land Bayview sits on. Stage one of Docs/CITY_MAP_PLAN.md.
    ///
    /// **Terrain before buildings, and districts flattened before either.** Every building generator
    /// in this project is a box builder that assumes a flat floor, so ground that slopes under a
    /// district is ground that will float or sink a building later. The hills are scenery between
    /// the districts; the districts themselves are levelled pads.
    ///
    /// Generated rather than sculpted for the same reason the rooms are: the layout lives in
    /// <see cref="DistrictCatalog"/>, one description of the city that the terrain, the map screen
    /// and the tests all read. Moving a district is editing one line, not redrawing a heightmap.
    ///
    /// Deterministic from a fixed seed, so two runs of this produce the same coastline and a
    /// screenshot from last week still matches what is on disk.
    /// </summary>
    public static class CityTerrainBuilder
    {
        private const string ScenesFolder = "Assets/_ScalingLaws/Scenes";
        private const string DataFolder = "Assets/_ScalingLaws/Terrain";

        private const string ScenePath = ScenesFolder + "/City.unity";
        private const string TerrainDataPath = DataFolder + "/BayviewTerrain.asset";

        /// <summary>Fixed, so the coastline is the same coastline every time this is run.</summary>
        private const int Seed = 20260816;

        /// <summary>Metres of flat ground kept around a district before the land starts to rise.</summary>
        private const float PadMargin = 60f;

        /// <summary>How far past the pad the ground blends back to its natural height.</summary>
        private const float BlendWidth = 220f;

        [MenuItem("Scaling Laws/Build the city terrain")]
        public static void Build()
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(DataFolder);

            var data = BuildTerrainData();
            AssetDatabase.SaveAssets();

            BuildScene(data);

            Debug.Log($"[Scaling Laws] Bayview built: {DistrictCatalog.TerrainSize}m square, "
                + $"{DistrictCatalog.HeightmapResolution} heightmap, "
                + $"{DistrictCatalog.All.Count} districts. Scene at {ScenePath}.");
        }

        // ---- the land ---------------------------------------------------------------------------

        private static TerrainData BuildTerrainData()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);

            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = DistrictCatalog.HeightmapResolution;

            data.size = new Vector3(
                DistrictCatalog.TerrainSize,
                DistrictCatalog.TerrainHeight,
                DistrictCatalog.TerrainSize);

            // 512 rather than the default 1024: grass belongs in the parks, and a detail map that
            // covers the whole city is a detail map mostly describing rooftops.
            data.SetDetailResolution(512, 16);

            var resolution = data.heightmapResolution;
            var heights = new float[resolution, resolution];

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    // Heightmap is indexed [z, x], which is the one thing about Unity terrain that
                    // catches everybody once.
                    var worldX = x / (float)(resolution - 1) * DistrictCatalog.TerrainSize;
                    var worldZ = y / (float)(resolution - 1) * DistrictCatalog.TerrainSize;

                    heights[y, x] = HeightAt(worldX, worldZ) / DistrictCatalog.TerrainHeight;
                }
            }

            data.SetHeights(0, 0, heights);

            // Without a layer the terrain renders pure white and the plan shot is unreadable. One
            // flat ground colour is enough for stage one; the real texturing belongs with the roads
            // in stage three, when there is something to texture around.
            data.terrainLayers = new[] { GroundLayer() };

            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// The natural land, the districts flattened on it, and the water cut through last.
        ///
        /// **The order was the other way round and the first render showed why it was wrong.** With
        /// flattening last, a pad always won — including over the bay — so Greendale and Downtown
        /// filled in the channel that is supposed to separate them and the map had a puddle in the
        /// corner instead of a harbour.
        ///
        /// Water wins now. A district that ends up wet is a layout mistake to be fixed by moving
        /// the district, not by quietly filling the sea in underneath it, and the plan render is
        /// where that shows up.
        /// </summary>
        private static float HeightAt(float x, float z)
        {
            var height = NaturalHeight(x, z);
            height = FlattenDistricts(x, z, height);
            return CarveWater(x, z, height);
        }

        /// <summary>
        /// Hills to the east and north, a shallow shelf in the middle where the city goes.
        ///
        /// Three octaves of value noise rather than Perlin, because Mathf.PerlinNoise is not
        /// guaranteed identical across Unity versions and this has to regenerate the same land in
        /// two years' time.
        /// </summary>
        private static float NaturalHeight(float x, float z)
        {
            var u = x / DistrictCatalog.TerrainSize;
            var v = z / DistrictCatalog.TerrainSize;

            // The ridge: high in the north east, falling away towards the south west where the sea
            // and the flat land are. This is what gives the map the shape of the reference.
            var ridge = Mathf.Clamp01((u * 0.62f + v * 0.55f) - 0.42f);
            var basement = DistrictCatalog.SeaLevel + 8f + ridge * ridge * 300f;

            var detail =
                Noise(u * 3.1f, v * 3.1f) * 34f
                + Noise(u * 7.7f, v * 7.7f) * 14f
                + Noise(u * 17.3f, v * 17.3f) * 5f;

            return basement + detail;
        }

        /// <summary>
        /// The bay from the north west and the river down from the eastern hills.
        ///
        /// Both are cut as a distance to a line rather than painted, so the banks are smooth and the
        /// depth falls away from the shore instead of stepping down a cliff.
        /// </summary>
        private static float CarveWater(float x, float z, float height)
        {
            // The bay: a broad channel running from the north-west corner into the middle of the
            // map, which is what separates Greendale from Downtown and is why there are bridges.
            // A channel with banks rather than a basin: the whole point of it is that bridges
            // cross it, and nothing bridges four hundred metres of open water.
            var bay = DistanceToSegment(x, z, 60f, 2048f, 1020f, 1130f);
            var bayDepth = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((bay - 60f) / 110f));

            // The river: narrower, from the eastern hills down past the energy belt to the sea.
            var river = DistanceToSegment(x, z, 1900f, 1500f, 520f, 180f);
            var riverDepth = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((river - 14f) / 46f));

            var water = Mathf.Max(bayDepth, riverDepth);

            if (water <= 0f)
            {
                return height;
            }

            // Down to a bed below the sea, so the water plane has something to sit in rather than
            // meeting the land exactly at its own level.
            // Shallow banks and a deeper middle. The first pass cut a bed twenty metres down and
            // then lerped towards it across the whole falloff, which put a wide skirt of land under
            // sea level and drew both waterways as motorways rather than as a channel and a river.
            var bed = DistrictCatalog.SeaLevel - 4f - water * 26f;
            return Mathf.Lerp(height, bed, water);
        }

        /// <summary>
        /// Levels the ground under each district and blends it back into the hills.
        ///
        /// The pad is the district radius plus a margin, so a building on the edge still has flat
        /// ground under its car park. Beyond the pad the height eases back over
        /// <see cref="BlendWidth"/> rather than stepping, because a district on a plateau with
        /// vertical sides looks like a bug and not like a valley.
        /// </summary>
        private static float FlattenDistricts(float x, float z, float height)
        {
            foreach (var district in DistrictCatalog.All)
            {
                var dx = x - district.CentreX;
                var dz = z - district.CentreZ;
                var distance = Mathf.Sqrt(dx * dx + dz * dz);

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

        // ---- the scene ------------------------------------------------------------------------

        private static void BuildScene(TerrainData data)
        {
            if (!ScalingLawsSceneBuilder.MayOverwriteScene(ScenePath))
            {
                Debug.LogWarning($"Kept {ScenePath} as it is. The terrain asset was still rebuilt.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Bayview";

            var terrain = terrainObject.GetComponent<Terrain>();

            // The camera looks at the whole city, so the basemap has to reach further than the
            // default thousand metres or the far half of the map renders as flat colour.
            terrain.basemapDistance = 2000f;
            terrain.heightmapPixelError = 3f;

            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";

            // A Unity plane is ten metres across, so the scale is the map size over ten, and a
            // little over so the edge is never visible from a corner.
            sea.transform.localScale = new Vector3(
                DistrictCatalog.TerrainSize / 10f * 1.2f, 1f,
                DistrictCatalog.TerrainSize / 10f * 1.2f);

            sea.transform.position = new Vector3(
                DistrictCatalog.TerrainSize / 2f,
                DistrictCatalog.SeaLevel,
                DistrictCatalog.TerrainSize / 2f);

            UnityEngine.Object.DestroyImmediate(sea.GetComponent<MeshCollider>());
            sea.GetComponent<MeshRenderer>().sharedMaterial = SeaMaterial();

            var markers = new GameObject("Districts");

            foreach (var district in DistrictCatalog.All)
            {
                var marker = new GameObject(district.DisplayName);
                marker.transform.SetParent(markers.transform, false);
                marker.transform.position =
                    new Vector3(district.CentreX, district.GroundHeight, district.CentreZ);
            }

            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.90f);
            sunObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);
            camera.farClipPlane = 6000f;

            // The reference's angle, from the south west, high enough to hold the whole city.
            cameraObject.transform.rotation = Quaternion.Euler(42f, 35f, 0f);
            cameraObject.transform.position = new Vector3(-420f, 1500f, -520f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>A single flat ground layer, created once and reused on later runs.</summary>
        private static TerrainLayer GroundLayer()
        {
            const string path = DataFolder + "/Ground.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

            if (layer != null)
            {
                return layer;
            }

            var texture = new Texture2D(8, 8);
            var ground = new Color(0.34f, 0.38f, 0.31f);

            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    texture.SetPixel(x, y, ground);
                }
            }

            texture.Apply();

            AssetDatabase.CreateAsset(texture, DataFolder + "/GroundTexture.asset");

            layer = new TerrainLayer
            {
                diffuseTexture = texture,
                tileSize = new Vector2(32f, 32f)
            };

            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        private static Material SeaMaterial()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            shader = shader != null ? shader : Shader.Find("Standard");

            var material = new Material(shader) { name = "Sea" };
            var colour = new Color(0.08f, 0.22f, 0.34f);

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
                material.SetFloat("_Glossiness", 0.85f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.85f);
            }

            return material;
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>
        /// Distance from a point to a line segment, in metres.
        ///
        /// The bay and the river are both cut from one of these, which is what keeps their banks
        /// smooth and their depth falling away from the shore.
        /// </summary>
        private static float DistanceToSegment(float px, float pz,
            float ax, float az, float bx, float bz)
        {
            var abx = bx - ax;
            var abz = bz - az;
            var apx = px - ax;
            var apz = pz - az;

            var lengthSquared = abx * abx + abz * abz;
            var t = lengthSquared <= 0f ? 0f : Mathf.Clamp01((apx * abx + apz * abz) / lengthSquared);

            var cx = ax + abx * t;
            var cz = az + abz * t;

            return Mathf.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
        }

        /// <summary>
        /// Smoothed value noise from a fixed integer hash.
        ///
        /// Not Mathf.PerlinNoise: that is not promised to be identical across Unity versions, and a
        /// terrain that quietly changes shape on an upgrade is a terrain whose districts no longer
        /// sit on their pads.
        /// </summary>
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
