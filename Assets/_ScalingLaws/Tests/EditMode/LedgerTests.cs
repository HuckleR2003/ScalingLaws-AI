using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The books.
    ///
    /// One claim matters more than all the others here: the report and the bank balance must be the
    /// same arithmetic. A report that recomputes its own totals is a second copy of the sums and will
    /// eventually tell the player something the cash does not agree with.
    /// </summary>
    public sealed class LedgerTests
    {
        private static CompanySimulation RunFor(int days, uint seed = 71)
        {
            var simulation = new CompanySimulation(new CompanyState("Bookco", seed));
            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        /// <summary>
        /// The test the whole design exists to pass. Every cash movement is posted, so the ledger's
        /// running total has to reconstruct the balance exactly.
        /// </summary>
        [Test]
        public void TheBooksAddUpToTheBankBalance()
        {
            var simulation = RunFor(900);
            var state = simulation.State;

            var reconstructed = CompanyState.StartingCashUsd + state.Ledger.TotalCashFlowUsd;

            Assert.AreEqual(state.CashUsd, reconstructed,
                $"The bank says {state.CashUsd:N0} and the books say {reconstructed:N0}. Money moved "
                + "somewhere that did not post a reason, so the report cannot explain the balance.");
        }

        [Test]
        public void DepreciationIsRecordedAndDeliberatelyNotCash()
        {
            var simulation = RunFor(700);
            var info = Ledger.Info(LedgerLine.Depreciation);

            Assert.IsFalse(info.IsCash,
                "Depreciation is real and it is not cash. Counting it in the cash total would stop "
                + "the report adding up to the balance.");

            Assert.IsFalse(info.IsIncome);
        }

        [Test]
        public void ACostCannotBePostedAsIncomeByAMistakeAtTheCallSite()
        {
            var state = new CompanyState("Signco", 5);
            var before = state.CashUsd;

            // Posted negative on purpose. Whether a line adds or subtracts belongs to the line, not to
            // whoever calls it, so a sign slip at one of fourteen call sites cannot invert a cost.
            state.PostCash(LedgerLine.Salaries, -50_000L);

            Assert.AreEqual(before - 50_000L, state.CashUsd, "A salary must always leave the account.");
            Assert.AreEqual(50_000L, state.Ledger.MonthTotal(
                Ledger.MonthKeyOf(state.Date), LedgerLine.Salaries));
        }

        [Test]
        public void IncomeAndCostsAreCountedOnTheirOwnSides()
        {
            var state = new CompanyState("Sideco", 9);
            var month = Ledger.MonthKeyOf(state.Date);

            state.PostCash(LedgerLine.Subscriptions, 300_000L);
            state.PostCash(LedgerLine.Salaries, 120_000L);

            Assert.AreEqual(300_000L, state.Ledger.MonthIncome(month));
            Assert.AreEqual(120_000L, state.Ledger.MonthCost(month));
            Assert.AreEqual(180_000L, state.Ledger.MonthCashFlow(month));
        }

        /// <summary>
        /// A campaign writes real books.
        ///
        /// This asserted that serving, salaries and depreciation all appear, and it was wrong: the
        /// baseline company in this fixture buys no hardware and hires nobody, so those costs are
        /// genuinely zero and demanding them would have been the test insisting on a fact the game
        /// does not contain. The strong claim lives in the reconciliation test above, which catches
        /// any movement that fails to post whatever line it belongs to.
        /// </summary>
        [Test]
        public void ARealCampaignWritesBooksMadeOnlyOfKnownLines()
        {
            var simulation = RunFor(900);
            var ledger = simulation.State.Ledger;

            Assert.IsTrue(ledger.HasAnything, "Nine hundred days and the books are empty.");

            foreach (var key in ledger.RecordedMonths())
            {
                foreach (var info in Ledger.Lines)
                {
                    Assert.GreaterOrEqual(ledger.MonthTotal(key, info.Line), 0L,
                        $"{info.DisplayName} went negative, which the sign rule is supposed to "
                        + "make impossible.");
                }
            }
        }

        /// <summary>
        /// Every line has to be reachable, which the catalog cannot prove on its own. A line nobody
        /// posts is a permanently blank row in the report.
        /// </summary>
        [Test]
        public void EveryLineInTheCatalogCanActuallyBePosted()
        {
            var state = new CompanyState("Everyco", 11);
            var month = Ledger.MonthKeyOf(state.Date);

            foreach (var info in Ledger.Lines)
            {
                if (info.IsCash)
                {
                    state.PostCash(info.Line, 1_000L);
                }
                else
                {
                    state.PostNonCash(info.Line, 1_000L);
                }

                Assert.AreEqual(1_000L, state.Ledger.MonthTotal(month, info.Line),
                    $"{info.DisplayName} did not record.");
            }
        }

        [Test]
        public void TheBooksSurviveASave()
        {
            var simulation = RunFor(500);
            var before = simulation.State.Ledger;
            var month = Ledger.MonthKeyOf(simulation.State.Date);

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(before.RecordedMonths().Count, restored.Ledger.RecordedMonths().Count,
                "A report that empties on load is worse than no report.");

            foreach (var info in Ledger.Lines)
            {
                Assert.AreEqual(before.MonthTotal(month, info.Line),
                    restored.Ledger.MonthTotal(month, info.Line), info.DisplayName);
            }
        }

        /// <summary>
        /// A file whose ledger has a different number of columns is dropped rather than stretched.
        /// A report whose lines have shifted by one is worse than an empty one, because it is wrong
        /// in a way the player cannot see.
        /// </summary>
        [Test]
        public void AMalformedLedgerIsDroppedRatherThanMisread()
        {
            var ledger = new Ledger();
            ledger.Restore(new List<int> { 24_000 }, new List<long> { 1L, 2L, 3L });

            Assert.IsFalse(ledger.HasAnything);
        }

        [Test]
        public void AnOlderSaveStartsWithEmptyBooksRatherThanInventedOnes()
        {
            var data = new SaveData { version = 15 };
            var upgraded = SaveMigration.UpgradeV15ToV16(data);

            Assert.AreEqual(16, upgraded.version);
            Assert.IsEmpty(upgraded.ledgerMonths);
            Assert.IsNotEmpty(SaveMigration.LastMigrationNotes);
        }

        [Test]
        public void OnlySoManyMonthsAreKept()
        {
            var ledger = new Ledger();

            for (var month = 0; month < Ledger.MonthsKept + 24; month++)
            {
                ledger.Post(GameDate.FromCalendar(2022 + month / 12, month % 12 + 1, 1),
                    LedgerLine.Salaries, 1000L);
            }

            Assert.LessOrEqual(ledger.RecordedMonths().Count, Ledger.MonthsKept,
                "A fifteen year game would otherwise carry a save nobody can load.");
        }
    }
}
