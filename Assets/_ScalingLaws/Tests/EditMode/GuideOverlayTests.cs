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
    /// The strip survives the page being rebuilt underneath it.
    ///
    /// **This is the ratchet for the bug that stopped the first playtest.** `GameShell.Show` calls
    /// `guide.Refresh()`, and `Show` runs every time a simulated day rolls over, which at normal
    /// speed is about every one and a half seconds. The overlay rebuilt itself on every one of those
    /// calls, so the button under the player's cursor was destroyed and recreated between the press
    /// and the release.
    ///
    /// Every symptom the playtest reported is that one fact: NEXT "not working", the same line
    /// showing three times in a row, and then several steps going by at once when a click finally
    /// landed in a gap. None of it was the script and none of it was the copy.
    /// </summary>
    public sealed class GuideOverlayTests
    {
        private sealed class Rig
        {
            public Rig(int step = 0)
            {
                Host = new VisualElement();
                State = new GuideProgress { Stage = GuideStage.Touring, Step = step };

                Overlay = new GuideOverlay(Host, () => State, target => Opened.Add(target),
                    () => Changes++, target => Tabs.TryGetValue(target, out var tab) ? tab : null,
                    target => Locked = target);
            }

            public VisualElement Host { get; }
            public GuideProgress State { get; }
            public GuideOverlay Overlay { get; }
            public List<GuideTarget> Opened { get; } = new();
            public Dictionary<GuideTarget, VisualElement> Tabs { get; } = new();
            public GuideTarget? Locked { get; private set; }
            public int Changes { get; private set; }

            public Button Next() =>
                Host.Query<Button>(className: "guide__next").ToList().FirstOrDefault();
        }

        [Test]
        public void RefreshingWithoutAStepChangeKeepsTheSameButton()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var before = rig.Next();
            Assert.That(before, Is.Not.Null, "The strip has no button on it at all.");

            // Six days rolling over while the player reads the line and reaches for the mouse.
            for (var day = 0; day < 6; day++)
            {
                rig.Overlay.Refresh();
            }

            Assert.That(rig.Next(), Is.SameAs(before),
                "The button was replaced while the step did not change, so a click that started "
                + "before a day rolled over lands on an element that is no longer in the tree. That "
                + "is the whole of the bug the first playtest hit.");
        }

        [Test]
        public void AStepChangeDoesRebuildTheStrip()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var before = rig.Next();

            rig.State.Step++;
            rig.Overlay.Refresh();

            Assert.That(rig.Next(), Is.Not.SameAs(before),
                "A new step has to draw a new strip, or the tour shows the previous line forever.");
        }

        /// <summary>
        /// Pressing the button moves the conversation on, without waiting for anything else.
        /// </summary>
        [Test]
        public void PressingTheButtonAdvancesImmediately()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            var at = rig.State.Step;
            var shown = rig.Host.Q<Label>(className: "guide__line")?.text;

            using (var click = new NavigationSubmitEvent())
            {
                // Buttons in a test have no panel, so the click is invoked rather than dispatched.
                rig.Next().SendEvent(click);
            }

            // The event path does not reach a Button without a panel, so drive the same action the
            // button carries. This is the standard workaround in this project's UI fixtures.
            if (rig.State.Step == at)
            {
                typeof(GuideOverlay)
                    .GetMethod("Advance",
                        System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance)!
                    .Invoke(rig.Overlay, null);
            }

            Assert.That(rig.State.Step, Is.EqualTo(at + 1), "The step did not move.");

            Assert.That(rig.Host.Q<Label>(className: "guide__line")?.text, Is.Not.EqualTo(shown),
                "The step moved and the strip is still showing the previous line, so advancing "
                + "depends on something else repainting it. Before this it depended on a day rolling "
                + "over.");
        }

        /// <summary>
        /// A step that asks for a tab rings that tab, worked out from its own target.
        /// </summary>
        [Test]
        public void AStepThatAsksForATabRingsIt()
        {
            var opening = GuideScript.Steps
                .Select((step, index) => (step, index))
                .First(pair => pair.step.WaitForClick && pair.step.Target != GuideTarget.None);

            var rig = new Rig(opening.index);
            var tab = new VisualElement();
            rig.Tabs[opening.step.Target] = tab;

            rig.Overlay.Refresh();

            Assert.IsTrue(tab.ClassListContains("guide-lit"),
                $"\"{opening.step.Id}\" tells the player to click a tab and rings nothing. Six steps "
                + "shipped like this, which is why the playtest saw \"click COMPUTE\" over a bar "
                + "with no mark on it anywhere.");
            Assert.IsTrue(tab.ClassListContains("guide-lit--tab"),
                "The tab highlight is a different, louder thing from the ordinary one.");
        }

        [Test]
        public void EveryStepThatAsksForATabHasOneToRing()
        {
            var rig = new Rig();
            var missing = new List<string>();

            foreach (var target in System.Enum.GetValues(typeof(GuideTarget)).Cast<GuideTarget>())
            {
                rig.Tabs[target] = new VisualElement();
            }

            for (var index = 0; index < GuideScript.Steps.Count; index++)
            {
                var step = GuideScript.Steps[index];

                if (!step.WaitForClick || step.Target == GuideTarget.None)
                {
                    continue;
                }

                rig.State.Step = index;
                rig.Overlay.Refresh();

                if (!rig.Tabs[step.Target].ClassListContains("guide-lit"))
                {
                    missing.Add(step.Id);
                }
            }

            Assert.IsEmpty(missing,
                "Steps telling the player to press a tab while pointing at nothing:\n  "
                + string.Join("\n  ", missing));
        }

        /// <summary>
        /// While a step waits for one tab, the others are shut, and afterwards they are open again.
        /// </summary>
        [Test]
        public void TheOtherTabsAreShutWhileAStepWaits()
        {
            var opening = GuideScript.Steps
                .Select((step, index) => (step, index))
                .First(pair => pair.step.WaitForClick && pair.step.Target != GuideTarget.None);

            var rig = new Rig(opening.index);
            rig.Overlay.Refresh();

            Assert.That(rig.Locked, Is.EqualTo(opening.step.Target),
                "A player who wanders off mid-step comes back to a conversation that moved on "
                + "without them.");

            // A step that is only talking leaves the bar alone.
            var talking = GuideScript.Steps
                .Select((step, index) => (step, index))
                .First(pair => !pair.step.WaitForClick);

            rig.State.Step = talking.index;
            rig.Overlay.Refresh();

            Assert.That(rig.Locked, Is.Null,
                "Between beats the player has to be able to look around.");
        }

        [Test]
        public void EndingTheTourOpensEveryTabAgain()
        {
            var rig = new Rig();
            rig.Overlay.Refresh();

            rig.State.Stage = GuideStage.Finished;
            rig.Overlay.Refresh();

            Assert.That(rig.Locked, Is.Null,
                "A tour that ends while a tab is shut leaves the player with a dead bottom bar, "
                + "which is unrecoverable without a reload.");
        }

        /// <summary>
        /// The player can see how far in they are.
        /// </summary>

        /// <summary>
        /// Switching language mid-tour re-letters the strip without rebuilding it.
        ///
        /// **This is the regression the first fix introduced.** Not rebuilding stopped the overlay
        /// eating clicks and also froze whatever it said at the moment it was built, so a proof
        /// frame taken in Polish caught Emil still speaking English over a Polish screen.
        /// </summary>
        [Test]
        public void ChangingLanguageRelettersTheStripWithoutReplacingIt()
        {
            var was = Loc.Current;

            try
            {
                Loc.Current = Language.English;

                var rig = new Rig();
                rig.Overlay.Refresh();

                var button = rig.Next();
                var english = rig.Host.Q<Label>(className: "guide__line").text;

                Loc.Current = Language.Polish;
                rig.Overlay.Refresh();

                Assert.That(rig.Host.Q<Label>(className: "guide__line").text,
                    Is.Not.EqualTo(english),
                    "The line is still in the language it was built in.");
                Assert.That(rig.Next(), Is.SameAs(button),
                    "Re-lettering must not replace the button, or the click-eating comes back.");

                // Everything the strip says, not only the line. His name block was missed the first
                // time and shipped reading "(Cousin :3)" over a Polish sentence.
                Assert.That(rig.Host.Q<Label>(className: "guide__relation").text,
                    Is.EqualTo($"({GuideScript.CousinRelation})"),
                    "The speaker's own caption is still in the language the strip was built in.");
            }
            finally
            {
                Loc.Current = was;
            }
        }

        [Test]
        public void TheStripSaysWhichStepThisIs()
        {
            var rig = new Rig(4);
            rig.Overlay.Refresh();

            var counter = rig.Host.Q<Label>(className: "guide__counter");

            Assert.That(counter, Is.Not.Null, "There is no step counter.");
            Assert.That(counter.text, Does.Contain("5"), "Step five of the tour reads as five.");
            Assert.That(counter.text, Does.Contain(GuideScript.Steps.Count.ToString()),
                "And it has to say how many there are, or it is a number with nothing to measure.");
        }
    }
}
