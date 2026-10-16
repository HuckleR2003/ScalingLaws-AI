using System.IO;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The hard ban on duplicate accounts can be thrown by a player.
    ///
    /// **The eighteenth mechanism in this project finished underneath with no control on top of
    /// it, and the first one that advertised itself while being unreachable.** `TrySetMultiAccountBan`
    /// was written on 11 October 2026 with its cost, its research gate and seven tests, and until
    /// 16 October nothing in `Scripts/UI` ever called it. The duplicates tile had been printing
    /// the policy as ON or OFF the whole time, so the game showed a switch nothing could throw,
    /// which is the same fault as the announced offices: a row that reads as an option and refuses
    /// every click reads as a bug.
    ///
    /// Found by the mutator sweep, which is eight lines of grep and has now caught this class of
    /// fault eighteen times. Run it before believing a green suite.
    /// </summary>
    public sealed class MultiAccountBanIsReachableTests
    {
        private static readonly string PanelSource = Path.Combine(
            Application.dataPath, "_ScalingLaws", "Scripts", "UI", "OpsPanel.cs");

        private static CompanySimulation Understanding()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 909));
            simulation.State.CashUsd = 50_000_000;
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);

            return simulation;
        }

        [Test]
        public void SomethingInTheInterfaceActuallyThrowsTheSwitch()
        {
            Assert.IsTrue(File.Exists(PanelSource), $"Cannot find {PanelSource}.");

            var source = File.ReadAllText(PanelSource);

            Assert.That(source, Does.Contain("TrySetMultiAccountBan"),
                "Nothing in the operations band can switch the ban. It was unreachable for five "
                + "days while the tile printed its state.");
        }

        /// <summary>
        /// Two presses to switch it on, one to switch it off, and the arming survives the rebuild
        /// that happens between them.
        /// </summary>
        [Test]
        public void SwitchingItOnTakesTwoPressesAndSwitchingItOffTakesOne()
        {
            var simulation = Understanding();

            OpsPanel.Forget();
            Assert.IsFalse(OpsPanel.BanIsArmed, "Forget leaves the button disarmed.");

            // First press arms and changes nothing about the company.
            OpsPanel.ArmTheBan(true);
            Assert.IsFalse(simulation.State.MultiAccountBanOn,
                "Arming the button must not switch the policy or spend anything.");
            Assert.That(simulation.State.CashUsd, Is.EqualTo(50_000_000L));

            // Second press is the one that commits.
            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out var why), why);
            Assert.IsTrue(simulation.State.MultiAccountBanOn);
            Assert.IsTrue(simulation.State.MultiAccountBanBuilt);
            Assert.That(simulation.State.CashUsd,
                Is.EqualTo(50_000_000L - AccountIntegrity.BanCostUsd),
                "Switching the ban on charges for the card-linking integration once.");

            // Off, and the integration stays bought.
            Assert.IsTrue(simulation.TrySetMultiAccountBan(false, out why), why);
            Assert.IsFalse(simulation.State.MultiAccountBanOn);
            Assert.IsTrue(simulation.State.MultiAccountBanBuilt,
                "Lifting the ban does not unbuild the integration.");

            // And back on, free this time.
            var cash = simulation.State.CashUsd;
            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out why), why);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(cash),
                "The integration is paid for once and never again.");

            OpsPanel.Forget();
        }

        [Test]
        public void ForgettingTheBandDisarmsTheButton()
        {
            OpsPanel.ArmTheBan(true);
            Assert.IsTrue(OpsPanel.BanIsArmed);

            OpsPanel.Forget();

            Assert.IsFalse(OpsPanel.BanIsArmed,
                "A band rebuilt from nothing must not come back with the expensive button "
                + "already half pressed.");
        }

        [Test]
        public void EveryWordOnTheSwitchExistsInBothLanguages()
        {
            foreach (var language in new[] { Language.English, Language.Polish })
            {
                Loc.Current = language;

                foreach (var key in new[]
                {
                    "ops.dupes.ban_strap", "ops.dupes.ban_once", "ops.dupes.ban_built",
                    "ops.dupes.ban_set", "ops.dupes.ban_sure", "ops.dupes.ban_lift"
                })
                {
                    Assert.That(Loc.T(key), Is.Not.EqualTo(key),
                        $"{key} is missing in {language} and would print as the key itself.");
                }
            }

            Loc.Current = Language.English;
        }
    }
}
