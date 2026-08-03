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
            Assert.That(root.styleSheets.count, Is.GreaterThan(0),
                "No stylesheet on the root, so every rule in it is doing nothing.");

            var shell = root.Q(className: "shell");
            Assert.That(shell, Is.Not.Null, "No shell element.");
            Assert.That(shell.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row),
                "The shell is stacking vertically, so the rail is sitting on top of the content.");

            var rail = root.Q(className: "rail");
            Assert.That(rail, Is.Not.Null, "No rail element.");
            Assert.That(rail.resolvedStyle.width, Is.InRange(200f, 320f),
                $"The rail resolved to {rail.resolvedStyle.width} wide instead of its fixed column.");

            var topbar = root.Q(className: "topbar");
            Assert.That(topbar, Is.Not.Null, "No top bar.");
            Assert.That(topbar.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row),
                "The top bar is stacking, which is why the money and the date print over each other.");
            Assert.That(topbar.resolvedStyle.height, Is.InRange(30f, 80f));

            var railItem = root.Q<Button>(className: "rail__item");
            Assert.That(railItem, Is.Not.Null, "No rail entries.");
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
