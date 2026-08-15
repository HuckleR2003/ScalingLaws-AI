using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The five days between being caught and finding out.
    ///
    /// **The penalty used to arrive the same tick the incident did.** One moment the company was
    /// fine, the next there was a nine figure demand in the inbox, and the player had no moment in
    /// between. The outcome is decided either way; these five days are the whole difference between
    /// a hard game and an arbitrary one.
    /// </summary>
    public sealed class RegulatoryActionTests
    {
        private static SafetyIncident Serious(GameDate on) => new(
            IncidentSeverity.Severe, on, "Personal data was reachable from a public endpoint.",
            reputationLoss: 0.10, fineUsd: 90_000_000, forcedWithdrawal: false);

        [Test]
        public void AnInspectionRunsForFiveDaysAndThenCloses()
        {
            var action = new RegulatoryAction(Serious(GameDate.Start), GameDate.Start, "Muse");

            for (var day = 0; day < RegulatoryAction.InspectionDays; day++)
            {
                Assert.IsFalse(action.IsClosed, $"Closed early on day {day}.");
                Assert.Greater(action.DaysLeft, 0);
                action.Advance();
            }

            Assert.IsTrue(action.IsClosed);
            Assert.AreEqual(0, action.DaysLeft);
            Assert.AreEqual(1.0, action.Progress, 1e-9);
        }

        [Test]
        public void TheBarNeverRunsPastTheEnd()
        {
            var action = new RegulatoryAction(Serious(GameDate.Start), GameDate.Start, "Muse");

            for (var day = 0; day < 40; day++)
            {
                action.Advance();
            }

            Assert.AreEqual(RegulatoryAction.InspectionDays, action.DaysElapsed);
            Assert.AreEqual(1.0, action.Progress, 1e-9);
        }

        [Test]
        public void TheBannerNamesTheModelUnderInspection()
        {
            var action = new RegulatoryAction(Serious(GameDate.Start), GameDate.Start, "Muse 3");
            StringAssert.Contains("Muse 3", action.Subtitle);
        }

        // ---- the money does not move until the file closes ---------------------------------------

        [Test]
        public void NoPenaltyIsTakenWhileTheInspectionIsOpen()
        {
            var simulation = Caught(out var cashAtOpen);

            Assert.IsNotNull(simulation.State.PendingAction, "Nothing opened.");

            // Four days. The verdict lands on the fifth and not before.
            for (var day = 0; day < RegulatoryAction.InspectionDays - 1; day++)
            {
                simulation.State.CashUsd = cashAtOpen;
                simulation.Advance(1);
            }

            var demands = 0;
            foreach (var letter in simulation.State.Mail.All)
            {
                if (letter.Kind == MailKind.Fine)
                {
                    demands++;
                }
            }

            Assert.AreEqual(0, demands,
                "A demand arrived while the regulator was still reading. The five days mean nothing "
                + "if the money moves on day one.");
        }

        [Test]
        public void OpeningAFileIsAnnouncedBeforeAnythingIsDecided()
        {
            var simulation = Caught(out _);

            var told = false;
            foreach (var letter in simulation.State.Mail.All)
            {
                if (letter.Subject.Contains("Inspection opened"))
                {
                    told = true;
                    StringAssert.Contains("No penalty has been decided", letter.Body,
                        "The letter has to say the outcome is still open, or it reads as the fine.");
                }
            }

            Assert.IsTrue(told, "The company was never told a file had been opened.");
        }

        [Test]
        public void TheFileClosesOneWayOrTheOther()
        {
            var simulation = Caught(out var cash);

            for (var day = 0; day < RegulatoryAction.InspectionDays + 2; day++)
            {
                simulation.State.CashUsd = cash;
                simulation.Advance(1);
            }

            Assert.IsNull(simulation.State.PendingAction, "The inspection never closed.");

            var resolved = simulation.State.Incidents.Count > 0;
            foreach (var letter in simulation.State.Mail.All)
            {
                resolved |= letter.Subject.Contains("No further action");
            }

            Assert.IsTrue(resolved,
                "It closed and neither a penalty nor a reprieve reached the player, which is the "
                + "worst of both: the banner vanished and nothing happened.");
        }

        [Test]
        public void OnlyOneInspectionRunsAtATime()
        {
            var simulation = Caught(out var cash);
            var first = simulation.State.PendingAction;

            simulation.State.CashUsd = cash;
            simulation.Advance(1);

            Assert.AreSame(first, simulation.State.PendingAction,
                "A second file opened over the first, which is two verdicts on one model.");
        }

        // ---- reloading must not be an escape -------------------------------------------------------

        [Test]
        public void AnOpenInspectionSurvivesASaveBecauseTheVerdictIsStillUnrolled()
        {
            // **This is the one that matters.** The roll happens when the file closes, so dropping
            // the inspection on save would let a player reload their way out of every penalty in
            // the game.
            var simulation = Caught(out var cash);
            simulation.State.CashUsd = cash;
            simulation.Advance(2);

            var open = simulation.State.PendingAction;
            Assert.IsNotNull(open);

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.IsNotNull(restored.PendingAction, "The file was dropped on save.");
            Assert.AreEqual(open.DaysElapsed, restored.PendingAction.DaysElapsed,
                "It reopened at day zero, which is a free extension.");

            Assert.AreEqual(open.Incident.FineUsd, restored.PendingAction.Incident.FineUsd,
                "The penalty it was deciding about was not carried.");
        }

        [Test]
        public void ACampaignFromBeforeThisHasNoOpenFile()
        {
            var old = new SaveData { version = 27 };
            var moved = SaveMigration.UpgradeV27ToV28(old);

            Assert.AreEqual(28, moved.version);
            Assert.IsFalse(moved.actionOpen,
                "Inventing an inspection would fine somebody for a run that already finished.");
        }

        /// <summary>A company with a model on sale and a file already open on it.</summary>
        private static CompanySimulation Caught(out long cashAtOpen)
        {
            var state = new CompanyState("Prometheus AI");
            var simulation = new CompanySimulation(state);

            state.AddDeployedModel(new DeployedModel(
                "Muse", ArchitectureId.DenseTransformer, 45, state.Date, 2e10, 1.0,
                ModelType.General, "Muse"));

            state.PendingAction = new RegulatoryAction(Serious(state.Date), state.Date, "Muse");
            state.CashUsd = 500_000_000;
            cashAtOpen = state.CashUsd;

            // The letter the opener would have sent, so the tests that read the mail have it.
            state.Mail.Add(MailKind.Notice, state.Date, "Regulator",
                "Inspection opened: Severe",
                "No penalty has been decided. Findings are expected within five days.");

            return simulation;
        }
    }
}
