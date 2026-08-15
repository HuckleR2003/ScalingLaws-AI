using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A model the company intends to train: the four decisions that define a run, and nothing else.
    ///
    /// Architecture, size, data volume and data mix. Everything the player can get wrong is in here,
    /// and none of it can be undone once the run starts.
    /// </summary>
    public readonly struct ModelBlueprint
    {
        /// <summary>
        /// The parameter slider's own bounds, in log10 of billions.
        ///
        /// They live here rather than in the creator because the ceiling is a rule now, and a rule
        /// enforced in `Simulation/` cannot read a constant that only exists in `UI/`. The creator
        /// reads these; nothing else defines them.
        /// </summary>
        public const double LowLogParameters = -1.0;    // 0.1B

        /// <inheritdoc cref="LowLogParameters"/>
        public const double HighLogParameters = 4.0;    // 10,000B

        public const double MinimumParameterBillions = 0.05;
        public const double MaximumParameterBillions = 100_000.0;
        public const double MinimumTokenBillions = 1.0;
        public const double MaximumTokenBillions = 1_000_000.0;

        public ModelBlueprint(
            string name,
            ArchitectureId architecture,
            double parameterCountBillions,
            double trainingTokensBillions,
            DatasetSource dataSources,
            ModelType type = ModelType.General,
            string family = null,
            TrainingPrecision precision = TrainingPrecision.BFloat16,
            ModelShape shape = ModelShape.Balanced,
            DeduplicationPass deduplication = DeduplicationPass.Standard,
            int cutoffMonthsBack = 0)
        {
            // All four default to the neutral option, so every caller written before them describes
            // exactly the run it always described. The middle of each catalog is 1.0 on every axis.
            Precision = precision;
            Shape = shape;
            Deduplication = deduplication;
            CutoffMonthsBack = Math.Clamp(cutoffMonthsBack, 0, 36);
            // A line the model belongs to. Empty means it starts one of its own.
            Family = string.IsNullOrWhiteSpace(family) ? string.Empty : family.Trim();
            // Optional and defaulted rather than required, because every existing caller predates
            // types and a general model is exactly what all of them meant.
            Type = type == ModelType.None ? ModelType.General : type;
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled model" : name.Trim();
            Architecture = architecture == ArchitectureId.None ? ArchitectureId.DenseTransformer : architecture;
            ParameterCountBillions = Math.Clamp(
                SimUnits.Finite(parameterCountBillions, MinimumParameterBillions),
                MinimumParameterBillions,
                MaximumParameterBillions);
            TrainingTokensBillions = Math.Clamp(
                SimUnits.Finite(trainingTokensBillions, MinimumTokenBillions),
                MinimumTokenBillions,
                MaximumTokenBillions);
            DataSources = dataSources;
        }

        public string Name { get; }
        public ArchitectureId Architecture { get; }

        /// <summary>What the model is for. Decides which audience it can reach at all.</summary>
        public ModelType Type { get; }

        /// <summary>
        /// The product line this model belongs to, or empty for a line of its own.
        ///
        /// A line is one product as far as buyers are concerned, so only its strongest live model
        /// reaches the market. That is what makes shipping a successor an upgrade rather than a second
        /// thing to choose between, and it is why a company cannot improve its standing by leaving ten
        /// old models on sale.
        /// </summary>
        public string Family { get; }

        public bool HasFamily => Family.Length > 0;
        public double ParameterCountBillions { get; }
        public double TrainingTokensBillions { get; }
        public DatasetSource DataSources { get; }

        /// <summary>What the numbers are kept in. Buys throughput and pays in unpredictability.</summary>
        public TrainingPrecision Precision { get; }

        /// <summary>Many thin layers or few fat ones. Capability against cost to serve.</summary>
        public ModelShape Shape { get; }

        /// <summary>How hard the corpus is scrubbed. Fewer tokens, worth more each.</summary>
        public DeduplicationPass Deduplication { get; }

        /// <summary>
        /// How far before the run the corpus stops, in months.
        ///
        /// Nought is everything up to today: dearest, messiest, and right about the present. Two
        /// years back is cheap, clean and wrong about the world, and the market scores that.
        /// </summary>
        public int CutoffMonthsBack { get; }

        public double ParameterCount => ParameterCountBillions * SimUnits.ParametersPerBillion;
        public double TrainingTokens => TrainingTokensBillions * SimUnits.TokensPerBillion;

        /// <summary>The ratio the whole game turns on. Around twenty is compute-optimal.</summary>
        public double TokensPerParameter =>
            ParameterCountBillions <= 0.0 ? 0.0 : TrainingTokensBillions / ParameterCountBillions;

        public ModelBlueprint WithName(string name) =>
            new(name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources, Type, Family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public ModelBlueprint WithParameters(double parameterCountBillions) =>
            new(Name, Architecture, parameterCountBillions, TrainingTokensBillions, DataSources, Type, Family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public ModelBlueprint WithTokens(double trainingTokensBillions) =>
            new(Name, Architecture, ParameterCountBillions, trainingTokensBillions, DataSources, Type, Family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public ModelBlueprint WithArchitecture(ArchitectureId architecture) =>
            new(Name, architecture, ParameterCountBillions, TrainingTokensBillions, DataSources, Type, Family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public ModelBlueprint WithType(ModelType type) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources, type, Family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public ModelBlueprint WithDataSources(DatasetSource dataSources) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, dataSources, Type, Family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public ModelBlueprint WithPrecision(TrainingPrecision precision) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources,
                Type, Family, precision, Shape, Deduplication, CutoffMonthsBack);

        public ModelBlueprint WithShape(ModelShape shape) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources,
                Type, Family, Precision, shape, Deduplication, CutoffMonthsBack);

        public ModelBlueprint WithDeduplication(DeduplicationPass pass) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources,
                Type, Family, Precision, Shape, pass, CutoffMonthsBack);

        public ModelBlueprint WithCutoff(int monthsBack) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources,
                Type, Family, Precision, Shape, Deduplication, monthsBack);

        public ModelBlueprint WithFamily(string family) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources, Type, family, Precision, Shape, Deduplication,
                CutoffMonthsBack);

        public override string ToString() =>
            $"{Name}: {ParameterCountBillions:N0}B params, {TrainingTokensBillions:N0}B tokens, {Architecture}";
    }
}
