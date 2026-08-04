using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Tests
{
    /// <summary>
    /// The audience curves and the model types that bet on them.
    ///
    /// These are the tests that matter for the mechanic, because the mechanic is entirely about
    /// timing: a type researched before its segment exists is capital in an empty market. If the
    /// curves ever flatten out, the choice stops being a decision and the whole thing becomes a
    /// menu of equivalent options.
    /// </summary>
    public sealed class AudienceTests
    {
        private static GameDate Year(int year) => GameDate.FromCalendar(year, 6, 1);

        [Test]
        public void SharesAlwaysSumToTheWholeMarket()
        {
            for (var year = 2022; year <= AudienceCatalog.HorizonYear + 4; year++)
            {
                var total = AudienceCatalog.SharesOn(Year(year)).Sum();
                Assert.AreEqual(1.0, total, 1e-9,
                    $"In {year} the segments sum to {total}, so demand is being lost or invented.");
            }
        }

        [Test]
        public void NobodyIsAutonomousInTwentyTwentyTwo()
        {
            Assert.AreEqual(0.0, AudienceCatalog.ShareOf(AudienceSegment.Agentic, Year(2022)), 1e-9,
                "The agent market cannot exist before a model can hold a task down.");
        }

        [Test]
        public void DevelopersGoFromNothingToASegmentWorthChasing()
        {
            var early = AudienceCatalog.ShareOf(AudienceSegment.Developer, Year(2022));
            var later = AudienceCatalog.ShareOf(AudienceSegment.Developer, Year(2025));

            Assert.Less(early, 0.12, "Developers were not a market in 2022.");
            Assert.Greater(later, early * 2.0, "The coding jump never happens, so timing it means nothing.");
        }

        [Test]
        public void TheAutonomousSegmentOvertakesItsOwnEarlySelf()
        {
            var shares = new[] { 2024, 2028, 2032, 2036 }
                .Select(year => AudienceCatalog.ShareOf(AudienceSegment.Agentic, Year(year)))
                .ToArray();

            for (var index = 1; index < shares.Length; index++)
            {
                Assert.Greater(shares[index], shares[index - 1], "The agent curve stopped climbing.");
            }
        }

        [Test]
        public void EveryCurveIsHeldFlatOutsideItsAnchors()
        {
            foreach (var segment in AudienceCatalog.All)
            {
                Assert.AreEqual(segment.WeightIn(AudienceCatalog.HorizonYear),
                    segment.WeightIn(AudienceCatalog.HorizonYear + 20), 1e-9,
                    $"{segment.DisplayName} extrapolates past its last anchor, which is inventing a forecast.");
            }
        }

        [Test]
        public void TheMarketGrowsRatherThanJustRearranging()
        {
            Assert.Greater(AudienceCatalog.MarketSizeIndex(Year(2030)),
                AudienceCatalog.MarketSizeIndex(Year(2022)) * 3.0,
                "Shares moving is not the same as the market growing, and both have to be true.");
        }

        [Test]
        public void EveryTypeExceptGeneralIsGatedBehindResearch()
        {
            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Type == ModelType.General)
                {
                    Assert.AreEqual(ResearchNodeId.None, definition.Requires,
                        "The default type has to be free, or a new company cannot ship anything.");
                    continue;
                }

                Assert.AreNotEqual(ResearchNodeId.None, definition.Requires,
                    $"{definition.DisplayName} is free, so there is no decision to make.");
                Assert.IsTrue(ResearchTree.All.Any(node => node.Id == definition.Requires),
                    $"{definition.DisplayName} needs a node that is not on the tree.");
            }
        }

        [Test]
        public void NoTypeIsGoodAtEverything()
        {
            foreach (var definition in ModelTypeCatalog.All)
            {
                var strong = AudienceCatalog.All.Count(segment =>
                    definition.AffinityFor(segment.Segment) >= 1.0);

                Assert.Less(strong, AudienceCatalog.All.Count,
                    $"{definition.DisplayName} is at least par with every segment, so it is a free win.");
            }
        }

        [Test]
        public void TheGeneralModelLosesGroundAsTheMarketSpecialises()
        {
            var early = ModelTypeCatalog.ReachOn(ModelType.General, Year(2022));
            var late = ModelTypeCatalog.ReachOn(ModelType.General, Year(2034));

            Assert.Less(late, early,
                "A general model has to get relatively worse as the segments it is weakest at grow. "
                + "Otherwise shipping one forever is a valid strategy and the types are decoration.");
        }

        [Test]
        public void ACodingModelIsAMistakeEarlyAndCorrectLater()
        {
            var coding2022 = ModelTypeCatalog.ReachOn(ModelType.Coding, Year(2022));
            var general2022 = ModelTypeCatalog.ReachOn(ModelType.General, Year(2022));
            var coding2027 = ModelTypeCatalog.ReachOn(ModelType.Coding, Year(2027));

            Assert.Less(coding2022, general2022, "Specialising into an empty segment should hurt.");
            Assert.Greater(coding2027, coding2022, "The same bet should pay once the segment arrives.");
        }

        [Test]
        public void TheAgentTypeIsWorthlessBeforeItsMarketExists()
        {
            var early = ModelTypeCatalog.ReachOn(ModelType.Agentic, Year(2022));
            var late = ModelTypeCatalog.ReachOn(ModelType.Agentic, Year(2034));

            Assert.Less(early, late * 0.5,
                "Researching the agent line early has to be a real loss, not a head start.");
        }

        [Test]
        public void SpecialistAudiencesToleratePriceBetterThanConsumers()
        {
            var date = Year(2028);
            var general = ModelTypeCatalog.PriceToleranceOn(ModelType.General, date);
            var coding = ModelTypeCatalog.PriceToleranceOn(ModelType.Coding, date);
            var automation = ModelTypeCatalog.PriceToleranceOn(ModelType.Automation, date);

            Assert.Greater(coding, general, "Developers mind a price rise less than consumers do.");
            Assert.Greater(automation, coding, "Enterprise buyers mind it least of all.");
        }

        [Test]
        public void AnAudienceMixIsAWholeAudience()
        {
            foreach (var definition in ModelTypeCatalog.All)
            {
                var mix = ModelTypeCatalog.AudienceMixOn(definition.Type, Year(2029));
                Assert.AreEqual(1.0, mix.Sum(), 1e-9, definition.DisplayName);
            }
        }

        [Test]
        public void NothingInTheCurvesIsNaNOrNegative()
        {
            for (var year = 2020; year <= 2040; year++)
            {
                foreach (var share in AudienceCatalog.SharesOn(Year(year)))
                {
                    Assert.IsFalse(double.IsNaN(share));
                    Assert.GreaterOrEqual(share, 0.0);
                }

                foreach (var definition in ModelTypeCatalog.All)
                {
                    var reach = ModelTypeCatalog.ReachOn(definition.Type, Year(year));
                    Assert.IsFalse(double.IsNaN(reach), $"{definition.DisplayName} in {year}");
                    Assert.Greater(reach, 0.0, $"{definition.DisplayName} reaches nobody in {year}");
                }
            }
        }
    }
}
