using NUnit.Framework;
using ScalingLaws.Core;

namespace ScalingLaws.Tests.EditMode
{
    public sealed class GameDateTests
    {
        [Test]
        public void DayZeroIsTheFirstOfJanuary2022()
        {
            var start = GameDate.Start;

            Assert.That(start.Year, Is.EqualTo(2022));
            Assert.That(start.Month, Is.EqualTo(1));
            Assert.That(start.Day, Is.EqualTo(1));
            Assert.That(start.ToString(), Is.EqualTo("2022-01-01"));
        }

        [Test]
        public void CalendarRoundTripsThroughTheDayIndex()
        {
            var date = GameDate.FromCalendar(2024, 6, 20);

            Assert.That(date.Year, Is.EqualTo(2024));
            Assert.That(date.Month, Is.EqualTo(6));
            Assert.That(date.Day, Is.EqualTo(20));
            Assert.That(new GameDate(date.DayIndex), Is.EqualTo(date));
        }

        [Test]
        public void HardwareOlderThanTheCampaignKeepsItsOrdering()
        {
            // The catalog holds parts that shipped years before day zero. If those clamped to the
            // same day the game could not tell which generation succeeded which.
            var volta = GameDate.FromCalendar(2017, 6, 21);
            var ampere = GameDate.FromCalendar(2020, 11, 16);

            Assert.That(volta.DayIndex, Is.LessThan(0));
            Assert.That(volta, Is.LessThan(ampere));
            Assert.That(ampere, Is.LessThan(GameDate.Start));
        }

        [Test]
        public void ArithmeticAndComparisonAgree()
        {
            var start = GameDate.FromCalendar(2023, 1, 1);
            var later = start.AddDays(365);

            Assert.That(later.Year, Is.EqualTo(2024));
            Assert.That(later - start, Is.EqualTo(365));
            Assert.That(start.DaysUntil(later), Is.EqualTo(365));
            Assert.That(start.YearsUntil(later), Is.EqualTo(1.0).Within(0.01));
            Assert.That(later.IsOnOrAfter(start), Is.True);
            Assert.That(start.IsBefore(later), Is.True);
        }

        [Test]
        public void OutOfRangeInputIsClampedRatherThanThrown()
        {
            Assert.That(new GameDate(int.MaxValue).DayIndex, Is.EqualTo(GameDate.MaximumDayIndex));
            Assert.That(new GameDate(int.MinValue).DayIndex, Is.EqualTo(GameDate.MinimumDayIndex));
            Assert.That(GameDate.FromCalendar(2024, 2, 31).Day, Is.EqualTo(29), "February 2024 has 29 days.");
            Assert.That(GameDate.FromCalendar(2024, 13, 1).Month, Is.EqualTo(12));
        }

        [Test]
        public void ClockCarriesFractionalDaysInsteadOfLosingThem()
        {
            var clock = new SimClock(GameDate.Start, SimSpeed.Normal);
            var totalDays = 0;

            // Ten ticks that are each under a day must still add up to whole days.
            for (var index = 0; index < 10; index++)
            {
                totalDays += clock.Advance(SimClock.SecondsPerDayNormal * 0.5);
            }

            Assert.That(totalDays, Is.EqualTo(5));
            Assert.That(clock.Today.DayIndex, Is.EqualTo(5));
        }

        [Test]
        public void PausedClockNeverAdvances()
        {
            var clock = new SimClock(GameDate.Start, SimSpeed.Paused);

            Assert.That(clock.Advance(60.0), Is.Zero);
            Assert.That(clock.Today, Is.EqualTo(GameDate.Start));
        }
    }
}
