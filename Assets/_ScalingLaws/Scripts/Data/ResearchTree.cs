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
        Safety = 2,

        /// <summary>
        /// Research about the company rather than about the model.
        ///
        /// **The other three tracks are all the same subject.** `Capability`, `ModelImprovement`
        /// and `Safety` differ in what they do to a model, not in what they are about, and a survey
        /// of all 55 nodes found every one of them researching the model: bigger, cheaper, safer,
        /// better shaped, or a new family to build it from. That is the system a player spends the
        /// least clock time in.
        ///
        /// This is the room, the payroll, the power bill and the fleet. A fourth track rather than
        /// a sixth era, because the eras are a calendar and a technique belongs in the year it was
        /// real; cooling and substations land in the middle of it.
        /// </summary>
        Operations = 3
    }

    public enum ResearchEra
    {
        Foundations = 1,
        Scaling = 2,
        Autonomy = 3,
        Superintelligence = 4,

        /// <summary>
        /// The end of the game, and the only era whose subject is not the model.
        ///
        /// Everything before this asks what the company can build. This asks what it is willing to
        /// be responsible for. The nodes are cheap in capability and enormous in consequence: they
        /// open a door to running parts of a country, and nothing behind that door is reversible.
        /// </summary>
        Statecraft = 5
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

        /// <summary>Opens BF16. The second rung of the precision ladder, and an era one node.</summary>
        MixedPrecisionTraining = 208,

        /// <summary>Opens FP32. The first rung, and the cheapest node in the game.</summary>
        SinglePrecisionTraining = 209,
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

        // 7xx opens the architecture directions. Five ladders of three, first rung each an existing
        // node so the tree did not have to double in size to make the direction sliders real.
        //
        // Every one is optionalTechnology: none of them makes a better model on its own, each one
        // only raises how far a family programme may lean one way. The scripted operator in
        // PlayabilityTests skips them, which is what keeps the balance suite measuring the economy
        // rather than measuring this.
        LearnedRouting = 701,
        ExpertParallelism = 702,

        FusedKernels = 711,
        OverlappedCollectives = 712,

        CurriculumTraining = 721,
        SelfDistillation = 722,

        QuantisedServing = 731,
        SpeculativeDecoding = 732,

        ProcessSupervision = 741,
        InferenceTimeSearch = 742,

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

        // 8xx is the Operations track: the room, not the model. 80x is the server room.
        //
        // Numbered apart from the eras for the same reason 5xx and 7xx are: the track is a second
        // axis and these nodes sit wherever on the calendar the technique was actually real, which
        // for all four of these is the middle of the game.
        LiquidLoops = 801,
        AirflowModelling = 802,
        OwnSubstation = 803,
        RackTelemetry = 804,

        // Era 4, the end game.
        HybridArchitectures = 401,
        RecursiveSelfImprovement = 402,
        ArtificialSuperintelligence = 403,

        // Era 5, statecraft. What the company does with what it built.
        //
        // **900, not 500.** The first attempt used 501 to 505, which are `ShardedOptimizerStates`,
        // `PipelineParallelism` and `UltraReadiness` - the scale ceiling ladder, added months
        // earlier. Nothing failed to compile: the enum happily carried two names for one value, the
        // tree's index quietly kept whichever was built last, and three nodes became unreachable
        // while a fourth reported a prerequisite dated seven years after itself.
        GeneralIntelligence = 901,
        RealTimeAssimilation = 902,
        SovereignLiaison = 903,
        ContinuousOversight = 904,
        RedundantInference = 905
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
            // The English original the phrase book was built from. Not stored: Loc holds both
            // languages and falls back to English itself, so a second copy here would only be
            // somewhere for the two to disagree.
            _ = displayName;
            // The English original the phrase book was built from. Not stored, same as the name.
            _ = description;
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
        /// <summary>
        /// The node's name, in whatever language the player reads.
        ///
        /// **Read at access time rather than stored.** Fifty of these are drawn on the research
        /// tree, on the locked end of every architecture slider and in the news, so a name captured
        /// when the catalog was built leaves the largest screen in the game in English.
        /// </summary>
        public string DisplayName => Loc.T(KeyFor(Id));

        /// <summary>
        /// The phrase-book stem for a node.
        ///
        /// Written out rather than derived from the enum name: half of them do not match what the
        /// player reads, and deriving would produce keys nobody would think to look for.
        /// </summary>
        private static string KeyFor(ResearchNodeId id) => id switch
        {
            ResearchNodeId.FineTuningAndPrompting => "node.finetuning",
            ResearchNodeId.HumanFeedback => "node.humanfeedback",
            ResearchNodeId.EfficientAttention => "node.efficientattention",
            ResearchNodeId.MultimodalGeneration => "node.multimodal",
            ResearchNodeId.CuratedCorpora => "node.curatedcorpora",
            ResearchNodeId.CodingModels => "node.coding",
            ResearchNodeId.ConversationalModels => "node.conversational",
            ResearchNodeId.AutomationModels => "node.automation",
            ResearchNodeId.AgenticWorkstation => "node.agentic",
            ResearchNodeId.ModelSeries => "node.series",
            ResearchNodeId.ScalingLaws => "node.scalinglaws",
            ResearchNodeId.MixtureOfExperts => "node.mixtureofexperts",
            ResearchNodeId.ContextWindowExpansion => "node.context",
            ResearchNodeId.LicensedArchives => "node.licensed",
            ResearchNodeId.LowPrecisionTraining => "node.lowprecision",
            ResearchNodeId.CorpusDeduplication => "node.dedup",
            ResearchNodeId.MixedPrecisionTraining => "node.mixedprecision",
            ResearchNodeId.SinglePrecisionTraining => "node.singleprecision",
            ResearchNodeId.ContinuousDataPipeline => "node.pipeline_data",
            ResearchNodeId.AutonomousAgents => "node.autonomousagents",
            ResearchNodeId.SyntheticDataGeneration => "node.syntheticdata",
            ResearchNodeId.ReasoningModels => "node.reasoning",
            ResearchNodeId.LongContextMixtures => "node.longcontext",
            ResearchNodeId.DatacenterProgramme => "node.datacenter",
            ResearchNodeId.ShardedOptimizerStates => "node.sharding",
            ResearchNodeId.PipelineParallelism => "node.pipeline",
            ResearchNodeId.UltraReadiness => "node.ultrareadiness",
            ResearchNodeId.LicensedStackedAssa => "node.assa1",
            ResearchNodeId.AdvancedAssa => "node.assa2",
            ResearchNodeId.AssaEcosystem => "node.assa3",
            ResearchNodeId.AutomatedRedTeaming => "node.red1",
            ResearchNodeId.AdversarialCampaigns => "node.red2",
            ResearchNodeId.ContinuousRedTeam => "node.red3",
            ResearchNodeId.BasicDataIsolation => "node.data0",
            ResearchNodeId.EncryptedDataVaults => "node.data1",
            ResearchNodeId.DifferentialPrivacy => "node.data2",
            ResearchNodeId.PrivacyPreservingTraining => "node.data3",
            ResearchNodeId.HybridArchitectures => "node.hybrid",
            ResearchNodeId.RecursiveSelfImprovement => "node.recursive",
            ResearchNodeId.ArtificialSuperintelligence => "node.asi",
            ResearchNodeId.LearnedRouting => "node.learnedrouting",
            ResearchNodeId.ExpertParallelism => "node.expertparallelism",
            ResearchNodeId.FusedKernels => "node.fusedkernels",
            ResearchNodeId.OverlappedCollectives => "node.overlappedcollectives",
            ResearchNodeId.CurriculumTraining => "node.curriculumtraining",
            ResearchNodeId.SelfDistillation => "node.selfdistillation",
            ResearchNodeId.QuantisedServing => "node.quantisedserving",
            ResearchNodeId.SpeculativeDecoding => "node.speculativedecoding",
            ResearchNodeId.ProcessSupervision => "node.processsupervision",
            ResearchNodeId.InferenceTimeSearch => "node.inferencetimesearch",

            // The Operations track. The room, not the model.
            ResearchNodeId.LiquidLoops => "node.liquidloops",
            ResearchNodeId.AirflowModelling => "node.airflow",
            ResearchNodeId.OwnSubstation => "node.substation",
            ResearchNodeId.RackTelemetry => "node.racktelemetry",

            // **Era five had no arms at all until 2026-09-04**, so all five statecraft nodes fell
            // through the default and drew as "Fine-tuning and prompting" with era one's
            // description under them: on the tree, in the completion event and in the news. A
            // `_ =>` arm cannot fail loudly, which is why `NodeKeyTests` now walks every member.
            ResearchNodeId.GeneralIntelligence => "node.agi",
            ResearchNodeId.RealTimeAssimilation => "node.assimilation",
            ResearchNodeId.SovereignLiaison => "node.liaison",
            ResearchNodeId.ContinuousOversight => "node.oversight",
            ResearchNodeId.RedundantInference => "node.redundant",

            _ => "node.finetuning"
        };
        /// <summary>
        /// What the node actually is, in whatever language the player reads.
        ///
        /// Same rule as the name: read at access time, English in the book as the fallback. These
        /// are the longest player-facing text in the game after the tutorial, and they are the
        /// whole reason somebody picks one node over another.
        /// </summary>
        public string Description => Loc.T(KeyFor(Id) + ".desc");
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

        /// <summary>
        /// Whether the player may start this today.
        ///
        /// **Era one ignores its own dates.** Every Foundations node carries the month the real
        /// technique actually landed, which reads well in a tooltip and played terribly: a new
        /// company could see fourteen nodes and start two of them, and the rest grew a START button
        /// weeks later with no explanation. Players reasonably concluded the feature was broken.
        ///
        /// The prerequisite chain still holds, so nothing can be skipped — era one is simply open
        /// from the first day, which is the only era a new player is looking at. Later eras keep
        /// their dates, because by then the player knows what a locked node means and the dates are
        /// carrying the actual shape of the timeline.
        /// </summary>
        public bool IsAvailableOn(GameDate date) =>
            Era == ResearchEra.Foundations || date.IsOnOrAfter(EarliestDate);

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
                requires: new[] { ResearchNodeId.ScalingLaws, ResearchNodeId.MixedPrecisionTraining },
                warning: "Opens FP8, which is a bet rather than a saving. The spread on the finished "
                    + "model is more than twice what it is at BF16, and the silicon has to support "
                    + "it as well: the node alone is not enough before 2023.",
                optionalTechnology: true),

            // The bottom of the precision ladder, and deliberately the cheapest thing in the
            // tree. A company starts at double width, which nobody has trained at for years, and
            // this is the first month of work that gets it onto something modern. It is priced so
            // that a player who does nothing else can still have it inside two months.
            new(ResearchNodeId.SinglePrecisionTraining, ResearchEra.Foundations,
                "Single precision training",
                "Stop carrying sixty-four bits of mantissa nobody is using. A third more throughput "
                + "out of the cluster you already rent, for a fortnight of somebody's time.",
                GameDate.Start, costUsd: 450_000, durationDays: 20, petaflopDaysRequired: 8,
                requires: new[] { ResearchNodeId.FineTuningAndPrompting }),

            // The second rung. Era one, cheap, and early: a company should reach it inside its
            // first year, because until it does every run is being trained wider than it needs.
            new(ResearchNodeId.MixedPrecisionTraining, ResearchEra.Foundations,
                "Mixed precision training",
                "Keep the master weights wide and do the arithmetic narrow. Half the memory and "
                + "close to twice the throughput, for none of the risk that eight bits carries.",
                GameDate.Start, costUsd: 1_800_000, durationDays: 45, petaflopDaysRequired: 40,
                requires: new[] { ResearchNodeId.SinglePrecisionTraining }),

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

            // ------------------------------------------- the architecture directions
            //
            // Five ladders of two, sitting above a first rung that is an existing node. Nothing
            // here makes a model better by itself: each one raises how far a family programme is
            // allowed to lean in one direction, which is why every one is optionalTechnology and
            // why the scripted campaign never buys them.
            //
            // Dated so that a direction cannot be pushed to its limit before the industry knew how.
            // The dates are when the technique was actually being used at scale, rounded.

            new(ResearchNodeId.LearnedRouting, ResearchEra.Scaling,
                "Learned routing",
                "A sparse model is only as good as the thing deciding which experts see a token. "
                + "Train the router rather than hashing, balance the load across experts, and stop "
                + "half the network from never being picked.",
                GameDate.FromCalendar(2024, 4, 1), costUsd: 6_200_000, durationDays: 110,
                petaflopDaysRequired: 380,
                requires: new[] { ResearchNodeId.MixtureOfExperts },
                warning: "Routing is the part of a sparse model that goes wrong. This makes deeper "
                    + "sparsity survivable, not free.",
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.ExpertParallelism, ResearchEra.Autonomy,
                "Expert parallelism",
                "Put each expert on its own machines and route tokens across the fabric to reach "
                + "them. The model stops having to fit anywhere, and the network becomes the thing "
                + "you are buying.",
                GameDate.FromCalendar(2024, 11, 1), costUsd: 19_000_000, durationDays: 175,
                petaflopDaysRequired: 1_150,
                requires: new[] { ResearchNodeId.LearnedRouting, ResearchNodeId.PipelineParallelism },
                warning: "Opens the whole sparsity slider. A model this sparse is cheap per token "
                    + "and needs a fabric most companies cannot afford.",
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.FusedKernels, ResearchEra.Foundations,
                "Fused kernels",
                "Stop writing intermediate results to memory between every operation. One kernel "
                + "that does the whole block keeps the arithmetic units fed instead of the memory "
                + "bus busy.",
                GameDate.FromCalendar(2023, 2, 1), costUsd: 3_100_000, durationDays: 80,
                petaflopDaysRequired: 140,
                requires: new[] { ResearchNodeId.EfficientAttention },
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.OverlappedCollectives, ResearchEra.Scaling,
                "Overlapped collectives",
                "The cluster spends a large share of every step waiting for gradients to finish "
                + "moving. Start the next block of arithmetic while they are still in flight and "
                + "the waiting stops being time.",
                GameDate.FromCalendar(2023, 8, 1), costUsd: 11_500_000, durationDays: 145,
                petaflopDaysRequired: 700,
                requires: new[] { ResearchNodeId.FusedKernels, ResearchNodeId.ShardedOptimizerStates },
                warning: "Buys calendar and nothing else. Worth little to a company that is not "
                    + "waiting on its cluster.",
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.CurriculumTraining, ResearchEra.Scaling,
                "Curriculum training",
                "The order tokens arrive in changes what the model gets out of them. Sequence the "
                + "corpus rather than shuffling it, and raise the difficulty as the model can take "
                + "it.",
                GameDate.FromCalendar(2023, 3, 1), costUsd: 7_400_000, durationDays: 130,
                petaflopDaysRequired: 460,
                requires: new[] { ResearchNodeId.ScalingLaws },
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.SelfDistillation, ResearchEra.Autonomy,
                "Self distillation",
                "Train the next model against the one you already have as well as against the data. "
                + "The student reaches the teacher's level for less, and then goes past it.",
                GameDate.FromCalendar(2024, 5, 1), costUsd: 23_000_000, durationDays: 190,
                petaflopDaysRequired: 1_400,
                requires: new[] { ResearchNodeId.CurriculumTraining },
                warning: "The slowest direction to pay off in the game. It raises the ceiling of "
                    + "every model the family ever produces and none of them exist yet.",
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.QuantisedServing, ResearchEra.Scaling,
                "Quantised serving",
                "Serve at eight bits instead of sixteen. The weights are the same weights, the "
                + "answers are near enough the same answers, and the machine holds twice as much of "
                + "it.",
                GameDate.FromCalendar(2023, 7, 1), costUsd: 2_900_000, durationDays: 75,
                petaflopDaysRequired: 110,
                requires: new[] { ResearchNodeId.LowPrecisionTraining },
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.SpeculativeDecoding, ResearchEra.Scaling,
                "Speculative decoding",
                "A small model guesses the next several tokens and the large one checks them all at "
                + "once. Most guesses are right, and the ones that are not cost nothing to reject.",
                GameDate.FromCalendar(2024, 1, 1), costUsd: 9_800_000, durationDays: 135,
                petaflopDaysRequired: 540,
                requires: new[] { ResearchNodeId.QuantisedServing },
                warning: "Changes nothing a customer can see. It decides whether you can follow "
                    + "somebody else's price cut.",
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.ProcessSupervision, ResearchEra.Autonomy,
                "Process supervision",
                "Reward the working rather than the answer. A model marked only on the final line "
                + "learns to guess it; one marked on every step learns to get there.",
                GameDate.FromCalendar(2025, 6, 1), costUsd: 14_000_000, durationDays: 160,
                petaflopDaysRequired: 820,
                requires: new[] { ResearchNodeId.ReasoningModels },
                optionalTechnology: true,
                track: ResearchTrack.ModelImprovement),

            new(ResearchNodeId.InferenceTimeSearch, ResearchEra.Autonomy,
                "Inference time search",
                "Spend compute when the question is asked rather than only when the model is built. "
                + "Sample several attempts, score them, keep the best.",
                GameDate.FromCalendar(2026, 1, 1), costUsd: 26_000_000, durationDays: 200,
                petaflopDaysRequired: 1_600,
                requires: new[] { ResearchNodeId.ProcessSupervision },
                warning: "Capability a bigger cluster cannot buy, paid for on every request "
                    + "forever rather than once at training time.",
                optionalTechnology: true,
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
                + "reversible and no part of the campaign after it resembles the part before it."),

            // ---- era 5, statecraft ---------------------------------------------------------------
            //
            // **These are cheap in capability and enormous in consequence**, which is the opposite
            // shape to every era before them. Nothing here raises a ceiling or unlocks a corpus.
            // They open the door to running parts of a country, and the money behind that door is
            // larger than anything else in the game by an order of magnitude, as are the mistakes.
            //
            // The chain is deliberately narrow. There is one way in, one way to be trusted with it,
            // and two ways to make it survivable, and a player who takes the first two and skips the
            // last two has signed a national contract with no safety net. That is allowed, it is
            // profitable for a while, and it is how this ending goes wrong.

            new(ResearchNodeId.GeneralIntelligence, ResearchEra.Statecraft,
                "General intelligence",
                "Not a better model. A model that does not need to be told what kind of problem it "
                + "is looking at. Everything after this is about what you point it at.",
                GameDate.FromCalendar(2029, 1, 1), costUsd: 3_400_000_000, durationDays: 600,
                petaflopDaysRequired: 1_400_000,
                requires: new[] { ResearchNodeId.ArtificialSuperintelligence },
                warning:
                "This is the last capability node in the game. What follows is not research into "
                + "what the models can do, it is research into who is depending on them."),

            new(ResearchNodeId.RealTimeAssimilation, ResearchEra.Statecraft,
                "Real-time assimilation",
                "Reading a country as it happens: every transaction, every shipment, every filing, "
                + "as one continuous input rather than as a report that arrives on Friday.",
                GameDate.FromCalendar(2029, 6, 1), costUsd: 900_000_000, durationDays: 360,
                petaflopDaysRequired: 400_000,
                requires: new[] { ResearchNodeId.GeneralIntelligence }),

            new(ResearchNodeId.SovereignLiaison, ResearchEra.Statecraft,
                "Sovereign liaison",
                "Clearances, a permanent office, and somebody whose whole job is the government's "
                + "phone number. Nobody signs a national contract with a company they have not been "
                + "auditing for years.",
                GameDate.FromCalendar(2029, 6, 1), costUsd: 420_000_000, durationDays: 300,
                petaflopDaysRequired: 90_000,
                requires: new[] { ResearchNodeId.GeneralIntelligence },
                warning:
                "Opens the state programme. A government will look at five years of your safety "
                + "record before it will talk, and that is the one gate in this game that money "
                + "cannot move."),

            new(ResearchNodeId.ContinuousOversight, ResearchEra.Statecraft,
                "Continuous oversight",
                "A second model whose only job is watching the first one, on the sectors that matter, "
                + "without ever being the one making the decision.",
                GameDate.FromCalendar(2030, 1, 1), costUsd: 1_100_000_000, durationDays: 420,
                petaflopDaysRequired: 300_000,
                requires: new[] { ResearchNodeId.SovereignLiaison },
                track: ResearchTrack.Safety),

            new(ResearchNodeId.RedundantInference, ResearchEra.Statecraft,
                "Redundant inference",
                "The state's workload runs on two independent paths so a shortfall degrades instead "
                + "of stopping. Costs capacity to have and saves the company when it runs out.",
                GameDate.FromCalendar(2030, 1, 1), costUsd: 1_400_000_000, durationDays: 400,
                petaflopDaysRequired: 380_000,
                requires: new[] { ResearchNodeId.RealTimeAssimilation },
                track: ResearchTrack.Safety),

            // ------------------------------------------- operations: the room, not the model
            //
            // **The first four nodes in this game that research the company.** Every other node in
            // the tree makes the model bigger, cheaper, safer or better shaped; a survey of all 55
            // found no exception, and the server room, the payroll, the fleet and the power bill
            // had nothing behind them at all.
            //
            // Each one moves a constant that already existed, which is the rule every node here
            // obeys: a node that needs a new mechanic underneath it means the mechanic is the work
            // and the node is decoration. `RoomUpgrades` is the one place they are read.
            //
            // All four are optionalTechnology. They are worth a great deal to a company that owns
            // a basement and worth nothing at all to one that rents, so the scripted operator in
            // PlayabilityTests skips them the same way it skips the architecture ladders, and the
            // balance suite goes on measuring the economy rather than measuring these.

            new(ResearchNodeId.RackTelemetry, ResearchEra.Scaling,
                "Rack telemetry",
                "Inlet and outlet probes on every cabinet, logged, so the room stops being a thing "
                + "you find out about when the throughput drops. What one more card would do to a "
                + "cabinet is arithmetic somebody has already done; this is putting it on the "
                + "panel before the card is fitted rather than after.",
                GameDate.FromCalendar(2023, 3, 1), costUsd: 900_000, durationDays: 45,
                petaflopDaysRequired: 40,
                warning: "Buys no capacity and no throughput. It buys knowing, which is only worth "
                    + "something to somebody who was about to guess.",
                optionalTechnology: true,
                track: ResearchTrack.Operations),

            new(ResearchNodeId.AirflowModelling, ResearchEra.Scaling,
                "Airflow modelling",
                "Simulate the room rather than the cabinet: where the cold aisle actually goes, "
                + "which vents are fighting each other, and how much of the extract is recirculated "
                + "warm air that never left. Every cabinet on the floor sheds a little more for it.",
                GameDate.FromCalendar(2023, 11, 1), costUsd: 2_700_000, durationDays: 90,
                petaflopDaysRequired: 160,
                requires: new[] { ResearchNodeId.RackTelemetry },
                warning: "Wide and shallow, against the fan's narrow and deep. It will not save a "
                    + "cabinet that is badly over its rating; it buys a slot back across a floor "
                    + "that is merely warm.",
                optionalTechnology: true,
                track: ResearchTrack.Operations),

            new(ResearchNodeId.LiquidLoops, ResearchEra.Autonomy,
                "Liquid loops",
                "Plumbing, pumps and a heat rejection loop for the immersion tanks. Air runs out "
                + "long before the silicon does, and a tank that is properly served stops caring "
                + "how hot this year's accelerator runs.",
                GameDate.FromCalendar(2024, 8, 1), costUsd: 6_300_000, durationDays: 130,
                petaflopDaysRequired: 340,
                requires: new[] { ResearchNodeId.AirflowModelling },
                warning: "Immersion cabinets only. It is the reason to buy the dearest cabinet in "
                    + "the catalog, which until now aged on exactly the same curve as the cheapest.",
                optionalTechnology: true,
                track: ResearchTrack.Operations),

            new(ResearchNodeId.OwnSubstation, ResearchEra.Autonomy,
                "Own substation",
                "The room stops being on the household meter. A transformer, a metered industrial "
                + "connection and the paperwork that goes with drawing that much power in a "
                + "residential street.",
                GameDate.FromCalendar(2025, 1, 1), costUsd: 4_500_000, durationDays: 150,
                petaflopDaysRequired: 90,
                requires: new[] { ResearchNodeId.RackTelemetry },
                warning: "The power bill is the one cost that grows with the thing the player is "
                    + "proudest of. This does nothing at all for a company that owns no room.",
                optionalTechnology: true,
                track: ResearchTrack.Operations)
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
