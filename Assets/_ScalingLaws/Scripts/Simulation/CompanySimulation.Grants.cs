using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Grants: money from outside with a condition attached.
    ///
    /// **The advance is repayable and that is the whole safeguard.** The spine at the top of
    /// `CLAUDE.md` says nothing may guarantee income or let capital skip a calendar gate, and a
    /// programme that simply handed over cash for work the company was doing anyway would be both.
    /// Accepting one is a bet: take the money now, meet the condition by a date, or give it back
    /// with a mark against the company's name.
    ///
    /// The sustained conditions are the interesting half, because they cost money to *hold* rather
    /// than to achieve. Keeping the free tier generous is revenue given away every day. Keeping the
    /// fleet under three quarters loaded is capacity nobody is paying for. The body is buying a say
    /// in how the company runs, which is what a grant actually is.
    /// </summary>
    public sealed partial class CompanySimulation
    {
        // ---- the daily tick -----------------------------------------------------------------------

        /// <summary>
        /// Ages every offer and every award, and settles anything that reached its end today.
        ///
        /// Called after the market has been served, because `utilisation` and the flagship's
        /// capability are figures the day produced and reading them earlier would measure
        /// yesterday.
        /// </summary>
        private void AdvanceGrants(double utilisation)
        {
            ExpireGrantOffers();
            OfferAGrant();
            SettleGrants(utilisation);
        }

        private void ExpireGrantOffers()
        {
            for (var index = State.GrantOffers.Count - 1; index >= 0; index--)
            {
                var offer = State.GrantOffers[index];
                offer.Advance();

                if (!offer.HasLapsed)
                {
                    continue;
                }

                State.GrantOffers.RemoveAt(index);
                State.GrantQuietUntil[offer.Id] =
                    State.Date.AddDays(GrantCatalog.QuietDaysAfterDeclining).DayIndex;
            }
        }

        /// <summary>
        /// Somebody with a budget gets in touch.
        ///
        /// One at a time, and never a programme the company is already holding, already looking at,
        /// or has recently turned down. A board that refilled itself the moment it emptied would be
        /// a task list rather than an opportunity.
        /// </summary>
        private void OfferAGrant()
        {
            if (State.IsBankrupt || State.GrantOffers.Count >= GrantCatalog.MostOpenOffers)
            {
                return;
            }

            // **Its own stream.** Drawing from the shared one would shift every later roll in the
            // campaign the moment a grant was considered, and the replay tests measure days years
            // apart. Same reasoning as the rivalry desk.
            var random = new DeterministicRandom(GrantMix(State.RosterSeed, (uint)State.Date.DayIndex));

            if (random.NextDouble() > GrantCatalog.ChancePerDay)
            {
                return;
            }

            // **Only what the company has climbed to.** A ministry funds a first safe model; an
            // international consortium does not write to a lab that has never finished anything.
            var available = GrantCatalog.OpenTo(State.GrantsCompleted)
                .Where(definition => !IsGrantSpokenFor(definition.Id))
                .ToList();

            if (available.Count == 0)
            {
                return;
            }

            var picked = available[random.NextInt(0, available.Count)];
            State.GrantOffers.Add(new GrantOffer(picked.Id, State.Date));

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.GrantOffered, State.Date,
                Loc.T("grant.event.offered", Loc.T(picked.NameKey), BodyOf(picked)),
                picked.AdvanceUsd));
        }

        private bool IsGrantSpokenFor(GrantId id)
        {
            if (State.Grants.Any(grant => grant.Id == id))
            {
                return true;
            }

            if (State.GrantOffers.Any(offer => offer.Id == id))
            {
                return true;
            }

            if (State.GrantsCompleted.Contains(id))
            {
                return true;
            }

            return State.GrantQuietUntil.TryGetValue(id, out var until)
                && State.Date.DayIndex < until;
        }

        /// <summary>
        /// Ages every award, breaks the sustained ones the day they are broken, and pays or
        /// reclaims at the closing date.
        /// </summary>
        private void SettleGrants(double utilisation)
        {
            if (State.Grants.Count == 0)
            {
                return;
            }

            var capability = Flagship()?.Capability ?? 0.0;

            for (var index = State.Grants.Count - 1; index >= 0; index--)
            {
                var grant = State.Grants[index];
                var definition = grant.Definition;

                grant.Advance();

                var reading = GrantConditions.Reading(
                    definition.Goal, State, capability, utilisation);

                var met = GrantConditions.IsMet(
                    definition.Goal, grant.Baseline, definition.Target, reading);

                // A sustained condition is lost on the day it breaks, not at the close. Telling the
                // player at once is the difference between a rule and an ambush.
                if (GrantCatalog.IsSustained(definition.Goal) && !met && !grant.IsBroken)
                {
                    grant.Break();

                    State.RaiseEvent(new CompanyEvent(
                        CompanyEventType.GrantLost, State.Date,
                        Loc.T("grant.event.broken", Loc.T(definition.NameKey))));
                }

                if (!grant.HasClosed)
                {
                    continue;
                }

                State.Grants.RemoveAt(index);

                if (met && !grant.IsBroken)
                {
                    AwardGrant(definition);
                }
                else
                {
                    ReclaimGrant(definition);
                }
            }
        }

        private void AwardGrant(GrantDefinition definition)
        {
            State.PostCash(LedgerLine.GrantAward, definition.CompletionUsd);
            State.ResearchPoints += definition.ResearchPoints;
            State.GrantsCompleted.Add(definition.Id);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.GrantCompleted, State.Date,
                Loc.T("grant.event.completed", Loc.T(definition.NameKey),
                    (int)Math.Round(definition.ResearchPoints)),
                definition.CompletionUsd));
        }

        /// <summary>
        /// The term was missed, so the advance goes back.
        ///
        /// **Charged whether or not the company can afford it**, which is the point: an advance
        /// that could be kept by simply running out of money would make failing a grant a way of
        /// borrowing at nothing. Insolvency is checked later in the same tick and will catch it.
        /// </summary>
        private void ReclaimGrant(GrantDefinition definition)
        {
            State.PostCash(LedgerLine.GrantRepaid, definition.AdvanceUsd);
            State.Reputation += GrantCatalog.ReputationCostOfFailing;

            State.GrantQuietUntil[definition.Id] =
                State.Date.AddDays(GrantCatalog.QuietDaysAfterDeclining).DayIndex;

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.GrantLost, State.Date,
                Loc.T("grant.event.reclaimed", Loc.T(definition.NameKey)),
                definition.AdvanceUsd));
        }

        // ---- what the player does -----------------------------------------------------------------

        /// <summary>Every award on the table today, newest last.</summary>
        public IReadOnlyList<GrantOffer> GrantOffers() => State.GrantOffers;

        /// <summary>Which rung of the grant ladder the company has earned its way onto.</summary>
        public int GrantTierReached() => GrantCatalog.ReachedTier(State.GrantsCompleted);

        /// <summary>
        /// Who is writing, with the player's own country in the letterhead where the programme is
        /// a national one.
        ///
        /// The country is passed to every body key rather than only the national ones, because
        /// `string.Format` ignores an argument a string has no placeholder for. One call site is
        /// worth more here than a second table saying which programmes are national.
        /// </summary>
        public string BodyOf(GrantDefinition definition) =>
            Loc.T(definition.BodyKey, State.Home.DisplayName);

        /// <summary>Every award the company is currently working off.</summary>
        public IReadOnlyList<Grant> HeldGrants() => State.Grants;

        /// <summary>
        /// Where the measured quantity stands today, so a screen can draw the progress rather than
        /// only the calendar.
        /// </summary>
        public double GrantReading(Grant grant) =>
            grant == null
                ? 0.0
                : GrantConditions.Reading(grant.Definition.Goal, State,
                    Flagship()?.Capability ?? 0.0, State.LastQuality.Utilisation);

        public bool CanAcceptGrant(out string why)
        {
            why = string.Empty;

            if (State.IsBankrupt)
            {
                why = Loc.T("grant.why.insolvent");
                return false;
            }

            if (State.Grants.Count >= GrantCatalog.MostHeldAtOnce)
            {
                why = Loc.T("grant.why.too_many", GrantCatalog.MostHeldAtOnce);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Signs for an award. Pays the advance and captures the baseline.
        ///
        /// The baseline is recorded here and nowhere else, because this is the only moment at which
        /// "where the company stood when it signed" is a fact rather than a reconstruction.
        /// </summary>
        public bool TryAcceptGrant(GrantId id, out string failureReason)
        {
            failureReason = string.Empty;

            if (!CanAcceptGrant(out failureReason))
            {
                return false;
            }

            var offer = State.GrantOffers.FirstOrDefault(entry => entry.Id == id);

            if (offer == null)
            {
                failureReason = Loc.T("grant.why.not_offered");
                return false;
            }

            if (!GrantCatalog.TryGet(id, out var definition))
            {
                failureReason = Loc.T("grant.why.not_offered");
                return false;
            }

            var baseline = GrantConditions.Reading(
                definition.Goal, State, Flagship()?.Capability ?? 0.0,
                State.LastQuality.Utilisation);

            State.GrantOffers.Remove(offer);
            State.Grants.Add(new Grant(id, State.Date, baseline));
            State.PostCash(LedgerLine.GrantAward, definition.AdvanceUsd);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.GrantAccepted, State.Date,
                Loc.T("grant.event.accepted", Loc.T(definition.NameKey), definition.TermDays),
                definition.AdvanceUsd));

            return true;
        }

        /// <summary>
        /// Turns one down and puts it away.
        ///
        /// Asked for by name: a board the player cannot clear is a board they stop reading. It
        /// comes back eventually rather than being deleted from the campaign, because content that
        /// disappears on one click is content most players never see twice.
        /// </summary>
        public bool TryDismissGrant(GrantId id)
        {
            var offer = State.GrantOffers.FirstOrDefault(entry => entry.Id == id);

            if (offer == null)
            {
                return false;
            }

            State.GrantOffers.Remove(offer);
            State.GrantQuietUntil[id] =
                State.Date.AddDays(GrantCatalog.QuietDaysAfterDeclining).DayIndex;

            return true;
        }

        /// <summary>
        /// One seed per day for the grant desk, mixed so it shares no sequence with anything else.
        ///
        /// The salt is what keeps this stream apart from the rivalry desk's, which uses the same
        /// avalanche on the same company seed.
        /// </summary>
        private static uint GrantMix(uint seed, uint day)
        {
            unchecked
            {
                var value = seed ^ (day * 2246822519u) ^ 0x6D2B79F5u;
                value ^= value >> 15;
                value *= 2654435761u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;

                return value == 0 ? 0x9E3779B9u : value;
            }
        }
    }
}
