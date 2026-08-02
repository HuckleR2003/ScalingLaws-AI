using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>The five things in-house research can push on. Explicit values; they go into saves.</summary>
    public enum ResearchDirection
    {
        /// <summary>Fewer parameters firing per token. The biggest lever on what a run costs.</summary>
        Sparsity = 0,

        /// <summary>Better utilisation of the cluster during training. Shortens the calendar.</summary>
        Throughput = 1,

        /// <summary>More quality out of each parameter. Raises the ceiling on every model in the family.</summary>
        Quality = 2,

        /// <summary>Cheaper tokens once the model is live. Invisible until the price war.</summary>
        Serving = 3,

        /// <summary>Structural reasoning gains that scaling alone does not buy.</summary>
        Reasoning = 4
    }

    /// <summary>
    /// A family the company intends to design.
    ///
    /// The same shape as a model blueprint on purpose: a handful of decisions, all of them
    /// irreversible once the programme starts. Weight the five directions, set a budget, set a
    /// deadline, and optionally build on a family you already own instead of starting clean.
    ///
    /// Weights are relative. Spreading them evenly produces a family that is mediocre at everything,
    /// which is a real and usually bad choice, not a safe default.
    /// </summary>
    public readonly struct ArchitectureBlueprint
    {
        public const int DirectionCount = 5;
        public const int MinimumDurationDays = 60;
        public const int MaximumDurationDays = 900;
        public const long MinimumBudgetUsd = 2_000_000;
        public const long MaximumBudgetUsd = 20_000_000_000;

        private readonly double[] weights;

        public ArchitectureBlueprint(
            string name,
            ArchitectureId slot,
            ArchitectureId baseFamily,
            double sparsity,
            double throughput,
            double quality,
            double serving,
            double reasoning,
            long researchBudgetUsd,
            int durationDays)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled family" : name.Trim();
            Slot = slot;
            BaseFamily = baseFamily;
            ResearchBudgetUsd = Math.Clamp(researchBudgetUsd, MinimumBudgetUsd, MaximumBudgetUsd);
            DurationDays = Math.Clamp(durationDays, MinimumDurationDays, MaximumDurationDays);

            weights = new double[DirectionCount];
            weights[(int)ResearchDirection.Sparsity] = Math.Clamp(SimUnits.Finite(sparsity), 0.0, 1.0);
            weights[(int)ResearchDirection.Throughput] = Math.Clamp(SimUnits.Finite(throughput), 0.0, 1.0);
            weights[(int)ResearchDirection.Quality] = Math.Clamp(SimUnits.Finite(quality), 0.0, 1.0);
            weights[(int)ResearchDirection.Serving] = Math.Clamp(SimUnits.Finite(serving), 0.0, 1.0);
            weights[(int)ResearchDirection.Reasoning] = Math.Clamp(SimUnits.Finite(reasoning), 0.0, 1.0);
        }

        public string Name { get; }

        /// <summary>Which of the six family slots this will occupy. Reusing one overwrites it.</summary>
        public ArchitectureId Slot { get; }

        /// <summary>
        /// The family this builds on, or None to start clean. Iterating is cheaper and faster and
        /// hits a lower ceiling each time; starting clean costs full price and has no such ceiling.
        /// </summary>
        public ArchitectureId BaseFamily { get; }

        public long ResearchBudgetUsd { get; }
        public int DurationDays { get; }

        public bool IsIteration => BaseFamily != ArchitectureId.None;

        public double Weight(ResearchDirection direction)
        {
            var index = (int)direction;
            return index < 0 || index >= DirectionCount ? 0.0 : weights[index];
        }

        /// <summary>Sum of every weight. Zero means the programme has no direction at all.</summary>
        public double TotalWeight
        {
            get
            {
                var total = 0.0;
                for (var index = 0; index < DirectionCount; index++)
                {
                    total += weights[index];
                }

                return total;
            }
        }

        /// <summary>A direction's share of the effort, 0 to 1.</summary>
        public double NormalizedWeight(ResearchDirection direction)
        {
            var total = TotalWeight;
            return total <= 0.0 ? 0.0 : Weight(direction) / total;
        }

        /// <summary>
        /// How concentrated the programme is, 0 to 1. One means everything on a single direction.
        /// Focus is rewarded: a lab chasing all five at once gets a fifth of the depth in each.
        /// </summary>
        public double Focus
        {
            get
            {
                var total = TotalWeight;
                if (total <= 0.0)
                {
                    return 0.0;
                }

                var sumOfSquares = 0.0;
                for (var index = 0; index < DirectionCount; index++)
                {
                    var share = weights[index] / total;
                    sumOfSquares += share * share;
                }

                // Herfindahl, rescaled so an even spread is 0 and a single direction is 1.
                var even = 1.0 / DirectionCount;
                return Math.Clamp((sumOfSquares - even) / (1.0 - even), 0.0, 1.0);
            }
        }

        public ArchitectureBlueprint WithWeight(ResearchDirection direction, double value)
        {
            return new ArchitectureBlueprint(
                Name,
                Slot,
                BaseFamily,
                direction == ResearchDirection.Sparsity ? value : Weight(ResearchDirection.Sparsity),
                direction == ResearchDirection.Throughput ? value : Weight(ResearchDirection.Throughput),
                direction == ResearchDirection.Quality ? value : Weight(ResearchDirection.Quality),
                direction == ResearchDirection.Serving ? value : Weight(ResearchDirection.Serving),
                direction == ResearchDirection.Reasoning ? value : Weight(ResearchDirection.Reasoning),
                ResearchBudgetUsd,
                DurationDays);
        }

        public ArchitectureBlueprint WithBudget(long budgetUsd) => new(
            Name, Slot, BaseFamily,
            Weight(ResearchDirection.Sparsity), Weight(ResearchDirection.Throughput),
            Weight(ResearchDirection.Quality), Weight(ResearchDirection.Serving),
            Weight(ResearchDirection.Reasoning), budgetUsd, DurationDays);

        public ArchitectureBlueprint WithDuration(int durationDays) => new(
            Name, Slot, BaseFamily,
            Weight(ResearchDirection.Sparsity), Weight(ResearchDirection.Throughput),
            Weight(ResearchDirection.Quality), Weight(ResearchDirection.Serving),
            Weight(ResearchDirection.Reasoning), ResearchBudgetUsd, durationDays);

        public ArchitectureBlueprint WithName(string name) => new(
            name, Slot, BaseFamily,
            Weight(ResearchDirection.Sparsity), Weight(ResearchDirection.Throughput),
            Weight(ResearchDirection.Quality), Weight(ResearchDirection.Serving),
            Weight(ResearchDirection.Reasoning), ResearchBudgetUsd, DurationDays);

        public ArchitectureBlueprint WithSlot(ArchitectureId slot) => new(
            Name, slot, BaseFamily,
            Weight(ResearchDirection.Sparsity), Weight(ResearchDirection.Throughput),
            Weight(ResearchDirection.Quality), Weight(ResearchDirection.Serving),
            Weight(ResearchDirection.Reasoning), ResearchBudgetUsd, DurationDays);

        public ArchitectureBlueprint WithBaseFamily(ArchitectureId baseFamily) => new(
            Name, Slot, baseFamily,
            Weight(ResearchDirection.Sparsity), Weight(ResearchDirection.Throughput),
            Weight(ResearchDirection.Quality), Weight(ResearchDirection.Serving),
            Weight(ResearchDirection.Reasoning), ResearchBudgetUsd, DurationDays);

        /// <summary>A sane opening programme: sparsity led, one year, ten million.</summary>
        public static ArchitectureBlueprint Default(ArchitectureId slot) => new(
            "House family 1", slot, ArchitectureId.None,
            sparsity: 1.0, throughput: 0.4, quality: 0.5, serving: 0.3, reasoning: 0.2,
            researchBudgetUsd: 10_000_000, durationDays: 365);

        public override string ToString() =>
            $"{Name}: {UiDirections()} on {ResearchBudgetUsd:N0} over {DurationDays} days";

        private string UiDirections()
        {
            var best = ResearchDirection.Sparsity;
            var bestWeight = -1.0;
            for (var index = 0; index < DirectionCount; index++)
            {
                if (weights[index] > bestWeight)
                {
                    bestWeight = weights[index];
                    best = (ResearchDirection)index;
                }
            }

            return best.ToString();
        }
    }
}
