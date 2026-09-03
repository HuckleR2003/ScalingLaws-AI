using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
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

        /// <summary>
        /// What each speed is for, in the order the buttons are built. Phrase-book keys, not
        /// sentences, so the card resolves in whatever language is current when the cursor arrives.
        /// </summary>
        private static readonly string[] SpeedWords =
        {
            "hud.speed_x1_note",
            "hud.speed_x2_note",
            "hud.speed_x3_note"
        };

        private readonly Action<SimSpeed> onSpeed;
        private readonly Action onSkipDay;
        private readonly Action onCompanyInfo;

        private readonly List<Button> speedButtons = new();
        private readonly List<Button> slots = new();

        private HudTimeDial dial;
        private VisualElement overhang;
        private VisualElement plate;
        private Label dateLabel;
        private Label clockLabel;
        private Label plateDateLabel;
        private Label plateClockLabel;
        private Button pauseButton;
        private Button skipButton;
        private Button infoButton;
        private VisualElement dayFill;

        public GameHud(Action<SimSpeed> onSpeed, Action onSkipDay, Action onCompanyInfo = null)
        {
            this.onSpeed = onSpeed;
            this.onSkipDay = onSkipDay;
            this.onCompanyInfo = onCompanyInfo;
            Root = Build();
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Adds a category slot. Built as an empty plate with a label under it, because the icons
        /// are not decided yet and a slot that already has its own element is one edit away from
        /// carrying one.
        /// </summary>
        private readonly Dictionary<object, Label> badges = new();

        /// <summary>
        /// Puts a count on a slot, or clears it at zero.
        ///
        /// Zero is drawn as nothing rather than as "0", because a badge reading zero is a permanent
        /// small distraction that says there is nothing to look at.
        /// </summary>
        public void SetBadge(object key, int count)
        {
            if (!badges.TryGetValue(key, out var badge))
            {
                return;
            }

            badge.text = count > 99 ? "99+" : count.ToString();
            badge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Every slot caption, against the phrase-book key it came from.
        ///
        /// **The label used to be set once in <see cref="AddSlot"/> and never looked at again.** The
        /// keys were translated, so a Polish proof frame still showed SITE, MODEL, RESEARCH along
        /// the bottom of a Polish game. It was not reachable then, because the language could only
        /// be changed from the main menu, and it would have become real the day that setting reached
        /// the pause menu. This project has now been bitten nine times by something set once in a
        /// `Build` and never re-set.
        /// </summary>
        private readonly List<(Label Caption, string Key)> captions = new();

        public void AddSlot(string labelKey, object key, Action onClick, string iconName = null,
            string insightKey = null)
        {
            var label = Loc.T(labelKey);

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
            captions.Add((caption, labelKey));

            var underline = new VisualElement();
            underline.AddToClassList("hud-slot__underline");
            slot.Add(underline);

            // A badge for the slots that carry a count. Built for every slot and shown for none of
            // them until something asks, so adding a counter later does not mean touching this again.
            var badge = new Label();
            badge.AddToClassList("hud-slot__badge");
            badge.style.display = DisplayStyle.None;
            slot.Add(badge);
            badges[key] = badge;

            // What the tab is for, above the bar, on the line where the screen meets it. Fourteen
            // nouns along the bottom of a window is a lot to learn from nouns alone.
            //
            // Keyed rather than resolved, so the card cannot be left behind by a language change
            // the way the caption above it was.
            InsightTip.AttachKeyed(slot, labelKey, insightKey);

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

        /// <summary>
        /// The tab for a category, or null when there is no such slot.
        ///
        /// Slots already carry their key in `userData`, so this needs no second table. The tutorial
        /// uses it to ring the tab it is asking for, which it could not do before: the only class
        /// available was `hud-slot`, which is all fifteen of them.
        /// </summary>
        public VisualElement SlotFor(object key)
        {
            foreach (var slot in slots)
            {
                if (Equals(slot.userData, key))
                {
                    return slot;
                }
            }

            return null;
        }

        /// <summary>
        /// Turns every tab off except one, or turns them all back on when given null.
        ///
        /// **For the tutorial and nothing else.** A player who wanders to another screen in the
        /// middle of a step comes back to a conversation that has moved on without them, and the
        /// tour reads as broken. Closing the other doors for the two seconds it takes to press the
        /// right one removes the whole failure.
        /// </summary>
        public void LockToSlot(object key)
        {
            foreach (var slot in slots)
            {
                var open = key == null || Equals(slot.userData, key);

                slot.SetEnabled(open);
                slot.EnableInClassList("hud-slot--shut", !open);
            }
        }

        private VisualElement SlotHost { get; set; }

        /// <summary>Lights the info button while the panels it opens are on screen.</summary>
        public void SetCompanyInfoOpen(bool open)
        {
            infoButton?.EnableInClassList("hud-info--on", open);
        }

        public void SetActiveSlot(object key)
        {
            foreach (var slot in slots)
            {
                slot.EnableInClassList("hud-slot--on", Equals(slot.userData, key));
            }
        }

        /// <summary>
        /// Re-reads every word on the bar from the phrase book.
        ///
        /// Nothing is rebuilt: the same `Label` and the same `Button` instances get new text, so
        /// nothing the player's cursor is resting on leaves the tree. That distinction is not
        /// theoretical here. The tutorial's strip was rebuilt on every refresh and the click landed
        /// on an element that had already been destroyed, which read as four separate bugs.
        ///
        /// The insight cards need no pass of their own: they hold keys and resolve on hover.
        /// </summary>
        public void Retext()
        {
            foreach (var (caption, key) in captions)
            {
                caption.text = Loc.T(key);
            }

            if (skipButton != null)
            {
                skipButton.text = Loc.T("hud.skip_day");
            }

            if (infoButton != null)
            {
                infoButton.text = Loc.T("hud.company_info");
            }
        }

        /// <summary>
        /// Whether this screen is a room the player is standing in, or a page they are reading.
        ///
        /// **The disc overhangs the bar by about 170px and no page reserves for it**, so on every
        /// document screen it sat over the bottom-left corner and ate whatever was written there:
        /// the brand line on TEAM, the end of the marketing sentence on BUSINESS, the cabinet hint
        /// in the basement until that one was moved out of its way.
        ///
        /// The fix is not a global margin. A room fills the window and has nothing in that corner
        /// to lose, and the disc is the nicer object, so the disc stays there and a document gets
        /// a rectangular 24-hour plate in the bar instead. Same reading, in the flow of the row,
        /// taking its own space rather than borrowing the page's.
        ///
        /// Both are built once and toggled. Rebuilding the dial per screen change would restart
        /// its arc, and it is the one element here that draws itself.
        /// </summary>
        public void ShowDial(bool inARoom)
        {
            overhang.style.display = inARoom ? DisplayStyle.Flex : DisplayStyle.None;
            plate.style.display = inARoom ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Pushes the clock into the interface. Called every tick and after every action.</summary>
        public void Refresh(GameDate date, SimSpeed speed, double dayProgress)
        {
            // Both readings are set whichever is on screen. Two labels are two strings; branching
            // on the visible one is how the hidden half drifts and then appears already wrong.
            var stamp = date.ToString().ToUpperInvariant();
            var reading = HudTimeDial.ClockText(dayProgress);

            dateLabel.text = stamp;
            clockLabel.text = reading;
            plateDateLabel.text = stamp;
            plateClockLabel.text = reading;

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

            // The state the shell will immediately correct on its first `Show`. Set here so the
            // bar is never briefly carrying both readings, or neither.
            ShowDial(true);
            return hud;
        }

        private VisualElement BuildTimeModule()
        {
            var module = new VisualElement();
            module.AddToClassList("hud-time");

            // The disc overhangs the top of the bar, so it lives in a zero height host and is
            // positioned out of flow. Nothing else in the bar has to make room for it.
            overhang = new VisualElement();
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

            // The reading a document screen gets instead of the disc. In the flow of the row, the
            // same height as the speed buttons, so it takes bar space rather than page space.
            plate = new VisualElement();
            plate.AddToClassList("hud-clock");

            plateDateLabel = new Label("2022-01-01");
            plateDateLabel.AddToClassList("hud-clock__date");
            plate.Add(plateDateLabel);

            var divider = new VisualElement();
            divider.AddToClassList("hud-clock__rule");
            plate.Add(divider);

            plateClockLabel = new Label("00:00");
            plateClockLabel.AddToClassList("hud-clock__time");
            plate.Add(plateClockLabel);

            InsightTip.Attach(plate, Loc.T("hud.clock"), Loc.T("hud.clock_note"));
            controls.Add(plate);

            pauseButton = new Button(() => onSpeed?.Invoke(SimSpeed.Paused)) { text = "II" };
            pauseButton.AddToClassList("hud-speed");
            pauseButton.AddToClassList("hud-speed--pause");
            InsightTip.AttachKeyed(pauseButton, "hud.pause", "hud.pause_note");
            controls.Add(pauseButton);

            // The key is named on the card rather than printed on the button, because the buttons
            // are 42px wide and the shortcut is worth a sentence rather than a superscript.
            for (var index = 0; index < Speeds.Length; index++)
            {
                var (label, speed) = Speeds[index];

                var button = new Button(() => onSpeed?.Invoke(speed)) { text = label };
                button.AddToClassList("hud-speed");

                // The label is the key: X1, X2 and X3 are the same word in every language, and the
                // sentence under them is not. `SpeedWords` holds the key rather than the sentence.
                InsightTip.AttachKeyed(button, "hud.speed_" + label.ToLowerInvariant(),
                    SpeedWords[index]);

                speedButtons.Add(button);
                controls.Add(button);
            }

            skipButton = new Button(() => onSkipDay?.Invoke()) { text = Loc.T("hud.skip_day") };
            skipButton.AddToClassList("hud-skip");
            InsightTip.AttachKeyed(skipButton, "hud.skip_day", "hud.skip_day_note");
            controls.Add(skipButton);

            infoButton = new Button(() => onCompanyInfo?.Invoke()) { text = Loc.T("hud.company_info") };
            infoButton.AddToClassList("hud-skip");
            infoButton.AddToClassList("hud-info");
            InsightTip.AttachKeyed(infoButton, "hud.company_info", "hud.company_info_note");
            controls.Add(infoButton);

            module.Add(controls);
            return module;
        }
    }
}
