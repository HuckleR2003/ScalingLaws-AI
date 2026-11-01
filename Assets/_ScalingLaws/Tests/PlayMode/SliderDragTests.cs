using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Why no slider in this game can be dragged, only clicked.
    ///
    /// Reported by the author on 2026-10-31 about every slider in the game rather than about one
    /// screen, so the two candidate causes are measured apart instead of argued about: a
    /// stylesheet that leaves the handle unhittable, and a value-changed callback that rebuilds
    /// the page and takes the handle out of the tree between the press and the drag.
    ///
    /// The second is the tutorial's NEXT button again, where a click landed on an element that had
    /// already left the panel. That one cost a playtest.
    /// </summary>
    public sealed class SliderDragTests
    {
        private const int Width = 1280;
        private const int Height = 720;

        private static PanelSettings MakeSettings(RenderTexture texture)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme")
                ?? Resources.Load<ThemeStyleSheet>("unity default runtime theme");
            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(Width, Height);
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.043f, 0.055f, 0.078f, 1f);
            return settings;
        }

        /// <summary>
        /// A plain slider, this project's stylesheet, and a simulated press and drag on the handle.
        /// Nothing here rebuilds anything, so a failure is the stylesheet or the control itself.
        /// </summary>
        [UnityTest]
        public IEnumerator APlainSliderFollowsTheHandleWhenItIsDragged()
        {
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            var host = new GameObject("SliderDrag");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = MakeSettings(texture);

            try
            {
                yield return null;

                var root = document.rootVisualElement;
                var sheet = Resources.Load<StyleSheet>("ScalingLaws");
                Assert.That(sheet, Is.Not.Null, "The stylesheet did not load.");
                root.styleSheets.Add(sheet);

                var slider = new Slider(0f, 100f) { value = 20f };
                slider.style.width = 600f;
                root.Add(slider);

                yield return null;
                yield return null;

                var dragger = slider.Q(className: "unity-base-slider__dragger");
                var container = slider.Q(className: "unity-base-slider__drag-container");
                Assert.That(dragger, Is.Not.Null, "The slider has no handle element.");

                var handle = dragger.worldBound;
                var groove = container.worldBound;

                // Written to the log whatever happens, because the geometry is half the answer and
                // a bare pass or fail on the drag would not say which half.
                Debug.Log("[slider] handle " + handle + " container " + groove
                          + " picking " + dragger.pickingMode
                          + " parent-picking " + container.pickingMode);

                Assert.That(handle.height, Is.GreaterThan(1f),
                    "The handle has no height, so there is nothing to grab.");
                Assert.That(groove.Contains(handle.center), Is.True,
                    "The handle's centre " + handle.center + " is outside its own drag container "
                    + groove + ", so a press there never reaches the slider.");

                var before = slider.value;

                using (var down = PointerDownEvent.GetPooled(
                           new Event
                           {
                               type = EventType.MouseDown,
                               mousePosition = handle.center,
                               button = 0
                           }))
                {
                    down.target = dragger;
                    dragger.SendEvent(down);
                }

                yield return null;

                var to = new Vector2(handle.center.x + 200f, handle.center.y);
                using (var move = PointerMoveEvent.GetPooled(
                           new Event
                           {
                               type = EventType.MouseDrag,
                               mousePosition = to,
                               button = 0
                           }))
                {
                    move.target = dragger;
                    dragger.SendEvent(move);
                }

                yield return null;

                Debug.Log("[slider] value " + before + " -> " + slider.value);

                Assert.That(slider.value, Is.GreaterThan(before),
                    "Pressing the handle and moving right did not move the slider, so dragging is "
                    + "broken for every slider in the game and not only on one screen.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// The custom company window's cash slider, which rebuilds the whole page on every change.
        /// If the instance the player is holding leaves the panel, the drag ends on the first pixel
        /// of movement and only clicking can ever work.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHandleThePlayerIsHoldingStaysInTheTree()
        {
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            var host = new GameObject("SliderRebuild");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = MakeSettings(texture);
            var controller = host.AddComponent<MainMenuController>();

            try
            {
                const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic
                                                               | BindingFlags.Public;
                var type = typeof(MainMenuController);

                type.GetField("chosenArchetype", any)!.SetValue(controller, CompanyArchetype.Custom);
                type.GetProperty("CompanyIsChosen", any)!.SetValue(controller, true);
                type.GetField("companyName", any)!.SetValue(controller, "Prometheus AI");
                type.GetField("customStage", any)!.SetValue(controller, 1);

                var show = type.GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic);
                var stageType = type.GetNestedType("Stage", BindingFlags.NonPublic);
                show!.Invoke(controller, new[] { System.Enum.Parse(stageType!, "Company") });

                yield return null;
                yield return null;

                var slider = document.rootVisualElement.Query<Slider>()
                    .ToList()
                    .FirstOrDefault(s => s.ClassListContains("cslider"));

                Assert.That(slider, Is.Not.Null, "The START page has no cash slider on it.");
                Assert.That(slider.panel, Is.Not.Null, "The slider is not in a panel to begin with.");

                // What a drag does on its very first pixel of travel.
                slider.value -= 100_000f;

                yield return null;

                Debug.Log("[rebuild] the slider the player was holding has a panel: "
                          + (slider.panel != null));

                Assert.That(slider.panel, Is.Not.Null,
                    "Changing the value took the slider out of the panel, so the press is released "
                    + "on the first pixel of movement and only a click can ever work.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }
        /// <summary>
        /// The rent readout follows the handle instead of waiting for the mouse to come up.
        ///
        /// **The second half of one complaint.** Dragging was fixed on 2026-10-31 by holding the
        /// page rebuild until the handle is let go, and the author reported the consequence the
        /// next morning, on MOC, by name: the handle moves and the figures above and below it
        /// stand still until the release. A reading that only catches up afterwards is a control
        /// the player cannot aim, which is most of what a slider is for.
        ///
        /// It asserts both halves together on purpose. Repainting by rebuilding the page would
        /// satisfy the first assertion and bring straight back the fault the other two tests in
        /// this file exist for, so the slider is checked for a panel in the same breath.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRentReadoutFollowsTheHandleWhileItIsHeld()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null, "The game scene has no document on it.");

            // A company with an audience behind it, or the band under the slider has nobody to
            // measure against and reads the same sentence at every setting.
            TabProofCampaign.Furnish(shell.Simulation);

            Assert.That(shell.OpenScreenByName("Fleet"), Is.True, "There is no Fleet screen.");

            yield return null;
            yield return null;

            var panel = document.rootVisualElement.Q(className: "rent-panel");
            Assert.That(panel, Is.Not.Null, "The fleet page has no rented capacity panel on it.");

            var slider = panel.Query<Slider>().ToList().FirstOrDefault();
            Assert.That(slider, Is.Not.Null, "The rented capacity panel has no slider on it.");

            // Queried fresh on every call, because the live paint replaces these elements rather
            // than editing them, and a reference captured once would report the old text forever.
            string Reading() =>
                string.Join(" | ", panel.Query<Label>(className: "rmeters__value")
                    .ToList().Select(label => label.text))
                + " || " + (panel.Q<Label>(className: "rband__figure")?.text ?? "none");

            var before = Reading();

            // The press the helper watches for, so the rebuild is held back exactly as it is
            // while the player is holding the handle.
            var dragger = slider.Q(className: "unity-base-slider__dragger") ?? slider;

            using (var down = PointerDownEvent.GetPooled(
                       new Event
                       {
                           type = EventType.MouseDown,
                           mousePosition = dragger.worldBound.center,
                           button = 0
                       }))
            {
                down.target = dragger;
                dragger.SendEvent(down);
            }

            yield return null;

            // A real pixel of travel, away from whichever end the handle is sitting on.
            var travel = (slider.highValue - slider.lowValue) * 0.4f;
            slider.value = slider.value + travel > slider.highValue
                ? slider.value - travel
                : slider.value + travel;

            yield return null;

            var after = Reading();

            Debug.Log("[rent] before " + before);
            Debug.Log("[rent] after  " + after);

            Assert.That(slider.panel, Is.Not.Null,
                "Moving the handle took the slider out of the panel, so the drag ends on its "
                + "first pixel and only a click can ever work.");

            Assert.That(after, Is.Not.EqualTo(before),
                "The rent meters and the capacity band did not move while the handle was held, "
                + "so every figure on this panel waits for the mouse to come up.");
        }
    }
}
