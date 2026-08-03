using System.Collections;
using NUnit.Framework;
using ScalingLaws.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Does the game actually boot.
    ///
    /// Every EditMode test in this project drives the simulation directly and none of them load a
    /// scene, which is fast and was also a blind spot: a screen that throws in OnEnable renders
    /// nothing and every one of those tests still passes. These load the real scenes and check that
    /// something is on them.
    /// </summary>
    public sealed class BootTests
    {
        private static UIDocument FindDocument()
        {
            return Object.FindFirstObjectByType<UIDocument>();
        }

        /// <summary>
        /// Waits until the panel has actually laid out.
        ///
        /// Reading resolvedStyle before a layout pass returns defaults rather than applied values, so
        /// a styled row reports itself as an unstyled column and the test fails for a reason that has
        /// nothing to do with the game. A fixed number of yields is not enough: it passed on some
        /// runs and failed on others, which is worse than failing every time. The tell is
        /// resolvedStyle.width being NaN.
        /// </summary>
        private static IEnumerator WaitForLayout(VisualElement root, int maximumFrames = 120)
        {
            if (root == null)
            {
                yield break;
            }

            // A runtime panel in batch mode is never given a size by the screen, so Yoga has nothing
            // to solve and every resolvedStyle stays NaN. Pinning a known viewport makes the layout
            // run, and testing against a fixed 1920x1080 is what we want anyway: the question is
            // whether the stylesheet turns the shell into a row, not what the window happens to be.
            root.style.width = 1920;
            root.style.height = 1080;

            for (var frame = 0; frame < maximumFrames; frame++)
            {
                if (!float.IsNaN(root.resolvedStyle.width) && root.resolvedStyle.width > 0f)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static int CountLabels(VisualElement root)
        {
            var count = 0;
            root.Query<Label>().ForEach(label =>
            {
                if (!string.IsNullOrEmpty(label.text))
                {
                    count++;
                }
            });

            return count;
        }

        [UnityTest]
        public IEnumerator TheMainMenuBuildsSomethingVisible()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return null;
            yield return null;

            var document = FindDocument();
            Assert.That(document, Is.Not.Null, "No UIDocument on the menu scene.");

            var root = document.rootVisualElement;
            Assert.That(root, Is.Not.Null, "The menu UIDocument has no root element.");
            Assert.That(root.childCount, Is.GreaterThan(0), "The menu built no visual tree at all.");
            Assert.That(CountLabels(root), Is.GreaterThan(2), "The menu has almost no text on it.");
            Assert.That(document.panelSettings, Is.Not.Null, "The menu has no PanelSettings.");
            Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null,
                "No theme, which means no default control styling and no font.");
        }

        [UnityTest]
        public IEnumerator TheGameSceneBuildsSomethingVisible()
        {
            // Exactly what the menu does when the player presses BEGIN.
            SceneFlow.ResumeSavedCampaign = false;
            SceneFlow.RequestedCompanyName = "Boot test";
            SceneFlow.RequestedArchetype = 1;
            SceneFlow.RequestedTraitA = 1;
            SceneFlow.RequestedTraitB = 2;

            SceneManager.LoadScene(SceneFlow.GameScene);
            yield return null;
            yield return null;

            var document = FindDocument();
            Assert.That(document, Is.Not.Null, "No UIDocument on the game scene.");

            var root = document.rootVisualElement;
            Assert.That(root, Is.Not.Null, "The game UIDocument has no root element.");
            Assert.That(root.childCount, Is.GreaterThan(0),
                "The game scene built no visual tree. Something threw in OnEnable.");
            Assert.That(CountLabels(root), Is.GreaterThan(5),
                "The game scene has almost no text on it.");

            // The rail is what makes it navigable at all.
            var buttons = root.Query<Button>().ToList();
            Assert.That(buttons.Count, Is.GreaterThan(6),
                $"Only {buttons.Count} buttons on the game screen; the rail and toolbar are missing.");
        }

        [UnityTest]
        public IEnumerator EveryRailScreenOpensWithoutThrowing()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);
            yield return null;
            yield return null;

            var root = FindDocument().rootVisualElement;
            var railButtons = root.Query<Button>(className: "rail__item").ToList();

            Assert.That(railButtons.Count, Is.GreaterThan(5), "The rail is missing entries.");

            foreach (var button in railButtons)
            {
                using var click = new NavigationSubmitEvent() { target = button };
                button.SendEvent(click);
                yield return null;

                Assert.That(root.childCount, Is.GreaterThan(0),
                    $"Opening {button.text} emptied the screen.");
            }
        }

        /// <summary>
        /// The test that should have existed first.
        ///
        /// A broken layout passes every other check here: the tree is built, the buttons are there,
        /// nothing throws. It just renders as one full-width column of unstyled bars because the
        /// stylesheet never loaded. Counting elements proves nothing; the resolved layout has to be
        /// checked directly.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLayoutIsActuallyStyled()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);
            yield return null;
            yield return null;
            yield return null;

            var root = FindDocument().rootVisualElement;
            yield return WaitForLayout(root);

            // The bug this guards was a stylesheet that was never attached, which turned every screen
            // into one full width column of unstyled bars. That is checkable without a display and it
            // is the assertion that matters.
            Assert.That(root.styleSheets.count, Is.GreaterThan(0),
                "No stylesheet on the root, so every rule in it is doing nothing and the whole game "
                + "renders as an unstyled column.");

            var shell = root.Q(className: "shell");
            var rail = root.Q(className: "rail");
            var topbar = root.Q(className: "topbar");
            var railItem = root.Q<Button>(className: "rail__item");

            Assert.That(shell, Is.Not.Null, "No shell element.");
            Assert.That(rail, Is.Not.Null, "No rail element.");
            Assert.That(topbar, Is.Not.Null, "No top bar.");
            Assert.That(railItem, Is.Not.Null, "No rail entries.");

            // Resolved styles need a real layout pass, and a runtime panel in batch mode never gets
            // one: every resolvedStyle stays NaN however many frames are waited or however explicitly
            // the root is sized. Rather than assert against defaults and call it a pass, the check
            // stops here and says so. Run this fixture from the Test Runner window to get the rest.
            if (float.IsNaN(root.resolvedStyle.width) || root.resolvedStyle.width <= 0f)
            {
                Assert.Ignore(
                    "Layout did not run: this panel has no size in batch mode. The wiring above is "
                    + "verified; the resolved geometry needs the editor Test Runner or a real display.");
            }

            Assert.That(shell.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row),
                "The shell is stacking vertically, so the rail is sitting on top of the content.");
            Assert.That(rail.resolvedStyle.width, Is.InRange(200f, 320f),
                $"The rail resolved to {rail.resolvedStyle.width} wide instead of its fixed column.");
            Assert.That(topbar.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row),
                "The top bar is stacking, which is why the money and the date print over each other.");
            Assert.That(railItem.resolvedStyle.width, Is.LessThan(340f),
                $"A rail entry is {railItem.resolvedStyle.width} wide, so it is spanning the screen.");
        }

        [UnityTest]
        public IEnumerator TextHasAFontToRenderWith()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return null;
            yield return null;

            var root = FindDocument().rootVisualElement;
            yield return WaitForLayout(root);

            var label = root.Q<Label>();

            Assert.That(label, Is.Not.Null, "No label to check.");

            var hasFont = label.resolvedStyle.unityFont != null
                || label.resolvedStyle.unityFontDefinition.fontAsset != null
                || label.resolvedStyle.unityFontDefinition.font != null;

            Assert.That(hasFont, Is.True,
                "Labels resolve to no font at all, which is why the text is invisible.");
        }
    }
}
