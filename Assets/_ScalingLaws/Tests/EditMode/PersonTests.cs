using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What one person wants, what a bonus buys, and what the hours are.
    ///
    /// **The interesting claim is that the same benefit is worth different amounts to different
    /// people.** Everybody values a gym card a little; the person who asked for one values it a
    /// great deal, and the person who asked and did not get one notices every month. That is what
    /// makes reading a person worth a click rather than reading a row.
    /// </summary>
    public sealed class PersonTests
    {
        private static CompanySimulation Company(uint seed = 55)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.State.CashUsd = 40_000_000;

            // The garage has no desks, and  refuses anybody who needs one. A
            // company with nowhere to sit is the right rule and the wrong fixture for a test about
            // what a person is like once they are here.
            simulation.State.Staff.SetOffice(OfficeTier.Loft);

            return simulation;
        }

        private static Hire Somebody(string name, int startDay = 0) =>
            new(StaffRole.InfrastructureEngineer, 3, new GameDate(startDay), name,
                PlayerSkill.Development, HireSource.Agency, 90.0);

        /// <summary>
        /// The same person wants the same things, every time, forever.
        ///
        /// **Not `string.GetHashCode`**, which is randomised per process: the same employee would
        /// want different things on every launch, and a save reloaded twice would disagree with
        /// itself. This project has already been caught by that on the company mark's colour.
        /// </summary>
        [Test]
        public void WhatSomebodyWantsNeverChanges()
        {
            var hire = Somebody("Ada Kowalska", 120);
            var first = StaffExpectations.For(hire);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                CollectionAssert.AreEqual(first, StaffExpectations.For(hire),
                    "The same person asked for something different on a later read.");
            }

            // And a different person is a different answer, or this is a constant with extra steps.
            var others = new HashSet<string>();

            foreach (var name in new[] { "A", "B", "C", "D", "E", "F", "G", "H" })
            {
                others.Add(string.Join(",", StaffExpectations.For(Somebody(name, 30))));
            }

            Assert.Greater(others.Count, 1,
                "Everybody wants exactly the same thing, so this is not an expectation, it is a rule.");
        }

        /// <summary>
        /// Most people ask for nothing, and nobody asks for more than two things.
        ///
        /// If everybody arrived with a list, the benefits screen would stop being a decision and
        /// become a checklist to finish, and the panel would say the same thing about everybody.
        /// </summary>
        [Test]
        public void MostPeopleAskForNothingInParticular()
        {
            var withNone = 0;
            var mostWanted = 0;
            const int People = 400;

            for (var index = 0; index < People; index++)
            {
                var wanted = StaffExpectations.For(Somebody("Person " + index, index * 7));

                if (wanted.Count == 0)
                {
                    withNone++;
                }

                mostWanted = Math.Max(mostWanted, wanted.Count);
            }

            var share = withNone / (double)People;

            Assert.That(share, Is.EqualTo(StaffExpectations.ShareWithNoExpectations).Within(0.10),
                $"{share:P0} of people asked for nothing.");

            Assert.LessOrEqual(mostWanted, 2, "Nobody should arrive with a shopping list.");
        }

        /// <summary>
        /// Meeting what somebody asked for is worth more than the benefit is worth to everybody.
        ///
        /// The whole design in one assertion. Two identical people, the same benefit offered, and
        /// the one who asked for it is more attached than the one who did not.
        /// </summary>
        [Test]
        public void GettingWhatYouAskedForIsWorthMoreThanTheBenefitAlone()
        {
            var today = new GameDate(900);

            // Somebody who wants something, and somebody who wants nothing.
            Hire asks = default;
            Hire quiet = default;

            for (var index = 0; index < 500 && (asks.Name == null || quiet.Name == null); index++)
            {
                var person = Somebody("Person " + index, 100);
                var wanted = StaffExpectations.For(person);

                if (wanted.Count == 1 && asks.Name == null)
                {
                    asks = person;
                }

                if (wanted.Count == 0 && quiet.Name == null)
                {
                    quiet = person;
                }
            }

            Assert.IsNotNull(asks.Name, "No sample employee asked for exactly one thing.");
            Assert.IsNotNull(quiet.Name, "No sample employee asked for nothing.");

            var offered = new HashSet<StaffBenefit>(StaffExpectations.For(asks));
            var points = BenefitCatalog.PointsFor(offered);

            var attached = Loyalty.For(asks, today, points, 0L, offered);
            var ordinary = Loyalty.For(quiet, today, points, 0L, offered);

            Assert.Greater(attached, ordinary,
                "The person who asked for this benefit is no more attached than the one who did "
                + "not, so asking means nothing.");

            // And not getting it costs, rather than merely not paying.
            var withoutIt = Loyalty.For(asks, today, 0.0, 0L, new HashSet<StaffBenefit>());
            var quietWithout = Loyalty.For(quiet, today, 0.0, 0L, new HashSet<StaffBenefit>());

            Assert.Less(withoutIt, quietWithout,
                "Asking for something and not getting it should cost more than never asking.");
        }

        /// <summary>
        /// A bonus buys time, it is capped, and past the cap only time works.
        /// </summary>
        [Test]
        public void ABonusBuysSettlingInAndThenStopsBuyingIt()
        {
            var simulation = Company();
            var state = simulation.State;

            Assert.IsTrue(state.Staff.Add(Somebody("Marek Nowak", state.Date.DayIndex)));

            var monthly = state.Staff.Hires[0].SalaryPerYearUsd / 12;

            Assert.Greater(simulation.BonusDaysFor(0, monthly), 0,
                "A month of salary buys nothing at all.");

            var before = state.CashUsd;

            Assert.IsTrue(simulation.TryPayBonus(0, monthly, out var why), why);
            Assert.Less(state.CashUsd, before, "A bonus that costs nothing is not a bonus.");
            Assert.Greater(state.Staff.Hires[0].BonusDays, 0);

            // Somebody with more time bought is more attached than the same person without it.
            var today = state.Date.AddDays(200);
            var plain = Somebody("Marek Nowak", state.Date.DayIndex);

            Assert.Greater(
                Loyalty.For(state.Staff.Hires[0], today, 0.0, 0L),
                Loyalty.For(plain, today, 0.0, 0L),
                "The bonus is not reaching loyalty at all.");

            // And it runs out. Pay until the cap and the next one is refused rather than taken.
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (!simulation.TryPayBonus(0, monthly * 6, out _))
                {
                    break;
                }
            }

            Assert.LessOrEqual(state.Staff.Hires[0].BonusDays, Hire.MostBonusDays);
            Assert.IsFalse(simulation.TryPayBonus(0, monthly * 6, out var refused),
                "Money keeps buying loyalty past the cap.");

            Assert.IsNotEmpty(refused, "A refusal has to say why.");
        }

        /// <summary>Hours can be moved and cannot be made to end before they start.</summary>
        [Test]
        public void HoursCanBeMovedAndNeverRunBackwards()
        {
            var simulation = Company();
            var state = simulation.State;

            state.Staff.Add(Somebody("Ola Zielinska", state.Date.DayIndex));

            Assert.AreEqual(Hire.DefaultStartHour, state.Staff.Hires[0].StartHour);
            Assert.AreEqual(Hire.DefaultEndHour, state.Staff.Hires[0].EndHour);
            Assert.AreEqual(8, state.Staff.Hires[0].HoursPerDay);

            Assert.IsTrue(simulation.TrySetHours(0, 10, 18, out var why), why);
            Assert.AreEqual(10, state.Staff.Hires[0].StartHour);
            Assert.AreEqual(18, state.Staff.Hires[0].EndHour);

            Assert.IsFalse(simulation.TrySetHours(0, 14, 9, out var refused));
            Assert.IsNotEmpty(refused);

            Assert.AreEqual(10, state.Staff.Hires[0].StartHour,
                "A refused change still moved the hours.");
        }

        /// <summary>
        /// A bonus and a schedule survive a save, because loyalty reads one of them.
        ///
        /// **The fifth time in this project something that looked like a display value turned out
        /// to be causal.** A campaign reloaded without the bonus replays differently, which is
        /// exactly the class of bug the save replay tests exist to catch.
        /// </summary>
        [Test]
        public void ABonusAndAScheduleSurviveASave()
        {
            var simulation = Company(88);
            var state = simulation.State;

            state.Staff.Add(Somebody("Piotr Lis", state.Date.DayIndex));
            simulation.TryPayBonus(0, state.Staff.Hires[0].SalaryPerYearUsd / 12, out _);
            simulation.TrySetHours(0, 6, 14, out _);

            var paid = state.Staff.Hires[0].BonusDays;
            Assert.Greater(paid, 0);

            var json = UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state));
            var restored = SaveStore.Restore(SaveStore.Parse(json));

            Assert.AreEqual(paid, restored.Staff.Hires[0].BonusDays,
                "The bonus was lost on load, so a reloaded campaign has different loyalty.");

            Assert.AreEqual(6, restored.Staff.Hires[0].StartHour);
            Assert.AreEqual(14, restored.Staff.Hires[0].EndHour);
        }

        /// <summary>A v45 file arrives with the hours every company has implicitly been running.</summary>
        [Test]
        public void AnOlderSaveGetsTheShiftItWasAlreadyWorking()
        {
            var older = new SaveData { version = 45 };
            older.staff.Add(new HireData { role = (int)StaffRole.InfrastructureEngineer, skill = 3 });

            var upgraded = SaveMigration.UpgradeV45ToV46(older);

            Assert.AreEqual(46, upgraded.version);
            Assert.AreEqual(0, upgraded.staff[0].bonusDays,
                "Nobody has ever been paid a bonus, so nobody is credited one.");

            Assert.AreEqual(Hire.DefaultStartHour, upgraded.staff[0].startHour);
            Assert.AreEqual(Hire.DefaultEndHour, upgraded.staff[0].endHour);
        }
    }
}
