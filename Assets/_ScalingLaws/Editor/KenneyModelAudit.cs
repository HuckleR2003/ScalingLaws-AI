using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Measures Kenney models: bounds, centre offset, and which horizontal axis is the long one.
    ///
    /// **Written because guessing a model's axis cost this project a whole night.** `road-straight`
    /// turned out to run along its local X while the placement code assumed Z, and nothing short of
    /// looking at it settled the question. Everything placed from here on gets measured first.
    /// </summary>
    public static class KenneyModelAudit
    {
        private const string Roads = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";
        private const string Suburban = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";
        private const string Commercial = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        [MenuItem("Scaling Laws/Audit Kenney model sizes")]
        public static void Run()
        {
            foreach (var name in new[]
            {
                "road-straight", "road-crossing", "road-bridge", "bridge-pillar", "bridge-pillar-wide",
                "traffic-light", "traffic-light-object-vertical", "road-sign-stop", "road-sign-street",
                "road-driveway-single", "road-driveway-double", "road-side"
            })
            {
                Measure(Roads + name + ".fbx");
            }

            foreach (var name in new[]
            {
                "driveway-long", "driveway-short", "path-long", "path-short",
                "fence", "fence-low", "planter", "tree-large"
            })
            {
                Measure(Suburban + name + ".fbx");
            }

            foreach (var letter in "abcdefghijklmnopqrstu")
            {
                Measure(Suburban + "building-type-" + letter + ".fbx");
            }

            foreach (var letter in "abcdefghijklmn")
            {
                Measure(Commercial + "building-" + letter + ".fbx");
            }

            foreach (var letter in "abcde")
            {
                Measure(Commercial + "building-skyscraper-" + letter + ".fbx");
            }
        }

        private static void Measure(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogWarning($"[Model] MISSING {path}");
                return;
            }

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[Model] {model.name}: no renderer");
                return;
            }

            // Local bounds, combined. The asset is unrotated and unscaled on disk, so these are the
            // model's own dimensions in its own axes — which is the whole point of measuring.
            var bounds = renderers[0].localBounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].localBounds);
            }

            var size = bounds.size;
            var longAxis = size.x >= size.z ? "X" : "Z";

            Debug.Log($"[Model] {model.name,-32} size ({size.x:0.##} x {size.y:0.##} x {size.z:0.##})  "
                + $"centre ({bounds.center.x:0.##}, {bounds.center.y:0.##}, {bounds.center.z:0.##})  "
                + $"long axis: {longAxis}");
        }
    }
}
