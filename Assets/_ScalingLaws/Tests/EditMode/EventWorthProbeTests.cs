using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What going to events is actually worth, measured against not going.
    ///
    /// **Asked for directly: does this help the player.** Two identical companies, same seed, same
    /// model, same fleet. One books a stand at everything it can reach and pays for every one of
    /// them; the other never leaves the office. Four years, then the difference.
    ///
    /// Three things this fixture is careful about, because each is a way a measurement like this
    /// quietly stops measuring:
    ///
    /// **The attending company pays.** Comparing a company that gets event results against one
    /// that does not, without charging for the stands, measures a gift rather than a decision.
    ///
    /// **Both have something to sell.** A company with no model has no audience, so fans landing
    /// on nobody would make the difference meaningless. That is the fault `RoomOffloadTests`
    /// shipped before it was caught.
    ///
    /// **Every figure is printed.** "More fans" passes on a difference of three people.
    /// </summary>
    public sealed class EventWorthProbeTests
    {
        private const uint Seed = 0x5CA1AB1E;
        private const int Years = 4;

        private static CompanySimulation Company()
        {
            var state = new CompanyState("Prometheus AI", Seed)
            {
                Date = GameDate.FromCalendar(2026, 1, 1),
                CashUsd = 3_000_000_000
            };

            // Near the frontier of its own year, so the company has an audience for the fans to
            // join. A 2022-grade model in 2026 is bought by nobody.
            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 70,
                state.Date.AddDays(-120), 1e12, 1.0));

            // AddDeployedModel only adds to the list; LastReleaseDate is set by the release path and nothing else. Without this a fixture runs a company the game reads as having shipped nothing since day zero, which is -0.0016 a day of staleness for ever.
            state.LastReleaseDate = state.Date.AddDays(-120);

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(400.0);

            return simulation;
        }

        [Test]
        public void GoingToEventsIsWorthMoreThanStayingInTheOffice()
        {
            var goes = Company();
            var stays = Company();

            // The one that goes also pays for the early look, which is the whole point of it.
            goes.SetIntelSubscription(IntelTier.NationalPress, true);
            goes.SetEventEarlyAccess(true);

            var booked = 0;
            var spent = 0L;
            var refused = new List<string>();

            for (var week = 0; week < Years * 52; week++)
            {
                foreach (var (id, year) in EventSeason.VisibleOn(goes.State.Date,
                    goes.HasEventEarlyAccess))
                {
                    if (goes.State.BookedTierFor((int)id, year) != null)
                    {
                        continue;
                    }

                    var price = goes.PriceOfStand(id, year, StandTier.Medium);
                    var before = goes.State.CashUsd;

                    if (goes.TryRegisterForStand(id, year, StandTier.Medium, out var why))
                    {
                        booked++;
                        spent += before - goes.State.CashUsd;
                    }
                    else if (refused.Count < 4)
                    {
                        refused.Add(id + " " + year + " at " + UiLess(price) + ": " + why);
                    }
                }

                goes.Advance(7);
                stays.Advance(7);
            }

            UnityEngine.Debug.Log(
                "WORTH booked=" + booked + " spent=" + UiLess(spent)
                + (refused.Count == 0 ? "" : "\nWORTH refused: " + string.Join(" | ", refused))
                + "\nWORTH fans   goes=" + goes.State.Fans.ToString("N0")
                + " stays=" + stays.State.Fans.ToString("N0")
                + "\nWORTH rep    goes=" + goes.State.Reputation.ToString("0.0000")
                + " stays=" + stays.State.Reputation.ToString("0.0000")
                + "\nWORTH users  goes=" + goes.State.LastQuality.Demanded.ToString("0.0")
                + " stays=" + stays.State.LastQuality.Demanded.ToString("0.0")
                + "\nWORTH cash   goes=" + UiLess(goes.State.CashUsd)
                + " stays=" + UiLess(stays.State.CashUsd));

            Assert.That(booked, Is.GreaterThan(0),
                "Four years of trying and the company never got a stand anywhere, so this measures "
                + "two companies that both stayed in the office. Refusals: "
                + (refused.Count == 0 ? "none recorded" : string.Join(" | ", refused)));

            Assert.That(spent, Is.GreaterThan(0L),
                "Stands were booked and nothing left the account, so this is measuring a gift "
                + "rather than a decision.");

            Assert.That(goes.State.Fans, Is.GreaterThan(stays.State.Fans),
                "Going to " + booked + " events and spending " + UiLess(spent)
                + " left the company with no more of a following than never going at all.");
        }

        /// <summary>Money as a short string, without reaching into `UI/` from a simulation test.</summary>
        private static string UiLess(long amount)
            => "$" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
