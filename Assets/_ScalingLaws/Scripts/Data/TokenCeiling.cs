using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// How much of the training-token slider the company has opened.
    ///
    /// **The same shape as <see cref="ScaleCeiling"/>, and deliberately so.** Parameters are gated
    /// by what a lab can supervise; tokens are gated by what it can feed the thing. A company that
    /// has not solved its corpus problem cannot train on a corpus it does not have, and the slider
    /// saying otherwise was the game promising data nobody had collected.
    ///
    /// The rungs are the data nodes that already existed rather than four new ones. Each of them is
    /// about getting more usable text through the door, which is exactly what this measures, and a
    /// ladder built from nodes the player was already going to want is a ladder that costs them
    /// nothing extra to climb.
    ///
    /// Fractions of the slider's log travel, not billions, so the numbers keep meaning the same
    /// thing if the slider's ends ever move.
    /// </summary>
    public static class TokenCeiling
    {
        /// <summary>Bump when the rungs change. The save records nothing here; the tree does.</summary>
        public const string CatalogVersion = "2026.08.16";

        /// <summary>
        /// What a company that has researched none of this can reach.
        ///
        /// Half, which is what the author asked for. It is above the Chinchilla-optimal token count
        /// for every model a starting company can afford to train, so the cap bites on ambition
        /// rather than on the first run.
        /// </summary>
        public const double BaseFraction = 0.50;

        /// <summary>The rungs, in order, each with the node that opens it.</summary>
        public static readonly (ResearchNodeId Node, double Fraction)[] Ladder =
        {
            (ResearchNodeId.CuratedCorpora, 0.62),
            (ResearchNodeId.CorpusDeduplication, 0.74),
            (ResearchNodeId.ContinuousDataPipeline, 0.86),
            (ResearchNodeId.SyntheticDataGeneration, 1.00)
        };

        /// <summary>
        /// The highest fraction the company has opened.
        ///
        /// Takes a predicate rather than the company, because Data holds no state and must not
        /// learn what a company is. The caller passes <c>state.HasResearch</c>.
        /// </summary>
        public static double FractionFor(Func<ResearchNodeId, bool> hasResearch)
        {
            if (hasResearch == null)
            {
                return BaseFraction;
            }

            var fraction = BaseFraction;

            foreach (var (node, rung) in Ladder)
            {
                if (hasResearch(node) && rung > fraction)
                {
                    fraction = rung;
                }
            }

            return Math.Clamp(fraction, BaseFraction, 1.0);
        }

        /// <summary>
        /// The next rung the company has not bought, so the lock can name what would lift it.
        ///
        /// A refusal that does not say what would lift it is a dead end, which is the rule the
        /// parameter ceiling already follows.
        /// </summary>
        public static bool TryNextRung(Func<ResearchNodeId, bool> hasResearch,
            out ResearchNodeId node, out double fraction)
        {
            node = ResearchNodeId.None;
            fraction = BaseFraction;

            var opened = FractionFor(hasResearch);

            foreach (var (candidate, rung) in Ladder)
            {
                if (rung > opened)
                {
                    node = candidate;
                    fraction = rung;
                    return true;
                }
            }

            return false;
        }
    }
}
