using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A run in flight. It tracks compute delivered rather than days elapsed, so adding accelerators
    /// halfway through genuinely finishes it sooner and handing rented capacity back genuinely
    /// stretches it out.
    ///
    /// <see cref="ProjectedCapability"/> is the estimate recorded on the day the run started. It is
    /// kept only so the finished model can be compared against what was promised. It is never the
    /// model's capability.
    /// </summary>
    public sealed class TrainingRun
    {
        public TrainingRun(
            ModelBlueprint blueprint,
            GameDate startDate,
            double petaflopDaysRequired,
            double projectedCapability,
            double actualTokensBillions,
            long dataCostPaidUsd)
        {
            Blueprint = blueprint;
            StartDate = startDate;
            PetaflopDaysRequired = Math.Max(1.0, SimUnits.Finite(petaflopDaysRequired, 1.0));
            ProjectedCapability = Math.Clamp(SimUnits.Finite(projectedCapability), 0.0, 100.0);
            ActualTokensBillions = Math.Max(0.0, SimUnits.Finite(actualTokensBillions));
            DataCostPaidUsd = Math.Max(0L, dataCostPaidUsd);
        }

        public ModelBlueprint Blueprint { get; }
        public GameDate StartDate { get; }
        public double PetaflopDaysRequired { get; }
        public double PetaflopDaysCompleted { get; private set; }

        /// <summary>The estimate made on day one. For comparison only, never a result.</summary>
        public double ProjectedCapability { get; }

        /// <summary>Tokens the data mix could actually supply, which may be under what was requested.</summary>
        public double ActualTokensBillions { get; }

        public long DataCostPaidUsd { get; }

        /// <summary>Compute cash burned by this run so far, charged day by day.</summary>
        public long ComputeCashSpentUsd { get; private set; }

        public double Progress => Math.Clamp(PetaflopDaysCompleted / PetaflopDaysRequired, 0.0, 1.0);

        public bool IsComplete => PetaflopDaysCompleted >= PetaflopDaysRequired;

        public int DaysElapsed(GameDate date) => Math.Max(0, date.DayIndex - StartDate.DayIndex);

        /// <summary>Feeds one day of delivered compute into the run.</summary>
        public void Contribute(double petaflopDays, long cashUsd)
        {
            PetaflopDaysCompleted += Math.Max(0.0, SimUnits.Finite(petaflopDays));
            ComputeCashSpentUsd += Math.Max(0L, cashUsd);
        }

        /// <summary>Days left at the current delivery rate. Infinite when nothing is being delivered.</summary>
        public double EstimatedDaysRemaining(double petaflopDaysPerDay)
        {
            var rate = SimUnits.Finite(petaflopDaysPerDay);
            if (rate <= 0.0)
            {
                return double.PositiveInfinity;
            }

            return Math.Max(0.0, (PetaflopDaysRequired - PetaflopDaysCompleted) / rate);
        }

        public override string ToString() =>
            $"{Blueprint.Name}: {Progress * 100.0:0.0}% of {PetaflopDaysRequired:N0} PF-days";
    }
}
