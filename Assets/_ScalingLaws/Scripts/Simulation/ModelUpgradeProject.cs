using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// One trait being pushed up one level on one live model.
    ///
    /// Upgrades run on the same compute pool as training, so an upgrade programme and a new run are
    /// in direct competition for the cluster. That is the tension the whole upgrade grid exists to
    /// create: another point of Reasoning on last year's model, or a new model entirely.
    ///
    /// Unlike a training run, an upgrade has a fixed day count as well as a compute bill. Throwing
    /// the whole cluster at it does not make people integrate faster.
    /// </summary>
    public sealed class ModelUpgradeProject
    {
        public ModelUpgradeProject(
            int modelIndex,
            ModelTrait trait,
            int targetLevel,
            GameDate startedOn,
            int durationDays,
            double petaflopDaysRequired,
            long cashPaidUsd)
        {
            ModelIndex = Math.Max(0, modelIndex);
            Trait = trait;
            TargetLevel = Math.Clamp(targetLevel, 1, ModelTraitSetLimits.MaximumLevel);
            StartedOn = startedOn;
            DurationDays = Math.Clamp(durationDays, 1, 400);
            PetaflopDaysRequired = Math.Max(0.0, SimUnits.Finite(petaflopDaysRequired));
            CashPaidUsd = Math.Max(0L, cashPaidUsd);
        }

        /// <summary>
        /// Index into the deployed list, or into the shelf when <see cref="OnShelf"/> is set.
        ///
        /// One field for two lists rather than two fields, because a project belongs to exactly one
        /// model and carrying an index for the list it is not in invites the two to disagree.
        /// </summary>
        public int ModelIndex { get; }

        /// <summary>True when the model is still on the shelf rather than on sale.</summary>
        public bool OnShelf { get; set; }
        public ModelTrait Trait { get; }
        public int TargetLevel { get; }
        public GameDate StartedOn { get; }
        public int DurationDays { get; }
        public double PetaflopDaysRequired { get; }

        /// <summary>Paid up front, on the day the work was commissioned.</summary>
        public long CashPaidUsd { get; }

        public int DaysCompleted { get; private set; }
        public double PetaflopDaysCompleted { get; private set; }

        /// <summary>Both clocks have to finish. Calendar progress and compute progress.</summary>
        public double Progress => Math.Clamp(
            Math.Min(
                DaysCompleted / (double)DurationDays,
                PetaflopDaysRequired <= 0.0 ? 1.0 : PetaflopDaysCompleted / PetaflopDaysRequired),
            0.0,
            1.0);

        /// <summary>
        /// Calendar days still to run.
        ///
        /// The calendar only. Compute can be the binding clock — the banner shows this because it
        /// is the number a player can act on: they can add compute, they cannot add days.
        /// </summary>
        public int DaysRemaining => Math.Max(0, DurationDays - DaysCompleted);

        public bool IsComplete =>
            DaysCompleted >= DurationDays
            && (PetaflopDaysRequired <= 0.0 || PetaflopDaysCompleted >= PetaflopDaysRequired);

        /// <summary>Advances one day with whatever compute was allocated to it.</summary>
        public void Advance(double petaflopDays)
        {
            DaysCompleted++;
            PetaflopDaysCompleted += Math.Max(0.0, SimUnits.Finite(petaflopDays));
        }

        /// <summary>Restores a loaded project without replaying its days.</summary>
        public void Restore(int daysCompleted, double petaflopDaysCompleted)
        {
            DaysCompleted = Math.Clamp(daysCompleted, 0, DurationDays);
            PetaflopDaysCompleted = Math.Clamp(
                SimUnits.Finite(petaflopDaysCompleted),
                0.0,
                Math.Max(PetaflopDaysRequired, 0.0));
        }

        public override string ToString() =>
            $"{Trait} to L{TargetLevel} on model {ModelIndex}: {Progress * 100.0:0}%";
    }
}
