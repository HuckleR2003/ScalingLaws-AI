using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A lab has traced a smear back to this company and is deciding what to do about it.
    ///
    /// **A smear used to cost a number and nothing else.** It moved the target's brand down, moved
    /// the relationship down, and that was the whole of it: the lab being lied about never wrote,
    /// never rang, and never went near a court. So the loudest thing a player can do to a rival was
    /// also the one with nobody on the other end of it.
    ///
    /// This is the other end of it, and it exists **only when the campaign was traced back**. A smear
    /// that lands still costs the relationship, because they know, but knowing is not proving and
    /// nobody files on a suspicion. That distinction is what keeps the backfire roll meaningful
    /// instead of being a tax on the same decision twice.
    ///
    /// **The suit is rolled when the letter runs out, not when it arrives.** Same reasoning as
    /// <see cref="RegulatoryAction"/> and <see cref="Lawsuit"/>: an open threat is causal state, so
    /// it has to survive a save, or a player would reload their way past every consequence in the
    /// game. That is the seventh time in this project something that looked derived turned out to be
    /// causal, and the save test is what says so.
    /// </summary>
    public sealed class SmearThreat
    {
        /// <summary>How long their lawyers wait for an answer before they decide.</summary>
        public const int AnswerDays = 30;

        public SmearThreat(CompetitorId lab, GameDate openedOn, long settlementUsd, int mailId)
        {
            Lab = lab;
            OpenedOn = openedOn;
            SettlementUsd = Math.Max(0L, settlementUsd);
            MailId = mailId;
        }

        /// <summary>Who is threatening. The one they will file as, if it comes to that.</summary>
        public CompetitorId Lab { get; }

        public GameDate OpenedOn { get; }

        /// <summary>
        /// What they will take to drop it, paid now.
        ///
        /// Deliberately a fraction of what they would ask a court for. Settling has to be the cheap
        /// answer or nobody would ever take it, and it has to cost something real or refusing would
        /// not be a decision.
        /// </summary>
        public long SettlementUsd { get; }

        /// <summary>The letter in the inbox this belongs to, so an answer can find it.</summary>
        public int MailId { get; }

        public int DaysElapsed { get; private set; }

        /// <summary>True once the player has settled or refused. The day loop stops looking.</summary>
        public bool IsAnswered { get; private set; }

        public bool IsExpired => DaysElapsed >= AnswerDays;

        public int DaysLeft => Math.Max(0, AnswerDays - DaysElapsed);

        public void Advance() => DaysElapsed = Math.Min(AnswerDays, DaysElapsed + 1);

        public void Answer() => IsAnswered = true;

        /// <summary>Only the save uses these.</summary>
        public void Restore(int daysElapsed, bool answered)
        {
            DaysElapsed = Math.Clamp(daysElapsed, 0, AnswerDays);
            IsAnswered = answered;
        }

        public override string ToString() =>
            $"{Lab}: threat day {DaysElapsed} of {AnswerDays}, {SettlementUsd} to settle";
    }
}
