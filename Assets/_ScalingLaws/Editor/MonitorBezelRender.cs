using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Photographs the office monitor head on, on a transparent ground, for the creator's run monitor.
    ///
    /// **Rendered, not cropped.** The only picture of a monitor in the project was an editor screenshot
    /// with the model tilted, a lamp's glare on the glass and the Unity interface round it. The model
    /// itself is in the JustPlay pack, which is gitignored, so this writes a flat PNG into `Resources/`
    /// and the pack never has to travel: a fresh clone gets the picture and not the source asset, the
    /// same arrangement the premises cards already have with the hub snapshots.
    ///
    /// `Probe` writes four sides to `ScreenProof~/` so which way the model faces is looked at rather
    /// than assumed. `Bake` writes the chosen side. Render without `-nographics`.
    /// </summary>
    public static class MonitorBezelRender
    {
        private const string Prefab = "Assets/JustPlay/Сomputer devices/Prefabs/Monitor.prefab";
        private const string Output = "Assets/_ScalingLaws/Resources/Creator/monitor_bezel.png";

        /// <summary>The side the screen faces, settled by <see cref="Probe"/>.</summary>
        private static readonly Vector3 Front = Vector3.forward;

        [MenuItem("Scaling Laws/Monitor bezel/Probe four sides")]
        public static void Probe()
        {
            Directory.CreateDirectory("ScreenProof~");

            foreach (var (label, direction) in new[]
            {
                ("plusZ", Vector3.forward), ("minusZ", Vector3.back),
                ("plusX", Vector3.right), ("minusX", Vector3.left)
            })
            {
                var png = Shoot(direction, 600);
                if (png != null)
                {
                    File.WriteAllBytes(Path.Combine("ScreenProof~", $"monitor_{label}.png"), png);
                }
            }

            Debug.Log("[Monitor] Probe written.");
        }

        [MenuItem("Scaling Laws/Monitor bezel/Bake")]
        public static void Bake()
        {
            var png = Shoot(Front, 1400);
            if (png == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
            File.WriteAllBytes(Output, png);
            AssetDatabase.ImportAsset(Output);
            Debug.Log($"[Monitor] Wrote {Output}");
        }

        private static byte[] Shoot(Vector3 from, int size)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (source == null)
            {
                Debug.LogError($"[Monitor] Missing {Prefab}. The JustPlay pack is local only.");
                return null;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.9f;
            sun.transform.rotation = Quaternion.LookRotation(-from + Vector3.down * 0.6f + Vector3.right * 0.3f);
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.5f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = bounds.extents.magnitude * 1.02f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.transform.position = bounds.center + from * (bounds.extents.magnitude * 4f);
            camera.transform.LookAt(bounds.center);

            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var shot = new Texture2D(size, size, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            var png = shot.EncodeToPNG();
            Object.DestroyImmediate(shot);
            camera.targetTexture = null;
            target.Release();
            return png;
        }
    }
}
