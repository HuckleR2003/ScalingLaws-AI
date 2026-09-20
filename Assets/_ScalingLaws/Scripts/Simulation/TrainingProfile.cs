using System;
using System.Collections.Generic;
using System.Globalization;
using ScalingLaws.Core;
using ScalingLaws.Data;

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
            double servingBurden, bool isEstimated, IReadOnlyList<string> notes)
        {
            ServingBurden = Math.Max(0.0, SimUnits.Finite(servingBurden, 1.0));
            IsEstimated = isEstimated;
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

        /// <summary>
        /// Whether there is an optimum to compare against at all.
        ///
        /// A run the planner could not cost, because the fleet has no usable compute, carries an
        /// optimal ratio of zero. Dividing by it produced a ratio of zero, which drove the marker to
        /// the far left of the belt and printed OVERSIZED next to "optimum 0.0". The screen was
        /// stating a confident falsehood about a shape it had not been able to evaluate.
        /// </summary>
        public bool IsEstimated { get; }

        /// <summary>
        /// What this model will cost to serve relative to a twenty billion parameter one, once it is
        /// live. Shown on the Scale stage so the bill is visible before the run is paid for rather
        /// than months later.
        /// </summary>
        public double ServingBurden { get; }

        public string ProfileName => !IsEstimated ? "NO ESTIMATE" : Profile switch
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
            var estimated = projection.OptimalTokensPerParameter > 0.0;

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

            // The size the audience will actually feel, which is the active parameters rather than the
            // headline count. A sparse model is cheap to serve for its size and that is the whole
            // reason to build one.
            // A readout must never throw while the screen is being drawn, and an unknown architecture
            // is reachable through a blocked or default projection. Falling back to a fully dense model
            // is the least flattering assumption that is still defensible, which is the same rule the
            // save migrations follow when they have to invent a value.
            var fraction = projection.Blueprint.Architecture == ArchitectureId.None
                ? 1.0
                : ArchitectureCatalog.Get(projection.Blueprint.Architecture).ActiveParameterFraction;

            var active = projection.Blueprint.ParameterCountBillions * 1e9 * fraction;

            var serving = MarketShareModel.SizeBurden(active);

            return new TrainingProfile(
                profile,
                ratio,

                // Dead centre when there is nothing to compare against, so the marker sits neutral
                // rather than pinned to a zone it was never measured into.
                estimated ? PositionOnBelt(ratio) : 0.5,
                projection.ShapeEfficiency,
                budget,
                memory,
                serving,
                estimated,
                BuildNotes(projection, profile, memory, serving, estimated));
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

        /// <summary>Whole gigabytes, in the one culture every figure in this game is written in.</summary>
        private static string UiWhole(double value) =>
            Math.Round(Math.Max(0.0, SimUnits.Finite(value))).ToString("N0", CultureInfo.InvariantCulture);

        private static List<string> BuildNotes(TrainingProjection projection, ShapeProfile profile,
            double memory, double serving, bool estimated)
        {
            var notes = new List<string>(4);

            if (!estimated)
            {
                notes.Add(Loc.T("profile.note.no_compute"));
            }

            // Memory first. Everything else is advice; this one stops the run.
            if (memory > 1.0)
            {
                notes.Add(Loc.T("profile.note.memory_short",
                    UiWhole(projection.MemoryRequiredGigabytes),
                    UiWhole(projection.MemoryAvailableGigabytes)));
            }
            else if (memory > 0.85)
            {
                notes.Add(Loc.T("profile.note.memory_tight"));
            }

            if (!estimated)
            {
                return notes;
            }

            // Written out rather than built from the enum's name, because a phrase-book key made by
            // concatenation is invisible to the guard that checks every key exists.
            switch (profile)
            {
                case ShapeProfile.Oversized:
                    notes.Add(Loc.T("profile.note.oversized"));
                    break;
                case ShapeProfile.ComputeHungry:
                    notes.Add(Loc.T("profile.note.hungry"));
                    break;
                case ShapeProfile.Balanced:
                    notes.Add(Loc.T("profile.note.balanced"));
                    break;
                case ShapeProfile.DataRich:
                    notes.Add(Loc.T("profile.note.datarich"));
                    break;
                case ShapeProfile.Lean:
                    notes.Add(Loc.T("profile.note.lean"));
                    break;
            }

            if (serving > 1.6)
            {
                notes.Add(Loc.T("profile.note.dear_to_serve",
                    serving.ToString("0.0", CultureInfo.InvariantCulture)));
            }
            else if (serving < 0.7)
            {
                notes.Add(Loc.T("profile.note.cheap_to_serve"));
            }

            if (projection.DataAcquisitionCostUsd > projection.ComputeCashCostUsd / 2)
            {
                notes.Add(Loc.T("profile.note.data_heavy"));
            }

            if (projection.TrainingDays > 240)
            {
                notes.Add(Loc.T("profile.note.long_run", projection.TrainingDays.ToString()));
            }

            return notes;
        }
    }
}
