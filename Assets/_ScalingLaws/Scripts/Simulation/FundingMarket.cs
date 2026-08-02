using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>A term sheet on the table, with an expiry date on it.</summary>
    public readonly struct FundingOffer
    {
        public FundingOffer(
            FundingStage stage,
            GameDate openedOn,
            GameDate expiresOn,
            long raiseUsd,
            long preMoneyValuationUsd,
            double equitySold,
            double sentiment,
            bool isDownRound)
        {
            Stage = stage;
            OpenedOn = openedOn;
            ExpiresOn = expiresOn;
            RaiseUsd = Math.Max(0L, raiseUsd);
            PreMoneyValuationUsd = Math.Max(0L, preMoneyValuationUsd);
            EquitySold = Math.Clamp(SimUnits.Finite(equitySold), 0.0, 0.95);
            Sentiment = Math.Max(0.0, SimUnits.Finite(sentiment, 1.0));
            IsDownRound = isDownRound;
        }

        public FundingStage Stage { get; }
        public GameDate OpenedOn { get; }
        public GameDate ExpiresOn { get; }
        public long RaiseUsd { get; }
        public long PreMoneyValuationUsd { get; }

        /// <summary>Share of the whole company the new investors take.</summary>
        public double EquitySold { get; }

        public double Sentiment { get; }

        /// <summary>Priced below the last round. Same money, considerably more of the company.</summary>
        public bool IsDownRound { get; }

        public long PostMoneyValuationUsd => PreMoneyValuationUsd + RaiseUsd;

        public bool IsOpen => RaiseUsd > 0;

        public bool HasExpired(GameDate date) => date > ExpiresOn;

        public int DaysRemaining(GameDate date) => Math.Max(0, ExpiresOn.DayIndex - date.DayIndex);

        /// <summary>Dollars raised per point of equity given up. The number worth comparing across dates.</summary>
        public double DollarsPerEquityPoint => EquitySold <= 0.0 ? 0.0 : RaiseUsd / (EquitySold * 100.0);

        public static FundingOffer None => default;

        public override string ToString() => IsOpen
            ? $"{Stage}: ${RaiseUsd:N0} for {EquitySold:P1} at ${PreMoneyValuationUsd:N0} pre"
            : "no offer";
    }

    /// <summary>Why a round is or is not available, in a form the UI can render without guessing.</summary>
    public readonly struct FundingAvailability
    {
        public FundingAvailability(FundingStage stage, bool isAvailable, FundingRefusal refusal, string reason)
        {
            Stage = stage;
            IsAvailable = isAvailable;
            Refusal = isAvailable ? FundingRefusal.None : refusal;
            Reason = isAvailable ? string.Empty : reason ?? string.Empty;
        }

        public FundingStage Stage { get; }
        public bool IsAvailable { get; }
        public FundingRefusal Refusal { get; }
        public string Reason { get; }

        public override string ToString() => IsAvailable ? $"{Stage}: open" : $"{Stage}: {Reason}";
    }

    /// <summary>
    /// The ONE place capital is priced. Pure functions of company standing and the calendar, so a
    /// test can ask what a Series B would have cost in March 2023 without simulating to March 2023.
    ///
    /// Valuation has two halves and both matter:
    ///   the story    how close the company sits to the frontier, to the fourth power
    ///   the numbers  annual revenue run rate at a multiple
    /// Multiplied by investor sentiment, which is the AI funding cycle and swings four to one across
    /// the campaign. A lab that raises on a good story in a hot market keeps far more of itself than
    /// one that waits for the revenue to be undeniable.
    /// </summary>
    public static class FundingMarket
    {
        /// <summary>Days after a round closes before anyone will discuss the next one.</summary>
        public const int CooldownDays = 180;

        /// <summary>
        /// What the market would price the company at today, before any new money goes in.
        /// </summary>
        public static long PreMoneyValuationUsd(
            GameDate date,
            double bestCapability,
            double frontierCapability,
            long annualRevenueRunRateUsd)
        {
            var frontier = Math.Max(1.0, frontierCapability);
            var ratio = Math.Clamp(bestCapability / frontier, 0.0, 1.25);

            var storyValue = FundingCatalog.FrontierParityValuationUsd
                * Math.Pow(ratio, FundingCatalog.CapabilityValuationExponent);
            var revenueValue = Math.Max(0L, annualRevenueRunRateUsd) * FundingCatalog.RevenueMultiple;

            var sentiment = FundingCatalog.SentimentOn(date);
            return SimUnits.ToDollars((storyValue + revenueValue) * sentiment);
        }

        /// <summary>Checks one stage against the company without building an offer.</summary>
        public static FundingAvailability Evaluate(
            FundingStage stage,
            GameDate date,
            double bestCapability,
            double frontierCapability,
            long annualRevenueRunRateUsd,
            int releasedModels)
        {
            if (!FundingCatalog.TryGet(stage, out var definition))
            {
                return new FundingAvailability(stage, false, FundingRefusal.AlreadyRaised, "No further rounds.");
            }

            if (date.IsBefore(definition.EarliestDate))
            {
                return new FundingAvailability(stage, false, FundingRefusal.TooEarly,
                    $"Not before {definition.EarliestDate}.");
            }

            if (releasedModels < definition.RequiredReleasedModels)
            {
                return new FundingAvailability(stage, false, FundingRefusal.NeedsReleasedModels,
                    $"Needs {definition.RequiredReleasedModels} released model(s), has {releasedModels}.");
            }

            var frontier = Math.Max(1.0, frontierCapability);
            var ratio = bestCapability / frontier;
            if (ratio < definition.RequiredCapabilityRatio)
            {
                return new FundingAvailability(stage, false, FundingRefusal.NeedsCapability,
                    $"Needs {definition.RequiredCapabilityRatio:P0} of the frontier, sits at {Math.Max(0.0, ratio):P0}.");
            }

            if (annualRevenueRunRateUsd < definition.RequiredAnnualRevenueUsd)
            {
                return new FundingAvailability(stage, false, FundingRefusal.NeedsRevenue,
                    $"Needs ${definition.RequiredAnnualRevenueUsd:N0} annual run rate, has ${Math.Max(0L, annualRevenueRunRateUsd):N0}.");
            }

            return new FundingAvailability(stage, true, FundingRefusal.None, string.Empty);
        }

        /// <summary>
        /// Builds the term sheet for a stage that has already passed <see cref="Evaluate"/>.
        /// A round priced under the previous one is a down round and costs extra dilution.
        /// </summary>
        public static FundingOffer BuildOffer(
            FundingStage stage,
            GameDate date,
            double bestCapability,
            double frontierCapability,
            long annualRevenueRunRateUsd,
            long lastPostMoneyValuationUsd)
        {
            var definition = FundingCatalog.Get(stage);
            var preMoney = PreMoneyValuationUsd(date, bestCapability, frontierCapability, annualRevenueRunRateUsd);

            // Investors will not write a cheque larger than the company is worth. A small lab in a
            // cold market gets a small round, whatever the stage nominally says.
            var raise = Math.Min(definition.TargetRaiseUsd, Math.Max(1_000_000L, preMoney));

            var isDownRound = lastPostMoneyValuationUsd > 0 && preMoney < lastPostMoneyValuationUsd;
            var postMoney = preMoney + raise;
            var equity = postMoney <= 0 ? 0.95 : raise / (double)postMoney;

            if (isDownRound)
            {
                equity *= FundingCatalog.DownRoundPenalty;
            }

            return new FundingOffer(
                stage,
                date,
                date.AddDays(definition.OfferWindowDays),
                raise,
                preMoney,
                Math.Clamp(equity, 0.0, 0.95),
                FundingCatalog.SentimentOn(date),
                isDownRound);
        }
    }
}
