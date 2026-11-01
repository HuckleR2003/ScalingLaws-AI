using System.Collections;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Whether the fold chevron is anywhere a player could see it.
    ///
    /// **Why this is in PlayMode and not next to GuideFoldTests.** The EditMode fixture builds the
    /// strip outside a panel, so every `worldBound` on it is zero and every element is equally
    /// nowhere. Those tests proved the button exists, is a child of the strip, carries its class
    /// and answers a click. All of that was true, and all of it was true on 2026-10-26 when the
    /// build went out and the tester replied "I dont see the chevron thing for emil".
    ///
    /// A chain of green links is not a green chain. The link the EditMode tests never touched is
    /// the one that decides where the thing lands on a screen, and that link needs a real panel
    /// with a real width.
    ///
    /// **The widths are not decoration.** `.guide__speaker` carries a flat `margin-left: -420px`
    /// with no narrow-window variant, and the stylesheet's own comment beside it concedes the
    /// strip misbehaves on a 900px window, citing the same tester's report of 2026-10-17 that the
    /// display "is not adapting very well". 1920 is the machine this was built on. The rest are
    /// machines it was not.
    /// </summary>
    public sealed class GuideFoldReachTests
    {
        private const int Height = 1080;

        private sealed class Rig
        {
            private readonly GameObject host;
            private readonly RenderTexture texture;

            public Rig(int width)
            {
                Width = width;

                texture = new RenderTexture(width, Height, 24, RenderTextureFormat.ARGB32);
                texture.Create();

                var settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.themeStyleSheet =
                    Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme")
                    ?? Resources.Load<ThemeStyleSheet>("unity default runtime theme");
                settings.targetTexture = texture;
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.referenceResolution = new Vector2Int(width, Height);
                settings.clearColor = true;

                host = new GameObject("GuideFoldReach");
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = settings;

                Root = document.rootVisualElement;
                UiBootstrap.Prepare(Root, null);
                Root.style.flexGrow = 1;

                Stage = new VisualElement();
                Stage.style.flexGrow = 1;
                Root.Add(Stage);

                State = new GuideProgress { Stage = GuideStage.Touring, Step = 0 };
                Overlay = new GuideOverlay(Stage, () => State, _ => { }, () => { });
            }

            public int Width { get; }
            public RenderTexture Texture => texture;
            public VisualElement Root { get; }
            public VisualElement Stage { get; }
            public GuideProgress State { get; }
            public GuideOverlay Overlay { get; }

            public Button Fold() =>
                Stage.Query<Button>(className: "guide__fold").ToList().FirstOrDefault();

            public VisualElement Portrait() =>
                Stage.Query<VisualElement>(className: "guide__portrait").ToList().FirstOrDefault();

            public void Dispose()
            {
                Object.DestroyImmediate(host);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Lets the panel lay itself out. One frame builds the tree, the second settles the
        /// geometry the first one requested, and reading `worldBound` before both have passed
        /// reports zeroes that look exactly like the bug being tested for.
        /// </summary>
        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheChevronIsOnScreenAtEveryWidthAPlayerMightHave(
            [Values(1920, 1600, 1440, 1366, 1280)] int width)
        {
            var rig = new Rig(width);
            try
            {
                rig.Overlay.Refresh();
                yield return Settle();

                var fold = rig.Fold();
                Assert.That(fold, Is.Not.Null, "The fold button was never built.");

                var box = fold.worldBound;

                Assert.That(box.width, Is.GreaterThan(0f),
                    $"At {width}px the fold button has no width, so nothing is drawn.");

                Assert.That(box.xMin, Is.GreaterThanOrEqualTo(0f),
                    $"At {width}px the fold button starts at x={box.xMin:0.#}, which is off the "
                    + "left edge of the window. A player cannot click what is not on the screen.");

                Assert.That(box.xMax, Is.LessThanOrEqualTo(width),
                    $"At {width}px the fold button ends at x={box.xMax:0.#}, past the right edge.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        /// <summary>
        /// Writes what the strip actually looks like to `GuideFoldProof~/`, because the two tests
        /// above can only say the button occupies a rectangle on the screen. They passed on the
        /// build the tester could not find the chevron in, so "it is on the screen" and "a person
        /// can see it" have now been measured to be different claims in this exact case.
        ///
        /// Fails only on the one thing a picture cannot be trusted about: a frame nobody drew into
        /// is a flat rectangle of the clear colour and looks identical to a dark screen that
        /// worked. Everything else about these frames is for a human to look at.
        /// </summary>
        [UnityTest]
        public IEnumerator WriteTheStripOutSoSomebodyCanLookAtIt(
            [Values(1920, 1366)] int width)
        {
            var rig = new Rig(width);
            try
            {
                rig.Overlay.Refresh();
                // **Not `WaitForEndOfFrame`.** Batchmode never evokes it and the yield throws, which
                // is how this fixture failed on its first run while still writing usable frames.
                yield return Settle();
                yield return null;

                var shot = new Texture2D(width, Height, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                RenderTexture.active = rig.Texture;
                shot.ReadPixels(new Rect(0, 0, width, Height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;

                var folder = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "GuideFoldProof~");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(folder, $"guide_{width}.png"), shot.EncodeToPNG());

                var pixels = shot.GetPixels32();
                var first = pixels[0];
                var moved = false;
                foreach (var pixel in pixels)
                {
                    if (pixel.r != first.r || pixel.g != first.g || pixel.b != first.b)
                    {
                        moved = true;
                        break;
                    }
                }

                Object.DestroyImmediate(shot);

                Assert.That(moved, Is.True,
                    $"The {width}px frame is one flat colour, so nothing was drawn into it and "
                    + "the picture proves nothing either way.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        /// <summary>
        /// The portrait travels with the chevron: both sit inside `.guide__speaker` and share its
        /// margin. If the chevron has gone off the edge then Emil's face went with it, and that is
        /// worth failing separately, because a missing face reads as a style choice and a missing
        /// button reads as nothing at all.
        /// </summary>
        [UnityTest]
        public IEnumerator HisFaceIsOnScreenAtEveryWidthToo(
            [Values(1920, 1600, 1440, 1366, 1280)] int width)
        {
            var rig = new Rig(width);
            try
            {
                rig.Overlay.Refresh();
                yield return Settle();

                var portrait = rig.Portrait();
                Assert.That(portrait, Is.Not.Null, "The portrait was never built.");

                Assert.That(portrait.worldBound.xMin, Is.GreaterThanOrEqualTo(0f),
                    $"At {width}px Emil's portrait starts at x={portrait.worldBound.xMin:0.#}, "
                    + "off the left edge of the window.");
            }
            finally
            {
                rig.Dispose();
            }
        }
    }
}
