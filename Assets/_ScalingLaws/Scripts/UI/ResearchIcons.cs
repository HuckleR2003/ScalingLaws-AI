using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Icons for the research tree, loaded once and shared.
    ///
    /// Same contract as <see cref="SkillIcons"/>: a node with no file yet is not an error, it simply
    /// draws without a picture. The tree has twenty one nodes and the art arrives in batches, so a
    /// half filled folder has to be a normal state rather than a broken screen.
    /// </summary>
    public static class ResearchIcons
    {
        private const string ResourceFolder = "Research/";

        /// <summary>
        /// Node to file. Only the nodes that have art are listed; the rest fall through to nothing.
        /// Names match the list handed to the artist, so a new file drops in without a code change
        /// as long as it is named the same way.
        /// </summary>
        private static readonly Dictionary<ResearchNodeId, string> FileNames = new()
        {
            { ResearchNodeId.FineTuningAndPrompting, "research_finetuning" },
            { ResearchNodeId.HumanFeedback, "research_humanfeedback" },
            { ResearchNodeId.EfficientAttention, "research_efficientattention" },
            { ResearchNodeId.MultimodalGeneration, "research_multimodal" },
            { ResearchNodeId.CuratedCorpora, "research_curatedcorpora" },
            { ResearchNodeId.ConversationalModels, "research_conversational" },
            { ResearchNodeId.CodingModels, "research_coding" },
            { ResearchNodeId.AutomationModels, "research_automation" },
            { ResearchNodeId.AgenticWorkstation, "research_agentic" },
            { ResearchNodeId.ModelSeries, "research_series" },
            { ResearchNodeId.ScalingLaws, "research_scalinglaws" },
            { ResearchNodeId.MixtureOfExperts, "research_mixtureofexperts" },
            { ResearchNodeId.ContextWindowExpansion, "research_context" },
            { ResearchNodeId.LicensedArchives, "research_licensed" },
            { ResearchNodeId.AutonomousAgents, "research_autonomousagents" },
            { ResearchNodeId.SyntheticDataGeneration, "research_syntheticdata" },
            { ResearchNodeId.ReasoningModels, "research_reasoning" },
            { ResearchNodeId.LongContextMixtures, "research_longcontext" },
            { ResearchNodeId.DatacenterProgramme, "research_datacenter" },

            // The Model Improvement track. Named here before the files exist, so dropping the three
            // pictures into Resources/Research is the whole job: the loader already asks for them
            // and draws an empty badge until they arrive.
            { ResearchNodeId.ShardedOptimizerStates, "research_sharding" },
            { ResearchNodeId.PipelineParallelism, "research_pipeline" },
            { ResearchNodeId.UltraReadiness, "research_ultrareadiness" }
        };

        private static readonly Dictionary<ResearchNodeId, Texture2D> Loaded = new();

        /// <summary>The icon for a node, or null when its art has not been drawn yet.</summary>
        /// <summary>
        /// Whether the map claims a file for this node.
        ///
        /// Separate from <see cref="Get"/> returning null, because those are two different facts: a
        /// node nobody has drawn yet is fine, and a node the map names whose file is absent is a
        /// broken promise. Only the second one is worth failing a test over.
        /// </summary>
        public static bool HasArtFor(ResearchNodeId node) => FileNames.ContainsKey(node);

        public static Texture2D Get(ResearchNodeId node)
        {
            if (Loaded.TryGetValue(node, out var cached))
            {
                return cached;
            }

            var texture = FileNames.TryGetValue(node, out var file)
                ? Resources.Load<Texture2D>(ResourceFolder + file)
                : null;

            Loaded[node] = texture;
            return texture;
        }

        /// <summary>How many nodes have art, for the art list rather than for the game.</summary>
        public static int Drawn => FileNames.Count;
    }
}
