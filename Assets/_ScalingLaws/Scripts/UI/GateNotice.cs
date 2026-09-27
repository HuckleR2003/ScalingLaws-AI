using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The answer to clicking something the company cannot use yet.
    ///
    /// **Every locked control in this game was silent.** A shut precision card, a shut deduplication
    /// pass, a shut upgrade tile: each printed the name of the node it needs in small grey type on
    /// its own face, and then a click on it did nothing at all. Worse than nothing, in two cases the
    /// card was `SetEnabled(false)`, so the click was never dispatched and the player had no way of
    /// telling a locked control from a broken one. That is the reading a playtest gives it.
    ///
    /// **Why this is not `StartedNotice`.** That one is an announcement: it arrives at the top of
    /// the screen, holds for three seconds and leaves, and nothing may depend on having read it.
    /// This is a reply to something the player just did, so it belongs under the control they
    /// clicked, it stays until they deal with it, and it carries the one button that acts on it.
    /// Different anchor, different lifetime, different job, and a timed announcement carrying a
    /// button is a button that disappears while the cursor is travelling towards it.
    ///
    /// **It never decides anything.** The caller says what is locked and which nodes would open it;
    /// pressing the button hands those nodes back through <see cref="Wanted"/>, and what to do about
    /// them is the shell's business.
    /// </summary>
    public sealed class GateNotice
    {
        /// <summary>Where the notice mounts. Set once by <c>UiBootstrap.Prepare</c>.</summary>
        public static VisualElement Host { get; set; }

        /// <summary>The notice on screen, or null. For tests, which have no eyes.</summary>
        public static VisualElement Frame => frame;

        /// <summary>
        /// What the player asked to be shown, and what should happen next.
        ///
        /// A delegate rather than a reference to the shell, for the reason every other element here
        /// follows: `UI/` may draw the game and may not know how to navigate it.
        /// </summary>
        public static Action<IReadOnlyList<ResearchNodeId>> Wanted { get; set; }

        private static VisualElement frame;

        /// <summary>What the notice on screen is offering, or null when it offers nothing.</summary>
        private static List<ResearchNodeId> offered;

        /// <summary>Where an alliance stands, for the bar on a decision card.</summary>
        public readonly struct AllianceProgress
        {
            public AllianceProgress(string nowText, string nextText, double share, string note)
            {
                NowText = nowText;
                NextText = nextText;
                Share = share;
                Note = note;
            }

            public string NowText { get; }
            public string NextText { get; }
            public double Share { get; }
            public string Note { get; }
        }

        /// <summary>
        /// Says that a control needs research, and offers the way to it.
        ///
        /// <paramref name="subject"/> is what was clicked, in the player's words, so the sentence
        /// can name it rather than saying "this". Nodes already researched are dropped, and a call
        /// with nothing left to research draws nothing: a notice explaining that a finished node is
        /// missing is worse than the silence it replaced.
        /// </summary>
        public static void NeedsResearch(string subject, IReadOnlyList<ResearchNodeId> nodes,
            Func<ResearchNodeId, bool> hasResearch)
        {
            if (Host == null || nodes == null || nodes.Count == 0)
            {
                return;
            }

            var wanted = new List<ResearchNodeId>();

            foreach (var node in nodes)
            {
                if (node != ResearchNodeId.None && hasResearch != null && !hasResearch(node)
                    && !wanted.Contains(node))
                {
                    wanted.Add(node);
                }
            }

            if (wanted.Count == 0)
            {
                return;
            }

            Build(subject, wanted);
        }

        /// <summary>
        /// The other half: something is missing and no research would fix it.
        ///
        /// The creator's own refusals go through here, which is why this class is not called
        /// something with "research" in the name. A player who presses CONTINUE with an empty name
        /// gets a button that does nothing and no sentence anywhere, which is the identical fault
        /// one screen earlier.
        /// </summary>
        public static void Says(string subject, string sentence)
        {
            if (Host == null || string.IsNullOrEmpty(sentence))
            {
                return;
            }

            Build(subject, sentence, null);
        }


        /// <summary>
        /// A decision put to the player: a sentence, the facts under it, and two ways to answer.
        ///
        /// **The same card as the rest of this class and that is deliberate.** A locked control, a
        /// creator page that will not continue and a lab asking to carry on are three different
        /// subjects with one shape in common: something answered the player, it sits under whatever
        /// they were looking at, and it stays until they deal with it. Three separate notices with
        /// that lifetime would be three places to get the click-eating wrong, which this project
        /// has already shipped twice.
        ///
        /// It differs from the announcement notice in every way that matters: that one arrives at
        /// the top, holds three seconds and leaves, and **a timed announcement carrying two buttons
        /// is two buttons that vanish while the cursor is travelling towards them**.
        /// </summary>
        public static void Decide(string kicker, string sentence,
            IReadOnlyList<(string Label, string Value)> rows,
            string yesText, Action yes, string noText, Action no,
            AllianceProgress? progress = null)
        {
            if (Host == null || string.IsNullOrEmpty(sentence))
            {
                return;
            }

            Hide();

            frame = new VisualElement();
            frame.AddToClassList("gate");
            frame.AddToClassList("gate--decide");
            frame.pickingMode = PickingMode.Ignore;

            var head = new VisualElement();
            head.AddToClassList("gate__head");
            head.pickingMode = PickingMode.Ignore;

            var top = new Label(kicker ?? string.Empty);
            top.AddToClassList("gate__kicker");
            head.Add(top);
            frame.Add(head);

            var line = new Label(sentence);
            line.AddToClassList("gate__line");
            frame.Add(line);

            if (rows != null && rows.Count > 0)
            {
                frame.Add(Tile(rows));
            }

            if (progress.HasValue)
            {
                frame.Add(Ladder(progress.Value));
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("gate__buttons");

            var take = new Button(() =>
            {
                Hide();
                yes?.Invoke();
            })
            { text = yesText };

            take.AddToClassList("gate__go");
            take.AddToClassList("gate__go--half");
            buttons.Add(take);

            var leave = new Button(() =>
            {
                Hide();
                no?.Invoke();
            })
            { text = noText };

            leave.AddToClassList("gate__later");
            buttons.Add(leave);

            frame.Add(buttons);
            Host.Add(frame);

            frame.AddToClassList("gate--arriving");
            frame.schedule.Execute(() => frame?.RemoveFromClassList("gate--arriving")).ExecuteLater(16);
        }

        /// <summary>The facts, in the same shape the ranking board states them.</summary>
        private static VisualElement Tile(IReadOnlyList<(string Label, string Value)> rows)
        {
            var tile = new VisualElement();
            tile.AddToClassList("gate__tile");
            tile.pickingMode = PickingMode.Ignore;

            foreach (var (label, value) in rows)
            {
                var row = new VisualElement();
                row.AddToClassList("gate__tilerow");
                row.pickingMode = PickingMode.Ignore;

                var left = new Label(label);
                left.AddToClassList("gate__rowlabel");
                row.Add(left);

                var right = new Label(value);
                right.AddToClassList("gate__rowvalue");
                row.Add(right);

                tile.Add(row);
            }

            return tile;
        }

        /// <summary>
        /// Where the alliance with them stands, and how far it is to the next rung.
        ///
        /// **Current level on the left, the one being worked towards on the right, and the bar
        /// between them fills with days.** Asked for by name, and it is the right thing to put on
        /// this card in particular: a company deciding whether to carry on with somebody is exactly
        /// the moment the answer to "where is this going" is worth having in front of them.
        /// </summary>
        private static VisualElement Ladder(AllianceProgress progress)
        {
            var block = new VisualElement();
            block.AddToClassList("gate__ladder");
            block.pickingMode = PickingMode.Ignore;

            var row = new VisualElement();
            row.AddToClassList("gate__ladderrow");
            row.pickingMode = PickingMode.Ignore;

            var now = new Label(progress.NowText);
            now.AddToClassList("gate__laddernow");
            row.Add(now);

            var track = new VisualElement();
            track.AddToClassList("gate__laddertrack");
            track.pickingMode = PickingMode.Ignore;

            var fill = new VisualElement();
            fill.AddToClassList("gate__ladderfill");
            fill.pickingMode = PickingMode.Ignore;
            fill.style.width = new StyleLength(
                Length.Percent((float)(Math.Clamp(progress.Share, 0.0, 1.0) * 100.0)));

            track.Add(fill);
            row.Add(track);

            var next = new Label(progress.NextText);
            next.AddToClassList("gate__laddernext");
            row.Add(next);

            block.Add(row);

            if (!string.IsNullOrEmpty(progress.Note))
            {
                var note = new Label(progress.Note);
                note.AddToClassList("gate__laddernote");
                note.pickingMode = PickingMode.Ignore;
                block.Add(note);
            }

            return block;
        }

        /// <summary>Takes it down. Safe to call when nothing is up, which is the common case.</summary>
        public static void Hide()
        {
            frame?.RemoveFromHierarchy();
            frame = null;
            offered = null;
        }

        private static void Build(string subject, IReadOnlyList<ResearchNodeId> nodes) =>
            Build(subject, Loc.T("gate.needs_research"), nodes);

        private static void Build(string subject, string sentence, IReadOnlyList<ResearchNodeId> nodes)
        {
            Hide();

            frame = new VisualElement();
            frame.AddToClassList("gate");

            // **The close button is the only thing here that takes a pointer.** The notice sits at
            // the foot of the screen, over whatever the page has put there, and this project has
            // shipped two invisible click eaters already.
            frame.pickingMode = PickingMode.Ignore;

            var head = new VisualElement();
            head.AddToClassList("gate__head");
            head.pickingMode = PickingMode.Ignore;

            var kicker = new Label(string.IsNullOrEmpty(subject)
                ? Loc.T("gate.kicker")
                : subject.ToUpperInvariant());

            kicker.AddToClassList("gate__kicker");
            head.Add(kicker);

            var close = new Button(Hide) { text = Loc.T("gate.close") };
            close.AddToClassList("gate__close");
            head.Add(close);

            frame.Add(head);

            var line = new Label(sentence);
            line.AddToClassList("gate__line");
            frame.Add(line);

            if (nodes != null && nodes.Count > 0)
            {
                frame.Add(BuildResearch(nodes));
            }

            Host.Add(frame);

            // Born low and released a frame later, so it rises into place rather than appearing.
            frame.AddToClassList("gate--arriving");
            frame.schedule.Execute(() => frame?.RemoveFromClassList("gate--arriving")).ExecuteLater(16);
        }

        /// <summary>
        /// The icon, the name of what is missing, and one wide button under it.
        ///
        /// **The icon is the node's own**, not a generic research glyph, because the board the
        /// button leads to draws the same picture and the player is about to go looking for it.
        /// A missing file yields an empty plate rather than an exception, the rule every loader in
        /// this project follows.
        /// </summary>
        private static VisualElement BuildResearch(IReadOnlyList<ResearchNodeId> nodes)
        {
            var block = new VisualElement();
            block.AddToClassList("gate__research");
            block.pickingMode = PickingMode.Ignore;

            var row = new VisualElement();
            row.AddToClassList("gate__row");
            row.pickingMode = PickingMode.Ignore;

            var plate = new VisualElement();
            plate.AddToClassList("gate__icon");
            plate.pickingMode = PickingMode.Ignore;

            var art = ResearchIcons.Get(nodes[0]);

            if (art != null)
            {
                plate.style.backgroundImage = new StyleBackground(art);
            }

            row.Add(plate);

            var words = new VisualElement();
            words.AddToClassList("gate__words");
            words.pickingMode = PickingMode.Ignore;

            var caption = new Label(nodes.Count == 1
                ? Loc.T("gate.one")
                : Loc.T("gate.several", nodes.Count.ToString()));

            caption.AddToClassList("gate__caption");
            words.Add(caption);

            var name = new Label(Names(nodes));
            name.AddToClassList("gate__name");
            words.Add(name);

            row.Add(words);
            block.Add(row);

            offered = new List<ResearchNodeId>(nodes);

            var go = new Button(TakeTheOffer) { text = Loc.T("gate.go") };

            go.AddToClassList("gate__go");
            block.Add(go);

            return block;
        }

        /// <summary>
        /// Takes the offer: the notice closes and the nodes go back to whoever is listening.
        ///
        /// **Its own method because a test cannot press a button.** An EditMode element has no
        /// panel, so a click sent to one is never dispatched and the lambda behind it goes
        /// unmeasured. Same shape as `ManagementScreen.ShowDesk` and `PartsShop.Order`, and the
        /// button calls exactly this.
        /// </summary>
        public static void TakeTheOffer()
        {
            var nodes = offered;

            Hide();

            if (nodes != null && nodes.Count > 0)
            {
                Wanted?.Invoke(nodes);
            }
        }

        /// <summary>The names of what is missing, on one line, in the order the caller gave them.</summary>
        private static string Names(IReadOnlyList<ResearchNodeId> nodes)
        {
            var names = new string[nodes.Count];

            for (var index = 0; index < nodes.Count; index++)
            {
                names[index] = ResearchTree.Get(nodes[index]).DisplayName;
            }

            return string.Join(Loc.T("gate.join"), names);
        }
    }
}
