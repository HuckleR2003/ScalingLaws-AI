using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Clicking a person in the office, measured rather than assumed.
    ///
    /// **The office is the one screen whose whole interaction is clicking a person, and none of the
    /// arithmetic behind it was tested.** A playtest reported it as "clicking a person does nothing"
    /// and the cause turned out to be the raycast rather than the panel: `StaffPresence.NearestTo`
    /// was written as the answer, projecting instead of raycasting, and then nothing checked that
    /// the projection picks the right person.
    ///
    /// That is the same gap the basement cursor still has, written down at the time: the grid was
    /// tested, the mapping from a click to a square was not, and the note said the first real click
    /// would be the author's. This closes it for the office.
    ///
    /// The camera is set up the way the room's is: orthographic, looking down at an angle, which is
    /// the arrangement that makes two people at different depths project to nearly the same height
    /// on screen. A test on a camera pointing straight down would pass while the real one failed.
    /// </summary>
    public sealed class OfficeClickTests
    {
        private GameObject host;
        private Camera camera;

        [SetUp]
        public void MakeACamera()
        {
            host = new GameObject("Office click test camera");
            camera = host.AddComponent<Camera>();

            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.transform.position = new Vector3(9f, 9f, -9f);
            camera.transform.rotation = Quaternion.Euler(30f, -45f, 0f);
        }

        [TearDown]
        public void PutItAway() => Object.DestroyImmediate(host);

        /// <summary>
        /// What `NearestTo` hands over for somebody standing here: the chest, not the feet.
        ///
        /// **The first version of this fixture passed feet and failed**, which was the test being
        /// wrong rather than the code. `NearestTo` raises each person by `AimHeight` before
        /// comparing, because that is where a player points; a metre of world is 0.067 of the
        /// viewport at this camera size and the radius is 0.055, so aiming at the feet misses by
        /// slightly more than the whole target.
        /// </summary>
        private static Vector3 Aim(Vector3 feet) => feet + Vector3.up;

        /// <summary>Where on screen that aim lands.</summary>
        private Vector2 ScreenPlaceOf(Vector3 aim)
        {
            var view = camera.WorldToViewportPoint(aim);
            return new Vector2(view.x, view.y);
        }

        [Test]
        public void ClickingAPersonPicksThatPersonAndNotTheirNeighbour()
        {
            var people = new List<Vector3?>
            {
                Aim(new Vector3(0f, 0f, 0f)),
                Aim(new Vector3(3f, 0f, 0f)),
                Aim(new Vector3(0f, 0f, 3f)),
            };

            for (var index = 0; index < people.Count; index++)
            {
                var at = ScreenPlaceOf(people[index].Value);

                Assert.That(StaffPresence.NearestIn(camera, at, people), Is.EqualTo(index),
                    $"a click on person {index} landed on somebody else");
            }
        }

        /// <summary>
        /// A click on empty floor opens nobody.
        ///
        /// The failure this rules out is the opposite of a missed click and is worse: a card for
        /// whoever happened to be nearest, opened by clicking the carpet.
        /// </summary>
        [Test]
        public void ClickingEmptyFloorOpensNobody()
        {
            var people = new List<Vector3?> { Aim(new Vector3(0f, 0f, 0f)) };

            var far = ScreenPlaceOf(Aim(new Vector3(0f, 0f, 0f))) + new Vector2(0.4f, 0.35f);

            Assert.That(StaffPresence.NearestIn(camera, far, people), Is.EqualTo(-1));
        }

        /// <summary>
        /// Somebody off shift is not in the room, so they cannot be clicked.
        ///
        /// Their renderers are off. A card opened over an empty patch of floor because the roster
        /// still holds the entry is a card about somebody who is not there.
        /// </summary>
        [Test]
        public void SomebodyOffShiftIsNotClickable()
        {
            var here = Aim(new Vector3(0f, 0f, 0f));
            var people = new List<Vector3?> { null };

            Assert.That(StaffPresence.NearestIn(camera, ScreenPlaceOf(here), people),
                Is.EqualTo(-1));
        }

        /// <summary>
        /// **The index still means the same person when somebody is away.**
        ///
        /// This is why the absent are passed as nulls rather than dropped. Filtering renumbers
        /// everybody behind the gap, so clicking the third person opens the second one's card, and
        /// from the player's chair that is indistinguishable from the click having missed.
        /// </summary>
        [Test]
        public void AnAbsentColleagueDoesNotRenumberTheOnesStillThere()
        {
            var third = Aim(new Vector3(0f, 0f, 3f));

            var people = new List<Vector3?>
            {
                Aim(new Vector3(0f, 0f, 0f)),
                null,
                third,
            };

            Assert.That(StaffPresence.NearestIn(camera, ScreenPlaceOf(third), people), Is.EqualTo(2),
                "the roster index shifted when a colleague was off shift");
        }

        /// <summary>
        /// Somebody behind the camera is never the nearest thing to the cursor.
        ///
        /// `WorldToViewportPoint` happily projects what is behind it, mirrored, so without the depth
        /// check a person standing off the back of the room becomes clickable through the floor.
        /// </summary>
        [Test]
        public void SomebodyBehindTheCameraIsNotClickable()
        {
            var behind = Aim(camera.transform.position - camera.transform.forward * 12f);
            var people = new List<Vector3?> { behind };

            var view = camera.WorldToViewportPoint(behind);

            Assert.That(view.z, Is.LessThan(0f), "the fixture failed to put anybody behind the camera");
            Assert.That(StaffPresence.NearestIn(camera, new Vector2(view.x, view.y), people),
                Is.EqualTo(-1));
        }
    }
}
