using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Who wants a piece of the company, and what they will pay for it.
    ///
    /// **Reported: there was one offer, from nobody.** A term sheet with no name on it cannot be
    /// compared with anything, so the only question was whether to sign, which is not a question.
    /// Several named firms offering at once is what makes it one, and they differ in the three ways
    /// that matter: what they pay against the market price, how big a cheque they want to write, and
    /// how long they will wait for an answer.
    ///
    /// Taking any of them goes through `CloseRound`, which is the only place equity moves. A round
    /// the player went looking for and a firm that turned up unasked are the same piece of paper.
    /// </summary>
    public sealed partial class CompanySimulation
    {
        /// <summary>
        /// A day at the desk: withdraw what lapsed, and see whether anybody calls.
        ///
        /// **Nothing here rolls a die against a stored counter.** Whether somebody turns up is a
        /// pure function of the campaign seed and the day of the last arrival, so a reload replays
        /// the same callers and no part of the schedule needs saving.
        /// </summary>
        private void AdvanceInvestorDesk()
        {
            var withdrawn = State.Investors.Withdraw(State.Date);

            if (withdrawn > 0)
            {
                State.RaiseEvent(new CompanyEvent(
                    CompanyEventType.Notice,
                    State.Date,
                    Loc.T("investor.withdrew", withdrawn)));
            }

            if (State.Investors.IsFull)
            {
                return;
            }

            if (!InvestorDesk.SomebodyCallsOn(State.RosterSeed, State.Date, State.LastInvestorCallDayIndex))
            {
                return;
            }

            // Nobody offers for a company that has not shown them anything. The stage ladder is
            // already the answer to "what does this company qualify for", so the desk asks it rather
            // than inventing a second set of conditions.
            var availability = NextRoundAvailability();
            if (!availability.IsAvailable)
            {
                return;
            }

            var who = State.Investors.WhoCalls(State.RosterSeed, State.Date);
            if (who == InvestorId.None)
            {
                return;
            }

            var offer = BuildInvestorOffer(who, availability.Stage);
            if (!State.Investors.Add(offer))
            {
                return;
            }

            State.LastInvestorCallDayIndex = State.Date.DayIndex;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.FundingOffered,
                State.Date,
                Loc.T("investor.called",
                    InvestorCatalog.Get(who).DisplayName,
                    UiMoney(offer.RaiseUsd),
                    UiFormatPercent(offer.EquitySold)),
                offer.RaiseUsd));
        }

        /// <summary>
        /// One firm's terms for a stage.
        ///
        /// **Their appetite moves the price, not the money.** A firm that pays up values the company
        /// higher and therefore takes less of it for the same cheque, which is the whole shape of the
        /// trade: the dearest money is the money that asks for the most of your company, and the
        /// patient money is the money that waits while it gets worse.
        /// </summary>
        private FundingOffer BuildInvestorOffer(InvestorId investor, FundingStage stage)
        {
            var definition = InvestorCatalog.Get(investor);
            var stageDefinition = FundingCatalog.Get(stage);

            var market = FundingMarket.PreMoneyValuationUsd(
                State.Date,
                State.BestCapability,
                Market.FrontierCapability,
                State.AnnualRevenueRunRateUsd,
                State.Reputation,
                State.Fans);

            var preMoney = SimUnits.ToDollars(market * definition.Appetite
                * State.Founder.ValuationMultiplier);

            // Nobody writes a cheque larger than the company is worth, whatever the stage says.
            var raise = Math.Min(
                SimUnits.ToDollars(stageDefinition.TargetRaiseUsd * definition.TicketShare),
                Math.Max(1_000_000L, preMoney));

            var last = State.CapTable.LastPostMoneyValuationUsd;
            var isDownRound = last > 0 && preMoney < last;

            var postMoney = preMoney + raise;
            var equity = postMoney <= 0 ? 0.95 : raise / (double)postMoney;

            if (isDownRound)
            {
                equity *= FundingCatalog.DownRoundPenalty;
            }

            return new FundingOffer(
                stage,
                State.Date,
                State.Date.AddDays(definition.PatienceDays),
                raise,
                preMoney,
                Math.Clamp(equity, 0.0, 0.95),
                FundingCatalog.SentimentOn(State.Date),
                isDownRound,
                investor);
        }

        /// <summary>
        /// Takes one of the offers on the table.
        ///
        /// Public because the book is the only caller and it is the one decision on that screen.
        /// </summary>
        public bool TryTakeInvestorOffer(InvestorId investor, out string failureReason)
        {
            failureReason = string.Empty;

            if (!State.Investors.TryTake(investor, out var offer))
            {
                failureReason = Loc.T("funding.none_open");
                return false;
            }

            if (offer.HasExpired(State.Date))
            {
                failureReason = Loc.T("funding.lapsed");
                return false;
            }

            CloseRound(offer);
            return true;
        }

        /// <summary>
        /// The money lands, the register moves, and everything else on the table goes.
        ///
        /// **One body, because there is one thing that can happen.** Two accept paths is two places
        /// to get dilution wrong, and the register and the founder's share have to move together or
        /// the bar on the screen stops adding up to a company.
        /// </summary>
        private void CloseRound(FundingOffer offer)
        {
            State.PostCash(LedgerLine.Funding, offer.RaiseUsd);

            State.CapTable.Issue(offer.Investor, offer.EquitySold, offer.RaiseUsd, State.Date);

            State.CapTable.Record(new FundingRoundRecord(
                offer.Stage,
                State.Date,
                offer.RaiseUsd,
                offer.PostMoneyValuationUsd,
                offer.EquitySold,
                offer.IsDownRound));

            // Every other term sheet was priced against a company that no longer exists.
            State.Investors.ClearAll();
            State.CurrentFundingOffer = FundingOffer.None;
            State.LastRoundClosedOn = State.Date;

            var who = offer.Investor == InvestorId.None
                ? FundingCatalog.Get(offer.Stage).DisplayName
                : InvestorCatalog.Get(offer.Investor).DisplayName;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.FundingClosed,
                State.Date,
                Loc.T("funding.closed_with", who, UiFormatPercent(State.CapTable.FounderEquity)),
                offer.RaiseUsd));
        }

        /// <summary>
        /// What one holder has made from the company today, and this month.
        ///
        /// **Their share of the profit, not of the revenue**, because a shareholder owns what is
        /// left rather than what came in, and a loss-making month really does cost them. Derived
        /// from the ledger the finance report already reads, so the two can never disagree.
        /// </summary>
        public (long Day, long Month) HolderEarnings(in Holding holding)
        {
            var ledger = State.Ledger;
            var days = ledger.RecordedDays();

            // The last day the books actually recorded, which is the same day the finance report
            // calls "today". A company that has not traded yet has no days and earns nobody
            // anything, rather than reading a day that is not there.
            var today = days.Count > 0 ? ledger.DayCashFlow(days[days.Count - 1]) : 0L;
            var month = ledger.MonthCashFlow(Ledger.MonthKeyOf(State.Date));

            return (SimUnits.ToDollars(today * holding.Fraction),
                SimUnits.ToDollars(month * holding.Fraction));
        }

    }
}
