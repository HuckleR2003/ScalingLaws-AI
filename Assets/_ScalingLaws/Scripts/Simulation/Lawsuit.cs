using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    public enum LawsuitVerdict
    {
        /// <summary>Still in front of a court.</summary>
        Pending = 0,

        /// <summary>The demand was met, in full or near enough.</summary>
        Won = 1,

        /// <summary>Thrown out, and the company pays both sides.</summary>
        Lost = 2,

        /// <summary>They paid a fraction to make it go away and admitted nothing.</summary>
        Settled = 3
    }

    /// <summary>
    /// One action against one lab, from filing to judgment.
    ///
    /// **The verdict is rolled when the case closes, not when it is filed.** That makes an open case
    /// causal state that has to survive a save, the same reasoning `RegulatoryAction` is built on:
    /// deciding it at filing time and holding the answer would let a player reload their way to a
    /// better outcome, and deciding it at filing time and *showing* it would delete the wait, which
    /// is the only thing a lawsuit actually is from the player's seat.
    /// </summary>
    public sealed class Lawsuit
    {
        /// <summary>
        /// How long a case runs.
        ///
        /// Long enough that filing one is not a way to raise money this quarter, which is exactly
        /// what it would become at thirty days.
        /// </summary>
        public const int DaysInCourt = 270;

        public Lawsuit(CompetitorId target, GameDate filedOn, long damagesDemandedUsd,
            long costsUsd, string groundsKey, bool againstUs = false)
        {
            Target = target;
            FiledOn = filedOn;
            DamagesDemandedUsd = Math.Max(0L, damagesDemandedUsd);
            CostsUsd = Math.Max(0L, costsUsd);
            GroundsKey = string.IsNullOrEmpty(groundsKey) ? "suit.grounds.training" : groundsKey;
            AgainstUs = againstUs;
        }

        /// <summary>
        /// The other lab, whichever side of the room it is sitting on.
        ///
        /// It is the defendant on a case the company filed and the plaintiff on one filed against
        /// it, which is why the field is not called `Defendant`.
        /// </summary>
        public CompetitorId Target { get; }

        /// <summary>
        /// True when the lab is suing the company rather than the other way round.
        ///
        /// **A direction on the existing case rather than a second court.** Everything about a
        /// hearing is the same from either chair: the same calendar, the same odds curve, the same
        /// roll held back to the day it closes. Two types would be two places to change
        /// `DaysInCourt` and one place to forget.
        /// </summary>
        public bool AgainstUs { get; }
        public GameDate FiledOn { get; }
        public long DamagesDemandedUsd { get; }

        /// <summary>What the company has already spent getting to court. Never refunded.</summary>
        public long CostsUsd { get; }

        public string GroundsKey { get; }
        public string Grounds => Loc.T(GroundsKey);

        public int DaysElapsed { get; private set; }
        public LawsuitVerdict Verdict { get; private set; } = LawsuitVerdict.Pending;

        /// <summary>What actually changed hands. Zero until the case closes.</summary>
        public long AwardedUsd { get; private set; }

        public bool IsClosed => Verdict != LawsuitVerdict.Pending;
        public int DaysLeft => Math.Max(0, DaysInCourt - DaysElapsed);

        public double Progress => Math.Clamp(DaysElapsed / (double)DaysInCourt, 0.0, 1.0);

        public void Advance()
        {
            if (!IsClosed)
            {
                DaysElapsed = Math.Min(DaysInCourt, DaysElapsed + 1);
            }
        }

        public bool ReadyForJudgment => !IsClosed && DaysElapsed >= DaysInCourt;

        public void Decide(LawsuitVerdict verdict, long awardedUsd)
        {
            Verdict = verdict;
            AwardedUsd = Math.Max(0L, awardedUsd);
        }

        /// <summary>Rebuilds an open case from a save. Never rolls anything.</summary>
        public void Restore(int daysElapsed, LawsuitVerdict verdict, long awardedUsd)
        {
            DaysElapsed = Math.Clamp(daysElapsed, 0, DaysInCourt);
            Verdict = Enum.IsDefined(typeof(LawsuitVerdict), verdict)
                ? verdict
                : LawsuitVerdict.Pending;

            AwardedUsd = Math.Max(0L, awardedUsd);
        }
    }

    /// <summary>
    /// When a company has a case, what it is worth asking for, and what asking for it costs.
    ///
    /// **The whole mechanic is that the demand and the odds move against each other.** Asking for a
    /// token sum is nearly certain and buys nothing worth the wait; asking for everything is a
    /// lottery ticket with a nine-figure legal bill attached. Neither end is correct, which is the
    /// same rule every other control in this game is held to.
    /// </summary>
    public static class LawsuitBook
    {
        /// <summary>The best odds available, when almost nothing is being demanded.</summary>
        public const double BestOdds = 0.62;

        /// <summary>The odds at the ceiling of what can be demanded.</summary>
        public const double WorstOdds = 0.11;

        /// <summary>
        /// Below this share of the demand, a loss is downgraded to a settlement.
        ///
        /// A case that was never frivolous rarely ends in nothing. This is what stops the mechanic
        /// from being a coin flip on a very large number.
        /// </summary>
        public const double SettlementShare = 0.18;

        /// <summary>Chance a losing case still ends in a settlement rather than nothing.</summary>
        public const double SettlementChance = 0.45;

        /// <summary>What filing costs, as a share of the sum demanded.</summary>
        public const double CostShare = 0.035;

        public const long MinimumCostUsd = 250_000;

        /// <summary>Relations with the lab being sued, whatever the court decides.</summary>
        public const double RelationCostOfFiling = -22.0;

        /// <summary>How far behind the company a rival's model may sit and still be arguable.</summary>
        public const double GroundsCapabilityWindow = 8.0;

        /// <summary>Nothing can be demanded past this, however rich anybody is.</summary>
        public const long CeilingUsd = 9_000_000_000;

        /// <summary>
        /// The largest sum this company could credibly ask this lab for.
        ///
        /// Tied to the company's own scale rather than to a flat number, so a two-person lab cannot
        /// open by suing the market leader for four billion dollars and a mature company is not
        /// capped at a sum it would not bother filing for.
        /// </summary>
        public static long CeilingFor(long annualRevenueUsd, long cashUsd)
        {
            var basis = Math.Max(annualRevenueUsd * 3L, cashUsd * 2L);
            var floor = 25_000_000L;

            return Math.Clamp(Math.Max(basis, floor), floor, CeilingUsd);
        }

        public static long CostOf(long damagesDemandedUsd) =>
            Math.Max(MinimumCostUsd, (long)(damagesDemandedUsd * CostShare));

        /// <summary>
        /// The chance of winning what was asked for.
        ///
        /// Falls with the share of the ceiling being demanded, and the curve is deliberately not a
        /// straight line: the first third of the range costs very little confidence and the last
        /// third costs most of it, which is what makes a greedy demand feel like a gamble rather
        /// than like a slightly worse choice.
        /// </summary>
        public static double OddsFor(long damagesDemandedUsd, long ceilingUsd)
        {
            if (ceilingUsd <= 0)
            {
                return WorstOdds;
            }

            var share = Math.Clamp(damagesDemandedUsd / (double)ceilingUsd, 0.0, 1.0);
            var curve = share * share;

            return SimUnits.Finite(BestOdds - (BestOdds - WorstOdds) * curve, WorstOdds);
        }

        /// <summary>
        /// Whether there is anything to sue over, and what it would be called.
        ///
        /// **Grounds are read from what has actually happened, never invented.** A lab is arguable
        /// when it is selling something close to what this company sells, shipped after them, and
        /// the relationship is already on the record as bad. All three come from state the game
        /// already keeps, so a case can never be filed over a thing the player cannot point at.
        /// </summary>
        public static bool GroundsAgainst(CompetitorId lab, double playerCapability,
            GameDate playerReleasedOn, IReadOnlyList<RivalModel> theirModels,
            double relation, out string groundsKey)
        {
            groundsKey = string.Empty;

            if (playerCapability <= 0.0 || relation > RivalRelations.TenseAbove)
            {
                return false;
            }

            foreach (var model in theirModels)
            {
                if (model.Competitor != lab)
                {
                    continue;
                }

                if (model.ReleaseDate.DayIndex <= playerReleasedOn.DayIndex)
                {
                    continue;
                }

                if (Math.Abs(model.Capability - playerCapability) > GroundsCapabilityWindow)
                {
                    continue;
                }

                groundsKey = "suit.grounds.training";
                return true;
            }

            return false;
        }
    }
}
