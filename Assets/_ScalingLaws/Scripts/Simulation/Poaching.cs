using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>What happened when you called somebody at another company.</summary>
    public enum PoachOutcome
    {
        /// <summary>They took it.</summary>
        Accepted = 0,

        /// <summary>They said no and left it there.</summary>
        Refused = 1,

        /// <summary>They said no and told their employer who had called.</summary>
        Reported = 2,

        /// <summary>The offer could not be made at all. See the reason.</summary>
        Blocked = 3
    }

    /// <summary>
    /// The arithmetic of taking somebody off a rival's payroll.
    ///
    /// **The whole mechanic is that you are bidding against a number you cannot see.** Loyalty is
    /// shown as a band rather than a figure, so the decision is "is this person settled enough that
    /// I need to overpay, and can I afford to be wrong", which is exactly the shape of the real
    /// thing.
    ///
    /// Pure arithmetic in its own file rather than four more methods on the simulation, because the
    /// interesting part is the curve and a curve buried in a five hundred line class is a curve
    /// nobody tunes.
    /// </summary>
    public static class Poaching
    {
        /// <summary>Bonus that makes an average person think seriously, as a share of their salary.</summary>
        public const double SeriousBonusShare = 0.5;

        /// <summary>Chance a refusal is reported, at the very top of the loyalty scale.</summary>
        public const double ReportChanceAtCommitted = 0.55;

        /// <summary>And at the bottom, where somebody is already looking.</summary>
        public const double ReportChanceAtLoose = 0.03;

        /// <summary>What a successful raid costs the relationship with that lab.</summary>
        public const double RelationCostOfSuccess = -14.0;

        /// <summary>And what being reported costs, which is worse: they found out and got nothing.</summary>
        public const double RelationCostOfReport = -9.0;

        /// <summary>What a curt reply to their call costs on top.</summary>
        public const double RelationCostOfHangingUp = -6.0;

        /// <summary>And what an apology buys back. Less than the insult cost, because it is words.</summary>
        public const double RelationCostOfApology = -2.0;

        /// <summary>
        /// What this person is paid where they are, per year.
        ///
        /// Derived from the rating on the same curve the player's own payroll uses, so an offer is
        /// comparable to what the company already spends rather than to an invented figure.
        /// </summary>
        public static long SalaryAt(RivalStaffMember member)
        {
            var role = RoleFor(member.Position);
            return StaffCatalog.Get(role).SalaryPerYearUsd(member.SkillBand);
        }

        /// <summary>
        /// The chance they say yes.
        ///
        /// Two terms and they pull against each other: how settled they are, and how much money is
        /// on the table. A committed person is not unbuyable, they are expensive, which is the only
        /// version of this that leaves the player a decision at every loyalty band.
        /// </summary>
        public static double AcceptanceChance(RivalStaffMember member, GameDate today,
            long signingBonusUsd)
        {
            var loyalty = member.Loyalty(today);
            var salary = Math.Max(1L, SalaryAt(member));

            // A bonus worth half a year's salary is the reference. Square rooted, so the first
            // fifty thousand moves the number far more than the fifth fifty thousand.
            var offer = Math.Sqrt(
                Math.Max(0.0, signingBonusUsd / (salary * SeriousBonusShare)));

            // Loyalty is the wall. At the base of the scale somebody is nearly gettable for nothing;
            // at the top, half a year's salary is not enough on its own.
            var resistance = Math.Clamp(loyalty / 100.0, 0.0, 1.0);
            var chance = (0.9 - resistance * 0.85) + offer * 0.42 * (1.0 - resistance * 0.5);

            return Math.Clamp(SimUnits.Finite(chance), 0.01, 0.95);
        }

        /// <summary>
        /// The chance a refusal gets reported to their employer.
        ///
        /// **Rises with loyalty, which is what makes the warning worth reading.** Aiming at
        /// somebody who is already looking is quiet and cheap; aiming at a lifer is how a company
        /// finds out you have been calling its people.
        /// </summary>
        public static double ReportChance(RivalStaffMember member, GameDate today)
        {
            var loyalty = Math.Clamp(member.Loyalty(today) / 100.0, 0.0, 1.0);

            return Math.Clamp(
                ReportChanceAtLoose + (ReportChanceAtCommitted - ReportChanceAtLoose)
                    * Math.Pow(loyalty, 2.2),
                0.0,
                1.0);
        }

        /// <summary>
        /// Which payroll role a discipline maps onto.
        ///
        /// Written out rather than cast, because the two enums are different scales that happen to
        /// overlap and a cast between them would be correct today and silently wrong the first time
        /// either gains a member.
        /// </summary>
        public static StaffRole RoleFor(PlayerSkill position) => position switch
        {
            PlayerSkill.Development => StaffRole.ResearchScientist,
            PlayerSkill.Concept => StaffRole.ResearchScientist,
            PlayerSkill.Software => StaffRole.InfrastructureEngineer,
            PlayerSkill.DataEngineering => StaffRole.DataEngineer,
            PlayerSkill.Safety => StaffRole.SafetyEngineer,
            _ => StaffRole.InfrastructureEngineer
        };
    }
}
