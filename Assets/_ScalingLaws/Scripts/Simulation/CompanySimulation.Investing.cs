using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Owning pieces of other companies, and eventually all of one.
    ///
    /// **The ladder is the design.** A few shares are a small monthly cheque and a reason to care
    /// what happens to a rival. Half of one is control. All of it is their following, their
    /// standing and their newest model, at a price that climbs the closer you get, so the last
    /// stretch is the expensive one.
    ///
    /// Money moves here and only here. The screen draws what comes back.
    /// </summary>
    public sealed partial class CompanySimulation
    {
        /// <summary>Dividends are paid on this day of the month, once.</summary>
        public const int DividendDayOfMonth = 1;

        // ---- reading the market -------------------------------------------------------------------

        /// <summary>
        /// What a lab's shares cost today.
        ///
        /// Anything the company has paid to do to them reaches the price through their standing,
        /// which is the only honest route: a smear that moved a share price directly would be a
        /// second mechanism for the same fact.
        /// </summary>
        public double SharePriceOf(CompetitorId lab) =>
            ShareMarket.PriceOn(lab, State.Date, RivalStandingMultiplier(lab));

        public long SharesHeldIn(CompetitorId lab) =>
            State.Shareholdings.TryGetValue(lab, out var held) ? held : 0L;

        public long SpentOnSharesIn(CompetitorId lab) =>
            State.ShareCostBasis.TryGetValue(lab, out var spent) ? spent : 0L;

        /// <summary>The fraction of a company the player holds, 0 to 1.</summary>
        public double OwnershipOf(CompetitorId lab)
        {
            var outstanding = ShareMarket.SharesOutstanding(lab);
            return outstanding <= 0 ? 0.0 : SharesHeldIn(lab) / (double)outstanding;
        }

        /// <summary>What the holding would fetch today, after the broker takes their cut.</summary>
        public long ValueOfHoldingIn(CompetitorId lab) =>
            ShareMarket.ProceedsOf(SharesHeldIn(lab), SharePriceOf(lab));

        /// <summary>
        /// How many shares are on offer, and how many the player could sell into the market.
        ///
        /// The float is capped so a company cannot be bought outright in one click on a Tuesday.
        /// Reaching control is meant to take a run of purchases the rival can watch happen.
        /// </summary>
        public long SharesAvailableIn(CompetitorId lab)
        {
            if (State.AcquiredLabs.Contains(lab))
            {
                return 0L;
            }

            var outstanding = ShareMarket.SharesOutstanding(lab);
            var tradable = (long)(outstanding * ShareMarket.TradableShare);

            return Math.Max(0L, tradable - SharesHeldIn(lab));
        }

        // ---- trading ------------------------------------------------------------------------------

        public bool TryBuyShares(CompetitorId lab, long shares, out long costUsd, out string why)
        {
            costUsd = 0L;
            why = string.Empty;

            if (shares <= 0)
            {
                why = Loc.T("invest.nothing");
                return false;
            }

            if (State.AcquiredLabs.Contains(lab))
            {
                why = Loc.T("invest.already_owned");
                return false;
            }

            if (shares > SharesAvailableIn(lab))
            {
                why = Loc.T("invest.not_for_sale");
                return false;
            }

            costUsd = ShareMarket.CostOf(shares, SharePriceOf(lab));

            if (State.CashUsd < costUsd)
            {
                why = Loc.T("invest.no_cash");
                return false;
            }

            State.PostCash(LedgerLine.Investment, costUsd);

            State.Shareholdings[lab] = SharesHeldIn(lab) + shares;
            State.ShareCostBasis[lab] = SpentOnSharesIn(lab) + costUsd;

            return true;
        }

        public bool TrySellShares(CompetitorId lab, long shares, out long proceedsUsd, out string why)
        {
            proceedsUsd = 0L;
            why = string.Empty;

            var held = SharesHeldIn(lab);

            if (shares <= 0 || held <= 0)
            {
                why = Loc.T("invest.nothing");
                return false;
            }

            var sold = Math.Min(shares, held);
            proceedsUsd = ShareMarket.ProceedsOf(sold, SharePriceOf(lab));

            State.CashUsd += proceedsUsd;

            var left = held - sold;

            // The basis is reduced in proportion, so what is left still says what it cost. Clearing
            // it on a partial sale would make the next reading claim the remainder was free.
            var basis = SpentOnSharesIn(lab);
            var keptShare = held > 0 ? left / (double)held : 0.0;

            if (left <= 0)
            {
                State.Shareholdings.Remove(lab);
                State.ShareCostBasis.Remove(lab);
            }
            else
            {
                State.Shareholdings[lab] = left;
                State.ShareCostBasis[lab] = (long)(basis * keptShare);
            }

            return true;
        }

        /// <summary>
        /// The monthly cheque, paid on the first.
        ///
        /// A lab whose standing has collapsed pays nothing, which is the whole reason holding one
        /// is a position rather than a bond: the dividend stops exactly when the share price is
        /// falling and selling is dearest.
        /// </summary>
        private void PayShareDividends()
        {
            if (State.Date.Day != DividendDayOfMonth || State.Shareholdings.Count == 0)
            {
                return;
            }

            var total = 0L;

            foreach (var pair in State.Shareholdings)
            {
                var lab = pair.Key;
                var price = SharePriceOf(lab);

                if (price <= 0.0)
                {
                    continue;
                }

                var brand = 0.0;

                foreach (var model in State.Rivals.LiveModels(State.Date))
                {
                    if (model.Competitor == lab && model.BrandStrength > brand)
                    {
                        brand = model.BrandStrength;
                    }
                }

                if (brand < ShareMarket.PaysDividendAboveBrand)
                {
                    continue;
                }

                total += (long)(pair.Value * price * ShareMarket.MonthlyYield);
            }

            if (total <= 0L)
            {
                return;
            }

            State.CashUsd += total;

            State.RaiseEvent(new CompanyEvent(CompanyEventType.DividendPaid, State.Date,
                Loc.T("invest.event.dividend"), total));
        }

        // ---- buying the whole thing ---------------------------------------------------------------

        /// <summary>
        /// Whether the rest of a company can be bought, and what it would cost.
        ///
        /// Control first. Buying a company you hold none of is a different transaction, negotiated
        /// rather than accumulated, and this game already has that in the other direction.
        /// </summary>
        public bool CanTakeOver(CompetitorId lab, out long costUsd, out string why)
        {
            costUsd = 0L;
            why = string.Empty;

            if (State.AcquiredLabs.Contains(lab))
            {
                why = Loc.T("invest.already_owned");
                return false;
            }

            if (OwnershipOf(lab) < ShareMarket.ControlThreshold)
            {
                why = Loc.T("invest.need_control",
                    UiFormatPercent(ShareMarket.ControlThreshold));

                return false;
            }

            costUsd = ShareMarket.BuyoutCostUsd(lab, SharesHeldIn(lab), SharePriceOf(lab));
            return true;
        }

        /// <summary>
        /// Buy the rest, and take what survives the purchase.
        ///
        /// **Their people came for them, not for you.** `ShareMarket.TransferShare` of the
        /// following and the standing crosses over and the rest evaporates, which is what stops
        /// buying a rival from being strictly better than out-building one. Their newest model
        /// joins the fleet, because a model is an asset that does not care whose logo is on it.
        /// </summary>
        public bool TryTakeOver(CompetitorId lab, out string why)
        {
            if (!CanTakeOver(lab, out var cost, out why))
            {
                return false;
            }

            if (State.CashUsd < cost)
            {
                why = Loc.T("invest.no_cash");
                return false;
            }

            State.PostCash(LedgerLine.Investment, cost);

            State.Shareholdings[lab] = ShareMarket.SharesOutstanding(lab);
            State.ShareCostBasis[lab] = SpentOnSharesIn(lab) + cost;
            State.AcquiredLabs.Add(lab);

            AbsorbFollowing(lab);
            AbsorbNewestModel(lab);

            var them = CompetitorCatalog.NameOf(lab);

            State.Relations.Record(lab, State.Date, -30.0, "relation.reason.bought_them", them);

            State.RaiseEvent(new CompanyEvent(CompanyEventType.LabAcquired, State.Date,
                Loc.T("invest.event.bought", them), -cost));

            return true;
        }

        /// <summary>
        /// Their fans and their reputation, at most three quarters of each.
        ///
        /// Reputation is an opinion and fans are a stock, so they move differently: the fans are
        /// added to the company's own, and the reputation is blended toward theirs rather than
        /// summed, because two companies with a good name do not make one company with twice as
        /// good a name.
        /// </summary>
        private void AbsorbFollowing(CompetitorId lab)
        {
            var brand = 0.0;

            foreach (var model in State.Rivals.LiveModels(State.Date))
            {
                if (model.Competitor == lab && model.BrandStrength > brand)
                {
                    brand = model.BrandStrength;
                }
            }

            // Their following is not tracked as a headcount anywhere, so it is read off the one
            // figure that does describe how many people care about them, on the same scale the
            // player's own fans use.
            // Measured against the company's own live headcount, because that is the only user
            // figure this side of the market that is a real number of people. A lab with a strong
            // brand is worth roughly a third of your own audience again in followers.
            var theirFans = Math.Max(0.0, brand) * State.Users.Latest * 0.35;

            State.Fans += theirFans * ShareMarket.TransferShare;

            var theirReputation = Math.Clamp(brand, 0.0, 1.0);
            var moved = (theirReputation - State.Reputation) * ShareMarket.TransferShare;

            if (moved > 0.0)
            {
                State.Reputation = Math.Clamp(State.Reputation + moved, 0.0, 1.0);
            }
        }

        /// <summary>Their newest model joins the fleet at the capability it shipped with.</summary>
        private void AbsorbNewestModel(CompetitorId lab)
        {
            RivalModel newest = default;
            var found = false;

            foreach (var model in State.Rivals.LiveModels(State.Date))
            {
                if (model.Competitor != lab)
                {
                    continue;
                }

                if (!found || model.ReleaseDate.DayIndex > newest.ReleaseDate.DayIndex)
                {
                    newest = model;
                    found = true;
                }
            }

            if (!found)
            {
                return;
            }

            // Rebuilt as one of ours rather than referenced. A rival's model carries no
            // training shape, no safety tiers and no line, because none of that was ever
            // simulated for them, so it arrives at the capability it shipped with and nothing
            // more. Claiming a shape it never had would be inventing a spec.
            State.AddDeployedModel(new DeployedModel(
                newest.DisplayName,
                ArchitectureId.DenseTransformer,
                newest.Capability,
                State.Date,
                20_000_000_000.0,
                newest.PriceMultiplier,
                newest.Type,
                newest.DisplayName));
        }

        private static string UiFormatPercent(double fraction) =>
            (fraction * 100.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
