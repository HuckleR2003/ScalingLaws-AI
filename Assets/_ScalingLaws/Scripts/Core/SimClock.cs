using System;

namespace ScalingLaws.Core
{
    /// <summary>How fast wall-clock seconds turn into campaign days.</summary>
    public enum SimSpeed
    {
        Paused = 0,
        Slow = 1,
        Normal = 2,
        Fast = 3,
        Turbo = 4
    }

    /// <summary>
    /// Turns real elapsed seconds into whole campaign days. The clock never runs the simulation
    /// itself: it hands out an integer day count and the caller ticks that many days. That keeps
    /// a 60 fps session and a batch-mode test on exactly the same code path.
    /// </summary>
    public sealed class SimClock
    {
        /// <summary>Real seconds per campaign day at <see cref="SimSpeed.Normal"/>.</summary>
        public const double SecondsPerDayNormal = 1.5;

        /// <summary>A single frame never advances more than this, so an alt-tab cannot skip a year.</summary>
        public const int MaximumDaysPerAdvance = 30;

        private double accumulator;

        public SimClock(GameDate startDate = default, SimSpeed speed = SimSpeed.Normal)
        {
            Today = startDate;
            Speed = speed;
        }

        public GameDate Today { get; private set; }

        public SimSpeed Speed { get; set; }

        /// <summary>Days per real second at the current speed. Zero while paused.</summary>
        public double DaysPerSecond => Speed switch
        {
            SimSpeed.Slow => 1.0 / (SecondsPerDayNormal * 3.0),
            SimSpeed.Normal => 1.0 / SecondsPerDayNormal,
            SimSpeed.Fast => 3.0 / SecondsPerDayNormal,
            SimSpeed.Turbo => 10.0 / SecondsPerDayNormal,
            _ => 0.0
        };

        /// <summary>
        /// Feeds real time in and returns how many whole days elapsed. The fractional remainder is
        /// carried, so no time is lost between frames.
        /// </summary>
        public int Advance(double realDeltaSeconds)
        {
            if (double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds) || realDeltaSeconds <= 0.0)
            {
                return 0;
            }

            accumulator += Math.Min(realDeltaSeconds, 5.0) * DaysPerSecond;
            if (accumulator < 1.0)
            {
                return 0;
            }

            var days = (int)Math.Floor(accumulator);
            accumulator -= days;

            if (days > MaximumDaysPerAdvance)
            {
                days = MaximumDaysPerAdvance;
                accumulator = 0.0;
            }

            Today = Today.AddDays(days);
            return days;
        }

        /// <summary>
        /// How far into the current day the clock has run, 0 to 1. The simulation only ever moves in
        /// whole days, so this is presentation: it is what the day bar and the clock face read from,
        /// and nothing in the rules is allowed to depend on it.
        /// </summary>
        public double DayProgress => Speed == SimSpeed.Paused ? 0.0 : Math.Clamp(accumulator, 0.0, 1.0);

        /// <summary>Jumps the calendar without consuming real time. Used by save loading and tests.</summary>
        public void SetDate(GameDate date)
        {
            Today = date;
            accumulator = 0.0;
        }
    }
}
