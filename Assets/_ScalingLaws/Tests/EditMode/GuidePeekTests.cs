using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// His next line showing itself over a folded strip.
    ///
    /// Asked for on 2026-10-25. Folded, his message is hidden and so is NEXT, so the only way past
    /// a step is the player doing the thing it asked for, and that moves him on in silence. The
    /// line comes out for a moment, then goes back, so they know something was said.
    ///
    /// **The second fixture here is the one that earns its place.** The peek puts a bar carrying
    /// NEXT, the step's offer, LATER and SKIP under the cursor for a second and a half. Without
    /// the clicks turned off, a click meant for the page lands on a button that ends the tutorial,
    /// and this project has shipped two invisible click eaters already.
    /// </summary>
    public sealed class GuidePeekTests
    {
        private sealed class Rig
        {
            public Rig()
            {
                Host = new VisualElement();
                State = new GuideProgress { Stage = GuideStage.Touring, Step = 0 };

                Overlay = new GuideOverlay(Host, () => State, _ => { }, () => { });
            }

            public VisualElement Host { get; }
            public GuideProgress State { get; }
            public GuideOverlay Overlay { get; }

            public VisualElement Bar() =>
                Host.Query<VisualElement>(className: "guide__bar").ToList().FirstOrDefault();

            public VisualElement Strip() =>
                Host.Query<VisualElement>(className: "guide").ToList().FirstOrDefault();

            /// <summary>Every button on his message, whichever ones this step happens to carry.</summary>
            public System.Collections.Generic.List<Button> BarButtons() =>
                Bar() is { } bar ? bar.Query<Button>().ToList() : new System.Collections.Generic.List<Button>();

            /// <summary>Folds him away and moves the tour on by one step, the way a player does.</summary>
            public void FoldThenSatisfyAStep()
            {
                Overlay.Refresh();
                Overlay.SetFolded(true);

                State.Step += 1;
                Overlay.Refresh();
            }
        }

        [Test]
        public void AStepSatisfiedWhileHeIsFoldedShowsHisLine()
        {
            var rig = new Rig();
            rig.FoldThenSatisfyAStep();

            Assert.That(rig.Overlay.Peeking, Is.True,
                "He moved on while folded away and said nothing at all, so the player has no way "
                + "of knowing a line went by.");

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "The peek is running and his message is not laid out, so there is nothing on "
                + "screen to animate.");

            Assert.That(rig.Overlay.Folded, Is.True,
                "The peek unfolded him. It is a notice, not a decision to bring him back: the "
                + "player folded him away and has not asked for him since.");
        }

        /// <summary>
        /// **Nothing on a peeking bar may be clicked.**
        /// </summary>
        [Test]
        public void NothingOnThePeekCanBeClicked()
        {
            var rig = new Rig();
            rig.FoldThenSatisfyAStep();

            var buttons = rig.BarButtons();

            Assert.That(buttons.Count, Is.GreaterThan(0),
                "There are no buttons on his message at all, so this test is measuring nothing. "
                + "The bar carries NEXT, and usually SKIP and LATER beside it.");

            foreach (var button in buttons)
            {
                Assert.That(button.pickingMode, Is.EqualTo(PickingMode.Ignore),
                    "'" + button.text + "' takes clicks while it is sliding past under the "
                    + "cursor. A click aimed at the page behind would end or advance the tutorial.");
            }

            rig.Overlay.EndPeek();

            foreach (var button in rig.BarButtons())
            {
                Assert.That(button.pickingMode, Is.EqualTo(PickingMode.Position),
                    "'" + button.text + "' is still deaf after the peek, so unfolding him gives "
                    + "the player a message whose buttons do nothing.");
            }
        }

        [Test]
        public void ThePeekPutsItselfAwayAndLeavesTheFoldAlone()
        {
            var rig = new Rig();
            rig.FoldThenSatisfyAStep();

            rig.Overlay.EndPeek();

            Assert.That(rig.Overlay.Peeking, Is.False, "The peek did not end.");

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.None),
                "His message stayed on screen after the peek, so folding him away is undone by "
                + "the first step the player satisfies.");

            Assert.That(rig.Overlay.Folded, Is.True, "The peek ending unfolded him.");

            Assert.That(rig.Strip().ClassListContains("guide--peek-away"), Is.False,
                "The away class was left on the strip, so the next peek has nothing to come from "
                + "and will appear without animating.");
        }

        /// <summary>
        /// Opening him up during a peek has to win, and it has to leave nothing behind that a
        /// timer belonging to the peek can undo a second later.
        /// </summary>
        [Test]
        public void OpeningHimUpDuringAPeekCancelsIt()
        {
            var rig = new Rig();
            rig.FoldThenSatisfyAStep();

            Assert.That(rig.Overlay.Peeking, Is.True, "There is no peek to cancel.");

            rig.Overlay.SetFolded(false);

            Assert.That(rig.Overlay.Peeking, Is.False,
                "The peek is still live over an unfolded strip, so its timer will put his message "
                + "away again while the player is reading it.");

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "He was opened up and his message is hidden.");

            Assert.That(rig.Strip().ClassListContains("guide--peek-away"), Is.False,
                "The strip still wears the away class, so his message is laid out at zero opacity "
                + "and twenty two pixels down: present, invisible, and taking up the room.");
        }

        /// <summary>
        /// **It must not fire when he is on screen**, where the player can see the line change,
        /// and it must not fire on the opening strip of a campaign, which has moved on from
        /// nothing.
        /// </summary>
        [Test]
        public void ThereIsNoPeekWhenHeIsNotFoldedAwayAndNoneOnTheFirstStrip()
        {
            var open = new Rig();
            open.Overlay.Refresh();
            open.State.Step += 1;
            open.Overlay.Refresh();

            Assert.That(open.Overlay.Peeking, Is.False,
                "A step changed while he was on screen and the bar announced itself anyway, so "
                + "the player gets his message sliding about underneath them while they read it.");

            var first = new Rig();
            first.Overlay.SetFolded(true);
            first.Overlay.Refresh();

            Assert.That(first.Overlay.Peeking, Is.False,
                "The very first strip of a campaign peeked. There is no previous step for it to "
                + "have moved on from, so there is nothing to announce.");
        }

        /// <summary>
        /// Ending a peek that is not running must do nothing at all, because two of the three
        /// routes into `EndPeek` are timers that can arrive after it is over.
        /// </summary>
        [Test]
        public void EndingAPeekThatIsNotRunningChangesNothing()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var before = rig.Bar().style.display.value;

            rig.Overlay.EndPeek();
            rig.Overlay.EndPeek();

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(before),
                "A stale timer firing into a strip nobody is peeking at changed what is on screen.");

            Assert.That(rig.Overlay.Folded, Is.False, "It folded him away on its own.");
        }
    }
}
