using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The tick rule behind the map legend, checked with no scene, no panel and no pin: ticking a row
    /// adds its category, ticking a lit row drops it, and several at once are allowed.
    /// </summary>
    public sealed class MapFilterStateTests
    {
        [Test]
        public void NothingIsSelectedAtTheStart()
        {
            var state = new MapFilterState();

            Assert.That(state.Selected, Is.Null);
            Assert.That(state.Filtering, Is.False);
            Assert.That(state.Picked, Is.Empty);

            foreach (var category in MapCategoryPalette.All)
            {
                Assert.That(state.IsEmphasised(category), Is.True,
                    $"{category} should read as emphasised when nothing is picked.");
            }
        }

        [Test]
        public void TogglingACategoryPicksIt()
        {
            var state = new MapFilterState();

            state.Toggle(MapCategory.Energy);

            Assert.That(state.Selected, Is.EqualTo(MapCategory.Energy));
            Assert.That(state.IsEmphasised(MapCategory.Energy), Is.True);
            Assert.That(state.IsEmphasised(MapCategory.Finance), Is.False);
        }

        [Test]
        public void TogglingTheSameCategoryTwiceClearsIt()
        {
            var state = new MapFilterState();

            state.Toggle(MapCategory.Research);
            state.Toggle(MapCategory.Research);

            Assert.That(state.Selected, Is.Null);
            Assert.That(state.IsEmphasised(MapCategory.Finance), Is.True);
        }

        /// <summary>
        /// **Two ticks mean two categories, not the second one.** The legend used to be one-of-eight,
        /// so a player comparing where the compute halls are against where the power comes from had to
        /// hold one of the two in their head.
        /// </summary>
        [Test]
        public void TickingASecondCategoryKeepsTheFirst()
        {
            var state = new MapFilterState();

            state.Toggle(MapCategory.Research);
            state.Toggle(MapCategory.Energy);

            Assert.That(state.Picked, Is.EquivalentTo(new[] { MapCategory.Research, MapCategory.Energy }));
            Assert.That(state.IsEmphasised(MapCategory.Research), Is.True);
            Assert.That(state.IsEmphasised(MapCategory.Energy), Is.True);
            Assert.That(state.IsEmphasised(MapCategory.Finance), Is.False);

            // Two are ticked, so there is no single one to hand the walk through the places.
            Assert.That(state.Selected, Is.Null);
        }

        [Test]
        public void UntickingOneOfTwoLeavesTheOther()
        {
            var state = new MapFilterState();

            state.Toggle(MapCategory.Research);
            state.Toggle(MapCategory.Energy);
            state.Toggle(MapCategory.Research);

            Assert.That(state.Picked, Is.EquivalentTo(new[] { MapCategory.Energy }));
            Assert.That(state.Selected, Is.EqualTo(MapCategory.Energy));
            Assert.That(state.IsEmphasised(MapCategory.Research), Is.False);
        }

        /// <summary>The rows come back in the legend's own order, whatever order they were ticked in.</summary>
        [Test]
        public void PickedFollowsTheLegendOrder()
        {
            var state = new MapFilterState();
            var all = MapCategoryPalette.All;

            state.Toggle(all[^1]);
            state.Toggle(all[0]);

            Assert.That(state.Picked, Is.EqualTo(new[] { all[0], all[^1] }));
        }

        [Test]
        public void ClearingDropsEveryTick()
        {
            var state = new MapFilterState();

            state.Toggle(MapCategory.Research);
            state.Toggle(MapCategory.Energy);
            state.Clear();

            Assert.That(state.Picked, Is.Empty);
            Assert.That(state.Filtering, Is.False);
            Assert.That(state.IsEmphasised(MapCategory.Finance), Is.True);
        }

        [Test]
        public void ChangedFiresOnceForEveryToggleAndClear()
        {
            var state = new MapFilterState();
            var fired = 0;
            state.Changed += () => fired++;

            state.Toggle(MapCategory.Media);
            state.Toggle(MapCategory.Media);
            state.Clear();

            // The third call is a no-op: already clear, so Changed should not fire a third time.
            Assert.That(fired, Is.EqualTo(2));
        }

        [Test]
        public void EveryCategoryHasAUniqueColourAndANameKey()
        {
            var seen = new System.Collections.Generic.HashSet<UnityEngine.Color>();

            foreach (var category in MapCategoryPalette.All)
            {
                Assert.That(seen.Add(MapCategoryPalette.ColourFor(category)), Is.True,
                    $"{category} shares its colour with another category.");

                Assert.That(MapCategoryPalette.NameKey(category), Is.Not.Empty,
                    $"{category} has no phrase-book key.");

                Assert.That(Loc.T(MapCategoryPalette.NameKey(category)),
                    Is.Not.EqualTo(MapCategoryPalette.NameKey(category)),
                    $"{category}'s key resolves to itself — missing from the phrase book.");
            }
        }
    }
}
