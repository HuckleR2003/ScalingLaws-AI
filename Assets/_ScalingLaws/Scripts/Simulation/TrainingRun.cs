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

        /// <summary>
        /// Calendar days the run was promised to take, or zero for a run started before this
        /// existed. See <see cref="IsComplete"/> for why it is here.
        /// </summary>
        public int CalendarDaysRequired { get; private set; }

        public int DaysCompleted { get; private set; }

        /// <summary>
        /// Both clocks, and the slower of the two is the one the player watches.
        ///
        /// **The compute clock alone was letting the calendar evaporate.** The creator adds the
        /// safety stage to the projected duration — that is deliberate, the stage is work and work
        /// takes weeks — but the run only ever counted petaflop-days, so a model priced at eleven
        /// days on the design screen finished in one. Everything the SAFETY stage promised in time
        /// was quietly refunded, and the number the player planned around was fiction.
        ///
        /// The same two-clock rule the upgrade programmes have always used, for the same reason.
        /// </summary>
        public double Progress => Math.Clamp(
            Math.Min(
                PetaflopDaysRequired <= 0.0 ? 1.0 : PetaflopDaysCompleted / PetaflopDaysRequired,
                CalendarDaysRequired <= 0 ? 1.0 : DaysCompleted / (double)CalendarDaysRequired),
            0.0,
            1.0);

        public bool IsComplete =>
            PetaflopDaysCompleted >= PetaflopDaysRequired
            && (CalendarDaysRequired <= 0 || DaysCompleted >= CalendarDaysRequired);

        /// <summary>Sets the calendar clock. Called once, when the run is created or restored.</summary>
        public void SetCalendar(int daysRequired, int daysCompleted = 0)
        {
            CalendarDaysRequired = Math.Max(0, daysRequired);
            DaysCompleted = Math.Max(0, daysCompleted);
        }

        /// <summary>Moves the calendar on a day. The compute clock moves in Contribute.</summary>
        public void AdvanceCalendar() => DaysCompleted++;

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

        /// <summary>
        /// Whole days until this run lands, counting **both** clocks.
        ///
        /// **The banner used to read the compute clock alone**, so a playtest saw a run quoted at
        /// twenty-one days announce four, then sit at "0 days" while the calendar kept turning. Both
        /// symptoms were the same missing `Math.Max`: `IsComplete` needs the compute clock and the
        /// safety stage, and a countdown that watches one of two clocks is worse than no countdown,
        /// because it describes a thing that has already finished.
        ///
        /// <paramref name="petaflopDaysPerDay"/> must be what actually reaches this run after the
        /// cluster is split, not the size of the fleet. Reading the whole fleet here is the other
        /// half of why the quote and the banner disagreed.
        /// </summary>
        public int DaysRemaining(double petaflopDaysPerDay)
        {
            var calendar = Math.Max(0, CalendarDaysRequired - DaysCompleted);
            var compute = EstimatedDaysRemaining(petaflopDaysPerDay);

            if (double.IsPositiveInfinity(compute))
            {
                // Nothing is being delivered, so the run is not landing at all. The calendar is the
                // only honest thing left to report.
                return calendar;
            }

            return Math.Max(calendar, (int)Math.Ceiling(compute));
        }

        public override string ToString() =>
            $"{Blueprint.Name}: {Progress * 100.0:0.0}% of {PetaflopDaysRequired:N0} PF-days";
    }
}
