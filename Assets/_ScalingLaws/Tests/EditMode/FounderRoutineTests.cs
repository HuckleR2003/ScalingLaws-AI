using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Where the founder is standing, and whether the room has a point of that name.
    ///
    /// **The room has had nine named waypoints and an empty `Staff` group since the day it was
    /// generated, and nothing ever moved between them.** The routine is the rule half of fixing
    /// that, and it is testable precisely because it answers in waypoint names rather than in
    /// positions.
    /// </summary>
    public sealed class FounderRoutineTests
    {
        [Test]
        public void TheFounderIsAtTheDeskAlmostAlways()
        {
            var working = 0;

            for (var day = 0; day < 365; day++)
            {
                if (FounderRoutine.TaskFor(day, false, false) == FounderTask.Working)
                {
                    working++;
                }
            }

            Assert.Greater(working, 330,
                "Rest is a texture, not a mechanic. A founder who is away often enough to notice is "
                + "a founder the player is waiting on.");
        }

        [Test]
        public void TheFirstDayIsNotANightOff()
        {
            Assert.AreEqual(FounderTask.Working, FounderRoutine.TaskFor(0, false, false),
                "A campaign that opens with the founder walking away from the desk opens by saying "
                + "nothing here is urgent.");
        }

        [Test]
        public void RestComesRoundOnceAFortnight()
        {
            var rests = new List<int>();

            for (var day = 0; day < FounderRoutine.RestIntervalDays * 4; day++)
            {
                if (FounderRoutine.IsRestDay(day))
                {
                    rests.Add(day);
                }
            }

            // Three, not four, across four intervals: day zero is deliberately a working day, so
            // the first night off is day fourteen rather than day one.
            Assert.AreEqual(3, rests.Count, $"Got {string.Join(", ", rests)}");
            Assert.AreEqual(FounderRoutine.RestIntervalDays, rests[0]);

            for (var index = 1; index < rests.Count; index++)
            {
                Assert.AreEqual(FounderRoutine.RestIntervalDays, rests[index] - rests[index - 1]);
            }
        }

        [Test]
        public void ClickingTheMapBeatsGoingToBed()
        {
            // The player clicked something. The game answers the click rather than finishing a nap.
            var restDay = FounderRoutine.RestIntervalDays;
            Assert.IsTrue(FounderRoutine.IsRestDay(restDay), "Picked the wrong day to test.");

            Assert.AreEqual(FounderTask.Leaving,
                FounderRoutine.TaskFor(restDay, isLeaving: true, isAway: false));
        }

        [Test]
        public void TheCountdownAgreesWithTheCalendar()
        {
            for (var day = 0; day < 60; day++)
            {
                var ahead = FounderRoutine.DaysUntilRest(day);

                Assert.Greater(ahead, 0);
                Assert.IsTrue(FounderRoutine.IsRestDay(day + ahead),
                    $"Day {day} says {ahead} days to go and that day is not a rest day.");
            }
        }

        [Test]
        public void SleepingRunsTheClockFasterAndNothingElseDoes()
        {
            Assert.Greater(FounderRoutine.TimeScaleFor(FounderTask.Resting), 1.0f);
            Assert.AreEqual(1.0f, FounderRoutine.TimeScaleFor(FounderTask.Working));
            Assert.AreEqual(1.0f, FounderRoutine.TimeScaleFor(FounderTask.Leaving));
        }

        // ---- the routes have to exist in the room ------------------------------------------------

        [Test]
        public void EveryWaypointTheRoutineAsksForIsOneTheRoomActuallyBuilds()
        {
            // This is the whole reason the routine answers in names. A route through a waypoint the
            // builder never writes is a founder who walks to the origin and stands there, and it
            // would look exactly like a physics bug.
            var builder = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Editor", "OfficeRoomBuilder.cs"));

            var missing = new List<string>();

            foreach (FounderTask task in System.Enum.GetValues(typeof(FounderTask)))
            {
                foreach (var point in FounderRoutine.RouteFor(task))
                {
                    if (!builder.Contains($"\"{point}\""))
                    {
                        missing.Add($"{task} -> {point}");
                    }
                }
            }

            CollectionAssert.IsEmpty(missing,
                "The routine walks to points the room does not have: " + string.Join(", ", missing));
        }

        [Test]
        public void GoingOutLeavesThroughTheDoorAndEndsAtTheCar()
        {
            var route = FounderRoutine.RouteFor(FounderTask.Leaving);

            Assert.AreEqual("Car", route[route.Length - 1],
                "The map opens after the founder reaches the car, so the car has to be last.");

            CollectionAssert.Contains(route, "Door");
        }

        [Test]
        public void GoingToBedGoesUpTheStairsRatherThanThroughTheCeiling()
        {
            var route = FounderRoutine.RouteFor(FounderTask.Resting);

            Assert.AreEqual("Bed", route[route.Length - 1]);
            CollectionAssert.Contains(route, "StairFoot");
            CollectionAssert.Contains(route, "StairHead");

            Assert.Less(System.Array.IndexOf(route, "StairFoot"),
                System.Array.IndexOf(route, "StairHead"),
                "Bottom of the stairs before the top, or the walk is a levitation.");
        }

        [Test]
        public void BeingAwayIsTheOneTaskWithNowhereToWalk()
        {
            Assert.IsEmpty(FounderRoutine.RouteFor(FounderTask.Away),
                "The room is empty while the world map is up. An empty route is how the actor knows "
                + "to stay put rather than walking somewhere invisible.");
        }
    }
}
