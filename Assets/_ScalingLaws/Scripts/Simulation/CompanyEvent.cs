using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    public enum CompanyEventType
    {
        TrainingStarted = 0,
        TrainingCompleted = 1,
        ModelReleased = 2,
        HardwareOrdered = 3,
        HardwareDelivered = 4,
        HardwareSold = 5,
        ComputeTierUnlocked = 6,
        ArchitectureAdopted = 7,
        DataSourceAcquired = 8,
        DemandUnserved = 9,
        CreditLineBreached = 10,
        Bankrupt = 11,
        UpgradeStarted = 12,
        UpgradeCompleted = 13,
        RivalReleased = 14,
        FundingOffered = 15,
        FundingClosed = 16,
        FundingExpired = 17,
        IntelReceived = 18,
        ModelShelved = 19,
        ArchitectureResearchStarted = 20,
        ArchitectureResearchCompleted = 21,
        ResearchStarted = 22,
        ResearchCompleted = 23,
        LoanTaken = 24,
        LoanSettled = 25,
        LoanMissed = 26,
        LoanDefaulted = 27,
        StaffHired = 28,
        StaffLeft = 29,
        OfficeMoved = 30,
        SafetyIncident = 31,
        SkillLevelled = 32,

        /// <summary>A booked campaign has run its term and stopped costing money.</summary>
        MarketingFinished = 33,

        /// <summary>The year's corporation tax has been billed and is waiting in the inbox.</summary>
        TaxDemanded = 34,

        /// <summary>A demand went past its date and started growing.</summary>
        DemandOverdue = 35,

        /// <summary>The revenue agreed to wait, for a fee.</summary>
        TaxDeferred = 36,

        /// <summary>A research programme was abandoned before it finished.</summary>
        ResearchCancelled = 37,

        /// <summary>A candidate answered, withdrew, or the company signed a partnership.</summary>
        HiringNotice = 38,

        /// <summary>
        /// Something the company should know that nobody has to answer.
        ///
        /// Deliberately broad. Timed effects starting and ending, and the calls that follow a
        /// poaching attempt, are all things that happened rather than things waiting on a decision,
        /// which is exactly the line the mailbox draws between itself and the wire.
        /// </summary>
        Notice = 39,

        /// <summary>Somebody left a rival and joined this company.</summary>
        StaffPoached = 40,

        /// <summary>Money was spent making a competitor look worse.</summary>
        SmearLaunched = 41,

        /// <summary>An action was filed against a rival.</summary>
        LawsuitFiled = 42,

        /// <summary>A court decided one, either way.</summary>
        LawsuitDecided = 43,

        /// <summary>Somebody offered to buy the company.</summary>
        AcquisitionOffered = 44,

        /// <summary>A rival opened something: an office, a site, a region.</summary>
        RivalExpanded = 45,

        /// <summary>The press ran a story about something this company did.</summary>
        ModelScandal = 46,

        /// <summary>The monthly cheque from shares held in other companies.</summary>
        DividendPaid = 47,

        /// <summary>A rival was bought outright.</summary>
        LabAcquired = 48
    }

    /// <summary>
    /// Something worth telling the player about. The simulation queues these; a presenter drains the
    /// queue. Keeping them as data rather than callbacks means an EditMode test can assert on the
    /// exact sequence a day produced.
    /// </summary>
    public readonly struct CompanyEvent
    {
        public CompanyEvent(CompanyEventType type, GameDate date, string message, long amountUsd = 0L)
        {
            Type = type;
            Date = date;
            Message = message ?? string.Empty;
            AmountUsd = amountUsd;
        }

        public CompanyEventType Type { get; }
        public GameDate Date { get; }
        public string Message { get; }
        public long AmountUsd { get; }

        public override string ToString() => $"[{Date}] {Type}: {Message}";
    }
}
