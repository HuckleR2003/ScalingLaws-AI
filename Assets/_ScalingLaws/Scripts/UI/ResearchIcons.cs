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

        private static readonly Dictionary<string, Texture2D> ByFile = new();

        /// <summary>
        /// Node to file. Only the nodes that have art are listed; the rest fall through to nothing.
        /// Names match the list handed to the artist, so a new file drops in without a code change
        /// as long as it is named the same way.
        /// </summary>
        private static readonly Dictionary<ResearchNodeId, string> FileNames = new()
        {
            { ResearchNodeId.FineTuningAndPrompting, "research_finetuning" },

            // The eight that had no icon named at all, so the tree drew an empty badge for
            // each of them. Six of the files existed in Art and had never reached
            // Resources, where alone they can be loaded from.
            { ResearchNodeId.SinglePrecisionTraining, "research_fp32" },
            { ResearchNodeId.MixedPrecisionTraining, "research_bf16" },
            { ResearchNodeId.LowPrecisionTraining, "research_int8" },
            { ResearchNodeId.CorpusDeduplication, "research_dedup" },
            { ResearchNodeId.ContinuousDataPipeline, "research_pipeline_data" },
            { ResearchNodeId.HybridArchitectures, "research_hybrid" },
            { ResearchNodeId.RecursiveSelfImprovement, "research_recursive" },
            { ResearchNodeId.ArtificialSuperintelligence, "research_asi" },
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
            { ResearchNodeId.UltraReadiness, "research_ultrareadiness" },

            // The safety track. These are the author's own icons and the same files are used for
            // the tier tiles in the creator's SAFETY stage, so a node and the thing it unlocks are
            // never two different pictures.
            { ResearchNodeId.LicensedStackedAssa, "research_assa1_licensed" },
            { ResearchNodeId.AdvancedAssa, "research_assa2_advanced" },
            { ResearchNodeId.AssaEcosystem, "research_assa3_ecosystem" },

            { ResearchNodeId.AutomatedRedTeaming, "research_red1_automated_teaming" },
            { ResearchNodeId.AdversarialCampaigns, "research_red2_adversarial_campaigns" },
            { ResearchNodeId.ContinuousRedTeam, "research_red3_redteam" },

            { ResearchNodeId.BasicDataIsolation, "research_data0_basic_isolation" },
            { ResearchNodeId.EncryptedDataVaults, "research_data1_encrypted_data" },
            { ResearchNodeId.DifferentialPrivacy, "research_data2_differential_privacy" },
            { ResearchNodeId.PrivacyPreservingTraining, "research_data3_privacy_training" },

            // The architecture direction ladders. Drawn to match the twenty two above: a thin ring,
            // dark ink line work, one idea each rather than a diagram.
            { ResearchNodeId.LearnedRouting, "research_learnedrouting" },
            { ResearchNodeId.ExpertParallelism, "research_expertparallelism" },
            { ResearchNodeId.FusedKernels, "research_fusedkernels" },
            { ResearchNodeId.OverlappedCollectives, "research_overlappedcollectives" },
            { ResearchNodeId.CurriculumTraining, "research_curriculumtraining" },
            { ResearchNodeId.SelfDistillation, "research_selfdistillation" },
            { ResearchNodeId.QuantisedServing, "research_quantisedserving" },
            { ResearchNodeId.SpeculativeDecoding, "research_speculativedecoding" },
            { ResearchNodeId.ProcessSupervision, "research_processsupervision" },
            { ResearchNodeId.InferenceTimeSearch, "research_inferencetimesearch" },

            // The last nine. Five are era four and five, where the tree is at its most abstract and
            // an empty disc reads as a node that is not finished rather than one nobody drew; four
            // are the Operations track, which is the newest and had never had art at all.
            //
            // They arrived in `Art/Research` and were copied to `Resources/Research`, because that
            // is the only folder `Resources.Load` can see. The note above this table is about the
            // same mistake made once before, with six files.
            { ResearchNodeId.GeneralIntelligence, "research_agi" },
            { ResearchNodeId.RealTimeAssimilation, "research_assimilation" },
            { ResearchNodeId.SovereignLiaison, "research_sovereign" },
            { ResearchNodeId.ContinuousOversight, "research_oversight" },
            { ResearchNodeId.RedundantInference, "research_redundant" },
            { ResearchNodeId.RackTelemetry, "research_telemetry" },
            { ResearchNodeId.AirflowModelling, "research_airflow" },
            { ResearchNodeId.LiquidLoops, "research_liquidloops" },
            { ResearchNodeId.OwnSubstation, "research_substation" },

            // **The premises nodes wear the photograph of the place they open**, which is what the
            // author asked for and is also the only honest picture: a lease has no icon, and the
            // question the node answers is "which room". A value carrying a folder is loaded from
            // that folder instead of from Research/, so these are the chooser's own pictures
            // rather than a second copy of them.
            { ResearchNodeId.LeaseASmallHub, "Offices/office_smallhub" },
            { ResearchNodeId.LeaseABigHub, "Offices/office_bighub" }
        };

        /// <summary>
        /// Where a file lives. Bare names are in Research/; anything with a folder in it is a path
        /// already, which is how the premises nodes borrow the office photographs.
        /// </summary>
        private static string PathTo(string file) =>
            file.Contains('/') ? file : ResourceFolder + file;

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

        /// <summary>
        /// The icon for a file name rather than for a node.
        ///
        /// The SAFETY tiles need this: tier zero of self auditing and of red teaming has no research
        /// node at all, because the company starts knowing them, and it still has a picture.
        /// </summary>
        public static Texture2D ByName(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return null;
            }

            if (ByFile.TryGetValue(file, out var cached))
            {
                return cached;
            }

            var loaded = Resources.Load<Texture2D>(PathTo(file));
            ByFile[file] = loaded;
            return loaded;
        }

        public static Texture2D Get(ResearchNodeId node)
        {
            if (Loaded.TryGetValue(node, out var cached))
            {
                return cached;
            }

            var texture = FileNames.TryGetValue(node, out var file)
                ? Resources.Load<Texture2D>(PathTo(file))
                : null;

            Loaded[node] = texture;
            return texture;
        }

        /// <summary>How many nodes have art, for the art list rather than for the game.</summary>
        public static int Drawn => FileNames.Count;
    }
}
