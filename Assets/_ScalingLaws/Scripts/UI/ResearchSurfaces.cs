using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>Where on the research screen a node is drawn.</summary>
    public enum ResearchSurface
    {
        /// <summary>The big board in an era.</summary>
        EraCapability,

        /// <summary>The MODEL IMPROVEMENT band under an era.</summary>
        EraDeepening,

        /// <summary>The OPERATIONS band under an era.</summary>
        EraOperations,

        /// <summary>
        /// The SAFETY band under an era, for safety research that is not a level of a creator module.
        /// Era five's oversight and redundancy nodes are the ones on it today.
        /// </summary>
        EraSafety,

        /// <summary>The PREMISES map under the funding panel.</summary>
        PremisesPanel,

        /// <summary>The MODEL SAFETY tiles under the premises map.</summary>
        SafetyPanel
    }

    /// <summary>
    /// Which surface a node is drawn on. The research screen asks this and nothing else.
    ///
    /// **Francisco found the fault this exists to stop.** Every safety node went into the era board
    /// through a `default:` arm, and the era board lays out capability nodes only, so all twelve were
    /// handed to a board that quietly left them out. The creator's SAFETY stage said "needs research"
    /// over three modules, and two statecraft nodes the state programme reads were unreachable too.
    ///
    /// A written-out switch with a throwing arm, so the next track added fails
    /// `ResearchSurfacesTests` instead of vanishing the same way.
    /// </summary>
    public static class ResearchSurfaces
    {
        /// <summary>Every node that is a level of a creator safety module, read from the catalog.</summary>
        private static readonly HashSet<ResearchNodeId> SafetyLevels = SafetyModuleCatalog.All
            .Select(tier => tier.Requires)
            .Where(id => id != ResearchNodeId.None)
            .ToHashSet();

        public static ResearchSurface Of(ResearchNode node) =>
            Of(node.Track, SafetyLevels.Contains(node.Id));

        /// <summary>
        /// The surface for a track. A safety node is on the tiles when it is a module's level and on
        /// its era's safety band otherwise, which is the one decision a track alone cannot make.
        /// </summary>
        public static ResearchSurface Of(ResearchTrack track, bool isSafetyLevel = false) => track switch
        {
            ResearchTrack.Capability => ResearchSurface.EraCapability,
            ResearchTrack.ModelImprovement => ResearchSurface.EraDeepening,
            ResearchTrack.Operations => ResearchSurface.EraOperations,
            ResearchTrack.Premises => ResearchSurface.PremisesPanel,
            ResearchTrack.Safety => isSafetyLevel ? ResearchSurface.SafetyPanel : ResearchSurface.EraSafety,
            _ => throw new ArgumentOutOfRangeException(nameof(track), track,
                "A research track with nowhere on the screen to be drawn.")
        };
    }
}
