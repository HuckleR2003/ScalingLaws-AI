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
        /// Ages every award and settles anything that reached its end today.
        ///
        /// Called after the market has been served, because `utilisation` and the flagship's
        /// capability are figures the day produced and reading them earlier would measure
        /// yesterday.
        /// </summary>
        private void AdvanceGrants(double utilisation)
        {
            SettleGrants(utilisation);
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

        /// <summary>
        /// Every programme the company could apply to today.
        ///
        /// **A board, not an inbox.** This was a one per cent daily roll that put the first offer a
        /// hundred days out on average, so most players spent their opening months looking at an
        /// empty panel that said nobody was funding anything. A grant register is a list you read
        /// and apply to, and the rung you have climbed to is what decides how long the list is.
        /// </summary>
        public List<GrantDefinition> AvailableGrants()
        {
            var open = new List<GrantDefinition>();

            foreach (var definition in GrantCatalog.OpenTo(State.GrantsCompleted))
            {
                if (IsGrantSpokenFor(definition.Id))
                {
                    continue;
                }

                open.Add(definition);
            }

            return open;
        }

        private bool IsGrantSpokenFor(GrantId id)
        {
            if (State.Grants.Any(grant => grant.Id == id))
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

        /// <summary>Every award the company is currently working off.</summary>
        public IReadOnlyList<Grant> HeldGrants() => State.Grants;

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
        /// Applies for a programme. Pays the advance and captures the baseline.
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

            if (!GrantCatalog.TryGet(id, out var definition) || IsGrantSpokenFor(id))
            {
                failureReason = Loc.T("grant.why.not_offered");
                return false;
            }

            if (definition.Tier > GrantTierReached())
            {
                failureReason = Loc.T("grant.why.not_offered");
                return false;
            }

            var baseline = GrantConditions.Reading(
                definition.Goal, State, Flagship()?.Capability ?? 0.0,
                State.LastQuality.Utilisation);

            State.Grants.Add(new Grant(id, State.Date, baseline));
            State.PostCash(LedgerLine.GrantAward, definition.AdvanceUsd);

            State.RaiseEvent(new CompanyEvent(
                CompanyEventType.GrantAccepted, State.Date,
                Loc.T("grant.event.accepted", Loc.T(definition.NameKey), definition.TermDays),
                definition.AdvanceUsd));

            return true;
        }

        /// <summary>
        /// Takes one off the board for a while.
        ///
        /// Asked for by name: a board the player cannot clear is a board they stop reading. It
        /// comes back rather than being deleted from the campaign, because content that disappears
        /// on one click is content most players never see twice.
        /// </summary>
        public bool TryDismissGrant(GrantId id)
        {
            if (!GrantCatalog.TryGet(id, out _) || IsGrantSpokenFor(id))
            {
                return false;
            }

            State.GrantQuietUntil[id] =
                State.Date.AddDays(GrantCatalog.QuietDaysAfterDeclining).DayIndex;

            return true;
        }
    }
}
