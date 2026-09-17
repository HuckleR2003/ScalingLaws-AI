using System.Collections.Generic;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The things that stand beside a road rather than under it: garages on the suburban plots, and
    /// traffic lights on the junctions.
    /// </summary>
    public static class CityPropsStage
    {
        private const string Roads = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";
        private const string Suburban = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";

        /// <summary>
        /// A real traffic light stands about seven metres to the top of the lamp; the model is 0.52
        /// tall on Kenney's own abstract grid, so this is the factor between the two. Measured, not
        /// guessed — see <see cref="KenneyModelAudit"/>.
        /// </summary>
        private const float TrafficLightScale = 13f;

        [MenuItem("Scaling Laws/Props stage (garages, traffic lights)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Props] No City.unity.");
                return;
            }

            SwapGarages();
            PlaceTrafficLights();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Props] Props stage complete.");
        }

        /// <summary>
        /// **No pack here ships a garage**, so the smallest, lowest house in the Suburban kit stands
        /// in for one. At the surveyed garage footprint (5.6m wide) `building-type-h` comes out
        /// 3.2m to the ridge — which is exactly the height the placeholder box was, so the swap is
        /// to scale rather than a guess dressed up as one. It reads as a single-storey outbuilding
        /// at the end of a driveway, which is what a garage is.
        /// </summary>
        private static void SwapGarages()
        {
            var garage = AssetDatabase.LoadAssetAtPath<GameObject>(Suburban + "building-type-h.fbx");

            if (garage == null)
            {
                Debug.LogError("[Props] No garage stand-in model.");
                return;
            }

            CityAssetSwapper.RunSwap(
                new Dictionary<CityPropKind, GameObject[]> { [CityPropKind.Garage] = new[] { garage } },
                matchFootprint: true, keepTheBox: false);
        }

        /// <summary>
        /// Four lights on every junction, one per corner, each turned to face the traffic arriving
        /// at it.
        ///
        /// **Found from the junctions already in the scene, not recomputed.** The crossroad tiles
        /// were placed where two roads genuinely cross, and each one is scaled to the wider of the
        /// two roads — so the tile itself already knows where the junction is and how big it is, and
        /// asking it is one lookup against a number that cannot drift out of step.
        /// </summary>
        private static void PlaceTrafficLights()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Roads + "traffic-light.fbx");

            if (model == null)
            {
                Debug.LogError("[Props] No traffic-light model.");
                return;
            }

            var crossings = GameObject.Find("RoadCrossings");
            if (crossings == null)
            {
                Debug.LogWarning("[Props] No RoadCrossings group; run Tile intersections first.");
                return;
            }

            var existing = GameObject.Find("TrafficLights");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var cityRoot = GameObject.Find("City");
            var root = new GameObject("TrafficLights").transform;
            if (cityRoot != null)
            {
                root.SetParent(cityRoot.transform, false);
            }

            var placed = 0;

            foreach (Transform junction in crossings.transform)
            {
                // The crossroad tile is a unit square scaled to the junction's width, so its own
                // scale is the junction's width in metres.
                var half = junction.localScale.x * 0.5f;
                var stand = half + 3f;

                for (var corner = 0; corner < 4; corner++)
                {
                    var turn = 90f * corner;
                    var away = Quaternion.Euler(0f, turn, 0f) * new Vector3(1f, 0f, 1f).normalized;
                    var at = junction.position + away * (stand * 1.05f);

                    var height = CityTerrainBuilder.HeightAt(at.x, at.z);

                    var light = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                    light.transform.position = new Vector3(at.x, height, at.z);

                    // Turned back towards the middle of the junction, which is where the traffic it
                    // is stopping is coming from.
                    light.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
                    light.transform.localScale = Vector3.one * TrafficLightScale;

                    placed++;
                }
            }

            Debug.Log($"[Props] {placed} traffic lights on {crossings.transform.childCount} junctions.");
        }
    }
}
