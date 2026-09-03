using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The furniture around the game: the cold open, the keys, the bottom bar and the cards that
    /// say what a control is for.
    ///
    /// **None of this can be checked by driving the simulation**, which is the whole reason it keeps
    /// going wrong. The intro printed its opening twice for a week. The bottom bar wanted 1710px of
    /// a 1920px window and printed the category row over the clock. Both were found by the author
    /// looking at a screenshot, and neither could have failed a test that only advances days.
    ///
    /// So these read the source and the stylesheet, the same way <see cref="UiWiringTests"/> does.
    /// Crude, and it catches the exact class of fault that has actually shipped.
    /// </summary>
    public sealed class ShellChromeTests
    {
        private static string UiFolder =>
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");

        private static string Source(string fileName) =>
            File.ReadAllText(Path.Combine(UiFolder, fileName));

        private static string StyleSheet() =>
            File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Resources", "ScalingLaws.uss"));

        // ---- the keys ---------------------------------------------------------------------------

        [Test]
        public void SpaceStopsTheClockAndStartsItAgain()
        {
            var keys = new KeyboardShortcuts(null, () => SimSpeed.Normal, _ => { });

            Assert.AreEqual(SimSpeed.Paused,
                keys.Resolve(SimSpeed.Normal, space: true, false, false, false));

            Assert.AreEqual(SimSpeed.Normal,
                keys.Resolve(SimSpeed.Paused, space: true, false, false, false));
        }

        [Test]
        public void SpaceResumesTheSpeedItWasPausedFrom()
        {
            var keys = new KeyboardShortcuts(null, () => SimSpeed.Fast, _ => { });

            // Running at triple, paused, then space. A player who paused to read something wants the
            // game back the way they left it, not reset to the default.
            keys.Resolve(SimSpeed.Fast, space: true, false, false, false);

            Assert.AreEqual(SimSpeed.Fast,
                keys.Resolve(SimSpeed.Paused, space: true, false, false, false));
        }

        [Test]
        public void SpaceOnAGameThatHasNeverRunPicksTheDefault()
        {
            var keys = new KeyboardShortcuts(null, () => SimSpeed.Paused, _ => { });

            Assert.AreEqual(KeyboardShortcuts.DefaultResumeSpeed,
                keys.Resolve(SimSpeed.Paused, space: true, false, false, false),
                "The first press of the first day has no previous speed to go back to.");
        }

        [Test]
        public void TheNumberKeysAreTheThreeSpeedsInOrder()
        {
            var keys = new KeyboardShortcuts(null, () => SimSpeed.Paused, _ => { });

            Assert.AreEqual(SimSpeed.Slow, keys.Resolve(SimSpeed.Paused, false, true, false, false));
            Assert.AreEqual(SimSpeed.Normal, keys.Resolve(SimSpeed.Paused, false, false, true, false));
            Assert.AreEqual(SimSpeed.Fast, keys.Resolve(SimSpeed.Paused, false, false, false, true));
        }

        [Test]
        public void NothingPressedChangesNothing()
        {
            var keys = new KeyboardShortcuts(null, () => SimSpeed.Normal, _ => { });

            Assert.IsNull(keys.Resolve(SimSpeed.Normal, false, false, false, false),
                "A frame with no key down must not re-send the speed the clock is already at.");
        }

        [Test]
        public void EveryBoundKeyIsDescribedInTheTable()
        {
            Assert.AreEqual(4, KeyboardShortcuts.All.Length);

            foreach (var shortcut in KeyboardShortcuts.All)
            {
                Assert.IsNotEmpty(shortcut.KeyName);
                Assert.IsNotEmpty(shortcut.Action);
            }

            // The table is what the interface reads to say what a key does. A binding that exists in
            // Resolve and not here is a shortcut nobody can be told about.
            var source = Source("KeyboardShortcuts.cs");
            foreach (var key in new[] { "Space", "Alpha1", "Alpha2", "Alpha3" })
            {
                StringAssert.Contains(key, source);
            }
        }

        [Test]
        public void TheKeysGoQuietWhileTheresATextFieldInFocus()
        {
            // Typing "GPT-3" into a model name must not send the clock to triple speed. There is no
            // panel in an EditMode test so this cannot be driven, but the check must exist and it
            // must not be the one that looks right and disables every shortcut forever: Button
            // derives from TextElement, so testing the focused element for TextElement would mean
            // one click anywhere killed the keyboard.
            var source = Source("KeyboardShortcuts.cs");

            StringAssert.Contains("is TextField", source);
            StringAssert.DoesNotContain("is TextElement", source);
        }

        // ---- the cold open ------------------------------------------------------------------------

        [Test]
        public void TheOpeningIsPrintedOnce()
        {
            var source = Source("MainMenuController.cs");

            // It was printed twice: a typed label mounted in BuildIntro, and the same words revealed
            // a line at a time by an Update loop that was never removed when the typewriter arrived.
            Assert.AreEqual(1, Regex.Matches(source, @"IntroLines\s*=").Count,
                "One list of opening lines, or the screen prints two of them.");

            StringAssert.DoesNotContain("private const string IntroText", source,
                "The one-piece constant was the second copy of the opening.");
        }

        [Test]
        public void TheHeaderIsTheSecondLineAndTheMoneyIsAboveIt()
        {
            var source = Source("MainMenuController.cs");

            // **The lines are keys now, not literals.** The opening shipped in English on a Polish
            // machine because a `static readonly string[]` is built once, before a language has been
            // chosen, so it froze whatever was current at type load. The order is still the thing
            // worth guarding; it is just measured on the keys.
            var money = source.IndexOf("\"intro.money\"", System.StringComparison.Ordinal);
            var header = source.IndexOf("\"intro.month\"", System.StringComparison.Ordinal);

            Assert.Greater(money, 0, "The money line is not in the opening at all.");
            Assert.Greater(header, 0, "The header is not in the opening at all.");
            Assert.Less(money, header, "The money line sits above the header, which is the ask.");

            // And it has to resolve per read rather than once, or keying it changed nothing.
            StringAssert.Contains("private static string[] IntroLines => new[]", source,
                "A static array is built before the player picks a language.");

            // And the big styling has to follow the header to its new index, or the money line is
            // set at 46px and the header at 17px.
            StringAssert.Contains("if (introLine == 1)", source);
        }

        [Test]
        public void TheFilmCannotStrandThePlayerOnABlackScreen()
        {
            var source = Source("MainMenuController.cs");

            StringAssert.Contains("errorReceived", source, "A file that will not decode has to move on.");
            StringAssert.Contains("loopPointReached", source, "And the end of the clip has to move on.");
            StringAssert.Contains("ExecuteLater(watchdog)", source,
                "A missing platform codec plays nothing and raises nothing, which is neither of the "
                + "other two cases and is the one that hangs.");
        }

        [Test]
        public void TheFilmActuallyLoadsAsAVideoClip()
        {
            // Not a file-existence check. The file can be on disk, in the right folder, and still
            // not load: Resources.Load returns null if Unity imported it as something other than a
            // VideoClip, and the opening would then cut silently to the creator with a warning in a
            // console nobody is reading.
            var clip = UnityEngine.Resources.Load<UnityEngine.Video.VideoClip>(
                "Intro/ScalingLaws_Introduction");

            Assert.IsNotNull(clip,
                "Resources.Load reads Resources/Intro. A film in Art/ is a film the build cannot see.");

            Assert.Greater(clip.length, 0.5, "A clip of no length is a clip that will not play.");
            Assert.Greater(clip.width, 0);

            // The watchdog is sized from this, so a clip longer than the guard would fire the guard
            // in the middle of the film.
            Assert.Less(clip.length, 120.0, "An intro this long needs a different guard.");
        }

        // ---- the bottom bar --------------------------------------------------------------------

        [Test]
        public void TheBottomBarFitsTheWindowItIsDrawnIn()
        {
            var uss = StyleSheet();

            var slotWidth = Number(uss, @"\.hud-slot \{[^}]*?width:\s*(\d+)px");
            var slotLeft = Number(uss, @"\.hud-slot \{[^}]*?margin-left:\s*(\d+)px");
            var slotRight = Number(uss, @"\.hud-slot \{[^}]*?margin-right:\s*(\d+)px");

            // Fifteen categories, and the count is read from the shell rather than written here so
            // that adding a sixteenth fails this rather than silently overflowing.
            var slots = Regex.Matches(
                Source("GameShell.cs"), @"hud\.AddSlot\(").Count;

            Assert.GreaterOrEqual(slots, 15);

            var row = slots * (slotWidth + slotLeft + slotRight);

            // The panel scales to a 1920 reference, but only at 16:9. At 16:10 the match-0.5 blend
            // gives a virtual width nearer 1820, and that is the case the bar has to survive.
            const int NarrowestVirtualWidth = 1820;
            const int TimeModule = 360;
            const int BarPadding = 28;

            Assert.LessOrEqual(row, NarrowestVirtualWidth - TimeModule - BarPadding,
                $"{slots} categories want {row}px. The clock and the speed controls need the rest, "
                + "and when they do not get it the row prints over them.");
        }

        /// <summary>
        /// A room keeps the disc; a document gets the rectangular plate.
        ///
        /// **The disc overhangs the bar by about 170px and no page reserves for it.** On TEAM it
        /// covered the brand line, on BUSINESS the end of the marketing sentence, in the basement a
        /// hint that had to be moved out of its way. A global bottom margin would have paid for it
        /// on twenty screens to keep an ornament on two.
        ///
        /// Both readings are pushed on every refresh, whichever is on screen, because branching on
        /// the visible one is how the hidden half drifts and then appears already wrong.
        /// </summary>
        [Test]
        public void ADocumentGetsTheRectangularClockAndARoomKeepsTheDisc()
        {
            var hud = new GameHud(_ => { }, () => { });

            var disc = hud.Root.Q(className: "hud-time__overhang");
            var plate = hud.Root.Q(className: "hud-clock");

            Assert.IsNotNull(disc, "The disc is gone, so this is checking nothing.");
            Assert.IsNotNull(plate, "There is no rectangular clock for a document screen.");

            hud.ShowDial(false);
            Assert.AreEqual(DisplayStyle.None, disc.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, plate.style.display.value);

            hud.ShowDial(true);
            Assert.AreEqual(DisplayStyle.Flex, disc.style.display.value);
            Assert.AreEqual(DisplayStyle.None, plate.style.display.value);

            // Half past midday, so a reading that silently stayed at its built value is visible.
            hud.Refresh(GameDate.FromCalendar(2024, 3, 18), SimSpeed.Normal, 0.5);

            var readings = hud.Root.Query<Label>()
                .Where(label => label.ClassListContains("hud-time__clock")
                                || label.ClassListContains("hud-clock__time"))
                .ToList();

            Assert.AreEqual(2, readings.Count, "There should be exactly two clock readings.");
            Assert.AreEqual(readings[0].text, readings[1].text,
                "The disc and the plate are two views of one day. Letting the hidden one fall "
                + "behind is how it appears already wrong the moment it is shown.");

            Assert.IsTrue(Source("GameShell.cs").Contains("hud.ShowDial("),
                "Nothing tells the bar which kind of screen it is on, so the disc is back over "
                + "the bottom-left corner of every page.");
        }

        [Test]
        public void TheSpeedControlsStartAtTheLeftEdge()
        {
            var uss = StyleSheet();

            var margin = Number(uss, @"\.hud-time__controls \{[^}]*?margin-left:\s*(\d+)");

            Assert.AreEqual(0, margin,
                "The dial is out of flow and sits above the bar. The 200px reserve was holding "
                + "space for something that is not there, and the category row paid for it.");
        }

        [Test]
        public void NothingCanPaintOverTheClock()
        {
            var uss = StyleSheet();

            var host = Regex.Match(uss, @"\.hud__slots \{[^}]*\}").Value;
            StringAssert.Contains("overflow: hidden", host,
                "Sizing the row to fit is the fix; clipping it is the guarantee. Without this a "
                + "window the numbers did not anticipate goes straight back to overlapping labels.");
        }

        // ---- the insight cards -------------------------------------------------------------------

        [Test]
        public void EveryCategoryInTheBarSaysWhatItIsFor()
        {
            var shell = Source("GameShell.cs");

            var calls = Regex.Matches(shell,
                @"hud\.AddSlot\((?:[^()]|\([^()]*\))*?\);", RegexOptions.Singleline);

            Assert.GreaterOrEqual(calls.Count, 15, "The slot calls were not found, so this is "
                                                   + "checking nothing.");

            var silent = new List<string>();

            foreach (Match call in calls)
            {
                // Five arguments is label, key, action, icon, description. Counted properly rather
                // than by counting commas: every description is a sentence and sentences have commas
                // in them, which is how the first version of this test failed on a screen that was
                // right.
                if (TopLevelArguments(call.Value) < 5)
                {
                    silent.Add(call.Value.Substring(0, System.Math.Min(60, call.Value.Length)));
                }
            }

            CollectionAssert.IsEmpty(silent,
                "A tab with no description is a noun on a bar. That is what the cards are for.");
        }

        /// <summary>
        /// Counts the arguments of one call, ignoring commas inside strings and inside nested calls.
        /// </summary>
        private static int TopLevelArguments(string call)
        {
            var depth = 0;
            var inString = false;
            var arguments = 0;
            var seenAnything = false;

            for (var index = 0; index < call.Length; index++)
            {
                var character = call[index];

                if (inString)
                {
                    if (character == '\\')
                    {
                        index++;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inString = true;
                        seenAnything = true;
                        break;
                    case '(':
                        depth++;
                        break;
                    case ')':
                        depth--;
                        break;
                    case ',' when depth == 1:
                        arguments++;
                        break;
                    default:
                        if (depth >= 1 && !char.IsWhiteSpace(character))
                        {
                            seenAnything = true;
                        }

                        break;
                }
            }

            return seenAnything ? arguments + 1 : 0;
        }

        [Test]
        public void TheCardIsMountedAboveEverythingRatherThanInsideWhatItDescribes()
        {
            StringAssert.Contains("InsightTip.Host = root", Source("UiBootstrap.cs"),
                "Mounted inside the bar it would be clipped by the bar, which now clips on purpose.");

            var uss = StyleSheet();
            StringAssert.Contains(".insight {", uss);
            StringAssert.Contains(".insight--in {", uss);
        }

        // ---- the places screen ---------------------------------------------------------------------

        [Test]
        public void TheHouseHasAPhotographOfTheHouse()
        {
            var art = Path.Combine(Application.dataPath, "_ScalingLaws", "Resources", "Offices",
                "office_house.png");

            Assert.IsTrue(File.Exists(art),
                "LVL 0 fell back to the office glyph from the bottom bar, so the first row of the "
                + "screen showed a 64px interface icon where a room belongs.");

            var chooser = Source("OfficeChooser.cs");
            StringAssert.DoesNotContain("Ui/office_upgrade", chooser,
                "And the fallback that did it has to be gone, or a later missing file brings it back.");
        }

        private static int Number(string uss, string pattern)
        {
            var match = Regex.Match(uss, pattern, RegexOptions.Singleline);
            Assert.IsTrue(match.Success, $"No match for {pattern}. The rule was renamed or removed.");
            return int.Parse(match.Groups[1].Value);
        }
    }
}
