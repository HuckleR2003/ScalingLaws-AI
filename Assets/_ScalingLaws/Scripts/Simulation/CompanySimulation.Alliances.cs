using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Relations that can go up, and what a company signs when they do.
    ///
    /// **Every way a relation moved before this file was something the player did to somebody.**
    /// Seven recorders, all negative: a smear, a smear traced back, a suit, a refused acquisition,
    /// a lab bought, a person poached. So a player who never attacked anybody sat at Neutral with
    /// fourteen labs for fourteen years and never saw the mechanic at all, and a good relation
    /// bought nothing, because there was no way to have one.
    ///
    /// The four offers are the player's side of deals the industry actually did, and the reasoning
    /// with the sources is in `Docs/RELATIONS_PLAN.md`. What matters here is the shape they share:
    /// **an offer is sent, and answered days later.** Nothing lands on the click. That is the same
    /// rule `RegulatoryAction` and `Lawsuit` already follow, and for the same reason: an outcome
    /// that arrives the instant it is paid for is an outcome nobody can plan around.
    /// </summary>
    public sealed partial class CompanySimulation
    {
        /// <summary>
        /// Sends an offer. The other side answers in its own time.
        ///
        /// The cost is paid on sending and **is not returned when they say no**, which is what makes
        /// choosing who to approach a decision rather than a sweep of the board.
        /// </summary>
        public bool TrySendOffer(CompetitorId lab, RelationOffer offer, out string why)
        {
            var definition = RelationOfferCatalog.Get(offer);

            if (State.PendingOffers.Exists(pending => pending.Lab == lab))
            {
                why = Loc.T("offer.fail.waiting", CompetitorCatalog.NameOf(lab));
                return false;
            }

            if (State.Relations.BandWith(lab) < definition.NeedsAtLeast)
            {
                why = Loc.T("offer.fail.band",
                    RelationScale.NameOf(definition.NeedsAtLeast));

                return false;
            }

            if (definition.NeedsLiveModel && Flagship() == null)
            {
                why = Loc.T("offer.fail.no_model");
                return false;
            }

            if (State.ResearchPoints < definition.PointCost)
            {
                why = Loc.T("offer.fail.points", UiPoints(definition.PointCost));
                return false;
            }

            if (State.CashUsd < definition.CashCostUsd)
            {
                why = Loc.T("offer.fail.cash");
                return false;
            }

            State.ResearchPoints -= definition.PointCost;

            if (definition.CashCostUsd > 0)
            {
                State.PostCash(LedgerLine.Marketing, definition.CashCostUsd);
            }

            State.PendingOffers.Add(new PendingOffer(lab, offer, State.Date));

            // **A published finding is public the day it is published.** It is not a deal, so there
            // is nobody to say no, and every other lab on the board notices a little. That is what
            // keeps the first rung of this system reachable by a company with no money.
            if (offer == RelationOffer.PublishFinding)
            {
                foreach (CompetitorId other in Enum.GetValues(typeof(CompetitorId)))
                {
                    if (other != lab && other != CompetitorId.None)
                    {
                        State.Relations.Record(other, State.Date,
                            RelationOfferCatalog.PublicGainToEverybody, "relation.reason.published");
                    }
                }
            }

            State.RaiseEvent(new CompanyEvent(CompanyEventType.OfferSent, State.Date,
                Loc.T("offer.event.sent", definition.DisplayName, CompetitorCatalog.NameOf(lab)),
                -definition.CashCostUsd));

            why = string.Empty;
            return true;
        }

        /// <summary>
        /// A day of waiting for everybody who has been asked something.
        ///
        /// Walked backwards so an answered offer can be taken out of the list without moving the
        /// ones behind it, which is the shape every list in this simulation drains with.
        /// </summary>
        private void AdvanceOffers()
        {
            for (var index = State.PendingOffers.Count - 1; index >= 0; index--)
            {
                var pending = State.PendingOffers[index];
                var definition = RelationOfferCatalog.Get(pending.Offer);

                if (pending.DaysWaiting(State.Date) < definition.DaysToAnswer)
                {
                    continue;
                }

                State.PendingOffers.RemoveAt(index);
                Answer(pending, definition);
            }

            AdvanceAlliances();
            AdvanceCampaign();

            for (var index = State.Deals.Count - 1; index >= 0; index--)
            {
                if (!State.Deals[index].IsLiveOn(State.Date))
                {
                    var ended = State.Deals[index];
                    State.Deals.RemoveAt(index);

                    Remember(ended.Lab, ended.Offer, ended.Started, DealOutcome.Finished);

                    // **And half the time they ring about it.** A term that always waits to be
                    // noticed is a subscription; one the other company sometimes brings up is two
                    // companies that have been working together.
                    MaybeOfferRenewal(ended.Lab, ended.Offer);

                    State.RaiseEvent(new CompanyEvent(CompanyEventType.DealEnded, State.Date,
                        Loc.T("offer.event.ended",
                            RelationOfferCatalog.Get(ended.Offer).DisplayName,
                            CompetitorCatalog.NameOf(ended.Lab))));
                }
            }
        }

        /// <summary>
        /// An alliance nobody is keeping warm falls apart.
        ///
        /// **Derived from the band rather than hooked onto every hostile act**, and that is the
        /// whole point: there are six places in this simulation that charge a relation, a seventh
        /// will be written one day, and a rule that has to be remembered at each of them is a rule
        /// that gets forgotten at one. A smear takes the relation well under Friendly by itself, so
        /// the level falls the same day without anything having to call in here.
        ///
        /// It also covers the case a hook never would: an alliance left to drift back to Neutral
        /// over a year of neither side doing anything. That is not a betrayal and it is not an
        /// alliance either.
        /// </summary>
        private void AdvanceAlliances()
        {
            List<CompetitorId> cooled = null;

            foreach (var pair in State.Alliances.Signed)
            {
                if (State.Relations.BandWith(pair.Key) < LabAlliances.HoldsAt)
                {
                    cooled ??= new List<CompetitorId>();
                    cooled.Add(pair.Key);
                }
            }

            if (cooled == null)
            {
                return;
            }

            foreach (var lab in cooled)
            {
                BreakAlliance(lab);
            }
        }

        /// <summary>
        /// They answer. Yes signs a term; no costs nothing further and says so.
        ///
        /// **The roll is here rather than at the sending**, which is what makes the wait mean
        /// something and what stops a reload buying a different answer.
        /// </summary>
        private void Answer(PendingOffer pending, RelationOfferDefinition definition)
        {
            var them = CompetitorCatalog.NameOf(pending.Lab);

            var chance = RelationOfferCatalog.AcceptanceChance(
                pending.Offer, State.Relations.With(pending.Lab),
                CapabilityOf(pending.Lab), OurCapability(),
                State.Alliances.LevelWith(pending.Lab));

            // Its own stream, keyed on the day the offer went out and on who it went to, so adding
            // this mechanic cannot shift a single draw the balance suite depends on.
            var random = new DeterministicRandom(RelationMix(
                State.RosterSeed, (uint)pending.Lab, (uint)pending.Sent.DayIndex, 0xA111Eu));

            if (!random.NextChance(chance))
            {
                Remember(pending.Lab, pending.Offer, pending.Sent, DealOutcome.Refused);

                State.RaiseEvent(new CompanyEvent(CompanyEventType.OfferRefused, State.Date,
                    Loc.T("offer.event.refused", definition.DisplayName, them)));

                return;
            }

            State.Relations.Record(pending.Lab, State.Date, definition.RelationGain,
                ReasonKeyFor(pending.Offer), them);

            if (definition.TermDays > 0)
            {
                State.Deals.Add(new StandingDeal(pending.Lab, pending.Offer, State.Date,
                    State.Date.AddDays(definition.TermDays)));
            }

            State.RaiseEvent(new CompanyEvent(CompanyEventType.OfferAccepted, State.Date,
                Loc.T("offer.event.accepted", definition.DisplayName, them)));
        }

        /// <summary>Why the relation moved, written out so the guard can read it.</summary>
        private static string ReasonKeyFor(RelationOffer offer) => offer switch
        {
            RelationOffer.PublishFinding => "relation.reason.published_with",
            RelationOffer.JointEvaluation => "relation.reason.evaluated",
            RelationOffer.DistributionLicence => "relation.reason.licensed",
            _ => "relation.reason.bought_capacity"
        };

        /// <summary>
        /// Writes one line into the history of what the two companies have done together.
        ///
        /// **One body, four callers**, because a refusal, a finished term and an early exit are the
        /// same row with a different last word, and three copies of that is three chances to write
        /// the wrong date into one of them.
        /// </summary>
        private void Remember(CompetitorId lab, RelationOffer offer, GameDate started,
            DealOutcome outcome)
        {
            State.DealHistory.Add(new DealRecord(lab, offer, started, State.Date, outcome));

            while (State.DealHistory.Count > CompanyState.DealsKept)
            {
                State.DealHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// What a signed level is worth to this lab in particular, past the offers it makes easier.
        ///
        /// **Level three sells capacity at cost.** That is the one place an alliance touches a
        /// number the fleet reads, and it is the reason to climb past two once the research
        /// programmes are open: everything below is about being able to ask, and this is about the
        /// price when they say yes.
        /// </summary>
        public double CapacityPremiumWith(CompetitorId lab) =>
            State.Alliances.LevelWith(lab) >= LabAlliances.TopLevel
                ? 1.0
                : RelationOfferCatalog.CapacityPremium;

        /// <summary>Whether a deal of this kind is running with anybody at all.</summary>
        public bool HasDeal(RelationOffer offer)
        {
            foreach (var deal in State.Deals)
            {
                if (deal.Offer == offer && deal.IsLiveOn(State.Date))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What the running deals do to the company, as one multiplier each.
        ///
        /// **Multipliers on numbers that already exist, never a second economy.** `RivalExpansion`
        /// is one multiplier on a rival's standing for exactly this reason: giving fourteen labs a
        /// fleet and a payroll would mean inventing two dozen uncheckable numbers per company.
        /// </summary>
        public double AllianceIncidentMultiplier() =>
            HasDeal(RelationOffer.JointEvaluation) ? RelationOfferCatalog.EvaluationSafety : 1.0;

        /// <summary>Extra audience a distribution partner reaches for the company.</summary>
        public double AllianceReachMultiplier() =>
            HasDeal(RelationOffer.DistributionLicence)
                ? 1.0 + RelationOfferCatalog.DistributionReach
                : 1.0;

        /// <summary>
        /// Petaflops bought from an ally, on top of whatever is rented.
        ///
        /// Priced at a premium over the pool, because reserved capacity always is: that is the same
        /// trade the three hosting packages make and it is what stops this being free compute.
        /// </summary>
        public double AlliedPetaflops()
        {
            var most = 0.0;

            foreach (var deal in State.Deals)
            {
                if (deal.Offer != RelationOffer.CapacityPurchase || !deal.IsLiveOn(State.Date))
                {
                    continue;
                }

                // **At cost from a deep ally, at a premium from everybody else.** The same money
                // buys more capacity from a lab that has signed three levels with you, which is the
                // one place an alliance reaches a number the fleet reads.
                var share = RelationOfferCatalog.CapacityShare
                    * (RelationOfferCatalog.CapacityPremium / CapacityPremiumWith(deal.Lab));

                most = Math.Max(most, State.Pool.RentedPetaflops * share);
            }

            return most;
        }

        // ---- the alliance itself ------------------------------------------------------------

        /// <summary>
        /// Signs the next level with a lab, when the calendar and the band both allow it.
        ///
        /// **The fee is charged here and the clock is not for sale.** Level three needs a year at
        /// level two with nothing hostile in between, and there is no way to pay that down.
        /// </summary>
        /// <summary>
        /// Why the next level cannot be signed with this lab, or empty when it can.
        ///
        /// **Reads and never writes, which is the whole reason it exists.** The rival card used to
        /// call `TrySignAlliance` to get this sentence out of its `out` parameter, so drawing a card
        /// whose level was ready signed the alliance and charged the fee. A render caught it: a
        /// company at level one came back from being looked at sitting at level two. A `Try` method
        /// is a write however harmless its out parameter looks.
        /// </summary>
        public string WhyNotAlliance(CompetitorId lab)
        {
            var band = State.Relations.BandWith(lab);

            if (State.Alliances.CanSignNext(lab, State.Date, band, out var next))
            {
                return string.Empty;
            }

            return band < LabAlliances.HoldsAt
                ? Loc.T("alliance.fail.band", RelationScale.NameOf(LabAlliances.HoldsAt))
                : next > LabAlliances.TopLevel
                    ? Loc.T("alliance.fail.top")
                    : Loc.T("alliance.fail.days",
                        (LabAlliances.DaysNeededFor(next)
                            - State.Alliances.DaysAtLevel(lab, State.Date)).ToString());
        }

        public bool TrySignAlliance(CompetitorId lab, out string why)
        {
            var band = State.Relations.BandWith(lab);

            if (!State.Alliances.CanSignNext(lab, State.Date, band, out _))
            {
                why = WhyNotAlliance(lab);
                return false;
            }

            var next = State.Alliances.LevelWith(lab) + 1;

            var fee = LabAlliances.FeeFor(next);

            if (State.CashUsd < fee)
            {
                why = Loc.T("alliance.fail.cash");
                return false;
            }

            State.PostCash(LedgerLine.Marketing, fee);
            State.Alliances.Sign(lab, State.Date);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.AllianceSigned, State.Date,
                Loc.T("alliance.event.signed", CompetitorCatalog.NameOf(lab), next.ToString()),
                -fee));

            why = string.Empty;
            return true;
        }

        /// <summary>
        /// Something hostile happened, so whatever was signed drops a level.
        ///
        /// Called from the places that already charge the relation, so the two can never disagree
        /// about what counts as hostile. One body, because two would be two chances to forget.
        /// </summary>
        public void BreakAlliance(CompetitorId lab)
        {
            if (State.Alliances.LevelWith(lab) <= 0)
            {
                return;
            }

            State.Alliances.Break(lab, State.Date);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.AllianceBroken, State.Date,
                Loc.T("alliance.event.broken", CompetitorCatalog.NameOf(lab))));
        }



        // ---- renewing something that ran its term ---------------------------------------------

        /// <summary>
        /// The chance the other side rings you about it rather than waiting to be asked.
        ///
        /// **Half, and it is the difference between a relationship and a supplier.** A term that
        /// ends and always waits for the player to notice is a subscription; one where the other
        /// company sometimes calls first is two companies that have been working together. The
        /// author asked for exactly this number.
        /// </summary>
        public const double TheyCallFirstChance = 0.5;

        /// <summary>How long a renewal stays on the table before it is assumed to be a no.</summary>
        public const int RenewalOpenDays = 14;

        /// <summary>
        /// Whether they proposed it themselves, which is what an accepted renewal costs nothing to
        /// find out: no letter, no waiting, no roll. That is the whole value of being called.
        /// </summary>
        public bool RenewalIsOnTheTable =>
            State.Renewal.HasValue
            && State.Renewal.Value.OpenedOn.DayIndex + RenewalOpenDays > State.Date.DayIndex;

        /// <summary>
        /// Takes a renewal the other side offered. Charged, and it starts today.
        ///
        /// **No acceptance roll, and that is the point.** Sending the same offer again through
        /// `TrySendOffer` is always available and costs a wait and a chance of a no. When they rang
        /// you, they have already said yes, so the only question left is whether the company can
        /// pay for it.
        /// </summary>
        public bool TryAcceptRenewal(out string why)
        {
            if (!RenewalIsOnTheTable)
            {
                why = Loc.T("renew.fail.gone");
                return false;
            }

            var renewal = State.Renewal.Value;
            var definition = RelationOfferCatalog.Get(renewal.Offer);

            if (State.ResearchPoints < definition.PointCost)
            {
                why = Loc.T("offer.fail.points", definition.PointCost.ToString());
                return false;
            }

            if (State.CashUsd < definition.CashCostUsd)
            {
                why = Loc.T("offer.fail.cash");
                return false;
            }

            State.ResearchPoints -= definition.PointCost;

            if (definition.CashCostUsd > 0)
            {
                State.PostCash(LedgerLine.Marketing, definition.CashCostUsd);
            }

            State.Deals.Add(new StandingDeal(renewal.Lab, renewal.Offer, State.Date,
                State.Date.AddDays(definition.TermDays)));

            State.Relations.Record(renewal.Lab, State.Date, definition.RelationGain * 0.5,
                ReasonKeyFor(renewal.Offer), CompetitorCatalog.NameOf(renewal.Lab));

            State.Renewal = null;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.OfferAccepted, State.Date,
                Loc.T("renew.event.taken", definition.DisplayName,
                    CompetitorCatalog.NameOf(renewal.Lab)),
                -definition.CashCostUsd));

            why = string.Empty;
            return true;
        }

        /// <summary>Puts it down. Nothing is charged and the relation is not touched.</summary>
        public void DeclineRenewal() => State.Renewal = null;

        /// <summary>
        /// Rolls for whether they ring about a term that has just run out.
        ///
        /// **Only for something that was worth renewing.** A published finding has no term, and a
        /// company does not telephone about a paper. Its own stream, keyed on the day and the lab,
        /// so adding this cannot shift a draw the balance suite depends on.
        /// </summary>
        private void MaybeOfferRenewal(CompetitorId lab, RelationOffer offer)
        {
            if (RelationOfferCatalog.Get(offer).TermDays <= 0 || State.Renewal.HasValue)
            {
                return;
            }

            // They do not ring somebody they have fallen out with in the meantime.
            if (State.Relations.BandWith(lab) < RelationBand.Neutral)
            {
                return;
            }

            var random = new DeterministicRandom(RelationMix(
                State.RosterSeed, (uint)lab, (uint)State.Date.DayIndex, 0x4E5Eu));

            if (!random.NextChance(TheyCallFirstChance))
            {
                return;
            }

            State.Renewal = new PendingRenewal(lab, offer, State.Date);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.RenewalOffered, State.Date,
                Loc.T("renew.event.offered", RelationOfferCatalog.Get(offer).DisplayName,
                    CompetitorCatalog.NameOf(lab))));
        }

        // ---- the joint research campaign ------------------------------------------------------

        /// <summary>
        /// Starts a campaign with the labs named, or says why it cannot.
        ///
        /// **A member has to be at the working-group level**, which is a hundred and eighty days at
        /// level one on top of the ninety it took to get there. That is deliberate and it is the
        /// whole reason this is not a purchase: the cheapest consortium in the game is nine months
        /// of somebody liking you, and nothing in the account shortens that.
        /// </summary>
        public bool TryStartCampaign(CampaignTerm term, IReadOnlyList<CompetitorId> partners,
            out string why)
        {
            if (State.Campaign != null)
            {
                why = Loc.T("campaign.fail.running");
                return false;
            }

            var members = new List<CompetitorId>();

            if (partners != null)
            {
                foreach (var lab in partners)
                {
                    if (members.Contains(lab))
                    {
                        continue;
                    }

                    if (State.Alliances.LevelWith(lab) < ResearchCampaignCatalog.NeedsAllianceLevel)
                    {
                        why = Loc.T("campaign.fail.level", CompetitorCatalog.NameOf(lab),
                            ResearchCampaignCatalog.NeedsAllianceLevel.ToString());

                        return false;
                    }

                    members.Add(lab);
                }
            }

            if (members.Count == 0)
            {
                why = Loc.T("campaign.fail.nobody");
                return false;
            }

            if (members.Count + 1 > ResearchCampaignCatalog.MostMembers)
            {
                why = Loc.T("campaign.fail.crowded",
                    ResearchCampaignCatalog.MostMembers.ToString());

                return false;
            }

            var daily = CampaignDailyCostUsd(members.Count + 1);

            // A month is checked rather than charged: a programme the company cannot fund past its
            // first fortnight is a break fee wearing a research budget.
            if (State.CashUsd < daily * 30)
            {
                why = Loc.T("campaign.fail.cash");
                return false;
            }

            State.Campaign = new ResearchCampaign(term, State.Date,
                State.Date.AddDays(ResearchCampaignCatalog.DaysIn(term)), members);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.CampaignStarted, State.Date,
                Loc.T("campaign.event.started", MemberNames(members),
                    ResearchCampaignCatalog.DaysIn(term).ToString())));

            why = string.Empty;
            return true;
        }

        /// <summary>
        /// A day of the campaign: the bill is paid and the points arrive.
        ///
        /// **Points daily rather than in a lump at the end**, which is what the author asked for and
        /// is also the only honest shape: a laboratory that has been running four months has learned
        /// four months of things, and a programme that pays nothing until it finishes is a purchase
        /// with a delay on it.
        ///
        /// A company that cannot pay the day's bill is dropped rather than allowed to run it for
        /// nothing, and that costs the break fee, because the other side was relying on the money.
        /// </summary>
        private void AdvanceCampaign()
        {
            var campaign = State.Campaign;

            if (campaign == null)
            {
                return;
            }

            if (!campaign.IsLiveOn(State.Date))
            {
                State.Campaign = null;

                State.RaiseEvent(new CompanyEvent(CompanyEventType.CampaignFinished, State.Date,
                    Loc.T("campaign.event.finished", MemberNames(campaign.Members))));

                return;
            }

            // **An alliance that has gone cold cannot go on funding a joint programme.** Read here
            // rather than hooked onto the break, for the same reason the levels are: there is one
            // place that decides an alliance has ended and everything else reads it.
            foreach (var lab in campaign.Members)
            {
                if (State.Alliances.LevelWith(lab) < ResearchCampaignCatalog.NeedsAllianceLevel)
                {
                    LeaveCampaign("campaign.event.collapsed");
                    return;
                }
            }

            var daily = CampaignDailyCostUsd(campaign.Members.Count + 1);

            if (State.CashUsd < daily)
            {
                LeaveCampaign("campaign.event.dropped");
                return;
            }

            State.PostCash(LedgerLine.Research, daily);

            var points = ResearchCampaignCatalog.PointsPerDay
                * ResearchCampaignCatalog.PointsMultiplier(campaign.Members.Count + 1)
                * ResearchCampaignCatalog.RateFor(campaign.Term);

            State.ResearchPoints += SimUnits.Finite(points);
            State.ResearchPointsToday += SimUnits.Finite(points);
        }

        /// <summary>Walks out. The remaining term is forfeit and the break fee is charged.</summary>
        public bool TryLeaveCampaign(out string why)
        {
            if (State.Campaign == null)
            {
                why = Loc.T("campaign.fail.none");
                return false;
            }

            LeaveCampaign("campaign.event.left");

            why = string.Empty;
            return true;
        }

        /// <summary>
        /// One body for every way out that is not the term running out.
        ///
        /// Three callers reach it and they differ in one word, which is exactly the shape that gets
        /// a fee wrong when it is written three times.
        /// </summary>
        private void LeaveCampaign(string reasonKey)
        {
            var campaign = State.Campaign;

            if (campaign == null)
            {
                return;
            }

            var left = Math.Max(0, campaign.Ends.DayIndex - State.Date.DayIndex);

            var fee = (long)(CampaignDailyCostUsd(campaign.Members.Count + 1) * left
                * ResearchCampaignCatalog.BreakFeeShare);

            State.Campaign = null;

            if (fee > 0)
            {
                State.PostCash(LedgerLine.Research, fee);
            }

            State.RaiseEvent(new CompanyEvent(CompanyEventType.CampaignLeft, State.Date,
                Loc.T(reasonKey, MemberNames(campaign.Members)), -fee));
        }

        /// <summary>What a day of the programme costs this company, at this many members.</summary>
        public static long CampaignDailyCostUsd(int members) =>
            (long)(ResearchCampaignCatalog.CostPerDayUsd
                * ResearchCampaignCatalog.CostShare(members));

        /// <summary>The labs in the room, as a sentence.</summary>
        private static string MemberNames(IReadOnlyList<CompetitorId> members)
        {
            var names = new string[members.Count];

            for (var index = 0; index < members.Count; index++)
            {
                names[index] = CompetitorCatalog.NameOf(members[index]);
            }

            return string.Join(Loc.T("gate.join"), names);
        }

        /// <summary>Everybody who could be asked into a campaign today.</summary>
        public List<CompetitorId> CampaignCandidates()
        {
            var found = new List<CompetitorId>();

            foreach (var pair in State.Alliances.Signed)
            {
                if (pair.Value >= ResearchCampaignCatalog.NeedsAllianceLevel)
                {
                    found.Add(pair.Key);
                }
            }

            return found;
        }

        /// <summary>The capability of one rival today, or zero when they have nothing on sale.</summary>
        /// <summary>What one rival is selling today, on the player's own scale. For the screens.</summary>
        public double RivalCapability(CompetitorId lab) => CapabilityOf(lab);

        /// <summary>What this company is selling, on the same scale. For the screens.</summary>
        public double OurCapabilityToday() => OurCapability();

        private double CapabilityOf(CompetitorId lab)
        {
            foreach (var entry in State.Rivals.LiveModels(State.Date))
            {
                if (entry.Competitor == lab)
                {
                    return entry.Capability;
                }
            }

            return 0.0;
        }

        /// <summary>What the company itself is selling, on the same scale.</summary>
        private double OurCapability() => Flagship()?.Capability ?? 0.0;

        /// <summary>Points as the phrase book wants them. `Simulation/` may not touch `UiFormat`.</summary>
        private static string UiPoints(int points) =>
            points.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The mixer for this system's own random stream.
        ///
        /// **Its own salt and its own call site.** A new mechanism that draws from an existing
        /// stream shifts every draw after it, and this project has a standing rule about that
        /// because it once rewrote fourteen years of a balance measurement by accident.
        /// </summary>
        private static uint RelationMix(uint seed, uint lab, uint day, uint salt)
        {
            unchecked
            {
                var value = seed ^ (lab * 2246822519u) ^ (day * 668265263u) ^ salt;
                value ^= value >> 15;
                value *= 2654435761u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;

                return value == 0 ? 0x85EBCA6Bu : value;
            }
        }
    }
}
