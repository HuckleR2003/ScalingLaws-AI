using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The news, laid out as a publication rather than as a log.
    ///
    /// Three columns, because that is what makes it read as somewhere the player goes to find things
    /// out rather than a list of strings with dates on. The wire runs down the left in strict order.
    /// The middle carries the two cuts worth checking daily: who is in trouble and what shipped. The
    /// right is the part somebody is charging for.
    ///
    /// **The typography is the design.** One lead story per section set large with a kicker over it,
    /// the rest small underneath, hairlines instead of boxes, a coloured rule over each section
    /// title. That hierarchy is why a newspaper is readable at a glance and a table is not, and it
    /// costs nothing but discipline about which single story is the biggest one on the page.
    ///
    /// Nothing on this screen decides anything. The feed is filled by <see cref="NewsDesk"/> in the
    /// simulation, so what the player reads and what the game did cannot drift apart.
    /// </summary>
    public sealed class NewsScreen
    {
        private readonly CompanySimulation simulation;
        private readonly Action<IntelTier, bool> setMembership;

        public NewsScreen(CompanySimulation simulation, Action<IntelTier, bool> setMembership)
        {
            this.simulation = simulation;
            this.setMembership = setMembership;

            Root = new VisualElement();
            Root.AddToClassList("content");
            Root.AddToClassList("news");
        }

        public VisualElement Root { get; }

        public void Refresh()
        {
            Root.Clear();
            simulation.State.News.MarkRead();

            Root.Add(BuildMasthead());

            var columns = new VisualElement();
            columns.AddToClassList("news__columns");

            columns.Add(BuildWire());
            columns.Add(BuildMiddle());
            columns.Add(BuildDesks());

            Root.Add(columns);
        }

        /// <summary>The name of the paper, the date, and what the company is paying for it.</summary>
        private VisualElement BuildMasthead()
        {
            var state = simulation.State;

            var bar = new VisualElement();
            bar.AddToClassList("news__masthead");

            var left = new VisualElement();

            var title = new Label(Loc.T("news.the_wire"));
            title.AddToClassList("news__title");
            left.Add(title);

            var strap = new Label("Everything that happened, and what somebody will sell you about "
                + "what happens next.");

            strap.AddToClassList("news__strap");
            left.Add(strap);
            bar.Add(left);

            var right = new VisualElement();
            right.AddToClassList("news__mastright");

            var date = new Label(state.Date.ToString().ToUpperInvariant());
            date.AddToClassList("news__date");
            right.Add(date);

            var monthly = simulation.MonthlyIntelRetainerUsd();
            var spend = new Label(monthly > 0L
                ? UiFormat.Money(monthly) + " a month on memberships"
                : "No memberships. The right hand column is closed.");

            spend.AddToClassList("news__spend");
            right.Add(spend);

            bar.Add(right);
            return bar;
        }

        // ---- left: the wire ------------------------------------------------------------------

        private VisualElement BuildWire()
        {
            var column = new VisualElement();
            column.AddToClassList("news-col");
            column.AddToClassList("news-col--wire");

            column.Add(SectionTitle("LATEST", "rule--wire"));

            var stories = simulation.State.News.In(NewsSection.Wire, 28);
            if (stories.Count == 0)
            {
                column.Add(Empty("Nothing filed yet. The wire fills as the company and the field do "
                    + "things worth reporting."));

                return column;
            }

            var scroll = new ScrollView();
            scroll.AddToClassList("news-scroll");

            foreach (var story in stories)
            {
                scroll.Add(WireRow(story));
            }

            column.Add(scroll);
            return column;
        }

        private static VisualElement WireRow(in NewsItem story)
        {
            var row = new VisualElement();
            row.AddToClassList("wire-row");

            var rule = new VisualElement();
            rule.AddToClassList("wire-row__rule");
            rule.AddToClassList(RuleClassFor(story.Section));
            row.Add(rule);

            var text = new VisualElement();
            text.AddToClassList("wire-row__text");

            var head = new Label(story.Headline);
            head.AddToClassList("wire-row__headline");
            head.EnableInClassList("wire-row__headline--mine", story.IsAboutPlayer);
            text.Add(head);

            var meta = new Label(story.Date.ToString()
                + (story.Outlet.Length > 0 ? "  ·  " + story.Outlet : string.Empty));

            meta.AddToClassList("wire-row__meta");
            text.Add(meta);

            row.Add(text);
            row.tooltip = story.Body;
            return row;
        }

        // ---- middle: scandals and premieres -----------------------------------------------------

        private VisualElement BuildMiddle()
        {
            var column = new VisualElement();
            column.AddToClassList("news-col");
            column.AddToClassList("news-col--middle");

            column.Add(Section("SCANDALS", "rule--scandal", NewsSection.Scandals, 4,
                "Nothing has gone wrong yet. It will."));

            column.Add(Section("PREMIERES", "rule--premiere", NewsSection.Premieres, 4,
                "No launches yet, yours or theirs."));

            return column;
        }

        /// <summary>
        /// One section: a lead story set large, then the rest small.
        ///
        /// The size difference is doing real work. Four stories at the same weight is a list and the
        /// eye has to read all four to find the one that matters; one large and three small is a page
        /// and the eye is told.
        /// </summary>
        private VisualElement Section(string title, string ruleClass, NewsSection section, int most,
            string emptyNote)
        {
            var block = new VisualElement();
            block.AddToClassList("news-section");

            block.Add(SectionTitle(title, ruleClass));

            var stories = simulation.State.News.In(section, most);
            if (stories.Count == 0)
            {
                block.Add(Empty(emptyNote));
                return block;
            }

            block.Add(LeadStory(stories[0]));

            for (var index = 1; index < stories.Count; index++)
            {
                block.Add(SmallStory(stories[index]));
            }

            return block;
        }

        private static VisualElement LeadStory(in NewsItem story)
        {
            var card = new VisualElement();
            card.AddToClassList("lead");

            var kicker = new Label(story.IsAboutPlayer ? "ABOUT YOU" : story.Outlet.ToUpperInvariant());
            kicker.AddToClassList("lead__kicker");
            kicker.EnableInClassList("lead__kicker--mine", story.IsAboutPlayer);
            card.Add(kicker);

            var head = new Label(story.Headline);
            head.AddToClassList("lead__headline");
            card.Add(head);

            var body = new Label(story.Body);
            body.AddToClassList("lead__body");
            card.Add(body);

            var date = new Label(story.Date.ToString());
            date.AddToClassList("lead__date");
            card.Add(date);

            return card;
        }

        private static VisualElement SmallStory(in NewsItem story)
        {
            var card = new VisualElement();
            card.AddToClassList("small");

            var head = new Label(story.Headline);
            head.AddToClassList("small__headline");
            head.EnableInClassList("small__headline--mine", story.IsAboutPlayer);
            card.Add(head);

            var meta = new Label(story.Date.ToString());
            meta.AddToClassList("small__meta");
            card.Add(meta);

            card.tooltip = story.Body;
            return card;
        }

        // ---- right: the three paid desks ----------------------------------------------------------

        private VisualElement BuildDesks()
        {
            var column = new VisualElement();
            column.AddToClassList("news-col");
            column.AddToClassList("news-col--desks");

            foreach (var desk in NewsCatalog.PaidDesks)
            {
                column.Add(BuildDesk(desk));
            }

            return column;
        }

        private VisualElement BuildDesk(NewsDeskDefinition desk)
        {
            var state = simulation.State;

            var hasFirst = state.IsMember(desk.Requires);
            var hasSecond = !desk.NeedsTwo || state.IsMember(desk.AlsoRequires);
            var open = hasFirst && hasSecond;

            var panel = new VisualElement();
            panel.AddToClassList("desk");
            panel.AddToClassList(DeskClassFor(desk.Section));
            panel.EnableInClassList("desk--shut", !open);

            var head = new VisualElement();
            head.AddToClassList("desk__head");

            var title = new Label(desk.Title);
            title.AddToClassList("desk__title");
            head.Add(title);

            var outlet = new Label(desk.Outlet);
            outlet.AddToClassList("desk__outlet");
            head.Add(outlet);

            panel.Add(head);

            if (open)
            {
                var stories = state.News.In(desk.Section, 4);
                if (stories.Count == 0)
                {
                    panel.Add(Empty("Paid up. The desk files when it has something, not on a "
                        + "schedule you set."));
                }
                else
                {
                    panel.Add(LeadStory(stories[0]));
                    for (var index = 1; index < stories.Count; index++)
                    {
                        panel.Add(SmallStory(stories[index]));
                    }
                }

                panel.Add(MembershipButton(desk.Requires, true));
                return panel;
            }

            var pitch = new Label(desk.Pitch);
            pitch.AddToClassList("desk__pitch");
            panel.Add(pitch);

            // Which membership is actually missing, named. Event Hunter is the awkward one: National
            // Press sells the section and it only opens for TrendSearch members, so a player who has
            // paid National Press and still cannot read it is owed the reason in plain words.
            var missing = !hasFirst ? desk.Requires : desk.AlsoRequires;

            var lockNote = new Label(!hasFirst
                ? desk.LockedNote
                : $"Requires {NewsCatalog.OutletName(desk.AlsoRequires)} membership. "
                    + $"{NewsCatalog.OutletName(desk.Requires)} is paid and it is not enough on its own.");

            lockNote.AddToClassList("desk__locked");
            panel.Add(lockNote);

            panel.Add(MembershipButton(missing, false));

            if (desk.NeedsTwo && !hasFirst)
            {
                var second = new Label($"And then {NewsCatalog.OutletName(desk.AlsoRequires)} on top "
                    + $"of it, at {UiFormat.Money(IntelligenceService.MonthlyRetainerUsd(desk.AlsoRequires))} "
                    + "a month.");

                second.AddToClassList("desk__second");
                panel.Add(second);
            }

            return panel;
        }

        private Button MembershipButton(IntelTier tier, bool held)
        {
            var price = IntelligenceService.MonthlyRetainerUsd(tier);

            var button = new Button(() => setMembership(tier, !held))
            {
                text = held
                    ? $"CANCEL {NewsCatalog.OutletName(tier).ToUpperInvariant()}"
                    : $"JOIN {NewsCatalog.OutletName(tier).ToUpperInvariant()}  {UiFormat.Money(price)}/MO"
            };

            button.AddToClassList("desk__join");
            button.EnableInClassList("desk__join--held", held);
            button.tooltip = NewsCatalog.OutletPitch(tier);
            return button;
        }

        // ---- small pieces ---------------------------------------------------------------------------

        private static VisualElement SectionTitle(string text, string ruleClass)
        {
            var block = new VisualElement();
            block.AddToClassList("sec");

            var rule = new VisualElement();
            rule.AddToClassList("sec__rule");
            rule.AddToClassList(ruleClass);
            block.Add(rule);

            var label = new Label(text);
            label.AddToClassList("sec__title");
            block.Add(label);

            return block;
        }

        private static Label Empty(string text)
        {
            var label = new Label(text);
            label.AddToClassList("news-empty");
            return label;
        }

        private static string RuleClassFor(NewsSection section) => section switch
        {
            NewsSection.Scandals => "rule--scandal",
            NewsSection.Premieres => "rule--premiere",
            NewsSection.TotalTrueNews => "rule--gold",
            NewsSection.ItSpy => "rule--blue",
            NewsSection.EventHunter => "rule--violet",
            _ => "rule--wire"
        };

        private static string DeskClassFor(NewsSection section) => section switch
        {
            NewsSection.TotalTrueNews => "desk--gold",
            NewsSection.ItSpy => "desk--blue",
            _ => "desk--violet"
        };
    }
}
