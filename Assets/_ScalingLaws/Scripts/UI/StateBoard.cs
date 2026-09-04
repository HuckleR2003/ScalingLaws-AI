using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The state programme, drawn beside era five.
    ///
    /// **Not another row of research cards, and that is deliberate.** Everything else on this screen
    /// is a node: pay points and calendar, get a capability. These are commitments. A sector is not
    /// something the company gains, it is something it becomes responsible for, and drawing them in
    /// the same shape as the tree would say they were the same kind of decision.
    ///
    /// So: eight square tiles, each carrying the three numbers that make it a trade rather than a
    /// purchase. What it pays, what it holds, and what it costs when it goes wrong. The last of
    /// those is the largest figure on the tile on purpose. A board that led with the fee would be
    /// inviting the player to read one column, and the whole ending is about the other two.
    ///
    /// The three readings across the top are the company's own standing: the record a government
    /// looks at, how much of the promise the fleet is covering, and today's odds. They are above the
    /// tiles because a player deciding whether to take Defence on needs to know what their delivery
    /// already looks like before they add another twelve hundred petaflops to the promise.
    /// </summary>
    public sealed class StateBoard
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        /// <summary>Set when an operation refuses, printed once, then cleared.</summary>
        private string problem = string.Empty;

        public StateBoard(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("sboard");

            var simulation = company?.Invoke();

            if (simulation == null)
            {
                return root;
            }

            var state = simulation.State;
            var programme = state.Programme;

            root.Add(BuildHead());

            if (problem.Length > 0)
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("mcb-problem");
                root.Add(trouble);
                problem = string.Empty;
            }

            root.Add(BuildReadings(simulation, state, programme));

            if (!programme.IsSigned)
            {
                root.Add(BuildSigning(simulation));
            }

            root.Add(BuildTiles(simulation, state, programme));
            root.Add(BuildHorizon());

            return root;
        }

        private static VisualElement BuildHead()
        {
            var head = new VisualElement();
            head.AddToClassList("sboard__head");

            var title = new Label(Loc.T("state.title"));
            title.AddToClassList("sboard__title");
            head.Add(title);

            var strap = new Label(Loc.T("state.strap"));
            strap.AddToClassList("sboard__strap");
            head.Add(strap);

            return head;
        }

        /// <summary>
        /// The three numbers the player is judged on, before any tile is read.
        ///
        /// Delivery and risk are blank until something is signed rather than showing 100% and 0%,
        /// which would read as a company doing brilliantly at nothing.
        /// </summary>
        private VisualElement BuildReadings(CompanySimulation simulation, CompanyState state,
            StateProgramme programme)
        {
            var row = new VisualElement();
            row.AddToClassList("sboard__readings");

            var record = SafetyRecord.For(state, state.Date);

            row.Add(Reading(
                Loc.T("state.record"),
                UiFormat.Percent(record, 0),
                Loc.T("state.record.note"),
                record,
                record >= SafetyRecord.ContractThreshold ? "sboard__reading--good"
                    : record >= StateProgramme.NoticeBelow ? "sboard__reading--warn"
                    : "sboard__reading--bad"));

            if (programme.IsSigned)
            {
                var delivery = programme.LastDelivery;

                row.Add(Reading(
                    Loc.T("state.delivery"),
                    UiFormat.Percent(delivery, 0),
                    Loc.T("state.delivery.note"),
                    delivery,
                    delivery >= 0.995 ? "sboard__reading--good"
                        : delivery >= 0.90 ? "sboard__reading--warn"
                        : "sboard__reading--bad"));

                var risk = simulation.StateFailureRisk(delivery);

                row.Add(Reading(
                    Loc.T("state.risk"),
                    UiFormat.Percent(risk, 3),
                    Loc.T("state.held.note"),
                    // Scaled against a tenth of a per cent a day, which is roughly one failure every
                    // three years and about as much as any company should be carrying.
                    Math.Clamp(risk / 0.001, 0.0, 1.0),
                    risk <= 0.0002 ? "sboard__reading--good"
                        : risk <= 0.0006 ? "sboard__reading--warn"
                        : "sboard__reading--bad"));
            }

            return row;
        }

        private static VisualElement Reading(string label, string value, string note, double fill,
            string tone)
        {
            var card = new VisualElement();
            card.AddToClassList("sboard__reading");
            card.AddToClassList(tone);

            var kicker = new Label(label);
            kicker.AddToClassList("sboard__kicker");
            card.Add(kicker);

            var figure = new Label(value);
            figure.AddToClassList("sboard__figure");
            card.Add(figure);

            var track = new VisualElement();
            track.AddToClassList("sboard__track");

            var bar = new VisualElement();
            bar.AddToClassList("sboard__bar");
            bar.style.width = Length.Percent((float)(Math.Clamp(fill, 0.0, 1.0) * 100.0));
            track.Add(bar);

            card.Add(track);

            var caption = new Label(note);
            caption.AddToClassList("sboard__note");
            card.Add(caption);

            return card;
        }

        /// <summary>The signing row, and the reason it is refused when it is.</summary>
        private VisualElement BuildSigning(CompanySimulation simulation)
        {
            var row = new VisualElement();
            row.AddToClassList("sboard__signing");

            var can = simulation.CanSignStateProgramme(out var reason);

            var sign = new Button(() =>
            {
                if (!simulation.TrySignStateProgramme(out var why))
                {
                    problem = why;
                }

                changed?.Invoke();
            })
            { text = Loc.T("state.sign") };

            sign.AddToClassList("sboard__sign");
            sign.SetEnabled(can);
            row.Add(sign);

            if (!can)
            {
                var note = new Label(reason);
                note.AddToClassList("sboard__blocked");
                row.Add(note);
            }

            return row;
        }

        private VisualElement BuildTiles(CompanySimulation simulation, CompanyState state,
            StateProgramme programme)
        {
            var grid = new VisualElement();
            grid.AddToClassList("sboard__grid");

            if (programme.IsSigned && StateProgramme.IsOnNotice(SafetyRecord.For(state, state.Date)))
            {
                var notice = new Label(Loc.T("state.on_notice"));
                notice.AddToClassList("sboard__notice");
                grid.Add(notice);
            }

            foreach (var definition in StateSectorCatalog.All)
            {
                grid.Add(Tile(simulation, programme, definition));
            }

            return grid;
        }

        /// <summary>
        /// One sector.
        ///
        /// **The failure figure is the largest thing on the tile.** The fee is what a player wants
        /// to read and the failure is what decides whether taking it is a good idea, so the layout
        /// puts the second one where the eye lands. That is the same reasoning that put the
        /// low-value band on the "(i)" cards: a control whose downside is in a tooltip is a control
        /// people learn about afterwards.
        /// </summary>
        private VisualElement Tile(CompanySimulation simulation, StateProgramme programme,
            StateSectorDefinition definition)
        {
            var running = programme.IsRunning(definition.Sector);
            var can = simulation.CanStartSector(definition.Sector, out var reason);

            var tile = new VisualElement();
            tile.AddToClassList("stile");
            tile.EnableInClassList("stile--on", running);
            tile.EnableInClassList("stile--locked", !running && !can);

            var head = new VisualElement();
            head.AddToClassList("stile__head");

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("stile__name");
            head.Add(name);

            var badge = new Label(running
                ? Loc.T("state.running")
                : can ? string.Empty : Loc.T("state.locked"));
            badge.AddToClassList("stile__badge");
            head.Add(badge);

            tile.Add(head);

            // The danger bar. Four sectors deep it is the fastest read on the board: a row of these
            // side by side says which half of the programme is the dangerous half without numbers.
            var danger = new VisualElement();
            danger.AddToClassList("stile__danger");

            var dangerFill = new VisualElement();
            dangerFill.AddToClassList("stile__dangerfill");
            dangerFill.style.width = Length.Percent(
                (float)(Math.Clamp(definition.FailureWeight / 3.4, 0.0, 1.0) * 100.0));
            danger.Add(dangerFill);

            tile.Add(danger);

            var cost = new Label(UiFormat.Money(definition.FailureCostUsd));
            cost.AddToClassList("stile__cost");
            tile.Add(cost);

            var costLabel = new Label(Loc.T("state.failure_cost"));
            costLabel.AddToClassList("stile__costlabel");
            tile.Add(costLabel);

            tile.Add(Stat(Loc.T("state.fee"), UiFormat.Money(definition.FeeUsdPerDay) + " / d"));
            tile.Add(Stat(Loc.T("state.held"),
                UiFormat.Number(definition.PetaflopsRequired, 0) + " PF"));
            tile.Add(Stat(Loc.T("state.power"),
                UiFormat.Number(definition.MegawattsRequired, 0) + " MW"));

            var blurb = new Label(definition.Blurb);
            blurb.AddToClassList("stile__blurb");
            tile.Add(blurb);

            var action = new Button(() =>
            {
                var ok = running
                    ? simulation.TryStopSector(definition.Sector, out var why)
                    : simulation.TryStartSector(definition.Sector, out why);

                if (!ok)
                {
                    problem = why;
                }

                changed?.Invoke();
            })
            { text = running ? Loc.T("state.stop") : Loc.T("state.start") };

            action.AddToClassList("stile__action");
            action.EnableInClassList("stile__action--stop", running);
            action.SetEnabled(running || can);
            tile.Add(action);

            if (!running && !can && reason.Length > 0)
            {
                var why = new Label(reason);
                why.AddToClassList("stile__why");
                tile.Add(why);
            }

            return tile;
        }

        private static VisualElement Stat(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("stile__stat");

            var name = new Label(label);
            name.AddToClassList("stile__statname");
            row.Add(name);

            var figure = new Label(value);
            figure.AddToClassList("stile__statvalue");
            row.Add(figure);

            return row;
        }

        /// <summary>
        /// What all eight would be worth, and what they would hold.
        ///
        /// Quoted as a horizon rather than a target, because taking all eight is possible and is
        /// almost certainly the run that ends in a bankruptcy nobody saw coming.
        /// </summary>
        private static VisualElement BuildHorizon()
        {
            var line = new Label(Loc.T("state.everything",
                UiFormat.Money(StateSectorCatalog.EverythingPerDay()),
                UiFormat.Number(StateSectorCatalog.EverythingPetaflops(), 0)));

            line.AddToClassList("sboard__horizon");

            return line;
        }
    }
}
