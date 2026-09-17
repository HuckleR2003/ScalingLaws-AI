using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The toggle rule behind the map legend, checked with no scene, no panel and no pin: click the
    /// lit row and it clears, click any other row and it replaces whatever was lit.
    /// </summary>
    public sealed class MapFilterStateTests
    {
        [Test]
        public void NothingIsSelectedAtTheStart()
        {
            var state = new MapFilterState();

            Assert.That(state.Selected, Is.Null);

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

        [Test]
        public void TogglingADifferentCategoryReplacesTheOldOne()
        {
            var state = new MapFilterState();

            state.Toggle(MapCategory.Research);
            state.Toggle(MapCategory.Energy);

            Assert.That(state.Selected, Is.EqualTo(MapCategory.Energy));
            Assert.That(state.IsEmphasised(MapCategory.Research), Is.False);
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
