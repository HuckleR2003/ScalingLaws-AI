using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// An offer made to another lab and not yet answered.
    ///
    /// **The answer is rolled when the days run out, never when the offer is made.** That makes it
    /// causal state, which has to be saved: deciding it on the click would let a player reload their
    /// way to a yes, and it would delete the wait, which is the only thing an offer is from the
    /// player's chair. Tenth time in this project that something which looked derived was not.
    /// </summary>
    public readonly struct PendingOffer
    {
        public PendingOffer(CompetitorId lab, RelationOffer offer, GameDate sent)
        {
            Lab = lab;
            Offer = offer;
            Sent = sent;
        }

        public CompetitorId Lab { get; }
        public RelationOffer Offer { get; }
        public GameDate Sent { get; }

        public int DaysWaiting(GameDate today) => Math.Max(0, today.DayIndex - Sent.DayIndex);
    }


    /// <summary>
    /// A joint research programme the company is running with one or two allied labs.
    ///
    /// **Causal, and saved.** It pays points every day it runs, so a campaign dropped on load is a
    /// year of a laboratory that never happened. Twelfth time in this project.
    /// </summary>
    public sealed class ResearchCampaign
    {
        public ResearchCampaign(CampaignTerm term, GameDate started, GameDate ends,
            IReadOnlyList<CompetitorId> members)
        {
            Term = term;
            Started = started;
            Ends = ends;
            Members = members ?? new List<CompetitorId>();
        }

        public CampaignTerm Term { get; }
        public GameDate Started { get; }
        public GameDate Ends { get; }

        /// <summary>The other labs in the room. The player is not in this list.</summary>
        public IReadOnlyList<CompetitorId> Members { get; }

        public bool IsLiveOn(GameDate date) => date.DayIndex < Ends.DayIndex;

        /// <summary>Days still to run, for the screen.</summary>
        public int DaysLeft(GameDate date) => Math.Max(0, Ends.DayIndex - date.DayIndex);
    }


    /// <summary>How a piece of work between two companies ended.</summary>
    public enum DealOutcome
    {
        /// <summary>It ran its whole term.</summary>
        Finished = 0,

        /// <summary>It ended early, whoever ended it.</summary>
        Ended = 1,

        /// <summary>They said no, so it never started.</summary>
        Refused = 2
    }

    /// <summary>
    /// Something that happened between the two companies, kept after it stopped mattering.
    ///
    /// **A record, not a derivation.** Nothing else in the game remembers that a licence ran for
    /// nine months in 2024 and ended well, and a relationship with no history behind it is a number
    /// on a bar. The author asked for the list of current and older work together by name, and this
    /// is what that list reads.
    ///
    /// Capped, because a fourteen-year campaign making one offer a month is a hundred and sixty
    /// rows, which is an archive rather than a memory. Same reasoning and the same shape as
    /// `RivalRelations.HistoryKept`.
    /// </summary>
    public readonly struct DealRecord
    {
        public DealRecord(CompetitorId lab, RelationOffer offer, GameDate started, GameDate ended,
            DealOutcome outcome)
        {
            Lab = lab;
            Offer = offer;
            Started = started;
            Ended = ended;
            Outcome = outcome;
        }

        public CompetitorId Lab { get; }
        public RelationOffer Offer { get; }
        public GameDate Started { get; }
        public GameDate Ended { get; }
        public DealOutcome Outcome { get; }

        /// <summary>How long it ran, in days. Zero for something that was never agreed.</summary>
        public int Days => Math.Max(0, Ended.DayIndex - Started.DayIndex);
    }

    /// <summary>
    /// A renewal the other side has put on the table, waiting for an answer.
    ///
    /// **Causal, and saved.** The roll for whether they rang has already happened, so dropping
    /// this on load would let a reload buy a second go at it, and would also lose an offer the
    /// player might have been sleeping on. Thirteenth time in this project.
    /// </summary>
    public readonly struct PendingRenewal
    {
        public PendingRenewal(CompetitorId lab, RelationOffer offer, GameDate openedOn)
        {
            Lab = lab;
            Offer = offer;
            OpenedOn = openedOn;
        }

        public CompetitorId Lab { get; }
        public RelationOffer Offer { get; }
        public GameDate OpenedOn { get; }
    }

    /// <summary>Something signed and running: a licence, an evaluation, a capacity term.</summary>
    public readonly struct StandingDeal
    {
        public StandingDeal(CompetitorId lab, RelationOffer offer, GameDate started, GameDate ends)
        {
            Lab = lab;
            Offer = offer;
            Started = started;
            Ends = ends;
        }

        public CompetitorId Lab { get; }
        public RelationOffer Offer { get; }
        public GameDate Started { get; }
        public GameDate Ends { get; }

        public bool IsLiveOn(GameDate date) => date.DayIndex < Ends.DayIndex;
    }

    /// <summary>
    /// What has been signed with each lab, and how long it has held.
    ///
    /// **A level is earned in days, not bought.** The fee is the small part; the gate is time spent
    /// at the level below it without a hostile act in between. That is the spine of this game
    /// applied to a relationship: a deep alliance is a decision made in year two that pays in year
    /// four, and no amount of money brings it forward.
    ///
    /// **And it falls faster than it climbs.** One hostile act drops a level immediately, which is
    /// the same asymmetry `Standing` uses for fans: they arrive at 0.004 a day and leave at 0.0012.
    /// Two years to build, one afternoon to lose.
    /// </summary>
    public sealed class LabAlliances
    {
        /// <summary>The highest level there is.</summary>
        public const int TopLevel = 3;

        /// <summary>Days at the level below before the next one can be signed.</summary>
        public static int DaysNeededFor(int level) => level switch
        {
            1 => 90,
            2 => 180,
            _ => 365
        };

        /// <summary>What signing costs. Small against the calendar it cannot buy.</summary>
        public static long FeeFor(int level) => level switch
        {
            1 => 250_000,
            2 => 1_500_000,
            _ => 6_000_000
        };

        /// <summary>The band that has to hold for the clock to run at all.</summary>
        public const RelationBand HoldsAt = RelationBand.Friendly;

        private readonly Dictionary<CompetitorId, int> levels = new();
        private readonly Dictionary<CompetitorId, int> heldSince = new();

        /// <summary>
        /// Labs the company has ever signed anything with, at any level, whatever happened after.
        ///
        /// **Kept separately and never cleared.** The author's own rule for the telephone: a number
        /// you keep after a friendship cools is how that works in life, so a lab that reached level
        /// one stays callable forever even if the alliance is long gone.
        /// </summary>
        private readonly HashSet<CompetitorId> everSigned = new();

        /// <summary>
        /// The day each lab was last telephoned, as a day index.
        ///
        /// **Separate from the levels, because calling somebody is not signing anything with
        /// them.** A lab whose alliance has been broken back to nothing still has a last-call day,
        /// and it still matters: the cooldown is about the call, not about the alliance.
        /// </summary>
        private readonly Dictionary<CompetitorId, int> lastCalled = new();

        /// <summary>Where a lab stands. Zero for anybody nothing has been signed with.</summary>
        public int LevelWith(CompetitorId lab) => levels.TryGetValue(lab, out var level) ? level : 0;

        /// <summary>The day the current level was reached, as a day index.</summary>
        public int HeldSince(CompetitorId lab) => heldSince.TryGetValue(lab, out var day) ? day : 0;

        /// <summary>How long the current level has held, in days.</summary>
        public int DaysAtLevel(CompetitorId lab, GameDate today) =>
            LevelWith(lab) == 0 ? 0 : Math.Max(0, today.DayIndex - HeldSince(lab));

        /// <summary>Every lab anything has ever been signed with, whatever happened afterwards.</summary>
        public IEnumerable<CompetitorId> EverSigned => everSigned;

        /// <summary>Whether this lab can be telephoned. Once true, true forever.</summary>
        public bool CanCall(CompetitorId lab) => everSigned.Contains(lab);

        /// <summary>The day this lab was last rung, or -1 for one that never has been.</summary>
        public int LastCalled(CompetitorId lab) =>
            lastCalled.TryGetValue(lab, out var day) ? day : -1;

        /// <summary>Writes down that they were rung today. The caller owns the cooldown rule.</summary>
        public void RecordCall(CompetitorId lab, GameDate today) =>
            lastCalled[lab] = today.DayIndex;

        /// <summary>Everybody currently at a level, for the board and the wire.</summary>
        public IEnumerable<KeyValuePair<CompetitorId, int>> Signed => levels;

        /// <summary>
        /// Whether the next level could be signed today, and why not when it could not.
        ///
        /// The band is read by the caller and handed in, because this class holds no opinion about
        /// how anybody feels: it holds what was signed and when.
        /// </summary>
        public bool CanSignNext(CompetitorId lab, GameDate today, RelationBand band, out int next)
        {
            next = LevelWith(lab) + 1;

            if (next > TopLevel || band < HoldsAt)
            {
                return false;
            }

            // The first level has no level below it to have held, so the clock it waits on is the
            // relation's, which the caller has already checked by passing the band.
            return next == 1 || DaysAtLevel(lab, today) >= DaysNeededFor(next);
        }

        /// <summary>Signs the next level. The caller charges the fee; this records what was signed.</summary>
        public void Sign(CompetitorId lab, GameDate today)
        {
            var next = Math.Min(TopLevel, LevelWith(lab) + 1);

            levels[lab] = next;
            heldSince[lab] = today.DayIndex;
            everSigned.Add(lab);
        }

        /// <summary>
        /// Drops a level after something hostile, and restarts the clock on what is left.
        ///
        /// Restarting matters: without it a player could attack a lab at level three, fall to two,
        /// and sign three again the same afternoon on a clock that ran while they were allies.
        /// </summary>
        public void Break(CompetitorId lab, GameDate today)
        {
            var level = LevelWith(lab);

            if (level <= 0)
            {
                return;
            }

            levels[lab] = level - 1;
            heldSince[lab] = today.DayIndex;

            if (levels[lab] == 0)
            {
                levels.Remove(lab);
                heldSince.Remove(lab);
            }
        }

        /// <summary>Restores a saved campaign. Unknown labs and impossible levels are dropped.</summary>
        public void Restore(IReadOnlyList<int> labs, IReadOnlyList<int> levelValues,
            IReadOnlyList<int> sinceDays, IReadOnlyList<int> ever,
            IReadOnlyList<int> calledLabs = null, IReadOnlyList<int> calledDays = null)
        {
            levels.Clear();
            heldSince.Clear();
            everSigned.Clear();
            lastCalled.Clear();

            if (labs != null && levelValues != null)
            {
                for (var index = 0; index < labs.Count && index < levelValues.Count; index++)
                {
                    if (!Enum.IsDefined(typeof(CompetitorId), labs[index]))
                    {
                        continue;
                    }

                    var level = Math.Clamp(levelValues[index], 0, TopLevel);

                    if (level == 0)
                    {
                        continue;
                    }

                    var lab = (CompetitorId)labs[index];
                    levels[lab] = level;
                    heldSince[lab] = sinceDays != null && index < sinceDays.Count
                        ? Math.Max(0, sinceDays[index])
                        : 0;
                }
            }

            if (ever != null)
            {
                foreach (var lab in ever)
                {
                    if (Enum.IsDefined(typeof(CompetitorId), lab))
                    {
                        everSigned.Add((CompetitorId)lab);
                    }
                }
            }

            if (calledLabs == null || calledDays == null)
            {
                return;
            }

            for (var index = 0; index < calledLabs.Count && index < calledDays.Count; index++)
            {
                if (Enum.IsDefined(typeof(CompetitorId), calledLabs[index]))
                {
                    lastCalled[(CompetitorId)calledLabs[index]] = Math.Max(0, calledDays[index]);
                }
            }
        }

        /// <summary>Writes the current state out, for the save.</summary>
        public void Capture(List<int> labs, List<int> levelValues, List<int> sinceDays,
            List<int> ever, List<int> calledLabs = null, List<int> calledDays = null)
        {
            labs?.Clear();
            levelValues?.Clear();
            sinceDays?.Clear();
            ever?.Clear();
            calledLabs?.Clear();
            calledDays?.Clear();

            foreach (var pair in levels)
            {
                labs?.Add((int)pair.Key);
                levelValues?.Add(pair.Value);
                sinceDays?.Add(HeldSince(pair.Key));
            }

            foreach (var lab in everSigned)
            {
                ever?.Add((int)lab);
            }

            foreach (var pair in lastCalled)
            {
                calledLabs?.Add((int)pair.Key);
                calledDays?.Add(pair.Value);
            }
        }
    }
}
