using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests
{
    /// <summary>
    /// The corner banner, on the two states a playtest found it wrong in.
    ///
    /// Both were reported as things vanishing, and both were the panel being asked to hold one
    /// subject when the company had two. A run took the product's place; a second product had no
    /// chart. Neither is visible to a test that only counts elements, so these read the display
    /// styles the way the renderer does.
    /// </summary>
    [TestFixture]
    public sealed class ModelBannerTests
    {
        private static ProductStanding Selling(string name = "AURORA") =>
            new(name, true, 0.7, 0.8, 120_000.0, 4_000_000L, 900_000L, 40, 61.0, 64.0);

        private static ProductStanding Nothing() => default;

        private static ModelBanner Build(ProductStanding standing, WorkInFlight work,
            IReadOnlyList<long> series = null, bool compact = false) =>
            new(() => standing, () => work, () => series ?? new List<long>(), () => { }, compact);

        /// <summary>Walks the tree for the one element with this class, or null.</summary>
        private static VisualElement Find(VisualElement root, string className)
        {
            if (root.ClassListContains(className))
            {
                return root;
            }

            foreach (var child in root.Children())
            {
                var hit = Find(child, className);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        private static bool Shown(VisualElement element) =>
            element != null && element.style.display.value != DisplayStyle.None;

        [Test]
        public void ATrainingRunDoesNotTakeThePlaceOfTheProductOnSale()
        {
            var banner = Build(Selling(), new WorkInFlight("TRAINING MODEL", "AURORA 2", 0.3, 140));
            banner.Refresh();

            var title = (Label)Find(banner.Root, "mb__title");

            Assert.That(title.text, Is.EqualTo("AURORA"),
                "the banner is about the product that is selling, not about the run in flight");

            Assert.That(Shown(Find(banner.Root, "mb__body")), Is.True,
                "the product's own figures stay on screen while the next one trains");

            Assert.That(Shown(Find(banner.Root, "mb__manage")), Is.True,
                "and the way through to the management desk stays reachable");

            Assert.That(Shown(Find(banner.Root, "mb__training")), Is.True,
                "with the run as a strip underneath it");
        }

        [Test]
        public void WithNothingOnSaleTheRunIsTheWholeBanner()
        {
            var banner = Build(Nothing(), new WorkInFlight("TRAINING MODEL", "AURORA", 0.1, 180));
            banner.Refresh();

            var title = (Label)Find(banner.Root, "mb__title");

            Assert.That(title.text, Is.EqualTo("AURORA"),
                "on day one the run is the only thing there is to report");

            Assert.That(Shown(Find(banner.Root, "mb__body")), Is.False);
            Assert.That(Shown(Find(banner.Root, "mb__training")), Is.True);
        }

        [Test]
        public void TheStripGoesAwayWhenTheRunFinishes()
        {
            var banner = Build(Selling(), WorkInFlight.Idle);
            banner.Refresh();

            Assert.That(Shown(Find(banner.Root, "mb__training")), Is.False);
            Assert.That(Shown(Find(banner.Root, "mb__body")), Is.True);
        }

        [Test]
        public void AFollowerBannerHasAChart()
        {
            var follower = Build(Selling("HALO"), WorkInFlight.Idle,
                new List<long> { 40L, 55L, 51L, 70L }, true);

            follower.Refresh();

            Assert.That(Find(follower.Root, "mb-chart"), Is.Not.Null,
                "the second product on sale is the one the player cannot read any other way");
        }

        /// <summary>
        /// One run is one strip. Three products on sale must not report three trainings.
        /// </summary>
        [Test]
        public void AFollowerBannerNeverDrawsTheTrainingStrip()
        {
            var follower = Build(Selling("HALO"),
                new WorkInFlight("TRAINING MODEL", "AURORA 2", 0.3, 140), null, true);

            follower.Refresh();

            Assert.That(Shown(Find(follower.Root, "mb__training")), Is.False);

            var title = (Label)Find(follower.Root, "mb__title");
            Assert.That(title.text, Is.EqualTo("HALO"));
        }
    }
}
