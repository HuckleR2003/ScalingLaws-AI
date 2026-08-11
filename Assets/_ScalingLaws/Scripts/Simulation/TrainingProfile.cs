using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>What kind of run this is, in one word.</summary>
    public enum ShapeProfile
    {
        /// <summary>Far more model than the data can train. The worst place to be.</summary>
        Oversized = 0,

        /// <summary>Short of tokens. More compute on the same shape would still pay.</summary>
        ComputeHungry = 1,

        /// <summary>Inside the efficient band.</summary>
        Balanced = 2,

        /// <summary>Past the band on the data side, still gaining but slowly.</summary>
        DataRich = 3,

        /// <summary>A small model trained hard. Cheap to serve, capped on capability.</summary>
        Lean = 4
    }

    /// <summary>
    /// The Scale stage's readouts, derived rather than stored.
    ///
    /// This lives in Simulation and not in the panel because a readout the player makes decisions
    /// from is part of the rules, not decoration, and because a derivation with no UnityEngine in it
    /// can be tested in milliseconds. The panel draws what this says and computes nothing itself.
    /// </summary>
    public readonly struct TrainingProfile
    {
        private TrainingProfile(ShapeProfile profile, double ratio, double bandPosition,
            double trainingEfficiency, double budgetEfficiency, double memoryPressure,
            IReadOnlyList<string> notes)
        {
            Profile = profile;
            Ratio = Math.Max(0.0, SimUnits.Finite(ratio));
            BandPosition = Math.Clamp(SimUnits.Finite(bandPosition), 0.0, 1.0);
            TrainingEfficiency = Math.Clamp(SimUnits.Finite(trainingEfficiency), 0.0, 1.0);
            BudgetEfficiency = Math.Clamp(SimUnits.Finite(budgetEfficiency), 0.0, 1.0);
            MemoryPressure = Math.Max(0.0, SimUnits.Finite(memoryPressure));
            Notes = notes ?? Array.Empty<string>();
        }

        public ShapeProfile Profile { get; }

        /// <summary>Tokens per parameter as a multiple of optimal. One is compute optimal.</summary>
        public double Ratio { get; }

        /// <summary>
        /// Where the marker sits on the belt, nothing to everything. Logarithmic, because the ratio
        /// is a multiple: half optimal and twice optimal are the same distance from the middle and a
        /// linear scale would squash the whole undertrained half into the first few pixels.
        /// </summary>
        public double BandPosition { get; }

        /// <summary>How much of the compute the shape converts into capability.</summary>
        public double TrainingEfficiency { get; }

        /// <summary>
        /// How much of the money is buying capability.
        ///
        /// Distinct from the above, and the difference is the point: a perfectly shaped run that also
        /// bought a corpus it did not need converts its compute beautifully and still wasted cash.
        /// </summary>
        public double BudgetEfficiency { get; }

        /// <summary>Memory the run needs over what it has. Above one does not fit.</summary>
        public double MemoryPressure { get; }

        /// <summary>Short sentences about this specific run. Empty when there is nothing to say.</summary>
        public IReadOnlyList<string> Notes { get; }

        public bool Fits => MemoryPressure <= 1.0;

        public string ProfileName => Profile switch
        {
            ShapeProfile.Oversized => "OVERSIZED",
            ShapeProfile.ComputeHungry => "COMPUTE-HUNGRY",
            ShapeProfile.Balanced => "BALANCED",
            ShapeProfile.DataRich => "DATA-RICH",
            _ => "LEAN"
        };

        /// <summary>
        /// Reads a projection. The band edges come from <see cref="TrainingProjection"/> so the words
        /// here and the zones drawn on the belt can never describe different bands.
        /// </summary>
        public static TrainingProfile Read(TrainingProjection projection)
        {
            var ratio = projection.ShapeRatio;

            var profile = ratio switch
            {
                <= 0.0 => ShapeProfile.Oversized,
                < 0.3 => ShapeProfile.Oversized,
                var r when r < TrainingProjection.UndertrainedBelow => ShapeProfile.ComputeHungry,
                var r when r <= TrainingProjection.OvertrainedAbove => ShapeProfile.Balanced,
                < 3.5 => ShapeProfile.DataRich,
                _ => ShapeProfile.Lean
            };

            var memory = projection.MemoryAvailableGigabytes <= 0.0
                ? 0.0
                : projection.MemoryRequiredGigabytes / projection.MemoryAvailableGigabytes;

            // Compute that converts, over every dollar the run actually costs. Data money is in the
            // denominator and not in the numerator, which is what makes an unnecessary corpus show up
            // here rather than nowhere.
            var spend = (double)projection.ComputeCashCostUsd + projection.DataAcquisitionCostUsd;
            var budget = spend <= 0.0
                ? 0.0
                : projection.ShapeEfficiency * projection.ComputeCashCostUsd / spend;

            return new TrainingProfile(
                profile,
                ratio,
                PositionOnBelt(ratio),
                projection.ShapeEfficiency,
                budget,
                memory,
                BuildNotes(projection, profile, memory));
        }

        /// <summary>
        /// Maps the ratio onto the belt on a log scale, with optimal dead centre. The visible range is
        /// an eighth of optimal to eight times it, which covers everything the sliders can reach.
        /// </summary>
        public static double PositionOnBelt(double ratio)
        {
            // Finite first. Math.Clamp passes NaN straight through, so clamping at the end is not a
            // guard against it, and a NaN width sends the marker off the element entirely.
            var safe = SimUnits.Finite(ratio);
            if (safe <= 0.0)
            {
                return 0.0;
            }

            const double span = 3.0; // log base two of the eightfold range on each side
            var position = 0.5 + Math.Log(safe, 2.0) / (2.0 * span);
            return Math.Clamp(SimUnits.Finite(position, 0.5), 0.0, 1.0);
        }

        /// <summary>Where the efficient band sits on the belt, as a pair of positions.</summary>
        public static (double From, double To) BandOnBelt() => (
            PositionOnBelt(TrainingProjection.UndertrainedBelow),
            PositionOnBelt(TrainingProjection.OvertrainedAbove));

        private static List<string> BuildNotes(TrainingProjection projection, ShapeProfile profile,
            double memory)
        {
            var notes = new List<string>(4);

            // Memory first. Everything else is advice; this one stops the run.
            if (memory > 1.0)
            {
                notes.Add($"This run needs {projection.MemoryRequiredGigabytes:N0} GB and you have "
                    + $"{projection.MemoryAvailableGigabytes:N0} GB. It will not start.");
            }
            else if (memory > 0.85)
            {
                notes.Add("Memory is nearly full. A slightly larger model will not fit at all.");
            }

            switch (profile)
            {
                case ShapeProfile.Oversized:
                    notes.Add("The model is far too large for the amount of data selected. "
                        + "Most of this compute is being spent on parameters that never get trained.");
                    break;
                case ShapeProfile.ComputeHungry:
                    notes.Add("Short of tokens for a model this size. More data, or a smaller model, "
                        + "converts the same bill into a better result.");
                    break;
                case ShapeProfile.Balanced:
                    notes.Add("You are inside the efficient scaling band.");
                    break;
                case ShapeProfile.DataRich:
                    notes.Add("Extra tokens beyond this point mostly buy diminishing returns.");
                    break;
                case ShapeProfile.Lean:
                    notes.Add("A small model trained hard. Cheap to serve for years, "
                        + "and it will never reach the frontier.");
                    break;
            }

            if (projection.DataAcquisitionCostUsd > projection.ComputeCashCostUsd / 2)
            {
                notes.Add("Data is costing more than half of what the compute costs.");
            }

            if (projection.TrainingDays > 240)
            {
                notes.Add($"{projection.TrainingDays} days on one run. The frontier moves while "
                    + "you wait.");
            }

            return notes;
        }
    }
}
