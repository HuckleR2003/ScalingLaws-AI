using System;
using ScalingLaws.Data;
using System.Collections.Generic;

using ScalingLaws.Simulation;

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

    /// <summary>
    /// One published version of a model and the share of its users still on it. Added in v36.
    ///
    /// **The share is a record, not a derivation.** It is the result of every day of drift since the
    /// version shipped, and nothing in the state can recompute it: the same four releases in the
    /// same order reach different shares depending on how long the player left between them.
    /// </summary>
    [Serializable]
    public sealed class ModelVersionData
    {
        public string name = string.Empty;
        public int releasedDayIndex;
        public double capability;
        public double priceUsdPerMonth;
        public double freeTokensPerDay;
        public double adoption;
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

        /// <summary>Every version still in use, oldest first. Added in v36.</summary>
        public List<ModelVersionData> versions = new();

        // What this model did while it was on sale. Added in v23. Records, not derivations: nothing
        // can recompute a model's 2024 earnings from the company's state in 2031.
        public long lifetimeRevenueUsd;
        public int daysOnSale;
        public double peakUsers;
        public int retiredDayIndex;

        /// <summary>
        /// What it earned on each of its last thirty one days on sale, oldest first. Added in v50.
        ///
        /// A record for the same reason the three above it are: a day's take is a share of that
        /// day's revenue weighted by users and capability, and none of the three survive the day.
        /// </summary>
        public List<long> recentRevenue = new();

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
        /// What the model is good at while it waits.
        ///
        /// Added in v33, when upgrading before release became possible. Without it, work bought
        /// on a shelved model vanished on the next load and the player paid for nothing.
        /// </summary>
        public List<int> traitLevels = new();

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

        /// <summary>True when modelIndex points into the shelf rather than the deployed list. v33.</summary>
        public bool onShelf;

        // ---- the basket, v39 ---------------------------------------------------------------------
        //
        // A programme is a batch now. `trait` and `targetLevel` above stay as the headline so a v38
        // file still loads into something sensible; these two carry the rest. Parallel lists rather
        // than a list of pairs, because JsonUtility does not serialise nested types the way anybody
        // expects and every other grid in this file is already shaped this way.
        public List<int> stepTraits = new();
        public List<int> stepTargetLevels = new();
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
        public const int CurrentVersion = 51;

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

        // ---- added in v47, when walkthroughs arrived ------------------------------------------

        /// <summary>Walkthroughs finished, by catalog id. Never renumbered, so ids are safe.</summary>
        public List<string> walkthroughsDone = new();

        /// <summary>And the ones whose offer the player waved away.</summary>
        public List<string> walkthroughsDismissed = new();

        // ---- added in v48, when the phone kept its thread ---------------------------------------

        /// <summary>The saved conversation with Emil, oldest first.</summary>
        public List<ChatLineData> messages = new();

        // ---- added in v49, the state programme ---------------------------------------------------

        public bool programmeSigned;
        public int programmeSignatory;
        public int programmeSignedDay;

        /// <summary>Sectors running, as `StateSector` values. Never renumbered.</summary>
        public List<int> programmeSectors = new();

        public int programmeLastFailureDay = -9999;
        public int programmeFailures;
        public long programmePaidOutUsd;

        /// <summary>
        /// Yesterday's delivery.
        ///
        /// **Looks derived, is causal.** Tomorrow's failure risk reads it, so a campaign reloaded
        /// without it rolls different odds than the run that wrote it. Sixth time in this project.
        /// </summary>
        public double programmeLastDelivery = 1.0;
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

        public string founderName = string.Empty;

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

        // ---- the guide, added in v35 --------------------------------------------------------

        /// <summary>GuideStage. Zero means the phone has never rung for this company.</summary>
        public int guideStage;

        /// <summary>How far through Emil's tour they got.</summary>
        public int guideStep;

        /// <summary>What the company was worth when the tutorial started.</summary>
        public long guideStartingCashUsd;

        /// <summary>True once the task strip was closed for good.</summary>
        public bool guideBannerDismissed;

        /// <summary>
        /// A research node still owed to the player. Added in v37.
        ///
        /// Causal state, not a record: the next programme reads it and does not charge. Dropping it
        /// on reload would take back something the tutorial already gave.
        /// </summary>
        public bool guideFreeResearchOwed;

        /// <summary>
        /// True once he has handed the favour over, spent or not. v39.
        ///
        /// Separate from `guideFreeResearchOwed`, which only says it is unspent. Without the two
        /// being distinct, a campaign that used the gift would look identical to one that never
        /// reached the step, and the tour would hand it over again.
        /// </summary>
        public bool guideFavourGranted;

        /// <summary>
        /// True once the one letter asking where the player got stuck has been posted. v40.
        ///
        /// A thing that happened to this campaign rather than something derivable from it. Without
        /// it, reloading posts the letter again, and a request for help that keeps arriving is an
        /// advertisement.
        /// </summary>
        public bool feedbackLetterSent;

        // ---- the world that has opinions about you, v41 -------------------------------------------
        //
        // Every one of these is causal. Effects multiply demand, relations gate what rivals will do,
        // and a poached person is somebody who is no longer on a roster. Parallel lists rather than
        // nested types, because JsonUtility does not serialise a list of structs the way anybody
        // expects and every other collection in this file is already shaped this way.

        /// <summary>Benefits currently offered, as StaffBenefit values.</summary>
        public List<int> benefits = new();

        /// <summary>How each lab feels about the company, paired by index.</summary>
        public List<int> relationLabs = new();

        public List<double> relationValues = new();

        /// <summary>And why, which is the half that makes the number mean anything.</summary>
        public List<int> relationHistoryLabs = new();

        public List<int> relationHistoryDays = new();
        public List<double> relationHistoryDeltas = new();
        public List<string> relationHistoryKeys = new();
        public List<string> relationHistorySubjects = new();

        /// <summary>Timed effects still running, paired by index.</summary>
        public List<int> effectKinds = new();

        public List<int> effectStartDays = new();
        public List<int> effectDays = new();
        public List<double> effectMagnitudes = new();
        public List<int> effectModelIndices = new();

        /// <summary>People taken off rivals' payrolls, by generated id.</summary>
        public List<int> poachedRivalStaff = new();

        /// <summary>The last day something went publicly wrong. Safe Harbour counts from here.</summary>
        public int lastTroubleDayIndex = -1;

        /// <summary>True once the new-lab window has been spent.</summary>
        public bool firstReleaseWindowUsed;

        /// <summary>The seed rival rosters are generated from. Never moves.</summary>
        public uint rosterSeed = 0x5CA1AB1E;

        // ---- rivalry, v42 ------------------------------------------------------------------------
        //
        // Parallel lists rather than nested types, the same shape the rest of this file uses,
        // because JsonUtility does not serialise a list of nested types the way anybody expects.

        /// <summary>Standing taken off rivals by things this company paid for. Decays daily.</summary>
        public List<int> smearLabs = new();

        public List<double> smearDamage = new();

        /// <summary>The first day each lab can be targeted again.</summary>
        public List<int> smearQuietLabs = new();

        public List<int> smearQuietUntil = new();

        /// <summary>
        /// Actions in front of a court.
        ///
        /// Open cases carry an unrolled verdict, so this is causal rather than a record: dropping
        /// it would let a reload undo a judgment the same way dropping `actionOpen` would.
        /// </summary>
        public List<int> lawsuitTargets = new();

        public List<int> lawsuitFiledDays = new();
        public List<long> lawsuitDamages = new();
        public List<long> lawsuitCosts = new();
        public List<string> lawsuitGrounds = new();
        public List<int> lawsuitDaysElapsed = new();
        public List<int> lawsuitVerdicts = new();
        public List<long> lawsuitAwarded = new();

        /// <summary>
        /// One per case: non-zero when the lab is suing the company rather than the other way round.
        /// Added in v51.
        ///
        /// Read defensively as well as migrated, so a file short of an entry reads as a case the
        /// player filed, which is what every case written before v51 was.
        /// </summary>
        public List<int> lawsuitAgainstUs = new();

        // ---- a lab threatening to sue over a smear it traced back. Added in v51 --------------------
        //
        // Causal, not derived: whether they file is rolled on the day the letter runs out. Dropping
        // it on load would be a consequence a reload could walk past.

        public bool smearThreatOpen;
        public int smearThreatLab;
        public int smearThreatOpenedDay;
        public long smearThreatSettlementUsd;
        public int smearThreatMailId;
        public int smearThreatDaysElapsed;
        public bool smearThreatAnswered;

        /// <summary>
        /// Grants: what is on the table, what is being worked off, and who has been turned away.
        ///
        /// **Causal, not a record.** A held award carries the baseline captured on the day it was
        /// signed and, for the sustained ones, whether it has already been broken. Both decide an
        /// outcome that has not happened yet, so a file that dropped them would let a reload change
        /// the answer. That is the same hole `actionOpen` and the open lawsuits were built to close,
        /// and this project has now made the mistake six times.
        /// </summary>
        public List<int> grantOfferIds = new();

        public List<int> grantOfferDays = new();

        public List<int> grantHeldIds = new();
        public List<int> grantHeldStartDays = new();
        public List<double> grantHeldBaselines = new();
        public List<int> grantHeldDaysElapsed = new();
        public List<bool> grantHeldBroken = new();

        /// <summary>Programmes already seen through, so a body does not fund the same work twice.</summary>
        public List<int> grantsCompleted = new();

        /// <summary>Programme id to the day index before which it will not be offered again.</summary>
        public List<int> grantQuietIds = new();

        public List<int> grantQuietUntilDays = new();

        /// <summary>An open offer to buy the company. Negative bidder means none.</summary>
        public int acquisitionFrom = -1;

        public int acquisitionMadeDay = -1;
        public long acquisitionAmountUsd;
        public double acquisitionMultiple = 1.0;
        public int acquisitionDaysElapsed;

        /// <summary>When the last offer was refused, so nobody asks again immediately.</summary>
        public int acquisitionRefusedOnDayIndex = -1;

        /// <summary>Non-zero once the company has been sold.</summary>
        public long acquiredForUsd;

        /// <summary>The last day the press ran a story. Negative means never.</summary>
        public int lastScandalDayIndex = -1;

        /// <summary>Yesterday's free allowance, so a cut can be noticed.</summary>
        public double lastFreeTierSeen = -1.0;

        // ---- investing, v43 ----------------------------------------------------------------------
        //
        // The share price is a function of the lab's own live standing on a date, so the market
        // itself is never written down. Storing ninety days of prices for fourteen labs would put
        // a few thousand recomputable numbers in every file, and a recorded series would drift
        // from the live price the first time a smear moved a lab's standing.

        /// <summary>Shares held, paired by index with <see cref="shareCounts"/>.</summary>
        public List<int> shareLabs = new();

        public List<long> shareCounts = new();

        /// <summary>What the holding cost, so the screen can say whether it was worth it.</summary>
        public List<long> shareCostBasis = new();

        /// <summary>Labs bought outright. They stop trading.</summary>
        public List<int> acquiredLabs = new();

        // ---- the basement, v38 -------------------------------------------------------------------
        //
        // Three parallel lists rather than a list of structs, because that is the shape every other
        // grid in this file uses and JsonUtility does not serialise a list of nested types the way
        // anybody expects.
        public bool hasServerRoom;
        public bool serverRoomWasAGift;
        public List<int> hallRacks = new();
        public List<int> hallAccelerators = new();
        public List<int> hallFans = new();

        /// <summary>
        /// The store room: cabinets bought and not standing, by kind, and loose fans.
        ///
        /// Separate from the hall lists because it is a different fact. The hall says what is on
        /// the floor; this says what the company owns and has not put anywhere, which is the state
        /// that made buying and placing two decisions instead of one.
        /// </summary>
        public List<int> storeRackKinds = new();
        public List<int> storeRackCounts = new();
        public int storeFans;

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
    /// <summary>
    /// One message in the phone's thread.
    ///
    /// The text is stored rather than a key, because most of these lines are assembled from the
    /// company's own figures on the day they were sent. A key would replay a 2023 message with 2027
    /// numbers in it.
    /// </summary>
    [Serializable]
    public sealed class ChatLineData
    {
        public int day;
        public bool mine;
        public string text = string.Empty;
    }

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

        // ---- added in v46, when people got bonuses and hours ---------------------------------

        /// <summary>Settling-in bought with money. Loyalty reads it, so it is causal.</summary>
        public int bonusDays;

        /// <summary>The working day. Eight to four on anybody nobody has moved.</summary>
        public int startHour = Hire.DefaultStartHour;

        public int endHour = Hire.DefaultEndHour;
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
