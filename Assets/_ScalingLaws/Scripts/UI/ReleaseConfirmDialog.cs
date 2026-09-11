using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The last look at a model before anybody can buy it.
    ///
    /// **Reported: clicking a model on the release screen shipped it, immediately, at whatever price
    /// the company happened to be charging.** The tutorial says at step 44 to click it and set
    /// something; there was nothing to set. A run that took two hundred days and most of the
    /// company's cash went on sale on a single click with no screen in between, and the two terms
    /// that decide whether anybody pays for it were invisible.
    ///
    /// Three decisions, and they are the three a real launch has: what it costs a month, whether
    /// there is a free tier, and how much of one. Everything else about the model was decided months
    /// ago and is shown rather than offered.
    ///
    /// **The price belongs to the company, not to the model**, which is a fact this card has to say
    /// out loud rather than imply: `MonetizationPolicy` carries one subscription price and every
    /// model on sale is sold at it. Setting it here moves it for the whole catalogue, and a player
    /// who learns that by watching their existing product's revenue change has learned it the
    /// expensive way.
    /// </summary>
    public sealed class ReleaseConfirmDialog
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action<int> released;
        private readonly Action closed;

        private VisualElement mounted;
        private int shelfIndex = -1;

        private float price;
        private float freeTokens;
        private bool freeTierOn;

        public ReleaseConfirmDialog(Func<CompanySimulation> company, Action<int> released,
            Action closed)
        {
            this.company = company;
            this.released = released;
            this.closed = closed;
        }

        public bool IsOpen => mounted?.panel != null;

        /// <summary>What the card would ship at. Read by the tests, and by nothing else.</summary>
        public double PriceUsdPerMonth => price;

        /// <summary>Tokens a day given away, or zero when the free tier is switched off.</summary>
        public double FreeTokensPerDay => freeTierOn ? freeTokens : 0.0;

        public void Close()
        {
            mounted?.RemoveFromHierarchy();
            mounted = null;
            shelfIndex = -1;
            closed?.Invoke();
        }

        /// <summary>
        /// Opens on one shelved model, with the terms the company currently offers.
        ///
        /// **Seeded from the policy rather than from a default**, because a company that has already
        /// chosen a price is not asking the question again: it is confirming. Anybody who wants to
        /// change it has the two controls right there.
        /// </summary>
        public void Show(VisualElement host, int index)
        {
            var simulation = company();
            if (host == null || simulation == null
                || index < 0 || index >= simulation.State.Shelf.Count)
            {
                return;
            }

            var policy = simulation.State.Monetization;

            shelfIndex = index;
            price = (float)Math.Clamp(policy.SubscriptionPriceUsdPerMonth,
                ReleasePlanPanel.MinimumPriceUsd, ReleasePlanPanel.MaximumPriceUsd);

            freeTierOn = policy.FreeTierTokensPerUserPerDay > 0.0;
            freeTokens = (float)Math.Clamp(
                freeTierOn ? policy.FreeTierTokensPerUserPerDay : ReleasePlanPanel.MaximumFreeTokens * 0.4,
                ReleasePlanPanel.MinimumFreeTokens, ReleasePlanPanel.MaximumFreeTokens);

            mounted?.RemoveFromHierarchy();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => Close());

            mounted = veil;
            host.Add(veil);

            Rebuild();
            AudioDirector.Page();
        }

        /// <summary>
        /// Ships it, on the terms on the card.
        ///
        /// The policy is written **before** the release rather than after, because `TryReleaseModel`
        /// seeds the model's version line from it: setting the price afterwards would put the model
        /// on sale at the old figure and then quietly change what everybody pays a frame later.
        ///
        /// Public because the button is its only caller and an EditMode element has no panel, so a
        /// click sent to a button is never dispatched.
        /// </summary>
        public bool Release(out string failureReason)
        {
            failureReason = string.Empty;

            var simulation = company();
            if (simulation == null || shelfIndex < 0 || shelfIndex >= simulation.State.Shelf.Count)
            {
                failureReason = Loc.T("model.nothing_on_shelf");
                return false;
            }

            simulation.State.Monetization.SubscriptionPriceUsdPerMonth = price;
            simulation.State.Monetization.FreeTierTokensPerUserPerDay = FreeTokensPerDay;

            var index = shelfIndex;

            if (!simulation.TryReleaseModel(index, simulation.State.DefaultPriceMultiplier,
                    out failureReason))
            {
                AudioDirector.Deny();
                return false;
            }

            AudioDirector.Confirm();
            Close();
            released?.Invoke(index);
            return true;
        }

        private void Rebuild()
        {
            if (mounted == null)
            {
                return;
            }

            var simulation = company();
            var state = simulation.State;
            var shelved = state.Shelf[shelfIndex];

            mounted.Clear();

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("relship");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var head = new VisualElement();
            head.AddToClassList("relship__head");

            // The same silicon plate the upgrade screen draws, which is the one picture in this game
            // that says "this is a product" rather than "this is a row in a list".
            var chip = ChipPreview.Build(state.CompanyName, shelved.Name);
            chip.AddToClassList("die--release");
            head.Add(chip);

            var titles = new VisualElement();
            titles.AddToClassList("relship__titles");

            var kicker = new Label(Loc.T("release.confirm.kicker"));
            kicker.AddToClassList("relship__kicker");
            titles.Add(kicker);

            var name = new Label(shelved.Name);
            name.AddToClassList("relship__name");
            titles.Add(name);

            var ships = new Label(Loc.T("money.ships_at",
                UiFormat.Number(shelved.CapabilityIfReleasedOn(state.Date)),
                UiFormat.Number(shelved.Capability)));

            ships.AddToClassList("relship__ships");
            titles.Add(ships);

            head.Add(titles);
            card.Add(head);

            card.Add(BuildPrice());
            card.Add(BuildFreeTier());

            // Only when there is something already selling at this price. A warning on a company's
            // first model would be a caution about a consequence that cannot happen.
            if (simulation.MarketedModels().Count > 0)
            {
                var shared = new Label(Loc.T("release.confirm.shared_price"));
                shared.AddToClassList("relship__shared");
                card.Add(shared);
            }

            var problem = new Label(string.Empty);
            problem.AddToClassList("relship__problem");
            problem.style.display = DisplayStyle.None;
            card.Add(problem);

            var buttons = new VisualElement();
            buttons.AddToClassList("notice__buttons");

            var back = new Button(Close) { text = Loc.T("common.back") };
            back.AddToClassList("notice__button");
            buttons.Add(back);

            var go = new Button(() =>
            {
                if (Release(out var why))
                {
                    return;
                }

                problem.text = why;
                problem.style.display = DisplayStyle.Flex;
            })
            {
                text = Loc.T("release.confirm.go")
            };

            go.AddToClassList("notice__button");
            go.AddToClassList("notice__button--go");
            buttons.Add(go);

            card.Add(buttons);
            mounted.Add(card);
        }

        private VisualElement BuildPrice()
        {
            var block = new VisualElement();
            block.AddToClassList("relship__row");

            var label = new Label(Loc.T("release.confirm.price"));
            label.AddToClassList("relship__label");
            block.Add(label);

            var reading = new Label(UiFormat.Money((long)Math.Round(price))
                + " " + Loc.T("release.confirm.a_month"));

            reading.AddToClassList("relship__reading");
            block.Add(reading);

            var slider = new Slider(ReleasePlanPanel.MinimumPriceUsd,
                ReleasePlanPanel.MaximumPriceUsd)
            {
                value = price
            };

            slider.AddToClassList("relship__slider");

            // **The reading is updated in place rather than through a rebuild.** Rebuilding the card
            // mid-drag destroys the handle under the cursor, which is exactly the bug that stopped
            // the first tutorial playtest.
            slider.RegisterValueChangedCallback(evt =>
            {
                SetPrice(evt.newValue);
                reading.text = UiFormat.Money((long)Math.Round(price))
                    + " " + Loc.T("release.confirm.a_month");
            });

            block.Add(slider);
            return block;
        }

        /// <summary>
        /// Turns the free tier on or off and redraws.
        ///
        /// **Off is not the slider at its minimum.** Those read the same on screen and are
        /// different facts in the market: one is a company that gives nothing away, the other is a
        /// company that has a free tier set to almost nothing, and `MonetizationPolicy.Generosity`
        /// treats them differently. Its own method because the button is the only caller and a
        /// `Button.clicked` event cannot be raised from a test.
        /// </summary>
        /// <summary>
        /// What the model will be sold at. The price slider is the only caller.
        ///
        /// Clamped here rather than trusted from the control, so the one place that decides what a
        /// legal price is stays one place whatever ends up driving it.
        /// </summary>
        public void SetPrice(double usdPerMonth)
        {
            price = (float)Math.Clamp(usdPerMonth,
                ReleasePlanPanel.MinimumPriceUsd, ReleasePlanPanel.MaximumPriceUsd);
        }

        /// <summary>How much of a free tier, when there is one. The second slider is the only caller.</summary>
        public void SetFreeTokens(double perDay)
        {
            freeTokens = (float)Math.Clamp(perDay,
                ReleasePlanPanel.MinimumFreeTokens, ReleasePlanPanel.MaximumFreeTokens);
        }

        public void SetFreeTier(bool on)
        {
            freeTierOn = on;
            Rebuild();
        }

        private VisualElement BuildFreeTier()
        {
            var block = new VisualElement();
            block.AddToClassList("relship__row");

            var row = new VisualElement();
            row.AddToClassList("relship__switchrow");

            var label = new Label(Loc.T("release.confirm.free"));
            label.AddToClassList("relship__label");
            row.Add(label);

            // On or off first, amount second. "How generous" is a question that only exists once the
            // answer to "at all?" is yes, and a slider sitting at zero says neither.
            var toggle = new Button(() => SetFreeTier(!freeTierOn))
            {
                text = Loc.T(freeTierOn ? "common.on" : "common.off")
            };

            toggle.AddToClassList("relship__toggle");
            toggle.EnableInClassList("relship__toggle--on", freeTierOn);
            row.Add(toggle);

            block.Add(row);

            if (!freeTierOn)
            {
                var none = new Label(Loc.T("release.confirm.free_none"));
                none.AddToClassList("relship__hint");
                block.Add(none);
                return block;
            }

            var reading = new Label(Loc.T("release.confirm.free_reading",
                UiFormat.Count(freeTokens)));

            reading.AddToClassList("relship__reading");
            block.Add(reading);

            var slider = new Slider(ReleasePlanPanel.MinimumFreeTokens,
                ReleasePlanPanel.MaximumFreeTokens)
            {
                value = freeTokens
            };

            slider.AddToClassList("relship__slider");
            slider.RegisterValueChangedCallback(evt =>
            {
                SetFreeTokens(evt.newValue);
                reading.text = Loc.T("release.confirm.free_reading", UiFormat.Count(freeTokens));
            });

            block.Add(slider);
            return block;
        }
    }
}
