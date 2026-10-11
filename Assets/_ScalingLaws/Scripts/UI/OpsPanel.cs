using System;
using System.Collections.Generic;
using System.Globalization;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The magnifying glass on a tile that has nothing to show yet.
    ///
    /// Drawn rather than typed. A glyph from the font would be a different shape on every machine
    /// and whatever colour the fallback face decided, which is the reason the dial, the brand mark,
    /// the world map and the support face are all drawn here too.
    /// </summary>
    public sealed class SearchGlyph : VisualElement
    {
        private static readonly Color Ink = new(0.42f, 0.47f, 0.56f);

        public SearchGlyph()
        {
            AddToClassList("opstile__glyph");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 4f || rect.height <= 4f)
            {
                return;
            }

            var painter = context.painter2D;
            var size = Mathf.Min(rect.width, rect.height);
            var radius = size * 0.30f;
            var centre = new Vector2(size * 0.40f, size * 0.40f);

            painter.strokeColor = Ink;
            painter.lineWidth = Mathf.Max(2f, size * 0.07f);
            painter.lineCap = LineCap.Round;

            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Stroke();

            // The handle leaves the ring on the diagonal, so it starts on the circle rather than
            // inside it: a handle drawn from the centre reads as a struck-through circle.
            var edge = centre + new Vector2(radius, radius) * 0.707f;

            painter.BeginPath();
            painter.MoveTo(edge);
            painter.LineTo(new Vector2(size * 0.84f, size * 0.84f));
            painter.Stroke();
        }
    }

    /// <summary>
    /// One card in the operations band: a heading, a state, some figures, and one thing to do.
    ///
    /// **A shell rather than two nearly identical cards.** The band holds the readings a company
    /// lives with day to day, and each has the same shape: a number that is either fine or is
    /// quietly costing money, and a ladder that does something about it. Hand-building each one
    /// is how the padding drifts and how two locked states end up subtly different, which is how
    /// a screen stops reading as one screen.
    ///
    /// The locked state is part of the shell for the same reason. A tile whose technology has not
    /// been researched says what it would show rather than drawing an empty box, because an empty
    /// box reads as broken and a list of what is behind the door is an argument for opening it.
    /// </summary>
    public sealed class OpsTile : VisualElement
    {
        private readonly Label state = new();
        private readonly VisualElement body = new();
        private readonly VisualElement drawer = new();
        private readonly VisualElement foot = new();

        public OpsTile(string caption)
        {
            AddToClassList("opstile");

            var head = new VisualElement();
            head.AddToClassList("opstile__head");

            var name = new Label(caption);
            name.AddToClassList("opstile__name");
            head.Add(name);

            state.AddToClassList("opstile__state");
            head.Add(state);

            hierarchy.Add(head);

            body.AddToClassList("opstile__body");
            hierarchy.Add(body);

            drawer.AddToClassList("opstile__drawer");
            drawer.style.display = DisplayStyle.None;
            hierarchy.Add(drawer);

            foot.AddToClassList("opstile__foot");
            hierarchy.Add(foot);
        }

        /// <summary>
        /// How the tile asks whether a node is held, handed in rather than reached for.
        ///
        /// `GateNotice` needs it to work out which prerequisites are still missing, and a shell in
        /// `UI/` has no business holding a simulation to ask.
        /// </summary>
        public Func<ResearchNodeId, bool> HasResearch { get; set; } = _ => false;

        /// <summary>The short word in the corner of the heading: a level, or that nothing is on.</summary>
        public void Chip(string text, bool lit)
        {
            state.text = text ?? string.Empty;
            state.EnableInClassList("opstile__state--on", lit);
        }

        /// <summary>
        /// What this tile would tell the player, and the way to the research that opens it.
        ///
        /// Through `GateNotice`, which already answers a click on a locked control with the node's
        /// own picture and a button that rings it and the whole road to it on the board. A second
        /// way of saying "you need to research this" would be a second thing to keep in step.
        /// </summary>
        public void Locked(string subject, IReadOnlyList<ResearchNodeId> nodes, string strap,
            IReadOnlyList<string> promises)
        {
            var blank = new VisualElement();
            blank.AddToClassList("opstile__blank");

            blank.Add(new SearchGlyph());

            var head = new Label(Loc.T("ops.nodata"));
            head.AddToClassList("opstile__nodata");
            blank.Add(head);

            var why = new Label(strap);
            why.AddToClassList("opstile__why");
            blank.Add(why);

            foreach (var promise in promises)
            {
                var row = new VisualElement();
                row.AddToClassList("opstile__promise");

                var dot = new VisualElement();
                dot.AddToClassList("opstile__dot");
                row.Add(dot);

                var words = new Label(promise);
                words.AddToClassList("opstile__promisetext");
                row.Add(words);

                blank.Add(row);
            }

            body.Add(blank);

            Act(Loc.T("ops.show_me"),
                () => GateNotice.NeedsResearch(subject, nodes, HasResearch));
        }

        /// <summary>The headline reading: a large figure and what it counts.</summary>
        public void Figure(string value, string caption, string tone = null)
        {
            var block = new VisualElement();
            block.AddToClassList("opstile__figblock");

            var big = new Label(value);
            big.AddToClassList("opstile__fig");
            if (!string.IsNullOrEmpty(tone))
            {
                big.AddToClassList(tone);
            }

            block.Add(big);

            var words = new Label(caption);
            words.AddToClassList("opstile__figcap");
            block.Add(words);

            body.Add(block);
        }

        /// <summary>A line of detail: a label on the left and a figure on the right.</summary>
        public void Line(string caption, string value, string tone = null)
        {
            var row = new VisualElement();
            row.AddToClassList("opstile__line");

            var words = new Label(caption);
            words.AddToClassList("opstile__linecap");
            row.Add(words);

            var figure = new Label(value);
            figure.AddToClassList("opstile__linefig");
            if (!string.IsNullOrEmpty(tone))
            {
                figure.AddToClassList(tone);
            }

            row.Add(figure);
            body.Add(row);
        }

        /// <summary>A hairline between two groups of lines, so a card reads in parts.</summary>
        public void Rule()
        {
            var rule = new VisualElement();
            rule.AddToClassList("opstile__rule");
            body.Add(rule);
        }

        /// <summary>Anything the shell has no shape for, into the body.</summary>
        public void Fill(VisualElement child) => body.Add(child);

        /// <summary>
        /// The ladder, folded away until the player asks for it.
        ///
        /// **In the tile rather than in a window**, which is a change from the brief and the reason
        /// is written in `GateNotice`: three separate overlays with that lifetime would be three
        /// places to get the click-eating wrong, and this project has shipped an invisible control
        /// that swallowed clicks twice. Folded rather than always open because the tile is a
        /// reading first; a player who has not asked what to do about it should not have to scroll
        /// past a shop to read their own numbers.
        /// </summary>
        public void Drawer(VisualElement content)
        {
            drawer.Clear();
            drawer.Add(content);
        }

        /// <summary>Whether the ladder is showing. Set rather than toggled, so a rebuild can restore it.</summary>
        public bool DrawerOpen
        {
            get => drawer.style.display == DisplayStyle.Flex;
            set => drawer.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// The one thing this tile does. Always enabled, never greyed.
        ///
        /// A disabled control dispatches no pointer event, so it cannot explain itself, and this
        /// project has shipped that three times. A tile that cannot act yet still answers the
        /// click, through `GateNotice`.
        /// </summary>
        public void Act(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("opstile__act");
            foot.Add(button);
        }
    }

    /// <summary>
    /// The operations band: what the company is quietly paying for, and the levers against it.
    ///
    /// **Both tiles exist because of a measurement rather than an idea.** The campaign probe
    /// reports serving load at 0% for eight consecutive years against a fleet bill that reached
    /// $5,806,735 a day, and the duplicate-account work found a company serving 1.74 free accounts
    /// per human being. Neither number appeared anywhere in the game.
    ///
    /// It computes almost nothing. Every figure is read from the simulation, for the reason the
    /// demographic panel states: a panel that does its own arithmetic is a second copy of the rules
    /// waiting to disagree with the first.
    /// </summary>
    public sealed class OpsPanel
    {
        private static readonly string[] OpenDrawers = new string[2];

        private readonly CompanySimulation simulation;
        private readonly Action refresh;
        private readonly Action<string> failed;

        public OpsPanel(CompanySimulation simulation, Action refresh, Action<string> failed)
        {
            this.simulation = simulation;
            this.refresh = refresh;
            this.failed = failed;
        }

        /// <summary>Builds the band. A row of tiles, each one a subject the company lives with.</summary>
        public VisualElement Build()
        {
            var band = new VisualElement();
            band.AddToClassList("opsband");

            band.Add(Duplicates());

            // UI Toolkit has no :last-child, so the gutter on the final tile comes off here rather
            // than from a selector that would parse, warn in every player's log and style nothing.
            var last = SpareCapacity();
            last.AddToClassList("opstile--last");
            band.Add(last);

            return band;
        }

        /// <summary>
        /// Which ladders are folded open, kept across a rebuild.
        ///
        /// The screen rebuilds itself whenever a day rolls over, which at normal speed is every
        /// second and a half. A drawer that closed on every rebuild would be a drawer nobody could
        /// use, which is the same fault the research card and the insight tip were both fixed for.
        /// </summary>
        public static void Forget()
        {
            OpenDrawers[0] = null;
            OpenDrawers[1] = null;
        }

        /// <summary>
        /// Folds a ladder out or away, the same way the button does.
        ///
        /// A method rather than only a closure, because an EditMode element has no panel and
        /// a click sent to a button is never dispatched. Same seam as `ManagementScreen.ShowDesk`
        /// and `PartsShop.Order`, and for the same reason: a screen whose only route into a
        /// state is a closure is a state no test can reach.
        /// </summary>
        public static void Show(int slot, string key, bool open)
        {
            if (slot < 0 || slot >= OpenDrawers.Length)
            {
                return;
            }

            OpenDrawers[slot] = open ? key : null;
        }

        private static bool IsOpen(int slot, string key) => OpenDrawers[slot] == key;

        private static void Toggle(int slot, string key) =>
            OpenDrawers[slot] = OpenDrawers[slot] == key ? null : key;

        // ---- duplicate accounts --------------------------------------------------------------

        private OpsTile Duplicates()
        {
            var tile = new OpsTile(Loc.T("ops.dupes.title"))
            {
                HasResearch = simulation.State.HasResearch
            };

            if (!simulation.UnderstandsAccounts())
            {
                tile.Chip(Loc.T("ops.locked"), false);
                tile.Locked(
                    Loc.T("ops.dupes.title"),
                    new[] { ResearchNodeId.AccountRecognition },
                    Loc.T("ops.dupes.strap"),
                    new[]
                    {
                        Loc.T("ops.dupes.promise1"),
                        Loc.T("ops.dupes.promise2"),
                        Loc.T("ops.dupes.promise3")
                    });

                return tile;
            }

            var accounts = simulation.AccountsPerPerson();
            var level = simulation.State.AccountDetectionLevel;

            tile.Chip(Loc.T("ops.level", level.ToString(CultureInfo.InvariantCulture)), level > 0);

            tile.Figure(
                UiFormat.Number(accounts.Free, 2),
                Loc.T("ops.dupes.free_cap"),
                accounts.FreeExcess > 0.45 ? "opstile__fig--bad" : null);

            tile.Line(Loc.T("ops.dupes.spent"),
                UiFormat.Money(simulation.DuplicateServingLastMonthUsd()),
                "opstile__linefig--bad");

            tile.Rule();

            tile.Figure(UiFormat.Number(accounts.Paid, 2), Loc.T("ops.dupes.paid_cap"));

            tile.Line(Loc.T("ops.dupes.policy"),
                Loc.T(simulation.State.MultiAccountBanOn ? "ops.on" : "ops.off"),
                simulation.State.MultiAccountBanOn ? "opstile__linefig--on" : null);

            tile.Drawer(Ladder(
                level,
                AccountIntegrity.MostDetectionLevels,
                Loc.T("ops.dupes.ladder"),
                AccountIntegrity.PointsFor(level + 1),
                AccountIntegrity.CashFor(level + 1),
                () => simulation.TryUpgradeAccountDetection(out var why) ? null : why));

            tile.DrawerOpen = IsOpen(0, "accounts");
            tile.Act(Loc.T(tile.DrawerOpen ? "ops.hide_action" : "ops.take_action"), () =>
            {
                Toggle(0, "accounts");
                refresh?.Invoke();
            });

            return tile;
        }

        // ---- the fleet nothing is using --------------------------------------------------------

        private OpsTile SpareCapacity()
        {
            var tile = new OpsTile(Loc.T("ops.idle.title"))
            {
                HasResearch = simulation.State.HasResearch
            };

            var quality = simulation.State.LastQuality;
            var idle = simulation.IdleShareOfFleet();
            var level = simulation.State.CapacityResaleLevel;
            var desk = simulation.RunsAResaleDesk();

            tile.Chip(
                desk ? Loc.T("ops.level", level.ToString(CultureInfo.InvariantCulture))
                     : Loc.T("ops.no_desk"),
                desk && level > 0);

            // **The reading is never gated and the selling is.** Knowing your own cluster is
            // standing idle takes no technology, and it is the most expensive thing a company here
            // fails to notice: putting it behind a $2.1M node would keep the biggest number on the
            // screen away from exactly the player who most needs to see it.
            var dial = new VisualElement();
            dial.AddToClassList("opstile__dial");

            var gauge = new ServiceGauge();
            gauge.Set(quality);
            dial.Add(gauge);

            var percent = new Label(UiFormat.Percent(quality.Utilisation, 0));
            percent.AddToClassList("opstile__dialfig");
            dial.Add(percent);

            var caption = new Label(Loc.T("fleet.server_usage"));
            caption.AddToClassList("opstile__dialcap");
            dial.Add(caption);

            tile.Fill(dial);

            tile.Line(Loc.T("ops.idle.spare"), UiFormat.Percent(idle, 0),
                idle > 0.5 ? "opstile__linefig--bad" : null);

            tile.Line(Loc.T("ops.idle.burning"),
                Loc.T("release.per_day", UiFormat.Money(IdleCostPerDayUsd())),
                idle > 0.5 ? "opstile__linefig--bad" : null);

            tile.Rule();

            if (!desk)
            {
                tile.Line(Loc.T("ops.idle.sold_yesterday"), Loc.T("ops.dash"));
                tile.Line(Loc.T("ops.idle.sold_month"), Loc.T("ops.dash"));

                tile.Act(Loc.T("ops.sell_it"), () => GateNotice.NeedsResearch(
                    Loc.T("ops.idle.title"),
                    new[] { ResearchNodeId.SpotCapacityDesk },
                    simulation.State.HasResearch));

                return tile;
            }

            tile.Line(Loc.T("ops.idle.sold_yesterday"),
                UiFormat.Money(simulation.CapacitySalesYesterdayUsd()), "opstile__linefig--on");

            tile.Line(Loc.T("ops.idle.sold_month"),
                UiFormat.Money(simulation.CapacitySalesLastMonthUsd()), "opstile__linefig--on");

            tile.Drawer(Ladder(
                level,
                CapacityResale.MostLevels,
                Loc.T("ops.idle.ladder"),
                CapacityResale.PointsFor(level + 1),
                CapacityResale.CashFor(level + 1),
                () => simulation.TryUpgradeCapacityResale(out var why) ? null : why));

            tile.DrawerOpen = IsOpen(1, "resale");
            tile.Act(Loc.T(tile.DrawerOpen ? "ops.hide_action" : "ops.take_action"), () =>
            {
                Toggle(1, "resale");
                refresh?.Invoke();
            });

            return tile;
        }

        // ---- the ladder, which both tiles wear ------------------------------------------------

        /// <summary>
        /// A level, a segmented bar for how far it has climbed, and one button to climb it.
        ///
        /// `SegmentBar` rather than a continuous fill, for the reason the bar itself states: a
        /// continuous fill at one level of eight is a sliver nobody can see, and a row of segments
        /// is one lit block. Both ladders wear the same one so the two tiles read as one screen.
        /// </summary>
        private VisualElement Ladder(int level, int mostLevels, string strap,
            int points, long cash, Func<string> buy)
        {
            var block = new VisualElement();
            block.AddToClassList("opsladder");

            var words = new Label(strap);
            words.AddToClassList("opsladder__strap");
            block.Add(words);

            // One block per rung, never one per thirty pixels: this bar stands for a ladder
            // and a player counts the lit ones.
            var bar = new SegmentBar { Segments = mostLevels };
            bar.AddToClassList("opsladder__bar");
            bar.Set(mostLevels <= 0 ? 0.0 : level / (double)mostLevels,
                new Color(0.49f, 0.84f, 0.64f));
            block.Add(bar);

            var count = new Label(Loc.T("ops.of_levels",
                level.ToString(CultureInfo.InvariantCulture),
                mostLevels.ToString(CultureInfo.InvariantCulture)));

            count.AddToClassList("opsladder__count");
            block.Add(count);

            if (level >= mostLevels)
            {
                var done = new Label(Loc.T("ops.ladder_done"));
                done.AddToClassList("opsladder__done");
                block.Add(done);
                return block;
            }

            var price = new VisualElement();
            price.AddToClassList("opsladder__price");

            var pointsLabel = new Label(Loc.T("ops.points",
                points.ToString("N0", CultureInfo.InvariantCulture)));

            pointsLabel.AddToClassList("opsladder__points");
            price.Add(pointsLabel);

            var cashLabel = new Label(UiFormat.Money(cash));
            cashLabel.AddToClassList("opsladder__cash");
            price.Add(cashLabel);

            block.Add(price);

            var button = new Button(() =>
            {
                var why = buy();
                if (!string.IsNullOrEmpty(why))
                {
                    failed?.Invoke(why);
                }

                refresh?.Invoke();
            })
            {
                text = Loc.T("ops.buy_level")
            };

            button.AddToClassList("opsladder__buy");
            block.Add(button);

            return block;
        }

        /// <summary>
        /// What the idle share of the fleet costs to hold for a day.
        ///
        /// The fleet bill times the share of it doing nothing. Not a new figure: both halves are
        /// read from the simulation, and the point of putting them together is that neither one
        /// alone says what a player needs to hear.
        /// </summary>
        private long IdleCostPerDayUsd() =>
            (long)Math.Round(simulation.Profile.Bill.TotalUsd * simulation.IdleShareOfFleet());
    }
}
