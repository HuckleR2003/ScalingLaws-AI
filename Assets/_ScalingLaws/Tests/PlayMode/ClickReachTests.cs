using System;
using System.Collections;
using System.Collections.Generic;
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
            UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        private static IEnumerator Open(string screen)
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = UnityEngine.Object.FindFirstObjectByType<GameShell>();
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
        /// <summary>
        /// The part of this control the player can actually see, after every scroller and every
        /// clipping frame above it has had its say.
        ///
        /// **Without this the sweep reports the wrong screen.** A research card panned out of the
        /// map frame, or a row below the fold of a page, still has a world rectangle where it would
        /// be, and asking what is on top of a point nobody can see answers a question nobody asked.
        /// An empty rectangle means there is nothing to click, which is not the same as being
        /// covered, so those are skipped rather than reported.
        /// </summary>
        private static Rect Seen(VisualElement element)
        {
            var box = element.worldBound;

            for (var parent = element.hierarchy.parent; parent != null;
                 parent = parent.hierarchy.parent)
            {
                // The two things in this interface that actually clip: a scroller shows its
                // viewport, and the research map is a window onto a board that is panned behind
                // it. `resolvedStyle` carries no overflow, so this names them rather than asking.
                var clip = parent switch
                {
                    ScrollView scroller => scroller.contentViewport.worldBound,
                    ResearchMap map => map.worldBound,
                    _ => Rect.zero
                };

                if (clip.width <= 0f && clip.height <= 0f)
                {
                    continue;
                }

                var xMin = Mathf.Max(box.xMin, clip.xMin);
                var yMin = Mathf.Max(box.yMin, clip.yMin);
                var xMax = Mathf.Min(box.xMax, clip.xMax);
                var yMax = Mathf.Min(box.yMax, clip.yMax);

                box = new Rect(xMin, yMin, Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
            }

            return box;
        }

        private static string OnTopOf(VisualElement target)
        {
            var box = Seen(target);
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
        /// **Every button on every screen, in one sweep.**
        ///
        /// The three fixtures above were written against one report and each names one screen. This
        /// is the general form of that question, and it is the one worth keeping: a control that
        /// exists, is enabled and is laid out is still unreachable when something transparent lies
        /// over it, and nothing else in this project can see that.
        ///
        /// A fresh campaign per screen, so nothing is covered by a card the previous screen left
        /// open. Disabled buttons are skipped: they are allowed to refuse a click, and what is
        /// being asked here is whether the click arrives at all.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryButtonOnEveryScreenCanBeClicked()
        {
            var trouble = new List<string>();

            foreach (var screen in GameShell.ScreenNames)
            {
                yield return Open(screen);

                var buttons = Root.Query<Button>().ToList()
                    .Where(button => button.enabledInHierarchy)
                    .Where(button => Seen(button).width > 2f && Seen(button).height > 2f)
                    .Where(button => button.resolvedStyle.display != DisplayStyle.None)
                    .Where(button => button.resolvedStyle.visibility == Visibility.Visible)
                    .ToList();

                foreach (var button in buttons)
                {
                    var onTop = OnTopOf(button);

                    if (onTop == "the control itself")
                    {
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(button.text)
                        ? string.Join(".", button.GetClasses().Take(2))
                        : button.text;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = button.name ?? "unnamed";
                    }

                    trouble.Add($"{screen}: \"{Trim(name)}\" at {button.worldBound} "
                        + $"is under {onTop}");
                }
            }

            Assert.That(trouble, Is.Empty,
                trouble.Count + " control(s) cannot be clicked because something is over them:"
                + Environment.NewLine + string.Join(Environment.NewLine, trouble.Take(20)));
        }

        private static string Trim(string text) =>
            text.Length <= 32 ? text : text[..32] + "...";

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
