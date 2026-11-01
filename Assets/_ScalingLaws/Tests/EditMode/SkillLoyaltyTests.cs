using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Teamwork's second effect: the people on the payroll are more attached to the company.
    ///
    /// **Asked for on 2026-10-25, and the card already declares it**, which is why this is a test
    /// rather than an option. Teamwork's first effect is the staff saturation curve, which is real
    /// and is also the most abstract number in `SkillSet`: it decides how late a growing team stops
    /// paying off, and a player cannot see it at any single headcount.
    ///
    /// Two of the four tests here are about where the effect must *not* reach. A founder skill that
    /// made other companies' people harder to poach, or that outweighed what the company pays,
    /// would be the same number pointing the wrong way.
    /// </summary>
    public sealed class SkillLoyaltyTests
    {
        private static readonly GameDate Today = GameDate.FromCalendar(2024, 1, 1);

        private static CompanyState Company(int teamwork)
        {
            var state = new CompanyState("Prometheus AI", 909)
            {
                Date = Today,
                CashUsd = 400_000_000
            };

            state.Skills.SetLevel(PlayerSkill.Teamwork, teamwork);

            // Remote, because `StaffRoster.Add` checks a seated hire against the lease and the
            // garage has no desks. The note about a fixture hanging on exactly that is older than
            // this file.
            state.Staff.Add(new Hire(
                StaffRole.ResearchScientist, 4, GameDate.FromCalendar(2023, 1, 1),
                "Ada", PlayerSkill.Development, HireSource.Remote, 0.0));

            return state;
        }

        [Test]
        public void AFounderWhoIsGoodWithPeopleKeepsThemBetter()
        {
            var poor = Company(0);
            var strong = Company(100);

            var low = poor.LoyaltyOf(poor.Staff.Hires[0]);
            var high = strong.LoyaltyOf(strong.Staff.Hires[0]);

            Assert.That(high, Is.GreaterThan(low),
                "The same person, on the same wage, with the same benefits, is no more attached to "
                + "a company run by somebody good with people (" + high.ToString("0.0")
                + " against " + low.ToString("0.0") + "). The skill card says Teamwork moves staff "
                + "loyalty, so either the wiring is missing or the card is lying.");
        }

        /// <summary>
        /// **The reading on the card is the reading the game uses.** `SkillEffectCatalog` says this
        /// skill raises loyalty at a high setting and lowers it at a low one, and a card that says
        /// so while the simulation does nothing is worse than a card that says nothing.
        /// </summary>
        [Test]
        public void TheCardAndTheSimulationAgreeAboutTheDirection()
        {
            var declared = 0;

            foreach (var point in SkillEffectCatalog.For(PlayerSkill.Teamwork))
            {
                if (point.Key == "skilleffect.loyalty")
                {
                    declared = point.AtHigh;
                }
            }

            Assert.That(declared, Is.GreaterThan(0),
                "The Teamwork card no longer declares a loyalty effect, so this test is guarding "
                + "nothing. Either put the point back or delete this test deliberately.");

            var poor = Company(0);
            var neutral = Company(PlayerSkillLimits.StartingLevel);
            var strong = Company(100);

            Assert.That(strong.LoyaltyOf(strong.Staff.Hires[0]),
                Is.GreaterThan(neutral.LoyaltyOf(neutral.Staff.Hires[0])),
                "A high setting does not read as better than the baseline.");

            Assert.That(poor.LoyaltyOf(poor.Staff.Hires[0]),
                Is.LessThan(neutral.LoyaltyOf(neutral.Staff.Hires[0])),
                "A dumped Teamwork is not a handicap, so the low band of the card is decoration. "
                + "Everything in this game measures from level 20, never from zero.");
        }

        /// <summary>
        /// It must never be the largest term, or it becomes a way out of the wage decision.
        /// </summary>
        [Test]
        public void ItNeverOutweighsWhatTheCompanyPays()
        {
            var state = Company(100);
            var founder = state.Skills.LoyaltyPoints();

            Assert.That(founder, Is.GreaterThan(0.0),
                "A founder at the top of this skill is worth nothing to anybody's loyalty, so the "
                + "two assertions below pass while measuring nothing.");

            Assert.That(founder, Is.LessThan(Loyalty.PaySwing),
                "The founder's Teamwork is worth " + founder.ToString("0.0") + " points against a "
                + "pay swing of " + Loyalty.PaySwing.ToString("0.0") + ", so being pleasant buys "
                + "more than paying people does and the wage slider has stopped being a decision.");

            Assert.That(founder, Is.LessThan(Loyalty.TenureCeiling),
                "It outweighs five years of service, which is the one term in this reading that "
                + "cannot be bought at all.");
        }

        /// <summary>
        /// **And it reaches nobody else's payroll.** The whole point of poaching is bidding against
        /// a number you cannot see, and that number belongs to the company the person works for. A
        /// player's own Teamwork making a rival's staff harder to take is the effect inverted.
        /// </summary>
        [Test]
        public void ItDoesNotMakeARivalsPeopleHarderToTake()
        {
            var roster = RivalStaff.RosterFor(CompetitorId.OpenAi, Today, 4242);

            Assert.That(roster.Count, Is.GreaterThan(0),
                "There is nobody on this rival's payroll, so the comparison below measures nothing.");

            var before = Loyalty.For(roster[0], Today);

            // The same reading, with a company whose founder is as good with people as the game
            // allows. Nothing about the rival changed, so nothing about their staff may either.
            var strong = Company(100);
            var after = Loyalty.For(roster[0], Today);

            Assert.That(after, Is.EqualTo(before).Within(1e-9),
                "Somebody on a rival's payroll reads differently depending on the player's own "
                + "skills, which is the effect pointing the wrong way: a good manager would be "
                + "making other companies harder to raid.");

            Assert.That(strong.Skills.LoyaltyPoints(), Is.GreaterThan(0.0),
                "The founder in this test has no effect to leak, so the assertion above passes for "
                + "the wrong reason.");
        }
    }
}
