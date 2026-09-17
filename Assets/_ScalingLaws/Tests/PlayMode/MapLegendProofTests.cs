using System.Collections;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Renders <see cref="MapLegendPanel"/> to a PNG, the same rig <see cref="ScreenProofTests"/>
    /// uses for every other screen: build a panel against a render texture, wait out layout and the
    /// entry transition, read it back.
    ///
    /// A panel this small and this new is exactly where a picture earns its keep over an assertion
    /// — a swatch with no colour set reads as a blank grey square to every test here and is
    /// unmistakable in a render.
    /// </summary>
    public sealed class MapLegendProofTests
    {
        private const int Width = 480;
        private const int Height = 480;

        private static string ProofFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "ScreenProof~");

        private static IEnumerator Capture(VisualElement page, string fileName)
        {
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme")
                ?? Resources.Load<ThemeStyleSheet>("unity default runtime theme");

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.043f, 0.055f, 0.078f, 1f);

            var host = new GameObject("MapLegendProof");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;

            var root = document.rootVisualElement;
            UiBootstrap.Prepare(root, null);
            root.style.flexGrow = 1;

            page.style.position = Position.Absolute;
            page.style.top = 16;
            page.style.right = 16;
            root.Add(page);

            for (var pass = 0; pass < 6; pass++)
            {
                yield return null;
            }

            var readable = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(ProofFolder);
            var path = Path.Combine(ProofFolder, fileName);
            File.WriteAllBytes(path, readable.EncodeToPNG());

            Assert.That(Spread(readable), Is.GreaterThan(0.02f),
                $"{fileName} came back flat. Nothing reached this panel.");

            Debug.Log($"[Scaling Laws] wrote {path}");

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private static float Spread(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            var lowest = 255;
            var highest = 0;

            for (var index = 0; index < pixels.Length; index += 37)
            {
                var value = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                lowest = Mathf.Min(lowest, value);
                highest = Mathf.Max(highest, value);
            }

            return (highest - lowest) / 255f;
        }

        [UnityTest]
        public IEnumerator TheLegendDrawsWithNothingPicked()
        {
            var state = new MapFilterState();
            yield return Capture(new MapLegendPanel(state), "map_legend.png");
        }

        /// <summary>One category picked, so the on/dim states are both in the same frame.</summary>
        [UnityTest]
        public IEnumerator TheLegendDrawsWithACategoryPicked()
        {
            var state = new MapFilterState();
            state.Toggle(MapCategory.Energy);

            yield return Capture(new MapLegendPanel(state), "map_legend_picked.png");
        }
    }
}
