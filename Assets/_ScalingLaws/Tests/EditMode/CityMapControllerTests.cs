using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The part of the city map's camera that does not need a live scene: which keys mean which
    /// direction, and where the clamps sit. `Input` cannot be driven from a test, so this is split
    /// from the polling the same way <see cref="KeyboardShortcuts.Resolve"/> is — see
    /// <see cref="PageScrollTests"/> for the same pattern on the same problem.
    /// </summary>
    public sealed class CityMapControllerTests
    {
        [Test]
        public void NoKeysMeansNoMovement()
        {
            Assert.That(CityMapController.ResolvePan(false, false, false, false), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void OppositeKeysCancelInsteadOfFightingEachOther()
        {
            Assert.That(CityMapController.ResolvePan(true, true, false, false), Is.EqualTo(Vector2.zero),
                "W and S held together must stop the camera, not average out to a slow crawl.");

            Assert.That(CityMapController.ResolvePan(false, false, true, true), Is.EqualTo(Vector2.zero),
                "A and D held together must stop the camera the same way W and S do.");
        }

        [Test]
        public void EachKeyMovesOneWay()
        {
            Assert.That(CityMapController.ResolvePan(true, false, false, false), Is.EqualTo(Vector2.up),
                "W (and the up arrow) is forward.");

            Assert.That(CityMapController.ResolvePan(false, true, false, false), Is.EqualTo(Vector2.down),
                "S (and the down arrow) is back.");

            Assert.That(CityMapController.ResolvePan(false, false, false, true), Is.EqualTo(Vector2.right),
                "D (and the right arrow) is right.");

            Assert.That(CityMapController.ResolvePan(false, false, true, false), Is.EqualTo(Vector2.left),
                "A (and the left arrow) is left.");
        }

        /// <summary>
        /// A held W and a held D is a diagonal, and a diagonal that is not renormalised moves the
        /// camera faster than a single key does — the classic strafe-running bug, ported to a top
        /// down camera instead of a first person one.
        /// </summary>
        [Test]
        public void ADiagonalIsNoFasterThanAStraightLine()
        {
            var diagonal = CityMapController.ResolvePan(true, false, false, true);

            Assert.That(diagonal.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(diagonal.x, Is.GreaterThan(0f));
            Assert.That(diagonal.y, Is.GreaterThan(0f));
        }

        [Test]
        public void ZoomKeysAgreeOrCancelNeverBoth()
        {
            Assert.That(CityMapController.ResolveZoom(false, false), Is.EqualTo(0f));
            Assert.That(CityMapController.ResolveZoom(true, false), Is.EqualTo(1f), "\"=\" zooms in.");
            Assert.That(CityMapController.ResolveZoom(false, true), Is.EqualTo(-1f), "\"-\" zooms out.");

            Assert.That(CityMapController.ResolveZoom(true, true), Is.EqualTo(0f),
                "Both held at once must hold the view still, not fight every other frame.");
        }

        [Test]
        public void PanningIsSlowerCloseInAndFasterZoomedOut()
        {
            var atReference = CityMapController.HeightFactor(CityMapController.ReferenceHeight);
            var closeIn = CityMapController.HeightFactor(CityMapController.MinHeight);
            var farOut = CityMapController.HeightFactor(CityMapController.MaxHeight);

            Assert.That(atReference, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(closeIn, Is.LessThan(atReference),
                "Crossing the whole map should not take the same few seconds whether the camera is "
                + "reading street names or looking at all five districts at once.");
            Assert.That(farOut, Is.GreaterThan(atReference));
        }

        [Test]
        public void HeightFactorNeverReachesZeroOrRunsAway()
        {
            Assert.That(CityMapController.HeightFactor(1f), Is.EqualTo(CityMapController.MinHeightFactor),
                "An unclamped height factor at a very low height would round pan speed to nothing.");

            Assert.That(CityMapController.HeightFactor(100000f), Is.EqualTo(CityMapController.MaxHeightFactor),
                "An unclamped height factor zoomed all the way out would fling the camera off the map "
                + "in one frame.");
        }

        [Test]
        public void PositionsInsideTheMapAreLeftAlone()
        {
            var middle = new Vector2(CityLayout.Size / 2f, CityLayout.Size / 2f);
            Assert.That(CityMapController.ClampToMap(middle), Is.EqualTo(middle));
        }

        [Test]
        public void PanningStopsAShortDistancePastTheCoastRatherThanAtTheSquareEdge()
        {
            var farPast = new Vector2(CityLayout.Size * 10f, -CityLayout.Size * 10f);
            var clamped = CityMapController.ClampToMap(farPast);

            Assert.That(clamped.x, Is.EqualTo(CityLayout.Size + CityMapController.PanMargin));
            Assert.That(clamped.y, Is.EqualTo(CityMapController.SouthmostGround - CityMapController.PanMargin));
        }

        /// <summary>Straight down, the point under the camera is exactly what the camera looks at.</summary>
        [Test]
        public void StraightDownMeansTheGroundPointIsTheCameraPoint()
        {
            var ground = CityMapController.CameraGroundFor(new Vector2(500f, 700f), 60f, 900f, Vector3.down);
            Assert.That(ground, Is.EqualTo(new Vector2(500f, 700f)));
        }

        /// <summary>
        /// The real check for a district jump: aim the same fixed-angle look direction from the
        /// computed camera point and confirm it actually lands back on the requested target,
        /// rather than trusting the algebra by inspection.
        /// </summary>
        [Test]
        public void TheComputedCameraGroundActuallyLooksAtTheTarget()
        {
            var forward = new Vector3(0.4755f, -0.5878f, 0.6545f); // the map camera's fixed 36/36 pitch and yaw
            var target = new Vector2(1000f, 660f);
            const float targetHeight = 52f;
            const float cameraHeight = 1050f;

            var cameraGround = CityMapController.CameraGroundFor(target, targetHeight, cameraHeight, forward);

            var reach = (cameraHeight - targetHeight) / -forward.y;
            var landed = new Vector2(cameraGround.x + forward.x * reach, cameraGround.y + forward.z * reach);

            Assert.That(landed.x, Is.EqualTo(target.x).Within(0.01f));
            Assert.That(landed.y, Is.EqualTo(target.y).Within(0.01f));
        }

        /// <summary>The wheel moves the camera in the direction the notch was turned, or not at all.</summary>
        [Test]
        public void TheWheelZoomsInWhenItIsTurnedForward()
        {
            Assert.That(CityMapController.ScrollDolly(0f), Is.EqualTo(0f));
            Assert.That(CityMapController.ScrollDolly(1f), Is.EqualTo(CityMapController.ZoomUnitsPerNotch));
            Assert.That(CityMapController.ScrollDolly(-1f), Is.EqualTo(-CityMapController.ZoomUnitsPerNotch));
        }

        /// <summary>
        /// A trackpad flick arrives as one enormous delta on a single frame. Without the cap that
        /// frame takes the camera from the whole city down to the pavement.
        /// </summary>
        [Test]
        public void OneFlickOfATrackpadCannotCrossTheWholeZoomRange()
        {
            var jump = Mathf.Abs(CityMapController.ScrollDolly(40f));
            Assert.That(jump, Is.LessThan(CityMapController.MaxHeight - CityMapController.MinHeight));
            Assert.That(jump, Is.EqualTo(CityMapController.ScrollDolly(3f)));
        }

        /// <summary>
        /// The dolly stops on the limit rather than being dropped at it, or the last notch before
        /// the floor would do nothing and the wheel would read as broken exactly where it matters.
        /// </summary>
        [Test]
        public void TheWheelStopsOnTheLimitRatherThanBeingRefused()
        {
            const float forwardY = -0.5878f; // the map camera's fixed pitch, looking down

            var partial = CityMapController.ClampDolly(
                CityMapController.MinHeight + 20f, forwardY, CityMapController.ZoomUnitsPerNotch);

            var landed = CityMapController.MinHeight + 20f + forwardY * partial;

            Assert.That(partial, Is.GreaterThan(0f), "there is still twenty metres to give");
            Assert.That(landed, Is.EqualTo(CityMapController.MinHeight).Within(0.01f));

            Assert.That(
                CityMapController.ClampDolly(CityMapController.MinHeight, forwardY, 400f),
                Is.EqualTo(0f), "against the floor and pushing into it");

            Assert.That(
                CityMapController.ClampDolly(CityMapController.MaxHeight, forwardY, -400f),
                Is.EqualTo(0f), "against the ceiling and pushing into it");
        }

        /// <summary>
        /// The opening shot starts on the founder's house: aim the fixed look direction from where
        /// it begins and it has to land on the house, the same check the district jump gets.
        /// </summary>
        [Test]
        public void TheMapOpensLookingAtTheFoundersHouse()
        {
            var forward = new Vector3(0.4755f, -0.5878f, 0.6545f); // the map camera's fixed 36/36
            var home = CityLayout.FounderHome;
            var ground = CityLayout.GroundHeightAt(home);

            var from = CityMapController.OpeningFrom(home, ground, forward);

            var reach = (from.y - ground) / -forward.y;
            var landed = new Vector2(from.x + forward.x * reach, from.z + forward.z * reach);

            Assert.That(landed.x, Is.EqualTo(home.X).Within(0.01f));
            Assert.That(landed.y, Is.EqualTo(home.Z).Within(0.01f));
            Assert.That(from.y, Is.GreaterThanOrEqualTo(CityMapController.MinHeight),
                "it must not start inside the terrain");
            Assert.That(from.y, Is.LessThan(1050f),
                "it starts below the overview it pulls back to, or there is no pull-back");
        }

        /// <summary>
        /// The pull-back runs from one end to the other, in the time the author asked for, and it
        /// covers most of the ground early. An even travel reads as a lift rather than as leaving.
        /// </summary>
        [Test]
        public void ThePullBackStartsQuicklyAndSettles()
        {
            Assert.That(CityMapController.OpeningEase(0f), Is.EqualTo(0f));
            Assert.That(CityMapController.OpeningEase(CityMapController.OpeningSeconds),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(CityMapController.OpeningEase(CityMapController.OpeningSeconds * 4f),
                Is.EqualTo(1f), "a long frame cannot take it past the end");

            var half = CityMapController.OpeningEase(CityMapController.OpeningSeconds * 0.5f);
            Assert.That(half, Is.GreaterThan(0.7f), "most of the distance is covered early");

            var previous = 0f;
            for (var step = 1; step <= 20; step++)
            {
                var now = CityMapController.OpeningEase(CityMapController.OpeningSeconds * step / 20f);
                Assert.That(now, Is.GreaterThanOrEqualTo(previous), "the shot never travels backwards");
                previous = now;
            }
        }

        /// <summary>
        /// Every district levels its own ground, so a camera aimed with one guessed height centres
        /// every district except the one it was guessed for.
        /// </summary>
        [Test]
        public void TheGroundHeightAtAPlaceIsItsOwnDistrictsHeight()
        {
            foreach (var district in CityLayout.Districts)
            {
                var centre = new MapPoint(district.CentreX, district.CentreZ);
                Assert.That(CityLayout.GroundHeightAt(centre), Is.EqualTo(district.GroundHeight),
                    district.Id + " does not report its own levelled height");
                Assert.AreSame(district, CityLayout.DistrictById(district.Id));
            }

            Assert.IsNull(CityLayout.DistrictById("nowhere"));
        }

        /// <summary>Well inside the limits the wheel is not interfered with at all.</summary>
        [Test]
        public void InTheMiddleOfTheRangeTheWheelIsLeftAlone()
        {
            var distance = CityMapController.ScrollDolly(1f);

            Assert.That(CityMapController.ClampDolly(1200f, -0.5878f, distance), Is.EqualTo(distance));
        }
    }
}
