using System.Collections;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Whether a click on a control actually reaches that control.
    ///
    /// **Reported as totally critical**: the whole first stage of the model creator took no clicks
    /// at all, not the name field, not NEXT, while the stage rail above it and the bar below it
    /// worked. The buttons on the site screen went the same way. That is the signature of something
    /// invisible lying on top of them, and it is a question a test can answer exactly, because the
    /// panel can be asked what is under a point.
    ///
    /// Every other test of these screens passed the whole time, because a control that exists, is
    /// enabled and is laid out correctly is still unreachable when something transparent is over it.
    /// That is the same class as the office buttons sixty four pixels under a clipping edge.
    /// </summary>
    public sealed class ClickReachTests
    {
        private static VisualElement Root =>
            Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        private static IEnumerator Open(string screen)
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell.OpenScreenByName(screen), Is.True, "there is no " + screen + " screen");

            for (var frame = 0; frame < 4; frame++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// What the panel says is on top at the middle of this control, described for a failure
        /// message. Walks up from the picked element so the answer names something recognisable.
        /// </summary>
        private static string OnTopOf(VisualElement target)
        {
            var box = target.worldBound;
            var middle = new Vector2(box.center.x, box.center.y);

            var picked = target.panel.Pick(middle);

            if (picked == null)
            {
                return "nothing (the pick found no element at all)";
            }

            for (var step = picked; step != null; step = step.hierarchy.parent)
            {
                if (step == target)
                {
                    return "the control itself";
                }
            }

            var trail = string.Empty;

            for (var step = picked; step != null; step = step.hierarchy.parent)
            {
                var name = step.GetClasses().FirstOrDefault() ?? step.GetType().Name;
                trail = trail.Length == 0 ? name : trail + " < " + name;

                if (trail.Length > 160)
                {
                    break;
                }
            }

            return trail;
        }

        /// <summary>**The report, as a test.** The first page of the creator takes clicks.</summary>
        [UnityTest]
        public IEnumerator TheFirstPageOfTheCreatorTakesClicks()
        {
            yield return Open("Create");

            var next = Root.Query<Button>().ToList()
                .FirstOrDefault(button => button.ClassListContains("menu-button--primary"));

            Assert.That(next, Is.Not.Null, "the creator has no NEXT button");

            Assert.That(next.worldBound.width, Is.GreaterThan(1f), "NEXT has no size");

            Assert.That(OnTopOf(next), Is.EqualTo("the control itself"),
                "Something is lying on top of NEXT: " + OnTopOf(next) + ". The button exists, is "
                + "laid out and is enabled, and the player cannot press it.");
        }

        /// <summary>And so does the name field, which is the first thing anybody touches.</summary>
        [UnityTest]
        public IEnumerator TheNameFieldOnTheFirstPageTakesClicks()
        {
            yield return Open("Create");

            var field = Root.Q<TextField>();

            Assert.That(field, Is.Not.Null, "the branding page has no name field");

            var phone = Root.Q(className: "phone");
            var stack = Root.Q(className: "mb-stack");

            Assert.That(OnTopOf(field), Is.EqualTo("the control itself"),
                "Something is lying on top of the model name field: " + OnTopOf(field) + "."
                + " panel " + Root.worldBound
                + " field " + field.worldBound
                + " phone " + (phone == null ? "none" : phone.worldBound.ToString())
                + " stack " + (stack == null ? "none" : stack.worldBound.ToString()));
        }

        /// <summary>
        /// The corner icons on the site screen: the map, the premises, the server room, the
        /// furnishing. All four were reported dead in the same build.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSiteCornerIconsTakeClicks()
        {
            yield return Open("Site");

            var rail = Root.Q(className: "site-rail");
            Assert.That(rail, Is.Not.Null, "the site screen has no corner rail");

            var buttons = rail.Query<Button>().ToList()
                .Where(button => button.worldBound.width > 1f)
                .ToList();

            Assert.That(buttons, Is.Not.Empty, "the corner rail has no buttons on it");

            foreach (var button in buttons)
            {
                Assert.That(OnTopOf(button), Is.EqualTo("the control itself"),
                    "Something is lying on top of a corner icon: " + OnTopOf(button) + ".");
            }
        }
    }
}
