using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The second half of the campaign has to be playable.
    ///
    /// Three faults, found together by `CeilingProbe` and the deep campaign on 2026-09-18, and all
    /// three passed every test in the suite because nothing measured anything past 2028:
    ///
    /// 1. **The field outran the physics.** Rivals gained a few points a release until the index
    ///    stopped them at 100 in 2031, while the best run any player could train stopped at 90.9 in
    ///    2028. Seven years of fourteen could not be won however well anybody played.
    /// 2. **The recipes stopped improving on a single day** at sixty four times, which is what froze
    ///    the player's ceiling.
    /// 3. **The world lost half its users** between 2028 and 2035 and the market's takings fell from
    ///    $52bn a year to $22bn, because demand saturated while each person's use kept growing, and
    ///    users are tokens divided by use.
    /// </summary>
    public sealed class LateGameTests
    {
        private static GameDate On(int year) => GameDate.FromCalendar(year, 1, 1);

        [Test]
        public void TheReachableFrontierKeepsClimbingAndNeverReachesTheTopOfTheIndex()
        {
            var previous = FrontierPhysics.ReachableOn(On(2028));

            for (var year = 2029; year <= 2033; year++)
            {
                var now = FrontierPhysics.ReachableOn(On(year));

                Assert.That(now, Is.GreaterThan(previous),
                    $"The best trainable model stopped improving in {year}; the late game is frozen.");

                previous = now;
            }

            // `ScalingLaw` has always said nothing in the campaign reaches 100. Hold it to that.
            Assert.That(FrontierPhysics.ReachableOn(GameDate.FromCalendar(2036, 12, 31)),
                Is.LessThan(100.0));
        }

        [Test]
        public void NoRivalEverShipsPastWhatCouldBeTrained()
        {
            var simulation = new CompanySimulation(new CompanyState("Late", 4242u));
            var state = simulation.State;
            var worst = double.MinValue;
            var worstOn = GameDate.Start;

            for (var day = 0; day < 14 * 365; day++)
            {
                simulation.AdvanceDay();

                while (state.TryDequeueEvent(out _))
                {
                }

                // Only the projected years: the reference table is history and is left as it was.
                if (state.Date < CompetitorCatalog.LastKnownRelease || day % 30 != 0)
                {
                    continue;
                }

                var reachable = FrontierPhysics.ReachableOn(state.Date);

                foreach (var rival in state.Rivals.LiveModels(state.Date))
                {
                    var over = rival.Capability - reachable;

                    if (over > worst)
                    {
                        worst = over;
                        worstOn = state.Date;
                    }
                }
            }

            Assert.That(worst, Is.LessThanOrEqualTo(0.01),
                $"A rival stood {worst:0.0} above what could be trained on {worstOn}.");
        }

        [Test]
        public void RecipesKeepImprovingAfterTheYearlyDoublingEnds()
        {
            var endOfDoubling = MarketModel.BaseAlgorithmicEfficiencyOn(On(2028));
            var later = MarketModel.BaseAlgorithmicEfficiencyOn(On(2031));

            Assert.That(endOfDoubling, Is.EqualTo(MarketModel.EarlyAlgorithmicEfficiency).Within(1.0));
            Assert.That(later, Is.EqualTo(endOfDoubling * 2.0).Within(2.0),
                "Past sixty four times the recipes double every three years.");
            Assert.That(MarketModel.BaseAlgorithmicEfficiencyOn(On(2040)),
                Is.LessThanOrEqualTo(MarketModel.MaximumAlgorithmicEfficiency));
        }

        [Test]
        public void TheWorldKeepsItsPeopleOnceDemandHasSaturated()
        {
            // Measured the way the market panel measures them: each audience's slice of the token
            // pool, divided by what one of its people gets through that year.
            static double People(int year)
            {
                var date = On(year);
                var demand = MarketModel.DemandOn(date);
                var shares = AudienceCatalog.SharesOn(date);
                var people = 0.0;

                for (var index = 0; index < AudienceCatalog.All.Count; index++)
                {
                    people += AudienceCatalog.All[index].UsersFor(demand * shares[index], year);
                }

                return people;
            }

            var peak = People(2029);

            for (var year = 2030; year <= 2036; year++)
            {
                Assert.That(People(year), Is.GreaterThan(peak * 0.85),
                    $"The world lost more than a sixth of its users by {year}.");
            }
        }

        [Test]
        public void TheMarketsTakingsDoNotCollapseInTheLateGame()
        {
            static double Takings(int year) =>
                MarketModel.DemandOn(On(year)) * MarketModel.PriceOn(On(year));

            Assert.That(Takings(2035), Is.GreaterThan(Takings(2029)),
                "The prize for the late game shrank; nothing past 2028 is worth fighting for.");
        }

        [Test]
        public void TheYearsTheBalanceWasTunedOnAreUntouched()
        {
            // The heavier use only starts once the curve has saturated.
            var before = GameDate.FromCalendar(2028, 12, 1);
            var expected = MarketModel.DemandCeilingBillionTokensPerDay
                * System.Math.Exp(-11.891 * System.Math.Exp(-0.00109 * before.DayIndex))
                * WorldEventCatalog.MultiplierOn(WorldLever.Demand, before);

            Assert.That(MarketModel.DemandOn(before), Is.EqualTo(expected).Within(expected * 1e-9));
        }
    }
}
