using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// One simulated day, closed out. Every number a ledger or a chart could want, and nothing that
    /// needs recomputing to be understood.
    /// </summary>
    public readonly struct DayReport
    {
        public DayReport(
            GameDate date,
            double marketShare,
            double demandedBillionTokens,
            double servedBillionTokens,
            long revenueUsd,
            long operatingCostUsd,
            long depreciationUsd,
            long cashAfterUsd,
            double trainingProgress,
            double bestCapability,
            double frontierCapability)
        {
            Date = date;
            MarketShare = Math.Clamp(SimUnits.Finite(marketShare), 0.0, 1.0);
            DemandedBillionTokens = Math.Max(0.0, SimUnits.Finite(demandedBillionTokens));
            ServedBillionTokens = Math.Clamp(SimUnits.Finite(servedBillionTokens), 0.0, DemandedBillionTokens);
            RevenueUsd = Math.Max(0L, revenueUsd);
            OperatingCostUsd = Math.Max(0L, operatingCostUsd);
            DepreciationUsd = Math.Max(0L, depreciationUsd);
            CashAfterUsd = cashAfterUsd;
            TrainingProgress = Math.Clamp(SimUnits.Finite(trainingProgress), 0.0, 1.0);
            BestCapability = Math.Clamp(SimUnits.Finite(bestCapability), 0.0, 100.0);
            FrontierCapability = Math.Clamp(SimUnits.Finite(frontierCapability), 0.0, 100.0);
        }

        public GameDate Date { get; }
        public double MarketShare { get; }

        /// <summary>Tokens the market wanted from the company.</summary>
        public double DemandedBillionTokens { get; }

        /// <summary>Tokens the fleet could actually produce. The gap is revenue that walked away.</summary>
        public double ServedBillionTokens { get; }

        public long RevenueUsd { get; }

        /// <summary>Cash out: rent, power, rack fees, maintenance.</summary>
        public long OperatingCostUsd { get; }

        /// <summary>Value the owned fleet lost. Not cash, but it is the real cost of owning early.</summary>
        public long DepreciationUsd { get; }

        public long CashAfterUsd { get; }
        public double TrainingProgress { get; }

        /// <summary>Best measured capability the company has live.</summary>
        public double BestCapability { get; }

        public double FrontierCapability { get; }

        /// <summary>Revenue minus the cash that left. What the bank account felt.</summary>
        public long CashFlowUsd => RevenueUsd - OperatingCostUsd;

        /// <summary>Cash flow minus value lost. What the business actually earned.</summary>
        public long EconomicProfitUsd => RevenueUsd - OperatingCostUsd - DepreciationUsd;

        public double UnservedBillionTokens => Math.Max(0.0, DemandedBillionTokens - ServedBillionTokens);

        /// <summary>How far behind the frontier the company is. Positive means behind.</summary>
        public double CapabilityGap => FrontierCapability - BestCapability;

        public override string ToString() =>
            $"{Date}: share {MarketShare * 100.0:0.00}%, revenue ${RevenueUsd:N0}, cash ${CashAfterUsd:N0}";
    }
}
