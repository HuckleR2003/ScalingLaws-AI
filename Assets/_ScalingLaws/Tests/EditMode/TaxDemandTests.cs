using System.Linq;
using NUnit.Framework;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The one bill in this game a company can plan for, and the two ways of not paying it.
    ///
    /// **Deferring and ignoring must not converge.** Asking costs interest and no standing and has a
    /// ceiling; taking it costs a flat surcharge, costs standing, and is on the public record. If
    /// those two ever cost the same the polite option is a button nobody has a reason to press.
    /// </summary>
    public sealed class TaxDemandTests
    {
        /// <summary>A company with a year of taxable profit behind it, on the day of the demand.</summary>
        private static CompanySimulation WithADemand(long owed = 4_000_000L)
        {
            var simulation = new CompanySimulation(new CompanyState("Revenue", 0x7A11u));

            // Straight to the second of January with the year's liability already accrued, which is
            // what a year of trading leaves behind. Running a real year would test the accrual, and
            // that is a different fixture's job.
            while (!(simulation.State.Date.Month == 1 && simulation.State.Date.Day == 1))
            {
                simulation.AdvanceDay();
            }

            simulation.State.AccruedTaxUsd = owed;
            simulation.State.CashUsd = 500_000_000L;
            simulation.AdvanceDay();

            return simulation;
        }

        [Test]
        public void TheYearsTaxArrivesAsOneLetterWithADeadline()
        {
            var simulation = WithADemand();
            var letter = simulation.OutstandingTaxDemand();

            Assert.That(letter, Is.Not.Null, "the demand lands on the second of January");
            Assert.That(letter.AmountUsd, Is.EqualTo(4_000_000L));
            Assert.That(letter.DueDayIndex, Is.GreaterThan(simulation.State.Date.DayIndex),
                "a demand with no deadline is a demand nothing ever happens about");
        }

        /// <summary>
        /// Paying clears it, and the banner reads off the same letter so it clears too.
        /// </summary>
        [Test]
        public void PayingItEndsIt()
        {
            var simulation = WithADemand();
            var letter = simulation.OutstandingTaxDemand();
            var before = simulation.State.CashUsd;

            Assert.That(simulation.TryActOnMail(letter.Id, MailAction.Pay, out var why), Is.True, why);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - 4_000_000L));
            Assert.That(simulation.OutstandingTaxDemand(), Is.Null);
        }

        /// <summary>
        /// Ignoring it moves the bill to next year with the surcharge on top, and says so.
        /// </summary>
        [Test]
        public void IgnoringItCarriesTheBillForwardWithASurcharge()
        {
            var simulation = WithADemand();
            var letter = simulation.OutstandingTaxDemand();
            var due = letter.DueDayIndex;
            var owed = letter.AmountUsd;

            while (simulation.State.Date.DayIndex <= due)
            {
                simulation.AdvanceDay();
            }

            simulation.AdvanceDay();

            Assert.That(simulation.OutstandingTaxDemand(), Is.Null,
                "the file is closed, which is what makes the corner strip go away");

            var expected = owed
                + (long)System.Math.Round(owed * CompanySimulation.UnjustifiedDeferralSurcharge);

            Assert.That(simulation.State.AccruedTaxUsd, Is.GreaterThanOrEqualTo(expected),
                "the whole sum plus nine per cent has to reach next year's assessment");
        }

        /// <summary>
        /// The player is told, with the number, on the day it happens.
        ///
        /// **This is the whole change.** What used to happen was the amount growing at thirty five
        /// per cent a year inside a letter nobody had opened, which is a worse penalty that nobody
        /// ever saw arrive.
        /// </summary>
        [Test]
        public void TheCarryForwardIsAnnounced()
        {
            var simulation = WithADemand();
            var due = simulation.OutstandingTaxDemand().DueDayIndex;

            var announced = false;

            while (simulation.State.Date.DayIndex <= due + 1)
            {
                simulation.AdvanceDay();

                while (simulation.State.TryDequeueEvent(out var raised))
                {
                    if (raised.Type == CompanyEventType.TaxCarriedForward)
                    {
                        announced = true;

                        Assert.That(raised.AmountUsd, Is.GreaterThan(4_000_000L),
                            "the figure announced is what is owed next year, surcharge included");
                    }
                }
            }

            Assert.That(announced, Is.True);
        }

        /// <summary>
        /// Asking is cheaper than taking, which is the reason both buttons exist.
        /// </summary>
        [Test]
        public void AskingToDeferCostsStandingAndTakingItDoes()
        {
            var asked = WithADemand();
            var standingBefore = asked.State.Reputation;

            Assert.That(asked.TryActOnMail(asked.OutstandingTaxDemand().Id,
                MailAction.Defer, out var why), Is.True, why);

            Assert.That(asked.State.Reputation, Is.EqualTo(standingBefore),
                "a postponement the company asked for costs nothing in standing");

            var taken = WithADemand();
            var before = taken.State.Reputation;
            var due = taken.OutstandingTaxDemand().DueDayIndex;

            while (taken.State.Date.DayIndex <= due + 1)
            {
                taken.AdvanceDay();
            }

            Assert.That(taken.State.Reputation, Is.LessThan(before),
                "taking one without asking is a matter of public record");
        }

        /// <summary>
        /// One demand a year, whatever happened to the last one.
        ///
        /// A carried bill joins next January's rather than sitting beside it, because two open
        /// demands is two deadlines and the date is the thing a player is meant to be able to plan
        /// around.
        /// </summary>
        [Test]
        public void ACarriedBillArrivesInsideNextYearsDemandRatherThanBesideIt()
        {
            var simulation = WithADemand();
            var due = simulation.OutstandingTaxDemand().DueDayIndex;

            while (simulation.State.Date.DayIndex <= due + 1)
            {
                simulation.AdvanceDay();
            }

            var year = simulation.State.Date.Year;

            while (simulation.State.Date.Year == year)
            {
                simulation.AdvanceDay();
            }

            simulation.AdvanceDay();
            simulation.AdvanceDay();

            var open = simulation.State.Mail.All
                .Count(item => item.Kind == MailKind.TaxDemand && !item.IsClosed);

            Assert.That(open, Is.LessThanOrEqualTo(1),
                "two open tax demands is two deadlines and the whole point is that there is one");
        }
    }
}
