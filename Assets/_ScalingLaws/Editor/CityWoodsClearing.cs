using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Clears the planted woods off every district grid's ground and every subdivision's plots.
    ///
    /// <see cref="CityAtmosphere"/> planted woods wherever the land between districts was empty, and
    /// Midtown and River Works were grown later onto exactly that land. A block is not a forest:
    /// until the trees go, the infill pass counts every one of them as occupied ground and builds
    /// nothing, and the streets run through a wood. Only the planted woods are touched — park trees
    /// and garden trees belong where they are.
    /// </summary>
    public static class CityWoodsClearing
    {
        /// <summary>Beyond a grid's outer streets: the frontage on the far side of them is built on too.</summary>
        private const float Margin = 30f;

        [MenuItem("Scaling Laws/Clear the woods off district blocks")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityTerrainBuilder.ScenePath, OpenSceneMode.Single);
            var woods = GameObject.Find("City")?.transform.Find("Woods");

            if (!scene.IsValid() || woods == null)
            {
                Debug.LogError("[Woods] No City.unity with a Woods group.");
                return;
            }

            var doomed = new List<GameObject>();

            foreach (Transform tree in woods)
            {
                var at = new Vector2(tree.position.x, tree.position.z);

                foreach (var grid in CityBlocks.Grids)
                {
                    var angle = grid.RotationDegrees * Mathf.Deg2Rad;
                    var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var across = new Vector2(-along.y, along.x);
                    var offset = at - new Vector2(grid.CentreX, grid.CentreZ);

                    if (Mathf.Abs(Vector2.Dot(offset, across)) <= grid.Width * 0.5f + Margin
                        && Mathf.Abs(Vector2.Dot(offset, along)) <= grid.Depth * 0.5f + Margin)
                    {
                        doomed.Add(tree.gameObject);
                        break;
                    }
                }

                // And off every subdivision's plots: a suburb grown onto a wood has to clear it too.
                foreach (var block in CityBlocks.Residential)
                {
                    if (doomed.Count > 0 && doomed[^1] == tree.gameObject)
                    {
                        break;
                    }

                    var angle = block.RotationDegrees * Mathf.Deg2Rad;
                    var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var across = new Vector2(-along.y, along.x);
                    var offset = at - new Vector2(block.CentreX, block.CentreZ);

                    if (Mathf.Abs(Vector2.Dot(offset, across)) <= block.Width * 0.5f + Margin
                        && Mathf.Abs(Vector2.Dot(offset, along)) <= block.Depth * 0.5f + Margin)
                    {
                        doomed.Add(tree.gameObject);
                        break;
                    }
                }
            }

            foreach (var tree in doomed)
            {
                Object.DestroyImmediate(tree);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Woods] {doomed.Count} trees cleared off district blocks; {woods.childCount} left standing.");
        }
    }
}
