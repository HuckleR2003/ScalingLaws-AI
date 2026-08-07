using System;

namespace ScalingLaws.Core
{
    /// <summary>
    /// The unit vocabulary of the whole simulation, in one place so nobody invents a second one.
    ///
    /// Compute is measured in petaflop/s-days (PF-days): one petaflop/s of throughput running for
    /// one day, which is 8.64e19 FLOP. It is the unit the real scaling-law papers use, and it keeps
    /// training budgets in numbers a player can hold in their head (GPT-3 was roughly 3600 PF-days).
    ///
    /// Money is whole US dollars in a <see cref="long"/>. No decimals anywhere in the simulation.
    /// Token counts are billions of tokens, stored as <see cref="double"/>.
    /// </summary>
    public static class SimUnits
    {
        public const double FlopsPerPetaflop = 1e15;
        public const double SecondsPerDay = 86400.0;
        public const double FlopPerPetaflopDay = FlopsPerPetaflop * SecondsPerDay;

        public const double TokensPerBillion = 1e9;
        public const double ParametersPerBillion = 1e9;

        /// <summary>Hours in a day, for per-hour rental pricing.</summary>
        public const double HoursPerDay = 24.0;

        public static double FlopToPetaflopDays(double flop) => flop / FlopPerPetaflopDay;

        public static double PetaflopDaysToFlop(double petaflopDays) => petaflopDays * FlopPerPetaflopDay;

        /// <summary>Days a run takes at a sustained throughput. Returns infinity when there is no compute.</summary>
        public static double DaysAtThroughput(double petaflopDays, double petaflops)
        {
            return petaflops <= 0.0 ? double.PositiveInfinity : petaflopDays / petaflops;
        }

        /// <summary>Rounds a day count up to a whole day, floored at one. Training never takes zero days.</summary>
        public static int WholeDays(double days)
        {
            if (double.IsNaN(days) || double.IsInfinity(days) || days >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(1, (int)Math.Ceiling(days));
        }

        /// <summary>Guards a double that is about to become money or a rate. NaN and infinity become zero.</summary>
        /// <summary>
        /// Rounds a value to a grid the save format can actually hold.
        ///
        /// JsonUtility writes a double at about fifteen significant digits, so a value carrying
        /// seventeen comes back subtly different and a restored campaign is no longer the campaign
        /// that was saved. A number that cannot survive its own save file is not well defined state,
        /// so anything destined for the save is put on a grid first rather than being repaired
        /// afterwards. One part in a billion is far below anything the game measures.
        /// </summary>
        public const int StorableDigits = 9;

        public static double Storable(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : Math.Round(value, StorableDigits);

        public static double Finite(double value, double fallback = 0.0)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;
        }

        /// <summary>Converts a finite double to whole dollars without overflowing.</summary>
        public static long ToDollars(double value)
        {
            var safe = Finite(value);
            return safe switch
            {
                >= 9.2e18 => long.MaxValue,
                <= -9.2e18 => long.MinValue,
                _ => (long)Math.Round(safe)
            };
        }
    }
}
