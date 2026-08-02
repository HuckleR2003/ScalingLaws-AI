using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Everything the company is, at one moment. Plain mutable state with no rules in it: the rules
    /// live in <see cref="CompanySimulation"/>, which is the only thing allowed to change any of it
    /// during a tick.
    ///
    /// Keeping the two apart is what makes an EditMode test able to build any situation directly,
    /// including ones a normal playthrough would take four years to reach.
    /// </summary>
    public sealed class CompanyState : IArchitectureSource
    {
        /// <summary>Seed round. Enough for one serious run in 2022 and not much else.</summary>
        public const long StartingCashUsd = 12_000_000;

        /// <summary>How far the account may go under before the lights go out.</summary>
        public const long CreditLineUsd = 5_000_000;

        /// <summary>Upgrade programmes that can run at once. Past this the research org thrashes.</summary>
        public const int MaximumConcurrentUpgrades = 3;

        /// <summary>Days of revenue kept to work out an annualised run rate for investors.</summary>
        public const int RevenueWindowDays = 30;

        private readonly List<DeployedModel> deployedModels = new();
        private readonly List<TrainedModel> shelf = new();
        private readonly List<ModelUpgradeProject> upgradeProjects = new();
        private readonly List<IntelSignal> signals = new();
        private readonly Queue<long> revenueWindow = new();
        private readonly Queue<CompanyEvent> events = new();
        private long revenueWindowTotal;
        private double trainingComputeShare = 0.7;
        private double reputation;

        public CompanyState(string companyName = "Newco", uint randomSeed = 0x5CA1AB1E)
        {
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? "Newco" : companyName.Trim();
            Date = GameDate.Start;
            CashUsd = StartingCashUsd;
            Pool = new ComputePool();
            Random = new DeterministicRandom(randomSeed);
            OwnedDataSources = DatasetCatalog.StartingSources;
            AdoptedArchitectures = new HashSet<ArchitectureId> { ArchitectureId.DenseTransformer };
            Reputation = 0.05;
            Rivals = CompetitorField.CreateFromCatalog();
            CapTable = new CapTable();
            UnlockedResearch = new HashSet<ResearchNodeId> { ResearchTree.StartingNode };
            Founder = FounderProfile.Neutral;
        }

        /// <summary>
        /// Builds a company from an opening screen choice. The archetype sets the starting position
        /// and contributes a house trait on top of the two the founder picked.
        /// </summary>
        public static CompanyState FromOpeningChoice(
            string companyName,
            CompanyArchetype archetype,
            FounderTrait first,
            FounderTrait second,
            uint randomSeed = 0x5CA1AB1E)
        {
            var identity = CompanyIdentityCatalog.Get(archetype);
            var state = new CompanyState(
                string.IsNullOrWhiteSpace(companyName) ? identity.DisplayName : companyName,
                randomSeed)
            {
                Archetype = archetype,
                CashUsd = identity.StartingCashUsd,
                Reputation = identity.StartingReputation,
                OwnedDataSources = identity.StartingData,
                DefaultPriceMultiplier = identity.PriceMultiplier
            };

            state.Founder = identity.HouseTrait == FounderTrait.None
                ? new FounderProfile(first, second)
                : new FounderProfile(first, second, identity.HouseTrait);

            return state;
        }

        /// <summary>Which opening tile this campaign started from.</summary>
        public CompanyArchetype Archetype { get; set; } = CompanyArchetype.Custom;

        /// <summary>The founder's traits, folded into multipliers. Fixed for the campaign.</summary>
        public FounderProfile Founder { get; set; }

        /// <summary>Price a newly released model launches at, from the house style.</summary>
        public double DefaultPriceMultiplier { get; set; } = 1.0;

        /// <summary>Technology tree nodes already completed.</summary>
        public HashSet<ResearchNodeId> UnlockedResearch { get; }

        /// <summary>The node being researched, or null. One at a time.</summary>
        public ResearchProject ActiveResearch { get; set; }

        public bool HasResearch(ResearchNodeId node) =>
            node == ResearchNodeId.None || UnlockedResearch.Contains(node);

        /// <summary>The live field of rival labs. Agents, not a lookup table.</summary>
        public CompetitorField Rivals { get; }

        private readonly Dictionary<ArchitectureId, ArchitectureDefinition> customArchitectures = new();
        private readonly Dictionary<ArchitectureId, int> familyGenerations = new();

        /// <summary>Families the company designed itself, by slot.</summary>
        public IReadOnlyDictionary<ArchitectureId, ArchitectureDefinition> CustomArchitectures => customArchitectures;

        /// <summary>The family research programme in flight, or null. One at a time.</summary>
        public ArchitectureProject ActiveArchitectureProject { get; set; }

        /// <summary>
        /// Resolves any architecture the company can use: its own families first, then the public
        /// catalog. This is the <see cref="IArchitectureSource"/> the whole simulation reads through.
        /// </summary>
        public bool TryGetArchitecture(ArchitectureId id, out ArchitectureDefinition definition)
        {
            return customArchitectures.TryGetValue(id, out definition)
                || ArchitectureCatalog.TryGet(id, out definition);
        }

        /// <summary>Same lookup, falling back to the dense baseline rather than throwing.</summary>
        public ArchitectureDefinition ResolveArchitecture(ArchitectureId id)
        {
            return TryGetArchitecture(id, out var definition) ? definition : ArchitectureCatalog.Baseline;
        }

        /// <summary>Writes a finished family into its slot and adopts it.</summary>
        public void StoreCustomArchitecture(ArchitectureId slot, ArchitectureDefinition definition, int generation)
        {
            if (!ArchitectureCatalog.IsCustomSlot(slot))
            {
                return;
            }

            customArchitectures[slot] = definition;
            familyGenerations[slot] = Math.Clamp(generation, 0, ArchitectureDesigner.MaximumGenerations);
            AdoptedArchitectures.Add(slot);
        }

        /// <summary>How many times a lineage has already been iterated. Zero for a clean sheet.</summary>
        public int FamilyGeneration(ArchitectureId id) =>
            familyGenerations.TryGetValue(id, out var generation) ? generation : 0;

        /// <summary>The first custom slot with nothing in it, or None when all six are used.</summary>
        public ArchitectureId FirstFreeArchitectureSlot()
        {
            foreach (var slot in ArchitectureCatalog.CustomSlots)
            {
                if (!customArchitectures.ContainsKey(slot))
                {
                    return slot;
                }
            }

            return ArchitectureId.None;
        }

        /// <summary>Who owns the company after every round raised so far.</summary>
        public CapTable CapTable { get; }

        /// <summary>Every loan being serviced. Non-dilutive money on a schedule that never pauses.</summary>
        public LoanBook Loans { get; } = new();

        /// <summary>Finished runs waiting for a release decision.</summary>
        public IReadOnlyList<TrainedModel> Shelf => shelf;

        /// <summary>Upgrade programmes in flight. They compete with training for the cluster.</summary>
        public IReadOnlyList<ModelUpgradeProject> UpgradeProjects => upgradeProjects;

        /// <summary>Advance warning bought so far, newest last.</summary>
        public IReadOnlyList<IntelSignal> Signals => signals;

        /// <summary>What the research desk is on retainer for. Billed monthly.</summary>
        public IntelTier IntelSubscription { get; set; } = IntelTier.PublicNews;

        /// <summary>Days until the desk files its next note.</summary>
        public int DaysUntilNextSignal { get; set; }

        /// <summary>The term sheet currently on the table, if any.</summary>
        public FundingOffer CurrentFundingOffer { get; set; }

        /// <summary>When the last round closed, so the next one is not offered the same week.</summary>
        public GameDate LastRoundClosedOn { get; set; } = new(GameDate.MinimumDayIndex);

        /// <summary>Annualised revenue from the trailing window. What investors actually price on.</summary>
        public long AnnualRevenueRunRateUsd =>
            SimUnits.ToDollars(revenueWindowTotal * (365.2425 / RevenueWindowDays));

        /// <summary>Records a day of revenue and drops whatever fell out of the window.</summary>
        public void RecordDailyRevenue(long revenueUsd)
        {
            var safe = Math.Max(0L, revenueUsd);
            revenueWindow.Enqueue(safe);
            revenueWindowTotal += safe;

            while (revenueWindow.Count > RevenueWindowDays)
            {
                revenueWindowTotal -= revenueWindow.Dequeue();
            }
        }

        public void AddToShelf(TrainedModel model)
        {
            if (model != null)
            {
                shelf.Add(model);
            }
        }

        public bool RemoveFromShelf(int index)
        {
            if (index < 0 || index >= shelf.Count)
            {
                return false;
            }

            shelf.RemoveAt(index);
            return true;
        }

        public void AddUpgradeProject(ModelUpgradeProject project)
        {
            if (project != null)
            {
                upgradeProjects.Add(project);
            }
        }

        public bool RemoveUpgradeProject(ModelUpgradeProject project) => upgradeProjects.Remove(project);

        public void AddSignal(IntelSignal signal) => signals.Add(signal);

        /// <summary>True when this trait already has a programme running on this model.</summary>
        public bool IsUpgradeInFlight(int modelIndex, ModelTrait trait)
        {
            foreach (var project in upgradeProjects)
            {
                if (project.ModelIndex == modelIndex && project.Trait == trait)
                {
                    return true;
                }
            }

            return false;
        }

        public string CompanyName { get; set; }
        public GameDate Date { get; set; }
        public long CashUsd { get; set; }
        public ComputePool Pool { get; }
        public DeterministicRandom Random { get; }

        public IReadOnlyList<DeployedModel> DeployedModels => deployedModels;

        /// <summary>The run in flight, or null. Only one at a time: a company trains one model.</summary>
        public TrainingRun ActiveRun { get; set; }

        public DatasetSource OwnedDataSources { get; set; }
        public HashSet<ArchitectureId> AdoptedArchitectures { get; }

        public long LifetimeRevenueUsd { get; set; }
        public long LifetimeOperatingCostUsd { get; set; }
        public long LifetimeCapitalSpentUsd { get; set; }

        /// <summary>Whether the datacenter shell has been paid for, and when it opens.</summary>
        public bool DatacenterOrdered { get; set; }
        public GameDate DatacenterReadyDate { get; set; }

        public bool IsBankrupt { get; set; }

        /// <summary>Days the account has been under water. Thirty is not fatal, forever is.</summary>
        public int DaysInDebt { get; set; }

        /// <summary>Share of the fleet pointed at training while a run is in flight, 0 to 1.</summary>
        public double TrainingComputeShare
        {
            get => trainingComputeShare;
            set => trainingComputeShare = Math.Clamp(SimUnits.Finite(value, 0.7), 0.0, 1.0);
        }

        /// <summary>How much the name is worth in the demand split, 0 to 1.</summary>
        public double Reputation
        {
            get => reputation;
            set => reputation = Math.Clamp(SimUnits.Finite(value), 0.0, 1.0);
        }

        public int ReleasedModelCount => deployedModels.Count;

        /// <summary>Best measured capability the company has live. Projections never count here.</summary>
        public double BestCapability
        {
            get
            {
                var best = 0.0;
                foreach (var model in deployedModels)
                {
                    if (!model.IsLiveOn(Date))
                    {
                        continue;
                    }

                    var capability = model.EffectiveCapability(Date);
                    if (capability > best)
                    {
                        best = capability;
                    }
                }

                return best;
            }
        }

        public bool IsDatacenterOnline => DatacenterOrdered && Date.IsOnOrAfter(DatacenterReadyDate);

        public void AddDeployedModel(DeployedModel model)
        {
            if (model != null)
            {
                deployedModels.Add(model);
            }
        }

        public void ClearDeployedModels() => deployedModels.Clear();

        public void RaiseEvent(CompanyEvent companyEvent) => events.Enqueue(companyEvent);

        public bool TryDequeueEvent(out CompanyEvent companyEvent)
        {
            if (events.Count == 0)
            {
                companyEvent = default;
                return false;
            }

            companyEvent = events.Dequeue();
            return true;
        }

        public int PendingEventCount => events.Count;

        public void ClearEvents() => events.Clear();

        /// <summary>The compute tier ladder with every gate evaluated against the company right now.</summary>
        public List<ComputeTierStatus> ComputeTierLadder()
        {
            var ladder = ComputeTierCatalog.EvaluateAll(Date, CashUsd, ReleasedModelCount, LifetimeRevenueUsd);
            for (var index = 0; index < ladder.Count; index++)
            {
                ladder[index] = ApplyResearchGate(ladder[index]);
            }

            return ladder;
        }

        /// <summary>
        /// A tier can be affordable and still shut. Owning hardware needs the scaling laws worked
        /// out; a datacenter needs the programme researched. Money alone opens nothing past renting.
        /// </summary>
        private ComputeTierStatus ApplyResearchGate(ComputeTierStatus status)
        {
            var gate = ResearchTree.GateForTier(status.Tier);
            if (gate == ResearchNodeId.None || HasResearch(gate))
            {
                return status;
            }

            var requirement = $"the {ResearchTree.Get(gate).DisplayName} research";
            return new ComputeTierStatus(
                status.Tier,
                false,
                status.IsUnlocked
                    ? $"Needs {requirement}."
                    : status.LockReason.TrimEnd('.') + $", and {requirement}.");
        }

        public bool IsTierUnlocked(ComputeTier tier)
        {
            if (!ComputeTierCatalog.TryGet(tier, out var definition))
            {
                return false;
            }

            var status = ApplyResearchGate(
                definition.Evaluate(Date, CashUsd, ReleasedModelCount, LifetimeRevenueUsd));
            return status.IsUnlocked;
        }

        public bool HasDataSource(DatasetSource source) => (OwnedDataSources & source) == source;

        public bool HasArchitecture(ArchitectureId architecture) => AdoptedArchitectures.Contains(architecture);
    }
}
