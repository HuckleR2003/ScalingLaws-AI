using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// How large a model the company knows how to train.
    ///
    /// **The parameter slider used to run its whole length from day one**, which quietly said that a
    /// two person company in January 2022 could schedule a trillion parameter run if it could pay
    /// the bill. Money was the only gate, and money compounds. That is the same failure the research
    /// tree was built to fix, applied to the one control the player touches most.
    ///
    /// So the slider is capped, and the cap is raised by research. The techniques are real and they
    /// are the actual reason a lab can train something bigger than a single node holds: sharding the
    /// optimizer state across the cluster, then splitting the model itself across it, then keeping
    /// the whole thing running for months without a failure taking the run down with it.
    ///
    /// **Expressed as a fraction of the slider's travel, not of the parameter count**, because the
    /// slider is logarithmic and the player is looking at the slider. The range is five decades, so
    /// every tenth of the slider is half a decade of scale.
    ///
    /// **The base is half the slider and not the two fifths first tried**, and that was decided by
    /// a failing balance suite rather than by taste. Two fifths caps the opening at ten billion
    /// parameters, which is *below* the twenty billion the creator defaults to and that
    /// `MarketShareModel.SizeBurden` measures everything against. The scripted campaign could not
    /// start its first run, never earned, never researched its way out, and was insolvent by August
    /// 2024 with nothing shipped. A ceiling under the game's own reference model is not a tight
    /// gate, it is a wall.
    ///
    /// Two fifths was tried a second time after the scripted operator was taught to build down to
    /// the cap the way a player does, and the campaign did then survive it. It was still rejected,
    /// because sixteen tests of unrelated mechanics build twenty billion parameter models and every
    /// one of them was refused. Those fixtures are not wrong: twenty billion is the size
    /// `MarketShareModel.SizeBurden` scores as exactly 1.0, so at two fifths the neutral reference
    /// model of the whole economy is unbuildable on day one. That is an inconsistency rather than a
    /// difficulty setting, and the alternative fix, moving the economy's reference down to ten
    /// billion, would retune the serving burden of every model in the game.
    /// </summary>
    public static class ScaleCeiling
    {
        /// <summary>Bump when the rungs change. The save records nothing here; the tree does.</summary>
        public const string CatalogVersion = "2026.08.15";

        /// <summary>What a company that has researched none of this can reach.</summary>
        public const double BaseFraction = 0.50;   // 31.6B, comfortably over the 20B reference

        /// <summary>
        /// The rungs, in order, each with the node that opens it.
        ///
        /// The last one is deliberately an era four node that already existed rather than a fourth
        /// new one. A slider with a permanently dead top ten percent reads as a bug, and recursive
        /// self improvement is exactly the thing that would let a lab run something it could not
        /// previously supervise.
        /// </summary>
        public static readonly (ResearchNodeId Node, double Fraction)[] Ladder =
        {
            (ResearchNodeId.ShardedOptimizerStates, 0.65),      //    178B
            (ResearchNodeId.PipelineParallelism, 0.80),         //  1,000B
            (ResearchNodeId.UltraReadiness, 0.90),              //  3,162B
            (ResearchNodeId.RecursiveSelfImprovement, 1.00)     // 10,000B
        };

        /// <summary>
        /// The highest fraction the company has opened.
        ///
        /// Takes a predicate rather than the company, because `Data/` holds no state and must not
        /// learn what a company is. The caller passes `state.HasResearch`.
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

            return fraction;
        }

        /// <summary>
        /// The next rung that is not open yet, so the interface can name what would raise the cap
        /// rather than drawing a grey bar with no explanation on it.
        /// </summary>
        public static bool TryNextRung(Func<ResearchNodeId, bool> hasResearch, out ResearchNodeId node,
            out double fraction)
        {
            var current = FractionFor(hasResearch);

            foreach (var (candidate, rung) in Ladder)
            {
                if (rung > current)
                {
                    node = candidate;
                    fraction = rung;
                    return true;
                }
            }

            node = ResearchNodeId.None;
            fraction = current;
            return false;
        }

        /// <summary>
        /// The cap in billions of parameters, given the slider's own log bounds.
        ///
        /// The bounds live in the creator and are passed in rather than duplicated here. A second
        /// copy of "the slider runs from 0.1B to 10,000B" is a second copy that goes stale.
        /// </summary>
        public static double CeilingBillions(double fraction, double lowLog, double highLog)
        {
            var clamped = Math.Clamp(fraction, 0.0, 1.0);
            return Math.Pow(10.0, lowLog + (highLog - lowLog) * clamped);
        }
    }
}
