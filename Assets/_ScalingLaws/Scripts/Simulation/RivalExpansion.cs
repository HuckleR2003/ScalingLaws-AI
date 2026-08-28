using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Rivals build offices and datacenters too, expressed as one multiplier rather than as a
    /// second copy of the player's economy.
    ///
    /// **Deliberately simplified, and the reason is the honesty flag.** Giving every lab a fleet, a
    /// rent bill and a hiring queue would mean inventing two dozen numbers per company that nobody
    /// can check, for a simulation the player never sees the inside of. What a player can see is
    /// that a competitor got bigger, and that their products started reaching further. So that is
    /// what is modelled: a level, a multiplier on their standing, and a news item when it moves.
    ///
    /// **Nothing about it is stored.** The level is a pure function of the campaign seed, the lab
    /// and the date, so it replays identically, needs no save migration, and cannot drift out of
    /// step with a news item that announced it. The announcement is derived the same way: today's
    /// level differs from yesterday's, so today is the day it is news.
    /// </summary>
    public static class RivalExpansion
    {
        /// <summary>The most a lab can grow. Four steps over fourteen years is a slow ladder.</summary>
        public const int MaximumLevel = 4;

        /// <summary>What each step adds to a lab's reach. Compounding is not used, on purpose.</summary>
        public const double BrandPerLevel = 0.055;

        /// <summary>Nobody expands in the first stretch: everybody here is small in 2022.</summary>
        public const int QuietDays = 420;

        /// <summary>
        /// Roughly how long between steps, before the per-lab spread is applied.
        ///
        /// Chosen so a lab reaches the top of the ladder near the end of a long campaign rather
        /// than halfway through it. A field that has finished growing by 2028 spends the last seven
        /// years static, which is the state this system exists to remove.
        /// </summary>
        public const int DaysPerStep = 760;

        /// <summary>How much labs differ from each other in pace, as a share of a step.</summary>
        public const double Spread = 0.45;

        /// <summary>
        /// How far along a lab is on a given day.
        ///
        /// The per-lab offset is hashed rather than random so two neighbouring labs do not expand
        /// in the same week, which would read as one scripted event rather than as a field of
        /// companies each doing their own thing.
        /// </summary>
        public static int LevelOn(uint campaignSeed, CompetitorId lab, GameDate date)
        {
            var days = date.DayIndex - QuietDays;

            if (days <= 0)
            {
                return 0;
            }

            var offset = Offset(campaignSeed, lab);
            var step = DaysPerStep * (1.0 + offset * Spread);

            if (step <= 1.0)
            {
                return MaximumLevel;
            }

            var level = (int)Math.Floor(days / step);

            return Math.Clamp(level, 0, MaximumLevel);
        }

        /// <summary>What their standing is multiplied by at that level.</summary>
        public static double BrandMultiplier(int level) =>
            1.0 + BrandPerLevel * Math.Clamp(level, 0, MaximumLevel);

        /// <summary>
        /// True on exactly the day a lab steps up, which is the day it is worth printing.
        ///
        /// Comparing against yesterday rather than keeping a record is what lets this whole system
        /// stay out of the save file. Day zero is excluded because a campaign opening with four
        /// expansion notices is a wire nobody reads.
        /// </summary>
        public static bool StepsUpOn(uint campaignSeed, CompetitorId lab, GameDate date)
        {
            if (date.DayIndex <= QuietDays)
            {
                return false;
            }

            var today = LevelOn(campaignSeed, lab, date);
            var yesterday = LevelOn(campaignSeed, lab, date.AddDays(-1));

            return today > yesterday;
        }

        /// <summary>What they built, which is only ever flavour on a number that already moved.</summary>
        public static string HeadlineKey(int level) => level switch
        {
            1 => "expand.office",
            2 => "expand.datacenter",
            3 => "expand.second_site",
            _ => "expand.region"
        };

        private static double Offset(uint campaignSeed, CompetitorId lab)
        {
            unchecked
            {
                var value = campaignSeed ^ ((uint)lab * 2654435761u);
                value ^= value >> 15;
                value *= 2246822519u;
                value ^= value >> 13;

                return value / 4294967296.0;
            }
        }
    }
}
