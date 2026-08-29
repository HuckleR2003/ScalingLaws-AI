using System;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>Which page of the pause menu is open.</summary>
    public enum PauseTab
    {
        Menu = 0,
        Save = 1,
        Load = 2,
        Settings = 3,
        Stats = 4
    }

    /// <summary>
    /// What Escape opens.
    ///
    /// **The game had no Escape at all.** `KeyboardShortcuts` bound space and the three speeds and
    /// nothing else, so there was no pause menu, no way to save deliberately, no way to load
    /// without going back to the main menu, and no settings once a campaign had started.
    ///
    /// It pauses on open and restores the speed the player was on when it closes, rather than
    /// resuming at normal. Somebody who was running at x3 and looked something up did not ask to
    /// be slowed down.
    ///
    /// Nothing here computes. Saving goes through `SaveStore`, settings through `GameSettings`, and
    /// the statistics are read off the company rather than kept beside it.
    /// </summary>
    public sealed class PauseMenu
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        private PauseTab tab = PauseTab.Menu;

        /// <summary>A slot armed for overwriting, so a full slot takes two clicks.</summary>
        private int armed = -1;

        private string note = string.Empty;

        public PauseMenu(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        /// <summary>True while the menu is up. The shell holds the clock while it is.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Closes the menu and hands the game back. Set by the shell.</summary>
        public Action Closed { get; set; }

        /// <summary>Leaves for the main menu. Set by the shell so this file knows nothing of scenes.</summary>
        public Action Quit { get; set; }

        /// <summary>Opens the feedback form. Set by the shell.</summary>
        public Action Feedback { get; set; }

        public void Open()
        {
            IsOpen = true;
            tab = PauseTab.Menu;
            armed = -1;
            note = string.Empty;
        }

        public void Close()
        {
            IsOpen = false;
            armed = -1;
            note = string.Empty;
        }

        /// <summary>
        /// Opens a page directly.
        ///
        /// For the proof render, the way `ServerRoomScreen.PickFor` and `GameShell.OpenScreenByName`
        /// are: a test has no panel, so a click sent to a row is never dispatched, and the pages
        /// worth photographing are the ones behind a click.
        /// </summary>
        public void OpenTab(PauseTab page)
        {
            tab = page;
            armed = -1;
            note = string.Empty;
        }

        /// <summary>Escape opens it, and closes it again from any page.</summary>
        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
                Closed?.Invoke();
            }
            else
            {
                Open();
            }
        }

        public VisualElement Build()
        {
            var sheet = new VisualElement();
            sheet.AddToClassList("pause");

            var card = new VisualElement();
            card.AddToClassList("pause__card");

            card.Add(BuildHead());

            switch (tab)
            {
                case PauseTab.Save:
                    card.Add(BuildSlots(saving: true));
                    break;
                case PauseTab.Load:
                    card.Add(BuildSlots(saving: false));
                    break;
                case PauseTab.Settings:
                    card.Add(BuildSettings());
                    break;
                case PauseTab.Stats:
                    card.Add(BuildStats());
                    break;
                default:
                    card.Add(BuildMenu());
                    break;
            }

            if (!string.IsNullOrEmpty(note))
            {
                var said = new Label(note);
                said.AddToClassList("pause__note");
                card.Add(said);
            }

            sheet.Add(card);
            return sheet;
        }

        private VisualElement BuildHead()
        {
            var head = new VisualElement();
            head.AddToClassList("pause__head");

            var title = new Label(Loc.T(tab switch
            {
                PauseTab.Save => "pause.save",
                PauseTab.Load => "pause.load",
                PauseTab.Settings => "pause.settings",
                PauseTab.Stats => "pause.stats",
                _ => "pause.title"
            }));

            title.AddToClassList("pause__title");
            head.Add(title);

            var back = new Button(() =>
            {
                if (tab == PauseTab.Menu)
                {
                    Close();
                    Closed?.Invoke();
                }
                else
                {
                    tab = PauseTab.Menu;
                    armed = -1;
                    note = string.Empty;
                }

                changed?.Invoke();
            })
            { text = Loc.T(tab == PauseTab.Menu ? "pause.resume" : "common.back") };

            back.AddToClassList("chip");
            head.Add(back);

            return head;
        }

        // ---- the menu itself ------------------------------------------------------------------------

        private VisualElement BuildMenu()
        {
            var block = new VisualElement();
            block.AddToClassList("pause__menu");

            block.Add(Row("pause.resume", () =>
            {
                Close();
                Closed?.Invoke();
                changed?.Invoke();
            }));

            block.Add(Row("pause.save", () => Go(PauseTab.Save)));
            block.Add(Row("pause.load", () => Go(PauseTab.Load)));
            block.Add(Row("pause.settings", () => Go(PauseTab.Settings)));
            block.Add(Row("pause.stats", () => Go(PauseTab.Stats)));

            // **The feedback button is deliberately not one of these rows.** It is the one thing on
            // this menu that is a favour rather than a function, and a row identical to the other
            // five is a row nobody reads.
            block.Add(BuildFeedbackButton());

            block.Add(Row("pause.quit", () => Quit?.Invoke()));

            return block;
        }

        private VisualElement BuildFeedbackButton()
        {
            var button = new Button(() => Feedback?.Invoke())
            {
                text = Loc.T("feedback.button")
            };

            button.AddToClassList("feedbutton");
            return button;
        }

        private Button Row(string key, Action clicked)
        {
            var row = new Button(clicked) { text = Loc.T(key) };
            row.AddToClassList("pause__row");
            return row;
        }

        private void Go(PauseTab next)
        {
            tab = next;
            armed = -1;
            note = string.Empty;
            changed?.Invoke();
        }

        // ---- slots ----------------------------------------------------------------------------------

        /// <summary>
        /// The four slots, for saving into or loading out of.
        ///
        /// **One list, two verbs.** The slots are the same four things whichever way the player is
        /// going, and two screens that draw the same four rows differently is how a load screen
        /// ends up able to show a slot the save screen cannot.
        /// </summary>
        private VisualElement BuildSlots(bool saving)
        {
            var block = new VisualElement();
            block.AddToClassList("pause__slots");

            for (var slot = 1; slot <= SaveStore.SlotCount; slot++)
            {
                block.Add(BuildSlot(slot, saving));
            }

            return block;
        }

        private VisualElement BuildSlot(int slot, bool saving)
        {
            var summary = SaveStore.SummaryOf(slot);
            var occupied = summary != null;

            var card = new VisualElement();
            card.AddToClassList("slotcard");
            card.EnableInClassList("slotcard--empty", !occupied);

            var head = new VisualElement();
            head.AddToClassList("slotcard__head");

            var number = new Label(Loc.T("pause.slot", slot));
            number.AddToClassList("slotcard__number");
            head.Add(number);

            var name = new Label(occupied ? summary.companyName : Loc.T("pause.slot.empty"));
            name.AddToClassList("slotcard__name");
            head.Add(name);

            card.Add(head);

            if (occupied)
            {
                var line = new Label(
                    $"{summary.dateText}   ·   {Loc.Counted(summary.day, "noun.day")}"
                    + $"   ·   {UiFormat.Money(summary.cashUsd)}");

                line.AddToClassList("slotcard__line");
                card.Add(line);
            }

            var action = new Button(() => Act(slot, saving, occupied))
            {
                text = Loc.T(saving
                    ? occupied && armed != slot ? "pause.overwrite" : "pause.save.here"
                    : "pause.load.this")
            };

            action.AddToClassList("button");
            action.AddToClassList(armed == slot ? "button--armed" : "button--primary");
            action.SetEnabled(saving || occupied);

            card.Add(action);
            return card;
        }

        /// <summary>
        /// Saving over a campaign takes two clicks, because nothing comes back.
        ///
        /// The same two-click shape research and the training cancel already use. An empty slot
        /// takes one, because there is nothing there to regret.
        /// </summary>
        private void Act(int slot, bool saving, bool occupied)
        {
            if (!saving)
            {
                SaveStore.CurrentSlot = slot;
                Quit?.Invoke();
                return;
            }

            if (occupied && armed != slot)
            {
                armed = slot;
                note = Loc.T("pause.overwrite.warn");
                changed?.Invoke();
                return;
            }

            SaveStore.SaveTo(slot, company().State);
            SaveStore.CurrentSlot = slot;

            armed = -1;
            note = Loc.T("pause.saved", slot);
            changed?.Invoke();
        }

        // ---- settings -------------------------------------------------------------------------------

        private VisualElement BuildSettings()
        {
            var block = new VisualElement();
            block.AddToClassList("pause__settings");

            var autosave = new VisualElement();
            autosave.AddToClassList("pause__setting");

            var label = new Label(Loc.T("pause.autosave"));
            label.AddToClassList("pause__label");
            autosave.Add(label);

            var choices = new VisualElement();
            choices.AddToClassList("pause__choices");

            foreach (var minutes in GameSettings.AutosaveChoices)
            {
                var pick = minutes;

                var chip = new Button(() =>
                {
                    GameSettings.SetAutosaveMinutes(pick);
                    changed?.Invoke();
                })
                {
                    text = pick == 0
                        ? Loc.T("pause.autosave.off")
                        : Loc.T("pause.autosave.every", pick)
                };

                chip.AddToClassList("pause__chip");
                chip.EnableInClassList("pause__chip--on", GameSettings.AutosaveMinutes == pick);
                choices.Add(chip);
            }

            autosave.Add(choices);
            block.Add(autosave);

            var hint = new Label(Loc.T("pause.autosave.note"));
            hint.AddToClassList("pause__hint");
            block.Add(hint);

            block.Add(Toggle("pause.reduce_motion", GameSettings.ReduceMotion,
                value => GameSettings.SetReduceMotion(value)));

            block.Add(Toggle("pause.fullscreen", GameSettings.Fullscreen,
                value => GameSettings.SetFullscreen(value)));

            return block;
        }

        private VisualElement Toggle(string key, bool on, Action<bool> set)
        {
            var row = new VisualElement();
            row.AddToClassList("pause__setting");

            var label = new Label(Loc.T(key));
            label.AddToClassList("pause__label");
            row.Add(label);

            var button = new Button(() =>
            {
                set(!on);
                changed?.Invoke();
            })
            { text = Loc.T(on ? "common.on" : "common.off") };

            button.AddToClassList("pause__chip");
            button.EnableInClassList("pause__chip--on", on);
            row.Add(button);

            return row;
        }

        // ---- statistics -----------------------------------------------------------------------------

        /// <summary>
        /// What this campaign has done, read off the company rather than counted beside it.
        ///
        /// Every figure here already exists somewhere the simulation maintains. A statistics screen
        /// that kept its own tallies would be a second set of numbers to disagree with the first.
        /// </summary>
        private VisualElement BuildStats()
        {
            var state = company().State;

            var block = new VisualElement();
            block.AddToClassList("pause__stats");

            block.Add(UiParts.StatLine(Loc.T("stat.survived"), UiFormat.Days(state.Date.DayIndex)));
            block.Add(UiParts.StatLine(Loc.T("stat.cash"), UiFormat.Money(state.CashUsd)));
            block.Add(UiParts.StatLine(Loc.T("stat.models"), state.ReleasedModelCount.ToString()));

            block.Add(UiParts.StatLine(Loc.T("stat.best"),
                UiFormat.Number(state.BestCapability)));

            block.Add(UiParts.StatLine(Loc.T("stat.revenue"),
                UiFormat.Money(state.LifetimeRevenueUsd)));

            block.Add(UiParts.StatLine(Loc.T("stat.operating"),
                UiFormat.Money(state.LifetimeOperatingCostUsd)));

            block.Add(UiParts.StatLine(Loc.T("stat.hardware"),
                UiFormat.Money(state.LifetimeCapitalSpentUsd)));

            block.Add(UiParts.StatLine(Loc.T("stat.fans"), UiFormat.Count(state.Fans)));

            block.Add(UiParts.StatLine(Loc.T("stat.reputation"),
                UiFormat.Percent(state.Reputation)));

            return block;
        }
    }
}
