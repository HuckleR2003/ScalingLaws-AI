using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The wire between the routine and a model in the room.
    ///
    /// There is no scene here, so no founder ever spawns and the actor stays null. That is exactly
    /// what makes these worth writing: **every one of them is the case where the office scene is not
    /// loaded**, and a player must not be stranded because of it.
    /// </summary>
    public sealed class FounderPresenceTests
    {
        private static FounderPresence Presence() =>
            new(() => new CompanyState("Prometheus AI"));

        [Test]
        public void ThePrefabIsSomewhereTheGameCanActuallyLoadItFrom()
        {
            // Resources, not Art. The shell spawns it at runtime, and Resources.Load cannot see
            // anything outside a Resources folder: this was in Art and would have failed silently.
            var prefab = Resources.Load<GameObject>(FounderPresence.PrefabPath);

            Assert.IsNotNull(prefab,
                $"Nothing at Resources/{FounderPresence.PrefabPath}. Run "
                + "Scaling Laws > Characters > Build founder rig and clips.");

            Assert.IsNotNull(prefab.GetComponent<OfficeActor>());
        }

        [Test]
        public void TheGroupItSpawnsIntoIsOneTheRoomBuilderMakes()
        {
            var builder = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "_ScalingLaws", "Editor", "OfficeRoomBuilder.cs"));

            StringAssert.Contains($"\"{FounderPresence.StaffGroup}\"", builder,
                "The founder spawns into a group the room does not have, so it lands at the origin.");
        }

        [Test]
        public void SpawningWithNoSceneIsQuietAndHappensOnce()
        {
            var presence = Presence();

            // No office scene in an EditMode test, so this finds nothing. It must not throw, and it
            // must not go on searching the hierarchy every frame for a group that will never exist.
            presence.Spawn();
            presence.Spawn();

            Assert.AreEqual(FounderTask.Working, presence.Task);
        }

        [Test]
        public void OpeningTheMapDoesNotSendAnybodyWalking()
        {
            var presence = Presence();
            presence.Spawn();

            // The founder used to walk out to the car and the screen followed when they arrived.
            // The map opens on the click now, so nothing here may answer with a journey.
            for (var day = 0; day < FounderRoutine.RestIntervalDays; day++)
            {
                presence.Refresh(day);

                Assert.AreNotEqual(FounderTask.Leaving, presence.Task);
                Assert.AreNotEqual(FounderTask.Away, presence.Task);
            }
        }

        [Test]
        public void TheClockOnlyEverSpeedsUpForSleeping()
        {
            var presence = Presence();

            presence.Refresh(1);
            Assert.AreEqual(FounderTask.Working, presence.Task);
            Assert.AreEqual(1f, presence.TimeScale,
                "Nothing but sleeping is allowed to move the clock.");

            presence.Refresh(FounderRoutine.RestIntervalDays);
            Assert.AreEqual(FounderTask.Resting, presence.Task);
            Assert.Greater(presence.TimeScale, 1f);
        }
    }
}
