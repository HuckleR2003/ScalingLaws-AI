using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The bottom interface: a half disc on the left carrying the date, the clock and the speed
    /// controls, a row of category slots across the middle, and one thin gradient line along the
    /// very bottom edge showing how far the day has run.
    ///
    /// It replaces the left rail. A rail of eleven text buttons down the side of the screen was a
    /// list of screens; this is a control panel, and it leaves the whole width of the window for the
    /// screen the player is actually reading.
    ///
    /// The shell owns the clock and the simulation. This owns none of it: every button raises a
    /// callback and every value is pushed in by <see cref="Refresh"/>, so the interface cannot show
    /// a number the simulation does not agree with.
    /// </summary>
    public sealed class GameHud
    {
        /// <summary>What the three speed buttons mean. Paused is the round button beside them.</summary>
        private static readonly (string Label, SimSpeed Speed)[] Speeds =
        {
            ("X1", SimSpeed.Slow),
            ("X2", SimSpeed.Normal),
            ("X3", SimSpeed.Fast)
        };

        private readonly Action<SimSpeed> onSpeed;
        private readonly Action onSkipDay;

        private readonly List<Button> speedButtons = new();
        private readonly List<Button> slots = new();

        private HudTimeDial dial;
        private Label dateLabel;
        private Label clockLabel;
        private Button pauseButton;
        private VisualElement dayFill;

        public GameHud(Action<SimSpeed> onSpeed, Action onSkipDay)
        {
            this.onSpeed = onSpeed;
            this.onSkipDay = onSkipDay;
            Root = Build();
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Adds a category slot. Built as an empty plate with a label under it, because the icons
        /// are not decided yet and a slot that already has its own element is one edit away from
        /// carrying one.
        /// </summary>
        public void AddSlot(string label, object key, Action onClick, string iconName = null)
        {
            var slot = new Button(onClick) { userData = key };
            slot.AddToClassList("hud-slot");

            var icon = new VisualElement();
            icon.AddToClassList("hud-slot__icon");

            // A missing file leaves the plate as it was rather than throwing, so a category is still
            // reachable while its icon is being drawn.
            var texture = string.IsNullOrEmpty(iconName) ? null : PageArt.Icon(iconName);
            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
                icon.AddToClassList("hud-slot__icon--art");
            }

            slot.Add(icon);

            var caption = new Label(label);
            caption.AddToClassList("hud-slot__label");
            slot.Add(caption);

            var underline = new VisualElement();
            underline.AddToClassList("hud-slot__underline");
            slot.Add(underline);

            slots.Add(slot);
            SlotHost.Add(slot);

            // The underline is the selected marker, and it is a piece of the same gradient the
            // bottom line uses, positioned by where the slot sits across the row.
            for (var index = 0; index < slots.Count; index++)
            {
                var at = 0.32f + 0.62f * (slots.Count == 1 ? 0.5f : index / (float)(slots.Count - 1));
                HudAccent.PaintSlice(slots[index].Q(className: "hud-slot__underline"), at, at + 0.06f);
            }
        }

        private VisualElement SlotHost { get; set; }

        public void SetActiveSlot(object key)
        {
            foreach (var slot in slots)
            {
                slot.EnableInClassList("hud-slot--on", Equals(slot.userData, key));
            }
        }

        /// <summary>Pushes the clock into the interface. Called every tick and after every action.</summary>
        public void Refresh(GameDate date, SimSpeed speed, double dayProgress)
        {
            dateLabel.text = date.ToString().ToUpperInvariant();
            clockLabel.text = HudTimeDial.ClockText(dayProgress);
            dial.Progress = (float)dayProgress;

            dayFill.style.width = Length.Percent((float)(dayProgress * 100.0));
            pauseButton.EnableInClassList("hud-speed--on", speed == SimSpeed.Paused);

            for (var index = 0; index < speedButtons.Count; index++)
            {
                speedButtons[index].EnableInClassList("hud-speed--on", Speeds[index].Speed == speed);
            }
        }

        private VisualElement Build()
        {
            var hud = new VisualElement();
            hud.AddToClassList("hud");

            var bar = new VisualElement();
            hud.AddToClassList("hud");
            bar.AddToClassList("hud__bar");
            hud.Add(bar);

            bar.Add(BuildTimeModule());

            SlotHost = new VisualElement();
            SlotHost.AddToClassList("hud__slots");
            bar.Add(SlotHost);

            // One line, edge to edge, carrying the whole gradient. It is the signature of the
            // interface and the reason every other accent here is a slice of the same three colours.
            var day = new VisualElement();
            day.AddToClassList("hud__day");

            dayFill = new VisualElement();
            dayFill.AddToClassList("hud__day-fill");
            HudAccent.PaintSlice(dayFill, 0f, 1f);
            day.Add(dayFill);

            hud.Add(day);
            return hud;
        }

        private VisualElement BuildTimeModule()
        {
            var module = new VisualElement();
            module.AddToClassList("hud-time");

            // The disc overhangs the top of the bar, so it lives in a zero height host and is
            // positioned out of flow. Nothing else in the bar has to make room for it.
            var overhang = new VisualElement();
            overhang.AddToClassList("hud-time__overhang");

            dial = new HudTimeDial();

            dateLabel = new Label("2022-01-01");
            dateLabel.AddToClassList("hud-time__date");
            dial.Add(dateLabel);

            clockLabel = new Label("00:00");
            clockLabel.AddToClassList("hud-time__clock");
            dial.Add(clockLabel);

            overhang.Add(dial);
            module.Add(overhang);

            var controls = new VisualElement();
            controls.AddToClassList("hud-time__controls");

            pauseButton = new Button(() => onSpeed?.Invoke(SimSpeed.Paused)) { text = "II" };
            pauseButton.AddToClassList("hud-speed");
            pauseButton.AddToClassList("hud-speed--pause");
            controls.Add(pauseButton);

            foreach (var (label, speed) in Speeds)
            {
                var button = new Button(() => onSpeed?.Invoke(speed)) { text = label };
                button.AddToClassList("hud-speed");
                speedButtons.Add(button);
                controls.Add(button);
            }

            var skip = new Button(() => onSkipDay?.Invoke()) { text = "SKIP DAY" };
            skip.AddToClassList("hud-skip");
            controls.Add(skip);

            module.Add(controls);
            return module;
        }
    }
}
