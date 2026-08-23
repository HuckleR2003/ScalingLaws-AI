using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The phrase book, checked for the ways a translation quietly stops being one.
    ///
    /// **The Polish side was 129 entries out of 143 empty and nothing said so**, because nothing
    /// read it: every screen held its own English constants and `Loc` was a table with no callers.
    /// A translation nobody can see is worse than none, since it looks finished in the file.
    /// </summary>
    public sealed class LocalisationTests
    {
        private static readonly Language[] Languages = { Language.English, Language.Polish };

        [Test]
        public void EveryEnglishPhraseHasAPolishOne()
        {
            var missing = Loc.English.Keys.Where(key => !Loc.Polish.ContainsKey(key)).OrderBy(k => k);
            Assert.IsEmpty(missing.ToList(), "Keys with no Polish: " + string.Join(", ", missing));
        }

        [Test]
        public void ThereAreNoPolishPhrasesForKeysThatNoLongerExist()
        {
            var orphans = Loc.Polish.Keys.Where(key => !Loc.English.ContainsKey(key)).OrderBy(k => k);

            Assert.IsEmpty(orphans.ToList(),
                "Polish entries for keys English does not have, so they are dead weight nobody will "
                + "notice is stale: " + string.Join(", ", orphans));
        }

        [Test]
        public void NoPhraseIsBlank()
        {
            var blank = new List<string>();

            foreach (var language in Languages)
            {
                var table = language == Language.English ? Loc.English : Loc.Polish;

                blank.AddRange(table
                    .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => $"{language}: {pair.Key}"));
            }

            Assert.IsEmpty(blank, "Blank phrases render as an empty label, which reads as a missing "
                + "control rather than as a missing translation:\n  " + string.Join("\n  ", blank));
        }

        /// <summary>
        /// A phrase with a `{0}` in one language and none in the other.
        ///
        /// **This is the one that crashes rather than looks wrong.** `Loc.T(key, value)` formats
        /// against whichever language is current, so a Polish string that dropped its placeholder
        /// silently loses the number, and one that gained a `{1}` throws on a call that passes one
        /// argument. Neither is visible in English.
        /// </summary>
        [Test]
        public void PlaceholdersMatchAcrossLanguages()
        {
            var wrong = new List<string>();

            foreach (var (key, english) in Loc.English)
            {
                if (!Loc.Polish.TryGetValue(key, out var polish))
                {
                    continue;
                }

                var inEnglish = Placeholders(english);
                var inPolish = Placeholders(polish);

                if (!inEnglish.SetEquals(inPolish))
                {
                    wrong.Add($"{key}: en has {Show(inEnglish)}, pl has {Show(inPolish)}");
                }
            }

            Assert.IsEmpty(wrong, "Placeholders differ between languages:\n  "
                + string.Join("\n  ", wrong));
        }

        private static HashSet<string> Placeholders(string value) =>
            Regex.Matches(value, @"\{(\d+)\}").Select(match => match.Groups[1].Value).ToHashSet();

        private static string Show(HashSet<string> set) =>
            set.Count == 0 ? "none" : string.Join(",", set.OrderBy(x => x));

        [Test]
        public void SwitchingLanguageChangesWhatComesBack()
        {
            var was = Loc.Current;

            try
            {
                Loc.Current = Language.English;
                var english = Loc.T("arch.title");

                Loc.Current = Language.Polish;
                var polish = Loc.T("arch.title");

                Assert.That(polish, Is.Not.EqualTo(english),
                    "The whole mechanism is that the same key answers differently.");

                Assert.That(polish, Is.EqualTo("ARCHITEKTURA"));
            }
            finally
            {
                Loc.Current = was;
            }
        }

        /// <summary>
        /// Every key a screen asks for is a key the book has.
        ///
        /// The same shape as `StylesheetTests.EveryClassTheInterfaceUsesIsStyled`, and for the same
        /// reason: a missing key does not throw, it renders the key itself, so `arch.tilte` ships as
        /// the word "arch.tilte" on a page nobody re-reads after the screenshot.
        /// </summary>
        [Test]
        public void EveryKeyTheInterfaceAsksForExists()
        {
            var folder = Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts");
            var missing = new List<string>();

            foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                foreach (Match match in Regex.Matches(text, @"Loc\.(?:T|Plural|Counted)\(\s*""([^""]+)"""))
                {
                    var key = match.Groups[1].Value;

                    // Plural and Counted are handed a stem and add the form themselves.
                    var known = Loc.English.ContainsKey(key)
                        || Loc.English.ContainsKey(key + ".one");

                    if (!known)
                    {
                        missing.Add($"{name}: {key}");
                    }
                }
            }

            missing.Sort();

            Assert.IsEmpty(missing, "Asked for from C# and absent from the phrase book, so the key "
                + "itself is what the player reads:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// Polish counted nouns have three forms and the middle one is not decoration.
        ///
        /// 2 to 4 take `few`, and 12 to 14 take `many` despite ending in the same digits. Getting
        /// that wrong is the single most obvious sign that a translation was done by a table lookup.
        /// </summary>
        [Test]
        public void ThePolishPluralRuleIsTheRealOne()
        {
            var was = Loc.Current;

            try
            {
                Loc.Current = Language.Polish;

                Assert.That(Loc.Plural(1, "noun.desk"), Is.EqualTo("biurko"));
                Assert.That(Loc.Plural(3, "noun.desk"), Is.EqualTo("biurka"));
                Assert.That(Loc.Plural(5, "noun.desk"), Is.EqualTo("biurek"));

                Assert.That(Loc.Plural(13, "noun.desk"), Is.EqualTo("biurek"),
                    "Thirteen takes the many form even though three takes few.");

                Assert.That(Loc.Plural(22, "noun.desk"), Is.EqualTo("biurka"),
                    "Twenty two takes few again.");
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
