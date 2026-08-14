using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>How the company decides what to spend on research each month.</summary>
    public enum ResearchFundingMode
    {
        /// <summary>A figure the player sets and the company pays whatever else is happening.</summary>
        Fixed = 0,

        /// <summary>A share of what came in. Nothing earned, nothing spent.</summary>
        RevenueShare = 1
    }

    /// <summary>
    /// Where research points come from, and what they cost.
    ///
    /// Two sources on purpose, and they answer different questions. **Work** produces points because
    /// a lab learns by building: a run in flight teaches you things a cheque cannot. **Money** buys
    /// points because a lab can also hire the answer. Neither alone is enough to keep up, and that is
    /// the decision the system exists to create.
    ///
    /// Funding has heavy diminishing returns. Doubling the budget does not double the discovery, or
    /// research becomes a second cash sink with a linear payoff and the only question left is whether
    /// you can afford it.
    /// </summary>
    public static class ResearchBudget
    {
        /// <summary>Points a day from the founder working on a run. The baseline everything else scales from.</summary>
        public const double PointsPerDayTraining = 6.0;

        /// <summary>Points a day from an upgrade programme. Narrower work, so less of it.</summary>
        public const double PointsPerDayUpgrading = 3.5;

        /// <summary>
        /// What one member of staff adds against the founder's own rate.
        ///
        /// Sixty percent less, as agreed. They are doing the work rather than deciding it, and a lab
        /// where hiring is a straight multiplier on discovery has no reason to ever stop hiring.
        /// </summary>
        public const double StaffShare = 0.40;

        /// <summary>Diminishing returns bite hard past this much a month.</summary>
        public const double FundingKneeUsd = 250_000.0;

        /// <summary>Points a month at the knee. Above it the curve flattens.</summary>
        public const double PointsAtKnee = 90.0;

        /// <summary>The smallest budget the slider offers. Enough to feel like nothing.</summary>
        public const long MinimumMonthlyUsd = 1_000;

        public const long MaximumMonthlyUsd = 5_000_000;

        /// <summary>
        /// Points a month bought by money, on a square root curve through the knee.
        ///
        /// At the knee a quarter of a million buys ninety points. Four times the money buys twice the
        /// points, not four times, which is what stops the richest company simply purchasing the tree.
        /// </summary>
        public static double PointsFromFunding(double monthlyUsd)
        {
            var spend = Math.Max(0.0, SimUnits.Finite(monthlyUsd));
            if (spend <= 0.0)
            {
                return 0.0;
            }

            return PointsAtKnee * Math.Sqrt(spend / FundingKneeUsd);
        }

        /// <summary>
        /// Points earned today by people doing the work.
        ///
        /// Nothing is earned by a company that is not building anything. Research funding still runs,
        /// but the bench learns nothing while it is idle, which is the pressure that stops a player
        /// parking the company and waiting for the tree to fill itself in.
        /// </summary>
        public static double PointsFromWork(bool training, bool upgrading, int staffCount,
            double researchDepthMultiplier)
        {
            var founder = 0.0;
            if (training)
            {
                founder += PointsPerDayTraining;
            }

            if (upgrading)
            {
                founder += PointsPerDayUpgrading;
            }

            if (founder <= 0.0)
            {
                return 0.0;
            }

            // Staff multiply the work that is happening rather than creating work of their own.
            var people = 1.0 + Math.Max(0, staffCount) * StaffShare;
            var depth = Math.Clamp(SimUnits.Finite(researchDepthMultiplier, 1.0), 0.25, 4.0);

            return founder * people * depth;
        }

        /// <summary>What a fixed budget or a revenue share comes to this month.</summary>
        public static long MonthlyBudgetUsd(ResearchFundingMode mode, long fixedMonthlyUsd,
            double revenueShare, long monthlyRevenueUsd)
        {
            if (mode == ResearchFundingMode.Fixed)
            {
                return Math.Clamp(fixedMonthlyUsd, 0L, MaximumMonthlyUsd);
            }

            var share = Math.Clamp(SimUnits.Finite(revenueShare), 0.0, 1.0);
            var take = (long)Math.Round(Math.Max(0L, monthlyRevenueUsd) * share);

            return Math.Clamp(take, 0L, MaximumMonthlyUsd);
        }

        /// <summary>
        /// What a node costs in points, derived from the cash figure the catalog already carries.
        ///
        /// Derived rather than hand written into twenty one entries, so the two can never disagree and
        /// a new node needs one number rather than two. The split the author asked for: a little cash
        /// and a lot of points.
        /// </summary>
        public static double PointCostOf(long catalogCostUsd) =>
            Math.Max(1.0, Math.Round(Math.Max(0L, catalogCostUsd) / 9_000.0));

        /// <summary>The cash a node still asks for, which is a fraction of what it used to.</summary>
        public static long CashCostOf(long catalogCostUsd) =>
            (long)Math.Round(Math.Max(0L, catalogCostUsd) * 0.15);
    }
}
