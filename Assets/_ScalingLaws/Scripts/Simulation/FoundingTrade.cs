using System;
using System.Collections.Generic;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the founder arrived with: money, or things they already know how to do.
    ///
    /// **One decision with two readings, not two settings.** The player moves the cash the company
    /// opens on, and what they already know follows, because somebody who spent two years inside
    /// another lab turns up understanding things and with nothing in the bank. Two independent
    /// controls would let a player take both ends and there would be no decision left on the page.
    ///
    /// **It hands over nodes rather than research points, and that was decided by measurement.**
    /// The first draft traded cash for points. A sweep of the scripted campaign at five positions,
    /// three horizons and three seeds found it changed nothing at all: every opening converged to
    /// 62.5-63.6 capability by year four and none went bankrupt. Two reasons, both hard.
    ///
    /// The cash side drowns, because the first funding round lands inside six months and brings
    /// sixty five to a hundred million, against which eight million of opening capital is noise.
    /// And the points side was never scarce: `ShardedOptimizerStates` costs 2.6M, which is 289
    /// points, and a company starting with **zero** points had five nodes finished by day 180. A
    /// node needs `DurationDays` of calendar and `PetaflopDaysRequired` of cluster as well, and
    /// those are what a young company actually runs out of. Handing over points was a gift of the
    /// one currency nobody was short of.
    ///
    /// A finished node skips all three. That is why this is worth something.
    ///
    /// **It is knowledge the founder brought, never knowledge that was bought.** `ResearchBudget`
    /// holds the line that a company learns by building and that half a billion in the bank buys
    /// nothing without the understanding. Nothing here lets a rich company convert cash into
    /// progress during a campaign: this is an opening position, settled before the first day, and
    /// the money is gone rather than spent.
    /// </summary>
    public static class FoundingTrade
    {
        /// <summary>The untouched position: every dollar the game has always started with.</summary>
        public const long DefaultCashUsd = CompanyState.StartingCashUsd;

        /// <summary>
        /// What a founder can already do, in the order they learned it.
        ///
        /// **Authored rather than sorted, for the same reason `ScaleCeiling.Ladders` is.** Walking
        /// the catalogue cheapest first spends the whole budget on seven small nodes and never
        /// reaches the one that matters, and it also breaks prerequisite closure, because
        /// `FusedKernels` is cheaper than the `EfficientAttention` it needs.
        ///
        /// **The second rung is the point of the whole control.** `ShardedOptimizerStates` lifts
        /// the parameter ceiling from 31.6B to 178B, so a founder who gave up three million opens
        /// able to build something their neighbour cannot, on day one, visibly. Putting it last
        /// would mean the control only paid at the very bottom of the slider.
        ///
        /// Every entry is available on the start date, so nothing is granted before the world has
        /// it, and the list is prerequisite closed against what a new company already owns.
        ///
        /// **`ResearchTree.StartingNode` is deliberately absent.** It is free and every company
        /// already holds it, so leaving it on the ladder handed a node to a player who had given
        /// up nothing and the untouched position stopped being untouched. Caught by the neutral
        /// option test on the first run.
        /// </summary>
        public static readonly IReadOnlyList<ResearchNodeId> Ladder = new[]
        {
            ResearchNodeId.SinglePrecisionTraining,
            ResearchNodeId.ShardedOptimizerStates,
            ResearchNodeId.MixedPrecisionTraining,
            ResearchNodeId.SubwordTokenizer
        };

        /// <summary>What a rung costs in founding capital: the node's own price, never a second copy of it.</summary>
        public static long PriceOf(ResearchNodeId node) => ResearchTree.Get(node).CostUsd;

        /// <summary>The whole ladder, which is also how much cash the slider can move.</summary>
        public static long LadderTotalUsd
        {
            get
            {
                var total = 0L;
                foreach (var node in Ladder)
                {
                    total += PriceOf(node);
                }

                return total;
            }
        }

        /// <summary>
        /// The least a company may open on.
        ///
        /// **Derived from the ladder rather than chosen**, so the bottom of the slider buys exactly
        /// the whole ladder and not a dollar more. A floor picked separately leaves a stretch of
        /// slider where moving the handle changes nothing, which reads as a broken control.
        /// </summary>
        public static long LeastCashUsd => DefaultCashUsd - LadderTotalUsd;

        /// <summary>
        /// What a founder opening on <paramref name="cashUsd"/> already knows.
        ///
        /// Empty at the default, which is the neutral-option rule this project holds every new
        /// mechanism to: a player who never touches the control plays the game that was balanced
        /// before the control existed.
        /// </summary>
        public static List<ResearchNodeId> NodesFor(long cashUsd)
        {
            var cash = Math.Clamp(cashUsd, LeastCashUsd, DefaultCashUsd);
            var budget = DefaultCashUsd - cash;

            var known = new List<ResearchNodeId>();
            foreach (var node in Ladder)
            {
                var price = PriceOf(node);
                if (price > budget)
                {
                    break;
                }

                budget -= price;
                known.Add(node);
            }

            return known;
        }

        /// <summary>
        /// Opens a company on the chosen position.
        ///
        /// **The one place this happens.** The creator and the balance sweep both come through
        /// here, so the screen cannot promise a founder something the campaign does not hand over.
        /// That is the fault this project has shipped most often.
        /// </summary>
        public static void Open(CompanyState state, long cashUsd)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var cash = Math.Clamp(cashUsd, LeastCashUsd, DefaultCashUsd);

            state.CashUsd = cash;

            foreach (var node in NodesFor(cash))
            {
                state.UnlockedResearch.Add(node);
            }
        }
    }
}
