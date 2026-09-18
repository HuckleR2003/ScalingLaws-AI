using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The wind farm on the ridge east of Silicon Valley, and the empty ground beside it that is for
    /// sale.
    ///
    /// **Built from primitives on purpose.** The author asked for turbines now and better models
    /// later, so this makes them out of a cylinder, a box and three blades rather than waiting for a
    /// pack: a tower that reads as a tower from map height, and a rotor that turns. Swapping in a
    /// model later means changing `Build` alone, because nothing else in the game knows what a
    /// turbine is made of.
    ///
    /// **Where.** The ground climbs from 65 m at x = 1060 to 225 m at the map's east edge, and holds
    /// nothing at all between Silicon Valley's streets and the top. Turbines stand on the ridge line
    /// rather than on the peak, which is where they stand in life: the wind is squeezed over the
    /// shoulder, and from the valley they are on the skyline.
    /// </summary>
    public static class CityWindFarm
    {
        private const string GroupName = "WindFarm";

        /// <summary>Along the ridge, in metres.</summary>
        private static readonly Vector2 From = new(1180f, -1010f);
        private static readonly Vector2 To = new(1330f, -380f);

        /// <summary>How many stand in the row.</summary>
        private const int Count = 9;

        /// <summary>How far a turbine may wander off the line, so the row is not a ruler.</summary>
        private const float Wander = 55f;

        [MenuItem("Scaling Laws/Wind farm/Build the turbines")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogWarning("[Wind farm] No city scene.");
                return;
            }

            var city = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "City");
            var existing = city == null ? null : city.transform.Find(GroupName);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var group = new GameObject(GroupName);
            group.transform.SetParent(city == null ? null : city.transform, false);

            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var white = Material("WindTurbineWhite", new Color(0.90f, 0.91f, 0.93f));
            var grey = Material("WindTurbineGrey", new Color(0.62f, 0.64f, 0.68f));

            var random = new System.Random(918);
            var built = 0;

            for (var index = 0; index < Count; index++)
            {
                var t = Count == 1 ? 0.5f : index / (float)(Count - 1);
                var on = Vector2.Lerp(From, To, t);
                var side = new Vector2(To.y - From.y, From.x - To.x).normalized;
                var at = on + side * ((float)random.NextDouble() - 0.5f) * 2f * Wander;

                var ground = Sample(terrains, at.x, at.y);

                if (ground < CityLayout.SeaLevel + 5f)
                {
                    continue;
                }

                // Tall enough to read from the valley, different enough not to look stamped.
                var tower = Mathf.Lerp(52f, 74f, (float)random.NextDouble());
                var blade = tower * 0.42f;

                Raise(group.transform, at, ground, tower, blade, white, grey,
                    Mathf.Lerp(18f, 32f, (float)random.NextDouble()),
                    Mathf.Lerp(-35f, 35f, (float)random.NextDouble()), index);

                built++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Wind farm] {built} turbines on the ridge from " +
                      $"({From.x:0}, {From.y:0}) to ({To.x:0}, {To.y:0}).");
        }

        private static void Raise(Transform parent, Vector2 at, float ground, float towerHeight,
            float bladeLength, Material white, Material grey, float speed, float facing, int index)
        {
            var turbine = new GameObject($"Turbine_{index:00}");
            turbine.transform.SetParent(parent, false);
            turbine.transform.position = new Vector3(at.x, ground, at.y);
            turbine.transform.rotation = Quaternion.Euler(0f, facing, 0f);

            var tower = Piece(turbine.transform, PrimitiveType.Cylinder, white, "Tower");
            tower.localPosition = new Vector3(0f, towerHeight * 0.5f, 0f);
            tower.localScale = new Vector3(2.6f, towerHeight * 0.5f, 2.6f);

            var nacelle = Piece(turbine.transform, PrimitiveType.Cube, grey, "Nacelle");
            nacelle.localPosition = new Vector3(0f, towerHeight, 1.6f);
            nacelle.localScale = new Vector3(3.2f, 3.2f, 8f);

            // The rotor is the thing that turns, so it is its own object with the blades under it.
            var rotor = new GameObject("Rotor");
            rotor.transform.SetParent(turbine.transform, false);
            rotor.transform.localPosition = new Vector3(0f, towerHeight, -2.6f);

            var hub = Piece(rotor.transform, PrimitiveType.Sphere, grey, "Hub");
            hub.localScale = Vector3.one * 3.4f;

            for (var blade = 0; blade < 3; blade++)
            {
                var wing = Piece(rotor.transform, PrimitiveType.Cube, white, $"Blade{blade}");
                var angle = blade * 120f;

                wing.localRotation = Quaternion.Euler(0f, 0f, angle);
                wing.localPosition = wing.localRotation * new Vector3(0f, bladeLength * 0.5f, 0f);
                wing.localScale = new Vector3(1.5f, bladeLength, 0.6f);
            }

            turbine.AddComponent<WindTurbine>().Drive(rotor.transform, speed);
        }

        private static Transform Piece(Transform parent, PrimitiveType shape, Material material, string name)
        {
            var piece = GameObject.CreatePrimitive(shape);
            piece.name = name;
            piece.transform.SetParent(parent, false);

            var collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            return piece.transform;
        }

        /// <summary>
        /// A material kept as an asset rather than made at build time, so the scene does not carry a
        /// material nobody owns and a rerun does not leave the old one behind.
        /// </summary>
        private static Material Material(string name, Color colour)
        {
            var path = $"Assets/_ScalingLaws/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Standard")) { color = colour };
            material.SetFloat("_Glossiness", 0.15f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static float Sample(IEnumerable<Terrain> terrains, float x, float z)
        {
            foreach (var terrain in terrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;

                if (x < origin.x || x > origin.x + size.x || z < origin.z || z > origin.z + size.z)
                {
                    continue;
                }

                return terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
            }

            return -1f;
        }
    }
}
