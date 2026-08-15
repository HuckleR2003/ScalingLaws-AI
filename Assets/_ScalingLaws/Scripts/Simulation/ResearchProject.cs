using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A technology tree node being researched. Calendar and compute, like everything else that
    /// competes for the cluster, and it competes with training, upgrades and family programmes.
    /// </summary>
    public sealed class ResearchProject
    {
        public ResearchProject(
            ResearchNodeId node,
            GameDate startedOn,
            int durationDays,
            double petaflopDaysRequired,
            long cashPaidUsd)
        {
            Node = node;
            StartedOn = startedOn;
            DurationDays = Math.Clamp(durationDays, 1, 1500);
            PetaflopDaysRequired = Math.Max(0.0, SimUnits.Finite(petaflopDaysRequired));
            CashPaidUsd = Math.Max(0L, cashPaidUsd);
        }

        public ResearchNodeId Node { get; }
        public GameDate StartedOn { get; }
        public int DurationDays { get; }
        public double PetaflopDaysRequired { get; }
        public long CashPaidUsd { get; }

        public int DaysCompleted { get; private set; }
        public double PetaflopDaysCompleted { get; private set; }

        public double Progress => Math.Clamp(
            Math.Min(
                DaysCompleted / (double)DurationDays,
                PetaflopDaysRequired <= 0.0 ? 1.0 : PetaflopDaysCompleted / PetaflopDaysRequired),
            0.0,
            1.0);

        public bool IsComplete =>
            DaysCompleted >= DurationDays
            && (PetaflopDaysRequired <= 0.0 || PetaflopDaysCompleted >= PetaflopDaysRequired);

        /// <summary>
        /// The calendar is done and the cluster is not.
        ///
        /// **A node needs both days and compute, and only the days pass on their own.** A company
        /// that put every accelerator on a training run, or that has no fleet at all, sits here: the
        /// day count runs past the duration, the bar stops moving, and the screen used to say
        /// "0 days left, 30% done" for the rest of the campaign. The work is not lost and nothing is
        /// broken, but a player has no way of telling that from a hang.
        ///
        /// Naming it is the whole fix. The strip can then say what is actually missing.
        /// </summary>
        public bool IsWaitingForCompute =>
            !IsComplete
            && DaysCompleted >= DurationDays
            && PetaflopDaysRequired > 0.0
            && PetaflopDaysCompleted < PetaflopDaysRequired;

        /// <summary>Compute still owed, in petaflop-days. Zero once the cluster has paid it.</summary>
        public double PetaflopDaysRemaining =>
            Math.Max(0.0, PetaflopDaysRequired - PetaflopDaysCompleted);

        public void Advance(double petaflopDays)
        {
            DaysCompleted++;
            PetaflopDaysCompleted += Math.Max(0.0, SimUnits.Finite(petaflopDays));
        }

        public void Restore(int daysCompleted, double petaflopDaysCompleted)
        {
            DaysCompleted = Math.Clamp(daysCompleted, 0, DurationDays);
            PetaflopDaysCompleted = Math.Clamp(
                SimUnits.Finite(petaflopDaysCompleted), 0.0, Math.Max(PetaflopDaysRequired, 0.0));
        }

        public override string ToString() => $"{Node}: {Progress * 100.0:0}%";
    }

    /// <summary>A node as the research screen needs to draw it.</summary>
    public readonly struct ResearchStanding
    {
        public ResearchStanding(
            ResearchNode node,
            bool isUnlocked,
            bool isInProgress,
            bool canStart,
            string blockedReason,
            int durationDays)
        {
            Node = node;
            IsUnlocked = isUnlocked;
            IsInProgress = isInProgress;
            CanStart = canStart;
            BlockedReason = canStart ? string.Empty : blockedReason ?? string.Empty;
            DurationDays = Math.Max(1, durationDays);
        }

        public ResearchNode Node { get; }
        public bool IsUnlocked { get; }
        public bool IsInProgress { get; }
        public bool CanStart { get; }
        public string BlockedReason { get; }

        /// <summary>Calendar after the founder's research speed is applied.</summary>
        public int DurationDays { get; }

        public override string ToString() => IsUnlocked
            ? $"{Node.DisplayName}: done"
            : CanStart ? $"{Node.DisplayName}: available" : $"{Node.DisplayName}: {BlockedReason}";
    }
}
