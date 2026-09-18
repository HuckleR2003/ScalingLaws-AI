using System.Collections.Generic;
using System.Linq;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Makes the houses in the city bigger, the small ones most.
    ///
    /// **Asked for on 2026-09-18**: houses fifteen to twenty-five per cent bigger, because some of
    /// them read as built for a rat next to the streets they stand on. The houses came out of
    /// Kenney's kit at the kit's own scale and were never measured against the roads.
    ///
    /// Each house is scaled about the middle of its base, so it grows outwards and upwards and does
    /// not sink into the ground or float. The smallest houses get the full twenty-five per cent and
    /// the largest fifteen, so the street keeps its variety instead of every house growing by the
    /// same factor. **Run once.** It records what it did on each house (`CityHouseScaled`) and skips
    /// a house it has already scaled, so a second run changes nothing.
    /// </summary>
    public static class CityHouseScale
    {
        public const float SmallestGrowth = 1.25f;
        public const float LargestGrowth = 1.15f;

        [MenuItem("Scaling Laws/City/Report house sizes")]
        public static void Report()
        {
            OpenCity();
            var houses = Houses();
            var areas = houses.Select(Footprint).OrderBy(area => area).ToList();

            if (areas.Count == 0)
            {
                Debug.Log("[Houses] none found");
                return;
            }

            Debug.Log($"[Houses] {areas.Count} houses. Footprint m2: min {areas[0]:0}, "
                + $"p25 {areas[areas.Count / 4]:0}, median {areas[areas.Count / 2]:0}, "
                + $"p75 {areas[areas.Count * 3 / 4]:0}, max {areas[^1]:0}. "
                + $"Already scaled: {houses.Count(house => house.name.EndsWith(Marker))}");
        }

        [MenuItem("Scaling Laws/City/Census of props")]
        public static void Census()
        {
            OpenCity();

            foreach (var group in Object.FindObjectsByType<CityProp>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .GroupBy(prop => prop.Kind))
            {
                Debug.Log($"[Census] {group.Key}: {group.Count()}");
            }

            var names = new Dictionary<string, int>();

            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mesh = filter.sharedMesh == null ? "(none)" : filter.sharedMesh.name;

                if (!mesh.ToLowerInvariant().Contains("house") && !mesh.ToLowerInvariant().Contains("building")
                    && !mesh.ToLowerInvariant().Contains("suburban"))
                {
                    continue;
                }

                names.TryGetValue(mesh, out var count);
                names[mesh] = count + 1;
            }

            foreach (var pair in names.OrderByDescending(pair => pair.Value).Take(40))
            {
                Debug.Log($"[Census] mesh {pair.Key}: {pair.Value}");
            }
        }

        [MenuItem("Scaling Laws/City/Make the houses bigger")]
        public static void Grow()
        {
            var scene = OpenCity();
            var houses = Houses();
            var areas = houses.Select(Footprint).OrderBy(area => area).ToList();

            if (areas.Count == 0)
            {
                Debug.LogError("[Houses] none found, nothing scaled.");
                return;
            }

            var smallest = areas[areas.Count / 10];
            var largest = areas[areas.Count * 9 / 10];
            var grown = 0;

            foreach (var house in houses)
            {
                if (house.name.EndsWith(Marker))
                {
                    continue;
                }

                var bounds = Bounds(house);
                var t = Mathf.InverseLerp(smallest, largest, Footprint(house));
                var factor = Mathf.Lerp(SmallestGrowth, LargestGrowth, t);

                // About the middle of the base: the point that must not move.
                var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                house.localScale *= factor;
                var after = Bounds(house);
                var drift = pivot - new Vector3(after.center.x, after.min.y, after.center.z);
                house.position += drift;

                house.name += Marker;
                grown++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Houses] {grown} houses grown by {LargestGrowth:0.00} to {SmallestGrowth:0.00}.");
        }

        private const string Marker = " [grown]";

        private static UnityEngine.SceneManagement.Scene OpenCity() =>
            EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity", OpenSceneMode.Single);

        /// <summary>
        /// Every house on the map: each placed model from Kenney's suburban kit (`building-type-*`),
        /// taken at its outermost prefab root so the whole house scales as one, plus the few props
        /// the builders tagged <see cref="CityPropKind.House"/> themselves.
        /// </summary>
        private static List<Transform> Houses()
        {
            var found = new HashSet<Transform>();

            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mesh = filter.sharedMesh == null ? string.Empty : filter.sharedMesh.name;

                if (!mesh.StartsWith("building-type-"))
                {
                    continue;
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(filter.gameObject);
                found.Add(root != null ? root.transform : filter.transform);
            }

            foreach (var prop in Object.FindObjectsByType<CityProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop.Kind == CityPropKind.House)
                {
                    found.Add(prop.transform);
                }
            }

            // A house inside another house would be scaled twice.
            return found.Where(house => !found.Any(other => other != house && house.IsChildOf(other))).ToList();
        }

        /// <summary>A close shot of the founder's street, where the author saw the houses too small.</summary>
        [MenuItem("Scaling Laws/City/Photograph the founder's street")]
        public static void ShootStreet() => ShootStreet("houses_street.png");

        public static void ShootStreet(string file)
        {
            OpenCity();
            var map = Object.FindFirstObjectByType<CityMapController>();
            var forward = map != null ? map.transform.forward : Quaternion.Euler(36f, 0f, 0f) * Vector3.forward;
            var home = Data.CityLayout.FounderHome;
            var from = CityMapController.OpeningFrom(home, Data.CityLayout.GroundHeightAt(home), forward);

            var go = new GameObject("StreetShot");
            var camera = go.AddComponent<Camera>();
            go.transform.position = from + Vector3.up * 60f - forward * 40f;
            go.transform.rotation = Quaternion.LookRotation(forward);
            camera.fieldOfView = map != null ? map.GetComponent<Camera>().fieldOfView : 40f;
            camera.farClipPlane = 6000f;
            camera.clearFlags = CameraClearFlags.Skybox;

            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            RenderTexture.active = null;

            System.IO.Directory.CreateDirectory("CitySnapshot~");
            System.IO.File.WriteAllBytes("CitySnapshot~/" + file, image.EncodeToPNG());
            Object.DestroyImmediate(go);
            Debug.Log("[Houses] wrote CitySnapshot~/" + file);
        }

        /// <summary>Before and after in one run, for the batch command line.</summary>
        public static void GrowWithPhotographs()
        {
            ShootStreet("houses_before.png");
            Grow();
            ShootStreet("houses_after.png");
        }

        private static float Footprint(Transform house)
        {
            var bounds = Bounds(house);
            return bounds.size.x * bounds.size.z;
        }

        private static Bounds Bounds(Transform house)
        {
            var renderers = house.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(house.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;

            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }
}
