using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Which grant. Explicit values, written into saves, never renumbered.
    /// </summary>
    public enum GrantId
    {
        None = 0,
        ResearchFellowship = 1,
        OpenBenchmark = 2,
        RegionalEmployment = 3,
        PublicAccess = 4,
        ReliabilityPledge = 5,
        SafetyDisclosure = 6,
        ContinuityAward = 7,
        StandardsStipend = 8
    }

    /// <summary>
    /// What the awarding body wants in return.
    ///
    /// **Two shapes, and the difference is the whole design.** A counting goal is something the
    /// company was probably going to do anyway, now with a date on it. A sustained goal has to hold
    /// on every single day of the term, which means it decides how the company is run rather than
    /// what it achieves, and one bad day loses it.
    /// </summary>
    public enum GrantGoal
    {
        /// <summary>Release this many models before the term is up.</summary>
        ReleaseModels = 0,

        /// <summary>Raise the flagship this far above where it stood on the day of the award.</summary>
        ReachCapability = 1,

        /// <summary>Finish this many research nodes.</summary>
        FinishResearch = 2,

        /// <summary>Employ this many people by the closing date.</summary>
        EmployPeople = 3,

        /// <summary>Keep the free tier at least this generous, every day.</summary>
        SustainFreeTier = 4,

        /// <summary>Keep the fleet below this load, every day.</summary>
        SustainHeadroom = 5,

        /// <summary>Keep reputation at or above this, every day.</summary>
        SustainReputation = 6,

        /// <summary>Ship a model carrying at least this tier of data protection.</summary>
        ShipProtected = 7
    }

    /// <summary>One programme somebody is prepared to fund.</summary>
    public readonly struct GrantDefinition
    {
        public GrantDefinition(GrantId id, GrantGoal goal, double target, int termDays,
            long advanceUsd, long completionUsd, double researchPoints, int earliestYear)
        {
            Id = id;
            Goal = goal;
            Target = SimUnits.Finite(target);
            TermDays = Math.Clamp(termDays, 30, 2000);
            AdvanceUsd = Math.Max(0L, advanceUsd);
            CompletionUsd = Math.Max(0L, completionUsd);
            ResearchPoints = Math.Max(0.0, SimUnits.Finite(researchPoints));
            EarliestYear = earliestYear;
        }

        public GrantId Id { get; }
        public GrantGoal Goal { get; }

        /// <summary>How many, how far, or how high, depending on the goal.</summary>
        public double Target { get; }

        public int TermDays { get; }

        /// <summary>Paid on accepting, and **repayable if the term is missed.**</summary>
        public long AdvanceUsd { get; }

        /// <summary>Paid on meeting the condition.</summary>
        public long CompletionUsd { get; }

        /// <summary>
        /// The part money cannot buy.
        ///
        /// Research points are the one currency in this game with no cash price beyond a square
        /// root curve, so a body handing them over is offering something the player genuinely
        /// cannot get faster by being rich. That is what makes a small grant worth reading.
        /// </summary>
        public double ResearchPoints { get; }

        /// <summary>Nobody funds work in a field that does not exist yet.</summary>
        public int EarliestYear { get; }

        /// <summary>The phrase-book stem. Written out per entry, never built by concatenation.</summary>
        public string NameKey => KeyFor(Id, "name");
        public string BodyKey => KeyFor(Id, "body");
        public string TermsKey => KeyFor(Id, "terms");

        private static string KeyFor(GrantId id, string part) => id switch
        {
            GrantId.ResearchFellowship => "grant.fellowship." + part,
            GrantId.OpenBenchmark => "grant.benchmark." + part,
            GrantId.RegionalEmployment => "grant.employment." + part,
            GrantId.PublicAccess => "grant.access." + part,
            GrantId.ReliabilityPledge => "grant.reliability." + part,
            GrantId.SafetyDisclosure => "grant.safety." + part,
            GrantId.ContinuityAward => "grant.continuity." + part,
            GrantId.StandardsStipend => "grant.standards." + part,
            _ => "grant.unknown." + part
        };
    }

    /// <summary>
    /// Money from outside, with a string attached.
    ///
    /// **A grant is not income and it must never become income.** Every one of these pays an
    /// advance the company has to give back if it misses the term, so accepting is a bet rather
    /// than a windfall, and the sustained ones cost real money to hold: a generous free tier is
    /// revenue given away, and spare fleet headroom is capacity nobody is paying for. The body is
    /// paying the company to run itself their way, which is exactly what a grant is.
    ///
    /// That framing is what keeps this off the wrong side of the spine at the top of `CLAUDE.md`.
    /// Guaranteed income, or capital that skips a calendar gate, would be working against the
    /// design. Sums are deliberately small against a company holding twelve million on day one.
    /// </summary>
    public static class GrantCatalog
    {
        public const string CatalogVersion = "2026.08.29";

        /// <summary>How long an offer stays on the table before it lapses.</summary>
        public const int OfferOpenDays = 60;

        /// <summary>The most offers on the board at once. More than this is a to-do list.</summary>
        public const int MostOpenOffers = 3;

        /// <summary>The most awards a company can be holding at once.</summary>
        public const int MostHeldAtOnce = 2;

        /// <summary>How long before a dismissed or lapsed programme comes round again.</summary>
        public const int QuietDaysAfterDeclining = 420;

        /// <summary>Chance per day that a body with something to fund gets in touch.</summary>
        public const double ChancePerDay = 0.006;

        /// <summary>What missing the term costs on top of repaying the advance.</summary>
        public const double ReputationCostOfFailing = -0.03;

        private static readonly GrantDefinition[] Entries =
        {
            //                                   goal                          target term   advance   completion  points  from
            new(GrantId.StandardsStipend,   GrantGoal.ReleaseModels,      3,     480,   100_000,    500_000,   40, 2022),
            new(GrantId.ResearchFellowship, GrantGoal.FinishResearch,     2,     540,   120_000,    600_000,   90, 2022),
            new(GrantId.ContinuityAward,    GrantGoal.SustainReputation,  0.45,  360,   140_000,    700_000,   45, 2022),
            new(GrantId.OpenBenchmark,      GrantGoal.ReachCapability,    6,     450,   150_000,    900_000,   70, 2023),
            new(GrantId.ReliabilityPledge,  GrantGoal.SustainHeadroom,    0.75,  300,   160_000,    800_000,   55, 2023),
            new(GrantId.PublicAccess,       GrantGoal.SustainFreeTier,    0.60,  270,   180_000,    850_000,   60, 2023),
            new(GrantId.RegionalEmployment, GrantGoal.EmployPeople,       6,     360,   200_000,    750_000,   30, 2024),
            new(GrantId.SafetyDisclosure,   GrantGoal.ShipProtected,      1,     420,   220_000,  1_400_000,  160, 2024)
        };

        public static IReadOnlyList<GrantDefinition> All => Entries;

        public static bool TryGet(GrantId id, out GrantDefinition definition)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    definition = entry;
                    return true;
                }
            }

            definition = default;
            return false;
        }

        public static GrantDefinition Get(GrantId id) =>
            TryGet(id, out var definition) ? definition : Entries[0];

        /// <summary>Programmes that exist yet on this date.</summary>
        public static IEnumerable<GrantDefinition> OpenOn(GameDate date)
        {
            foreach (var entry in Entries)
            {
                if (date.Year >= entry.EarliestYear)
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Whether the goal has to hold every day, or only by the closing date.
        ///
        /// Written as a switch rather than a range check on the enum, so adding a member forces a
        /// decision here instead of silently landing on whichever side the numbers fall.
        /// </summary>
        public static bool IsSustained(GrantGoal goal) => goal switch
        {
            GrantGoal.SustainFreeTier => true,
            GrantGoal.SustainHeadroom => true,
            GrantGoal.SustainReputation => true,
            _ => false
        };
    }
}
