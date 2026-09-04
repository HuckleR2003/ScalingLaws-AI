using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The state programme: signing it, running sectors in it, and the day it either pays or does
    /// not.
    ///
    /// Its own file for the reason `CompanySimulation.Rivalry.cs` is: this is a whole ending, it is
    /// the largest single block of rules added to the game since the market, and it has no business
    /// making the main file longer. `partial` is a file boundary and nothing else - the compiler
    /// builds the same type, no field changes lifetime, nothing is renamed.
    /// </summary>
    public sealed partial class CompanySimulation
    {
        /// <summary>How much of the failure risk continuous oversight takes off.</summary>
        public const double OversightRiskCut = 0.45;

        /// <summary>
        /// How much of a shortfall redundant inference absorbs.
        ///
        /// It does not add capacity. It makes a programme that is short degrade rather than break,
        /// which is a different and cheaper thing: the fee is still cut, the risk climbs more slowly.
        /// </summary>
        public const double RedundancyAbsorbs = 0.40;

        /// <summary>
        /// What one failure costs beyond the sector's own price.
        ///
        /// Reputation, in a single step. Not a backlash that decays - a state-scale failure is the
        /// kind of thing that is still the first result about the company years later.
        /// </summary>
        public const double FailureReputationLoss = 0.22;

        // ---- can it be signed --------------------------------------------------------------------

        /// <summary>
        /// Whether a government would talk to this company today, and why not.
        ///
        /// **One body, so the screen and the operation never disagree.** The basement shipped with
        /// two, the button was enabled on cash alone while the operation also checked the tier, and
        /// the refusal was thrown away: the author clicked it and nothing happened at all.
        /// </summary>
        public bool CanSignStateProgramme(out string reason)
        {
            reason = string.Empty;

            if (State.Programme.IsSigned)
            {
                reason = Loc.T("state.already_signed");
                return false;
            }

            if (!State.HasResearch(ResearchNodeId.SovereignLiaison))
            {
                reason = Loc.T("state.needs_liaison",
                    ResearchTree.Get(ResearchNodeId.SovereignLiaison).DisplayName);
                return false;
            }

            var record = SafetyRecord.For(State, State.Date);

            if (record < SafetyRecord.ContractThreshold)
            {
                reason = Loc.T("state.needs_record",
                    UiPercent(record), UiPercent(SafetyRecord.ContractThreshold));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Signs. One way, and it is the second ending this game has.
        ///
        /// The signatory is the company's own country, because that is who has been auditing them
        /// for five years. Picking a country would be a diplomacy system, and this game does not
        /// have one and should not grow one to support a signature.
        /// </summary>
        public bool TrySignStateProgramme(out string why)
        {
            if (!CanSignStateProgramme(out why))
            {
                return false;
            }

            if (!State.Programme.Sign(State.HomeCountry, State.Date))
            {
                why = Loc.T("state.already_signed");
                return false;
            }

            State.RaiseEvent(new CompanyEvent(CompanyEventType.StateProgrammeSigned, State.Date,
                Loc.T("state.signed.story", WorldRegionCatalog.Get(State.HomeCountry).DisplayName)));

            return true;
        }

        // ---- sectors ------------------------------------------------------------------------------

        /// <summary>Whether this sector could be started today, and why not.</summary>
        public bool CanStartSector(StateSector sector, out string reason)
        {
            reason = string.Empty;

            var definition = StateSectorCatalog.Get(sector);

            if (!State.Programme.IsSigned)
            {
                reason = Loc.T("state.not_signed");
                return false;
            }

            if (State.Programme.IsRunning(sector))
            {
                reason = Loc.T("state.sector_running");
                return false;
            }

            foreach (var needed in definition.Requires)
            {
                if (!State.Programme.IsRunning(needed))
                {
                    reason = Loc.T("state.needs_sector",
                        StateSectorCatalog.Get(needed).DisplayName);
                    return false;
                }
            }

            if (State.ResearchPoints < definition.ResearchPoints)
            {
                reason = Loc.T("state.needs_points",
                    UiWhole(definition.ResearchPoints), UiWhole(State.ResearchPoints));
                return false;
            }

            if (State.CashUsd < definition.ResearchCashUsd)
            {
                reason = Loc.T("state.needs_cash", UiMoney(definition.ResearchCashUsd));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Teaches the models a sector and puts them in charge of it.
        ///
        /// **Charged in points and cash, the same two currencies the research tree uses**, because
        /// this is research: the money is small next to the points and half a billion in the bank
        /// buys none of it without the understanding. There is deliberately no calendar here - the
        /// calendar was the whole of era five and it has already been paid.
        /// </summary>
        public bool TryStartSector(StateSector sector, out string why)
        {
            if (!CanStartSector(sector, out why))
            {
                return false;
            }

            var definition = StateSectorCatalog.Get(sector);

            State.ResearchPoints -= definition.ResearchPoints;
            State.PostCash(LedgerLine.Research, definition.ResearchCashUsd);

            if (!State.Programme.Start(sector))
            {
                why = Loc.T("state.sector_running");
                return false;
            }

            State.RaiseEvent(new CompanyEvent(CompanyEventType.StateSectorStarted, State.Date,
                Loc.T("state.sector.story", definition.DisplayName,
                    UiMoney(definition.FeeUsdPerDay))));

            return true;
        }

        /// <summary>
        /// Hands a sector back.
        ///
        /// **Allowed, and it refunds nothing.** A company drowning in a programme it cannot serve
        /// needs a way out that is not bankruptcy, and this is it: the fee stops, the capacity comes
        /// back, and everything paid to learn the sector is gone. Anything else would make the board
        /// a set of switches to flip rather than a set of commitments.
        /// </summary>
        public bool TryStopSector(StateSector sector, out string why)
        {
            why = string.Empty;

            if (!State.Programme.IsRunning(sector))
            {
                why = Loc.T("state.sector_not_running");
                return false;
            }

            // Anything standing on it goes too, or the chain in the catalog stops meaning anything
            // and a player can run Defence with no Security under it.
            foreach (var definition in StateSectorCatalog.All)
            {
                if (!State.Programme.IsRunning(definition.Sector))
                {
                    continue;
                }

                foreach (var needed in definition.Requires)
                {
                    if (needed == sector)
                    {
                        why = Loc.T("state.sector_holds_up", definition.DisplayName);
                        return false;
                    }
                }
            }

            State.Programme.Stop(sector);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.StateSectorStopped, State.Date,
                Loc.T("state.handed_back.story", StateSectorCatalog.Get(sector).DisplayName)));

            return true;
        }

        // ---- the day ------------------------------------------------------------------------------

        /// <summary>
        /// What the programme holds today, in petaflops.
        ///
        /// Read before the market is served, because this comes off the top. Public so the screen
        /// can show the same number the day uses.
        /// </summary>
        public double StateReservedPetaflops() => State.Programme.PetaflopsRequired;

        /// <summary>
        /// How much of what was promised the company can actually deliver, from 0 to 1.
        ///
        /// **Redundant inference absorbs part of a shortfall rather than removing it.** A programme
        /// at eighty per cent delivers eighty-eight with the node and eighty without, which is worth
        /// having and is never worth skipping capacity for.
        /// </summary>
        public double StateDelivery(ComputeProfile profile)
        {
            var required = State.Programme.PetaflopsRequired;

            if (required <= 0.0)
            {
                return 1.0;
            }

            var raw = Math.Clamp(profile.EffectivePetaflops / required, 0.0, 1.0);

            if (!State.HasResearch(ResearchNodeId.RedundantInference))
            {
                return raw;
            }

            return Math.Clamp(raw + (1.0 - raw) * RedundancyAbsorbs, 0.0, 1.0);
        }

        /// <summary>
        /// Today's chance of a national-scale failure.
        ///
        /// One method, so the figure the screen prints is the figure the roll uses. The last time
        /// this project had two of these it was telling the player a number that governed nothing.
        /// </summary>
        public double StateFailureRisk(double delivery)
        {
            var oversight = State.HasResearch(ResearchNodeId.ContinuousOversight)
                ? 1.0 - OversightRiskCut
                : 1.0;

            return State.Programme.DailyFailureRisk(
                delivery,
                SafetyRecord.For(State, State.Date),
                // The same two multipliers the model-level incident roll uses, so a company that
                // invested in safety people is safer at national scale for the same reason it is
                // safer at model scale. Two different answers to "how safe is this company" would
                // be two places for the number to live.
                State.Staff.IncidentRiskMultiplier() * State.Skills.IncidentRiskMultiplier()
                * oversight);
        }

        /// <summary>
        /// The programme's day: it gets paid for what it delivered, and it might go wrong.
        ///
        /// Called from the daily tick after the market has been served, so the delivery figure is
        /// measured against the fleet the day actually had.
        /// </summary>
        private void AdvanceStateProgramme(ComputeProfile profile)
        {
            var programme = State.Programme;

            if (!programme.IsSigned)
            {
                return;
            }

            var delivery = StateDelivery(profile);

            programme.RecordDelivery(delivery);

            var earned = programme.EarnedUsdPerDay(delivery);

            if (earned > 0L)
            {
                State.PostCash(LedgerLine.StateProgramme, earned);
                State.LifetimeRevenueUsd += earned;
                State.RecordDailyRevenue(earned);
            }

            // The programme's own power, on the same bill the fleet pays. Not a new line: the
            // company is drawing it through the same meter and a second electricity row would be a
            // second place the total lives.
            var megawatts = programme.MegawattsRequired;

            if (megawatts > 0.0)
            {
                var kilowattHours = megawatts * 1000.0 * 24.0;
                var powerUsd = (long)Math.Round(kilowattHours * StatePowerTariffUsd);

                State.PostCash(LedgerLine.Electricity, powerUsd);
                State.LifetimeOperatingCostUsd += powerUsd;
            }

            if (programme.Running.Count == 0 || !programme.CouldFailOn(State.Date))
            {
                return;
            }

            var risk = StateFailureRisk(delivery);

            if (State.Random.NextDouble() >= risk)
            {
                return;
            }

            LandStateFailure(programme.SectorForRoll(
                State.Random.NextDouble() * programme.FailureWeight));
        }

        /// <summary>
        /// A rate a state programme pays for power.
        ///
        /// The industrial contract rate rather than the domestic one the basement pays: a facility
        /// running a country's logistics is not on a household tariff. Same units as everything
        /// else in the fleet bill, dollars per kilowatt hour.
        /// </summary>
        public const double StatePowerTariffUsd = 0.058;

        /// <summary>
        /// Something went wrong at national scale.
        ///
        /// **One body, so every route to a failure does the same thing**, which is the rule
        /// `LandPenalty` already follows for regulatory action. The bill is the sector's own price,
        /// which is the number the board printed next to it before the player agreed.
        /// </summary>
        private void LandStateFailure(StateSector sector)
        {
            if (sector == StateSector.None)
            {
                return;
            }

            var definition = StateSectorCatalog.Get(sector);
            var cost = definition.FailureCostUsd;

            State.Programme.RecordFailure(State.Date, cost);
            State.PostCash(LedgerLine.Fines, cost);

            // Straight off the opinion, in one step. Not a decaying backlash: a country's
            // hospitals going down is still the first thing anybody finds about the company years
            // later, and modelling it as a wave that passes would be the wrong story.
            State.Reputation -= FailureReputationLoss;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.StateFailure, State.Date,
                Loc.T("state.failure.story", definition.DisplayName, UiMoney(cost))));
        }

        // ---- formatting, kept out of the UI layer's way ---------------------------------------------
        //
        // `Simulation/` may not import UnityEngine and the interface's own formatter lives in
        // `UI/`, so these are here. `UiMoney` was already in the main file and is reused rather
        // than repeated. Invariant culture on purpose: a raw format string follows the machine's
        // culture and this project has shipped `$20,00` on a Polish machine four separate times.

        private static string UiWhole(double value) =>
            value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        private static string UiPercent(double share) =>
            (share * 100.0).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
