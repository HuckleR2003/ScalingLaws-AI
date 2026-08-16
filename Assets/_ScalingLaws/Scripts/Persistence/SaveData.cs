using System;
using ScalingLaws.Data;
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

    /// <summary>One booked marketing campaign, flattened for the save.</summary>
    [Serializable]
    public sealed class CampaignData
    {
        public List<int> channels = new();
        public int target;
        public int termMonths;
        public int startedDayIndex;
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

        /// <summary>The product line, empty for a model that starts its own. Added in v15.</summary>
        public string family = string.Empty;

        /// <summary>Trait levels in catalog order. Added in v3.</summary>
        public List<int> traitLevels = new();

        // What this model did while it was on sale. Added in v23. Records, not derivations: nothing
        // can recompute a model's 2024 earnings from the company's state in 2031.
        public long lifetimeRevenueUsd;
        public int daysOnSale;
        public double peakUsers;
        public int retiredDayIndex;

        /// <summary>How the parameters were arranged. Read by the market daily. Added in v25.</summary>
        public int shape;

        /// <summary>
        /// What this model was hardened with when it was built.
        ///
        /// **Saved on the model rather than derived from the company**, because the protection is a
        /// property of the run. A company that researches a tier next year does not retroactively
        /// harden something it shipped today, and reading the company's current research on load
        /// would do exactly that.
        ///
        /// Data protection is minus one for none: its first tier has to be bought.
        /// </summary>
        public int assaTier;

        /// <inheritdoc cref="assaTier"/>
        public int redTeamTier;

        /// <inheritdoc cref="assaTier"/>
        public int dataProtectionTier = -1;

        /// <inheritdoc cref="assaTier"/>
        public int safetyEffort = 1;
    }

    /// <summary>One letter. Added in v24.</summary>
    [Serializable]
    public sealed class MailItemData
    {
        public int id;
        public int kind;
        public int arrivedDayIndex;
        public string sender = string.Empty;
        public string subject = string.Empty;
        public string body = string.Empty;
        public bool isRead;
        public bool isClosed;
        public string outcome = string.Empty;
        public long amountUsd;
        public int dueDayIndex;
        public int role;
        public int skill;
        public long askingSalaryUsd;
        public bool hasBeenHaggled;
        public int loan;
        public int deferredDays;
    }

    /// <summary>One filed story. Added in v23.</summary>
    [Serializable]
    public sealed class NewsItemData
    {
        public int dayIndex;
        public int section;
        public string headline = string.Empty;
        public string body = string.Empty;
        public string outlet = string.Empty;
        public bool aboutPlayer;
        public int weight;
    }

    /// <summary>
    /// The four Scale and Data choices, flattened. Added in v25.
    ///
    /// Kept as one block because they always travel together: a blueprint carries all four or the
    /// run it describes is not the run that was started.
    /// </summary>
    [Serializable]
    public sealed class ChoiceData
    {
        public int precision = 1;
        public int shape = 1;

        /// <summary>
        /// What this model was hardened with when it was built.
        ///
        /// **Saved on the model rather than derived from the company**, because the protection is a
        /// property of the run. A company that researches a tier next year does not retroactively
        /// harden something it shipped today, and reading the company's current research on load
        /// would do exactly that.
        ///
        /// Data protection is minus one for none: its first tier has to be bought.
        /// </summary>
        public int assaTier;

        /// <inheritdoc cref="assaTier"/>
        public int redTeamTier;

        /// <inheritdoc cref="assaTier"/>
        public int dataProtectionTier = -1;

        /// <inheritdoc cref="assaTier"/>
        public int safetyEffort = 1;

        public int deduplication = 1;
        public int cutoffMonthsBack;
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

        /// <summary>The product line, empty for a model that starts its own. Added in v15.</summary>
        public string family = string.Empty;

        /// <summary>Only the shape survives a release; the rest were spent on the run. v25.</summary>
        public int shape = 1;

        /// <summary>
        /// What this model was hardened with when it was built.
        ///
        /// **Saved on the model rather than derived from the company**, because the protection is a
        /// property of the run. A company that researches a tier next year does not retroactively
        /// harden something it shipped today, and reading the company's current research on load
        /// would do exactly that.
        ///
        /// Data protection is minus one for none: its first tier has to be bought.
        /// </summary>
        public int assaTier;

        /// <inheritdoc cref="assaTier"/>
        public int redTeamTier;

        /// <inheritdoc cref="assaTier"/>
        public int dataProtectionTier = -1;

        /// <inheritdoc cref="assaTier"/>
        public int safetyEffort = 1;

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

        /// <summary>
        /// What the run is building, added in v15. It was never written at all, so a save taken during
        /// training restored the blueprint with the default type and a coding model came back general.
        /// </summary>
        public int modelType;

        /// <summary>The product line the run will join. Added in v15.</summary>
        public string family = string.Empty;

        /// <summary>
        /// The four Scale and Data choices this run was started with. Added in v25.
        ///
        /// A run in flight has to remember all of them, not only the ones that reach the finished
        /// model: precision decides how far the outcome lands from the projection, and that roll
        /// happens on the day it finishes. This is the same fault v15 fixed for the model type.
        /// </summary>
        public ChoiceData choices = new();
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
        public const int CurrentVersion = 31;

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

        /// <summary>The books, added in v16. Month keys, then one total per ledger line per month.</summary>
        public List<int> ledgerMonths = new();

        public List<long> ledgerAmounts = new();

        /// <summary>Net cash from months dropped off the history. Added in v17.</summary>
        public long ledgerCarriedForward;

        /// <summary>People who follow the brand rather than the product. Added in v18.</summary>
        public double fans;

        /// <summary>When the newest model went on sale, for judging a stale line. Added in v18.</summary>
        public int lastReleaseDayIndex;

        /// <summary>Hosting packages held, one count per kind, in catalog order. Added in v19.</summary>
        public List<int> hostingPackages = new();

        /// <summary>Yesterday's service load, which tomorrow's market reads. Added in v19.</summary>
        public double qualityDemanded;

        public double qualityCapacity;
        public double qualityPackagedShare;

        /// <summary>Registered users per day, oldest first, up to ninety. Added in v20.</summary>
        public List<double> userHistory = new();

        /// <summary>Research points banked and how they are funded. Added in v21.</summary>
        public double researchPoints;

        public int researchFundingMode;
        public long researchMonthlyUsd = 1_000;
        public double researchRevenueShare;

        /// <summary>How well known the company is, one per audience in catalog order. Added in v22.</summary>
        public List<double> awareness = new();

        /// <summary>Booked campaigns. Added in v22.</summary>
        public List<CampaignData> campaigns = new();

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

        /// <summary>Kept so a v22 file can still be read. v23 writes <see cref="memberships"/>.</summary>
        public int intelSubscription;

        public int daysUntilNextSignal;

        /// <summary>Every research outfit on retainer, as IntelTier values. Added in v23.</summary>
        public List<int> memberships = new();

        /// <summary>One countdown per outfit, indexed by IntelTier. Added in v23.</summary>
        public List<int> signalCountdowns = new();

        public int daysUntilNextDossier;
        public int nextDossierLab;

        /// <summary>The news the company has read, oldest first. Added in v23.</summary>
        public List<NewsItemData> news = new();

        public int newsUnread;

        /// <summary>The inbox. Added in v24.</summary>
        public List<MailItemData> mail = new();

        /// <summary>Corporation tax owed for the year so far. Causal: January reads it. v24.</summary>
        public long accruedTaxUsd;

        public int taxYear;
        public int daysUntilNextApplicant;

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

        /// <summary>
        /// Which model the founder is, by prefab name, and whether they wear glasses.
        ///
        /// A name rather than an index. Dropping another character pack into the project renumbers
        /// every look, and an index would quietly turn an existing founder into a stranger the next
        /// time their campaign was loaded.
        /// </summary>
        /// <summary>
        /// An inspection that was open when the game was saved.
        ///
        /// **Causal, not cosmetic.** The verdict is rolled when the inspection closes, so a save
        /// made on day three of five carries an undecided outcome. Dropping it would hand the player
        /// a free escape from a penalty by saving and reloading, which is the one thing that would
        /// turn the whole system into a slot machine.
        /// </summary>
        /// <summary>
        /// Places the company owns outright, as tier values.
        ///
        /// **Owning is per place, not a flag on the current office.** A company that buys the small
        /// hub and later moves up still owns the small hub, and moving back has to be free.
        /// </summary>
        public List<int> ownedOffices = new();

        /// <summary>
        /// Everything bought for the office, one entry per piece.
        ///
        /// Four parallel lists rather than a list of objects because Unity's JsonUtility does not
        /// serialise nested generic lists of custom types reliably, which is the same reason every
        /// other collection in this file is shaped this way.
        /// </summary>
        public List<int> decorKinds = new();

        /// <inheritdoc cref="decorKinds"/>
        public List<float> decorX = new();

        /// <inheritdoc cref="decorKinds"/>
        public List<float> decorZ = new();

        /// <inheritdoc cref="decorKinds"/>
        public List<bool> decorPlaced = new();

        // ---- hiring, added in v31 ---------------------------------------------------------

        /// <summary>Conversations in flight. Empty for a company that is not hiring.</summary>
        public List<ApproachData> approaches = new();

        /// <summary>Bought from IThand.hck. Raises the remote ceiling and never lapses.</summary>
        public bool remotePartnership;

        /// <summary>So a reloaded campaign does not hand two people the same id.</summary>
        public int nextCandidateId = 1;

        /// <summary>Hiring's own random stream, kept apart from the company's.</summary>
        public uint hiringRandomState;

        public bool actionOpen;

        /// <inheritdoc cref="actionOpen"/>
        public int actionDaysElapsed;

        /// <inheritdoc cref="actionOpen"/>
        public string actionModel = string.Empty;

        /// <inheritdoc cref="actionOpen"/>
        public long actionFineUsd;

        /// <inheritdoc cref="actionOpen"/>
        public bool actionWithdrawal;

        /// <inheritdoc cref="actionOpen"/>
        public double actionReputationLoss;

        /// <inheritdoc cref="actionOpen"/>
        public string actionHeadline = string.Empty;

        /// <inheritdoc cref="actionOpen"/>
        public string actionSeverity = string.Empty;

        public string founderLook = string.Empty;

        /// <inheritdoc cref="founderLook"/>
        public int founderGlasses;

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

        // ---- added in v31, when people got names --------------------------------------------

        /// <summary>Empty on anybody hired before the new hiring flow existed.</summary>
        public string name = string.Empty;

        /// <summary>PlayerSkill. Zero (None) for a legacy hire, which has no discipline.</summary>
        public int position;

        /// <summary>HireSource. Defaults to Agency, which is what the old system effectively was.</summary>
        public int source = (int)HireSource.Agency;

        /// <summary>What was agreed. Zero means fall back to the catalog salary.</summary>
        public double hourlyWageUsd;
    }

    /// <summary>An approach still waiting for an answer. Added in v31.</summary>
    [Serializable]
    public sealed class ApproachData
    {
        public int candidateId;
        public string name = string.Empty;
        public int position;
        public int advertisedLevel = 20;
        public int source;
        public double askingHourlyUsd;
        public double reservationHourlyUsd;
        public int portraitSeed;
        public int startedDayIndex;
        public int daysNeeded = 3;
        public int daysElapsed;
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
