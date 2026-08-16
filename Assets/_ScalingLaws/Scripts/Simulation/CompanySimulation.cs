using System;
using System.Text;
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

            // Seats the player bought. Set every tick rather than when something is placed, so a
            // save loaded mid-campaign seats the same number of people as the run that wrote it.
            State.Staff.ExtraDesks = State.Decor?.ExtraDesks ?? 0;
            State.Staff.ComfortBonus = State.Decor?.MoraleBonus ?? 0.0;

            // Anybody who has finished thinking about it writes back today.
            AdvanceHiring();


            ReportDeliveries();
            SyncPricing(market);

            var trainingProgress = AdvanceResearch(profile);
            var (share, demanded, served, revenue) = ServeMarket(profile, market);

            var servingCost = SimUnits.ToDollars(
                profile.DailyOperatingCostUsd * State.Founder.OperatingCostMultiplier
                * State.Skills.OperatingCostMultiplier());
            var intelCost = DailyIntelRetainerUsd();
            var salaryCost = SimUnits.ToDollars(
                State.Staff.DailyCostUsd * State.Founder.OperatingCostMultiplier);
            var marketingCost = State.Monetization.TotalMarketingDailyUsd;

            var operatingCost = servingCost + intelCost + salaryCost + marketingCost;
            var depreciation = SimUnits.ToDollars(profile.DailyDepreciationUsd);

            // Tax is charged on profit, not on turnover, so a loss-making year is not made worse
            // by where the company is registered. It is the only cost in the game the player can
            // reduce by choosing a place rather than by spending money.
            var taxable = Math.Max(0L, revenue - operatingCost);
            var tax = (long)Math.Round(taxable * State.Home.TaxRate);

            // The books, written from the same numbers the cash movement uses rather than from a
            // second pass over the day. Serving is split by the share of tokens nobody was invoiced
            // for, which the market already computes and states costs the same to produce.
            var freeShare = served > 0.0
                ? Math.Clamp(State.FreeTokensServedBillions / served, 0.0, 1.0)
                : 0.0;

            var freeServing = (long)Math.Round(servingCost * freeShare);

            // The four bills, scaled by the same multipliers the charged total already carries, and
            // the last one taken as the remainder so the parts add up to the whole exactly. Rounding
            // four numbers independently and hoping they match the fifth is how a report drifts a few
            // cents a day and a few thousand a decade.
            var bill = profile.Bill;
            var billScale = bill.TotalUsd > 0.0 ? servingCost / bill.TotalUsd : 0.0;

            var rent = (long)Math.Round(bill.CloudRentUsd * billScale);
            var power = (long)Math.Round(bill.ElectricityUsd * billScale);
            var housing = (long)Math.Round(bill.HousingUsd * billScale);
            var upkeep = servingCost - rent - power - housing;

            RecordModelDay(revenue);

            State.PostCash(LedgerLine.Subscriptions, revenue);
            State.PostCash(LedgerLine.CloudRent, rent);
            State.PostCash(LedgerLine.Electricity, power);
            State.PostCash(LedgerLine.Housing, housing);
            State.PostCash(LedgerLine.Maintenance, upkeep);

            // A memo rather than a payment. The free tier sends no invoice of its own, it eats a
            // share of the fleet bill already counted above.
            State.PostNonCash(LedgerLine.ServingFree, freeServing);
            State.PostCash(LedgerLine.Salaries, salaryCost);
            State.PostCash(LedgerLine.Marketing, marketingCost);
            State.PostCash(LedgerLine.Intelligence, intelCost);
            // Accrued, not paid. The demand arrives in January and the money has to still be
            // there; see AdvanceMail. Posting it here as well would tax the same profit twice.
            AccrueTax(tax);
            State.PostNonCash(LedgerLine.Depreciation, depreciation);

            State.LifetimeRevenueUsd += revenue;
            State.LifetimeOperatingCostUsd += operatingCost;
            State.RecordDailyRevenue(revenue);

            AdvanceResearchPoints();
            AdvanceMarketing();
            AdvanceRegulatoryAction();
            RollSafetyIncident();
            ServiceDebt();
            AdvanceIntelligence();
            AdvanceMail();
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
            // The pipeline only reaches the fresh end of the range; the catalog decides where
            // that is, so this passes the fact rather than the rule.
            return TrainingPlanner.Project(
                blueprint,
                Profile,
                Market,
                State.BestCapability,
                State.TrainingComputeShare,
                State,
                State.Founder.DataSupplyMultiplier,
                State.Staff.DataQualityMultiplier() * State.Skills.DataQualityMultiplier(),
                State.HasResearch(ResearchNodeId.ContinuousDataPipeline)
                    ? TrainingChoiceCatalog.PipelineDiscount
                    : 1.0,
                State.DeployedModels.Count,
                TrainingThroughputMultiplier());
        }

        /// <summary>
        /// Everything outside the fleet that makes a run go faster or slower.
        ///
        /// One function, called by the projection and by the day, so the two can never drift. That
        /// they had drifted is the whole reason it exists.
        /// </summary>
        public double TrainingThroughputMultiplier() =>
            State.Founder.TrainingThroughputMultiplier * (1.0 + State.Staff.UtilizationBonus());

        /// <summary>
        /// The largest run the company currently knows how to hold together, in billions.
        ///
        /// One method, read by the rule above, by the slider and by the tests, so the number on the
        /// screen and the number that blocks the run cannot drift apart.
        /// </summary>
        public double ParameterCeilingBillions() => ScaleCeiling.CeilingBillions(
            ScaleCeiling.FractionFor(State.HasResearch),
            ModelBlueprint.LowLogParameters,
            ModelBlueprint.HighLogParameters);

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
        /// <summary>
        /// Whether the company knows how to do a thing, with the reason if not.
        /// </summary>
        private bool HasChoiceResearch(ResearchNodeId gate, out string failureReason)
        {
            failureReason = string.Empty;

            if (gate == ResearchNodeId.None || State.HasResearch(gate))
            {
                return true;
            }

            failureReason = $"That needs the {ResearchTree.Get(gate).DisplayName} research first.";
            return false;
        }

        public bool TryStartTraining(ModelBlueprint blueprint, out string failureReason)
        {
            if (!State.CanBuildType(blueprint.Type))
            {
                failureReason = $"{ModelTypeCatalog.Get(blueprint.Type).DisplayName} models need "
                    + $"{ResearchTree.Get(ModelTypeCatalog.Get(blueprint.Type).Requires).DisplayName} first.";
                return false;
            }

            failureReason = string.Empty;

            // The three technologies that open the Scale and Data options. Checked here rather than
            // in the planner, which is pure and knows nothing about what this company has learned.
            // The neutral option of every catalog is ungated, so a company that has researched
            // nothing can still build exactly the model the game always let it build.
            if (!HasChoiceResearch(TrainingChoiceCatalog.GateFor(blueprint.Precision), out failureReason)
                || !HasChoiceResearch(TrainingChoiceCatalog.GateFor(blueprint.Deduplication),
                    out failureReason))
            {
                return false;
            }

            // How large a run the company knows how to supervise. Enforced here and not only on
            // the slider, because a cap that lives in the interface is a suggestion: the moment a
            // second way to start a run exists, it is not there any more.
            var ceiling = ParameterCeilingBillions();
            if (blueprint.ParameterCountBillions > ceiling * 1.0001)
            {
                failureReason = ScaleCeiling.TryNextRung(State.HasResearch, out var rung, out _)
                    ? $"The company can supervise a run up to {ceiling:N1}B parameters. "
                      + $"{ResearchTree.Get(rung).DisplayName} raises that."
                    : $"The company can supervise a run up to {ceiling:N1}B parameters.";

                return false;
            }

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

            State.PostCash(LedgerLine.Hardware, total);
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
            State.PostCash(LedgerLine.AssetSales, proceedsUsd);
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

            State.PostCash(LedgerLine.Facilities, definition.FacilityCapexUsd);
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

            State.PostCash(LedgerLine.Research, architecture.AdoptionCostUsd);
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

            State.PostCash(LedgerLine.DataAcquisition, definition.AcquisitionCostUsd);
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
            State.LastReleaseDate = State.Date;

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
        /// <summary>
        /// Takes a model off sale for good.
        ///
        /// **Until now the only thing in the game that could retire a model was a safety incident**,
        /// so a player could put a weak line on the market and never take it off. It is a real
        /// decision rather than tidying: a live model in its own line competes, wins share, and is
        /// then served out of the same fleet as everything else. A tired product holding a slice of
        /// an audience is a slice your good model is not holding, served at a latency your good
        /// model then shares.
        ///
        /// It is deliberately **not reversible**. Withdrawing a product and quietly putting it back
        /// when the quarter looks thin is not a decision, and an undo would make shutting down free.
        /// </summary>
        public bool TryRetireModel(DeployedModel model, out string failureReason)
        {
            failureReason = string.Empty;

            if (model == null)
            {
                failureReason = "No model.";
                return false;
            }

            if (model.IsRetired)
            {
                failureReason = $"{model.Name} is already off sale.";
                return false;
            }

            // An upgrade in flight is work being paid for. Letting the product it improves vanish
            // underneath it would leave the programme running against nothing.
            var slot = -1;
            for (var index = 0; index < State.DeployedModels.Count; index++)
            {
                if (ReferenceEquals(State.DeployedModels[index], model))
                {
                    slot = index;
                    break;
                }
            }
            foreach (var project in State.UpgradeProjects)
            {
                if (project.ModelIndex == slot)
                {
                    failureReason = $"An upgrade programme is running on {model.Name}. "
                        + "Cancel it or let it finish first.";

                    return false;
                }
            }

            model.RetireOn(State.Date);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ModelShelved,
                State.Date,
                $"{model.Name} has been withdrawn from sale after "
                + $"{model.DaysOnSale} days and {UsdText(model.LifetimeRevenueUsd)} earned."));

            return true;
        }

        private static string UsdText(long amount) => "$" + amount.ToString("N0");

        /// <summary>
        /// Every model the company ever put on sale, newest first.
        ///
        /// The list the archive draws. Retired models stay in <see cref="CompanyState.DeployedModels"/>
        /// rather than being removed, which is what makes a history possible at all, so this is a
        /// view over that list rather than a second store.
        /// </summary>
        public List<ModelRecord> ModelHistory()
        {
            var records = new List<ModelRecord>(State.DeployedModels.Count);

            for (var index = 0; index < State.DeployedModels.Count; index++)
            {
                var model = State.DeployedModels[index];
                if (model == null)
                {
                    continue;
                }

                var live = model.IsLiveOn(State.Date);
                var marketed = live && !IsSupersededInItsLine(model);

                var users = 0.0;
                if (marketed && MarketByType().TryGetType(model.Type, out var standing))
                {
                    users = standing.PlayerUsers;
                }

                records.Add(new ModelRecord(index, model, live, marketed, users,
                    model.EffectiveCapability(State.Date)));
            }

            // Newest first, because the question the archive answers is "how does the thirtieth
            // compare to the first" and the thirtieth is the one being looked at.
            records.Sort((left, right) =>
                right.Model.ReleaseDate.DayIndex.CompareTo(left.Model.ReleaseDate.DayIndex));

            return records;
        }

        /// <summary>
        /// Commissions post-training work on one model.
        ///
        /// **`onShelf` is the whole reason a finished model can be improved before it ships.** A run
        /// that has completed and not been released is exactly when a real lab does its evaluation
        /// work, and until this argument existed the UPGRADE screen simply had nothing to show for
        /// a company whose only model was sitting on the shelf.
        /// </summary>
        public bool TryStartUpgrade(int modelIndex, ModelTrait trait, out string failureReason,
            bool onShelf = false)
        {
            failureReason = string.Empty;

            if (State.IsBankrupt)
            {
                failureReason = "The company is insolvent.";
                return false;
            }

            var stock = onShelf ? State.Shelf.Count : State.DeployedModels.Count;

            if (modelIndex < 0 || modelIndex >= stock)
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

            if (State.IsUpgradeInFlight(modelIndex, trait, onShelf))
            {
                failureReason = $"{definition.DisplayName} is already being worked on for this model.";
                return false;
            }

            var traits = onShelf
                ? State.Shelf[modelIndex].Traits
                : State.DeployedModels[modelIndex].Traits;

            var subject = onShelf
                ? State.Shelf[modelIndex].Name
                : State.DeployedModels[modelIndex].Name;

            var level = traits.GetLevel(trait);
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

            State.PostCash(LedgerLine.Research, cost);
            State.AddUpgradeProject(new ModelUpgradeProject(
                modelIndex,
                trait,
                level + 1,
                State.Date,
                ScaleResearchDuration(definition.UpgradeDays(level)),
                definition.UpgradePetaflopDays(level),
                cost)
            { OnShelf = onShelf });

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.UpgradeStarted,
                State.Date,
                $"{subject}: {definition.DisplayName} to level {level + 1}, about {definition.UpgradeDays(level)} days."
                + (onShelf ? " Before release." : string.Empty),
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

            // Points first, because that is the real gate now. Cash is a fraction of what it was and
            // a company that has been building will usually have it; a company that has been idle
            // will have the money and none of the understanding, which is the whole point.
            var points = ResearchBudget.PointCostOf(node.CostUsd);
            if (State.ResearchPoints < points)
            {
                return new ResearchStanding(node, false, false, false,
                    $"needs {points:N0} research points, you have {State.ResearchPoints:N0}", duration);
            }

            var cash = ResearchBudget.CashCostOf(node.CostUsd);
            if (State.CashUsd < cash)
            {
                return new ResearchStanding(node, false, false, false,
                    $"needs ${cash:N0}", duration);
            }

            return new ResearchStanding(node, false, false, true, string.Empty, duration);
        }

        /// <summary>
        /// Abandons the programme in flight.
        ///
        /// **Nothing comes back.** The cash was spent on the day it started, the points were spent
        /// with it, and the days are gone. What cancelling buys is the right to start something else
        /// today rather than in four months, which is the only reason anybody would do it: the
        /// frontier moved and the node you picked in March stopped being the one you need.
        ///
        /// Refunding would make starting a programme free to reconsider, and the whole weight of the
        /// research system is that a node costs a season you cannot get back.
        /// </summary>
        public bool TryCancelResearch(out string failureReason)
        {
            failureReason = string.Empty;

            var active = State.ActiveResearch;
            if (active == null)
            {
                failureReason = "Nothing is being researched.";
                return false;
            }

            var node = ResearchTree.Get(active.Node);
            var days = active.DaysCompleted;

            State.ActiveResearch = null;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ResearchCancelled,
                State.Date,
                $"{node.DisplayName} abandoned after {days} days. Nothing spent on it comes back."));

            return true;
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

            // A little cash and a lot of points, both derived from the one figure the catalog
            // carries so the two can never drift apart.
            var cash = ResearchBudget.CashCostOf(node.CostUsd);
            var points = ResearchBudget.PointCostOf(node.CostUsd);

            State.PostCash(LedgerLine.Research, cash);
            State.ResearchPoints = Math.Max(0.0, State.ResearchPoints - points);

            State.ActiveResearch = new ResearchProject(
                nodeId, State.Date, standing.DurationDays, node.PetaflopDaysRequired, cash);

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

            // **The node hands over what its own card promised.**
            //
            // Until 2026-08-15 it did not. The research card prints "CORPUS: Curated corpora" and
            // "ARCHITECTURE: Mixture of experts" straight off the node, the player pays the points,
            // the cash and four months of calendar, and received neither: the only code that ever
            // granted a corpus was TryAcquireDataSource and the only code that adopted an
            // architecture was TryAdoptArchitecture, and nothing in the interface called either.
            // A campaign was locked to the web crawl and its starting family from the first day to
            // the last, and every test passed, because the scripted operator calls those two
            // methods directly and so never noticed they were unreachable.
            //
            // Granted here rather than through the Try methods on purpose. Those two check cash and
            // charge for the purchase; this node has already been paid for, in a currency the
            // player cannot buy it with.
            var granted = new StringBuilder();

            if (node.UnlocksData != DatasetSource.None
                && (State.OwnedDataSources & node.UnlocksData) != node.UnlocksData)
            {
                State.OwnedDataSources |= node.UnlocksData;

                foreach (var corpus in DatasetCatalog.All)
                {
                    if ((node.UnlocksData & corpus.Flag) == corpus.Flag)
                    {
                        granted.Append(granted.Length > 0 ? ", " : " Opens ").Append(corpus.DisplayName);
                    }
                }
            }

            if (node.UnlocksArchitecture != ArchitectureId.None
                && !State.HasArchitecture(node.UnlocksArchitecture)
                && ArchitectureCatalog.TryGet(node.UnlocksArchitecture, out var family))
            {
                State.AdoptedArchitectures.Add(node.UnlocksArchitecture);
                granted.Append(granted.Length > 0 ? ", " : " Opens ").Append(family.DisplayName);
            }

            State.AwardSkill(PlayerSkill.Concept, 620);


            State.AwardSkill(PlayerSkill.DataEngineering, 200);
            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ResearchCompleted,
                State.Date,
                $"{node.DisplayName} is done.{granted}.",
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
            State.PostCash(LedgerLine.Research, cash);

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

            State.PostCash(LedgerLine.Funding, offer.RaiseUsd);
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
        /// <summary>
        /// Buys the place outright, and moves in if the company is not already there.
        ///
        /// **Separate from moving because they are separate decisions.** Renting is a monthly bill a
        /// struggling company can walk away from; buying is capital that never comes back and ends
        /// the rent forever. The price is about ten years of it, so a company that will still be
        /// here in a decade should buy and one that is not sure should not tie up the money.
        ///
        /// Owning is remembered per place. A company that buys the small hub and later moves up
        /// still owns the small hub, and moving back into it costs nothing.
        /// </summary>
        public bool TryBuyOffice(OfficeTier tier, out string failureReason)
        {
            failureReason = string.Empty;

            if (!OfficeCatalog.TryGet(tier, out var definition))
            {
                failureReason = "Unknown office.";
                return false;
            }

            if (!definition.CanBeBought)
            {
                failureReason = $"{definition.DisplayName} is not for sale.";
                return false;
            }

            if (State.Staff.Owns(tier))
            {
                failureReason = "The company already owns it.";
                return false;
            }

            if (State.Date.IsBefore(definition.EarliestDate))
            {
                failureReason = $"Not available before {definition.EarliestDate}.";
                return false;
            }

            // The fit-out is only owed when the company is actually moving in. Buying a place it is
            // already sitting in is a purchase, not a move.
            var moving = State.Staff.Office != tier;
            var owed = definition.PurchasePriceUsd + (moving ? definition.FitOutCostUsd : 0L);

            if (State.CashUsd < owed)
            {
                failureReason = $"Needs ${owed:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            if (moving && definition.Desks < State.Staff.Headcount)
            {
                failureReason =
                    $"{definition.DisplayName} holds {definition.Desks}, the company has "
                    + $"{State.Staff.Headcount} people.";

                return false;
            }

            State.PostCash(LedgerLine.Facilities, owed);
            State.Staff.Owned.Add(tier);

            if (moving)
            {
                State.Staff.SetOffice(tier);
            }

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.OfficeMoved,
                State.Date,
                $"Bought {definition.DisplayName} outright for {Usd(definition.PurchasePriceUsd)}. "
                + "No rent on it again.",
                owed));

            return true;
        }

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

            State.PostCash(LedgerLine.Facilities, definition.FitOutCostUsd);
            State.LifetimeCapitalSpentUsd += definition.FitOutCostUsd;
            State.Staff.SetOffice(tier);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.OfficeMoved,
                State.Date,
                $"Moved into {definition.DisplayName}: {definition.Desks} desks at ${definition.MonthlyRentUsd:N0} a month.",
                definition.FitOutCostUsd));

            return true;
        }

        // ------------------------------------------------------------------ hiring

        /// <summary>
        /// Generates a shortlist for one discipline through one channel.
        ///
        /// **The list is rolled from the company's own random stream**, so two players who open the
        /// agency on the same day of the same seed see the same people. It advances that stream,
        /// which is what stops the player rerolling a bad list by closing the tab: looking costs a
        /// draw, and the next look is a different list.
        /// </summary>
        public IReadOnlyList<Candidate> Shortlist(PlayerSkill position, HireSource source,
            int centreLevel, int howMany)
        {
            var list = new List<Candidate>();
            var count = Math.Clamp(howMany, 1, 12);

            for (var index = 0; index < count; index++)
            {
                list.Add(Candidate.Roll(State.Hiring.NextCandidateId++, position, source,
                    centreLevel, State.Hiring.Random));
            }

            return list;
        }

        /// <summary>
        /// What a specialist search of this shape costs to commission.
        ///
        /// A real fee, paid whether or not the person signs, because that is what makes the
        /// minimum-level slider a decision. Without it the player would always ask for a hundred.
        /// </summary>
        public static long SpecialistFeeUsd(PlayerSkill position, int minimumLevel)
        {
            var definition = PositionCatalog.Get(position);
            var reach = Math.Clamp(minimumLevel, 1, PlayerSkillLimits.MaximumLevel) / 100.0;

            return (long)Math.Round(definition.BaseHourlyWageUsd * 40.0 * (0.5 + 2.5 * reach * reach));
        }

        /// <summary>
        /// Starts talking to somebody. Returns the reason it could not happen, or empty.
        ///
        /// Nobody is hired here. This opens a conversation that answers in two to four days, and
        /// the letter it produces is where the money is agreed.
        /// </summary>
        public string TryApproach(Candidate candidate)
        {
            if (candidate == null)
            {
                return "Nobody selected.";
            }

            if (State.IsBankrupt)
            {
                return "The company is insolvent.";
            }

            if (!State.Hiring.CanApproach)
            {
                return $"Already talking to {State.Hiring.OpenCount} people. "
                    + "Close one of those first.";
            }

            if (candidate.Source == HireSource.Remote)
            {
                var seats = State.Hiring.RemoteSeats;

                if (State.Staff.CountFrom(HireSource.Remote) >= seats)
                {
                    return State.Hiring.HasRemotePartnership
                        ? $"All {seats} remote contracts are taken."
                        : $"Remote is capped at {seats} without an IThand partnership.";
                }
            }
            else if (!State.Staff.HasFreeSeat)
            {
                return $"No free desk. {State.Staff.OfficeDefinition.DisplayName} holds "
                    + $"{State.Staff.Desks} and {State.Staff.SeatedHeadcount} are taken.";
            }

            State.Hiring.Begin(candidate, State.Date);
            return string.Empty;
        }

        /// <summary>Buys the IThand partnership, which is the only way past five remote people.</summary>
        public string TryBuyRemotePartnership()
        {
            if (State.Hiring.HasRemotePartnership)
            {
                return "Already a partner.";
            }

            if (State.CashUsd < HiringChannels.PartnershipCostUsd)
            {
                return $"Needs ${HiringChannels.PartnershipCostUsd:N0}, has ${State.CashUsd:N0}.";
            }

            State.PostCash(LedgerLine.Facilities, HiringChannels.PartnershipCostUsd);
            State.Hiring.HasRemotePartnership = true;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.HiringNotice, State.Date,
                $"IThand.hck partnership signed. Remote contracts now capped at "
                + $"{HiringChannels.PartneredRemoteSeats}.",
                HiringChannels.PartnershipCostUsd));

            return string.Empty;
        }

        /// <summary>
        /// Moves every open approach on a day and posts a letter from anybody who answered.
        ///
        /// Called from the daily tick. The letter carries the whole candidate, because the
        /// negotiation happens in the inbox and the inbox has to be able to price an offer without
        /// asking the desk who this person was.
        /// </summary>
        private void AdvanceHiring()
        {
            foreach (var approach in State.Hiring.Advance())
            {
                var candidate = approach.Candidate;
                var definition = candidate.Definition;
                var channel = HiringChannels.Get(candidate.Source);

                var letter = State.Mail.Add(MailKind.JobOffer, State.Date, candidate.Name,
                    $"Re: {definition.Title} position",
                    $"Thank you for considering me for the open position. I have {candidate.TrueLevel} "
                    + $"in {PlayerSkillCatalog.Get(candidate.Position).DisplayName} and I am "
                    + $"available from next week. Let us discuss the wage for this job.\n\n"
                    + $"Found through {channel.SiteName}. Advertised at {candidate.AdvertisedLevel}, "
                    + $"assessed at {candidate.TrueLevel} after the {channel.DisplayName.ToLowerInvariant()} "
                    + "adjustment.");

                letter.Role = definition.Role;
                letter.Skill = candidate.RoleSkill;
                letter.AskingSalaryUsd = candidate.AnnualSalaryUsd(candidate.AskingHourlyUsd);
                letter.Candidate = candidate;
                letter.OfferedHourlyUsd = candidate.AskingHourlyUsd;
                letter.DueDayIndex = State.Date.DayIndex + 14;

                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.HiringNotice, State.Date,
                    $"{candidate.Name} replied about the {definition.Title} position.", 0L));
            }
        }

        /// <summary>
        /// Puts an offer in front of a candidate and does whatever they decide.
        ///
        /// Returns what happened in the player's words. **Every outcome closes something**: signing
        /// closes the letter and adds a person, holding firm burns a round, and walking away closes
        /// the letter for good. A negotiation that could be retried forever would have no stakes.
        /// </summary>
        public OfferVerdict Negotiate(MailItem letter, double hourlyUsd, long signingBonusUsd,
            out string note)
        {
            note = string.Empty;

            if (letter == null || letter.Candidate == null || letter.IsClosed)
            {
                note = "That conversation is over.";
                return OfferVerdict.WalkedAway;
            }

            var candidate = letter.Candidate;
            var offered = Math.Max(0.0, hourlyUsd);
            var bonus = Math.Max(0L, signingBonusUsd);

            var verdict = Negotiation.Judge(candidate, offered, bonus, letter.NegotiationRounds);
            letter.NegotiationRounds++;
            letter.OfferedHourlyUsd = offered;

            switch (verdict)
            {
                case OfferVerdict.Accepted:
                    return Sign(letter, candidate, offered, bonus, out note);

                case OfferVerdict.HeldFirm:
                    // Sometimes the wait costs the company. Rolled from the hiring stream so it
                    // cannot shift the sequence the market runs on.
                    if (State.Hiring.Random.NextChance(Negotiation.ImpatienceChance))
                    {
                        var now = candidate.RaiseTheAsk(Negotiation.ImpatienceRaise);

                        note = $"{candidate.Name} has had another offer and now wants "
                            + $"${now:N2} per hour.";
                    }
                    else
                    {
                        note = $"{candidate.Name} holds firm at "
                            + $"${candidate.AskingHourlyUsd:N2} per hour.";
                    }

                    letter.AskingSalaryUsd =
                        candidate.AnnualSalaryUsd(candidate.AskingHourlyUsd);

                    return OfferVerdict.HeldFirm;

                default:
                    letter.IsClosed = true;
                    letter.Outcome = "Walked away to another offer.";
                    note = $"{candidate.Name} has withdrawn.";

                    State.RaiseEvent(new CompanyEvent(
                        CompanyEventType.HiringNotice, State.Date,
                        $"{candidate.Name} withdrew from the {candidate.Definition.Title} process.",
                        0L));

                    return OfferVerdict.WalkedAway;
            }
        }

        /// <summary>Accepts whatever they asked for, with no haggling at all.</summary>
        public OfferVerdict AcceptAsking(MailItem letter, out string note)
        {
            note = string.Empty;

            if (letter?.Candidate == null || letter.IsClosed)
            {
                note = "That conversation is over.";
                return OfferVerdict.WalkedAway;
            }

            return Sign(letter, letter.Candidate, letter.Candidate.AskingHourlyUsd, 0L, out note);
        }

        private OfferVerdict Sign(MailItem letter, Candidate candidate, double hourlyUsd,
            long signingBonusUsd, out string note)
        {
            var definition = candidate.Definition;

            if (State.CashUsd < signingBonusUsd)
            {
                note = $"The signing bonus needs ${signingBonusUsd:N0} and the company has "
                    + $"${State.CashUsd:N0}.";
                return OfferVerdict.HeldFirm;
            }

            var hire = new Hire(definition.Role, candidate.RoleSkill, State.Date, candidate.Name,
                candidate.Position, candidate.Source, hourlyUsd);

            if (!State.Staff.Add(hire))
            {
                note = candidate.Source == HireSource.Remote
                    ? "There is no remote contract left to put them on."
                    : $"There is nowhere for them to sit. "
                      + $"{State.Staff.OfficeDefinition.DisplayName} holds "
                      + $"{State.Staff.Desks} desks.";

                return OfferVerdict.HeldFirm;
            }

            if (signingBonusUsd > 0L)
            {
                State.PostCash(LedgerLine.Salaries, signingBonusUsd);
            }

            letter.IsClosed = true;
            letter.Outcome = $"Signed at ${hourlyUsd:N2} an hour.";

            note = $"{candidate.Name} starts as {definition.Title} at ${hourlyUsd:N2} an hour.";

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.StaffHired, State.Date,
                $"{candidate.Name} joined as {definition.Title} "
                + $"({HiringChannels.Get(candidate.Source).DisplayName.ToLowerInvariant()}), "
                + $"${hourlyUsd:N2} an hour.",
                signingBonusUsd));

            return OfferVerdict.Accepted;
        }

        // ------------------------------------------------------------------ the furniture shop

        /// <summary>
        /// Buys a piece for the office and stands it up.
        ///
        /// Returns the reason it could not happen, or empty on success. Same shape as the other
        /// TryX methods on this class, so the screen that calls it does not need a second pattern.
        ///
        /// The room's own size is passed in because the simulation does not know what the office
        /// looks like, only which tier it is; the caller reads it from the room catalog.
        /// </summary>
        public string TryBuyFurniture(FurnitureKind kind, DecorZone zone)
        {
            var piece = FurnitureCatalog.Get(kind);

            if (State.CashUsd < piece.PriceUsd)
            {
                return $"Needs ${piece.PriceUsd:N0}, has ${State.CashUsd:N0}.";
            }

            State.Decor ??= new DecorPlan();

            var item = State.Decor.Buy(kind, zone);

            State.PostCash(LedgerLine.Facilities, (long)piece.PriceUsd);
            State.LifetimeCapitalSpentUsd += (long)piece.PriceUsd;

            // Reapplied at once rather than waiting for the next tick, so the desk the player just
            // bought can be hired into on the same screen.
            State.Staff.ExtraDesks = State.Decor.ExtraDesks;
            State.Staff.ComfortBonus = State.Decor.MoraleBonus;

            return item.IsPlaced
                ? string.Empty
                : "Bought, but the floor is full. It is in storage until something is moved.";
        }

        /// <summary>
        /// Sells a piece back at the catalog's resale fraction and returns what it fetched.
        ///
        /// The refund is revenue on the facilities line rather than a negative cost, because that is
        /// what it is: the company got money for a thing it owned.
        /// </summary>
        public double SellFurniture(DecorItem item)
        {
            if (State.Decor == null)
            {
                return 0.0;
            }

            var refund = State.Decor.Sell(item);
            if (refund <= 0.0)
            {
                return 0.0;
            }

            // AssetSales, not a negative facilities cost. PostCash reads the sign from the line
            // rather than from the number, so a negative on a cost line charges the company for
            // selling its own sofa.
            State.PostCash(LedgerLine.AssetSales, (long)refund);

            State.Staff.ExtraDesks = State.Decor.ExtraDesks;
            State.Staff.ComfortBonus = State.Decor.MoraleBonus;

            return refund;
        }

        /// <summary>Puts a stored piece on the floor, or reports that there is no room for it.</summary>
        public string TryPlaceFurniture(DecorItem item, DecorZone zone)
        {
            if (State.Decor == null || item == null)
            {
                return "Nothing to place.";
            }

            if (!State.Decor.Place(item, zone))
            {
                return "The floor is full.";
            }

            State.Staff.ExtraDesks = State.Decor.ExtraDesks;
            State.Staff.ComfortBonus = State.Decor.MoraleBonus;
            return string.Empty;
        }

        /// <summary>
        /// Takes a piece off the floor.
        ///
        /// Blocked when a desk is being sat at, because removing it would leave somebody employed
        /// with nowhere to sit and the hiring cap is the one number the shop can actually break.
        /// </summary>
        public string TryStoreFurniture(DecorItem item)
        {
            if (State.Decor == null || item == null)
            {
                return "Nothing to store.";
            }

            var seats = item.Definition.DeskSeats;
            if (seats > 0 && State.Staff.Desks - seats < State.Staff.Headcount)
            {
                return $"Somebody is sitting there. The company has {State.Staff.Headcount} people "
                    + $"and {State.Staff.Desks} desks.";
            }

            State.Decor.Store(item);
            State.Staff.ExtraDesks = State.Decor.ExtraDesks;
            State.Staff.ComfortBonus = State.Decor.MoraleBonus;
            return string.Empty;
        }

        // ------------------------------------------------------------------ safety incidents

        /// <summary>Chance of a public safety failure today, for the screen that shows it.</summary>
        /// <summary>
        /// The safety plan of whatever is currently on sale.
        ///
        /// One method, so the figure the interface shows and the figure the roll uses cannot drift.
        /// Reads the model rather than the company on purpose: protection belongs to the run.
        /// </summary>
        public SafetyPlan CurrentSafety()
        {
            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);

            return best == null
                ? new SafetyPlan(0, 0, -1, 1, 0)
                : new SafetyPlan(best.AssaTier, best.RedTeamTier, best.DataProtectionTier,
                    best.SafetyEffort, State.DeployedModels.Count);
        }

        public double DailyIncidentRisk()
        {
            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);
            // The safety stage lands here as well as in the roll, because a player looking at this
            // number is looking at what the modules bought them.
            return IncidentModel.DailyRisk(best, State.Date,
                       State.Staff.IncidentRiskMultiplier() * State.Skills.IncidentRiskMultiplier())
                   * (1.0 - CurrentSafety().RiskReduction);
        }

        private void RollSafetyIncident()
        {
            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);
            if (best == null)
            {
                return;
            }

            var risk = IncidentModel.DailyRisk(best, State.Date, State.Staff.IncidentRiskMultiplier() * State.Skills.IncidentRiskMultiplier());

            // What this model was hardened with when it was built, which is not what the company
            // knows today. A run shipped before the research landed is still that run.
            var safety = new SafetyPlan(best.AssaTier, best.RedTeamTier, best.DataProtectionTier,
                best.SafetyEffort, State.DeployedModels.Count);

            risk *= 1.0 - safety.RiskReduction;

            if (risk <= 0.0 || !State.Random.NextChance(risk))
            {
                return;
            }

            var incident = IncidentModel.Resolve(best, State.Date, State.AnnualRevenueRunRateUsd, State.Random);

            // **Anything with a penalty behind it opens an inspection rather than landing.** The
            // outcome is already decided by what follows; the five days are what turn a number
            // changing into something happening to you. Everything smaller lands as it always did,
            // because a regulator does not open a file over a bad week.
            if (incident.FineUsd > 0L || incident.ForcedWithdrawal)
            {
                OpenRegulatoryAction(incident, best.Name);
                return;
            }

            LandPenalty(incident, best);
        }

        /// <summary>
        /// The verdict, applied.
        ///
        /// Reached two ways: straight from a small incident nobody opens a file over, and from an
        /// inspection that ran its five days and found something. One body, so the two paths cannot
        /// disagree about what a penalty does.
        /// </summary>
        private void LandPenalty(SafetyIncident incident, DeployedModel best)
        {
            State.Incidents.Add(incident);

            State.Reputation -= incident.ReputationLoss;

            // The penalty arrives as a letter rather than vanishing from the account. A fine the
            // player never saw was indistinguishable from the market turning against them, which is
            // the specific reason incidents read as a bug rather than as a hard game.
            if (incident.FineUsd > 0L)
            {
                var demand = State.Mail.Add(MailKind.Fine, State.Date,
                    WorldRegionCatalog.Get(State.HomeCountry).DisplayName + " regulator",
                    "Penalty notice: " + incident.Severity,
                    incident.Headline
                    + $"\n\nThe penalty is {Usd(incident.FineUsd)}, payable within "
                    + $"{DemandGraceDays} days. Unpaid it grows at {LatePenaltyPerYear:P0} a year."
                    + (incident.ForcedWithdrawal
                        ? "\n\nThe model named above has been withdrawn from sale with immediate "
                          + "effect. This is not optional and it is not reversible."
                        : string.Empty));

                demand.AmountUsd = incident.FineUsd;
                demand.DueDayIndex = State.Date.DayIndex + DemandGraceDays;
            }

            if (incident.ForcedWithdrawal && best != null)
            {
                // Recorded with a date, so the archive can show when it came off and why.
                best.RetireOn(State.Date);
            }

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.SafetyIncident,
                State.Date,
                incident.FineUsd > 0
                    ? $"{incident.Headline} Penalty ${incident.FineUsd:N0}."
                    : incident.Headline,
                incident.FineUsd));
        }

        /// <summary>
        /// Opens the file, and tells the player twice.
        ///
        /// The banner is the moment; the letter is the record. A player who tabs away during the
        /// five days still finds out what happened when they come back.
        /// </summary>
        private void OpenRegulatoryAction(SafetyIncident incident, string modelName)
        {
            State.PendingAction = new RegulatoryAction(incident, State.Date, modelName);

            State.Mail.Add(MailKind.Notice, State.Date,
                WorldRegionCatalog.Get(State.HomeCountry).DisplayName + " regulator",
                "Inspection opened: " + incident.Severity,
                incident.Headline
                + $"\n\nAn inspection has been opened into {modelName}. No penalty has been "
                + $"decided. Findings are expected within {RegulatoryAction.InspectionDays} days, "
                + "and the company will be notified either way."
                + "\n\nCooperation is not optional and the file is already open.");

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.SafetyIncident,
                State.Date,
                $"A regulator opened an inspection into {modelName}."));
        }

        /// <summary>
        /// Runs the clock on an open inspection and delivers the verdict when it stops.
        ///
        /// **The roll happens here rather than when the file opened.** Not because the odds change,
        /// but because a save made mid-inspection would otherwise carry a decided outcome the player
        /// could reload away from, which is the one thing that would make the whole system a slot
        /// machine.
        /// </summary>
        private void AdvanceRegulatoryAction()
        {
            var action = State.PendingAction;
            if (action == null)
            {
                return;
            }

            action.Advance();
            if (!action.IsClosed)
            {
                return;
            }

            State.PendingAction = null;

            var best = MarketShareModel.BestLiveModel(State.DeployedModels, State.Date);
            var safety = best == null
                ? new SafetyPlan(0, 0, -1, 1, 0)
                : new SafetyPlan(best.AssaTier, best.RedTeamTier, best.DataProtectionTier,
                    best.SafetyEffort, State.DeployedModels.Count);

            var saviour = safety.Saviour(State.Random.NextDouble());
            if (saviour.HasValue)
            {
                SafetyWasSaved(saviour.Value, action.Incident, safety);
                return;
            }

            LandPenalty(action.Incident, best);
        }

        /// <summary>
        /// A penalty that was coming and did not arrive.
        ///
        /// **Announced twice on purpose**: an event for the wire and a letter in the mail. A saving
        /// throw the player never sees is indistinguishable from nothing having happened, and the
        /// entire value of the safety stage is that the player finds out it worked.
        /// </summary>
        private void SafetyWasSaved(SafetyModule saviour, SafetyIncident incident, SafetyPlan plan)
        {
            var module = saviour == SafetyModule.RedTeam
                ? SafetyModuleCatalog.Get(SafetyModule.RedTeam, plan.RedTeamTier)
                : SafetyModuleCatalog.Get(SafetyModule.DataProtection,
                    Math.Max(0, plan.DataProtectionTier));

            var what = saviour == SafetyModule.RedTeam
                ? "The red team had already found it."
                : "The data they went looking for was not reachable.";

            State.Mail.Add(MailKind.Notice, State.Date,
                "Safety office",
                "No further action: " + incident.Severity,
                $"{incident.Headline}\n\nThe review closed without a penalty. {what} "
                + $"{module.DisplayName} is why, and on the numbers it had about a "
                + $"{plan.SaveChance:P0} chance of holding.\n\nThis is not a reprieve that can be "
                + "relied on twice. It held this time.");

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.SafetyIncident,
                State.Date,
                $"Regulator closed a case with no penalty. {module.DisplayName} held."));
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

            State.PostCash(LedgerLine.Funding, definition.PrincipalUsd);
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
            State.PostCash(LedgerLine.Interest, paid);
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
        public void SetIntelSubscription(IntelTier tier, bool joined)
        {
            if (tier == IntelTier.PublicNews || State.IsMember(tier) == joined)
            {
                return;
            }

            State.SetMembership(tier, joined);

            // Joining starts that desk's own clock at half an interval, so a new member hears
            // something reasonably soon rather than paying a month for silence.
            State.SetCountdownFor(tier, joined
                ? Math.Max(1, IntelligenceService.ReportIntervalDays(tier) / 2)
                : 0);

            var best = State.BestMembership;
            State.DaysUntilNextSignal = best == IntelTier.PublicNews
                ? 0
                : Math.Max(1, IntelligenceService.ReportIntervalDays(best) / 2);
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
                profile.EffectivePetaflops * share * TrainingThroughputMultiplier();
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

            // Precision belongs here as well as in the projection. Narrow numbers move more of
            // them through the same silicon; leaving the factor out of the advance meant FP8 was
            // priced as a saving on the design screen and delivered nothing on the calendar.
            var architecture = State.ResolveArchitecture(run.Blueprint.Architecture);
            var precision = TrainingChoiceCatalog.Get(run.Blueprint.Precision);

            run.Contribute(
                researchPetaflops * runSlice * architecture.TrainingEfficiency * precision.Throughput,
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

                // The bound has to be the list the project points at. This checked the deployed
                // list unconditionally, so a shelf programme on a company with nothing on sale
                // failed 0 >= 0, was dropped on the day it completed, and the player got nothing
                // for the money and the months.
                var stock = project.OnShelf ? State.Shelf.Count : State.DeployedModels.Count;

                if (project.ModelIndex < 0 || project.ModelIndex >= stock)
                {
                    continue;
                }

                // Whichever list the project was pointed at. A finished programme applying to
                // the wrong model would silently upgrade a stranger.
                if (project.OnShelf)
                {
                    State.Shelf[project.ModelIndex].Traits
                        .SetLevel(project.Trait, project.TargetLevel);
                }
                else
                {
                    State.DeployedModels[project.ModelIndex].Traits
                        .SetLevel(project.Trait, project.TargetLevel);
                }

                var subject = project.OnShelf
                    ? State.Shelf[project.ModelIndex].Name
                    : State.DeployedModels[project.ModelIndex].Name;

                var definition = ModelTraitCatalog.Get(project.Trait);

                State.AwardSkill(PlayerSkill.Software, 320);
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.UpgradeCompleted,
                    State.Date,
                    $"{subject}: {definition.DisplayName} now at level {project.TargetLevel}."
                    + (project.OnShelf ? " It ships with the model." : string.Empty),
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
            // Precision lands here rather than on the capability itself, which is the honest shape
            // of it: training narrow does not make a worse model on average, it makes a less
            // predictable one. The founder's Development skill multiplies in alongside, so being
            // good at this is exactly what buys the right to gamble on FP8.
            var spread = TrainingOutcomeStandardDeviation * State.Staff.OutcomeVarianceMultiplier()
                * State.Skills.TrainingSpreadMultiplier()
                * TrainingChoiceCatalog.Get(run.Blueprint.Precision).Instability;
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
                run.Blueprint.Type,
                run.Blueprint.Family,
                run.Blueprint.Shape,
                run.Blueprint.AssaTier,
                run.Blueprint.RedTeamTier,
                run.Blueprint.DataProtectionTier,
                run.Blueprint.SafetyEffort));

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
        /// <summary>
        /// Whether a stronger, or equally strong but newer, model in the same line is also on sale.
        ///
        /// Ties break on release date so the answer is stable and never depends on list order, which
        /// would otherwise make the market quietly sensitive to how the save was written.
        /// </summary>
        private bool IsSupersededInItsLine(DeployedModel model)
        {
            if (model.Family.Length == 0)
            {
                return false;
            }

            var mine = model.EffectiveCapability(State.Date);

            foreach (var other in State.DeployedModels)
            {
                if (other == null || ReferenceEquals(other, model)
                    || !other.IsLiveOn(State.Date) || !model.SharesLineWith(other))
                {
                    continue;
                }

                var theirs = other.EffectiveCapability(State.Date);
                if (theirs > mine)
                {
                    return true;
                }

                if (theirs < mine)
                {
                    continue;
                }

                // Equal capability, so the newer one leads. Two models of the same strength released
                // on the same day fall through to the name, because without a total order neither
                // superseded the other and both stayed on sale, which is the whole thing this rule
                // exists to prevent.
                if (other.ReleaseDate.DayIndex != model.ReleaseDate.DayIndex)
                {
                    if (other.ReleaseDate.DayIndex > model.ReleaseDate.DayIndex)
                    {
                        return true;
                    }

                    continue;
                }

                if (string.CompareOrdinal(other.Name, model.Name) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private List<MarketEntrant> BuildEntrants(double brand, IReadOnlyList<RivalModel> rivals)
        {
            var entrants = new List<MarketEntrant>(State.DeployedModels.Count + rivals.Count);

            foreach (var model in State.DeployedModels)
            {
                if (model == null || !model.IsLiveOn(State.Date))
                {
                    continue;
                }

                // One product line is one product. A buyer choosing between your last four releases
                // is not four separate chances at their business, and without this a company could
                // raise its standing simply by never withdrawing anything: every live model added its
                // own score to the same bucket, so shipping often beat shipping well.
                if (IsSupersededInItsLine(model))
                {
                    continue;
                }

                // The player's serving burden is read off the same numbers that bill the player for
                // serving, so a cheap model to run is cheap here too rather than being asserted.
                //
                // That comment was a lie until 2026-08-11. It omitted the size, which is the dominant
                // term in what serving actually costs, so a ten times larger model was just as cheap
                // in the market's eyes and the Scale stage had no consequence past the training bill.
                var architecture = State.ResolveArchitecture(model.Architecture);
                var burden = architecture.InferenceCostMultiplier
                    * model.EfficiencyMultiplier(State.Date)
                    * State.Skills.ServingCostMultiplier()
                    * ModelTypeCatalog.Get(model.Type).ServingCostMultiplier
                    * MarketShareModel.SizeBurden(model.ActiveParameterCount)

                    // Depth is sequential and width is parallel, so the same parameter count costs
                    // more per token arranged deep. This is the other half of the shape trade and
                    // without it the deep option would be a free capability bonus.
                    * TrainingChoiceCatalog.Get(model.Shape).ServingBurden;

                entrants.Add(new MarketEntrant(
                    -1,
                    model.Name,
                    model.Type,
                    model.EffectiveCapability(State.Date),
                    Math.Clamp(brand + model.BrandBonus(State.Date), 0.0, 1.0),
                    model.PriceMultiplier,
                    model.AgeYears(State.Date),
                    burden,

                    // Yesterday's measured experience, because today's is not known until the market
                    // has been served. One day of lag is honest and it stops the calculation eating
                    // its own tail.
                    State.LastQuality.Reliability));
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

        /// <summary>
        /// The player's standing in the market as reputation the audiences can see. Extracted so the
        /// tick and every readout score the same company; two copies of this drifted apart once
        /// already elsewhere in this file and it took a save replay to find.
        /// </summary>
        public double PlayerBrand() => Math.Clamp(
            State.Reputation
            + State.Founder.BrandBonus
            + State.Staff.BrandBonus()
            + State.Monetization.BrandBonus()
            - (State.Home.LocalCompetitionMultiplier - 1.0) * 0.12,
            0.0,
            1.0);

        /// <summary>
        /// What the player's users think and where their number is heading.
        ///
        /// Satisfaction is measured against the alternative each audience actually has, including the
        /// option of buying nothing. That is the only comparison that means anything: a model can be
        /// excellent and still lose people if something better is on sale, and it can be modest and
        /// keep them if it is the best thing there is.
        /// </summary>
        /// <summary>
        /// The product on sale, for the banner and the management page.
        ///
        /// The flagship is the strongest live model that is not superseded inside its own line, which
        /// is exactly the model the market is choosing between. Reusing that rule rather than picking
        /// the newest keeps the banner describing the thing the simulation is actually selling.
        /// </summary>
        /// <summary>
        /// The model the company is actually selling, or null when nothing is on sale.
        ///
        /// Public because the banner, the management page and the standing all need to name the same
        /// model, and three copies of "strongest live model not superseded inside its own line" is
        /// three chances for them to disagree about what the company is.
        /// </summary>
        public DeployedModel Flagship()
        {
            DeployedModel best = null;
            var bestCapability = double.NegativeInfinity;

            foreach (var model in State.DeployedModels)
            {
                if (model == null || !model.IsLiveOn(State.Date) || IsSupersededInItsLine(model))
                {
                    continue;
                }

                var capability = model.EffectiveCapability(State.Date);
                if (capability > bestCapability)
                {
                    bestCapability = capability;
                    best = model;
                }
            }

            return best;
        }

        /// <summary>
        /// One product's standing, for its own corner banner.
        ///
        /// The company-wide figures are deliberately replaced by this model's own: a follower banner
        /// carries the people on it and what it has taken since release, because "net income" three
        /// times over would be the same number printed three times.
        /// </summary>
        public ProductStanding ProductFor(in ModelRecord record)
        {
            var model = record.Model;
            if (model == null)
            {
                return new ProductStanding(null, false, 0.0, 0.0, 0.0, 0L, 0L, 0, 0.0,
                    Market.FrontierCapability);
            }

            var capability = record.CapabilityToday;
            var age = State.Date.DayIndex - model.ReleaseDate.DayIndex;

            return new ProductStanding(
                model.Name,
                true,
                Sentiment().Satisfaction,
                ProductStanding.TopicalityOf(age, capability, Market.FrontierCapability),
                record.Users,
                model.LifetimeRevenueUsd,
                (long)Math.Round(record.Users),
                age,
                capability,
                Market.FrontierCapability);
        }

        /// <summary>Everything on sale right now, strongest first. One banner each.</summary>
        public List<ModelRecord> MarketedModels()
        {
            var marketed = new List<ModelRecord>();
            foreach (var record in ModelHistory())
            {
                if (record.IsMarketed)
                {
                    marketed.Add(record);
                }
            }

            marketed.Sort((left, right) =>
                right.CapabilityToday.CompareTo(left.CapabilityToday));

            return marketed;
        }

        public ProductStanding Product()
        {
            var best = Flagship();
            var bestCapability = best?.EffectiveCapability(State.Date) ?? 0.0;

            var month = Ledger.MonthKeyOf(State.Date);
            var earnings = State.Ledger.MonthTotal(month, LedgerLine.Subscriptions);
            var net = State.Ledger.MonthCashFlow(month);

            if (best == null)
            {
                return new ProductStanding(null, false, 0.0, 0.0, 0.0, earnings, net, 0, 0.0,
                    Market.FrontierCapability);
            }

            var age = State.Date.DayIndex - best.ReleaseDate.DayIndex;
            var frontier = Market.FrontierCapability;

            return new ProductStanding(
                best.Name,
                true,
                Sentiment().Satisfaction,
                ProductStanding.TopicalityOf(age, bestCapability, frontier),
                Sentiment().Users,
                earnings,
                net,
                age,
                bestCapability,
                frontier);
        }

        /// <summary>
        /// One row per live model: who is using it and what it earns.
        ///
        /// **Split by the same utility weight the market itself uses**, rather than tracked
        /// separately. A second set of per-model counters would be a second source of truth that
        /// could disagree with the revenue the company actually banks, and the first time it did
        /// the player would be reading a table that contradicts their own bank balance.
        ///
        /// Live models only. A retired model has no users, and a table that listed it would be
        /// showing the player a product they cannot do anything about.
        /// </summary>
        public List<ModelRow> ModelBoard()
        {
            var rows = new List<ModelRow>();
            var live = new List<DeployedModel>();

            foreach (var model in State.DeployedModels)
            {
                if (model.IsLiveOn(State.Date))
                {
                    live.Add(model);
                }
            }

            if (live.Count == 0)
            {
                return rows;
            }

            var month = Ledger.MonthKeyOf(State.Date);
            var earnings = State.Ledger.MonthTotal(month, LedgerLine.Subscriptions);
            var users = Sentiment().Users;

            var weights = new double[live.Count];
            var total = 0.0;

            for (var index = 0; index < live.Count; index++)
            {
                var model = live[index];

                weights[index] = Math.Exp(MarketShareModel.Utility(
                    model.EffectiveCapability(State.Date),
                    Math.Clamp(State.Reputation + model.BrandBonus(State.Date), 0.0, 1.0),
                    model.PriceMultiplier,
                    model.AgeYears(State.Date)));

                total += weights[index];
            }

            for (var index = 0; index < live.Count; index++)
            {
                var model = live[index];
                var share = total <= 0.0 ? 0.0 : weights[index] / total;

                rows.Add(new ModelRow(
                    model.Name,
                    model.Type,
                    model.EffectiveCapability(State.Date),
                    users * share,
                    users * share * SubscriberFraction,
                    (long)Math.Round(earnings * share),
                    model.DaysOnSale));
            }

            rows.Sort(static (left, right) => right.MonthEarningsUsd.CompareTo(left.MonthEarningsUsd));
            return rows;
        }

        /// <summary>
        /// How many users pay.
        ///
        /// One number for the whole company because the pricing model does not vary it per model,
        /// and inventing a per-model conversion rate here would be a figure with nothing behind it.
        /// </summary>
        public const double SubscriberFraction = 0.043;

        public UserSentiment Sentiment()
        {
            var breakdown = MarketByType();
            var users = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);

            var bestRival = 0.0;
            for (var owner = 1; owner < breakdown.OwnerUsersOverall.Count; owner++)
            {
                bestRival = Math.Max(bestRival, breakdown.OwnerUsersOverall[owner]);
            }

            var entrants = BuildEntrants(PlayerBrand(), State.Rivals.LiveModels(State.Date));
            var segments = AudienceCatalog.All;
            var segmentShares = AudienceCatalog.SharesOn(State.Date);

            var satisfactionWeight = 0.0;
            var satisfaction = 0.0;
            var momentum = 0.0;

            for (var index = 0; index < segments.Count; index++)
            {
                var definition = segments[index];

                var mine = 0.0;
                var theirs = SegmentMarket.WalkAwayScore(definition);

                for (var entry = 0; entry < entrants.Count; entry++)
                {
                    var score = SegmentMarket.Attractiveness(entrants[entry], definition, State.Date);
                    if (entrants[entry].IsPlayer)
                    {
                        mine = Math.Max(mine, score);
                    }
                    else
                    {
                        theirs = Math.Max(theirs, score);
                    }
                }

                var held = State.Segments.PlayerShareIn(definition.Segment);

                if (held > 0.0 && mine + theirs > 0.0)
                {
                    // How strongly this audience prefers what the player sells over the best thing
                    // they could switch to, walking away included.
                    //
                    // The first version of this divided by whichever was larger, which saturated at
                    // one: a company that was merely the best option read as delighted no matter what
                    // it charged, and quadrupling the price moved the figure by nothing at all. A
                    // preference share cannot saturate, so being ahead still costs something when the
                    // deal gets worse. Same logit family as the market itself.
                    satisfaction += mine / (mine + theirs) * held;
                    satisfactionWeight += held;
                }

                // Where this audience is heading, weighted by how much of the market it is.
                momentum += (State.Segments.LastTargets[index] - held) * segmentShares[index];
            }

            return new UserSentiment(
                users,
                satisfactionWeight <= 0.0 ? 0.0 : satisfaction / satisfactionWeight,
                momentum,
                bestRival);
        }

        private (double Share, double Demanded, double Served, long Revenue) ServeMarket(
            ComputeProfile profile,
            MarketConditions market)
        {
            var rivals = State.Rivals.LiveModels(State.Date);
            var brand = PlayerBrand();

            // The segmented market replaced a single instant logit over one global pool. It is the
            // same utility function scoring the same products; what changed is that the standing
            // moves toward the answer over days instead of being it, and that it is tracked per
            // audience rather than as one number for everybody.
            var share = State.Segments.Advance(
                BuildEntrants(brand, rivals),
                State.Date,
                market.TotalDemandBillionTokensPerDay,
                State.Awareness);

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

            // What today was like to use, recorded for tomorrow's market and for the operations panel.
            State.LastQuality = new ServiceQuality(
                demanded, capacityBillions, State.Pool.PackagedQuality);

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

        /// <summary>
        /// A day of discovery.
        ///
        /// Two sources, and they are deliberately different. Work earns points because a lab learns
        /// by building, so a company that ships nothing learns nothing however much it spends. Money
        /// buys points on a square root curve, so a rich company can hire the answer but cannot
        /// simply purchase the tree.
        /// </summary>
        private void AdvanceResearchPoints()
        {
            var fromWork = ResearchBudget.PointsFromWork(
                State.ActiveRun != null,
                State.UpgradeProjects.Count > 0,
                State.Staff.Hires.Count,
                State.Skills.ResearchDepthMultiplier());

            var budget = ResearchBudget.MonthlyBudgetUsd(
                State.ResearchFunding,
                State.ResearchMonthlyUsd,
                State.ResearchRevenueShare,
                MonthlyRevenueUsd());

            // The month's budget is paid a thirtieth at a time, so the bill and the points arrive
            // together rather than the money leaving in one lump nobody sees.
            var dailySpend = (long)Math.Round(budget / 30.0);
            if (dailySpend > 0L)
            {
                State.PostCash(LedgerLine.Research, dailySpend);
            }

            var fromMoney = ResearchBudget.PointsFromFunding(budget) / 30.0;

            // The office adds a few per cent on top, from whatever is standing on the floor. It
            // lifts what the work and the money already earned rather than being a source of its
            // own, so a lab that does no research still gets nothing out of a nice sofa.
            var fromRoom = 1.0 + (State.Decor?.ResearchBonus ?? 0.0);

            State.ResearchPointsToday = (fromWork + fromMoney) * fromRoom;
            State.ResearchPoints += State.ResearchPointsToday;
        }

        /// <summary>
        /// What came in over the last thirty recorded days, for the revenue share mode.
        ///
        /// Read from the ledger rather than tracked a second time, so the figure the player sets a
        /// percentage of is the same figure the finance report shows them.
        /// </summary>
        public long MonthlyRevenueUsd()
        {
            var month = Ledger.MonthKeyOf(State.Date);
            var thisMonth = State.Ledger.MonthTotal(month, LedgerLine.Subscriptions);
            var previous = State.Ledger.MonthTotal(month - 1, LedgerLine.Subscriptions);

            // Early in a month there is barely anything recorded yet, so the larger of this month so
            // far and the whole of last month is the honest answer rather than a figure that collapses
            // on the first of every month.
            return Math.Max(thisMonth, previous);
        }

        /// <summary>
        /// A day of campaigns.
        ///
        /// Marketing buys awareness and nothing else. It does not touch capability, reliability or
        /// what an audience will pay, because those are what the product is. What it does touch is
        /// whether anybody is considering the product at all, which is enough to matter and is the
        /// only honest thing advertising does.
        ///
        /// The version this replaces added a number straight to reputation, which is the exact shape
        /// the design note forbids. Standing still moves with marketing, through the same named
        /// driver as everything else rather than through a second private channel.
        /// </summary>
        private void AdvanceMarketing()
        {
            var spend = State.Awareness.Advance(
                State.Campaigns, State.Date, State.Reputation, State.Random,
                State.Segments.PlayerShareIn);

            if (spend > 0L)
            {
                State.PostCash(LedgerLine.Marketing, spend);

                // It was reaching the books and the bank and not the lifetime total, so every report
                // built on that total understated what the company had spent. Found the day tax
                // stopped being added to the same figure and stopped hiding the gap.
                State.LifetimeOperatingCostUsd += spend;
            }

            // Finished bookings are cleared here rather than by the interface, so a campaign that has
            // run its term stops costing money whether or not anybody is looking at the screen.
            for (var index = State.Campaigns.Count - 1; index >= 0; index--)
            {
                var campaign = State.Campaigns[index];
                if (campaign.HasFinished(State.Date))
                {
                    State.RemoveCampaign(campaign);
                    State.RaiseEvent(new CompanyEvent(
                        CompanyEventType.MarketingFinished,
                        State.Date,
                        $"The {campaign.TermMonths} month campaign has run its course."));
                }
            }
        }

        /// <summary>What the running campaigns cost today, for the standing driver and the panel.</summary>
        public long MarketingDailyUsd()
        {
            var total = 0L;
            foreach (var campaign in State.Campaigns)
            {
                if (!campaign.HasFinished(State.Date))
                {
                    total += campaign.DailyCostUsd;
                }
            }

            return total;
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

        /// <summary>
        /// Credits the day's takings to the models that earned them.
        ///
        /// Foundations for the model history screen: "what did the thirtieth model earn" is a
        /// question nothing can answer after the fact, so it is recorded as it happens.
        ///
        /// **The split is by the audience each model's kind actually holds**, which the segmented
        /// market already computes, and only marketed models are credited: a superseded model in a
        /// line is not on sale, so it earns nothing, which is exactly what the market thinks too.
        /// Where two lines share a kind the split is by capability, because capability is what wins
        /// the share in the first place. This can never disagree with the books: the parts are taken
        /// as shares of one revenue figure rather than being computed a second time.
        /// </summary>
        private void RecordModelDay(long revenue)
        {
            var marketed = new List<DeployedModel>();
            foreach (var model in State.DeployedModels)
            {
                if (model != null && model.IsLiveOn(State.Date) && !IsSupersededInItsLine(model))
                {
                    marketed.Add(model);
                }
            }

            if (marketed.Count == 0)
            {
                return;
            }

            var breakdown = MarketByType();
            var weights = new double[marketed.Count];
            var total = 0.0;

            for (var index = 0; index < marketed.Count; index++)
            {
                var users = breakdown.TryGetType(marketed[index].Type, out var standing)
                    ? standing.PlayerUsers
                    : 0.0;

                // Several lines can sell the same kind. Capability breaks the tie, and it is also
                // the tie-break the market itself used to hand out that share.
                var weight = users * Math.Max(0.01, marketed[index].EffectiveCapability(State.Date));
                weights[index] = weight;
                total += weight;
            }

            if (total <= 0.0)
            {
                // Nobody is holding anybody yet, so nothing is earned and nothing is credited. The
                // day still counts as a day on sale, because it was.
                foreach (var model in marketed)
                {
                    model.RecordDay(0L, 0.0);
                }

                return;
            }

            var credited = 0L;
            for (var index = 0; index < marketed.Count; index++)
            {
                var share = weights[index] / total;

                // The last one takes the remainder so the parts add up to the whole exactly, the
                // same rule the four fleet bills follow.
                var slice = index == marketed.Count - 1
                    ? revenue - credited
                    : (long)Math.Round(revenue * share);

                credited += slice;

                var users = breakdown.TryGetType(marketed[index].Type, out var standing)
                    ? standing.PlayerUsers * share
                    : 0.0;

                marketed[index].RecordDay(slice, users);
            }
        }

        // ------------------------------------------------------------------ the inbox

        /// <summary>Days a demand may sit before it starts costing more.</summary>
        public const int DemandGraceDays = 45;

        /// <summary>What an ignored demand adds, per year overdue, on top of itself.</summary>
        public const double LatePenaltyPerYear = 0.35;

        /// <summary>Standing lost when a demand goes unpaid past its deadline.</summary>
        public const double LateStandingLoss = 0.05;

        /// <summary>
        /// The longest a tax demand can be pushed back, in days. Two and a half years.
        ///
        /// It is a real arrangement rather than an excuse: the revenue will wait, at a price, and the
        /// price is what makes it a decision. Deferring buys the company the one thing it cannot buy
        /// with anything else, which is time to let a model it has already paid for start earning.
        /// </summary>
        public const int LongestDeferralDays = 913;

        /// <summary>What each deferral adds to what is owed. The author's figure.</summary>
        public const double DeferralInterest = 0.086;

        /// <summary>How far one deferral pushes the date. Three of these reach the ceiling.</summary>
        public const int DeferralStepDays = 304;

        /// <summary>Roughly how often somebody writes in asking for a job.</summary>
        public const int ApplicantIntervalDays = 70;

        /// <summary>Corporation tax owed so far this year, held rather than taken.</summary>
        private void AccrueTax(long tax)
        {
            var year = State.Date.Year;
            if (State.TaxYear != year)
            {
                State.TaxYear = year;
            }

            State.AccruedTaxUsd += Math.Max(0L, tax);
        }

        private void AdvanceMail()
        {
            IssueTaxDemand();
            ChaseOverdueDemands();
            InviteApplicant();
        }

        /// <summary>
        /// The year's tax, billed on the second of January for the year that just ended.
        ///
        /// This is the one letter the player can plan for, which is the point: the liability has been
        /// visible all year, so a company that cannot pay in January decided that in September.
        /// </summary>
        private void IssueTaxDemand()
        {
            if (State.Date.Month != 1 || State.Date.Day != 2)
            {
                return;
            }

            var owed = State.AccruedTaxUsd;
            State.AccruedTaxUsd = 0L;

            if (owed <= 0L)
            {
                return;
            }

            var year = State.Date.Year - 1;
            var letter = State.Mail.Add(MailKind.TaxDemand, State.Date,
                WorldRegionCatalog.Get(State.HomeCountry).DisplayName + " Revenue",
                $"Corporation tax for {year}",
                $"Assessed on {year} operating profit at "
                + $"{State.Home.TaxRate:P0}. The amount was accrued across the year as it was earned; "
                + "this is the demand for it.\n\nUnpaid after the due date it attracts "
                + $"{LatePenaltyPerYear:P0} a year and is a matter of public record.");

            letter.AmountUsd = owed;
            letter.DueDayIndex = State.Date.DayIndex + DemandGraceDays;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.TaxDemanded, State.Date,
                $"Corporation tax for {year} is due: {Usd(owed)}.", owed));
        }

        /// <summary>
        /// What ignoring a demand costs.
        ///
        /// It grows rather than being a one off, and it costs standing as well as money, because a
        /// penalty that is merely a fixed fee is a price a rich company would happily pay to never
        /// think about the letter again.
        /// </summary>
        private void ChaseOverdueDemands()
        {
            foreach (var letter in State.Mail.All)
            {
                if (letter.IsClosed || letter.AmountUsd <= 0L || !letter.IsOverdue(State.Date))
                {
                    continue;
                }

                var added = (long)Math.Round(letter.AmountUsd * LatePenaltyPerYear / 365.0);
                if (added <= 0L)
                {
                    continue;
                }

                letter.AmountUsd += added;

                // Once, on the day it tips over, rather than every day it stays there.
                if (State.Date.DayIndex == letter.DueDayIndex + 1)
                {
                    State.Reputation -= LateStandingLoss;

                    State.RaiseEvent(new CompanyEvent(CompanyEventType.DemandOverdue, State.Date,
                        $"{letter.Subject} is overdue. It is now growing at "
                        + $"{LatePenaltyPerYear:P0} a year.", letter.AmountUsd));
                }
            }
        }

        /// <summary>
        /// Somebody writes in asking for a job at a price.
        ///
        /// It is the same hire the team screen offers, reached differently: **the screen is the
        /// company going looking, the letter is somebody arriving**, and the letter is the only one
        /// of the two where the price is negotiable. Asking is above the going rate, because a
        /// candidate who writes to you first thinks they are worth more than the market says.
        /// </summary>
        private void InviteApplicant()
        {
            if (State.IsBankrupt)
            {
                return;
            }

            // Deliberately not gated on having a free desk. People apply to companies that are full,
            // and a letter arriving when there is nowhere to seat anybody is the clearest thing the
            // game can say about why the next office is worth its rent. Accepting is what refuses,
            // and it refuses with the reason.
            State.DaysUntilNextApplicant--;
            if (State.DaysUntilNextApplicant > 0)
            {
                return;
            }

            State.DaysUntilNextApplicant =
                Math.Max(20, ApplicantIntervalDays + State.Random.NextInt(-25, 26));

            var roles = StaffCatalog.All;
            if (roles.Count == 0)
            {
                return;
            }

            // **Unsolicited applicants go through the same machinery as searched-for ones.**
            // They used to be their own kind of letter with their own accept-or-haggle-once rules,
            // which meant the game had two hiring models and the nicer one was the one the player
            // had to go looking for. Somebody writing in out of the blue is now simply a candidate
            // the company did not have to pay to find.
            // Rolled from hiring's own stream for the same reason the shortlists are: an
            // applicant writing in must not shift the sequence the market runs on.
            var positions = PositionCatalog.All;
            var position = positions[State.Hiring.Random.NextInt(0, positions.Count)];

            var candidate = Candidate.Roll(State.Hiring.NextCandidateId++, position.Skill,
                HireSource.Agency, State.Hiring.Random.NextInt(18, 62), State.Hiring.Random);

            var letter = State.Mail.Add(MailKind.JobOffer, State.Date, candidate.Name,
                $"Speculative application: {position.Title}",
                $"You have not advertised, but I wanted to write anyway. I have "
                + $"{candidate.TrueLevel} in "
                + $"{PlayerSkillCatalog.Get(candidate.Position).DisplayName.ToLowerInvariant()} "
                + $"and I am looking for my next thing.\n\nI am asking "
                + $"${candidate.AskingHourlyUsd:N2} an hour, which is "
                + $"{Usd(candidate.AnnualSalaryUsd(candidate.AskingHourlyUsd))} a year. I know that "
                + "is above the going rate and I think I am worth it."
                + NoRoomNote());

            letter.Role = position.Role;
            letter.Skill = candidate.RoleSkill;
            letter.AskingSalaryUsd = candidate.AnnualSalaryUsd(candidate.AskingHourlyUsd);
            letter.Candidate = candidate;
            letter.OfferedHourlyUsd = candidate.AskingHourlyUsd;
            letter.DueDayIndex = State.Date.DayIndex + 30;
        }

        /// <summary>
        /// The one way a letter can change anything.
        ///
        /// Everything the interface can do to the inbox comes through here, so the rules for what a
        /// letter means live beside the rest of the rules rather than in a screen. The screen knows a
        /// letter has a Pay button; it does not know what paying does.
        /// </summary>
        /// <summary>
        /// Appended to an application when there is nowhere to seat the person.
        ///
        /// The letter still arrives, because people apply to companies that are full and that is
        /// the clearest thing the game can say about why the next office is worth its rent. What
        /// it must not do is let the player find out only when the button refuses.
        /// </summary>
        private string NoRoomNote() => State.Staff.HasFreeDesk
            ? string.Empty
            : $"\n\n[There is nowhere for them to sit. {State.Staff.OfficeDefinition.DisplayName} "
              + $"holds {State.Staff.Desks} and {State.Staff.Headcount} are taken.]";

        public bool TryActOnMail(int mailId, MailAction action, out string failureReason)
        {
            failureReason = string.Empty;

            if (!State.Mail.TryGet(mailId, out var letter))
            {
                failureReason = "No such letter.";
                return false;
            }

            if (letter.IsClosed)
            {
                failureReason = "That has already been dealt with.";
                return false;
            }

            switch (action)
            {
                case MailAction.Pay:
                    return PayDemand(letter, out failureReason);

                case MailAction.Accept:
                    return AcceptApplicant(letter, out failureReason);

                case MailAction.Haggle:
                    return HaggleApplicant(letter, out failureReason);

                case MailAction.Defer:
                    return DeferDemand(letter, out failureReason);

                case MailAction.Decline:
                    letter.IsClosed = true;
                    letter.Outcome = "Declined.";
                    return true;

                default:
                    failureReason = "Nothing to do.";
                    return false;
            }
        }

        private bool PayDemand(MailItem letter, out string failureReason)
        {
            failureReason = string.Empty;

            if (letter.AmountUsd <= 0L)
            {
                failureReason = "Nothing owed.";
                return false;
            }

            if (State.CashUsd < letter.AmountUsd)
            {
                failureReason = $"{Usd(letter.AmountUsd)} owed and {Usd(State.CashUsd)} in the "
                    + "account. It keeps growing while it is unpaid.";

                return false;
            }

            var line = letter.Kind == MailKind.TaxDemand ? LedgerLine.Tax : LedgerLine.Fines;
            State.PostCash(line, letter.AmountUsd);
            State.LifetimeOperatingCostUsd += letter.AmountUsd;

            if (letter.Kind == MailKind.TaxDemand)
            {
                State.LifetimeTaxPaidUsd += letter.AmountUsd;
            }
            else
            {
                State.LifetimeFinesUsd += letter.AmountUsd;
            }

            letter.IsClosed = true;
            letter.Outcome = $"Paid {Usd(letter.AmountUsd)}.";
            letter.AmountUsd = 0L;
            return true;
        }

        /// <summary>
        /// Pushes a tax demand back, at a price.
        ///
        /// **Deferring is not the same as ignoring**, and the two must not converge. Ignoring grows
        /// the debt faster, costs standing the day it tips over, and is a matter of public record.
        /// Deferring costs more in total than paying now and nothing at all in standing, because the
        /// company asked. A player who cannot tell those apart will use the wrong one, so the
        /// deferred letter says the new figure and the new date in as many words.
        ///
        /// The interest compounds on what is already owed rather than on the original bill, so three
        /// deferrals cost more than three times one. That is the whole reason there is a ceiling: at
        /// simple interest a company could roll the debt forever for a fixed annual fee.
        /// </summary>
        private bool DeferDemand(MailItem letter, out string failureReason)
        {
            failureReason = string.Empty;

            if (letter.Kind != MailKind.TaxDemand)
            {
                failureReason = "Only the revenue will wait. A penalty will not.";
                return false;
            }

            if (letter.DeferredDays >= LongestDeferralDays)
            {
                failureReason = $"Already deferred {letter.DeferredDays} days, which is the limit. "
                    + "It has to be paid.";

                return false;
            }

            var step = Math.Min(DeferralStepDays, LongestDeferralDays - letter.DeferredDays);
            var added = (long)Math.Round(letter.AmountUsd * DeferralInterest);

            letter.DeferredDays += step;
            letter.AmountUsd += added;
            letter.DueDayIndex += step;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.TaxDeferred, State.Date,
                $"Tax deferred {step} days for {Usd(added)}. Now {Usd(letter.AmountUsd)}, "
                + $"due {new GameDate(letter.DueDayIndex)}.", added));

            return true;
        }

        private bool AcceptApplicant(MailItem letter, out string failureReason)
        {
            failureReason = string.Empty;

            if (letter.Kind != MailKind.JobOffer)
            {
                failureReason = "Nothing to accept.";
                return false;
            }

            // Anything written since v31 carries the person, and goes through the negotiation
            // path so the agreed rate is what gets paid.
            if (letter.Candidate != null)
            {
                var verdict = AcceptAsking(letter, out failureReason);
                return verdict == OfferVerdict.Accepted;
            }

            // A letter restored from an older save has only a role and a band. It is hired at the
            // catalog rate, which is exactly what that save was already paying for that person.
            if (State.IsBankrupt)
            {
                failureReason = "The company is insolvent.";
                return false;
            }

            if (!State.Staff.HasFreeSeat)
            {
                failureReason =
                    $"No free desk. {State.Staff.OfficeDefinition.DisplayName} holds "
                    + $"{State.Staff.Desks}.";
                return false;
            }

            if (!StaffCatalog.TryGet(letter.Role, out var legacy))
            {
                failureReason = "Unknown role.";
                return false;
            }

            var fee = legacy.HiringCostUsd_ForSkill(letter.Skill);

            if (State.CashUsd < fee)
            {
                failureReason = $"Hiring costs ${fee:N0}, has ${State.CashUsd:N0}.";
                return false;
            }

            if (!State.Staff.Add(new Hire(letter.Role, letter.Skill, State.Date)))
            {
                failureReason = "There is nowhere for them to sit.";
                return false;
            }

            State.PostCash(LedgerLine.Salaries, fee);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.StaffHired, State.Date,
                $"Hired a {legacy.DisplayName.ToLowerInvariant()} at band {letter.Skill}.", fee));

            letter.IsClosed = true;
            letter.Outcome = $"Hired at {Usd(letter.AskingSalaryUsd)} a year.";
            return true;
        }

        /// <summary>
        /// The standing counter-offer, for callers that do not have a wage panel.
        ///
        /// The screen lets the player name their own number; this is what HAGGLE means to anything
        /// that just wants to push back once — a counter at
        /// <see cref="StandardCounterFraction"/> of what was asked, judged by the same rules as any
        /// other offer. **All three outcomes are possible**, which is the difference from the old
        /// model: a counter can now succeed outright and hire them, rather than only ever lowering
        /// a price the player then had to accept separately.
        /// </summary>
        private bool HaggleApplicant(MailItem letter, out string failureReason)
        {
            failureReason = string.Empty;

            if (letter.Kind != MailKind.JobOffer || letter.IsClosed)
            {
                failureReason = "Nothing to negotiate.";
                return false;
            }

            if (letter.Candidate == null)
            {
                failureReason = "That letter has no candidate behind it.";
                return false;
            }

            var counter = letter.Candidate.AskingHourlyUsd * StandardCounterFraction;
            var verdict = Negotiate(letter, counter, 0L, out failureReason);

            return verdict != OfferVerdict.WalkedAway;
        }

        /// <summary>
        /// What a one-click counter offers, against what they asked.
        ///
        /// Eight per cent under. Inside the four-to-eighteen band candidates are willing to move,
        /// so it works often enough to be worth pressing and fails often enough to be a decision.
        /// </summary>
        public const double StandardCounterFraction = 0.92;


        private static string Usd(long amount) => "$" + amount.ToString("N0");

        /// <summary>Every retainer the company holds, not just the dearest one. They all invoice.</summary>
        private long DailyIntelRetainerUsd()
        {
            var monthly = 0L;
            foreach (var tier in State.Memberships)
            {
                monthly += IntelligenceService.MonthlyRetainerUsd(tier);
            }

            return SimUnits.ToDollars(monthly / DaysPerMonth);
        }

        public long MonthlyIntelRetainerUsd()
        {
            var monthly = 0L;
            foreach (var tier in State.Memberships)
            {
                monthly += IntelligenceService.MonthlyRetainerUsd(tier);
            }

            return monthly;
        }

        /// <summary>
        /// Every desk on retainer files on its own clock, at its own tier.
        ///
        /// **Per outfit, not per company.** Running one clock at the best membership meant a company
        /// paying National Press and TrendSearch had every note routed to Total True News, and Event
        /// Hunter, which needs both to open, never received anything at all. Three retainers is three
        /// invoices and three columns; the tier still decides lead time and hit rate, so buying
        /// everything buys more coverage rather than better coverage.
        /// </summary>
        private void AdvanceIntelligence()
        {
            AdvanceLabHistory();
            AdvanceDossiers();

            foreach (var tier in NewsCatalog.Memberships)
            {
                if (!State.IsMember(tier))
                {
                    continue;
                }

                var left = State.CountdownFor(tier) - 1;
                if (left > 0)
                {
                    State.SetCountdownFor(tier, left);
                    continue;
                }

                var interval = IntelligenceService.ReportIntervalDays(tier);
                State.SetCountdownFor(tier, Math.Max(1, interval + State.Random.NextInt(-6, 7)));

                var signal = IntelligenceService.Generate(tier, State.Date, State.Rivals, State.Random);
                State.AddSignal(signal);

                // The note goes on that desk's own page rather than into the general wire, so paying
                // for one outfit fills one column and the player can see what their money bought.
                State.News.Add(NewsDesk.FromSignal(signal));

                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.IntelReceived,
                    State.Date,
                    $"{signal.Headline} (desk confidence {signal.Confidence:P0})."));
            }
        }

        /// <summary>
        /// The rivals' own history, filed on the day it happens.
        ///
        /// **Free, and deliberately so.** The paid desks sell advance warning; this is the public
        /// record arriving at the same time it arrives for everybody. A player who buys nothing
        /// still watches three companies come apart over four years.
        /// </summary>
        private void AdvanceLabHistory()
        {
            foreach (var (lab, chapter) in LabDossiers.ChaptersOn(State.Date))
            {
                State.News.Add(NewsDesk.FromLabChapter(lab, chapter));
            }
        }

        /// <summary>
        /// KnownWords filing on one rival at a time, in order, so the column fills rather than
        /// repeating whoever is largest.
        ///
        /// Nothing here is rolled. The dossier reads the lab's live state and the market's own user
        /// count, which is why it can say something the player could not already see without ever
        /// being able to contradict the game.
        /// </summary>
        private void AdvanceDossiers()
        {
            if (!State.IsMember(IntelTier.KnownWords))
            {
                return;
            }

            State.DaysUntilNextDossier--;
            if (State.DaysUntilNextDossier > 0)
            {
                return;
            }

            State.DaysUntilNextDossier = NewsDesk.DossierIntervalDays;

            var agents = State.Rivals.Agents;
            if (agents.Count == 0)
            {
                return;
            }

            var index = State.NextDossierLab % agents.Count;
            State.NextDossierLab = (index + 1) % agents.Count;

            var lab = agents[index];
            var breakdown = MarketByType();
            var owner = index + 1;

            var users = owner < breakdown.OwnerUsersOverall.Count
                ? breakdown.OwnerUsersOverall[owner]
                : 0.0;

            // Their revenue on the player's own arithmetic: the people they hold, each consuming and
            // paying what this market says a person consumes and pays.
            var market = MarketModel.Evaluate(State.Date, State.Rivals.FrontierCapability(State.Date));
            var perUserPerDay = AudienceCatalog.AverageTokensPerUserPerDay(State.Date);
            var revenuePerYear = users * perUserPerDay * 365.0 / 1_000_000.0
                * market.PricePerMillionTokensUsd * lab.LivePrice;

            State.News.Add(NewsDesk.Dossier(lab, State.Date, users,
                SimUnits.Finite(revenuePerYear), ReleasesShippedBy(lab)));
        }

        /// <summary>
        /// How many models a lab has put out by today. Counted from the reference table, which is the
        /// only record of it, plus the one it is selling if that came after the table ran out.
        /// </summary>
        private int ReleasesShippedBy(CompetitorAgent lab)
        {
            var shipped = 0;
            var lastCatalogued = new GameDate(GameDate.MinimumDayIndex);

            foreach (var release in CompetitorCatalog.All)
            {
                if (release.Competitor == lab.Competitor && State.Date.IsOnOrAfter(release.ReleaseDate))
                {
                    shipped++;
                    if (release.ReleaseDate.IsOnOrAfter(lastCatalogued))
                    {
                        lastCatalogued = release.ReleaseDate;
                    }
                }
            }

            // Past the end of the table a lab invents its own successors, and those are not written
            // down anywhere. What is on sale now is evidence of one more than the table knows about.
            if (lab.HasShipped && lab.LiveReleaseDate.IsOnOrAfter(lastCatalogued.AddDays(1)))
            {
                shipped++;
            }

            return shipped;
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

        /// <summary>
        /// One day of public opinion, and the following that trails it.
        ///
        /// The drivers live in <see cref="Standing"/> so each can be tested alone and so the interface
        /// can say which one moved. Reputation is still a single number nudged in a single place;
        /// what changed is that the nudge is now explainable.
        /// </summary>
        private void UpdateReputation(double share, double served)
        {
            // Marketing intensity as a fraction of a spend that would be unmistakable. A company
            // spending a hundred thousand a day is being seen everywhere; below that it scales.
            var marketing = Math.Clamp(MarketingDailyUsd() / 100_000.0, 0.0, 1.0);

            var change = Standing.Today(
                share,
                served,
                State.Monetization.Generosity,
                State.Date.DayIndex - State.LastReleaseDate.DayIndex,
                State.Monetization.PaidPriceMultiplier,
                marketing,
                State.Founder.ReputationGainMultiplier,
                State.Reputation);

            State.LastStandingChange = change;
            State.Reputation += change.Total;

            // Fans follow the users the company actually holds, weighted by how it is regarded.
            var breakdown = MarketByType();
            var users = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);

            State.Fans = Standing.AdvanceFans(
                State.Fans, Standing.FanTarget(users, State.Reputation));

            // One number a day, written by the code that already worked it out. A chart built from a
            // second calculation would eventually disagree with the counter beside it.
            State.Users.Record(users);
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
