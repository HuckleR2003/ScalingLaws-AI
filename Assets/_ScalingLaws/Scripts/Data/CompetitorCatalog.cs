using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The ONE competitor library: the real release timeline of the 2022 to 2026 model race, scored
    /// on the same 0 to 100 capability scale the player's own models use.
    ///
    /// The capability numbers are a coarse relative class, not a benchmark. They exist to say
    /// "clearly ahead / roughly level / a generation behind" and nothing more precise than that.
    /// Calibration anchors: a 2022 chat model sits near 30, a 2023 frontier model near 46, a 2025
    /// reasoning model near 65.
    ///
    /// Entries dated after the known timeline are marked IsProjection. Past the last entry the
    /// frontier keeps climbing at <see cref="ProjectedCapabilityGainPerYear"/>, because the race
    /// does not stop just because this table does.
    /// </summary>
    public static class CompetitorCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        /// <summary>
        /// Capability the frontier gains each year once the table runs out. Roughly a tripling of
        /// training compute per year, which on this scale is worth about five points.
        /// </summary>
        public const double ProjectedCapabilityGainPerYear = 5.0;

        private static readonly CompetitorRelease[] Entries =
        {
            new(CompetitorId.OpenAi, "Chat assistant launch", GameDate.FromCalendar(2022, 11, 30),
                capability: 31.0, brandStrength: 0.55, priceMultiplier: 1.00, isProjection: false),
            new(CompetitorId.Anthropic, "Claude", GameDate.FromCalendar(2023, 3, 14),
                capability: 33.0, brandStrength: 0.30, priceMultiplier: 1.05, isProjection: false),
            new(CompetitorId.OpenAi, "GPT-4", GameDate.FromCalendar(2023, 3, 14),
                capability: 46.0, brandStrength: 0.72, priceMultiplier: 2.40, isProjection: false),
            new(CompetitorId.Anthropic, "Claude 2", GameDate.FromCalendar(2023, 7, 11),
                capability: 42.0, brandStrength: 0.40, priceMultiplier: 1.20, isProjection: false),
            new(CompetitorId.MetaAi, "Llama 2", GameDate.FromCalendar(2023, 7, 18),
                capability: 38.0, brandStrength: 0.45, priceMultiplier: 0.25, isProjection: false),
            new(CompetitorId.MistralAi, "Mistral 7B", GameDate.FromCalendar(2023, 9, 27),
                capability: 35.0, brandStrength: 0.25, priceMultiplier: 0.30, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Gemini 1.0", GameDate.FromCalendar(2023, 12, 6),
                capability: 45.0, brandStrength: 0.65, priceMultiplier: 1.10, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Gemini 1.5 Pro", GameDate.FromCalendar(2024, 2, 15),
                capability: 50.0, brandStrength: 0.68, priceMultiplier: 0.90, isProjection: false),
            new(CompetitorId.Anthropic, "Claude 3 Opus", GameDate.FromCalendar(2024, 3, 4),
                capability: 52.0, brandStrength: 0.58, priceMultiplier: 1.90, isProjection: false),
            new(CompetitorId.MetaAi, "Llama 3", GameDate.FromCalendar(2024, 4, 18),
                capability: 47.0, brandStrength: 0.52, priceMultiplier: 0.20, isProjection: false),
            new(CompetitorId.OpenAi, "GPT-4o", GameDate.FromCalendar(2024, 5, 13),
                capability: 53.0, brandStrength: 0.80, priceMultiplier: 0.70, isProjection: false),
            new(CompetitorId.Anthropic, "Claude 3.5 Sonnet", GameDate.FromCalendar(2024, 6, 20),
                capability: 56.0, brandStrength: 0.66, priceMultiplier: 0.60, isProjection: false),
            new(CompetitorId.OpenAi, "o1", GameDate.FromCalendar(2024, 9, 12),
                capability: 60.0, brandStrength: 0.82, priceMultiplier: 3.00, isProjection: false),
            new(CompetitorId.AlibabaQwen, "Qwen 2.5", GameDate.FromCalendar(2024, 9, 19),
                capability: 49.0, brandStrength: 0.30, priceMultiplier: 0.15, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Gemini 2.0", GameDate.FromCalendar(2024, 12, 11),
                capability: 58.0, brandStrength: 0.74, priceMultiplier: 0.45, isProjection: false),
            new(CompetitorId.DeepSeek, "DeepSeek R1", GameDate.FromCalendar(2025, 1, 20),
                capability: 60.0, brandStrength: 0.35, priceMultiplier: 0.08, isProjection: false),
            new(CompetitorId.XAi, "Grok 3", GameDate.FromCalendar(2025, 2, 17),
                capability: 58.0, brandStrength: 0.38, priceMultiplier: 0.60, isProjection: false),
            new(CompetitorId.Anthropic, "Claude 4", GameDate.FromCalendar(2025, 5, 22),
                capability: 65.0, brandStrength: 0.72, priceMultiplier: 1.30, isProjection: false),
            new(CompetitorId.OpenAi, "GPT-5", GameDate.FromCalendar(2025, 8, 7),
                capability: 68.0, brandStrength: 0.86, priceMultiplier: 1.00, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Gemini 3", GameDate.FromCalendar(2025, 11, 18),
                capability: 70.0, brandStrength: 0.80, priceMultiplier: 0.55, isProjection: false),
            new(CompetitorId.Anthropic, "Claude 4.5 Opus", GameDate.FromCalendar(2026, 2, 5),
                capability: 72.0, brandStrength: 0.78, priceMultiplier: 1.10, isProjection: false)
        };

        public static IReadOnlyList<CompetitorRelease> All => Entries;

        /// <summary>The last dated entry. Past this the frontier is projected, not tabulated.</summary>
        public static GameDate LastKnownRelease => Entries[Entries.Length - 1].ReleaseDate;

        public static IEnumerable<CompetitorRelease> LiveOn(GameDate date)
        {
            foreach (var entry in Entries)
            {
                if (entry.IsLiveOn(date))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// The best model any rival has on the market that day. After the table runs out it keeps
        /// climbing at the projected rate, so a player who stalls in 2028 still falls behind.
        /// </summary>
        public static double FrontierCapabilityOn(GameDate date)
        {
            var best = 0.0;
            foreach (var entry in Entries)
            {
                if (entry.IsLiveOn(date) && entry.Capability > best)
                {
                    best = entry.Capability;
                }
            }

            if (best <= 0.0)
            {
                // Before the first rival ships, the bar is whatever the research world had in 2021.
                return 26.0;
            }

            var lastKnown = LastKnownRelease;
            if (date <= lastKnown)
            {
                return best;
            }

            var extraYears = lastKnown.YearsUntil(date);
            return Math.Clamp(best + extraYears * ProjectedCapabilityGainPerYear, 0.0, 100.0);
        }

        /// <summary>Every rival's current best model on a given day, one entry per lab.</summary>
        public static List<CompetitorRelease> BestPerCompetitorOn(GameDate date)
        {
            var best = new Dictionary<CompetitorId, CompetitorRelease>();
            foreach (var entry in Entries)
            {
                if (!entry.IsLiveOn(date))
                {
                    continue;
                }

                if (!best.TryGetValue(entry.Competitor, out var current) || entry.Capability > current.Capability)
                {
                    best[entry.Competitor] = entry;
                }
            }

            return new List<CompetitorRelease>(best.Values);
        }
    }
}
