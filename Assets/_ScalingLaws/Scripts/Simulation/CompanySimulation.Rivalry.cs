using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Everything the company does *to* other companies, and what the world does back.
    ///
    /// **A second file on the same class rather than four hundred more lines on the first one.**
    /// `CompanySimulation` is the second largest file in the project and it got there the way
    /// `GameShell` did: every mechanism that needed the till was written into it rather than beside
    /// it. `partial` is the cheapest possible split, because the compiler produces exactly the same
    /// type either way, so nothing about lifetimes, wiring or the save format changes.
    ///
    /// The rule the whole file follows: **money and relationships move here, never in a panel.**
    /// </summary>
    public sealed partial class CompanySimulation
    {
        /// <summary>How much of a smear wears off each day. About a season to fade out.</summary>
        public const double SmearDecayPerDay = 0.012;

        // ---- smear campaigns ----------------------------------------------------------------------

        /// <summary>Whether this lab can be targeted today, and when it could be if not.</summary>
        public bool CanSmear(CompetitorId lab, out int quietUntilDayIndex)
        {
            quietUntilDayIndex = State.SmearQuietUntil.TryGetValue(lab, out var until) ? until : -1;
            return quietUntilDayIndex <= State.Date.DayIndex;
        }

        /// <summary>
        /// Pay to make a competitor look worse.
        ///
        /// **The relationship is charged whether or not it is traced back**, and that is the honest
        /// reading: they may not be able to prove who paid for it, but they know, and the game does
        /// not pretend otherwise. What the backfire roll decides is whether it also lands on the
        /// company that paid, in public, at more than it would have gained.
        /// </summary>
        public bool TrySmear(CompetitorId lab, SmearTier tier, out bool backfired, out string note)
        {
            backfired = false;
            note = string.Empty;

            var definition = SmearCatalog.Get(tier);

            if (!CanSmear(lab, out var until))
            {
                note = Loc.T("smear.too_soon", Math.Max(0, until - State.Date.DayIndex));
                return false;
            }

            if (State.CashUsd < definition.CostUsd)
            {
                note = Loc.T("smear.no_cash");
                return false;
            }

            State.CashUsd -= definition.CostUsd;
            State.SmearQuietUntil[lab] = State.Date.DayIndex + definition.QuietDays;

            var them = CompetitorCatalog.NameOf(lab);

            // Its own stream, keyed on the day and the target, so this cannot shift the draws every
            // balance test downstream depends on. Same reason the rival rosters have their own.
            var random = new DeterministicRandom(
                RivalryMix(State.RosterSeed, (uint)lab, (uint)State.Date.DayIndex, 0x5EEDu));

            backfired = random.NextChance(definition.BackfireChance);

            if (backfired)
            {
                var cost = definition.BrandDamage * SmearCatalog.BackfireSeverity;

                State.Reputation = Math.Clamp(State.Reputation - cost, 0.0, 1.0);
                State.LastTroubleDayIndex = State.Date.DayIndex;

                State.Relations.Record(lab, State.Date,
                    definition.RelationCost * 1.5, "relation.reason.smear_caught", them);

                note = Loc.T("smear.backfired", them);

                State.RaiseEvent(new CompanyEvent(CompanyEventType.SmearLaunched, State.Date,
                    Loc.T("smear.event.backfired", them), -definition.CostUsd));

                return true;
            }

            State.SmearDamage.TryGetValue(lab, out var standing);
            State.SmearDamage[lab] = Math.Clamp(standing + definition.BrandDamage, 0.0, 0.6);

            State.Relations.Record(lab, State.Date,
                definition.RelationCost, "relation.reason.smeared", them);

            note = Loc.T("smear.landed", them);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.SmearLaunched, State.Date,
                Loc.T("smear.event.landed", them), -definition.CostUsd));

            return true;
        }

        /// <summary>What a lab's standing is currently multiplied by, after anything paid for.</summary>
        public double RivalStandingMultiplier(CompetitorId lab)
        {
            var level = RivalExpansion.LevelOn(State.RosterSeed, lab, State.Date);
            var grown = RivalExpansion.BrandMultiplier(level);

            var damaged = State.SmearDamage.TryGetValue(lab, out var damage) ? damage : 0.0;

            return SimUnits.Finite(grown * Math.Clamp(1.0 - damage, 0.4, 1.0), 1.0);
        }

        private void FadeSmears()
        {
            if (State.SmearDamage.Count == 0)
            {
                return;
            }

            var labs = new List<CompetitorId>(State.SmearDamage.Keys);

            foreach (var lab in labs)
            {
                var left = State.SmearDamage[lab] - SmearDecayPerDay;

                if (left <= 0.0005)
                {
                    State.SmearDamage.Remove(lab);
                }
                else
                {
                    State.SmearDamage[lab] = left;
                }
            }
        }

        // ---- lawsuits -----------------------------------------------------------------------------

        /// <summary>
        /// Whether there is a case against this lab, and the most that could be demanded.
        ///
        /// Reads the grounds off state the game already keeps rather than rolling for them, so the
        /// player can always point at the thing they are suing over.
        /// </summary>
        public bool CanSue(CompetitorId lab, out long ceilingUsd, out string groundsKey)
        {
            ceilingUsd = 0L;
            groundsKey = string.Empty;

            foreach (var suit in State.Lawsuits)
            {
                if (suit.Target == lab && !suit.IsClosed)
                {
                    return false;
                }
            }

            var flagship = Flagship();

            if (flagship == null)
            {
                return false;
            }

            var arguable = LawsuitBook.GroundsAgainst(lab, flagship.Capability, flagship.ReleaseDate,
                State.Rivals.LiveModels(State.Date), State.Relations.With(lab), out groundsKey);

            if (!arguable)
            {
                return false;
            }

            ceilingUsd = LawsuitBook.CeilingFor(State.AnnualRevenueRunRateUsd, State.CashUsd);
            return true;
        }

        /// <summary>
        /// File it. The costs are paid now and are never refunded, win or lose.
        ///
        /// That ordering is the point: a case is a bill today against a chance of money in nine
        /// months, which is the same shape as everything else in this game that is worth doing.
        /// </summary>
        public bool TryFileLawsuit(CompetitorId lab, long damagesDemandedUsd, out string why)
        {
            why = string.Empty;

            if (!CanSue(lab, out var ceiling, out var groundsKey))
            {
                why = Loc.T("suit.no_grounds");
                return false;
            }

            var demanded = Math.Clamp(damagesDemandedUsd, 0L, ceiling);
            var costs = LawsuitBook.CostOf(demanded);

            if (State.CashUsd < costs)
            {
                why = Loc.T("suit.no_cash");
                return false;
            }

            State.CashUsd -= costs;
            State.Lawsuits.Add(new Lawsuit(lab, State.Date, demanded, costs, groundsKey));

            var them = CompetitorCatalog.NameOf(lab);

            State.Relations.Record(lab, State.Date,
                LawsuitBook.RelationCostOfFiling, "relation.reason.sued", them);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.LawsuitFiled, State.Date,
                Loc.T("suit.event.filed", them), -costs));

            return true;
        }

        private void AdvanceLawsuits()
        {
            for (var index = 0; index < State.Lawsuits.Count; index++)
            {
                var suit = State.Lawsuits[index];

                if (suit.IsClosed)
                {
                    continue;
                }

                suit.Advance();

                if (!suit.ReadyForJudgment)
                {
                    continue;
                }

                Judge(suit);
            }
        }

        /// <summary>
        /// The verdict, rolled on the day the case closes and not before.
        ///
        /// A loss is downgraded to a settlement most of the time, because a case with real grounds
        /// behind it rarely ends in nothing and a straight coin flip on a nine-figure sum is a
        /// slot machine rather than a decision.
        /// </summary>
        private void Judge(Lawsuit suit)
        {
            var ceiling = LawsuitBook.CeilingFor(State.AnnualRevenueRunRateUsd, State.CashUsd);
            var odds = LawsuitBook.OddsFor(suit.DamagesDemandedUsd, Math.Max(1L, ceiling));

            var random = new DeterministicRandom(RivalryMix(
                State.RosterSeed, (uint)suit.Target, (uint)suit.FiledOn.DayIndex, 0xC0FFEEu));

            var them = CompetitorCatalog.NameOf(suit.Target);

            if (random.NextChance(odds))
            {
                suit.Decide(LawsuitVerdict.Won, suit.DamagesDemandedUsd);
                State.CashUsd += suit.DamagesDemandedUsd;

                State.Relations.Record(suit.Target, State.Date,
                    -12.0, "relation.reason.suit_won", them);

                State.RaiseEvent(new CompanyEvent(CompanyEventType.LawsuitDecided, State.Date,
                    Loc.T("suit.event.won", them), suit.DamagesDemandedUsd));

                return;
            }

            if (random.NextChance(LawsuitBook.SettlementChance))
            {
                var settled = (long)(suit.DamagesDemandedUsd * LawsuitBook.SettlementShare);

                suit.Decide(LawsuitVerdict.Settled, settled);
                State.CashUsd += settled;

                State.RaiseEvent(new CompanyEvent(CompanyEventType.LawsuitDecided, State.Date,
                    Loc.T("suit.event.settled", them), settled));

                return;
            }

            suit.Decide(LawsuitVerdict.Lost, 0L);

            // Their costs as well as your own, which is the risk the demand was bought with.
            var theirCosts = LawsuitBook.CostOf(suit.DamagesDemandedUsd);
            State.CashUsd -= theirCosts;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.LawsuitDecided, State.Date,
                Loc.T("suit.event.lost", them), -theirCosts));
        }

        // ---- being bought -------------------------------------------------------------------------

        /// <summary>What the company would be worth to somebody buying it today.</summary>
        public long BookValueUsd()
        {
            var fleet = 0L;

            foreach (var asset in State.Pool.Assets)
            {
                fleet += HardwareValuation.ResidualValueUsd(asset, State.Date);
            }

            return Acquisitions.BookValueUsd(State.CashUsd, fleet,
                State.AnnualRevenueRunRateUsd, State.Fans);
        }

        /// <summary>
        /// Whether anybody could be buying today.
        ///
        /// **A state loan blocks it outright.** A government that put a sovereign compute programme
        /// into this company is not going to watch it be sold to a competitor, and without the rule
        /// the strongest line in the game would be to take ten billion of public money and then
        /// sell the company that holds it.
        /// </summary>
        public bool CanBeAcquired(out string why)
        {
            why = string.Empty;

            foreach (var loan in State.Loans.Loans)
            {
                if (loan.IsSettled)
                {
                    continue;
                }

                if (loan.Product is LoanProduct.SovereignCompute or LoanProduct.SovereignSeed)
                {
                    why = Loc.T("buyout.blocked_state");
                    return false;
                }
            }

            return true;
        }

        private void ConsiderAcquisitionOffer()
        {
            if (State.PendingAcquisition != null)
            {
                State.PendingAcquisition.Advance();

                if (State.PendingAcquisition.HasLapsed)
                {
                    State.PendingAcquisition = null;
                }

                return;
            }

            if (State.AcquiredForUsd > 0L || !CanBeAcquired(out _))
            {
                return;
            }

            if (State.AcquisitionRefusedOnDayIndex >= 0
                && State.Date.DayIndex - State.AcquisitionRefusedOnDayIndex
                    < Acquisitions.QuietDaysAfterRefusal)
            {
                return;
            }

            var book = BookValueUsd();

            if (book < Acquisitions.InterestFloorUsd)
            {
                return;
            }

            var random = new DeterministicRandom(RivalryMix(
                State.RosterSeed, 0u, (uint)State.Date.DayIndex, 0xB1DDEDu));

            if (!random.NextChance(Acquisitions.ChancePerDay))
            {
                return;
            }

            var flagship = Flagship();
            var mine = flagship?.Capability ?? 0.0;

            var bidder = StrongestRivalBehind(mine, out var theirs);

            if (!bidder.HasValue)
            {
                return;
            }

            var multiple = Acquisitions.MultipleFor(mine, theirs, random.NextDouble());
            var amount = (long)(book * multiple);

            State.PendingAcquisition = new AcquisitionOffer(bidder.Value, State.Date, amount, multiple);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.AcquisitionOffered, State.Date,
                Loc.T("buyout.event.offered", CompetitorCatalog.NameOf(bidder.Value)), amount));
        }

        /// <summary>Who would want it: the strongest lab that is not already further ahead.</summary>
        private CompetitorId? StrongestRivalBehind(double playerCapability, out double theirs)
        {
            CompetitorId? best = null;
            theirs = 0.0;

            foreach (var model in State.Rivals.LiveModels(State.Date))
            {
                if (model.Capability > playerCapability + 12.0)
                {
                    continue;
                }

                if (best == null || model.Capability > theirs)
                {
                    best = model.Competitor;
                    theirs = model.Capability;
                }
            }

            return best;
        }

        /// <summary>Take the money. The campaign is over and the figure is the score.</summary>
        public bool AcceptAcquisition(out long amountUsd)
        {
            amountUsd = 0L;

            if (State.PendingAcquisition == null)
            {
                return false;
            }

            amountUsd = State.PendingAcquisition.AmountUsd;

            State.AcquiredForUsd = amountUsd;
            State.CashUsd += amountUsd;
            State.PendingAcquisition = null;

            return true;
        }

        /// <summary>Turn them down. Cheap, because a refusal is not an insult.</summary>
        public void DeclineAcquisition()
        {
            if (State.PendingAcquisition == null)
            {
                return;
            }

            var from = State.PendingAcquisition.From;

            State.Relations.Record(from, State.Date, Acquisitions.RelationCostOfRefusal,
                "relation.reason.refused_buyout", CompetitorCatalog.NameOf(from));

            State.AcquisitionRefusedOnDayIndex = State.Date.DayIndex;
            State.PendingAcquisition = null;
        }

        // ---- the world moving on ------------------------------------------------------------------

        private void ReportRivalExpansion()
        {
            foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
            {
                if (!RivalExpansion.StepsUpOn(State.RosterSeed, lab, State.Date))
                {
                    continue;
                }

                var level = RivalExpansion.LevelOn(State.RosterSeed, lab, State.Date);

                State.RaiseEvent(new CompanyEvent(CompanyEventType.RivalExpanded, State.Date,
                    Loc.T(RivalExpansion.HeadlineKey(level), CompetitorCatalog.NameOf(lab))));
            }
        }

        private void RunTheScandalDesk(double sustainedLoad, double marketPricePerMillionUsd)
        {
            if (State.LastScandalDayIndex >= 0
                && State.Date.DayIndex - State.LastScandalDayIndex < ModelScandals.QuietDays)
            {
                return;
            }

            var free = State.Monetization.FreeTierTokensPerUserPerDay;
            var cut = State.LastFreeTierSeen >= 0.0 && free < State.LastFreeTierSeen * 0.5;

            State.LastFreeTierSeen = free;

            var flagship = Flagship();

            if (flagship == null)
            {
                return;
            }

            var sinceRelease = State.Date.DayIndex - flagship.ReleaseDate.DayIndex;
            var corners = flagship.AssaTier <= 0 && flagship.RedTeamTier <= 0
                && State.ReleasedModelCount > 2;

            var kind = ModelScandals.Today(
                State.Reputation,
                State.Monetization.RelativePrice(marketPricePerMillionUsd),
                cut,
                sustainedLoad,
                sinceRelease,
                corners);

            if (kind == ScandalKind.None)
            {
                return;
            }

            State.Reputation = Math.Clamp(
                State.Reputation - ModelScandals.CostFor(kind, State.Reputation), 0.0, 1.0);

            State.LastScandalDayIndex = State.Date.DayIndex;
            State.LastTroubleDayIndex = State.Date.DayIndex;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.ModelScandal, State.Date,
                Loc.T(ModelScandals.HeadlineKey(kind), State.CompanyName)));
        }

        /// <summary>
        /// One hash for every stream in this file.
        ///
        /// Each caller passes its own salt, so a smear, a verdict and a bid taken on the same day
        /// against the same lab are three unrelated draws rather than the same number three times.
        /// </summary>
        private static uint RivalryMix(uint seed, uint lab, uint day, uint salt)
        {
            unchecked
            {
                var value = seed ^ (lab * 2654435761u) ^ (day * 40503u) ^ salt;
                value ^= value >> 15;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;

                return value == 0 ? 0x9E3779B9u : value;
            }
        }
    }
}
