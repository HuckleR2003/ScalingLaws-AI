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

        private VisualElement strip;

        /// <summary>What was drawn last, so the strip is not rebuilt on every frame.</summary>
        private string shownTask;
        private int shownDone = -1;

        public TaskBanner(VisualElement host, Func<CompanyState> state,
            Func<GuideProgress> progress, Action changed)
        {
            this.host = host;
            this.state = state;
            this.progress = progress;
            this.changed = changed;
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

            if (strip != null && shownTask == current && shownDone == done)
            {
                return;
            }

            shownTask = current;
            shownDone = done;

            Build(company, guide, current, done);
        }

        private void Build(CompanyState company, GuideProgress guide, string current, int done)
        {
            strip?.RemoveFromHierarchy();

            strip = new VisualElement();
            strip.AddToClassList("taskbar");

            var head = new VisualElement();
            head.AddToClassList("taskbar__head");

            var kicker = new Label($"ZADANIE  {done + 1}/{Data.GuideScript.Tasks.Count}");
            kicker.AddToClassList("taskbar__kicker");
            head.Add(kicker);

            var close = new Button(() =>
            {
                guide.BannerDismissed = true;
                Hide();
                changed?.Invoke();
            })
            { text = "x" };

            close.AddToClassList("taskbar__close");
            head.Add(close);

            strip.Add(head);

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

            // Born small and released a frame later, which is what makes it read as the phone
            // having just finished rolling up into it.
            strip.AddToClassList("taskbar--arriving");
            strip.schedule.Execute(() => strip.RemoveFromClassList("taskbar--arriving"))
                .ExecuteLater(16);
        }

        public void Hide()
        {
            strip?.RemoveFromHierarchy();
            strip = null;
            shownTask = null;
            shownDone = -1;
        }
    }
}
