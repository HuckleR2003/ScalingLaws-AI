using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Persistence
{
    /// <summary>
    /// The ONE way a campaign gets written down or read back.
    ///
    /// Loading always runs the same three steps in the same order: upgrade the file to the current
    /// version, clamp every field into a range the simulation can survive, then build state from it.
    /// A save that cannot be understood starts a new campaign rather than a corrupt one.
    /// </summary>
    public static class SaveStore
    {
        private const string SaveKey = "ScalingLaws.Campaign";

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public static void Save(CompanyState state)
        {
            var data = Capture(state);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Reads the stored campaign, or null when there is nothing readable there.</summary>
        public static SaveData LoadData()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return null;
            }

            return Parse(PlayerPrefs.GetString(SaveKey, string.Empty));
        }

        /// <summary>
        /// Turns raw JSON of any supported version into a sanitized current-version save. Exposed
        /// separately from <see cref="LoadData"/> so tests can feed it a v1 payload directly.
        /// </summary>
        public static SaveData Parse(string json)
        {
            try
            {
                var upgraded = SaveMigration.Upgrade(json, JsonUtility.FromJson);
                return upgraded == null ? null : Sanitize(upgraded);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Scaling Laws] Save was unreadable and has been ignored: {exception.Message}");
                return null;
            }
        }

        /// <summary>Loads a playable company, falling back to a fresh one when the save is unusable.</summary>
        public static CompanyState LoadOrCreate(string companyName = "Newco")
        {
            var data = LoadData();
            return data == null ? new CompanyState(companyName) : Restore(data);
        }

        // ------------------------------------------------------------------ capture

        public static SaveData Capture(CompanyState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                hardwareCatalogVersion = HardwareCatalog.CatalogVersion,
                companyName = state.CompanyName,
                dayIndex = state.Date.DayIndex,
                cashUsd = state.CashUsd,
                randomState = state.Random.State,
                reputation = state.Reputation,
                trainingComputeShare = state.TrainingComputeShare,
                ownedDataSources = (int)state.OwnedDataSources,
                lifetimeRevenueUsd = state.LifetimeRevenueUsd,
                lifetimeOperatingCostUsd = state.LifetimeOperatingCostUsd,
                lifetimeCapitalSpentUsd = state.LifetimeCapitalSpentUsd,
                datacenterOrdered = state.DatacenterOrdered,
                datacenterReadyDayIndex = state.DatacenterReadyDate.DayIndex,
                isBankrupt = state.IsBankrupt,
                daysInDebt = state.DaysInDebt,
                rentedPetaflops = state.Pool.RentedPetaflops,
                archetype = (int)state.Archetype,
                defaultPriceMultiplier = state.DefaultPriceMultiplier
            };

            foreach (var trait in state.Founder.Traits)
            {
                data.founderTraits.Add((int)trait);
            }

            foreach (var node in state.UnlockedResearch)
            {
                data.unlockedResearch.Add((int)node);
            }

            foreach (var loan in state.Loans.Loans)
            {
                data.loans.Add(new LoanData
                {
                    product = (int)loan.Product,
                    takenOnDayIndex = loan.TakenOn.DayIndex,
                    principalUsd = loan.PrincipalUsd,
                    totalRepaymentUsd = loan.TotalRepaymentUsd,
                    termDays = loan.TermDays,
                    graceDays = loan.GraceDays,
                    repaidUsd = loan.RepaidUsd,
                    daysInArrears = loan.DaysInArrears
                });
            }

            data.founderName = state.FounderName;

            data.skillLevels = new List<int>(state.Skills.LevelsToArray());

            data.skillExperience = new List<long>(state.Skills.ExperienceToArray());
            data.worldRegion = (int)state.Region;
            data.homeCountry = (int)state.HomeCountry;
            data.lifetimeTaxPaidUsd = state.LifetimeTaxPaidUsd;
            data.segmentShares = new List<double>(state.Segments.ToArray());
            data.segmentOwnerCount = state.Segments.OwnerCount;
            data.segmentTypeCount = ModelTypeCatalog.All.Count;

            data.pricingModel = (int)state.Monetization.Model;

            data.paidPriceMultiplier = state.Monetization.PaidPriceMultiplier;

            data.subscriptionPriceUsdPerMonth = state.Monetization.SubscriptionPriceUsdPerMonth;

            data.freeTierTokensPerUserPerDay = state.Monetization.FreeTierTokensPerUserPerDay;

            data.companyMarketingDailyUsd = state.Monetization.CompanyMarketingDailyUsd;

            data.modelMarketingDailyUsd = state.Monetization.ModelMarketingDailyUsd;

            data.modelAwareness = state.Monetization.ModelAwareness;

            data.lifetimeFreeTokensBillions = state.LifetimeFreeTokensBillions;

            data.officeTier = (int)state.Staff.Office;
            data.lifetimeFinesUsd = state.LifetimeFinesUsd;

            foreach (var hire in state.Staff.Hires)
            {
                data.staff.Add(new HireData
                {
                    role = (int)hire.Role,
                    skill = hire.Skill,
                    startedDayIndex = hire.StartedOn.DayIndex
                });
            }

            foreach (var incident in state.Incidents)
            {
                data.incidents.Add(new IncidentData
                {
                    severity = (int)incident.Severity,
                    dayIndex = incident.Date.DayIndex,
                    headline = incident.Headline,
                    reputationLoss = incident.ReputationLoss,
                    fineUsd = incident.FineUsd,
                    forcedWithdrawal = incident.ForcedWithdrawal
                });
            }

            var research = state.ActiveResearch;
            data.hasResearchProject = research != null;
            if (research != null)
            {
                data.researchNode = (int)research.Node;
                data.researchStartedDayIndex = research.StartedOn.DayIndex;
                data.researchDurationDays = research.DurationDays;
                data.researchPetaflopDaysRequired = research.PetaflopDaysRequired;
                data.researchPetaflopDaysCompleted = research.PetaflopDaysCompleted;
                data.researchDaysCompleted = research.DaysCompleted;
                data.researchCashPaidUsd = research.CashPaidUsd;
            }

            foreach (var architecture in state.AdoptedArchitectures)
            {
                data.adoptedArchitectures.Add((int)architecture);
            }

            foreach (var asset in state.Pool.Assets)
            {
                data.assets.Add(new HardwareAssetData
                {
                    generationId = (int)asset.GenerationId,
                    tier = (int)asset.Tier,
                    units = asset.Units,
                    purchaseDayIndex = asset.PurchaseDate.DayIndex,
                    pricePerUnitUsd = asset.PurchasePricePerUnitUsd,
                    leadTimeDays = asset.CommissionDate.DayIndex - asset.PurchaseDate.DayIndex
                });
            }

            foreach (var model in state.DeployedModels)
            {
                data.models.Add(new DeployedModelData
                {
                    name = model.Name,
                    architecture = (int)model.Architecture,
                    capability = model.Capability,
                    releaseDayIndex = model.ReleaseDate.DayIndex,
                    activeParameterCount = model.ActiveParameterCount,
                    priceMultiplier = model.PriceMultiplier,
                    isRetired = model.IsRetired,
                    modelType = (int)model.Type,
                    family = model.Family,
                    traitLevels = new List<int>(model.Traits.ToArray())
                });
            }

            state.Ledger.Capture(data.ledgerMonths, data.ledgerAmounts);

            foreach (var shelved in state.Shelf)
            {
                data.shelf.Add(new TrainedModelData
                {
                    name = shelved.Name,
                    architecture = (int)shelved.Architecture,
                    capability = shelved.Capability,
                    completedDayIndex = shelved.CompletedOn.DayIndex,
                    activeParameterCount = shelved.ActiveParameterCount,
                    projectedCapability = shelved.ProjectedCapability,
                    modelType = (int)shelved.Type,
                    family = shelved.Family
                });
            }

            foreach (var project in state.UpgradeProjects)
            {
                data.upgrades.Add(new UpgradeProjectData
                {
                    modelIndex = project.ModelIndex,
                    trait = (int)project.Trait,
                    targetLevel = project.TargetLevel,
                    startedDayIndex = project.StartedOn.DayIndex,
                    durationDays = project.DurationDays,
                    petaflopDaysRequired = project.PetaflopDaysRequired,
                    petaflopDaysCompleted = project.PetaflopDaysCompleted,
                    daysCompleted = project.DaysCompleted,
                    cashPaidUsd = project.CashPaidUsd
                });
            }

            foreach (var round in state.CapTable.Rounds)
            {
                data.fundingRounds.Add(new FundingRoundData
                {
                    stage = (int)round.Stage,
                    closedDayIndex = round.ClosedOn.DayIndex,
                    raisedUsd = round.RaisedUsd,
                    postMoneyValuationUsd = round.PostMoneyValuationUsd,
                    equitySold = round.EquitySold,
                    wasDownRound = round.WasDownRound
                });
            }

            data.founderEquity = state.CapTable.FounderEquity;
            data.lastRoundClosedDayIndex = state.LastRoundClosedOn.DayIndex;
            data.intelSubscription = (int)state.IntelSubscription;
            data.daysUntilNextSignal = state.DaysUntilNextSignal;

            var offer = state.CurrentFundingOffer;
            data.hasFundingOffer = offer.IsOpen;
            if (offer.IsOpen)
            {
                data.offerStage = (int)offer.Stage;
                data.offerOpenedDayIndex = offer.OpenedOn.DayIndex;
                data.offerExpiresDayIndex = offer.ExpiresOn.DayIndex;
                data.offerRaiseUsd = offer.RaiseUsd;
                data.offerPreMoneyUsd = offer.PreMoneyValuationUsd;
                data.offerEquitySold = offer.EquitySold;
                data.offerSentiment = offer.Sentiment;
                data.offerIsDownRound = offer.IsDownRound;
            }

            foreach (var pair in state.CustomArchitectures)
            {
                data.customArchitectures.Add(new CustomArchitectureData
                {
                    slot = (int)pair.Key,
                    displayName = pair.Value.DisplayName,
                    availableFromDayIndex = pair.Value.AvailableFrom.DayIndex,
                    parameterEfficiency = pair.Value.ParameterEfficiency,
                    activeParameterFraction = pair.Value.ActiveParameterFraction,
                    trainingEfficiency = pair.Value.TrainingEfficiency,
                    inferenceCostMultiplier = pair.Value.InferenceCostMultiplier,
                    capabilityBonus = pair.Value.CapabilityBonus,
                    generation = state.FamilyGeneration(pair.Key)
                });
            }

            var family = state.ActiveArchitectureProject;
            data.hasArchitectureProject = family != null;
            if (family != null)
            {
                var blueprint = family.Blueprint;
                data.architectureProject = new ArchitectureProjectData
                {
                    name = blueprint.Name,
                    slot = (int)blueprint.Slot,
                    baseFamily = (int)blueprint.BaseFamily,
                    sparsity = blueprint.Weight(ResearchDirection.Sparsity),
                    throughput = blueprint.Weight(ResearchDirection.Throughput),
                    quality = blueprint.Weight(ResearchDirection.Quality),
                    serving = blueprint.Weight(ResearchDirection.Serving),
                    reasoning = blueprint.Weight(ResearchDirection.Reasoning),
                    researchBudgetUsd = blueprint.ResearchBudgetUsd,
                    blueprintDurationDays = blueprint.DurationDays,
                    startedDayIndex = family.StartedOn.DayIndex,
                    durationDays = family.DurationDays,
                    petaflopDaysRequired = family.PetaflopDaysRequired,
                    petaflopDaysCompleted = family.PetaflopDaysCompleted,
                    daysCompleted = family.DaysCompleted,
                    cashPaidUsd = family.CashPaidUsd,
                    researchPower = family.ResearchPower,
                    variance = family.Variance,
                    generation = family.Generation,
                    baselineParameterEfficiency = family.Baseline.ParameterEfficiency,
                    baselineActiveParameterFraction = family.Baseline.ActiveParameterFraction,
                    baselineTrainingEfficiency = family.Baseline.TrainingEfficiency,
                    baselineInferenceCostMultiplier = family.Baseline.InferenceCostMultiplier,
                    baselineCapabilityBonus = family.Baseline.CapabilityBonus
                };
            }

            foreach (var agent in state.Rivals.Agents)
            {
                data.rivals.Add(new CompetitorAgentData
                {
                    competitor = (int)agent.Competitor,
                    hasShipped = agent.HasShipped,
                    liveModelName = agent.LiveModelName,
                    liveCapability = agent.LiveCapability,
                    liveBrand = agent.LiveBrand,
                    livePrice = agent.LivePrice,
                    liveReleaseDayIndex = agent.LiveReleaseDate.DayIndex,
                    nextReleaseDayIndex = agent.NextReleaseDate.DayIndex,
                    hasPlannedRelease = agent.HasPlannedRelease,
                    accumulatedDelayDays = agent.AccumulatedDelayDays,
                    isWaitingForHardware = agent.IsWaitingForHardware,
                    drift = agent.Drift,
                    pendingCapabilityAdjustment = agent.PendingCapabilityAdjustment,
                    hasPendingRelease = agent.TryGetPending(out var pendingRelease),
                    pendingName = pendingRelease.DisplayName,
                    pendingReleaseDayIndex = pendingRelease.ReleaseDate.DayIndex,
                    pendingCapability = pendingRelease.Capability,
                    pendingBrand = pendingRelease.BrandStrength,
                    pendingPrice = pendingRelease.PriceMultiplier,
                    pendingIsProjection = pendingRelease.IsProjection,
                    plannedReleasesRemaining = agent.PlannedReleasesRemaining,
                    waitingForGeneration = (int)agent.WaitingFor
                });
            }

            var run = state.ActiveRun;
            data.hasActiveRun = run != null;
            if (run != null)
            {
                data.activeRun = new TrainingRunData
                {
                    blueprintName = run.Blueprint.Name,
                    architecture = (int)run.Blueprint.Architecture,
                    parameterCountBillions = run.Blueprint.ParameterCountBillions,
                    trainingTokensBillions = run.Blueprint.TrainingTokensBillions,
                    dataSources = (int)run.Blueprint.DataSources,
                    startDayIndex = run.StartDate.DayIndex,
                    petaflopDaysRequired = run.PetaflopDaysRequired,
                    petaflopDaysCompleted = run.PetaflopDaysCompleted,
                    projectedCapability = run.ProjectedCapability,
                    actualTokensBillions = run.ActualTokensBillions,
                    computeCashSpentUsd = run.ComputeCashSpentUsd,
                    dataCostPaidUsd = run.DataCostPaidUsd,
                    modelType = (int)run.Blueprint.Type,
                    family = run.Blueprint.Family
                };
            }

            return data;
        }

        // ------------------------------------------------------------------ restore

        public static CompanyState Restore(SaveData data)
        {
            var safe = Sanitize(data);
            var state = new CompanyState(safe.companyName, safe.randomState == 0 ? 1u : safe.randomState)
            {
                Date = new GameDate(safe.dayIndex),
                CashUsd = safe.cashUsd,
                Reputation = safe.reputation,
                TrainingComputeShare = safe.trainingComputeShare,
                OwnedDataSources = (DatasetSource)safe.ownedDataSources,
                LifetimeRevenueUsd = safe.lifetimeRevenueUsd,
                LifetimeOperatingCostUsd = safe.lifetimeOperatingCostUsd,
                LifetimeCapitalSpentUsd = safe.lifetimeCapitalSpentUsd,
                DatacenterOrdered = safe.datacenterOrdered,
                DatacenterReadyDate = new GameDate(safe.datacenterReadyDayIndex),
                IsBankrupt = safe.isBankrupt,
                DaysInDebt = safe.daysInDebt,
                Archetype = (CompanyArchetype)safe.archetype,
                DefaultPriceMultiplier = safe.defaultPriceMultiplier
            };

            var founderTraits = new FounderTrait[safe.founderTraits.Count];
            for (var index = 0; index < founderTraits.Length; index++)
            {
                founderTraits[index] = (FounderTrait)safe.founderTraits[index];
            }

            state.Founder = new FounderProfile(founderTraits);

            state.UnlockedResearch.Clear();
            state.UnlockedResearch.Add(ResearchTree.StartingNode);
            foreach (var node in safe.unlockedResearch)
            {
                state.UnlockedResearch.Add((ResearchNodeId)node);
            }

            state.FounderName = safe.founderName;

            state.Skills.Restore(safe.skillLevels, safe.skillExperience);
            state.Region = (WorldRegion)safe.worldRegion;
            state.HomeCountry = (Country)safe.homeCountry;
            state.LifetimeTaxPaidUsd = safe.lifetimeTaxPaidUsd;
            state.Segments.Restore(safe.segmentShares, safe.segmentOwnerCount, safe.segmentTypeCount);

            state.LifetimeFinesUsd = safe.lifetimeFinesUsd;

            state.LifetimeFreeTokensBillions = safe.lifetimeFreeTokensBillions;

            state.Monetization.Restore(

                (PricingModel)safe.pricingModel,

                safe.paidPriceMultiplier,

                safe.subscriptionPriceUsdPerMonth,

                safe.freeTierTokensPerUserPerDay,

                safe.companyMarketingDailyUsd,

                safe.modelMarketingDailyUsd,

                safe.modelAwareness);

            var restoredHires = new List<Hire>(safe.staff.Count);
            foreach (var hire in safe.staff)
            {
                restoredHires.Add(new Hire(
                    (StaffRole)hire.role, hire.skill, new GameDate(hire.startedDayIndex)));
            }

            state.Staff.Restore((OfficeTier)safe.officeTier, restoredHires);

            foreach (var incident in safe.incidents)
            {
                state.Incidents.Add(new SafetyIncident(
                    (IncidentSeverity)incident.severity,
                    new GameDate(incident.dayIndex),
                    incident.headline,
                    incident.reputationLoss,
                    incident.fineUsd,
                    incident.forcedWithdrawal));
            }

            foreach (var loan in safe.loans)
            {
                var restoredLoan = new Loan(
                    (LoanProduct)loan.product,
                    new GameDate(loan.takenOnDayIndex),
                    loan.principalUsd,
                    loan.totalRepaymentUsd,
                    loan.termDays,
                    loan.graceDays);
                restoredLoan.Restore(loan.repaidUsd, loan.daysInArrears);
                state.Loans.Add(restoredLoan);
            }

            if (safe.hasResearchProject)
            {
                var project = new ResearchProject(
                    (ResearchNodeId)safe.researchNode,
                    new GameDate(safe.researchStartedDayIndex),
                    safe.researchDurationDays,
                    safe.researchPetaflopDaysRequired,
                    safe.researchCashPaidUsd);
                project.Restore(safe.researchDaysCompleted, safe.researchPetaflopDaysCompleted);
                state.ActiveResearch = project;
            }

            state.Random.State = safe.randomState == 0 ? 1u : safe.randomState;

            state.AdoptedArchitectures.Clear();
            foreach (var architecture in safe.adoptedArchitectures)
            {
                state.AdoptedArchitectures.Add((ArchitectureId)architecture);
            }

            if (state.AdoptedArchitectures.Count == 0)
            {
                state.AdoptedArchitectures.Add(ArchitectureId.DenseTransformer);
            }

            state.Pool.SetRentedPetaflops(safe.rentedPetaflops);
            foreach (var asset in safe.assets)
            {
                state.Pool.AddAsset(new HardwareAsset(
                    (HardwareGenerationId)asset.generationId,
                    (ComputeTier)asset.tier,
                    asset.units,
                    new GameDate(asset.purchaseDayIndex),
                    asset.pricePerUnitUsd,
                    asset.leadTimeDays));
            }

            foreach (var model in safe.models)
            {
                var deployed = new DeployedModel(
                    model.name,
                    (ArchitectureId)model.architecture,
                    model.capability,
                    new GameDate(model.releaseDayIndex),
                    model.activeParameterCount,
                    model.priceMultiplier,
                    (ModelType)model.modelType,
                    model.family);

                if (model.traitLevels != null && model.traitLevels.Count > 0)
                {
                    deployed.RestoreTraits(ModelTraitSet.FromArray(model.traitLevels));
                }

                if (model.isRetired)
                {
                    deployed.Retire();
                }

                state.AddDeployedModel(deployed);
            }

            foreach (var shelved in safe.shelf)
            {
                state.AddToShelf(new TrainedModel(
                    shelved.name,
                    (ArchitectureId)shelved.architecture,
                    shelved.capability,
                    new GameDate(shelved.completedDayIndex),
                    shelved.activeParameterCount,
                    shelved.projectedCapability,
                    (ModelType)shelved.modelType,
                    shelved.family));
            }

            state.Ledger.Restore(safe.ledgerMonths, safe.ledgerAmounts);

            foreach (var upgrade in safe.upgrades)
            {
                var project = new ModelUpgradeProject(
                    upgrade.modelIndex,
                    (ModelTrait)upgrade.trait,
                    upgrade.targetLevel,
                    new GameDate(upgrade.startedDayIndex),
                    upgrade.durationDays,
                    upgrade.petaflopDaysRequired,
                    upgrade.cashPaidUsd);
                project.Restore(upgrade.daysCompleted, upgrade.petaflopDaysCompleted);
                state.AddUpgradeProject(project);
            }

            var history = new List<FundingRoundRecord>(safe.fundingRounds.Count);
            foreach (var round in safe.fundingRounds)
            {
                history.Add(new FundingRoundRecord(
                    (FundingStage)round.stage,
                    new GameDate(round.closedDayIndex),
                    round.raisedUsd,
                    round.postMoneyValuationUsd,
                    round.equitySold,
                    round.wasDownRound));
            }

            state.CapTable.Restore(history, safe.founderEquity);
            state.LastRoundClosedOn = new GameDate(safe.lastRoundClosedDayIndex);
            state.IntelSubscription = (IntelTier)safe.intelSubscription;
            state.DaysUntilNextSignal = safe.daysUntilNextSignal;

            if (safe.hasFundingOffer)
            {
                state.CurrentFundingOffer = new FundingOffer(
                    (FundingStage)safe.offerStage,
                    new GameDate(safe.offerOpenedDayIndex),
                    new GameDate(safe.offerExpiresDayIndex),
                    safe.offerRaiseUsd,
                    safe.offerPreMoneyUsd,
                    safe.offerEquitySold,
                    safe.offerSentiment,
                    safe.offerIsDownRound);
            }

            foreach (var rival in safe.rivals)
            {
                state.Rivals.RestoreAgent(
                    (CompetitorId)rival.competitor,
                    rival.hasShipped,
                    rival.liveModelName,
                    rival.liveCapability,
                    rival.liveBrand,
                    rival.livePrice,
                    new GameDate(rival.liveReleaseDayIndex),
                    new GameDate(rival.nextReleaseDayIndex),
                    rival.hasPlannedRelease,
                    rival.accumulatedDelayDays,
                    rival.isWaitingForHardware,
                    rival.drift,
                    rival.pendingCapabilityAdjustment,
                    rival.hasPendingRelease
                        ? new CompetitorRelease(
                            (CompetitorId)rival.competitor,
                            rival.pendingName,
                            new GameDate(rival.pendingReleaseDayIndex),
                            rival.pendingCapability,
                            rival.pendingBrand,
                            rival.pendingPrice,
                            rival.pendingIsProjection)
                        : null,
                    rival.plannedReleasesRemaining,
                    (HardwareGenerationId)rival.waitingForGeneration);
            }

            foreach (var revenue in safe.revenueWindow)
            {
                state.RecordDailyRevenue(revenue);
            }

            foreach (var family in safe.customArchitectures)
            {
                var slot = (ArchitectureId)family.slot;
                state.StoreCustomArchitecture(
                    slot,
                    new ArchitectureDefinition(
                        slot,
                        family.displayName,
                        new GameDate(family.availableFromDayIndex),
                        family.parameterEfficiency,
                        family.activeParameterFraction,
                        family.trainingEfficiency,
                        family.inferenceCostMultiplier,
                        family.capabilityBonus,
                        adoptionCostUsd: 0),
                    family.generation);
            }

            if (safe.hasArchitectureProject && safe.architectureProject != null)
            {
                var programme = safe.architectureProject;
                var blueprint = new ArchitectureBlueprint(
                    programme.name,
                    (ArchitectureId)programme.slot,
                    (ArchitectureId)programme.baseFamily,
                    programme.sparsity,
                    programme.throughput,
                    programme.quality,
                    programme.serving,
                    programme.reasoning,
                    programme.researchBudgetUsd,
                    programme.blueprintDurationDays);

                var baseline = new ArchitectureDefinition(
                    ArchitectureId.None,
                    "baseline",
                    new GameDate(programme.startedDayIndex),
                    programme.baselineParameterEfficiency,
                    programme.baselineActiveParameterFraction,
                    programme.baselineTrainingEfficiency,
                    programme.baselineInferenceCostMultiplier,
                    programme.baselineCapabilityBonus,
                    adoptionCostUsd: 0);

                var project = new ArchitectureProject(
                    blueprint,
                    new GameDate(programme.startedDayIndex),
                    programme.durationDays,
                    programme.petaflopDaysRequired,
                    programme.cashPaidUsd,
                    programme.researchPower,
                    programme.variance,
                    baseline,
                    programme.generation);
                project.Restore(programme.daysCompleted, programme.petaflopDaysCompleted);
                state.ActiveArchitectureProject = project;
            }

            if (safe.hasActiveRun && safe.activeRun != null)
            {
                var blueprint = new ModelBlueprint(
                    safe.activeRun.blueprintName,
                    (ArchitectureId)safe.activeRun.architecture,
                    safe.activeRun.parameterCountBillions,
                    safe.activeRun.trainingTokensBillions,
                    (DatasetSource)safe.activeRun.dataSources,
                    (ModelType)safe.activeRun.modelType,
                    safe.activeRun.family);

                var run = new TrainingRun(
                    blueprint,
                    new GameDate(safe.activeRun.startDayIndex),
                    safe.activeRun.petaflopDaysRequired,
                    safe.activeRun.projectedCapability,
                    safe.activeRun.actualTokensBillions,
                    safe.activeRun.dataCostPaidUsd);

                run.Contribute(safe.activeRun.petaflopDaysCompleted, safe.activeRun.computeCashSpentUsd);
                state.ActiveRun = run;
            }

            return state;
        }

        // ------------------------------------------------------------------ sanitize

        /// <summary>
        /// Clamps a save into ranges the simulation can survive. Runs after migration and before
        /// anything reads the values, on the assumption that a file on disk may have been edited,
        /// truncated or written by a build that no longer exists.
        /// </summary>
        public static SaveData Sanitize(SaveData data)
        {
            var safe = data ?? new SaveData();

            safe.version = SaveData.CurrentVersion;
            safe.companyName = string.IsNullOrWhiteSpace(safe.companyName) ? "Newco" : safe.companyName.Trim();
            safe.dayIndex = Math.Clamp(safe.dayIndex, 0, GameDate.MaximumDayIndex);
            safe.cashUsd = Math.Clamp(safe.cashUsd, -1_000_000_000_000L, 1_000_000_000_000L);
            safe.reputation = Math.Clamp(Finite(safe.reputation), 0.0, 1.0);
            safe.trainingComputeShare = Math.Clamp(Finite(safe.trainingComputeShare, 0.7), 0.0, 1.0);
            safe.lifetimeRevenueUsd = Math.Max(0L, safe.lifetimeRevenueUsd);
            safe.lifetimeOperatingCostUsd = Math.Max(0L, safe.lifetimeOperatingCostUsd);
            safe.lifetimeCapitalSpentUsd = Math.Max(0L, safe.lifetimeCapitalSpentUsd);
            safe.datacenterReadyDayIndex = Math.Clamp(safe.datacenterReadyDayIndex, 0, GameDate.MaximumDayIndex);
            safe.daysInDebt = Math.Clamp(safe.daysInDebt, 0, 100_000);
            safe.rentedAccelerators = Math.Clamp(safe.rentedAccelerators, 0, 5_000_000);
            safe.rentedPetaflops = Math.Clamp(Finite(safe.rentedPetaflops), 0.0, 5_000_000.0);
            safe.ownedDataSources = SanitizeDataSources(safe.ownedDataSources);

            safe.adoptedArchitectures ??= new List<int>();
            safe.adoptedArchitectures.RemoveAll(static id => !Enum.IsDefined(typeof(ArchitectureId), id));

            safe.assets ??= new List<HardwareAssetData>();
            safe.assets.RemoveAll(static asset =>
                asset == null
                || asset.units <= 0
                || !Enum.IsDefined(typeof(HardwareGenerationId), asset.generationId)
                || !Enum.IsDefined(typeof(ComputeTier), asset.tier));

            foreach (var asset in safe.assets)
            {
                asset.units = Math.Clamp(asset.units, 1, 10_000_000);
                asset.purchaseDayIndex = Math.Clamp(asset.purchaseDayIndex, GameDate.MinimumDayIndex, GameDate.MaximumDayIndex);
                asset.pricePerUnitUsd = Math.Clamp(asset.pricePerUnitUsd, 0L, 10_000_000L);
                asset.leadTimeDays = Math.Clamp(asset.leadTimeDays, 0, 1500);
            }

            safe.models ??= new List<DeployedModelData>();
            safe.models.RemoveAll(static model =>
                model == null || !Enum.IsDefined(typeof(ArchitectureId), model.architecture));

            foreach (var model in safe.models)
            {
                model.name = string.IsNullOrWhiteSpace(model.name) ? "Untitled model" : model.name.Trim();
                model.capability = Math.Clamp(Finite(model.capability), 0.0, 100.0);
                model.releaseDayIndex = Math.Clamp(model.releaseDayIndex, 0, GameDate.MaximumDayIndex);
                model.activeParameterCount = Math.Clamp(Finite(model.activeParameterCount, 1e6), 1e6, 1e15);
                model.priceMultiplier = Math.Clamp(Finite(model.priceMultiplier, 1.0), 0.05, 10.0);
            }

            // ---- v3 collections ----
            safe.shelf ??= new List<TrainedModelData>();
            safe.upgrades ??= new List<UpgradeProjectData>();
            safe.fundingRounds ??= new List<FundingRoundData>();
            safe.rivals ??= new List<CompetitorAgentData>();
            safe.revenueWindow ??= new List<long>();

            safe.founderEquity = Math.Clamp(Finite(safe.founderEquity, 1.0), 0.0, 1.0);
            safe.lastRoundClosedDayIndex = Math.Clamp(
                safe.lastRoundClosedDayIndex, GameDate.MinimumDayIndex, GameDate.MaximumDayIndex);
            safe.daysUntilNextSignal = Math.Clamp(safe.daysUntilNextSignal, 0, 5000);
            if (!Enum.IsDefined(typeof(IntelTier), safe.intelSubscription))
            {
                safe.intelSubscription = (int)IntelTier.PublicNews;
            }

            safe.shelf.RemoveAll(static item =>
                item == null || !Enum.IsDefined(typeof(ArchitectureId), item.architecture));
            foreach (var item in safe.shelf)
            {
                item.name = string.IsNullOrWhiteSpace(item.name) ? "Untitled model" : item.name.Trim();
                item.capability = Math.Clamp(Finite(item.capability), 0.0, 100.0);
                item.projectedCapability = Math.Clamp(Finite(item.projectedCapability), 0.0, 100.0);
                item.completedDayIndex = Math.Clamp(item.completedDayIndex, 0, GameDate.MaximumDayIndex);
                item.activeParameterCount = Math.Clamp(Finite(item.activeParameterCount, 1e6), 1e6, 1e15);
            }

            safe.upgrades.RemoveAll(item =>
                item == null
                || !Enum.IsDefined(typeof(ModelTrait), item.trait)
                || item.modelIndex < 0
                || item.modelIndex >= safe.models.Count);
            if (safe.upgrades.Count > CompanyState.MaximumConcurrentUpgrades)
            {
                safe.upgrades.RemoveRange(
                    CompanyState.MaximumConcurrentUpgrades,
                    safe.upgrades.Count - CompanyState.MaximumConcurrentUpgrades);
            }

            foreach (var item in safe.upgrades)
            {
                item.targetLevel = Math.Clamp(item.targetLevel, 1, ModelTraitSetLimits.MaximumLevel);
                item.durationDays = Math.Clamp(item.durationDays, 1, 400);
                item.daysCompleted = Math.Clamp(item.daysCompleted, 0, item.durationDays);
                item.petaflopDaysRequired = Math.Clamp(Finite(item.petaflopDaysRequired), 0.0, 1e12);
                item.petaflopDaysCompleted = Math.Clamp(
                    Finite(item.petaflopDaysCompleted), 0.0, item.petaflopDaysRequired);
                item.cashPaidUsd = Math.Max(0L, item.cashPaidUsd);
            }

            safe.fundingRounds.RemoveAll(static round =>
                round == null || !Enum.IsDefined(typeof(FundingStage), round.stage));
            foreach (var round in safe.fundingRounds)
            {
                round.equitySold = Math.Clamp(Finite(round.equitySold), 0.0, 1.0);
                round.raisedUsd = Math.Max(0L, round.raisedUsd);
                round.postMoneyValuationUsd = Math.Max(1L, round.postMoneyValuationUsd);
            }

            safe.rivals.RemoveAll(static rival =>
                rival == null || !Enum.IsDefined(typeof(CompetitorId), rival.competitor));
            foreach (var rival in safe.rivals)
            {
                rival.liveCapability = Math.Clamp(Finite(rival.liveCapability), 0.0, 100.0);
                rival.liveBrand = Math.Clamp(Finite(rival.liveBrand), 0.0, 1.0);
                rival.livePrice = Math.Clamp(Finite(rival.livePrice, 1.0), 0.05, 20.0);
            }

            if (safe.hasFundingOffer && !Enum.IsDefined(typeof(FundingStage), safe.offerStage))
            {
                safe.hasFundingOffer = false;
            }

            safe.offerEquitySold = Math.Clamp(Finite(safe.offerEquitySold), 0.0, 0.95);
            safe.offerRaiseUsd = Math.Max(0L, safe.offerRaiseUsd);
            safe.offerPreMoneyUsd = Math.Max(0L, safe.offerPreMoneyUsd);

            foreach (var model in safe.models)
            {
                model.traitLevels ??= new List<int>();
                for (var index = 0; index < model.traitLevels.Count; index++)
                {
                    model.traitLevels[index] = Math.Clamp(
                        model.traitLevels[index], 0, ModelTraitSetLimits.MaximumLevel);
                }
            }

            // ---- v10 fields ----

            safe.founderName = string.IsNullOrWhiteSpace(safe.founderName) ? "Anonymous" : safe.founderName.Trim();
            safe.skillLevels ??= new List<int>();
            safe.skillExperience ??= new List<long>();

            for (var index = 0; index < safe.skillLevels.Count; index++)
            {
                safe.skillLevels[index] = Math.Clamp(safe.skillLevels[index], 0, PlayerSkillLimits.MaximumLevel);
            }

            for (var index = 0; index < safe.skillExperience.Count; index++)
            {
                safe.skillExperience[index] = Math.Max(0L, safe.skillExperience[index]);
            }

            // ---- v11 fields ----

            // An edited file can name a country that does not exist, or one that is not in the
            // region it claims. Both fall back rather than throw, same rule as every other enum here.
            if (!Enum.IsDefined(typeof(WorldRegion), safe.worldRegion) || safe.worldRegion == 0)
            {
                safe.worldRegion = (int)WorldRegion.America;
            }

            if (!Enum.IsDefined(typeof(Country), safe.homeCountry) || safe.homeCountry == 0
                || WorldRegionCatalog.Get((Country)safe.homeCountry).Region != (WorldRegion)safe.worldRegion)
            {
                safe.homeCountry = (int)WorldRegionCatalog.FirstIn((WorldRegion)safe.worldRegion);
            }

            safe.lifetimeTaxPaidUsd = Math.Max(0L, safe.lifetimeTaxPaidUsd);

            // ---- v13 fields ----

            safe.segmentShares ??= new List<double>();
            safe.segmentOwnerCount = Math.Max(0, safe.segmentOwnerCount);
            safe.segmentTypeCount = Math.Max(0, safe.segmentTypeCount);

            for (var index = 0; index < safe.segmentShares.Count; index++)
            {
                safe.segmentShares[index] = Math.Clamp(
                    SimUnits.Finite(safe.segmentShares[index]), 0.0, 1.0);
            }

            // ---- v12 fields ----

            safe.activeRunType = LegalType(safe.activeRunType);
            foreach (var model in safe.models)
            {
                if (model != null)
                {
                    model.modelType = LegalType(model.modelType);
                }
            }

            foreach (var model in safe.shelf)
            {
                if (model != null)
                {
                    model.modelType = LegalType(model.modelType);
                }
            }

            // ---- v9 fields ----

            if (!Enum.IsDefined(typeof(PricingModel), safe.pricingModel))

            {

                safe.pricingModel = (int)PricingModel.PayPerToken;

            }

            safe.paidPriceMultiplier = Math.Clamp(Finite(safe.paidPriceMultiplier, 1.0), 0.05, 10.0);

            safe.subscriptionPriceUsdPerMonth = Math.Clamp(Finite(safe.subscriptionPriceUsdPerMonth, 20.0), 0.0, 2000.0);

            safe.freeTierTokensPerUserPerDay = Math.Clamp(Finite(safe.freeTierTokensPerUserPerDay), 0.0, 2_000_000.0);

            safe.companyMarketingDailyUsd = Math.Clamp(safe.companyMarketingDailyUsd, 0L, 500_000_000L);

            safe.modelMarketingDailyUsd = Math.Clamp(safe.modelMarketingDailyUsd, 0L, 500_000_000L);

            safe.modelAwareness = Math.Clamp(Finite(safe.modelAwareness), 0.0, 0.35);

            safe.lifetimeFreeTokensBillions = Math.Max(0.0, Finite(safe.lifetimeFreeTokensBillions));

            // ---- v8 collections ----
            safe.staff ??= new List<HireData>();
            safe.incidents ??= new List<IncidentData>();
            safe.lifetimeFinesUsd = Math.Max(0L, safe.lifetimeFinesUsd);

            if (!Enum.IsDefined(typeof(OfficeTier), safe.officeTier))
            {
                safe.officeTier = (int)OfficeTier.Garage;
            }

            safe.staff.RemoveAll(static hire =>
                hire == null || !Enum.IsDefined(typeof(StaffRole), hire.role) || hire.role == (int)StaffRole.None);

            var desks = OfficeCatalog.Get((OfficeTier)safe.officeTier).Desks;
            if (safe.staff.Count > desks)
            {
                safe.staff.RemoveRange(desks, safe.staff.Count - desks);
            }

            foreach (var hire in safe.staff)
            {
                hire.skill = Math.Clamp(hire.skill, 1, StaffLimits.MaximumSkill);
                hire.startedDayIndex = Math.Clamp(hire.startedDayIndex, 0, GameDate.MaximumDayIndex);
            }

            safe.incidents.RemoveAll(static incident =>
                incident == null || !Enum.IsDefined(typeof(IncidentSeverity), incident.severity));

            foreach (var incident in safe.incidents)
            {
                incident.headline = string.IsNullOrWhiteSpace(incident.headline)
                    ? "An incident the records no longer describe."
                    : incident.headline;
                incident.reputationLoss = Math.Clamp(Finite(incident.reputationLoss), 0.0, 1.0);
                incident.fineUsd = Math.Max(0L, incident.fineUsd);
                incident.dayIndex = Math.Clamp(incident.dayIndex, 0, GameDate.MaximumDayIndex);
            }

            // ---- v7 collections ----
            safe.loans ??= new List<LoanData>();
            safe.loans.RemoveAll(static loan =>
                loan == null
                || !Enum.IsDefined(typeof(LoanProduct), loan.product)
                || loan.principalUsd <= 0
                || loan.termDays <= 0);

            if (safe.loans.Count > LoanCatalog.MaximumConcurrentLoans)
            {
                safe.loans.RemoveRange(
                    LoanCatalog.MaximumConcurrentLoans,
                    safe.loans.Count - LoanCatalog.MaximumConcurrentLoans);
            }

            foreach (var loan in safe.loans)
            {
                loan.termDays = Math.Clamp(loan.termDays, 1, 8000);
                loan.graceDays = Math.Clamp(loan.graceDays, 0, loan.termDays - 1);
                loan.principalUsd = Math.Clamp(loan.principalUsd, 1L, 1_000_000_000_000L);
                loan.totalRepaymentUsd = Math.Clamp(
                    loan.totalRepaymentUsd, loan.principalUsd, 5_000_000_000_000L);
                loan.repaidUsd = Math.Clamp(loan.repaidUsd, 0L, loan.totalRepaymentUsd);
                loan.daysInArrears = Math.Clamp(loan.daysInArrears, 0, 100_000);
                loan.takenOnDayIndex = Math.Clamp(loan.takenOnDayIndex, 0, GameDate.MaximumDayIndex);
            }

            // ---- v6 fields ----
            safe.founderTraits ??= new List<int>();
            safe.unlockedResearch ??= new List<int>();
            safe.founderTraits.RemoveAll(static id => !Enum.IsDefined(typeof(FounderTrait), id));
            safe.unlockedResearch.RemoveAll(static id => !Enum.IsDefined(typeof(ResearchNodeId), id));
            safe.defaultPriceMultiplier = Math.Clamp(Finite(safe.defaultPriceMultiplier, 1.0), 0.05, 10.0);

            if (!Enum.IsDefined(typeof(CompanyArchetype), safe.archetype))
            {
                safe.archetype = (int)CompanyArchetype.Custom;
            }

            if (safe.hasResearchProject
                && (!Enum.IsDefined(typeof(ResearchNodeId), safe.researchNode) || safe.researchDurationDays <= 0))
            {
                safe.hasResearchProject = false;
            }

            safe.researchDurationDays = Math.Clamp(safe.researchDurationDays, 1, 1500);
            safe.researchDaysCompleted = Math.Clamp(safe.researchDaysCompleted, 0, safe.researchDurationDays);
            safe.researchPetaflopDaysRequired = Math.Clamp(Finite(safe.researchPetaflopDaysRequired), 0.0, 1e12);
            safe.researchPetaflopDaysCompleted = Math.Clamp(
                Finite(safe.researchPetaflopDaysCompleted), 0.0, safe.researchPetaflopDaysRequired);
            safe.researchCashPaidUsd = Math.Max(0L, safe.researchCashPaidUsd);

            // ---- v4 collections ----
            safe.customArchitectures ??= new List<CustomArchitectureData>();
            safe.architectureProject ??= new ArchitectureProjectData();

            safe.customArchitectures.RemoveAll(static family =>
                family == null || !ArchitectureCatalog.IsCustomSlot((ArchitectureId)family.slot));

            foreach (var family in safe.customArchitectures)
            {
                family.displayName = string.IsNullOrWhiteSpace(family.displayName)
                    ? "House family"
                    : family.displayName.Trim();
                family.availableFromDayIndex = Math.Clamp(
                    family.availableFromDayIndex, GameDate.MinimumDayIndex, GameDate.MaximumDayIndex);
                family.parameterEfficiency = Math.Clamp(Finite(family.parameterEfficiency, 1.0), 0.25, 4.0);
                family.activeParameterFraction = Math.Clamp(Finite(family.activeParameterFraction, 1.0), 0.02, 1.0);
                family.trainingEfficiency = Math.Clamp(Finite(family.trainingEfficiency, 1.0), 0.25, 2.0);
                family.inferenceCostMultiplier = Math.Clamp(Finite(family.inferenceCostMultiplier, 1.0), 0.1, 10.0);
                family.capabilityBonus = Math.Clamp(Finite(family.capabilityBonus), 0.0, 20.0);
                family.generation = Math.Clamp(family.generation, 0, ArchitectureDesigner.MaximumGenerations);
            }

            if (safe.hasArchitectureProject)
            {
                var project = safe.architectureProject;
                if (!ArchitectureCatalog.IsCustomSlot((ArchitectureId)project.slot) || project.durationDays <= 0)
                {
                    safe.hasArchitectureProject = false;
                }
                else
                {
                    project.name = string.IsNullOrWhiteSpace(project.name) ? "House family" : project.name.Trim();
                    project.durationDays = Math.Clamp(
                        project.durationDays,
                        ArchitectureBlueprint.MinimumDurationDays,
                        ArchitectureBlueprint.MaximumDurationDays);
                    project.daysCompleted = Math.Clamp(project.daysCompleted, 0, project.durationDays);
                    project.petaflopDaysRequired = Math.Clamp(Finite(project.petaflopDaysRequired), 0.0, 1e12);
                    project.petaflopDaysCompleted = Math.Clamp(
                        Finite(project.petaflopDaysCompleted), 0.0, project.petaflopDaysRequired);
                    project.researchPower = Math.Clamp(Finite(project.researchPower), 0.0, 1.5);
                    project.variance = Math.Clamp(Finite(project.variance), 0.0, 1.0);
                    project.generation = Math.Clamp(project.generation, 0, ArchitectureDesigner.MaximumGenerations);
                    project.researchBudgetUsd = Math.Clamp(
                        project.researchBudgetUsd,
                        ArchitectureBlueprint.MinimumBudgetUsd,
                        ArchitectureBlueprint.MaximumBudgetUsd);
                }
            }

            safe.activeRun ??= new TrainingRunData();
            if (safe.hasActiveRun)
            {
                var run = safe.activeRun;
                if (!Enum.IsDefined(typeof(ArchitectureId), run.architecture) || run.petaflopDaysRequired <= 0.0)
                {
                    safe.hasActiveRun = false;
                }
                else
                {
                    run.blueprintName = string.IsNullOrWhiteSpace(run.blueprintName) ? "Untitled model" : run.blueprintName.Trim();
                    run.startDayIndex = Math.Clamp(run.startDayIndex, 0, GameDate.MaximumDayIndex);
                    run.petaflopDaysRequired = Math.Clamp(Finite(run.petaflopDaysRequired, 1.0), 1.0, 1e12);
                    run.petaflopDaysCompleted = Math.Clamp(Finite(run.petaflopDaysCompleted), 0.0, run.petaflopDaysRequired);
                    run.projectedCapability = Math.Clamp(Finite(run.projectedCapability), 0.0, 100.0);
                    run.actualTokensBillions = Math.Max(0.0, Finite(run.actualTokensBillions));
                    run.dataSources = SanitizeDataSources(run.dataSources);
                    run.computeCashSpentUsd = Math.Max(0L, run.computeCashSpentUsd);
                    run.dataCostPaidUsd = Math.Max(0L, run.dataCostPaidUsd);
                }
            }

            return safe;
        }

        /// <summary>
        /// A model type that is not on the enum, or is None, becomes general. Same rule as every
        /// other enum here: fall back to something legal rather than trusting an edited file.
        /// </summary>
        private static int LegalType(int value) =>
            Enum.IsDefined(typeof(ModelType), value) && value != (int)ModelType.None
                ? value
                : (int)ModelType.General;

        private static int SanitizeDataSources(int mask)
        {
            var known = 0;
            foreach (var source in DatasetCatalog.All)
            {
                known |= (int)source.Flag;
            }

            return mask & known;
        }

        private static double Finite(double value, double fallback = 0.0)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;
        }
    }
}
