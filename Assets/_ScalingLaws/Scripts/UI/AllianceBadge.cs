using System;
using System.Collections.Generic;
using System.Text;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the company has going with other labs, in the corner of the top bar.
    ///
    /// **On the right, and the effects are on the left, which is the whole point.** The left of that
    /// bar is what is temporarily true about the company and mostly happens to it: a viral window, a
    /// backlash, a campaign running down. This is the opposite kind of fact. It is permanent until
    /// somebody ends it, it was agreed rather than suffered, and it belongs to two companies. Two
    /// kinds of fact sharing a corner would teach the player that the corner means nothing.
    ///
    /// **It is a button, not a badge.** The effect strip carries hover cards and nothing else, which
    /// is why it can be rebuilt under the cursor without costing anybody a click. This one has
    /// somewhere to go, so it is built once and repointed, the rule the tutorial strip taught this
    /// project after four separate reports of a button that did not work.
    /// </summary>
    public sealed class AllianceBadge
    {
        /// <summary>
        /// Lines listed in the card before it stops being a card and starts being a table.
        ///
        /// Past this the count says how many more, which is the same shape the effect strip uses
        /// for the same reason.
        /// </summary>
        public const int MostListed = 4;

        private readonly Label count;

        /// <summary>What the card lists, as of the last refresh.</summary>
        private IReadOnlyList<string> carried = System.Array.Empty<string>();

        public AllianceBadge(Action opened)
        {
            Root = new Button(() => opened?.Invoke());
            Root.AddToClassList("ally");

            // **The author's own mark, and it is loaded rather than drawn.** Every loader in this
            // project answers a missing file with something that still reads, so a badge with no
            // art is the count and the word and nothing is broken about it.
            var art = UnityEngine.Resources.Load<UnityEngine.Texture2D>("Ui/partnership");

            if (art != null)
            {
                var mark = new VisualElement();
                mark.AddToClassList("ally__mark");
                mark.style.backgroundImage = new StyleBackground(art);
                mark.pickingMode = PickingMode.Ignore;
                Root.Add(mark);
            }

            count = new Label();
            count.AddToClassList("ally__count");
            count.pickingMode = PickingMode.Ignore;
            Root.Add(count);

            var word = new Label(Loc.T("ally.badge"));
            word.AddToClassList("ally__word");
            word.pickingMode = PickingMode.Ignore;
            Root.Add(word);

            // **Attached once, and the words are read when the cursor arrives.** Calling the plain
            // `Attach` from `Refresh` would register another handler every tick, and after a
            // minute of play one hover would open the card forty times.
            InsightTip.AttachLive(Root, () => Loc.T("ally.card.title"), () => Body(carried));
        }

        public Button Root { get; }

        /// <summary>How many things are being carried. For the tests, which have no panel.</summary>
        public int Carrying { get; private set; }

        /// <summary>
        /// Repoints the badge at whatever the company has going today.
        ///
        /// Hidden entirely when there is nothing, because a corner reading "0" is a corner telling
        /// the player about a system they have not met yet.
        /// </summary>
        public void Refresh(CompanySimulation simulation)
        {
            if (simulation == null)
            {
                return;
            }

            var state = simulation.State;
            var lines = new List<string>();

            foreach (var pair in state.Alliances.Signed)
            {
                lines.Add(Loc.T("ally.line.level",
                    CompetitorCatalog.NameOf(pair.Key), pair.Value.ToString()));
            }

            foreach (var deal in state.Deals)
            {
                if (!deal.IsLiveOn(state.Date))
                {
                    continue;
                }

                lines.Add(Loc.T("ally.line.deal",
                    CompetitorCatalog.NameOf(deal.Lab),
                    RelationOfferCatalog.Get(deal.Offer).DisplayName,
                    Math.Max(0, deal.Ends.DayIndex - state.Date.DayIndex).ToString()));
            }

            if (state.Campaign != null && state.Campaign.IsLiveOn(state.Date))
            {
                lines.Add(Loc.T("ally.line.campaign",
                    state.Campaign.DaysLeft(state.Date).ToString()));
            }

            Carrying = lines.Count;
            carried = lines;

            // Hidden entirely rather than drawn empty, so a hidden badge also takes no hover.
            Root.style.display = lines.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            if (lines.Count > 0)
            {
                count.text = lines.Count.ToString();
            }
        }

        /// <summary>
        /// The card: every arrangement on its own line, then the way in.
        ///
        /// **The last line is the one that makes this worth hovering.** A card that lists what is
        /// running and stops is a card the player reads once; one that says where the terms, the
        /// costs and the dates are is a card that gets used.
        /// </summary>
        private static string Body(IReadOnlyList<string> lines)
        {
            var text = new StringBuilder();

            for (var index = 0; index < lines.Count && index < MostListed; index++)
            {
                text.Append(lines[index]).Append('\n');
            }

            if (lines.Count > MostListed)
            {
                text.Append(Loc.T("ally.card.more", (lines.Count - MostListed).ToString()))
                    .Append('\n');
            }

            text.Append('\n').Append(Loc.T("ally.card.open"));

            return text.ToString();
        }
    }
}
