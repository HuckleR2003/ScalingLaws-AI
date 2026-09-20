using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>Which column the shop is ordered by.</summary>
    public enum PartsOrder
    {
        /// <summary>Newest silicon first, which is where a shop should open.</summary>
        Newest = 0,

        /// <summary>Most petaflops a unit.</summary>
        Power = 1,

        /// <summary>Cheapest unit.</summary>
        Price = 2,

        /// <summary>Most petaflops a dollar, which is the number that decides a purchase.</summary>
        Value = 3,

        /// <summary>Most memory, which is what a large run is actually blocked on.</summary>
        Memory = 4,

        /// <summary>Coolest, for a room whose cabinets are already warm.</summary>
        Heat = 5
    }

    /// <summary>
    /// The shop, as a window rather than as two buttons at the bottom of a page.
    ///
    /// **Reported plainly**: the parts could not be browsed or bought from the room they go in, and
    /// the only door anybody found was a pair of cards at the foot of the compute screen, which is
    /// a place a player reaches rarely and reads once. The room had a shop of its own all along and
    /// it showed three generations in a rail 306 pixels wide, which is a shelf rather than a shop.
    ///
    /// Everything here is a view. The price comes from `MarketModel.PurchasePricePerUnitUsd` with
    /// the founder and the home country on it, which is the same arithmetic the purchase charges, so
    /// the number on the row is the number that leaves the account. The purchase is
    /// `CompanySimulation.TryBuyHardware`, unchanged: this adds a door, not a rule.
    /// </summary>
    public sealed class PartsShop
    {
        /// <summary>How many units a row buys at once. The player picks one of these.</summary>
        public static readonly int[] Batches = { 1, 4, 16, 64 };

        private readonly CompanySimulation simulation;
        private readonly Action changed;

        /// <summary>Says the purchase went through, across the top of the screen.</summary>
        private readonly Action<string, string> announce;

        private readonly VisualElement rows = new();
        private readonly Label subtitle = new();

        private PartsOrder order = PartsOrder.Newest;
        private int batch = 4;

        /// <summary>
        /// Which column the shelf is sorted by.
        ///
        /// Settable because an EditMode element has no panel, so a click sent to a sort chip is
        /// never dispatched and the lambda behind it goes unmeasured. Same shape as
        /// <see cref="ManagementScreen.ShowDesk"/> and <see cref="FinanceReport.ShowDays"/>.
        /// </summary>
        public PartsOrder Order
        {
            get => order;
            set
            {
                order = value;
                Refresh();
            }
        }

        public PartsShop(CompanySimulation simulation, Action changed,
            Action<string, string> announce = null)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.changed = changed;
            this.announce = announce;

            Root = new VisualElement();
            Root.AddToClassList("shop");

            Build();
        }

        public VisualElement Root { get; }

        /// <summary>The tier a room purchase lands in. The room is colocated by definition.</summary>
        private const ComputeTier Tier = ComputeTier.ColocatedServers;

        /// <summary>
        /// Whether the company is allowed to buy silicon at all today.
        ///
        /// **The colocated tier asks for a shipped model and five million in the bank**, so a
        /// company that has just been given the room by Emil is refused at the till whatever is
        /// on the shelf. A shop whose every BUY fails silently is the shape this project has
        /// already shipped twice: a control no reachable state enables is not a button.
        /// </summary>
        public bool CanBuy => Gate().IsUnlocked;

        /// <summary>Why not, in the tier's own words. Empty when it is open.</summary>
        public string LockReason => Gate().LockReason;

        /// <summary>
        /// The gate, read from the tier rather than re-derived.
        ///
        /// Two readings of one rule is the disagreement with a date on it: the shop would go on
        /// saying the shelf is open for a week after the till started refusing it.
        /// </summary>
        private ComputeTierStatus Gate()
        {
            var state = simulation.State;

            return ComputeTierCatalog.Get(Tier).Evaluate(
                state.Date, state.CashUsd, state.ReleasedModelCount, state.LifetimeRevenueUsd);
        }

        private void Build()
        {
            var head = new VisualElement();
            head.AddToClassList("shop__head");

            var title = new Label(Loc.T("shop.title"));
            title.AddToClassList("shop__title");
            head.Add(title);

            subtitle.AddToClassList("shop__subtitle");
            head.Add(subtitle);

            Root.Add(head);

            // ---- how it is ordered -------------------------------------------------------------
            var sort = new VisualElement();
            sort.AddToClassList("shop__sort");

            var label = new Label(Loc.T("shop.sort"));
            label.AddToClassList("shop__sortlabel");
            sort.Add(label);

            foreach (PartsOrder option in Enum.GetValues(typeof(PartsOrder)))
            {
                var chosen = option;

                var chip = new Button(() =>
                {
                    order = chosen;
                    Refresh();
                })
                {
                    text = Loc.T(KeyFor(chosen))
                };

                chip.AddToClassList("shop__chip");
                chip.userData = chosen;
                sort.Add(chip);
            }

            Root.Add(sort);

            // ---- how many at a time ------------------------------------------------------------
            var size = new VisualElement();
            size.AddToClassList("shop__sort");

            var sizeLabel = new Label(Loc.T("shop.batch"));
            sizeLabel.AddToClassList("shop__sortlabel");
            size.Add(sizeLabel);

            foreach (var count in Batches)
            {
                var chosen = count;

                var chip = new Button(() =>
                {
                    batch = chosen;
                    Refresh();
                })
                {
                    text = "x" + count
                };

                chip.AddToClassList("shop__chip");
                chip.AddToClassList("shop__chip--batch");
                chip.userData = chosen;
                size.Add(chip);
            }

            Root.Add(size);

            rows.AddToClassList("shop__rows");
            Root.Add(rows);
        }

        /// <summary>
        /// What a unit costs today, including everything that moves it.
        ///
        /// **The same call the purchase makes.** A shop that priced things itself would be a second
        /// opinion, and the first time scarcity moved, the row and the till would disagree.
        /// </summary>
        public long UnitPriceUsd(HardwareGeneration generation)
        {
            var tier = ComputeTierCatalog.Get(Tier);
            var state = simulation.State;

            return (long)Math.Round(
                MarketModel.PurchasePricePerUnitUsd(
                    generation, tier, MarketModel.ScarcityOn(state.Date))
                * state.Founder.HardwarePriceMultiplier
                * state.Home.HardwarePriceMultiplier);
        }

        /// <summary>Everything on sale today, in the order the player asked for.</summary>
        public IReadOnlyList<HardwareGeneration> OnSale()
        {
            var today = simulation.State.Date;

            var available = HardwareCatalog.All
                .Where(generation => generation.Class == HardwareClass.Accelerator)
                .Where(generation => generation.IsAvailableOn(today))
                .ToList();

            IEnumerable<HardwareGeneration> ordered = order switch
            {
                PartsOrder.Power => available.OrderByDescending(g => g.PetaflopsPerUnit),
                PartsOrder.Price => available.OrderBy(UnitPriceUsd),
                PartsOrder.Value => available.OrderByDescending(
                    g => UnitPriceUsd(g) <= 0 ? 0.0 : g.PetaflopsPerUnit / UnitPriceUsd(g)),
                PartsOrder.Memory => available.OrderByDescending(g => g.MemoryGigabytes),
                PartsOrder.Heat => available.OrderBy(g => g.PowerKilowatts),
                _ => available.OrderByDescending(g => g.ReleaseDate.DayIndex)
            };

            return ordered.ToList();
        }

        public void Refresh()
        {
            var state = simulation.State;
            var stock = OnSale();

            subtitle.text = CanBuy
                ? Loc.T("shop.subtitle", stock.Count, UiFormat.Money(state.CashUsd))
                : LockReason;

            foreach (var chip in Root.Query<Button>(className: "shop__chip").ToList())
            {
                chip.EnableInClassList("shop__chip--on",
                    chip.userData is PartsOrder option && option == order
                    || chip.userData is int count && count == batch);
            }

            rows.Clear();

            foreach (var generation in stock)
            {
                rows.Add(Row(generation));
            }
        }

        private VisualElement Row(HardwareGeneration generation)
        {
            var price = UnitPriceUsd(generation);
            var total = price * batch;
            var affordable = simulation.State.CashUsd >= total && CanBuy;

            var row = new VisualElement();
            row.AddToClassList("shoprow");
            row.EnableInClassList("shoprow--dear", !affordable);

            // ---- who it is ---------------------------------------------------------------------
            var identity = new VisualElement();
            identity.AddToClassList("shoprow__identity");

            var name = new Label(generation.DisplayName.ToUpperInvariant());
            name.AddToClassList("shoprow__name");
            identity.Add(name);

            var vendor = new Label(generation.VendorName + " · " + generation.ReleaseDate);
            vendor.AddToClassList("shoprow__vendor");
            identity.Add(vendor);

            row.Add(identity);

            // ---- what it is --------------------------------------------------------------------
            row.Add(Figure(Loc.T("shop.power"), UiFormat.Petaflops(generation.PetaflopsPerUnit)));
            row.Add(Figure(Loc.T("shop.memory"), generation.MemoryGigabytes + " GB"));
            row.Add(Figure(Loc.T("shop.heat"), UiFormat.Kilowatts(generation.PowerKilowatts)));

            // **Petaflops a dollar, spelled out.** It is the number that decides between two cards
            // and the one nobody works out in their head.
            row.Add(Figure(Loc.T("shop.value"),
                UiFormat.Number(generation.PetaflopsPerUnit / Math.Max(1.0, price / 1_000_000.0), 2)));

            // ---- what it costs -----------------------------------------------------------------
            var cost = new VisualElement();
            cost.AddToClassList("shoprow__cost");

            var each = new Label(UiFormat.Money(price));
            each.AddToClassList("shoprow__each");
            cost.Add(each);

            var sum = new Label(Loc.T("shop.for_batch", batch, UiFormat.Money(total)));
            sum.AddToClassList("shoprow__sum");
            cost.Add(sum);

            // The ceiling before the click rather than after it. Cards the cellar has slots for do
            // not count, and `PowerAfterOrder` already knows that.
            var power = simulation.PowerAfterOrder(generation, batch, Tier);
            cost.Add(UiParts.SitePowerLine(power));

            row.Add(cost);

            var buy = new Button(() => OpenOrder(generation, price, total))
            {
                text = Loc.T("shop.buy")
            };

            buy.AddToClassList("shoprow__buy");
            buy.SetEnabled(affordable && power.Fits);
            row.Add(buy);

            // **Projected silicon says so.** The catalogue knows where the real products stop, and a
            // card the game invented must never be read as a dated announcement.
            if (generation.IsProjection)
            {
                row.AddToClassList("shoprow--projected");
                buy.tooltip = Loc.T("room.silicon.projected");
            }

            InsightTip.AttachKeyed(row, generation.DisplayName, Loc.T("shop.row_note"));

            return row;
        }

        /// <summary>
        /// The order window: what it costs, when it lands, and how much of that wait money buys off.
        ///
        /// **Asked for after a playtest where BUY spent the money on the click.** Sixty four
        /// accelerators is a serious purchase and the only thing a player saw before it left the
        /// account was the price on a button. Now the figures sit still for a moment and the rush
        /// fee is a decision: the slider is the one place in this game where money buys calendar,
        /// and it says what it costs while it does it.
        /// </summary>
        private void OpenOrder(HardwareGeneration generation, long unitPrice, long total)
        {
            var lead = ComputeTierCatalog.Get(Tier).LeadTimeDays;
            var express = 0f;

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => veil.RemoveFromHierarchy());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("partsorder");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var title = new Label(generation.DisplayName.ToUpperInvariant());
            title.AddToClassList("partsorder__title");
            card.Add(title);

            var units = new Label(Loc.T("order.units", batch, UiFormat.Money(unitPrice)));
            units.AddToClassList("partsorder__line");
            card.Add(units);

            var cost = new Label();
            cost.AddToClassList("partsorder__cost");
            card.Add(cost);

            var arrives = new Label();
            arrives.AddToClassList("partsorder__line");
            card.Add(arrives);

            var wait = new Label();
            wait.AddToClassList("partsorder__note");
            card.Add(wait);

            var slider = new Slider(0f, 1f) { value = 0f };
            slider.AddToClassList("partsorder__slider");
            card.Add(slider);

            var rush = new Label();
            rush.AddToClassList("partsorder__note");
            card.Add(rush);

            var confirm = new Button { text = Loc.T("order.confirm") };
            confirm.AddToClassList("button");
            confirm.AddToClassList("partsorder__confirm");
            card.Add(confirm);

            var cancel = new Button(() => veil.RemoveFromHierarchy()) { text = Loc.T("order.cancel") };
            cancel.AddToClassList("button");
            card.Add(cancel);

            void Reprice()
            {
                var terms = CompanySimulation.ExpressTerms(total, lead, express);
                var due = total + terms.SurchargeUsd;

                cost.text = UiFormat.Money(due);
                arrives.text = Loc.T("order.arrives",
                    simulation.State.Date.AddDays(terms.LeadTimeDays).ToString());
                wait.text = Loc.T("order.wait", terms.LeadTimeDays);
                rush.text = terms.SurchargeUsd <= 0L
                    ? Loc.T("order.rush_none")
                    : Loc.T("order.rush", UiFormat.Money(terms.SurchargeUsd),
                        UiFormat.Percent(1.0 - terms.LeadTimeDays / (double)Math.Max(1, lead), 0));

                confirm.SetEnabled(simulation.State.CashUsd >= due);
            }

            slider.RegisterValueChangedCallback(change =>
            {
                express = change.newValue;
                Reprice();
            });

            confirm.clicked += () =>
            {
                if (simulation.TryBuyHardware(generation.Id, batch, Tier, express, out var why))
                {
                    AudioDirector.Confirm();

                    var terms = CompanySimulation.ExpressTerms(total, lead, express);

                    // **Money left the account and the only thing that moved was a figure in a
                    // rail.** Reported as buying without knowing you bought.
                    announce?.Invoke(Loc.T("room.bought.title"),
                        Loc.T("room.bought.note", batch, generation.DisplayName,
                            UiFormat.Money(total + terms.SurchargeUsd), terms.LeadTimeDays));

                    veil.RemoveFromHierarchy();
                    changed?.Invoke();
                    Refresh();
                    return;
                }

                AudioDirector.Deny();
                subtitle.text = why;
                veil.RemoveFromHierarchy();
            };

            Reprice();
            veil.Add(card);
            Root.Add(veil);
            AudioDirector.Page();
        }

        private static VisualElement Figure(string caption, string value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("shoprow__cell");

            var label = new Label(caption);
            label.AddToClassList("shoprow__label");
            cell.Add(label);

            var figure = new Label(value);
            figure.AddToClassList("shoprow__figure");
            cell.Add(figure);

            return cell;
        }

        private static string KeyFor(PartsOrder order) => order switch
        {
            PartsOrder.Power => "shop.power",
            PartsOrder.Price => "shop.price",
            PartsOrder.Value => "shop.value",
            PartsOrder.Memory => "shop.memory",
            PartsOrder.Heat => "shop.heat",
            _ => "shop.newest"
        };
    }
}
