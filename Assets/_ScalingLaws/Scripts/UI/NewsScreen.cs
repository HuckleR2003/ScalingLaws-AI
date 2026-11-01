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

        /// <summary>Ticking the early-word box, which the shell turns into a refresh.</summary>
        private readonly Action<bool> setEarlyAccess;

        /// <summary>Open the city map standing at a venue. The shell knows how to leave.</summary>
        private readonly Action<EventVenueId> showVenue;

        /// <summary>The stand window, while one is open. One at a time, like every sheet here.</summary>
        private VisualElement standSheet;

        /// <summary>
        /// Which desk has its benefits open, or none.
        ///
        /// **One at a time.** Three tiles with three paragraphs open is the wall of text this
        /// column was rebuilt to stop being, and the point of the button is that a player reads the
        /// case for the one they are actually considering.
        /// </summary>
        private IntelTier? benefitsFor;

        public NewsScreen(CompanySimulation simulation, Action<IntelTier, bool> setMembership,
            Action<bool> setEarlyAccess, Action<EventVenueId> showVenue = null)
        {
            this.simulation = simulation;
            this.setMembership = setMembership;
            this.setEarlyAccess = setEarlyAccess;
            this.showVenue = showVenue;

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

            var strap = new Label(Loc.T("news.strap"));

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
                ? Loc.T("news.spend", UiFormat.Money(monthly))
                : Loc.T("news.none.memberships"));

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

            column.Add(SectionTitle(Loc.T("news.latest"), "rule--wire"));

            var stories = simulation.State.News.In(NewsSection.Wire, 28);
            if (stories.Count == 0)
            {
                column.Add(Empty(Loc.T("news.none.wire")));

                return column;
            }

            var scroll = ScrollMemory.Keep(new ScrollView(), "news.wire");
            scroll.AddToClassList("news-scroll");

            foreach (var story in stories)
            {
                scroll.Add(WireRow(story));
            }

            column.Add(scroll);
            return column;
        }

        /// <summary>
        /// One line of the free wire.
        ///
        /// **This is the third story builder and the first two got the venue tag.** Reported
        /// on 2026-10-25 as event stories having no place tag and not being clickable, and
        /// measured before believing it: the announcements do reach the wire and they do carry
        /// a venue. They were drawn by a method that knew nothing about either, while
        /// `LeadStory` and `SmallStory` had both. A feature wired into two of three paths is
        /// the shape this project keeps finding.
        ///
        /// A row with a place is a `Button`, because it goes somewhere; a row without one is a
        /// `VisualElement`, because a control that reacts to the cursor and does nothing is
        /// worse than one that never looked clickable.
        /// </summary>
        private VisualElement WireRow(in NewsItem story)
        {
            var goes = story.Venue != null && showVenue != null;
            var asked = story.Venue ?? default;

            var row = goes
                ? new Button(() => showVenue(asked))
                : new VisualElement();

            row.AddToClassList("wire-row");
            row.EnableInClassList("wire-row--goes", goes);

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
            AddVenueTag(text, story);

            row.tooltip = story.Body;
            return row;
        }

        // ---- middle: scandals and premieres -----------------------------------------------------

        private VisualElement BuildMiddle()
        {
            var column = new VisualElement();
            column.AddToClassList("news-col");
            column.AddToClassList("news-col--middle");

            column.Add(Section(Loc.T("news.scandals"), "rule--scandal", NewsSection.Scandals, 4,
                Loc.T("news.none.scandals")));

            column.Add(Section(Loc.T("news.premieres"), "rule--premiere", NewsSection.Premieres, 4,
                Loc.T("news.none.premieres")));

            // **A section of their own, because they were drowning.** Measured on a campaign
            // in its opening months: two of seven stories on the wire were events, and in a
            // busy year they are two of twenty-eight lines that look identical to a rival
            // shipping something. A story that is filed, correct and indistinguishable from
            // the fourteen above it has not reached anybody.
            //
            // Two at most. This is a reminder that something is open, not a second calendar:
            // the one that lists everything is what the National Press desk sells.
            column.Add(BuildEventSection());

            return column;
        }

        /// <summary>The two most recent stories that are about a place in the city.</summary>
        private VisualElement BuildEventSection()
        {
            var block = new VisualElement();
            block.AddToClassList("news-section");

            block.Add(SectionTitle(Loc.T("news.events"), "rule--event"));

            var found = new List<NewsItem>();

            foreach (var story in simulation.State.News.In(NewsSection.Wire, 40))
            {
                if (story.Venue == null)
                {
                    continue;
                }

                found.Add(story);

                if (found.Count == 2)
                {
                    break;
                }
            }

            if (found.Count == 0)
            {
                block.Add(Empty(Loc.T("news.none.events")));

                return block;
            }

            block.Add(LeadStory(found[0]));

            for (var index = 1; index < found.Count; index++)
            {
                block.Add(SmallStory(found[index]));
            }

            return block;
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

        /// <summary>
        /// The place a story is about, as a tag under it, in that district's own colour.
        ///
        /// **Only when the story has one.** Almost none do, and a tag on a story about a token
        /// price would be a tag about nothing. Pressing it opens the city map standing at that
        /// venue, which is how a player gets from reading that an event exists to looking at
        /// where it is held.
        /// </summary>
        /// <summary>
        /// What pressing a story about a place should do.
        ///
        /// **The decision if there is one to make, the place otherwise.** A story about an event
        /// is about the event, so the useful answer is the stand window rather than a map of the
        /// city. When registration has closed there is nothing to decide and the map is the honest
        /// second best, which is also exactly what the tag does.
        /// </summary>
        /// <summary>
        /// Opens the stand window for one venue, from outside this screen.
        ///
        /// **The way back from the map.** The city scene has no company to sell a stand with, so
        /// the book button there records the venue and leaves; the shell calls this once the desk
        /// is on screen. One window, one place money moves, reached two ways.
        ///
        /// Silent when registration has closed, because by then there is nothing to open and a
        /// window saying so is a window the player has to dismiss for no reason.
        /// </summary>
        public void OpenStandWindowFor(EventVenueId venue)
        {
            if (simulation.TryUpcomingAt(venue, out var row))
            {
                OpenStandSheet(row);
            }
        }

        private void OpenTheDecisionAt(EventVenueId venue)
        {
            if (simulation.TryUpcomingAt(venue, out var row))
            {
                OpenStandSheet(row);

                return;
            }

            showVenue?.Invoke(venue);
        }

        private void AddVenueTag(VisualElement card, in NewsItem story)
        {
            if (story.Venue == null || showVenue == null)
            {
                return;
            }

            var venue = EventVenueCatalog.Get(story.Venue.Value);
            var asked = story.Venue.Value;

            // **The tag still goes to the place, and the card around it goes to the
            // decision.** Two different answers, which is the point of having both.
            var tag = new Button { text = venue.DisplayName.ToUpperInvariant() };

            // **Registered rather than passed to the constructor, so the event can be stopped.**
            // The card is a `Button` now as well, and a click that reached both would open the map
            // and the stand window from one press. A nested control has to keep its own click.
            tag.RegisterCallback<ClickEvent>(click =>
            {
                click.StopPropagation();
                showVenue(asked);
            });

            tag.AddToClassList("venuetag");
            card.Add(tag);
        }

        /// <summary>
        /// The large story at the head of a section.
        ///
        /// **It goes where its tag goes, when it has one.** Reported on 2026-10-25: an event story
        /// did not light up and could not be pressed, and only the small place tag underneath it
        /// could. A headline about a venue that refuses the click while the three words under it
        /// accept it reads as the headline being broken, and the tag is the smallest target on the
        /// card.
        /// </summary>
        private VisualElement LeadStory(in NewsItem story)
        {
            var asked = story.Venue ?? default;
            var goes = story.Venue != null && showVenue != null;

            var card = goes
                ? new Button(() => OpenTheDecisionAt(asked))
                : new VisualElement();

            card.AddToClassList("lead");
            card.EnableInClassList("story--goes", goes);

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

            AddVenueTag(card, story);

            return card;
        }

        /// <summary>The same, one size down. See <see cref="LeadStory"/> for why it is a button.</summary>
        private VisualElement SmallStory(in NewsItem story)
        {
            var asked = story.Venue ?? default;
            var goes = story.Venue != null && showVenue != null;

            var card = goes
                ? new Button(() => OpenTheDecisionAt(asked))
                : new VisualElement();

            card.AddToClassList("small");
            card.EnableInClassList("story--goes", goes);

            var head = new Label(story.Headline);
            head.AddToClassList("small__headline");
            head.EnableInClassList("small__headline--mine", story.IsAboutPlayer);
            card.Add(head);

            var meta = new Label(story.Date.ToString());
            meta.AddToClassList("small__meta");
            card.Add(meta);

            AddVenueTag(card, story);

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
                    panel.Add(Empty(Loc.T("news.none.paid")));
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
                panel.Add(BenefitsBlock(desk.Requires));
                AddEventsBlock(panel, desk);

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
                : Loc.T("news.desk.needs_two", NewsCatalog.OutletName(desk.AlsoRequires),
                    NewsCatalog.OutletName(desk.Requires)));

            lockNote.AddToClassList("desk__locked");
            panel.Add(lockNote);

            panel.Add(MembershipButton(missing, false));

            if (desk.NeedsTwo && !hasFirst)
            {
                var second = new Label(Loc.T("news.also_requires", NewsCatalog.OutletName(desk.AlsoRequires),
                    UiFormat.Money(IntelligenceService.MonthlyRetainerUsd(desk.AlsoRequires))));

                second.AddToClassList("desk__second");
                panel.Add(second);
            }

            panel.Add(BenefitsBlock(missing));
            AddEventsBlock(panel, desk);

            return panel;
        }


        /// <summary>
        /// The early-word box and the list of what is coming, under National Press.
        ///
        /// **Only on that desk, and gated on that one membership rather than on the section.**
        /// EVENT HUNTER needs two memberships to open, which this project has recorded as the
        /// awkward one: a player who paid National Press and still could not read it was owed an
        /// explanation. The early look fixes that from the other end. It is what the cheapest
        /// membership buys on its own, and until now that membership bought nothing a player
        /// could point at.
        ///
        /// Events themselves stay free to read about on the wire. What is sold here is a month of
        /// head start, which at three of the seven events in the year is the difference between a
        /// stand and an auction.
        /// </summary>
        private void AddEventsBlock(VisualElement panel, NewsDeskDefinition desk)
        {
            if (desk.Section != NewsSection.EventHunter)
            {
                return;
            }

            var paid = simulation.State.IsMember(IntelTier.NationalPress);

            if (paid)
            {
                var box = new VisualElement();
                box.AddToClassList("earlytoggle");

                var tick = new Toggle(Loc.T("early.toggle"))
                {
                    value = simulation.State.EventEarlyAccess
                };

                tick.RegisterValueChangedCallback(change => setEarlyAccess(change.newValue));
                box.Add(tick);

                var note = new Label(Loc.T("early.toggle.note"));
                note.AddToClassList("earlytoggle__note");
                box.Add(note);

                panel.Add(box);
            }

            panel.Add(UpcomingEventsList.Build(
                paid ? simulation.UpcomingEvents() : null,
                paid,
                NewsCatalog.OutletName(IntelTier.NationalPress),
                paid ? OpenStandSheet : null));
        }

        /// <summary>
        /// The stand window for one upcoming event, over the page.
        ///
        /// **This is the door that was missing, and it was the whole fault.** `EventVenuePanel` is
        /// a finished screen with tiles, prices and an auction, and on 2026-10-25 it had no caller
        /// anywhere in the game; `TryRegisterForStand` had none in `UI/` either. The simulation
        /// could sell a stand and no player could buy one.
        ///
        /// Over the page rather than as a screen of its own, because it is a decision about one
        /// line the player has just pressed and they are coming straight back to the list.
        /// </summary>
        private void OpenStandSheet(UpcomingEventRow row)
        {
            CloseStandSheet();

            var happening = CityEventCatalog.Get(row.Id);
            var venue = EventVenueCatalog.Get(happening.Venue);

            var panel = new EventVenuePanel(
                venue,
                happening,
                // **Asked of the simulation, never worked out here.** `StandsAt` is the one
                // reading, so the window cannot offer a stand the till will refuse.
                _ =>
                {
                    simulation.StandsAt(row.Id, row.Year, out var free, out var sellable,
                        out var interested);

                    return new EventStandInfo(free, sellable, interested);
                },
                tier => simulation.PriceOfStand(row.Id, row.Year, tier),
                tier =>
                {
                    if (simulation.TryRegisterForStand(row.Id, row.Year, tier, out var why))
                    {
                        CloseStandSheet();

                        // The list behind it is now wrong about how many stands are left.
                        Refresh();

                        return;
                    }

                    // The refusal is the simulation's own sentence. A second wording here is a
                    // second thing to keep true.
                    GateNotice.Says(happening.DisplayName, why);
                });

            standSheet = new VisualElement();
            standSheet.AddToClassList("shopsheet");
            standSheet.Add(panel.Root);

            var close = new Button(CloseStandSheet) { text = Loc.T("common.close") };
            close.AddToClassList("shopsheet__close");
            standSheet.Add(close);

            Root.Add(standSheet);
        }

        private void CloseStandSheet()
        {
            standSheet?.RemoveFromHierarchy();
            standSheet = null;
        }

        private Button MembershipButton(IntelTier tier, bool held)
        {
            var price = IntelligenceService.MonthlyRetainerUsd(tier);

            var button = new Button(() => setMembership(tier, !held))
            {
                text = held
                    ? Loc.T("news.cancel", NewsCatalog.OutletName(tier).ToUpperInvariant())
                    : Loc.T("news.join", NewsCatalog.OutletName(tier).ToUpperInvariant(),
                        UiFormat.Money(price))
            };

            button.AddToClassList("desk__join");
            button.EnableInClassList("desk__join--held", held);
            button.tooltip = NewsCatalog.OutletPitch(tier);
            return button;
        }

        /// <summary>
        /// The case for one membership, behind a button.
        ///
        /// The pitch above says what the outlet is. This says what it buys you, which is a
        /// different question and the one a player is actually asking with their hand on the
        /// money.
        /// </summary>
        private VisualElement BenefitsBlock(IntelTier tier)
        {
            var open = benefitsFor == tier;

            return IntelBenefits.Block(tier, open, () =>
            {
                benefitsFor = open ? null : tier;
                Refresh();
            });
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
