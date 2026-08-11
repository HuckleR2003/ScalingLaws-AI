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

    /// <summary>What one audience looks like today. Immutable, for the UI and for tests.</summary>
    public readonly struct SegmentStanding
    {
        public SegmentStanding(AudienceSegment segment, double totalTokensPerDay, double totalUsers,
            double playerShare, double[] ownerShares, int leaderIndex, string leaderName)
        {
            Segment = segment;
            TotalTokensPerDay = Math.Max(0.0, SimUnits.Finite(totalTokensPerDay));
            TotalUsers = Math.Max(0.0, SimUnits.Finite(totalUsers));
            PlayerShare = Math.Clamp(SimUnits.Finite(playerShare), 0.0, 1.0);
            OwnerShares = ownerShares ?? Array.Empty<double>();
            LeaderIndex = leaderIndex;
            LeaderName = leaderName ?? string.Empty;
        }

        public AudienceSegment Segment { get; }
        public double TotalTokensPerDay { get; }

        /// <summary>People, not tokens. Derived from the pool by how much this audience consumes.</summary>
        public double TotalUsers { get; }

        public double PlayerShare { get; }

        /// <summary>Share held by each owner. Index zero is the player, then each rival in order.</summary>
        public IReadOnlyList<double> OwnerShares { get; }

        /// <summary>Zero when the player leads, otherwise the owner index.</summary>
        public int LeaderIndex { get; }

        public string LeaderName { get; }

        public double PlayerTokensPerDay => TotalTokensPerDay * PlayerShare;
        public double PlayerUsers => TotalUsers * PlayerShare;
    }

    /// <summary>
    /// One kind of product across the whole market: how many people use that kind of model, who
    /// holds them, and who is winning that category.
    ///
    /// This is the cut the Foundation screen asks for, and it is a genuinely different question
    /// from the audience cut. An audience is who someone is; a type is what they are being sold.
    /// </summary>
    public readonly struct TypeStanding
    {
        public TypeStanding(ModelType type, double totalUsers, double[] ownerUsers, int leaderIndex,
            string leaderName)
        {
            Type = type;
            TotalUsers = Math.Max(0.0, SimUnits.Finite(totalUsers));
            OwnerUsers = ownerUsers ?? Array.Empty<double>();
            LeaderIndex = leaderIndex;
            LeaderName = leaderName ?? string.Empty;
        }

        public ModelType Type { get; }
        public double TotalUsers { get; }

        /// <summary>Users held by each owner. Index zero is the player.</summary>
        public IReadOnlyList<double> OwnerUsers { get; }

        public int LeaderIndex { get; }
        public string LeaderName { get; }

        public double PlayerUsers => OwnerUsers.Count > 0 ? OwnerUsers[0] : 0.0;

        public double PlayerShare => TotalUsers <= 0.0 ? 0.0 : PlayerUsers / TotalUsers;

        /// <summary>What fraction of this category each owner holds. Sums to one when anyone is in it.</summary>
        public double ShareOf(int ownerIndex) =>
            TotalUsers <= 0.0 || ownerIndex < 0 || ownerIndex >= OwnerUsers.Count
                ? 0.0
                : OwnerUsers[ownerIndex] / TotalUsers;
    }

    /// <summary>
    /// Everything the demographic panel draws, built once from the standing rather than recomputed
    /// per widget. Owner index zero is always the player.
    /// </summary>
    public sealed class MarketBreakdown
    {
        public MarketBreakdown(IReadOnlyList<string> ownerNames, IReadOnlyList<TypeStanding> types,
            double[] ownerUsersOverall, double totalUsersOverall, double addressableUsers)
        {
            OwnerNames = ownerNames ?? Array.Empty<string>();
            Types = types ?? Array.Empty<TypeStanding>();
            OwnerUsersOverall = ownerUsersOverall ?? Array.Empty<double>();
            TotalUsersOverall = Math.Max(0.0, SimUnits.Finite(totalUsersOverall));
            AddressableUsers = Math.Max(0.0, SimUnits.Finite(addressableUsers));
        }

        /// <summary>Index zero is the player, then every rival lab in field order.</summary>
        public IReadOnlyList<string> OwnerNames { get; }

        /// <summary>Every type that has anybody in it, largest audience first.</summary>
        public IReadOnlyList<TypeStanding> Types { get; }

        public IReadOnlyList<double> OwnerUsersOverall { get; }

        /// <summary>
        /// People actually using something today. This is what the pie divides, so it adds to a
        /// hundred percent by construction.
        /// </summary>
        public double TotalUsersOverall { get; }

        /// <summary>
        /// Everybody in the market, whether or not anyone is serving them. The gap between this and
        /// the held total is demand nobody has taken yet, which is the room a new model has to grow
        /// into rather than having to take from somebody.
        /// </summary>
        public double AddressableUsers { get; }

        /// <summary>Share of the market nobody holds. Room, not loss.</summary>
        public double UnservedShare => AddressableUsers <= 0.0
            ? 0.0
            : Math.Clamp(1.0 - TotalUsersOverall / AddressableUsers, 0.0, 1.0);

        public double OverallShareOf(int ownerIndex) =>
            TotalUsersOverall <= 0.0 || ownerIndex < 0 || ownerIndex >= OwnerUsersOverall.Count
                ? 0.0
                : OwnerUsersOverall[ownerIndex] / TotalUsersOverall;

        public bool TryGetType(ModelType type, out TypeStanding standing)
        {
            for (var index = 0; index < Types.Count; index++)
            {
                if (Types[index].Type == type)
                {
                    standing = Types[index];
                    return true;
                }
            }

            standing = default;
            return false;
        }
    }

    /// <summary>
    /// Who is actually using each product, and how that moves over time.
    ///
    /// This exists because the share model it sits on top of is a pure function of today: ship a
    /// better model on Tuesday and you owned the market on Tuesday. There was no user base, no
    /// switching cost and no momentum, which meant no audience could behave differently from any
    /// other and a niche was impossible to hold.
    ///
    /// What it adds is exactly one idea: **a target and a speed**. Attractiveness is still scored by
    /// <see cref="MarketShareModel.Utility"/>, the same function, for the player and for every rival,
    /// with no second quality formula anywhere. The pool is still <see cref="MarketModel.DemandOn"/>.
    /// What is new is that the standing eases toward the target instead of being it.
    ///
    /// The standing is tracked per **audience, owner and model type** rather than per audience and
    /// owner. That third axis is what lets the game answer "who is winning Programming", which is a
    /// different question from "who is winning developers": a general model sells to developers too.
    /// </summary>
    public sealed class SegmentMarket
    {
        private readonly int segmentCount;
        private readonly int typeCount;
        private int ownerCount;

        /// <summary>[segment][owner * typeCount + type]. Owner zero is the player.</summary>
        private double[][] shares;

        /// <summary>
        /// False for a standing that was never recorded, which means a save written before this
        /// existed. The first tick after that snaps to the target instead of easing toward it.
        ///
        /// Easing would be wrong rather than merely slow: a company that had held a market for three
        /// years would come back from a save with no users and have to earn them again, which is a
        /// punishment for saving. A market with no recorded history has no inertia to respect.
        /// </summary>
        private bool seeded = true;

        public SegmentMarket(int rivals)
        {
            segmentCount = AudienceCatalog.All.Count;
            typeCount = ModelTypeCatalog.All.Count;
            ownerCount = Math.Max(0, rivals) + 1;

            shares = new double[segmentCount][];
            for (var segment = 0; segment < segmentCount; segment++)
            {
                shares[segment] = new double[ownerCount * typeCount];
            }
        }

        /// <summary>Rival labs only. The player is not counted here.</summary>
        public int RivalCount => ownerCount - 1;

        /// <summary>Player plus every rival.</summary>
        public int OwnerCount => ownerCount;

        /// <summary>Where the player share is heading per audience, as of the last tick.</summary>
        public double[] LastTargets { get; private set; } = new double[AudienceCatalog.All.Count];

        /// <summary>How many products competed on the last tick.</summary>
        public int LastEntrantCount { get; private set; }

        private static int TypeIndex(ModelType type)
        {
            var all = ModelTypeCatalog.All;
            for (var index = 0; index < all.Count; index++)
            {
                if (all[index].Type == type)
                {
                    return index;
                }
            }

            return 0;
        }

        /// <summary>Owner index used everywhere: the player is zero, rival n is n plus one.</summary>
        private static int OwnerIndexOf(in MarketEntrant entrant) =>
            entrant.IsPlayer ? 0 : entrant.OwnerIndex + 1;

        public double PlayerShareIn(AudienceSegment segment)
        {
            var row = RowFor(segment);
            if (row == null)
            {
                return 0.0;
            }

            var total = 0.0;
            for (var type = 0; type < typeCount; type++)
            {
                total += row[type];
            }

            return total;
        }

        public double ShareIn(AudienceSegment segment, int ownerIndex, ModelType type)
        {
            var row = RowFor(segment);
            if (row == null || ownerIndex < 0 || ownerIndex >= ownerCount)
            {
                return 0.0;
            }

            return row[ownerIndex * typeCount + TypeIndex(type)];
        }

        private double[] RowFor(AudienceSegment segment)
        {
            var all = AudienceCatalog.All;
            for (var index = 0; index < all.Count; index++)
            {
                if (all[index].Segment == segment)
                {
                    return shares[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Attractiveness of one product to one audience.
        ///
        /// The base is the same utility every part of this game has always used. The audience then
        /// applies its own weighting: how much a price rise costs it, how much it cares who made the
        /// thing, and whether the product's type suits it at all.
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

            var felt = entrant.PriceMultiplier / Math.Max(0.25, segment.WillingnessToPay);

            var utility = MarketShareModel.Utility(
                entrant.Capability,
                entrant.Brand * segment.BrandWeight,
                felt,
                entrant.AgeYears);

            // Cost sensitive audiences punish an expensive model to serve, because that cost has to
            // come out of the price eventually and they are the ones who notice.
            var burden = 1.0 + (entrant.ServingBurden - 1.0) * segment.ServingCostWeight;

            return Math.Exp(utility) * fit / Math.Max(0.2, burden);
        }

        /// <summary>
        /// Moves every audience one day toward what its buyers would prefer today, and returns the
        /// player's share of the whole market, which is what the rest of the simulation consumes.
        /// </summary>
        public double Advance(IReadOnlyList<MarketEntrant> entrants, GameDate date, double totalTokensPerDay)
        {
            if (entrants == null || entrants.Count == 0)
            {
                Decay();
                return 0.0;
            }

            var segments = AudienceCatalog.All;
            var segmentShares = AudienceCatalog.SharesOn(date);
            var total = Math.Max(0.0, totalTokensPerDay);

            LastEntrantCount = entrants.Count;
            var playerTokens = 0.0;

            var target = new double[ownerCount * typeCount];

            for (var index = 0; index < segments.Count; index++)
            {
                var definition = segments[index];
                Array.Clear(target, 0, target.Length);
                var sum = 0.0;

                for (var entry = 0; entry < entrants.Count; entry++)
                {
                    var score = Attractiveness(entrants[entry], definition, date);
                    if (score <= 0.0)
                    {
                        continue;
                    }

                    var owner = OwnerIndexOf(entrants[entry]);
                    if (owner < 0 || owner >= ownerCount)
                    {
                        continue;
                    }

                    target[owner * typeCount + TypeIndex(entrants[entry].Type)] += score;
                    sum += score;
                }

                if (sum <= 0.0)
                {
                    continue;
                }

                for (var bucket = 0; bucket < target.Length; bucket++)
                {
                    target[bucket] /= sum;
                }

                var playerTarget = 0.0;
                for (var type = 0; type < typeCount; type++)
                {
                    playerTarget += target[type];
                }

                LastTargets[index] = playerTarget;

                // The whole point. Users move a fraction of the way, and the fraction belongs to the
                // audience. Developers reprice their loyalty in weeks; an enterprise contract does
                // not care what shipped on Tuesday.
                var speed = seeded ? Math.Clamp(definition.AdoptionRatePerDay, 0.002, 0.5) : 1.0;
                var row = shares[index];

                for (var bucket = 0; bucket < row.Length; bucket++)
                {
                    row[bucket] += (target[bucket] - row[bucket]) * speed;
                }

                Normalise(index);

                var held = 0.0;
                for (var type = 0; type < typeCount; type++)
                {
                    held += row[type];
                }

                playerTokens += total * segmentShares[index] * held;
            }

            seeded = true;
            return total <= 0.0 ? 0.0 : Math.Clamp(playerTokens / total, 0.0, 1.0);
        }

        /// <summary>One row per audience, for anything that wants the market by who people are.</summary>
        public List<SegmentStanding> Standings(GameDate date, double totalTokensPerDay,
            IReadOnlyList<string> ownerNames)
        {
            var result = new List<SegmentStanding>();
            var segments = AudienceCatalog.All;
            var segmentShares = AudienceCatalog.SharesOn(date);
            var total = Math.Max(0.0, totalTokensPerDay);

            for (var index = 0; index < segments.Count; index++)
            {
                var row = shares[index];
                var owners = new double[ownerCount];

                for (var owner = 0; owner < ownerCount; owner++)
                {
                    for (var type = 0; type < typeCount; type++)
                    {
                        owners[owner] += row[owner * typeCount + type];
                    }
                }

                var leader = 0;
                for (var owner = 1; owner < ownerCount; owner++)
                {
                    if (owners[owner] > owners[leader])
                    {
                        leader = owner;
                    }
                }

                var tokens = total * segmentShares[index];

                result.Add(new SegmentStanding(
                    segments[index].Segment,
                    tokens,
                    segments[index].UsersFor(tokens),
                    owners[0],
                    owners,
                    leader,
                    NameOf(ownerNames, leader)));
            }

            return result;
        }

        /// <summary>
        /// The market by what people are being sold rather than by who they are.
        ///
        /// Users are derived from the token pool rather than tracked separately, because two numbers
        /// that mean the same thing eventually disagree. Each audience says how many tokens one of
        /// its people gets through in a day, and the count falls out of that.
        /// </summary>
        public MarketBreakdown Breakdown(GameDate date, double totalTokensPerDay,
            IReadOnlyList<string> ownerNames)
        {
            var segments = AudienceCatalog.All;
            var segmentShares = AudienceCatalog.SharesOn(date);
            var total = Math.Max(0.0, totalTokensPerDay);
            var types = ModelTypeCatalog.All;

            var perType = new double[typeCount][];
            for (var type = 0; type < typeCount; type++)
            {
                perType[type] = new double[ownerCount];
            }

            var overall = new double[ownerCount];
            var addressable = 0.0;
            var held = 0.0;

            for (var index = 0; index < segments.Count; index++)
            {
                var row = shares[index];
                var users = segments[index].UsersFor(total * segmentShares[index]);
                addressable += users;

                for (var owner = 0; owner < ownerCount; owner++)
                {
                    for (var type = 0; type < typeCount; type++)
                    {
                        var owned = users * row[owner * typeCount + type];
                        perType[type][owner] += owned;
                        overall[owner] += owned;
                        held += owned;
                    }
                }
            }

            var standings = new List<TypeStanding>(typeCount);
            for (var type = 0; type < typeCount; type++)
            {
                var owners = perType[type];
                var sum = 0.0;
                var leader = 0;

                for (var owner = 0; owner < ownerCount; owner++)
                {
                    sum += owners[owner];
                    if (owners[owner] > owners[leader])
                    {
                        leader = owner;
                    }
                }

                standings.Add(new TypeStanding(
                    types[type].Type, sum, owners, leader, NameOf(ownerNames, leader)));
            }

            standings.Sort((left, right) => right.TotalUsers.CompareTo(left.TotalUsers));

            var names = new List<string>(ownerCount);
            for (var owner = 0; owner < ownerCount; owner++)
            {
                names.Add(NameOf(ownerNames, owner));
            }

            return new MarketBreakdown(names, standings, overall, held, addressable);
        }

        private static string NameOf(IReadOnlyList<string> ownerNames, int ownerIndex)
        {
            if (ownerIndex <= 0)
            {
                return "You";
            }

            return ownerNames != null && ownerIndex < ownerNames.Count
                ? ownerNames[ownerIndex]
                : $"Lab {ownerIndex}";
        }

        private void Decay()
        {
            var segments = AudienceCatalog.All;
            for (var index = 0; index < segments.Count; index++)
            {
                var speed = Math.Clamp(segments[index].AdoptionRatePerDay, 0.002, 0.5);
                var row = shares[index];

                for (var bucket = 0; bucket < row.Length; bucket++)
                {
                    row[bucket] *= 1.0 - speed;
                }
            }
        }

        /// <summary>
        /// An audience holds exactly one market. Float drift across fifteen years of daily ticks is
        /// enough to invent or lose users, and an audience that sums to 1.03 is one where three
        /// percent of the revenue came from nowhere.
        /// </summary>
        private void Normalise(int segmentIndex)
        {
            var row = shares[segmentIndex];
            var total = 0.0;

            for (var bucket = 0; bucket < row.Length; bucket++)
            {
                if (row[bucket] < 0.0)
                {
                    row[bucket] = 0.0;
                }

                total += row[bucket];
            }

            if (total <= 1.0 || total <= 0.0)
            {
                return;
            }

            for (var bucket = 0; bucket < row.Length; bucket++)
            {
                row[bucket] /= total;
            }
        }

        // ---------------------------------------------------------------- saves

        /// <summary>Every bucket, audience major. Length is audiences times owners times types.</summary>
        public double[] ToArray()
        {
            var flat = new double[segmentCount * ownerCount * typeCount];
            var at = 0;

            for (var segment = 0; segment < segmentCount; segment++)
            {
                Array.Copy(shares[segment], 0, flat, at, shares[segment].Length);
                at += shares[segment].Length;
            }

            return flat;
        }

        /// <summary>
        /// Restores a standing. A file written for a different number of labs or types is dropped
        /// rather than stretched: guessing which lab held which users would be inventing history.
        ///
        /// Deliberately not normalised. These values were normalised when they were written, and
        /// running the pass again divides by a total that is one plus or minus a float epsilon,
        /// which moves every share by a hair and makes a loaded campaign diverge from an unloaded
        /// one. The replay test refuses to accept that, correctly.
        /// </summary>
        public void Restore(IReadOnlyList<double> flat, int ownersInFile, int typesInFile)
        {
            var expected = segmentCount * ownerCount * typeCount;

            seeded = flat != null
                && flat.Count == expected
                && ownersInFile == ownerCount
                && typesInFile == typeCount;

            if (!seeded)
            {
                return;
            }

            var at = 0;
            for (var segment = 0; segment < segmentCount; segment++)
            {
                for (var bucket = 0; bucket < shares[segment].Length; bucket++)
                {
                    shares[segment][bucket] = Math.Clamp(SimUnits.Finite(flat[at++]), 0.0, 1.0);
                }
            }
        }
    }
}
