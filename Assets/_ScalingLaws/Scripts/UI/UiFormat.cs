using System;
using System.Globalization;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Number formatting for every screen, in one place. Invariant culture on purpose: the numbers
    /// are the same in every language and a comma appearing where a dot belongs is a support ticket.
    /// </summary>
    public static class UiFormat
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static string Money(long amount)
        {
            var sign = amount < 0 ? "-" : string.Empty;
            return sign + FormatMagnitude(Math.Abs(amount));
        }

        private static string FormatMagnitude(long value)
        {
            return value switch
            {
                >= 1_000_000_000_000 => "$" + (value / 1_000_000_000_000.0).ToString("0.##", Culture) + "T",
                >= 1_000_000_000 => "$" + (value / 1_000_000_000.0).ToString("0.##", Culture) + "B",
                >= 1_000_000 => "$" + (value / 1_000_000.0).ToString("0.##", Culture) + "M",
                >= 1_000 => "$" + (value / 1_000.0).ToString("0.#", Culture) + "k",
                _ => "$" + value.ToString(Culture)
            };
        }

        /// <summary>Full precision with separators, for anything that reads like a bank balance.</summary>
        public static string MoneyExact(long amount) => amount.ToString("C0", Culture);

        public static string Count(double value)
        {
            return value switch
            {
                >= 1_000_000_000 => (value / 1_000_000_000.0).ToString("0.##", Culture) + "B",
                >= 1_000_000 => (value / 1_000_000.0).ToString("0.##", Culture) + "M",
                >= 1_000 => (value / 1_000.0).ToString("0.#", Culture) + "k",
                _ => value.ToString("0.##", Culture)
            };
        }

        public static string Billions(double billions)
        {
            return billions >= 1000
                ? (billions / 1000.0).ToString("0.##", Culture) + "T"
                : billions.ToString("0.##", Culture) + "B";
        }

        public static string Percent(double fraction, int decimals = 1) =>
            (fraction * 100.0).ToString("F" + decimals, Culture) + "%";

        public static string Number(double value, int decimals = 1) =>
            value.ToString("F" + decimals, Culture);

        public static string Days(int days)
        {
            if (days < 60)
            {
                // Counted, because Polish has three forms and "1 dni" is the sort of thing that
                // makes a translation read as a machine translation.
                return days + " " + Loc.Plural(days, "noun.day");
            }

            var months = days / 30.4375;
            return months.ToString("0.#", Culture) + " "
                + Loc.Plural((int)System.Math.Round(months), "noun.month");
        }

        public static string Petaflops(double petaflops) => Count(petaflops) + " PF";

        /// <summary>
        /// Power, one decimal.
        ///
        /// **Here rather than as a format string at the call site**, which is how the server room
        /// shipped "22,6 kW" on its first render: a raw `:0.0` follows the machine's culture and
        /// this one is Polish. Every number the player reads goes through this file for exactly
        /// that reason and it has caught the same fault three times.
        /// </summary>
        public static string Kilowatts(double kilowatts) =>
            Number(kilowatts, 1) + " kW";

        /// <summary>Milliseconds, whole. Same reason.</summary>
        public static string Milliseconds(double milliseconds) =>
            Number(milliseconds, 0) + " ms";

        public static string PetaflopDays(double petaflopDays) => Count(petaflopDays) + " PF-days";
    }
}
