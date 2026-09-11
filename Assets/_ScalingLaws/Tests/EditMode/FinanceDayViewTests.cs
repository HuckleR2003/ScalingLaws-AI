using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// BY DAY reports a day.
    ///
    /// **Reported by the author: the button does nothing.** It did slightly worse than nothing. The
    /// headline stayed on the month whichever toggle was lit, and the lines underneath walked all
    /// thirty one day buckets and added them up, which is the month again. So both views printed the
    /// same figures and the only thing that changed was a caption claiming they were daily.
    ///
    /// The fixture drives the real panel through <see cref="FinanceReport.ShowDays"/>, which is what
    /// the toggles call: an EditMode element has no panel, so a click sent to a button is never
    /// dispatched and the lambda behind it would go unmeasured.
    /// </summary>
    public sealed class FinanceDayViewTests
    {
        private static GameDate Day(int day) => GameDate.FromCalendar(2024, 3, day);

        /// <summary>A month with two very different days in it, so one cannot stand for the other.</summary>
        private static Ledger TwoDays()
        {
            var books = new Ledger();

            books.Post(Day(1), LedgerLine.Subscriptions, 100_000L);
            books.Post(Day(1), LedgerLine.Salaries, 40_000L);

            books.Post(Day(2), LedgerLine.Subscriptions, 10_000L);
            books.Post(Day(2), LedgerLine.Salaries, 40_000L);
            books.Post(Day(2), LedgerLine.Fines, 500_000L);

            return books;
        }

        [Test]
        public void TheBooksCanBeAskedAboutOneDayTheWayTheyAreAskedAboutOneMonth()
        {
            var books = TwoDays();

            Assert.AreEqual(60_000L, books.DayCashFlow(1),
                "Day one took 100k and paid 40k.");

            Assert.AreEqual(-530_000L, books.DayCashFlow(2),
                "Day two took 10k and paid 40k of wages and a 500k fine.");

            Assert.AreEqual(10_000L, books.DayIncome(2));
            Assert.AreEqual(540_000L, books.DayCost(2));

            Assert.AreEqual(books.MonthCashFlow(Ledger.MonthKeyOf(Day(2))),
                books.DayCashFlow(1) + books.DayCashFlow(2),
                "The days have to add up to the month, or the two views of one report disagree.");

            CollectionAssert.AreEqual(new[] { 1, 2 }, books.RecordedDays());
        }

        /// <summary>
        /// The big red figure is the point of the panel, and it is the thing that did not move.
        /// </summary>
        [Test]
        public void TheHeadlineChangesWhenTheViewChanges()
        {
            var books = TwoDays();
            var report = new FinanceReport(() => books, () => Day(2), () => { });

            report.Open();

            var headline = report.Root.Q<Label>(className: "finance__headline");
            Assert.IsNotNull(headline, "the report has no headline to read");

            var byMonth = headline.text;

            report.ShowDays(true);
            var byDay = headline.text;

            Assert.AreNotEqual(byMonth, byDay,
                "BY DAY left the headline on the month. That is the whole complaint: the toggle "
                + "lights up and the number it is meant to change does not.");

            Assert.AreEqual("-" + UiFormat.Money(530_000L), byDay,
                "The day view reports the last day recorded, which is day two.");

            report.ShowDays(false);
            Assert.AreEqual(byMonth, headline.text, "Switching back has to come back.");
        }

        /// <summary>
        /// The detail under the headline is the same day, not the whole month wearing its name.
        /// </summary>
        [Test]
        public void TheLinesUnderneathAreThatDayAsWell()
        {
            var books = TwoDays();
            var report = new FinanceReport(() => books, () => Day(2), () => { });

            report.Open();
            report.ShowDays(true);

            var found = false;
            foreach (var value in report.Root.Query<Label>(className: "finance-row__value").ToList())
            {
                // Subscriptions: 10k on the day being reported against 110k across the month. The
                // month's figure appearing here is the fault, and it is the figure the old code
                // produced by summing every day bucket.
                if (value.text == "+" + UiFormat.Money(10_000L))
                {
                    found = true;
                }

                Assert.AreNotEqual("+" + UiFormat.Money(110_000L), value.text,
                    "A day view printing the month's subscription total is the month with a "
                    + "different caption on it.");
            }

            Assert.IsTrue(found,
                "The day's own subscription line is missing, so the view is reporting nothing "
                + "rather than reporting a day.");
        }
    }
}
