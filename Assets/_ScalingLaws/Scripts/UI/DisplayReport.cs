using ScalingLaws.Data;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What this machine is actually drawing, in one line the player can read out loud.
    ///
    /// **It exists because two people spent three days describing two different screens.** Reports
    /// kept arriving from a laptop saying a page was cut in half, and every proof render taken here
    /// at the same nominal resolution showed the page whole. The panel is `ScaleWithScreenSize`
    /// against a 1920x1080 reference with `match 0.5`, so a 16:9 window of *any* size gives the
    /// identical logical page, measured: 1920x1080, 1600x900 and 1366x768 all produce a page of
    /// 1920 x 943. That rules out the obvious explanation and leaves nothing to look at.
    ///
    /// So the game says it. Window, logical panel, the scale between them, and the aspect. A
    /// screenshot of the settings sheet now answers in four numbers what a paragraph of description
    /// could not, and the answer is checkable by whoever reads it.
    ///
    /// **Nothing here is a setting and nothing here decides anything.** It reads what already
    /// happened and prints it, which is the same rule the status strip on the official page follows.
    /// </summary>
    public static class DisplayReport
    {
        /// <summary>
        /// The line itself. `panel` is any element attached to the panel being measured, usually
        /// the document root; null is answered rather than thrown, because a settings sheet that
        /// crashes when it cannot measure something is worse than one that says so.
        /// </summary>
        public static string Line(VisualElement panel)
        {
            var window = $"{Screen.width}x{Screen.height}";
            var mode = Screen.fullScreen ? Loc.T("display.full") : Loc.T("display.windowed");

            if (panel?.panel == null)
            {
                return Loc.T("display.line_short", window, mode);
            }

            var box = panel.panel.visualTree.layout;

            if (float.IsNaN(box.width) || float.IsNaN(box.height)
                || box.width <= 0f || box.height <= 0f)
            {
                return Loc.T("display.line_short", window, mode);
            }

            // The scale is the physical window over the logical page, which is the number that
            // explains everything else: at one they are the same, under one the game is being drawn
            // larger than the window and cropped, over one it is being drawn smaller.
            var scale = Screen.width / box.width;

            return Loc.T("display.line",
                window,
                $"{box.width.ToString("0", CultureInfo.InvariantCulture)}x"
                + box.height.ToString("0", CultureInfo.InvariantCulture),
                scale.ToString("0.000", CultureInfo.InvariantCulture),
                Aspect(Screen.width, Screen.height),
                mode);
        }

        /// <summary>
        /// The window's shape as a ratio, reduced, because that is the thing that can differ
        /// between two machines and change the layout.
        ///
        /// Written as a ratio rather than as a decimal: 16:9 and 16:10 are one glance apart and
        /// 1.778 against 1.600 is not.
        /// </summary>
        public static string Aspect(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return "?";
            }

            var divisor = GreatestCommonDivisor(width, height);

            return $"{width / divisor}:{height / divisor}";
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }

            return a == 0 ? 1 : a;
        }
    }
}
