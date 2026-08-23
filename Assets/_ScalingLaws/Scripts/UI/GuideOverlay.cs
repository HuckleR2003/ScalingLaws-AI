using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Emil walking the player round: his face, his name, and what he is saying at the bottom.
    ///
    /// **It sits over the game rather than replacing it.** The whole point of a guide like this is
    /// that the player is looking at the real screens while somebody talks over them — a tutorial
    /// that shows its own mock-ups teaches the mock-ups. So this is a strip along the bottom, the
    /// screens behind it are live and clickable, and the step that says "open COMPUTE" waits until
    /// COMPUTE is actually open.
    ///
    /// The highlight works by class rather than by element: a step names a USS class and everything
    /// wearing it gets a ring. That way a screen can be rebuilt out of different elements and the
    /// tutorial still points at the right thing.
    /// </summary>
    public sealed class GuideOverlay
    {
        /// <summary>Milliseconds between the strip arriving and the text being readable.</summary>
        public const int ArriveMilliseconds = 16;

        private readonly VisualElement host;
        private readonly Func<GuideProgress> progress;
        private readonly Action<GuideTarget> goTo;
        private readonly Action changed;

        private VisualElement strip;
        private Label line;
        private Button next;
        private VisualElement portrait;

        public GuideOverlay(VisualElement host, Func<GuideProgress> progress,
            Action<GuideTarget> goTo, Action changed)
        {
            this.host = host;
            this.progress = progress;
            this.goTo = goTo;
            this.changed = changed;
        }

        public bool IsShowing => strip != null && strip.parent != null;

        /// <summary>The step being shown, or null when the tour is over.</summary>
        public GuideStep Current
        {
            get
            {
                var state = progress();

                return state.Step >= 0 && state.Step < GuideScript.Steps.Count
                    ? GuideScript.Steps[state.Step]
                    : null;
            }
        }

        /// <summary>
        /// Draws the strip for whatever step the player is on.
        ///
        /// Rebuilt rather than updated, because the strip is four elements and a rebuild cannot
        /// leave a stale highlight behind — which is exactly the bug a partial update would have.
        /// </summary>
        public void Refresh()
        {
            var state = progress();

            if (state.Stage != GuideStage.Touring)
            {
                Hide();
                return;
            }

            var step = Current;

            if (step == null)
            {
                state.Stage = GuideStage.Finished;
                Hide();
                changed?.Invoke();
                return;
            }

            Build(step);
        }

        private void Build(GuideStep step)
        {
            strip?.RemoveFromHierarchy();

            strip = new VisualElement();
            strip.AddToClassList("guide");

            // Ignores the mouse everywhere except its own buttons, so the player can keep clicking
            // the screens underneath while he talks.
            strip.pickingMode = PickingMode.Ignore;

            var speaker = new VisualElement();
            speaker.AddToClassList("guide__speaker");
            speaker.pickingMode = PickingMode.Ignore;

            portrait = PhonePanel.Avatar(96);
            portrait.AddToClassList("guide__portrait");
            speaker.Add(portrait);

            var names = new VisualElement();
            names.AddToClassList("guide__names");
            names.pickingMode = PickingMode.Ignore;

            var name = new Label(GuideScript.CousinName);
            name.AddToClassList("guide__name");
            names.Add(name);

            var relation = new Label($"({GuideScript.CousinRelation})");
            relation.AddToClassList("guide__relation");
            names.Add(relation);

            speaker.Add(names);
            strip.Add(speaker);

            var bar = new VisualElement();
            bar.AddToClassList("guide__bar");

            line = new Label(step.Line);
            line.AddToClassList("guide__line");
            bar.Add(line);

            var buttons = new VisualElement();
            buttons.AddToClassList("guide__buttons");

            if (step.WaitForClick)
            {
                // A step that waits gets a button that takes you there rather than one that says
                // Next. Being told to press something and then being handed a Next is the single
                // most common way a tutorial teaches nothing.
                next = new Button(() =>
                {
                    goTo?.Invoke(step.Target);
                    Advance();
                })
                { text = step.Prompt ?? Loc.T("guide.show_me") };

                next.AddToClassList("guide__next");
                next.AddToClassList("guide__next--go");
            }
            else
            {
                next = new Button(Advance) { text = step.Prompt ?? Loc.T("guide.next") };
                next.AddToClassList("guide__next");
            }

            buttons.Add(next);

            var skip = new Button(Skip) { text = Loc.T("guide.skip") };
            skip.AddToClassList("guide__skip");
            buttons.Add(skip);

            bar.Add(buttons);
            strip.Add(bar);

            host.Add(strip);

            strip.AddToClassList("guide--arriving");
            strip.schedule.Execute(() => strip.RemoveFromClassList("guide--arriving"))
                .ExecuteLater(ArriveMilliseconds);

            ApplyHighlight(step);
        }

        /// <summary>
        /// Rings everything wearing the step's class.
        ///
        /// Deferred a frame because the screen behind may have been rebuilt in the same pass, and a
        /// query that runs before the layout finds nothing at all.
        /// </summary>
        private void ApplyHighlight(GuideStep step)
        {
            ClearHighlight();

            if (string.IsNullOrEmpty(step.Highlight))
            {
                return;
            }

            var wanted = step.Highlight;

            host.schedule.Execute(() =>
            {
                foreach (var element in host.Query<VisualElement>(className: wanted).ToList())
                {
                    element.AddToClassList("guide-lit");
                }
            }).ExecuteLater(24);
        }

        /// <summary>Takes the ring off everything. Public so the shell can clear it on a repaint.</summary>
        public void ClearHighlight()
        {
            foreach (var element in host.Query<VisualElement>(className: "guide-lit").ToList())
            {
                element.RemoveFromClassList("guide-lit");
            }
        }

        private void Advance()
        {
            var state = progress();

            // The favour is handed over on the way out of the step that offers it, so a player who
            // leaves the tour before that point never had it and one who hears the offer keeps it
            // whatever they do next. Named rather than counted: inserting a line above it must not
            // quietly move the gift to a different part of the conversation.
            if (Current?.Id == GuideScript.GiftStepId)
            {
                state.FreeResearchOwed = true;
            }

            state.Step++;

            if (state.Step >= GuideScript.Steps.Count)
            {
                state.Stage = GuideStage.Finished;
                Hide();
            }

            changed?.Invoke();
        }

        /// <summary>
        /// Leaving early, which is allowed at every step and is not a failure.
        ///
        /// **Anything already given stays given.** Walking out after he offers the favour and before
        /// he finishes explaining it would otherwise take it back, and a tutorial that punishes you
        /// for ending it is a tutorial people sit through resenting.
        /// </summary>
        private void Skip()
        {
            var state = progress();

            if (Current?.Id == GuideScript.GiftStepId)
            {
                state.FreeResearchOwed = true;
            }

            state.Stage = GuideStage.Finished;

            Hide();
            changed?.Invoke();
        }

        public void Hide()
        {
            ClearHighlight();
            strip?.RemoveFromHierarchy();
            strip = null;
        }
    }
}
