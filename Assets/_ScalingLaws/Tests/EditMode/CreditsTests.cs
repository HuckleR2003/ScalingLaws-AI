using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;

namespace ScalingLaws.Tests
{
    /// <summary>
    /// The credits are a promise made to real people, so the list is held by a test: a name that
    /// quietly drops out in a refactor is a name somebody was told would be there.
    /// </summary>
    public sealed class CreditsTests
    {
        [Test]
        public void NatalkaIsCreditedAsATester()
        {
            Assert.That(Credits.Testers, Does.Contain("Natalka6456"));
        }

        [Test]
        public void NoNameIsBlankOrListedTwice()
        {
            var everyone = Credits.Testers.Concat(Credits.Supporters).ToList();

            Assert.That(everyone.All(name => !string.IsNullOrWhiteSpace(name)), Is.True);
            Assert.That(everyone.Count, Is.EqualTo(everyone.Distinct().Count()));
        }

        [Test]
        public void TheSheetHasItsWordsInBothLanguages()
        {
            var keys = new[]
            {
                "menu.credits", "credits.title", "credits.note", "credits.created", "credits.testers",
                "credits.testers.note", "credits.supporters", "credits.supporters.empty"
            };

            // English first: a key English lacks comes back as the key itself. Polish second, through
            // the translator's worklist, because a missing Polish key falls back to English silently.
            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.English;
                foreach (var key in keys)
                {
                    Assert.That(Loc.T(key), Is.Not.EqualTo(key), $"English is missing {key}");
                }
            }
            finally
            {
                Loc.Current = previous;
            }

            var untranslated = Loc.Untranslated(Language.Polish);
            foreach (var key in keys)
            {
                Assert.That(untranslated, Does.Not.Contain(key), $"Polish is missing {key}");
            }
        }
    }
}
