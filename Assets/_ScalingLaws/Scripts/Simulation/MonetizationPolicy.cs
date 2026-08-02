using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// How the company charges, how generous it is, and what it spends on being noticed.
    ///
    /// The free tier is the interesting decision and the one that can quietly ruin a quarter.
    /// Generosity buys reach: more people try the product, so the company's share of total demand
    /// goes up. It also decides what share of the tokens it serves earn nothing. Serving capacity
    /// does not care which kind of token it is producing, and neither does the bill.
    ///
    /// A company that gives away a quarter of a million tokens a day per user will have an enormous
    /// user base, a serving cost to match, and revenue from under a third of what it produces.
    /// Whether that is a growth strategy or a slow death depends entirely on what its serving cost
    /// per token is, which is a different screen and a different decision made months earlier.
    /// </summary>
    public sealed class MonetizationPolicy
    {
        private double paidPriceMultiplier = 1.0;
        private double subscriptionPriceUsdPerMonth = 20.0;
        private double freeTierTokensPerUserPerDay;
        private long companyMarketingDailyUsd;
        private long modelMarketingDailyUsd;
        private double modelAwareness;

        public PricingModel Model { get; set; } = PricingModel.PayPerToken;

        /// <summary>Position against the market rate. Only used when charging per token.</summary>
        public double PaidPriceMultiplier
        {
            get => paidPriceMultiplier;
            set => paidPriceMultiplier = Math.Clamp(SimUnits.Finite(value, 1.0), 0.05, 10.0);
        }

        /// <summary>Monthly fee. Only used on a subscription, and it ignores the market rate.</summary>
        public double SubscriptionPriceUsdPerMonth
        {
            get => subscriptionPriceUsdPerMonth;
            set => subscriptionPriceUsdPerMonth = Math.Clamp(SimUnits.Finite(value, 20.0), 0.0, 2000.0);
        }

        /// <summary>Tokens a free account gets each day. Zero means no free tier at all.</summary>
        public double FreeTierTokensPerUserPerDay
        {
            get => freeTierTokensPerUserPerDay;
            set => freeTierTokensPerUserPerDay = Math.Clamp(SimUnits.Finite(value), 0.0, 2_000_000.0);
        }

        public long CompanyMarketingDailyUsd
        {
            get => companyMarketingDailyUsd;
            set => companyMarketingDailyUsd = Math.Clamp(value, 0L, 500_000_000L);
        }

        public long ModelMarketingDailyUsd
        {
            get => modelMarketingDailyUsd;
            set => modelMarketingDailyUsd = Math.Clamp(value, 0L, 500_000_000L);
        }

        /// <summary>Brand from the current model campaign. Decays the moment the spending stops.</summary>
        public double ModelAwareness
        {
            get => modelAwareness;
            private set => modelAwareness = Math.Clamp(SimUnits.Finite(value), 0.0, 0.35);
        }

        public long TotalMarketingDailyUsd => CompanyMarketingDailyUsd + ModelMarketingDailyUsd;

        /// <summary>Free tier generosity, 0 to 1. Drives both reach and how much is given away.</summary>
        public double Generosity => Math.Clamp(
            FreeTierTokensPerUserPerDay / MonetizationCatalog.GenerousFreeTierTokensPerDay, 0.0, 1.0);

        /// <summary>Multiplier on the company's share of total demand. More generous, more reach.</summary>
        public double ReachMultiplier => Model == PricingModel.FreeOnly
            ? 1.0 + MonetizationCatalog.FreeTierReachBonus
            : 1.0 + MonetizationCatalog.FreeTierReachBonus * Generosity;

        /// <summary>Share of served tokens that produce no revenue at all.</summary>
        public double FreeShareOfTokens => Model == PricingModel.FreeOnly
            ? 1.0
            : Math.Clamp(
                MonetizationCatalog.BaseFreeShare
                + MonetizationCatalog.MaximumExtraFreeShare * Generosity,
                0.0,
                0.95);

        /// <summary>
        /// Dollars per million tokens the company actually charges. On a subscription this is set by
        /// the company and does not move when the market price does, which is the whole trade: it
        /// protects a good position and traps a bad one.
        /// </summary>
        public double RatePerMillionTokensUsd(double marketPricePerMillionUsd)
        {
            return Model switch
            {
                PricingModel.PayPerToken =>
                    Math.Max(0.0, marketPricePerMillionUsd) * PaidPriceMultiplier,
                PricingModel.Subscription =>
                    SubscriptionPriceUsdPerMonth
                    / (MonetizationCatalog.TokensPerSubscriberPerMonth / 1_000_000.0),
                _ => 0.0
            };
        }

        /// <summary>
        /// Where the company sits against the market, for the demand split. Free reads as very
        /// cheap rather than as free, because attention still has to be won from paid rivals.
        /// </summary>
        public double RelativePrice(double marketPricePerMillionUsd)
        {
            if (marketPricePerMillionUsd <= 0.0)
            {
                return 1.0;
            }

            if (Model == PricingModel.FreeOnly)
            {
                return 0.05;
            }

            var rate = RatePerMillionTokensUsd(marketPricePerMillionUsd);
            return Math.Clamp(rate / marketPricePerMillionUsd, 0.05, 10.0);
        }

        /// <summary>Brand from marketing: the company line plus whatever the model campaign holds.</summary>
        public double BrandBonus() => ModelAwareness;

        /// <summary>
        /// Runs a day of marketing. Company spend feeds reputation directly, which is handled by the
        /// caller; model spend feeds an awareness pool that leaks away as soon as it is not topped up.
        /// </summary>
        public double AdvanceMarketing()
        {
            var companyEffect = MonetizationCatalog.EffectFor(CompanyMarketingDailyUsd, 0.0015)
                * CompanyMarketingDailyUsd;

            var modelEffect = MonetizationCatalog.EffectFor(ModelMarketingDailyUsd, 0.0070)
                * ModelMarketingDailyUsd;

            ModelAwareness = ModelAwareness
                + modelEffect
                - MonetizationCatalog.ModelAwarenessDecayPerDay * ModelAwareness;

            return companyEffect;
        }

        public void Restore(
            PricingModel model,
            double paidPrice,
            double subscriptionPrice,
            double freeTokens,
            long companyMarketing,
            long modelMarketing,
            double awareness)
        {
            Model = Enum.IsDefined(typeof(PricingModel), model) ? model : PricingModel.PayPerToken;
            PaidPriceMultiplier = paidPrice;
            SubscriptionPriceUsdPerMonth = subscriptionPrice;
            FreeTierTokensPerUserPerDay = freeTokens;
            CompanyMarketingDailyUsd = companyMarketing;
            ModelMarketingDailyUsd = modelMarketing;
            ModelAwareness = awareness;
        }

        public override string ToString() =>
            $"{MonetizationCatalog.PricingName(Model)}, {FreeShareOfTokens:P0} given away";
    }
}
