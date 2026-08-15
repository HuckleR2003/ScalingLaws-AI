using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The stylesheet, checked for the two faults that make an edit silently do nothing.
    ///
    /// **Both of these cost a turn.** A request to make the research rings a quarter larger was
    /// applied to a `.tree-pip` rule that a later copy of the same rule overrode, so the numbers in
    /// the file changed and the screen did not. The channel price had two `font-size` declarations
    /// inside one block, and the stale one was second, so it won.
    ///
    /// Neither is a compile error, neither shows up in a screenshot unless you already know the
    /// number you expected, and the stylesheet is four thousand lines long.
    /// </summary>
    public sealed class StylesheetTests
    {
        private static string Sheet =>
            File.ReadAllText(Path.Combine(Application.dataPath, "_ScalingLaws", "Resources",
                "ScalingLaws.uss"));

        /// <summary>
        /// One rule per selector.
        ///
        /// A second rule for the same selector is not always wrong in CSS, but in a single hand
        /// written sheet it is nearly always an edit that did not know the first one existed, and it
        /// makes every later edit a coin flip about which copy gets changed.
        /// </summary>
        [Test]
        public void NoSelectorIsDefinedTwice()
        {
            var counts = new Dictionary<string, int>();

            foreach (Match match in Regex.Matches(Sheet, @"^(\.[A-Za-z0-9_-]+)\s*\{", RegexOptions.Multiline))
            {
                var selector = match.Groups[1].Value;
                counts[selector] = counts.TryGetValue(selector, out var seen) ? seen + 1 : 1;
            }

            var doubled = new List<string>();
            foreach (var (selector, count) in counts)
            {
                if (count > 1)
                {
                    doubled.Add($"{selector} x{count}");
                }
            }

            doubled.Sort();

            Assert.IsEmpty(doubled,
                $"{doubled.Count} selectors are defined more than once, so the last copy silently "
                + "wins and editing any of the others does nothing:\n  "
                + string.Join("\n  ", doubled)
                + "\n\nMerge them. This has already made two resize requests appear to be ignored.");
        }

        /// <summary>
        /// One declaration of a property per rule.
        ///
        /// Two font sizes in one block is the same failure at a smaller scale, and it is harder to
        /// see because both are on screen at once.
        /// </summary>
        [Test]
        public void NoRuleSetsTheSamePropertyTwice()
        {
            var offenders = new List<string>();

            foreach (Match rule in Regex.Matches(Sheet, @"^(\.[^\{\n]+)\{([^\}]*)\}",
                         RegexOptions.Multiline))
            {
                var selector = rule.Groups[1].Value.Trim();
                var seen = new Dictionary<string, int>();

                foreach (Match declaration in Regex.Matches(rule.Groups[2].Value,
                             @"^\s*([-a-zA-Z]+)\s*:", RegexOptions.Multiline))
                {
                    var property = declaration.Groups[1].Value;
                    seen[property] = seen.TryGetValue(property, out var count) ? count + 1 : 1;
                }

                foreach (var (property, count) in seen)
                {
                    if (count > 1)
                    {
                        offenders.Add($"{selector} sets {property} {count} times");
                    }
                }
            }

            offenders.Sort();

            Assert.IsEmpty(offenders,
                "A rule that sets the same property twice keeps the last one:\n  "
                + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// Every class the interface asks for exists in the sheet.
        ///
        /// The oldest recurring fault in this project: a class named from C# and never written into
        /// the stylesheet takes default flex, which does not throw and does not look obviously wrong,
        /// it just quietly collapses whatever it was on.
        /// </summary>
        [Test]
        public void EveryClassTheInterfaceUsesIsStyled()
        {
            var sheet = Sheet;
            var declared = new HashSet<string>();

            foreach (Match match in Regex.Matches(sheet, @"\.([A-Za-z0-9_-]+)"))
            {
                declared.Add(match.Groups[1].Value);
            }

            var folder = Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");
            var missing = new List<string>();

            foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                foreach (Match match in Regex.Matches(text,
                             @"(?:AddToClassList|EnableInClassList)\(""([^""]+)"""))
                {
                    if (!declared.Contains(match.Groups[1].Value))
                    {
                        missing.Add($"{name}: {match.Groups[1].Value}");
                    }
                }
            }

            missing.Sort();

            Assert.IsEmpty(missing,
                "Named from C# and absent from the stylesheet, so it takes default flex and quietly "
                + "collapses what it is on:\n  " + string.Join("\n  ", missing));
        }
    }
}
