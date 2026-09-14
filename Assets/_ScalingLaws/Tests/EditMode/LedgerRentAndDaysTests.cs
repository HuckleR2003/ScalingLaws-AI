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
            Assert.AreEqual(2 * Ledger.Lines.Count, upgraded.ledgerAmounts.Count,
                "The old months were not widened to the new set of lines.");

            var books = new Ledger();
            books.Restore(upgraded.ledgerMonths, upgraded.ledgerAmounts);

            Assert.AreEqual(5_000L, books.MonthTotal(24_290, LedgerLine.Salaries),
                "The books of a loaded campaign were thrown away on the way through the migration.");

            Assert.AreEqual(5_001L, books.MonthTotal(24_291, LedgerLine.Salaries));
            Assert.AreEqual(0L, books.MonthTotal(24_291, LedgerLine.OfficeRent),
                "A v56 month cannot say what its rent was, so its rent line is zero, not a guess.");
        }
    }
}
