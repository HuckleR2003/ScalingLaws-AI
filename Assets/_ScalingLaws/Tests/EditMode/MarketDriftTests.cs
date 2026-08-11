using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Does the market actually change shape over a whole game, or does it only change on paper?
    ///
    /// <see cref="AudienceTests"/> already proves the catalog curves move. That is not the same claim.
    /// The curves could move perfectly while the served market stayed frozen, because what people are
    /// sold has to travel through affinity, adoption speed and whoever happens to be shipping. This
    /// fixture drives fourteen years of real simulation and reads the served standing, which is the
    /// number the player is shown.
    /// </summary>
    public sealed class MarketDriftTests
    {
        /// <summary>Every reading is taken from one run, so a drift cannot be a seed artefact.</summary>
        private static Dictionary<int, MarketBreakdown> WalkTheDecade(uint seed = 77)
        {
            var simulation = new CompanySimulation(new CompanyState("Driftco", seed));
            var readings = new Dictionary<int, MarketBreakdown>();

            while (simulation.State.Date.Year <= 2035)
            {
                simulation.AdvanceDay();

                if (simulation.State.Date.Month == 6 && simulation.State.Date.Day == 1)
                {
                    readings[simulation.State.Date.Year] = simulation.MarketByType();
                }
            }

            return readings;
        }

        private static double ShareOfMarket(MarketBreakdown breakdown, ModelType type) =>
            breakdown.TryGetType(type, out var standing) && breakdown.TotalUsersOverall > 0.0
                ? standing.TotalUsers / breakdown.TotalUsersOverall
                : 0.0;

        [Test]
        public void TheServedMarketChangesShapeOverAWholeGame()
        {
            var readings = WalkTheDecade();
            var report = new StringBuilder();

            foreach (var year in new[] { 2023, 2026, 2030, 2035 })
            {
                var reading = readings[year];
                report.AppendLine($"{year}: {UiCount(reading.TotalUsersOverall)} users, "
                    + $"unserved {reading.UnservedShare:P1}");

                foreach (var type in ModelTypeCatalog.All)
                {
                    report.AppendLine($"    {type.DisplayName,-18} "
                        + $"{ShareOfMarket(reading, type.Type),8:P2}");
                }
            }

            TestContext.WriteLine(report.ToString());

            // Counted in people, not in share of people.
            //
            // The first version of this asserted Coding's *percentage* grows, and it failed while the
            // category was in fact growing thirteenfold. Consumers are the cheapest audience per head
            // by a factor of fifty, so headcount share is dominated by whoever sells conversation no
            // matter what happens anywhere else. A ratio Coding structurally cannot win is the wrong
            // instrument, and loosening it would have hidden that rather than fixed it.
            readings[2023].TryGetType(ModelType.Coding, out var codingEarly);
            readings[2030].TryGetType(ModelType.Coding, out var codingLate);

            Assert.Greater(codingLate.TotalUsers, codingEarly.TotalUsers * 5.0,
                $"Coding served {codingEarly.TotalUsers:N0} people in 2023 and "
                + $"{codingLate.TotalUsers:N0} in 2030. The developer curve rises steeply across those "
                + "years, so the number of developers actually being served has to rise with it.");

            // And the market as a whole has to be a different shape, not merely a bigger one.
            var early = ShareOfMarket(readings[2023], ModelType.General);
            var late = ShareOfMarket(readings[2030], ModelType.General);

            Assert.Less(late, early * 0.75,
                $"General purpose held {early:P1} of the market in 2023 and {late:P1} in 2030. "
                + "Shipping one model for everybody has to get relatively worse as the audiences "
                + "separate, or specialising never becomes the right call.");
        }

        /// <summary>
        /// Measured in people rather than in percent. A share of a market that is barely served yet is
        /// numerically unstable, and the first version of this test read 2.55% off a 2023 market that
        /// held almost nobody, which is noise dressed up as a finding.
        /// </summary>
        [Test]
        public void AutonomousWorkArrivesOnlyOnceTheCalendarAllowsIt()
        {
            var opens = ResearchTree.Get(ResearchNodeId.AgenticWorkstation).EarliestDate;
            Assert.Greater(opens.Year, 2023, "This test assumes the agent line is not a year one node.");

            var readings = WalkTheDecade();
            var before = readings[2023];
            var after = readings[2035];

            before.TryGetType(ModelType.Agentic, out var early);
            after.TryGetType(ModelType.Agentic, out var late);

            Assert.LessOrEqual(early.TotalUsers, before.TotalUsersOverall * 1e-6,
                $"Autonomous agents held {early.TotalUsers:0} users in 2023, before "
                + $"{opens} opened the line. Nobody could run an agent unsupervised then, so a market "
                + "for it in year one is the gate leaking.");

            Assert.Greater(late.TotalUsers, 0.0,
                "Autonomous work owns the late game. If nobody is ever running an agent, the last "
                + "third of the calendar has nothing new in it and the type is dead weight.");
        }

        /// <summary>
        /// Every type has to be something somebody actually builds at some point in a game. Two of
        /// them were not: EnterpriseFocus was never assigned to any lab in the field, so Automation
        /// was unreachable, and Agentic had no branch in the strategy map at all. Both read zero for
        /// fourteen years and no test noticed, because every test asked whether the numbers were
        /// consistent rather than whether they were ever non-zero.
        /// </summary>
        [Test]
        public void EveryModelTypeIsSomethingSomebodyEventuallyBuilds()
        {
            var readings = WalkTheDecade();
            var everReached = new Dictionary<ModelType, double>();

            foreach (var type in ModelTypeCatalog.All)
            {
                everReached[type.Type] = 0.0;
            }

            foreach (var reading in readings.Values)
            {
                foreach (var standing in reading.Types)
                {
                    everReached[standing.Type] = System.Math.Max(
                        everReached[standing.Type], standing.TotalUsers);
                }
            }

            foreach (var (type, peak) in everReached)
            {
                Assert.Greater(peak, 0.0,
                    $"{ModelTypeCatalog.Get(type).DisplayName} never had a single user in fourteen "
                    + "years, so nothing in the field can build it and the category is decoration.");
            }
        }

        [Test]
        public void TheMarketNeverBreaksOverFourteenYears()
        {
            var readings = WalkTheDecade(seed: 4242);

            foreach (var (year, reading) in readings)
            {
                Assert.Greater(reading.AddressableUsers, 0.0, $"{year} has nobody in it at all.");
                Assert.GreaterOrEqual(reading.UnservedShare, 0.0, year.ToString());
                Assert.LessOrEqual(reading.UnservedShare, 1.0, year.ToString());

                var summed = 0.0;
                foreach (var standing in reading.Types)
                {
                    Assert.IsFalse(double.IsNaN(standing.TotalUsers), $"{year} {standing.Type}");
                    Assert.GreaterOrEqual(standing.TotalUsers, 0.0, $"{year} {standing.Type}");
                    summed += standing.TotalUsers;
                }

                Assert.AreEqual(reading.TotalUsersOverall, summed,
                    reading.TotalUsersOverall * 1e-9 + 1e-6,
                    $"{year}: the categories stopped adding up to the market.");
            }
        }

        /// <summary>
        /// Somebody has to be serving people. A market where every reading is unserved would pass the
        /// drift tests above while showing the player an empty pie for fourteen years.
        /// </summary>
        [Test]
        public void SomebodyIsActuallyServingTheMarketThroughout()
        {
            var readings = WalkTheDecade();

            foreach (var (year, reading) in readings)
            {
                if (year < 2024)
                {
                    continue;
                }

                Assert.Greater(reading.TotalUsersOverall, 0.0,
                    $"{year}: eight labs are shipping and nobody holds a single user.");
            }
        }

        private static string UiCount(double value) => value switch
        {
            >= 1e9 => $"{value / 1e9:0.0}B",
            >= 1e6 => $"{value / 1e6:0.0}M",
            >= 1e3 => $"{value / 1e3:0.0}k",
            _ => $"{value:0}"
        };
    }
}
