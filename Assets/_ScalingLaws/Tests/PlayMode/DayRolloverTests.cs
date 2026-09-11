using System.Collections;
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
    /// What the player was reading survives a day going past.
    ///
    /// **Reported by Natalia: things vanish while you are looking at them.** A research card opened
    /// to decide on a four month programme closes every time a day rolls over, which at normal speed
    /// is every second and a half, so the card cannot be read to the end. The same for the "(i)"
    /// cards.
    ///
    /// The fixture does not need a clock. `Show(current)` **is** the rollover: the shell rebuilds the
    /// open page by calling it, and every control on every screen answers the same way, so opening
    /// the tab that is already open runs the identical path a day does. That makes the measurement
    /// deterministic rather than a race against wall time.
    /// </summary>
    public sealed class DayRolloverTests
    {
        private static (GameShell Shell, VisualElement Root) Load()
        {
            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);

            // The phone rings on the first frame of a new company and mounts on the panel root, so
            // it sits over whatever is being measured.
            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            return (shell, document.rootVisualElement);
        }

        /// <summary>Buttons answer a submit the same way they answer a click, and a synthetic pointer
        /// press is three events that have to arrive in the right order to do the same thing.</summary>
        private static void Press(VisualElement target)
        {
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = target;
            target.SendEvent(submit);
        }

        /// <summary>
        /// A research pip listens for the click itself rather than through the button action,
        /// because the card opens where the finger is. A pooled event carries no position, which
        /// puts the card in the top corner and is the one thing about it this fixture does not care
        /// about.
        /// </summary>
        private static void Click(VisualElement target)
        {
            using var click = ClickEvent.GetPooled();
            click.target = target;
            target.SendEvent(click);
        }

        [UnitySetUp]
        public IEnumerator OpenTheGame()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheResearchCardSurvivesTheDayRollingOver()
        {
            var (shell, root) = Load();

            Assert.That(shell.OpenScreenByName("Research"), Is.True);
            yield return null;

            var node = root.Q(className: "tree-pip");
            Assert.That(node, Is.Not.Null, "The research tree drew no nodes to click.");

            Click(node);
            yield return null;

            Assert.That(root.Q(className: "rcard"), Is.Not.Null,
                "Clicking a node opened no card, so this fixture is measuring nothing.");

            // The rollover.
            shell.OpenScreenByName("Research");
            yield return null;

            Assert.That(root.Q(className: "rcard"), Is.Not.Null,
                "The card a player opened to read about a four month programme is gone. A day rolls "
                + "over every second and a half at normal speed, so it cannot be read to the end.");
        }

        [UnityTest]
        public IEnumerator TheInfoCardSurvivesTheDayRollingOver()
        {
            var (shell, root) = Load();

            Assert.That(shell.OpenScreenByName("Family"), Is.True);
            yield return null;

            var badge = root.Q(className: "infodot");
            Assert.That(badge, Is.Not.Null, "No info badge on the architecture screen.");

            Press(badge);
            yield return null;

            Assert.That(root.Q(className: "insight"), Is.Not.Null,
                "The badge opened no card, so this fixture is measuring nothing.");

            shell.OpenScreenByName("Family");
            yield return null;

            Assert.That(root.Q(className: "insight"), Is.Not.Null,
                "The card explaining the control under the cursor closed itself when the day rolled "
                + "over. The control it belongs to still exists; it was rebuilt.");
        }

        /// <summary>
        /// The reading position does not move, and it does not move on the frame either.
        ///
        /// **The offset used to be captured and put back a frame later**, on purpose: a scroller
        /// whose content has not been laid out has no range to scroll within, so setting it there
        /// does nothing. It worked, and it left one rendered frame at the top of the page every
        /// time a day rolled over, which at normal speed is every second and a half. That is the
        /// jumping Natalia reported, and no amount of restoring it afterwards can remove it.
        ///
        /// Asserted on the same frame as the rebuild rather than after a yield, because a frame
        /// later is exactly what the old code already got right.
        /// </summary>
        [UnityTest]
        public IEnumerator TheReadingPositionDoesNotEvenFlicker()
        {
            var (shell, root) = Load();

            Assert.That(shell.OpenScreenByName("Research"), Is.True);

            // Laid out, or the scroller has no range and nothing below can mean anything.
            for (var frame = 0; frame < 6; frame++)
            {
                yield return null;
            }

            var scroller = root.Q<ScrollView>(className: "page-scroll");
            Assert.That(scroller, Is.Not.Null, "the page is not in a scroller");

            Assume.That(scroller.contentContainer.layout.height,
                Is.GreaterThan(scroller.layout.height),
                "the research tree fits the window here, so there is nothing to scroll and this "
                + "fixture cannot measure anything");

            scroller.scrollOffset = new Vector2(0f, 180f);
            yield return null;

            var before = scroller.scrollOffset.y;
            Assume.That(before, Is.GreaterThan(1f), "the page refused to scroll");

            // The rollover, and the reading is taken immediately after it rather than a frame on.
            shell.OpenScreenByName("Research");

            var after = root.Q<ScrollView>(className: "page-scroll");
            Assert.That(after, Is.SameAs(scroller),
                "The page was given a new scroller, so whatever the player was reading is at the "
                + "top of the window for at least one frame.");

            Assert.That(after.scrollOffset.y, Is.EqualTo(before).Within(1.0),
                "The reading position moved on the frame the day rolled over.");
        }

        /// <summary>
        /// Money spent shows as money spent, without waiting for tomorrow.
        ///
        /// **Only the day rollover used to redraw the bar across the top.** Every control in the
        /// game answers by rebuilding the open page, and the bar is the one piece of the interface
        /// that survives that, so it was never included: buying an office or paying a demand left
        /// the figure at the top showing what the company had beforehand. Paused, which is when a
        /// player does most of their spending, it never caught up at all.
        ///
        /// `OpenScreenByName` had carried its own refresh for exactly this reason, with a comment
        /// saying an investigation had been spent on the symptom. It does not any more, which is
        /// what lets this fixture measure the shell rather than the workaround.
        /// </summary>
        [UnityTest]
        public IEnumerator TheMoneyAtTheTopFollowsTheMoneyInTheBank()
        {
            var (shell, root) = Load();

            Assert.That(shell.OpenScreenByName("Site"), Is.True);
            yield return null;

            var cash = root.Q<Label>(className: "topbar__stat");
            Assert.That(cash, Is.Not.Null, "no money figure on the top bar");

            var before = cash.text;

            // Spent, and no day allowed to pass. The clock is paused when a campaign opens, which
            // is the state a player buying an office is actually in.
            shell.Simulation.State.CashUsd -= 4_000_000L;

            shell.OpenScreenByName("Site");
            yield return null;

            Assert.That(cash.text, Is.Not.EqualTo(before),
                "The top bar still reports the money the company had before it was spent, and it "
                + "will go on doing so until a day is allowed to roll over.");
        }

        /// <summary>
        /// Leaving the screen is a different thing from redrawing it, and the card has to know which
        /// happened. A card left hanging over a different tab is the fault the corner banners had.
        /// </summary>
        [UnityTest]
        public IEnumerator ButBothGoWhenThePlayerLeavesTheScreen()
        {
            var (shell, root) = Load();

            Assert.That(shell.OpenScreenByName("Research"), Is.True);
            yield return null;

            var node = root.Q(className: "tree-pip");
            Assert.That(node, Is.Not.Null);

            Click(node);
            yield return null;
            Assume.That(root.Q(className: "rcard"), Is.Not.Null);

            // Asserted rather than called, because the first version of this fixture asked for a
            // screen called Compute, which does not exist. `OpenScreenByName` answers false on a
            // name it does not know rather than throwing, so the shell never rebuilt, the card was
            // never removed, and the test reported a bug in the game that was a typo in the test.
            Assert.That(shell.OpenScreenByName("Fleet"), Is.True, "no screen called Fleet");
            yield return null;

            Assert.That(root.Q(className: "rcard"), Is.Null,
                "A research card is still up over the compute screen.");
        }
    }
}
