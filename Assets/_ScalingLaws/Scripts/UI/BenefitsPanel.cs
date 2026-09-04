using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the company offers beyond the salary, and what that costs.
    ///
    /// **Two figures on every row and the second one is the point.** A benefit's price per person is
    /// a small number that nobody reacts to; the same number times the headcount is a line on the
    /// monthly bill. Showing only the first is how a company ticks all six and then wonders where
    /// the money went.
    ///
    /// The running total is grey while it is hypothetical and green once it is being paid, which is
    /// the one piece of colour on this panel and the only thing it has to say.
    /// </summary>
    public sealed class BenefitsPanel
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        public BenefitsPanel(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        public VisualElement Build()
        {
            var simulation = company();
            var state = simulation.State;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("benefits.title"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var strap = new Label(Loc.T("benefits.strap"));
            strap.AddToClassList("field__hint");
            panel.Add(strap);

            // The one figure that says whether any of this is working. It reads the same function
            // the poaching maths reads, so a company that looks loyal here is genuinely hard to
            // raid rather than merely well decorated.
            var average = Loyalty.Average(state.Staff.Hires, state.Date, state.BenefitPoints);

            var summary = new VisualElement();
            summary.AddToClassList("benefits__summary");

            summary.Add(Figure(Loc.T("benefits.average"),
                state.Staff.Headcount == 0
                    ? "-"
                    : $"{average:0} · {Loyalty.NameOf(Loyalty.BandFor(average))}"));

            var perHead = BenefitCatalog.MonthlyCostPerHead(state.Benefits);

            summary.Add(Figure(
                Loc.T("benefits.per_head", UiFormat.Money(perHead)),
                Loc.T("benefits.total",
                    UiFormat.Money(perHead * Math.Max(0, state.Staff.Headcount)),
                    Loc.Counted(state.Staff.Headcount, "noun.person"))));

            panel.Add(summary);

            var grid = new VisualElement();
            grid.AddToClassList("benefits__grid");

            foreach (var definition in BenefitCatalog.All)
            {
                grid.Add(BuildTile(simulation, definition));
            }

            panel.Add(grid);

            if (state.Benefits.Count == 0)
            {
                var none = new Label(Loc.T("benefits.none"));
                none.AddToClassList("field__hint");
                panel.Add(none);
            }

            return panel;
        }

        private VisualElement BuildTile(CompanySimulation simulation, BenefitDefinition definition)
        {
            var state = simulation.State;
            var on = state.Benefits.Contains(definition.Benefit);

            var tile = new Button(() =>
            {
                // The simulation owns the set, because the bill is charged from the day loop and a
                // panel that kept its own copy would be a second place deciding what is offered.
                if (!state.Benefits.Remove(definition.Benefit))
                {
                    state.Benefits.Add(definition.Benefit);
                }

                changed?.Invoke();
            });

            tile.AddToClassList("benefit");
            tile.EnableInClassList("benefit--on", on);

            var head = new VisualElement();
            head.AddToClassList("benefit__head");

            var box = new VisualElement();
            box.AddToClassList("benefit__box");
            box.EnableInClassList("benefit__box--on", on);
            head.Add(box);

            var name = new Label(definition.DisplayName);
            name.AddToClassList("benefit__name");
            head.Add(name);

            tile.Add(head);

            var note = new Label(definition.Note);
            note.AddToClassList("benefit__note");
            tile.Add(note);

            var bill = new VisualElement();
            bill.AddToClassList("benefit__bill");

            // **Both figures say what they are.** The tile used to print two bare amounts, a
            // per-head price and a payroll total, one above the other with nothing naming either.
            // Reported from a playtest as "it gives an amount per employee but does not say that
            // is what it is", which is exactly what two unlabelled numbers look like.
            var each = new Label(Loc.T("benefits.per_head",
                UiFormat.Money(definition.MonthlyCostPerHeadUsd)));

            each.AddToClassList("benefit__each");
            bill.Add(each);

            // What it would cost across the payroll as it stands, which is the number that decides
            // this and the one a per-person price hides.
            var total = new Label(Loc.T("benefits.everybody",
                UiFormat.Money(definition.MonthlyCostPerHeadUsd * Math.Max(0, state.Staff.Headcount)),
                Loc.Counted(Math.Max(0, state.Staff.Headcount), "noun.person")));

            total.AddToClassList("benefit__total");
            total.EnableInClassList("benefit__total--on", on);
            bill.Add(total);

            tile.Add(bill);

            var gain = new Label(Loc.T("benefits.loyalty",
                definition.LoyaltyPoints.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));

            gain.AddToClassList("benefit__gain");
            tile.Add(gain);

            return tile;
        }

        private static VisualElement Figure(string caption, string value)
        {
            var block = new VisualElement();
            block.AddToClassList("benefits__figure");

            var label = new Label(caption);
            label.AddToClassList("benefits__caption");
            block.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("benefits__value");
            block.Add(reading);

            return block;
        }
    }
}
