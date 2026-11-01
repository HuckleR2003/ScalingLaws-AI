using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Why a company that is doing everything right still reads zero regard.
    ///
    /// **This exists because the diagnosis was wrong twice.** Both times it came from quoting
    /// `CLAUDE.md`'s balance table, which says `ServiceGain` wants a tenth of the market and
    /// `DailyDrift` is a flat -0.0006. Neither is true of the code any more: the share gate was
    /// replaced with a headcount knee at two million users, and the drift is a share of what the
    /// company has rather than a fixed subtraction, so it cannot drive anything to zero on its own.
    ///
    /// Arithmetic on those numbers says a company serving two million people should settle at
    /// 0.0012 / 0.0015, which is **0.8**, and two separate probes measured **0.0000**. One of those
    /// is wrong and this fixture is here to say which, by printing every driver rather than
    /// reasoning about them.
    /// </summary>
    public sealed class StandingDriverProbeTests
    {
        private const uint Seed = 0x5CA1AB1E;

        /// <summary>
        /// A company that ships regularly, serves millions and never goes stale.
        ///
        /// Releases every 180 days against `Standing.FreshDays` of 240, so the staleness term is
        /// zero throughout: whatever drags the regard down here, it is not a company that stopped
        /// trying.
        /// </summary>
        [Test]
        public void TheDriversBehindAWorkingCompanysRegard()
        {
            var state = new CompanyState("Prometheus AI", Seed)
            {
                Date = GameDate.FromCalendar(2026, 1, 1),
                CashUsd = 3_000_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 70,
                state.Date.AddDays(-30), 1e12, 1.0));

            // **AddDeployedModel only adds to the list; LastReleaseDate is set by the release path and nothing else. Without this a fixture runs a company the game reads as having shipped nothing since day zero, which is -0.0016 a day of staleness for ever.**
            // The project's own fixtures set it by hand for the same reason; mine did not,
            // and that is what every zero in the first reading of this table was.
            state.LastReleaseDate = state.Date.AddDays(-30);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(3_000.0);

            var rows = new List<string>();

            for (var half = 0; half < 8; half++)
            {
                simulation.Advance(180);

                // Ship again, so the line is never older than FreshDays.
                state.AddDeployedModel(new DeployedModel(
                    "Aurora " + (half + 2), ArchitectureId.DenseTransformer, 70 + 2.0 * half,
                    state.Date, 1e12, 1.0));

                state.LastReleaseDate = state.Date;

                var change = state.LastStandingChange;
                var quality = state.LastQuality;

                rows.Add("DRIVER +" + ((half + 1) * 180) + "d"
                    + " rep=" + state.Reputation.ToString("0.0000")
                    + " fans=" + state.Fans.ToString("N0")
                    + "\n    service=" + change.Service.ToString("0.000000")
                    + " free=" + change.FreeTier.ToString("0.000000")
                    + " age=" + change.ModelAge.ToString("0.000000")
                    + " price=" + change.Price.ToString("0.000000")
                    + "\n    marketing=" + change.Marketing.ToString("0.000000")
                    + " drift=" + change.Drift.ToString("0.000000")
                    + " checks=" + change.IdentityChecks.ToString("0.000000")
                    + " TOTAL=" + change.Total.ToString("0.000000")
                    + "\n    demanded=" + quality.Demanded.ToString("0.0")
                    + " capacity=" + quality.Capacity.ToString("0.0")
                    + " load=" + quality.Utilisation.ToString("0.000")
                    + " daysSinceRelease="
                    + (state.Date.DayIndex - state.LastReleaseDate.DayIndex));
            }

            UnityEngine.Debug.Log(string.Join("\n", rows));

            // **No assertion about the value, on purpose.** This fixture is a measurement, and
            // putting a number on it before knowing which driver is responsible would be writing
            // the answer down before reading it. What it does hold is that the drivers were
            // actually computed: an all-zero change means the standing was never evaluated and the
            // table above is a picture of nothing.
            var last = state.LastStandingChange;

            Assert.That(last.ModelAge, Is.EqualTo(0.0).Within(1e-12),
                "The line reads as stale while shipping every 180 days against a 240 day "
                + "freshness window, so the fixture is not releasing anything.");

            // **The drift assertion that used to be here was wrong.** It read a zero drift as
            // proof the standing was never evaluated, and a proportional drift is legitimately
            // zero at zero regard, which is exactly the state this fixture is investigating.
            // The freshness check above is the one that catches a fixture not releasing.
        }
        /// <summary>
        /// The same company, charging what the market charges, and the difference is the finding.
        ///
        /// **The subscription is a fixed fee against a market rate that halves every year.** That
        /// is written down as deliberate, with a note that the planner never shows the player the
        /// market equivalent. What it costs is measured here: a company that never touches the
        /// slider is paying the maximum price penalty to its standing for most of a campaign, and
        /// that penalty is what holds the regard down rather than anything about serving people.
        /// </summary>
        [Test]
        public void KeepingThePriceNearTheMarketIsWorthMoreThanAnythingElseOnThisScreen()
        {
            var drifts = Build();
            var tracks = Build();

            var rows = new List<string>();

            for (var half = 0; half < 8; half++)
            {
                // The one that tracks resets its fee to whatever a month of tokens is worth now.
                var month = MonetizationCatalog.TokensPerSubscriberPerMonthOn(tracks.State.Date);
                var market = tracks.Market.PricePerMillionTokensUsd;

                tracks.State.Monetization.SubscriptionPriceUsdPerMonth =
                    market * (month / 1_000_000.0);

                foreach (var simulation in new[] { drifts, tracks })
                {
                    simulation.Advance(180);

                    simulation.State.AddDeployedModel(new DeployedModel(
                        "Aurora " + (half + 2), ArchitectureId.DenseTransformer,
                        70 + 2.0 * half, simulation.State.Date, 1e12, 1.0));

                    simulation.State.LastReleaseDate = simulation.State.Date;
                }

                rows.Add("PRICE +" + ((half + 1) * 180) + "d"
                    + "  drifting rep=" + drifts.State.Reputation.ToString("0.000")
                    + " fans=" + drifts.State.Fans.ToString("N0")
                    + " pricePenalty="
                    + drifts.State.LastStandingChange.Price.ToString("0.000000")
                    + "\n          tracking rep=" + tracks.State.Reputation.ToString("0.000")
                    + " fans=" + tracks.State.Fans.ToString("N0")
                    + " pricePenalty="
                    + tracks.State.LastStandingChange.Price.ToString("0.000000")
                    + " fee=$"
                    + tracks.State.Monetization.SubscriptionPriceUsdPerMonth.ToString("0"));
            }

            UnityEngine.Debug.Log(string.Join("\n", rows));

            Assert.That(tracks.State.Reputation, Is.GreaterThan(drifts.State.Reputation),
                "Charging the going rate left the company no better thought of than charging ten "
                + "times it, so the price driver reaches nothing and the note in CLAUDE.md about "
                + "a fixed fee against a halving market is describing something that does not "
                + "happen.");
        }

        private static CompanySimulation Build()
        {
            var state = new CompanyState("Prometheus AI", Seed)
            {
                Date = GameDate.FromCalendar(2026, 1, 1),
                CashUsd = 3_000_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 70,
                state.Date.AddDays(-30), 1e12, 1.0));

            state.LastReleaseDate = state.Date.AddDays(-30);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(3_000.0);

            return simulation;
        }
    }
}
