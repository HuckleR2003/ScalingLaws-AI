using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// How many people were registered on each of the last few months of days.
    ///
    /// A chart needs a past and the simulation did not keep one: every readout so far has described
    /// today. This is the smallest thing that fixes that, a ring of daily counts, written once a tick
    /// by the code that already knows the answer.
    ///
    /// Ninety days rather than the whole campaign. A fifteen year game is five and a half thousand
    /// numbers in every save, nobody reads day four hundred, and a chart that wide is a smear.
    /// </summary>
    public sealed class UserHistory
    {
        public const int DaysKept = 90;

        private readonly double[] days = new double[DaysKept];
        private int written;

        /// <summary>How many days have actually been recorded, up to the ring size.</summary>
        public int Count => Math.Min(written, DaysKept);

        public void Record(double registeredUsers)
        {
            days[written % DaysKept] = Math.Max(0.0, SimUnits.Finite(registeredUsers));
            written++;
        }

        /// <summary>Oldest first, which is the order a chart reads left to right.</summary>
        public List<double> Recent(int wanted)
        {
            var take = Math.Clamp(wanted, 0, Count);
            var series = new List<double>(take);

            for (var back = take - 1; back >= 0; back--)
            {
                series.Add(days[(written - 1 - back + DaysKept * 2) % DaysKept]);
            }

            return series;
        }

        public double Latest => Count == 0 ? 0.0 : days[(written - 1 + DaysKept) % DaysKept];

        /// <summary>Flattens oldest first for the save.</summary>
        public void Capture(List<double> into)
        {
            into.Clear();
            into.AddRange(Recent(Count));
        }

        public void Restore(IReadOnlyList<double> series)
        {
            Array.Clear(days, 0, days.Length);
            written = 0;

            if (series == null)
            {
                return;
            }

            foreach (var value in series)
            {
                Record(value);
            }
        }
    }

    /// <summary>
    /// How many of the registered are using the product at any one moment.
    ///
    /// Registered is a stock and online is a rate, and confusing the two is how a dashboard ends up
    /// claiming ten million people are typing at once. Real services see a few percent of their
    /// accounts concurrently at the busy hour and far less overnight.
    ///
    /// The shape lives here rather than in the panel so the number the player reads and the number a
    /// test asserts are the same function. It takes the hour as an argument instead of reading the
    /// clock, because the simulation moves in whole days and nothing in the rules may depend on where
    /// inside a day the presentation happens to be.
    /// </summary>
    public static class Concurrency
    {
        /// <summary>Share of registered accounts online at the busiest hour.</summary>
        public const double PeakShare = 0.048;

        /// <summary>Share online in the quietest hour before dawn.</summary>
        public const double TroughShare = 0.012;

        /// <summary>
        /// The daily rhythm, nothing to everything, for an hour of the day.
        ///
        /// Flat and low overnight, a step up when people start work, a dip at lunch and a broad
        /// evening peak. Deliberately not a sine wave: a smooth curve reads as a decoration and this
        /// one is meant to look like traffic.
        /// </summary>
        public static double ShapeAt(double hour)
        {
            var h = ((SimUnits.Finite(hour) % 24.0) + 24.0) % 24.0;

            return h switch
            {
                < 6.0 => 0.06,
                < 7.0 => 0.20,
                < 9.0 => 0.62,
                < 12.0 => 0.70,
                < 14.0 => 0.58,
                < 17.0 => 0.74,
                < 20.0 => 1.00,
                < 22.0 => 0.86,
                _ => 0.34
            };
        }

        /// <summary>How many are online at that hour, given the registered base.</summary>
        public static double OnlineAt(double registered, double hour)
        {
            var people = Math.Max(0.0, SimUnits.Finite(registered));
            var share = TroughShare + (PeakShare - TroughShare) * ShapeAt(hour);
            return people * share;
        }
    }
}
