using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The state programme is a contract, not a pension. From the author's report of 2026-09-18:
    /// *"it never fails, though I have not moved in years and there are scandals, so I cannot go
    /// bankrupt doing nothing"*, and a year's tax of $357.6k on tens of millions a month.
    /// </summary>
    public sealed class StateProgrammeReviewTests
    {
        private static CompanySimulation Signed(double capability)
        {
            var state = new CompanyState("Prometheus AI", 4242u)
            {
                Date = GameDate.FromCalendar(2027, 1, 1),
                CashUsd = 2_000_000_000L,
                Reputation = 0.8
            };

            if (capability > 0.0)
            {
                state.AddDeployedModel(new DeployedModel("Aurora", ArchitectureId.DenseTransformer,
                    capability, state.Date, 2e10, 1.0));
                state.LastReleaseDate = state.Date;
            }

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(4_000.0);
            state.Programme.Sign(state.HomeCountry, state.Date);

            return simulation;
        }

        [Test]
        public void ACompanyThatKeepsShippingIsTrustedAsTheDayItSigned()
        {
            var simulation = Signed(95.0);

            Assert.That(simulation.StateDoubt(), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(simulation.StateRelevance(), Is.EqualTo(1.0).Within(1e-9),
                "A model level with the frontier is paid in full.");
        }

        [Test]
        public void SittingStillWithNoStandingMultipliesTheRiskToTheCap()
        {
            var simulation = Signed(95.0);
            var state = simulation.State;

            var calm = simulation.StateFailureRisk(1.0);

            state.LastReleaseDate = state.Date.AddDays(-900);
            state.Reputation = 0.0;

            Assert.That(simulation.StateDoubt(), Is.EqualTo(CompanySimulation.MostDoubt).Within(1e-9),
                "Two and a half years without a release and no regard left: that is as bad as it gets.");

            // With no sector running the base contract carries no roll at all, so compare the
            // multiplier the roll would use rather than a risk of zero against zero.
            Assert.That(simulation.StateDoubt(), Is.GreaterThan(1.0));
            Assert.That(simulation.StateFailureRisk(1.0), Is.GreaterThanOrEqualTo(calm));
        }

        [Test]
        public void AModelBehindTheFrontierIsPaidLessAndNeverUnderAQuarter()
        {
            // Measured against the frontier the company actually sees, which in a fixture that
            // starts in 2027 without playing the years before is lower than the table's.
            var behind = Signed(0.5);
            var frontier = behind.State.Rivals.FrontierCapability(behind.State.Date);
            var gap = frontier - behind.State.BestCapability;

            Assume.That(gap, Is.GreaterThan(6.0), "The fixture's model is not behind anything.");

            var expected = System.Math.Clamp(1.0 - (gap - 5.0) / 25.0, 0.25, 1.0);

            Assert.That(behind.StateRelevance(), Is.EqualTo(expected).Within(1e-9));
            Assert.That(behind.StateRelevance(), Is.LessThan(1.0));
            Assert.That(behind.StateRelevance(), Is.GreaterThanOrEqualTo(0.25));
        }

        [Test]
        public void TheContractIsTaxedLikeAnyOtherProfit()
        {
            // No model on sale, so the market pays nothing and every dollar of profit is the state's.
            var simulation = Signed(0.0);
            var state = simulation.State;

            for (var day = 0; day < 20; day++)
            {
                simulation.AdvanceDay();

                while (state.TryDequeueEvent(out _))
                {
                }
            }

            Assert.That(state.AccruedTaxUsd, Is.GreaterThan(0L),
                "Twenty days on a state contract and not a dollar of tax accrued on it.");
        }
    }
}
