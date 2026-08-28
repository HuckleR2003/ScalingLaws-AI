using System;
using System.Collections.Generic;
using System.Linq;
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
            // The open file, written whole. Everything the verdict needs is here, because the
            // verdict has not been rolled yet and the incident it will apply cannot be rebuilt from
            // anywhere else.
            var action = state.PendingAction;
            data.ownedOffices.Clear();
            foreach (var owned in state.Staff.Owned)
            {
                data.ownedOffices.Add((int)owned);
            }

            data.decorKinds.Clear();
            data.decorX.Clear();
            data.decorZ.Clear();
            data.decorPlaced.Clear();

            foreach (var item in state.Decor?.Items ?? (IReadOnlyList<DecorItem>)Array.Empty<DecorItem>())
            {
                data.decorKinds.Add((int)item.Kind);
                data.decorX.Add(item.X);
                data.decorZ.Add(item.Z);
                data.decorPlaced.Add(item.IsPlaced);
            }

            data.actionOpen = action != null;

            if (action != null)
            {
                data.actionDaysElapsed = action.DaysElapsed;
                data.actionModel = action.ModelName;
                data.actionFineUsd = action.Incident.FineUsd;
                data.actionWithdrawal = action.Incident.ForcedWithdrawal;
                data.actionReputationLoss = action.Incident.ReputationLoss;
                data.actionHeadline = action.Incident.Headline;
                data.actionSeverity = action.Incident.Severity.ToString();
            }

            data.founderLook = state.FounderLook ?? string.Empty;
            data.founderGlasses = state.FounderGlasses;

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
                    startedDayIndex = hire.StartedOn.DayIndex,
                    name = hire.Name,
                    position = (int)hire.Position,
                    source = (int)hire.Source,
                    hourlyWageUsd = hire.HourlyWageUsd
                });
            }

            data.remotePartnership = state.Hiring.HasRemotePartnership;
            data.nextCandidateId = state.Hiring.NextCandidateId;
            data.hiringRandomState = state.Hiring.Random.State;

            data.guideStage = (int)state.Guide.Stage;
            data.guideStep = state.Guide.Step;
            data.guideStartingCashUsd = state.Guide.StartingCashUsd;
            data.guideBannerDismissed = state.Guide.BannerDismissed;
            data.guideFreeResearchOwed = state.Guide.FreeResearchOwed;
            data.guideFavourGranted = state.Guide.FavourGranted;
            data.feedbackLetterSent = state.FeedbackLetterSent;

            data.lastTroubleDayIndex = state.LastTroubleDayIndex;
            data.firstReleaseWindowUsed = state.FirstReleaseWindowUsed;
            data.rosterSeed = state.RosterSeed;

            foreach (var benefit in state.Benefits)
            {
                data.benefits.Add((int)benefit);
            }

            foreach (var id in state.PoachedRivalStaff)
            {
                data.poachedRivalStaff.Add(id);
            }

            // ---- rivalry, v42 ----

            foreach (var pair in state.SmearDamage)
            {
                data.smearLabs.Add((int)pair.Key);
                data.smearDamage.Add(pair.Value);
            }

            foreach (var pair in state.SmearQuietUntil)
            {
                data.smearQuietLabs.Add((int)pair.Key);
                data.smearQuietUntil.Add(pair.Value);
            }

            foreach (var suit in state.Lawsuits)
            {
                data.lawsuitTargets.Add((int)suit.Target);
                data.lawsuitFiledDays.Add(suit.FiledOn.DayIndex);
                data.lawsuitDamages.Add(suit.DamagesDemandedUsd);
                data.lawsuitCosts.Add(suit.CostsUsd);
                data.lawsuitGrounds.Add(suit.GroundsKey);
                data.lawsuitDaysElapsed.Add(suit.DaysElapsed);
                data.lawsuitVerdicts.Add((int)suit.Verdict);
                data.lawsuitAwarded.Add(suit.AwardedUsd);
            }

            if (state.PendingAcquisition != null)
            {
                data.acquisitionFrom = (int)state.PendingAcquisition.From;
                data.acquisitionMadeDay = state.PendingAcquisition.MadeOn.DayIndex;
                data.acquisitionAmountUsd = state.PendingAcquisition.AmountUsd;
                data.acquisitionMultiple = state.PendingAcquisition.ValuationMultiple;
                data.acquisitionDaysElapsed = state.PendingAcquisition.DaysElapsed;
            }
            else
            {
                data.acquisitionFrom = -1;
            }

            data.acquisitionRefusedOnDayIndex = state.AcquisitionRefusedOnDayIndex;
            data.acquiredForUsd = state.AcquiredForUsd;
            data.lastScandalDayIndex = state.LastScandalDayIndex;
            data.lastFreeTierSeen = state.LastFreeTierSeen;

            foreach (var lab in state.Relations.Known)
            {
                data.relationLabs.Add((int)lab);
                data.relationValues.Add(state.Relations.With(lab));
            }

            foreach (var entry in state.Relations.History)
            {
                data.relationHistoryLabs.Add((int)entry.Lab);
                data.relationHistoryDays.Add(entry.Date.DayIndex);
                data.relationHistoryDeltas.Add(entry.Delta);
                data.relationHistoryKeys.Add(entry.ReasonKey);
                data.relationHistorySubjects.Add(entry.Subject);
            }

            foreach (var effect in state.Effects.All)
            {
                data.effectKinds.Add((int)effect.Kind);
                data.effectStartDays.Add(effect.StartedOn.DayIndex);
                data.effectDays.Add(effect.Days);
                data.effectMagnitudes.Add(effect.Magnitude);
                data.effectModelIndices.Add(effect.ModelIndex);
            }

            data.hasServerRoom = state.HasServerRoom;
            data.serverRoomWasAGift = state.ServerRoomWasAGift;
            state.Hall.Capture(data.hallRacks, data.hallAccelerators, data.hallFans);

            foreach (var approach in state.Hiring.Approaches)
            {
                var candidate = approach.Candidate;

                data.approaches.Add(new ApproachData
                {
                    candidateId = candidate.Id,
                    name = candidate.Name,
                    position = (int)candidate.Position,
                    advertisedLevel = candidate.AdvertisedLevel,
                    source = (int)candidate.Source,
                    askingHourlyUsd = candidate.AskingHourlyUsd,
                    reservationHourlyUsd = candidate.ReservationHourlyUsd,
                    portraitSeed = candidate.PortraitSeed,
                    startedDayIndex = approach.StartedDayIndex,
                    daysNeeded = approach.DaysNeeded,
                    daysElapsed = approach.DaysElapsed
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
                // The version list, written out as it stands. Adoption is causal: tomorrow's drift
                // reads it, and the market's view of this model is the average of what its users are
                // running. Dropping it would move every user onto the newest release on reload.
                var versions = new List<ModelVersionData>();

                foreach (var version in model.Line.Versions)
                {
                    versions.Add(new ModelVersionData
                    {
                        name = version.Name,
                        releasedDayIndex = version.ReleasedOn.DayIndex,
                        capability = version.Capability,
                        priceUsdPerMonth = version.PriceUsdPerMonth,
                        freeTokensPerDay = version.FreeTokensPerDay,
                        adoption = version.Adoption
                    });
                }

                data.models.Add(new DeployedModelData
                {
                    versions = versions,
                    name = model.Name,
                    architecture = (int)model.Architecture,
                    capability = model.Capability,
                    releaseDayIndex = model.ReleaseDate.DayIndex,
                    activeParameterCount = model.ActiveParameterCount,
                    priceMultiplier = model.PriceMultiplier,
                    isRetired = model.IsRetired,
                    modelType = (int)model.Type,
                    family = model.Family,
                    traitLevels = new List<int>(model.Traits.ToArray()),
                    shape = (int)model.Shape,
                    assaTier = model.AssaTier,
                    redTeamTier = model.RedTeamTier,
                    dataProtectionTier = model.DataProtectionTier,
                    safetyEffort = model.SafetyEffort,
                    lifetimeRevenueUsd = model.LifetimeRevenueUsd,
                    daysOnSale = model.DaysOnSale,
                    peakUsers = model.PeakUsers,
                    retiredDayIndex = model.RetiredOn.DayIndex
                });
            }

            state.Ledger.Capture(data.ledgerMonths, data.ledgerAmounts);
            data.ledgerCarriedForward = state.Ledger.CarriedForwardUsd;
            data.fans = state.Fans;
            data.lastReleaseDayIndex = state.LastReleaseDate.DayIndex;
            data.qualityDemanded = state.LastQuality.Demanded;
            data.qualityCapacity = state.LastQuality.Capacity;
            data.qualityPackagedShare = state.LastQuality.PackagedShare;
            state.Users.Capture(data.userHistory);
            data.researchPoints = state.ResearchPoints;
            data.researchFundingMode = (int)state.ResearchFunding;
            data.researchMonthlyUsd = state.ResearchMonthlyUsd;
            data.researchRevenueShare = state.ResearchRevenueShare;
            state.Awareness.Capture(data.awareness);

            data.campaigns.Clear();
            foreach (var campaign in state.Campaigns)
            {
                var flat = new CampaignData
                {
                    target = (int)campaign.Target,
                    termMonths = campaign.TermMonths,
                    startedDayIndex = campaign.StartedOn.DayIndex
                };

                foreach (var channel in campaign.Channels)
                {
                    flat.channels.Add((int)channel);
                }

                data.campaigns.Add(flat);
            }

            data.hostingPackages.Clear();
            foreach (var definition in HostingCatalog.All)
            {
                data.hostingPackages.Add(state.Pool.PackageCount(definition.Id));
            }

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
                    family = shelved.Family,
                    shape = (int)shelved.Shape,
                    assaTier = shelved.AssaTier,
                    redTeamTier = shelved.RedTeamTier,
                    dataProtectionTier = shelved.DataProtectionTier,
                    safetyEffort = shelved.SafetyEffort,
                    traitLevels = new List<int>(shelved.Traits.ToArray())
                });
            }

            foreach (var project in state.UpgradeProjects)
            {
                var row = new UpgradeProjectData
                {
                    modelIndex = project.ModelIndex,
                    trait = (int)project.Trait,
                    targetLevel = project.TargetLevel,
                    startedDayIndex = project.StartedOn.DayIndex,
                    durationDays = project.DurationDays,
                    petaflopDaysRequired = project.PetaflopDaysRequired,
                    petaflopDaysCompleted = project.PetaflopDaysCompleted,
                    daysCompleted = project.DaysCompleted,
                    cashPaidUsd = project.CashPaidUsd,
                    onShelf = project.OnShelf
                };

                foreach (var step in project.Steps)
                {
                    row.stepTraits.Add((int)step.Trait);
                    row.stepTargetLevels.Add(step.TargetLevel);
                }

                data.upgrades.Add(row);
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
            data.intelSubscription = 0;
            data.memberships.Clear();
            foreach (var tier in state.Memberships)
            {
                data.memberships.Add((int)tier);
            }

            state.CaptureCountdowns(data.signalCountdowns);
            data.daysUntilNextDossier = state.DaysUntilNextDossier;
            data.nextDossierLab = state.NextDossierLab;

            data.news.Clear();
            foreach (var story in state.News.All)
            {
                data.news.Add(new NewsItemData
                {
                    dayIndex = story.Date.DayIndex,
                    section = (int)story.Section,
                    headline = story.Headline,
                    body = story.Body,
                    outlet = story.Outlet,
                    aboutPlayer = story.IsAboutPlayer,
                    weight = (int)story.Weight
                });
            }

            data.newsUnread = state.News.Unread;

            data.accruedTaxUsd = state.AccruedTaxUsd;
            data.taxYear = state.TaxYear;
            data.daysUntilNextApplicant = state.DaysUntilNextApplicant;

            data.mail.Clear();
            foreach (var letter in state.Mail.All)
            {
                data.mail.Add(new MailItemData
                {
                    id = letter.Id,
                    kind = (int)letter.Kind,
                    arrivedDayIndex = letter.Arrived.DayIndex,
                    sender = letter.Sender,
                    subject = letter.Subject,
                    body = letter.Body,
                    isRead = letter.IsRead,
                    isClosed = letter.IsClosed,
                    outcome = letter.Outcome,
                    amountUsd = letter.AmountUsd,
                    dueDayIndex = letter.DueDayIndex,
                    role = (int)letter.Role,
                    skill = letter.Skill,
                    askingSalaryUsd = letter.AskingSalaryUsd,
                    hasBeenHaggled = letter.HasBeenHaggled,
                    loan = (int)letter.Loan,
                    deferredDays = letter.DeferredDays
                });
            }
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
                    family = run.Blueprint.Family,
                    choices = new ChoiceData
                    {
                        precision = (int)run.Blueprint.Precision,
                        shape = (int)run.Blueprint.Shape,
                        assaTier = run.Blueprint.AssaTier,
                        redTeamTier = run.Blueprint.RedTeamTier,
                        dataProtectionTier = run.Blueprint.DataProtectionTier,
                        safetyEffort = run.Blueprint.SafetyEffort,
                        deduplication = (int)run.Blueprint.Deduplication,
                        cutoffMonthsBack = run.Blueprint.CutoffMonthsBack
                    }
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
            state.FeedbackLetterSent = safe.feedbackLetterSent;

            state.LastTroubleDayIndex = safe.lastTroubleDayIndex;
            state.FirstReleaseWindowUsed = safe.firstReleaseWindowUsed;
            state.RosterSeed = safe.rosterSeed == 0 ? 0x5CA1AB1E : safe.rosterSeed;

            state.Benefits.Clear();

            foreach (var benefit in safe.benefits)
            {
                if (Enum.IsDefined(typeof(StaffBenefit), benefit))
                {
                    state.Benefits.Add((StaffBenefit)benefit);
                }
            }

            state.PoachedRivalStaff.Clear();

            foreach (var id in safe.poachedRivalStaff)
            {
                state.PoachedRivalStaff.Add(id);
            }

            // ---- rivalry, v42 ----

            state.SmearDamage.Clear();

            for (var index = 0;
                index < safe.smearLabs.Count && index < safe.smearDamage.Count;
                index++)
            {
                if (Enum.IsDefined(typeof(CompetitorId), safe.smearLabs[index]))
                {
                    state.SmearDamage[(CompetitorId)safe.smearLabs[index]] =
                        Math.Clamp(safe.smearDamage[index], 0.0, 0.6);
                }
            }

            state.SmearQuietUntil.Clear();

            for (var index = 0;
                index < safe.smearQuietLabs.Count && index < safe.smearQuietUntil.Count;
                index++)
            {
                if (Enum.IsDefined(typeof(CompetitorId), safe.smearQuietLabs[index]))
                {
                    state.SmearQuietUntil[(CompetitorId)safe.smearQuietLabs[index]] =
                        safe.smearQuietUntil[index];
                }
            }

            state.Lawsuits.Clear();

            for (var index = 0; index < safe.lawsuitTargets.Count; index++)
            {
                if (index >= safe.lawsuitFiledDays.Count
                    || index >= safe.lawsuitDamages.Count
                    || index >= safe.lawsuitCosts.Count
                    || index >= safe.lawsuitGrounds.Count
                    || index >= safe.lawsuitDaysElapsed.Count
                    || index >= safe.lawsuitVerdicts.Count
                    || index >= safe.lawsuitAwarded.Count)
                {
                    break;
                }

                if (!Enum.IsDefined(typeof(CompetitorId), safe.lawsuitTargets[index]))
                {
                    continue;
                }

                var suit = new Lawsuit(
                    (CompetitorId)safe.lawsuitTargets[index],
                    new GameDate(safe.lawsuitFiledDays[index]),
                    safe.lawsuitDamages[index],
                    safe.lawsuitCosts[index],
                    safe.lawsuitGrounds[index]);

                var verdict = Enum.IsDefined(typeof(LawsuitVerdict), safe.lawsuitVerdicts[index])
                    ? (LawsuitVerdict)safe.lawsuitVerdicts[index]
                    : LawsuitVerdict.Pending;

                suit.Restore(safe.lawsuitDaysElapsed[index], verdict, safe.lawsuitAwarded[index]);
                state.Lawsuits.Add(suit);
            }

            if (safe.acquisitionFrom >= 0
                && Enum.IsDefined(typeof(CompetitorId), safe.acquisitionFrom))
            {
                var offer = new AcquisitionOffer(
                    (CompetitorId)safe.acquisitionFrom,
                    new GameDate(safe.acquisitionMadeDay),
                    safe.acquisitionAmountUsd,
                    safe.acquisitionMultiple);

                offer.Restore(safe.acquisitionDaysElapsed);
                state.PendingAcquisition = offer;
            }
            else
            {
                state.PendingAcquisition = null;
            }

            state.AcquisitionRefusedOnDayIndex = safe.acquisitionRefusedOnDayIndex;
            state.AcquiredForUsd = Math.Max(0L, safe.acquiredForUsd);
            state.LastScandalDayIndex = safe.lastScandalDayIndex;
            state.LastFreeTierSeen = safe.lastFreeTierSeen;

            state.Relations.Restore(
                safe.relationLabs, safe.relationValues,
                safe.relationHistoryLabs, safe.relationHistoryDays, safe.relationHistoryDeltas,
                safe.relationHistoryKeys, safe.relationHistorySubjects);

            state.Effects.Restore(
                safe.effectKinds, safe.effectStartDays, safe.effectDays,
                safe.effectMagnitudes, safe.effectModelIndices);

            state.HasServerRoom = safe.hasServerRoom;
            state.ServerRoomWasAGift = safe.serverRoomWasAGift;
            state.Hall.Restore(safe.hallRacks, safe.hallAccelerators, safe.hallFans);
            state.Staff.Owned.Clear();
            if (safe.ownedOffices != null)
            {
                foreach (var owned in safe.ownedOffices)
                {
                    if (Enum.IsDefined(typeof(OfficeTier), owned))
                    {
                        state.Staff.Owned.Add((OfficeTier)owned);
                    }
                }
            }

            // Read by index across the four lists, and stopped by the shortest of them: a truncated
            // file must not throw, and a piece with no position is a piece that cannot be drawn.
            var decor = new List<(FurnitureKind, float, float, bool)>();
            var pieces = safe.decorKinds?.Count ?? 0;

            for (var index = 0; index < pieces; index++)
            {
                if (index >= (safe.decorX?.Count ?? 0)
                    || index >= (safe.decorZ?.Count ?? 0)
                    || index >= (safe.decorPlaced?.Count ?? 0))
                {
                    break;
                }

                if (!Enum.IsDefined(typeof(FurnitureKind), safe.decorKinds[index]))
                {
                    continue;
                }

                decor.Add(((FurnitureKind)safe.decorKinds[index], safe.decorX[index],
                    safe.decorZ[index], safe.decorPlaced[index]));
            }

            state.Decor = DecorPlan.Restore(decor);
            state.Staff.ExtraDesks = state.Decor.ExtraDesks;
            state.Staff.ComfortBonus = state.Decor.MoraleBonus;

            if (safe.actionOpen)
            {
                var severity = Enum.TryParse<IncidentSeverity>(safe.actionSeverity, out var parsed)
                    ? parsed
                    : IncidentSeverity.Minor;

                var incident = new SafetyIncident(
                    severity,
                    state.Date,
                    safe.actionHeadline ?? string.Empty,
                    safe.actionReputationLoss,
                    safe.actionFineUsd,
                    safe.actionWithdrawal);

                state.PendingAction = new RegulatoryAction(incident, state.Date, safe.actionModel);
                state.PendingAction.Restore(safe.actionDaysElapsed);
            }

            state.FounderLook = safe.founderLook ?? string.Empty;
            state.FounderGlasses = Math.Max(0, safe.founderGlasses);

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
                // Enum values are checked rather than cast blind: a file edited by hand, or
                // written by a build that had one more source than this one, must not put a person
                // on the payroll with a channel nobody can price.
                var source = Enum.IsDefined(typeof(HireSource), hire.source)
                    ? (HireSource)hire.source
                    : HireSource.Agency;

                var position = Enum.IsDefined(typeof(PlayerSkill), hire.position)
                    ? (PlayerSkill)hire.position
                    : PlayerSkill.None;

                restoredHires.Add(new Hire(
                    (StaffRole)hire.role, hire.skill, new GameDate(hire.startedDayIndex),
                    hire.name, position, source, hire.hourlyWageUsd));
            }

            state.Staff.Restore((OfficeTier)safe.officeTier, restoredHires);

            // The approaches, rebuilt with the people inside them. A conversation whose candidate
            // cannot be reconstructed is dropped rather than restored half empty: the player would
            // get a banner counting down to a letter that could never be written.
            var approaches = new List<Approach>();

            foreach (var saved in safe.approaches ?? new List<ApproachData>())
            {
                if (saved == null || !Enum.IsDefined(typeof(PlayerSkill), saved.position)
                    || !Enum.IsDefined(typeof(HireSource), saved.source))
                {
                    continue;
                }

                var candidate = new Candidate(saved.candidateId, saved.name,
                    (PlayerSkill)saved.position, saved.advertisedLevel, (HireSource)saved.source,
                    saved.askingHourlyUsd, saved.reservationHourlyUsd, saved.portraitSeed);

                var approach = new Approach(candidate, saved.startedDayIndex, saved.daysNeeded);
                approach.Restore(saved.daysElapsed);
                approaches.Add(approach);
            }

            state.Hiring.Restore(approaches, safe.remotePartnership, safe.nextCandidateId,
                safe.hiringRandomState);

            // An unknown stage is treated as never seen rather than thrown away, so a file from a
            // build with one more stage in the enum still opens — and the worst case is that the
            // phone rings once more, which is recoverable in a way an exception is not.
            state.Guide.Restore(
                Enum.IsDefined(typeof(GuideStage), safe.guideStage)
                    ? (GuideStage)safe.guideStage
                    : GuideStage.Unseen,
                safe.guideStep,
                safe.guideStartingCashUsd,
                safe.guideBannerDismissed,
                safe.guideFreeResearchOwed,
                safe.guideFavourGranted);

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
                    model.family,
                    Math.Clamp(model.assaTier, 0, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(model.redTeamTier, 0, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(model.dataProtectionTier, -1, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(model.safetyEffort, 1, 4));

                if (model.traitLevels != null && model.traitLevels.Count > 0)
                {
                    deployed.RestoreTraits(ModelTraitSet.FromArray(model.traitLevels));
                }

                if (Enum.IsDefined(typeof(ModelShape), model.shape))
                {
                    deployed.SetShape((ModelShape)model.shape);
                }

                deployed.RestoreHistory(model.lifetimeRevenueUsd, model.daysOnSale, model.peakUsers,
                    new GameDate(Math.Max(GameDate.MinimumDayIndex, model.retiredDayIndex)));

                if (model.versions is { Count: > 0 })
                {
                    deployed.RestoreLine(ReleaseLine.Restore(model.versions.Select(version => (
                        version.name,
                        Math.Max(GameDate.MinimumDayIndex, version.releasedDayIndex),
                        version.capability,
                        version.priceUsdPerMonth,
                        version.freeTokensPerDay,
                        Math.Clamp(version.adoption, 0.0, 1.0)))));
                }
                else
                {
                    // Written before v36, or by a build that never shipped a second version. Either
                    // way the company sold one thing, at the terms the file records for it.
                    deployed.SeedLine(safe.subscriptionPriceUsdPerMonth,
                        safe.freeTierTokensPerUserPerDay);
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
                    shelved.family,
                    Enum.IsDefined(typeof(ModelShape), shelved.shape)
                        ? (ModelShape)shelved.shape
                        : ModelShape.Balanced,
                    Math.Clamp(shelved.assaTier, 0, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(shelved.redTeamTier, 0, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(shelved.dataProtectionTier, -1, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(shelved.safetyEffort, 1, 4)));

                // Anything upgraded while it waited. Empty on a file written before v33, in which
                // case the model keeps the par set its constructor gave it, which is exactly what
                // that save was already carrying implicitly.
                if (shelved.traitLevels != null && shelved.traitLevels.Count > 0)
                {
                    state.Shelf[^1].RestoreTraits(ModelTraitSet.FromArray(shelved.traitLevels));
                }
            }

            state.Ledger.Restore(safe.ledgerMonths, safe.ledgerAmounts, safe.ledgerCarriedForward);
            state.Fans = Math.Max(0.0, SimUnits.Finite(safe.fans));
            state.LastReleaseDate = new GameDate(Math.Max(0, safe.lastReleaseDayIndex));
            state.Users.Restore(safe.userHistory);
            state.ResearchPoints = Math.Max(0.0, SimUnits.Finite(safe.researchPoints));
            state.ResearchFunding = Enum.IsDefined(typeof(ResearchFundingMode), safe.researchFundingMode)
                ? (ResearchFundingMode)safe.researchFundingMode
                : ResearchFundingMode.Fixed;
            state.ResearchMonthlyUsd = Math.Clamp(safe.researchMonthlyUsd, 0L,
                ResearchBudget.MaximumMonthlyUsd);
            state.ResearchRevenueShare = Math.Clamp(SimUnits.Finite(safe.researchRevenueShare), 0.0, 1.0);
            state.Awareness.Restore(safe.awareness);

            state.ClearCampaigns();
            foreach (var flat in safe.campaigns)
            {
                if (flat == null)
                {
                    continue;
                }

                var channels = new List<MarketingChannel>();
                foreach (var raw in flat.channels)
                {
                    if (Enum.IsDefined(typeof(MarketingChannel), raw))
                    {
                        channels.Add((MarketingChannel)raw);
                    }
                }

                var target = Enum.IsDefined(typeof(AudienceSegment), flat.target)
                    ? (AudienceSegment)flat.target
                    : AudienceSegment.Consumer;

                state.AddCampaign(new MarketingCampaign(channels, target, flat.termMonths,
                    new GameDate(Math.Max(0, flat.startedDayIndex))));
            }
            state.LastQuality = new ServiceQuality(
                safe.qualityDemanded, safe.qualityCapacity, safe.qualityPackagedShare);

            for (var index = 0; index < HostingCatalog.All.Count; index++)
            {
                var units = index < safe.hostingPackages.Count ? safe.hostingPackages[index] : 0;
                state.Pool.SetPackageCount(HostingCatalog.All[index].Id, units);
            }

            foreach (var upgrade in safe.upgrades)
            {
                // A v38 file has no step list, so the headline trait is the whole programme, which
                // is exactly what it was when that file was written.
                var steps = new List<UpgradeStep>();

                for (var index = 0; index < upgrade.stepTraits.Count; index++)
                {
                    var level = index < upgrade.stepTargetLevels.Count
                        ? upgrade.stepTargetLevels[index]
                        : 1;

                    steps.Add(new UpgradeStep((ModelTrait)upgrade.stepTraits[index], level));
                }

                if (steps.Count == 0)
                {
                    steps.Add(new UpgradeStep((ModelTrait)upgrade.trait, upgrade.targetLevel));
                }

                var project = new ModelUpgradeProject(
                    upgrade.modelIndex,
                    steps,
                    new GameDate(upgrade.startedDayIndex),
                    upgrade.durationDays,
                    upgrade.petaflopDaysRequired,
                    upgrade.cashPaidUsd)
                { OnShelf = upgrade.onShelf };

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
            foreach (var raw in safe.memberships)
            {
                if (Enum.IsDefined(typeof(IntelTier), raw))
                {
                    state.SetMembership((IntelTier)raw, true);
                }
            }

            state.RestoreCountdowns(safe.signalCountdowns);
            state.DaysUntilNextDossier = Math.Max(0, safe.daysUntilNextDossier);
            state.NextDossierLab = Math.Max(0, safe.nextDossierLab);

            state.News.Clear();
            foreach (var story in safe.news)
            {
                if (story == null)
                {
                    continue;
                }

                var section = Enum.IsDefined(typeof(NewsSection), story.section)
                    ? (NewsSection)story.section
                    : NewsSection.Wire;

                var weight = Enum.IsDefined(typeof(NewsWeight), story.weight)
                    ? (NewsWeight)story.weight
                    : NewsWeight.Routine;

                state.News.Add(new NewsItem(new GameDate(Math.Max(0, story.dayIndex)), section,
                    story.headline, story.body, story.outlet, story.aboutPlayer, weight));
            }

            state.AccruedTaxUsd = Math.Max(0L, safe.accruedTaxUsd);
            state.TaxYear = safe.taxYear;
            state.DaysUntilNextApplicant = Math.Max(0, safe.daysUntilNextApplicant);

            state.Mail.Clear();
            foreach (var flat in safe.mail)
            {
                if (flat == null)
                {
                    continue;
                }

                var kind = Enum.IsDefined(typeof(MailKind), flat.kind)
                    ? (MailKind)flat.kind
                    : MailKind.Notice;

                var letter = new MailItem(Math.Max(1, flat.id), kind,
                    new GameDate(Math.Max(0, flat.arrivedDayIndex)),
                    flat.sender, flat.subject, flat.body)
                {
                    IsRead = flat.isRead,
                    IsClosed = flat.isClosed,
                    Outcome = flat.outcome ?? string.Empty,
                    AmountUsd = Math.Max(0L, flat.amountUsd),
                    DueDayIndex = Math.Max(0, flat.dueDayIndex),
                    Skill = Math.Clamp(flat.skill, 0, 10),
                    AskingSalaryUsd = Math.Max(0L, flat.askingSalaryUsd),
                    HasBeenHaggled = flat.hasBeenHaggled,
                    DeferredDays = Math.Max(0, flat.deferredDays)
                };

                if (Enum.IsDefined(typeof(StaffRole), flat.role))
                {
                    letter.Role = (StaffRole)flat.role;
                }

                if (Enum.IsDefined(typeof(LoanProduct), flat.loan))
                {
                    letter.Loan = (LoanProduct)flat.loan;
                }

                state.Mail.Restore(letter);
            }

            state.News.MarkRead();
            for (var unread = 0; unread < safe.newsUnread; unread++)
            {
                // Restoring the count without re-adding the stories, so a save taken with four
                // unread stories opens with four unread stories rather than ninety.
                state.News.NoteUnread();
            }
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
                    safe.activeRun.family,
                    Enum.IsDefined(typeof(TrainingPrecision), safe.activeRun.choices.precision)
                        ? (TrainingPrecision)safe.activeRun.choices.precision
                        : TrainingPrecision.BFloat16,
                    Enum.IsDefined(typeof(ModelShape), safe.activeRun.choices.shape)
                        ? (ModelShape)safe.activeRun.choices.shape
                        : ModelShape.Balanced,
                    Enum.IsDefined(typeof(DeduplicationPass), safe.activeRun.choices.deduplication)
                        ? (DeduplicationPass)safe.activeRun.choices.deduplication
                        : DeduplicationPass.Standard,
                    safe.activeRun.choices.cutoffMonthsBack,
                    Math.Clamp(safe.activeRun.choices.assaTier, 0, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(safe.activeRun.choices.redTeamTier, 0, SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(safe.activeRun.choices.dataProtectionTier, -1,
                        SafetyModuleCatalog.TierCount - 1),
                    Math.Clamp(safe.activeRun.choices.safetyEffort, 1, 4));

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

            safe.benefits ??= new List<int>();
            safe.poachedRivalStaff ??= new List<int>();

            safe.smearLabs ??= new List<int>();
            safe.smearDamage ??= new List<double>();
            safe.smearQuietLabs ??= new List<int>();
            safe.smearQuietUntil ??= new List<int>();
            safe.lawsuitTargets ??= new List<int>();
            safe.lawsuitFiledDays ??= new List<int>();
            safe.lawsuitDamages ??= new List<long>();
            safe.lawsuitCosts ??= new List<long>();
            safe.lawsuitGrounds ??= new List<string>();
            safe.lawsuitDaysElapsed ??= new List<int>();
            safe.lawsuitVerdicts ??= new List<int>();
            safe.lawsuitAwarded ??= new List<long>();
            safe.relationLabs ??= new List<int>();
            safe.relationValues ??= new List<double>();
            safe.relationHistoryLabs ??= new List<int>();
            safe.relationHistoryDays ??= new List<int>();
            safe.relationHistoryDeltas ??= new List<double>();
            safe.relationHistoryKeys ??= new List<string>();
            safe.relationHistorySubjects ??= new List<string>();
            safe.effectKinds ??= new List<int>();
            safe.effectStartDays ??= new List<int>();
            safe.effectDays ??= new List<int>();
            safe.effectMagnitudes ??= new List<double>();
            safe.effectModelIndices ??= new List<int>();

            foreach (var upgrade in safe.upgrades)
            {
                upgrade.stepTraits ??= new List<int>();
                upgrade.stepTargetLevels ??= new List<int>();
            }
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

            // The bound depends on which list the programme points at. Checking the deployed list
            // for everything threw away every shelf programme on load, which is the same mistake
            // the completion path made and just as silent: the player reloads and the work is gone.
            safe.upgrades.RemoveAll(item =>
                item == null
                || !Enum.IsDefined(typeof(ModelTrait), item.trait)
                || item.modelIndex < 0
                || item.modelIndex >= (item.onShelf ? safe.shelf.Count : safe.models.Count));
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
            safe.founderLook = safe.founderLook?.Trim() ?? string.Empty;
            safe.founderGlasses = Math.Max(0, safe.founderGlasses);
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
            safe.activeRun.choices ??= new ChoiceData();
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
