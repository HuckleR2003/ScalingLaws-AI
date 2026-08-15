using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The SAFETY stage: three modules, four tiers, and an effort dial.
    ///
    /// **Everything here buys a smaller chance of something rather than a bigger number**, which is
    /// the hardest kind of value to see and the easiest to skip. These tests exist because the whole
    /// stage is a promise made at build time and cashed years later, and a promise the simulation
    /// quietly fails to keep is indistinguishable from bad luck.
    /// </summary>
    public sealed class SafetyStageTests
    {
        private static ModelBlueprint Run(int assa = 0, int red = 0, int data = -1, int effort = 1) =>
            new("Muse", ArchitectureId.DenseTransformer, 20, 400, DatasetSource.WebCrawl,
                assaTier: assa, redTeamTier: red, dataProtectionTier: data, safetyEffort: effort);

        // ---- the catalog holds its shape ---------------------------------------------------------

        [Test]
        public void EveryModuleHasFourTiersAndTheyOnlyGetBetter()
        {
            foreach (SafetyModule module in System.Enum.GetValues(typeof(SafetyModule)))
            {
                var tiers = SafetyModuleCatalog.TiersOf(module);
                Assert.AreEqual(SafetyModuleCatalog.TierCount, tiers.Count, module.ToString());

                for (var index = 1; index < tiers.Count; index++)
                {
                    var worth = tiers[index].RiskReduction + tiers[index].SaveChance;
                    var before = tiers[index - 1].RiskReduction + tiers[index - 1].SaveChance;

                    Assert.Greater(worth, before,
                        $"{module} tier {index} is not worth more than tier {index - 1}, so nobody "
                        + "would ever research it.");

                    Assert.Greater(tiers[index].ExtraDays, tiers[index - 1].ExtraDays,
                        $"{module} tier {index} costs no more calendar, so there is no trade in it.");
                }
            }
        }

        [Test]
        public void OnlyDataProtectionIsLockedAtTierZero()
        {
            // A company knows how to point its own model at itself on day one. It does not know how
            // to isolate user data, and that is the tier that has to be bought.
            Assert.AreEqual(ResearchNodeId.None,
                SafetyModuleCatalog.Get(SafetyModule.Assa, 0).Requires);

            Assert.AreEqual(ResearchNodeId.None,
                SafetyModuleCatalog.Get(SafetyModule.RedTeam, 0).Requires);

            Assert.AreNotEqual(ResearchNodeId.None,
                SafetyModuleCatalog.Get(SafetyModule.DataProtection, 0).Requires,
                "Data protection tier zero is free, which is the one thing it must not be.");
        }

        [Test]
        public void EveryTierHasArtAndEveryResearchedOneHasANode()
        {
            foreach (var tier in SafetyModuleCatalog.All)
            {
                Assert.IsNotEmpty(tier.Icon, $"{tier.DisplayName} has no picture.");
                Assert.IsNotEmpty(tier.Description, $"{tier.DisplayName} says nothing about itself.");

                if (tier.Requires != ResearchNodeId.None)
                {
                    Assert.IsTrue(ResearchTree.TryGet(tier.Requires, out var node),
                        $"{tier.DisplayName} needs a node that is not in the tree.");

                    Assert.AreEqual(ResearchTrack.Safety, node.Track,
                        $"{node.DisplayName} is not on the safety track.");
                }
            }
        }

        [Test]
        public void EffortOneIsExactlyNeutral()
        {
            // The neutral-option rule. x1 has to be 1.0 on every axis or adding this stage retunes
            // every run that was balanced before it existed.
            var neutral = SafetyModuleCatalog.EffortOf(1);

            Assert.AreEqual(1.0, neutral.TimeMultiplier, 1e-9);
            Assert.AreEqual(0.0, neutral.StatBonus, 1e-9);
        }

        [Test]
        public void EffortCostsALotOfCalendarForVeryLittle()
        {
            var cheap = SafetyModuleCatalog.EffortOf(1);
            var dear = SafetyModuleCatalog.EffortOf(4);

            Assert.Greater(dear.TimeMultiplier, cheap.TimeMultiplier * 3.0,
                "x4 has to actually hurt.");

            Assert.Less(dear.StatBonus, 0.05,
                "And it has to stay small. If effort is worth taking every time it is not a "
                + "decision, it is a tax on anybody who forgets to click it.");
        }

        // ---- the plan ------------------------------------------------------------------------------

        [Test]
        public void ModulesStackOnWhatIsLeftRatherThanAddingUp()
        {
            // Two at half strength are not one at full, and nothing reaches certainty. Adding
            // percentages is how a safety system hits 100% and the mechanic stops existing.
            var everything = new SafetyPlan(3, 3, 3, 4, 8);

            Assert.Less(everything.RiskReduction, 0.95);
            Assert.Less(everything.SaveChance, 0.95);

            var assaOnly = new SafetyPlan(3, 0, -1, 1, 0);
            var dataOnly = new SafetyPlan(0, 0, 3, 1, 0);
            var both = new SafetyPlan(3, 0, 3, 1, 0);

            Assert.Less(both.RiskReduction, assaOnly.RiskReduction + dataOnly.RiskReduction,
                "Stacked by addition, which can be driven past one.");

            Assert.Greater(both.RiskReduction, assaOnly.RiskReduction,
                "And the second module still has to be worth having.");
        }

        [Test]
        public void EffortLengthensOnlyTheSafetyWork()
        {
            var plain = new SafetyPlan(1, 1, -1, 1, 0);
            var hard = new SafetyPlan(1, 1, -1, 4, 0);

            Assert.Greater(hard.ExtraDays, plain.ExtraDays * 3);
            Assert.AreEqual(plain.ExtraCostUsd, hard.ExtraCostUsd,
                "Effort buys time, not equipment. Charging for it twice makes it a pure loss.");
        }

        [Test]
        public void TheFleetBonusCapsWhereTheCatalogSaysItDoes()
        {
            var atCap = new SafetyPlan(0, 0, -1, 1, 5);
            var past = new SafetyPlan(0, 0, -1, 1, 40);

            Assert.AreEqual(atCap.RiskReduction, past.RiskReduction, 1e-9,
                "A fleet of products is not a fleet of auditors.");
        }

        [Test]
        public void RedTeamingDoesNothingAboutTheRiskAndDataProtectionDoesBoth()
        {
            var none = new SafetyPlan(0, 0, -1, 1, 0);
            var redTop = new SafetyPlan(0, 3, -1, 1, 0);

            Assert.AreEqual(none.RiskReduction, redTop.RiskReduction, 1e-9,
                "Red teaming is the appeal after the verdict, not a lower chance of the verdict.");

            Assert.Greater(redTop.SaveChance, none.SaveChance);

            var data = new SafetyPlan(0, 0, 2, 1, 0);
            Assert.Greater(data.RiskReduction, none.RiskReduction);
            Assert.Greater(data.SaveChance, none.SaveChance);
        }

        // ---- it reaches the run and the model ------------------------------------------------------

        [Test]
        public void TheStageCostsRealDaysAndRealMoney()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.SetRentedAccelerators(2000);

            var bare = simulation.Project(Run());
            var hardened = simulation.Project(Run(assa: 2, red: 2));

            Assert.Greater(hardened.TrainingDays, bare.TrainingDays,
                "Safety is work and work takes the calendar.");

            Assert.Greater(hardened.TotalCashCostUsd, bare.TotalCashCostUsd);
        }

        [Test]
        public void TheTiersTravelAllTheWayToTheModelOnSale()
        {
            // **This is the exact path that has failed before.** The model type was chosen
            // everywhere and passed nowhere, so every model released was general, forever, with 244
            // tests green. A safety tier that stops at the shelf is the same bug wearing a hat.
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.SetRentedAccelerators(3000);

            Assert.IsTrue(simulation.TryStartTraining(Run(assa: 0, red: 0, effort: 3), out var why), why);

            for (var day = 0; day < 900 && simulation.State.ActiveRun != null; day++)
            {
                simulation.State.CashUsd = 5_000_000_000;
                simulation.Advance(1);
            }

            Assert.IsNotEmpty(simulation.State.Shelf, "The run never finished.");
            Assert.AreEqual(3, simulation.State.Shelf[0].SafetyEffort, "Lost between run and shelf.");

            Assert.IsTrue(simulation.TryReleaseModel(0, 1.0, out var problem), problem);
            Assert.AreEqual(3, simulation.State.DeployedModels[0].SafetyEffort,
                "Lost between shelf and market, which is where nobody would ever look for it.");
        }

        [Test]
        public void HardeningActuallyLowersTheRiskTheGameRollsAgainst()
        {
            // **Measured through the public figure, which is the same one the roll uses.** Counting
            // incidents over a campaign was the first attempt and it measured nothing: the baseline
            // company had zero in fourteen hundred days across six seeds, so both arms read zero and
            // the test passed for the wrong reason until it was made to fail.
            Assert.Greater(RiskWith(assa: 0, data: -1), 0.0, "Nothing to reduce, so nothing is tested.");

            Assert.Less(RiskWith(assa: 3, data: 3), RiskWith(assa: 0, data: -1) * 0.65,
                "The whole stage is decoration if the risk does not move.");
        }

        [Test]
        public void TheRiskShownIsTheRiskRolled()
        {
            // If these two ever come from different places, the player is being told a number that
            // does not govern anything, which is worse than showing nothing.
            var simulation = Selling(assa: 2, data: 1);

            Assert.AreEqual(simulation.CurrentSafety().RiskReduction,
                new SafetyPlan(2, 0, 1, 1, simulation.State.DeployedModels.Count).RiskReduction,
                1e-9);
        }

        private static double RiskWith(int assa, int data) =>
            Selling(assa, data).DailyIncidentRisk();

        /// <summary>A company with one model on sale, hardened as asked.</summary>
        private static CompanySimulation Selling(int assa, int data)
        {
            var state = new CompanyState("Prometheus AI");
            var simulation = new CompanySimulation(state);

            state.AddDeployedModel(new DeployedModel(
                "Muse", ArchitectureId.DenseTransformer, 45, state.Date, 2e10, 1.0,
                ModelType.General, "Muse",
                assaTier: assa, redTeamTier: 0, dataProtectionTier: data, safetyEffort: 1));

            return simulation;
        }

        // ---- the save --------------------------------------------------------------------------------

        [Test]
        public void TheHardeningSurvivesASaveBecauseItBelongsToTheModel()
        {
            var state = new CompanyState("Prometheus AI");
            state.AddDeployedModel(new DeployedModel(
                "Muse", ArchitectureId.DenseTransformer, 40, state.Date, 2e10, 1.0,
                ModelType.General, "Muse",
                assaTier: 2, redTeamTier: 3, dataProtectionTier: 1, safetyEffort: 4));

            var restored = SaveStore.Restore(SaveStore.Capture(state));
            var model = restored.DeployedModels[0];

            Assert.AreEqual(2, model.AssaTier);
            Assert.AreEqual(3, model.RedTeamTier);
            Assert.AreEqual(1, model.DataProtectionTier);
            Assert.AreEqual(4, model.SafetyEffort);
        }

        [Test]
        public void AnOlderCampaignGetsWhatItStartedWithAndNothingItNeverBought()
        {
            var old = new SaveData { version = 26 };
            old.models.Add(new DeployedModelData { name = "Muse" });

            var moved = SaveMigration.UpgradeV26ToV27(old);

            Assert.AreEqual(27, moved.version);
            Assert.AreEqual(0, moved.models[0].assaTier);
            Assert.AreEqual(-1, moved.models[0].dataProtectionTier,
                "Data protection has to be bought, and a v26 company never could have.");
        }
    }
}
