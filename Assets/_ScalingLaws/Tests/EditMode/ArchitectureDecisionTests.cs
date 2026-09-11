using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The architecture step is a decision the player makes, not a screen they watch.
    ///
    /// **Reported: he explains the house family, fills the sliders in, and walks on.** The player is
    /// left looking at a page they have just been told decides the next five years of every model
    /// they build, having done nothing with it, and the tour never comes back. Everything about the
    /// screen had been said and nothing about it had been settled.
    ///
    /// Two answers now, and the interesting half is that there is still only one step behind them:
    /// the step that follows waits for the programme and clears itself when there is nothing to wait
    /// for, so declining walks straight through with no branch anywhere in the script.
    /// </summary>
    public sealed class ArchitectureDecisionTests
    {
        /// <summary>
        /// The shape of the decision, read off the script.
        ///
        /// **This is the ratchet for the walk-on.** A step that offers something and then advances
        /// to whatever came next is exactly what shipped, and it reads as finished.
        /// </summary>
        [Test]
        public void TheOfferAsksAQuestionAndTheStepAfterItWaitsForTheAnswer()
        {
            var at = GuideScript.IndexOf(GuideScript.ArchitectureOfferStepId);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "The offer step is not in the script.");

            var offer = GuideScript.Steps[at];

            Assert.That(offer.Signal, Is.Null.Or.Empty,
                "The offer step waits on a signal, so it draws no buttons at all and the offer "
                + "cannot be answered.");

            Assert.That(offer.Prompt, Is.Not.Null.And.Not.Empty,
                "There is no caption on the other answer, so the choice is 'do it' against a "
                + "button saying NEXT, which does not read as a decision.");

            Assert.That(at + 1, Is.LessThan(GuideScript.Steps.Count),
                "The offer is the last step in the tour, so nothing waits for the programme.");

            Assert.That(GuideScript.Steps[at + 1].Signal,
                Is.EqualTo(GuideScript.ArchitectureBuiltSignal),
                "The step after the offer does not wait for the family, so a player who said yes "
                + "is walked past the thing they have just commissioned.");
        }

        /// <summary>
        /// Both answers are on screen, and they are different things to press.
        /// </summary>
        [Test]
        public void TheStepDrawsBothAnswers()
        {
            var host = new VisualElement();
            var progress = new GuideProgress
            {
                Stage = GuideStage.Touring,
                Step = GuideScript.IndexOf(GuideScript.ArchitectureOfferStepId)
            };

            var overlay = new GuideOverlay(host, () => progress, _ => { }, () => { })
            {
                offerFor = step => step.Id == GuideScript.ArchitectureOfferStepId
                    ? new GuideOverlay.GuideOffer("BUILD IT NOW", () => { })
                    : null
            };

            overlay.Refresh();

            var accept = host.Query<Button>(className: "guide__offer").ToList().FirstOrDefault();
            var decline = host.Query<Button>(className: "guide__next").ToList().FirstOrDefault();

            Assert.That(accept, Is.Not.Null, "Nothing on screen starts the programme.");
            Assert.That(decline, Is.Not.Null, "There is no way past the offer without taking it.");

            Assert.That(decline.text, Is.EqualTo(Loc.T("guide.offer.arch_not")),
                "The way out still says NEXT, so declining reads as skipping ahead rather than as "
                + "an answer to what he just asked.");
        }

        /// <summary>
        /// **The work a branch would otherwise have done.** A player who declines has nothing in
        /// flight, so the waiting step is satisfied the moment it is reached and unwinds by itself.
        /// Without that the tour parks forever on a bar waiting for a programme nobody started.
        ///
        /// Driven by moving the step rather than by pressing the button: an EditMode element has no
        /// panel, so a click sent to a button is never dispatched. What is measured is the arrival,
        /// which is where the skip lives.
        /// </summary>
        [Test]
        public void DecliningWalksStraightThroughTheWaitingStep()
        {
            var host = new VisualElement();
            var at = GuideScript.IndexOf(GuideScript.ArchitectureOfferStepId);
            var progress = new GuideProgress { Stage = GuideStage.Touring, Step = at };

            // What the shell answers for a company with no programme running.
            var overlay = new GuideOverlay(host, () => progress, _ => { }, () => { },
                alreadyDone: signal => signal == GuideScript.ArchitectureBuiltSignal);

            overlay.Refresh();

            // Where NEXT puts them.
            progress.Step = at + 1;
            overlay.Refresh();

            Assert.That(progress.Step, Is.GreaterThan(at + 1),
                "The tour is sitting on the step that waits for the family after the player said "
                + "no, so it is waiting for something that is never going to happen.");
        }

        /// <summary>
        /// **The other half: saying yes has to hold him there.** A step that clears itself whatever
        /// the answer is the same walk-on with a progress bar drawn on it.
        /// </summary>
        [Test]
        public void SayingYesLeavesSomethingToWaitFor()
        {
            var simulation = Funded();
            var panel = new ArchitectureCreatorPanel(simulation);

            panel.TakeTheAdvice();
            Assert.That(panel.CommitNow(out var why), Is.True, why);

            Assert.That(simulation.State.ActiveArchitectureProject, Is.Not.Null,
                "Nothing is running, so the step ahead clears itself and the player is never told "
                + "the family landed. The offer would be a button that says something and does "
                + "nothing, which is what it was.");
        }

        /// <summary>
        /// The number on the button is the number the calendar runs for.
        ///
        /// **`ArchitectureDesigner.DurationDays` is not that number**, and this screen quoted it:
        /// the founder, the research staff and the home country all move a programme's length
        /// through `ScaleResearchDuration`, and it is the scaled figure that reaches the project.
        /// A button offering to spend a year of the company on a figure that is out by a seventh is
        /// worse than a button with no figure on it at all.
        /// </summary>
        [Test]
        public void TheQuoteIsTheLengthTheProgrammeActuallyRunsFor()
        {
            var simulation = Funded();
            var panel = new ArchitectureCreatorPanel(simulation);
            panel.TakeTheAdvice();

            var quoted = panel.ProgrammeDurationDays();
            var designed = ArchitectureDesigner.DurationDays(panel.Blueprint);

            // Pinned, because the gap is the whole point and a campaign where the two agree would
            // pass this test while measuring nothing. A default company sits in the United States,
            // whose innovation multiplier is 1.15, and the duration slider opens at 365.
            Assert.That(designed, Is.EqualTo(365), "the duration slider no longer opens at a year");

            Assert.That(quoted, Is.EqualTo(317),
                "The scaling changed, which is fine, but the figures quoted in CHANGELOG.md came "
                + "from here and no longer describe the game.");

            Assert.That(panel.CommitNow(out var why), Is.True, why);

            Assert.That(simulation.State.ActiveArchitectureProject.DurationDays,
                Is.EqualTo(quoted),
                "The screen quoted " + quoted + " days and the programme runs for "
                + simulation.State.ActiveArchitectureProject.DurationDays + ", so it is reading "
                + "the unscaled figure the designer works in rather than the one the company has "
                + "to live through.");
        }

        /// <summary>
        /// The advice moves the five directions and leaves the length alone, which is what lets the
        /// offer quote a duration before it has applied anything.
        /// </summary>
        [Test]
        public void TakingTheAdviceDoesNotChangeWhatTheOfferQuoted()
        {
            var simulation = Funded();
            var panel = new ArchitectureCreatorPanel(simulation);

            var before = panel.ProgrammeDurationDays();
            panel.TakeTheAdvice();

            Assert.That(panel.ProgrammeDurationDays(), Is.EqualTo(before),
                "Taking the advice changed how long the programme takes, so the figure on the "
                + "button is a quote for a plan that no longer exists by the time it is pressed.");
        }

        /// <summary>
        /// A company that cannot pay says so and starts nothing, rather than half-starting.
        /// </summary>
        [Test]
        public void RefusingLeavesNothingRunningAndSaysWhy()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 1L;

            var panel = new ArchitectureCreatorPanel(simulation);
            panel.TakeTheAdvice();

            Assert.That(panel.CommitNow(out var why), Is.False,
                "A company with a dollar in the bank commissioned a family programme.");

            Assert.That(why, Is.Not.Null.And.Not.Empty, "It refused and said nothing.");

            Assert.That(simulation.State.ActiveArchitectureProject, Is.Null,
                "It reported a failure and started the programme anyway.");
        }

        private static CompanySimulation Funded()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 400_000_000L;
            simulation.SetRentedPetaflops(400.0);
            return simulation;
        }
    }
}
