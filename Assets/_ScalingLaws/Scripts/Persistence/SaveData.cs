using System;
using System.Collections.Generic;

namespace ScalingLaws.Persistence
{
    /// <summary>Read first to find out what shape the rest of the file is in.</summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public int version;
    }

    [Serializable]
    public sealed class HardwareAssetData
    {
        public int generationId;
        public int tier;
        public int units;
        public int purchaseDayIndex;
        public long pricePerUnitUsd;
        public int leadTimeDays;
    }

    [Serializable]
    public sealed class DeployedModelData
    {
        public int modelType;
        public string name;
        public int architecture;
        public double capability;
        public int releaseDayIndex;
        public double activeParameterCount;
        public double priceMultiplier = 1.0;
        public bool isRetired;

        /// <summary>Trait levels in catalog order. Added in v3.</summary>
        public List<int> traitLevels = new();
    }

    /// <summary>A finished run waiting for a release decision. Added in v3.</summary>
    [Serializable]
    public sealed class TrainedModelData
    {
        public int modelType;
        public string name;
        public int architecture;
        public double capability;
        public int completedDayIndex;
        public double activeParameterCount;
        public double projectedCapability;
    }

    /// <summary>An upgrade programme in flight. Added in v3.</summary>
    [Serializable]
    public sealed class UpgradeProjectData
    {
        public int modelIndex;
        public int trait;
        public int targetLevel;
        public int startedDayIndex;
        public int durationDays;
        public double petaflopDaysRequired;
        public double petaflopDaysCompleted;
        public int daysCompleted;
        public long cashPaidUsd;
    }

    /// <summary>One closed round. Added in v3.</summary>
    [Serializable]
    public sealed class FundingRoundData
    {
        public int stage;
        public int closedDayIndex;
        public long raisedUsd;
        public long postMoneyValuationUsd;
        public double equitySold;
        public bool wasDownRound;
    }

    /// <summary>A rival lab's live state. Added in v3.</summary>
    [Serializable]
    public sealed class CompetitorAgentData
    {
        public int competitor;
        public bool hasShipped;
        public string liveModelName;
        public double liveCapability;
        public double liveBrand;
        public double livePrice = 1.0;
        public int liveReleaseDayIndex;
        public int nextReleaseDayIndex;
        public bool hasPlannedRelease;
        public int accumulatedDelayDays;
        public bool isWaitingForHardware;

        /// <summary>Added in v13. Capability crept since release, and the roll already made.</summary>
        public double drift;

        public double pendingCapabilityAdjustment;

        /// <summary>
        /// Added in v13. The release this lab is working toward. Past the end of the reference
        /// table a lab invents its own, with a random gain, and that invention has to be written
        /// down or a reload silently rebuilds a different rival field.
        /// </summary>
        public bool hasPendingRelease;

        public string pendingName;
        public int pendingReleaseDayIndex;
        public double pendingCapability;
        public double pendingBrand;
        public double pendingPrice;
        public bool pendingIsProjection;

        /// <summary>How many catalog releases the lab still had queued. Recorded, never inferred.</summary>
        public int plannedReleasesRemaining = -1;

        /// <summary>Which accelerator generation a patient lab is holding out for.</summary>
        public int waitingForGeneration;
    }

    [Serializable]
    public sealed class TrainingRunData
    {
        public string blueprintName;
        public int architecture;
        public double parameterCountBillions;
        public double trainingTokensBillions;
        public int dataSources;
        public int startDayIndex;
        public double petaflopDaysRequired;
        public double petaflopDaysCompleted;
        public double projectedCapability;
        public double actualTokensBillions;
        public long computeCashSpentUsd;
        public long dataCostPaidUsd;
    }

    /// <summary>A family the company designed itself. Added in v4.</summary>
    [Serializable]
    public sealed class CustomArchitectureData
    {
        public int slot;
        public string displayName;
        public int availableFromDayIndex;
        public double parameterEfficiency = 1.0;
        public double activeParameterFraction = 1.0;
        public double trainingEfficiency = 1.0;
        public double inferenceCostMultiplier = 1.0;
        public double capabilityBonus;
        public int generation;
    }

    /// <summary>A family research programme in flight. Added in v4.</summary>
    [Serializable]
    public sealed class ArchitectureProjectData
    {
        public string name;
        public int slot;
        public int baseFamily;
        public double sparsity;
        public double throughput;
        public double quality;
        public double serving;
        public double reasoning;
        public long researchBudgetUsd;
        public int blueprintDurationDays;

        public int startedDayIndex;
        public int durationDays;
        public double petaflopDaysRequired;
        public double petaflopDaysCompleted;
        public int daysCompleted;
        public long cashPaidUsd;
        public double researchPower;
        public double variance;
        public int generation;

        public double baselineParameterEfficiency = 1.0;
        public double baselineActiveParameterFraction = 1.0;
        public double baselineTrainingEfficiency = 1.0;
        public double baselineInferenceCostMultiplier = 1.0;
        public double baselineCapabilityBonus;
    }

    /// <summary>
    /// The whole campaign on disk. Plain public fields because Unity's JsonUtility only sees those.
    ///
    /// Version history, kept here so each migration branch has something to be checked against:
    ///   v1  compute was two integers, a rented count and an owned count, with no purchase dates.
    ///       Depreciation could not be computed from it, which is the reason v2 exists.
    ///   v2  owned compute became a list of dated batches with tier and price paid. Models went
    ///       straight from training to market and had no upgrade levels; rivals were a static table.
    ///   v3  models carry trait levels, finished runs wait on a shelf, funding rounds have a cap
    ///       table, rival labs are agents with their own state, and a research desk files signals.
    ///   v4  the company can design its own architecture families, so those and any programme in
    ///       flight have to be written down.
    ///   v5  rented compute is contracted in petaflop/s rather than in accelerator units, because a
    ///       unit count silently tripled the bill on the day the clouds changed generation.
    ///       Current.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 14;

        public int version = CurrentVersion;

        /// <summary>Which catalog build the save was written against. Diagnostic only, never a gate.</summary>
        public string hardwareCatalogVersion = string.Empty;

        public string companyName = "Newco";
        public int dayIndex;
        public long cashUsd;
        public uint randomState;
        public double reputation;
        public double trainingComputeShare = 0.7;

        public int ownedDataSources;
        public List<int> adoptedArchitectures = new();

        public long lifetimeRevenueUsd;
        public long lifetimeOperatingCostUsd;
        public long lifetimeCapitalSpentUsd;

        public bool datacenterOrdered;
        public int datacenterReadyDayIndex;

        public bool isBankrupt;
        public int daysInDebt;

        /// <summary>Superseded by <see cref="rentedPetaflops"/> in v5. Read by the migration only.</summary>
        public int rentedAccelerators;

        public List<HardwareAssetData> assets = new();
        public List<DeployedModelData> models = new();

        public bool hasActiveRun;
        public TrainingRunData activeRun = new();

        // ---- added in v3 ----

        public List<TrainedModelData> shelf = new();
        public List<UpgradeProjectData> upgrades = new();
        public List<FundingRoundData> fundingRounds = new();
        public double founderEquity = 1.0;
        public int lastRoundClosedDayIndex;

        public bool hasFundingOffer;
        public int offerStage;
        public int offerOpenedDayIndex;
        public int offerExpiresDayIndex;
        public long offerRaiseUsd;
        public long offerPreMoneyUsd;
        public double offerEquitySold;
        public double offerSentiment = 1.0;
        public bool offerIsDownRound;

        public int intelSubscription;
        public int daysUntilNextSignal;

        public List<CompetitorAgentData> rivals = new();

        /// <summary>Trailing daily revenue, oldest first. Feeds the run rate investors price on.</summary>
        public List<long> revenueWindow = new();

        // ---- added in v4 ----

        public List<CustomArchitectureData> customArchitectures = new();
        public bool hasArchitectureProject;
        public ArchitectureProjectData architectureProject = new();

        // ---- added in v5 ----

        /// <summary>Contracted rental throughput. Replaces the old unit count.</summary>
        public double rentedPetaflops;

        // ---- added in v6 ----

        public int archetype;
        public List<int> founderTraits = new();
        public double defaultPriceMultiplier = 1.0;
        public List<int> unlockedResearch = new();

        public bool hasResearchProject;
        public int researchNode;
        public int researchStartedDayIndex;
        public int researchDurationDays;
        public double researchPetaflopDaysRequired;
        public double researchPetaflopDaysCompleted;
        public int researchDaysCompleted;
        public long researchCashPaidUsd;

        // ---- added in v7 ----

        public List<LoanData> loans = new();

        // ---- added in v8 ----

        public int officeTier;
        public List<HireData> staff = new();
        public List<IncidentData> incidents = new();
        public long lifetimeFinesUsd;

        // ---- added in v9 ----

        public int pricingModel;
        public double paidPriceMultiplier = 1.0;
        public double subscriptionPriceUsdPerMonth = 20.0;
        public double freeTierTokensPerUserPerDay;
        public long companyMarketingDailyUsd;
        public long modelMarketingDailyUsd;
        public double modelAwareness;
        public double lifetimeFreeTokensBillions;

        // ---- added in v10 ----

        public string founderName = "Anonymous";

        public List<int> skillLevels = new();

        public List<long> skillExperience = new();

        // ---- added in v11 ----

        public int worldRegion = 1;
        public int homeCountry = 1;
        public long lifetimeTaxPaidUsd;

        // ---- added in v12 ----

        /// <summary>Model type, on the run in flight. Zero means the file predates types.</summary>
        public int activeRunType;

        // ---- added in v13 ----

        /// <summary>Superseded in v14. Read by the migration only.</summary>
        public List<double> segmentPlayerShares = new();

        public List<double> segmentRivalShares = new();

        public int segmentRivalCount;

        // ---- added in v14 ----

        /// <summary>
        /// The whole standing, audience major then owner then model type. Owner zero is the player.
        /// A mismatch on either count drops it rather than stretching it.
        /// </summary>
        public List<double> segmentShares = new();

        public int segmentOwnerCount;
        public int segmentTypeCount;
    }

    /// <summary>One person on the payroll. Added in v8.</summary>
    [Serializable]
    public sealed class HireData
    {
        public int role;
        public int skill = 1;
        public int startedDayIndex;
    }

    /// <summary>A public safety failure that already happened. Added in v8.</summary>
    [Serializable]
    public sealed class IncidentData
    {
        public int severity;
        public int dayIndex;
        public string headline;
        public double reputationLoss;
        public long fineUsd;
        public bool forcedWithdrawal;
    }

    /// <summary>A facility being serviced. Added in v7.</summary>
    [Serializable]
    public sealed class LoanData
    {
        public int product;
        public int takenOnDayIndex;
        public long principalUsd;
        public long totalRepaymentUsd;
        public int termDays;
        public int graceDays;
        public long repaidUsd;
        public int daysInArrears;
    }
}
