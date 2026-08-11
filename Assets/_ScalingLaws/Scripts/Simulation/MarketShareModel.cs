using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The ONE place demand gets split. A standard multinomial logit over every model on the market,
    /// the player's and the rivals' scored by the same four terms:
    ///
    ///   capability   how good it is
    ///   brand        whether anyone has heard of you
    ///   price        cheaper wins, with diminishing returns
    ///   age          a model nobody has updated in two years quietly stops being chosen
    ///
    /// Nothing in here guarantees the player a floor. Ship nothing for eighteen months and the share
    /// goes to zero while the cluster keeps billing.
    /// </summary>
    public static class MarketShareModel
    {
        public const double CapabilityWeight = 0.16;
        public const double BrandWeight = 1.2;
        public const double PriceWeight = 1.1;
        public const double AgeWeightPerYear = 0.45;

        /// <summary>
        /// Share of total market demand the company's live models take, 0 to 1.
        /// </summary>
        public static double PlayerShare(
            IReadOnlyList<DeployedModel> playerModels,
            double playerReputation,
            MarketConditions market)
        {
            var playerScore = 0.0;
            if (playerModels != null)
            {
                for (var index = 0; index < playerModels.Count; index++)
                {
                    var model = playerModels[index];
                    if (model == null || !model.IsLiveOn(market.Date))
                    {
                        continue;
                    }

                    playerScore += Math.Exp(Utility(
                        model.EffectiveCapability(market.Date),
                        Math.Clamp(playerReputation + model.BrandBonus(market.Date), 0.0, 1.0),
                        model.PriceMultiplier / ToleranceFactor(model.Type, market.Date),
                        model.AgeYears(market.Date))) * ReachFactor(model.Type, market.Date);
                }
            }

            var rivalScore = RivalScore(market.Date);
            var total = playerScore + rivalScore;
            return total <= 0.0 ? 0.0 : Math.Clamp(playerScore / total, 0.0, 1.0);
        }

        /// <summary>
        /// Share against a live field of agents rather than the static table. This is the path the
        /// simulation uses; the table version below stays as the seed and as a reference.
        /// </summary>
        public static double PlayerShare(
            IReadOnlyList<DeployedModel> playerModels,
            double playerReputation,
            MarketConditions market,
            IReadOnlyList<RivalModel> rivals)
        {
            var playerScore = PlayerScore(playerModels, playerReputation, market);
            var rivalScore = RivalScore(market.Date, rivals);
            var total = playerScore + rivalScore;
            return total <= 0.0 ? 0.0 : Math.Clamp(playerScore / total, 0.0, 1.0);
        }

        /// <summary>Sum of the exponentiated utilities of a live rival field, plus the incumbent world.</summary>
        public static double RivalScore(GameDate date, IReadOnlyList<RivalModel> rivals)
        {
            var score = 0.0;
            if (rivals != null)
            {
                for (var index = 0; index < rivals.Count; index++)
                {
                    var rival = rivals[index];
                    score += Math.Exp(Utility(
                        rival.Capability,
                        rival.BrandStrength,
                        rival.PriceMultiplier,
                        rival.ReleaseDate.YearsUntil(date)));
                }
            }

            return score + Math.Exp(Utility(
                CompetitorField.IncumbentCapability,
                CompetitorField.IncumbentBrand,
                1.0,
                0.0));
        }

        /// <summary>Exponentiated utility of everything the company has live. Shared by both paths.</summary>
        public static double PlayerScore(
            IReadOnlyList<DeployedModel> playerModels,
            double playerReputation,
            MarketConditions market)
        {
            var playerScore = 0.0;
            if (playerModels == null)
            {
                return 0.0;
            }

            for (var index = 0; index < playerModels.Count; index++)
            {
                var model = playerModels[index];
                if (model == null || !model.IsLiveOn(market.Date))
                {
                    continue;
                }

                playerScore += Math.Exp(Utility(
                    model.EffectiveCapability(market.Date),
                    Math.Clamp(playerReputation + model.BrandBonus(market.Date), 0.0, 1.0),
                    model.PriceMultiplier / ToleranceFactor(model.Type, market.Date),
                    model.AgeYears(market.Date))) * ReachFactor(model.Type, market.Date);
            }

            return playerScore;
        }

        /// <summary>
        /// How much of the market a type can reach, measured against a general model on the same
        /// date rather than in absolute terms.
        ///
        /// The ratio is what keeps this from being a balance change. Everything in this game was
        /// tuned against a general model, so a general model stays at exactly 1.0 forever and only
        /// the specialists move. A coding model in 2022 comes out below one and is punished for
        /// arriving early; the same model in 2026 comes out above one and is paid for the wait.
        ///
        /// It weights the price average as well as the demand split, because the price the market
        /// sees is the price paid by the users who are actually there.
        /// </summary>
        /// <summary>
        /// How much less this type's audience minds the price, measured against a general model on
        /// the same date.
        ///
        /// This is the half of the mechanic that makes specialising worth doing. Reach alone never
        /// can: the consumer segment is the largest one in every year of the game, so a model that
        /// trades consumer appeal for developer appeal always reaches fewer people. What it gets in
        /// return is an audience that does not leave when the price goes up, and the honest place to
        /// put that is the price term, not the headcount.
        /// </summary>
        public static double ToleranceFactor(ModelType type, GameDate date)
        {
            var baseline = ModelTypeCatalog.PriceToleranceOn(ModelType.General, date);
            if (baseline <= 0.0)
            {
                return 1.0;
            }

            return Math.Clamp(ModelTypeCatalog.PriceToleranceOn(type, date) / baseline, 0.6, 1.8);
        }

        public static double ReachFactor(ModelType type, GameDate date)
        {
            var baseline = ModelTypeCatalog.ReachOn(ModelType.General, date);
            if (baseline <= 0.0)
            {
                return 1.0;
            }

            return Math.Clamp(ModelTypeCatalog.ReachOn(type, date) / baseline, 0.25, 2.5);
        }

        /// <summary>Sum of the exponentiated utilities of every rival's current best model.</summary>
        public static double RivalScore(GameDate date)
        {
            var score = 0.0;
            foreach (var release in CompetitorCatalog.BestPerCompetitorOn(date))
            {
                score += Math.Exp(Utility(
                    release.Capability,
                    release.BrandStrength,
                    release.PriceMultiplier,
                    release.ReleaseDate.YearsUntil(date)));
            }

            // Before anyone has shipped, there is still an incumbent world of search boxes and
            // classical software to lose to. Without this the first model would take 100 percent.
            return score + Math.Exp(Utility(24.0, 0.5, 1.0, 0.0));
        }

        /// <summary>The creator's default model size, and therefore the size that scores exactly 1.0.</summary>
        public const double ReferenceActiveParameters = 2e10;

        /// <summary>
        /// How much of a big model's serving cost reaches the price the audience pays.
        ///
        /// Not all of it. A larger model justifies a higher price on its own merits, and distillation
        /// and quantisation take a further bite, which is already modelled by
        /// <see cref="DeployedModel.ServingDistillationFactor"/>. Passing the whole cost through would
        /// make anything above a hundred billion parameters unsellable, which is not what happened.
        /// </summary>
        public const double SizePassThrough = 0.6;

        /// <summary>
        /// What a model's size does to the cost of serving it, relative to a twenty billion parameter
        /// model.
        ///
        /// This existed nowhere before, and its absence made the whole Scale stage consequence free
        /// past the training bill. <c>InferenceFlopPerToken</c> already scales with active parameters
        /// and already bills the player, but the market's burden term dropped the size entirely, so a
        /// ten times larger model cost the audience nothing extra and the warning that an oversized
        /// model would be expensive to serve later was simply untrue.
        ///
        /// Square rooted rather than linear because only part of the cost passes into the price.
        /// </summary>
        public static double SizeBurden(double activeParameterCount)
        {
            var parameters = Math.Max(1e6,
                SimUnits.Finite(activeParameterCount, ReferenceActiveParameters));

            var ratio = parameters / ReferenceActiveParameters;
            return Math.Clamp(1.0 + (Math.Sqrt(ratio) - 1.0) * SizePassThrough, 0.15, 8.0);
        }

        public static double Utility(double capability, double brand, double priceMultiplier, double ageYears)
        {
            var safePrice = Math.Clamp(SimUnits.Finite(priceMultiplier, 1.0), 0.05, 10.0);
            var safeAge = Math.Max(0.0, SimUnits.Finite(ageYears));

            return CapabilityWeight * Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0)
                + BrandWeight * Math.Clamp(SimUnits.Finite(brand), 0.0, 1.0)
                - PriceWeight * Math.Log(safePrice)
                - AgeWeightPerYear * safeAge;
        }

        /// <summary>
        /// Average price multiplier across the company's live models, weighted by how much of the
        /// share each one is pulling. Used to turn served tokens into revenue.
        /// </summary>
        public static double EffectivePriceMultiplier(
            IReadOnlyList<DeployedModel> playerModels,
            double playerReputation,
            MarketConditions market)
        {
            var weightSum = 0.0;
            var weightedPrice = 0.0;

            if (playerModels != null)
            {
                for (var index = 0; index < playerModels.Count; index++)
                {
                    var model = playerModels[index];
                    if (model == null || !model.IsLiveOn(market.Date))
                    {
                        continue;
                    }

                    var weight = Math.Exp(Utility(
                        model.EffectiveCapability(market.Date),
                        Math.Clamp(playerReputation + model.BrandBonus(market.Date), 0.0, 1.0),
                        model.PriceMultiplier / ToleranceFactor(model.Type, market.Date),
                        model.AgeYears(market.Date))) * ReachFactor(model.Type, market.Date);
                    weightSum += weight;
                    weightedPrice += weight * model.PriceMultiplier;
                }
            }

            return weightSum <= 0.0 ? 1.0 : weightedPrice / weightSum;
        }

        /// <summary>The company's best live model, or null when nothing is on the market.</summary>
        public static DeployedModel BestLiveModel(IReadOnlyList<DeployedModel> playerModels, GameDate date)
        {
            DeployedModel best = null;
            if (playerModels == null)
            {
                return null;
            }

            for (var index = 0; index < playerModels.Count; index++)
            {
                var model = playerModels[index];
                if (model == null || !model.IsLiveOn(date))
                {
                    continue;
                }

                if (best == null || model.EffectiveCapability(date) > best.EffectiveCapability(date))
                {
                    best = model;
                }
            }

            return best;
        }
    }
}
