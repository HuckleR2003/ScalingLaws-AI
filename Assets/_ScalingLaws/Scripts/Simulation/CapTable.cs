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
        private readonly List<Holding> holders = new();

        public IReadOnlyList<FundingRoundRecord> Rounds => rounds;

        /// <summary>
        /// Who owns the rest of it, by name.
        ///
        /// **The founder's share used to be the whole story, and one number cannot be a bar.**
        /// The screen said the founders held ninety three per cent and nothing said who had the
        /// other seven or what they paid for it, so an investor was an abstraction the player
        /// diluted themselves against rather than somebody sitting on their board.
        /// </summary>
        public IReadOnlyList<Holding> Holders => holders;

        /// <summary>Founders' share of the company. Starts whole, only ever goes down.</summary>
        public double FounderEquity { get; private set; } = 1.0;

        public FundingStage LastStage { get; private set; } = FundingStage.Seed;

        public long LastPostMoneyValuationUsd { get; private set; }

        public long TotalRaisedUsd { get; private set; }

        public bool HasBoardControl => FounderEquity >= BoardControlThreshold;

        /// <summary>True once investors own enough to insist the company chase revenue.</summary>
        public bool IsUnderInvestorMandate => FounderEquity < InvestorMandateThreshold;

        public int RoundCount => rounds.Count;

        /// <summary>
        /// Everything not held by a name. Derived rather than stored, so the bar on the screen
        /// cannot disagree with the rows under it however the holdings were arrived at.
        /// </summary>
        public double InvestorEquity
        {
            get
            {
                var held = 0.0;
                foreach (var holding in holders)
                {
                    held += holding.Fraction;
                }

                return Math.Clamp(held, 0.0, 1.0);
            }
        }

        /// <summary>
        /// Hands a named investor their slice, and dilutes everybody who was already here.
        ///
        /// **Everyone dilutes, including the founders, which is what makes the bar add up.** New
        /// shares are issued, so every existing holding is the same number of shares out of a
        /// larger company. Topping up a name that is already on the register rather than adding a
        /// second row, because a second row is the same investor twice and the screen would be
        /// the only thing that knew.
        /// </summary>
        public void Issue(InvestorId investor, double fraction, long paidUsd, GameDate on)
        {
            var slice = Math.Clamp(SimUnits.Finite(fraction), 0.0, 0.95);
            if (slice <= 0.0)
            {
                return;
            }

            // **An unnamed round still dilutes.** Refusing outright meant a term sheet with nobody
            // at the top of it took nothing from anybody while the money still landed, which is the
            // shape of a cheat. Every caller names somebody today; this is the floor under the ones
            // that will not, and an old save whose rounds predate the register lands here.
            if (investor == InvestorId.None)
            {
                FounderEquity = Math.Clamp(FounderEquity * (1.0 - slice), 0.0, 1.0);

                for (var index = 0; index < holders.Count; index++)
                {
                    holders[index] = holders[index].Diluted(1.0 - slice);
                }

                return;
            }

            var kept = 1.0 - slice;

            for (var index = 0; index < holders.Count; index++)
            {
                holders[index] = holders[index].Diluted(kept);
            }

            FounderEquity = Math.Clamp(FounderEquity * kept, 0.0, 1.0);

            for (var index = 0; index < holders.Count; index++)
            {
                if (holders[index].Investor != investor)
                {
                    continue;
                }

                holders[index] = holders[index].Plus(slice, paidUsd);
                return;
            }

            holders.Add(new Holding(investor, slice, paidUsd, on));
        }

        /// <summary>
        /// The slice Emil's firm has held since before there was anything to hold.
        ///
        /// Seeded rather than raised, because no round was ever signed for it: it is the favour
        /// the tutorial is built on, priced at nothing, and the founders keep the rest.
        /// </summary>
        public void SeedFriendsAndFamily(GameDate on)
        {
            if (holders.Count > 0)
            {
                return;
            }

            holders.Add(new Holding(InvestorId.ESolutions,
                InvestorCatalog.FriendsAndFamilyStake, 0L, on));

            FounderEquity = Math.Clamp(1.0 - InvestorCatalog.FriendsAndFamilyStake, 0.0, 1.0);
        }

        /// <summary>
        /// Books a closed round.
        ///
        /// **The dilution happens in `Issue` now and not here**, or a round would take the slice
        /// off the founders twice: once against the register and once against this figure. The
        /// caller issues to the name that wrote the cheque and then records what was signed.
        /// </summary>
        public void Record(FundingRoundRecord round)
        {
            rounds.Add(round);
            LastStage = round.Stage;
            LastPostMoneyValuationUsd = round.PostMoneyValuationUsd;
            TotalRaisedUsd += round.RaisedUsd;
        }

        /// <summary>Restores a loaded campaign without replaying the dilution arithmetic.</summary>
        public void Restore(IEnumerable<FundingRoundRecord> history, double founderEquity,
            IEnumerable<Holding> register = null)
        {
            rounds.Clear();
            holders.Clear();
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

            if (register == null)
            {
                return;
            }

            foreach (var holding in register)
            {
                if (holding.Investor != InvestorId.None && holding.Fraction > 0.0)
                {
                    holders.Add(holding);
                }
            }
        }

        /// <summary>What the founders' slice is worth at a given company valuation.</summary>
        public long FounderStakeValueUsd(long valuationUsd)
        {
            return SimUnits.ToDollars(Math.Max(0L, valuationUsd) * FounderEquity);
        }

        public override string ToString() =>
            $"{rounds.Count} rounds, founders hold {FounderEquity:P1}, raised ${TotalRaisedUsd:N0}";
    }

    /// <summary>
    /// One name on the register, what they hold, and what they paid for it.
    ///
    /// A struct because it is a reading the interface is handed rather than a thing it can move,
    /// which is the rule every snapshot in this project follows.
    /// </summary>
    public readonly struct Holding
    {
        public Holding(InvestorId investor, double fraction, long paidUsd, GameDate since)
        {
            Investor = investor;
            Fraction = Math.Clamp(SimUnits.Finite(fraction), 0.0, 1.0);
            PaidUsd = Math.Max(0L, paidUsd);
            Since = since;
        }

        public InvestorId Investor { get; }

        /// <summary>Share of the whole company, after every dilution since.</summary>
        public double Fraction { get; }

        /// <summary>What they have put in, across every round they took part in.</summary>
        public long PaidUsd { get; }

        public GameDate Since { get; }

        public Holding Diluted(double kept) =>
            new(Investor, Fraction * Math.Clamp(kept, 0.0, 1.0), PaidUsd, Since);

        public Holding Plus(double fraction, long paidUsd) =>
            new(Investor, Fraction + fraction, PaidUsd + Math.Max(0L, paidUsd), Since);

        /// <summary>What their slice is worth at a given company valuation.</summary>
        public long ValueUsd(long valuationUsd) =>
            SimUnits.ToDollars(Math.Max(0L, valuationUsd) * Fraction);

        public override string ToString() => $"{Investor} {Fraction:P2}";
    }
}
