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

        /// <summary>
        /// How long his next line shows itself while he is folded away.
        ///
        /// Asked for on 2026-10-25: while folded, a step the player satisfies by doing the thing
        /// moves him on silently, so they have no way of knowing anything was said. It comes out,
        /// waits, and goes back, which is a notice rather than a conversation.
        /// </summary>
        public const int PeekMilliseconds = 1200;

        /// <summary>How long the slide out and back takes. Matches `.guide__bar`'s transition.</summary>
        public const int PeekFadeMilliseconds = 240;

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
        /// <summary>
        /// Whether the thing a step is waiting for has already happened.
        ///
        /// **A signal is an event and the tour needs a state.** `Reported` only advances the step
        /// that is on screen when the event arrives, so a player who releases a model one step early
        /// dropped the only `model_released` they were ever going to get and the tour stopped at a
        /// step with no button on it. Answered from the company rather than from a remembered set,
        /// so it survives a save and a reload the way a latch would not.
        /// </summary>
        private readonly Func<string, bool> alreadyDone;

        /// <summary>
        /// Redraws whatever screen is open, for the one case where a gift changes it underneath.
        ///
        /// The shell's own `changed` repaints the chrome and not the page, which is correct for
        /// every other use and wrong for this one: a research tree built a moment before the favour
        /// landed still has a price on every node.
        /// </summary>
        private readonly Action repaintScreen;

        private readonly Func<int, int> showCreatorStage;

        /// <summary>Raised when the player steps out meaning to come back.</summary>
        public Action leftForNow;

        /// <summary>Opens the basement. Set by the shell, because only it can move money.</summary>
        public Action handOverBasement;

        /// <summary>
        /// Hands over the cousin's favour to somebody who never walked to the step that offers it.
        ///
        /// Set by the shell, for the same reason the basement is: this ends in a letter and a
        /// promise the company has to keep, and neither belongs to an overlay.
        /// </summary>
        public Action skippedTheTour;

        /// <summary>One extra button a step may offer, or null. Set by the shell.</summary>
        public Func<GuideStep, GuideOffer?> offerFor;

        /// <summary>
        /// What a step is waiting for, when it is waiting for work rather than for a click.
        ///
        /// **Reported: at step 40 Emil says "nothing happens for months" and nothing on screen
        /// says anything is happening.** He is right, the run is under way, and the strip has no
        /// button because the claim is that the game does the next thing. From the player's chair
        /// that is indistinguishable from a tutorial that has frozen.
        ///
        /// Same shape as `offerFor`: the shell knows what the number means and the tour only
        /// draws it. Null when the step is not waiting on anything measurable, which is most of
        /// them.
        /// </summary>
        public Func<GuideStep, GuideWait?> waitFor;

        /// <summary>One line and a bar, for a step whose next move belongs to the calendar.</summary>
        public readonly struct GuideWait
        {
            public GuideWait(string label, double progress)
            {
                Label = label ?? string.Empty;
                Progress = Math.Clamp(progress, 0.0, 1.0);
            }

            public string Label { get; }

            public double Progress { get; }
        }

        /// <summary>A thing the cousin will do for you, if you ask him on the right step.</summary>
        public readonly struct GuideOffer
        {
            public GuideOffer(string label, Action act, bool confirms = false)
            {
                Confirms = confirms;
                Label = label;
                Act = act;
            }

            public string Label { get; }
            public Action Act { get; }

            /// <summary>
            /// Draw it as a <see cref="ConfirmButton"/>, which asks once before it acts.
            ///
            /// For an offer the player cannot take back: refusing the free research is four months
            /// of calendar and a node they will otherwise pay points for.
            /// </summary>
            public bool Confirms { get; }
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
        private Button fold;
        private GuideFoldArrow foldArrow;
        private Label foldLabel;
        private VisualElement messageBar;
        private VisualElement nameBlock;

        /// <summary>
        /// Whether he is folded away to the chevron and his portrait.
        ///
        /// **A field on the overlay rather than state on the strip, and that is the whole care
        /// here.** The strip is rebuilt whenever the step index changes, which is the 2026-08-25
        /// fix: it used to be rebuilt on every day rollover and the button under the cursor was
        /// destroyed between the press and the release. A player who folded him away on step four
        /// has not asked for him back on step five, so this has to outlive the strip that carries
        /// the button.
        ///
        /// Not saved. It is a view preference about the next few minutes, like which tab the
        /// management screen opens on, and nothing in the rules reads it.
        /// </summary>
        private bool folded;

        /// <summary>Whether his line is currently showing itself over a folded strip.</summary>
        private bool peeking;

        /// <summary>
        /// Which peek is the live one.
        ///
        /// **Every scheduled step of a peek is guarded on this.** A peek is three callbacks over
        /// about a second and a half, and in that time the player can satisfy another step, unfold
        /// him, or skip the tour entirely. Without the token the callbacks of a peek that is over
        /// go on firing into whatever state replaced it, which is how a stale timer hides a strip
        /// the player has just opened on purpose.
        /// </summary>
        private int peekToken;

        /// <summary>Which step the strip currently shows, or -1 when there is no strip.</summary>
        private int builtFor = -1;

        public GuideOverlay(VisualElement host, Func<GuideProgress> progress,
            Action<GuideTarget> goTo, Action changed,
            Func<GuideTarget, VisualElement> tabFor = null,
            Func<int, int> showCreatorStage = null,
            Action<GuideTarget?> lockToTab = null,
            Action repaintScreen = null,
            Func<string, bool> alreadyDone = null)
        {
            this.alreadyDone = alreadyDone;
            this.repaintScreen = repaintScreen;
            this.showCreatorStage = showCreatorStage;
            this.host = host;
            this.progress = progress;
            this.goTo = goTo;
            this.changed = changed;
            this.tabFor = tabFor;
            this.lockToTab = lockToTab;

            // The screens report through the static; there is one shell and this is the one overlay.
            Reached = WalkthroughDid;
            TurnedCreatorPage = PlayerTurnedCreatorPage;
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

            // **The return value is the whole point and it used to be discarded.** A node that has
            // just been paid for is drawn with its price until something rebuilds the page, and the
            // chrome refresh does not. From `Show` this reports false, because the gift was handed
            // over at the top of that method before the page was built, so this cannot re-enter.
            if (state.GrantGiftsUpTo(state.Step))
            {
                repaintScreen?.Invoke();
            }

            if (state.BasementIsOwed(state.Step))
            {
                handOverBasement?.Invoke();
            }
        }

        public bool IsShowing => strip != null && strip.parent != null;

        /// <summary>
        /// How tall the strip actually is, measured, or zero while it is not up.
        ///
        /// **A constant was wrong and nothing could tell.** The page reserved 168px at its foot for
        /// the tour bar, which was written once and never measured, and the strip is taller than
        /// that: the portrait hangs above the bar on a negative margin, so Emil's head sat over the
        /// bottom band of whatever page was open. A tester reported exactly that, on the creator's
        /// review summary, and the only honest reserve is the one the strip reports about itself.
        ///
        /// **Do not shrink this to the bar.** That was the next thing tried, on the reasoning that
        /// the portrait hangs up and to the left of the bar and therefore off the page. Measured,
        /// it does not: at 1920x1080 the bar runs x 269 to 1651 and the portrait sits at x 596 to
        /// 692, squarely inside it, 84px above it and squarely over the page. Reserving to the bar
        /// puts Emil's head back on the text, which is the report this was written for.
        ///
        /// What the taller reserve did expose is a second fault with a different cause: the
        /// creator was 838px of page in a 647px window, the tour scrolls its own highlight into
        /// view, and that took the stage heading off the top and the WSTECZ/DALEJ row off the
        /// bottom at once. The repair for that is in the creator, which now pins its heading and
        /// its buttons and scrolls only the stage between them. Neither fault is the other's fix.
        /// </summary>
        public float MeasuredHeight { get; private set; }

        /// <summary>
        /// Raised when <see cref="MeasuredHeight"/> changes, so the page can re-reserve.
        ///
        /// The strip grows and shrinks with the length of the line and with whether the step
        /// carries buttons, so one measurement taken on the first step is another constant with a
        /// longer life.
        /// </summary>
        public Action SizeChanged { get; set; }

        /// <summary>
        /// A little air between the bottom of the page and the top of the strip.
        ///
        /// Measured clearance alone puts the last line of a page against the portrait's edge, and
        /// two things touching read as one thing overlapping.
        /// </summary>
        public const float StripClearance = 24f;

        /// <summary>How wide Emil's head is on an ordinary screen, and over the creator.</summary>
        private const int PortraitPixels = 96;

        private const int TightPortraitPixels = 52;

        /// <summary>
        /// The strip goes compact over the creator, at every window size.
        ///
        /// **The first version of this gated on the window and the gate was built on a wrong
        /// reading.** The page geometry logged by `TabProofTests` is taken with the tour already
        /// put away, so 922 and 787 are the page with no strip on it rather than the window, and
        /// the window itself is 1059 logical pixels at 900px against 1080 at 1080px. Twenty one
        /// pixels apart is nowhere to put a threshold.
        ///
        /// Then the frames settled it: `tour_create_3.png` at a full 1920x1080 is cut in exactly
        /// the same place as `small_900_data.png`, losing GRANICA WIEDZY and DEDUPLIKACJA, which
        /// are the two panels that step is asking the player to decide between. This was never a
        /// small-screen fault. It is every screen, and the laptop is only where it was noticed.
        ///
        /// So the condition is the page underneath rather than the pixels: the creator is pinned
        /// top and bottom and must not scroll, so whatever the strip takes is taken out of the
        /// middle of it. Every other screen the tour visits can scroll and keeps the full portrait.
        /// </summary>
        private bool WantsTightStrip => (Current?.CreatorStage ?? -1) >= 0;

        /// <summary>Whether he is currently folded away. Read by the tests and by nothing else.</summary>
        public bool Folded => folded;

        /// <summary>
        /// Folds him away, or brings him back.
        ///
        /// **Public because an EditMode element has no panel, so a click sent to the chevron is
        /// never dispatched.** Same seam, for the same reason, as `ManagementScreen.ShowDesk`,
        /// `PartsShop.Order`, `GateNotice.TakeTheOffer` and `RivalPanel.ShowTogether`. The button
        /// calls this and holds no logic of its own, so what a test drives is what a click does.
        ///
        /// It changes nothing about the tour. The step does not advance, the highlight stays on
        /// whatever it was ringing and a waiting step goes on waiting, because folding a
        /// conversation away is not the same as being finished with it. Skipping already has two
        /// buttons and they say what they do.
        /// </summary>
        public void SetFolded(bool value)
        {
            if (folded == value)
            {
                return;
            }

            folded = value;

            // **A peek in flight belongs to the fold that is ending.** Cleared rather than ended,
            // so the bar is put straight into the state the new fold asks for instead of flicking
            // through the old one on the way.
            ClearPeek();
            ApplyFold();
        }

        /// <summary>
        /// Puts the current fold on the strip, whichever way it got there.
        ///
        /// Called from `SetFolded` and again at the end of every `Build`, because the strip the
        /// fold applies to is a new one by then.
        /// </summary>
        private void ApplyFold()
        {
            strip?.EnableInClassList("guide--folded", folded);

            if (foldArrow != null)
            {
                foldArrow.PointsUp = folded;
            }

            // **The word is what makes it a control.** The chevron alone shipped on 2026-10-26 and
            // the tester who asked for the button in the first place reported he could not see it.
            // A proof render showed why: a 46px yellow disc beside a portrait reads as part of the
            // portrait, and every other control in this strip is a labelled rectangle along the
            // bottom. It was on the screen at every width measured and still nobody found it, so
            // "drawn" and "visible" are two claims and only the second one matters here.
            if (foldLabel != null)
            {
                foldLabel.text = Loc.T(folded ? "guide.unfold.short" : "guide.fold.short");
            }

            var shown = folded ? DisplayStyle.None : DisplayStyle.Flex;

            if (messageBar != null)
            {
                messageBar.style.display = shown;
            }

            if (nameBlock != null)
            {
                nameBlock.style.display = shown;
            }

            // The strip has changed height by a lot, and the page underneath reserves room for it.
            // `MeasureStrip` is raised by the layout that follows and does the re-reserving, so
            // nothing is forced here: forcing it would read a height the layout has not computed.
        }

        /// <summary>Whether his line is showing itself over a folded strip right now.</summary>
        public bool Peeking => peeking;

        /// <summary>
        /// Shows his next line for a moment over a folded strip, then puts it away again.
        ///
        /// **Public for the reason `SetFolded` is: an EditMode element has no panel, so nothing
        /// scheduled ever fires there.** The three timers below are the animation; this method and
        /// `EndPeek` are the state change, and a test drives those.
        ///
        /// **Nothing in it can be clicked, and that is a change from what was asked for rather
        /// than an oversight.** A bar that appears under the cursor for a second with NEXT, the
        /// offer, LATER and SKIP on it is a second in which a click meant for the page lands on a
        /// button that ends the tutorial. This project has shipped two invisible click eaters
        /// already and both were this shape. So the peek is something to read: it says a line went
        /// by, and the chevron beside it is the way to open him up and read it properly.
        /// </summary>
        public void BeginPeek()
        {
            if (!folded || messageBar == null)
            {
                return;
            }

            peeking = true;
            peekToken++;

            var mine = peekToken;

            // Laid out but away, so there is a state for the transition to come *from*. Setting
            // `display` and the final look in one frame animates nothing: the element has no
            // previous value on the frame it first exists in. Same trick, and the same 16ms, as
            // `guide--arriving` at the foot of `Build`.
            messageBar.style.display = DisplayStyle.Flex;
            strip?.EnableInClassList("guide--peek-away", true);
            SetBarClicksOff(true);

            strip?.schedule.Execute(() =>
                {
                    if (mine == peekToken)
                    {
                        strip?.EnableInClassList("guide--peek-away", false);
                    }
                })
                .ExecuteLater(ArriveMilliseconds);

            strip?.schedule.Execute(() =>
                {
                    if (mine == peekToken)
                    {
                        FadePeekAway(mine);
                    }
                })
                .ExecuteLater(PeekMilliseconds);
        }

        /// <summary>Starts the slide back, and takes the bar out of the layout once it is gone.</summary>
        private void FadePeekAway(int mine)
        {
            strip?.EnableInClassList("guide--peek-away", true);

            strip?.schedule.Execute(() =>
                {
                    if (mine == peekToken)
                    {
                        EndPeek();
                    }
                })
                .ExecuteLater(PeekFadeMilliseconds);
        }

        /// <summary>
        /// Puts his line away again, leaving the fold exactly as it was.
        ///
        /// Reached three ways: the timer running out, the player unfolding him, and the strip being
        /// rebuilt under a peek. All three want the same thing, which is why there is one body.
        /// </summary>
        public void EndPeek()
        {
            if (!peeking)
            {
                return;
            }

            ClearPeek();
            ApplyFold();
        }

        /// <summary>
        /// Drops the peek state without deciding what the bar should look like afterwards.
        ///
        /// Separate from `EndPeek` because `SetFolded` has to clear a peek and then apply a fold
        /// that has just changed. Ending the peek first would put the bar back to the *old* fold
        /// for a frame.
        /// </summary>
        private void ClearPeek()
        {
            peeking = false;

            // Anything the old peek still has scheduled belongs to a number nobody holds now.
            peekToken++;

            strip?.EnableInClassList("guide--peek-away", false);
            SetBarClicksOff(false);
        }

        /// <summary>
        /// Turns the buttons on his message off, or back on.
        ///
        /// **Queried rather than listed.** The bar carries NEXT, the step's own offer, LATER and
        /// SKIP, and which of them exist depends on the step; three of the four are fields here and
        /// the offer is not. A list would be right today and wrong the first time a step carries
        /// something new.
        ///
        /// `PickingMode` rather than `SetEnabled`, because a greyed button is how this game says a
        /// control is refusing, and nothing here is refusing anything.
        /// </summary>
        private void SetBarClicksOff(bool off)
        {
            if (messageBar == null)
            {
                return;
            }

            foreach (var button in messageBar.Query<Button>().ToList())
            {
                button.pickingMode = off ? PickingMode.Ignore : PickingMode.Position;
            }
        }

        /// <summary>
        /// What the chevron says when the pointer rests on it.
        ///
        /// **It carries the line he is on**, which is the thing a folded tour cannot otherwise
        /// answer: a player who folded him away and then wonders why the other tabs are shut has
        /// no way back to the sentence that explains it. Read live rather than captured, because
        /// the step changes underneath a chevron that is never rebuilt for it.
        /// </summary>
        private string FoldNote()
        {
            var step = Current;

            if (step == null)
            {
                return Loc.T("guide.fold.note");
            }

            // A waiting step is the case this note exists for. Everything else is simply paused.
            var why = step.WaitForClick
                ? Loc.T("guide.fold.waiting")
                : Loc.T("guide.fold.note");

            return string.IsNullOrEmpty(step.Line) ? why : step.Line + " " + why;
        }

        private void MeasureStrip(GeometryChangedEvent _)
        {
            ApplyFade(true);

            // **The strip shrinks where the page cannot spare it.** Checked here because this is
            // the one callback a laid-out strip is guaranteed to raise, and because the step can
            // change without the strip ever being rebuilt from scratch.
            var tight = WantsTightStrip;

            strip?.EnableInClassList("guide--tight", tight);

            // **The head is sized here because the stylesheet physically cannot do it.**
            // `PhonePanel.Avatar(96)` writes `style.width` and `style.height` on the element,
            // and an inline style outranks every USS rule however many classes it carries. So
            // `.guide--tight .guide__portrait { width: 52px }` was correct, specific, loaded,
            // and beaten, which is why two measured runs came back with the class applied and
            // the portrait unchanged. This is the `.rackface.roombuild__face` lesson one level
            // worse: no selector wins against inline.
            if (portrait != null)
            {
                var head = tight ? TightPortraitPixels : PortraitPixels;

                portrait.style.width = head;
                portrait.style.height = head;
            }

            var now = strip == null ? 0f : strip.resolvedStyle.height;

            if (Mathf.Approximately(now, MeasuredHeight))
            {
                return;
            }

            MeasuredHeight = now;
            SizeChanged?.Invoke();
        }

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
        /// The model creator turned a page. Static for the same reason `Reached` is: the screen
        /// that turns the page does not know whether a tour exists, and should not have to.
        /// </summary>
        public static Action<int> TurnedCreatorPage;

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
        /// <summary>
        /// The walkthrough on screen, or null when the opening tour is what is running.
        ///
        /// **Read by the shell to put a screen on the right tab before the highlight runs.** A
        /// walkthrough that rings a class sitting behind an unopened tab points at nothing, which
        /// is the same fault as pointing off screen and reads to a player as the tour being
        /// broken. Exposed rather than adding a second record of which one is up.
        /// </summary>
        public Walkthrough RunningWalkthrough => running;

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

            // **A step cannot wait for something that has already happened.** `Reported` only moves
            // the step that is on screen when the event fires, so releasing a model one step early
            // spends the single `model_released` this campaign will ever raise and leaves the next
            // step waiting forever with no button on it. That is the tour dying at 44 of 57.
            //
            // Asked of the company rather than remembered, so a reload cannot lose the answer. Each
            // pass moves the index forward, so a run of already-satisfied steps unwinds and stops.
            if (!string.IsNullOrEmpty(step.Signal) && alreadyDone != null && alreadyDone(step.Signal))
            {
                Advance();
                return;
            }

            // **Outside the rebuild branch, and derived from the step index.** Arming the favour
            // inside the branch below made the gift depend on whether that one step happened to
            // trigger a rebuild: the second playtest reached the research screen, past the offer,
            // and was still told it needed 278 points it did not have. The rule is now "the tour has
            // reached the step where he pays for it", which is true however the player got there.
            HandOverAnythingOwed();

            // **A step change, as opposed to the strip having been remounted.** Only the first
            // is him saying something new; the second is housekeeping and must not announce itself.
            // `builtFor >= 0` keeps the opening line of a campaign out of it, since there is nothing
            // for a first strip to have moved on from.
            var movedOn = builtFor >= 0 && builtFor != state.Step;

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

                // **He went on while he was folded away.** With the message hidden there is no
                // NEXT to press, so the only way past a step is the player doing the thing it
                // asked for, and that moves him on in silence. Asked for on 2026-10-25: the line
                // comes out for a moment so they know something was said and can open him up.
                if (movedOn && folded)
                {
                    BeginPeek();
                }
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
        /// <summary>
        /// The bar under the line, with the words the report asked for in it.
        ///
        /// **It moves, and it moves without the strip being rebuilt.** The first version said
        /// plainly that it did not animate, on the reasoning that a creeping bar would mean
        /// rebuilding the strip every day and rebuilding it destroys the button under the player's
        /// cursor between the press and the release. That reasoning is right about the rebuild and
        /// wrong about the conclusion: the fill is repointed the way every other live word on this
        /// strip is repointed, in `Retext`, touching no element. A bar that sits at four per cent
        /// for a year and a half is not evidence that a wait is a wait, which is the one job it
        /// has, and it was reported as broken because from the outside it is indistinguishable
        /// from broken.
        /// </summary>
        private VisualElement BuildWait(GuideWait waiting)
        {
            var block = new VisualElement();
            block.AddToClassList("guide__wait");

            waitLabel = new Label(waiting.Label);
            waitLabel.AddToClassList("guide__waitlabel");
            block.Add(waitLabel);

            var track = new VisualElement();
            track.AddToClassList("guide__waittrack");

            waitFill = new VisualElement();
            waitFill.AddToClassList("guide__waitfill");
            waitFill.style.width = Length.Percent((float)(waiting.Progress * 100.0));
            track.Add(waitFill);

            block.Add(track);
            return block;
        }

        /// <summary>The two live parts of the wait block, repointed rather than rebuilt.</summary>
        private Label waitLabel;

        private VisualElement waitFill;

        private void Retext(GuideStep step)
        {
            if (line != null)
            {
                line.text = step.Line;
            }

            // The wait, which is the one thing on this strip that changes without the player
            // doing anything. Repointed, never rebuilt: see BuildWait.
            if (waitFill != null && waitFor != null && waitFor(step) is { } waiting)
            {
                waitFill.style.width = Length.Percent((float)(waiting.Progress * 100.0));

                if (waitLabel != null)
                {
                    waitLabel.text = waiting.Label;
                }
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

            // The old strip's pieces go with it, or a Retext against a rebuilt step would write
            // into elements that have left the tree.
            waitLabel = null;
            waitFill = null;

            strip = new VisualElement();

            // Starts at rest rather than solid, so the strip looks the same before the pointer has
            // ever been near it as it does after. Setting it only from the move handler left the
            // very first strip of a campaign a shade brighter than every one after it.
            strip.style.opacity = RestingOpacity;
            strip.AddToClassList("guide");

            // **The one step that hands the player something.** Asked for on 2026-10-26: a quiet
            // green so the eye stops on the offer rather than reading it as another line of talk.
            strip.EnableInClassList("guide--gift", step.Id == GuideScript.GiftStepId);

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

            // Measured rather than assumed. See MeasuredHeight for what the assumption cost.
            strip.RegisterCallback<GeometryChangedEvent>(MeasureStrip);

            var speaker = new VisualElement();
            speaker.AddToClassList("guide__speaker");
            speaker.pickingMode = PickingMode.Ignore;

            portrait = PhonePanel.Avatar(96);
            portrait.AddToClassList("guide__portrait");
            speaker.Add(portrait);

            // **Beside his head, as asked for on 2026-10-25.** The strip and the speaker are both
            // `PickingMode.Ignore` so the player can keep working the screen underneath while he
            // talks; a child button still receives its own events, which the NEXT and SKIP buttons
            // in this same strip have proven since the tour was written. Stated anyway, because
            // the one thing worse than a dead button here is a dead button nobody can explain.
            foldArrow = new GuideFoldArrow();

            fold = new Button(() => SetFolded(!folded));
            fold.AddToClassList("guide__fold");
            fold.pickingMode = PickingMode.Position;
            fold.Add(foldArrow);

            // `PickingMode.Ignore` on both children, or the label and the chevron each eat the
            // click meant for the button they sit inside. That has cost this project a dead
            // control three times.
            foldLabel = new Label(Loc.T("guide.fold.short"));
            foldLabel.AddToClassList("guide__fold-word");
            foldLabel.pickingMode = PickingMode.Ignore;
            fold.Add(foldLabel);

            InsightTip.AttachLive(fold,
                () => Loc.T(folded ? "guide.unfold" : "guide.fold"),
                FoldNote);

            speaker.Add(fold);

            // The torn plate, with the names laid over it. Behind rather than around, because a
            // drawn shape cannot be a container that lays its own children out.
            nameBlock = new VisualElement();
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
            messageBar = bar;
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

            // **Under the line, not instead of it.** What he says is the reason the wait is
            // worth sitting through; the bar is the evidence that it is a wait rather than a
            // hang. Replacing one with the other would answer the complaint by deleting the
            // thing the step exists for.
            if (waitFor != null && waitFor(step) is { } waiting)
            {
                bar.Add(BuildWait(waiting));
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("guide__buttons");

            Label buttonsWaiting = null;

            if (!string.IsNullOrEmpty(step.Signal))
            {
                // **It says what it is waiting for, on something that cannot be pressed.**
                // Drawing nothing at all was honest and unreadable: the strip sat there with no
                // button while the player looked for one. Asked for on 2026-10-26.
                //
                // A `Label` rather than a disabled `Button`, deliberately. A disabled button
                // dispatches no pointer event, so it cannot explain itself, and this project has
                // shipped that three times; a label never looked pressable in the first place.
                if (!string.IsNullOrEmpty(step.Prompt))
                {
                    var says = new Label(step.Prompt);
                    says.AddToClassList("guide__waiting");
                    says.pickingMode = PickingMode.Ignore;
                    buttonsWaiting = says;
                }

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

            if (buttonsWaiting != null)
            {
                buttons.Add(buttonsWaiting);
            }

            if (next != null)
            {
                buttons.Add(next);
            }

            // **Only on the step that asks.** A step can offer one extra thing, and this is the
            // mechanism for it: the shell decides what the offer does, the tour only draws it.
            if (offerFor != null && offerFor(step) is { } offer)
            {
                var help = offer.Confirms
                    ? new ConfirmButton(offer.Label, () =>
                    {
                        offer.Act();
                        Advance();
                    })
                    : new Button(() =>
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
            // **Neither escape is drawn on a step that is waiting for the game.** Asked for
            // on 2026-10-26 about the verdict step. "I will come back later" means nothing while
            // the tour is already waiting, and both buttons sat next to a sentence saying he would
            // not press the thing for you, which is an invitation to press something else instead.
            //
            // It is not a trap: a signal step does not lock the interface, so the player can go
            // anywhere, and the chevron beside his head folds him away entirely.
            var waitingOnTheGame = !string.IsNullOrEmpty(step.Signal);

            if (running == null && !waitingOnTheGame)
            {
                later = new Button(Later) { text = Loc.T("guide.later") };
                later.AddToClassList("guide__later");
                buttons.Add(later);
            }
            else
            {
                later = null;
            }

            if (waitingOnTheGame)
            {
                skip = null;
            }
            else
            {
                skip = new Button(Skip)
                {
                    text = running != null ? Loc.T("walk.stop") : Loc.T("guide.skip")
                };

                skip.AddToClassList("guide__skip");
                buttons.Add(skip);
            }

            bar.Add(buttons);
            strip.Add(bar);

            host.Add(strip);

            // **The fold outlives the strip.** This is a new strip, built because the step moved,
            // and the player folded the last one away for a reason that has not stopped applying.
            ApplyFold();

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
                var lit = host.Query<VisualElement>(className: wanted).ToList();

                foreach (var element in lit)
                {
                    element.AddToClassList("guide-lit");
                }

                // **And bring it on screen.** Reported from a playtest: he talks about the rented
                // power while it is a section and a half below the fold, so the player reads a
                // sentence about a control they cannot see and presses NEXT to make it go away.
                // Pointing at something off screen is the same as pointing at nothing.
                if (lit.Count > 0)
                {
                    ScrollIntoView(lit[0]);

                    // **And point at it, when it is sitting in a corner.** The ring is enough on
                    // something the eye is already near and is not enough on the banner in the top
                    // right, which is what the support walkthrough opens on. `AttentionPull` makes
                    // its own decision about whether the target is far enough out to be worth it,
                    // so this stays a rule rather than a list of steps somebody has to maintain.
                    AttentionPull.Play(lit[0]);
                }
            }).ExecuteLater(24);
        }

        /// <summary>
        /// Scrolls whatever encloses this element until the element is in the window.
        ///
        /// Walks up for the nearest `ScrollView` rather than being handed the page's one, because
        /// the thing being pointed at can be inside a rail with its own scroller, and the one that
        /// has to move is the one it is actually in. Runs after the highlight, which is already
        /// deferred a frame, so the layout it scrolls against is the laid out one.
        /// </summary>
        private static void ScrollIntoView(VisualElement element)
        {
            for (var parent = element?.parent; parent != null; parent = parent.parent)
            {
                if (parent is ScrollView scroller)
                {
                    scroller.ScrollTo(element);
                    return;
                }
            }
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
        /// **This has now been wrong in both directions.** It started at a fifth while the cursor
        /// rested on it, so the one moment a player was deliberately reading the instruction was
        /// the moment it faded out from under them. Setting it to 0.86 fixed that and introduced
        /// the opposite fault: a tester reported that Emil's head covers the review summary in the
        /// creator and there is no way to read what is underneath, because 0.86 is opaque.
        ///
        /// The page reserves the strip's height, so this only matters where something floats over
        /// the page rather than flowing under it, and that is exactly where it matters most. Half
        /// is legible enough to keep the instruction in the corner of the eye and thin enough to
        /// read a figure through. Certain under the cursor, as before.
        /// </summary>
        public const float RestingOpacity = 0.5f;

        /// <summary>
        /// Brings the strip fully forward while the pointer is over it.
        ///
        /// Registered once per build on the host rather than on the strip, because the strip ignores
        /// the mouse and would never hear about it. Cheap: one rectangle test per mouse move, and it
        /// only touches the style when the answer changes.
        /// </summary>
        private void FadeIfPointerIsOver(MouseMoveEvent move)
        {
            lastPointer = move.mousePosition;
            ApplyFade(false);
        }

        /// <summary>
        /// Sets the strip's opacity from where the pointer was last seen.
        ///
        /// **`force` is what the reported bug needed and it is not an optimisation flag.** Pressing
        /// NEXT advances the step, which rebuilds the strip, and a fresh strip is built at
        /// <see cref="RestingOpacity"/> while <see cref="faded"/> still says the pointer is over it
        /// from a moment ago. The next mouse move then agrees with the stale field and returns
        /// early, so the bar the player is still pointing at stays half transparent until they take
        /// the cursor off it and bring it back. A tester reported exactly that, in those words.
        ///
        /// So the rebuild re-applies rather than assuming, and it does not wait for a mouse move:
        /// a player who clicks and holds the cursor still produces no further events at all.
        /// </summary>
        private void ApplyFade(bool force)
        {
            if (strip == null || strip.parent == null || float.IsNaN(lastPointer.x))
            {
                return;
            }

            var over = strip.worldBound.Contains(lastPointer);

            if (!force && over == faded)
            {
                return;
            }

            faded = over;
            strip.style.opacity = over ? 1f : RestingOpacity;
        }

        private bool faded;

        /// <summary>
        /// Where the mouse was last seen, in panel space.
        ///
        /// NaN until the pointer has moved once, because a strip built before the player has
        /// touched the mouse must stay at its resting opacity rather than guess at the origin.
        /// </summary>
        private Vector2 lastPointer = new(float.NaN, float.NaN);

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

            if (step == null)
            {
                return;
            }

            if (step.WaitForClick && step.Target == target)
            {
                Advance();
                return;
            }

            // **One step of lookahead, and no more.** The step that describes the model hub rings
            // the NEW MODEL door, and the step after it is the one waiting for that door. A player
            // who reads the line and presses the lit button was then told to press NEXT, and then
            // told to click a door they were already through.
            //
            // Only from a step that is not itself waiting for a click, so this can never carry a
            // player past a door the tour is holding them at, and only one step, so a stray screen
            // change cannot skip an act.
            if (!step.WaitForClick && NextStepWaitsFor(target))
            {
                Advance();
                Advance();
            }
        }

        /// <summary>
        /// The player turned a page of the model creator themselves, so the tour catches up.
        ///
        /// **There were two NEXT buttons on that screen and only one of them moved Emil.** The
        /// creator has its own DALEJ, which is the one a player presses because it is the one the
        /// page is about; the tour sat behind on the stage they had just left, describing a
        /// control that was no longer on screen. Reported after a full pass: "gdy gracz klika
        /// DALEJ, by sie samouczek kontynuowal, w koncu gracz jest w etapie FUNDAMENT".
        ///
        /// **What it will never do is carry the player past a step that is waiting on the game.**
        /// A step with a `Signal` stops the unwind dead, which is what keeps the run from being
        /// skipped: `create_start` waits for a training run to begin and no amount of page turning
        /// can get past it. Same for a step waiting on a click.
        /// </summary>
        public void PlayerTurnedCreatorPage(int stage)
        {
            var progressNow = progress();

            if (running != null || progressNow.Stage != GuideStage.Touring)
            {
                return;
            }

            // Bounded by the number of steps, so a malformed script cannot spin here.
            for (var guard = 0; guard < GuideScript.Steps.Count; guard++)
            {
                var step = Current;

                if (step == null
                    || step.CreatorStage < 0
                    || step.CreatorStage >= stage
                    || !string.IsNullOrEmpty(step.Signal)
                    || step.WaitForClick)
                {
                    return;
                }

                Advance();
            }
        }

        /// <summary>Whether the step after the current one is waiting for this screen to open.</summary>
        private bool NextStepWaitsFor(GuideTarget target)
        {
            var at = progress().Step + 1;

            if (at < 0 || at >= GuideScript.Steps.Count)
            {
                return false;
            }

            var next = GuideScript.Steps[at];
            return next.WaitForClick && next.Target == target;
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

            // **Skipping used to cost the player the free node.** The favour is handed over at the
            // step where he offers it, so pressing SKIP on the first screen walked past it, and
            // the task list went on saying to start researching against a tree where nothing was
            // affordable. He writes instead.
            skippedTheTour?.Invoke();

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
