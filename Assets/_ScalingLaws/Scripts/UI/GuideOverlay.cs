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
        /// <summary>
        /// Puts the creator on a page and answers with the page it is actually on.
        ///
        /// Two-way on purpose. The tour may move the creator forward and may never move it back, so
        /// when the player is ahead the answer is larger than the request and the tour catches up.
        /// </summary>
        private readonly Func<int, int> showCreatorStage;

        /// <summary>Raised when the player steps out meaning to come back.</summary>
        public Action leftForNow;

        /// <summary>Opens the basement. Set by the shell, because only it can move money.</summary>
        public Action handOverBasement;

        /// <summary>One extra button a step may offer, or null. Set by the shell.</summary>
        public Func<GuideStep, GuideOffer?> offerFor;

        /// <summary>A thing the cousin will do for you, if you ask him on the right step.</summary>
        public readonly struct GuideOffer
        {
            public GuideOffer(string label, Action act)
            {
                Label = label;
                Act = act;
            }

            public string Label { get; }
            public Action Act { get; }
        }

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
            Func<int, int> showCreatorStage = null,
            Action<GuideTarget?> lockToTab = null)
        {
            this.showCreatorStage = showCreatorStage;
            this.host = host;
            this.progress = progress;
            this.goTo = goTo;
            this.changed = changed;
            this.tabFor = tabFor;
            this.lockToTab = lockToTab;

            // The screens report through the static; there is one shell and this is the one overlay.
            Reached = WalkthroughDid;
            KeepClear = KeepClearOfTheBottom;
        }

        /// <summary>
        /// Hands over anything the tour owes at the step it is on.
        ///
        /// **Separate from `Refresh` because the order mattered and nobody could see it.** `Show`
        /// builds the page and then refreshes the guide, so the research screen drew itself while
        /// the favour was still unarmed: every node priced against points the company did not have,
        /// and the tree only opened when the next day rebuilt the page. A playtest found it as
        /// "everything is locked until a day passes", which is exactly what it looks like from the
        /// outside and says nothing about the cause.
        ///
        /// Idempotent, derived from the step index, and safe to call as often as anything likes.
        /// </summary>
        public void HandOverAnythingOwed()
        {
            var state = progress();

            if (state == null)
            {
                return;
            }

            state.GrantGiftsUpTo(state.Step);

            if (state.BasementIsOwed(state.Step))
            {
                handOverBasement?.Invoke();
            }
        }

        public bool IsShowing => strip != null && strip.parent != null;

        // ---- walkthroughs -----------------------------------------------------------------------

        /// <summary>
        /// The walkthrough being run, or null when the opening tour is what is on screen.
        ///
        /// **Not saved.** Which walkthroughs are *finished* is a fact about the player and lives in
        /// `GuideProgress`; being three steps into one is a fact about this minute. A player who
        /// quits during a two minute tutorial and comes back to it half done, with the interface
        /// still locked and no memory of why, is worse off than one who starts it again.
        /// </summary>
        private Walkthrough running;

        private int runningStep;

        /// <summary>
        /// Told when the player does the thing a walkthrough step is waiting for.
        ///
        /// **Static, the way `InsightTip.Host` is**, and for the same reason: the screens that know a
        /// cabinet was bought are four constructors away from the overlay, and threading a callback
        /// through each of them would put a parameter about tutorials on classes that have no other
        /// business with one. There is exactly one shell.
        /// </summary>
        public static Action<string> Reached;

        /// <summary>
        /// Which walkthrough step is on screen, or null. Static for the same reason `Reached` is:
        /// there is one shell and one overlay, and the screens that answer to a walkthrough should
        /// not have to be handed a reference to it.
        /// </summary>
        public static string WalkingStepId { get; private set; }

        /// <summary>
        /// Moves the bar to the top of the screen while something else owns the bottom of it.
        ///
        /// The cabinet panel is the one that does: its actions sit exactly where the bar does, and
        /// a walkthrough step telling the player to press one of them was drawing over it.
        /// </summary>
        public void KeepClearOfTheBottom(bool clear)
        {
            strip?.EnableInClassList("guide--high", clear);
        }

        /// <summary>
        /// The same thing, for the screens. Static like `Reached`, and for the same reason: there is
        /// one shell and one overlay, and a screen should not have to be handed a reference to the
        /// tutorial to say "I am covering the bottom of the window".
        /// </summary>
        public static Action<bool> KeepClear;

        /// <summary>
        /// A walkthrough was completed. The shell sets this; the overlay only reports.
        ///
        /// Not fired on a STOP, deliberately: somebody who put a walkthrough down is not somebody
        /// who wants a follow-up flashing at them.
        /// </summary>
        public Action<string> walkthroughFinished;

        /// <summary>True when a walkthrough is showing this exact step.</summary>
        public static bool WalkingOn(string stepId) =>
            !string.IsNullOrEmpty(stepId) && WalkingStepId == stepId;

        /// <summary>Is a walkthrough on screen right now.</summary>
        public bool IsWalking => running != null;

        /// <summary>
        /// Starts a walkthrough, from the phone or from the chip that offers it.
        ///
        /// Opens the screen it happens on first, because every step after this rings something that
        /// has to already be in the tree.
        /// </summary>
        public void StartWalkthrough(Walkthrough walkthrough)
        {
            if (walkthrough == null || walkthrough.Steps.Count == 0)
            {
                return;
            }

            running = walkthrough;
            runningStep = 0;
            builtFor = -1;

            goTo?.Invoke(walkthrough.OpensOn);

            Refresh();
        }

        /// <summary>
        /// The player did the thing the current step was waiting for.
        ///
        /// Matched on the step's own id rather than on a position, so re-ordering the catalog cannot
        /// silently make a walkthrough advance on the wrong action. An id that is not the current
        /// step is ignored, which is what makes it safe to call from a screen unconditionally.
        /// </summary>
        public void WalkthroughDid(string stepId)
        {
            if (running == null || string.IsNullOrEmpty(stepId))
            {
                return;
            }

            var step = Current;

            if (step == null || step.Id != stepId)
            {
                return;
            }

            Advance();
        }

        /// <summary>
        /// Puts a walkthrough down, finished or abandoned.
        ///
        /// **The lock is lifted here and nowhere else.** A walkthrough holds the bottom bar shut for
        /// its whole length, so every way out of one has to come through this method or the player is
        /// left in a game they cannot navigate.
        /// </summary>
        private void EndWalkthrough(bool finished)
        {
            WalkingStepId = null;

            var walkthrough = running;

            running = null;
            runningStep = 0;

            if (finished && walkthrough != null)
            {
                progress().WalkthroughsDone.Add(walkthrough.Id);

                // The shell decides what to do about it. Today one thing does: the site rail lights
                // the way back to the basement, because it is the only door into it.
                walkthroughFinished?.Invoke(walkthrough.Id);
            }

            Hide();
            changed?.Invoke();
        }

        /// <summary>The step being shown, or null when the tour is over.</summary>
        public GuideStep Current
        {
            get
            {
                if (running != null)
                {
                    return runningStep >= 0 && runningStep < running.Steps.Count
                        ? running.Steps[runningStep]
                        : null;
                }

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

            // **A walkthrough is not part of the tour and does not read its stage.** The tour is
            // finished by the time any of these are offered, so gating on `Touring` would mean no
            // walkthrough could ever draw.
            if (running != null)
            {
                RefreshWalkthrough();
                return;
            }

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

            // **Outside the rebuild branch, and derived from the step index.** Arming the favour
            // inside the branch below made the gift depend on whether that one step happened to
            // trigger a rebuild: the second playtest reached the research screen, past the offer,
            // and was still told it needed 278 points it did not have. The rule is now "the tour has
            // reached the step where he pays for it", which is true however the player got there.
            HandOverAnythingOwed();

            if (builtFor != state.Step || strip == null || strip.parent == null)
            {
                // The basement on arrival, read off the index for the same reason: the step names a
                // screen, and a player who clicks through to it before pressing the button would
                // otherwise find a locked page with a price on it. Opening is idempotent, so asking
                // again on a rebuild costs nothing.
                if (state.BasementIsOwed(state.Step))
                {
                    handOverBasement?.Invoke();
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
            if (step.CreatorStage >= 0 && showCreatorStage != null)
            {
                var on = showCreatorStage(step.CreatorStage);

                // **The player got there first.** Same rule as `PlayerOpened` for tabs: somebody
                // who has already pressed NEXT through to the compute page does not need to be
                // told to open it, and does not want to be dragged back to say so.
                if (on > step.CreatorStage && CatchUpToCreatorStage(on))
                {
                    return;
                }
            }

            // Always, whatever happened above. The page behind was very likely rebuilt, so the ring
            // is sitting on elements that have left the tree.
            ApplyHighlight(step);
            ApplyLock(step);
        }

        /// <summary>
        /// The same pass, for a walkthrough.
        ///
        /// Two differences from the tour and both are the point of the thing. The interface is held
        /// on this screen for **every** step rather than only the ones waiting for a click, because a
        /// player who wanders off in the middle is the failure a walkthrough exists to prevent. And
        /// running off the end finishes it rather than ending the tour, which is already over.
        /// </summary>
        private void RefreshWalkthrough()
        {
            var step = Current;

            if (step == null)
            {
                EndWalkthrough(finished: true);
                return;
            }

            // Published for the screens that answer to a walkthrough with something other than a
            // USS class. The basement is the only one so far: its cabinets are drawn in 3D and a
            // class cannot reach them.
            WalkingStepId = step.Id;

            if (builtFor != runningStep || strip == null || strip.parent == null)
            {
                Build(step);
                builtFor = runningStep;
            }
            else
            {
                Retext(step);
            }

            ApplyHighlight(step);

            // Held on the screen the walkthrough happens on, whatever the step is doing.
            lockToTab?.Invoke(running.OpensOn);
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
                counter.text = CounterText();
            }

            if (next != null && string.IsNullOrEmpty(step.Signal))
            {
                next.text = step.Prompt
                    ?? (step.WaitForClick ? Loc.T("guide.show_me") : Loc.T("guide.next"));
            }

            if (skip != null)
            {
                skip.text = running != null ? Loc.T("walk.stop") : Loc.T("guide.skip");
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

            // Starts at rest rather than solid, so the strip looks the same before the pointer has
            // ever been near it as it does after. Setting it only from the move handler left the
            // very first strip of a campaign a shade brighter than every one after it.
            strip.style.opacity = RestingOpacity;
            strip.AddToClassList("guide");

            // Ignores the mouse everywhere except its own buttons, so the player can keep clicking
            // the screens underneath while he talks.
            strip.pickingMode = PickingMode.Ignore;

            // **He comes forward when you look at him.** Resting the cursor over the strip brings it
            // to full opacity, which is the opposite of what this did until a playtest reported the
            // instruction fading out exactly while it was being read.
            //
            // Driven from the host's mouse position rather than from MouseEnter on the strip: the
            // strip is `PickingMode.Ignore` on purpose, so it never receives a mouse event of its
            // own, and turning picking back on to get one would put a dead surface over the page.
            host.RegisterCallback<MouseMoveEvent>(FadeIfPointerIsOver);

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
            counter = new Label(CounterText());
            counter.AddToClassList("guide__counter");
            bar.Add(counter);

            line = new Label(step.Line);
            line.AddToClassList("guide__line");
            bar.Add(line);

            var buttons = new VisualElement();
            buttons.AddToClassList("guide__buttons");

            if (!string.IsNullOrEmpty(step.Signal))
            {
                // **A step waiting on the game has no button either**, for the same reason a
                // waiting walkthrough step has none: the claim is that the thing happened. A NEXT
                // beside "I am not pressing that for you" is a way to not press it and move on,
                // which is what the playtest did and then reported the training as instant.
                next = null;
            }
            else if (running != null && step.WaitForClick)
            {
                // **A waiting step in a walkthrough has no button at all.** The whole claim of one
                // is that the player did the thing, and a NEXT beside "click the cabinet" is a way
                // to finish without ever clicking a cabinet. The screen calls `WalkthroughDid` when
                // it happens; until then there is nothing here to press.
                next = null;
            }
            else if (step.WaitForClick)
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

            if (next != null)
            {
                buttons.Add(next);
            }

            // **Only on the step that asks.** A step can offer one extra thing, and this is the
            // mechanism for it: the shell decides what the offer does, the tour only draws it.
            if (offerFor != null && offerFor(step) is { } offer)
            {
                var help = new Button(() =>
                {
                    offer.Act();
                    Advance();
                })
                { text = offer.Label };

                help.AddToClassList("guide__offer");
                buttons.Add(help);
            }

            // **Two ways out, and they are different promises.** "I'll take it from here" is a
            // player who does not want a tutorial. "I'll come back later" is one who does and has
            // something else on, and telling them there is something waiting at the end is the
            // cheapest possible reason to return.
            // **No "come back later" on a walkthrough.** It is three minutes long and it is on one
            // screen; pausing it would mean saving a half-finished position and a lock, which is the
            // state this deliberately does not keep.
            if (running == null)
            {
                later = new Button(Later) { text = Loc.T("guide.later") };
                later.AddToClassList("guide__later");
                buttons.Add(later);
            }
            else
            {
                later = null;
            }

            skip = new Button(Skip)
            {
                text = running != null ? Loc.T("walk.stop") : Loc.T("guide.skip")
            };

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

            // **Not during a walkthrough.** There, "waits for a click" means a click on the screen
            // rather than on the bar, and the player is already standing on the tab this would ring.
            if (running == null && step.WaitForClick && step.Target != GuideTarget.None)
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

        /// <summary>
        /// How solid the strip is when nobody is looking at it.
        ///
        /// **This used to be the other way round and it was backwards.** The strip dropped to a
        /// fifth of its opacity while the cursor rested on it, so the one moment a player was
        /// deliberately reading the instruction was the one moment it faded out from under them.
        /// The intent was to keep it from covering the page; the page reserves its height now, so
        /// there is nothing left to uncover and only the reading was being harmed.
        ///
        /// Not fully solid at rest, because a strip that never changes is a strip the eye stops
        /// treating as live. Readable at rest, certain under the cursor.
        /// </summary>
        public const float RestingOpacity = 0.86f;

        /// <summary>
        /// Brings the strip fully forward while the pointer is over it.
        ///
        /// Registered once per build on the host rather than on the strip, because the strip ignores
        /// the mouse and would never hear about it. Cheap: one rectangle test per mouse move, and it
        /// only touches the style when the answer changes.
        /// </summary>
        private void FadeIfPointerIsOver(MouseMoveEvent move)
        {
            if (strip == null || strip.parent == null)
            {
                return;
            }

            var over = strip.worldBound.Contains(move.mousePosition);

            if (over == faded)
            {
                return;
            }

            faded = over;
            strip.style.opacity = over ? 1f : RestingOpacity;
        }

        private bool faded;

        /// <summary>Takes the ring off everything. Public so the shell can clear it on a repaint.</summary>
        public void ClearHighlight()
        {
            foreach (var element in host.Query<VisualElement>(className: "guide-lit").ToList())
            {
                element.RemoveFromClassList("guide-lit");
                element.RemoveFromClassList("guide-lit--tab");
            }
        }

        /// <summary>Where the player is, in whichever track is running.</summary>
        private string CounterText() =>
            running != null
                ? $"{runningStep + 1} / {running.Steps.Count}"
                : $"{progress().Step + 1} / {GuideScript.Steps.Count}";

        private void Advance()
        {
            if (running != null)
            {
                runningStep++;

                if (runningStep >= running.Steps.Count)
                {
                    EndWalkthrough(finished: true);
                    return;
                }

                changed?.Invoke();
                Refresh();
                return;
            }

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

        /// <summary>
        /// The player got there on their own, so the step is finished.
        ///
        /// **Asked for after the playtest and it is a real annoyance, not a nicety.** A step that
        /// says "click COMPUTE" and then still wants a button pressed after the player has clicked
        /// COMPUTE is a tutorial arguing with somebody who is already doing what it asked.
        ///
        /// Only for steps that were waiting on that exact screen. Everything else is a step that is
        /// talking, and talking is not finished by walking away from it.
        /// </summary>
        /// <summary>
        /// The game did something a step was waiting for.
        ///
        /// **The tour's half of `GuideStep.Signal`.** A step carrying one draws no NEXT, so this is
        /// the only thing that can move it on: the player has to start the run, or wait for it, or
        /// put the model on sale. Anything else would put the button back and make the sentence
        /// above it optional again.
        ///
        /// Unknown signals are ignored, and a signal that arrives while a different step is showing
        /// is ignored too. Both are normal: the shell reports what happened without knowing or
        /// caring where the tour has got to.
        /// </summary>
        public void Reported(string signal)
        {
            if (string.IsNullOrEmpty(signal) || running != null)
            {
                return;
            }

            var state = progress();

            if (state.Stage != GuideStage.Touring)
            {
                return;
            }

            if (Current?.Signal != signal)
            {
                return;
            }

            Advance();
        }

        public void PlayerOpened(GuideTarget target)
        {
            var state = progress();

            // A walkthrough already sits on its own screen, so "the player got here on their own" is
            // true of every step in one and would skip the whole thing on the first repaint.
            if (running != null || state.Stage != GuideStage.Touring || target == GuideTarget.None)
            {
                return;
            }

            var step = Current;

            if (step == null || !step.WaitForClick || step.Target != target)
            {
                return;
            }

            Advance();
        }

        /// <summary>
        /// Walks the tour forward to the last step that is about a creator page the player has
        /// already passed.
        ///
        /// Stops at the first step that is not about the creator at all, so it can never run off
        /// the end of the act: the steps after the creator are about other screens and have to be
        /// read.
        ///
        /// Returns true when it moved, so the caller can leave the rest of the refresh to the
        /// rebuild that follows.
        /// </summary>
        private bool CatchUpToCreatorStage(int stage)
        {
            var state = progress();
            var moved = false;

            while (true)
            {
                var step = Current;

                if (step == null || step.CreatorStage < 0 || step.CreatorStage >= stage)
                {
                    break;
                }

                state.Step++;
                moved = true;
            }

            if (moved)
            {
                changed?.Invoke();
            }

            return moved;
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
            // **A walkthrough keeps one way out and this is it.** The author asked for a forced run,
            // and the lock delivers that: the bottom bar is shut for the whole length of one. A run
            // with no exit at all is a different thing, though — one mis-wired step and the player is
            // in a game they cannot navigate and cannot leave, with no way back but deleting a save.
            // Nothing here is marked done, so the chip stays and the phone still offers it.
            if (running != null)
            {
                EndWalkthrough(finished: false);
                return;
            }

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
