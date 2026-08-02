using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One closed round, kept so the next one can be priced against it.</summary>
    public readonly struct FundingRoundRecord
    {
        public FundingRoundRecord(
            FundingStage stage,
            GameDate closedOn,
            long raisedUsd,
            long postMoneyValuationUsd,
            double equitySold,
            bool wasDownRound)
        {
            Stage = stage;
            ClosedOn = closedOn;
            RaisedUsd = Math.Max(0L, raisedUsd);
            PostMoneyValuationUsd = Math.Max(1L, postMoneyValuationUsd);
            EquitySold = Math.Clamp(SimUnits.Finite(equitySold), 0.0, 1.0);
            WasDownRound = wasDownRound;
        }

        public FundingStage Stage { get; }
        public GameDate ClosedOn { get; }
        public long RaisedUsd { get; }
        public long PostMoneyValuationUsd { get; }
        public double EquitySold { get; }

        /// <summary>Priced below the previous round. Investors take more for the same money.</summary>
        public bool WasDownRound { get; }

        public override string ToString() =>
            $"{Stage} {ClosedOn}: ${RaisedUsd:N0} at ${PostMoneyValuationUsd:N0} for {EquitySold:P1}";
    }

    /// <summary>
    /// Who owns the company. Every round multiplies the founders' slice by what is left after the
    /// new investors take theirs, so dilution compounds and cannot be undone.
    ///
    /// Selling equity is not free money. It is the only free money in the game, which is why the
    /// question is never whether to raise but when: the same eight percent buys 25 million dollars
    /// in 2022 and 400 million in mid 2025.
    /// </summary>
    public sealed class CapTable
    {
        /// <summary>Below this the board can override the founders on strategy.</summary>
        public const double BoardControlThreshold = 0.50;

        /// <summary>Below this investors force the company toward revenue over research.</summary>
        public const double InvestorMandateThreshold = 0.30;

        private readonly List<FundingRoundRecord> rounds = new();

        public IReadOnlyList<FundingRoundRecord> Rounds => rounds;

        /// <summary>Founders' share of the company. Starts whole, only ever goes down.</summary>
        public double FounderEquity { get; private set; } = 1.0;

        public FundingStage LastStage { get; private set; } = FundingStage.Seed;

        public long LastPostMoneyValuationUsd { get; private set; }

        public long TotalRaisedUsd { get; private set; }

        public bool HasBoardControl => FounderEquity >= BoardControlThreshold;

        /// <summary>True once investors own enough to insist the company chase revenue.</summary>
        public bool IsUnderInvestorMandate => FounderEquity < InvestorMandateThreshold;

        public int RoundCount => rounds.Count;

        public void Record(FundingRoundRecord round)
        {
            rounds.Add(round);
            FounderEquity = Math.Clamp(FounderEquity * (1.0 - round.EquitySold), 0.0, 1.0);
            LastStage = round.Stage;
            LastPostMoneyValuationUsd = round.PostMoneyValuationUsd;
            TotalRaisedUsd += round.RaisedUsd;
        }

        /// <summary>Restores a loaded campaign without replaying the dilution arithmetic.</summary>
        public void Restore(IEnumerable<FundingRoundRecord> history, double founderEquity)
        {
            rounds.Clear();
            FounderEquity = 1.0;
            LastStage = FundingStage.Seed;
            LastPostMoneyValuationUsd = 0;
            TotalRaisedUsd = 0;

            if (history != null)
            {
                foreach (var round in history)
                {
                    rounds.Add(round);
                    LastStage = round.Stage;
                    LastPostMoneyValuationUsd = round.PostMoneyValuationUsd;
                    TotalRaisedUsd += round.RaisedUsd;
                }
            }

            FounderEquity = Math.Clamp(SimUnits.Finite(founderEquity, 1.0), 0.0, 1.0);
        }

        /// <summary>What the founders' slice is worth at a given company valuation.</summary>
        public long FounderStakeValueUsd(long valuationUsd)
        {
            return SimUnits.ToDollars(Math.Max(0L, valuationUsd) * FounderEquity);
        }

        public override string ToString() =>
            $"{rounds.Count} rounds, founders hold {FounderEquity:P1}, raised ${TotalRaisedUsd:N0}";
    }
}
