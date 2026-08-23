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
        SovereignCompute = 4,

        /// <summary>Secured on the fleet. Added after the funding screen was rebuilt.</summary>
        EquipmentFinance = 5
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
            double reputationOnDefault,
            double monthlyCommissionRate)
        {
            Product = product;

            // The English originals the phrase book was built from. Not stored, for the reason the
            // traits and the research nodes are not: Loc holds both languages and falls back to
            // English itself, so a copy here would only be somewhere for the two to disagree.
            _ = displayName;
            _ = description;
            PrincipalUsd = Math.Clamp(principalUsd, 1_000_000L, 1_000_000_000_000L);
            RepaymentMultiple = Math.Clamp(SimUnits.Finite(repaymentMultiple, 1.2), 1.0, 5.0);
            TermDays = Math.Clamp(termDays, 90, 8000);
            GraceDays = Math.Clamp(graceDays, 0, TermDays - 1);
            EarliestDate = earliestDate;
            RequiredAnnualRevenueUsd = Math.Max(0L, requiredAnnualRevenueUsd);
            RequiredCapabilityRatio = Math.Clamp(SimUnits.Finite(requiredCapabilityRatio), 0.0, 1.5);
            RequiredResearch = requiredResearch;
            ReputationOnDefault = Math.Clamp(SimUnits.Finite(reputationOnDefault), 0.0, 1.0);
            MonthlyCommissionRate = Math.Clamp(SimUnits.Finite(monthlyCommissionRate), 0.0, 0.05);
        }

        public LoanProduct Product { get; }

        /// <summary>The product's name, in whatever language the player reads.</summary>
        public string DisplayName => Loc.T($"loan.{KeyFor(Product)}.name");

        public string Description => Loc.T($"loan.{KeyFor(Product)}.desc");

        private static string KeyFor(LoanProduct product) => product switch
        {
            LoanProduct.BridgeFacility => "bridge",
            LoanProduct.EquipmentFinance => "equipment",
            LoanProduct.VentureDebt => "venture",
            LoanProduct.CorporateBond => "bond",
            _ => "sovereign"
        };
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

        /// <summary>
        /// The arrangement fee, as a share of the principal, charged every month the facility is open.
        ///
        /// **It pays nothing off.** The instalment reduces the balance and stops when the loan is
        /// settled; this is rent on the facility itself and it runs for as long as the loan does.
        /// Two costs rather than one is the point: it makes borrowing more than you need expensive
        /// instead of merely large, which a single repayment multiple cannot express.
        ///
        /// Charged against the original principal rather than the outstanding balance, because that
        /// is the number on the screen and a fee that quietly shrinks is a fee nobody can plan for.
        /// </summary>
        public double MonthlyCommissionRate { get; }

        /// <summary>What the fee costs each month, in dollars.</summary>
        public long MonthlyCommissionUsd =>
            SimUnits.ToDollars(PrincipalUsd * MonthlyCommissionRate);

        /// <summary>The instalment, monthly, which is how a player thinks about a repayment.</summary>
        public long MonthlyInstalmentUsd =>
            SimUnits.ToDollars(DailyInstalmentUsd * 30.4375);

        /// <summary>
        /// Everything the facility costs across its life, fee included.
        ///
        /// The headline multiple is only half the price now, so anything comparing two products has
        /// to compare this instead.
        /// </summary>
        public long TotalCostUsd =>
            TotalRepaymentUsd + SimUnits.ToDollars(
                PrincipalUsd * MonthlyCommissionRate * (TermDays / 30.4375));

        /// <summary>What comes back per dollar borrowed once the fee is counted.</summary>
        public double EffectiveMultiple =>
            PrincipalUsd <= 0L ? 1.0 : TotalCostUsd / (double)PrincipalUsd;

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
                repaymentMultiple: 1.17,
                termDays: 540,
                graceDays: 60,
                earliestDate: GameDate.Start,
                requiredAnnualRevenueUsd: 0,
                requiredCapabilityRatio: 0.45,
                requiredResearch: ResearchNodeId.None,
                reputationOnDefault: 0.06,
                monthlyCommissionRate: 0.0030),

            new(LoanProduct.VentureDebt, "Venture debt",
                "Growth capital that does not touch the cap table. The lender wants to see revenue, "
                + "and once the schedule starts it does not pause for a bad quarter.",
                principalUsd: 120_000_000,
                repaymentMultiple: 1.32,
                termDays: 1_460,
                graceDays: 180,
                earliestDate: GameDate.Start,
                requiredAnnualRevenueUsd: 40_000_000,
                requiredCapabilityRatio: 0.60,
                requiredResearch: ResearchNodeId.ScalingLaws,
                reputationOnDefault: 0.12,
                monthlyCommissionRate: 0.0030),

            new(LoanProduct.CorporateBond, "Corporate bond",
                "A real issuance against a real balance sheet. Cheap money by the standards of this "
                + "industry, and the size of it means a missed schedule is a public event.",
                principalUsd: 900_000_000,
                repaymentMultiple: 1.44,
                termDays: 2_555,
                graceDays: 365,
                earliestDate: GameDate.FromCalendar(2024, 1, 1),
                requiredAnnualRevenueUsd: 400_000_000,
                requiredCapabilityRatio: 0.75,
                requiredResearch: ResearchNodeId.DatacenterProgramme,
                reputationOnDefault: 0.20,
                monthlyCommissionRate: 0.0025),

            // The largest sum in the game and the only one that can end a campaign by itself.
            new(LoanProduct.SovereignCompute, "Sovereign compute programme",
                "A state decides that domestic frontier capability is infrastructure and writes the "
                + "cheque to prove it. Ten billion now, twenty two and a half billion back over eleven "
                + "years, and a government that will not renegotiate. Nothing else in the game moves "
                + "this much money, and nothing else fails this loudly.",
                principalUsd: 10_000_000_000,
                repaymentMultiple: 2.40,
                termDays: 4_015,
                graceDays: 730,
                earliestDate: GameDate.FromCalendar(2026, 1, 1),
                requiredAnnualRevenueUsd: 2_000_000_000,
                requiredCapabilityRatio: 0.90,
                requiredResearch: ResearchNodeId.RecursiveSelfImprovement,
                reputationOnDefault: 0.35,
                monthlyCommissionRate: 0.0035),

            // A second commercial option on day one, so the opening is a choice rather than one
            // button. Secured against the fleet, which is the only collateral a young lab has, and
            // priced accordingly: the cheapest money in the game and the smallest sum.
            new(LoanProduct.EquipmentFinance, "Equipment finance",
                "Borrowed against the accelerators rather than against the company. The lender does "
                + "not care what the models do, only what the hardware would fetch, so it is cheap "
                + "and it is small. Miss the schedule and the fleet is what they take.",
                principalUsd: 40_000_000,
                repaymentMultiple: 1.11,
                termDays: 1_095,
                graceDays: 90,
                earliestDate: GameDate.Start,
                requiredAnnualRevenueUsd: 0,
                requiredCapabilityRatio: 0.30,
                requiredResearch: ResearchNodeId.None,
                reputationOnDefault: 0.09,
                monthlyCommissionRate: 0.0018)
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
