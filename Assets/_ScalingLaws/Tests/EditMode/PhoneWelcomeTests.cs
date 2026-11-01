using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The phone's opening screen is somebody else's selection, not a question to the player.
    ///
    /// **Francisco's report: four options shown, two given.** The app's home list was numbered one
    /// to four, which is the signal to pick one, and three of the four name features this game
    /// genuinely has elsewhere. Every row ran the handset's universal skip, so choosing
    /// Achievements and choosing Contact List both produced the conversation and its two buttons.
    /// The player did not get a dead control; they got one that answered every choice the same way,
    /// which reads worse.
    ///
    /// **Making the rows work was the wrong fix and that is the part worth keeping.** This is the
    /// first minute of the game, inside a scripted boot, and a live Achievements row here drops a
    /// new player into a page of empty trophies before they have a company.
    ///
    /// It reads the source because the menu is built on the handset's own scheduler, and an
    /// EditMode test has no panel, so nothing in it ever runs.
    /// </summary>
    public sealed class PhoneWelcomeTests
    {
        private static readonly string PhoneSource = Path.Combine(
            Application.dataPath, "_ScalingLaws", "Scripts", "UI", "PhonePanel.cs");

        /// <summary>The body of `ShowWelcome`, up to the method that follows it.</summary>
        private static string Welcome()
        {
            var source = File.ReadAllText(PhoneSource);
            var start = source.IndexOf("private void ShowWelcome()", System.StringComparison.Ordinal);

            Assert.That(start, Is.GreaterThan(-1),
                "ShowWelcome has been renamed. This fixture is about what that screen shows, so "
                + "point it at the new name rather than deleting it.");

            var end = source.IndexOf("private void ShowChat", start, System.StringComparison.Ordinal);

            return end > start ? source.Substring(start, end - start) : source.Substring(start);
        }

        [Test]
        public void TheOpeningMenuDoesNotNumberItsRows()
        {
            var numbered = new Regex(@"index\s*\+\s*1", RegexOptions.Compiled);

            Assert.That(numbered.IsMatch(Welcome()), Is.False,
                "The rows on the phone's opening screen are numbered again. A numbered list is an "
                + "instruction to pick one, and these cannot be picked: every one of them runs the "
                + "handset's skip and opens the conversation. That was reported as four options "
                + "offered and two given.");
        }

        [Test]
        public void TheRowsNobodyTookAreMarkedAsNotTaken()
        {
            Assert.That(Welcome(), Does.Contain("dinapp__row--passed"),
                "Only the chosen row is being marked, so the other three sit at full strength "
                + "beside it and the screen reads as four live options with one highlighted. "
                + "Mark the rest as passed, or this is the same fault in a second shape.");
        }

        /// <summary>
        /// The row that lights up is one the list actually has.
        ///
        /// Not pedantry: the index is a constant beside a hand-written array in another file, and
        /// an out-of-range read here throws inside a scheduled callback during the opening, where
        /// nothing is watching for it.
        /// </summary>
        [Test]
        public void TheRowThatLightsUpExists()
        {
            Assert.That(GuideScript.AutoSelectedMenuItem, Is.InRange(0, GuideScript.AppMenu.Count - 1));
            Assert.That(GuideScript.AppMenu[GuideScript.AutoSelectedMenuItem], Is.EqualTo("Messages"),
                "The conversation opens straight after this row lights up, so it has to be the "
                + "messages one or the phone appears to open the wrong app.");
        }
    }
}
