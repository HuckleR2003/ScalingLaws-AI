using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The inbox.
    ///
    /// **The wire reports and the inbox asks.** Everything here turns on that split: a letter is
    /// something waiting on the player, so the tests are mostly about whether it can actually be
    /// answered and whether ignoring it costs anything.
    ///
    /// The other claim is the one that made this worth building around tax and fines rather than
    /// around flavour: money now leaves the account **when the player says so**, which means a
    /// company can be solvent on paper in December and unable to pay in January.
    /// </summary>
    public sealed class MailTests
    {
        private static CompanySimulation Fresh(uint seed = 1000) =>
            new(new CompanyState("Adco", seed));

        private static CompanySimulation Earning(uint seed = 1001, int days = 400)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(90.0);

            simulation.State.AddDeployedModel(new DeployedModel("Atlas One",
                ArchitectureId.DenseTransformer, 46.0, simulation.State.Date, 2e10, 1.0,
                ModelType.General));

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        private static List<string> Words(VisualElement root)
        {
            var found = new List<string>();

            void Walk(VisualElement element)
            {
                switch (element)
                {
                    case Label label when !string.IsNullOrEmpty(label.text):
                        found.Add(label.text);
                        break;
                    case Button button when !string.IsNullOrEmpty(button.text):
                        found.Add(button.text);
                        break;
                }

                foreach (var child in element.Children())
                {
                    Walk(child);
                }
            }

            Walk(root);
            return found;
        }

        private static bool Says(VisualElement root, string fragment) =>
            Words(root).Exists(text => text.Contains(fragment));

        private static MailItem FirstOfKind(CompanySimulation simulation, MailKind kind)
        {
            foreach (var letter in simulation.State.Mail.All)
            {
                if (letter.Kind == kind)
                {
                    return letter;
                }
            }

            return null;
        }

        // ---- tax, once a year, out of the account -------------------------------------------------

        /// <summary>
        /// Tax used to leave the account every day, which is tidy and is not how a company
        /// experiences it. Accruing it makes January a real event.
        /// </summary>
        [Test]
        public void TaxIsAccruedAcrossTheYearAndBilledOnceInJanuary()
        {
            var simulation = Earning(1002, 400);
            var demand = FirstOfKind(simulation, MailKind.TaxDemand);

            Assert.IsNotNull(demand, "A profitable year has to produce a demand.");
            Assert.Greater(demand.AmountUsd, 0L);
            Assert.AreEqual(1, demand.Arrived.Month, "It is billed in January.");
            Assert.IsFalse(demand.IsClosed, "It arrives owed, not paid.");
        }

        [Test]
        public void NothingLeavesTheAccountUntilTheDemandIsPaid()
        {
            var simulation = Earning(1003, 400);
            var demand = FirstOfKind(simulation, MailKind.TaxDemand);
            Assert.IsNotNull(demand);

            Assert.AreEqual(0L, simulation.State.LifetimeTaxPaidUsd,
                "Nothing has been paid yet, so nothing should be recorded as paid.");

            var owed = demand.AmountUsd;
            var before = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryActOnMail(demand.Id, MailAction.Pay, out var reason), reason);

            Assert.AreEqual(before - owed, simulation.State.CashUsd);
            Assert.AreEqual(owed, simulation.State.LifetimeTaxPaidUsd);
            Assert.IsTrue(demand.IsClosed);
        }

        [Test]
        public void ADemandTheCompanyCannotAffordIsRefusedWithTheNumbers()
        {
            var simulation = Fresh(1004);
            var demand = simulation.State.Mail.Add(MailKind.Fine, simulation.State.Date,
                "Regulator", "Penalty", "Pay up.");

            demand.AmountUsd = simulation.State.CashUsd + 1_000_000L;
            demand.DueDayIndex = simulation.State.Date.DayIndex + 30;

            Assert.IsFalse(simulation.TryActOnMail(demand.Id, MailAction.Pay, out var reason));
            Assert.IsTrue(reason.Contains("in the account"), reason);
            Assert.IsFalse(demand.IsClosed, "A refused payment must leave the letter open.");
        }

        /// <summary>
        /// A penalty that is a fixed fee is a price a rich company happily pays to stop thinking
        /// about the letter. It has to grow, and it has to cost standing as well as money.
        /// </summary>
        [Test]
        public void IgnoringADemandMakesItGrowAndCostsStanding()
        {
            var simulation = Fresh(1005);
            var demand = simulation.State.Mail.Add(MailKind.Fine, simulation.State.Date,
                "Regulator", "Penalty", "Pay up.");

            demand.AmountUsd = 1_000_000L;
            demand.DueDayIndex = simulation.State.Date.DayIndex + 5;

            var standingBefore = simulation.State.Reputation;

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.Greater(demand.AmountUsd, 1_000_000L, "An ignored demand has to grow.");
            Assert.Less(simulation.State.Reputation, standingBefore,
                "And being publicly in arrears has to cost more than money.");
        }

        [Test]
        public void TheTotalOwedIsWhatIsStillOpen()
        {
            var simulation = Fresh(1006);
            var one = simulation.State.Mail.Add(MailKind.Fine, simulation.State.Date, "A", "x", "y");
            one.AmountUsd = 500L;

            var two = simulation.State.Mail.Add(MailKind.Fine, simulation.State.Date, "B", "x", "y");
            two.AmountUsd = 700L;

            Assert.AreEqual(1200L, simulation.State.Mail.OwedUsd);

            two.IsClosed = true;
            Assert.AreEqual(500L, simulation.State.Mail.OwedUsd,
                "A settled letter is not still owed.");
        }

        // ---- incidents arrive as letters --------------------------------------------------------------

        /// <summary>
        /// The specific reason incidents read as a bug: a fine the player never saw was
        /// indistinguishable from the market turning against them.
        /// </summary>
        [Test]
        public void AnIncidentFineArrivesAsADemandRatherThanVanishingFromTheAccount()
        {
            var simulation = Earning(1007, 5);
            var before = simulation.State.CashUsd;

            // Drive the incident path directly rather than waiting for a rare roll.
            simulation.State.Mail.Add(MailKind.Fine, simulation.State.Date, "Regulator",
                "Penalty notice", "An inquiry has concluded.").AmountUsd = 4_000_000L;

            Assert.AreEqual(before, simulation.State.CashUsd,
                "A demand does not take the money by itself. That is the whole point of it.");

            var fine = FirstOfKind(simulation, MailKind.Fine);
            Assert.IsNotNull(fine);
            Assert.AreEqual(4_000_000L, fine.AmountUsd);
        }

        // ---- hiring by letter --------------------------------------------------------------------------

        [Test]
        public void SomebodyEventuallyWritesInLookingForWork()
        {
            var simulation = Earning(1008, 400);

            Assert.IsNotNull(FirstOfKind(simulation, MailKind.JobOffer),
                "Over a year nobody applied, so the inbox has nothing in it but bills.");
        }

        [Test]
        public void AnApplicantAsksAboveTheGoingRate()
        {
            var simulation = Earning(1009, 400);
            var offer = FirstOfKind(simulation, MailKind.JobOffer);
            Assert.IsNotNull(offer);

            var going = StaffCatalog.Get(offer.Role).SalaryPerYearUsd(offer.Skill);

            Assert.Greater(offer.AskingSalaryUsd, going,
                "Somebody who writes to you first thinks they are worth more than the market says. "
                + "If they asked the going rate there would be nothing to negotiate.");
        }

        [Test]
        public void AcceptingAnApplicantHiresThem()
        {
            var simulation = Earning(1010, 400);

            // Somewhere to put them. Applications arrive at the house too, deliberately, but
            // accepting one there is refused and that is a different test.
            Assert.IsTrue(simulation.TryMoveOffice(OfficeTier.Loft, out var moveReason), moveReason);

            var offer = FirstOfKind(simulation, MailKind.JobOffer);
            Assert.IsNotNull(offer);

            var before = simulation.State.Staff.Headcount;

            if (!simulation.TryActOnMail(offer.Id, MailAction.Accept, out var reason))
            {
                Assert.Fail($"Could not accept: {reason}");
            }

            Assert.AreEqual(before + 1, simulation.State.Staff.Headcount);
            Assert.IsTrue(offer.IsClosed);
        }

        /// <summary>
        /// People apply to companies that are full, and the letter says so.
        ///
        /// The application used to be suppressed when there was no free desk, which removed the one
        /// signal that makes the move to an office worth its rent: somebody wants to work here and
        /// there is nowhere to put them.
        /// </summary>
        [Test]
        public void SomebodyStillAppliesWhenThereIsNowhereToSeatThem()
        {
            var simulation = Earning(1019, 400);
            Assert.AreEqual(0, simulation.State.Staff.Desks, "This test needs the house.");

            var offer = FirstOfKind(simulation, MailKind.JobOffer);
            Assert.IsNotNull(offer, "Nobody applied, so the player is never told they need room.");

            Assert.IsTrue(offer.Body.Contains("nowhere for them to sit"),
                "The letter has to say there is no room, rather than letting the player find out "
                + "when the button refuses.");

            Assert.IsFalse(simulation.TryActOnMail(offer.Id, MailAction.Accept, out var reason));
            Assert.IsTrue(reason.Contains("desk"), reason);
        }

        [Test]
        public void HagglingEitherLowersTheAskOrLosesThemAndNeverBoth()
        {
            var simulation = Earning(1011, 400);
            var offer = FirstOfKind(simulation, MailKind.JobOffer);
            Assert.IsNotNull(offer);

            var asked = offer.AskingSalaryUsd;
            var took = simulation.TryActOnMail(offer.Id, MailAction.Haggle, out _);

            if (took)
            {
                Assert.Less(offer.AskingSalaryUsd, asked, "A successful counter has to be cheaper.");
                Assert.IsFalse(offer.IsClosed, "They are still available at the new price.");
            }
            else
            {
                Assert.IsTrue(offer.IsClosed, "Losing them closes the letter.");
                Assert.IsTrue(offer.Outcome.Contains("Walked"));
            }
        }

        [Test]
        public void NobodyHagglesTwice()
        {
            var simulation = Earning(1012, 400);
            var offer = FirstOfKind(simulation, MailKind.JobOffer);
            Assert.IsNotNull(offer);

            simulation.TryActOnMail(offer.Id, MailAction.Haggle, out _);

            Assert.IsFalse(simulation.TryActOnMail(offer.Id, MailAction.Haggle, out var reason),
                "A second counter would make haggling free and therefore automatic.");

            Assert.IsTrue(reason.Length > 0);
        }

        [Test]
        public void ADeclinedLetterAsksForNothingMore()
        {
            var simulation = Earning(1013, 400);
            var offer = FirstOfKind(simulation, MailKind.JobOffer);
            Assert.IsNotNull(offer);

            Assert.IsTrue(simulation.TryActOnMail(offer.Id, MailAction.Decline, out _));
            Assert.IsTrue(offer.IsClosed);
            Assert.IsEmpty(offer.Actions, "A closed letter offers nothing.");
        }

        // ---- the screen ------------------------------------------------------------------------------------

        [Test]
        public void TheScreenShowsALetterAndTheWayToAnswerIt()
        {
            var simulation = Earning(1014, 400);
            var demand = FirstOfKind(simulation, MailKind.TaxDemand);
            Assert.IsNotNull(demand);

            var screen = new MailScreen(simulation, () => { });
            screen.Select(demand.Id);

            Assert.IsTrue(Says(screen.Root, "Corporation tax"), "The letter is not on screen.");
            Assert.IsTrue(Says(screen.Root, "AMOUNT DUE"), "No amount block.");
            Assert.IsTrue(Says(screen.Root, "PAY"), "No way to pay it.");
        }

        [Test]
        public void PayingFromTheScreenActuallyPaysIt()
        {
            var simulation = Earning(1015, 400);
            var demand = FirstOfKind(simulation, MailKind.TaxDemand);
            Assert.IsNotNull(demand);

            var screen = new MailScreen(simulation, () => { });
            screen.Select(demand.Id);
            screen.Act(demand.Id, MailAction.Pay);

            Assert.IsTrue(demand.IsClosed, "The button has to reach the simulation.");
            Assert.IsTrue(Says(screen.Root, "Paid"), "And the screen has to say it happened.");
        }

        [Test]
        public void OpeningALetterMarksItRead()
        {
            var simulation = Earning(1016, 400);
            var demand = FirstOfKind(simulation, MailKind.TaxDemand);
            Assert.IsNotNull(demand);
            Assert.IsFalse(demand.IsRead);

            new MailScreen(simulation, () => { }).Select(demand.Id);
            Assert.IsTrue(demand.IsRead);
        }

        [Test]
        public void AnEmptyInboxSaysSoRatherThanRenderingNothing()
        {
            var screen = new MailScreen(Fresh(1017), () => { });
            Assert.DoesNotThrow(() => screen.Refresh());
            Assert.IsTrue(Says(screen.Root, "Nothing yet"));
        }

        // ---- persistence -------------------------------------------------------------------------------------

        [Test]
        public void TheInboxAndTheAccrualSurviveASave()
        {
            var simulation = Earning(1018, 400);
            var demand = FirstOfKind(simulation, MailKind.TaxDemand);
            Assert.IsNotNull(demand);

            var owed = demand.AmountUsd;
            var accrued = simulation.State.AccruedTaxUsd;

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(simulation.State.Mail.All.Count, restored.Mail.All.Count,
                "Losing the inbox on load would forgive every debt in it.");

            Assert.IsTrue(restored.Mail.TryGet(demand.Id, out var back));
            Assert.AreEqual(owed, back.AmountUsd);
            Assert.AreEqual(demand.DueDayIndex, back.DueDayIndex);

            Assert.AreEqual(accrued, restored.AccruedTaxUsd,
                "The accrual is causal: January reads it. Dropping it changes the next bill.");
        }

        [Test]
        public void AnOlderSaveStartsWithAnEmptyInboxAndNoAccrual()
        {
            var upgraded = SaveMigration.UpgradeV23ToV24(new SaveData { version = 23 });

            Assert.AreEqual(24, upgraded.version);
            Assert.IsEmpty(upgraded.mail);
            Assert.AreEqual(0L, upgraded.accruedTaxUsd,
                "A v23 company paid its tax daily as it went, so carrying a balance forward would "
                + "bill it a second time for a year it has already settled.");
        }
    }
}
