using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the company's whole fleet looks like on one day: the numbers a training run and the
    /// accountants both need. Produced by <see cref="ComputePool"/>, consumed by everything else.
    /// </summary>
    public readonly struct ComputeProfile
    {
        public ComputeProfile(
            int acceleratorCount,
            int rentedAcceleratorCount,
            int acceleratorsInTransit,
            double rawPetaflops,
            double effectivePetaflops,
            double utilizationCeiling,
            double balanceFactor,
            double scalingEfficiency,
            double totalAcceleratorMemoryGigabytes,
            double powerDrawKilowatts,
            double powerCapacityKilowatts,
            double dailyOperatingCostUsd,
            double dailyDepreciationUsd,
            long residualValueUsd,
            FleetBill bill = default)
        {
            Bill = bill;
            AcceleratorCount = Math.Max(0, acceleratorCount);
            RentedAcceleratorCount = Math.Max(0, rentedAcceleratorCount);
            AcceleratorsInTransit = Math.Max(0, acceleratorsInTransit);
            RawPetaflops = Math.Max(0.0, SimUnits.Finite(rawPetaflops));
            EffectivePetaflops = Math.Clamp(SimUnits.Finite(effectivePetaflops), 0.0, RawPetaflops);
            UtilizationCeiling = Math.Clamp(SimUnits.Finite(utilizationCeiling, 0.35), 0.0, 0.95);
            BalanceFactor = Math.Clamp(SimUnits.Finite(balanceFactor, 1.0), 0.0, 1.0);
            ScalingEfficiency = Math.Clamp(SimUnits.Finite(scalingEfficiency, 1.0), 0.0, 1.0);
            TotalAcceleratorMemoryGigabytes = Math.Max(0.0, SimUnits.Finite(totalAcceleratorMemoryGigabytes));
            PowerDrawKilowatts = Math.Max(0.0, SimUnits.Finite(powerDrawKilowatts));
            PowerCapacityKilowatts = Math.Max(0.0, SimUnits.Finite(powerCapacityKilowatts));
            DailyOperatingCostUsd = Math.Max(0.0, SimUnits.Finite(dailyOperatingCostUsd));
            DailyDepreciationUsd = Math.Max(0.0, SimUnits.Finite(dailyDepreciationUsd));
            ResidualValueUsd = Math.Max(0L, residualValueUsd);
        }

        /// <summary>Accelerators actually producing FLOPs today, owned and rented together.</summary>
        public int AcceleratorCount { get; }

        public int RentedAcceleratorCount { get; }

        /// <summary>Bought, paid for, not yet delivered. Capital already gone, work not yet started.</summary>
        public int AcceleratorsInTransit { get; }

        /// <summary>Nameplate throughput. Nobody ever gets this.</summary>
        public double RawPetaflops { get; }

        /// <summary>Weighted best-case model FLOPs utilization of the silicon in the fleet.</summary>
        public double UtilizationCeiling { get; }

        /// <summary>How well fed the owned accelerators are by hosts, memory and fabric. One is balanced.</summary>
        public double BalanceFactor { get; }

        /// <summary>The tax for spreading one run across a lot of accelerators.</summary>
        public double ScalingEfficiency { get; }

        public double TotalAcceleratorMemoryGigabytes { get; }
        public double PowerDrawKilowatts { get; }
        public double PowerCapacityKilowatts { get; }

        /// <summary>Rent, power, rack fees and maintenance for one day. Cash out, every day, busy or not.</summary>
        public double DailyOperatingCostUsd { get; }

        /// <summary>Value the owned fleet loses today. Not cash, but it is what makes early buying expensive.</summary>
        public double DailyDepreciationUsd { get; }

        /// <summary>
        /// What the daily bill is actually made of.
        ///
        /// The four amounts were already computed separately in <see cref="ComputePool"/> and then
        /// added together, so the books could only ever say "operating cost". Rented capacity was
        /// being reported to the player as the cost of serving paying users, on a screen where they
        /// had no users at all.
        /// </summary>
        public FleetBill Bill { get; }

        /// <summary>What the owned fleet would fetch if sold today.</summary>
        public long ResidualValueUsd { get; }

        /// <summary>
        /// Throughput a training run can actually count on, before the architecture multiplier.
        /// Nameplate throughput cut by utilization, by how well fed the owned half of the fleet is,
        /// and by the fabric tax on spreading one run wide. This is the number that turns a compute
        /// budget into a number of days on the calendar.
        /// </summary>
        public double EffectivePetaflops { get; }

        /// <summary>Share of nameplate throughput that reaches a training run.</summary>
        public double RealizedEfficiency => RawPetaflops <= 0.0 ? 0.0 : EffectivePetaflops / RawPetaflops;

        /// <summary>Blended all-in cost of one raw petaflop/s-day, including value lost.</summary>
        public double TotalCostPerPetaflopDayUsd =>
            RawPetaflops <= 0.0 ? 0.0 : (DailyOperatingCostUsd + DailyDepreciationUsd) / RawPetaflops;

        public bool IsOverPowerBudget => PowerDrawKilowatts > PowerCapacityKilowatts;

        /// <summary>
        /// The same fleet with some of it already spoken for.
        ///
        /// **Everything except throughput is untouched, and that is the point.** The company still
        /// owns the cards, still pays for the power and still watches them depreciate; what it does
        /// not have is the capacity, because somebody else is holding it. A state contract does not
        /// queue behind consumer traffic, so its share comes off the top and the market divides what
        /// is left.
        ///
        /// Copying the struct rather than reaching into `ServeMarket` keeps one rule about how much
        /// throughput exists. A second subtraction inside the market would be a second place for the
        /// answer to live, and this project has paid for that arrangement before.
        /// </summary>
        public ComputeProfile WithReserved(double petaflops)
        {
            var held = Math.Clamp(SimUnits.Finite(petaflops), 0.0, EffectivePetaflops);

            if (held <= 0.0)
            {
                return this;
            }

            return new ComputeProfile(
                AcceleratorCount, RentedAcceleratorCount, AcceleratorsInTransit,
                RawPetaflops, EffectivePetaflops - held,
                UtilizationCeiling, BalanceFactor, ScalingEfficiency,
                TotalAcceleratorMemoryGigabytes, PowerDrawKilowatts, PowerCapacityKilowatts,
                DailyOperatingCostUsd, DailyDepreciationUsd, ResidualValueUsd, Bill);
        }

        public static ComputeProfile Empty => new(0, 0, 0, 0, 0, 0.35, 1, 1, 0, 0, 0, 0, 0, 0);
    }
}
