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
            Segments = new SegmentMarket(Rivals.Agents.Count);
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

        /// <summary>The founder's name. Shown wherever the company is not the subject.</summary>
        public string FounderName { get; set; } = "Anonymous";

        /// <summary>Where the company is registered. Chosen once at creation and never moved.</summary>
        public WorldRegion Region { get; set; } = WorldRegion.America;

        /// <summary>
        /// The country inside that region. It carries the actual numbers; the region is only how
        /// the player finds it on the map.
        /// </summary>
        public Country HomeCountry { get; set; } = Country.UnitedStates;

        /// <summary>The four modifiers the home country applies. Read, never stored.</summary>
        public CountryDefinition Home => WorldRegionCatalog.Get(HomeCountry);

        /// <summary>
        /// The founder's skills. The only progression in the game that is earned rather than bought,
        /// which is why it cannot be accelerated with money.
        /// </summary>
        public SkillSet Skills { get; } = new();

        /// <summary>Levels gained today, for the day report to surface. Cleared each tick.</summary>
        public List<PlayerSkill> SkillsLevelledToday { get; } = new();

        /// <summary>
        /// Grants experience and raises an event on a level up. Every call site names a real action:
        /// experience is never awarded for the passage of time alone, because that would reward
        /// leaving the game running.
        /// </summary>
        public void AwardSkill(PlayerSkill skill, long amount)
        {
            if (Skills.AddExperience(skill, amount) <= 0)
            {
                return;
            }

            SkillsLevelledToday.Add(skill);
            RaiseEvent(new CompanyEvent(
                CompanyEventType.SkillLevelled,
                Date,
                $"{PlayerSkillCatalog.Get(skill).DisplayName} is now level {Skills.Level(skill)}."));
        }

        /// <summary>Price a newly released model launches at, from the house style.</summary>
        public double DefaultPriceMultiplier { get; set; } = 1.0;

        /// <summary>Technology tree nodes already completed.</summary>
        public HashSet<ResearchNodeId> UnlockedResearch { get; }

        /// <summary>The node being researched, or null. One at a time.</summary>
        public ResearchProject ActiveResearch { get; set; }

        /// <summary>
        /// Research points banked. Earned by building things and by paying for them, spent on nodes.
        ///
        /// A stock rather than a rate, because the decision the player makes is when to spend it: a
        /// node held back is points kept for a more expensive one later.
        /// </summary>
        public double ResearchPoints { get; set; }

        /// <summary>Points earned on the last day simulated, for the counter and the bubbles.</summary>
        public double ResearchPointsToday { get; set; }

        public ResearchFundingMode ResearchFunding { get; set; } = ResearchFundingMode.Fixed;

        /// <summary>What a fixed budget pays each month. Starts at the smallest the slider offers.</summary>
        public long ResearchMonthlyUsd { get; set; } = ResearchBudget.MinimumMonthlyUsd;

        /// <summary>Share of revenue diverted to research when the mode is a revenue share.</summary>
        public double ResearchRevenueShare { get; set; }

        /// <summary>
        /// How well known the company is, audience by audience. Marketing buys this and nothing else.
        /// </summary>
        public Awareness Awareness { get; } = new();

        private readonly List<MarketingCampaign> campaigns = new();

        /// <summary>Campaigns currently booked, finished ones included until the tick clears them.</summary>
        public IReadOnlyList<MarketingCampaign> Campaigns => campaigns;

        public void AddCampaign(MarketingCampaign campaign)
        {
            if (campaign != null && campaign.Channels.Count > 0)
            {
                campaigns.Add(campaign);
            }
        }

        public bool RemoveCampaign(MarketingCampaign campaign) => campaigns.Remove(campaign);

        public void ClearCampaigns() => campaigns.Clear();

        public bool HasResearch(ResearchNodeId node) =>
            node == ResearchNodeId.None || UnlockedResearch.Contains(node);

        /// <summary>The live field of rival labs. Agents, not a lookup table.</summary>
        public CompetitorField Rivals { get; }

        /// <summary>
        /// Who is using what, per audience segment. Persisted, because a user base that reset on
        /// load would make every save a fresh market and quietly delete the whole point of it.
        /// </summary>
        public SegmentMarket Segments { get; private set; }

        /// <summary>Rebuilds the standing when the number of labs changes. Only saves need this.</summary>
        public void ResetSegments(int rivalCount) => Segments = new SegmentMarket(rivalCount);

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

        /// <summary>Everyone on the payroll and the room they work in.</summary>
        public StaffRoster Staff { get; } = new();

        /// <summary>How the company charges, how generous it is, and what it spends being noticed.</summary>
        public MonetizationPolicy Monetization { get; } = new();

        /// <summary>Tokens given away yesterday. The number that turns reach into a problem.</summary>
        public double FreeTokensServedBillions { get; set; }

        /// <summary>Lifetime tokens served to accounts that never paid anything.</summary>
        public double LifetimeFreeTokensBillions { get; set; }

        /// <summary>Public safety incidents, newest last. History, not a counter.</summary>
        public List<SafetyIncident> Incidents { get; } = new();

        /// <summary>Total paid in regulatory penalties. Investors ask about this.</summary>
        public long LifetimeFinesUsd { get; set; }

        /// <summary>Finished runs waiting for a release decision.</summary>
        public IReadOnlyList<TrainedModel> Shelf => shelf;

        /// <summary>Upgrade programmes in flight. They compete with training for the cluster.</summary>
        public IReadOnlyList<ModelUpgradeProject> UpgradeProjects => upgradeProjects;

        /// <summary>Advance warning bought so far, newest last.</summary>
        public IReadOnlyList<IntelSignal> Signals => signals;

        /// <summary>
        /// Which outfits are on retainer, each bought on its own. Billed monthly, all of them.
        ///
        /// This replaced a single tier on 2026-08-13. One tier could not express what the design
        /// needs: Event Hunter is sold by National Press and only opens for TrendSearch members, and
        /// with a single subscription "I hold the cheap one and the dear one" was not a state the
        /// company could be in.
        /// </summary>
        private readonly HashSet<IntelTier> memberships = new();

        public bool IsMember(IntelTier tier) => memberships.Contains(tier);

        public void SetMembership(IntelTier tier, bool joined)
        {
            if (tier == IntelTier.PublicNews)
            {
                return;
            }

            if (joined)
            {
                memberships.Add(tier);
            }
            else
            {
                memberships.Remove(tier);
            }
        }

        public IReadOnlyCollection<IntelTier> Memberships => memberships;

        /// <summary>
        /// The best desk being paid for, which is the one whose signals arrive.
        ///
        /// Holding three memberships does not mean three notes a month from three desks; it means the
        /// company hears what the best of them hears, plus the sections the others unlock. Otherwise
        /// buying everything would be strictly better than choosing, and the choice is the mechanic.
        /// </summary>
        public IntelTier BestMembership
        {
            get
            {
                var best = IntelTier.PublicNews;
                foreach (var tier in memberships)
                {
                    if (tier > best)
                    {
                        best = tier;
                    }
                }

                return best;
            }
        }

        /// <summary>What the company has read. Filled by the news desk, never by the interface.</summary>
        public NewsFeed News { get; } = new();

        /// <summary>Days until the desk files its next note. The best membership's clock.</summary>
        public int DaysUntilNextSignal { get; set; }

        /// <summary>
        /// A countdown per outfit, because each one files on its own schedule.
        ///
        /// One shared clock was a bug: signals were generated at the best membership only, so a
        /// company paying National Press and TrendSearch had everything routed to Total True News and
        /// Event Hunter, which needs both memberships to open, stayed empty forever. Paying four
        /// hundred and twenty thousand a month for a column that never fills is worse than not
        /// selling the column.
        /// </summary>
        private readonly int[] signalCountdowns = new int[4];

        public int CountdownFor(IntelTier tier) =>
            signalCountdowns[Math.Clamp((int)tier, 0, signalCountdowns.Length - 1)];

        public void SetCountdownFor(IntelTier tier, int days) =>
            signalCountdowns[Math.Clamp((int)tier, 0, signalCountdowns.Length - 1)] = Math.Max(0, days);

        public void CaptureCountdowns(List<int> into)
        {
            into.Clear();
            foreach (var days in signalCountdowns)
            {
                into.Add(days);
            }
        }

        public void RestoreCountdowns(IReadOnlyList<int> values)
        {
            for (var index = 0; index < signalCountdowns.Length; index++)
            {
                signalCountdowns[index] = values != null && index < values.Count
                    ? Math.Max(0, values[index])
                    : 0;
            }
        }

        /// <summary>Days until KnownWords files its next dossier.</summary>
        public int DaysUntilNextDossier { get; set; }

        /// <summary>
        /// Which rival the next dossier is about.
        ///
        /// A cursor rather than a roll, so the column works through the field in order and every lab
        /// gets written up. Picking at random would report whoever came up twice and leave the lab
        /// quietly waiting out a hardware cycle unmentioned for a year, which is the one thing this
        /// desk exists to catch.
        /// </summary>
        public int NextDossierLab { get; set; }

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

        /// <summary>
        /// The books. Written where the money moves, never recalculated, so the report and the bank
        /// balance cannot disagree.
        /// </summary>
        public Ledger Ledger { get; } = new();

        /// <summary>
        /// Moves cash and records why in one step.
        ///
        /// Cash used to be a public setter poked from a dozen places, which is exactly how a company
        /// ends up with a balance nobody can explain. Anything that still writes CashUsd directly is
        /// spending money the report will not know about.
        /// </summary>
        public void PostCash(LedgerLine line, long amountUsd)
        {
            if (amountUsd == 0L)
            {
                return;
            }

            var magnitude = Math.Abs(amountUsd);
            CashUsd += Ledger.Info(line).IsIncome ? magnitude : -magnitude;
            Ledger.Post(Date, line, magnitude);
        }

        /// <summary>Records something real that is not cash, depreciation being the only one today.</summary>
        public void PostNonCash(LedgerLine line, long amountUsd) =>
            Ledger.Post(Date, line, amountUsd);
        public ComputePool Pool { get; }
        public DeterministicRandom Random { get; }

        public IReadOnlyList<DeployedModel> DeployedModels => deployedModels;

        /// <summary>The run in flight, or null. Only one at a time: a company trains one model.</summary>
        public TrainingRun ActiveRun { get; set; }

        public DatasetSource OwnedDataSources { get; set; }
        public HashSet<ArchitectureId> AdoptedArchitectures { get; }

        public long LifetimeRevenueUsd { get; set; }
        public long LifetimeOperatingCostUsd { get; set; }

        /// <summary>Tax paid to date. Counted inside operating cost as well, this is the breakdown.</summary>
        public long LifetimeTaxPaidUsd { get; set; }
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

        /// <summary>
        /// People who follow the brand rather than the product.
        ///
        /// A separate stock from users on purpose. Users are whoever is on the service today and they
        /// leave the moment something better appears; fans stay attached between products, arrive
        /// slowly and leave slowly. A scandal can halve the public's opinion overnight and still leave
        /// most of the fans, which is why these cannot be the same number.
        /// </summary>
        public double Fans { get; set; }

        /// <summary>The day the most recent model went on sale, for judging whether the line is stale.</summary>
        public GameDate LastReleaseDate { get; set; } = GameDate.Start;

        /// <summary>What moved the company's standing on the last day simulated.</summary>
        public StandingChange LastStandingChange { get; set; }

        /// <summary>
        /// How the service held up on the last day simulated.
        ///
        /// **Persisted, and it has to be.** It looks derived and it is not: tomorrow's market reads
        /// it, so it is causal, and a save that dropped it replayed one day differently. That is the
        /// same mistake as the rival release date and the quantised drift, and it was caught by the
        /// same replay test.
        /// </summary>
        public ServiceQuality LastQuality { get; set; }

        /// <summary>Registered users on each of the last ninety days, for the charts.</summary>
        public UserHistory Users { get; } = new();

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

        /// <summary>
        /// Records something that happened, and files it as news in the same breath.
        ///
        /// Filing here rather than in the daily tick is what guarantees the feed cannot fall behind
        /// the simulation: there is one way to say a thing happened, and it always reaches the
        /// reader. The desk decides what is worth printing; most events are not.
        /// </summary>
        public void RaiseEvent(CompanyEvent companyEvent)
        {
            events.Enqueue(companyEvent);

            if (NewsDesk.TryFile(companyEvent, CompanyName, out var story))
            {
                News.Add(story);
            }
        }

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

        /// <summary>
        /// Whether the company is allowed to build this kind of model yet. General needs nothing;
        /// every other type is behind its own node, which is what makes choosing one a bet on when
        /// its audience turns up rather than a free preference.
        /// </summary>
        public bool CanBuildType(ModelType type)
        {
            var required = ModelTypeCatalog.Get(type).Requires;
            return required == ResearchNodeId.None || HasResearch(required);
        }

        public bool HasArchitecture(ArchitectureId architecture) => AdoptedArchitectures.Contains(architecture);
    }
}
