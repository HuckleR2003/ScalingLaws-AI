using System;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The bar that manages one model: shut it down, upgrade it, and see who is on it.
    ///
    /// Built to the same shape as the hosting switch, because it answers the same kind of question:
    /// two heavy actions side by side with the state of the thing between them, rather than a menu.
    /// Red on the left, amber in the middle, the live figure on the right.
    ///
    /// **SHUTDOWN only appears when there is something to shut down.** A withdrawn model shows what
    /// it did and offers nothing, which is the honest reading: the decision has already been taken
    /// and there is no putting it back.
    /// </summary>
    public static class ModelControlBar
    {
        /// <summary>
        /// One bar for one model.
        /// </summary>
        /// <param name="record">The model and where it stands today.</param>
        /// <param name="shutdown">Called when the player confirms. Null hides the control.</param>
        /// <param name="upgrade">Called to open the upgrade screen on this model.</param>
        /// <param name="armed">
        /// True when this model is the one waiting for a second click. Shutting a product down is not
        /// reversible, so the first click arms and the second commits; a single click next to an
        /// upgrade button would eventually be a mis-click that cannot be undone.
        /// </param>
        public static VisualElement Build(in ModelRecord record, Action shutdown, Action upgrade,
            bool armed)
        {
            var bar = new VisualElement();
            bar.AddToClassList("mcb");
            bar.EnableInClassList("mcb--armed", armed);

            if (record.CanRetire && shutdown != null)
            {
                var stop = new Button(shutdown)
                {
                    text = armed ? "CONFIRM SHUTDOWN" : "SHUTDOWN"
                };

                stop.AddToClassList("mcb__stop");
                stop.EnableInClassList("mcb__stop--armed", armed);

                stop.tooltip = armed
                    ? "Click again to withdraw it. This cannot be undone."
                    : $"Take {record.Model.Name} off sale for good. A tired line still wins share and "
                        + "is still served out of the same fleet as everything else.";

                bar.Add(stop);
            }
            else
            {
                // Keeps the middle button in the same place whether or not the left one is there, so
                // a column of these does not shuffle sideways as models retire.
                var gone = new Label(record.IsLive ? string.Empty : "WITHDRAWN");
                gone.AddToClassList("mcb__gone");
                bar.Add(gone);
            }

            var improve = new Button(upgrade) { text = "UPGRADE" };
            improve.AddToClassList("mcb__upgrade");
            improve.SetEnabled(record.IsLive && upgrade != null);

            improve.tooltip = record.IsLive
                ? "Run an upgrade programme on this model."
                : "A withdrawn model cannot be improved.";

            bar.Add(improve);

            var active = new VisualElement();
            active.AddToClassList("mcb__active");

            var caption = new Label(record.IsMarketed ? "ACTIVE" : record.StateWord);
            caption.AddToClassList("mcb__caption");
            active.Add(caption);

            var figure = new Label(record.IsMarketed
                ? UiFormat.Count(record.Users)
                : record.IsLive ? "no buyers" : UiFormat.Money(record.Model.LifetimeRevenueUsd));

            figure.AddToClassList("mcb__figure");
            active.Add(figure);

            bar.Add(active);
            return bar;
        }
    }
}
