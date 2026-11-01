using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// What the custom company window actually looks like, written to `CustomCompanyProof~/`.
    ///
    /// **Built because of the chevron, four days earlier.** That control was on screen at five
    /// window widths, every geometry test passed, and the tester who had asked for it could not
    /// find it. "Drawn" and "visible" turned out to be two claims and only a picture separated
    /// them. This window is two hundred and fifty lines of interface nobody has looked at, so it
    /// gets looked at before it goes in a build.
    ///
    /// The assertions here are deliberately few: a frame nobody drew into is a flat rectangle of
    /// the clear colour and reads exactly like a dark screen that worked, and nothing reaching
    /// past the right edge is the one layout fault a render can be trusted on. Everything else
    /// about these frames is for a person to look at.
    /// </summary>
    public sealed class CustomCompanyProofTests
    {
        private const int Width = 1600;
        private const int Height = 900;

        private sealed class Rig
        {
            private readonly GameObject host;

            public Rig(int stage)
            {
                Texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                Texture.Create();

                var settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.themeStyleSheet =
                    Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme")
                    ?? Resources.Load<ThemeStyleSheet>("unity default runtime theme");
                settings.targetTexture = Texture;
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.referenceResolution = new Vector2Int(Width, Height);
                settings.clearColor = true;
                settings.colorClearValue = new Color(0.043f, 0.055f, 0.078f, 1f);

                host = new GameObject("CustomCompanyProof");
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = settings;

                Controller = host.AddComponent<MainMenuController>();

                // The menu builds itself in OnEnable, which AddComponent has already run. Put it
                // on the company page with a company of the player's own chosen, then rebuild.
                Set("chosenArchetype", CompanyArchetype.Custom);
                Set("CompanyIsChosen", true);
                Set("companyName", "Prometheus AI");
                Set("customStage", stage);

                var show = typeof(MainMenuController).GetMethod(
                    "Show", BindingFlags.Instance | BindingFlags.NonPublic);
                var stageType = typeof(MainMenuController)
                    .GetNestedType("Stage", BindingFlags.NonPublic);

                show?.Invoke(Controller, new[] { System.Enum.Parse(stageType!, "Company") });
            }

            public RenderTexture Texture { get; }
            public MainMenuController Controller { get; }

            public VisualElement Root =>
                host.GetComponent<UIDocument>().rootVisualElement;

            private void Set(string name, object value)
            {
                const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic
                                                               | BindingFlags.Public;

                var field = typeof(MainMenuController).GetField(name, any);
                if (field != null)
                {
                    field.SetValue(Controller, value);
                    return;
                }

                var property = typeof(MainMenuController).GetProperty(name, any);
                Assert.That(property, Is.Not.Null, $"The menu has no {name} to set.");
                property!.SetValue(Controller, value);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(host);
                Texture.Release();
                Object.DestroyImmediate(Texture);
            }
        }

        [UnityTest]
        public IEnumerator EveryPageOfTheWindowDrawsSomething(
            [Values(0, 1, 2, 3)] int stage)
        {
            var rig = new Rig(stage);
            try
            {
                yield return null;
                yield return null;

                var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                RenderTexture.active = rig.Texture;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;

                var folder = Path.Combine(Directory.GetCurrentDirectory(), "CustomCompanyProof~");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, $"custom_{stage}.png"), shot.EncodeToPNG());

                var pixels = shot.GetPixels32();
                var first = pixels[0];
                var moved = pixels.Any(p => p.r != first.r || p.g != first.g || p.b != first.b);

                Object.DestroyImmediate(shot);

                Assert.That(moved, Is.True,
                    $"Page {stage} is one flat colour, so nothing was drawn and the picture "
                    + "proves nothing either way.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        /// <summary>
        /// The one layout fault a render can be trusted on. The creator pages are held to fitting
        /// one screen with no scroller, and this window stands inside that budget.
        /// </summary>
        [UnityTest]
        public IEnumerator NothingInTheWindowReachesPastTheEdge(
            [Values(0, 1, 2, 3)] int stage)
        {
            var rig = new Rig(stage);
            try
            {
                yield return null;
                yield return null;

                foreach (var element in rig.Root.Query<VisualElement>().ToList())
                {
                    var box = element.worldBound;
                    if (box.width <= 0f)
                    {
                        continue;
                    }

                    Assert.That(box.xMax, Is.LessThanOrEqualTo(Width + 1f),
                        $"Page {stage}: a {element.GetType().Name} ends at "
                        + $"x={box.xMax:0} against a {Width}px window.");
                }
            }
            finally
            {
                rig.Dispose();
            }
        }
    }
}
