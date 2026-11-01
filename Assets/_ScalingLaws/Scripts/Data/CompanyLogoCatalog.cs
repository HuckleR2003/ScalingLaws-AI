using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The marks a player may put on their own company.
    ///
    /// **Stored by name, never by index.** Dropping another mark into the folder renumbers every
    /// logo after it, and an index written into a save would quietly turn an existing company's
    /// badge into somebody else's. Same rule, for the same reason, as the founder look.
    ///
    /// **Rivals keep their own marks in `Resources/Labs` and these are deliberately separate.** A
    /// lab logo is authored for one company and belongs to it; these are a set to choose from, and
    /// mixing the two folders would let a player wear a competitor's badge.
    /// </summary>
    public static class CompanyLogoCatalog
    {
        /// <summary>Where the files live, relative to `Resources`.</summary>
        public const string ResourceFolder = "CompanyLogos/";

        private const string Prefix = "custom_logo_";

        /// <summary>How many marks ship. Seventeen, delivered 2026-10-30, all 200x200.</summary>
        public const int Count = 17;

        /// <summary>The first mark, and what a company falls back to when its own cannot be read.</summary>
        public static string Default => NameOf(1);

        /// <summary>The stored name of the nth mark, counting from one as the files do.</summary>
        public static string NameOf(int number) =>
            Prefix + Math.Clamp(number, 1, Count).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Whether a stored name is one this build actually ships.</summary>
        public static bool Knows(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            for (var number = 1; number <= Count; number++)
            {
                if (string.Equals(NameOf(number), name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The position of a stored name in the set, for a chooser that steps left and right.
        /// An unknown name reads as the first, which is what a save from a build with more marks
        /// in it should do rather than throw.
        /// </summary>
        public static int NumberOf(string name)
        {
            for (var number = 1; number <= Count; number++)
            {
                if (string.Equals(NameOf(number), name, StringComparison.Ordinal))
                {
                    return number;
                }
            }

            return 1;
        }

        /// <summary>Steps through the set and wraps, so neither arrow is ever a dead control.</summary>
        public static string Step(string name, int by)
        {
            var number = NumberOf(name) - 1 + by;
            var wrapped = ((number % Count) + Count) % Count;

            return NameOf(wrapped + 1);
        }
    }
}
