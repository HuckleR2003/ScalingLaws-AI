using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Debt products, cheapest first. Explicit values, saved, never renumbered.</summary>
    public enum LoanProduct
    {
        None = 0,
        BridgeFacility = 1,
        VentureDebt = 2,
        CorporateBond = 3,
        SovereignCompute = 4
    }

    /// <summary>
    /// One debt product.
    ///
    /// Debt is the other way to get money, and it is the opposite trade to equity. A round costs a
    /// slice of the company forever and never has to be paid back. A loan costs nothing permanent
    /// and has to be paid back on a schedule that does not care whether the quarter went well.
    ///
    /// The repayment multiple is total, not annual: borrow ten billion at 2.25 and eleven years
    /// later twenty two and a half billion has left the account, in daily instalments, whatever
    /// happened in between.
    /// </summary>
    public readonly struct LoanDefinition
    {
        public LoanDefinition(
            LoanProduct product,
            string displayName,
            string description,
            long principalUsd,
            double repaymentMultiple,
            int termDays,
            int graceDays,
            GameDate earliestDate,
            long requiredAnnualRevenueUsd,
            double requiredCapabilityRatio,
            ResearchNodeId requiredResearch,
            double reputationOnDefault)
        {
            Product = product;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? product.ToString() : displayName;
            Description = description ?? string.Empty;
            PrincipalUsd = Math.Clamp(principalUsd, 1_000_000L, 1_000_000_000_000L);
            RepaymentMultiple = Math.Clamp(SimUnits.Finite(repaymentMultiple, 1.2), 1.0, 5.0);
            TermDays = Math.Clamp(termDays, 90, 8000);
            GraceDays = Math.Clamp(graceDays, 0, TermDays - 1);
            EarliestDate = earliestDate;
            RequiredAnnualRevenueUsd = Math.Max(0L, requiredAnnualRevenueUsd);
            RequiredCapabilityRatio = Math.Clamp(SimUnits.Finite(requiredCapabilityRatio), 0.0, 1.5);
            RequiredResearch = requiredResearch;
            ReputationOnDefault = Math.Clamp(SimUnits.Finite(reputationOnDefault), 0.0, 1.0);
        }

        public LoanProduct Product { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public long PrincipalUsd { get; }

        /// <summary>Total repaid per dollar borrowed, across the whole term.</summary>
        public double RepaymentMultiple { get; }

        public int TermDays { get; }

        /// <summary>Days before the first instalment. Long enough to put the money to work.</summary>
        public int GraceDays { get; }

        public GameDate EarliestDate { get; }
        public long RequiredAnnualRevenueUsd { get; }
        public double RequiredCapabilityRatio { get; }
        public ResearchNodeId RequiredResearch { get; }

        /// <summary>Reputation lost if the company cannot service this. Sovereign debt hurts most.</summary>
        public double ReputationOnDefault { get; }

        public long TotalRepaymentUsd => SimUnits.ToDollars(PrincipalUsd * RepaymentMultiple);

        public long InterestUsd => TotalRepaymentUsd - PrincipalUsd;

        /// <summary>What leaves the account each day once the grace period is over.</summary>
        public long DailyInstalmentUsd =>
            SimUnits.ToDollars(TotalRepaymentUsd / (double)Math.Max(1, TermDays - GraceDays));

        public override string ToString() =>
            $"{DisplayName}: ${PrincipalUsd:N0} at {RepaymentMultiple:0.00}x over {TermDays} days";
    }

    /// <summary>
    /// The ONE debt library.
    ///
    /// The ladder runs from a short expensive bridge to the sovereign compute programme, which is
    /// the largest single sum in the game and the one that can end a campaign on its own. Every
    /// product is non-dilutive, which is the whole reason to take one, and every product has to be
    /// serviced daily, which is the whole reason not to.
    /// </summary>
    public static class LoanCatalog
    {
        public const string CatalogVersion = "2026.08.02";

        /// <summary>Loans that can be open at once. Past this nobody will lend to the company.</summary>
        public const int MaximumConcurrentLoans = 3;

        private static readonly LoanDefinition[] Entries =
        {
            new(LoanProduct.BridgeFacility, "Bridge facility",
                "Six months of runway from a lender who expects a round to close behind it. Small, fast, "
                + "and priced as if the round might not.",
                principalUsd: 15_000_000,
                repaymentMultiple: 1.22,
                termDays: 540,
                graceDays: 60,
                earliestDate: GameDate.Start,
                requiredAnnualRevenueUsd: 0,
                requiredCapabilityRatio: 0.45,
                requiredResearch: ResearchNodeId.None,
                reputationOnDefault: 0.06),

            new(LoanProduct.VentureDebt, "Venture debt",
                "Growth capital that does not touch the cap table. The lender wants to see revenue, "
                + "and once the schedule starts it does not pause for a bad quarter.",
                principalUsd: 120_000_000,
                repaymentMultiple: 1.45,
                termDays: 1_460,
                graceDays: 180,
                earliestDate: GameDate.Start,
                requiredAnnualRevenueUsd: 40_000_000,
                requiredCapabilityRatio: 0.60,
                requiredResearch: ResearchNodeId.ScalingLaws,
                reputationOnDefault: 0.12),

            new(LoanProduct.CorporateBond, "Corporate bond",
                "A real issuance against a real balance sheet. Cheap money by the standards of this "
                + "industry, and the size of it means a missed schedule is a public event.",
                principalUsd: 900_000_000,
                repaymentMultiple: 1.62,
                termDays: 2_555,
                graceDays: 365,
                earliestDate: GameDate.FromCalendar(2024, 1, 1),
                requiredAnnualRevenueUsd: 400_000_000,
                requiredCapabilityRatio: 0.75,
                requiredResearch: ResearchNodeId.DatacenterProgramme,
                reputationOnDefault: 0.20),

            // The largest sum in the game and the only one that can end a campaign by itself.
            new(LoanProduct.SovereignCompute, "Sovereign compute programme",
                "A state decides that domestic frontier capability is infrastructure and writes the "
                + "cheque to prove it. Ten billion now, twenty two and a half billion back over eleven "
                + "years, and a government that will not renegotiate. Nothing else in the game moves "
                + "this much money, and nothing else fails this loudly.",
                principalUsd: 10_000_000_000,
                repaymentMultiple: 2.25,
                termDays: 4_015,
                graceDays: 730,
                earliestDate: GameDate.FromCalendar(2026, 1, 1),
                requiredAnnualRevenueUsd: 2_000_000_000,
                requiredCapabilityRatio: 0.90,
                requiredResearch: ResearchNodeId.RecursiveSelfImprovement,
                reputationOnDefault: 0.35)
        };

        private static readonly Dictionary<LoanProduct, LoanDefinition> ByProduct = BuildIndex();

        public static IReadOnlyList<LoanDefinition> All => Entries;

        public static LoanDefinition Get(LoanProduct product)
        {
            if (!ByProduct.TryGetValue(product, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(product), product, "Unknown loan product.");
            }

            return definition;
        }

        public static bool TryGet(LoanProduct product, out LoanDefinition definition) =>
            ByProduct.TryGetValue(product, out definition);

        private static Dictionary<LoanProduct, LoanDefinition> BuildIndex()
        {
            var index = new Dictionary<LoanProduct, LoanDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Product] = entry;
            }

            return index;
        }
    }
}
