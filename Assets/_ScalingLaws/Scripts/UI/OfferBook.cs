using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The term sheets on the table, all of them, side by side.
    ///
    /// **Reported: there was one offer, from nobody.** An offer you cannot compare with anything is
    /// not a decision, it is a yes or no, and the screen behind it said only how much and how much
    /// of the company. Several named firms at once is what makes it a choice, and the three numbers
    /// that separate them are on every row: what they price the company at, how much of it they
    /// want, and how long they will wait.
    ///
    /// A card over the screen rather than a panel on it, the same shape as the finished-run notice,
    /// because it is something the player asked to look at and dismisses by clicking away.
    /// </summary>
    public sealed class OfferBook
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        private VisualElement mounted;

        public OfferBook(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        public bool IsOpen => mounted != null && mounted.parent != null;

        public void Close()
        {
            mounted?.RemoveFromHierarchy();
            mounted = null;
        }

        public void Show(VisualElement host)
        {
            var simulation = company();
            if (host == null || simulation == null)
            {
                return;
            }

            Close();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => Close());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("offerbook");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var title = new Label(Loc.T("own.offers_title"));
            title.AddToClassList("notice__title");
            card.Add(title);

            var desk = simulation.State.Investors;

            var strap = new Label(desk.Count > 0
                ? Loc.T("own.offers_strap", Loc.Counted(desk.Count, "noun.offer"))
                : Loc.T("own.offers_none"));

            strap.AddToClassList("notice__body");
            card.Add(strap);

            var list = ScrollMemory.Keep(new ScrollView(), "offers");
            list.AddToClassList("offerbook__list");

            foreach (var offer in desk.Open)
            {
                list.Add(BuildRow(simulation, offer));
            }

            card.Add(list);

            var close = new Button(Close) { text = Loc.T("common.close") };
            close.AddToClassList("notice__button");
            card.Add(close);

            veil.Add(card);
            mounted = veil;
            host.Add(veil);

            AudioDirector.Page();
        }

        private VisualElement BuildRow(CompanySimulation simulation, FundingOffer offer)
        {
            var row = new VisualElement();
            row.AddToClassList("offerrow");

            if (!InvestorCatalog.TryGet(offer.Investor, out var definition))
            {
                return row;
            }

            if (ColorUtility.TryParseHtmlString(definition.AccentHex, out var accent))
            {
                row.style.borderLeftColor = accent;
            }

            var words = new VisualElement();
            words.AddToClassList("offerrow__words");

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("offerrow__name");
            words.Add(name);

            var kind = new Label(definition.KindName + "  ·  " + definition.Pitch);
            kind.AddToClassList("offerrow__pitch");
            words.Add(kind);

            // **Inside the words column, not beside it.** As a sibling at full width it forced the
            // row to wrap, and the row it was warning about was the one that came out unreadable.
            if (offer.IsDownRound)
            {
                var warn = new Label(Loc.T("own.down_round_row"));
                warn.AddToClassList("offerrow__down");
                words.Add(warn);
            }

            row.Add(words);

            var terms = new VisualElement();
            terms.AddToClassList("offerrow__terms");

            terms.Add(Reading(UiFormat.Money(offer.RaiseUsd), Loc.T("own.for_this_much")));
            terms.Add(Reading(UiFormat.Percent(offer.EquitySold, 1), Loc.T("own.of_the_company")));

            terms.Add(Reading(UiFormat.Money(offer.PreMoneyValuationUsd),
                Loc.T("own.values_you_at")));

            terms.Add(Reading(
                Loc.Counted(offer.DaysRemaining(simulation.State.Date), "noun.day"),
                Loc.T("own.left_to_decide")));

            row.Add(terms);

            var take = new Button(() =>
            {
                if (!simulation.TryTakeInvestorOffer(offer.Investor, out var why))
                {
                    AudioDirector.Deny();
                    var problem = new Label(why);
                    problem.AddToClassList("offerrow__problem");
                    row.Add(problem);
                    return;
                }

                Close();
                changed?.Invoke();
            })
            {
                text = Loc.T("own.take_it")
            };

            take.AddToClassList("button");
            take.AddToClassList("offerrow__take");
            row.Add(take);

            return row;
        }

        private static VisualElement Reading(string value, string caption)
        {
            var cell = new VisualElement();
            cell.AddToClassList("offerrow__cell");

            var figure = new Label(value);
            figure.AddToClassList("offerrow__figure");
            cell.Add(figure);

            var under = new Label(caption);
            under.AddToClassList("offerrow__caption");
            cell.Add(under);

            return cell;
        }
    }
}
