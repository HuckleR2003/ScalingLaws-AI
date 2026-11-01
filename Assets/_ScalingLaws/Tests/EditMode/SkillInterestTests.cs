using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Concept's second effect: a following gathers faster around what the company ships.
    ///
    /// **The careful part is which half of the fan base it touches.** Fans are a stock with a target
    /// and two speeds: the target is users times regard squared, and it is earned by serving people
    /// well and being liked. A founder skill that raised the target would be a way of being admired
    /// without having done anything, which is the line marketing is already held to and the reason
    /// `MarketingNeverImprovesTheProductItself` exists.
    ///
    /// So this moves the courtship and nothing else, and three of the four tests here are about
    /// that: the ceiling is untouched, departures are untouched, and a company that has earned
    /// nothing gains nothing however compelling its founder.
    /// </summary>
    public sealed class SkillInterestTests
    {
        [Test]
        public void ACompellingFounderGathersAFollowingSooner()
        {
            const double target = 1_000_000.0;

            var plain = Standing.AdvanceFans(0.0, target);
            var compelling = Standing.AdvanceFans(0.0, target, 1.5);

            Assert.That(compelling, Is.GreaterThan(plain),
                "The same earned following arrives at the same speed whoever is running the "
                + "company (" + compelling.ToString("0") + " against " + plain.ToString("0")
                + " on day one), so the Concept card's second line does not reach the simulation.");
        }

        /// <summary>
        /// **The ceiling is the thing that must not move.** Over a long enough run both companies
        /// have to arrive at the same following, because that figure is what the company earned.
        /// </summary>
        [Test]
        public void ItBuysTimeAndNeverABiggerFollowing()
        {
            const double target = 1_000_000.0;

            var plain = 0.0;
            var compelling = 0.0;

            // Ten years. Fans approach at 0.4% of the gap a day, so this is comfortably past the
            // point where the speed stops mattering.
            for (var day = 0; day < 3650; day++)
            {
                plain = Standing.AdvanceFans(plain, target);
                compelling = Standing.AdvanceFans(compelling, target, 1.5);
            }

            Assert.That(compelling, Is.EqualTo(plain).Within(target * 0.01),
                "After ten years the compelling founder holds " + compelling.ToString("0")
                + " fans against " + plain.ToString("0") + ". The skill has bought a larger "
                + "following rather than an earlier one, which means it is buying regard that was "
                + "never earned.");

            Assert.That(compelling, Is.LessThanOrEqualTo(target * 1.0001),
                "The following has gone past what the company earned.");
        }

        /// <summary>
        /// **And it does not slow the leaving.** Fans drift away at a third of the rate they arrive,
        /// and that asymmetry is what makes them a stock rather than a second user count. People
        /// leave over the product, not over who is running the place.
        /// </summary>
        [Test]
        public void ItDoesNotChangeHowFastAFollowingWalksAway()
        {
            var plain = Standing.AdvanceFans(1_000_000.0, 0.0);
            var compelling = Standing.AdvanceFans(1_000_000.0, 0.0, 1.5);

            Assert.That(compelling, Is.EqualTo(plain).Within(1e-6),
                "A founder with vision loses their following at a different speed, so the effect "
                + "has been applied to both branches rather than to the arrival alone.");

            var dull = Standing.AdvanceFans(1_000_000.0, 0.0, 0.7);

            Assert.That(dull, Is.EqualTo(plain).Within(1e-6),
                "And a founder nobody finds interesting keeps their fans longer, which is the "
                + "effect inverted.");
        }

        /// <summary>
        /// A company with nothing to show for itself earns nothing, whoever is in charge. This is
        /// the test that would catch the effect being moved onto the target by mistake.
        /// </summary>
        [Test]
        public void AFounderWithNothingShippedGathersNobody()
        {
            Assert.That(Standing.AdvanceFans(0.0, 0.0, 1.5), Is.EqualTo(0.0).Within(1e-9),
                "Somebody acquired a following without serving a single person.");

            Assert.That(Standing.FanTarget(1_000_000.0, 0.0), Is.EqualTo(0.0).Within(1e-9),
                "A company nobody regards has a following, so the squared term has gone.");
        }

        /// <summary>
        /// **And the day actually passes it.** Every test above drives `Standing.AdvanceFans`
        /// directly, which proves the link and says nothing about the chain: a multiplier the
        /// daily tick never hands over is a green fixture over a dead effect. This project has
        /// shipped that shape thirteen times.
        /// </summary>
        [Test]
        public void TheDayHandsTheMultiplierOver()
        {
            var plain = Running(PlayerSkillLimits.StartingLevel);
            var compelling = Running(100);

            // Three months of the same company, twice, differing in one skill. Fans follow served
            // users, so both arms have to be selling something for this to measure anything.
            for (var day = 0; day < 90; day++)
            {
                plain.AdvanceDay();
                compelling.AdvanceDay();
            }

            Assert.That(plain.State.Fans, Is.GreaterThan(0.0),
                "Neither company gathered a single fan in three months, so the comparison below is "
                + "between two zeros. Check that this fixture is actually serving anybody.");

            Assert.That(compelling.State.Fans, Is.GreaterThan(plain.State.Fans),
                "Ninety days in, the compelling founder holds "
                + compelling.State.Fans.ToString("0") + " fans against "
                + plain.State.Fans.ToString("0") + ". `FanInterestMultiplier` exists and the day "
                + "never passes it to `AdvanceFans`.");
        }

        /// <summary>A company three months into selling something, differing in one skill.</summary>
        private static CompanySimulation Running(int concept)
        {
            var state = new CompanyState("Prometheus AI", 7171)
            {
                Date = GameDate.FromCalendar(2024, 1, 1),
                CashUsd = 2_000_000_000,
                Reputation = 0.6
            };

            state.Skills.SetLevel(PlayerSkill.Concept, concept);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(2_000.0);

            state.AddDeployedModel(new DeployedModel(
                "Aurora", ArchitectureId.DenseTransformer, 42.0,
                GameDate.FromCalendar(2023, 10, 1), 2e10, 1.0, ModelType.General));

            // **`AddDeployedModel` does not set this and a probe that forgot it ran a permanently
            // stale company for a whole session.** A stale line costs reputation every day, which
            // is the term the fan target is squared in.
            state.LastReleaseDate = GameDate.FromCalendar(2023, 10, 1);

            return simulation;
        }

        /// <summary>
        /// The skill's reading itself: a real effect, in the right direction, and bounded.
        /// </summary>
        [Test]
        public void TheSkillIsWhatSuppliesTheMultiplierAndItIsBounded()
        {
            var declared = 0;

            foreach (var point in SkillEffectCatalog.For(PlayerSkill.Concept))
            {
                if (point.Key == "skilleffect.interest")
                {
                    declared = point.AtHigh;
                }
            }

            Assert.That(declared, Is.GreaterThan(0),
                "The Concept card no longer declares an interest effect, so this fixture is "
                + "guarding nothing. Either put the point back or delete this test deliberately.");

            var skills = new SkillSet();

            skills.SetLevel(PlayerSkill.Concept, PlayerSkillLimits.StartingLevel);
            Assert.That(skills.FanInterestMultiplier(), Is.EqualTo(1.0).Within(1e-9),
                "The baseline is not exactly neutral, so adding this effect has quietly retuned "
                + "every company that was balanced before it existed.");

            skills.SetLevel(PlayerSkill.Concept, 100);
            var high = skills.FanInterestMultiplier();

            skills.SetLevel(PlayerSkill.Concept, 0);
            var low = skills.FanInterestMultiplier();

            Assert.That(high, Is.GreaterThan(1.0), "A high setting does nothing.");
            Assert.That(low, Is.LessThan(1.0),
                "A dumped Concept is not a handicap, so the low band of the card is decoration.");

            Assert.That(high, Is.LessThanOrEqualTo(1.5).And.GreaterThan(1.1),
                "At level 100 the multiplier is " + high.ToString("0.000")
                + ", which is either doing nothing or doing too much.");
        }
    }
}
