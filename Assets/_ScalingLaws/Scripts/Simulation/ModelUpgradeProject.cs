using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One trait moving up one level, inside a programme.</summary>
    public readonly struct UpgradeStep
    {
        public UpgradeStep(ModelTrait trait, int targetLevel)
        {
            Trait = trait;
            TargetLevel = Math.Clamp(targetLevel, 1, ModelTraitSetLimits.MaximumLevel);
        }

        public ModelTrait Trait { get; }
        public int TargetLevel { get; }

        public override string ToString() => $"{Trait}→L{TargetLevel}";
    }

    /// <summary>
    /// One block of post-training work on one model, covering everything the player picked.
    ///
    /// **A programme is a batch, not a trait.** Picking four cards used to commission four
    /// programmes, and because each one advanced its own calendar by a day every day, all four ran
    /// the same days at once: a playtest saw four "UPGRADE IN PROGRESS" rows counting down in step,
    /// four completion messages, and work finishing during work it was supposed to follow. The team
    /// does one job at a time, so the days add up and the cluster time adds up, and the whole basket
    /// lands together.
    ///
    /// Upgrades run on the same compute pool as training, so a programme and a new run are in direct
    /// competition for the cluster. That is the tension the whole upgrade grid exists to create:
    /// another point of Reasoning on last year's model, or a new model entirely.
    ///
    /// Unlike a training run, a programme has a fixed day count as well as a compute bill. Throwing
    /// the whole cluster at it does not make people integrate faster.
    /// </summary>
    public sealed class ModelUpgradeProject
    {
        private readonly List<UpgradeStep> steps = new();

        public ModelUpgradeProject(
            int modelIndex,
            IEnumerable<UpgradeStep> work,
            GameDate startedOn,
            int durationDays,
            double petaflopDaysRequired,
            long cashPaidUsd)
        {
            ModelIndex = Math.Max(0, modelIndex);

            if (work != null)
            {
                foreach (var step in work)
                {
                    steps.Add(step);
                }
            }

            if (steps.Count == 0)
            {
                throw new ArgumentException("A programme with no work in it cannot be commissioned.",
                    nameof(work));
            }

            StartedOn = startedOn;
            DurationDays = Math.Clamp(durationDays, 1, 400);
            PetaflopDaysRequired = Math.Max(0.0, SimUnits.Finite(petaflopDaysRequired));
            CashPaidUsd = Math.Max(0L, cashPaidUsd);
        }

        /// <summary>One trait on its own, which is still the common case.</summary>
        public ModelUpgradeProject(
            int modelIndex,
            ModelTrait trait,
            int targetLevel,
            GameDate startedOn,
            int durationDays,
            double petaflopDaysRequired,
            long cashPaidUsd)
            : this(modelIndex, new[] { new UpgradeStep(trait, targetLevel) }, startedOn,
                durationDays, petaflopDaysRequired, cashPaidUsd)
        {
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

        /// <summary>Everything this programme will apply, in the order the player picked it.</summary>
        public IReadOnlyList<UpgradeStep> Steps => steps;

        /// <summary>
        /// The headline trait, which is the first one picked.
        ///
        /// Kept because the strip needs something to name and the in-flight check needs something to
        /// compare, and both were written when a programme was a single trait.
        /// </summary>
        public ModelTrait Trait => steps[0].Trait;

        public int TargetLevel => steps[0].TargetLevel;

        /// <summary>True when this programme is doing more than one thing.</summary>
        public bool IsBatch => steps.Count > 1;

        /// <summary>Whether this programme includes the given trait.</summary>
        public bool Covers(ModelTrait trait)
        {
            foreach (var step in steps)
            {
                if (step.Trait == trait)
                {
                    return true;
                }
            }

            return false;
        }

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
            $"{steps.Count} step(s) on model {ModelIndex}: {Progress * 100.0:0}%";
    }
}
