using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// How far the company knows how to push each research direction.
    ///
    /// **The five direction sliders ran their whole length on day one**, which said that a two
    /// person company in January 2022 already knew how to build a well routed sparse mixture and
    /// simply chose not to. That is the same failure <see cref="ScaleCeiling"/> was built to fix on
    /// the parameter slider: the control the decision lives in was sitting outside the research
    /// tree, so money and taste were the only gates.
    ///
    /// Every direction now starts at <see cref="BaseFraction"/> and is opened by nodes. A company
    /// that has researched nothing can lean a direction slightly and no more, which is what a lab
    /// with no published work behind it can actually do.
    ///
    /// **The first rung of every ladder is a node that already existed**, so the tree did not have
    /// to double in size to make this real, and so a player who researched mixture of experts for
    /// its own sake finds that it also opened something. The deeper rungs are new.
    ///
    /// Same shape as `ScaleCeiling` on purpose: a fraction of the slider's travel, a ladder of
    /// nodes, and a `TryNextRung` so the interface can name what would lift the cap. A grey bar
    /// with no explanation on it reads as a bug.
    /// </summary>
    public static class ArchitectureCeiling
    {
        /// <summary>Bump when the rungs change.</summary>
        public const string CatalogVersion = "2026.08.23";

        /// <summary>
        /// What a company that has researched none of this can ask for in any one direction.
        ///
        /// A third rather than nothing, because a slider pinned at zero is not a control and the
        /// programme has to be able to lean somewhere on day one. At 0.35 the player can express a
        /// preference and cannot express a specialism, which is the right shape for a lab whose
        /// entire technical position is other people's papers.
        /// </summary>
        public const double BaseFraction = 0.35;

        /// <summary>
        /// The ladders. First rung is an existing node, the two above it are new.
        ///
        /// Read down a column and it is a real research programme: know the technique, then make it
        /// work at cluster scale, then make it work well.
        /// </summary>
        public static readonly IReadOnlyDictionary<ResearchDirection, (ResearchNodeId Node, double Fraction)[]>
            Ladders = new Dictionary<ResearchDirection, (ResearchNodeId, double)[]>
            {
                [ResearchDirection.Sparsity] = new[]
                {
                    (ResearchNodeId.MixtureOfExperts, 0.55),
                    (ResearchNodeId.LearnedRouting, 0.75),
                    (ResearchNodeId.ExpertParallelism, 1.00)
                },

                [ResearchDirection.Throughput] = new[]
                {
                    (ResearchNodeId.EfficientAttention, 0.55),
                    (ResearchNodeId.FusedKernels, 0.75),
                    (ResearchNodeId.OverlappedCollectives, 1.00)
                },

                [ResearchDirection.Quality] = new[]
                {
                    (ResearchNodeId.ScalingLaws, 0.55),
                    (ResearchNodeId.CurriculumTraining, 0.75),
                    (ResearchNodeId.SelfDistillation, 1.00)
                },

                [ResearchDirection.Serving] = new[]
                {
                    (ResearchNodeId.LowPrecisionTraining, 0.55),
                    (ResearchNodeId.QuantisedServing, 0.75),
                    (ResearchNodeId.SpeculativeDecoding, 1.00)
                },

                [ResearchDirection.Reasoning] = new[]
                {
                    (ResearchNodeId.ReasoningModels, 0.55),
                    (ResearchNodeId.ProcessSupervision, 0.75),
                    (ResearchNodeId.InferenceTimeSearch, 1.00)
                }
            };

        /// <summary>
        /// The highest fraction the company has opened in one direction.
        ///
        /// Takes a predicate rather than the company, because `Data/` holds no state and must not
        /// learn what a company is. The caller passes `state.HasResearch`.
        /// </summary>
        public static double FractionFor(ResearchDirection direction,
            Func<ResearchNodeId, bool> hasResearch)
        {
            if (hasResearch == null || !Ladders.TryGetValue(direction, out var ladder))
            {
                return BaseFraction;
            }

            var fraction = BaseFraction;

            foreach (var (node, rung) in ladder)
            {
                if (hasResearch(node) && rung > fraction)
                {
                    fraction = rung;
                }
            }

            return fraction;
        }

        /// <summary>
        /// The next rung not yet open, so the interface can name what would raise this cap.
        /// </summary>
        public static bool TryNextRung(ResearchDirection direction,
            Func<ResearchNodeId, bool> hasResearch, out ResearchNodeId node, out double fraction)
        {
            var current = FractionFor(direction, hasResearch);

            if (Ladders.TryGetValue(direction, out var ladder))
            {
                foreach (var (candidate, rung) in ladder)
                {
                    if (rung > current)
                    {
                        node = candidate;
                        fraction = rung;
                        return true;
                    }
                }
            }

            node = ResearchNodeId.None;
            fraction = current;
            return false;
        }

        /// <summary>
        /// Whether a whole blueprint sits inside what the company has opened.
        ///
        /// **The rule lives here and the simulation enforces it**, not the slider. A cap that only
        /// exists in the interface is a suggestion the moment a second way to start a programme
        /// exists, and this project has already shipped that mistake six times.
        /// </summary>
        public static bool IsWithinCeiling(Func<ResearchDirection, double> weightOf,
            Func<ResearchNodeId, bool> hasResearch, out ResearchDirection offending)
        {
            offending = ResearchDirection.Sparsity;

            if (weightOf == null)
            {
                return true;
            }

            foreach (var direction in Ladders.Keys)
            {
                // A hair of slack, because a slider that stops exactly on the cap reports a value
                // a float rounding away from it and would refuse a programme the player was shown
                // as legal.
                if (weightOf(direction) > FractionFor(direction, hasResearch) + 1e-4)
                {
                    offending = direction;
                    return false;
                }
            }

            return true;
        }
    }
}
