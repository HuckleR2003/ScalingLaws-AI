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
