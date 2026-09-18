using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The SHOW button at the foot of the legend, and the walk behind it.
    ///
    /// The camera flight it starts needs a scene and cannot be checked here. The order, the wrap,
    /// the counter and the idle reset are the parts a player would notice being wrong, and none of
    /// them need one.
    /// </summary>
    public sealed class MapTourTests
    {
        [Test]
        public void WithNoCategoryPickedItWalksTheWholeMap()
        {
            var tour = new MapTour();

            Assert.AreEqual(MapSiteCatalog.All.Count, tour.Count);
            Assert.AreEqual(1, tour.NextOrdinal, "the first click shows the first place");
        }

        [Test]
        public void TheCounterFollowsTheClicksAndWrapsAtTheEnd()
        {
            var tour = new MapTour();
            tour.Focus(MapCategory.Energy);

            var expected = MapSiteCatalog.All.Where(site => site.Category == MapCategory.Energy).ToList();

            Assert.Greater(expected.Count, 0, "a category with nothing in it would make the button a lie");
            Assert.AreEqual(expected.Count, tour.Count);

            for (var index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(index + 1, tour.NextOrdinal);
                Assert.AreSame(expected[index], tour.Show());
            }

            Assert.AreEqual(1, tour.NextOrdinal, "past the last place it starts again at the first");
        }

        [Test]
        public void EveryCategoryHasSomewhereToGo()
        {
            var tour = new MapTour();

            foreach (MapCategory category in System.Enum.GetValues(typeof(MapCategory)))
            {
                tour.Focus(category);
                Assert.Greater(tour.Count, 0, $"{category} has no place on the map to show");
                Assert.IsNotNull(tour.Show(), $"{category} shows nothing");
            }
        }

        [Test]
        public void GoingQuietTakesItBackToTheFirstPlace()
        {
            var tour = new MapTour();

            tour.Show();
            tour.Show();
            Assert.AreEqual(3, tour.NextOrdinal);

            Assert.IsFalse(tour.Tick(MapTour.IdleResetSeconds - 1f), "it waits the whole eight seconds");
            Assert.AreEqual(3, tour.NextOrdinal);

            Assert.IsTrue(tour.Tick(2f), "the caption changed, so the button has to be redrawn");
            Assert.AreEqual(1, tour.NextOrdinal);
        }

        [Test]
        public void TheClockOnlyRunsWhileTheWalkIsUnderWay()
        {
            var tour = new MapTour();

            Assert.IsFalse(tour.Tick(MapTour.IdleResetSeconds * 3f),
                "a button nobody has pressed has nothing to reset");
            Assert.AreEqual(1, tour.NextOrdinal);
        }

        [Test]
        public void PickingACategoryStartsTheWalkAgain()
        {
            var tour = new MapTour();

            tour.Show();
            tour.Show();

            Assert.IsTrue(tour.Focus(MapCategory.Compute), "the button now counts a different set");
            Assert.AreEqual(1, tour.NextOrdinal);
            Assert.AreEqual(MapSiteCatalog.All.Count(site => site.Category == MapCategory.Compute),
                tour.Count);
        }

        [Test]
        public void EveryPlaceTheWalkVisitsIsInTheCategoryItIsWalking()
        {
            var tour = new MapTour();
            tour.Focus(MapCategory.Business);

            for (var index = 0; index < tour.Count; index++)
            {
                Assert.AreEqual(MapCategory.Business, tour.Show().Category);
            }
        }
        /// <summary>
        /// The legend ticks several categories now, so the walk has to cover all of them. With two
        /// ticked it visits both sets and nothing else.
        /// </summary>
        [Test]
        public void FocusManyWalksEveryTickedCategory()
        {
            var tour = new MapTour();

            tour.FocusMany(new[] { MapCategory.Energy, MapCategory.Compute });

            var expected = MapSiteCatalog.All
                .Count(site => site.Category == MapCategory.Energy || site.Category == MapCategory.Compute);

            Assert.AreEqual(expected, tour.Count);
            Assert.That(tour.Stops.All(stop =>
                stop.Category == MapCategory.Energy || stop.Category == MapCategory.Compute), Is.True);

            // Two at once, so there is no single category to name.
            Assert.IsNull(tour.Category);
        }

        [Test]
        public void FocusManyWithNothingTickedWalksTheWholeMap()
        {
            var tour = new MapTour();

            tour.FocusMany(new[] { MapCategory.Energy });
            tour.FocusMany(System.Array.Empty<MapCategory>());

            Assert.AreEqual(MapSiteCatalog.All.Count, tour.Count);
        }
    }
}
