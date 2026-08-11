using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What a run is expected to produce, worked out before a single FLOP is spent.
    ///
    /// This is an estimate and the type name says so. A projection never enters the market, never
    /// counts toward the company's best capability and never appears on a scoreboard: only a run
    /// that finished produces a measured number. Same rule PC Workman applies to estimated CPU
    /// temperatures, and for the same reason. If a guess and a measurement are stored in the same
    /// field, the guess eventually gets treated as fact.
    /// </summary>
    public readonly struct TrainingProjection
    {
        public TrainingProjection(
            ModelBlueprint blueprint,
            bool isFeasible,
            string blockingReason,
            double projectedLoss,
            double projectedCapability,
            double shapeEfficiency,
            double tokensPerParameter,
            double optimalTokensPerParameter,
            double trainingPetaflopDays,
            double effectivePetaflops,
            int trainingDays,
            long computeCashCostUsd,
            long computeEconomicCostUsd,
            long dataAcquisitionCostUsd,
            double memoryRequiredGigabytes,
            double memoryAvailableGigabytes,
            DatasetBlend blend)
        {
            Blueprint = blueprint;
            IsFeasible = isFeasible;
            BlockingReason = isFeasible ? string.Empty : blockingReason ?? string.Empty;
            ProjectedLoss = Math.Max(0.0, SimUnits.Finite(projectedLoss, 3.0));
            ProjectedCapability = Math.Clamp(SimUnits.Finite(projectedCapability), 0.0, 100.0);
            ShapeEfficiency = Math.Clamp(SimUnits.Finite(shapeEfficiency), 0.0, 1.0);
            TokensPerParameter = Math.Max(0.0, SimUnits.Finite(tokensPerParameter));
            OptimalTokensPerParameter = Math.Max(0.0, SimUnits.Finite(optimalTokensPerParameter));
            TrainingPetaflopDays = Math.Max(0.0, SimUnits.Finite(trainingPetaflopDays));
            EffectivePetaflops = Math.Max(0.0, SimUnits.Finite(effectivePetaflops));
            TrainingDays = Math.Max(0, trainingDays);
            ComputeCashCostUsd = Math.Max(0L, computeCashCostUsd);
            ComputeEconomicCostUsd = Math.Max(0L, computeEconomicCostUsd);
            DataAcquisitionCostUsd = Math.Max(0L, dataAcquisitionCostUsd);
            MemoryRequiredGigabytes = Math.Max(0.0, SimUnits.Finite(memoryRequiredGigabytes));
            MemoryAvailableGigabytes = Math.Max(0.0, SimUnits.Finite(memoryAvailableGigabytes));
            Blend = blend;
        }

        public ModelBlueprint Blueprint { get; }

        /// <summary>False when the run cannot legally start. <see cref="BlockingReason"/> says why.</summary>
        public bool IsFeasible { get; }

        /// <summary>Empty when feasible. Otherwise every unmet condition, in one sentence.</summary>
        public string BlockingReason { get; }

        public double ProjectedLoss { get; }

        /// <summary>Expected capability. What the run actually lands on will differ.</summary>
        public double ProjectedCapability { get; }

        /// <summary>How much of the compute budget the chosen shape converts into capability, 0 to 1.</summary>
        public double ShapeEfficiency { get; }

        public double TokensPerParameter { get; }

        /// <summary>The ratio a compute-optimal run of this budget would have used.</summary>
        public double OptimalTokensPerParameter { get; }

        public double TrainingPetaflopDays { get; }
        public double EffectivePetaflops { get; }
        public int TrainingDays { get; }

        /// <summary>Cash the run burns: rent, power, rack fees, maintenance. Money that leaves the account.</summary>
        public long ComputeCashCostUsd { get; }

        /// <summary>Cash cost plus the value the owned fleet loses over the run. The true price.</summary>
        public long ComputeEconomicCostUsd { get; }

        /// <summary>
        /// What the corpora in this mix cost to license. A reference figure: if the company already
        /// owns them the money is long gone, and the run is not charged for it again.
        /// </summary>
        public long DataAcquisitionCostUsd { get; }

        public double MemoryRequiredGigabytes { get; }
        public double MemoryAvailableGigabytes { get; }
        public DatasetBlend Blend { get; }

        /// <summary>Cash this run will actually burn. Data the company already owns is not in here.</summary>
        public long TotalCashCostUsd => ComputeCashCostUsd;

        /// <summary>Is the run undertrained relative to compute-optimal, meaning too big for its data.</summary>
        /// <summary>
        /// Where the efficient band starts and ends, as a fraction of the optimal ratio.
        ///
        /// These were inline magic numbers on the two properties below. The Scale belt and the
        /// profile badge both have to draw the same band, and a band drawn from a second copy of
        /// these numbers would eventually disagree with the words printed next to it.
        /// </summary>
        public const double UndertrainedBelow = 0.6;

        public const double OvertrainedAbove = 1.8;

        public bool IsUndertrained => TokensPerParameter < OptimalTokensPerParameter * UndertrainedBelow;

        /// <summary>Is the run overtrained, meaning the parameters ran out before the tokens did.</summary>
        public bool IsOvertrained => TokensPerParameter > OptimalTokensPerParameter * OvertrainedAbove;

        /// <summary>
        /// The ratio as a multiple of optimal. One is compute optimal, below one is short of tokens,
        /// above one is spending on data past the point it buys much.
        /// </summary>
        public double ShapeRatio => OptimalTokensPerParameter <= 0.0
            ? 0.0
            : TokensPerParameter / OptimalTokensPerParameter;

        public static TrainingProjection Blocked(ModelBlueprint blueprint, string reason) =>
            new(blueprint, false, reason, 3.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, DatasetBlend.Empty);

        public override string ToString() => IsFeasible
            ? $"{Blueprint.Name}: cap {ProjectedCapability:0.0} in {TrainingDays}d for ${TotalCashCostUsd:N0}"
            : $"{Blueprint.Name}: blocked ({BlockingReason})";
    }
}
