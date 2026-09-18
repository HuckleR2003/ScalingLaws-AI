using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The map fixes of 2026-09-18: OFFICES FOR RENT, where the map opens and closes, the reach of a
    /// click, and the frame limit that made the opening shot visible at all.
    /// </summary>
    public sealed class MapOfficeAndShotTests
    {
        private static MapSiteDefinition Site(string id) => MapSiteCatalog.All.First(site => site.Id == id);

        [Test]
        public void OfficesAreSortedIntoThreeSizesByTheirDesks()
        {
            Assert.That(OfficeListing.SizeOf(Site("office.loft")), Is.EqualTo(OfficeSize.Small));
            Assert.That(OfficeListing.SizeOf(Site("office.floor")), Is.EqualTo(OfficeSize.Small));
            Assert.That(OfficeListing.SizeOf(Site("office.campus")), Is.EqualTo(OfficeSize.Medium));
            Assert.That(OfficeListing.SizeOf(Site("office.tower")), Is.EqualTo(OfficeSize.Large));
            Assert.That(OfficeListing.SizeOf(Site("office.soon.campus")), Is.EqualTo(OfficeSize.Large),
                "The announced offices have no tier; their size comes from the desks announced.");
            Assert.That(OfficeListing.DesksOf(Site("office.soon.tower")), Is.EqualTo(320));
        }

        [Test]
        public void EveryOfficeOnTheMapIsInExactlyOneSize()
        {
            var all = MapSiteCatalog.OfKind(MapSiteKind.OfficeLease).Count();
            var listing = new OfficeListing();

            Assert.That(listing.Count, Is.EqualTo(all), "With nothing ticked the walk covers every office.");

            var bySize = 0;

            foreach (var size in new[] { OfficeSize.Small, OfficeSize.Medium, OfficeSize.Large })
            {
                var one = new OfficeListing();
                one.Toggle(size);
                bySize += one.Count;
            }

            Assert.That(bySize, Is.EqualTo(all), "An office fell between two sizes or into both.");
        }

        [Test]
        public void NextWalksTheTickedSizeSmallestFirstAndWraps()
        {
            var listing = new OfficeListing();
            listing.Toggle(OfficeSize.Small);

            var first = listing.Next();
            var second = listing.Next();
            var wrapped = listing.Next();

            Assert.That(first.Id, Is.EqualTo("office.loft"));
            Assert.That(second.Id, Is.EqualTo("office.floor"));
            Assert.That(wrapped.Id, Is.EqualTo("office.loft"), "The walk did not go back to the start.");
        }

        [Test]
        public void TheMapOpensOnTheOfficeTheCompanyIsIn()
        {
            Assert.That(MapSiteCatalog.HomeFor(OfficeTier.Garage).X, Is.EqualTo(CityLayout.FounderHome.X));
            Assert.That(MapSiteCatalog.HomeFor(OfficeTier.Loft).X, Is.EqualTo(Site("office.loft").Position.X));
            Assert.That(MapSiteCatalog.HomeFor(OfficeTier.Tower).Z, Is.EqualTo(Site("office.tower").Position.Z));
        }

        [Test]
        public void AFirstFrameThatTakesSecondsCannotSkipTheOpeningShot()
        {
            // The city's first frames take seconds to draw. One of them used to be the whole shot.
            Assert.That(CityMapController.ShotStep(4.2f), Is.EqualTo(CityMapController.LongestShotStep));
            Assert.That(CityMapController.ShotStep(float.NaN), Is.Zero);
            Assert.That(CityMapController.ShotStep(-1f), Is.Zero);

            var framesToOverview = CityMapController.OpeningSeconds / CityMapController.LongestShotStep;
            Assert.That(framesToOverview, Is.GreaterThan(60f),
                "The opening would be over in a handful of frames, which reads as a cut.");
        }

        [Test]
        public void TheClosingShotStartsWhereTheMapIsAndEndsAtTheHouse()
        {
            Assert.That(CityMapController.ClosingEase(0f), Is.EqualTo(0f));
            Assert.That(CityMapController.ClosingEase(CityMapController.ClosingSeconds), Is.EqualTo(1f));
            Assert.That(CityMapController.ClosingEase(CityMapController.ClosingSeconds * 0.5f),
                Is.LessThan(0.5f), "It should leave the overview slowly and arrive quickly.");
        }

        [Test]
        public void APlaceWithNoRadiusCanStillBeClicked()
        {
            Assert.That(MapSiteSelection.ReachOf(0f), Is.EqualTo(MapSiteSelection.MinimumReach),
                "Terrace Park and Valley Plaza have no radius, and could not be clicked.");
            Assert.That(MapSiteSelection.ReachOf(90f), Is.EqualTo(90f),
                "The wind farm's own radius is larger than the minimum and has to be kept.");
        }
    }
}
