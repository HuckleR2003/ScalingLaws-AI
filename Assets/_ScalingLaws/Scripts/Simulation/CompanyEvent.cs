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
        LoanDefaulted = 27
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
