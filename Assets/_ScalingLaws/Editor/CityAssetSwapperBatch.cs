using System.Collections.Generic;
using System.Linq;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Runs <see cref="CityAssetSwapper.RunSwap"/> from the command line, with a curated mapping,
    /// instead of a human dragging prefabs into the window.
    ///
    /// **Stage one only: single objects, never a tiled strip.** Houses, trees and lamps are one
    /// object on one surveyed spot each, which is exactly what the swapper already does well. Roads
    /// are not here on purpose — a road segment is a long box meant to be covered by several 2m
    /// road tiles in a row, and stretching one tile to fill it is a different, worse problem the
    /// swapper was never built to solve. That is its own stage.
    ///
    /// Assets: Kenney City Kit (Suburban), City Kit (Roads), City Kit (Commercial). CC0.
    /// </summary>
    public static class CityAssetSwapperBatch
    {
        private const string Suburban = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";
        private const string Roads = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";
        private const string Commercial = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        [MenuItem("Scaling Laws/Swap city assets (batch, stage 1)")]
        public static void RunStageOne()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Scaling Laws] No City.unity to swap assets in.");
                return;
            }

            var byKind = new Dictionary<CityPropKind, GameObject[]>
            {
                [CityPropKind.House] = Load(Suburban,
                    "building-type-a", "building-type-b", "building-type-c",
                    "building-type-d", "building-type-e", "building-type-f"),

                [CityPropKind.Villa] = Load(Suburban,
                    "building-type-h", "building-type-i", "building-type-j"),

                [CityPropKind.FounderHome] = Load(Suburban, "building-type-u"),

                [CityPropKind.Tree] = Load(Suburban, "tree-large", "tree-small"),

                [CityPropKind.StreetLamp] = Load(Roads, "light-square", "light-curved"),

                [CityPropKind.Block] = Load(Commercial,
                    "building-a", "building-b", "building-c", "building-d",
                    "building-e", "building-f", "building-g"),

                [CityPropKind.Tower] = Load(Commercial,
                    "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
                    "building-skyscraper-d", "building-skyscraper-e")
            };

            // Nothing to load for these yet, and it is worth saying which rather than silently
            // leaving them grey: Suburban has no garage model, and Roads/Sidewalk/Driveway/
            // BridgeDeck/BridgePier all need the tiling stage, not this one.
            foreach (var missing in new[]
            {
                CityPropKind.Garage, CityPropKind.RoadSegment, CityPropKind.Sidewalk,
                CityPropKind.Driveway, CityPropKind.BridgeDeck, CityPropKind.BridgePier,
                CityPropKind.Mall, CityPropKind.ParkingRow
            })
            {
                Debug.Log($"[Scaling Laws] {missing}: left as the grey box, stage 1 has nothing for it.");
            }

            foreach (var pair in byKind)
            {
                var missing = pair.Value.Count(p => p == null);

                if (missing > 0)
                {
                    Debug.LogWarning($"[Scaling Laws] {pair.Key}: {missing} of {pair.Value.Length} "
                        + "listed prefabs did not load — check the file names.");
                }
            }

            CityAssetSwapper.RunSwap(byKind, matchFootprint: true, keepTheBox: false);

            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject[] Load(string folder, params string[] names)
        {
            var loaded = new GameObject[names.Length];

            for (var index = 0; index < names.Length; index++)
            {
                loaded[index] = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{folder}{names[index]}.fbx");
            }

            return loaded;
        }
    }
}
