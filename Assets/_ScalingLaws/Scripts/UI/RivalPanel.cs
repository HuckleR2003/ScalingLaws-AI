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

        public RivalPanel(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        /// <summary>Forgets any open form. Called when the card is closed or another lab opened.</summary>
        public void Reset()
        {
            openOffer = -1;
            bonusUsd = 0;
            outcomeNote = string.Empty;
            callFrom = null;
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
        public VisualElement Build(CompetitorId lab)
        {
            var simulation = company();
            var block = new ScrollView();
            block.AddToClassList("rival");
            block.verticalScrollerVisibility = ScrollerVisibility.Auto;
            block.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            block.Add(BuildStanding(simulation, lab));

            var history = simulation.State.Relations.HistoryWith(lab);

            if (history.Count > 0)
            {
                block.Add(BuildHistory(history));
            }

            block.Add(BuildRoster(simulation, lab));

            if (callFrom.HasValue && callFrom.Value == lab)
            {
                block.Add(BuildCall(simulation, lab));
            }

            return block;
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

            return panel;
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

            var heading = new Label(Loc.T("poach.title"));
            heading.AddToClassList("dossier__heading");
            panel.Add(heading);

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
