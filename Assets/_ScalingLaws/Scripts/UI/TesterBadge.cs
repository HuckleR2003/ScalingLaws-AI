using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The line at the top of a build that went out to testers.
    ///
    /// It sits in the main menu and on every screen of a campaign, in the place the sandbox mark
    /// already uses, because somebody sending a report needs to be able to say which build they
    /// were playing without being asked.
    ///
    /// **The version is read from `Application.version` rather than written here**, and that is the
    /// whole of why this is a class instead of two labels. The version already lives in three
    /// places that do not read each other, `bundleVersion`, the README badge and the changelog
    /// heading, and the changelog records a release where a stale version string caused three real
    /// failures in PC Workman. A fourth copy on the one screen a tester quotes back is a fourth
    /// chance to be told 0.7.5 about a build that is 0.7.6.
    /// </summary>
    public static class TesterBadge
    {
        /// <summary>
        /// Whether this build says it is a test build.
        ///
        /// One word, in one file, so turning it off for a public release is not a search. A badge
        /// addressed to testers on a shop build reads as an unfinished game.
        /// </summary>
        public const bool Shown = true;

        /// <summary>What it says, with whatever version the player is actually running.</summary>
        public static string Text => Loc.T("tester.mark", Application.version);

        /// <summary>
        /// The label, or null when this is not a test build.
        ///
        /// Null rather than an empty element, so a caller that forgets to check adds nothing rather
        /// than adding an invisible one. `PickingMode.Ignore` for the reason every floating mark in
        /// this interface carries it: it sits over the chrome and must never eat a click meant for
        /// something underneath. Two invisible click eaters have shipped here already.
        /// </summary>
        public static Label Build()
        {
            if (!Shown)
            {
                return null;
            }

            var mark = new Label(Text) { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("tester-mark");

            return mark;
        }
    }
}
