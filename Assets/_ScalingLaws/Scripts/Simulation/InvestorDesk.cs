using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The offers on the table, and the clock that puts them there.
    ///
    /// **Reported: there was one offer, from nobody, and it was the whole of investing.** A term
    /// sheet with no name on it cannot be compared with anything, so the only decision was whether
    /// to sign, which is not a decision. Several named offers standing at once is what makes it one:
    /// a firm that pays up wants more of the company, one that lowballs will wait months, and the
    /// player picks.
    ///
    /// **The arrivals are derived from the day, not remembered.** Whether somebody turns up on day
    /// 412 is a pure function of the campaign seed and the date, so a reload cannot re-roll it and
    /// nothing about the schedule has to be saved. The offers themselves are saved, because an offer
    /// that has been seen and not yet taken is a thing the player is holding in their head.
    /// </summary>
    public sealed class InvestorDesk
    {
        /// <summary>Nobody turns up sooner than this after the last one.</summary>
        public const int SoonestGapDays = 2;

        /// <summary>Nor later than this, so a quiet stretch is short.</summary>
        public const int LongestGapDays = 20;

        /// <summary>Past this the book is a list rather than a choice.</summary>
        public const int MostOpenOffers = 10;

        private readonly List<FundingOffer> open = new();

        public IReadOnlyList<FundingOffer> Open => open;

        public int Count => open.Count;

        public bool IsFull => open.Count >= MostOpenOffers;

        /// <summary>
        /// Whether somebody turns up today.
        ///
        /// Pure, so it can be tested without a campaign, and derived from the seed and the date so
        /// it replays identically. The gap is redrawn from the day of the last arrival, which is
        /// what makes the cadence two to twenty days rather than a coin flip every morning.
        /// </summary>
        public static bool SomebodyCallsOn(uint seed, GameDate today, int lastArrivalDayIndex)
        {
            if (today.DayIndex <= lastArrivalDayIndex)
            {
                return false;
            }

            var since = today.DayIndex - lastArrivalDayIndex;
            var random = new DeterministicRandom(Mix(seed, (uint)lastArrivalDayIndex));
            var gap = SoonestGapDays
                + (int)(random.NextDouble() * (LongestGapDays - SoonestGapDays + 1));

            return since >= Math.Clamp(gap, SoonestGapDays, LongestGapDays);
        }

        /// <summary>Which name it is, picked from whoever is not already offering.</summary>
        public InvestorId WhoCalls(uint seed, GameDate today)
        {
            var candidates = new List<InvestorId>();

            foreach (var definition in InvestorCatalog.Approachable)
            {
                if (!HasOfferFrom(definition.Id))
                {
                    candidates.Add(definition.Id);
                }
            }

            if (candidates.Count == 0)
            {
                return InvestorId.None;
            }

            var random = new DeterministicRandom(Mix(seed, (uint)today.DayIndex + 7717u));
            return candidates[(int)(random.NextDouble() * candidates.Count) % candidates.Count];
        }

        public bool HasOfferFrom(InvestorId investor)
        {
            foreach (var offer in open)
            {
                if (offer.Investor == investor)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Puts one on the table. Refused when the book is full or the name is already on it.</summary>
        public bool Add(FundingOffer offer)
        {
            if (!offer.IsOpen || IsFull || HasOfferFrom(offer.Investor))
            {
                return false;
            }

            open.Add(offer);
            return true;
        }

        /// <summary>Drops anything nobody took in time. Returns how many went.</summary>
        public int Withdraw(GameDate today)
        {
            var before = open.Count;
            open.RemoveAll(offer => offer.HasExpired(today));
            return before - open.Count;
        }

        public bool TryTake(InvestorId investor, out FundingOffer taken)
        {
            for (var index = 0; index < open.Count; index++)
            {
                if (open[index].Investor != investor)
                {
                    continue;
                }

                taken = open[index];
                open.RemoveAt(index);
                return true;
            }

            taken = default;
            return false;
        }

        /// <summary>
        /// Everything on the table goes when a round closes.
        ///
        /// A term sheet priced against a company that has just taken somebody else's money is priced
        /// against a company that no longer exists, and leaving it there would let a player stack
        /// three rounds in an afternoon at yesterday's valuation.
        /// </summary>
        public void ClearAll() => open.Clear();

        public void Restore(IEnumerable<FundingOffer> saved)
        {
            open.Clear();

            if (saved == null)
            {
                return;
            }

            foreach (var offer in saved)
            {
                if (offer.IsOpen && !HasOfferFrom(offer.Investor) && open.Count < MostOpenOffers)
                {
                    open.Add(offer);
                }
            }
        }

        private static uint Mix(uint seed, uint salt)
        {
            unchecked
            {
                var value = seed ^ (salt * 2654435761u);
                value ^= value >> 15;
                value *= 2246822519u;
                value ^= value >> 13;
                return value;
            }
        }

        public override string ToString() => $"{open.Count} offers on the table";
    }
}
