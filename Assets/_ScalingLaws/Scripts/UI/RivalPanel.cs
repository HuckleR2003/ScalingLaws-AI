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
        /// Where the relationship stands, on a scale with all five bands marked on it.
        ///
        /// **The bar used to be a stripe floating in the middle of nothing.** It carried the one
        /// number the card is about and gave it no context at all: a fill ending two thirds of the
        /// way left says the relationship is bad and not how bad, nor how far it is from the next
        /// thing that changes. The five bands are drawn on the track now, the one the lab is in is
        /// lit, and the marker sits where they actually are.
        ///
        /// The words stay the headline. "Minus sixty-three" is a fact nobody can act on and
        /// "hostile, competing against your interests on purpose" is one they can.
        /// </summary>
        private static VisualElement BuildStanding(CompanySimulation simulation, CompetitorId lab)
        {
            var value = simulation.State.Relations.With(lab);
            var band = RelationScale.BandFor(value);

            var panel = new VisualElement();
            panel.AddToClassList("rstand");

            var head = new VisualElement();
            head.AddToClassList("rstand__head");

            var name = new Label(RelationScale.NameOf(band));
            name.AddToClassList("rstand__band");
            name.AddToClassList(BandTextClass(band));
            head.Add(name);

            var reading = new Label(value.ToString("+0;-0;0",
                System.Globalization.CultureInfo.InvariantCulture));

            reading.AddToClassList("rstand__value");
            head.Add(reading);

            panel.Add(head);

            var note = new Label(RelationScale.NoteFor(band));
            note.AddToClassList("rstand__note");
            panel.Add(note);

            panel.Add(BuildBandTrack(value, band));

            // **What kind of company this is, which the board has never said.** It says who is
            // ahead, and a player had no way of knowing whether the lab above them is the one that
            // ships recklessly, the one that has state money behind it, or the one that is about to
            // fall over. All of that was in the dossier, in prose, which is what a player skims.
            panel.Add(BuildTraits(simulation, lab));

            return panel;
        }

        /// <summary>
        /// The whole scale with the five bands on it, and a marker where this lab is.
        ///
        /// **Every band is the width of the range it covers**, so the track is the scale rather
        /// than a decoration of it: Neutral is wide because it is wide, and Rivalry is a sliver at
        /// the end because almost nothing gets that far. A player can see how close the next band
        /// is, which is the question a single fill can never answer.
        /// </summary>
        private static VisualElement BuildBandTrack(double value, RelationBand band)
        {
            var track = new VisualElement();
            track.AddToClassList("rstand__track");

            var span = RelationScale.Best - RelationScale.Worst;

            // Bottom to top, because the track reads left to right and the scale does too.
            var edges = new[]
            {
                (RelationScale.Worst, RelationScale.HostileAbove, RelationBand.Rivalry),
                (RelationScale.HostileAbove, RelationScale.TenseAbove, RelationBand.Hostile),
                (RelationScale.TenseAbove, RelationScale.NeutralAbove, RelationBand.Tense),
                (RelationScale.NeutralAbove, RelationScale.FriendlyAbove, RelationBand.Neutral),
                (RelationScale.FriendlyAbove, RelationScale.Best, RelationBand.Friendly)
            };

            foreach (var (from, to, which) in edges)
            {
                var segment = new VisualElement();
                segment.AddToClassList("rstand__seg");
                segment.AddToClassList(BandClass(which));
                segment.EnableInClassList("rstand__seg--on", which == band);

                segment.style.width = Length.Percent((float)((to - from) / span * 100.0));
                track.Add(segment);
            }

            // The marker, out of flow so it can sit anywhere along the track without pushing a band
            // sideways. Clamped inside the ends, or at the extremes half of it hangs off the edge.
            var marker = new VisualElement();
            marker.AddToClassList("rstand__marker");

            var at = (value - RelationScale.Worst) / span * 100.0;
            marker.style.left = Length.Percent((float)Math.Clamp(at, 1.0, 99.0));

            track.Add(marker);

            return track;
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

        /// <summary>
        /// What has moved the relationship, newest first.
        ///
        /// **Rows in a card rather than two columns of loose text.** It had no card at all and sat
        /// flush against the edge of the panel under the one that did, so the two halves of this
        /// tab read as two screens. Each row carries a lit edge in the direction it moved, which is
        /// the same mark the work list on the other tab uses for the same reason: a column of
        /// numbers where some are good and some are bad is a column nobody reads twice.
        /// </summary>
        private static VisualElement BuildHistory(IReadOnlyList<RelationEntry> history)
        {
            var panel = new VisualElement();
            panel.AddToClassList("rhist");

            var heading = new Label(Loc.T("relation.history"));
            heading.AddToClassList("rhist__heading");
            panel.Add(heading);

            // Newest first, and capped: the recent half is what a player is deciding against, and a
            // forty line list of grudges is an archive rather than a memory.
            for (var index = 0; index < history.Count && index < 6; index++)
            {
                var entry = history[index];
                var good = entry.Delta >= 0.0;

                var row = new VisualElement();
                row.AddToClassList("rhist__row");
                row.EnableInClassList("rhist__row--good", good);
                row.EnableInClassList("rhist__row--bad", !good);

                var text = new Label(entry.Reason);
                text.AddToClassList("rhist__reason");
                row.Add(text);

                var delta = new Label(entry.Delta.ToString("+0;-0",
                    System.Globalization.CultureInfo.InvariantCulture));

                delta.AddToClassList("rhist__delta");
                delta.EnableInClassList("rhist__delta--bad", !good);
                row.Add(delta);

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
            {
                // **The licence asks to talk, the other three ask for a yes.** Asked for by name,
                // and it is what the button now does: a distribution partner agreeing means a
                // contract arrives to be read and signed, not a term starting. A button that says
                // SEND over a thing that opens a negotiation is the wrong promise.
                text = definition.Offer == RelationOffer.DistributionLicence
                    ? Loc.T("offer.settle")
                    : Loc.T("together.send")
            };

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
