using NUnit.Framework;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Three years of postponement is where the asking stops and the collecting starts.
    ///
    /// **Reported by Natalia: nobody knew what happened if the player simply never paid.** The answer
    /// was that nothing did. `LongestDeferralDays` stopped them *asking* for another postponement and
    /// nothing else: the letter sat, the date passed, `CarryOverdueTax` added nine per cent and rolled
    /// it into next January, and the new demand arrived with its three postponements restored. The
    /// ceiling was a closed door standing beside an open one, and corporation tax was rollable
    /// forever for a predictable annual fee.
    ///
    /// The fixture drives the real deferral path rather than writing the state by hand, because the
    /// thing being measured is what a player who keeps clicking POSTPONE actually arrives at.
    /// </summary>
    public sealed class TaxRefusalTests
    {
        private static CompanySimulation Owing(long cash = 500_000_000L)
        {
            var simulation = new CompanySimulation(new CompanyState("Arrears"));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        /// <summary>Puts a real demand in the inbox, the way the year end does.</summary>
        private static MailItem Demand(CompanySimulation simulation, long amount)
        {
            var letter = simulation.State.Mail.Add(MailKind.TaxDemand, simulation.State.Date,
                "Revenue", "Corporation tax", "The demand for the year that just ended.");

            letter.AmountUsd = amount;
            letter.DueDayIndex = simulation.State.Date.DayIndex + CompanySimulation.DemandGraceDays;
            return letter;
        }

        private static void Postpone(CompanySimulation simulation, MailItem letter, int times)
        {
            for (var attempt = 0; attempt < times; attempt++)
            {
                Assert.That(simulation.TryActOnMail(letter.Id, MailAction.Defer, out var why), Is.True,
                    "postponement " + (attempt + 1) + " was refused: " + why);
            }
        }

        [Test]
        public void ThreePostponementsAreAllowedAndTheFourthIsNot()
        {
            var simulation = Owing();
            var letter = Demand(simulation, 10_000_000L);

            Postpone(simulation, letter, 3);

            Assert.That(simulation.TryActOnMail(letter.Id, MailAction.Defer, out var why), Is.False,
                "A fourth postponement was accepted, so the ceiling is not a ceiling.");

            Assert.That(why, Is.Not.Empty, "refused without saying why");
        }

        /// <summary>
        /// **This is the ratchet.** Running the allowance out and then waiting is what a player does,
        /// and it used to cost nine per cent and buy another year.
        /// </summary>
        [Test]
        public void PastTheCeilingTheWholeArrearsIsTakenWithAPenalty()
        {
            var simulation = Owing();
            var letter = Demand(simulation, 10_000_000L);

            Postpone(simulation, letter, 3);

            var owed = letter.AmountUsd;
            var cashBefore = simulation.State.CashUsd;
            var accruedBefore = simulation.State.AccruedTaxUsd;

            // Past the last date it was pushed to.
            while (simulation.State.Date.DayIndex <= letter.DueDayIndex)
            {
                simulation.AdvanceDay();
            }

            Assert.That(letter.IsClosed, Is.True, "the demand is still open past its final date");

            var taken = cashBefore - simulation.State.CashUsd;
            var expected = owed + (long)System.Math.Round(owed * CompanySimulation.RefusedDeferralPenalty);

            Assert.That(taken, Is.EqualTo(expected).Within(owed * 0.02),
                "The revenue took " + taken + " against " + owed + " owed plus a "
                + CompanySimulation.RefusedDeferralPenalty + " penalty.");

            Assert.That(simulation.State.AccruedTaxUsd, Is.LessThanOrEqualTo(accruedBefore + 1L),
                "The arrears was rolled into next year as well as being collected, so the company "
                + "has been charged for it twice.");
        }

        /// <summary>
        /// A tax bill a company can always afford is a fee. This one is allowed to overdraw them,
        /// which is the difference between a deadline and a suggestion.
        /// </summary>
        [Test]
        public void ItIsCollectedEvenWhenTheMoneyIsNotThere()
        {
            var simulation = Owing(1_000L);
            var letter = Demand(simulation, 40_000_000L);

            Postpone(simulation, letter, 3);

            while (simulation.State.Date.DayIndex <= letter.DueDayIndex)
            {
                simulation.AdvanceDay();
            }

            Assert.That(letter.IsClosed, Is.True);
            Assert.That(simulation.State.CashUsd, Is.LessThan(0L),
                "The company covered a forty million demand out of a thousand dollars, so the "
                + "collection is refusing itself when the account is short.");
        }

        /// <summary>
        /// A letter still inside its allowance keeps the old behaviour: nine per cent and next
        /// January. Losing that would make every late payment a company-ending event.
        /// </summary>
        [Test]
        public void ALateLetterThatStillHasPostponementsLeftIsCarriedRatherThanCollected()
        {
            var simulation = Owing();
            var letter = Demand(simulation, 10_000_000L);

            var cashBefore = simulation.State.CashUsd;

            while (simulation.State.Date.DayIndex <= letter.DueDayIndex)
            {
                simulation.AdvanceDay();
            }

            Assert.That(letter.IsClosed, Is.True);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cashBefore).Within(2_000_000L),
                "A demand that was merely late took money out of the account on the day. That is "
                + "the refusal penalty firing on a company that never ran out of postponements.");

            Assert.That(simulation.State.AccruedTaxUsd, Is.GreaterThan(10_000_000L),
                "It was not carried into next year's assessment either, so it simply vanished.");
        }

        /// <summary>
        /// The event exists and says the figures, because a nine figure charge that arrives with no
        /// sentence attached is indistinguishable from a bug.
        /// </summary>
        [Test]
        public void TheCollectionAnnouncesItself()
        {
            var simulation = Owing();
            var letter = Demand(simulation, 10_000_000L);

            Postpone(simulation, letter, 3);

            while (simulation.State.TryDequeueEvent(out _))
            {
            }

            while (simulation.State.Date.DayIndex <= letter.DueDayIndex)
            {
                simulation.AdvanceDay();
            }

            var said = false;
            while (simulation.State.TryDequeueEvent(out var raised))
            {
                said |= raised.Type == CompanyEventType.TaxCollected;
            }

            Assert.That(said, Is.True,
                "The whole arrears was taken and nothing was filed about it.");
        }
    }
}
