using System;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The little strip the phone rolls up into: what to do next, with a box to tick.
    ///
    /// **It sits below the corner banners rather than beside them.** The product, research, hiring
    /// and upgrade strips already own the top right, and a fifth thing competing for that space
    /// would push one of them off screen. This starts under all of them and it is deliberately the
    /// quietest thing there: seventy-five per cent transparent body, fully opaque text, no colour
    /// of its own. It is a reminder, not an alert.
    ///
    /// Only the current task and the ones already done are shown. A list of three things a new
    /// player has not done yet is a chore list; one line saying what is next is a nudge.
    /// </summary>
    public sealed class TaskBanner
    {
        private readonly VisualElement host;
        private readonly Func<CompanyState> state;
        private readonly Func<GuideProgress> progress;
        private readonly Action changed;

        /// <summary>
        /// Whether the player is standing in the headquarters.
        ///
        /// **The tasks are all things you do at home.** Start the first research, release the first
        /// model: every one of them is reached from the site, so on any other screen the strip is a
        /// list of instructions for somewhere the player is not. It rolls up into its own counter
        /// there and unrolls again when they come back.
        /// </summary>
        private readonly Func<bool> atHeadquarters;

        private VisualElement strip;

        /// <summary>What was drawn last, so the strip is not rebuilt on every frame.</summary>
        private string shownTask;
        private int shownDone = -1;
        private bool shownRolledUp;

        /// <summary>
        /// Whether the player has opened the counter back up on a screen away from home.
        ///
        /// Forgotten on the way back, so the next screen they leave for rolls it up again. A strip
        /// that stayed open because of one click twenty minutes ago is a strip that has stopped
        /// meaning anything by being on screen.
        /// </summary>
        private bool openedByHand;

        public TaskBanner(VisualElement host, Func<CompanyState> state,
            Func<GuideProgress> progress, Action changed, Func<bool> atHeadquarters = null)
        {
            this.host = host;
            this.state = state;
            this.progress = progress;
            this.changed = changed;
            this.atHeadquarters = atHeadquarters;
        }

        /// <summary>
        /// Draws the strip, or takes it away.
        ///
        /// Cheap to call every frame: it works out what it would draw, compares that to what is
        /// already there, and returns without touching the tree when nothing has changed.
        /// </summary>
        public void Refresh()
        {
            var company = state();
            var guide = progress();

            if (company == null || guide == null || guide.BannerDismissed
                || guide.Stage == GuideStage.Unseen || guide.Stage == GuideStage.Talking)
            {
                Hide();
                return;
            }

            var current = guide.CurrentTask(company);

            if (current == null)
            {
                // Everything done. The strip has said all it has to say.
                Hide();
                return;
            }

            var done = 0;

            foreach (var (_, _, complete) in guide.Tasks(company))
            {
                if (complete)
                {
                    done++;
                }
            }

            var home = atHeadquarters == null || atHeadquarters();

            if (home)
            {
                openedByHand = false;
            }

            var rolledUp = !home && !openedByHand;

            if (strip != null && shownTask == current && shownDone == done
                && shownRolledUp == rolledUp)
            {
                return;
            }

            shownTask = current;
            shownDone = done;
            shownRolledUp = rolledUp;

            Build(company, guide, current, done, rolledUp);
        }

        /// <summary>
        /// Opens the counter back up, or rolls it down again.
        ///
        /// Goes through <see cref="Refresh"/> rather than rebuilding here, so the strip is drawn on
        /// the one path that draws it and there is no second copy of the decision.
        ///
        /// Public for the same reason <c>PauseMenu.OpenTab</c> is: a test has no panel, so a click
        /// sent to an element is never dispatched, and the behaviour worth guarding is on the far
        /// side of that click.
        /// </summary>
        public void Toggle()
        {
            openedByHand = !openedByHand;
            Refresh();
        }

        private void Build(CompanyState company, GuideProgress guide, string current, int done,
            bool rolledUp)
        {
            strip?.RemoveFromHierarchy();

            strip = new VisualElement();
            strip.AddToClassList("taskbar");
            strip.EnableInClassList("taskbar--rolled", rolledUp);

            var head = new VisualElement();
            head.AddToClassList("taskbar__head");

            var kicker = new Label($"{Data.Loc.T("guide.task")}  {done + 1}/{Data.GuideScript.Tasks.Count}");
            kicker.AddToClassList("taskbar__kicker");
            head.Add(kicker);

            if (rolledUp)
            {
                // **The whole pill is the button and there is nothing else on it.** A counter with a
                // dismiss cross beside it offers two things at a size where they are one thing, and
                // the cross is the one that cannot be undone. Opening it first costs a click and
                // puts the cross back at full size next to the tasks it would be throwing away.
                strip.AddToClassList("taskbar--clickable");
                strip.RegisterCallback<ClickEvent>(_ => Toggle());
            }
            else
            {
                var close = new Button(() =>
                {
                    guide.BannerDismissed = true;
                    Hide();
                    changed?.Invoke();
                })
                { text = "x" };

                close.AddToClassList("taskbar__close");
                head.Add(close);
            }

            strip.Add(head);

            if (rolledUp)
            {
                host.Add(strip);
                Arrive();
                return;
            }

            // Rolled down away from home, so the counter can be put back. At home it never rolls up
            // and a control that only ever does nothing is worse than no control.
            if (atHeadquarters != null && !atHeadquarters())
            {
                kicker.AddToClassList("taskbar__kicker--clickable");
                kicker.RegisterCallback<ClickEvent>(_ => Toggle());
            }

            foreach (var (id, text, complete) in guide.Tasks(company))
            {
                // The ones still ahead are not shown. A new player does not need to be told about
                // doubling the budget before they have trained anything.
                if (!complete && id != current)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("taskbar__row");
                row.EnableInClassList("taskbar__row--done", complete);

                var box = new VisualElement();
                box.AddToClassList("taskbar__box");
                box.EnableInClassList("taskbar__box--ticked", complete);

                if (complete)
                {
                    var tick = new Label("✓");
                    tick.AddToClassList("taskbar__tick");
                    box.Add(tick);
                }

                row.Add(box);

                var label = new Label(text);
                label.AddToClassList("taskbar__text");
                row.Add(label);

                strip.Add(row);
            }

            host.Add(strip);
            Arrive();
        }

        /// <summary>
        /// Born small and released a frame later, which is what makes it read as the phone having
        /// just finished rolling up into it.
        /// </summary>
        private void Arrive()
        {
            var arriving = strip;
            arriving.AddToClassList("taskbar--arriving");
            arriving.schedule.Execute(() => arriving.RemoveFromClassList("taskbar--arriving"))
                .ExecuteLater(16);
        }

        public void Hide()
        {
            strip?.RemoveFromHierarchy();
            strip = null;
            shownTask = null;
            shownDone = -1;
            shownRolledUp = false;
        }
    }
}
