using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>What a company can offer beyond the salary.</summary>
    public enum StaffBenefit
    {
        None = 0,

        /// <summary>Private healthcare. The one everybody expects and nobody thanks you for.</summary>
        Healthcare = 1,

        /// <summary>Lunch, paid for. Small, cheap, and people notice it every single day.</summary>
        Meals = 2,

        /// <summary>A travel card. Worth most to the people who live furthest away.</summary>
        Transport = 3,

        /// <summary>A gym card. The classic Polish benefit, and it costs almost nothing.</summary>
        SportsCard = 4,

        /// <summary>Life and disability cover. Dull, expensive, and it is what people stay for.</summary>
        Insurance = 5,

        /// <summary>A learning budget. Cheap for what it buys, and it is why researchers stay.</summary>
        Learning = 6
    }

    /// <summary>One thing you can offer, its monthly cost per head, and what it buys.</summary>
    public readonly struct BenefitDefinition
    {
        public BenefitDefinition(StaffBenefit benefit, string nameKey, string noteKey,
            long monthlyCostPerHeadUsd, double loyaltyPoints)
        {
            Benefit = benefit;
            this.nameKey = nameKey;
            this.noteKey = noteKey;
            MonthlyCostPerHeadUsd = Math.Max(0L, monthlyCostPerHeadUsd);
            LoyaltyPoints = Math.Clamp(loyaltyPoints, 0.0, 30.0);
        }

        private readonly string nameKey;
        private readonly string noteKey;

        public StaffBenefit Benefit { get; }

        /// <summary>Resolved when read, so switching language mid-campaign renames it.</summary>
        public string DisplayName => Loc.T(nameKey);

        /// <summary>One sentence on who actually values it.</summary>
        public string Note => Loc.T(noteKey);

        /// <summary>Billed for every person on the payroll, every month, whether they use it or not.</summary>
        public long MonthlyCostPerHeadUsd { get; }

        /// <summary>How many loyalty points it adds. See <see cref="Simulation.Loyalty"/>.</summary>
        public double LoyaltyPoints { get; }

        /// <summary>Loyalty per thousand dollars a month. The only honest way to compare these.</summary>
        public double PointsPerThousand => MonthlyCostPerHeadUsd <= 0
            ? 0.0
            : LoyaltyPoints / (MonthlyCostPerHeadUsd / 1000.0);
    }

    /// <summary>
    /// The benefits a company can offer, and what each one is worth.
    ///
    /// **Deliberately not a ladder.** The cheap ones are not simply worse: a sports card buys more
    /// loyalty per dollar than anything else here and caps out low, while insurance is the most
    /// expensive line on the list and the one that moves the number most. A company that ticks
    /// everything is paying a great deal for the last few points, and a company that ticks nothing
    /// is fine right up until a rival starts calling its people.
    ///
    /// Every one of these is billed **per head, every month, for everybody**, including the people
    /// who never use it. That is what makes the decision a decision: the cost scales with hiring and
    /// the benefit does not scale with anything.
    /// </summary>
    public static class BenefitCatalog
    {
        /// <summary>Most loyalty the full set can add. Everything else has to come from elsewhere.</summary>
        public const double MaximumPoints = 26.0;

        private static readonly BenefitDefinition[] Entries =
        {
            // **Cost and points both climb, and no two are level on either.** The design check in
            // `LivingWorldTests` caught the first pass: a sports card at 45 for 3.0 points was
            // cheaper than a travel card and no worse, so nobody would ever have ticked the travel
            // card. Same rule the marketing channels are held to, for the same reason.
            //
            // What varies is value per dollar, which falls the whole way down the list: the cheap
            // ones are the efficient ones and the dear ones are the ones that move the number.
            new(StaffBenefit.SportsCard, "benefit.sports", "benefit.sports.note",
                monthlyCostPerHeadUsd: 45, loyaltyPoints: 3.0),

            new(StaffBenefit.Transport, "benefit.transport", "benefit.transport.note",
                monthlyCostPerHeadUsd: 110, loyaltyPoints: 4.0),

            new(StaffBenefit.Meals, "benefit.meals", "benefit.meals.note",
                monthlyCostPerHeadUsd: 180, loyaltyPoints: 5.0),

            new(StaffBenefit.Learning, "benefit.learning", "benefit.learning.note",
                monthlyCostPerHeadUsd: 260, loyaltyPoints: 5.5),

            new(StaffBenefit.Healthcare, "benefit.healthcare", "benefit.healthcare.note",
                monthlyCostPerHeadUsd: 320, loyaltyPoints: 6.0),

            new(StaffBenefit.Insurance, "benefit.insurance", "benefit.insurance.note",
                monthlyCostPerHeadUsd: 540, loyaltyPoints: 7.0)
        };

        /// <summary>Cheapest first, which is the order somebody actually adds them in.</summary>
        public static IReadOnlyList<BenefitDefinition> All => Entries;

        public static BenefitDefinition Get(StaffBenefit benefit)
        {
            foreach (var entry in Entries)
            {
                if (entry.Benefit == benefit)
                {
                    return entry;
                }
            }

            return Entries[0];
        }

        public static bool TryGet(StaffBenefit benefit, out BenefitDefinition definition)
        {
            foreach (var entry in Entries)
            {
                if (entry.Benefit == benefit)
                {
                    definition = entry;
                    return true;
                }
            }

            definition = default;
            return false;
        }

        /// <summary>What a set of benefits costs for one person, per month.</summary>
        public static long MonthlyCostPerHead(IEnumerable<StaffBenefit> chosen)
        {
            if (chosen == null)
            {
                return 0L;
            }

            var total = 0L;

            foreach (var benefit in chosen)
            {
                if (TryGet(benefit, out var definition))
                {
                    total += definition.MonthlyCostPerHeadUsd;
                }
            }

            return total;
        }

        /// <summary>
        /// Loyalty points a set of benefits is worth.
        ///
        /// A plain sum, capped. Diminishing returns are already in the prices: the last few points
        /// cost several times what the first ones did.
        /// </summary>
        public static double PointsFor(IEnumerable<StaffBenefit> chosen)
        {
            if (chosen == null)
            {
                return 0.0;
            }

            var total = 0.0;

            foreach (var benefit in chosen)
            {
                if (TryGet(benefit, out var definition))
                {
                    total += definition.LoyaltyPoints;
                }
            }

            return Math.Min(total, MaximumPoints);
        }
    }
}
