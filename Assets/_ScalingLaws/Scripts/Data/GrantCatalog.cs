using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Which grant. Explicit values, written into saves, never renumbered.
    ///
    /// The first eight kept their numbers when the flat list became a ladder, because a campaign
    /// saved before the rework stores these as ints and a renumbering would silently turn one
    /// programme into another.
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
        StandardsStipend = 8,

        // Added with the ladder.
        MinistrySafeStart = 9,
        MinistryFirstLine = 10,
        ContinuityTwoYears = 11,
        FrontierProgramme = 12
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
        ShipProtected = 7,

        /// <summary>
        /// Keep this many products on sale, every day, for a long term.
        ///
        /// The low bar per day is the point. What makes it hard is the length: two years of staying
        /// in business without retiring the thing that pays for it, through every incident and
        /// every quarter where the money is tight.
        /// </summary>
        SustainOnSale = 8
    }

    /// <summary>One programme somebody is prepared to fund.</summary>
    public readonly struct GrantDefinition
    {
        public GrantDefinition(GrantId id, int tier, GrantGoal goal, double target, int termDays,
            long advanceUsd, long completionUsd, double researchPoints)
        {
            Id = id;
            Tier = Math.Clamp(tier, 1, GrantCatalog.TopTier);
            Goal = goal;
            Target = SimUnits.Finite(target);
            TermDays = Math.Clamp(termDays, 30, 2000);
            AdvanceUsd = Math.Max(0L, advanceUsd);
            CompletionUsd = Math.Max(0L, completionUsd);
            ResearchPoints = Math.Max(0.0, SimUnits.Finite(researchPoints));
        }

        public GrantId Id { get; }

        /// <summary>
        /// Which rung of the ladder.
        ///
        /// A body does not fund a company it has never heard of to do the hardest thing on the
        /// list. Finishing anything on one rung is what puts the company on the next body's desk,
        /// which is how a grant record actually works and it gives the run a spine to climb.
        /// </summary>
        public int Tier { get; }

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

        /// <summary>The phrase-book stem. Written out per entry, never built by concatenation.</summary>
        public string NameKey => KeyFor(Id, "name");
        public string BodyKey => KeyFor(Id, "body");
        public string TermsKey => KeyFor(Id, "terms");

        private static string KeyFor(GrantId id, string part) => id switch
        {
            GrantId.MinistrySafeStart => "grant.safestart." + part,
            GrantId.MinistryFirstLine => "grant.firstline." + part,
            GrantId.StandardsStipend => "grant.standards." + part,
            GrantId.ResearchFellowship => "grant.fellowship." + part,
            GrantId.OpenBenchmark => "grant.benchmark." + part,
            GrantId.ContinuityTwoYears => "grant.twoyears." + part,
            GrantId.PublicAccess => "grant.access." + part,
            GrantId.ContinuityAward => "grant.continuity." + part,
            GrantId.RegionalEmployment => "grant.employment." + part,
            GrantId.ReliabilityPledge => "grant.reliability." + part,
            GrantId.FrontierProgramme => "grant.frontier." + part,
            GrantId.SafetyDisclosure => "grant.safety." + part,
            _ => "grant.unknown." + part
        };
    }

    /// <summary>
    /// Money from outside, with a string attached, arriving in a sequence the player climbs.
    ///
    /// **A grant is not income and it must never become income.** Every one of these pays an
    /// advance the company has to give back if it misses the term, so accepting is a bet rather
    /// than a windfall, and the sustained ones cost real money to hold: a generous free tier is
    /// revenue given away, and spare fleet headroom is capacity nobody is paying for. The body is
    /// paying the company to run itself their way, which is exactly what a grant is.
    ///
    /// **The ladder is what makes it a campaign rather than a noticeboard.** The first rung is the
    /// company's own government asking for one safe model, which is a thing a two-person lab can
    /// actually do. Finishing anything on a rung puts the company on the next body's list, and the
    /// bodies get larger as the rungs do: a ministry, then a research council, then a continental
    /// programme, then an international consortium. A player who works the ladder is being read
    /// about by progressively more serious people, and that is the story.
    /// </summary>
    public static class GrantCatalog
    {
        public const string CatalogVersion = "2026.08.30";

        /// <summary>The highest rung. Nothing above this exists to be unlocked.</summary>
        public const int TopTier = 5;

        /// <summary>How long an offer stays on the table before it lapses.</summary>
        public const int OfferOpenDays = 60;

        /// <summary>The most offers on the board at once. More than this is a to-do list.</summary>
        public const int MostOpenOffers = 3;

        /// <summary>The most awards a company can be working off at once.</summary>
        public const int MostHeldAtOnce = 2;

        /// <summary>How long before a dismissed or lapsed programme comes round again.</summary>
        public const int QuietDaysAfterDeclining = 300;

        /// <summary>Chance per day that a body with something to fund gets in touch.</summary>
        public const double ChancePerDay = 0.010;

        /// <summary>What missing the term costs on top of repaying the advance.</summary>
        public const double ReputationCostOfFailing = -0.03;

        private static readonly GrantDefinition[] Entries =
        {
            // tier                              goal                        target term   advance   completion  points
            new(GrantId.MinistrySafeStart,   1, GrantGoal.ShipProtected,     0,     300,    90_000,    400_000,   60),
            new(GrantId.MinistryFirstLine,   1, GrantGoal.ReleaseModels,     1,     240,    70_000,    300_000,   45),

            new(GrantId.StandardsStipend,    2, GrantGoal.ReleaseModels,     3,     540,   120_000,    600_000,   75),
            new(GrantId.ResearchFellowship,  2, GrantGoal.FinishResearch,    2,     540,   130_000,    650_000,   95),
            new(GrantId.OpenBenchmark,       2, GrantGoal.ReachCapability,   6,     450,   140_000,    700_000,   80),

            new(GrantId.ContinuityTwoYears,  3, GrantGoal.SustainOnSale,     1,     720,   200_000,  1_200_000,  120),
            new(GrantId.PublicAccess,        3, GrantGoal.SustainFreeTier,   0.60,  540,   180_000,    900_000,   90),
            new(GrantId.ContinuityAward,     3, GrantGoal.SustainReputation, 0.45,  480,   160_000,    800_000,   85),

            new(GrantId.RegionalEmployment,  4, GrantGoal.EmployPeople,      8,     420,   220_000,  1_000_000,   70),
            new(GrantId.ReliabilityPledge,   4, GrantGoal.SustainHeadroom,   0.75,  480,   200_000,  1_100_000,  100),

            new(GrantId.FrontierProgramme,   5, GrantGoal.ReachCapability,  15,     600,   300_000,  1_900_000,  210),
            new(GrantId.SafetyDisclosure,    5, GrantGoal.ShipProtected,     2,     540,   280_000,  1_700_000,  190)
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

        /// <summary>
        /// The highest rung the company has earned its way onto.
        ///
        /// One above the best rung it has finished something on, capped at the top. A company that
        /// has completed nothing is on rung one, which is where everybody starts and where the
        /// national programmes are.
        /// </summary>
        public static int ReachedTier(ICollection<GrantId> completed)
        {
            var best = 0;

            if (completed != null)
            {
                foreach (var id in completed)
                {
                    if (TryGet(id, out var definition) && definition.Tier > best)
                    {
                        best = definition.Tier;
                    }
                }
            }

            return Math.Clamp(best + 1, 1, TopTier);
        }

        /// <summary>
        /// Programmes the company could be offered today.
        ///
        /// **Everything at or below the rung reached**, not only the newest rung. A body two levels
        /// down still funds work, and clearing a lower rung the player skipped should stay possible
        /// rather than becoming permanently unreachable content.
        /// </summary>
        public static IEnumerable<GrantDefinition> OpenTo(ICollection<GrantId> completed)
        {
            var reached = ReachedTier(completed);

            foreach (var entry in Entries)
            {
                if (entry.Tier <= reached)
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
            GrantGoal.SustainOnSale => true,
            _ => false
        };
    }
}
