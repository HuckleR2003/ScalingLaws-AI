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
        private double subscriptionPriceUsdPerMonth = OpeningSubscriptionUsdPerMonth;
        private double freeTierTokensPerUserPerDay;
        private long companyMarketingDailyUsd;
        private long modelMarketingDailyUsd;
        private double modelAwareness;

        /// <summary>
        /// The monthly fee that charges exactly the market rate on the first day.
        ///
        /// **Derived, because the alternative is a silent price cut.** A company opens on a
        /// subscription now, and the old default of $20 a month converts to $5 per million
        /// tokens against a market opening at $20, so simply switching which billing model a
        /// company starts on would have handed every new campaign a 75% discount it never asked
        /// for and no screen ever mentioned.
        ///
        /// This is the neutral-option rule the rest of the game already lives under: safety
        /// effort x1 is exactly 1.0, the skill baseline is the neutral point rather than the
        /// floor, and a player who has not touched a control has not made a decision. Written as
        /// the arithmetic rather than as 80.0 so the two constants behind it cannot drift apart
        /// without this moving with them.
        /// </summary>
        public const double OpeningSubscriptionUsdPerMonth =
            MarketModel.InitialPricePerMillionTokensUsd
            * (MonetizationCatalog.TokensPerSubscriberPerMonth / 1_000_000.0);

        /// <summary>
        /// How the company bills, and it opens on the one the rest of the game asks about.
        ///
        /// **It opened on `PayPerToken`, and nothing in the game ever asks for a token price.** The
        /// creator asks for a monthly subscription, the release card asks for a monthly
        /// subscription, and the release screen prints that figure back as what people pay. None of
        /// it reached the till: `RatePerMillionTokensUsd` reads the subscription only on a
        /// subscription, so a fresh company charged the market rate times one whatever the player
        /// set, and the only price control the game offers moved no number at all.
        ///
        /// Metered and free are still here and still reachable from the BUSINESS page. What changed
        /// is which one a company starts on, and it is now the one every other screen is written
        /// for.
        /// </summary>
        public PricingModel Model { get; set; } = PricingModel.Subscription;

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
            set => subscriptionPriceUsdPerMonth = Math.Clamp(
                SimUnits.Finite(value, OpeningSubscriptionUsdPerMonth), 0.0, 2000.0);
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

        /// <summary>
        /// Share of served tokens somebody is actually invoiced for.
        ///
        /// One line, and it lives here because two screens were each computing it from
        /// `FreeShareOfTokens` in their own private helper. Two copies of one subtraction is how
        /// a page ends up quoting a payer count the page beside it disagrees with.
        /// </summary>
        public double PaidShareOfTokens => Math.Clamp(1.0 - FreeShareOfTokens, 0.0, 1.0);

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
        public double RatePerMillionTokensUsd(double marketPricePerMillionUsd, GameDate date)
        {
            return Model switch
            {
                PricingModel.PayPerToken =>
                    Math.Max(0.0, marketPricePerMillionUsd) * PaidPriceMultiplier,
                PricingModel.Subscription =>
                    SubscriptionPriceUsdPerMonth
                    / (MonetizationCatalog.TokensPerSubscriberPerMonthOn(date) / 1_000_000.0),
                _ => 0.0
            };
        }

        /// <summary>The rate on the opening day, for the arithmetic that does not depend on one.</summary>
        public double RatePerMillionTokensUsd(double marketPricePerMillionUsd) =>
            RatePerMillionTokensUsd(marketPricePerMillionUsd, GameDate.Start);

        /// <summary>
        /// Where the company sits against the market, for the demand split. Free reads as very
        /// cheap rather than as free, because attention still has to be won from paid rivals.
        /// </summary>
        public double RelativePrice(double marketPricePerMillionUsd) =>
            RelativePrice(marketPricePerMillionUsd, GameDate.Start);

        public double RelativePrice(double marketPricePerMillionUsd, GameDate date)
        {
            if (marketPricePerMillionUsd <= 0.0)
            {
                return 1.0;
            }

            if (Model == PricingModel.FreeOnly)
            {
                return 0.05;
            }

            var rate = RatePerMillionTokensUsd(marketPricePerMillionUsd, date);
            return Math.Clamp(rate / marketPricePerMillionUsd, 0.05, MarketShareModel.MostRelativePrice);
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
