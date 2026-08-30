using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Persistence;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The settings that only matter once somebody downloads the game.
    ///
    /// **Every one of these is invisible in the editor**, which is the whole reason they are worth
    /// a fixture. The editor has its own window, its own title bar and no icon, and it plays the
    /// scene you have open rather than the one the build settings list first. A project can be
    /// entirely correct on screen and ship an exe called ScalingLaws.exe, wearing the default Unity
    /// logo, reporting a version that appears in no changelog, in a window that covers its own
    /// bottom bar with the taskbar. All four of those were true the day before the first build.
    /// </summary>
    public sealed class ShippingTests
    {
        /// <summary>
        /// The version in the player is the version the changelog says shipped.
        ///
        /// Not bookkeeping. `FeedbackLink` puts `Application.version` into every report a
        /// playtester files, so a wrong number here sorts real bug reports under a release that
        /// does not exist, and nobody finds out until they try to reproduce one.
        /// </summary>
        [Test]
        public void TheVersionInThePlayerIsTheNewestVersionInTheChangelog()
        {
            var path = Path.Combine(Application.dataPath, "..", "CHANGELOG.md");

            Assert.That(File.Exists(path), Is.True, $"No changelog at {path}.");

            var headings = Regex.Matches(File.ReadAllText(path), @"^## \[(\d+\.\d+\.\d+)\]",
                RegexOptions.Multiline);

            Assert.That(headings.Count, Is.GreaterThan(0),
                "The changelog has no released version heading in it.");

            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(headings[0].Groups[1].Value),
                "The player's version and the newest changelog entry disagree, so every bug report "
                + "from this build would be filed against the wrong release.");
        }

        /// <summary>
        /// The build opens on the menu.
        ///
        /// Scene zero is what a player sees when the exe starts. Putting `Game` there drops them
        /// into an unconfigured campaign with no founder, which does not throw and does not look
        /// like a mistake, it looks like the game is broken.
        /// </summary>
        [Test]
        public void TheBuildOpensOnTheMenuAndCarriesTheGameWithIt()
        {
            var scenes = ScalingLaws.Editor.BuildPlayer.ScenesInOrder();

            Assert.That(scenes.Length, Is.EqualTo(2),
                "Expected the menu and the game: " + string.Join(", ", scenes));

            Assert.That(Path.GetFileNameWithoutExtension(scenes[0]), Is.EqualTo("MainMenu"),
                "The first scene in the build is what the executable starts on.");

            Assert.That(scenes.Any(scene => scene.EndsWith("Game.unity")), Is.True,
                "The game scene is not in the build, so NEW GAME would load nothing.");
        }

        /// <summary>
        /// The window, the taskbar and the exe carry the name of the game.
        ///
        /// `productName` defaulted to the project folder, so all three read `ScalingLaws`.
        /// </summary>
        [Test]
        public void TheGameIsNamedAfterItselfRatherThanAfterItsFolder()
        {
            Assert.That(PlayerSettings.productName, Is.EqualTo("Scaling Laws"));
            Assert.That(PlayerSettings.companyName, Is.EqualTo("HCK Labs"));
        }

        /// <summary>
        /// The executable has an icon.
        ///
        /// An unset icon is not a blank one, it is Unity's own logo, which says the game was
        /// exported and never finished before anybody has opened it.
        /// </summary>
        [Test]
        public void TheExecutableWearsItsOwnIcon()
        {
            var slots = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone,
                IconKind.Application).Length;

            Assert.That(slots, Is.GreaterThan(0), "Windows standalone reports no icon sizes.");

            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application);

            Assert.That(icons.Count(icon => icon != null), Is.EqualTo(slots),
                "Some icon slots are empty, and an empty slot falls back to the Unity logo. "
                + "Run Scaling Laws > Set the application icon.");
        }

        /// <summary>
        /// A window a player cannot resize is one they cannot rescue.
        ///
        /// The panel scales with the screen, so every size renders correctly and there is nothing
        /// to protect by forbidding this.
        /// </summary>
        [Test]
        public void TheWindowCanBeResized()
        {
            Assert.That(PlayerSettings.resizableWindow, Is.True);
        }

        /// <summary>
        /// **The windowed game fits on the display it opens on.** This is the one that shipped.
        ///
        /// `Screen.fullScreenMode = Windowed` changes presentation and leaves resolution alone, so
        /// leaving fullscreen on a 1080p desktop produced a 1920x1080 window on a 1920x1080 screen:
        /// title bar above the top, the game's bottom bar behind the taskbar, and no way to resize.
        /// </summary>
        [Test]
        public void TheWindowedGameFitsOnEveryDisplayItWillMeet()
        {
            var displays = new[]
            {
                new Vector2Int(1366, 768),
                new Vector2Int(1440, 900),
                new Vector2Int(1600, 900),
                new Vector2Int(1920, 1080),
                new Vector2Int(1920, 1200),
                new Vector2Int(2560, 1440),
                new Vector2Int(3440, 1440),
                new Vector2Int(3840, 2160)
            };

            foreach (var display in displays)
            {
                var window = GameSettings.WindowedSize(display.x, display.y);

                Assert.That(window.x, Is.LessThanOrEqualTo(display.x),
                    $"On {display.x}x{display.y} the window is wider than the screen.");

                Assert.That(window.y, Is.LessThan(display.y),
                    $"On {display.x}x{display.y} the window is as tall as the screen, which puts "
                    + "the bottom bar under the taskbar.");

                Assert.That(window.x, Is.GreaterThanOrEqualTo(960),
                    $"On {display.x}x{display.y} the window is too small to click the bottom bar.");
            }
        }

        /// <summary>
        /// The window is the shape the interface is laid out in.
        ///
        /// The panel scales against 1920x1080. A window of some other shape does not show more of
        /// anything, it just scales to the shorter axis and leaves the rest empty.
        /// </summary>
        [Test]
        public void TheWindowKeepsTheShapeThePanelScalesAgainst()
        {
            foreach (var display in new[] { new Vector2Int(1366, 768), new Vector2Int(3440, 1440) })
            {
                var window = GameSettings.WindowedSize(display.x, display.y);
                var aspect = window.x / (double)window.y;

                Assert.That(aspect, Is.EqualTo(16.0 / 9.0).Within(0.01),
                    $"On {display.x}x{display.y} the window is {window.x}x{window.y}.");
            }
        }

        /// <summary>Nonsense from a display driver still produces a window somebody can play in.</summary>
        [Test]
        public void ARubbishDisplayReadingStillProducesAUsableWindow()
        {
            foreach (var display in new[]
                     {
                         new Vector2Int(0, 0),
                         new Vector2Int(-1920, -1080),
                         new Vector2Int(int.MaxValue, int.MaxValue)
                     })
            {
                var window = GameSettings.WindowedSize(display.x, display.y);

                Assert.That(window.x, Is.InRange(960, GameSettings.DesignWidth),
                    $"{display.x}x{display.y} produced a {window.x}px window.");

                Assert.That(window.y, Is.GreaterThan(0));
            }
        }
    }
}
