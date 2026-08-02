using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>How the company charges. Explicit values, saved, never renumbered.</summary>
    public enum PricingModel
    {
        /// <summary>Metered, at whatever the market is paying that day multiplied by your position.</summary>
        PayPerToken = 0,

        /// <summary>A monthly fee you set. Decoupled from the market price, for better and worse.</summary>
        Subscription = 1,

        /// <summary>Nobody pays. Reach is enormous, revenue is zero, the serving bill is not.</summary>
        FreeOnly = 2
    }

    /// <summary>What a marketing programme is aimed at.</summary>
    public enum CampaignKind
    {
        None = 0,

        /// <summary>The company. Slow, compounding, and it survives a model going out of date.</summary>
        Company = 1,

        /// <summary>The current flagship. Fast, loud, and it stops working the day you stop paying.</summary>
        Model = 2
    }

    /// <summary>One marketing programme the player can run.</summary>
    public readonly struct CampaignDefinition
    {
        public CampaignDefinition(
            string displayName,
            CampaignKind kind,
            string description,
            long dailyBudgetUsd,
            double effectPerDay,
            GameDate earliestDate)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Campaign" : displayName;
            Kind = kind;
            Description = description ?? string.Empty;
            DailyBudgetUsd = Math.Clamp(dailyBudgetUsd, 0L, 500_000_000L);
            EffectPerDay = Math.Clamp(SimUnits.Finite(effectPerDay), 0.0, 0.05);
            EarliestDate = earliestDate;
        }

        public string DisplayName { get; }
        public CampaignKind Kind { get; }
        public string Description { get; }
        public long DailyBudgetUsd { get; }

        /// <summary>Brand added per day at full effect, before diminishing returns.</summary>
        public double EffectPerDay { get; }

        public GameDate EarliestDate { get; }

        public long MonthlyBudgetUsd => SimUnits.ToDollars(DailyBudgetUsd * 30.4375);

        public override string ToString() => $"{DisplayName} (${DailyBudgetUsd:N0}/day)";
    }

    /// <summary>
    /// The ONE monetization and marketing library.
    ///
    /// Two decisions live here and they pull against each other. How you charge decides what a
    /// served token is worth, and how generous the free tier is decides how many served tokens are
    /// worth nothing at all. A free tier is the cheapest distribution in the game and the most
    /// expensive infrastructure, and the point at which one turns into the other is not signposted.
    /// </summary>
    public static class MonetizationCatalog
    {
        public const string CatalogVersion = "2026.08.03";

        /// <summary>Tokens a paying account gets through in a month. Turns a fee into a rate.</summary>
        public const double TokensPerSubscriberPerMonth = 4_000_000.0;

        /// <summary>Free tokens per user per day at which generosity reads as maximum.</summary>
        public const double GenerousFreeTierTokensPerDay = 250_000.0;

        /// <summary>Extra reach a maximally generous free tier buys, as a share multiplier.</summary>
        public const double FreeTierReachBonus = 0.40;

        /// <summary>Share of served tokens that earn nothing when no free tier is offered.</summary>
        public const double BaseFreeShare = 0.08;

        /// <summary>Additional share that earns nothing at maximum generosity.</summary>
        public const double MaximumExtraFreeShare = 0.62;

        /// <summary>Diminishing returns point for marketing spend, in dollars per day.</summary>
        public const double MarketingSaturationUsdPerDay = 2_500_000.0;

        /// <summary>Brand a model campaign loses per day once the spending stops.</summary>
        public const double ModelAwarenessDecayPerDay = 0.010;

        private static readonly CampaignDefinition[] Entries =
        {
            new("Developer relations", CampaignKind.Company,
                "Conference talks, sample code and someone who answers the forum. Slow, cheap, and it "
                + "outlives whichever model is current.",
                dailyBudgetUsd: 8_000, effectPerDay: 0.00055, earliestDate: GameDate.Start),

            new("Trade press", CampaignKind.Company,
                "Briefings and benchmarks in the places procurement teams read before they shortlist.",
                dailyBudgetUsd: 45_000, effectPerDay: 0.00150, earliestDate: GameDate.Start),

            new("Brand campaign", CampaignKind.Company,
                "The company name in places that have nothing to do with software. Expensive, and the "
                + "only thing that reaches people who will never read a benchmark.",
                dailyBudgetUsd: 400_000, effectPerDay: 0.00380,
                earliestDate: GameDate.FromCalendar(2023, 6, 1)),

            new("Launch push", CampaignKind.Model,
                "Everything pointed at the model that just shipped. Works immediately and stops working "
                + "the day the invoices stop.",
                dailyBudgetUsd: 30_000, effectPerDay: 0.00260, earliestDate: GameDate.Start),

            new("Performance advertising", CampaignKind.Model,
                "Bought attention, measured daily. Reliable, unromantic, and it never compounds.",
                dailyBudgetUsd: 180_000, effectPerDay: 0.00700, earliestDate: GameDate.Start),

            new("Everywhere at once", CampaignKind.Model,
                "The saturation campaign. Enormous reach while it runs and nothing left behind when it "
                + "does not.",
                dailyBudgetUsd: 1_400_000, effectPerDay: 0.01800,
                earliestDate: GameDate.FromCalendar(2024, 1, 1))
        };

        public static IReadOnlyList<CampaignDefinition> All => Entries;

        public static IEnumerable<CampaignDefinition> OfKind(CampaignKind kind)
        {
            foreach (var entry in Entries)
            {
                if (entry.Kind == kind)
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Effect of a day of spending, with diminishing returns. Doubling a budget past the
        /// saturation point buys well under double the attention.
        /// </summary>
        public static double EffectFor(long dailyBudgetUsd, double effectPerDay)
        {
            var budget = Math.Max(0.0, dailyBudgetUsd);
            if (budget <= 0.0)
            {
                return 0.0;
            }

            var saturating = budget / (1.0 + budget / MarketingSaturationUsdPerDay);
            return effectPerDay * saturating / Math.Max(1.0, budget);
        }

        public static string PricingName(PricingModel model) => model switch
        {
            PricingModel.PayPerToken => "Pay per token",
            PricingModel.Subscription => "Subscription",
            _ => "Free"
        };
    }
}
