using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The books after the author's report: rent inside Salaries, and a day view that forgot.
    ///
    /// **Rent really was inside Salaries.** `StaffRoster.DailyCostUsd` is payroll plus rent and the
    /// sum was posted as one line. And the day view held the current month alone, emptied on the
    /// first of every month, so thirty days of history could not be drawn on any day but the last.
    /// </summary>
    public sealed class LedgerRentAndDaysTests
    {
        [Test]
        public void OfficeRentIsItsOwnLineAndNotPartOfTheWages()
        {
            var simulation = new CompanySimulation(new CompanyState("Rentco", 71));

            simulation.State.UnlockedResearch.Add(ResearchNodeId.LeaseASmallHub);
            Assert.IsTrue(simulation.TryMoveOffice(OfficeTier.Loft, out var why), why);

            var staff = simulation.State.Staff;
            var rent = staff.DailyRentUsd;
            var payroll = staff.DailyPayrollUsd;
            var multiplier = simulation.State.Founder.OperatingCostMultiplier;
            var benefits = simulation.State.DailyBenefitCostUsd;

            Assume.That(rent, Is.GreaterThan(0L), "the office charges no rent, so this measures nothing");

            simulation.AdvanceDay();

            var books = simulation.State.Ledger;
            var months = books.RecordedMonths();
            var month = months[months.Count - 1];

            Assert.AreEqual(SimUnits.ToDollars(rent * multiplier), books.MonthTotal(month, LedgerLine.OfficeRent),
                "The lease is not on its own line.");

            Assert.AreEqual(SimUnits.ToDollars(payroll * multiplier) + benefits,
                books.MonthTotal(month, LedgerLine.Salaries),
                "Salaries still carry something that is not wages. That was the report.");
        }

        [Test]
        public void TheDaysReachBackAcrossTheStartOfAMonth()
        {
            var books = new Ledger();
            var lastOfMarch = GameDate.FromCalendar(2024, 3, 31);
            var firstOfApril = GameDate.FromCalendar(2024, 4, 1);

            books.Post(lastOfMarch, LedgerLine.Subscriptions, 70_000L);
            books.Post(firstOfApril, LedgerLine.Subscriptions, 20_000L);

            Assert.AreEqual(70_000L, books.DayIndexCashFlow(lastOfMarch.DayIndex),
                "The first of April threw away the thirty first of March, which is exactly the day a "
                + "thirty day view needs.");

            CollectionAssert.AreEqual(new[] { 1 }, books.RecordedDays(),
                "The day-of-month questions still mean the month being recorded.");

            Assert.AreEqual(20_000L, books.DayCashFlow(1));
            Assert.AreEqual(firstOfApril.DayIndex, books.LastRecordedDayIndex);
        }

        [Test]
        public void OnlyTheNewestDaysAreKept()
        {
            var books = new Ledger();
            var start = GameDate.FromCalendar(2024, 1, 1);
            var posted = Ledger.DaysKept + 10;

            for (var day = 0; day < posted; day++)
            {
                books.Post(start.AddDays(day), LedgerLine.Subscriptions, 1_000L);
            }

            Assert.AreEqual(1_000L, books.DayIndexCashFlow(start.AddDays(posted - 1).DayIndex));
            Assert.AreEqual(1_000L, books.DayIndexCashFlow(start.AddDays(10).DayIndex), "the oldest day kept");
            Assert.AreEqual(0L, books.DayIndexCashFlow(start.AddDays(9).DayIndex),
                "Day detail grows for ever. It is meant to hold two months, not a campaign.");
        }

        /// <summary>
        /// A v56 save keeps every month it recorded and gains an empty rent column.
        ///
        /// `Ledger.Restore` drops a history whose width does not match, which is right for a file
        /// nobody can explain and would have been wrong here: the column is new and at the end, so
        /// every old month is an exact prefix of a new one.
        /// </summary>
        [Test]
        public void AV56LedgerKeepsItsMonthsAndGainsAnEmptyRentColumn()
        {
            const int oldWidth = 26;
            const int salaries = 9;

            var data = new SaveData
            {
                version = 56,
                ledgerMonths = new List<int> { 24_290, 24_291 },
                ledgerAmounts = new List<long>()
            };

            for (var month = 0; month < 2; month++)
            {
                for (var column = 0; column < oldWidth; column++)
                {
                    data.ledgerAmounts.Add(column == salaries ? 5_000L + month : 0L);
                }
            }

            var upgraded = SaveMigration.UpgradeV56ToV57(data);

            Assert.AreEqual(57, upgraded.version);

            // **The width this step produces, not the width the game has today.**
            //
            // This read `Ledger.Lines.Count` and passed for as long as v57 happened to be the
            // newest ledger shape. The moment a second line was appended it failed, naming the
            // step that had done nothing wrong: one migration widens by one column and the
            // catalogue had grown by two. A step is measured against its own target or it breaks
            // every time a later one is written.
            const int v57Width = oldWidth + 1;

            Assert.AreEqual(2 * v57Width, upgraded.ledgerAmounts.Count,
                "v56 to v57 widens a month by exactly one column.");

            // **Nothing is read back out of a half-migrated row, and that is not squeamishness.**
            // `Ledger.Restore` expects one column per line in today's catalogue, so a row that has
            // been widened by one step out of two does not fit it and comes back empty. The
            // content assertions belong after the whole chain, below, where the row is the shape
            // the game actually loads.

            // **And the whole chain, which is the assertion that cannot go stale.** However many
            // ledger lines are appended after this, a v56 file run all the way forward has to
            // arrive at exactly as many columns as the catalogue has rows, with its salaries still
            // in the salaries column.
            var whole = new SaveData
            {
                version = 56,
                ledgerMonths = new List<int> { 24_290, 24_291 },
                ledgerAmounts = new List<long>()
            };

            for (var month = 0; month < 2; month++)
            {
                for (var column = 0; column < oldWidth; column++)
                {
                    whole.ledgerAmounts.Add(column == salaries ? 5_000L + month : 0L);
                }
            }

            var carried = SaveStore.Parse(UnityEngine.JsonUtility.ToJson(whole));

            Assert.AreEqual(SaveData.CurrentVersion, carried.version);
            Assert.AreEqual(2 * Ledger.Lines.Count, carried.ledgerAmounts.Count,
                "A v56 file carried all the way forward has to end with one column per ledger line.");

            var carriedBooks = new Ledger();
            carriedBooks.Restore(carried.ledgerMonths, carried.ledgerAmounts);

            Assert.AreEqual(5_000L, carriedBooks.MonthTotal(24_290, LedgerLine.Salaries),
                "A column was inserted rather than appended somewhere along the chain.");

            Assert.AreEqual(5_001L, carriedBooks.MonthTotal(24_291, LedgerLine.Salaries));

            Assert.AreEqual(0L, carriedBooks.MonthTotal(24_291, LedgerLine.OfficeRent),
                "A v56 month cannot say what its rent was, so its rent line is zero, not a guess.");

            Assert.AreEqual(0L, carriedBooks.MonthTotal(24_291, LedgerLine.PartnerShare),
                "No partner share was ever charged before v71, so an older month records none.");
        }
    }
}
