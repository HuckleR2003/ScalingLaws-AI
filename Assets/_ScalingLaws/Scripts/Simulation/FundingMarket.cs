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
            bool isDownRound,
            InvestorId investor = InvestorId.None)
        {
            Investor = investor;
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

        /// <summary>
        /// Who is writing it.
        ///
        /// **One term sheet type, two ways of being handed one.** A round the player goes looking
        /// for and a firm that turns up unasked are the same piece of paper with a different name
        /// at the top, and building a second type for the second case is how a game ends up with
        /// two accept paths that disagree about what dilution means.
        /// </summary>
        public InvestorId Investor { get; }

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
        ///
        /// Two halves, and only one of them is discounted. The story is what one model says the
        /// company might become, and a cheque writer pays for that in proportion to how much
        /// they trust the company telling it. Revenue is the thing the story was a proxy for and
        /// counts in full. See `FundingCatalog.TrustIn`.
        /// </summary>
        public static long PreMoneyValuationUsd(
            GameDate date,
            double bestCapability,
            double frontierCapability,
            long annualRevenueRunRateUsd,
            double reputation,
            double fans)
        {
            var frontier = Math.Max(1.0, frontierCapability);
            var ratio = Math.Clamp(bestCapability / frontier, 0.0, 1.25);

            var storyValue = FundingCatalog.FrontierParityValuationUsd
                * Math.Pow(ratio, FundingCatalog.CapabilityValuationExponent)
                * FundingCatalog.TrustIn(reputation, fans);

            // **The revenue half is discounted for being behind too, lightly.** It used to read no
            // parity at all, which made the fourth power on the story half fade to nothing as a
            // company grew: measured on 2026-11-01, a lab at 44 per cent of the frontier with a
            // billion of revenue sold exactly as little of itself as a frontier lab did. The gate
            // that used to refuse the round had been hiding that, and removing it the same day is
            // what made it visible. Asked for by the author: a large company should hurt a little.
            var revenueValue = Math.Max(0L, annualRevenueRunRateUsd)
                * FundingCatalog.RevenueMultiple
                * FundingCatalog.RevenueParityFactor(ratio);

            var sentiment = FundingCatalog.SentimentOn(date);
            return SimUnits.ToDollars((storyValue + revenueValue) * sentiment);
        }

        /// <summary>Checks one stage against the company without building an offer.</summary>
        public static FundingAvailability Evaluate(
            FundingStage stage,
            GameDate date,
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

            // **There is no parity gate here any more, and that is deliberate.** Removed
            // 2026-11-01 after a tester reported the late game as unsurvivable and the probe
            // measured it: every scripted style that plays the game falls under the Series A
            // requirement of 55 per cent of the frontier in its second year and is then locked out
            // of every round for the rest of the campaign. One round raised, ever.
            //
            // The frontier is designed to run away, so a gate written as a share of it is the
            // design arguing with itself. And the cost of falling behind is already charged once,
            // where it belongs: PreMoneyValuationUsd raises the story half to the fourth power of
            // parity, so a company at 44 per cent is valued on a $75M story against $2B at parity.
            // A $25M Series A therefore costs that company a quarter of itself and costs a
            // frontier lab about one per cent. That is a severe, visible, plannable price.
            //
            // Charging a second time by refusing the round is not a price, it is erasure, and this
            // repository had already settled that principle in those words for reputation and for
            // valuation. The game's own dossier for Algho Alpha has half a billion arriving while
            // the frontier triples past it, so the research disagreed with the ladder too.
            //
            // Measured both ways before it was changed: with the gate off, Frugal survives to 2030
            // instead of 2026 and Reckless to 2030 instead of 2027, each taking one more round.
            // Nobody reaches 2031 either way, because the whole market contracts by 49 per cent
            // between 2027 and 2030, which is a separate open question in Docs/TESTER_REPORTS.md.
            //
            // The calendar, the revenue and the released-model requirements are untouched, so no
            // capital skips a gate it used to wait for. **The loan ladder still gates on parity**,
            // at 0.30 to 0.90 by product, and nothing here measured that one.

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
            long lastPostMoneyValuationUsd,
            double reputation,
            double fans)
        {
            var definition = FundingCatalog.Get(stage);

            var preMoney = PreMoneyValuationUsd(date, bestCapability, frontierCapability,
                annualRevenueRunRateUsd, reputation, fans);

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
