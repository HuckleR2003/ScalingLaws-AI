using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The stock screen: every lab down the left, one company's chart in the middle, and the
    /// ticket on the right.
    ///
    /// **Three columns because the decision has three parts and they are read in that order.**
    /// Which company, how it has been doing, how much to buy. A single scrolling page would make
    /// the player hold the price in their head while they scrolled to the button.
    ///
    /// Nothing here computes and nothing here decides. Prices come from `ShareMarket`, which
    /// derives them from the lab's own live standing, and every purchase goes through the
    /// simulation because that is where money moves.
    /// </summary>
    public sealed class InvestingScreen
    {
        /// <summary>How many days of price history the chart draws.</summary>
        public const int ChartDays = 90;

        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        private CompetitorId selected = CompetitorId.OpenAi;
        private bool selling;

        /// <summary>How much of the available parcel the slider is asking for, 0 to 1.</summary>
        private double parcel = 0.25;

        private string note = string.Empty;

        /// <summary>
        /// Which half of the screen is showing: the register of other companies, or what this
        /// company owns.
        ///
        /// **A method rather than only a tab, for the reason `ManagementScreen.ShowDesk` is
        /// one:** an EditMode element has no panel, so a click sent to a tab is never
        /// dispatched and the lambda behind it goes unmeasured.
        /// </summary>
        private bool estate;

        /// <inheritdoc cref="estate"/>
        public void ShowProperty(bool on)
        {
            estate = on;
            note = string.Empty;
            Refresh();
        }

        public InvestingScreen(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;

            Root = new VisualElement();
            Root.AddToClassList("invest");
        }

        public VisualElement Root { get; }

        /// <summary>Which lab the screen opens on. Used by the banner in the ranking.</summary>
        public void Select(CompetitorId lab)
        {
            selected = lab;
            parcel = 0.25;
            note = string.Empty;
        }

        public void Refresh()
        {
            Root.Clear();

            var simulation = company();

            Root.Add(BuildHeader(simulation));
            Root.Add(BuildTabs());

            if (estate)
            {
                Root.Add(BuildEstate(simulation));
                return;
            }

            var body = new VisualElement();
            body.AddToClassList("invest__body");

            body.Add(BuildList(simulation));
            body.Add(BuildDetail(simulation));

            Root.Add(body);
        }

        // ---- shares, or what the company owns ---------------------------------------------

        private VisualElement BuildTabs()
        {
            var row = new VisualElement();
            row.AddToClassList("invest__tabs");

            row.Add(Tab("plant.tab_shares", false));
            row.Add(Tab("plant.tab_estate", true));

            return row;
        }

        private Button Tab(string key, bool property)
        {
            var tab = new Button(() => ShowProperty(property)) { text = Loc.T(key) };

            tab.AddToClassList("invest__tab");
            tab.EnableInClassList("invest__tab--on", estate == property);

            return tab;
        }

        /// <summary>
        /// What the company owns, which today is power stations and nothing else.
        ///
        /// **The ceiling is printed first and the income second, in that order on purpose.**
        /// Nine measured campaigns say a company that owns its accelerators stops growing at
        /// 2,500 kW in its third year and nothing in the game ever told it so; a station is
        /// what moves that number. The wholesale income is real, small, and stated as small,
        /// because a purchase this size with a hidden payback would be the one guaranteed
        /// return in a game whose whole design is that there is none.
        /// </summary>
        private VisualElement BuildEstate(CompanySimulation simulation)
        {
            var state = simulation.State;

            var page = new VisualElement();
            page.AddToClassList("estate");

            var strap = new Label(Loc.T("plant.strap"));
            strap.AddToClassList("estate__strap");
            page.Add(strap);

            // What the company may draw today against what it is drawing, which is the number
            // this whole tab is about.
            var ceiling = new VisualElement();
            ceiling.AddToClassList("estate__ceiling");

            var ceilingLabel = new Label(Loc.T("plant.ceiling"));
            ceilingLabel.AddToClassList("estate__ceilinglabel");
            ceiling.Add(ceilingLabel);

            // **A company that rents everything has no site at all**, and printing that as
            // "0.0 kW / 0.0 kW" reads as a broken figure rather than as the true answer.
            var supply = simulation.SitePowerCapacityKilowatts();

            var ceilingValue = new Label(supply > 0.0
                ? UiFormat.Kilowatts(simulation.Profile.PowerDrawKilowatts)
                    + "  /  " + UiFormat.Kilowatts(supply)
                : Loc.T("plant.ceiling.empty"));

            ceilingValue.AddToClassList("estate__ceilingvalue");
            ceiling.Add(ceilingValue);

            InsightTip.AttachKeyed(ceiling, "plant.ceiling", "plant.ceiling.note");
            page.Add(ceiling);

            if (state.Power.Count == 0)
            {
                var none = new Label(Loc.T("plant.none"));
                none.AddToClassList("estate__none");
                page.Add(none);
            }

            if (!string.IsNullOrEmpty(note))
            {
                var why = new Label(note);
                why.AddToClassList("estate__note");
                page.Add(why);
            }

            var rows = new VisualElement();
            rows.AddToClassList("estate__rows");

            foreach (var plant in PowerPlantCatalog.All)
            {
                rows.Add(BuildPlant(simulation, plant));
            }

            page.Add(rows);
            return page;
        }

        private VisualElement BuildPlant(CompanySimulation simulation, PowerPlantDefinition plant)
        {
            var state = simulation.State;

            var card = new VisualElement();
            card.AddToClassList("estate__card");

            var name = new Label(plant.DisplayName.ToUpperInvariant());
            name.AddToClassList("estate__name");
            card.Add(name);

            var body = new Label(plant.Description);
            body.AddToClassList("estate__body");
            card.Add(body);

            var figures = new VisualElement();
            figures.AddToClassList("estate__figures");

            figures.Add(UiParts.StatLine(Loc.T("plant.output"),
                UiFormat.Kilowatts(plant.Kilowatts)));

            figures.Add(UiParts.StatLine(Loc.T("plant.build"),
                Loc.Counted(plant.BuildDays, "noun.day")));

            figures.Add(UiParts.StatLine(Loc.T("plant.fuel"),
                UiFormat.Money((long)Math.Round(
                    plant.Kilowatts * 24.0 * plant.FuelCostPerKilowattHourUsd))
                + " " + Loc.T("common.a_day")));

            figures.Add(UiParts.StatLine(Loc.T("plant.upkeep"),
                UiFormat.Money((long)Math.Round(
                    PowerPlantCatalog.DailyRunningCostUsd(plant)))
                + " " + Loc.T("common.a_day")));

            card.Add(figures);

            // **What it earns, said out loud, against what it cost.** A card that printed only
            // the output would let a player read a three billion dollar purchase as an
            // investment, and this one takes about a century to come back on power alone.
            var yearly = (long)Math.Round(
                plant.Kilowatts * 24.0 * 365.0
                * PowerPlantCatalog.WholesalePricePerKilowattHourUsd
                - PowerPlantCatalog.DailyRunningCostUsd(plant) * 365.0);

            var payback = new Label(Loc.T("plant.payback",
                UiFormat.Money(yearly), UiFormat.Money(plant.CapexUsd)));

            payback.AddToClassList("estate__payback");
            card.Add(payback);

            var ready = state.Power.ReadyDate(plant.Site);

            if (ready.HasValue)
            {
                var days = Math.Max(0, ready.Value.DayIndex - state.Date.DayIndex);

                var status = new Label(days > 0
                    ? Loc.T("plant.building", ready.Value.ToString(),
                        Loc.Counted(days, "noun.day"))
                    : Loc.T("plant.online", ready.Value.ToString()));

                status.AddToClassList("estate__status");
                status.EnableInClassList("estate__status--on", days == 0);
                card.Add(status);

                return card;
            }

            var commission = new Button(() =>
            {
                if (simulation.TryBuildPowerPlant(plant.Site, out var why))
                {
                    AudioDirector.Confirm();
                }
                else
                {
                    note = why;
                    AudioDirector.Deny();
                }

                changed?.Invoke();
            })
            {
                text = Loc.T("plant.capex", UiFormat.Money(plant.CapexUsd))
            };

            commission.AddToClassList("estate__buy");
            commission.SetEnabled(state.CashUsd >= plant.CapexUsd);
            card.Add(commission);

            return card;
        }

        // ---- the strip across the top -------------------------------------------------------------

        private VisualElement BuildHeader(CompanySimulation simulation)
        {
            var head = new VisualElement();
            head.AddToClassList("invest__head");

            var title = new Label(Loc.T("invest.title"));
            title.AddToClassList("invest__title");
            head.Add(title);

            var rule = new VisualElement();
            rule.AddToClassList("invest__rule");
            head.Add(rule);

            var subtitle = new Label(Loc.T("invest.subtitle"));
            subtitle.AddToClassList("invest__subtitle");
            head.Add(subtitle);

            var spacer = new VisualElement();
            spacer.AddToClassList("invest__spacer");
            head.Add(spacer);

            var cash = new Label(UiFormat.Money(simulation.State.CashUsd));
            cash.AddToClassList("invest__cash");
            head.Add(cash);

            return head;
        }

        // ---- the companies ------------------------------------------------------------------------

        private VisualElement BuildList(CompanySimulation simulation)
        {
            var scroller = new ScrollView();
            scroller.AddToClassList("invest__list");
            scroller.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroller.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
            {
                // `None` is the absence of a competitor, not a company. It was drawn with a price
                // and a badge reading "N" until the board was rendered and looked at.
                if (lab == CompetitorId.None)
                {
                    continue;
                }

                scroller.Add(BuildRow(simulation, lab));
            }

            return scroller;
        }

        private VisualElement BuildRow(CompanySimulation simulation, CompetitorId lab)
        {
            var price = simulation.SharePriceOf(lab);

            // Yesterday against today, from the same derived series the chart draws, so the arrow
            // and the line can never disagree about which way the day went.
            var yesterday = PriceOn(simulation, lab, simulation.State.Date.AddDays(-1));
            var move = yesterday > 0.0 ? (price - yesterday) / yesterday : 0.0;

            var row = new Button(() =>
            {
                Select(lab);
                changed?.Invoke();
            });

            row.AddToClassList("srow");
            row.EnableInClassList("srow--on", lab == selected);
            row.EnableInClassList("srow--up", move > 0.0001);
            row.EnableInClassList("srow--down", move < -0.0001);

            var badge = LabLogos.Badge(lab, CompetitorCatalog.NameOf(lab));
            badge.AddToClassList("srow__badge");
            row.Add(badge);

            var text = new VisualElement();
            text.AddToClassList("srow__text");

            var nameRow = new VisualElement();
            nameRow.AddToClassList("srow__namerow");

            var name = new Label(CompetitorCatalog.NameOf(lab));
            name.AddToClassList("srow__name");
            nameRow.Add(name);

            if (simulation.SharesHeldIn(lab) > 0)
            {
                var held = new Label(UiFormat.Percent(simulation.OwnershipOf(lab)));
                held.AddToClassList("srow__held");
                nameRow.Add(held);
            }

            text.Add(nameRow);

            var figures = new VisualElement();
            figures.AddToClassList("srow__figures");

            var reading = new Label(UiFormat.SharePrice(price));
            reading.AddToClassList("srow__price");
            figures.Add(reading);

            var arrow = new Label(Arrow(move) + " " + UiFormat.Percent(Math.Abs(move), 2));
            arrow.AddToClassList("srow__move");
            figures.Add(arrow);

            text.Add(figures);
            row.Add(text);

            return row;
        }

        private static string Arrow(double move) =>
            move > 0.0001 ? "↑" : move < -0.0001 ? "↓" : "↕";

        // ---- one company --------------------------------------------------------------------------

        private VisualElement BuildDetail(CompanySimulation simulation)
        {
            var column = new VisualElement();
            column.AddToClassList("invest__detail");

            column.Add(BuildChart(simulation));

            var lower = new VisualElement();
            lower.AddToClassList("invest__lower");

            lower.Add(BuildPosition(simulation));
            lower.Add(BuildTicket(simulation));

            column.Add(lower);
            return column;
        }

        /// <summary>
        /// Ninety days of price, with the numbers that make it a chart rather than a shape.
        ///
        /// **The line on its own says nothing.** It was drawn with no scale and no dates, so a rise
        /// across the frame could have been four percent or four hundred, over a fortnight or a
        /// year, and there was no way to tell which. A price chart whose axis is missing is
        /// decoration wearing the costume of a figure.
        /// </summary>
        private VisualElement BuildChart(CompanySimulation simulation)
        {
            var days = new List<double>(ChartDays);

            for (var back = ChartDays - 1; back >= 0; back--)
            {
                days.Add(PriceOn(simulation, selected, simulation.State.Date.AddDays(-back)));
            }

            var frame = new VisualElement();
            frame.AddToClassList("invest__chartframe");

            var chart = new PriceChart(days);
            chart.AddToClassList("invest__chart");
            frame.Add(chart);

            var high = double.MinValue;
            var low = double.MaxValue;

            foreach (var price in days)
            {
                high = Math.Max(high, price);
                low = Math.Min(low, price);
            }

            // The band down the left, high at the top, the way every price chart is read.
            var scale = new VisualElement();
            scale.AddToClassList("invest__scale");
            scale.Add(ChartNote(UiFormat.SharePrice(high), "invest__scale-high"));
            scale.Add(ChartNote(UiFormat.SharePrice(low), "invest__scale-low"));
            frame.Add(scale);

            // And what the width is worth, because ninety days is not obvious from a line.
            var axis = new VisualElement();
            axis.AddToClassList("invest__axis");
            axis.Add(ChartNote(
                simulation.State.Date.AddDays(-(ChartDays - 1)).ToString(), "invest__axis-from"));

            axis.Add(ChartNote(
                Loc.T("invest.window", ChartDays), "invest__axis-window"));

            axis.Add(ChartNote(simulation.State.Date.ToString(), "invest__axis-to"));
            frame.Add(axis);

            return frame;
        }

        private static Label ChartNote(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList("invest__note");
            label.AddToClassList(className);

            return label;
        }

        /// <summary>
        /// The price on a past day, from the same function today's price comes from.
        ///
        /// **The history is derived rather than recorded, and it has to be.** Storing ninety days
        /// for every lab would put a few thousand recomputable numbers into every save, and a
        /// recorded series would drift from the live price the first time a smear moved a lab's
        /// standing.
        /// </summary>
        private static double PriceOn(CompanySimulation simulation, CompetitorId lab, GameDate date)
            => ShareMarket.PriceOn(lab, date);

        private VisualElement BuildPosition(CompanySimulation simulation)
        {
            var panel = new VisualElement();
            panel.AddToClassList("invest__position");

            var held = simulation.SharesHeldIn(selected);
            var spent = simulation.SpentOnSharesIn(selected);
            var worth = simulation.ValueOfHoldingIn(selected);

            panel.Add(Pair(Loc.T("invest.my_shares"), UiFormat.Compact(held), "pos--plain"));
            panel.Add(Pair(Loc.T("invest.spent"), UiFormat.Money(spent), "pos--spent"));

            // The value carries its own verdict, because "$626.8 M" against "$645.27 M" is a
            // subtraction the player should not have to do while deciding whether to sell.
            var swing = spent > 0 ? (worth - spent) / (double)spent : 0.0;

            var value = Pair(Loc.T("invest.value"),
                $"{UiFormat.Money(worth)}   {Arrow(swing)} {UiFormat.Percent(Math.Abs(swing), 2)}",
                swing >= 0.0 ? "pos--good" : "pos--bad");

            panel.Add(value);

            var ownership = new VisualElement();
            ownership.AddToClassList("invest__ownership");

            var label = new Label(
                $"{Loc.T("invest.your_share")}  ·  {UiFormat.Percent(simulation.OwnershipOf(selected))}");

            label.AddToClassList("invest__ownlabel");
            ownership.Add(label);

            var track = new VisualElement();
            track.AddToClassList("invest__owntrack");

            var fill = new VisualElement();
            fill.AddToClassList("invest__ownfill");
            fill.style.width = Length.Percent(
                (float)Math.Clamp(simulation.OwnershipOf(selected) * 100.0, 0.0, 100.0));

            track.Add(fill);
            ownership.Add(track);

            var hint = new Label(Loc.T("invest.your_share.note"));
            hint.AddToClassList("invest__ownnote");
            ownership.Add(hint);

            panel.Add(ownership);

            var available = new Label(
                $"{Loc.T("invest.available")}   {UiFormat.Compact(simulation.SharesAvailableIn(selected))}");

            available.AddToClassList("invest__available");
            panel.Add(available);

            // What the holding actually pays, which is the whole reason to hold one rather than to
            // trade it, and the panel had nothing about it. A month at today's price: honest,
            // because a lab whose standing collapses stops paying and this figure goes with it.
            var monthly = (long)(held * simulation.SharePriceOf(selected) * ShareMarket.MonthlyYield);

            panel.Add(Pair(Loc.T("invest.dividend"), "+" + UiFormat.Money(monthly),
                monthly > 0 ? "pos--good" : "pos--plain"));

            return panel;
        }

        private static VisualElement Pair(string label, string value, string modifier)
        {
            var row = new VisualElement();
            row.AddToClassList("pos");

            var name = new Label(label);
            name.AddToClassList("pos__label");
            row.Add(name);

            var reading = new Label(value);
            reading.AddToClassList("pos__value");
            reading.AddToClassList(modifier);
            row.Add(reading);

            return row;
        }

        // ---- the ticket ---------------------------------------------------------------------------

        private VisualElement BuildTicket(CompanySimulation simulation)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ticket");

            if (simulation.State.AcquiredLabs.Contains(selected))
            {
                var owned = new Label(Loc.T("invest.owned"));
                owned.AddToClassList("ticket__owned");
                panel.Add(owned);

                return panel;
            }

            var tabs = new VisualElement();
            tabs.AddToClassList("ticket__tabs");

            tabs.Add(Tab(Loc.T("invest.buy"), !selling, () => { selling = false; changed?.Invoke(); }));
            tabs.Add(Tab(Loc.T("invest.sell"), selling, () => { selling = true; changed?.Invoke(); }));

            panel.Add(tabs);

            var price = simulation.SharePriceOf(selected);

            var most = selling
                ? simulation.SharesHeldIn(selected)
                : Affordable(simulation, price);

            var shares = (long)(most * Math.Clamp(parcel, 0.0, 1.0));

            panel.Add(Line(selling ? Loc.T("invest.sell_shares") : Loc.T("invest.buy_shares"),
                UiFormat.Compact(shares), "ticket--count"));

            panel.Add(Line(Loc.T("invest.share_price"),
                UiFormat.SharePrice(price), null));

            panel.Add(Line(Loc.T("invest.commission"),
                (selling ? "-" : "+") + UiFormat.Percent(ShareMarket.CommissionRate), null));

            var total = selling
                ? ShareMarket.ProceedsOf(shares, price)
                : ShareMarket.CostOf(shares, price);

            panel.Add(Line(selling ? Loc.T("invest.total_gain") : Loc.T("invest.total_cost"),
                (selling ? "+" : "-") + UiFormat.Money(total),
                selling ? "ticket--in" : "ticket--out"));

            var slider = new Slider(0f, 1f) { value = (float)parcel };
            slider.AddToClassList("ticket__slider");
            slider.RegisterValueChangedCallback(change =>
            {
                parcel = change.newValue;
                changed?.Invoke();
            });

            panel.Add(slider);

            var act = new Button(() =>
            {
                if (selling)
                {
                    simulation.TrySellShares(selected, shares, out _, out note);
                }
                else if (!simulation.TryBuyShares(selected, shares, out _, out var why))
                {
                    note = why;
                }

                changed?.Invoke();
            })
            {
                text = (selling ? "+" : "-") + UiFormat.Money(total)
            };

            act.AddToClassList("button");
            act.AddToClassList(selling ? "button--primary" : "button--armed");
            act.SetEnabled(shares > 0);
            panel.Add(act);

            panel.Add(BuildTakeover(simulation));

            if (!string.IsNullOrEmpty(note))
            {
                var said = new Label(note);
                said.AddToClassList("ticket__note");
                panel.Add(said);
            }

            return panel;
        }

        /// <summary>
        /// The most shares the company could pay for, so the slider never offers a purchase that
        /// will be refused.
        ///
        /// A control whose top third is always rejected is a control the player learns to distrust,
        /// and this is the same fault the free-tier slider shipped with once.
        /// </summary>
        private long Affordable(CompanySimulation simulation, double price)
        {
            var each = price * (1.0 + ShareMarket.CommissionRate);

            if (each <= 0.0)
            {
                return 0L;
            }

            return Math.Min(
                simulation.SharesAvailableIn(selected),
                (long)(simulation.State.CashUsd / each));
        }

        private VisualElement BuildTakeover(CompanySimulation simulation)
        {
            var block = new VisualElement();
            block.AddToClassList("ticket__takeover");

            if (!simulation.CanTakeOver(selected, out var cost, out var why))
            {
                var locked = new Label(why);
                locked.AddToClassList("ticket__locked");
                block.Add(locked);

                return block;
            }

            block.Add(Line(Loc.T("invest.takeover.cost"), UiFormat.Money(cost), "ticket--out"));

            var note2 = new Label(Loc.T("invest.takeover.note"));
            note2.AddToClassList("ticket__takenote");
            block.Add(note2);

            var buy = new Button(() =>
            {
                if (!simulation.TryTakeOver(selected, out var failure))
                {
                    note = failure;
                }

                changed?.Invoke();
            })
            { text = Loc.T("invest.takeover") };

            buy.AddToClassList("button");
            buy.AddToClassList("button--armed");
            buy.SetEnabled(simulation.State.CashUsd >= cost);
            block.Add(buy);

            return block;
        }

        private static Button Tab(string text, bool on, Action clicked)
        {
            var tab = new Button(clicked) { text = text };
            tab.AddToClassList("ticket__tab");
            tab.EnableInClassList("ticket__tab--on", on);
            return tab;
        }

        private static VisualElement Line(string label, string value, string modifier)
        {
            var row = new VisualElement();
            row.AddToClassList("ticket__line");

            var name = new Label(label);
            name.AddToClassList("ticket__label");
            row.Add(name);

            var reading = new Label(value);
            reading.AddToClassList("ticket__value");

            if (!string.IsNullOrEmpty(modifier))
            {
                reading.AddToClassList(modifier);
            }

            row.Add(reading);
            return row;
        }
    }
}
