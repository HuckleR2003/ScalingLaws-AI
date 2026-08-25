using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Emil walking the player round: his face, his name, and what he is saying at the bottom.
    ///
    /// **It sits over the game rather than replacing it.** The whole point of a guide like this is
    /// that the player is looking at the real screens while somebody talks over them — a tutorial
    /// that shows its own mock-ups teaches the mock-ups. So this is a strip along the bottom, the
    /// screens behind it are live, and the step that says "open COMPUTE" rings the COMPUTE tab.
    ///
    /// ## The bug that made the first playtest stop here
    ///
    /// The strip used to be rebuilt on every `Refresh`, and `Refresh` is called from `Show`, which
    /// runs **every time a simulated day rolls over** — about every one and a half seconds at normal
    /// speed. So the button under the player's cursor was destroyed and replaced between the press
    /// and the release, and the click went nowhere. Every symptom the playtest reported came from
    /// that one fact: NEXT "not working", the same line appearing three times in a row, and then
    /// several steps going by at once when a click finally landed between two rebuilds.
    ///
    /// **The strip is now rebuilt only when the step actually changes.** The highlight is still
    /// re-applied on every refresh, because the page behind it genuinely was rebuilt and the old
    /// ring is on elements that no longer exist. Those are two different lifetimes and treating them
    /// as one is what broke it.
    /// </summary>
    public sealed class GuideOverlay
    {
        /// <summary>Milliseconds between the strip arriving and the text being readable.</summary>
        public const int ArriveMilliseconds = 16;

        private readonly VisualElement host;
        private readonly Func<GuideProgress> progress;
        private readonly Action<GuideTarget> goTo;
        private readonly Action changed;

        /// <summary>
        /// Finds the bottom-bar tab a step is pointing at, or null when the step points at no tab.
        ///
        /// **The step does not name a class for this and must not.** Every "open the tab" step
        /// already says which target it wants, and asking the author to also write
        /// `hud-slot--compute` beside it is a second copy of the same fact that can go out of step
        /// with the first. It did: all six of those steps shipped with no highlight at all, which is
        /// how the tour ended up saying "click COMPUTE" while pointing at nothing.
        /// </summary>
        private readonly Func<GuideTarget, VisualElement> tabFor;

        /// <summary>Turns every other tab off while a step is waiting for one particular click.</summary>
        private readonly Action<GuideTarget?> lockToTab;

        /// <summary>Puts the model creator on the page the step is describing.</summary>
        private readonly Action<int> showCreatorStage;

        /// <summary>Raised when the player steps out meaning to come back.</summary>
        public Action leftForNow;

        private VisualElement strip;
        private Label line;
        private Label counter;
        private Button next;
        private Button skip;
        private Button later;
        private Label speakerName;
        private Label speakerRelation;
        private VisualElement portrait;

        /// <summary>Which step the strip currently shows, or -1 when there is no strip.</summary>
        private int builtFor = -1;

        public GuideOverlay(VisualElement host, Func<GuideProgress> progress,
            Action<GuideTarget> goTo, Action changed,
            Func<GuideTarget, VisualElement> tabFor = null,
            Action<int> showCreatorStage = null,
            Action<GuideTarget?> lockToTab = null)
        {
            this.showCreatorStage = showCreatorStage;
            this.host = host;
            this.progress = progress;
            this.goTo = goTo;
            this.changed = changed;
            this.tabFor = tabFor;
            this.lockToTab = lockToTab;
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
        /// **Rebuilds only on a step change.** See the note on the class: rebuilding on every call
        /// is what ate the player's clicks.
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

            if (builtFor != state.Step || strip == null || strip.parent == null)
            {
                // **Armed on the way in, not on the way out.** The playtest reached the step where
                // he offers to pay for the first node, clicked a node while he was still talking,
                // and was told it needed fifty research points it did not have. Handing the favour
                // over when the step is left means the offer is refused for exactly as long as the
                // offer is on screen.
                if (step.Id == GuideScript.GiftStepId)
                {
                    state.FreeResearchOwed = true;
                }

                Build(step);
                builtFor = state.Step;
            }
            else
            {
                // **The words are updated even when the elements are not.** Not rebuilding was the
                // fix for the click-eating; it also froze whatever the strip said at the moment it
                // was built, so switching language mid-tour left Emil talking English over a Polish
                // screen. Text is cheap to set and setting it destroys nothing.
                Retext(step);
            }

            // The creator page first, because the highlight below is a query against whatever is on
            // screen and moving the page after ringing something rings the wrong thing.
            if (step.CreatorStage >= 0)
            {
                showCreatorStage?.Invoke(step.CreatorStage);
            }

            // Always, whatever happened above. The page behind was very likely rebuilt, so the ring
            // is sitting on elements that have left the tree.
            ApplyHighlight(step);
            ApplyLock(step);
        }

        /// <summary>
        /// Repoints the existing strip at the current step's words, touching no elements.
        /// </summary>
        private void Retext(GuideStep step)
        {
            if (line != null)
            {
                line.text = step.Line;
            }

            if (counter != null)
            {
                counter.text = $"{progress().Step + 1} / {GuideScript.Steps.Count}";
            }

            if (next != null)
            {
                next.text = step.Prompt
                    ?? (step.WaitForClick ? Loc.T("guide.show_me") : Loc.T("guide.next"));
            }

            if (skip != null)
            {
                skip.text = Loc.T("guide.skip");
            }

            if (later != null)
            {
                later.text = Loc.T("guide.later");
            }

            // His name and how he is related. Missed on the first pass at this, and it is the same
            // fault: anything set once in Build is frozen in whatever language Build ran in.
            if (speakerName != null)
            {
                speakerName.text = GuideScript.CousinName;
            }

            if (speakerRelation != null)
            {
                speakerRelation.text = $"({GuideScript.CousinRelation})";
            }
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

            // The torn plate, with the names laid over it. Behind rather than around, because a
            // drawn shape cannot be a container that lays its own children out.
            var nameBlock = new VisualElement();
            nameBlock.AddToClassList("guide__nameblock");
            nameBlock.pickingMode = PickingMode.Ignore;
            nameBlock.Add(new GuideNamePlate());

            var names = new VisualElement();
            names.AddToClassList("guide__names");
            names.pickingMode = PickingMode.Ignore;

            speakerName = new Label(GuideScript.CousinName);
            speakerName.AddToClassList("guide__name");
            names.Add(speakerName);

            speakerRelation = new Label($"({GuideScript.CousinRelation})");
            speakerRelation.AddToClassList("guide__relation");
            names.Add(speakerRelation);

            nameBlock.Add(names);
            speaker.Add(nameBlock);
            strip.Add(speaker);

            var bar = new VisualElement();
            bar.AddToClassList("guide__bar");

            // **Where the player is in the tour.** The playtest reported not knowing what stage it
            // was at, which is fair: a conversation with no shape to it could be two steps from the
            // end or twenty.
            counter = new Label($"{progress().Step + 1} / {GuideScript.Steps.Count}");
            counter.AddToClassList("guide__counter");
            bar.Add(counter);

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

            // **Two ways out, and they are different promises.** "I'll take it from here" is a
            // player who does not want a tutorial. "I'll come back later" is one who does and has
            // something else on, and telling them there is something waiting at the end is the
            // cheapest possible reason to return.
            later = new Button(Later) { text = Loc.T("guide.later") };
            later.AddToClassList("guide__later");
            buttons.Add(later);

            skip = new Button(Skip) { text = Loc.T("guide.skip") };
            skip.AddToClassList("guide__skip");
            buttons.Add(skip);

            bar.Add(buttons);
            strip.Add(bar);

            host.Add(strip);

            strip.AddToClassList("guide--arriving");
            strip.schedule.Execute(() => strip.RemoveFromClassList("guide--arriving"))
                .ExecuteLater(ArriveMilliseconds);
        }

        /// <summary>
        /// Rings whatever this step is pointing at.
        ///
        /// Two sources, in order. A step that waits for a tab rings that tab, worked out from its
        /// own target so it cannot drift. Anything else rings every element wearing the class the
        /// step names.
        ///
        /// Deferred a frame because the screen behind may have been rebuilt in the same pass, and a
        /// query that runs before the layout finds nothing at all.
        /// </summary>
        private void ApplyHighlight(GuideStep step)
        {
            ClearHighlight();

            if (step.WaitForClick && step.Target != GuideTarget.None)
            {
                var tab = tabFor?.Invoke(step.Target);

                if (tab != null)
                {
                    tab.AddToClassList("guide-lit");
                    tab.AddToClassList("guide-lit--tab");
                    return;
                }
            }

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

        /// <summary>
        /// While a step is waiting for one particular tab, the other tabs are off.
        ///
        /// **Asked for after a playtest, and it is a fix rather than a restriction.** A player who
        /// wanders off to another screen mid-step comes back to a conversation that has moved on
        /// without them, and the tour reads as broken. Closing the other doors for the two seconds
        /// it takes to press the right one costs nothing and removes the whole failure.
        ///
        /// Only for the steps that ask for a click. Everything else leaves the bar alone, so a
        /// player who wants to look around between beats still can.
        /// </summary>
        private void ApplyLock(GuideStep step)
        {
            lockToTab?.Invoke(
                step.WaitForClick && step.Target != GuideTarget.None ? step.Target : null);
        }

        /// <summary>Takes the ring off everything. Public so the shell can clear it on a repaint.</summary>
        public void ClearHighlight()
        {
            foreach (var element in host.Query<VisualElement>(className: "guide-lit").ToList())
            {
                element.RemoveFromClassList("guide-lit");
                element.RemoveFromClassList("guide-lit--tab");
            }
        }

        private void Advance()
        {
            var state = progress();

            state.Step++;

            if (state.Step >= GuideScript.Steps.Count)
            {
                state.Stage = GuideStage.Finished;
                Hide();
            }

            changed?.Invoke();

            // The shell's chrome refresh does not rebuild the page, so nothing else would draw the
            // next line. Before this, advancing relied on a day rolling over to repaint the tour.
            Refresh();
        }

        /// <summary>
        /// Leaving early, which is allowed at every step and is not a failure.
        ///
        /// **Anything already given stays given.** Walking out after he offers the favour and before
        /// he finishes explaining it would otherwise take it back, and a tutorial that punishes you
        /// for ending it is a tutorial people sit through resenting.
        /// </summary>
        /// <summary>
        /// Stepping out with the intention of coming back.
        ///
        /// **The tour is paused, not ended.** `GuideStage` goes back to Talking, which is the state
        /// the phone leaves it in before the player answers, so the step is kept and the phone can
        /// ring again. Ending it here would make the button a second Skip with a friendlier label.
        /// </summary>
        private void Later()
        {
            // **He gets the last word before the strip goes.** A tutorial that vanishes when you
            // press a button tells you nothing about whether it is coming back, and the one reason
            // to return is that there is something waiting at the end.
            if (line != null && next != null && skip != null && later != null)
            {
                line.text = Loc.T("guide.later.reply");

                later.style.display = DisplayStyle.None;
                skip.style.display = DisplayStyle.None;
                next.text = Loc.T("common.close");

                // The button now closes rather than advances, so it is rebuilt with that one job.
                var closing = new Button(Pause) { text = Loc.T("common.close") };
                closing.AddToClassList("guide__next");
                next.parent.Add(closing);
                next.style.display = DisplayStyle.None;

                return;
            }

            Pause();
        }

        /// <summary>Puts the tour down, keeping the step so it can be picked up again.</summary>
        private void Pause()
        {
            var state = progress();
            state.Stage = GuideStage.Paused;

            Hide();
            changed?.Invoke();
            leftForNow?.Invoke();
        }

        private void Skip()
        {
            var state = progress();

            state.Stage = GuideStage.Finished;

            Hide();
            changed?.Invoke();
        }

        public void Hide()
        {
            ClearHighlight();
            lockToTab?.Invoke(null);
            strip?.RemoveFromHierarchy();
            strip = null;
            builtFor = -1;
        }
    }
}
