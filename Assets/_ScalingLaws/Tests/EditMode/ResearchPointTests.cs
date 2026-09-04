using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Research points: earned by building, bought with money, spent on the tree.
    ///
    /// Two sources on purpose, answering different questions. A lab learns by building, so a company
    /// that ships nothing learns nothing however rich it is. A lab can also hire the answer, but on a
    /// curve steep enough that nobody buys the tree outright. Neither alone keeps pace, and that is
    /// the decision the system exists to create.
    /// </summary>
    public sealed class ResearchPointTests
    {
        [Test]
        public void ACompanyBuildingNothingLearnsNothingFromWork()
        {
            Assert.AreEqual(0.0,
                ResearchBudget.PointsFromWork(false, false, 20, 2.0), 1e-12,
                "Twenty staff and a doubled research skill still learn nothing from a bench that is "
                + "not building anything. Idle discovery would remove the reason to ever start a run.");
        }

        [Test]
        public void ARunInFlightTeachesMoreThanAnUpgradeProgramme()
        {
            var training = ResearchBudget.PointsFromWork(true, false, 1.0, 1.0);
            var upgrading = ResearchBudget.PointsFromWork(false, true, 1.0, 1.0);

            Assert.Greater(training, upgrading,
                "Building something new has to teach more than improving something old, or there is "
                + "no reason to take the risk.");

            Assert.Greater(ResearchBudget.PointsFromWork(true, true, 1.0, 1.0), training,
                "Doing both is doing more.");
        }

        /// <summary>
        /// **Who you hire changes what the company learns.**
        ///
        /// This replaced a test that asserted every head added a flat forty per cent. That was the
        /// rule and it made the job title decorative for the one currency money cannot buy: a
        /// go-to-market hire moved research points exactly as much as a research scientist. The
        /// role affected the calendar, through the duration multiplier, and never the gate.
        /// </summary>
        [Test]
        public void AResearchScientistIsWorthMoreToResearchThanASalesperson()
        {
            var scientist = StaffCatalog.Get(StaffRole.ResearchScientist).ResearchPointShare;
            var sales = StaffCatalog.Get(StaffRole.GoToMarket).ResearchPointShare;

            Assert.Greater(scientist, sales,
                "The two roles contribute the same to research, so the job title is decorative.");

            // Every role still contributes something. A hire that helps the company in one way and
            // is worth literally nothing in another reads as a bug rather than as a trade.
            foreach (var definition in StaffCatalog.All)
            {
                Assert.Greater(definition.ResearchPointShare, 0.0, definition.DisplayName);
                Assert.LessOrEqual(definition.ResearchPointShare, 1.0, definition.DisplayName);
            }

            var alone = ResearchBudget.PointsFromWork(true, false, 1.0, 1.0);
            var withOne = ResearchBudget.PointsFromWork(true, false, 1.0 + scientist, 1.0);

            Assert.AreEqual(alone * (1.0 + scientist), withOne, 1e-9);
        }

        /// <summary>
        /// A lab full of researchers with nothing on the cluster is still a lab.
        ///
        /// **The one exception to "staff multiply work rather than making it".** Before this, a
        /// company between runs with six research scientists earned exactly zero, which is not what
        /// a research lab is. It stays small and it stays the only role that has it, so the rule the
        /// exception is carved out of still holds.
        /// </summary>
        [Test]
        public void ResearchersLearnSomethingBetweenRuns()
        {
            Assert.AreEqual(0.0, ResearchBudget.PointsFromWork(false, false, 3.0, 1.0), 1e-9,
                "Idle staff generate work of their own, so hiring is the whole strategy.");

            var idleLab = ResearchBudget.PointsFromScientists(4.0, 1.0);

            Assert.Greater(idleLab, 0.0,
                "Four research scientists with nothing training learn nothing at all.");

            // And never more than actually building something.
            var training = ResearchBudget.PointsFromWork(true, false, 1.0 + 4.0 * 0.60, 1.0);

            Assert.Less(idleLab, training,
                "Sitting still out-earns training a model, so nobody would ever start a run.");
        }

        [Test]
        public void HiringNeverBecomesAStraightMultiplierOnDiscovery()
        {
            var one = ResearchBudget.PointsFromWork(true, false, 1.4, 1.0);
            var many = ResearchBudget.PointsFromWork(true, false, 1.0 + 10 * 0.4, 1.0);

            Assert.Less(many, one * 10.0,
                "Ten people must not be ten times one, or the only research strategy is hiring.");

            // The roster's own curve is where the diminishing return actually lives, and it is the
            // half a unit test of the formula cannot see.
            var roster = new StaffRoster();
            roster.SetOffice(OfficeTier.Floor);

            var first = 0.0;

            for (var index = 0; index < 12; index++)
            {
                roster.Add(new Hire(StaffRole.ResearchScientist, 3, new GameDate(0),
                    "Scientist " + index, PlayerSkill.Concept, HireSource.Agency, 100.0));

                if (index == 0)
                {
                    first = roster.ResearchPeopleFactor() - 1.0;
                }
            }

            var twelve = roster.ResearchPeopleFactor() - 1.0;

            Assert.Less(twelve, first * 12.0,
                "The twelfth scientist is worth as much as the first, so the tenth is not a meeting.");
        }

        [Test]
        public void MoneyBuysPointsOnACurveSteepEnoughThatNobodyBuysTheTree()
        {
            var knee = ResearchBudget.PointsFromFunding(ResearchBudget.FundingKneeUsd);
            var fourTimes = ResearchBudget.PointsFromFunding(ResearchBudget.FundingKneeUsd * 4.0);

            Assert.AreEqual(ResearchBudget.PointsAtKnee, knee, 1e-9);

            Assert.AreEqual(knee * 2.0, fourTimes, 1e-6,
                "Four times the money has to buy twice the points. Linear funding turns research "
                + "into a second cash sink with nothing to decide.");
        }

        [Test]
        public void NoBudgetBuysNoPoints()
        {
            Assert.AreEqual(0.0, ResearchBudget.PointsFromFunding(0.0), 1e-12);
            Assert.AreEqual(0.0, ResearchBudget.PointsFromFunding(-500_000.0), 1e-12);
        }

        [Test]
        public void ARevenueShareCostsNothingWhenNothingIsEarned()
        {
            var broke = ResearchBudget.MonthlyBudgetUsd(
                ResearchFundingMode.RevenueShare, 5_000_000L, 0.5, 0L);

            Assert.AreEqual(0L, broke,
                "A share of nothing is nothing. That is the trade against a fixed budget, which is "
                + "paid whatever happens.");

            var earning = ResearchBudget.MonthlyBudgetUsd(
                ResearchFundingMode.RevenueShare, 0L, 0.25, 1_000_000L);

            Assert.AreEqual(250_000L, earning);
        }

        [Test]
        public void AFixedBudgetIsPaidWhateverHappensAndIsCapped()
        {
            Assert.AreEqual(50_000L, ResearchBudget.MonthlyBudgetUsd(
                ResearchFundingMode.Fixed, 50_000L, 0.9, 0L));

            Assert.AreEqual(ResearchBudget.MaximumMonthlyUsd, ResearchBudget.MonthlyBudgetUsd(
                ResearchFundingMode.Fixed, 999_000_000L, 0.0, 0L));
        }

        /// <summary>
        /// The split the author asked for, and it comes from one figure so the two cannot drift.
        /// </summary>
        [Test]
        public void ANodeCostsALittleCashAndALotOfPoints()
        {
            var charged = 0;

            foreach (var node in ResearchTree.All)
            {
                var cash = ResearchBudget.CashCostOf(node.CostUsd);
                var points = ResearchBudget.PointCostOf(node.CostUsd);

                Assert.GreaterOrEqual(points, 1.0,
                    $"{node.DisplayName} costs no points, so it is bought with money alone.");

                // The entry node is free and stays free; it is the door rather than a purchase.
                if (node.CostUsd <= 0L)
                {
                    Assert.AreEqual(0L, cash, node.DisplayName);
                    continue;
                }

                charged++;
                Assert.Less(cash, node.CostUsd,
                    $"{node.DisplayName} still costs what it used to in cash.");
            }

            Assert.Greater(charged, 15,
                "Almost every node should charge something, or this test is passing on emptiness.");
        }

        // ---- through the simulation -----------------------------------------------------------

        [Test]
        public void PointsAccumulateWhileARunIsInFlightAndStopWhenItIsNot()
        {
            var simulation = new CompanySimulation(new CompanyState("Learnco", 44));
            simulation.SetRentedPetaflops(60.0);

            for (var day = 0; day < 20; day++)
            {
                simulation.AdvanceDay();
            }

            var idle = simulation.State.ResearchPoints;

            simulation.TryStartTraining(new ModelBlueprint("Muse",
                ArchitectureId.DenseTransformer, 3.0, 60.0, DatasetSource.WebCrawl), out _);

            for (var day = 0; day < 20; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.Greater(simulation.State.ResearchPoints, idle,
                "Twenty days of building taught the company nothing.");
        }

        [Test]
        public void FundingEarnsPointsEvenWithNothingBeingBuilt()
        {
            var simulation = new CompanySimulation(new CompanyState("Payco", 45));
            simulation.State.ResearchFunding = ResearchFundingMode.Fixed;
            simulation.State.ResearchMonthlyUsd = 500_000L;

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.Greater(simulation.State.ResearchPoints, 0.0,
                "A company can pay for answers as well as discover them.");
        }

        [Test]
        public void StartingANodeSpendsThePointsAndOnlyALittleCash()
        {
            var simulation = new CompanySimulation(new CompanyState("Spendco", 46));
            var node = ResearchTree.Get(ResearchTree.StartingNode);

            // Enough of both to be allowed to start.
            simulation.State.ResearchPoints = 10_000.0;
            var cashBefore = simulation.State.CashUsd;
            var pointsBefore = simulation.State.ResearchPoints;

            var target = ResearchNodeId.None;
            foreach (var standing in simulation.ResearchBoard())
            {
                if (standing.CanStart)
                {
                    target = standing.Node.Id;
                    break;
                }
            }

            Assert.AreNotEqual(ResearchNodeId.None, target,
                "With ten thousand points banked something should be startable.");

            Assert.IsTrue(simulation.TryStartResearch(target, out var why), why);

            var definition = ResearchTree.Get(target);
            Assert.AreEqual(pointsBefore - ResearchBudget.PointCostOf(definition.CostUsd),
                simulation.State.ResearchPoints, 1e-6);

            Assert.AreEqual(cashBefore - ResearchBudget.CashCostOf(definition.CostUsd),
                simulation.State.CashUsd);

            Assert.IsNotNull(node);
        }

        [Test]
        public void ANodeCannotBeStartedOnMoneyAlone()
        {
            var simulation = new CompanySimulation(new CompanyState("Richco", 47));
            simulation.State.CashUsd = 500_000_000L;
            simulation.State.ResearchPoints = 0.0;

            var blocked = 0;
            foreach (var standing in simulation.ResearchBoard())
            {
                if (!standing.CanStart && standing.BlockedReason.Contains("research points"))
                {
                    blocked++;
                }
            }

            Assert.Greater(blocked, 0,
                "Half a billion in the bank and no understanding should still not buy a single node.");
        }

        [Test]
        public void PointsAndFundingSurviveASave()
        {
            var simulation = new CompanySimulation(new CompanyState("Saveco", 48));
            simulation.State.ResearchPoints = 137.5;
            simulation.State.ResearchFunding = ResearchFundingMode.RevenueShare;
            simulation.State.ResearchRevenueShare = 0.18;

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(137.5, restored.ResearchPoints, 1e-6);
            Assert.AreEqual(ResearchFundingMode.RevenueShare, restored.ResearchFunding);
            Assert.AreEqual(0.18, restored.ResearchRevenueShare, 1e-9);
        }

        [Test]
        public void AnOlderSaveStartsWithNoPointsRatherThanAGift()
        {
            var data = new SaveData { version = 20 };
            var upgraded = SaveMigration.UpgradeV20ToV21(data);

            Assert.AreEqual(21, upgraded.version);
            Assert.AreEqual(0.0, upgraded.researchPoints, 1e-9,
                "Points are earned by building and an older save has no record of which days were "
                + "spent building.");
        }
    }
}
