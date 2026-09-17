using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Photographs Kenney models from straight above, one per file, with a red bar on +X and a blue
    /// bar on +Z — so which way a model faces is something looked at, not assumed.
    ///
    /// <see cref="KenneyModelAudit"/> gives a model's size and its long axis, which is not enough for
    /// anything asymmetric: a T-junction's closed side, the rounded cap on a road end, the side of a
    /// traffic light its lamps are on. Those decide a rotation, and a rotation decided from a
    /// bounding box is how `road-straight` spent a night laid ninety degrees wrong.
    ///
    /// Written to `CityProof~/model_&lt;name&gt;.png`. Render without `-nographics`.
    /// </summary>
    public static class KenneyModelPreview
    {
        private const string Roads = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";

        [MenuItem("Scaling Laws/Preview road models from above")]
        public static void RoadPieces()
        {
            Shoot(Roads, "road-intersection", "road-crossroad", "road-end-round", "road-end",
                "road-bend", "road-straight", "road-crossing", "traffic-light", "road-bridge",
                "road-side-entry", "road-sign-stop");
        }

        /// <summary>
        /// A traffic light from all four sides, because which way its lamps face cannot be read from
        /// above — the head is a box either way.
        /// </summary>
        [MenuItem("Scaling Laws/Preview traffic light from four sides")]
        public static void TrafficLightSides()
        {
            Directory.CreateDirectory("CityProof~");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35f, 20f, 0f);
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.62f);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Roads + "traffic-light.fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.position = Vector3.zero;

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
            camera.orthographic = true;
            camera.orthographicSize = 0.32f;
            camera.nearClipPlane = 0.01f;

            var target = new RenderTexture(500, 500, 24) { antiAliasing = 4 };
            camera.targetTexture = target;

            // Named after where the camera stands, so "from_plusZ" shows the side that faces +Z.
            foreach (var (label, direction) in new[]
            {
                ("from_plusZ", Vector3.forward), ("from_minusZ", Vector3.back),
                ("from_plusX", Vector3.right), ("from_minusX", Vector3.left)
            })
            {
                camera.transform.position = new Vector3(0f, 0.3f, 0f) + direction * 3f;
                camera.transform.LookAt(new Vector3(0f, 0.3f, 0f));
                camera.Render();

                RenderTexture.active = target;
                var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine("CityProof~", $"model_traffic-light_{label}.png"),
                    shot.EncodeToPNG());
                Object.DestroyImmediate(shot);
            }

            Debug.Log("[Preview] Wrote four traffic light sides.");
        }

        private static void Shoot(string folder, params string[] names)
        {
            Directory.CreateDirectory("CityProof~");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(60f, -25f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);

            Marker(new Vector3(0.78f, 0.02f, 0f), new Vector3(0.3f, 0.02f, 0.05f), Color.red);
            Marker(new Vector3(0f, 0.02f, 0.78f), new Vector3(0.05f, 0.02f, 0.3f), Color.blue);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.01f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.transform.position = new Vector3(0f, 5f, 0f);

            var target = new RenderTexture(700, 700, 24) { antiAliasing = 4 };
            camera.targetTexture = target;

            foreach (var name in names)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".fbx");
                if (model == null)
                {
                    Debug.LogWarning($"[Preview] missing {name}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                camera.Render();

                RenderTexture.active = target;
                var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine("CityProof~", $"model_{name}.png"), shot.EncodeToPNG());
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(instance);

                Debug.Log($"[Preview] Wrote model_{name}.png");
            }
        }

        private static void Marker(Vector3 at, Vector3 size, Color colour)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.position = at;
            bar.transform.localScale = size;
            bar.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Unlit/Color")) { color = colour };
        }
    }
}
