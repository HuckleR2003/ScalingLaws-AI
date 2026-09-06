using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The two ways the tour used to stop dead, and the ratchet for both.
    ///
    /// **A signal is an event, and an event that arrives one step early is gone.** `Reported` only
    /// moves the step that is on screen when it fires. A player who releases their first model while
    /// the tour is still explaining how to release one has spent the only `model_released` this
    /// campaign will ever raise; the next step then waits for it forever, and a step with a signal
    /// on it draws no NEXT. That is the tour dying at 44 of 57.
    ///
    /// **A gift is worthless until something redraws the page it landed on.** The favour is handed
    /// over by the tour, and the tour's own repaint is the chrome, not the open screen. A player
    /// standing on RESEARCH when the favour arrives kept looking at a tree priced against points
    /// they had already been given, until a day rolled over. Which reads as "come back tomorrow".
    ///
    /// Both are one shape: the tour changed something and nobody asked the screen to notice.
    /// </summary>
    public sealed class GuideSignalTests
    {
        private sealed class Rig
        {
            public Rig(int step, Func<string, bool> alreadyDone = null)
            {
                Host = new VisualElement();
                State = new GuideProgress { Stage = GuideStage.Touring, Step = step };

                Overlay = new GuideOverlay(Host, () => State, target => Opened.Add(target),
                    () => Changes++, null, null, null,
                    () => Repaints++,
                    alreadyDone);
            }

            public VisualElement Host { get; }
            public GuideProgress State { get; }
            public GuideOverlay Overlay { get; }
            public List<GuideTarget> Opened { get; } = new();
            public int Changes { get; private set; }

            /// <summary>How many times the tour asked for the open screen to be rebuilt.</summary>
            public int Repaints { get; private set; }
        }

        /// <summary>Where in the script the one step waiting for this signal sits.</summary>
        private static int StepWaitingFor(string signal)
        {
            var at = GuideScript.Steps
                .Select((step, index) => (step, index))
                .Where(pair => pair.step.Signal == signal)
                .Select(pair => pair.index)
                .ToList();

            Assert.That(at, Has.Count.EqualTo(1),
                "the script has " + at.Count + " steps waiting for '" + signal + "', and this "
                + "fixture is written around there being exactly one");

            return at[0];
        }

        [Test]
        public void AStepWaitingForSomethingAlreadyDoneLetsTheTourThrough()
        {
            var waiting = StepWaitingFor(GuideScript.ModelReleasedSignal);
            var rig = new Rig(waiting, signal => signal == GuideScript.ModelReleasedSignal);

            rig.Overlay.Refresh();

            Assert.That(rig.State.Step, Is.GreaterThan(waiting),
                "The player released a model one step early, so the event fired against the wrong "
                + "step and was dropped. Waiting for a second one is waiting for something that "
                + "cannot happen again, and this step draws no NEXT to escape with.");
        }

        [Test]
        public void AStepWaitingForSomethingStillUndoneStaysOnScreen()
        {
            var waiting = StepWaitingFor(GuideScript.RunFinishedSignal);
            var rig = new Rig(waiting, _ => false);

            rig.Overlay.Refresh();

            Assert.That(rig.State.Step, Is.EqualTo(waiting),
                "Skipping a step whose work has genuinely not been done would hand the player the "
                + "next instruction for a company that is not in the state it describes.");
        }

        /// <summary>
        /// A step that waits for nothing is never skipped, however generous the answer.
        ///
        /// Most of the fifty seven steps are read-and-press-NEXT. Consulting the company about those
        /// would run the whole tour past a player who has released one model.
        /// </summary>
        [Test]
        public void AStepThatWaitsForNothingIsNeverSkipped()
        {
            var plain = GuideScript.Steps
                .Select((step, index) => (step, index))
                .First(pair => string.IsNullOrEmpty(pair.step.Signal)).index;

            var rig = new Rig(plain, _ => true);

            rig.Overlay.Refresh();

            Assert.That(rig.State.Step, Is.EqualTo(plain));
        }

        /// <summary>
        /// Answering yes to everything walks the tour forward and stops at the first step that is
        /// not waiting for anything.
        ///
        /// `Advance` calls `Refresh`, so a run of already-satisfied steps unwinds in one pass rather
        /// than needing a press each. The guard is that it terminates: the index rises every time
        /// and a plain step ends it.
        /// </summary>
        [Test]
        public void AVeteranWalkingInLateIsNotRunToTheEndOfTheScript()
        {
            var waiting = StepWaitingFor(GuideScript.RunStartedSignal);
            var rig = new Rig(waiting, _ => true);

            rig.Overlay.Refresh();

            Assert.That(rig.State.Step, Is.GreaterThan(waiting));

            Assert.That(rig.State.Stage, Is.EqualTo(GuideStage.Touring),
                "Running off the end of the script would end the tutorial for a player who has done "
                + "one of the three things it asks for.");

            Assert.That(GuideScript.Steps[rig.State.Step].Signal, Is.Null.Or.Empty,
                "It stopped on a step still waiting for something, which means the walk did not "
                + "come to rest anywhere the player can read.");
        }

        /// <summary>
        /// The favour redraws the page it lands on, once.
        ///
        /// `GrantGiftsUpTo` has returned "did anything change" since it was written and the caller
        /// threw it away. The player standing on RESEARCH when the offer arrives is the case: the
        /// tree was built a moment earlier with a price on every node.
        /// </summary>
        [Test]
        public void TheGiftRedrawsThePageItLandsOn()
        {
            var gift = GuideScript.IndexOf(GuideScript.GiftStepId);
            Assert.That(gift, Is.GreaterThanOrEqualTo(0), "the script has no gift step in it");

            var rig = new Rig(gift);

            rig.Overlay.HandOverAnythingOwed();

            Assert.That(rig.State.FreeResearchOwed, Is.True, "the favour was not handed over at all");

            Assert.That(rig.Repaints, Is.EqualTo(1),
                "Nothing asked the open screen to rebuild, so a research tree already drawn keeps "
                + "its prices until a day rolls over. That is the twenty four hour wait the author "
                + "reported, and it is not a wait, it is a stale page.");
        }

        [Test]
        public void NothingIsRedrawnWhenThereIsNothingToHandOver()
        {
            var gift = GuideScript.IndexOf(GuideScript.GiftStepId);
            var rig = new Rig(gift);

            rig.Overlay.HandOverAnythingOwed();
            rig.Overlay.HandOverAnythingOwed();
            rig.Overlay.HandOverAnythingOwed();

            Assert.That(rig.Repaints, Is.EqualTo(1),
                "The favour is granted once. Repainting on every pass would rebuild the open screen "
                + "under the player's cursor every day that rolls over, which is the bug the "
                + "overlay's own fixture exists to prevent.");
        }

        /// <summary>
        /// Pressing the lit door carries the tour through the step that was about to ask for it.
        ///
        /// **Reported at 26 of 57.** The step describing the model hub rings the NEW MODEL door and
        /// the step after it is the one waiting for that door to be clicked. A player who read the
        /// line and pressed the lit button was told to press NEXT, and then told to click a door
        /// they were already through.
        /// </summary>
        [Test]
        public void PressingTheDoorEarlyCarriesTheTourThroughTheStepThatAsksForIt()
        {
            var asks = GuideScript.Steps
                .Select((step, index) => (step, index))
                .First(pair => pair.step.WaitForClick && pair.index > 0
                    && !GuideScript.Steps[pair.index - 1].WaitForClick);

            var describing = asks.index - 1;
            var rig = new Rig(describing);

            rig.Overlay.Refresh();
            rig.Overlay.PlayerOpened(asks.step.Target);

            Assert.That(rig.State.Step, Is.EqualTo(asks.index + 1),
                "The player did what the next step was about to ask and the tour made them press "
                + "NEXT and then asked for it anyway.");
        }

        /// <summary>
        /// One step of lookahead and no more.
        ///
        /// A screen change two steps ahead of the tour is a player wandering off, not a player
        /// keeping up, and skipping to meet them would take whole acts with it.
        /// </summary>
        [Test]
        public void ItDoesNotRunAheadByMoreThanTheOneStep()
        {
            var asks = GuideScript.Steps
                .Select((step, index) => (step, index))
                .First(pair => pair.step.WaitForClick && pair.index > 1
                    && !GuideScript.Steps[pair.index - 1].WaitForClick
                    && !GuideScript.Steps[pair.index - 2].WaitForClick);

            var twoBack = asks.index - 2;
            var rig = new Rig(twoBack);

            rig.Overlay.Refresh();
            rig.Overlay.PlayerOpened(asks.step.Target);

            Assert.That(rig.State.Step, Is.EqualTo(twoBack).Or.EqualTo(twoBack + 1),
                "Opening a screen two steps early walked the tour to it, so a stray click can carry "
                + "a player past lines they have not read.");
        }

        /// <summary>
        /// The shell hands the tour both answers.
        ///
        /// Both are optional constructor arguments, so a shell that quietly stopped passing them
        /// would compile, pass every test above, and ship the two bugs back. Source-read for the
        /// same reason `ReachabilityTests` is: it proves the wiring is written, and the fixtures
        /// above prove what it does once it is.
        /// </summary>
        [Test]
        public void TheShellAnswersBothQuestionsTheTourAsks()
        {
            var source = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "UI", "GameShell.cs"));

            Assert.That(source, Does.Contain("AlreadyDone"),
                "nobody answers 'has this already been done', so the tour is back to latching on "
                + "events it can miss");

            Assert.That(source, Does.Contain("() => Show(current)"),
                "the tour has no way to redraw the page a gift lands on");
        }
    }
}
