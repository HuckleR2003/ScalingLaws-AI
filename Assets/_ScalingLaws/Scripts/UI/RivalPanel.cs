using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// How a lab feels about you, why, and who works there.
    ///
    /// **Its own file rather than four hundred more lines of `GameShell`.** That file is already the
    /// largest in the project by a wide margin, and the reason it is large is that every screen it
    /// ever grew got added to it rather than beside it. This is the shape the rest of it should be
    /// moving toward.
    ///
    /// Nothing here decides anything. The offer goes through `CompanySimulation.TryPoach`, because
    /// that is where money moves and where a relationship is recorded, and the panel only draws what
    /// comes back.
    /// </summary>
    public sealed class RivalPanel
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        /// <summary>Which person has an offer form open, or -1 for none.</summary>
        private int openOffer = -1;

        /// <summary>The bonus on the form, kept between repaints so typing is not lost.</summary>
        private long bonusUsd;

        /// <summary>What happened to the last offer, and who it was about.</summary>
        private string outcomeNote = string.Empty;

        /// <summary>Set when a refusal was reported, so the call can be answered.</summary>
        private CompetitorId? callFrom;

        /// <summary>
        /// Which half of the card is open.
        ///
        /// **Three sections stacked was four screens of scrolling in a card with a fixed height.**
        /// The standing, the history, a roster of up to twelve people and the whole smear desk were
        /// all mounted at once, so reading what a rival thought of you meant scrolling past a list
        /// of their employees. They are the same three things, one at a time.
        /// </summary>
        private RivalTab tab = RivalTab.Standing;

        /// <summary>The three things a player can do with a rival, in the order they think of them.</summary>
        private enum RivalTab
        {
            Standing = 0,
            People = 1,
            Actions = 2,

            /// <summary>What the two of you have signed, and the four ways to add to it.</summary>
            Together = 3
        }

        public RivalPanel(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        /// <summary>
        /// Opens the roster.
        ///
        /// **For tooling rather than for the game**, the same reason `GameShell` carries
        /// `OpenScreenByName`. A test has no panel, so a click on a tab is never dispatched, and a
        /// fixture that could not reach the roster would either be deleted or quietly rewritten to
        /// assert less. `ScreenProofTests` uses it to photograph the section as well.
        /// </summary>
        public void ShowPeople() => tab = RivalTab.People;

        /// <summary>Opens the desk where a rival's name is damaged. Tooling, as above.</summary>
        public void ShowActions() => tab = RivalTab.Actions;

        /// <summary>Forgets any open form. Called when the card is closed or another lab opened.</summary>
        public void Reset()
        {
            openOffer = -1;
            bonusUsd = 0;
            outcomeNote = string.Empty;
            callFrom = null;
            tab = RivalTab.Standing;
        }

        /// <summary>
        /// The whole block, in its own scroller.
        ///
        /// **The card it mounts into has a fixed top and bottom.** A roster of twelve people added
        /// to a fixed-height card does not overflow in UI Toolkit, it squashes every child until the
        /// names sit on top of their own rows, which is the deformation this project has already
        /// shipped once. The scroller is the floor against that, and every block inside states
        /// `flex-shrink: 0` for the same reason.
        /// </summary>
        /// <summary>
        /// Opens the section about what the two companies have signed.
        ///
        /// **Exists so a test and a render can reach it**, the same seam `ManagementScreen.ShowDesk`
        /// and `PartsShop.Order` exist for: an EditMode element has no panel, so a click sent to the
        /// tab button is never dispatched and everything behind it goes unmeasured.
        /// </summary>
        public void ShowTogether() => tab = RivalTab.Together;

        public VisualElement Build(CompetitorId lab, Func<VisualElement> actions = null)
        {
            var simulation = company();
            var block = ScrollMemory.Keep(new ScrollView(), "rival." + lab);
            block.AddToClassList("rival");
            block.verticalScrollerVisibility = ScrollerVisibility.Auto;
            block.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            block.Add(BuildTabs());

            switch (tab)
            {
                case RivalTab.People:
                    block.Add(BuildRoster(simulation, lab));
                    break;

                case RivalTab.Together:
                    block.Add(BuildTogether(simulation, lab));
                    break;

                case RivalTab.Actions:
                    if (actions != null)
                    {
                        block.Add(actions());
                    }

                    break;

                default:
                    block.Add(BuildStanding(simulation, lab));

                    var history = simulation.State.Relations.HistoryWith(lab);

                    if (history.Count > 0)
                    {
                        block.Add(BuildHistory(history));
                    }

                    break;
            }

            // The call belongs to whichever section is open: a refusal is an answer to an offer
            // the player made, and hiding it behind a tab switch would lose it.
            if (callFrom.HasValue && callFrom.Value == lab)
            {
                block.Add(BuildCall(simulation, lab));
            }

            return block;
        }

        /// <summary>The three buttons across the top of the card.</summary>
        private VisualElement BuildTabs()
        {
            var strip = new VisualElement();
            strip.AddToClassList("rival__tabs");

            strip.Add(TabButton(RivalTab.Standing, Loc.T("relation.title")));
            strip.Add(TabButton(RivalTab.People, Loc.T("poach.title")));
            strip.Add(TabButton(RivalTab.Actions, Loc.T("smear.title")));
            strip.Add(TabButton(RivalTab.Together, Loc.T("together.title")));

            return strip;
        }

        private Button TabButton(RivalTab which, string text)
        {
            var button = new Button(() =>
            {
                tab = which;

                // A half-typed offer belongs to the section it was typed in. Leaving it open across
                // a tab switch would let a bonus meant for one person be sent from another screen.
                openOffer = -1;
                outcomeNote = string.Empty;
                changed?.Invoke();
            })
            { text = text };

            button.AddToClassList("rival__tab");
            button.EnableInClassList("rival__tab--on", tab == which);

            return button;
        }

        /// <summary>
        /// Where the relationship stands, as a band with a sentence under it.
        ///
        /// The number is on the bar and not in words, because "minus sixty-three" is a fact nobody
        /// can act on and "hostile, competing against your interests on purpose" is one they can.
        /// </summary>
        private static VisualElement BuildStanding(CompanySimulation simulation, CompetitorId lab)
        {
            var value = simulation.State.Relations.With(lab);
            var band = RivalRelations.BandFor(value);

            var panel = new VisualElement();
            panel.AddToClassList("rival__standing");

            var heading = new Label(Loc.T("relation.title"));
            heading.AddToClassList("dossier__heading");
            panel.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("rival__bandrow");

            var name = new Label(RivalRelations.NameOf(band));
            name.AddToClassList("rival__band");
            name.AddToClassList(BandTextClass(band));
            row.Add(name);

            var reading = new Label(value.ToString("+0;-0;0",
                System.Globalization.CultureInfo.InvariantCulture));

            reading.AddToClassList("rival__value");
            row.Add(reading);

            panel.Add(row);

            // The scale runs both ways from the middle, so the fill grows left of centre when a
            // relationship has gone bad. A bar that only ever grows rightward cannot show that.
            var track = new VisualElement();
            track.AddToClassList("rival__track");

            var fill = new VisualElement();
            fill.AddToClassList("rival__fill");
            fill.AddToClassList(BandClass(band));

            var half = Math.Abs(value) / (RivalRelations.Best * 2.0) * 100.0;

            fill.style.left = Length.Percent((float)(value >= 0 ? 50.0 : 50.0 - half));
            fill.style.width = Length.Percent((float)half);

            track.Add(fill);
            panel.Add(track);

            var note = new Label(RivalRelations.NoteFor(band));
            note.AddToClassList("rival__note");
            panel.Add(note);

            // **What kind of company this is, which the board has never said.** It says who is
            // ahead, and a player had no way of knowing whether the lab above them is the one that
            // ships recklessly, the one that has state money behind it, or the one that is about to
            // fall over. All of that was in the dossier, in prose, which is what a player skims.
            panel.Add(BuildTraits(simulation, lab));

            return panel;
        }

        /// <summary>
        /// The badges under the relationship: what this lab is like, worked out from what it has
        /// done rather than from a field somebody has to remember to update.
        ///
        /// Three at most, most distinctive first. Seven of these is not a character, it is a table.
        /// </summary>
        private static VisualElement BuildTraits(CompanySimulation simulation, CompetitorId lab)
        {
            var block = new VisualElement();
            block.AddToClassList("rival__traits");

            var traits = LabTraits.For(lab, simulation.State);

            if (traits.Count == 0)
            {
                block.style.display = DisplayStyle.None;
                return block;
            }

            var heading = new Label(Loc.T("labtrait.title"));
            heading.AddToClassList("rival__traitshead");
            block.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("rival__traitrow");

            foreach (var trait in traits)
            {
                var chip = new Label(LabTraits.NameOf(trait));
                chip.AddToClassList("rival__trait");
                chip.EnableInClassList("rival__trait--warn", LabTraits.IsWarning(trait));

                // The word is the headline and the sentence is why it is there, which is the half a
                // single word cannot carry: "fearless" is a compliment until it is an obituary.
                InsightTip.Attach(chip, LabTraits.NameOf(trait), LabTraits.NoteFor(trait));

                row.Add(chip);
            }

            block.Add(row);
            return block;
        }

        private static VisualElement BuildHistory(IReadOnlyList<RelationEntry> history)
        {
            var panel = new VisualElement();
            panel.AddToClassList("rival__history");

            var heading = new Label(Loc.T("relation.history"));
            heading.AddToClassList("dossier__heading");
            panel.Add(heading);

            // Newest first, and capped: the recent half is what a player is deciding against, and a
            // forty line list of grudges is an archive rather than a memory.
            for (var index = 0; index < history.Count && index < 6; index++)
            {
                var entry = history[index];

                var row = new VisualElement();
                row.AddToClassList("rival__entry");

                var delta = new Label(entry.Delta.ToString("+0;-0",
                    System.Globalization.CultureInfo.InvariantCulture));

                delta.AddToClassList("rival__delta");
                delta.EnableInClassList("rival__delta--bad", entry.Delta < 0.0);
                row.Add(delta);

                var text = new Label(entry.Reason);
                text.AddToClassList("rival__reason");
                row.Add(text);

                panel.Add(row);
            }

            return panel;
        }

        /// <summary>
        /// Who works there, and what it would take to move them.
        ///
        /// The loyalty band is the only reading given, on purpose. A figure would turn the decision
        /// into arithmetic; a band leaves the player guessing how much overpaying is enough, which
        /// is the whole mechanic.
        /// </summary>
        private VisualElement BuildRoster(CompanySimulation simulation, CompetitorId lab)
        {
            var panel = new VisualElement();
            panel.AddToClassList("rival__roster");

            var warning = new Label(Loc.T("poach.warning"));
            warning.AddToClassList("rival__warning");
            panel.Add(warning);

            if (!string.IsNullOrEmpty(outcomeNote))
            {
                var said = new Label(outcomeNote);
                said.AddToClassList("rival__outcome");
                panel.Add(said);
            }

            var roster = simulation.RosterOf(lab);
            var top = simulation.State.IsMember(IntelTier.TrendSearch);
            var visible = RivalStaff.Visible(roster, top);

            foreach (var member in visible)
            {
                panel.Add(BuildPerson(simulation, member));
            }

            var hidden = RivalStaff.HiddenCount(roster);

            if (hidden > 0 && !top)
            {
                var locked = new VisualElement();
                locked.AddToClassList("rival__locked");

                var count = new Label(Loc.T("poach.hidden", hidden));
                count.AddToClassList("rival__lockedcount");
                locked.Add(count);

                var why = new Label(Loc.T("poach.hidden.note"));
                why.AddToClassList("rival__note");
                locked.Add(why);

                panel.Add(locked);
            }

            return panel;
        }

        private VisualElement BuildPerson(CompanySimulation simulation, RivalStaffMember member)
        {
            var today = simulation.State.Date;
            var band = Loyalty.BandFor(member.Loyalty(today));

            var row = new VisualElement();
            row.AddToClassList("person");

            var head = new VisualElement();
            head.AddToClassList("person__head");

            // Seeded on the person's own id, so the same employee has the same face every time the
            // card is opened, and falls back to initials on a clone without the character pack.
            head.Add(CandidateFaces.Frame(member.Id, member.Name, 44, "#7E8AA0"));

            var name = new Label(member.Name);
            name.AddToClassList("person__name");
            head.Add(name);

            var role = new Label(
                $"{PositionCatalog.Get(member.Position).Title}  ·  {member.Rating}");

            role.AddToClassList("person__role");
            head.Add(role);

            // Through the counted noun, not a raw number in a sentence: Polish takes "rok",
            // "lata" and "lat" and the rendered card read "2 lat u nich" until it did.
            var years = new Label(
                Loc.T("poach.years", Loc.Counted(member.YearsAt(today), "noun.year")));
            years.AddToClassList("person__years");
            head.Add(years);

            var loyal = new Label(Loyalty.NameOf(band));
            loyal.AddToClassList("person__loyalty");
            loyal.AddToClassList(LoyaltyClass(band));
            head.Add(loyal);

            var open = openOffer == member.Id;

            var offer = new Button(() =>
            {
                openOffer = open ? -1 : member.Id;
                bonusUsd = Poaching.SalaryAt(member) / 2;
                outcomeNote = string.Empty;
                changed?.Invoke();
            })
            { text = Loc.T(open ? "common.close" : "poach.send") };

            offer.AddToClassList("person__offer");
            head.Add(offer);

            row.Add(head);

            if (open)
            {
                row.Add(BuildOffer(simulation, member));
            }

            return row;
        }


        /// <summary>
        /// What the two companies have between them, and the four ways to add to it.
        ///
        /// **Three bands down the card and they answer three different questions.** Where the
        /// alliance stands and what is between it and the next level; what can be put to them today
        /// and how likely each is to be taken; and what the two of you have already done together.
        /// The first version of this was a column of four paragraphs with a button under each, which
        /// answered the second question four times and the other two not at all.
        /// </summary>
        private VisualElement BuildTogether(CompanySimulation simulation, CompetitorId lab)
        {
            var block = new VisualElement();
            block.AddToClassList("together");

            block.Add(BuildLadder(simulation, lab));
            block.Add(BuildOfferRow(simulation, lab));
            block.Add(BuildWorkList(simulation, lab));

            if (!string.IsNullOrEmpty(outcomeNote))
            {
                var note = new Label(outcomeNote);
                note.AddToClassList("together__note");
                block.Add(note);
            }

            return block;
        }

        /// <summary>
        /// The alliance as a track with three stops on it and a bar running between them.
        ///
        /// **The bar fills with days, never with money**, which is the whole design and the one
        /// thing a number could never say on its own. A player looking at a bar two thirds across
        /// with "another sixty-one days" under it understands they are waiting rather than saving.
        ///
        /// Each stop says what it opens, because a ladder whose rungs are unlabelled is a ladder
        /// nobody has a reason to climb. All three are real: level one makes offers land more often
        /// and puts the lab in the telephone, level two opens joint research, level three sells
        /// capacity at cost.
        /// </summary>
        private VisualElement BuildLadder(CompanySimulation simulation, CompetitorId lab)
        {
            var card = new VisualElement();
            card.AddToClassList("tog-ladder");

            var level = simulation.State.Alliances.LevelWith(lab);
            var band = simulation.State.Relations.BandWith(lab);
            var held = simulation.State.Alliances.DaysAtLevel(lab, simulation.State.Date);

            var head = new VisualElement();
            head.AddToClassList("tog-ladder__head");

            var name = new Label(level == 0
                ? Loc.T("alliance.none")
                : Loc.T("alliance.level", level.ToString(), held.ToString()));

            name.AddToClassList("tog-ladder__name");
            head.Add(name);

            var next = level + 1;

            var ready = next <= LabAlliances.TopLevel
                && simulation.State.Alliances.CanSignNext(lab, simulation.State.Date, band, out _);

            if (next <= LabAlliances.TopLevel)
            {
                if (ready)
                {
                    var sign = new Button(() =>
                    {
                        simulation.TrySignAlliance(lab, out var why);
                        outcomeNote = why;
                        changed?.Invoke();
                    })
                    {
                        text = Loc.T("alliance.sign", next.ToString(),
                            UiFormat.Money(LabAlliances.FeeFor(next)))
                    };

                    sign.AddToClassList("button");
                    sign.AddToClassList("tog-ladder__sign");
                    head.Add(sign);
                }
            }

            card.Add(head);

            // The track. Three segments, one per level, each filled by how far this company has
            // come through it. A level already signed is full; the one being worked on is partial;
            // the ones past it are empty.
            var track = new VisualElement();
            track.AddToClassList("tog-ladder__track");

            for (var step = 1; step <= LabAlliances.TopLevel; step++)
            {
                var segment = new VisualElement();
                segment.AddToClassList("tog-ladder__seg");

                var fill = new VisualElement();
                fill.AddToClassList("tog-ladder__fill");

                var share = step <= level
                    ? 1.0
                    : step == level + 1
                        ? Progress(simulation, lab, level, held, band)
                        : 0.0;

                fill.style.width = new StyleLength(Length.Percent((float)(share * 100.0)));
                segment.Add(fill);
                track.Add(segment);
            }

            card.Add(track);

            var stops = new VisualElement();
            stops.AddToClassList("tog-ladder__stops");

            for (var step = 1; step <= LabAlliances.TopLevel; step++)
            {
                var stop = new VisualElement();
                stop.AddToClassList("tog-ladder__stop");
                stop.EnableInClassList("tog-ladder__stop--on", step <= level);

                var title = new Label(Loc.T(LevelKey(step)));
                title.AddToClassList("tog-ladder__stopname");
                stop.Add(title);

                var gives = new Label(Loc.T(LevelGivesKey(step)));
                gives.AddToClassList("tog-ladder__stopgives");
                stop.Add(gives);

                stops.Add(stop);
            }

            card.Add(stops);

            // What the next stop is waiting on, in the simulation's own words so the card and the
            // till can never disagree about what is missing.
            //
            // **Only asked when the answer is no.** `TrySignAlliance` is not a question, it is the
            // thing that signs, and the first version of this card called it unconditionally to
            // read the refusal out of it. On a card where the level could be signed, drawing the
            // card signed it and charged the fee, which the render caught: a company at level one
            // came back from being looked at sitting at level two. **A read must never write, and
            // a `Try` method is a write however harmless its out parameter looks.**
            var blocked = simulation.WhyNotAlliance(lab);

            if (!string.IsNullOrEmpty(blocked))
            {
                var why = new Label(blocked);
                why.AddToClassList("tog-ladder__why");
                card.Add(why);
            }

            return card;
        }

        /// <summary>
        /// How far through the next level this company is, from zero to one.
        ///
        /// The first level has no calendar of its own: it waits on the relation being warm enough,
        /// so its bar reads how far up the band the relation has come. Everything above it is days.
        /// </summary>
        private static double Progress(CompanySimulation simulation, CompetitorId lab, int level,
            int held, RelationBand band)
        {
            if (band < LabAlliances.HoldsAt)
            {
                var value = simulation.State.Relations.With(lab);
                var floor = RelationScale.NeutralAbove;
                var roof = RelationScale.FriendlyAbove;

                return Math.Clamp((value - floor) / Math.Max(1.0, roof - floor), 0.0, 0.99);
            }

            if (level == 0)
            {
                return 1.0;
            }

            var needed = LabAlliances.DaysNeededFor(level + 1);

            return Math.Clamp(held / (double)Math.Max(1, needed), 0.0, 1.0);
        }

        /// <summary>Written out, never assembled: the key guard reads literals only.</summary>
        private static string LevelKey(int level) => level switch
        {
            1 => "alliance.name1",
            2 => "alliance.name2",
            _ => "alliance.name3"
        };

        /// <summary>See <see cref="LevelKey"/>.</summary>
        private static string LevelGivesKey(int level) => level switch
        {
            1 => "alliance.gives1",
            2 => "alliance.gives2",
            _ => "alliance.gives3"
        };

        /// <summary>
        /// The four offers, across rather than down.
        ///
        /// **Each one says how likely it is to be taken.** That is the change that makes this a
        /// decision rather than a gamble: a player about to spend sixty research points on a letter
        /// can see whether it is worth sending, and the reading comes from state the game already
        /// keeps rather than from a new number. Wide bands on purpose, because what matters is
        /// whether to send it and not that they will refuse 23% of the time.
        /// </summary>
        private VisualElement BuildOfferRow(CompanySimulation simulation, CompetitorId lab)
        {
            var row = new VisualElement();
            row.AddToClassList("tog-row");

            var pending = simulation.State.PendingOffers.Find(entry => entry.Lab == lab);
            var waiting = simulation.State.PendingOffers.Count > 0 && pending.Lab == lab;

            foreach (var definition in RelationOfferCatalog.All)
            {
                row.Add(BuildOfferTile(simulation, lab, definition, waiting, pending));
            }

            return row;
        }

        private VisualElement BuildOfferTile(CompanySimulation simulation, CompetitorId lab,
            RelationOfferDefinition definition, bool waiting, PendingOffer pending)
        {
            var tile = new VisualElement();
            tile.AddToClassList("tog-tile");

            var chance = RelationOfferCatalog.AcceptanceChance(
                definition.Offer, simulation.State.Relations.With(lab),
                simulation.RivalCapability(lab), simulation.OurCapabilityToday(),
                simulation.State.Alliances.LevelWith(lab));

            var odds = RelationOfferCatalog.OddsOf(chance);
            tile.AddToClassList(RelationOfferCatalog.ClassFor(odds));

            var mine = waiting && pending.Offer == definition.Offer;

            var kicker = new Label(mine
                ? Loc.T("together.sent",
                    Math.Max(0, definition.DaysToAnswer
                        - pending.DaysWaiting(simulation.State.Date)).ToString())
                : Loc.T(RelationOfferCatalog.KeyFor(odds)));

            kicker.AddToClassList("tog-tile__odds");
            tile.Add(kicker);

            var name = new Label(definition.DisplayName);
            name.AddToClassList("tog-tile__name");
            tile.Add(name);

            var body = new Label(definition.Description);
            body.AddToClassList("tog-tile__body");
            tile.Add(body);

            var parts = new List<string>();

            if (definition.PointCost > 0)
            {
                parts.Add(Loc.T("together.points", definition.PointCost.ToString()));
            }

            if (definition.CashCostUsd > 0)
            {
                parts.Add(UiFormat.Money(definition.CashCostUsd));
            }

            parts.Add(Loc.T("together.answer", definition.DaysToAnswer.ToString()));

            var price = new Label(string.Join("  ·  ", parts));
            price.AddToClassList("tog-tile__price");
            tile.Add(price);

            var send = new Button(() =>
            {
                simulation.TrySendOffer(lab, definition.Offer, out var why);
                outcomeNote = why;
                changed?.Invoke();
            })
            { text = Loc.T("together.send") };

            send.AddToClassList("button");
            send.AddToClassList("tog-tile__send");

            // **Shut is a look, not a dead control.** A disabled button takes no pointer event, and
            // the one thing a player does when something refuses them is press it again.
            send.SetEnabled(!waiting);
            tile.Add(send);

            return tile;
        }

        /// <summary>
        /// What the two companies have done together: what is running, then what is finished.
        ///
        /// **The author asked for this by name and it is the half a relationship screen usually
        /// forgets.** A bar and four buttons say where you are; this says how you got there, and it
        /// is the only place in the game that remembers a licence ran nine months in 2024 and ended
        /// well. Newest first, because the question is almost always about the last one.
        /// </summary>
        private static VisualElement BuildWorkList(CompanySimulation simulation, CompetitorId lab)
        {
            var block = new VisualElement();
            block.AddToClassList("tog-work");

            var heading = new Label(Loc.T("together.work"));
            heading.AddToClassList("tog-work__heading");
            block.Add(heading);

            var rows = 0;

            foreach (var deal in simulation.State.Deals)
            {
                if (deal.Lab != lab || !deal.IsLiveOn(simulation.State.Date))
                {
                    continue;
                }

                block.Add(WorkRow(
                    RelationOfferCatalog.Get(deal.Offer).DisplayName,
                    Loc.T("together.days_left",
                        Math.Max(0, deal.Ends.DayIndex - simulation.State.Date.DayIndex).ToString()),
                    "tog-work__row--live"));

                rows++;
            }

            var history = simulation.State.DealHistory;

            for (var index = history.Count - 1; index >= 0; index--)
            {
                var past = history[index];

                if (past.Lab != lab)
                {
                    continue;
                }

                block.Add(WorkRow(
                    RelationOfferCatalog.Get(past.Offer).DisplayName,
                    past.Outcome == DealOutcome.Refused
                        ? Loc.T("together.was_refused")
                        : Loc.T("together.ran_for", past.Days.ToString()),
                    past.Outcome == DealOutcome.Refused
                        ? "tog-work__row--refused"
                        : "tog-work__row--done"));

                rows++;
            }

            if (rows == 0)
            {
                var empty = new Label(Loc.T("together.nothing_yet"));
                empty.AddToClassList("tog-work__empty");
                block.Add(empty);
            }

            return block;
        }

        private static VisualElement WorkRow(string what, string when, string tone)
        {
            var row = new VisualElement();
            row.AddToClassList("tog-work__row");
            row.AddToClassList(tone);

            var left = new Label(what);
            left.AddToClassList("tog-work__what");
            row.Add(left);

            var right = new Label(when);
            right.AddToClassList("tog-work__when");
            row.Add(right);

            return row;
        }

        private VisualElement BuildOffer(CompanySimulation simulation, RivalStaffMember member)
        {
            var form = new VisualElement();
            form.AddToClassList("person__form");

            var salary = Poaching.SalaryAt(member);

            var caption = new Label($"{Loc.T("poach.offer")}   {UiFormat.Money(bonusUsd)}");
            caption.AddToClassList("person__caption");
            form.Add(caption);

            // Up to two years of their salary. Past that the curve has flattened and the slider
            // would be dead travel, which is the fault the free-tier control already shipped once.
            var slider = new Slider(0f, salary * 2f) { value = bonusUsd };
            slider.AddToClassList("person__slider");
            slider.RegisterValueChangedCallback(change =>
            {
                bonusUsd = (long)change.newValue;
                caption.text = $"{Loc.T("poach.offer")}   {UiFormat.Money(bonusUsd)}";
            });

            form.Add(slider);

            var send = new Button(() =>
            {
                simulation.TryPoach(member, bonusUsd, out var outcome, out var note);

                outcomeNote = note;
                openOffer = -1;

                // A reported refusal is the one outcome that has a second half: they ring you.
                callFrom = outcome == PoachOutcome.Reported ? member.Employer : null;

                changed?.Invoke();
            })
            { text = Loc.T("poach.send") };

            send.AddToClassList("button");
            send.AddToClassList("button--primary");
            send.SetEnabled(simulation.State.CashUsd >= bonusUsd);
            form.Add(send);

            return form;
        }

        /// <summary>
        /// The call that follows a reported approach.
        ///
        /// **Two ways to answer and both cost something.** There is no version of this conversation
        /// where the company comes out even, and apologising is cheaper because it is still an
        /// admission rather than because it is free.
        /// </summary>
        private VisualElement BuildCall(CompanySimulation simulation, CompetitorId lab)
        {
            var card = new VisualElement();
            card.AddToClassList("rival__call");

            var title = new Label(Loc.T("call.title", CompetitorCatalog.NameOf(lab)));
            title.AddToClassList("rival__calltitle");
            card.Add(title);

            var body = new Label(Loc.T("call.body"));
            card.Add(body);

            var buttons = new VisualElement();
            buttons.AddToClassList("rival__callbuttons");

            var sorry = new Button(() =>
            {
                simulation.AnswerTheCall(lab, apologise: true);
                callFrom = null;
                changed?.Invoke();
            })
            { text = Loc.T("call.apologise") };

            sorry.AddToClassList("button");
            buttons.Add(sorry);

            var hang = new Button(() =>
            {
                simulation.AnswerTheCall(lab, apologise: false);
                callFrom = null;
                changed?.Invoke();
            })
            { text = Loc.T("call.hangup") };

            hang.AddToClassList("button");
            hang.AddToClassList("button--armed");
            buttons.Add(hang);

            card.Add(buttons);
            return card;
        }

        /// <summary>
        /// The band's colour as ink, and separately as paint.
        ///
        /// **These were one class and the rendered frame caught it**: a single rule setting both
        /// `color` and `background-color` to the same value put the band name in gold on gold, so
        /// the one word saying how the relationship stands was invisible. Two names, one for the
        /// label and one for the bar, because they are two jobs that only look like one.
        /// </summary>
        private static string BandTextClass(RelationBand band) => band switch
        {
            RelationBand.Friendly => "rival--friendly",
            RelationBand.Neutral => "rival--neutral",
            RelationBand.Tense => "rival--tense",
            RelationBand.Hostile => "rival--hostile",
            _ => "rival--rivalry"
        };

        /// <summary>
        /// The same five bands as paint for the bar.
        ///
        /// **Written out rather than built from the text class plus a suffix.** A class name
        /// assembled by concatenation is invisible to `StylesheetTests` for exactly the reason a
        /// concatenated key is invisible to `LocalisationTests`, and the whole point of both guards
        /// is that a name nothing declares ships as a control with no styling on it.
        /// </summary>
        private static string BandClass(RelationBand band) => band switch
        {
            RelationBand.Friendly => "rival--friendly-fill",
            RelationBand.Neutral => "rival--neutral-fill",
            RelationBand.Tense => "rival--tense-fill",
            RelationBand.Hostile => "rival--hostile-fill",
            _ => "rival--rivalry-fill"
        };

        private static string LoyaltyClass(LoyaltyBand band) => band switch
        {
            LoyaltyBand.Loose => "person__loyalty--loose",
            LoyaltyBand.Open => "person__loyalty--open",
            LoyaltyBand.Settled => "person__loyalty--settled",
            _ => "person__loyalty--committed"
        };
    }
}
