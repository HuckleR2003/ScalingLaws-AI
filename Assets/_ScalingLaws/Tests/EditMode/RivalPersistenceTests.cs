using System.Globalization;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Ratchets for the rival field surviving a save.
    ///
    /// Every one of these exists because a restored campaign quietly grew a different rival field
    /// from the one that was saved, and none of it was visible until the market started reading
    /// rival quality per product. A campaign that changes when you reload it is not a campaign.
    /// </summary>
    public sealed class RivalPersistenceTests
    {
        private static CompanyState RoundTrip(CompanyState state) =>
            SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(state))));

        private static CompanyState RunFor(int days, uint seed)
        {
            var state = new CompanyState("Reloader", seed);
            var simulation = new CompanySimulation(state);

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return state;
        }

        /// <summary>
        /// The bug the whole hunt ended on.
        ///
        /// A lab that decides to wait for the next accelerator generation pushes its intended
        /// release date months past the date its planned release carries. Restoring the plan wrote
        /// the plan's date over the agent's, which un-waited every patient lab on load and made the
        /// restored field ship early and run ahead of the real one for the rest of the campaign.
        /// </summary>
        [Test]
        public void APatientLabKeepsItsDelayAcrossASave()
        {
            var state = RunFor(900, 901);

            var someoneIsWaiting = false;
            foreach (var agent in state.Rivals.Agents)
            {
                if (agent.TryGetPending(out var pending)
                    && agent.NextReleaseDate.DayIndex != pending.ReleaseDate.DayIndex)
                {
                    someoneIsWaiting = true;
                    break;
                }
            }

            Assert.IsTrue(someoneIsWaiting,
                "No lab has pushed its release past its plan, so this test is exercising nothing. "
                + "Either the waiting mechanic stopped working or the horizon is too short.");

            var restored = RoundTrip(state);

            for (var index = 0; index < state.Rivals.Agents.Count; index++)
            {
                Assert.AreEqual(
                    state.Rivals.Agents[index].NextReleaseDate.DayIndex,
                    restored.Rivals.Agents[index].NextReleaseDate.DayIndex,
                    $"{state.Rivals.Agents[index].LabName} forgot when it meant to ship.");
            }
        }

        [Test]
        public void EveryCausalFieldOnEveryLabSurvivesASave()
        {
            var state = RunFor(1200, 5150);
            var restored = RoundTrip(state);

            for (var index = 0; index < state.Rivals.Agents.Count; index++)
            {
                var before = state.Rivals.Agents[index];
                var after = restored.Rivals.Agents[index];
                var who = before.LabName;

                Assert.AreEqual(before.HasShipped, after.HasShipped, who);
                Assert.AreEqual(before.LiveModelName, after.LiveModelName, who);
                Assert.AreEqual(before.LiveCapability, after.LiveCapability, 1e-9, who);
                Assert.AreEqual(before.LiveBrand, after.LiveBrand, 1e-9, who);
                Assert.AreEqual(before.LivePrice, after.LivePrice, 1e-9, who);
                Assert.AreEqual(before.LiveReleaseDate.DayIndex, after.LiveReleaseDate.DayIndex, who);
                Assert.AreEqual(before.NextReleaseDate.DayIndex, after.NextReleaseDate.DayIndex, who);
                Assert.AreEqual(before.HasPlannedRelease, after.HasPlannedRelease, who);
                Assert.AreEqual(before.AccumulatedDelayDays, after.AccumulatedDelayDays, who);
                Assert.AreEqual(before.IsWaitingForHardware, after.IsWaitingForHardware, who);
                Assert.AreEqual(before.WaitingFor, after.WaitingFor, who);
                Assert.AreEqual(before.Drift, after.Drift, 1e-9, who);
                Assert.AreEqual(before.PendingCapabilityAdjustment, after.PendingCapabilityAdjustment, 1e-9, who);
                Assert.AreEqual(before.PlannedReleasesRemaining, after.PlannedReleasesRemaining, who);

                var hadPending = before.TryGetPending(out var pendingBefore);
                var hasPending = after.TryGetPending(out var pendingAfter);

                Assert.AreEqual(hadPending, hasPending, $"{who} lost the release it was working toward.");
                if (!hadPending)
                {
                    continue;
                }

                Assert.AreEqual(pendingBefore.DisplayName, pendingAfter.DisplayName, who);
                Assert.AreEqual(pendingBefore.ReleaseDate.DayIndex, pendingAfter.ReleaseDate.DayIndex, who);
                Assert.AreEqual(pendingBefore.Capability, pendingAfter.Capability, 1e-9, who);
            }
        }

        /// <summary>
        /// The save format holds about fifteen significant digits, so anything the simulation means
        /// to persist has to live on a grid that survives it. A value that comes back different is
        /// not a rounding annoyance, it is a different campaign.
        /// </summary>
        [Test]
        public void CausalDoublesSurviveTheSaveFormatExactly()
        {
            var awkward = new[] { 1.0999999999999999, 55.262436931952834, 0.0055 * 200.0, 1.0 / 3.0 };

            foreach (var raw in awkward)
            {
                var stored = SimUnits.Storable(raw);
                var written = stored.ToString("G15", CultureInfo.InvariantCulture);
                var reparsed = double.Parse(written, CultureInfo.InvariantCulture);

                Assert.AreEqual(stored, reparsed,
                    $"{raw} does not survive a fifteen digit round trip after quantisation.");
            }
        }

        /// <summary>A restored campaign has to keep producing the same random numbers.</summary>
        [Test]
        public void TheRandomStreamResumesWhereItStopped()
        {
            var state = RunFor(400, 4242);
            var restored = RoundTrip(state);

            Assert.AreEqual(state.Random.State, restored.Random.State);
            Assert.AreEqual(state.Random.NextDouble(), restored.Random.NextDouble(), 1e-15,
                "The next value out of a restored stream has to be the one the saved stream would have given.");
        }
    }
}
