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

            for (var index = State.Deals.Count - 1; index >= 0; index--)
            {
                if (!State.Deals[index].IsLiveOn(State.Date))
                {
                    var ended = State.Deals[index];
                    State.Deals.RemoveAt(index);

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
                CapabilityOf(pending.Lab), OurCapability());

            // Its own stream, keyed on the day the offer went out and on who it went to, so adding
            // this mechanic cannot shift a single draw the balance suite depends on.
            var random = new DeterministicRandom(RelationMix(
                State.RosterSeed, (uint)pending.Lab, (uint)pending.Sent.DayIndex, 0xA111Eu));

            if (!random.NextChance(chance))
            {
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
            if (!HasDeal(RelationOffer.CapacityPurchase))
            {
                return 0.0;
            }

            return State.Pool.RentedPetaflops * RelationOfferCatalog.CapacityShare;
        }

        // ---- the alliance itself ------------------------------------------------------------

        /// <summary>
        /// Signs the next level with a lab, when the calendar and the band both allow it.
        ///
        /// **The fee is charged here and the clock is not for sale.** Level three needs a year at
        /// level two with nothing hostile in between, and there is no way to pay that down.
        /// </summary>
        public bool TrySignAlliance(CompetitorId lab, out string why)
        {
            var band = State.Relations.BandWith(lab);

            if (!State.Alliances.CanSignNext(lab, State.Date, band, out var next))
            {
                why = band < LabAlliances.HoldsAt
                    ? Loc.T("alliance.fail.band", RelationScale.NameOf(LabAlliances.HoldsAt))
                    : next > LabAlliances.TopLevel
                        ? Loc.T("alliance.fail.top")
                        : Loc.T("alliance.fail.days",
                            (LabAlliances.DaysNeededFor(next)
                                - State.Alliances.DaysAtLevel(lab, State.Date)).ToString());

                return false;
            }

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

        /// <summary>The capability of one rival today, or zero when they have nothing on sale.</summary>
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
