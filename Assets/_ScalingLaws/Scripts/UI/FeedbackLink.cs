using System;
using System.Globalization;
using ScalingLaws.Core;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where "tell me where you got stuck" goes.
    ///
    /// **The interface owns the address and the simulation never learns it.** Opening a browser is
    /// `Application.OpenURL`, and `Simulation/` does not import UnityEngine, so the letter says only
    /// that it offers a link.
    ///
    /// Two facts travel with the click, and only two: the build this was played on, and how far into
    /// the campaign the player was. Both are things a report is useless without and neither says
    /// anything about the person. Nothing else is attached, the game has no networking of its own,
    /// and this only ever runs because somebody pressed a button.
    /// </summary>
    public static class FeedbackLink
    {
        /// <summary>The form. A page the author controls rather than a third-party host.</summary>
        public const string BaseUrl = "https://pcworkman.dev/scaling-laws/feedback";

        /// <summary>Where to send somebody when there is no form to send them to yet.</summary>
        public const string FallbackUrl =
            "https://github.com/HuckleR2003/ScalingLaws-AI/issues/new?template=playtest_feedback.md";

        /// <summary>
        /// Builds the address, with the build and the in-game day on it.
        ///
        /// Separate from <see cref="Open"/> so a test can read what would be opened. `Application`
        /// cannot be driven from an EditMode test and the interesting half is the string.
        /// </summary>
        public static string UrlFor(GameDate date, string version)
        {
            var build = Uri.EscapeDataString(string.IsNullOrWhiteSpace(version) ? "unknown" : version);
            var day = Math.Max(0, date.DayIndex).ToString(CultureInfo.InvariantCulture);

            return $"{BaseUrl}?build={build}&day={day}";
        }

        /// <summary>Opens the form. Called only from a button the player pressed.</summary>
        public static void Open(GameDate date)
        {
            Application.OpenURL(UrlFor(date, Application.version));
        }
    }
}
