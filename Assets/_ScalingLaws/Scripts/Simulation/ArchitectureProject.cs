using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A family research programme in flight.
    ///
    /// Like an upgrade it needs both clocks: calendar days and research compute. Unlike an upgrade
    /// it produces something whose value is not known until it lands, and it competes with training
    /// and upgrades for the same cluster. A company running a big model, three upgrades and a new
    /// family at once is doing all four badly.
    /// </summary>
    public sealed class ArchitectureProject
    {
        public ArchitectureProject(
            ArchitectureBlueprint blueprint,
            GameDate startedOn,
            int durationDays,
            double petaflopDaysRequired,
            long cashPaidUsd,
            double researchPower,
            double variance,
            ArchitectureDefinition baseline,
            int generation)
        {
            Blueprint = blueprint;
            StartedOn = startedOn;
            DurationDays = Math.Clamp(durationDays, 1, ArchitectureBlueprint.MaximumDurationDays);
            PetaflopDaysRequired = Math.Max(0.0, SimUnits.Finite(petaflopDaysRequired));
            CashPaidUsd = Math.Max(0L, cashPaidUsd);
            ResearchPower = Math.Clamp(SimUnits.Finite(researchPower), 0.0, 1.5);
            Variance = Math.Clamp(SimUnits.Finite(variance), 0.0, 1.0);
            Baseline = baseline;
            Generation = Math.Clamp(generation, 0, ArchitectureDesigner.MaximumGenerations);
        }

        public ArchitectureBlueprint Blueprint { get; }
        public GameDate StartedOn { get; }
        public int DurationDays { get; }
        public double PetaflopDaysRequired { get; }
        public long CashPaidUsd { get; }

        /// <summary>Frozen on commit so a later budget change cannot retroactively improve the roll.</summary>
        public double ResearchPower { get; }

        public double Variance { get; }
        public ArchitectureDefinition Baseline { get; }
        public int Generation { get; }

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

        /// <summary>Rebuilds the projection this programme was committed against.</summary>
        public ArchitectureProjection Projection() => new(
            Blueprint,
            true,
            string.Empty,
            default,
            default,
            default,
            ResearchPower,
            Variance,
            PetaflopDaysRequired,
            Baseline);

        public override string ToString() =>
            $"{Blueprint.Name} into {Blueprint.Slot}: {Progress * 100.0:0}%";
    }
}
