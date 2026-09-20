using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        /// No length in the sheet is written in a unit USS does not have.
        ///
        /// **One `0.08em` took the whole stylesheet down.** USS parses lengths in pixels and
        /// percent; anything else fails the rule, and a sheet with a failed rule in it does not
        /// load at all. The game came up with the bottom bar stacking vertically, every panel
        /// unstyled, and nothing in the log to say why. `BootTests.TheLayoutIsActuallyStyled`
        /// caught it, which is what that fixture is for, and this catches it a minute earlier and
        /// says which unit did it.
        /// </summary>
        [Test]
        public void EveryLengthIsInAUnitUssUnderstands()
        {
            var offenders = Regex.Matches(Sheet, @"[-+]?[0-9]*\.?[0-9]+(em|rem|pt|vh|vw|ex|ch)")
                .Select(match => match.Value)
                .Distinct()
                .ToList();

            Assert.That(offenders, Is.Empty,
                "USS takes px and % and nothing else. One rule in a unit it cannot read stops the "
                + "whole sheet from loading: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// Nothing in the game draws smaller than this.
        ///
        /// **Reported as the single biggest problem with the game, from a laptop.** Fifty eight
        /// rules were setting type between 8 and 10.5px: the day count on an effect badge was 8px,
        /// the research tree drew its own node names at 11 and the card explaining a four month
        /// programme at 12.5. A caption nobody can read is not a caption, it is a row of grey marks
        /// where a figure belongs.
        ///
        /// A floor rather than a table of sizes, because a table goes stale at the speed the sheet
        /// grows and this cannot: a rule added tomorrow at 9px fails here the first time anybody
        /// runs the suite. The exemption below is the one place in the game where small is a
        /// measured decision rather than an oversight, and it says which measurement.
        /// </summary>
        private static readonly char[] NewlineChars = { '\r', '\n' };

        [Test]
        public void NothingIsSetSmallerThanTheFloor()
        {
            const float floor = 11f;

            // SKIP DAY and COMPANY INFO were cut to 9px to fit fifteen category slots, the clock
            // and the controls into a 1920 bar that wanted 1710px for the slots alone. The words
            // themselves are wider than the space at any larger size, and
            // `TheBottomBarFitsTheWindowItIsDrawnIn` is the measurement that says so.
            var exempt = new[] { ".hud-skip" };

            var offenders = new List<string>();

            foreach (Match rule in Regex.Matches(Sheet, @"([^{}]+)\{([^}]*)\}"))
            {
                var size = Regex.Match(rule.Groups[2].Value, @"font-size:\s*([0-9.]+)px");
                if (!size.Success)
                {
                    continue;
                }

                var value = float.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
                if (value >= floor)
                {
                    continue;
                }

                // The last line of the selector block, which is the selector this rule belongs to.
                // A grouped rule spans several lines and only the last one names the thing.
                var block = rule.Groups[1].Value.Trim();
                var cut = block.LastIndexOfAny(NewlineChars);
                var selector = (cut < 0 ? block : block.Substring(cut + 1)).Trim();

                if (System.Array.IndexOf(exempt, selector) >= 0)
                {
                    continue;
                }

                offenders.Add(selector + " at " + size.Groups[1].Value + "px");
            }

            Assert.That(offenders, Is.Empty,
                "Set smaller than " + floor + "px, which is under what a laptop screen reads:"
                + System.Environment.NewLine + string.Join(System.Environment.NewLine, offenders));
        }

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

        /// <summary>
        /// No rule is written with a selector UI Toolkit does not implement.
        ///
        /// **Found in the first built player's log, not by a test.** Five rules used `:last-child`
        /// to close the trailing gap after the last item in a row. USS has no positional pseudo
        /// classes at all: the parser drops the rule, warns once per occurrence into the log of
        /// every launch on every player's machine, and the styling silently never applies. All
        /// five had been dead since the day they were written.
        ///
        /// This is the same failure as a class named from C# and never written into the sheet,
        /// which the test above already guards, arriving from the other direction: a rule that
        /// looks correct, reads correctly, and is never applied to anything.
        /// </summary>
        [Test]
        public void NoRuleUsesASelectorUiToolkitCannotRead()
        {
            var sheet = Sheet;

            // Only the selector halves. The note recording why these cannot be used is prose in a
            // comment, and a guard that fails on its own explanation is a guard nobody keeps.
            var found = Regex.Matches(sheet, @"^[^\n/]*(:(?:last|first|nth|only)-[a-z-]+)[^\n]*\{",
                    RegexOptions.Multiline)
                .Select(match => match.Value.Trim())
                .ToList();

            Assert.IsEmpty(found,
                "UI Toolkit has no positional pseudo classes. These rules parse, warn in every "
                + "player's log and style nothing:\n  " + string.Join("\n  ", found));
        }
    }
}
