using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The ONE thing allowed to change <see cref="CompanyState"/>. Every player action and the whole
    /// daily tick go through here, which is why a test can drive four years of company history
    /// without a scene, a MonoBehaviour or a frame.
    ///
    /// The day, in order: deliveries land, the run consumes compute, the market splits demand, the
    /// bills come out, the gates get re-checked. Nothing in that order is negotiable, because a
    /// cluster that arrives in the morning should serve tokens the same afternoon.
    /// </summary>
    public sealed class CompanySimulation
    {
        /// <summary>
        /// Share of nameplate throughput inference actually reaches. Far below training, because
        /// serving is bound by memory bandwidth rather than arithmetic.
        /// </summary>
        public const double InferenceUtilization = 0.06;

        /// <summary>Spread of the finished capability around its projection, in capability points.</summary>
        public const double TrainingOutcomeStandardDeviation = 1.2;

        public const double ReputationDailyDecay = 0.0006;
        public const double ReputationServiceGain = 0.0012;
        public const double ReputationReleaseGain = 0.10;

        /// <summary>Relative pull each research consumer has on the cluster when several are running.</summary>
        public const double RunComputeWeight = 3.0;

        public const double UpgradeComputeWeight = 1.2;
        public const double ArchitectureComputeWeight = 1.0;
        public const double ResearchComputeWeight = 1.4;

        private const double DaysPerMonth = 30.4375;

        public CompanySimulation(CompanyState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public CompanyState State { get; }

        /// <summary>
        /// Today's world, with the frontier read from the live rival field rather than the reference
        /// table. Everything else in it is a pure function of the date.
        /// </summary>
        public MarketConditions Market =>
            MarketModel.Evaluate(State.Date, State.Rivals.FrontierCapability(State.Date));

        public ComputeProfile Profile => State.Pool.BuildProfile(State.Date, Market);

        // ------------------------------------------------------------------ tick

        /// <summary>Runs one day and returns what happened.</summary>
        public DayReport AdvanceDay()
        {
            if (State.IsBankrupt)
            {
                return BuildReport(0.0, 0.0, 0.0, 0L, 0L, 0L);
            }

            var previousLadder = State.ComputeTierLadder();

            State.Date = State.Date.AddDays(1);

            // Rivals move first. The player reacts to a world that has already changed today, not to
            // one waiting politely for their turn.
            RunRivals();

            var market = MarketModel.Evaluate(State.Date, State.Rivals.FrontierCapability(State.Date));
            var profile = State.Pool.BuildProfile(State.Date, market);

            State.SkillsLevelledToday.Clear();


            State.Staff.SaturationMultiplier = State.Skills.TeamSaturationMultiplier();


            ReportDeliveries();
            SyncPricing(market);

            var trainingProgress = AdvanceResearch(profile);
            var (share, demanded, served, revenue) = ServeMarket(profile, market);

            var operatingCost =
                SimUnits.ToDollars(profile.DailyOperatingCostUsd * State.Founder.OperatingCostMultiplier * State.Skills.OperatingCostMultiplier())
                + DailyIntelRetainerUsd()
                + SimUnits.ToDollars(State.Staff.DailyCostUsd * State.Founder.OperatingCostMultiplier)
                + State.Monetization.TotalMarketingDailyUsd;
            var depreciation = SimUnits.ToDollars(profile.DailyDepreciationUsd);

            // Tax is charged on profit, not on turnover, so a loss-making year is not made worse
            // by where the company is registered. It is the only cost in the game the player can
            // reduce by choosing a place rather than by spending money.
            var taxable = Math.Max(0L, revenue - operatingCost);
            var tax = (long)Math.Round(taxable * State.Home.TaxRate);

            State.CashUsd += revenue - operatingCost - tax;
            State.LifetimeRevenueUsd += revenue;
            State.LifetimeOperatingCostUsd += operatingCost + tax;
            State.LifetimeTaxPaidUsd += tax;
            State.RecordDailyRevenue(revenue);

            AdvanceMarketing();
            RollSafetyIncident();
            ServiceDebt();
            AdvanceIntelligence();
            ExpireFundingOffer();
            UpdateReputation(share, served);
            ReportNewlyUnlockedTiers(previousLadder);
            CheckSolvency();

            return BuildReport(share, demanded, served, revenue, operatingCost, depreciation, trainingProgress);
        }

        /// <summary>Runs several days. Stops early on bankruptcy and returns the last day reached.</summary>
        public DayReport Advance(int days)
        {
            var report = BuildReport(0.0, 0.0, 0.0, 0L, 0L, 0L);
            var safeDays = Math.Clamp(days, 0, 20_000);
            for (var index = 0; index < safeDays; index++)
            {
                report = AdvanceDay();
                if (State.IsBankrupt)
                {
                    break;
                }
            }

            return report;
        }

        // ------------------------------------------------------------------ player actions

        /// <summary>
        /// Projects a blueprint against the fleet as it stands today. Pure: it changes nothing.
        /// </summary>
        public TrainingProjection Project(ModelBlueprint blueprint)
        {
            return TrainingPlanner.Project(
                blueprint,
                Profile,
                Market,
                State.BestCapability,
                State.TrainingComputeShare,
                State,
                State.Founder.DataSupplyMultiplier,
                State.Staff.DataQualityMultiplier() * State.Skills.DataQualityMultiplier());
        }

        /// <summary>
        /// A day count after both the founder's pace and the research team's. Every duration in the
        /// game goes through here so the two never get applied twice or forgotten once.
        /// </summary>
        public int ScaleResearchDuration(int days) => Math.Max(
            1,
            (int)Math.Round(State.Founder.ScaleDuration(days) * State.Staff.ResearchSpeedMultiplier()
                / State.Home.InnovationMultiplier));

        /// <summary>
        /// Commits to a run. Fails, with a reason, when the company does not own what the blueprint
        /// asks for or when the plan is not physically possible.
        /// </summary>
        public bool TryStartTraining(ModelBlueprint blueprint, out string failureReason)
        {
            if (!State.CanBuildType(blueprint.Type))
            {
                failureReason = $"{ModelTypeCatalog.Get(blueprint.Type).DisplayName} models need "
                    + $"{ResearchTree.Get(ModelTypeCatalog.Get(blueprint.Type).Requires).DisplayName} first.";
                return false;
            }

            failureReason = string.Empty;

            if (State.IsBankrupt)
            {
                failureReason = "The company is insolvent.";
                return false;
            }

            if (State.ActiveRun != null)
            {
                failureReason = "A training run is already in flight.";
                return false;
            }

            if (!State.HasArchitecture(blueprint.Architecture))
            {
                failureReason = $"{blueprint.Architecture} has not been adopted.";
                return false;
            }

            var missingData = blueprint.DataSources & ~State.OwnedDataSources;
            if (missingData != DatasetSource.None)
            {
                failureReason = $"The company does not own {missingData}.";
                return false;
            }

            var projection = Project(blueprint);
            if (!projection.IsFeasible)
            {
                failureReason = projection.BlockingReason;
                return false;
            }

            State.ActiveRun = new TrainingRun(
                blueprint,
                State.Date,
                projection.TrainingPetaflopDays,
                projection.ProjectedCapability,
                projection.Blend.AvailableTokensBillions,
                0L);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.TrainingStarted,
                State.Date,
                $"Started {blueprint.Name}: {projection.TrainingPetaflopDays:N0} PF-days, about {projection.TrainingDays} days at today's fleet.",
                projection.ComputeCashCostUsd));

            return true;
        }

        /// <summary>Cancels the run in flight. The compute already burned does not come back.</summary>
        public bool CancelTraining()
        {
            if (State.ActiveRun == null)
            {
                return false;
            }

            State.ActiveRun = null;
            return true;
        }

        /// <summary>Contracts for a given throughput. Billed daily, cancellable daily.</summary>
        public void SetRentedPetaflops(double petaflops) => State.Pool.SetRentedPetaflops(petaflops);

        /// <summary>
        /// Same thing expressed in units of whatever the clouds are renting today. Convenient for a
        /// slider; the contract underneath is still capacity.
        /// </summary>
        public void SetRentedAccelerators(int units) =>
            State.Pool.SetRentedAcceleratorEquivalent(units, Market.RentableGeneration);

        /// <summary>
        /// Buys hardware into a tier. Charges cash immediately; the units arrive after the tier's
        /// lead time and produce nothing until they do.
        /// </summary>
        public bool TryBuyHardware(HardwareGenerationId generationId, int units, ComputeTier tier, out string failureReason)
        {
            failureReason = string.Empty;

            if (units <= 0)
            {
                failureReason = "Nothing to buy.";
                return false;
            }

            if (!HardwareCatalog.TryGet(generationId, out var generation))
            {
                failureReason = "Unknown hardware.";
                return false;
            }

            if (!ComputeTierCatalog.TryGet(tier, out var tierDefinition) || tierDefinition.IsRented)
            {
                failureReason = "Hardware can only be bought into a tier the company operates.";
                return false;
            }

            var status = tierDefinition.Evaluate(State.Date, State.CashUsd, State.ReleasedModelCount, State.LifetimeRevenueUsd);
            if (!status.IsUnlocked)
            {
                failureReason = status.LockReason;
                return false;
            }

            if (!generation.IsAvailableOn(State.Date))
            {
                failureReason = $"{generation.DisplayName} does not ship until {generation.ReleaseDate}.";
                return false;
            }

            if (tier == ComputeTier.OwnDatacenter && !State.IsDatacenterOnline)
            {
                failureReason = State.DatacenterOrdered
                    ? $"The datacenter opens on {State.DatacenterReadyDate}."
                    : "No datacenter has been commissioned.";
                return false;
            }

            var pricePerUnit = SimUnits.ToDollars(
                MarketModel.PurchasePricePerUnitUsd(generation, tierDefinition, MarketModel.ScarcityOn(State.Date))
                * State.Founder.HardwarePriceMultiplier
                * State.Home.HardwarePriceMultiplier);
            var total = pricePerUnit * units;
            if (State.CashUsd < total)
            {
                failureReason = $"Needs ${total:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            var capacityAfter = State.Pool.PowerCapacityKilowatts();
            if (!HasAssetsInTier(tier))
            {
                capacityAfter += tierDefinition.PowerCapacityKilowatts;
            }

            var drawAfter = Profile.PowerDrawKilowatts + generation.PowerKilowatts * units;
            if (drawAfter > capacityAfter)
            {
                failureReason = $"Draws {drawAfter:N0} kW, the site provides {capacityAfter:N0} kW.";
                return false;
            }

            State.CashUsd -= total;
            State.LifetimeCapitalSpentUsd += total;
            State.Pool.AddAsset(new HardwareAsset(
                generationId,
                tier,
                units,
                State.Date,
                pricePerUnit,
                tierDefinition.LeadTimeDays));

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.HardwareOrdered,
                State.Date,
                $"Ordered {units:N0}x {generation.DisplayName} for {tierDefinition.DisplayName}, arriving in {tierDefinition.LeadTimeDays} days.",
                total));

            return true;
        }

        /// <summary>Sells part of a batch at today's resale value, which is rarely what was paid.</summary>
        public bool TrySellHardware(int assetIndex, int units, out long proceedsUsd, out string failureReason)
        {
            proceedsUsd = 0L;
            failureReason = string.Empty;

            if (assetIndex < 0 || assetIndex >= State.Pool.Assets.Count)
            {
                failureReason = "No such batch.";
                return false;
            }

            var asset = State.Pool.Assets[assetIndex];
            var sellUnits = Math.Clamp(units, 0, asset.Units);
            if (sellUnits <= 0)
            {
                failureReason = "Nothing to sell.";
                return false;
            }

            var perUnit = HardwareValuation.ResidualValuePerUnitUsd(
                asset.GenerationId,
                asset.PurchasePricePerUnitUsd,
                asset.PurchaseDate,
                State.Date);

            proceedsUsd = SimUnits.ToDollars(perUnit * sellUnits);
            State.CashUsd += proceedsUsd;
            State.Pool.ReplaceAssetAt(assetIndex, asset.WithUnits(asset.Units - sellUnits));

            var recovered = asset.PurchasePricePerUnitUsd * sellUnits;
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.HardwareSold,
                State.Date,
                $"Sold {sellUnits:N0}x {asset.GenerationId} for ${proceedsUsd:N0}, against ${recovered:N0} paid.",
                proceedsUsd));

            return true;
        }

        /// <summary>Signs for the building. Money leaves now, the site opens after the lead time.</summary>
        public bool TryOrderDatacenter(out string failureReason)
        {
            failureReason = string.Empty;

            if (State.DatacenterOrdered)
            {
                failureReason = "A datacenter is already commissioned.";
                return false;
            }

            var definition = ComputeTierCatalog.Get(ComputeTier.OwnDatacenter);
            var status = definition.Evaluate(State.Date, State.CashUsd, State.ReleasedModelCount, State.LifetimeRevenueUsd);
            if (!status.IsUnlocked)
            {
                failureReason = status.LockReason;
                return false;
            }

            if (State.CashUsd < definition.FacilityCapexUsd)
            {
                failureReason = $"Needs ${definition.FacilityCapexUsd:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            State.CashUsd -= definition.FacilityCapexUsd;
            State.LifetimeCapitalSpentUsd += definition.FacilityCapexUsd;
            State.DatacenterOrdered = true;
            State.DatacenterReadyDate = State.Date.AddDays(definition.LeadTimeDays);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.HardwareOrdered,
                State.Date,
                $"Datacenter commissioned. First power on {State.DatacenterReadyDate}.",
                definition.FacilityCapexUsd));

            return true;
        }

        public bool TryAdoptArchitecture(ArchitectureId architectureId, out string failureReason)
        {
            failureReason = string.Empty;

            if (!ArchitectureCatalog.TryGet(architectureId, out var architecture))
            {
                failureReason = "Unknown architecture.";
                return false;
            }

            if (State.HasArchitecture(architectureId))
            {
                failureReason = "Already adopted.";
                return false;
            }

            if (!architecture.IsAvailableOn(State.Date))
            {
                failureReason = $"Not published until {architecture.AvailableFrom}.";
                return false;
            }

            var gate = ResearchTree.GateForArchitecture(architectureId);
            if (!State.HasResearch(gate))
            {
                failureReason = $"Needs the {ResearchTree.Get(gate).DisplayName} research first.";
                return false;
            }

            if (State.CashUsd < architecture.AdoptionCostUsd)
            {
                failureReason = $"Needs ${architecture.AdoptionCostUsd:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            State.CashUsd -= architecture.AdoptionCostUsd;
            State.AdoptedArchitectures.Add(architectureId);
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ArchitectureAdopted,
                State.Date,
                $"Adopted {architecture.DisplayName}.",
                architecture.AdoptionCostUsd));

            return true;
        }

        public bool TryAcquireDataSource(DatasetSource source, out string failureReason)
        {
            failureReason = string.Empty;

            if (!DatasetCatalog.TryGet(source, out var definition))
            {
                failureReason = "Unknown data source.";
                return false;
            }

            if (State.HasDataSource(source))
            {
                failureReason = "Already owned.";
                return false;
            }

            if (!definition.IsAvailableOn(State.Date, State.BestCapability))
            {
                failureReason = State.Date.IsBefore(definition.AvailableFrom)
                    ? $"Not available until {definition.AvailableFrom}."
                    : $"Needs a live model at capability {definition.RequiredOwnedCapability:0}.";
                return false;
            }

            var dataGate = ResearchTree.GateForData(source);
            if (!State.HasResearch(dataGate))
            {
                failureReason = $"Needs the {ResearchTree.Get(dataGate).DisplayName} research first.";
                return false;
            }

            if (State.CashUsd < definition.AcquisitionCostUsd)
            {
                failureReason = $"Needs ${definition.AcquisitionCostUsd:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            State.CashUsd -= definition.AcquisitionCostUsd;
            State.OwnedDataSources |= source;
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.DataSourceAcquired,
                State.Date,
                $"Acquired {definition.DisplayName}.",
                definition.AcquisitionCostUsd));

            return true;
        }

        // ------------------------------------------------------------------ release timing

        /// <summary>
        /// Ships something off the shelf at a chosen price. Every day it waited, market par rose
        /// under it, so the capability it launches with is the capability it had minus the slippage.
        /// </summary>
        public bool TryReleaseModel(int shelfIndex, double priceMultiplier, out string failureReason)
        {
            failureReason = string.Empty;

            if (shelfIndex < 0 || shelfIndex >= State.Shelf.Count)
            {
                failureReason = "Nothing on the shelf at that slot.";
                return false;
            }

            var shelved = State.Shelf[shelfIndex];
            var model = shelved.Release(State.Date, priceMultiplier);

            // A founder who has been arguing about safety since 2021 ships models that are already
            // ahead of the market on it. Everyone else ships at par.
            if (State.Founder.SafetyHeadStart > 0)
            {
                model.Traits.SetLevel(
                    ModelTrait.Safety,
                    model.Traits.GetLevel(ModelTrait.Safety) + State.Founder.SafetyHeadStart);
            }

            State.AddDeployedModel(model);
            State.RemoveFromShelf(shelfIndex);

            var market = Market;
            var slippage = shelved.ParSlippage(State.Date);
            var waited = shelved.DaysOnShelf(State.Date);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ModelReleased,
                State.Date,
                waited > 0
                    ? $"{model.Name} is live after {waited} days on the shelf. Par moved {slippage:0.0} capability against it while it waited. Frontier stands at {market.FrontierCapability:0.0}."
                    : $"{model.Name} is live. Frontier stands at {market.FrontierCapability:0.0}."));

            State.AwardSkill(PlayerSkill.Management, 480);


            State.AwardSkill(PlayerSkill.Teamwork, 260);



            var relativeStanding = market.FrontierCapability <= 0.0
                ? 1.0
                : Math.Clamp(model.EffectiveCapability(State.Date) / market.FrontierCapability, 0.0, 1.3);
            State.Reputation += ReputationReleaseGain * relativeStanding;

            return true;
        }

        // ------------------------------------------------------------------ upgrades

        /// <summary>Everything the upgrade grid needs for one live model.</summary>
        public List<TraitStanding> UpgradeGrid(int modelIndex)
        {
            if (modelIndex < 0 || modelIndex >= State.DeployedModels.Count)
            {
                return new List<TraitStanding>();
            }

            return State.DeployedModels[modelIndex].Traits.Standings(State.Date);
        }

        /// <summary>
        /// Commissions one level of one trait. Cash goes now, compute and calendar are consumed as
        /// the programme runs, and it competes with the training run for the cluster.
        /// </summary>
        public bool TryStartUpgrade(int modelIndex, ModelTrait trait, out string failureReason)
        {
            failureReason = string.Empty;

            if (State.IsBankrupt)
            {
                failureReason = "The company is insolvent.";
                return false;
            }

            if (modelIndex < 0 || modelIndex >= State.DeployedModels.Count)
            {
                failureReason = "No such model.";
                return false;
            }

            if (!ModelTraitCatalog.TryGet(trait, out var definition))
            {
                failureReason = "Unknown trait.";
                return false;
            }

            if (!definition.IsAvailableOn(State.Date))
            {
                failureReason = $"{definition.DisplayName} is not a solved problem until {definition.AvailableFrom}.";
                return false;
            }

            var traitGate = ResearchTree.GateForTrait(trait);
            if (!State.HasResearch(traitGate))
            {
                failureReason = $"Needs the {ResearchTree.Get(traitGate).DisplayName} research first.";
                return false;
            }

            if (State.UpgradeProjects.Count >= CompanyState.MaximumConcurrentUpgrades)
            {
                failureReason = $"Already running {CompanyState.MaximumConcurrentUpgrades} upgrade programmes.";
                return false;
            }

            if (State.IsUpgradeInFlight(modelIndex, trait))
            {
                failureReason = $"{definition.DisplayName} is already being worked on for this model.";
                return false;
            }

            var model = State.DeployedModels[modelIndex];
            var level = model.Traits.GetLevel(trait);
            if (level >= ModelTraitSetLimits.MaximumLevel)
            {
                failureReason = $"{definition.DisplayName} is already at the ceiling.";
                return false;
            }

            var cost = definition.UpgradeCostUsd(level);
            if (State.CashUsd < cost)
            {
                failureReason = $"Needs ${cost:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            State.CashUsd -= cost;
            State.AddUpgradeProject(new ModelUpgradeProject(
                modelIndex,
                trait,
                level + 1,
                State.Date,
                ScaleResearchDuration(definition.UpgradeDays(level)),
                definition.UpgradePetaflopDays(level),
                cost));

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.UpgradeStarted,
                State.Date,
                $"{model.Name}: {definition.DisplayName} to level {level + 1}, about {definition.UpgradeDays(level)} days.",
                cost));

            return true;
        }

        // ------------------------------------------------------------------ technology tree

        /// <summary>The whole tree with every node evaluated. Locked nodes always appear.</summary>
        public List<ResearchStanding> ResearchBoard()
        {
            var board = new List<ResearchStanding>(ResearchTree.All.Count);
            foreach (var node in ResearchTree.All)
            {
                board.Add(EvaluateResearch(node));
            }

            return board;
        }

        private ResearchStanding EvaluateResearch(ResearchNode node)
        {
            var duration = ScaleResearchDuration(node.DurationDays);
            var unlocked = State.UnlockedResearch.Contains(node.Id);
            var inProgress = State.ActiveResearch != null && State.ActiveResearch.Node == node.Id;

            if (unlocked)
            {
                return new ResearchStanding(node, true, false, false, "already done", duration);
            }

            if (inProgress)
            {
                return new ResearchStanding(node, false, true, false, "in progress", duration);
            }

            foreach (var prerequisite in node.Prerequisites)
            {
                if (!State.UnlockedResearch.Contains(prerequisite))
                {
                    return new ResearchStanding(node, false, false, false,
                        $"needs {ResearchTree.Get(prerequisite).DisplayName}", duration);
                }
            }

            if (!node.IsAvailableOn(State.Date))
            {
                return new ResearchStanding(node, false, false, false,
                    $"the field gets here around {node.EarliestDate}", duration);
            }

            if (State.ActiveResearch != null)
            {
                return new ResearchStanding(node, false, false, false, "another programme is running", duration);
            }

            if (State.CashUsd < node.CostUsd)
            {
                return new ResearchStanding(node, false, false, false,
                    $"needs ${node.CostUsd:N0}", duration);
            }

            return new ResearchStanding(node, false, false, true, string.Empty, duration);
        }

        /// <summary>Commits to a node. Cash now, calendar and compute as it runs.</summary>
        public bool TryStartResearch(ResearchNodeId nodeId, out string failureReason)
        {
            failureReason = string.Empty;

            if (!ResearchTree.TryGet(nodeId, out var node))
            {
                failureReason = "Unknown research.";
                return false;
            }

            var standing = EvaluateResearch(node);
            if (!standing.CanStart)
            {
                failureReason = standing.BlockedReason;
                return false;
            }

            State.CashUsd -= node.CostUsd;
            State.ActiveResearch = new ResearchProject(
                nodeId, State.Date, standing.DurationDays, node.PetaflopDaysRequired, node.CostUsd);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ResearchStarted,
                State.Date,
                node.HasWarning
                    ? $"{node.DisplayName} begun. {node.Warning}"
                    : $"{node.DisplayName} begun, about {standing.DurationDays} days.",
                node.CostUsd));

            return true;
        }

        private void AdvanceResearchNode(double petaflopDays)
        {
            var project = State.ActiveResearch;
            if (project == null)
            {
                return;
            }

            project.Advance(petaflopDays);
            if (!project.IsComplete)
            {
                return;
            }

            State.UnlockedResearch.Add(project.Node);
            State.ActiveResearch = null;

            var node = ResearchTree.Get(project.Node);


            State.AwardSkill(PlayerSkill.Concept, 620);


            State.AwardSkill(PlayerSkill.DataEngineering, 200);
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ResearchCompleted,
                State.Date,
                $"{node.DisplayName} is done.",
                project.CashPaidUsd));
        }

        // ------------------------------------------------------------------ architecture families

        /// <summary>Prices a family research programme against the company as it stands.</summary>
        public ArchitectureProjection ProjectArchitecture(ArchitectureBlueprint blueprint)
        {
            return ArchitectureDesigner.Project(
                blueprint,
                State.Date,
                State,
                State.CashUsd,
                blueprint.IsIteration ? State.FamilyGeneration(blueprint.BaseFamily) + 1 : 0,
                State.Skills.ResearchDepthMultiplier());
        }

        /// <summary>
        /// Commits to designing a family. Cash goes now; the result is not known until it lands, and
        /// a cheap rushed programme is close to a coin toss.
        /// </summary>
        public bool TryStartArchitectureProgramme(ArchitectureBlueprint blueprint, out string failureReason)
        {
            failureReason = string.Empty;

            if (State.IsBankrupt)
            {
                failureReason = "The company is insolvent.";
                return false;
            }

            if (State.ActiveArchitectureProject != null)
            {
                failureReason = "A family programme is already running.";
                return false;
            }

            var projection = ProjectArchitecture(blueprint);
            if (!projection.IsFeasible)
            {
                failureReason = projection.BlockingReason;
                return false;
            }

            var cash = ArchitectureDesigner.CashCostUsd(blueprint);
            State.CashUsd -= cash;

            var generation = blueprint.IsIteration ? State.FamilyGeneration(blueprint.BaseFamily) + 1 : 0;
            State.ActiveArchitectureProject = new ArchitectureProject(
                blueprint,
                State.Date,
                ScaleResearchDuration(ArchitectureDesigner.DurationDays(blueprint)),
                projection.PetaflopDaysRequired,
                cash,
                projection.ResearchPower,
                projection.Variance,
                projection.Baseline,
                generation);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ArchitectureResearchStarted,
                State.Date,
                $"{blueprint.Name}: family programme started, about {ArchitectureDesigner.DurationDays(blueprint)} days.",
                cash));

            return true;
        }

        /// <summary>Abandons the programme. The money and the compute already spent do not come back.</summary>
        public bool CancelArchitectureProgramme()
        {
            if (State.ActiveArchitectureProject == null)
            {
                return false;
            }

            State.ActiveArchitectureProject = null;
            return true;
        }

        private void AdvanceArchitecture(double petaflopDays)
        {
            var project = State.ActiveArchitectureProject;
            if (project == null)
            {
                return;
            }

            project.Advance(petaflopDays);
            if (!project.IsComplete)
            {
                return;
            }

            var resolved = ArchitectureDesigner.Resolve(project.Projection(), State.Date, State.Random);
            State.StoreCustomArchitecture(project.Blueprint.Slot, resolved, project.Generation);
            State.ActiveArchitectureProject = null;

            var baseline = project.Baseline;
            var saving = baseline.ActiveParameterFraction <= 0.0
                ? 0.0
                : 1.0 - resolved.ActiveParameterFraction / baseline.ActiveParameterFraction;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ArchitectureResearchCompleted,
                State.Date,
                $"{resolved.DisplayName} is ready: {resolved.ActiveParameterFraction:0.000} active fraction "
                + $"({saving:P0} off the compute bill), {resolved.CapabilityBonus:0.0} capability bonus, "
                + $"serving at {resolved.InferenceCostMultiplier:0.00}x.",
                project.CashPaidUsd));
        }

        // ------------------------------------------------------------------ funding

        /// <summary>The round the company could raise next, and whether it can.</summary>
        public FundingAvailability NextRoundAvailability()
        {
            var stage = FundingCatalog.NextStageAfter(State.CapTable.LastStage);
            if (stage == FundingStage.None)
            {
                return new FundingAvailability(stage, false, FundingRefusal.AlreadyRaised, "No further rounds.");
            }

            if (State.IsBankrupt)
            {
                return new FundingAvailability(stage, false, FundingRefusal.Insolvent, "The company is insolvent.");
            }

            if (State.CurrentFundingOffer.IsOpen)
            {
                return new FundingAvailability(stage, false, FundingRefusal.RoundAlreadyOpen,
                    "A term sheet is already on the table.");
            }

            var cooldownEnds = State.LastRoundClosedOn.AddDays(FundingMarket.CooldownDays);
            if (State.CapTable.RoundCount > 0 && State.Date.IsBefore(cooldownEnds))
            {
                return new FundingAvailability(stage, false, FundingRefusal.TooEarly,
                    $"Investors will not reopen until {cooldownEnds}.");
            }

            return FundingMarket.Evaluate(
                stage,
                State.Date,
                State.BestCapability,
                Market.FrontierCapability,
                State.AnnualRevenueRunRateUsd,
                State.ReleasedModelCount);
        }

        /// <summary>What the market would value the company at right now, before any new money.</summary>
        public long CurrentValuationUsd() => SimUnits.ToDollars(
            FundingMarket.PreMoneyValuationUsd(
                State.Date,
                State.BestCapability,
                Market.FrontierCapability,
                State.AnnualRevenueRunRateUsd)
            * State.Founder.ValuationMultiplier);

        /// <summary>Opens a round. The term sheet then sits on the table until signed or lapsed.</summary>
        public bool TryOpenFundingRound(out string failureReason)
        {
            failureReason = string.Empty;

            var availability = NextRoundAvailability();
            if (!availability.IsAvailable)
            {
                failureReason = availability.Reason;
                return false;
            }

            var offer = FundingMarket.BuildOffer(
                availability.Stage,
                State.Date,
                State.BestCapability,
                Market.FrontierCapability,
                State.AnnualRevenueRunRateUsd,
                State.CapTable.LastPostMoneyValuationUsd);

            State.CurrentFundingOffer = offer;

            var definition = FundingCatalog.Get(offer.Stage);
            var sentiment = FundingCatalog.SentimentLabel(offer.Sentiment);
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.FundingOffered,
                State.Date,
                offer.IsDownRound
                    ? $"{definition.DisplayName} offered as a down round: ${offer.RaiseUsd:N0} for {offer.EquitySold:P1}. Market is {sentiment}."
                    : $"{definition.DisplayName} offered: ${offer.RaiseUsd:N0} for {offer.EquitySold:P1} at ${offer.PreMoneyValuationUsd:N0} pre. Market is {sentiment}.",
                offer.RaiseUsd));

            return true;
        }

        /// <summary>Signs the term sheet. Cash in, equity out, permanently.</summary>
        public bool TryAcceptFundingOffer(out string failureReason)
        {
            failureReason = string.Empty;

            var offer = State.CurrentFundingOffer;
            if (!offer.IsOpen)
            {
                failureReason = "No term sheet on the table.";
                return false;
            }

            if (offer.HasExpired(State.Date))
            {
                failureReason = "That term sheet has lapsed.";
                return false;
            }

            State.CashUsd += offer.RaiseUsd;
            State.CapTable.Record(new FundingRoundRecord(
                offer.Stage,
                State.Date,
                offer.RaiseUsd,
                offer.PostMoneyValuationUsd,
                offer.EquitySold,
                offer.IsDownRound));

            State.CurrentFundingOffer = FundingOffer.None;
            State.LastRoundClosedOn = State.Date;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.FundingClosed,
                State.Date,
                $"{FundingCatalog.Get(offer.Stage).DisplayName} closed. Founders now hold {State.CapTable.FounderEquity:P1}.",
                offer.RaiseUsd));

            return true;
        }

        // ------------------------------------------------------------------ staff and offices

        /// <summary>Hires one person. Fails when there is no desk, no money, or no such role.</summary>
        public bool TryHire(StaffRole role, int skill, out string failureReason)
        {
            failureReason = string.Empty;

            if (State.IsBankrupt)
            {
                failureReason = "The company is insolvent.";
                return false;
            }

            if (!StaffCatalog.TryGet(role, out var definition))
            {
                failureReason = "Unknown role.";
                return false;
            }

            if (!State.Staff.HasFreeDesk)
            {
                failureReason =
                    $"No free desk. {State.Staff.OfficeDefinition.DisplayName} holds {State.Staff.Desks}.";
                return false;
            }

            var safeSkill = Math.Clamp(skill, 1, StaffLimits.MaximumSkill);
            var cost = definition.HiringCostUsd_ForSkill(safeSkill);
            if (State.CashUsd < cost)
            {
                failureReason = $"Hiring costs ${cost:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            State.CashUsd -= cost;
            State.Staff.Add(new Hire(role, safeSkill, State.Date));

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.StaffHired,
                State.Date,
                $"{definition.DisplayName} at skill {safeSkill} joined. "
                + $"Payroll is now ${State.Staff.DailyPayrollUsd:N0} a day across {State.Staff.Headcount} people.",
                cost));

            return true;
        }

        /// <summary>Lets somebody go. No severance, and the desk frees up immediately.</summary>
        public bool TryLetGo(int index, out string failureReason)
        {
            failureReason = string.Empty;

            if (index < 0 || index >= State.Staff.Headcount)
            {
                failureReason = "No such person.";
                return false;
            }

            var hire = State.Staff.Hires[index];
            State.Staff.RemoveAt(index);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.StaffLeft,
                State.Date,
                $"{StaffCatalog.Get(hire.Role).DisplayName} at skill {hire.Skill} left the company."));

            return true;
        }

        /// <summary>Signs a new lease. The fit-out is paid today and the rent starts tomorrow.</summary>
        public bool TryMoveOffice(OfficeTier tier, out string failureReason)
        {
            failureReason = string.Empty;

            if (!OfficeCatalog.TryGet(tier, out var definition))
            {
                failureReason = "Unknown office.";
                return false;
            }

            if (tier == State.Staff.Office)
            {
                failureReason = "Already there.";
                return false;
            }

            if (State.Date.IsBefore(definition.EarliestDate))
            {
                failureReason = $"Not available before {definition.EarliestDate}.";
                return false;
            }

            if (State.CashUsd < definition.RequiredCashUsd || State.CashUsd < definition.FitOutCostUsd)
            {
                failureReason =
                    $"Needs ${Math.Max(definition.RequiredCashUsd, definition.FitOutCostUsd):N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            if (definition.Desks < State.Staff.Headcount)
            {
                failureReason =
                    $"{definition.DisplayName} holds {definition.Desks}, the company has {State.Staff.Headcount} people.";
                return false;
            }

            State.CashUsd -= definition.FitOutCostUsd;
            State.LifetimeCapitalSpentUsd += definition.FitOutCostUsd;
            State.Staff.SetOffice(tier);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.OfficeMoved,
                State.Date,
                $"Moved into {definition.DisplayName}: {definition.Desks} desks at ${definition.MonthlyRentUsd:N0} a month.",
                definition.FitOutCostUsd));

            return true;
        }

        // ------------------------------------------------------------------ safety incidents

        /// <summary>Chance of a public safety failure today, for the screen that shows it.</summary>
        public double DailyIncidentRisk()
        {
            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);
            return IncidentModel.DailyRisk(best, State.Date, State.Staff.IncidentRiskMultiplier() * State.Skills.IncidentRiskMultiplier());
        }

        private void RollSafetyIncident()
        {
            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);
            if (best == null)
            {
                return;
            }

            var risk = IncidentModel.DailyRisk(best, State.Date, State.Staff.IncidentRiskMultiplier() * State.Skills.IncidentRiskMultiplier());
            if (risk <= 0.0 || !State.Random.NextChance(risk))
            {
                return;
            }

            var incident = IncidentModel.Resolve(best, State.Date, State.AnnualRevenueRunRateUsd, State.Random);
            State.Incidents.Add(incident);

            State.Reputation -= incident.ReputationLoss;
            State.CashUsd -= incident.FineUsd;
            State.LifetimeFinesUsd += incident.FineUsd;
            State.LifetimeOperatingCostUsd += incident.FineUsd;

            if (incident.ForcedWithdrawal)
            {
                best.Retire();
            }

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.SafetyIncident,
                State.Date,
                incident.FineUsd > 0
                    ? $"{incident.Headline} Penalty ${incident.FineUsd:N0}."
                    : incident.Headline,
                incident.FineUsd));
        }

        // ------------------------------------------------------------------ debt

        /// <summary>Every debt product with its gate evaluated. Locked ones still appear.</summary>
        public List<LoanAvailability> LoanOffers()
        {
            var offers = new List<LoanAvailability>(LoanCatalog.All.Count);
            foreach (var definition in LoanCatalog.All)
            {
                offers.Add(EvaluateLoan(definition));
            }

            return offers;
        }

        private LoanAvailability EvaluateLoan(LoanDefinition definition)
        {
            if (State.IsBankrupt)
            {
                return new LoanAvailability(definition.Product, false, "The company is insolvent.");
            }

            if (State.Loans.Has(definition.Product))
            {
                return new LoanAvailability(definition.Product, false, "Already drawn and still being serviced.");
            }

            if (State.Loans.OpenCount >= LoanCatalog.MaximumConcurrentLoans)
            {
                return new LoanAvailability(definition.Product, false,
                    $"Already servicing {LoanCatalog.MaximumConcurrentLoans} facilities.");
            }

            if (State.Date.IsBefore(definition.EarliestDate))
            {
                return new LoanAvailability(definition.Product, false, $"Not offered before {definition.EarliestDate}.");
            }

            if (!State.HasResearch(definition.RequiredResearch))
            {
                return new LoanAvailability(definition.Product, false,
                    $"Needs the {ResearchTree.Get(definition.RequiredResearch).DisplayName} research.");
            }

            if (State.AnnualRevenueRunRateUsd < definition.RequiredAnnualRevenueUsd)
            {
                return new LoanAvailability(definition.Product, false,
                    $"Needs ${definition.RequiredAnnualRevenueUsd:N0} annual run rate, has ${State.AnnualRevenueRunRateUsd:N0}.");
            }

            var frontier = Math.Max(1.0, Market.FrontierCapability);
            var ratio = State.BestCapability / frontier;
            if (ratio < definition.RequiredCapabilityRatio)
            {
                return new LoanAvailability(definition.Product, false,
                    $"Needs {definition.RequiredCapabilityRatio:P0} of the frontier, sits at {Math.Max(0.0, ratio):P0}.");
            }

            return new LoanAvailability(definition.Product, true, string.Empty);
        }

        /// <summary>
        /// Draws a facility. The cash lands today and the schedule starts after the grace period,
        /// whatever the company is doing by then.
        /// </summary>
        public bool TryTakeLoan(LoanProduct product, out string failureReason)
        {
            failureReason = string.Empty;

            if (!LoanCatalog.TryGet(product, out var definition))
            {
                failureReason = "Unknown facility.";
                return false;
            }

            var availability = EvaluateLoan(definition);
            if (!availability.IsAvailable)
            {
                failureReason = availability.Reason;
                return false;
            }

            State.CashUsd += definition.PrincipalUsd;
            State.Loans.Add(new Loan(
                product,
                State.Date,
                definition.PrincipalUsd,
                definition.TotalRepaymentUsd,
                definition.TermDays,
                definition.GraceDays));

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.LoanTaken,
                State.Date,
                $"{definition.DisplayName} drawn: ${definition.PrincipalUsd:N0} now, ${definition.TotalRepaymentUsd:N0} back "
                + $"over {definition.TermDays} days from {State.Date.AddDays(definition.GraceDays)}.",
                definition.PrincipalUsd));

            return true;
        }

        private void ServiceDebt()
        {
            if (State.Loans.OpenCount == 0)
            {
                return;
            }

            var due = State.Loans.DailyServiceUsd(State.Date);
            if (due <= 0L)
            {
                return;
            }

            var available = Math.Max(0L, State.CashUsd + CompanyState.CreditLineUsd);
            var paid = State.Loans.Service(State.Date, available);
            State.CashUsd -= paid;
            State.LifetimeOperatingCostUsd += paid;

            if (paid < due)
            {
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.LoanMissed,
                    State.Date,
                    $"Missed ${due - paid:N0} of scheduled repayments.",
                    due - paid));
            }

            var defaulted = State.Loans.FirstDefaulted();
            if (defaulted == null)
            {
                return;
            }

            // A lender will carry a good company through a bad quarter, and no further. Default is
            // not instantly fatal, but it costs standing that took years to build.
            var definition = LoanCatalog.Get(defaulted.Product);
            State.Reputation -= definition.ReputationOnDefault;
            defaulted.Restore(defaulted.RepaidUsd, 0);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.LoanDefaulted,
                State.Date,
                $"{definition.DisplayName} has been in arrears for {LoanBook.ArrearsBeforeDefault} days. "
                + $"The lender has called it publicly and the company has lost {definition.ReputationOnDefault:P0} of its standing.",
                defaulted.OutstandingUsd));
        }

        // ------------------------------------------------------------------ intelligence

        /// <summary>
        /// Puts a research desk on retainer, or stands one down. Billed daily from the next tick.
        /// </summary>
        public void SetIntelSubscription(IntelTier tier)
        {
            if (State.IntelSubscription == tier)
            {
                return;
            }

            State.IntelSubscription = tier;
            State.DaysUntilNextSignal = tier == IntelTier.PublicNews
                ? 0
                : Math.Max(1, IntelligenceService.ReportIntervalDays(tier) / 2);
        }

        // ------------------------------------------------------------------ standings

        /// <summary>The live board, best first.</summary>
        public List<RankingEntry> Ranking() => RankingBoard.Build(State, Market, State.Rivals);

        // ------------------------------------------------------------------ day steps

        /// <summary>
        /// Splits the research slice of the cluster between the training run and the upgrade
        /// programmes, then advances both. A run in flight takes the larger share, which is why
        /// starting a big run stalls the upgrade grid and vice versa.
        /// </summary>
        private double AdvanceResearch(ComputeProfile profile)
        {
            var share = Math.Clamp(State.TrainingComputeShare, 0.0, 1.0);
            var researchPetaflops =
                profile.EffectivePetaflops * share * State.Founder.TrainingThroughputMultiplier
                * (1.0 + State.Staff.UtilizationBonus());
            var researchCash = profile.DailyOperatingCostUsd * share;

            var run = State.ActiveRun;
            var upgradeCount = State.UpgradeProjects.Count;
            var family = State.ActiveArchitectureProject;
            var node = State.ActiveResearch;

            // Four consumers, weighted, normalised over whichever are actually running. A company
            // doing all four at once does all four slowly, which is the intended pressure.
            var runWeight = run != null ? RunComputeWeight : 0.0;
            var upgradeWeight = upgradeCount > 0 ? UpgradeComputeWeight : 0.0;
            var familyWeight = family != null ? ArchitectureComputeWeight : 0.0;
            var nodeWeight = node != null ? ResearchComputeWeight : 0.0;
            var totalWeight = runWeight + upgradeWeight + familyWeight + nodeWeight;

            if (totalWeight <= 0.0)
            {
                return 0.0;
            }

            var runSlice = runWeight / totalWeight;
            var upgradeSlice = upgradeWeight / totalWeight;
            var familySlice = familyWeight / totalWeight;
            var nodeSlice = nodeWeight / totalWeight;

            AdvanceUpgrades(researchPetaflops * upgradeSlice, upgradeCount);
            AdvanceArchitecture(researchPetaflops * familySlice);
            AdvanceResearchNode(researchPetaflops * nodeSlice);

            if (run == null)
            {
                return 0.0;
            }

            var architecture = State.ResolveArchitecture(run.Blueprint.Architecture);
            run.Contribute(
                researchPetaflops * runSlice * architecture.TrainingEfficiency,
                SimUnits.ToDollars(researchCash * runSlice));

            if (!run.IsComplete)
            {
                return run.Progress;
            }

            CompleteRun(run, architecture);
            return 1.0;
        }

        private void AdvanceUpgrades(double petaflopDays, int upgradeCount)
        {
            if (upgradeCount <= 0)
            {
                return;
            }

            var perProject = petaflopDays / upgradeCount;
            var finished = new List<ModelUpgradeProject>();

            foreach (var project in State.UpgradeProjects)
            {
                project.Advance(perProject);
                if (project.IsComplete)
                {
                    finished.Add(project);
                }
            }

            foreach (var project in finished)
            {
                State.RemoveUpgradeProject(project);
                if (project.ModelIndex < 0 || project.ModelIndex >= State.DeployedModels.Count)
                {
                    continue;
                }

                var model = State.DeployedModels[project.ModelIndex];
                model.Traits.SetLevel(project.Trait, project.TargetLevel);

                var definition = ModelTraitCatalog.Get(project.Trait);


                State.AwardSkill(PlayerSkill.Software, 320);
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.UpgradeCompleted,
                    State.Date,
                    $"{model.Name}: {definition.DisplayName} now at level {project.TargetLevel}.",
                    project.CashPaidUsd));
            }
        }

        /// <summary>
        /// A finished run goes on the shelf, not straight to market. Choosing the release week is a
        /// decision, and it is one of the few in the game that costs nothing to get right.
        /// </summary>
        private void CompleteRun(TrainingRun run, ArchitectureDefinition architecture)
        {
            // The projection was a projection. What comes out of the oven is close to it, not equal
            // to it, and only this number is ever recorded as the model's capability.
            // Where the team actually shows up. Two labs with identical blueprints and identical
            // clusters do not get identical models: the one with the better research and safety
            // people lands nearer its own plan. The ceiling is the same, the spread is not.
            var spread = TrainingOutcomeStandardDeviation * State.Staff.OutcomeVarianceMultiplier()
                * State.Skills.TrainingSpreadMultiplier();
            var measured = Math.Clamp(
                run.ProjectedCapability + State.Random.NextGaussian(0.0, spread),
                0.0,
                100.0);

            var activeParameters = run.Blueprint.ParameterCount * architecture.ActiveParameterFraction;
            State.AddToShelf(new TrainedModel(
                run.Blueprint.Name,
                run.Blueprint.Architecture,
                measured,
                State.Date,
                activeParameters,
                run.ProjectedCapability,
                run.Blueprint.Type));

            State.ActiveRun = null;



            // Experience is only ever awarded for finishing something. Never for elapsed time,


            // which would reward leaving the game running.


            State.AwardSkill(PlayerSkill.Development, 900);


            State.AwardSkill(PlayerSkill.Concept, 350);


            var delta = measured - run.ProjectedCapability;
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.TrainingCompleted,
                State.Date,
                $"{run.Blueprint.Name} finished at capability {measured:0.0}, {(delta >= 0 ? "+" : "")}{delta:0.0} against projection. It is on the shelf until you ship it.",
                run.ComputeCashSpentUsd));
        }

        /// <summary>
        /// Every product on the market today, player and rival, described the same way.
        ///
        /// This is the piece that makes competitors play the same game. A rival used to be
        /// capability, brand and price while a player model additionally carried type, traits,
        /// architecture and serving cost, and the share model compared them as though they were the
        /// same kind of thing. They are now literally the same kind of thing.
        /// </summary>
        private List<MarketEntrant> BuildEntrants(double brand, IReadOnlyList<RivalModel> rivals)
        {
            var entrants = new List<MarketEntrant>(State.DeployedModels.Count + rivals.Count);

            foreach (var model in State.DeployedModels)
            {
                if (model == null || !model.IsLiveOn(State.Date))
                {
                    continue;
                }

                // The player's serving burden is read off the same numbers that bill the player for
                // serving, so a cheap model to run is cheap here too rather than being asserted.
                var architecture = State.ResolveArchitecture(model.Architecture);
                var burden = architecture.InferenceCostMultiplier
                    * model.EfficiencyMultiplier(State.Date)
                    * State.Skills.ServingCostMultiplier()
                    * ModelTypeCatalog.Get(model.Type).ServingCostMultiplier;

                entrants.Add(new MarketEntrant(
                    -1,
                    model.Name,
                    model.Type,
                    model.EffectiveCapability(State.Date),
                    Math.Clamp(brand + model.BrandBonus(State.Date), 0.0, 1.0),
                    model.PriceMultiplier,
                    model.AgeYears(State.Date),
                    burden));
            }

            for (var index = 0; index < rivals.Count; index++)
            {
                var rival = rivals[index];
                var slot = State.Rivals.IndexOf(rival.Competitor);
                if (slot < 0)
                {
                    continue;
                }

                entrants.Add(new MarketEntrant(
                    slot,
                    rival.DisplayName,
                    rival.Type,
                    rival.Capability,
                    rival.BrandStrength,
                    rival.PriceMultiplier,
                    rival.ReleaseDate.YearsUntil(State.Date),
                    rival.ServingBurden));
            }

            return entrants;
        }

        /// <summary>Owner names in standing order. Index zero is always the player.</summary>
        private List<string> OwnerNames()
        {
            var names = new List<string>(State.Rivals.Agents.Count + 1) { State.CompanyName };
            foreach (var agent in State.Rivals.Agents)
            {
                names.Add(agent.LabName);
            }

            return names;
        }

        /// <summary>The market by who people are. For balance tests and the audience readout.</summary>
        public List<SegmentStanding> SegmentStandings() =>
            State.Segments.Standings(State.Date, MarketModel.DemandOn(State.Date), OwnerNames());

        /// <summary>
        /// The market by what people are being sold, which is what the Foundation panel draws.
        /// Built from the same standing the tick just moved, never recomputed a second way.
        /// </summary>
        public MarketBreakdown MarketByType() =>
            State.Segments.Breakdown(State.Date, MarketModel.DemandOn(State.Date), OwnerNames());

        private (double Share, double Demanded, double Served, long Revenue) ServeMarket(
            ComputeProfile profile,
            MarketConditions market)
        {
            var rivals = State.Rivals.LiveModels(State.Date);
            var brand = Math.Clamp(
                State.Reputation
                + State.Founder.BrandBonus
                + State.Staff.BrandBonus()
                + State.Monetization.BrandBonus()
                - (State.Home.LocalCompetitionMultiplier - 1.0) * 0.12,
                0.0,
                1.0);

            // The segmented market replaced a single instant logit over one global pool. It is the
            // same utility function scoring the same products; what changed is that the standing
            // moves toward the answer over days instead of being it, and that it is tracked per
            // audience rather than as one number for everybody.
            var share = State.Segments.Advance(
                BuildEntrants(brand, rivals),
                State.Date,
                market.TotalDemandBillionTokensPerDay);

            // Reach from the free tier, then capped: a giveaway widens the funnel, it does not
            // hand over the market.
            share = Math.Clamp(share * State.Monetization.ReachMultiplier, 0.0, 1.0);
            var demanded = market.TotalDemandBillionTokensPerDay * share;

            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);
            if (best == null || demanded <= 0.0)
            {
                return (share, demanded, 0.0, 0L);
            }

            // Serving is embarrassingly parallel, so the fabric tax that hurts training does not
            // apply here. What does apply is the low utilization of memory-bound inference.
            var servingShare = State.ActiveRun != null ? 1.0 - State.TrainingComputeShare : 1.0;
            var servingPetaflops = profile.RawPetaflops * InferenceUtilization * Math.Clamp(servingShare, 0.0, 1.0);

            // Optimisation levels above market par make every token cheaper to produce, which turns
            // straight into capacity at the same cluster size.
            var architecture = State.ResolveArchitecture(best.Architecture);
            var flopPerToken = best.InferenceFlopPerToken
                * architecture.InferenceCostMultiplier
                * best.EfficiencyMultiplier(State.Date)
                * State.Skills.ServingCostMultiplier();
            if (flopPerToken <= 0.0)
            {
                return (share, demanded, 0.0, 0L);
            }

            var capacityTokens = servingPetaflops * SimUnits.FlopsPerPetaflop * SimUnits.SecondsPerDay / flopPerToken;
            var capacityBillions = capacityTokens / SimUnits.TokensPerBillion;
            var served = Math.Min(demanded, capacityBillions);

            // The split that makes a free tier a strategy rather than a giveaway. Every token costs
            // the same to produce; only some of them are invoiced.
            var freeShare = State.Monetization.FreeShareOfTokens;
            var freeTokens = served * freeShare;
            var paidTokens = served - freeTokens;

            State.FreeTokensServedBillions = freeTokens;
            State.LifetimeFreeTokensBillions += freeTokens;

            var rate = State.Monetization.RatePerMillionTokensUsd(market.PricePerMillionTokensUsd);
            var revenue = SimUnits.ToDollars(paidTokens * 1000.0 * rate);

            if (demanded - served > demanded * 0.15 && demanded > 0.0)
            {
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.DemandUnserved,
                    State.Date,
                    $"Turned away {(demanded - served):N0}B tokens. The fleet cannot serve what the market is asking for."));
            }

            return (share, demanded, served, revenue);
        }

        /// <summary>
        /// Pushes the pricing policy onto every live model so the demand split sees one price.
        /// Pricing is a company decision here, not a per-model one: a lab does not quietly charge
        /// four different rates for the same API.
        /// </summary>
        private void SyncPricing(MarketConditions market)
        {
            var relative = State.Monetization.RelativePrice(market.PricePerMillionTokensUsd);
            foreach (var model in State.DeployedModels)
            {
                if (model.IsLiveOn(State.Date))
                {
                    model.PriceMultiplier = relative;
                }
            }
        }

        /// <summary>Runs a day of marketing. Company spend compounds, model spend evaporates.</summary>
        private void AdvanceMarketing()
        {
            var companyEffect = State.Monetization.AdvanceMarketing();
            if (companyEffect > 0.0)
            {
                State.Reputation += companyEffect * State.Founder.ReputationGainMultiplier;
            }
        }

        private void RunRivals()
        {
            var shipped = State.Rivals.Tick(State.Date, State.BestCapability, State.Random);
            foreach (var lab in shipped)
            {
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.RivalReleased,
                    State.Date,
                    $"{lab.LabName} released {lab.LiveModelName} at capability {lab.LiveCapability:0.0}."));
            }
        }

        private long DailyIntelRetainerUsd()
        {
            var monthly = IntelligenceService.MonthlyRetainerUsd(State.IntelSubscription);
            return SimUnits.ToDollars(monthly / DaysPerMonth);
        }

        private void AdvanceIntelligence()
        {
            if (State.IntelSubscription == IntelTier.PublicNews)
            {
                return;
            }

            State.DaysUntilNextSignal--;
            if (State.DaysUntilNextSignal > 0)
            {
                return;
            }

            var interval = IntelligenceService.ReportIntervalDays(State.IntelSubscription);
            State.DaysUntilNextSignal = Math.Max(1, interval + State.Random.NextInt(-6, 7));

            var signal = IntelligenceService.Generate(
                State.IntelSubscription,
                State.Date,
                State.Rivals,
                State.Random);

            State.AddSignal(signal);
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.IntelReceived,
                State.Date,
                $"{signal.Headline} (desk confidence {signal.Confidence:P0})."));
        }

        private void ExpireFundingOffer()
        {
            var offer = State.CurrentFundingOffer;
            if (!offer.IsOpen || !offer.HasExpired(State.Date))
            {
                return;
            }

            State.CurrentFundingOffer = FundingOffer.None;
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.FundingExpired,
                State.Date,
                $"The {FundingCatalog.Get(offer.Stage).DisplayName} term sheet lapsed unsigned."));
        }

        private void UpdateReputation(double share, double served)
        {
            State.Reputation -= ReputationDailyDecay;
            if (served > 0.0)
            {
                State.Reputation += ReputationServiceGain
                    * Math.Clamp(share * 10.0, 0.0, 1.0)
                    * State.Founder.ReputationGainMultiplier;
            }
        }

        private void ReportDeliveries()
        {
            foreach (var asset in State.Pool.Assets)
            {
                if (asset.Units > 0 && asset.CommissionDate == State.Date)
                {
                    State.RaiseEvent(new CompanyEvent(
                        CompanyEventType.HardwareDelivered,
                        State.Date,
                        $"{asset.Units:N0}x {asset.GenerationId} came online in {asset.Tier}."));
                }
            }
        }

        private void ReportNewlyUnlockedTiers(List<ComputeTierStatus> previousLadder)
        {
            var current = State.ComputeTierLadder();
            for (var index = 0; index < current.Count && index < previousLadder.Count; index++)
            {
                if (current[index].IsUnlocked && !previousLadder[index].IsUnlocked)
                {
                    var definition = ComputeTierCatalog.Get(current[index].Tier);
                    State.RaiseEvent(new CompanyEvent(
                        CompanyEventType.ComputeTierUnlocked,
                        State.Date,
                        $"{definition.DisplayName} is now open."));
                }
            }
        }

        private void CheckSolvency()
        {
            if (State.CashUsd >= 0)
            {
                State.DaysInDebt = 0;
                return;
            }

            State.DaysInDebt++;
            if (State.DaysInDebt == 1)
            {
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.CreditLineBreached,
                    State.Date,
                    $"The account is under water. The credit line covers ${CompanyState.CreditLineUsd:N0}.",
                    State.CashUsd));
            }

            if (State.CashUsd < -CompanyState.CreditLineUsd)
            {
                State.IsBankrupt = true;
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.Bankrupt,
                    State.Date,
                    "The credit line is exhausted. The company is insolvent.",
                    State.CashUsd));
            }
        }

        private bool HasAssetsInTier(ComputeTier tier)
        {
            foreach (var asset in State.Pool.Assets)
            {
                if (asset.Tier == tier && asset.Units > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private DayReport BuildReport(
            double share,
            double demanded,
            double served,
            long revenue,
            long operatingCost,
            long depreciation,
            double trainingProgress = 0.0)
        {
            return new DayReport(
                State.Date,
                share,
                demanded,
                served,
                revenue,
                operatingCost,
                depreciation,
                State.CashUsd,
                trainingProgress,
                State.BestCapability,
                State.Rivals.FrontierCapability(State.Date));
        }
    }
}
