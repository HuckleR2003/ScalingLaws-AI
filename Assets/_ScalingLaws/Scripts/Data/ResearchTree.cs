using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Which stretch of the campaign a node belongs to.</summary>
    /// <summary>
    /// What kind of work a node is.
    ///
    /// The eras are a calendar and must stay one, so this is a second axis rather than a fifth era.
    /// A capability node opens something the company could not do at all; a model improvement node
    /// makes something it already does go further. They are drawn differently because they are read
    /// differently: one is a decision about direction, the other is a decision about depth.
    /// </summary>
    public enum ResearchTrack
    {
        Capability = 0,
        ModelImprovement = 1,

        /// <summary>
        /// The safety modules the creator's SAFETY stage picks between.
        ///
        /// Its own track rather than more Model Improvement, because these are read differently: a
        /// deepening node makes a run better and one of these decides whether the company is still
        /// here in two years. They are also the only nodes whose whole purpose is to reduce the
        /// chance of something rather than to raise the ceiling on anything.
        /// </summary>
        Safety = 2
    }

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

        // The three that open the Scale and Data choices. They are deliberately not on the
        // capability line: none of them makes a better model on its own, each one opens an option
        // whose whole character is that it trades one thing for another.
        LowPrecisionTraining = 205,
        CorpusDeduplication = 206,
        ContinuousDataPipeline = 207,

        // Era 3, autonomy.
        AutonomousAgents = 301,
        SyntheticDataGeneration = 302,
        ReasoningModels = 303,
        LongContextMixtures = 304,
        DatacenterProgramme = 305,

        // The Model Improvement track. Numbered apart from the eras because they are a second
        // axis: each one deepens something the company already does rather than opening a new
        // direction, and they are spread across the calendar like everything else.
        //
        // 5xx raises how large a run the company can supervise. 5x1 upward is the cluster fabric.
        ShardedOptimizerStates = 501,
        PipelineParallelism = 502,
        UltraReadiness = 503,

        // 6xx is the SAFETY stage. Three lines of three, because the first tier of the first two is
        // something the company already knows how to do on the day it opens.
        //
        // 60x: the model auditing itself. 61x: somebody attacking it. 62x: where the data lives.
        LicensedStackedAssa = 601,
        AdvancedAssa = 602,
        AssaEcosystem = 603,

        AutomatedRedTeaming = 611,
        AdversarialCampaigns = 612,
        ContinuousRedTeam = 613,

        BasicDataIsolation = 620,
        EncryptedDataVaults = 621,
        DifferentialPrivacy = 622,
        PrivacyPreservingTraining = 623,

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
            string warning = null,
            bool optionalTechnology = false,
            ResearchTrack track = ResearchTrack.Capability)
        {
            Track = track;
            OptionalTechnology = optionalTechnology;
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

        /// <summary>Capability, or one of the deepening tracks. See <see cref="ResearchTrack"/>.</summary>
        public ResearchTrack Track { get; }

        /// <summary>
        /// A node that opens an option rather than raising a ceiling.
        ///
        /// The tree used to be entirely ladder: every node moved capability, unlocked an
        /// architecture, a corpus, a compute tier or a trait line, so "research the cheapest thing
        /// available" was a reasonable description of a competent player. The Scale and Data
        /// technologies are not that. They open a trade that a player may have no intention of
        /// taking, and a company that researches one it will never use has spent months on nothing.
        ///
        /// The scripted operator in the balance suite reads this and skips them, because it is meant
        /// to stand in for a competent baseline rather than for somebody clicking the cheapest
        /// button. A real player sees them exactly like any other node.
        /// </summary>
        public bool OptionalTechnology { get; }
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
                GameDate.FromCalendar(2022, 12, 1), costUsd: 7_500_000, durationDays: 120,
                petaflopDaysRequired: 600,
                requires: new[] { ResearchNodeId.ConversationalModels }),

            new(ResearchNodeId.ConversationalModels, ResearchEra.Foundations,
                "Conversational tuning",
                "Tone, refusal behaviour and memory across a session. Turns a tool people try into a "
                + "product people open every morning.",
                GameDate.FromCalendar(2022, 8, 1), costUsd: 6_000_000, durationDays: 100,
                petaflopDaysRequired: 450,
                requires: new[] { ResearchNodeId.HumanFeedback }),

            new(ResearchNodeId.AutomationModels, ResearchEra.Scaling,
                "Process automation",
                "Structured output, long documents and tool calls that a compliance team will sign "
                + "off. Sells slowly, and then never leaves.",
                GameDate.FromCalendar(2023, 6, 1), costUsd: 18_000_000, durationDays: 180,
                petaflopDaysRequired: 2_400,
                requires: new[] { ResearchNodeId.CodingModels, ResearchNodeId.ContextWindowExpansion }),

            new(ResearchNodeId.AgenticWorkstation, ResearchEra.Autonomy,
                "Autonomous workstation",
                "A model with a machine of its own: a shell, a filesystem and hours of unsupervised "
                + "work. The most expensive line on the tree and the only one that owns the endgame.",
                GameDate.FromCalendar(2024, 6, 1), costUsd: 85_000_000, durationDays: 300,
                petaflopDaysRequired: 22_000,
                requires: new[] { ResearchNodeId.AutomationModels, ResearchNodeId.AutonomousAgents }),

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

            new(ResearchNodeId.LowPrecisionTraining, ResearchEra.Scaling,
                "Low precision training",
                "Keep the numbers in eight bits and watch for the run coming apart. Nearly twice the "
                + "compute out of the same cluster, and a loss curve that can diverge in the last "
                + "third with nothing to show for the money.",
                GameDate.FromCalendar(2023, 3, 1), costUsd: 16_000_000, durationDays: 165,
                petaflopDaysRequired: 2_200,
                requires: new[] { ResearchNodeId.ScalingLaws },
                warning: "Opens FP8, which is a bet rather than a saving. The spread on the finished "
                    + "model is more than twice what it is at BF16, and the silicon has to support "
                    + "it as well: the node alone is not enough before 2023.",
                optionalTechnology: true),

            new(ResearchNodeId.CorpusDeduplication, ResearchEra.Foundations,
                "Corpus deduplication",
                "Find the near duplicates, not just the exact ones. The crawl is full of the same "
                + "page under forty domains, and a run that reads it forty times learns it once and "
                + "pays forty times.",
                GameDate.FromCalendar(2022, 9, 1), costUsd: 4_500_000, durationDays: 110,
                petaflopDaysRequired: 400,
                requires: new[] { ResearchNodeId.CuratedCorpora },
                warning: "Opens the aggressive pass, which costs a fifth of the corpus. That is a "
                    + "gain when the run had tokens to spare and a straight loss when it did not.",
                optionalTechnology: true),

            new(ResearchNodeId.ContinuousDataPipeline, ResearchEra.Scaling,
                "Continuous data pipeline",
                "Ingest, clean and license text as it appears rather than in a batch once a year. "
                + "What it buys is a model that knows about this month.",
                GameDate.FromCalendar(2023, 9, 1), costUsd: 19_000_000, durationDays: 190,
                petaflopDaysRequired: 900,
                requires: new[] { ResearchNodeId.CuratedCorpora, ResearchNodeId.ScalingLaws },
                warning: "Opens the freshest corpus cutoffs. Recent text is dearer to license, "
                    + "because nobody has cleaned it and the people who own it know what it is for.",
                optionalTechnology: true),

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

            // ------------------------------------------------------- safety: staying in business
            //
            // **These are the only nodes in the tree that buy a smaller chance of something rather
            // than a bigger number.** That makes them hard to value and easy to skip, which is
            // exactly the mistake they exist to punish: the ninety million dollar fine is not a
            // slow decline, it is a Tuesday.
            //
            // Costs are deliberately modest against the capability line. A company that cannot
            // afford safety is a company that will not survive needing it, and pricing these like
            // frontier research would make skipping them the correct play.

            new(ResearchNodeId.LicensedStackedAssa, ResearchEra.Foundations,
                "Licensed Stacked-ASSA",
                "Automatic Self Safety Auditioning is already running. This licenses a suite of "
                + "outside algorithms to bolt onto it, and what the licence buys is that the "
                + "automated passes stop missing the same class of hole every time.",
                GameDate.FromCalendar(2022, 6, 1), costUsd: 3_400_000, durationDays: 80,
                petaflopDaysRequired: 180,
                track: ResearchTrack.Safety),

            new(ResearchNodeId.AdvancedAssa, ResearchEra.Scaling,
                "Advanced ASSA",
                "Every audit the company has ever run is training data now. Enough of it stands up "
                + "an attacking system the size of the model itself, sealed in with it.",
                GameDate.FromCalendar(2023, 4, 1), costUsd: 12_500_000, durationDays: 150,
                petaflopDaysRequired: 900,
                requires: new[] { ResearchNodeId.LicensedStackedAssa },
                track: ResearchTrack.Safety),

            new(ResearchNodeId.AssaEcosystem, ResearchEra.Autonomy,
                "ASSA Ecosystem",
                "A standing population of auditors rather than a pass that runs and finishes. Each "
                + "one specialised, each fed by what the others found, running against every model "
                + "the company has ever shipped.",
                GameDate.FromCalendar(2024, 6, 1), costUsd: 42_000_000, durationDays: 240,
                petaflopDaysRequired: 3_200,
                requires: new[] { ResearchNodeId.AdvancedAssa },
                warning: "Adds over three months to every run that uses it. The protection is the "
                    + "best in the game and the calendar is the price.",
                track: ResearchTrack.Safety),

            new(ResearchNodeId.AutomatedRedTeaming, ResearchEra.Foundations,
                "Automated Red Teaming",
                "The folder of hand-written attacks becomes a machine: it generates its own "
                + "attempts, keeps the ones that got closest and mutates those.",
                GameDate.FromCalendar(2022, 8, 1), costUsd: 2_800_000, durationDays: 70,
                petaflopDaysRequired: 220,
                track: ResearchTrack.Safety),

            new(ResearchNodeId.AdversarialCampaigns, ResearchEra.Scaling,
                "Adversarial Campaigns",
                "Long runs with a goal, a budget and a record, aimed at one class of failure at a "
                + "time. Every failed attempt is kept, because a failed attack is the most useful "
                + "thing anybody has about the next one.",
                GameDate.FromCalendar(2023, 6, 1), costUsd: 11_000_000, durationDays: 140,
                petaflopDaysRequired: 1_100,
                requires: new[] { ResearchNodeId.AutomatedRedTeaming },
                track: ResearchTrack.Safety),

            new(ResearchNodeId.ContinuousRedTeam, ResearchEra.Autonomy,
                "Continuous Red Team",
                "A standing team of agents that never stops and never ships. They attack every "
                + "model on sale, all the time, and they get better every time they fail.",
                GameDate.FromCalendar(2024, 9, 1), costUsd: 38_000_000, durationDays: 220,
                petaflopDaysRequired: 3_000,
                requires: new[] { ResearchNodeId.AdversarialCampaigns },
                track: ResearchTrack.Safety),

            new(ResearchNodeId.BasicDataIsolation, ResearchEra.Foundations,
                "Basic Data Isolation",
                "User data is pulled out of the model's ordinary working path and kept somewhere it "
                + "has to ask to reach. This is the difference between leaking a log and leaking a "
                + "customer list.",
                GameDate.FromCalendar(2022, 2, 1), costUsd: 1_400_000, durationDays: 55,
                petaflopDaysRequired: 60,
                warning: "The company does not start knowing this. Until it is done there is no "
                    + "data protection on any run at all.",
                track: ResearchTrack.Safety),

            new(ResearchNodeId.EncryptedDataVaults, ResearchEra.Foundations,
                "Encrypted Data Vaults",
                "Anything sensitive lives encrypted, in a store with its own short access list. It "
                + "does not stop a breach. It means the breach comes out unreadable.",
                GameDate.FromCalendar(2022, 10, 1), costUsd: 5_200_000, durationDays: 95,
                petaflopDaysRequired: 260,
                requires: new[] { ResearchNodeId.BasicDataIsolation },
                track: ResearchTrack.Safety),

            new(ResearchNodeId.DifferentialPrivacy, ResearchEra.Scaling,
                "Differential Privacy",
                "Training data is processed so no single person can be recovered from what the "
                + "model learned. The first tier a regulator accepts as an argument rather than as "
                + "a promise.",
                GameDate.FromCalendar(2023, 7, 1), costUsd: 18_000_000, durationDays: 165,
                petaflopDaysRequired: 1_400,
                requires: new[] { ResearchNodeId.EncryptedDataVaults },
                warning: "Costs a little capability on every run that uses it, because the data it "
                    + "trains on has been blurred on purpose.",
                track: ResearchTrack.Safety),

            new(ResearchNodeId.PrivacyPreservingTraining, ResearchEra.Autonomy,
                "Privacy-Preserving Training",
                "Privacy stops being something added after the run and becomes part of how the run "
                + "works. Half of everything that would have ended the company now does not.",
                GameDate.FromCalendar(2025, 1, 1), costUsd: 56_000_000, durationDays: 260,
                petaflopDaysRequired: 4_200,
                requires: new[] { ResearchNodeId.DifferentialPrivacy },
                track: ResearchTrack.Safety),

            // ------------------------------------------- model improvement: how big a run can be
            //
            // Three rungs on the parameter ceiling, and every one of them is the real reason a lab
            // can train something larger than a single node holds. They are cheap next to the
            // capability line on purpose: what they cost is the calendar, and the calendar is what
            // the player is short of.

            new(ResearchNodeId.ShardedOptimizerStates, ResearchEra.Foundations,
                "Sharded optimizer states",
                "Adam keeps two moments and a full precision copy of every weight, which is three "
                + "times the model sitting on every accelerator holding an identical duplicate. "
                + "Shard them across the cluster and each machine holds its slice.",
                GameDate.FromCalendar(2022, 3, 1), costUsd: 2_600_000, durationDays: 70,
                petaflopDaysRequired: 120,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.PipelineParallelism, ResearchEra.Scaling,
                "Pipeline parallelism",
                "Cut the model into stages and give each machine a stage rather than a copy. The "
                + "layers no longer have to fit anywhere in particular, only the stage does.",
                GameDate.FromCalendar(2022, 11, 1), costUsd: 8_400_000, durationDays: 120,
                petaflopDaysRequired: 520,
                requires: new[] { ResearchNodeId.ShardedOptimizerStates },
                warning: "The bubble between stages is real compute the run pays for and never "
                    + "gets back. It buys size, not speed.",
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.UltraReadiness, ResearchEra.Scaling,
                "Ultra readiness",
                "A run of this length will lose machines while it is running. Checkpoint often "
                + "enough to survive it, restart without a human, and hold thousands of "
                + "accelerators in step for months at a time.",
                GameDate.FromCalendar(2023, 8, 1), costUsd: 27_000_000, durationDays: 200,
                petaflopDaysRequired: 1_800,
                requires: new[] { ResearchNodeId.PipelineParallelism, ResearchNodeId.ScalingLaws },
                warning: "Opens almost the whole slider. What it does not do is pay for the run, "
                    + "and a run at this size is a year of the company's income.",
                track: ResearchTrack.ModelImprovement),

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
