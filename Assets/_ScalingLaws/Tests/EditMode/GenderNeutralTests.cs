using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The game never tells the player what they did in a gender.
    ///
    /// **The founder can be a woman, so Polish has a problem English does not.** Polish puts gender
    /// in the past tense: "Sprzedałeś firmę" is the game deciding the player is a man, and there is
    /// no neutral way to leave it. Every one of those was rewritten in the present tense or
    /// impersonally, which needs no gender field, no question at the creator and no save migration.
    ///
    /// **This exists because the list came back longer than the one that was handed over.** An
    /// outside pass found thirty nine against a snapshot of 1,961 phrases; re-derived against 2,481
    /// there were forty six, and seven of the new ones had been written that same day by whoever was
    /// fixing the other thirty nine. A rule nothing enforces is a rule that decays at the speed the
    /// book grows.
    ///
    /// Reads the source rather than the dictionary, for the same reason `NoPhraseIsWrittenTwice`
    /// does: the failure is in what somebody typed.
    /// </summary>
    public sealed class GenderNeutralTests
    {
        /// <summary>
        /// Second person past tense, either gender.
        ///
        /// `sam` is deliberately not here. It matched twenty entries on the first pass and nineteen
        /// were "ten sam" or "sam generuje", meaning "the same" and "itself". A pattern that is
        /// wrong nineteen times in twenty trains people to ignore the failure.
        /// </summary>
        private static readonly Regex SecondPersonPast =
            new(@"\w+(?:łeś|łbyś|łaś|łabyś|ąłeś|ęłaś)\b", RegexOptions.IgnoreCase);

        /// <summary>
        /// Lines a named character says about themselves, where the gender is settled and correct.
        ///
        /// Emil is a man and the people who write the reviews are who they are. Widening the rule to
        /// cover them would make the tutorial read like a form.
        /// </summary>
        private static readonly HashSet<string> SpokenByAName = new()
        {
            "guide.backlog.2",
            "guide.step.create_fine",
            "guide.step.arch_advised",
            "guide.step.emil_company",
            "world.nationalblock.body",
            "mg.speed_critical",
            "mg.speed_stable",
            "feedback.mail.body",
        };

        [Test]
        public void NoPolishPhraseTellsThePlayerWhatTheyDidInAGender()
        {
            var path = Path.Combine(Application.dataPath,
                "_ScalingLaws", "Scripts", "Data", "Loc.cs");

            Assert.That(File.Exists(path), Is.True, $"Cannot read the phrase book at {path}");

            var source = File.ReadAllText(path);
            var polish = source.IndexOf("Language.Polish", System.StringComparison.Ordinal);

            Assert.That(polish, Is.GreaterThan(0),
                "The Polish dictionary could not be found, so this test checked nothing.");

            var entries = Regex.Matches(source[polish..], "\\[\"([a-z0-9_.]+)\"\\]\\s*=\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

            Assert.That(entries.Count, Is.GreaterThan(1000),
                "Far too few Polish entries were read, so the pattern is wrong rather than the book.");

            var gendered = new List<string>();

            foreach (Match entry in entries)
            {
                var key = entry.Groups[1].Value;

                if (SpokenByAName.Contains(key))
                {
                    continue;
                }

                var forms = SecondPersonPast.Matches(entry.Groups[2].Value)
                    .Select(match => match.Value)
                    .Distinct()
                    .ToList();

                if (forms.Count > 0)
                {
                    gendered.Add($"{key}: {string.Join(", ", forms)}");
                }
            }

            CollectionAssert.IsEmpty(gendered,
                "These Polish phrases address the player in a gender, and the founder can be a "
                + "woman. Rewrite in the present tense or impersonally rather than adding a gender "
                + "field: \"Zapłaciłeś za tekst o nich\" becomes \"Płatny tekst o nich\", and "
                + "\"Gdzie utknąłeś?\" becomes \"W którym miejscu się zacięło?\".\n  "
                + string.Join("\n  ", gendered));
        }
    }
}
