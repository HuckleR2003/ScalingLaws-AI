using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Debt is the counterweight to the funding rounds: cheap once, relentless afterwards. These
    /// check that it behaves like a loan and not like free money.
    /// </summary>
    public sealed class DebtTests
    {
        private static CompanySimulation Company(long cash, GameDate date, double capability = 0.0)
        {
            var state = new CompanyState("Borrower", 77)
            {
                Date = date,
                CashUsd = cash
            };

            if (capability > 0.0)
            {
                state.AddDeployedModel(new DeployedModel(
                    "Flagship", ArchitectureId.DenseTransformer, capability, date, 2e10, 1.0));
            }

            return new CompanySimulation(state);
        }

        [Test]
        public void EveryProductRepaysMoreThanItLends()
        {
            foreach (var definition in LoanCatalog.All)
            {
                Assert.That(definition.TotalRepaymentUsd, Is.GreaterThan(definition.PrincipalUsd),
                    $"{definition.Product} is free money.");
                Assert.That(definition.RepaymentMultiple, Is.GreaterThan(1.0));
                Assert.That(definition.GraceDays, Is.LessThan(definition.TermDays));
                Assert.That(definition.DailyInstalmentUsd, Is.GreaterThan(0L));
                Assert.That(definition.Description, Is.Not.Empty);
            }
        }

        [Test]
        public void TheSovereignProgrammeIsTheLargestSumAndTheHarshestTerms()
        {
            var sovereign = LoanCatalog.Get(LoanProduct.SovereignCompute);

            Assert.That(sovereign.PrincipalUsd, Is.EqualTo(10_000_000_000L));
            Assert.That(sovereign.RepaymentMultiple, Is.EqualTo(2.25).Within(1e-9));
            Assert.That(sovereign.TotalRepaymentUsd, Is.EqualTo(22_500_000_000L));
            Assert.That(sovereign.TermDays / 365.0, Is.InRange(10.0, 12.0), "About eleven years.");

            foreach (var other in LoanCatalog.All)
            {
                if (other.Product == LoanProduct.SovereignCompute)
                {
                    continue;
                }

                Assert.That(sovereign.PrincipalUsd, Is.GreaterThan(other.PrincipalUsd));
                Assert.That(sovereign.RepaymentMultiple, Is.GreaterThan(other.RepaymentMultiple));
                Assert.That(sovereign.ReputationOnDefault, Is.GreaterThan(other.ReputationOnDefault));
            }
        }

        [Test]
        public void ALabWithNothingToShowCannotBorrowAnything()
        {
            var simulation = Company(50_000_000, GameDate.Start);

            foreach (var offer in simulation.LoanOffers())
            {
                Assert.That(offer.IsAvailable, Is.False, $"{offer.Product} was offered on day one.");
                Assert.That(offer.Reason, Is.Not.Empty);
            }

            Assert.That(simulation.TryTakeLoan(LoanProduct.SovereignCompute, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(50_000_000L), "A refused draw must not move money.");
        }

        [Test]
        public void DrawingAFacilityAddsCashWithoutTouchingTheCapTable()
        {
            var simulation = Company(20_000_000, GameDate.FromCalendar(2022, 6, 1), capability: 22.0);
            var founderEquityBefore = simulation.State.CapTable.FounderEquity;

            Assert.That(simulation.TryTakeLoan(LoanProduct.BridgeFacility, out var reason), Is.True, reason);

            var definition = LoanCatalog.Get(LoanProduct.BridgeFacility);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(20_000_000L + definition.PrincipalUsd));
            Assert.That(simulation.State.CapTable.FounderEquity, Is.EqualTo(founderEquityBefore),
                "Debt is the whole point: it does not dilute.");
            Assert.That(simulation.State.Loans.OpenCount, Is.EqualTo(1));
            Assert.That(simulation.State.Loans.TotalOutstandingUsd, Is.EqualTo(definition.TotalRepaymentUsd));
        }

        [Test]
        public void NothingIsOwedDuringTheGracePeriodAndEverythingIsAfterIt()
        {
            var simulation = Company(20_000_000, GameDate.FromCalendar(2022, 6, 1), capability: 22.0);
            simulation.TryTakeLoan(LoanProduct.BridgeFacility, out _);

            var loan = simulation.State.Loans.Loans[0];
            var grace = LoanCatalog.Get(LoanProduct.BridgeFacility).GraceDays;

            Assert.That(loan.IsInGracePeriod(simulation.State.Date), Is.True);
            Assert.That(simulation.State.Loans.DailyServiceUsd(simulation.State.Date), Is.Zero);

            simulation.Advance(grace + 5);

            Assert.That(loan.IsInGracePeriod(simulation.State.Date), Is.False);
            Assert.That(loan.RepaidUsd, Is.GreaterThan(0L), "The schedule starts on its own.");
        }

        [Test]
        public void AFacilityHeldToTermRepaysExactlyWhatWasAgreedAndNoMore()
        {
            var simulation = Company(400_000_000, GameDate.FromCalendar(2022, 6, 1), capability: 22.0);
            simulation.TryTakeLoan(LoanProduct.BridgeFacility, out _);

            var definition = LoanCatalog.Get(LoanProduct.BridgeFacility);
            var loan = simulation.State.Loans.Loans[0];

            simulation.Advance(definition.TermDays + 120);

            Assert.That(loan.IsSettled, Is.True, $"Still owes ${loan.OutstandingUsd:N0}.");
            Assert.That(loan.RepaidUsd, Is.EqualTo(definition.TotalRepaymentUsd));
            Assert.That(loan.OutstandingUsd, Is.Zero);
        }

        [Test]
        public void ScheduledRepaymentsLeaveTheAccountWhateverElseIsHappening()
        {
            var borrowed = Company(60_000_000, GameDate.FromCalendar(2022, 6, 1), capability: 22.0);
            borrowed.TryTakeLoan(LoanProduct.BridgeFacility, out _);

            var quiet = Company(60_000_000 + LoanCatalog.Get(LoanProduct.BridgeFacility).PrincipalUsd,
                GameDate.FromCalendar(2022, 6, 1), capability: 22.0);

            borrowed.Advance(400);
            quiet.Advance(400);

            Assert.That(borrowed.State.CashUsd, Is.LessThan(quiet.State.CashUsd),
                "The same starting cash, minus a year of instalments, has to be visibly less.");
        }

        [Test]
        public void MissingThePaymentsLongEnoughCostsStandingRatherThanEndingInstantly()
        {
            // Enough to draw, nowhere near enough to service once the grace period ends.
            var simulation = Company(6_000_000, GameDate.FromCalendar(2022, 6, 1), capability: 22.0);
            simulation.SetRentedPetaflops(0.0);
            simulation.TryTakeLoan(LoanProduct.BridgeFacility, out _);

            // Spend the principal so the account cannot meet the schedule.
            simulation.State.CashUsd = 200_000;
            var reputationBefore = simulation.State.Reputation;

            var grace = LoanCatalog.Get(LoanProduct.BridgeFacility).GraceDays;
            simulation.Advance(grace + LoanBook.ArrearsBeforeDefault + 5);

            Assert.That(simulation.State.Reputation, Is.LessThan(reputationBefore),
                "A called default has to cost standing.");
            Assert.That(simulation.State.Loans.Loans[0].OutstandingUsd, Is.GreaterThan(0L),
                "Defaulting does not clear the debt.");
        }

        [Test]
        public void OnlyThreeFacilitiesCanBeOpenAtOnce()
        {
            var state = new CompanyState("Leveraged", 5)
            {
                Date = GameDate.FromCalendar(2026, 6, 1),
                CashUsd = 40_000_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Flagship", ArchitectureId.DenseTransformer, 90, state.Date, 2e10, 1.0));

            foreach (var node in ResearchTree.All)
            {
                state.UnlockedResearch.Add(node.Id);
            }

            for (var day = 0; day < CompanyState.RevenueWindowDays; day++)
            {
                state.RecordDailyRevenue(20_000_000);
            }

            var simulation = new CompanySimulation(state);

            Assert.That(simulation.TryTakeLoan(LoanProduct.BridgeFacility, out var r1), Is.True, r1);
            Assert.That(simulation.TryTakeLoan(LoanProduct.VentureDebt, out var r2), Is.True, r2);
            Assert.That(simulation.TryTakeLoan(LoanProduct.CorporateBond, out var r3), Is.True, r3);
            Assert.That(simulation.TryTakeLoan(LoanProduct.SovereignCompute, out var r4), Is.False);
            Assert.That(r4, Does.Contain("3 facilities"));
        }

        [Test]
        public void TheSovereignProgrammeNeedsTheEndGameResearchAndRealRevenue()
        {
            var state = new CompanyState("Sovereign", 9)
            {
                Date = GameDate.FromCalendar(2026, 6, 1),
                CashUsd = 1_000_000_000
            };
            state.AddDeployedModel(new DeployedModel(
                "Flagship", ArchitectureId.DenseTransformer, 90, state.Date, 2e10, 1.0));
            for (var day = 0; day < CompanyState.RevenueWindowDays; day++)
            {
                state.RecordDailyRevenue(20_000_000);
            }

            var simulation = new CompanySimulation(state);

            Assert.That(simulation.TryTakeLoan(LoanProduct.SovereignCompute, out var reason), Is.False);
            Assert.That(reason, Does.Contain("Recursive self-improvement"));

            state.UnlockedResearch.Add(ResearchNodeId.RecursiveSelfImprovement);
            Assert.That(simulation.TryTakeLoan(LoanProduct.SovereignCompute, out var second), Is.True, second);
            Assert.That(state.CashUsd, Is.EqualTo(11_000_000_000L));
        }

        [Test]
        public void ALoanBookSurvivesASaveAndReload()
        {
            var simulation = Company(60_000_000, GameDate.FromCalendar(2022, 6, 1), capability: 22.0);
            simulation.TryTakeLoan(LoanProduct.BridgeFacility, out _);
            simulation.Advance(200);

            var original = simulation.State;
            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original))));

            Assert.That(restored.Loans.OpenCount, Is.EqualTo(original.Loans.OpenCount));
            Assert.That(restored.Loans.TotalOutstandingUsd, Is.EqualTo(original.Loans.TotalOutstandingUsd));

            var before = original.Loans.Loans[0];
            var after = restored.Loans.Loans[0];
            Assert.That(after.Product, Is.EqualTo(before.Product));
            Assert.That(after.TakenOn, Is.EqualTo(before.TakenOn));
            Assert.That(after.RepaidUsd, Is.EqualTo(before.RepaidUsd));
            Assert.That(after.DaysInArrears, Is.EqualTo(before.DaysInArrears));
        }

        [Test]
        public void DebtAndEquityAreOppositeTrades()
        {
            // The point of having both. Equity costs a permanent slice and never has to be repaid;
            // debt costs nothing permanent and has to be repaid on a schedule.
            var date = GameDate.FromCalendar(2024, 6, 1);
            var bridge = LoanCatalog.Get(LoanProduct.BridgeFacility);

            var borrower = Company(80_000_000, date, capability: 45.0);
            borrower.TryTakeLoan(LoanProduct.BridgeFacility, out _);

            Assert.That(borrower.State.CapTable.FounderEquity, Is.EqualTo(1.0),
                "A facility must never touch ownership.");
            Assert.That(borrower.State.Loans.TotalOutstandingUsd,
                Is.EqualTo(bridge.TotalRepaymentUsd),
                "And it must never be forgiven either.");
        }
    }
}
