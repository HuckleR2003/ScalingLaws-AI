using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the company charges, and the four figures that say whether it is working.
    ///
    /// **Reported: the page opened on a price per token and that was off-putting.** It was worse
    /// than off-putting. Nothing else in the game ever mentions a token price, the creator and the
    /// release card both ask for a monthly subscription, and a fresh company was billing per token,
    /// so the one price control the player is given reached the version line, was printed back to
    /// them on the release screen, and never entered the till. That is fixed in
    /// `MonetizationPolicy`; this screen is the half the player looks at.
    ///
    /// Three things were asked for here and each one answers a different complaint:
    ///
    /// **The slider is locked.** Setting a subscription price by brushing a control is the one slip
    /// in this game that is felt immediately by every customer the company has, and the release
    /// planner has had exactly this lock since it was written. CHANGE opens it, APPLY commits it,
    /// and leaving without applying leaves the company where it was.
    ///
    /// **The four tiles.** The official page already draws registered, paying, this month and net,
    /// and a price with no audience beside it is a number with nothing to judge it against. They are
    /// `UiParts.KpiRow`, the same element, so the two pages cannot drift.
    ///
    /// **The picker scopes what is shown, not what is changed**, and that needs saying because the
    /// note that asked for it assumed otherwise. `CompanySimulation.SyncPricing` has one rate for
    /// the whole company on purpose, with the reason written beside it: a lab does not quietly
    /// charge four different rates for the same API. So choosing a model re-reads the four tiles for
    /// that product, and applying a price still moves it for everything on sale. The card that comes
    /// up before it commits says that in those words, every time, rather than only when the picker
    /// happens to read ALL MODELS.
    /// </summary>
    public sealed class BusinessPricingPanel
    {
        /// <summary>The picker's first row: the company rather than one of its products.</summary>
        public const int EverythingOnSale = -1;

        private readonly Func<CompanySimulation> company;
        private readonly Action changed;
        private readonly Action<string, string, string, Action> ask;

        /// <summary>
        /// The free tier, built by the shell and stood beside the fee.
        ///
        /// **Beside rather than under, and that is a decision from August worth keeping.** What
        /// you charge and what you give away are the same question asked twice, and reading them
        /// a screen apart is what makes a generous free tier look free. It is handed in rather
        /// than built here because it belongs to the shell, which owns the sliders that write it.
        /// </summary>
        private readonly Func<VisualElement> aside;

        private readonly VisualElement root = new();

        private bool unlocked;
        private float pending;
        private int scope = EverythingOnSale;

        /// <param name="ask">Title, body, confirm caption, and what to run on yes.</param>
        /// <summary>Says the price moved. Set by the shell.</summary>
        public Action<string, string, NoticeTone> announce;

        public BusinessPricingPanel(Func<CompanySimulation> company, Action changed,
            Action<string, string, string, Action> ask, Func<VisualElement> aside = null)
        {
            this.company = company;
            this.changed = changed;
            this.ask = ask;
            this.aside = aside;

            root.AddToClassList("panel");
            root.AddToClassList("bizprice");
        }

        public VisualElement Root => root;

        /// <summary>Whether CHANGE has been pressed. Read by the tests, and by nothing else.</summary>
        public bool IsUnlocked => unlocked;

        /// <summary>What APPLY would commit. Equals the company's price while the lock is shut.</summary>
        public double PendingPriceUsdPerMonth => pending;

        /// <summary>Which product the figures are about, or <see cref="EverythingOnSale"/>.</summary>
        public int Scope => scope;

        /// <summary>
        /// Opens the lock.
        ///
        /// Public because the button is its only caller and an EditMode element has no panel, so a
        /// click sent to a button is never dispatched. Same shape as `ManagementScreen.ShowDesk`.
        /// </summary>
        public void OpenTheLock()
        {
            unlocked = true;
            pending = (float)CurrentPrice();
            Refresh();
        }

        /// <summary>What the slider calls while the lock is open. Nothing is charged yet.</summary>
        public void SetPrice(double usdPerMonth)
        {
            pending = (float)Math.Clamp(usdPerMonth, ReleasePlanPanel.MinimumPriceUsd,
                ReleasePlanPanel.MaximumPriceUsd);
        }

        /// <summary>Which product the four tiles are about.</summary>
        public void SelectScope(int index)
        {
            scope = index;
            Refresh();
        }

        /// <summary>
        /// Asks, and commits only if the answer is yes.
        ///
        /// **The warning is unconditional.** The note that asked for this expected it only when the
        /// picker read ALL MODELS, on the assumption that choosing one model would scope the change
        /// to it. It does not and cannot: the subscription is one company-wide rate. A warning that
        /// appeared only sometimes would teach the player that the other times are safe.
        /// </summary>
        public void Apply()
        {
            if (!unlocked)
            {
                return;
            }

            var price = pending;

            ask?.Invoke(
                Loc.T("biz.price_warn.title"),
                Loc.T("biz.price_warn.body", UiFormat.Money((long)Math.Round(price))),
                Loc.T("biz.price_apply"),
                () => Commit(price));
        }

        /// <summary>Writes the price. The only place on this screen that moves money.</summary>
        private void Commit(double price)
        {
            var simulation = company();
            if (simulation == null)
            {
                return;
            }

            simulation.State.Monetization.SubscriptionPriceUsdPerMonth = price;
            unlocked = false;

            AudioDirector.Confirm();

            announce?.Invoke(Loc.T("notice.price_changed"),
                Loc.T("notice.price_changed.note", UiFormat.Money((long)Math.Round(price))),
                NoticeTone.Standard);
            changed?.Invoke();
        }

        /// <summary>Shuts the lock and forgets the pending figure.</summary>
        public void Cancel()
        {
            unlocked = false;
            pending = (float)CurrentPrice();
            Refresh();
        }

        public void Refresh()
        {
            root.Clear();

            var simulation = company();
            if (simulation == null)
            {
                return;
            }

            var policy = simulation.State.Monetization;
            var market = simulation.Market.PricePerMillionTokensUsd;

            if (!unlocked)
            {
                pending = (float)CurrentPrice();
            }

            var heading = new Label(Loc.T("panel.pricing"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.Pricing);
            root.Add(heading);

            root.Add(BuildModelRow(policy));

            // The fee on the left, what is given away on the right, the audience underneath both.
            var pair = new VisualElement();
            pair.AddToClassList("bizprice__pair");

            var left = new VisualElement();
            left.AddToClassList("bizprice__half");

            if (policy.Model == PricingModel.Subscription)
            {
                left.Add(BuildSubscription(policy, market));
            }
            else if (policy.Model == PricingModel.PayPerToken)
            {
                left.Add(Hint(Loc.T("money.metered.note")));
            }
            else
            {
                left.Add(Hint(Loc.T("money.free_only_hint")));
            }

            // **The picker and the four figures sit under the fee, inside its column.** Under the
            // whole row they fell past the bottom of the window, because the free tier beside
            // them is six rows tall, and the left column was left with a third of its height
            // empty. One change fixes both: the answer to "is this price working" is now beside
            // the price rather than a scroll away from it.
            left.Add(BuildScopePicker(simulation));
            left.Add(UiParts.KpiRow(StandingForScope(simulation), policy.PaidShareOfTokens));

            pair.Add(left);

            if (aside?.Invoke() is { } beside)
            {
                beside.AddToClassList("bizprice__half");
                pair.Add(beside);
            }

            root.Add(pair);
        }

        /// <summary>
        /// The three ways to bill, with the one the rest of the game is written for first.
        /// </summary>
        private VisualElement BuildModelRow(MonetizationPolicy policy)
        {
            var row = new VisualElement();
            row.AddToClassList("bizprice__models");

            // **Subscription first, deliberately.** It is what the creator and the release card ask
            // for and what a company now opens on, so it is the row the eye should land on.
            foreach (var option in new[]
                     {
                         PricingModel.Subscription, PricingModel.PayPerToken, PricingModel.FreeOnly
                     })
            {
                var captured = option;
                var button = new Button(() =>
                {
                    policy.Model = captured;

                    // A billing model the company is not on has no pending price to keep.
                    unlocked = false;
                    changed?.Invoke();
                })
                {
                    text = MonetizationCatalog.PricingName(option).ToUpperInvariant()
                };

                button.AddToClassList("button");
                button.AddToClassList("bizprice__model");
                button.EnableInClassList("bizprice__model--on", policy.Model == option);
                button.SetEnabled(policy.Model != option);
                row.Add(button);
            }

            return row;
        }

        private VisualElement BuildSubscription(MonetizationPolicy policy, double market)
        {
            var block = new VisualElement();
            block.AddToClassList("bizprice__fee");

            var shown = unlocked ? pending : policy.SubscriptionPriceUsdPerMonth;

            var reading = new Label(Loc.T("biz.fee_reading",
                UiFormat.Money((long)Math.Round(shown))));

            reading.AddToClassList("bizprice__reading");
            block.Add(reading);

            // **Where it sits against the market, because standing still is now a decision.** The
            // market rate halves about every year and a monthly fee does not move, so a company that
            // never comes back here drifts into charging multiples of the going rate and eventually
            // reads about it. A screen that shows the fee and not the gap is hiding the half that
            // moves on its own.
            block.Add(BuildAgainstMarket(shown, market, company().State.Date));

            var row = new VisualElement();
            row.AddToClassList("bizprice__sliderrow");

            var slider = new Slider(ReleasePlanPanel.MinimumPriceUsd,
                ReleasePlanPanel.MaximumPriceUsd)
            {
                value = (float)shown
            };

            slider.AddToClassList("bizprice__slider");
            slider.SetEnabled(unlocked);

            // The reading is updated in place rather than by rebuilding, because rebuilding the
            // panel mid-drag destroys the handle under the cursor.
            slider.RegisterValueChangedCallback(evt =>
            {
                SetPrice(evt.newValue);
                reading.text = Loc.T("biz.fee_reading",
                    UiFormat.Money((long)Math.Round(pending)));
            });

            row.Add(slider);

            var act = new Button(() =>
            {
                if (unlocked)
                {
                    Apply();
                }
                else
                {
                    OpenTheLock();
                }
            })
            {
                text = unlocked ? Loc.T("biz.price_apply") : Loc.T("biz.price_change")
            };

            act.AddToClassList("button");
            act.AddToClassList("bizprice__act");
            act.EnableInClassList("bizprice__act--armed", unlocked);
            row.Add(act);

            if (unlocked)
            {
                var back = new Button(Cancel) { text = Loc.T("common.back") };
                back.AddToClassList("button");
                back.AddToClassList("bizprice__cancel");
                row.Add(back);
            }

            block.Add(row);
            block.Add(Hint(Loc.T("money.subscription_hint")));
            return block;
        }

        /// <summary>
        /// One sentence saying whether the fee is near the going rate, and how far off if not.
        /// </summary>
        private static VisualElement BuildAgainstMarket(double shown, double market, GameDate date)
        {
            var line = new Label();
            line.AddToClassList("bizprice__against");

            if (market <= 0.0)
            {
                line.text = string.Empty;
                return line;
            }

            var rate = shown / (MonetizationCatalog.TokensPerSubscriberPerMonthOn(date) / 1_000_000.0);
            var against = rate / market;

            if (against > ModelScandals.PricyAbove)
            {
                line.text = Loc.T("biz.against_market.steep",
                    UiFormat.Number(against, 1), UiFormat.Money((long)Math.Round(market)));

                line.AddToClassList("bizprice__against--steep");
            }
            else if (against > 1.1)
            {
                line.text = Loc.T("biz.against_market.over", UiFormat.Number(against, 1));
            }
            else if (against < 0.9)
            {
                line.text = Loc.T("biz.against_market.under",
                    UiFormat.Percent(1.0 - against, 0));
            }
            else
            {
                line.text = Loc.T("biz.against_market.at");
            }

            return line;
        }

        /// <summary>
        /// ALL MODELS, or one of them. It changes what the four tiles are about and nothing else.
        /// </summary>
        private VisualElement BuildScopePicker(CompanySimulation simulation)
        {
            var block = new VisualElement();
            block.AddToClassList("bizprice__scope");

            var sold = simulation.MarketedModels();

            var choices = new List<string> { Loc.T("biz.all_models") };
            foreach (var record in sold)
            {
                choices.Add(record.Model.Name);
            }

            var picker = new DropdownField(choices, ChoiceIndexFor(sold));
            picker.AddToClassList("bizprice__picker");

            picker.RegisterValueChangedCallback(evt =>
                SelectScope(choices.IndexOf(evt.newValue) - 1));

            block.Add(picker);

            // A company with nothing on sale gets the row anyway, reading ALL MODELS over four
            // zeroes. Hiding it would mean the control appears the first time a model ships, which
            // reads as the screen having changed rather than the company.
            if (sold.Count == 0)
            {
                block.Add(Hint(Loc.T("biz.nothing_on_sale")));
            }

            return block;
        }

        private int ChoiceIndexFor(IReadOnlyList<ModelRecord> sold) =>
            scope >= 0 && scope < sold.Count ? scope + 1 : 0;

        /// <summary>
        /// The figures the tiles draw: the company, or one product of it.
        ///
        /// A scope pointing past the end falls back to the company rather than throwing. The list is
        /// rebuilt whenever a model is superseded or withdrawn, and an index kept across that is a
        /// crash waiting for the day somebody ships an update while this screen is open.
        /// </summary>
        private ProductStanding StandingForScope(CompanySimulation simulation)
        {
            var sold = simulation.MarketedModels();

            if (scope < 0 || scope >= sold.Count)
            {
                scope = EverythingOnSale;
                return simulation.Product();
            }

            return simulation.ProductFor(sold[scope]);
        }

        private double CurrentPrice()
        {
            var simulation = company();
            return simulation?.State.Monetization.SubscriptionPriceUsdPerMonth
                   ?? MonetizationPolicy.OpeningSubscriptionUsdPerMonth;
        }

        private static Label Hint(string text)
        {
            var hint = new Label(text);
            hint.AddToClassList("field__hint");
            return hint;
        }
    }
}
