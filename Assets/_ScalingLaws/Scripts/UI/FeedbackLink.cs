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
    /// Three things travel with the click and only three: the build, how far into the campaign the
    /// player was, and the name they typed on the card themselves. The first two are what a report
    /// is useless without; the third is there so the form opens already knowing who is filling it
    /// in, and it is only ever what somebody chose to type. Nothing is read off the machine, the
    /// game has no networking of its own, and this runs only because a button was pressed.
    /// </summary>
    public static class FeedbackLink
    {
        /// <summary>The form. A page the author controls rather than a third-party host.</summary>
        public const string BaseUrl = "https://pcworkman.dev/hck-labs/scaling-laws/";

        /// <summary>Where to send somebody when there is no form to send them to yet.</summary>
        public const string FallbackUrl =
            "https://github.com/HuckleR2003/ScalingLaws-AI/issues/new?template=playtest_feedback.md";

        /// <summary>
        /// How long a name may be before it is cut.
        ///
        /// A query string is not a place to put an essay, and a field with no ceiling is a field
        /// somebody will paste a book into. Long enough for any nickname.
        /// </summary>
        public const int LongestName = 40;

        /// <summary>
        /// Builds the address, with the build, the in-game day and the name on it.
        ///
        /// Separate from <see cref="Open"/> so a test can read what would be opened. `Application`
        /// cannot be driven from an EditMode test and the interesting half is the string.
        ///
        /// **Every part is escaped.** A nickname with an ampersand in it would otherwise end the
        /// parameter early and the page would read half a name and a broken build number.
        /// </summary>
        public static string UrlFor(GameDate date, string version, string name = null)
        {
            var build = Uri.EscapeDataString(string.IsNullOrWhiteSpace(version) ? "unknown" : version);
            var day = Math.Max(0, date.DayIndex).ToString(CultureInfo.InvariantCulture);

            var url = $"{BaseUrl}?build={build}&day={day}";

            var trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return url;
            }

            if (trimmed.Length > LongestName)
            {
                trimmed = trimmed[..LongestName];
            }

            return url + "&name=" + Uri.EscapeDataString(trimmed);
        }

        /// <summary>Opens the form. Called only from a button the player pressed.</summary>
        public static void Open(GameDate date)
        {
            Application.OpenURL(UrlFor(date, Application.version));
        }

        /// <summary>
        /// Opens the form with a name on it, from the card that asked for one.
        ///
        /// The date is passed rather than defaulted. A report that says which build but not how far
        /// into a campaign somebody was is missing half of what makes it actionable.
        /// </summary>
        public static void Open(GameDate date, string version, string name)
        {
            Application.OpenURL(UrlFor(date, version, name));
        }
    }
}
