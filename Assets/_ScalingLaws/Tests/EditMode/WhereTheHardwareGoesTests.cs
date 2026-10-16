using System.IO;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The player never chooses a compute tier, so the game has to say which one it chose.
    ///
    /// **Reported 14 October 2026 by a tester who had played a whole campaign without finding
    /// colocation**: "I haven't seen it anywhere, and so only have access to renting compute and
    /// using the basement". He had been paying colocation's rack fee and its $0.14 per kWh since
    /// his first accelerator. Nothing on the purchase card said where a card was going, the ladder
    /// said OPEN rather than in use, and the cards went dead rather than refusing out loud while
    /// the tier was still locked.
    ///
    /// A tier that engages silently is indistinguishable from a tier that does not exist. That is
    /// the seventeenth time this project has shipped a mechanism a player could not see, and the
    /// first time the mechanism was running the whole time.
    /// </summary>
    public sealed class WhereTheHardwareGoesTests
    {
        private static readonly string MoneySource = Path.Combine(
            Application.dataPath, "_ScalingLaws", "Scripts", "UI", "GameShell.Money.cs");

        private static CompanySimulation Company()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 77));
            simulation.State.CashUsd = 400_000_000;
            return simulation;
        }

        /// <summary>
        /// Colocation from the first card, and the datacenter only once it is actually running.
        /// Commissioned is not the same as online: there are 300 days between them.
        /// </summary>
        [Test]
        public void HardwareGoesIntoColocationUntilADatacenterIsActuallyOpen()
        {
            var state = Company().State;

            Assert.That(state.HousingTier, Is.EqualTo(ComputeTier.ColocatedServers),
                "A company with no datacenter houses its cards in colocation, from the first one.");

            Assert.IsFalse(state.IsDatacenterOnline,
                "This fixture assumes nothing has been commissioned yet.");
        }

        /// <summary>
        /// One expression, one answer. The buy grid picked the tier inline and the ladder was about
        /// to pick it again, which is how this project keeps acquiring two formulas for one fact.
        /// </summary>
        [Test]
        public void TheScreenAsksTheCompanyWhereCardsGoRatherThanWorkingItOutAgain()
        {
            Assert.IsTrue(File.Exists(MoneySource), $"Cannot find {MoneySource}.");

            var source = File.ReadAllText(MoneySource);

            Assert.That(source,
                Does.Not.Contain("IsTierUnlocked(ComputeTier.OwnDatacenter) && state.IsDatacenterOnline"),
                "The purchase page is working out the housing tier for itself again. "
                + "Ask CompanyState.HousingTier.");

            Assert.That(source, Does.Contain("state.HousingTier"),
                "Nothing on this screen asks where a bought card would actually go.");
        }

        /// <summary>
        /// The card says where the money lands before it leaves, and the locked card still answers.
        ///
        /// `SetEnabled(false)` dispatches no pointer event, so a shut card cannot explain itself.
        /// This project has shipped that three times before: the precision cards, the deduplication
        /// cards and the fan button.
        /// </summary>
        [Test]
        public void ThePurchaseCardNamesItsTierAndAShutCardStillAnswers()
        {
            var source = File.ReadAllText(MoneySource);

            Assert.That(source, Does.Contain("money.card.housed"),
                "The purchase card does not say which tier it buys into.");

            Assert.That(source, Does.Not.Contain("card.SetEnabled(unlocked)"),
                "A hardware card is disabled again, so with colocation still locked the whole "
                + "grid takes clicks and answers nothing.");
        }

        /// <summary>Both new phrases exist in both languages, or they render as their own keys.</summary>
        [Test]
        public void TheNewPhrasesExistInBothLanguages()
        {
            foreach (var language in new[] { Language.English, Language.Polish })
            {
                Loc.Current = language;

                foreach (var key in new[] { "money.card.housed", "fleet.tier_in_force" })
                {
                    Assert.That(Loc.T(key), Is.Not.EqualTo(key),
                        $"{key} is missing in {language} and would print as the key itself.");
                }
            }

            Loc.Current = Language.English;
        }
    }
}
