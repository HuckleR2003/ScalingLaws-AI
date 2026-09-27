using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The four things one lab can offer another.
    ///
    /// **Every one of them is a shape the real industry used**, and each is the player's side of a
    /// deal that actually happened. `Docs/RELATIONS_PLAN.md` carries the sources; the short version
    /// is a published finding (the Frontier Model Forum's public library), a joint evaluation
    /// (OpenAI and Anthropic cross-testing each other's models in August 2025, with special API
    /// access and safeguards relaxed on both sides), a distribution licence (Snowflake carrying
    /// OpenAI's and Mistral's models to customers neither of them had) and a capacity purchase
    /// (Anthropic committing to $30bn of Azure while Microsoft invested in them).
    ///
    /// **Written into saves as ints, so these values must never be renumbered.**
    /// </summary>
    public enum RelationOffer
    {
        /// <summary>Put a result out where everybody can read it. Cheap, public, small.</summary>
        PublishFinding = 0,

        /// <summary>Each side tests the other's model and both publish. Costs time, buys trust.</summary>
        JointEvaluation = 1,

        /// <summary>Let them sell your model to people you do not reach. They pay; you share.</summary>
        DistributionLicence = 2,

        /// <summary>Buy capacity from them, over the market rate, for a term.</summary>
        CapacityPurchase = 3
    }

    /// <summary>What one offer costs, what it moves, and what it needs to be possible at all.</summary>
    public readonly struct RelationOfferDefinition
    {
        public RelationOfferDefinition(RelationOffer offer, string key, int pointCost,
            long cashCost, int daysToAnswer, int termDays, double relationGain,
            RelationBand needsAtLeast, bool needsLiveModel)
        {
            Offer = offer;
            this.key = key;
            PointCost = pointCost;
            CashCostUsd = cashCost;
            DaysToAnswer = daysToAnswer;
            TermDays = termDays;
            RelationGain = relationGain;
            NeedsAtLeast = needsAtLeast;
            NeedsLiveModel = needsLiveModel;
        }

        private readonly string key;

        public RelationOffer Offer { get; }

        /// <summary>Resolved per read, so a language change mid-campaign reaches it.</summary>
        public string DisplayName => Loc.T(key);

        public string Description => Loc.T(key + ".desc");

        /// <summary>What the player gives up. Points are the real gate on three of the four.</summary>
        public int PointCost { get; }

        public long CashCostUsd { get; }

        /// <summary>How long the other side takes to answer. Nothing here lands on the click.</summary>
        public int DaysToAnswer { get; }

        /// <summary>How long the thing it buys lasts, or zero for a one-off.</summary>
        public int TermDays { get; }

        /// <summary>What it adds to the relation when it is accepted.</summary>
        public double RelationGain { get; }

        /// <summary>The band below which they will not even read it.</summary>
        public RelationBand NeedsAtLeast { get; }

        /// <summary>Whether the company has to have something on sale to make this offer.</summary>
        public bool NeedsLiveModel { get; }
    }

    /// <summary>
    /// The four offers and their numbers.
    ///
    /// **Publishing is deliberately the cheap one.** It is the only move a company with nothing can
    /// make, and a relation system whose first rung costs a million dollars is a system the early
    /// game cannot see at all. It is also the only one that reaches every lab at once, because a
    /// paper is public and a deal is not.
    ///
    /// **None of them pays a dividend.** They buy points, trust, reach and capacity. A relation that
    /// paid money would be an income guarantee, which is against the spine of this game.
    /// </summary>
    public static class RelationOfferCatalog
    {
        /// <summary>What a published finding adds to every other lab, on top of the one it names.</summary>
        public const double PublicGainToEverybody = 1.5;

        /// <summary>
        /// The share of the model's own take a distribution partner keeps.
        ///
        /// Their channel, their margin. Thirty per cent is the ordinary platform cut and it is what
        /// makes this a trade rather than free money: the reach is real and so is the price.
        /// </summary>
        public const double DistributionShare = 0.30;

        /// <summary>How much more of an audience a distribution licence reaches, at its peak.</summary>
        public const double DistributionReach = 0.22;

        /// <summary>What a bought petaflop costs against renting it. Reserved capacity is dearer.</summary>
        public const double CapacityPremium = 1.35;

        /// <summary>How much of the fleet a capacity deal is worth, as a share of what is rented.</summary>
        public const double CapacityShare = 0.45;

        /// <summary>While a joint evaluation runs, both sides are less likely to have an incident.</summary>
        public const double EvaluationSafety = 0.82;

        private static readonly RelationOfferDefinition[] Entries =
        {
            new(RelationOffer.PublishFinding, "offer.publish",
                pointCost: 30, cashCost: 0, daysToAnswer: 4, termDays: 0,
                relationGain: 6.0, needsAtLeast: RelationBand.Rivalry, needsLiveModel: false),

            new(RelationOffer.JointEvaluation, "offer.evaluate",
                pointCost: 60, cashCost: 250_000, daysToAnswer: 12, termDays: 90,
                relationGain: 14.0, needsAtLeast: RelationBand.Tense, needsLiveModel: true),

            new(RelationOffer.DistributionLicence, "offer.distribution",
                pointCost: 0, cashCost: 1_200_000, daysToAnswer: 14, termDays: 270,
                relationGain: 12.0, needsAtLeast: RelationBand.Neutral, needsLiveModel: true),

            new(RelationOffer.CapacityPurchase, "offer.capacity",
                pointCost: 0, cashCost: 4_000_000, daysToAnswer: 9, termDays: 180,
                relationGain: 10.0, needsAtLeast: RelationBand.Neutral, needsLiveModel: false)
        };

        public static IReadOnlyList<RelationOfferDefinition> All => Entries;

        public static RelationOfferDefinition Get(RelationOffer offer)
        {
            foreach (var entry in Entries)
            {
                if (entry.Offer == offer)
                {
                    return entry;
                }
            }

            return Entries[0];
        }

        /// <summary>
        /// How likely they are to say yes, from what the game already knows about them.
        ///
        /// **Three readings, and none of them is a new number.** Where the relation stands, how far
        /// ahead of you they are, and whether the offer is a gift. A lab well ahead has less to gain
        /// from working with you and says so; a lab behind you takes the call.
        ///
        /// A published finding is not an offer at all, so it is never refused: nobody turns down
        /// reading a paper. That is what keeps the first rung reachable.
        /// </summary>
        public static double AcceptanceChance(RelationOffer offer, double relation,
            double theirCapability, double yourCapability)
        {
            if (offer == RelationOffer.PublishFinding)
            {
                return 1.0;
            }

            // Zero at the bottom of the scale, one at the top, and half at neutral-going-friendly.
            var standing = (relation - RelationScale.Worst) / (RelationScale.Best - RelationScale.Worst);

            // How far ahead they are, as a share. Capped, or an enormous gap makes every offer
            // impossible and the mechanic disappears exactly when a player most needs it.
            var gap = theirCapability <= 0.0
                ? 0.0
                : System.Math.Clamp((theirCapability - yourCapability) / theirCapability, -0.5, 0.6);

            return System.Math.Clamp(standing * 1.15 - gap * 0.55, 0.05, 0.95);
        }
    }
}
