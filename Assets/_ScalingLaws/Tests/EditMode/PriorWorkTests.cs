using System;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Research points put into a family programme to shorten its calendar.
    ///
    /// **Asked for on 2026-09-30, and the design question was which currency.** Money buying time
    /// is the one thing this game's spine refuses, and the architecture screen already sells quality
    /// for money through the budget and length pair, so a second money lever would be two mechanisms
    /// for one subject. Points are different in kind: they are only ever earned by finished work, so
    /// spending them says the lab already understands part of what it is about to build.
    ///
    /// Everything below is one of the rules that keep it from being a discount.
    /// </summary>
    public sealed class PriorWorkTests
    {
        private static CompanySimulation Funded()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 400_000_000L;
            simulation.SetRentedPetaflops(400.0);
            return simulation;
        }

        private static ArchitectureBlueprint Programme(int days = 365) => new(
            "Ardent",
            ArchitectureId.CustomFamilyA,
            ArchitectureId.None,
            0.35, 0.35, 0.35, 0.35, 0.35,
            40_000_000L,
            days);

        // ---- the rule on its own ----------------------------------------------------------------

        /// <summary>
        /// **The neutral option is exactly nothing.** Every control in this game is held to it: a
        /// player who never touches this runs the programme they would have run before it existed.
        /// </summary>
        [Test]
        public void NothingPutInIsExactlyTheCalendarItAlwaysWas()
        {
            Assert.AreEqual(0, PriorWork.DaysOff(365, 0.0));
            Assert.AreEqual(365, PriorWork.CalendarAfter(365, 0.0));
            Assert.AreEqual(0.0, PriorWork.PointsFor(0));
        }

        /// <summary>
        /// It compresses and it never skips. This is the rule the whole thing stands on: a
        /// programme is a bet on where the frontier will be when it lands, and a lever that could
        /// halve the wait would turn that bet into a purchase.
        /// </summary>
        [Test]
        public void NoAmountOfPointsReachesPastTheCap()
        {
            const int calendar = 400;

            var cap = PriorWork.MostDaysOff(calendar);

            Assert.AreEqual((int)Math.Floor(calendar * PriorWork.MostOfTheCalendar), cap,
                "The cap is not the share it says it is.");

            Assert.AreEqual(cap, PriorWork.DaysOff(calendar, 10_000_000.0),
                "A company with a mountain of points took more than the cap off.");

            Assert.That(PriorWork.CalendarAfter(calendar, 10_000_000.0),
                Is.GreaterThanOrEqualTo(ArchitectureBlueprint.MinimumDurationDays),
                "Points took the programme under the floor the length slider itself stops at.");
        }

        /// <summary>
        /// **The case a naive clamp gets backwards.** A founder and a country can already pull a
        /// programme under the floor, and a rule written as "never below sixty" would then make
        /// points *lengthen* it.
        /// </summary>
        [Test]
        public void AProgrammeAlreadyAtTheFloorIsLeftAloneRatherThanStretched()
        {
            Assert.AreEqual(0, PriorWork.MostDaysOff(ArchitectureBlueprint.MinimumDurationDays));
            Assert.AreEqual(0, PriorWork.MostDaysOff(40));

            Assert.AreEqual(40, PriorWork.CalendarAfter(40, 10_000.0),
                "A short programme came back longer than it went in.");
        }

        /// <summary>A day is never half paid for, and never handed over half paid.</summary>
        [Test]
        public void DaysAreWholeInBothDirections()
        {
            var one = PriorWork.PointsFor(1);

            Assert.AreEqual(1, PriorWork.DaysOff(365, one),
                "Paying for a day in full did not buy the day.");

            Assert.AreEqual(0, PriorWork.DaysOff(365, one - 0.01),
                "A day that was not paid for in full was handed over anyway.");
        }

        /// <summary>
        /// The card that explains this quotes the cap, so the cap has to be what it says.
        ///
        /// **Digits in both languages on purpose**, the same reason the tour's count of empty
        /// basement squares is written as a number: one test covers English and Polish, and a
        /// translator cannot quietly drop it.
        /// </summary>
        [Test]
        public void TheCardQuotesTheCapItActuallyHas()
        {
            var written = ((int)Math.Round(PriorWork.MostOfTheCalendar * 100.0)) + "%";
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    StringAssert.Contains(written, Loc.T("tech.prior.affects"),
                        $"The {language} card does not say what the cap is, or says a different "
                        + "number from the one the rule uses.");
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }

        // ---- the rule where it meets the company ------------------------------------------------


        /// <summary>
        /// The end to end claim: the points leave and the calendar is genuinely shorter. A chain of
        /// green links is not a green chain, and this project has shipped that fault before.
        /// </summary>
        [Test]
        public void ThePointsLeaveTheCompanyAndTheProgrammeRunsShorter()
        {
            var plain = Funded();
            Assert.IsTrue(plain.TryStartArchitectureProgramme(Programme(), out var why), why);

            var full = plain.State.ActiveArchitectureProject.DurationDays;

            var hurried = Funded();
            var spend = hurried.MostPriorWorkWorthSpending(Programme());

            Assert.That(spend, Is.GreaterThan(0.0),
                "A 365 day programme can have nothing taken off it, so there is no mechanic here.");

            hurried.State.ResearchPoints = spend + 25.0;

            Assert.IsTrue(
                hurried.TryStartArchitectureProgramme(Programme(), spend, out var reason), reason);

            Assert.That(hurried.State.ActiveArchitectureProject.DurationDays, Is.LessThan(full),
                "The points were taken and the programme runs for exactly as long as before.");

            Assert.AreEqual(25.0, hurried.State.ResearchPoints, 0.001,
                "The points did not leave the company, so the head start was free.");
        }

        /// <summary>
        /// **It buys calendar and nothing else.** Moving the compute as well would make it a
        /// discount on the programme rather than a head start on it, and a shortened programme
        /// would stop being able to sit waiting on a cluster, which is the honest outcome.
        /// </summary>
        [Test]
        public void PointsBuyCalendarAndNeitherComputeNorQuality()
        {
            var plain = Funded();
            plain.TryStartArchitectureProgramme(Programme(), out _);

            var hurried = Funded();
            var spend = hurried.MostPriorWorkWorthSpending(Programme());
            hurried.State.ResearchPoints = spend;
            hurried.TryStartArchitectureProgramme(Programme(), spend, out _);

            var slow = plain.State.ActiveArchitectureProject;
            var fast = hurried.State.ActiveArchitectureProject;

            Assert.AreEqual(slow.PetaflopDaysRequired, fast.PetaflopDaysRequired, 0.0001,
                "The cluster owes less for a programme that was only understood sooner.");

            Assert.AreEqual(slow.ResearchPower, fast.ResearchPower, 0.0001,
                "Points bought a better family, which makes this a quality lever rather than a "
                + "calendar one.");

            Assert.AreEqual(slow.CashPaidUsd, fast.CashPaidUsd,
                "The bill moved, so points are buying money as well.");
        }

        /// <summary>
        /// Asking for more than the company holds is refused, and refused without taking anything.
        /// </summary>
        [Test]
        public void AskingForPointsTheCompanyDoesNotHaveIsRefusedAndCostsNothing()
        {
            var simulation = Funded();
            var cash = simulation.State.CashUsd;

            simulation.State.ResearchPoints = 5.0;

            var asked = simulation.MostPriorWorkWorthSpending(Programme());

            Assert.IsFalse(simulation.TryStartArchitectureProgramme(Programme(), asked, out var why),
                "A company five points from broke started a programme it cannot pay for.");

            Assert.That(why, Is.Not.Null.And.Not.Empty, "The refusal says nothing.");
            Assert.IsNull(simulation.State.ActiveArchitectureProject);
            Assert.AreEqual(5.0, simulation.State.ResearchPoints, 0.001);
            Assert.AreEqual(cash, simulation.State.CashUsd, "A refused programme charged the cash.");
        }

        /// <summary>
        /// Paying past the cap charges for the days delivered and no more. Taking a currency for
        /// something that cannot be handed over is the shape of every refund argument there is.
        /// </summary>
        [Test]
        public void PayingPastTheCapOnlyChargesForWhatTheCapCanGive()
        {
            var simulation = Funded();
            var worth = simulation.MostPriorWorkWorthSpending(Programme());

            simulation.State.ResearchPoints = worth * 4.0;

            Assert.IsTrue(
                simulation.TryStartArchitectureProgramme(Programme(), worth * 4.0, out var why), why);

            Assert.AreEqual(worth * 3.0, simulation.State.ResearchPoints, 0.001,
                "Points past the cap were taken and bought nothing.");
        }

        /// <summary>
        /// The screen and the rule answer with the same number. The architecture calendar was
        /// computed in two places before this and the event announcing it quoted a third.
        /// </summary>
        [Test]
        public void TheQuotedCalendarIsTheCalendarTheProgrammeRunsFor()
        {
            var simulation = Funded();
            var spend = simulation.MostPriorWorkWorthSpending(Programme());
            simulation.State.ResearchPoints = spend;

            var quoted = simulation.ArchitectureCalendarDays(Programme(), spend);

            Assert.IsTrue(
                simulation.TryStartArchitectureProgramme(Programme(), spend, out var why), why);

            Assert.AreEqual(quoted, simulation.State.ActiveArchitectureProject.DurationDays,
                "The number on the screen is not the number the calendar runs for.");
        }
    }
}
