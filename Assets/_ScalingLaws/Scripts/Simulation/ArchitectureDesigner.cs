using System;
using System.Text;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>What a research programme is expected to produce, before it is committed to.</summary>
    public readonly struct ArchitectureProjection
    {
        public ArchitectureProjection(
            ArchitectureBlueprint blueprint,
            bool isFeasible,
            string blockingReason,
            ArchitectureDefinition expected,
            ArchitectureDefinition floor,
            ArchitectureDefinition ceiling,
            double researchPower,
            double variance,
            double petaflopDaysRequired,
            ArchitectureDefinition baseline)
        {
            Blueprint = blueprint;
            IsFeasible = isFeasible;
            BlockingReason = isFeasible ? string.Empty : blockingReason ?? string.Empty;
            Expected = expected;
            Floor = floor;
            Ceiling = ceiling;
            ResearchPower = Math.Clamp(SimUnits.Finite(researchPower), 0.0, 1.5);
            Variance = Math.Clamp(SimUnits.Finite(variance), 0.0, 1.0);
            PetaflopDaysRequired = Math.Max(0.0, SimUnits.Finite(petaflopDaysRequired));
            Baseline = baseline;
        }

        public ArchitectureBlueprint Blueprint { get; }
        public bool IsFeasible { get; }
        public string BlockingReason { get; }

        /// <summary>The middle of the distribution. Not a promise.</summary>
        public ArchitectureDefinition Expected { get; }

        /// <summary>Roughly the bad tail. A programme can land here.</summary>
        public ArchitectureDefinition Floor { get; }

        /// <summary>Roughly the good tail, already clipped to what the field could plausibly know.</summary>
        public ArchitectureDefinition Ceiling { get; }

        /// <summary>What the programme starts from: the parent family, or the dense baseline.</summary>
        public ArchitectureDefinition Baseline { get; }

        /// <summary>Budget and calendar combined, 0 to about 1.2. Drives both depth and certainty.</summary>
        public double ResearchPower { get; }

        /// <summary>Spread of the outcome. A cheap, rushed programme is close to a coin toss.</summary>
        public double Variance { get; }

        public double PetaflopDaysRequired { get; }

        /// <summary>Share of the FLOPs per token the expected result saves against the baseline.</summary>
        public double ComputeSavingVersusBaseline => Baseline.ActiveParameterFraction <= 0.0
            ? 0.0
            : 1.0 - Expected.ActiveParameterFraction / Baseline.ActiveParameterFraction;

        public override string ToString() => IsFeasible
            ? $"{Blueprint.Name}: active {Expected.ActiveParameterFraction:0.000}, bonus {Expected.CapabilityBonus:0.0}"
            : $"{Blueprint.Name}: blocked ({BlockingReason})";
    }

    /// <summary>
    /// The ONE place an in-house architecture family is designed and rolled.
    ///
    /// Deliberately the same shape as <see cref="TrainingPlanner"/>: a blueprint plus the world goes
    /// in, a fully priced projection comes out, and the same numbers are used when the programme
    /// actually resolves. The difference is that a training run lands within a point or so of its
    /// projection, and a research programme does not. Research is where the variance lives.
    ///
    /// Three things govern the outcome:
    ///   power     budget and calendar together. Neither alone does much.
    ///   focus     a programme chasing all five directions gets a fifth of the depth in each.
    ///   ceiling   nobody invents 2026 techniques in 2022. The best published family of the day,
    ///             improved on by a margin, is the hard cap.
    ///
    /// Iterating an owned family is cheaper and faster and hits a lower ceiling each generation.
    /// Families plateau. Eventually a clean sheet is the only way forward, and it costs full price.
    /// </summary>
    public static class ArchitectureDesigner
    {
        /// <summary>Budget at which budget power reads zero.</summary>
        public const long BudgetFloorUsd = 2_000_000;

        /// <summary>Budget at which budget power reads one. A thousand times the floor.</summary>
        public const long BudgetCeilingUsd = 2_000_000_000;

        /// <summary>Calendar at which time power reads one.</summary>
        public const int DurationCeilingDays = 540;

        /// <summary>Dollars of budget per petaflop/s-day of research compute the programme consumes.</summary>
        public const double BudgetPerPetaflopDay = 40_000.0;

        /// <summary>How far past the best published technique in-house research can reach.</summary>
        public const double FieldCeilingMargin = 1.35;

        /// <summary>Discount on cost and calendar when building on a family already owned.</summary>
        public const double IterationCostFactor = 0.60;

        /// <summary>Each generation of a family reaches a fraction of the gain the last one did.</summary>
        public const double IterationDiminishing = 0.55;

        public const int MaximumGenerations = 6;

        // How far each direction can move its stat at full depth.
        private const double SparsityMaxCut = 0.65;
        private const double ThroughputMaxGain = 0.45;
        private const double QualityMaxGain = 0.35;
        private const double ServingMaxCut = 0.50;
        private const double ReasoningMaxBonus = 8.0;

        /// <summary>Budget and calendar folded into one number. Both are needed; neither substitutes.</summary>
        public static double ResearchPower(long budgetUsd, int durationDays)
        {
            var budget = Math.Clamp(budgetUsd, BudgetFloorUsd, BudgetCeilingUsd);
            var budgetPower = Math.Log10(budget / (double)BudgetFloorUsd)
                / Math.Log10(BudgetCeilingUsd / (double)BudgetFloorUsd);

            var timePower = Math.Clamp(durationDays / (double)DurationCeilingDays, 0.15, 1.2);

            // Geometric mean: a billion dollars in three months is not a breakthrough, and neither
            // is three years of two people.
            return Math.Clamp(Math.Sqrt(Math.Clamp(budgetPower, 0.0, 1.2) * timePower), 0.0, 1.2);
        }

        /// <summary>Spread of the outcome. Falls as the programme gets better funded and longer.</summary>
        public static double Variance(double researchPower) =>
            Math.Clamp(0.42 - 0.32 * Math.Clamp(researchPower, 0.0, 1.2), 0.05, 0.45);

        public static double PetaflopDaysFor(long budgetUsd) =>
            Math.Max(0.0, budgetUsd / BudgetPerPetaflopDay);

        /// <summary>Cash the programme actually costs, after any iteration discount.</summary>
        public static long CashCostUsd(ArchitectureBlueprint blueprint) =>
            SimUnits.ToDollars(blueprint.ResearchBudgetUsd * (blueprint.IsIteration ? IterationCostFactor : 1.0));

        /// <summary>Calendar the programme actually takes, after any iteration discount.</summary>
        public static int DurationDays(ArchitectureBlueprint blueprint) => (int)Math.Max(
            ArchitectureBlueprint.MinimumDurationDays,
            Math.Round(blueprint.DurationDays * (blueprint.IsIteration ? IterationCostFactor : 1.0)));

        /// <summary>
        /// Prices a programme. <paramref name="generation"/> is how many times this lineage has
        /// already been iterated, which is what makes a family plateau.
        /// </summary>
        public static ArchitectureProjection Project(
            ArchitectureBlueprint blueprint,
            GameDate date,
            IArchitectureSource architectures,
            long availableCashUsd,
            int generation)
        {
            var source = architectures ?? ArchitectureCatalog.AsSource;
            var blocking = new StringBuilder();

            if (!ArchitectureCatalog.IsCustomSlot(blueprint.Slot))
            {
                Append(blocking, "a house family needs one of the six custom slots");
            }

            var baseline = ArchitectureCatalog.Baseline;
            if (blueprint.IsIteration)
            {
                if (!source.TryGetArchitecture(blueprint.BaseFamily, out baseline))
                {
                    Append(blocking, "the family it builds on is not owned");
                    baseline = ArchitectureCatalog.Baseline;
                }
                else if (generation >= MaximumGenerations)
                {
                    Append(blocking, $"this lineage has been iterated {MaximumGenerations} times and has nothing left");
                }
            }

            if (blueprint.TotalWeight <= 0.0)
            {
                Append(blocking, "the programme has no research direction at all");
            }

            var cash = CashCostUsd(blueprint);
            if (availableCashUsd < cash)
            {
                Append(blocking, $"needs ${cash:N0}, has ${Math.Max(0L, availableCashUsd):N0}");
            }

            var power = ResearchPower(blueprint.ResearchBudgetUsd, blueprint.DurationDays);
            var focus = blueprint.Focus;
            var depth = power * (0.55 + 0.45 * focus);

            // Each generation of the same lineage reaches less than the one before it.
            if (blueprint.IsIteration)
            {
                depth *= Math.Pow(IterationDiminishing, Math.Clamp(generation, 0, MaximumGenerations));
            }

            var variance = Variance(power);
            var fieldCeiling = FieldCeiling(date);

            var expected = Roll(blueprint, baseline, fieldCeiling, depth, 1.0);
            var floor = Roll(blueprint, baseline, fieldCeiling, depth, Math.Max(0.0, 1.0 - variance));
            var ceiling = Roll(blueprint, baseline, fieldCeiling, depth, 1.0 + variance);

            return new ArchitectureProjection(
                blueprint,
                blocking.Length == 0,
                blocking.ToString(),
                expected,
                floor,
                ceiling,
                power,
                variance,
                PetaflopDaysFor(blueprint.ResearchBudgetUsd),
                baseline);
        }

        /// <summary>
        /// Resolves a finished programme. The multiplier is drawn once, here, and nowhere else, so
        /// the same seed always produces the same family.
        /// </summary>
        public static ArchitectureDefinition Resolve(
            ArchitectureProjection projection,
            GameDate date,
            DeterministicRandom random)
        {
            var blueprint = projection.Blueprint;
            var power = projection.ResearchPower;
            var focus = blueprint.Focus;
            var depth = power * (0.55 + 0.45 * focus);

            var roll = Math.Clamp(random.NextGaussian(1.0, projection.Variance), 0.15, 1.0 + projection.Variance * 1.5);
            return Roll(blueprint, projection.Baseline, FieldCeiling(date), depth, roll, date, blueprint.Slot, blueprint.Name);
        }

        /// <summary>
        /// The best value of each stat any published family offers on a date, improved by
        /// <see cref="FieldCeilingMargin"/>. In-house research can beat the field, not leap it.
        /// </summary>
        private static ArchitectureDefinition FieldCeiling(GameDate date)
        {
            var bestActive = 1.0;
            var bestTraining = 1.0;
            var bestParameter = 1.0;
            var bestInference = 1.0;
            var bestBonus = 0.0;

            foreach (var entry in ArchitectureCatalog.AvailableOn(date))
            {
                bestActive = Math.Min(bestActive, entry.ActiveParameterFraction);
                bestTraining = Math.Max(bestTraining, entry.TrainingEfficiency);
                bestParameter = Math.Max(bestParameter, entry.ParameterEfficiency);
                bestInference = Math.Min(bestInference, entry.InferenceCostMultiplier);
                bestBonus = Math.Max(bestBonus, entry.CapabilityBonus);
            }

            return new ArchitectureDefinition(
                ArchitectureId.None,
                "field ceiling",
                date,
                parameterEfficiency: bestParameter * FieldCeilingMargin,
                activeParameterFraction: bestActive / FieldCeilingMargin,
                trainingEfficiency: bestTraining * FieldCeilingMargin,
                inferenceCostMultiplier: bestInference / FieldCeilingMargin,
                capabilityBonus: bestBonus * FieldCeilingMargin + 2.0,
                adoptionCostUsd: 0);
        }

        private static ArchitectureDefinition Roll(
            ArchitectureBlueprint blueprint,
            ArchitectureDefinition baseline,
            ArchitectureDefinition fieldCeiling,
            double depth,
            double multiplier,
            GameDate availableFrom = default,
            ArchitectureId id = ArchitectureId.None,
            string name = null)
        {
            var scaled = Math.Max(0.0, depth * multiplier);

            var sparsity = blueprint.NormalizedWeight(ResearchDirection.Sparsity) * scaled;
            var throughput = blueprint.NormalizedWeight(ResearchDirection.Throughput) * scaled;
            var quality = blueprint.NormalizedWeight(ResearchDirection.Quality) * scaled;
            var serving = blueprint.NormalizedWeight(ResearchDirection.Serving) * scaled;
            var reasoning = blueprint.NormalizedWeight(ResearchDirection.Reasoning) * scaled;

            var activeFraction = Math.Max(
                fieldCeiling.ActiveParameterFraction,
                baseline.ActiveParameterFraction * (1.0 - SparsityMaxCut * Math.Clamp(sparsity, 0.0, 1.0)));

            var trainingEfficiency = Math.Min(
                fieldCeiling.TrainingEfficiency,
                baseline.TrainingEfficiency * (1.0 + ThroughputMaxGain * throughput));

            var parameterEfficiency = Math.Min(
                fieldCeiling.ParameterEfficiency,
                baseline.ParameterEfficiency * (1.0 + QualityMaxGain * quality));

            var inferenceCost = Math.Max(
                fieldCeiling.InferenceCostMultiplier,
                baseline.InferenceCostMultiplier * (1.0 - ServingMaxCut * Math.Clamp(serving, 0.0, 1.0)));

            var capabilityBonus = Math.Min(
                fieldCeiling.CapabilityBonus,
                baseline.CapabilityBonus + ReasoningMaxBonus * reasoning);

            return new ArchitectureDefinition(
                id,
                string.IsNullOrWhiteSpace(name) ? blueprint.Name : name,
                availableFrom,
                parameterEfficiency,
                activeFraction,
                trainingEfficiency,
                inferenceCost,
                capabilityBonus,
                adoptionCostUsd: 0);
        }

        private static void Append(StringBuilder builder, string reason)
        {
            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(reason);
        }
    }
}
