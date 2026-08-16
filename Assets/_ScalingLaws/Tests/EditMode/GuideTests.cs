using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The phone, the tour and the three opening tasks.
    ///
    /// **The thing being defended is that the tasks are read from the company rather than ticked by
    /// the tutorial.** A task list that marks itself complete when a panel says so can congratulate
    /// somebody for something they did not do, and goes out of step the moment a save is reloaded
    /// halfway through. Everything below builds a real company state and asks the guide what it
    /// thinks, never the other way round.
    /// </summary>
    public sealed class GuideTests
    {
        private static CompanyState NewCompany(long cash = 14_000_000)
        {
            var state = new CompanyState("Prometheus AI") { CashUsd = cash };
            state.Guide.StartingCashUsd = cash;
            return state;
        }

        private static DeployedModel AModel(CompanyState state) => new(
            "Subject", ArchitectureId.DenseTransformer, capability: 30.0,
            releaseDate: state.Date, activeParameterCount: 6.0, priceMultiplier: 1.0);

        // ---- the script ---------------------------------------------------------------------

        [Test]
        public void EveryStepSaysSomething()
        {
            foreach (var step in GuideScript.Steps)
            {
                Assert.That(step.Id, Is.Not.Empty);
                Assert.That(step.Line, Is.Not.Empty, $"{step.Id} has no line.");
            }

            Assert.That(GuideScript.Steps.Select(step => step.Id).Distinct().Count(),
                Is.EqualTo(GuideScript.Steps.Count), "Two steps share an id.");
        }

        [Test]
        public void EveryStepThatWaitsHasSomewhereToSendThePlayer()
        {
            foreach (var step in GuideScript.Steps.Where(entry => entry.WaitForClick))
            {
                Assert.That(step.Target, Is.Not.EqualTo(GuideTarget.None),
                    $"{step.Id} waits for a click and names no screen, so the button goes nowhere.");

                Assert.That(step.Prompt, Is.Not.Empty,
                    $"{step.Id} waits for a click and its button has no caption.");
            }
        }

        [Test]
        public void TheMenuHasTheItemThatLightsUp()
        {
            Assert.That(GuideScript.AutoSelectedMenuItem,
                Is.InRange(0, GuideScript.AppMenu.Count - 1),
                "The item the app selects on its own is not in the menu.");

            Assert.That(GuideScript.AppMenu[GuideScript.AutoSelectedMenuItem],
                Is.EqualTo("Messages"));
        }

        // ---- the tasks ------------------------------------------------------------------------

        [Test]
        public void ANewCompanyHasDoneNothing()
        {
            var state = NewCompany();

            foreach (var (_, _, done) in state.Guide.Tasks(state))
            {
                Assert.That(done, Is.False);
            }

            Assert.That(state.Guide.CurrentTask(state), Is.EqualTo("first_model"));
        }

        [Test]
        public void TrainingSomethingTicksTheFirstTask()
        {
            var state = NewCompany();

            state.AddToShelf(new TrainedModel("Subject", ArchitectureId.DenseTransformer, 30.0,
                state.Date, 6.0, 30.0));

            Assert.That(state.Guide.IsDone("first_model", state), Is.True,
                "A model on the shelf is a model that was created.");

            Assert.That(state.Guide.IsDone("first_release", state), Is.False,
                "Nothing has been released, so the second task must not tick with the first.");
        }

        [Test]
        public void ReleasingTicksBothOfTheFirstTwo()
        {
            var state = NewCompany();
            state.AddDeployedModel(AModel(state));

            Assert.That(state.Guide.IsDone("first_model", state), Is.True);
            Assert.That(state.Guide.IsDone("first_release", state), Is.True);
        }

        [Test]
        public void DoublingIsMeasuredFromWhereTheCompanyStarted()
        {
            var state = NewCompany(10_000_000);

            state.CashUsd = 19_999_999;
            Assert.That(state.Guide.IsDone("double_cash", state), Is.False);

            state.CashUsd = 20_000_000;
            Assert.That(state.Guide.IsDone("double_cash", state), Is.True,
                "Twice the opening balance is the whole task.");
        }

        [Test]
        public void ACompanyThatNeverStartedTheGuideCannotDoubleNothing()
        {
            var state = new CompanyState("Prometheus AI") { CashUsd = 900_000_000 };

            // StartingCash is zero because the phone never rang. Without the guard this reads as
            // "anything at least twice zero", which is every company that has ever existed.
            Assert.That(state.Guide.IsDone("double_cash", state), Is.False);
        }

        [Test]
        public void TheTaskListWalksForwardAndThenEmpties()
        {
            var state = NewCompany(10_000_000);

            Assert.That(state.Guide.CurrentTask(state), Is.EqualTo("first_model"));

            state.AddToShelf(new TrainedModel("Subject", ArchitectureId.DenseTransformer, 30.0,
                state.Date, 6.0, 30.0));

            Assert.That(state.Guide.CurrentTask(state), Is.EqualTo("first_release"));

            state.AddDeployedModel(AModel(state));
            Assert.That(state.Guide.CurrentTask(state), Is.EqualTo("double_cash"));

            state.CashUsd = 20_000_000;
            Assert.That(state.Guide.CurrentTask(state), Is.Null);
            Assert.That(state.Guide.AllTasksDone(state), Is.True);
        }

        [Test]
        public void AnUnknownTaskIsNotSilentlyComplete()
        {
            var state = NewCompany();

            Assert.That(state.Guide.IsDone("something_from_a_newer_build", state), Is.False,
                "An id this build does not know must read as not done, never as done.");
        }

        // ---- the save --------------------------------------------------------------------------

        [Test]
        public void WhereThePlayerGotToSurvivesASave()
        {
            var state = NewCompany(12_000_000);

            state.Guide.Stage = GuideStage.Touring;
            state.Guide.Step = 5;
            state.Guide.BannerDismissed = true;

            var reloaded = SaveStore.Restore(SaveStore.Capture(state));

            Assert.That(reloaded.Guide.Stage, Is.EqualTo(GuideStage.Touring));
            Assert.That(reloaded.Guide.Step, Is.EqualTo(5));
            Assert.That(reloaded.Guide.StartingCashUsd, Is.EqualTo(12_000_000L),
                "Losing the opening balance turns 'double the budget' into 'have any money'.");

            Assert.That(reloaded.Guide.BannerDismissed, Is.True);
        }

        [Test]
        public void ACompanyThatFinishedIsNotAskedAgain()
        {
            var state = NewCompany();
            state.Guide.Stage = GuideStage.Finished;

            var reloaded = SaveStore.Restore(SaveStore.Capture(state));

            Assert.That(reloaded.Guide.Stage, Is.EqualTo(GuideStage.Finished),
                "A reloaded save that reopens the tutorial is worse than one that never had it.");
        }
    }
}
