using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Data Engineering's second effect: rented capacity costs less per day.
    ///
    /// **This one lands on money that is already discounted once**, which is why it is three fifths
    /// of the skill rather than all of it. The fleet bill is part of `DailyOperatingCostUsd`, and
    /// the day multiplies that by the founder's trait and by `OperatingCostMultiplier` before
    /// charging it, so Management already takes up to 18% off the rent. Two full skills on one
    /// dollar would take a third off the largest cost in the game.
    ///
    /// The other care is that the discount is applied where the profile is built rather than where
    /// the money is charged. The compute screen, the fleet bill and the four-way split the books use
    /// are all read off that profile, and this project has shipped a screen and a ledger disagreeing
    /// about one number more than once.
    /// </summary>
    public sealed class SkillRentTests
    {
        private static readonly GameDate Day = GameDate.FromCalendar(2024, 1, 1);

        private static ComputePool Renting(double petaflops)
        {
            var pool = new ComputePool();
            pool.SetRentedPetaflops(petaflops);
            return pool;
        }

        [Test]
        public void AGoodDataEngineerRentsTheSameCapacityForLess()
        {
            var market = MarketModel.Evaluate(Day);

            var plain = Renting(500.0).BuildProfile(Day, market);
            var sharp = Renting(500.0).BuildProfile(Day, market, null, null, null, 0.0, 0.8);

            Assert.That(plain.Bill.CloudRentUsd, Is.GreaterThan(0.0),
                "This fleet rents nothing, so the comparison below is between two zeros.");

            Assert.That(sharp.Bill.CloudRentUsd, Is.LessThan(plain.Bill.CloudRentUsd),
                "The same 500 petaflops cost the same whoever is staging the data: "
                + sharp.Bill.CloudRentUsd.ToString("N0") + " against "
                + plain.Bill.CloudRentUsd.ToString("N0") + ".");

            Assert.That(sharp.RawPetaflops, Is.EqualTo(plain.RawPetaflops).Within(1e-6),
                "The discount bought capacity rather than a cheaper bill, which is a different "
                + "mechanic and a much stronger one.");
        }

        /// <summary>
        /// **The discount has to reach the total the day actually charges**, or the fleet panel
        /// quotes a rent the account never paid.
        /// </summary>
        [Test]
        public void TheCheaperRentReachesTheOperatingCostAndNotOnlyTheBill()
        {
            var market = MarketModel.Evaluate(Day);

            var plain = Renting(500.0).BuildProfile(Day, market);
            var sharp = Renting(500.0).BuildProfile(Day, market, null, null, null, 0.0, 0.8);

            Assert.That(sharp.DailyOperatingCostUsd, Is.LessThan(plain.DailyOperatingCostUsd),
                "The bill says the rent is cheaper and the day's operating cost does not, so the "
                + "two are read from different arithmetic and the books will disagree with the "
                + "screen beside them.");

            var billSaving = plain.Bill.CloudRentUsd - sharp.Bill.CloudRentUsd;
            var costSaving = plain.DailyOperatingCostUsd - sharp.DailyOperatingCostUsd;

            Assert.That(costSaving, Is.EqualTo(billSaving).Within(1.0),
                "The saving on the bill is " + billSaving.ToString("N0") + " and the saving on the "
                + "operating cost is " + costSaving.ToString("N0") + ". They are the same money.");
        }

        /// <summary>
        /// **It is the rented half only.** Owned silicon is bought once and its daily cost is power
        /// and upkeep, neither of which a data engineer makes cheaper.
        /// </summary>
        [Test]
        public void ItDoesNotTouchWhatOwnedSiliconCosts()
        {
            var market = MarketModel.Evaluate(Day);

            var plain = Renting(0.0).BuildProfile(Day, market);
            var sharp = Renting(0.0).BuildProfile(Day, market, null, null, null, 0.0, 0.8);

            Assert.That(sharp.DailyOperatingCostUsd,
                Is.EqualTo(plain.DailyOperatingCostUsd).Within(1e-6),
                "A fleet that rents nothing came out cheaper, so the discount has reached the "
                + "electricity or the upkeep.");
        }

        /// <summary>
        /// **And the day actually hands the multiplier over.** Every test above drives
        /// `BuildProfile` with the figure passed in by hand, which proves the link and says nothing
        /// about the chain. A multiplier the daily tick never passes is a green fixture over a dead
        /// effect, and this project has shipped that shape thirteen times.
        ///
        /// Measured on the books rather than on the bank balance, because the two companies differ
        /// in a skill that also moves corpus quality: the ledger line is the rent and nothing else.
        /// </summary>
        [Test]
        public void TheDayChargesTheCheaperRent()
        {
            var plain = Running(PlayerSkillLimits.StartingLevel);
            var sharp = Running(100);

            var month = Ledger.MonthKeyOf(Day);

            for (var day = 0; day < 28; day++)
            {
                plain.AdvanceDay();
                sharp.AdvanceDay();
            }

            var plainRent = plain.State.Ledger.MonthTotal(month, LedgerLine.CloudRent);
            var sharpRent = sharp.State.Ledger.MonthTotal(month, LedgerLine.CloudRent);

            Assert.That(plainRent, Is.GreaterThan(0L),
                "Neither company was charged any rent in four weeks, so the comparison below is "
                + "between two zeros. Check that this fixture is actually renting anything.");

            Assert.That(sharpRent, Is.LessThan(plainRent),
                "Four weeks in, the sharp data engineer has been charged "
                + sharpRent.ToString("N0") + " of rent against " + plainRent.ToString("N0")
                + ". `RentedComputeCostMultiplier` exists and the day never passes it to "
                + "`BuildProfile`.");
        }

        /// <summary>A company renting a real fleet, differing in one skill.</summary>
        private static CompanySimulation Running(int dataEngineering)
        {
            var state = new CompanyState("Prometheus AI", 3131)
            {
                Date = Day,
                CashUsd = 5_000_000_000
            };

            state.Skills.SetLevel(PlayerSkill.DataEngineering, dataEngineering);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(1_000.0);

            return simulation;
        }

        /// <summary>
        /// The two skills that reach this money, together, against the largest cost in the game.
        /// </summary>
        [Test]
        public void TheSkillsTogetherNeverTakeAThirdOffTheRent()
        {
            var skills = new SkillSet();
            skills.SetLevel(PlayerSkill.DataEngineering, 100);
            skills.SetLevel(PlayerSkill.Management, 100);

            var together = skills.RentedComputeCostMultiplier() * skills.OperatingCostMultiplier();

            Assert.That(together, Is.GreaterThan(0.70),
                "A founder who maxes both skills pays " + together.ToString("0.000")
                + " of the rent, which is more than 30% off the single largest cost in the game "
                + "from character creation alone. Lower the share in RentedComputeCostMultiplier "
                + "rather than loosening this number.");

            Assert.That(together, Is.LessThan(1.0),
                "Maxing both skills does not make the rent cheaper at all, so one of the two "
                + "multipliers has stopped reaching it.");
        }

        /// <summary>
        /// The skill's own reading: neutral at the baseline, a handicap below it, bounded above.
        /// </summary>
        [Test]
        public void TheSkillIsNeutralAtTheBaselineAndAHandicapBelowIt()
        {
            var declared = 0;

            foreach (var point in SkillEffectCatalog.For(PlayerSkill.DataEngineering))
            {
                if (point.Key == "skilleffect.rentcost")
                {
                    declared = point.AtHigh;
                }
            }

            Assert.That(declared, Is.GreaterThan(0),
                "The Data Engineering card no longer declares an effect on rented compute, so this "
                + "fixture is guarding nothing. Either put the point back or delete this test.");

            var skills = new SkillSet();

            skills.SetLevel(PlayerSkill.DataEngineering, PlayerSkillLimits.StartingLevel);
            Assert.That(skills.RentedComputeCostMultiplier(), Is.EqualTo(1.0).Within(1e-9),
                "The baseline is not exactly neutral, so adding this effect has quietly retuned "
                + "every company balanced before it existed.");

            skills.SetLevel(PlayerSkill.DataEngineering, 100);
            var high = skills.RentedComputeCostMultiplier();

            skills.SetLevel(PlayerSkill.DataEngineering, 0);
            var low = skills.RentedComputeCostMultiplier();

            Assert.That(high, Is.LessThan(1.0), "A high setting makes nothing cheaper.");
            Assert.That(low, Is.GreaterThan(1.0),
                "A dumped Data Engineering is not a handicap, so the low band of the card is "
                + "decoration. Everything here measures from level 20, never from zero.");
        }
    }
}
