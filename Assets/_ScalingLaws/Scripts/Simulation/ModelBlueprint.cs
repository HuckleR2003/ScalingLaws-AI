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
        public const double MinimumParameterBillions = 0.05;
        public const double MaximumParameterBillions = 100_000.0;
        public const double MinimumTokenBillions = 1.0;
        public const double MaximumTokenBillions = 1_000_000.0;

        public ModelBlueprint(
            string name,
            ArchitectureId architecture,
            double parameterCountBillions,
            double trainingTokensBillions,
            DatasetSource dataSources)
        {
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
        public double ParameterCountBillions { get; }
        public double TrainingTokensBillions { get; }
        public DatasetSource DataSources { get; }

        public double ParameterCount => ParameterCountBillions * SimUnits.ParametersPerBillion;
        public double TrainingTokens => TrainingTokensBillions * SimUnits.TokensPerBillion;

        /// <summary>The ratio the whole game turns on. Around twenty is compute-optimal.</summary>
        public double TokensPerParameter =>
            ParameterCountBillions <= 0.0 ? 0.0 : TrainingTokensBillions / ParameterCountBillions;

        public ModelBlueprint WithName(string name) =>
            new(name, Architecture, ParameterCountBillions, TrainingTokensBillions, DataSources);

        public ModelBlueprint WithParameters(double parameterCountBillions) =>
            new(Name, Architecture, parameterCountBillions, TrainingTokensBillions, DataSources);

        public ModelBlueprint WithTokens(double trainingTokensBillions) =>
            new(Name, Architecture, ParameterCountBillions, trainingTokensBillions, DataSources);

        public ModelBlueprint WithArchitecture(ArchitectureId architecture) =>
            new(Name, architecture, ParameterCountBillions, TrainingTokensBillions, DataSources);

        public ModelBlueprint WithDataSources(DatasetSource dataSources) =>
            new(Name, Architecture, ParameterCountBillions, TrainingTokensBillions, dataSources);

        public override string ToString() =>
            $"{Name}: {ParameterCountBillions:N0}B params, {TrainingTokensBillions:N0}B tokens, {Architecture}";
    }
}
