using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The two row shapes that more than one screen draws.
    ///
    /// They live here rather than being written twice because a bar that is four pixels tall on one
    /// screen and six on another reads as two different widgets, and nobody would ever notice the
    /// drift in review. The styles were already shared; only the construction was not.
    /// </summary>
    public static class UiParts
    {
        private static readonly Color DefaultFill = new(0.36f, 0.62f, 0.88f);

        /// <summary>A caption, a slim proportion bar, and the figure it represents.</summary>
        public static VisualElement ThinBarRow(string label, string value, double fraction,
            Color? fill = null)
        {
            var row = new VisualElement();
            row.AddToClassList("thin-bar");

            var caption = new Label(label);
            caption.AddToClassList("thin-bar__label");
            row.Add(caption);

            var track = new VisualElement();
            track.AddToClassList("thin-bar__track");

            var bar = new VisualElement();
            bar.AddToClassList("thin-bar__fill");

            var clamped = double.IsNaN(fraction) ? 0.0 : Math.Clamp(fraction, 0.0, 1.0);
            bar.style.width = Length.Percent((float)(clamped * 100.0));
            bar.style.backgroundColor = fill ?? DefaultFill;
            track.Add(bar);

            row.Add(track);

            var amount = new Label(value);
            amount.AddToClassList("thin-bar__value");
            row.Add(amount);

            return row;
        }

        /// <summary>A name on the left and a figure on the right, sharing one line.</summary>
        public static VisualElement StatLine(string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("rnow-stat");

            var label = new Label(name);
            label.AddToClassList("rnow-stat__name");
            row.Add(label);

            var figure = new Label(value);
            figure.AddToClassList("rnow-stat__value");
            row.Add(figure);

            return row;
        }
    }
}
