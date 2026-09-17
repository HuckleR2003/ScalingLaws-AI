using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Builds the shopping gallery out of several low, wide buildings instead of leaving it as one
    /// grey slab.
    ///
    /// **No model in these packs is mall-shaped, and that is the whole problem.** The gallery's
    /// surveyed footprint is 190 × 120 metres and 26 tall — an aspect of about one to seven — while
    /// every commercial model Kenney ships is roughly as tall as it is wide, because they are city
    /// blocks. Scaling one of them to 190 metres across would stand it two hundred metres into the
    /// air. A real low-poly mall is assembled from wings, so this assembles wings: a grid of the
    /// flattest models in the pack, each at its own honest proportions, filling the footprint the
    /// gallery already occupies.
    /// </summary>
    public static class CityMallStage
    {
        private const string Commercial =
            "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitCommercial/Models/FBX format/";

        /// <summary>
        /// Frontage of one wing. At this width the flattest models come out around 24 metres tall,
        /// which is what the gallery's own placeholder was — so the complex keeps the height the
        /// surveyor intended rather than one that happens to fall out of the scaling.
        /// </summary>
        private const float WingWidth = 45f;

        /// <summary>Service gap between wings, so the complex reads as a building rather than a wall.</summary>
        private const float Gap = 6f;

        [MenuItem("Scaling Laws/Build the gallery out of wings")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Mall] No City.unity.");
                return;
            }

            var models = new List<GameObject>();
            foreach (var name in new[] { "building-e", "building-c", "building-k" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Commercial + name + ".fbx");

                if (model != null)
                {
                    models.Add(model);
                }
            }

            if (models.Count == 0)
            {
                Debug.LogError("[Mall] No wing models loaded.");
                return;
            }

            var existing = GameObject.Find("GalleryWings");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var cityRoot = GameObject.Find("City");
            var root = new GameObject("GalleryWings").transform;
            if (cityRoot != null)
            {
                root.SetParent(cityRoot.transform, false);
            }

            var random = new System.Random(20260916);
            var built = 0;
            var replaced = 0;

            foreach (var prop in Object.FindObjectsByType<CityProp>(FindObjectsSortMode.None))
            {
                if (prop.Kind != CityPropKind.Mall)
                {
                    continue;
                }

                built += BuildComplex(prop, models, root, random);
                Object.DestroyImmediate(prop.gameObject);
                replaced++;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mall] {replaced} gallery replaced by {built} wings.");
        }

        private static int BuildComplex(CityProp prop, IReadOnlyList<GameObject> models,
            Transform root, System.Random random)
        {
            var box = prop.transform;
            var footprint = prop.Footprint;

            var groundY = box.position.y - footprint.y * 0.5f;

            var columns = Mathf.Max(1, Mathf.FloorToInt(footprint.x / (WingWidth + Gap)));
            var step = footprint.x / columns;

            // How deep one wing comes out once it is scaled to its frontage, measured from the first
            // model rather than assumed — they differ, and a row spaced on the wrong number either
            // overlaps itself or leaves a street through the middle of a shop.
            if (!TryMeasure(models[0], out var reference) || reference.x <= 0.001f)
            {
                return 0;
            }

            var wingDepth = reference.z * (WingWidth / reference.x);
            var rows = Mathf.Max(1, Mathf.FloorToInt(footprint.z / (wingDepth + Gap)));
            var rowStep = footprint.z / rows;

            var built = 0;

            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    var local = new Vector3(
                        -footprint.x * 0.5f + step * (column + 0.5f),
                        0f,
                        -footprint.z * 0.5f + rowStep * (row + 0.5f));

                    var at = box.position + box.rotation * local;

                    if (CityTerrainBuilder.HeightAt(at.x, at.z) < CityLayout.SeaLevel + 1f)
                    {
                        continue;
                    }

                    var model = models[random.Next(models.Count)];

                    if (!TryMeasure(model, out var size) || size.x <= 0.001f)
                    {
                        continue;
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                    instance.transform.rotation = box.rotation;
                    instance.transform.localScale = Vector3.one * (WingWidth / size.x);
                    instance.transform.position = new Vector3(at.x, groundY, at.z);

                    // Settled onto the gallery's own surveyed base by its measured underside, the
                    // same way every other real model in this scene is placed.
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds;
                        for (var index = 1; index < renderers.Length; index++)
                        {
                            bounds.Encapsulate(renderers[index].bounds);
                        }

                        instance.transform.position += Vector3.up * (groundY - bounds.min.y);
                    }

                    built++;
                }
            }

            return built;
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
