using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Long campaigns, several seeds, and every screen built along the way.
    ///
    /// **This fixture exists for the sitting the author is about to do**, not for a bug already
    /// found. The unit tests cover mechanisms one at a time over weeks of game time; a person
    /// playing for an evening runs fifteen years with saves in the middle, and the failures that
    /// live there are the ones nothing else looks for: a number that only goes wrong once it is
    /// large, a screen that only throws when a list is empty in year twelve, state that survives one
    /// save and not three.
    ///
    /// Nothing here asserts balance. `PlayabilityTests` owns that. These only ask whether the game
    /// keeps working.
    /// </summary>
    public sealed class SoakTests
    {
        /// <summary>Start of 2022 to the end of the audience curves, which is the whole game.</summary>
        private const int FullCampaignDays = 5110;

        private static readonly uint[] Seeds = { 11, 4242, 90210, 777, 31337 };

        private static CompanySimulation Playing(uint seed)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(60.0);

            simulation.State.AddDeployedModel(new DeployedModel("Atlas One",
                ArchitectureId.DenseTransformer, 42.0, simulation.State.Date, 2e10, 1.0,
                ModelType.General, "Atlas"));

            return simulation;
        }

        private static void AssertSane(CompanyState state, string when)
        {
            Assert.IsFalse(double.IsNaN(state.Reputation), $"{when}: reputation is NaN.");
            Assert.IsFalse(double.IsNaN(state.Fans), $"{when}: fans are NaN.");
            Assert.IsFalse(double.IsNaN(state.BestCapability), $"{when}: capability is NaN.");

            Assert.That(state.Reputation, Is.InRange(0.0, 1.0), $"{when}: reputation out of range.");
            Assert.GreaterOrEqual(state.Fans, 0.0, $"{when}: negative fans.");
            Assert.GreaterOrEqual(state.AccruedTaxUsd, 0L, $"{when}: negative tax accrual.");

            // Cash may legitimately be negative while a company is failing. Absurd is the problem.
            Assert.That(Math.Abs(state.CashUsd), Is.LessThan(1_000_000_000_000L),
                $"{when}: cash has run away to {state.CashUsd:N0}.");

            foreach (var model in state.DeployedModels)
            {
                Assert.IsFalse(double.IsNaN(model.Capability), $"{when}: {model.Name} capability NaN.");
                Assert.GreaterOrEqual(model.LifetimeRevenueUsd, 0L,
                    $"{when}: {model.Name} has earned a negative amount.");
            }
        }

        // ---- the long run ---------------------------------------------------------------------

        [Test]
        public void AFullCampaignRunsToTheEndOnEverySeed()
        {
            foreach (var seed in Seeds)
            {
                var simulation = Playing(seed);

                for (var day = 0; day < FullCampaignDays; day++)
                {
                    simulation.AdvanceDay();

                    // Checked every quarter rather than every day: often enough to name the year a
                    // number went wrong in, cheap enough to run five of these.
                    if (day % 90 == 0)
                    {
                        AssertSane(simulation.State, $"seed {seed}, {simulation.State.Date}");
                    }
                }

                AssertSane(simulation.State, $"seed {seed}, end");
            }
        }

        /// <summary>
        /// Saving and loading over and over, which is what an evening of play actually does.
        ///
        /// One round trip is already covered. This is about the third and the tenth: state that is
        /// restored slightly wrong compounds, and a single save hides it.
        /// </summary>
        [Test]
        public void TenSaveAndLoadCyclesAcrossADecadeChangeNothing()
        {
            var direct = Playing(2024);
            var cycled = Playing(2024);

            for (var block = 0; block < 10; block++)
            {
                for (var day = 0; day < 365; day++)
                {
                    direct.AdvanceDay();
                    cycled.AdvanceDay();
                }

                var restored = SaveStore.Restore(
                    SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(cycled.State))));

                cycled = new CompanySimulation(restored);

                Assert.AreEqual(direct.State.Date.DayIndex, cycled.State.Date.DayIndex,
                    $"Cycle {block + 1}: the date drifted.");

                Assert.AreEqual(direct.State.CashUsd, cycled.State.CashUsd,
                    $"Cycle {block + 1}: cash drifted by "
                    + $"{direct.State.CashUsd - cycled.State.CashUsd:N0}. Something causal is not "
                    + "being saved, and it compounds.");

                Assert.AreEqual(direct.State.BestCapability, cycled.State.BestCapability, 1e-9,
                    $"Cycle {block + 1}: capability drifted.");

                Assert.AreEqual(direct.State.Mail.All.Count, cycled.State.Mail.All.Count,
                    $"Cycle {block + 1}: the inbox drifted.");
            }
        }

        [Test]
        public void ADecadeOfDaysNeverRaisesAnEventItCannotDescribe()
        {
            var simulation = Playing(555);

            for (var day = 0; day < 3650; day++)
            {
                simulation.AdvanceDay();

                while (simulation.State.TryDequeueEvent(out var raised))
                {
                    Assert.IsNotNull(raised.Message, $"{raised.Type} raised with a null message.");
                    Assert.IsTrue(Enum.IsDefined(typeof(CompanyEventType), raised.Type),
                        $"An event of type {(int)raised.Type} is not in the enum.");
                }
            }
        }

        // ---- every screen, at several points in the campaign -----------------------------------

        /// <summary>
        /// The screens that can be built without a scene, at four ages of company.
        ///
        /// Year one is the empty case, which is the one a new player sees and the one most likely to
        /// divide by a count of zero. Year fourteen is the case with a long history behind it, which
        /// is the one nobody plays to during development.
        /// </summary>
        [Test]
        public void EveryScreenBuildsAtEveryAgeOfCompany()
        {
            var simulation = Playing(8080);
            var checkpoints = new[] { 0, 400, 1800, 5110 };
            var reached = 0;

            for (var day = 0; day <= FullCampaignDays; day++)
            {
                if (Array.IndexOf(checkpoints, day) >= 0)
                {
                    var when = $"day {day} ({simulation.State.Date})";
                    reached++;

                    Assert.DoesNotThrow(() =>
                    {
                        var management = new ManagementScreen(simulation,
                            () => { }, () => { }, () => { }, () => { });

                        management.Refresh();
                        management.ShowDesk(true);
                        management.ShowArchive();
                    }, $"{when}: the management screen threw.");

                    Assert.DoesNotThrow(() =>
                    {
                        var news = new NewsScreen(simulation, (_, _) => { });
                        news.Refresh();
                    }, $"{when}: the news screen threw.");

                    Assert.DoesNotThrow(() =>
                    {
                        var mail = new MailScreen(simulation, () => { });
                        mail.Refresh();

                        // And every letter in it, because a reading pane is a different code path
                        // from a list row and the archive found that the hard way.
                        foreach (var letter in simulation.State.Mail.All)
                        {
                            mail.Select(letter.Id);
                        }
                    }, $"{when}: the inbox threw.");

                    Assert.DoesNotThrow(() =>
                    {
                        var banner = new NewsBanner(() => simulation.State.News, () => { });
                        banner.Refresh();
                    }, $"{when}: the news banner threw.");
                }

                if (day < FullCampaignDays)
                {
                    simulation.AdvanceDay();
                }
            }

            Assert.AreEqual(checkpoints.Length, reached, "Not every checkpoint was reached.");
        }

        [Test]
        public void TheRankingBoardNamesALabForEveryRowAcrossTheCampaign()
        {
            var simulation = Playing(4321);

            for (var year = 0; year < 14; year++)
            {
                for (var day = 0; day < 365; day++)
                {
                    simulation.AdvanceDay();
                }

                var board = simulation.Ranking();
                Assert.IsNotEmpty(board, $"Year {year + 1}: the board is empty.");

                foreach (var entry in board)
                {
                    Assert.IsNotEmpty(entry.LabName, $"Year {year + 1}: a row with no lab name.");

                    // The player carries None deliberately and falls back to an initial. A rival
                    // carrying None would be a row whose mark can never resolve.
                    if (!entry.IsPlayer)
                    {
                        Assert.AreNotEqual(CompetitorId.None, entry.Competitor,
                            $"Year {year + 1}: {entry.LabName} has no lab id, so it can never show "
                            + "its mark.");
                    }
                }
            }
        }

        // ---- deferring tax, which is the newest rule and the one with a ceiling ------------------

        private static MailItem NextTaxDemand(CompanySimulation simulation, int within = 800)
        {
            for (var day = 0; day < within; day++)
            {
                simulation.AdvanceDay();

                foreach (var letter in simulation.State.Mail.All)
                {
                    if (letter.Kind == MailKind.TaxDemand && !letter.IsClosed)
                    {
                        return letter;
                    }
                }
            }

            return null;
        }

        [Test]
        public void DeferringCostsInterestAndMovesTheDate()
        {
            var simulation = Playing(6060);
            simulation.SetRentedPetaflops(120.0);

            var demand = NextTaxDemand(simulation);
            Assert.IsNotNull(demand, "No tax demand arrived to defer.");

            var owed = demand.AmountUsd;
            var due = demand.DueDayIndex;

            Assert.IsTrue(simulation.TryActOnMail(demand.Id, MailAction.Defer, out var reason), reason);

            var expected = owed + (long)Math.Round(owed * CompanySimulation.DeferralInterest);
            Assert.AreEqual(expected, demand.AmountUsd, "The interest is not what was advertised.");
            Assert.AreEqual(due + CompanySimulation.DeferralStepDays, demand.DueDayIndex);
            Assert.IsFalse(demand.IsClosed, "Deferring does not settle it.");
        }

        /// <summary>
        /// The ceiling, and the reason there is one: at simple interest a company could roll the
        /// debt forever for a fixed annual fee, which is a loan with no lender and no limit.
        /// </summary>
        [Test]
        public void TaxCanBeDeferredToTheCeilingAndNoFurther()
        {
            var simulation = Playing(6061);
            simulation.SetRentedPetaflops(120.0);

            var demand = NextTaxDemand(simulation);
            Assert.IsNotNull(demand);

            var deferrals = 0;
            while (simulation.TryActOnMail(demand.Id, MailAction.Defer, out _))
            {
                deferrals++;
                Assert.Less(deferrals, 20, "Deferring never ran out, so the ceiling does nothing.");
            }

            Assert.Greater(deferrals, 1, "One deferral is not an arrangement.");

            Assert.AreEqual(CompanySimulation.LongestDeferralDays, demand.DeferredDays,
                "It should stop exactly on the ceiling rather than short of it.");

            Assert.IsFalse(demand.Actions.Contains(MailAction.Defer),
                "At the ceiling the option has to be gone, not present and refusing.");

            Assert.IsTrue(demand.Actions.Contains(MailAction.Pay), "Paying is always still offered.");
        }

        [Test]
        public void DeferringToTheCeilingCostsMoreThanTwentyPercent()
        {
            var simulation = Playing(6062);
            simulation.SetRentedPetaflops(120.0);

            var demand = NextTaxDemand(simulation);
            Assert.IsNotNull(demand);

            var original = demand.AmountUsd;
            while (simulation.TryActOnMail(demand.Id, MailAction.Defer, out _))
            {
            }

            var growth = (double)demand.AmountUsd / original - 1.0;

            // Compounding on what is already owed, so three steps cost more than three times one.
            Assert.Greater(growth, 0.20,
                $"Two and a half years of deferral cost {growth:P1}, which is cheap enough that "
                + "deferring becomes the default rather than a decision.");
        }

        [Test]
        public void OnlyTheRevenueWaitsAndAPenaltyDoesNot()
        {
            var simulation = Playing(6063);
            var fine = simulation.State.Mail.Add(MailKind.Fine, simulation.State.Date,
                "Regulator", "Penalty", "Pay up.");

            fine.AmountUsd = 500_000L;
            fine.DueDayIndex = simulation.State.Date.DayIndex + 45;

            Assert.IsFalse(simulation.TryActOnMail(fine.Id, MailAction.Defer, out var reason));
            Assert.IsTrue(reason.Contains("penalty", StringComparison.Ordinal) || reason.Contains("revenue", StringComparison.Ordinal), reason);
            Assert.IsFalse(fine.Actions.Contains(MailAction.Defer));
        }

        [Test]
        public void ADeferredDemandStillSurvivesASave()
        {
            var simulation = Playing(6064);
            simulation.SetRentedPetaflops(120.0);

            var demand = NextTaxDemand(simulation);
            Assert.IsNotNull(demand);

            simulation.TryActOnMail(demand.Id, MailAction.Defer, out _);

            var owed = demand.AmountUsd;
            var deferred = demand.DeferredDays;

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.IsTrue(restored.Mail.TryGet(demand.Id, out var back));
            Assert.AreEqual(owed, back.AmountUsd);
            Assert.AreEqual(deferred, back.DeferredDays,
                "Losing the count would let the player defer past the ceiling by saving.");
        }

        /// <summary>
        /// A company that defers everything it can and pays nothing is the worst case for the whole
        /// demand system. It has to end badly and it has to end without throwing.
        /// </summary>
        [Test]
        public void ACompanyThatDefersEverythingAndPaysNothingStillRunsToTheEnd()
        {
            var simulation = Playing(6065);
            simulation.SetRentedPetaflops(120.0);

            var deferrals = 0;

            for (var day = 0; day < FullCampaignDays; day++)
            {
                simulation.AdvanceDay();

                foreach (var letter in simulation.State.Mail.All)
                {
                    if (letter.Kind == MailKind.TaxDemand && !letter.IsClosed
                        && letter.Actions.Contains(MailAction.Defer)
                        && simulation.TryActOnMail(letter.Id, MailAction.Defer, out _))
                    {
                        deferrals++;
                    }
                }

                if (day % 180 == 0)
                {
                    AssertSane(simulation.State, $"deferring everything, {simulation.State.Date}");
                }
            }

            Assert.Greater(deferrals, 0, "Nothing was ever deferred, so this tested nothing.");
            AssertSane(simulation.State, "deferring everything, end");
        }
    }
}
