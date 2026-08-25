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

        /// <summary>
        /// What a lab is called. One lookup, in the data layer that owns the roster.
        ///
        /// There were two of these, one in CompetitorField and one in RankingBoard, with identical
        /// bodies. Two copies of a name list is how a lab ends up called one thing on the ranking
        /// screen and another in the news. It is now down to zero copies: the name lives on the
        /// dossier beside the lab's history, which is the only place that has to be edited if the
        /// roster is ever renamed.
        /// </summary>
        public static string NameOf(CompetitorId competitor) => LabDossiers.NameOf(competitor);

        private static readonly CompetitorRelease[] Entries =
        {
            // The one lab that starts where the player starts. Weak, cheap, and it never catches up:
            // capability climbs a few points a year while the frontier climbs ten, so by 2025 it is
            // selling something nobody wants at a price nobody needs. Marked as projection because
            // it is invented rather than taken from a real release.
            new(CompetitorId.Groq, "Early assistant", GameDate.FromCalendar(2022, 4, 12),
                capability: 12.0, brandStrength: 0.06, priceMultiplier: 0.55, isProjection: true),
            new(CompetitorId.Groq, "Assistant 2", GameDate.FromCalendar(2023, 2, 8),
                capability: 19.0, brandStrength: 0.10, priceMultiplier: 0.45, isProjection: true),
            new(CompetitorId.Groq, "Assistant 3", GameDate.FromCalendar(2024, 1, 22),
                capability: 27.0, brandStrength: 0.11, priceMultiplier: 0.35, isProjection: true),
            new(CompetitorId.Groq, "Assistant 4", GameDate.FromCalendar(2025, 5, 6),
                capability: 33.0, brandStrength: 0.09, priceMultiplier: 0.30, isProjection: true),

            // ---------------------------------------------------- the four added 2026-08-15
            //
            // Read these as arcs rather than as rows. Brand is what makes them worth having: a lab
            // whose capability keeps climbing while its brand collapses is a company in trouble,
            // and that is a thing the player can watch happen without being told.

            // Open image generation. Huge reach at almost no price, then the brand comes apart
            // faster than the capability does, which is exactly what happened.
            new(CompetitorId.StabilityAi, "Open image model", GameDate.FromCalendar(2022, 8, 22),
                capability: 26.0, brandStrength: 0.38, priceMultiplier: 0.10, isProjection: false),
            new(CompetitorId.StabilityAi, "Open image model 2", GameDate.FromCalendar(2023, 7, 26),
                capability: 32.0, brandStrength: 0.31, priceMultiplier: 0.10, isProjection: false),
            // The founder leaves in March 2024 and the layoffs follow in April. Capability creeps
            // up because the models were already trained; the name stops being worth anything.
            new(CompetitorId.StabilityAi, "Open image model 3", GameDate.FromCalendar(2024, 6, 12),
                capability: 36.0, brandStrength: 0.13, priceMultiplier: 0.10, isProjection: false),
            new(CompetitorId.StabilityAi, "Open image model 3.5", GameDate.FromCalendar(2025, 4, 8),
                capability: 40.0, brandStrength: 0.09, priceMultiplier: 0.12, isProjection: true),

            // The challenger. Climbs hard, reaches the frontier in March 2024, and twelve days
            // later the people who built it work somewhere else. The next entry is the whole story
            // in two numbers.
            new(CompetitorId.InflectionAi, "Personal assistant", GameDate.FromCalendar(2023, 5, 2),
                capability: 34.0, brandStrength: 0.17, priceMultiplier: 0.60, isProjection: false),
            new(CompetitorId.InflectionAi, "Assistant 2.5", GameDate.FromCalendar(2024, 3, 7),
                capability: 47.0, brandStrength: 0.27, priceMultiplier: 0.60, isProjection: false),
            new(CompetitorId.InflectionAi, "Enterprise pivot", GameDate.FromCalendar(2024, 3, 19),
                capability: 47.0, brandStrength: 0.05, priceMultiplier: 1.05, isProjection: false),
            // Nothing after that. Capability never moves again, which is what being hollowed out
            // looks like from the outside: the product still works and nothing new ever ships.

            // The European bid. Funded properly in late 2023, then held flat while the frontier
            // triples past it. It never collapses; it just stops being relevant, which is a
            // different and quieter kind of failure.
            new(CompetitorId.AlephAlpha, "Multilingual model", GameDate.FromCalendar(2022, 4, 14),
                capability: 24.0, brandStrength: 0.06, priceMultiplier: 1.30, isProjection: false),
            new(CompetitorId.AlephAlpha, "Multilingual model 2", GameDate.FromCalendar(2023, 11, 6),
                capability: 31.0, brandStrength: 0.15, priceMultiplier: 1.30, isProjection: false),
            new(CompetitorId.AlephAlpha, "Sovereign stack", GameDate.FromCalendar(2024, 9, 1),
                capability: 35.0, brandStrength: 0.12, priceMultiplier: 1.35, isProjection: false),
            new(CompetitorId.AlephAlpha, "Sovereign stack 2", GameDate.FromCalendar(2025, 10, 1),
                capability: 38.0, brandStrength: 0.10, priceMultiplier: 1.35, isProjection: true),

            // The survivor. Slower than the frontier at every single point on this list, and still
            // here at the end of it, because enterprise buyers do not switch every six months.
            new(CompetitorId.Cohere, "Enterprise model", GameDate.FromCalendar(2022, 11, 15),
                capability: 27.0, brandStrength: 0.09, priceMultiplier: 1.15, isProjection: false),
            new(CompetitorId.Cohere, "Enterprise model 2", GameDate.FromCalendar(2023, 6, 8),
                capability: 36.0, brandStrength: 0.15, priceMultiplier: 1.15, isProjection: false),
            new(CompetitorId.Cohere, "Retrieval model", GameDate.FromCalendar(2024, 4, 4),
                capability: 46.0, brandStrength: 0.21, priceMultiplier: 1.20, isProjection: false),
            new(CompetitorId.Cohere, "Retrieval model 2", GameDate.FromCalendar(2025, 8, 14),
                capability: 55.0, brandStrength: 0.25, priceMultiplier: 1.20, isProjection: true),

            // Emil's shop. Always a step behind the middle of the board and never falling off it,
            // which is what a small consultancy that ships what its customers asked for looks like.
            // Invented, so every entry is a projection.
            new(CompetitorId.ESolutions, "Helpdesk assistant", GameDate.FromCalendar(2023, 2, 20),
                capability: 24.0, brandStrength: 0.05, priceMultiplier: 0.92, isProjection: true),
            new(CompetitorId.ESolutions, "Helpdesk assistant 2", GameDate.FromCalendar(2024, 1, 16),
                capability: 38.0, brandStrength: 0.09, priceMultiplier: 0.92, isProjection: true),
            new(CompetitorId.ESolutions, "Back office suite", GameDate.FromCalendar(2025, 3, 4),
                capability: 49.0, brandStrength: 0.13, priceMultiplier: 0.95, isProjection: true),
            new(CompetitorId.ESolutions, "Back office suite 2", GameDate.FromCalendar(2026, 5, 19),
                capability: 61.0, brandStrength: 0.16, priceMultiplier: 0.95, isProjection: true),

            new(CompetitorId.OpenAi, "Chat assistant launch", GameDate.FromCalendar(2022, 11, 30),
                capability: 31.0, brandStrength: 0.55, priceMultiplier: 1.00, isProjection: false),
            new(CompetitorId.Anthropic, "Clyde", GameDate.FromCalendar(2023, 3, 14),
                capability: 33.0, brandStrength: 0.30, priceMultiplier: 1.05, isProjection: false),
            new(CompetitorId.OpenAi, "SI-4", GameDate.FromCalendar(2023, 3, 14),
                capability: 46.0, brandStrength: 0.72, priceMultiplier: 2.40, isProjection: false),
            new(CompetitorId.Anthropic, "Clyde 2", GameDate.FromCalendar(2023, 7, 11),
                capability: 42.0, brandStrength: 0.40, priceMultiplier: 1.20, isProjection: false),
            new(CompetitorId.MetaAi, "Lyra 2", GameDate.FromCalendar(2023, 7, 18),
                capability: 38.0, brandStrength: 0.45, priceMultiplier: 0.25, isProjection: false),
            new(CompetitorId.MistralAi, "Astral 7B", GameDate.FromCalendar(2023, 9, 27),
                capability: 35.0, brandStrength: 0.25, priceMultiplier: 0.30, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Twin 1.0", GameDate.FromCalendar(2023, 12, 6),
                capability: 45.0, brandStrength: 0.65, priceMultiplier: 1.10, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Twin 1.5 Pro", GameDate.FromCalendar(2024, 2, 15),
                capability: 50.0, brandStrength: 0.68, priceMultiplier: 0.90, isProjection: false),
            new(CompetitorId.Anthropic, "Clyde 3", GameDate.FromCalendar(2024, 3, 4),
                capability: 52.0, brandStrength: 0.58, priceMultiplier: 1.90, isProjection: false),
            new(CompetitorId.MetaAi, "Lyra 3", GameDate.FromCalendar(2024, 4, 18),
                capability: 47.0, brandStrength: 0.52, priceMultiplier: 0.20, isProjection: false),
            new(CompetitorId.OpenAi, "SI-4o", GameDate.FromCalendar(2024, 5, 13),
                capability: 53.0, brandStrength: 0.80, priceMultiplier: 0.70, isProjection: false),
            new(CompetitorId.Anthropic, "Clyde 3.5", GameDate.FromCalendar(2024, 6, 20),
                capability: 56.0, brandStrength: 0.66, priceMultiplier: 0.60, isProjection: false),
            new(CompetitorId.OpenAi, "SI-o1", GameDate.FromCalendar(2024, 9, 12),
                capability: 60.0, brandStrength: 0.82, priceMultiplier: 3.00, isProjection: false),
            new(CompetitorId.AlibabaQwen, "Swen 2.5", GameDate.FromCalendar(2024, 9, 19),
                capability: 49.0, brandStrength: 0.30, priceMultiplier: 0.15, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Twin 2.0", GameDate.FromCalendar(2024, 12, 11),
                capability: 58.0, brandStrength: 0.74, priceMultiplier: 0.45, isProjection: false),
            new(CompetitorId.DeepSeek, "Reason 1", GameDate.FromCalendar(2025, 1, 20),
                capability: 60.0, brandStrength: 0.35, priceMultiplier: 0.08, isProjection: false),
            new(CompetitorId.XAi, "Grak 3", GameDate.FromCalendar(2025, 2, 17),
                capability: 58.0, brandStrength: 0.38, priceMultiplier: 0.60, isProjection: false),
            new(CompetitorId.Anthropic, "Clyde 4", GameDate.FromCalendar(2025, 5, 22),
                capability: 65.0, brandStrength: 0.72, priceMultiplier: 1.30, isProjection: false),
            new(CompetitorId.OpenAi, "SI-5", GameDate.FromCalendar(2025, 8, 7),
                capability: 68.0, brandStrength: 0.86, priceMultiplier: 1.00, isProjection: false),
            new(CompetitorId.GoogleDeepMind, "Twin 3", GameDate.FromCalendar(2025, 11, 18),
                capability: 70.0, brandStrength: 0.80, priceMultiplier: 0.55, isProjection: false),
            new(CompetitorId.Anthropic, "Clyde 4.5", GameDate.FromCalendar(2026, 2, 5),
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

                // Capability first, then the later release. The tie-break is not cosmetic: a lab
                // whose next entry is the same capability at a fraction of the brand is a lab that
                // just lost its people, and without this the market went on seeing the old,
                // confident version of them forever.
                if (!best.TryGetValue(entry.Competitor, out var current)
                    || entry.Capability > current.Capability
                    || (entry.Capability >= current.Capability
                        && entry.ReleaseDate.IsOnOrAfter(current.ReleaseDate)))
                {
                    best[entry.Competitor] = entry;
                }
            }

            return new List<CompetitorRelease>(best.Values);
        }
    }
}
