using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// An immutable read of the company for anything that only wants to look: the future HUD, a
    /// save writer, an assertion in a test. Built by copying, so holding one while the simulation
    /// runs on cannot show a half-updated day.
    /// </summary>
    public readonly struct CompanySnapshot
    {
        public CompanySnapshot(CompanyState state, ComputeProfile profile, MarketConditions market)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            CompanyName = state.CompanyName;
            Date = state.Date;
            CashUsd = state.CashUsd;
            Reputation = state.Reputation;
            BestCapability = state.BestCapability;
            FrontierCapability = market.FrontierCapability;
            ReleasedModelCount = state.ReleasedModelCount;
            IsBankrupt = state.IsBankrupt;

            AcceleratorCount = profile.AcceleratorCount;
            RentedAcceleratorCount = profile.RentedAcceleratorCount;
            AcceleratorsInTransit = profile.AcceleratorsInTransit;
            RawPetaflops = profile.RawPetaflops;
            EffectivePetaflops = profile.EffectivePetaflops;
            FleetResidualValueUsd = profile.ResidualValueUsd;
            DailyOperatingCostUsd = SimUnits.ToDollars(profile.DailyOperatingCostUsd);
            DailyDepreciationUsd = SimUnits.ToDollars(profile.DailyDepreciationUsd);

            LifetimeRevenueUsd = state.LifetimeRevenueUsd;
            LifetimeOperatingCostUsd = state.LifetimeOperatingCostUsd;
            LifetimeCapitalSpentUsd = state.LifetimeCapitalSpentUsd;

            var run = state.ActiveRun;
            IsTraining = run != null;
            TrainingModelName = run?.Blueprint.Name ?? string.Empty;
            TrainingProgress = run?.Progress ?? 0.0;
            TrainingProjectedCapability = run?.ProjectedCapability ?? 0.0;

            MarketPricePerMillionTokensUsd = market.PricePerMillionTokensUsd;
            MarketDemandBillionTokensPerDay = market.TotalDemandBillionTokensPerDay;
            RentPricePerPetaflopHourUsd = market.RentPricePerPetaflopHourUsd;
            ScarcityIndex = market.ScarcityIndex;
        }

        public string CompanyName { get; }
        public GameDate Date { get; }
        public long CashUsd { get; }
        public double Reputation { get; }

        /// <summary>Measured, never projected.</summary>
        public double BestCapability { get; }

        public double FrontierCapability { get; }
        public int ReleasedModelCount { get; }
        public bool IsBankrupt { get; }

        public int AcceleratorCount { get; }
        public int RentedAcceleratorCount { get; }
        public int AcceleratorsInTransit { get; }
        public double RawPetaflops { get; }
        public double EffectivePetaflops { get; }
        public long FleetResidualValueUsd { get; }
        public long DailyOperatingCostUsd { get; }
        public long DailyDepreciationUsd { get; }

        public long LifetimeRevenueUsd { get; }
        public long LifetimeOperatingCostUsd { get; }
        public long LifetimeCapitalSpentUsd { get; }

        public bool IsTraining { get; }
        public string TrainingModelName { get; }
        public double TrainingProgress { get; }

        /// <summary>The estimate the run started with. Labelled as such wherever it is shown.</summary>
        public double TrainingProjectedCapability { get; }

        public double MarketPricePerMillionTokensUsd { get; }
        public double MarketDemandBillionTokensPerDay { get; }
        public double RentPricePerPetaflopHourUsd { get; }
        public double ScarcityIndex { get; }

        /// <summary>Positive means behind the best rival on the market.</summary>
        public double CapabilityGap => FrontierCapability - BestCapability;

        /// <summary>Cash plus what the fleet would fetch if sold today.</summary>
        public long NetWorthUsd => CashUsd + FleetResidualValueUsd;

        public override string ToString() =>
            $"{CompanyName} {Date}: ${CashUsd:N0}, cap {BestCapability:0.0} vs frontier {FrontierCapability:0.0}";
    }
}
