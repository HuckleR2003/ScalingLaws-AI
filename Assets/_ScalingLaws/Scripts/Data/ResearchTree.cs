using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Which stretch of the campaign a node belongs to.</summary>
    public enum ResearchEra
    {
        Foundations = 1,
        Scaling = 2,
        Autonomy = 3,
        Superintelligence = 4
    }

    /// <summary>
    /// Nodes of the technology tree. Explicit values, saved, never renumbered.
    /// </summary>
    public enum ResearchNodeId
    {
        None = 0,

        // Era 1, foundations.
        FineTuningAndPrompting = 101,
        HumanFeedback = 102,
        EfficientAttention = 103,
        MultimodalGeneration = 104,
        CuratedCorpora = 105,

        // Model types. A separate line of work from capability: these decide what the model is
        // for, not how good it is, and every one of them is a bet on a segment arriving.
        CodingModels = 111,
        ConversationalModels = 112,
        AutomationModels = 113,
        AgenticWorkstation = 114,
        ModelSeries = 115,

        // Era 2, the scaling race.
        ScalingLaws = 201,
        MixtureOfExperts = 202,
        ContextWindowExpansion = 203,
        LicensedArchives = 204,

        // Era 3, autonomy.
        AutonomousAgents = 301,
        SyntheticDataGeneration = 302,
        ReasoningModels = 303,
        LongContextMixtures = 304,
        DatacenterProgramme = 305,

        // Era 4, the end game.
        HybridArchitectures = 401,
        RecursiveSelfImprovement = 402,
        ArtificialSuperintelligence = 403
    }

    /// <summary>
    /// One node. A node is the only way anything in this game gets unlocked: architectures,
    /// corpora, model traits and the compute ladder all sit behind one.
    ///
    /// Before the tree existed, a company bought a sparse mixture for nine million dollars the day
    /// it wanted one. That made the mid game trivial: money was the only gate and money compounds.
    /// A node costs money, calendar and a prerequisite chain, and the calendar is the part that
    /// cannot be bought out of.
    /// </summary>
    public readonly struct ResearchNode
    {
        private readonly ResearchNodeId[] prerequisites;

        public ResearchNode(
            ResearchNodeId id,
            ResearchEra era,
            string displayName,
            string description,
            GameDate earliestDate,
            long costUsd,
            int durationDays,
            double petaflopDaysRequired,
            ResearchNodeId[] requires = null,
            ArchitectureId unlocksArchitecture = ArchitectureId.None,
            DatasetSource unlocksData = DatasetSource.None,
            ComputeTier unlocksTier = ComputeTier.None,
            ModelTrait unlocksTrait = ModelTrait.Reasoning,
            bool gatesTrait = false,
            string warning = null)
        {
            Id = id;
            Era = era;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
            Description = description ?? string.Empty;
            EarliestDate = earliestDate;
            CostUsd = Math.Clamp(costUsd, 0L, 500_000_000_000L);
            DurationDays = Math.Clamp(durationDays, 1, 1500);
            PetaflopDaysRequired = Math.Max(0.0, SimUnits.Finite(petaflopDaysRequired));
            prerequisites = requires ?? Array.Empty<ResearchNodeId>();
            UnlocksArchitecture = unlocksArchitecture;
            UnlocksData = unlocksData;
            UnlocksTier = unlocksTier;
            UnlocksTrait = unlocksTrait;
            GatesTrait = gatesTrait;
            Warning = warning ?? string.Empty;
        }

        public ResearchNodeId Id { get; }
        public ResearchEra Era { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public GameDate EarliestDate { get; }
        public long CostUsd { get; }
        public int DurationDays { get; }
        public double PetaflopDaysRequired { get; }

        public IReadOnlyList<ResearchNodeId> Prerequisites => prerequisites ?? Array.Empty<ResearchNodeId>();

        public ArchitectureId UnlocksArchitecture { get; }
        public DatasetSource UnlocksData { get; }
        public ComputeTier UnlocksTier { get; }
        public ModelTrait UnlocksTrait { get; }

        /// <summary>True when <see cref="UnlocksTrait"/> is meaningful. Enums have no null.</summary>
        public bool GatesTrait { get; }

        /// <summary>Shown before the node can be started, when it is not empty. The ASI node has one.</summary>
        public string Warning { get; }

        public bool HasWarning => !string.IsNullOrEmpty(Warning);

        public bool IsAvailableOn(GameDate date) => date.IsOnOrAfter(EarliestDate);

        public override string ToString() => $"{DisplayName} (era {(int)Era})";
    }

    /// <summary>
    /// The ONE technology tree.
    ///
    /// Four eras, ending somewhere the player is warned about well in advance. Nothing here invents
    /// a new subsystem: every node opens a door into a system that already exists, which is what
    /// keeps the tree from becoming a second, parallel set of rules.
    /// </summary>
    public static class ResearchTree
    {
        public const string CatalogVersion = "2026.08.02";

        /// <summary>The root every campaign starts holding. Nothing works without it.</summary>
        public const ResearchNodeId StartingNode = ResearchNodeId.FineTuningAndPrompting;

        private static readonly ResearchNode[] Entries =
        {
            // ---------------------------------------------------------- era 1
            new(ResearchNodeId.FineTuningAndPrompting, ResearchEra.Foundations,
                "Fine-tuning and prompting",
                "Take somebody else's base model and make it do one job well. Every company starts here "
                + "and none of them stay.",
                GameDate.Start, costUsd: 0, durationDays: 1, petaflopDaysRequired: 0),

            new(ResearchNodeId.HumanFeedback, ResearchEra.Foundations,
                "Reinforcement learning from human feedback",
                "Teach the model what a good answer looks like instead of hoping. Cuts the odds of a "
                + "public embarrassment and opens human preference data.",
                GameDate.FromCalendar(2022, 4, 1), costUsd: 4_000_000, durationDays: 90, petaflopDaysRequired: 200,
                requires: new[] { ResearchNodeId.FineTuningAndPrompting },
                unlocksData: DatasetSource.HumanFeedback,
                unlocksTrait: ModelTrait.Safety, gatesTrait: true),

            new(ResearchNodeId.CuratedCorpora, ResearchEra.Foundations,
                "Corpus curation",
                "Deduplication, filtering and a legal team. Turns a pile of scraped text into something "
                + "worth training on.",
                GameDate.Start, costUsd: 2_500_000, durationDays: 60, petaflopDaysRequired: 60,
                requires: new[] { ResearchNodeId.FineTuningAndPrompting },
                unlocksData: DatasetSource.CuratedWeb | DatasetSource.CodeCorpus),

            new(ResearchNodeId.EfficientAttention, ResearchEra.Foundations,
                "Efficient attention",
                "Memory-aware attention kernels and grouped queries. The same model, meaningfully faster "
                + "to train and cheaper to serve.",
                GameDate.FromCalendar(2022, 11, 1), costUsd: 6_000_000, durationDays: 120, petaflopDaysRequired: 400,
                requires: new[] { ResearchNodeId.FineTuningAndPrompting },
                unlocksArchitecture: ArchitectureId.EfficientAttention,
                unlocksTrait: ModelTrait.Latency, gatesTrait: true),

            // ------------------------------------------------- model types
            //
            // Priced and dated against when the audience for each one actually turns up. Researching
            // the agent line in 2023 buys a market with no customers in it, which is the hardware
            // timing mistake wearing a different hat.

            new(ResearchNodeId.ModelSeries, ResearchEra.Foundations,
                "Model series and versioning",
                "Ship the next model as a version of the last one instead of as a stranger. The name "
                + "carries its own audience across, and so does its reputation when it was bad.",
                GameDate.FromCalendar(2022, 9, 1), costUsd: 3_000_000, durationDays: 75,
                petaflopDaysRequired: 120,
                requires: new[] { ResearchNodeId.FineTuningAndPrompting }),

            new(ResearchNodeId.CodingModels, ResearchEra.Foundations,
                "Code specialisation",
                "Repository scale context, test execution and diffs as training signal. Almost nobody "
                + "wants this in 2022. By 2025 it is the segment that pays.",
                GameDate.FromCalendar(2022, 8, 1), costUsd: 7_500_000, durationDays: 120,
                petaflopDaysRequired: 600,
                requires: new[] { ResearchNodeId.CuratedCorpora }),

            new(ResearchNodeId.ConversationalModels, ResearchEra.Foundations,
                "Conversational tuning",
                "Tone, refusal behaviour and memory across a session. Turns a tool people try into a "
                + "product people open every morning.",
                GameDate.FromCalendar(2022, 10, 1), costUsd: 6_000_000, durationDays: 100,
                petaflopDaysRequired: 450,
                requires: new[] { ResearchNodeId.HumanFeedback }),

            new(ResearchNodeId.AutomationModels, ResearchEra.Scaling,
                "Process automation",
                "Structured output, long documents and tool calls that a compliance team will sign "
                + "off. Sells slowly, and then never leaves.",
                GameDate.FromCalendar(2023, 6, 1), costUsd: 18_000_000, durationDays: 180,
                petaflopDaysRequired: 2_400,
                requires: new[] { ResearchNodeId.ContextWindowExpansion }),

            new(ResearchNodeId.AgenticWorkstation, ResearchEra.Autonomy,
                "Autonomous workstation",
                "A model with a machine of its own: a shell, a filesystem and hours of unsupervised "
                + "work. The most expensive line on the tree and the only one that owns the endgame.",
                GameDate.FromCalendar(2024, 6, 1), costUsd: 85_000_000, durationDays: 300,
                petaflopDaysRequired: 22_000,
                requires: new[] { ResearchNodeId.AutonomousAgents, ResearchNodeId.AutomationModels }),

            new(ResearchNodeId.MultimodalGeneration, ResearchEra.Foundations,
                "Multimodal generation",
                "Images and audio in, images and audio out. Expensive, and by 2025 buyers stop treating "
                + "it as a feature and start treating it as the floor.",
                GameDate.FromCalendar(2023, 3, 1), costUsd: 14_000_000, durationDays: 180, petaflopDaysRequired: 1_200,
                requires: new[] { ResearchNodeId.HumanFeedback },
                unlocksTrait: ModelTrait.Multimodal, gatesTrait: true),

            // ---------------------------------------------------------- era 2
            new(ResearchNodeId.ScalingLaws, ResearchEra.Scaling,
                "Scaling laws",
                "Work out precisely how loss falls with parameters and tokens, and stop guessing what a "
                + "run will produce. Opens hardware you own rather than rent.",
                GameDate.FromCalendar(2022, 9, 1), costUsd: 9_000_000, durationDays: 150, petaflopDaysRequired: 900,
                requires: new[] { ResearchNodeId.CuratedCorpora },
                unlocksTier: ComputeTier.ColocatedServers),

            new(ResearchNodeId.MixtureOfExperts, ResearchEra.Scaling,
                "Mixture of experts",
                "Route each token to a fraction of the network. A quarter of the compute per token, and "
                + "a research org that has to be good enough to keep it stable.",
                GameDate.FromCalendar(2023, 12, 1), costUsd: 22_000_000, durationDays: 210, petaflopDaysRequired: 3_500,
                requires: new[] { ResearchNodeId.ScalingLaws, ResearchNodeId.EfficientAttention },
                unlocksArchitecture: ArchitectureId.SparseMixture,
                unlocksTrait: ModelTrait.Efficiency, gatesTrait: true),

            new(ResearchNodeId.ContextWindowExpansion, ResearchEra.Scaling,
                "Context window expansion",
                "Hold an entire codebase or a year of filings at once. This is what makes enterprises "
                + "stay rather than shop.",
                GameDate.FromCalendar(2023, 6, 1), costUsd: 12_000_000, durationDays: 150, petaflopDaysRequired: 1_400,
                requires: new[] { ResearchNodeId.EfficientAttention },
                unlocksTrait: ModelTrait.ContextLength, gatesTrait: true),

            new(ResearchNodeId.LicensedArchives, ResearchEra.Scaling,
                "Licensed archives",
                "Books, journals and everything else somebody owns. Slow to negotiate, and the quality "
                + "difference is not subtle.",
                GameDate.FromCalendar(2023, 3, 1), costUsd: 8_000_000, durationDays: 120, petaflopDaysRequired: 0,
                requires: new[] { ResearchNodeId.CuratedCorpora },
                unlocksData: DatasetSource.LicensedBooks | DatasetSource.AcademicArchive
                    | DatasetSource.VideoAndAudio),

            // ---------------------------------------------------------- era 3
            new(ResearchNodeId.AutonomousAgents, ResearchEra.Autonomy,
                "Autonomous agents",
                "The model stops answering and starts doing: calling tools, running loops, finishing "
                + "tasks nobody watched. The entire agent market runs through this node.",
                // Not before 2024: this needs mixtures, which the field does not have until the end
                // of 2023, and a node can never open earlier than the thing it depends on.
                GameDate.FromCalendar(2024, 1, 1), costUsd: 30_000_000, durationDays: 240, petaflopDaysRequired: 6_000,
                requires: new[] { ResearchNodeId.MixtureOfExperts },
                unlocksTrait: ModelTrait.ToolUse, gatesTrait: true),

            new(ResearchNodeId.SyntheticDataGeneration, ResearchEra.Autonomy,
                "Synthetic data generation",
                "The open web runs out. From here the model learns from text a model wrote, which works "
                + "far better than it sounds and only while your model is good enough to write it.",
                GameDate.FromCalendar(2024, 6, 1), costUsd: 26_000_000, durationDays: 210, petaflopDaysRequired: 9_000,
                requires: new[] { ResearchNodeId.LicensedArchives, ResearchNodeId.MixtureOfExperts },
                unlocksData: DatasetSource.Synthetic),

            new(ResearchNodeId.ReasoningModels, ResearchEra.Autonomy,
                "Reasoning models",
                "Train the model to spend inference thinking. It solves problems nothing else can and "
                + "the serving bill goes up by a factor of two and a half.",
                GameDate.FromCalendar(2025, 2, 1), costUsd: 70_000_000, durationDays: 270, petaflopDaysRequired: 22_000,
                requires: new[] { ResearchNodeId.AutonomousAgents },
                unlocksArchitecture: ArchitectureId.ReasoningMixture),

            new(ResearchNodeId.LongContextMixtures, ResearchEra.Autonomy,
                "Long context mixtures",
                "Sparsity and a very long window in the same model, without either ruining the other.",
                GameDate.FromCalendar(2024, 9, 1), costUsd: 34_000_000, durationDays: 210, petaflopDaysRequired: 11_000,
                requires: new[] { ResearchNodeId.ContextWindowExpansion, ResearchNodeId.MixtureOfExperts },
                unlocksArchitecture: ArchitectureId.LongContextMixture),

            new(ResearchNodeId.DatacenterProgramme, ResearchEra.Autonomy,
                "Datacenter programme",
                "Power contracts, land, substations and a construction schedule measured in years. The "
                + "cheapest FLOP in the game sits on the other side of it.",
                GameDate.FromCalendar(2024, 1, 1), costUsd: 45_000_000, durationDays: 240, petaflopDaysRequired: 0,
                requires: new[] { ResearchNodeId.ScalingLaws },
                unlocksTier: ComputeTier.OwnDatacenter),

            // ---------------------------------------------------------- era 4
            new(ResearchNodeId.HybridArchitectures, ResearchEra.Superintelligence,
                "Hybrid state space",
                "Attention where it earns its cost and recurrence everywhere else. Long context stops "
                + "being expensive.",
                GameDate.FromCalendar(2026, 6, 1), costUsd: 120_000_000, durationDays: 300, petaflopDaysRequired: 40_000,
                requires: new[] { ResearchNodeId.LongContextMixtures, ResearchNodeId.ReasoningModels },
                unlocksArchitecture: ArchitectureId.HybridStateSpace),

            new(ResearchNodeId.RecursiveSelfImprovement, ResearchEra.Superintelligence,
                "Recursive self-improvement",
                "The models start designing the next models, and the loop closes. Every run after this "
                + "one is partly written by the run before it.",
                GameDate.FromCalendar(2027, 1, 1), costUsd: 400_000_000, durationDays: 420, petaflopDaysRequired: 160_000,
                requires: new[] { ResearchNodeId.SyntheticDataGeneration, ResearchNodeId.ReasoningModels },
                unlocksTrait: ModelTrait.Ecosystem, gatesTrait: true),

            new(ResearchNodeId.ArtificialSuperintelligence, ResearchEra.Superintelligence,
                "Artificial superintelligence",
                "Beyond this point the company is no longer the most capable thing in the building. "
                + "Governments will want contracts. The governments that do not will want penalties.",
                GameDate.FromCalendar(2028, 1, 1), costUsd: 2_000_000_000, durationDays: 540,
                petaflopDaysRequired: 900_000,
                requires: new[] { ResearchNodeId.RecursiveSelfImprovement, ResearchNodeId.HybridArchitectures },
                warning:
                "SYSTEM ALERT: this crosses the threshold of human intelligence. The effects are not "
                + "reversible and no part of the campaign after it resembles the part before it.")
        };

        private static readonly Dictionary<ResearchNodeId, ResearchNode> ById = BuildIndex();

        public static IReadOnlyList<ResearchNode> All => Entries;

        public static ResearchNode Get(ResearchNodeId id)
        {
            if (!ById.TryGetValue(id, out var node))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown research node.");
            }

            return node;
        }

        public static bool TryGet(ResearchNodeId id, out ResearchNode node) => ById.TryGetValue(id, out node);

        public static IEnumerable<ResearchNode> InEra(ResearchEra era)
        {
            foreach (var entry in Entries)
            {
                if (entry.Era == era)
                {
                    yield return entry;
                }
            }
        }

        /// <summary>The node that opens a given architecture, or None when nothing gates it.</summary>
        public static ResearchNodeId GateForArchitecture(ArchitectureId architecture)
        {
            foreach (var entry in Entries)
            {
                if (entry.UnlocksArchitecture == architecture && architecture != ArchitectureId.None)
                {
                    return entry.Id;
                }
            }

            return ResearchNodeId.None;
        }

        /// <summary>The node that opens a given corpus, or None when nothing gates it.</summary>
        public static ResearchNodeId GateForData(DatasetSource source)
        {
            foreach (var entry in Entries)
            {
                if ((entry.UnlocksData & source) == source && source != DatasetSource.None)
                {
                    return entry.Id;
                }
            }

            return ResearchNodeId.None;
        }

        /// <summary>The node that opens a given compute tier, or None when nothing gates it.</summary>
        public static ResearchNodeId GateForTier(ComputeTier tier)
        {
            foreach (var entry in Entries)
            {
                if (entry.UnlocksTier == tier && tier != ComputeTier.None)
                {
                    return entry.Id;
                }
            }

            return ResearchNodeId.None;
        }

        /// <summary>The node that opens a given upgradeable trait, or None when nothing gates it.</summary>
        public static ResearchNodeId GateForTrait(ModelTrait trait)
        {
            foreach (var entry in Entries)
            {
                if (entry.GatesTrait && entry.UnlocksTrait == trait)
                {
                    return entry.Id;
                }
            }

            return ResearchNodeId.None;
        }

        private static Dictionary<ResearchNodeId, ResearchNode> BuildIndex()
        {
            var index = new Dictionary<ResearchNodeId, ResearchNode>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Id] = entry;
            }

            return index;
        }
    }
}
