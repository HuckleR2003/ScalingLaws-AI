using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One product competing for users, whoever built it.</summary>
    public readonly struct MarketEntrant
    {
        public MarketEntrant(int ownerIndex, string displayName, ModelType type, double capability,
            double brand, double priceMultiplier, double ageYears, double servingBurden)
        {
            OwnerIndex = ownerIndex;
            DisplayName = displayName ?? string.Empty;
            Type = type == ModelType.None ? ModelType.General : type;
            Capability = Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0);
            Brand = Math.Clamp(SimUnits.Finite(brand), 0.0, 1.0);
            PriceMultiplier = Math.Clamp(SimUnits.Finite(priceMultiplier, 1.0), 0.02, 20.0);
            AgeYears = Math.Max(0.0, SimUnits.Finite(ageYears));
            ServingBurden = Math.Clamp(SimUnits.Finite(servingBurden, 1.0), 0.1, 6.0);
        }

        /// <summary>-1 for the player, otherwise the index of the rival lab.</summary>
        public int OwnerIndex { get; }

        public string DisplayName { get; }
        public ModelType Type { get; }
        public double Capability { get; }
        public double Brand { get; }
        public double PriceMultiplier { get; }
        public double AgeYears { get; }

        /// <summary>Cost to produce a token relative to a plain dense model. Above one is expensive.</summary>
        public double ServingBurden { get; }

        public bool IsPlayer => OwnerIndex < 0;

        public override string ToString() => $"{DisplayName} ({Type}, cap {Capability:0.0})";
    }

    /// <summary>What one segment looks like today. Immutable, for the UI and for tests.</summary>
    public readonly struct SegmentStanding
    {
        public SegmentStanding(AudienceSegment segment, double totalTokensPerDay, double playerShare,
            double[] rivalShares, int leaderIndex, string leaderName)
        {
            Segment = segment;
            TotalTokensPerDay = Math.Max(0.0, SimUnits.Finite(totalTokensPerDay));
            PlayerShare = Math.Clamp(SimUnits.Finite(playerShare), 0.0, 1.0);
            RivalShares = rivalShares ?? Array.Empty<double>();
            LeaderIndex = leaderIndex;
            LeaderName = leaderName ?? string.Empty;
        }

        public AudienceSegment Segment { get; }
        public double TotalTokensPerDay { get; }
        public double PlayerShare { get; }
        public IReadOnlyList<double> RivalShares { get; }

        /// <summary>-1 when the player leads this segment, otherwise the rival index.</summary>
        public int LeaderIndex { get; }

        public string LeaderName { get; }

        public double PlayerTokensPerDay => TotalTokensPerDay * PlayerShare;
    }

    /// <summary>
    /// Who is actually using each product, per audience segment, and how that moves over time.
    ///
    /// This exists because the share model it sits on top of is a pure function of today: ship a
    /// better model on Tuesday and you owned the market on Tuesday. There was no user base, no
    /// switching cost and no momentum, which meant no segment could behave differently from any
    /// other segment and a niche was impossible to hold.
    ///
    /// What this adds is exactly one idea: **a target and a speed**. Attractiveness is still scored
    /// by <see cref="MarketShareModel.Utility"/>, the same function, for the player and for every
    /// rival, with no second quality formula anywhere. The total pool is still
    /// <see cref="MarketModel.DemandOn"/>. What is new is that the standing moves toward the target
    /// a little each day instead of being the target.
    ///
    /// Segments differ only in how fast they move and what they care about, and that is enough to
    /// make a coding model behave differently from a consumer model without a single special case.
    /// </summary>
    public sealed class SegmentMarket
    {
        /// <summary>Share a product keeps purely for already having it, per day.</summary>
        public const double IncumbentFloor = 0.02;

        private readonly Dictionary<AudienceSegment, double> playerShares = new();
        private readonly Dictionary<AudienceSegment, double[]> rivalShares = new();
        private int rivalCount;

        /// <summary>
        /// False for a standing that was never recorded, which today means a save written before
        /// this system existed. The first tick after that snaps straight to the target instead of
        /// easing toward it.
        ///
        /// Easing would be wrong, not merely slow: a company that had held a market for three years
        /// would come back from a save with no users and have to earn them again, which is a
        /// punishment for saving. A market with no recorded history has no inertia to respect.
        /// </summary>
        private bool seeded = true;

        /// <summary>
        /// Where the player share is heading, per segment, as of the last tick. This is the target
        /// the standing is easing toward, so the difference between it and the current share is
        /// exactly how much momentum the player still has to gain or lose.
        /// </summary>
        public double[] LastTargets { get; } = new double[AudienceCatalog.All.Count];

        /// <summary>How many products were competing on the last tick. Player models plus rivals.</summary>
        public int LastEntrantCount { get; private set; }

        public SegmentMarket(int rivals)
        {
            rivalCount = Math.Max(0, rivals);
            foreach (var segment in AudienceCatalog.All)
            {
                playerShares[segment.Segment] = 0.0;
                rivalShares[segment.Segment] = new double[rivalCount];
            }
        }

        public int RivalCount => rivalCount;

        public double PlayerShareIn(AudienceSegment segment) =>
            playerShares.TryGetValue(segment, out var value) ? value : 0.0;

        public double RivalShareIn(AudienceSegment segment, int rivalIndex)
        {
            if (!rivalShares.TryGetValue(segment, out var shares))
            {
                return 0.0;
            }

            return rivalIndex >= 0 && rivalIndex < shares.Length ? shares[rivalIndex] : 0.0;
        }

        /// <summary>
        /// Attractiveness of one product to one segment.
        ///
        /// The base is the same utility every part of this game has always used. The segment then
        /// applies its own weighting: what it pays for capability, how much a price rise costs it,
        /// how much it cares who made it, and how well the product's type suits it at all.
        ///
        /// Nothing here is a second quality formula. It is the same score, read by a buyer with
        /// opinions.
        /// </summary>
        public static double Attractiveness(in MarketEntrant entrant, AudienceSegmentDefinition segment,
            GameDate date)
        {
            var fit = ModelTypeCatalog.Get(entrant.Type).AffinityFor(segment.Segment);
            if (fit <= 0.0)
            {
                return 0.0;
            }

            // A segment that pays well feels a price rise less. Same idea as the tolerance factor,
            // applied per segment instead of averaged into one number.
            var felt = entrant.PriceMultiplier / Math.Max(0.25, segment.WillingnessToPay);

            var utility = MarketShareModel.Utility(
                entrant.Capability,
                entrant.Brand * segment.BrandWeight,
                felt,
                entrant.AgeYears);

            // Cost sensitive segments punish an expensive model to serve, because that cost has to
            // come out of the price eventually and they are the ones who notice.
            var burden = 1.0 + (entrant.ServingBurden - 1.0) * segment.ServingCostWeight;

            return Math.Exp(utility) * fit / Math.Max(0.2, burden);
        }

        /// <summary>
        /// Moves every segment one day toward what its buyers would prefer today.
        ///
        /// Returns the player's share of the whole market, which is what the rest of the simulation
        /// already consumes, so nothing downstream had to change to gain a segmented market.
        /// </summary>
        public double Advance(IReadOnlyList<MarketEntrant> entrants, GameDate date, double totalTokensPerDay)
        {
            if (entrants == null || entrants.Count == 0)
            {
                Decay();
                return 0.0;
            }

            var segments = AudienceCatalog.All;
            var shares = AudienceCatalog.SharesOn(date);

            var playerTokens = 0.0;
            var total = Math.Max(0.0, totalTokensPerDay);
            LastEntrantCount = entrants.Count;

            for (var index = 0; index < segments.Count; index++)
            {
                var definition = segments[index];
                var pool = total * shares[index];

                var targetPlayer = 0.0;
                var targetRivals = new double[rivalCount];
                var sum = 0.0;

                for (var entry = 0; entry < entrants.Count; entry++)
                {
                    var score = Attractiveness(entrants[entry], definition, date);
                    if (score <= 0.0)
                    {
                        continue;
                    }

                    sum += score;
                    if (entrants[entry].IsPlayer)
                    {
                        targetPlayer += score;
                    }
                    else if (entrants[entry].OwnerIndex < rivalCount)
                    {
                        targetRivals[entrants[entry].OwnerIndex] += score;
                    }
                }

                if (sum <= 0.0)
                {
                    continue;
                }

                targetPlayer /= sum;
                for (var rival = 0; rival < targetRivals.Length; rival++)
                {
                    targetRivals[rival] /= sum;
                }

                // The whole point. Users move a fraction of the way, and the fraction is the
                // segment's own. Developers reprice their loyalty in weeks; an enterprise contract
                // does not care what shipped on Tuesday.
                LastTargets[index] = targetPlayer;
                var speed = seeded ? Math.Clamp(definition.AdoptionRatePerDay, 0.002, 0.5) : 1.0;

                var current = playerShares[definition.Segment];
                playerShares[definition.Segment] = current + (targetPlayer - current) * speed;

                var live = rivalShares[definition.Segment];
                for (var rival = 0; rival < live.Length; rival++)
                {
                    live[rival] += (targetRivals[rival] - live[rival]) * speed;
                }

                Normalise(definition.Segment);
                playerTokens += pool * playerShares[definition.Segment];
            }

            seeded = true;
            return total <= 0.0 ? 0.0 : Math.Clamp(playerTokens / total, 0.0, 1.0);
        }

        /// <summary>
        /// Everything the Foundation screen needs, one row per segment. Built from the same numbers
        /// the tick used, never recomputed a second way.
        /// </summary>
        public List<SegmentStanding> Standings(GameDate date, double totalTokensPerDay,
            IReadOnlyList<string> rivalNames)
        {
            var result = new List<SegmentStanding>();
            var segments = AudienceCatalog.All;
            var shares = AudienceCatalog.SharesOn(date);

            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index].Segment;
                var live = rivalShares[segment];

                var leaderIndex = -1;
                var best = playerShares[segment];

                for (var rival = 0; rival < live.Length; rival++)
                {
                    if (live[rival] > best)
                    {
                        best = live[rival];
                        leaderIndex = rival;
                    }
                }

                var leaderName = leaderIndex < 0
                    ? "You"
                    : rivalNames != null && leaderIndex < rivalNames.Count
                        ? rivalNames[leaderIndex]
                        : $"Lab {leaderIndex}";

                result.Add(new SegmentStanding(
                    segment,
                    Math.Max(0.0, totalTokensPerDay) * shares[index],
                    playerShares[segment],
                    (double[])live.Clone(),
                    leaderIndex,
                    leaderName));
            }

            return result;
        }

        /// <summary>Everything drifts back toward nobody when there is nothing on the market.</summary>
        private void Decay()
        {
            foreach (var segment in AudienceCatalog.All)
            {
                var speed = Math.Clamp(segment.AdoptionRatePerDay, 0.002, 0.5);
                playerShares[segment.Segment] *= 1.0 - speed;

                var live = rivalShares[segment.Segment];
                for (var rival = 0; rival < live.Length; rival++)
                {
                    live[rival] *= 1.0 - speed;
                }
            }
        }

        /// <summary>
        /// A segment holds exactly one market. Floating point drift across fifteen years of daily
        /// ticks is enough to invent or lose users, and a segment that sums to 1.03 is a segment
        /// where three percent of the revenue came from nowhere.
        /// </summary>
        private void Normalise(AudienceSegment segment)
        {
            var live = rivalShares[segment];
            var total = playerShares[segment];

            for (var rival = 0; rival < live.Length; rival++)
            {
                if (live[rival] < 0.0)
                {
                    live[rival] = 0.0;
                }

                total += live[rival];
            }

            if (playerShares[segment] < 0.0)
            {
                playerShares[segment] = 0.0;
            }

            if (total <= 1.0 || total <= 0.0)
            {
                return;
            }

            playerShares[segment] /= total;
            for (var rival = 0; rival < live.Length; rival++)
            {
                live[rival] /= total;
            }
        }

        // ---------------------------------------------------------------- saves

        public double[] PlayerSharesToArray()
        {
            var segments = AudienceCatalog.All;
            var result = new double[segments.Count];
            for (var index = 0; index < segments.Count; index++)
            {
                result[index] = playerShares[segments[index].Segment];
            }

            return result;
        }

        public double[] RivalSharesToArray()
        {
            var segments = AudienceCatalog.All;
            var result = new double[segments.Count * rivalCount];

            for (var index = 0; index < segments.Count; index++)
            {
                var live = rivalShares[segments[index].Segment];
                for (var rival = 0; rival < rivalCount; rival++)
                {
                    result[index * rivalCount + rival] = live[rival];
                }
            }

            return result;
        }

        /// <summary>
        /// Restores a saved standing. A file written with a different number of labs is dropped
        /// rather than stretched: guessing which lab held which users would be inventing history.
        /// </summary>
        public void Restore(IReadOnlyList<double> player, IReadOnlyList<double> rivals, int rivalsInFile)
        {
            var segments = AudienceCatalog.All;
            seeded = player != null && player.Count == segments.Count;

            if (player != null && player.Count == segments.Count)
            {
                for (var index = 0; index < segments.Count; index++)
                {
                    playerShares[segments[index].Segment] = Math.Clamp(SimUnits.Finite(player[index]), 0.0, 1.0);
                }
            }

            if (rivals == null || rivalsInFile != rivalCount || rivals.Count != segments.Count * rivalCount)
            {
                return;
            }

            for (var index = 0; index < segments.Count; index++)
            {
                var live = rivalShares[segments[index].Segment];
                for (var rival = 0; rival < rivalCount; rival++)
                {
                    live[rival] = Math.Clamp(SimUnits.Finite(rivals[index * rivalCount + rival]), 0.0, 1.0);
                }
            }

            // Deliberately not normalised. These values were normalised when they were written, and
            // running the pass again divides by a total that is 1.0 plus or minus a float epsilon,
            // which moves every share by a hair. That is enough to make a loaded campaign diverge
            // from an unloaded one, which the replay test correctly refuses to accept.
        }
    }
}
