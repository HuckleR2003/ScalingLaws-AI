using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>How attached somebody is to the company they work for, as a band.</summary>
    public enum LoyaltyBand
    {
        /// <summary>Will take the first call they get.</summary>
        Loose = 0,

        /// <summary>Listening, if the number is good.</summary>
        Open = 1,

        /// <summary>Settled. It would take a serious offer.</summary>
        Settled = 2,

        /// <summary>Not going anywhere, and they will say so out loud.</summary>
        Committed = 3
    }

    /// <summary>
    /// How likely somebody is to stay, from 0 to 100.
    ///
    /// **Derived, never stored.** Loyalty is a reading of three facts the company already records:
    /// how long the person has been here, what it offers them beyond the salary, and whether the
    /// salary itself is above or below what the job pays elsewhere. Storing it would mean a fourth
    /// number that can disagree with those three, and this project has already been caught five
    /// times by state that looked derived and was not. This one genuinely is.
    ///
    /// The same function reads a rival's staff, which is the whole reason poaching can work: their
    /// people are loyal for the same reasons yours are, and the ones who have been somewhere two
    /// months are the ones who answer the phone.
    /// </summary>
    public static class Loyalty
    {
        /// <summary>Where somebody starts on their first day with nothing else offered.</summary>
        public const double Base = 45.0;

        /// <summary>Most that time alone can add, reached at <see cref="TenureYearsToFull"/>.</summary>
        public const double TenureCeiling = 30.0;

        /// <summary>Years to reach the tenure ceiling. Long, because the payoff should be slow.</summary>
        public const double TenureYearsToFull = 5.0;

        /// <summary>How far pay can move it either way, at half or double the going rate.</summary>
        public const double PaySwing = 16.0;

        /// <summary>
        /// How much settling-in one month of somebody's salary buys, in days.
        ///
        /// Denominated in their own salary rather than in dollars, so the same gesture costs more
        /// for a senior and is worth the same to both. A flat figure would make bonuses a way to
        /// buy junior loyalty for nothing.
        /// </summary>
        public const double BonusDaysPerMonthOfSalary = 55.0;

        // ---- the bands ---------------------------------------------------------------------------
        //
        // Named rather than numbered on screen, because "Loyalty 61" tells a player nothing and
        // "Settled" tells them whether to bother making an offer.

        public const double OpenAbove = 40.0;
        public const double SettledAbove = 62.0;
        public const double CommittedAbove = 80.0;

        /// <summary>
        /// Reads one person's loyalty.
        ///
        /// <paramref name="marketSalaryUsd"/> is what this role and skill are paid elsewhere. Zero
        /// means unknown, and the pay term simply drops out rather than being guessed at.
        /// </summary>
        public static double For(Hire hire, GameDate today, double benefitPoints,
            long marketSalaryUsd) =>
            For(hire, today, benefitPoints, marketSalaryUsd, null);

        /// <summary>
        /// The same reading, knowing what the company actually offers.
        ///
        /// **What a benefit is worth depends on who is receiving it.** Everybody values a gym card
        /// a little; the person who asked for one values it a great deal, and the person who asked
        /// and did not get one notices every month. That difference is the reason the person panel
        /// is worth opening, and it is why the same payroll buys more loyalty at one company than
        /// at another.
        ///
        /// <paramref name="offered"/> may be null, which means the caller does not know or does not
        /// care, and then this behaves exactly as it always did.
        /// </summary>
        public static double For(Hire hire, GameDate today, double benefitPoints,
            long marketSalaryUsd, IReadOnlyCollection<StaffBenefit> offered)
        {
            // A bonus counts as time served. Money buys the settling-in that months would have
            // bought, which is the only shape that lets a payment matter without letting it buy
            // somebody's loyalty outright: it is capped, and past the cap only time works.
            var days = Math.Max(0, today.DayIndex - hire.StartedOn.DayIndex) + hire.BonusDays;
            var years = days / 365.0;

            // Saturating rather than linear. The difference between six months and eighteen is
            // most of the effect; the difference between four years and five is almost none.
            var tenure = TenureCeiling * (1.0 - Math.Exp(-years / (TenureYearsToFull / 3.0)));

            // **A quarter faster, on the tenure term rather than on the total.** Somebody getting
            // what they asked for settles in sooner; they do not walk in already attached. Applied
            // here so the effect grows with the months, which is what "settles in" means.
            if (offered != null && StaffExpectations.IsLookedAfter(hire, offered))
            {
                tenure *= 1.0 + StaffExpectations.MetTenureBonus;
            }

            var pay = 0.0;

            if (marketSalaryUsd > 0)
            {
                // Log ratio, so paying double is liked exactly as much as paying half is resented.
                // A linear ratio makes underpaying almost free and overpaying enormously expensive,
                // which is the wrong shape for a thing people compare against their friends.
                var ratio = hire.SalaryPerYearUsd / (double)marketSalaryUsd;
                var swing = Math.Log(Math.Clamp(ratio, 0.5, 2.0)) / Math.Log(2.0);
                pay = PaySwing * swing;
            }

            var total = Base + tenure + Math.Clamp(benefitPoints, 0.0, BenefitCatalog.MaximumPoints)
                + pay;

            // And what they asked for and did not get, which is a drag rather than an absence.
            if (offered != null)
            {
                total += StaffExpectations.PointsFor(hire, offered);
            }

            return Math.Clamp(SimUnits.Finite(total, Base), 0.0, 100.0);
        }

        /// <summary>The same reading for a person on a rival's payroll.</summary>
        public static double For(RivalStaffMember member, GameDate today) => member.Loyalty(today);

        public static LoyaltyBand BandFor(double loyalty) =>
            loyalty >= CommittedAbove ? LoyaltyBand.Committed
            : loyalty >= SettledAbove ? LoyaltyBand.Settled
            : loyalty >= OpenAbove ? LoyaltyBand.Open
            : LoyaltyBand.Loose;

        /// <summary>The band's own name, from the phrase book.</summary>
        public static string NameOf(LoyaltyBand band) => band switch
        {
            LoyaltyBand.Committed => Loc.T("loyalty.committed"),
            LoyaltyBand.Settled => Loc.T("loyalty.settled"),
            LoyaltyBand.Open => Loc.T("loyalty.open"),
            _ => Loc.T("loyalty.loose")
        };

        /// <summary>
        /// Everybody's loyalty, averaged, which is the one figure worth putting on a screen.
        ///
        /// An empty payroll returns zero rather than the base: a company with nobody in it does not
        /// have happy staff, it has no staff.
        /// </summary>
        public static double Average(IReadOnlyList<Hire> hires, GameDate today, double benefitPoints)
        {
            if (hires == null || hires.Count == 0)
            {
                return 0.0;
            }

            var total = 0.0;

            foreach (var hire in hires)
            {
                total += For(hire, today, benefitPoints,
                    StaffCatalog.Get(hire.Role).SalaryPerYearUsd(hire.Skill));
            }

            return total / hires.Count;
        }
    }
}
