using System;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The corporation tax demand, as a decision rather than as a letter in a pile.
    ///
    /// **Reported by Natalia: the only thing the player could do about tax was defer it, and that
    /// happened the instant they clicked.** The demand arrived as a strip across the top and a letter
    /// in the inbox; paying it meant finding the letter, and the POSTPONE button committed the
    /// company to another year of interest with no sentence in between saying what that cost. So the
    /// cheaper of the two decisions was two clicks away and the dearer one was one click away.
    ///
    /// Two states, one card. The demand, and then what postponing it did, which is where the figures
    /// go: **a player is told the price on the day rather than finding it in next January's total.**
    /// That is the same rule the carry-forward notice already follows.
    /// </summary>
    public sealed class TaxDemandDialog
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action closed;

        private VisualElement mounted;

        public TaxDemandDialog(Func<CompanySimulation> company, Action closed)
        {
            this.company = company;
            this.closed = closed;
        }

        /// <summary>True while the card is up. The shell keeps the clock stopped for exactly this long.</summary>
        public bool IsOpen => mounted?.panel != null;

        public void Close()
        {
            mounted?.RemoveFromHierarchy();
            mounted = null;
            closed?.Invoke();
        }

        /// <summary>
        /// Puts the demand in front of the player, on a host that is the panel root.
        ///
        /// It refuses to open twice over the same letter, because the demand is raised once and the
        /// shell drains events in a loop: a second card on top of the first would take two dismissals
        /// to clear and the one underneath would be answering a letter already dealt with.
        /// </summary>
        public void Show(VisualElement host, MailItem letter)
        {
            if (host == null || letter == null || letter.IsClosed || letter.AmountUsd <= 0L)
            {
                return;
            }

            mounted?.RemoveFromHierarchy();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");

            // **No dismiss on the veil, unlike every other notice.** The others are telling the
            // player something; this one is asking, and a decision that can be clicked past by
            // missing the card is a decision the game made on their behalf.
            veil.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            veil.Add(BuildDemand(letter));

            host.Add(veil);
            mounted = veil;

            AudioDirector.Page();
        }

        private VisualElement BuildDemand(MailItem letter)
        {
            var simulation = company();

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("taxcard");

            var title = new Label(Loc.T("tax.now.title"));
            title.AddToClassList("notice__title");
            card.Add(title);

            var amount = new Label(UiFormat.Money(letter.AmountUsd));
            amount.AddToClassList("taxcard__amount");
            card.Add(amount);

            var body = new Label(Loc.T("tax.now.body",
                (simulation.State.Date.Year - 1).ToString(),
                UiFormat.Money(letter.AmountUsd),
                new GameDate(letter.DueDayIndex).ToString()));

            body.AddToClassList("notice__body");
            card.Add(body);

            var left = Math.Max(0,
                CompanySimulation.MostPostponements
                - letter.DeferredDays / CompanySimulation.DeferralStepDays);

            var note = new Label(Loc.T("tax.now.left", left.ToString()));
            note.AddToClassList("notice__note");
            card.Add(note);

            var affordable = simulation.State.CashUsd >= letter.AmountUsd;

            if (!affordable)
            {
                var cannot = new Label(Loc.T("tax.now.cannot",
                    UiFormat.Money(simulation.State.CashUsd)));

                cannot.AddToClassList("taxcard__cannot");
                card.Add(cannot);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("notice__buttons");

            var pay = new Button(() => Answer(letter, MailAction.Pay))
            {
                text = Loc.T("tax.now.pay")
            };

            pay.AddToClassList("notice__button");
            pay.AddToClassList("notice__button--go");
            pay.AddToClassList("taxcard__button");
            pay.SetEnabled(affordable);
            buttons.Add(pay);

            // Both exist even when only one can be pressed. A card that offered nothing but POSTPONE
            // to a company that cannot pay would read as the game hiding the option, and a company
            // that can pay has to be able to see the price of not doing so.
            var defer = new Button(() => Answer(letter, MailAction.Defer))
            {
                text = Loc.T("tax.now.defer")
            };

            defer.AddToClassList("notice__button");
            defer.AddToClassList("taxcard__button");
            defer.SetEnabled(left > 0);
            buttons.Add(defer);

            card.Add(buttons);
            return card;
        }

        /// <summary>
        /// Acts on the letter and decides what the card does next.
        ///
        /// Paying closes it: there is nothing further to say about a bill that is settled, and the
        /// money leaving is visible on the bar the moment the card goes. Postponing stays, because
        /// the whole complaint was that its price was invisible.
        ///
        /// Public because the two buttons are the only callers and an EditMode element has no panel,
        /// so a click sent to one is never dispatched. Same shape as `FinanceReport.ShowDays`.
        /// </summary>
        public void Answer(MailItem letter, MailAction action)
        {
            var simulation = company();
            var before = letter.AmountUsd;

            if (!simulation.TryActOnMail(letter.Id, action, out var why))
            {
                AudioDirector.Deny();

                var problem = mounted?.Q<Label>(className: "taxcard__cannot");
                if (problem != null)
                {
                    problem.text = why;
                }

                return;
            }

            AudioDirector.Confirm();

            if (action == MailAction.Pay)
            {
                Close();
                return;
            }

            ShowPostponed(letter, before);
        }

        /// <summary>
        /// What the postponement cost, in two lines, on the day it was taken.
        ///
        /// The first is the rate and what it added. The second is the sum and the date it is now due,
        /// which is the figure a player has to plan against. Both are read off the letter after the
        /// simulation has moved it, so the card cannot quote a price the books disagree with.
        /// </summary>
        private void ShowPostponed(MailItem letter, long before)
        {
            if (mounted == null)
            {
                return;
            }

            mounted.Clear();

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("taxcard");

            var title = new Label(Loc.T("tax.done.title"));
            title.AddToClassList("notice__title");
            card.Add(title);

            var added = Math.Max(0L, letter.AmountUsd - before);

            var rate = new Label(Loc.T("tax.done.rate",
                CompanySimulation.DeferralInterest.ToString("P1",
                    System.Globalization.CultureInfo.InvariantCulture),
                UiFormat.Money(added)));

            rate.AddToClassList("taxcard__rate");
            card.Add(rate);

            var total = new Label(Loc.T("tax.done.total",
                UiFormat.Money(letter.AmountUsd),
                new GameDate(letter.DueDayIndex).ToString()));

            total.AddToClassList("taxcard__total");
            card.Add(total);

            // **The warning belongs on the postponement that earns it, not on the one after.** A
            // player who has just used the last one has to be told here: the next date is not another
            // letter with buttons on it, it is the whole arrears leaving the account with a fifth on
            // top, whether or not the money is there.
            if (letter.DeferredDays >= CompanySimulation.LongestDeferralDays)
            {
                var last = new Label(Loc.T("tax.done.last",
                    CompanySimulation.RefusedDeferralPenalty.ToString("P0",
                        System.Globalization.CultureInfo.InvariantCulture)));

                last.AddToClassList("taxcard__last");
                card.Add(last);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("notice__buttons");

            var ok = new Button(Close) { text = Loc.T("common.ok") };
            ok.AddToClassList("notice__button");
            ok.AddToClassList("notice__button--go");
            ok.AddToClassList("taxcard__button");
            buttons.Add(ok);

            card.Add(buttons);
            mounted.Add(card);
        }
    }
}
