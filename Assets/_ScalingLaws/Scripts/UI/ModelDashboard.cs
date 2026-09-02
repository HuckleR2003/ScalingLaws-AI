using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The first thing MODEL shows: what the company already sells, and the two ways to change it.
    ///
    /// **MODEL used to open straight into the training designer**, which meant the tab about the
    /// product opened on a form. A player with four models on sale had nowhere that answered "how
    /// are they doing" without walking three screens, and a player with none was dropped into five
    /// decisions before being told why any of them mattered.
    ///
    /// So this is a landing page. Two big doors on the left — build a new one, or improve one you
    /// have — the live service state in the middle, the money on the right, and underneath, every
    /// model on sale ranked by what it earns. Retired models are not here on purpose: this screen
    /// is about what the player can still act on.
    /// </summary>
    public sealed class ModelDashboard
    {
        /// <summary>How often the little opening flourish plays. Every third visit, not every one.</summary>
        public const int FlourishEveryNthVisit = 3;

        /// <summary>
        /// Milliseconds the first line holds before it swaps.
        ///
        /// Halved after the first play session. The whole thing is a flourish, and a flourish that
        /// makes the player wait is just a door that sticks.
        /// </summary>
        public const int ThinkMilliseconds = 350;

        /// <summary>And the second, before the screen arrives underneath it.</summary>
        public const int CreateMilliseconds = 325;

        private readonly Func<CompanySimulation> simulation;
        private readonly Action newModel;
        private readonly Action upgrade;
        private readonly Action openRelease;

        /// <summary>Visits so far. Drives the every-third-time rule and nothing else.</summary>
        private int visits;

        public ModelDashboard(Func<CompanySimulation> simulation, Action newModel, Action upgrade,
            Action openRelease)
        {
            this.simulation = simulation;
            this.newModel = newModel;
            this.upgrade = upgrade;
            this.openRelease = openRelease;
        }

        /// <summary>True when this visit should play the opening lines.</summary>
        public bool ShouldFlourish => visits % FlourishEveryNthVisit == 1;

        public VisualElement Build()
        {
            visits++;

            var company = simulation();
            var state = company.State;
            var product = company.Product();

            var page = new VisualElement();
            page.AddToClassList("content");
            page.AddToClassList("modelhub");

            // This screen builds its own page rather than going through GameShell.NewPage, so it has
            // to ask for its own strip. Without this it was the only main screen with no picture on
            // it, which reads as a screen that failed to finish loading rather than as a choice.
            var strip = PageArt.BannerFor("background_model");

            if (strip != null)
            {
                page.Add(strip);
            }

            page.Add(BuildTopRow(company, state, product));
            page.Add(BuildTable(company));

            if (ShouldFlourish)
            {
                page.Add(BuildFlourish(page));
            }

            return page;
        }

        // ---- the top half ----------------------------------------------------------------------

        private VisualElement BuildTopRow(CompanySimulation company, CompanyState state,
            ProductStanding product)
        {
            var row = new VisualElement();
            row.AddToClassList("modelhub__top");

            row.Add(BuildDoors(state));
            row.Add(BuildService(state, product));
            row.Add(BuildMoney(company, state, product));

            return row;
        }

        /// <summary>
        /// The two doors.
        ///
        /// Deliberately the largest things on the screen and deliberately not the same colour: one
        /// spends months and a fortune, the other spends a fraction of both. A player who cannot
        /// tell them apart at a glance will open the wrong one.
        /// </summary>
        private VisualElement BuildDoors(CompanyState state)
        {
            var column = new VisualElement();
            column.AddToClassList("modelhub__doors");

            var fresh = new Button(newModel);
            fresh.AddToClassList("door");
            fresh.AddToClassList("door--new");

            var freshTitle = new Label(Loc.T("create.new_model"));
            freshTitle.AddToClassList("door__title");
            fresh.Add(freshTitle);

            var freshNote = new Label(Loc.T("model.design_run"));

            freshNote.AddToClassList("door__note");
            fresh.Add(freshNote);
            column.Add(fresh);

            var better = new Button(upgrade);
            better.AddToClassList("door");
            better.AddToClassList("door--upgrade");

            var betterTitle = new Label(Loc.T("model.upgrade"));
            betterTitle.AddToClassList("door__title");
            better.Add(betterTitle);

            var live = state.DeployedModels.Count;

            var betterNote = new Label(live > 0
                ? "Improve something already on sale, without training it again."
                : "Nothing on sale yet. Build one first and this is how you keep it current.");

            betterNote.AddToClassList("door__note");
            better.Add(betterNote);
            better.SetEnabled(live > 0);
            column.Add(better);

            return column;
        }

        private VisualElement BuildService(CompanyState state, ProductStanding product)
        {
            var panel = new VisualElement();
            panel.AddToClassList("modelhub__service");

            var heading = new Label(Loc.T("model.service"));
            heading.AddToClassList("modelhub__heading");
            panel.Add(heading);

            if (!product.Exists)
            {
                var none = new Label(Loc.T("model.service.none"));
                none.AddToClassList("modelhub__empty");
                panel.Add(none);
                return panel;
            }

            var quality = state.LastQuality;

            var dial = new VisualElement();
            dial.AddToClassList("modelhub__dial");

            var gauge = new ServiceGauge();
            gauge.Set(quality);
            dial.Add(gauge);

            var percent = new Label(UiFormat.Percent(quality.Utilisation, 0));
            percent.AddToClassList("service__percent");
            dial.Add(percent);

            var caption = new Label(Loc.T("fleet.server_usage"));
            caption.AddToClassList("service__caption");
            dial.Add(caption);

            panel.Add(dial);

            var latency = new Label($"Response {quality.ResponseMilliseconds:N0}ms");
            latency.AddToClassList("modelhub__latency");
            panel.Add(latency);

            return panel;
        }

        /// <summary>
        /// The money, as four bars rather than four numbers.
        ///
        /// A bar answers "is this a lot" without the player having to remember last month's figure,
        /// which a bare dollar amount never does. Each is drawn against a reference the company has
        /// actually hit, so a young lab sees full bars for small numbers and that is correct: it is
        /// doing well for what it is.
        /// </summary>
        private VisualElement BuildMoney(CompanySimulation company, CompanyState state,
            ProductStanding product)
        {
            var panel = new VisualElement();
            panel.AddToClassList("modelhub__money");

            var heading = new Label(Loc.T("model.this_month2"));
            heading.AddToClassList("modelhub__heading");
            panel.Add(heading);

            var month = Ledger.MonthKeyOf(state.Date);
            var income = state.Ledger.MonthIncome(month);
            var costs = state.Ledger.MonthCost(month);
            var subs = product.Subscribers;

            // The reference each bar is drawn against: the larger of the two flows, so income and
            // costs are on the same scale and the gap between them is the thing you see.
            var scale = Math.Max(1L, Math.Max(income, costs));

            panel.Add(Bar("INCOME", UiFormat.Money(income), income / (double)scale, "#5FBF7F"));
            panel.Add(Bar("COSTS", UiFormat.Money(costs), costs / (double)scale, "#D96A6A"));

            panel.Add(Bar("FROM SUBSCRIPTIONS", UiFormat.Money(product.MonthEarningsUsd),
                income <= 0L ? 0.0 : product.MonthEarningsUsd / (double)income, "#D6A03C"));

            panel.Add(Bar("SUBSCRIBERS", UiFormat.Count(subs),
                Math.Clamp(subs / Math.Max(1.0, company.Sentiment().Users), 0.0, 1.0), "#5B8DEF"));

            var net = income - costs;

            var bottom = new Label(net >= 0L
                ? $"Net {UiFormat.Money(net)} this month."
                : $"Losing {UiFormat.Money(-net)} this month.");

            bottom.AddToClassList("modelhub__net");
            bottom.EnableInClassList("modelhub__net--bad", net < 0L);
            panel.Add(bottom);

            return panel;
        }

        private static VisualElement Bar(string caption, string reading, double fill, string tint)
        {
            var block = new VisualElement();
            block.AddToClassList("loadbar");

            var head = new VisualElement();
            head.AddToClassList("loadbar__head");

            var label = new Label(caption);
            label.AddToClassList("loadbar__caption");
            head.Add(label);

            var value = new Label(reading);
            value.AddToClassList("loadbar__value");
            head.Add(value);

            block.Add(head);

            var track = new VisualElement();
            track.AddToClassList("loadbar__track");

            var bar = new VisualElement();
            bar.AddToClassList("loadbar__fill");
            bar.style.width = Length.Percent((float)(Math.Clamp(fill, 0.0, 1.0) * 100.0));

            if (ColorUtility.TryParseHtmlString(tint, out var colour))
            {
                bar.style.backgroundColor = colour;
            }

            track.Add(bar);
            block.Add(track);
            return block;
        }

        // ---- the table -------------------------------------------------------------------------

        private VisualElement BuildTable(CompanySimulation company)
        {
            var panel = new VisualElement();
            panel.AddToClassList("modelhub__table");

            var heading = new Label(Loc.T("model.on_sale"));
            heading.AddToClassList("modelhub__heading");
            panel.Add(heading);

            var rows = company.ModelBoard();

            if (rows.Count == 0)
            {
                var none = new Label(Loc.T("model.on_sale.none"));
                none.AddToClassList("modelhub__empty");
                panel.Add(none);
                return panel;
            }

            panel.Add(HeaderRow());

            foreach (var row in rows)
            {
                panel.Add(ModelRowElement(row, company.State.CompanyName));
            }

            var go = new Button(openRelease) { text = Loc.T("model.manage") };
            go.AddToClassList("modelhub__manage");
            panel.Add(go);

            return panel;
        }

        private static VisualElement HeaderRow()
        {
            var row = new VisualElement();
            row.AddToClassList("mrow");
            row.AddToClassList("mrow--head");

            row.Add(Cell(string.Empty, "mrow__icon"));
            row.Add(Cell("MODEL", "mrow__name"));
            row.Add(Cell("USERS", "mrow__users"));
            row.Add(Cell("SUBS", "mrow__subs"));
            row.Add(Cell("NET INCOME", "mrow__income"));

            return row;
        }

        private static VisualElement ModelRowElement(ModelRow model, string company)
        {
            var row = new VisualElement();
            row.AddToClassList("mrow");

            // **The same piece of silicon the creator designed and the upgrade screen improves.**
            // It was a coloured square with the model type's first letter in it, which said nothing
            // the row did not already say two columns to the right, and it meant the thing the
            // player had just branded turned into an anonymous tile the moment it shipped.
            //
            // The type keeps its colour, as the edge of the die rather than as the whole tile.
            var chip = ChipPreview.Build(company);
            chip.AddToClassList("die--row");

            if (ColorUtility.TryParseHtmlString(TintFor(model.Type), out var tint))
            {
                var body = chip.Q(className: "die__body");

                if (body != null)
                {
                    body.style.borderTopColor = tint;
                    body.style.borderBottomColor = tint;
                    body.style.borderLeftColor = tint;
                    body.style.borderRightColor = tint;
                }
            }

            row.Add(chip);

            var name = new VisualElement();
            name.AddToClassList("mrow__name");

            var title = new Label(model.Name);
            title.AddToClassList("mrow__title");
            name.Add(title);

            var under = new Label(
                Loc.T("model.row_line", ModelTypeCatalog.Get(model.Type).DisplayName,
                UiFormat.Number(model.Capability, 1), model.DaysOnSale));

            under.AddToClassList("mrow__under");
            name.Add(under);
            row.Add(name);

            row.Add(Cell(UiFormat.Count(model.Users), "mrow__users"));
            row.Add(Cell(UiFormat.Count(model.Subscribers), "mrow__subs"));
            row.Add(Cell(UiFormat.Money(model.MonthEarningsUsd), "mrow__income"));

            return row;
        }

        /// <summary>One colour per model type, so the table reads by shape before it reads by word.</summary>
        private static string TintFor(ModelType type) => type switch
        {
            ModelType.General => "#5B8DEF",
            ModelType.Coding => "#3FB6A8",
            ModelType.Conversational => "#D6A03C",
            ModelType.Automation => "#E0883C",
            ModelType.Agentic => "#A66BE0",
            _ => "#7A8496"
        };

        private static VisualElement Cell(string text, string style)
        {
            var label = new Label(text);
            label.AddToClassList(style);
            return label;
        }

        // ---- the flourish -----------------------------------------------------------------------

        /// <summary>
        /// Two lines over a veil, then gone.
        ///
        /// **Every third visit, not every visit.** An animation the player sees forty times an hour
        /// is a delay they learn to resent; one they see occasionally stays a flourish. It is
        /// pickable-through and removes itself, so it can never sit on top of a button the player
        /// is trying to press.
        /// </summary>
        private VisualElement BuildFlourish(VisualElement page)
        {
            var veil = new VisualElement();
            veil.AddToClassList("flourish");
            veil.pickingMode = PickingMode.Ignore;

            var line = new Label(Loc.T("model.lets_think"));
            line.AddToClassList("flourish__line");
            veil.Add(line);

            veil.schedule.Execute(() =>
            {
                line.text = "Let's create.";
                line.AddToClassList("flourish__line--go");
            }).ExecuteLater(ThinkMilliseconds);

            veil.schedule.Execute(() => veil.AddToClassList("flourish--out"))
                .ExecuteLater(ThinkMilliseconds + CreateMilliseconds);

            veil.schedule.Execute(veil.RemoveFromHierarchy)
                .ExecuteLater(ThinkMilliseconds + CreateMilliseconds + 400);

            return veil;
        }
    }
}
