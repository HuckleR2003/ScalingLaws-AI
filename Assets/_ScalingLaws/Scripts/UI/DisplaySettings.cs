using ScalingLaws.Data;
using ScalingLaws.Persistence;
using System;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The display block: the window sizes, and the line saying what the game is actually drawing.
    ///
    /// **It is a shared builder because it shipped on one settings sheet out of two.** 0.6.1 was cut
    /// as a test build for exactly one purpose, so that a second machine could finally say what it
    /// was drawing, and both halves of that went onto the pause sheet only. The sheet a person opens
    /// to look at a display problem is the one in the main menu, before any campaign exists, and
    /// that one had nothing on it but a fullscreen tick. Two people then took screenshots of it and
    /// reported, correctly, that there was nothing there.
    ///
    /// Fourteenth time in this project a finished mechanism has been unreachable, and the first
    /// where the control existed, worked, and was simply on the other screen. A caller sweep cannot
    /// see that fault: `DisplayReport` had a caller, so it looked connected.
    ///
    /// One builder with two callers, so a size added to the list appears on both sheets and the two
    /// readouts can never word the same fact differently.
    /// </summary>
    public static class DisplaySettings
    {
        /// <summary>
        /// `changed` is raised after a size is picked, for a sheet that needs to redraw itself to
        /// move the lit chip. Null is fine: the window still changes, only the highlight waits for
        /// the next time the sheet is built.
        /// </summary>
        public static VisualElement Build(Action changed = null)
        {
            var block = new VisualElement();

            var window = new VisualElement();
            window.AddToClassList("pause__setting");

            var windowLabel = new Label(Loc.T("settings.window"));
            windowLabel.AddToClassList("pause__label");
            window.Add(windowLabel);

            var sizes = new VisualElement();
            sizes.AddToClassList("pause__choices");

            foreach (var size in GameSettings.WindowSizes)
            {
                var pick = size;

                var chip = new Button(() =>
                {
                    GameSettings.SetWindowSize(pick.Width, pick.Height);
                    changed?.Invoke();
                })
                {
                    text = pick.Width == 0
                        ? Loc.T("settings.window.auto")
                        : $"{pick.Width}x{pick.Height}"
                };

                chip.AddToClassList("pause__chip");
                chip.EnableInClassList("pause__chip--on",
                    GameSettings.WindowWidth == pick.Width
                    && GameSettings.WindowHeight == pick.Height);

                sizes.Add(chip);
            }

            window.Add(sizes);
            block.Add(window);

            // **What this machine is actually drawing, in one line.** A screenshot of it answers in
            // four numbers what a paragraph of description could not, and whoever reads them can
            // check them. It is a reading, not a setting.
            var report = new Label(DisplayReport.Line(block));
            report.AddToClassList("pause__hint");
            report.AddToClassList("pause__display");

            // Measured after it has been laid out into a panel, or the panel it is asked about is
            // the one it is not in yet and the line comes back short.
            report.RegisterCallback<GeometryChangedEvent>(_ =>
                report.text = DisplayReport.Line(report));

            block.Add(report);

            return block;
        }
    }
}
