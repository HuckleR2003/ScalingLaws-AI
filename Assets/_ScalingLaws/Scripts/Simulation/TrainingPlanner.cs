using System;
using System.Text;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The ONE place a blueprint becomes numbers. Turns the four design decisions plus the fleet plus
    /// the calendar into a projected capability, a duration and a bill.
    ///
    /// It checks calendar availability of an architecture but not whether the company has paid to
    /// adopt it. That gate belongs to <see cref="CompanySimulation"/>, which is the only thing that
    /// knows what the company owns.
    /// </summary>
    public static class TrainingPlanner
    {
        /// <summary>
        /// Bytes of accelerator memory per parameter during training: weights, gradients and the two
        /// optimizer moments in mixed precision, with room for activations.
        /// </summary>
        public const double TrainingBytesPerParameter = 18.0;

        /// <summary>Share of on-package memory a run can actually use. The rest is fragmentation and buffers.</summary>
        public const double UsableMemoryFraction = 0.85;

        /// <summary>No run may be longer than this. A run nobody lives to see is not a plan.</summary>
        public const int MaximumTrainingDays = 900;

        /// <summary>
        /// Projects a blueprint against a fleet on a date.
        /// <paramref name="trainingComputeShare"/> is the slice of the fleet that will actually be
        /// pointed at the run: project at the share the company will really use, or the estimated
        /// duration is a number the run can never hit.
        /// </summary>
        public static TrainingProjection Project(
            ModelBlueprint blueprint,
            ComputeProfile profile,
            MarketConditions market,
            double bestOwnedCapability,
            double trainingComputeShare = 1.0,
            IArchitectureSource architectures = null,
            double dataSupplyMultiplier = 1.0)
        {
            var source = architectures ?? ArchitectureCatalog.AsSource;
            if (!source.TryGetArchitecture(blueprint.Architecture, out var architecture))
            {
                return TrainingProjection.Blocked(blueprint, "Unknown architecture.");
            }

            var blocking = new StringBuilder();

            if (!architecture.IsAvailableOn(market.Date))
            {
                Append(blocking, $"{architecture.DisplayName} is not published until {architecture.AvailableFrom}");
            }

            var blend = DatasetCatalog.Blend(
                blueprint.DataSources,
                blueprint.TrainingTokensBillions,
                market.Date,
                bestOwnedCapability,
                dataSupplyMultiplier);

            if (blend.SourceCount == 0)
            {
                Append(blocking, "no usable data sources selected");
            }
            else if (!blend.IsSufficient)
            {
                Append(blocking, $"data mix supplies {blend.AvailableTokensBillions:N0}B of the {blueprint.TrainingTokensBillions:N0}B tokens requested");
            }

            // The run trains on the tokens that exist, not the tokens that were asked for.
            var actualTokensBillions = Math.Max(
                ModelBlueprint.MinimumTokenBillions,
                Math.Min(blueprint.TrainingTokensBillions, blend.AvailableTokensBillions));
            var actualTokens = actualTokensBillions * SimUnits.TokensPerBillion;
            var parameters = blueprint.ParameterCount;

            var memoryRequired = parameters * TrainingBytesPerParameter / 1e9;
            var memoryAvailable = profile.TotalAcceleratorMemoryGigabytes * UsableMemoryFraction;
            if (memoryRequired > memoryAvailable)
            {
                Append(blocking, $"needs {memoryRequired:N0} GB of accelerator memory, fleet offers {memoryAvailable:N0} GB");
            }

            // Better recipes make the same FLOPs go further. Split evenly across parameters and
            // tokens so the multiplier lands on the compute budget without distorting the shape the
            // player chose.
            var recipeBoost = Math.Sqrt(market.AlgorithmicEfficiency);
            var effectiveParameters = parameters * architecture.ParameterEfficiency * recipeBoost;
            var effectiveTokens = actualTokens * blend.QualityMultiplier * recipeBoost;

            var loss = ScalingLaw.Loss(effectiveParameters, effectiveTokens);
            var capability = Math.Clamp(
                ScalingLaw.CapabilityFromLoss(loss) + architecture.CapabilityBonus,
                0.0,
                100.0);

            var petaflopDays = ScalingLaw.TrainingPetaflopDays(
                parameters,
                actualTokens,
                architecture.ActiveParameterFraction);

            var share = Math.Clamp(SimUnits.Finite(trainingComputeShare, 1.0), 0.0, 1.0);
            var throughput = profile.EffectivePetaflops * architecture.TrainingEfficiency * share;
            if (throughput <= 0.0)
            {
                Append(blocking, "the fleet has no usable compute");
                return new TrainingProjection(
                    blueprint,
                    false,
                    blocking.ToString(),
                    loss,
                    capability,
                    0.0,
                    blueprint.TokensPerParameter,
                    0.0,
                    petaflopDays,
                    0.0,
                    0,
                    0,
                    0,
                    blend.AcquisitionCostUsd,
                    memoryRequired,
                    memoryAvailable,
                    blend);
            }

            var trainingDays = SimUnits.WholeDays(petaflopDays / throughput);
            if (trainingDays > MaximumTrainingDays)
            {
                Append(blocking, $"the run would take {trainingDays} days, over the {MaximumTrainingDays} day limit");
            }

            // The fleet bills for every day the run occupies the calendar, whichever slice of it the
            // run is using. Idle capacity is not a discount.
            var cashCost = SimUnits.ToDollars(profile.DailyOperatingCostUsd * trainingDays);
            var economicCost = SimUnits.ToDollars(
                (profile.DailyOperatingCostUsd + profile.DailyDepreciationUsd) * trainingDays);

            var trainingFlop = ScalingLaw.TrainingFlop(parameters, actualTokens, architecture.ActiveParameterFraction);
            var optimalRatio = ScalingLaw.OptimalTokensPerParameter(trainingFlop, architecture.ActiveParameterFraction);
            var shapeEfficiency = ScalingLaw.ShapeEfficiency(
                effectiveParameters,
                effectiveTokens,
                architecture.ActiveParameterFraction);

            var actualRatio = blueprint.ParameterCountBillions <= 0.0
                ? 0.0
                : actualTokensBillions / blueprint.ParameterCountBillions;

            return new TrainingProjection(
                blueprint,
                blocking.Length == 0,
                blocking.ToString(),
                loss,
                capability,
                shapeEfficiency,
                actualRatio,
                optimalRatio,
                petaflopDays,
                throughput,
                trainingDays,
                cashCost,
                economicCost,
                blend.AcquisitionCostUsd,
                memoryRequired,
                memoryAvailable,
                blend);
        }

        /// <summary>
        /// The compute-optimal blueprint for a budget, as a starting point the player can then ruin
        /// on purpose. Handy for tests and for a "suggest a shape" button later.
        /// </summary>
        public static ModelBlueprint OptimalBlueprintForBudget(
            string name,
            ArchitectureId architectureId,
            double petaflopDayBudget,
            DatasetSource dataSources)
        {
            var architecture = ArchitectureCatalog.Get(architectureId);
            var flop = SimUnits.PetaflopDaysToFlop(Math.Max(1.0, petaflopDayBudget));
            var parameters = ScalingLaw.OptimalParameters(flop, architecture.ActiveParameterFraction);
            var tokens = ScalingLaw.OptimalTokens(flop, architecture.ActiveParameterFraction);

            return new ModelBlueprint(
                name,
                architectureId,
                parameters / SimUnits.ParametersPerBillion,
                tokens / SimUnits.TokensPerBillion,
                dataSources);
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
