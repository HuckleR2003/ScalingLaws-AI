using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>What one round of bidding did.</summary>
    public readonly struct AuctionRound
    {
        public AuctionRound(int number, long price, int stillIn, CompetitorId? droppedOut)
        {
            Number = number;
            Price = price;
            StillIn = stillIn;
            DroppedOut = droppedOut;
        }

        public int Number { get; }

        /// <summary>What the stand costs at the end of this round.</summary>
        public long Price { get; }

        /// <summary>How many labs are still bidding.</summary>
        public int StillIn { get; }

        /// <summary>Who let it go in this round, when somebody did.</summary>
        public CompetitorId? DroppedOut { get; }
    }

    /// <summary>
    /// The last stand at an event, and what the player has to beat to take it.
    ///
    /// Reached only when the free stands are gone, which is what being late costs. A player who
    /// registered in the first weeks never sees this class.
    ///
    /// **Nobody here has any money.** A rival keeps bidding while the stand is still worth more to
    /// them than the price, where worth is read from how they are doing, and they tire. The
    /// argument for doing it that way rather than by giving labs a budget is in
    /// `Docs/EVENTS_PLAN.md` and it is the reason this feature does not break the no-economy rule.
    /// </summary>
    public static class EventAuction
    {
        private const uint SeedSalt = 0x41554354;

        /// <summary>Each round costs this share of the list price.</summary>
        public const double StepOfList = 0.12;

        /// <summary>
        /// How much of their remaining appetite a lab loses per round.
        ///
        /// **This is the brake that a budget would otherwise have been**, and it is the author's
        /// own framing: a longer auction tires the bidders and raises the chance they let it go.
        /// Without it a lab having a strong quarter would simply be unbeatable, which is a worse
        /// answer than an expensive one.
        /// </summary>
        public const double FatiguePerRound = 0.085;

        /// <summary>
        /// The most rounds anybody runs, so an auction cannot fail to end.
        ///
        /// A guard rather than a rule: fatigue takes every bidder out long before this, and a
        /// test holds that. A loop in the day tick with no ceiling is a hang nobody can diagnose.
        /// </summary>
        public const int MostRounds = 40;

        /// <summary>
        /// What a stand at this event is worth to a lab, as a multiple of the list price.
        ///
        /// Three readings, all of them already in the game: how well they are doing, how highly
        /// the address is thought of, and whether the room is full of the people they sell to.
        /// Nothing is invented and nothing is stored.
        /// </summary>
        public static double WorthToLab(LabAppetite lab, CityEventId id, AudienceSegment sells)
        {
            var definition = CityEventCatalog.Get(id);
            var venue = EventVenueCatalog.Get(definition.Venue);

            var fit = sells == AudienceSegment.None || sells == definition.Favours ? 1.0 : 0.62;

            // At full appetite, a perfect fit at the best address is worth about twice list. That
            // ceiling is what stops a bidding war running away from a player who is doing well.
            return Math.Clamp(0.55 + 1.45 * lab.Strength * (0.5 + 0.5 * venue.Prestige) * fit,
                0.0, 2.2);
        }

        /// <summary>
        /// Whether a lab goes one more round, at this price, this far in.
        ///
        /// **Tiredness is applied to the worth rather than to the price**, because a tired bidder
        /// is one who has stopped wanting it rather than one who has run out of money. A rival
        /// with nothing left in the tank walks away from a bargain, which is what people do.
        /// </summary>
        public static bool WillGoOn(LabAppetite lab, CityEventId id, AudienceSegment sells,
            long price, long listPrice, int round, uint seed)
        {
            if (listPrice <= 0L || round >= MostRounds)
            {
                return false;
            }

            var tired = Math.Max(0.0, 1.0 - FatiguePerRound * round);
            var ceiling = listPrice * WorthToLab(lab, id, sells) * tired;

            if (price >= ceiling)
            {
                return false;
            }

            // Close to the ceiling they waver rather than stopping dead, so the last few rounds
            // are not the same every time the player reloads into them.
            var headroom = ceiling <= 0.0 ? 0.0 : 1.0 - price / ceiling;

            if (headroom > 0.3)
            {
                return true;
            }

            var random = StreamFor(seed, id, lab.Lab, round);

            return random.NextChance(Math.Clamp(headroom / 0.3, 0.0, 1.0));
        }

        /// <summary>
        /// Run the room until nobody else will go on, and report what it took.
        ///
        /// The last price is what the player has to beat for the stand. Walking away at any round
        /// costs nothing, which is what makes the first bid safe to make.
        /// </summary>
        public static List<AuctionRound> Settle(IReadOnlyList<LabAppetite> bidders, CityEventId id,
            AudienceSegment theySell, long listPrice, uint seed)
        {
            var rounds = new List<AuctionRound>();

            if (bidders == null || bidders.Count == 0 || listPrice <= 0L)
            {
                return rounds;
            }

            var inTheRoom = new List<LabAppetite>(bidders);
            var price = listPrice;
            var step = Math.Max(1L, (long)Math.Round(listPrice * StepOfList));

            for (var round = 0; round < MostRounds && inTheRoom.Count > 0; round++)
            {
                var next = price + step;
                CompetitorId? left = null;

                for (var index = inTheRoom.Count - 1; index >= 0; index--)
                {
                    if (WillGoOn(inTheRoom[index], id, theySell, next, listPrice, round, seed))
                    {
                        continue;
                    }

                    left ??= inTheRoom[index].Lab;
                    inTheRoom.RemoveAt(index);
                }

                if (inTheRoom.Count == 0)
                {
                    // Nobody went on, so the price never actually reached `next`.
                    rounds.Add(new AuctionRound(round + 1, price, 0, left));
                    break;
                }

                price = next;
                rounds.Add(new AuctionRound(round + 1, price, inTheRoom.Count, left));
            }

            return rounds;
        }

        /// <summary>What the player has to pay to take the stand, after the room has run.</summary>
        public static long PriceToBeat(IReadOnlyList<AuctionRound> rounds, long listPrice)
        {
            if (rounds == null || rounds.Count == 0)
            {
                return listPrice;
            }

            return Math.Max(listPrice, rounds[^1].Price);
        }

        private static DeterministicRandom StreamFor(uint seed, CityEventId id, CompetitorId lab,
            int round)
        {
            unchecked
            {
                var mixed = seed ^ SeedSalt;
                mixed = mixed * 16777619u + (uint)((int)id * 131 + 7);
                mixed = mixed * 16777619u + (uint)((int)lab * 31 + 11);
                mixed = mixed * 16777619u + (uint)round;

                return new DeterministicRandom(mixed == 0u ? SeedSalt : mixed);
            }
        }
    }
}
