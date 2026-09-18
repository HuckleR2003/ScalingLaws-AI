using NUnit.Framework;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// How far the map draws shadows from its camera.
    ///
    /// The office's quality settings stop shadows at 150 m, and the map is read from 300 to 900 m up,
    /// so buildings on the map only cast a shadow once the player had zoomed all the way in — the
    /// author's report. The distance now follows the camera; these pin down its three regimes.
    /// </summary>
    public sealed class MapShadowTests
    {
        [Test]
        public void CloseToTheGroundShadowsStillReachTheWholeFrame()
        {
            Assert.That(CityMapController.ShadowDistanceFor(CityMapController.MinHeight), Is.EqualTo(220f));
        }

        [Test]
        public void AtTheUsualHeightShadowsReachWellPastTheOffice()
        {
            var distance = CityMapController.ShadowDistanceFor(CityMapController.ReferenceHeight);

            Assert.That(distance, Is.GreaterThan(1500f),
                "At the height the map is normally read from, shadows must reach across the frame.");
        }

        [Test]
        public void FullyZoomedOutTheDistanceStopsAtItsCeiling()
        {
            Assert.That(CityMapController.ShadowDistanceFor(CityMapController.MaxHeight), Is.EqualTo(2600f));
        }
    }
}
