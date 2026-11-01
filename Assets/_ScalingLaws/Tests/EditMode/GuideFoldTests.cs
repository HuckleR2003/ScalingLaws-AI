using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Folding Emil away, and the one thing that makes it worth having.
    ///
    /// Asked for on 2026-10-25, in these words: a thick yellow chevron beside his head that hides
    /// him and his message, leaving the arrow and the portrait just above the bottom bar, and
    /// brings him back when it is clicked again.
    ///
    /// **The fixture that matters is the third one.** The strip is rebuilt whenever the step index
    /// changes, which is the 2026-08-25 repair for a tutorial whose button was destroyed between
    /// the press and the release. So a fold stored on the strip would come undone the next time he
    /// said anything, and a player who folded him away on step four would have him back on step
    /// five with no idea why.
    /// </summary>
    public sealed class GuideFoldTests
    {
        private sealed class Rig
        {
            public Rig(int step = 0)
            {
                Host = new VisualElement();
                State = new GuideProgress { Stage = GuideStage.Touring, Step = step };

                Overlay = new GuideOverlay(Host, () => State, _ => { }, () => { });
            }

            public VisualElement Host { get; }
            public GuideProgress State { get; }
            public GuideOverlay Overlay { get; }

            public Button Fold() =>
                Host.Query<Button>(className: "guide__fold").ToList().FirstOrDefault();

            public VisualElement Strip() =>
                Host.Query<VisualElement>(className: "guide").ToList().FirstOrDefault();

            public VisualElement Bar() =>
                Host.Query<VisualElement>(className: "guide__bar").ToList().FirstOrDefault();

            public VisualElement Portrait() =>
                Host.Query<VisualElement>(className: "guide__portrait").ToList().FirstOrDefault();

            public IEnumerable<VisualElement> Arrows() =>
                Host.Query<GuideFoldArrow>().ToList();
        }

        [Test]
        public void TheChevronIsOnTheStripAndItIsClickable()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var fold = rig.Fold();

            Assert.That(fold, Is.Not.Null,
                "There is no chevron on the strip, so there is no way to fold him away.");

            Assert.That(fold.pickingMode, Is.EqualTo(PickingMode.Position),
                "The chevron ignores the pointer. The strip and the speaker are both "
                + "PickingMode.Ignore on purpose so the player can work the screen underneath, and "
                + "a control inside them has to say that it wants its own clicks.");

            Assert.That(rig.Arrows().Count(), Is.EqualTo(1),
                "There is not exactly one drawn chevron on the strip.");
        }

        [Test]
        public void FoldingHidesHisMessageAndKeepsHisPortrait()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            Assert.That(rig.Overlay.Folded, Is.False, "He starts folded away.");
            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "His message is hidden before anybody folded anything.");

            rig.Overlay.SetFolded(true);

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.None),
                "His message is still on screen after being folded away.");

            Assert.That(rig.Portrait(), Is.Not.Null,
                "The portrait went with the message. The arrow and his face are what stays, which "
                + "is the whole of what was asked for.");

            Assert.That(rig.Fold(), Is.Not.Null,
                "The chevron folded itself away, so there is no way to bring him back.");

            Assert.That(rig.Strip().ClassListContains("guide--folded"), Is.True,
                "The strip does not wear the folded class, so the stylesheet cannot lay the pill "
                + "out differently from the full strip.");

            rig.Overlay.SetFolded(false);

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "Unfolding him left his message hidden.");

            Assert.That(rig.Strip().ClassListContains("guide--folded"), Is.False,
                "The folded class survived unfolding.");
        }

        /// <summary>
        /// **The fold has to outlive the strip that carries the button.**
        ///
        /// This failed when the peek was added, and the peek was right: a step satisfied while he
        /// is folded away now shows his line for a moment on purpose, so the bar is laid out at
        /// exactly the instant this used to check it was not. The claim worth holding is not that
        /// the bar is hidden on that frame, it is that **the fold is still the state he returns
        /// to**, so the check moved to the other side of the peek rather than being loosened.
        /// </summary>
        [Test]
        public void HeStaysFoldedWhenHeMovesToTheNextStep()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();
            rig.Overlay.SetFolded(true);

            var before = rig.Fold();

            // The step moves, which is the one thing that rebuilds the strip from scratch.
            rig.State.Step += 1;
            rig.Overlay.Refresh();

            Assert.That(rig.Fold(), Is.Not.SameAs(before),
                "The strip was not rebuilt, so this test is not measuring what it claims to. A "
                + "step change is the one case that rebuilds it.");

            Assert.That(rig.Overlay.Folded, Is.True,
                "The fold came undone when he moved on.");

            // The line is showing itself, which is the peek and is deliberate. What matters is
            // where it goes when that finishes.
            rig.Overlay.EndPeek();

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.None),
                "Once his line had finished showing itself, the new strip was left with his "
                + "message on screen, so the fold is remembered in the overlay and not applied to "
                + "the strip that replaced it.");

            Assert.That(rig.Overlay.Folded, Is.True,
                "His line showing itself brought him back for good.");
        }

        /// <summary>
        /// A day rolling over must not disturb it either, for the reason the whole strip is only
        /// rebuilt on a step change: `Refresh` runs about every second and a half.
        /// </summary>
        [Test]
        public void ADayRollingOverDoesNotBringHimBack()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();
            rig.Overlay.SetFolded(true);

            for (var day = 0; day < 8; day++)
            {
                rig.Overlay.Refresh();
            }

            Assert.That(rig.Overlay.Folded, Is.True,
                "Eight days of the clock running brought him back on his own.");

            Assert.That(rig.Bar().style.display.value, Is.EqualTo(DisplayStyle.None),
                "His message is back on screen after eight rollovers.");
        }

        /// <summary>
        /// **Folding is not skipping, and nothing about the tour may move.** Skipping has two
        /// buttons of its own and they say what they do.
        /// </summary>
        [Test]
        public void FoldingHimAwayDoesNotAdvanceOrEndTheTour()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var step = rig.State.Step;
            var stage = rig.State.Stage;

            rig.Overlay.SetFolded(true);
            rig.Overlay.SetFolded(false);
            rig.Overlay.SetFolded(true);

            Assert.That(rig.State.Step, Is.EqualTo(step),
                "Folding him away advanced the tour, so a player tidying the screen loses a step "
                + "of the tutorial every time they do it.");

            Assert.That(rig.State.Stage, Is.EqualTo(stage),
                "Folding him away ended or paused the tour. It is meant to hide him and nothing "
                + "else: " + stage + " became " + rig.State.Stage + ".");
        }

        /// <summary>
        /// The chevron points the way it is about to go, which is the only thing that tells a
        /// player it is a control rather than a decoration.
        /// </summary>
        [Test]
        public void TheChevronTurnsOver()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var arrow = rig.Arrows().FirstOrDefault() as GuideFoldArrow;

            Assert.That(arrow, Is.Not.Null, "There is no drawn chevron to turn over.");

            Assert.That(arrow.PointsUp, Is.False,
                "It points up while he is still on screen, so it is offering to bring back "
                + "somebody who is already here.");

            rig.Overlay.SetFolded(true);

            Assert.That(rig.Arrows().FirstOrDefault() is GuideFoldArrow { PointsUp: true }, Is.True,
                "It still points down once he is folded away, so the one control on the pill is "
                + "offering to do the thing it has just done.");
        }
    }
}
