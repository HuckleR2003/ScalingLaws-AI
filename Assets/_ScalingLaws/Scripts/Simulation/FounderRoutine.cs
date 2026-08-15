using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>Where the founder should be standing, and what they should be doing there.</summary>
    public enum FounderTask
    {
        /// <summary>At the desk, working. The default and by far the most common.</summary>
        Working = 0,

        /// <summary>Walking to bed, then asleep in it. Once a fortnight, briefly.</summary>
        Resting = 1,

        /// <summary>On the way out through the garage to the car, because the map was opened.</summary>
        Leaving = 2,

        /// <summary>Gone. The room is empty while the world map is up.</summary>
        Away = 3
    }

    /// <summary>
    /// What the founder is doing on a given day.
    ///
    /// **The room has had nine named waypoints and an empty `Staff` group since the day it was
    /// generated, and nothing has ever walked between them.** This is the rule half of fixing that:
    /// it answers "where should the person be" without knowing what a `Transform` is, so it can be
    /// tested in milliseconds and the scene component becomes a thing that only moves a model.
    ///
    /// **Rest is on the calendar, not on a fatigue meter.** A bar that fills up is a second resource
    /// to manage and this game already asks the player to manage four. A founder who walks upstairs
    /// once a fortnight, sleeps, and comes back down is a room that feels lived in and a mechanic
    /// that cannot be optimised, which is the entire point of it.
    /// </summary>
    public static class FounderRoutine
    {
        /// <summary>Days between one night off and the next.</summary>
        public const int RestIntervalDays = 14;

        /// <summary>
        /// How long the founder stays upstairs.
        ///
        /// One day. The clock runs faster while they are up there, so this is seconds of real time,
        /// and any longer reads as the game having stopped rather than as a night passing.
        /// </summary>
        public const int RestDays = 1;

        /// <summary>How much faster the day runs while the founder is asleep.</summary>
        public const float RestTimeScale = 6.0f;

        /// <summary>
        /// The task for a day.
        ///
        /// Leaving wins over resting: the player clicked something and the game has to answer the
        /// click rather than finish a nap first.
        /// </summary>
        public static FounderTask TaskFor(int dayIndex, bool isLeaving, bool isAway)
        {
            if (isAway)
            {
                return FounderTask.Away;
            }

            if (isLeaving)
            {
                return FounderTask.Leaving;
            }

            return IsRestDay(dayIndex) ? FounderTask.Resting : FounderTask.Working;
        }

        /// <summary>
        /// True on the days the founder goes upstairs.
        ///
        /// Day zero is not one of them. A campaign that opens with the founder walking away from the
        /// desk to go to bed is a campaign that opens by saying nothing here is urgent.
        /// </summary>
        public static bool IsRestDay(int dayIndex)
        {
            if (dayIndex <= 0)
            {
                return false;
            }

            var into = dayIndex % RestIntervalDays;
            return into < RestDays;
        }

        /// <summary>Days until the next night off, for anything that wants to say so.</summary>
        public static int DaysUntilRest(int dayIndex)
        {
            if (dayIndex < 0)
            {
                return RestIntervalDays;
            }

            for (var ahead = 1; ahead <= RestIntervalDays; ahead++)
            {
                if (IsRestDay(dayIndex + ahead))
                {
                    return ahead;
                }
            }

            return RestIntervalDays;
        }

        /// <summary>
        /// The waypoints a task sends the founder through, in order.
        ///
        /// Named rather than positioned, because the room owns the geometry and this owns the plan.
        /// The names are the ones `OfficeRoomBuilder` writes, and a test holds that every name here
        /// is one the builder actually creates.
        /// </summary>
        public static string[] RouteFor(FounderTask task) => task switch
        {
            FounderTask.Resting => new[] { "StairFoot", "StairHead", "Bed" },
            FounderTask.Leaving => new[] { "StairFoot", "Door", "Garage", "Car" },
            FounderTask.Away => Array.Empty<string>(),
            _ => new[] { "Desk" }
        };

        /// <summary>
        /// What the model plays on arrival.
        ///
        /// **The entry, not the rest.** Sitting down hands over to typing and lying down hands over
        /// to sleeping, both on exit time in the controller, so asking for the resting clip directly
        /// would snap the founder into a chair they never sat down in.
        /// </summary>
        public static string ClipFor(FounderTask task) => task switch
        {
            FounderTask.Resting => "LieDown",
            FounderTask.Leaving => "Idle",
            FounderTask.Away => "Idle",
            _ => "SitDown"
        };

        /// <summary>
        /// What the model settles into after <see cref="ClipFor"/> has played out.
        ///
        /// Only here so a test can assert the pair exists. The controller does the handover.
        /// </summary>
        public static string RestingClipFor(FounderTask task) => task switch
        {
            FounderTask.Resting => "Sleep",
            FounderTask.Leaving => "Idle",
            FounderTask.Away => "Idle",
            _ => "Type"
        };

        /// <summary>The clock multiplier while a task is running. One for everything but sleeping.</summary>
        public static float TimeScaleFor(FounderTask task) =>
            task == FounderTask.Resting ? RestTimeScale : 1.0f;
    }
}
