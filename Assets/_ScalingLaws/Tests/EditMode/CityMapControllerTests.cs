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
            Assert.That(clamped.y, Is.EqualTo(-CityMapController.PanMargin));
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
    }
}
