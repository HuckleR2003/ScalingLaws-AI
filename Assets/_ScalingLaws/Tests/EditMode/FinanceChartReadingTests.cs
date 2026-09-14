using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// WHERE THE MONEY WENT, read the way the author asked to read it.
    ///
    /// A date under every bar. The pointer resting on a bar turns the figures underneath to that
    /// bar, and a click keeps it. The day view is the last thirty days rather than the days of this
    /// month, and it moves when a day passes rather than when a toggle is pressed again.
    ///
    /// Hovering is driven through <see cref="FinanceReport.Preview"/> and clicking through
    /// <see cref="FinanceReport.Pin"/>, which are what the chart calls: an EditMode element has no
    /// panel, so a pointer event sent to it is never dispatched.
    /// </summary>
    public sealed class FinanceChartReadingTests
    {
        private static Ledger ThreeMonths()
        {
            var books = new Ledger();
            books.Post(GameDate.FromCalendar(2024, 1, 5), LedgerLine.Subscriptions, 100_000L);
            books.Post(GameDate.FromCalendar(2024, 2, 5), LedgerLine.Subscriptions, 200_000L);
            books.Post(GameDate.FromCalendar(2024, 3, 5), LedgerLine.Subscriptions, 300_000L);
            return books;
        }

        private static FinanceReport Open(Ledger books, GameDate today)
        {
            var report = new FinanceReport(() => books, () => today, () => { });
            report.Open();
            return report;
        }

        private static Label Headline(FinanceReport report) =>
            report.Root.Q<Label>(className: "finance__headline");

        [Test]
        public void RestingOnAMonthReportsThatMonthAndLeavingItGoesBack()
        {
            var report = Open(ThreeMonths(), GameDate.FromCalendar(2024, 3, 6));

            Assert.AreEqual("+" + UiFormat.Money(300_000L), Headline(report).text, "It opens on the newest month.");

            report.Preview(0);
            Assert.AreEqual("+" + UiFormat.Money(100_000L), Headline(report).text,
                "The figures did not follow the bar under the pointer.");

            report.Preview(-1);
            Assert.AreEqual("+" + UiFormat.Money(300_000L), Headline(report).text,
                "Moving off the chart left the report on a month nobody chose.");
        }

        [Test]
        public void AClickedMonthStaysPutThroughARefresh()
        {
            var report = Open(ThreeMonths(), GameDate.FromCalendar(2024, 3, 6));

            report.Pin(1);
            report.Refresh();

            Assert.AreEqual("+" + UiFormat.Money(200_000L), Headline(report).text,
                "A day passing threw away the month the player clicked.");
        }

        [Test]
        public void EveryBarHasADateUnderIt()
        {
            var report = Open(ThreeMonths(), GameDate.FromCalendar(2024, 3, 6));

            var chart = report.Root.Q<FinanceChart>();
            var labels = report.Root.Query<Label>(className: "finance-chart__label").ToList();

            Assert.AreEqual(chart.BarCount, labels.Count, "A bar without a place for its date.");
            Assert.AreEqual("01.2024", labels[0].text, "Months read the way the author asked: 04.2022.");
        }

        [Test]
        public void TheDayViewIsTheLastThirtyDaysAndMovesWithTheCompany()
        {
            var books = new Ledger();
            var day = GameDate.FromCalendar(2024, 3, 31);
            books.Post(day, LedgerLine.Subscriptions, 50_000L);

            var report = Open(books, day);
            report.ShowDays(true);

            Assert.AreEqual(FinanceReport.DaysShown, report.Root.Q<FinanceChart>().BarCount,
                "Thirty days are thirty bars, including the days nothing happened.");

            Assert.AreEqual("+" + UiFormat.Money(50_000L), Headline(report).text);

            books.Post(day.AddDays(1), LedgerLine.Fines, 80_000L);
            report.Refresh();

            Assert.AreEqual("-" + UiFormat.Money(80_000L), Headline(report).text,
                "The day view stayed on yesterday. That was the report: it only moved when clicked.");
        }

        /// <summary>
        /// Found while doing the above: the report walked three of the ledger's five groups, so the
        /// fleet's bills were in the bank balance and nowhere on the page that explains it.
        /// </summary>
        [Test]
        public void TheFleetBillIsOnThePage()
        {
            var books = new Ledger();
            books.Post(GameDate.FromCalendar(2024, 3, 5), LedgerLine.CloudRent, 40_000L);

            var report = Open(books, GameDate.FromCalendar(2024, 3, 6));

            var found = false;
            foreach (var value in report.Root.Query<Label>(className: "finance-row__value").ToList())
            {
                if (value.text == "-" + UiFormat.Money(40_000L))
                {
                    found = true;
                }
            }

            Assert.IsTrue(found, "Cloud rent is not on the report.");
        }
    }
}
