using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The instalment a borrower plans around.
    ///
    /// **Reported by the author: RATA MIESIĘCZNA read $0 while the commission beside it read $72k.**
    /// Nothing was broken underneath. A facility just drawn is in its grace period, so the instalment
    /// charged today really is zero, and the screen printed that true and useless figure instead of
    /// the instalment that was coming.
    /// </summary>
    public sealed class LoanScheduleTests
    {
        private static readonly GameDate Taken = GameDate.FromCalendar(2024, 1, 1);

        private const int Grace = 90;

        private static LoanBook OneLoanInGrace()
        {
            var book = new LoanBook();
            book.Add(new Loan(LoanProduct.SovereignSeed, Taken, 1_000_000_000L, 3_000_000_000L, 1_800, Grace));
            return book;
        }

        [Test]
        public void ALoanInItsGraceStillHasAnInstalmentComing()
        {
            var book = OneLoanInGrace();
            var soon = Taken.AddDays(10);

            Assert.AreEqual(0L, book.MonthlyInstalmentUsd(soon),
                "Nothing is charged during the grace period. That part was always right.");

            Assert.That(book.ScheduledMonthlyInstalmentUsd(), Is.GreaterThan(0L),
                "The instalment that is coming reads zero, which is the figure the author saw.");

            Assert.AreEqual(Taken.AddDays(Grace), book.FirstInstalmentAfterGrace(soon),
                "The screen cannot say when the first instalment starts.");
        }

        [Test]
        public void OnceTheGraceIsOverTheTwoFiguresAreTheSame()
        {
            var book = OneLoanInGrace();
            var later = Taken.AddDays(Grace + 5);

            Assert.IsNull(book.FirstInstalmentAfterGrace(later),
                "A loan already paying has no start date left to announce.");

            Assert.AreEqual(book.ScheduledMonthlyInstalmentUsd(), book.MonthlyInstalmentUsd(later),
                "The instalment shown during the grace is not the one that is actually charged.");
        }
    }
}
