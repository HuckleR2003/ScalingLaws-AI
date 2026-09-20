using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The fleet, the business page, releases, funding and debt.
    ///
    /// Part of <see cref="GameShell"/>, split out on 2026-08-29. `partial` is a file boundary and
    /// nothing else: the compiler builds the same type either way, so no field changed lifetime and
    /// no call site moved. The shell had reached 5,800 lines because every screen it ever grew was
    /// written into it rather than beside it, and that is the only thing being corrected here.
    /// </summary>
    public sealed partial class GameShell
    {
        private VisualElement BuildFleetScreen()
        {
            var profile = simulation.Profile;
            var market = simulation.Market;

            var page = NewPage(Loc.T("page.fleet"), string.Empty);

            // **The bill where the photograph was.** The strip on this page was decoration over the
            // one screen that is entirely about money going out, and the figure a player opens this
            // tab to read was four panels down. It carries its own darker ground so it reads as the
            // page's header rather than as the first of six identical cards.
            var bill = BuildFleetBill(profile);
            bill.AddToClassList("panel--header");
            page.Add(bill);

            page.Add(BuildHostingSwitch());
            page.Add(BuildServicePanel());

            var rental = new VisualElement();
            rental.AddToClassList("panel");

            // The two panels at the top of FLEET are the ones a player reads while deciding what to
            // spend, and they were the smallest things on the screen. Forty percent taller.
            rental.AddToClassList("fleet-panel");

            // The strip on the research banner jumps here when a node has run out of
            // cluster, and it has to be able to find this panel to light it.
            rental.AddToClassList("rent-panel");
            var rentalHeading = new Label(Loc.T("panel.rented_capacity"));
            rentalHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(rentalHeading, TechNotes.RentOrOwn);
            rental.Add(rentalHeading);

            // **Three meters instead of one sentence.** The capacity, the daily bill and what the
            // pool actually delivers were run together in a line of prose over a bare slider, so the
            // number the player is deciding was a fragment in the middle of it.
            // The question the slider is really being asked, so the ceiling is sized from it.
            var audience = simulation.MarketByType();
            var heldUsers = audience.TotalUsersOverall * audience.OverallShareOf(0);

            var ceiling = RentReadout.CeilingPetaflops(heldUsers, state.Pool.RentedPetaflops);

            rental.Add(RentReadout.Meters(
                profile, market, state.Pool.RentedPetaflops, ceiling));

            var rentedSlider = new Slider(0f, (float)ceiling)
            {
                value = (float)state.Pool.RentedPetaflops
            };

            rentedSlider.AddToClassList("field");
            rentedSlider.RegisterValueChangedCallback(evt =>
            {
                simulation.SetRentedPetaflops(evt.newValue);
                Show(Screen.Fleet);
            });

            rental.Add(rentedSlider);

            // And the question the slider is really being asked, in the largest type on the panel.
            rental.Add(RentReadout.CapacityBand(
                state.Pool.RentedAndPackagedPetaflops, heldUsers, simulation.UsersPerPetaflop()));

            rental.Add(Hint(Loc.T("money.reserved.note")));

            // **Under the capacity band, because the band is the number it moves.** The split was a
            // full-width panel three sections higher up the page: the control and the figure it
            // decides were a scroll apart, which is the arrangement that makes a slider feel like it
            // does nothing. The band says how many accounts the fleet holds; this says how much of
            // that fleet the customers are actually given.
            var split = BuildClusterSplitPanel();
            split.AddToClassList("panel--inset");
            rental.Add(split);

            // **The three reserved blocks across the page, side by side, which is the shape the
            // decision has.** They were half the page each with the rented panel, and three cards
            // do not fit in half a page: the stylesheet turned the row into a column, so the
            // packages became three full-width slabs stacked down the panel and only the first one
            // was on screen. Choosing between three things you have to scroll past each other is
            // not choosing, and the author asked for the row back by name.
            //
            // 290px a card is the width the covers were cropped for, and three of them plus the
            // margins sit inside the page with room to spare. Rented capacity keeps the whole width
            // underneath, where its slider and the figure it moves are still on one line.
            page.Add(BuildPackagePanel());
            page.Add(rental);

            var ladder = new VisualElement();
            ladder.AddToClassList("panel");
            ladder.AddToClassList("fleet-panel");
            var ladderHeading = new Label(Loc.T("panel.compute_tiers"));
            ladderHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(ladderHeading, TechNotes.PetaflopDay);
            ladder.Add(ladderHeading);

            // **The ceiling is not printed here.** It was, for about an hour, and it was a second
            // copy: the bill panel at the top of this same screen has said "Drawing X of the Y
            // this site supplies" since it was written. Two statements of one fact on one screen
            // is the disagreement with a date on it, and the older one is in the better place.

            foreach (var status in state.ComputeTierLadder())
            {
                var definition = ComputeTierCatalog.Get(status.Tier);
                var row = new VisualElement();
                row.AddToClassList("readout");
                row.Add(new Label(definition.DisplayName));

                // **The third tier had no door.**
                //
                // `TryOrderDatacenter` was written in full, with a gate, a capex, a lead time and
                // its own power tariff, and no screen in the game ever called it. The ladder showed
                // the row, said OPEN once the gate cleared, and offered nothing to press: an entire
                // layer of the infrastructure game was visible, unlocked and unreachable.
                if (status.Tier == ComputeTier.OwnDatacenter && status.IsUnlocked)
                {
                    row.Add(BuildDatacenterControl(definition));
                    ladder.Add(row);
                    continue;
                }

                var value = new Label(status.IsUnlocked ? Loc.T("fleet.tier_open") : status.LockReason);
                value.AddToClassList("readout__value");
                value.style.whiteSpace = WhiteSpace.Normal;
                value.style.maxWidth = 620;
                if (!status.IsUnlocked)
                {
                    value.AddToClassList("readout__value--warn");
                }

                row.Add(value);
                ladder.Add(row);
            }

            page.Add(ladder);

            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("panel-row");

            var owned = new VisualElement();
            owned.AddToClassList("panel");
            var ownedHeading = new Label(Loc.T("panel.owned_hardware"));
            ownedHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(ownedHeading, TechNotes.RentOrOwn);
            owned.Add(ownedHeading);

            if (state.Pool.Assets.Count == 0)
            {
                owned.Add(Hint(Loc.T("money.nothing_owned")));
            }
            else
            {
                for (var index = 0; index < state.Pool.Assets.Count; index++)
                {
                    var slot = index;
                    var asset = state.Pool.Assets[index];
                    var generation = HardwareCatalog.Get(asset.GenerationId);
                    var residual = HardwareValuation.ResidualValueUsd(asset, state.Date);
                    var paid = asset.TotalPurchasePriceUsd;
                    var kept = paid <= 0 ? 0.0 : residual / (double)paid;

                    var row = new VisualElement();
                    row.AddToClassList("readout");
                    row.Add(new Label(Loc.T("money.asset_line",
                            UiFormat.Number(asset.Units, 0), generation.DisplayName, asset.PurchaseDate)
                        + (asset.IsOnline(state.Date)
                            ? string.Empty
                            : Loc.T("money.asset_arrives", asset.DaysUntilOnline(state.Date)))));

                    var right = new VisualElement();
                    right.style.flexDirection = FlexDirection.Row;
                    right.style.alignItems = Align.Center;

                    var worth = new Label(Loc.T("money.residual_of_paid", UiFormat.Money(residual), UiFormat.Money(paid),
                UiFormat.Percent(kept, 0)));
                    worth.AddToClassList("readout__value");
                    worth.AddToClassList(kept < 0.4 ? "readout__value--bad" : "readout__value--good");
                    worth.style.marginRight = 10;
                    right.Add(worth);

                    var sell = new Button(() =>
                    {
                        simulation.TrySellHardware(slot, asset.Units, out _, out _);
                        Show(Screen.Fleet);
                    })
                    { text = Loc.T("common.sell") };
                    sell.AddToClassList("button");
                    sell.style.height = 28;
                    sell.style.minWidth = 90;
                    right.Add(sell);

                    row.Add(right);
                    owned.Add(row);
                }
            }

            bottomRow.Add(owned);

            var buy = new VisualElement();
            buy.AddToClassList("panel");
            var buyHeading = new Label(Loc.T("common.buy"));
            buyHeading.AddToClassList("panel__heading");
            buy.Add(buyHeading);

            var buyGrid = new VisualElement();
            buyGrid.AddToClassList("grid");
            buy.Add(buyGrid);

            var tier = state.IsTierUnlocked(ComputeTier.OwnDatacenter) && state.IsDatacenterOnline
                ? ComputeTier.OwnDatacenter
                : ComputeTier.ColocatedServers;

            foreach (var generation in HardwareCatalog.AvailableOn(state.Date, HardwareClass.Accelerator))
            {
                buyGrid.Add(BuildHardwareCard(generation, tier));
            }

            bottomRow.Add(buy);
            page.Add(bottomRow);
            return page;
        }

        /// <summary>
        /// The right-hand end of the own-datacenter row: what it costs, or how long until it opens.
        ///
        /// Three states and no fourth. Not commissioned yet, so there is a button; commissioned and
        /// being built, so there is a date and a countdown; open, so there is nothing to do here and
        /// the tier is simply available to buy hardware into.
        ///
        /// **The button is disabled with the money on it rather than hidden.** Eighty million
        /// dollars three hundred days before it produces a token is the largest single decision in
        /// the game, and a player who cannot afford it yet should be able to see what they are
        /// saving for. A control that appears only once it is affordable teaches nothing.
        /// </summary>
        private VisualElement BuildDatacenterControl(ComputeTierDefinition definition)
        {
            var side = new VisualElement();
            side.style.flexDirection = FlexDirection.Row;
            side.style.alignItems = Align.Center;

            if (state.IsDatacenterOnline)
            {
                var open = new Label(Loc.T("fleet.tier_open"));
                open.AddToClassList("readout__value");
                open.AddToClassList("readout__value--good");
                side.Add(open);
                return side;
            }

            if (state.DatacenterOrdered)
            {
                var days = Math.Max(0, state.DatacenterReadyDate.DayIndex - state.Date.DayIndex);

                var building = new Label(Loc.T("fleet.dc_building",
                    state.DatacenterReadyDate.ToString(), Loc.Counted(days, "noun.day")));

                building.AddToClassList("readout__value");
                building.AddToClassList("readout__value--warn");
                side.Add(building);
                return side;
            }

            var affordable = state.CashUsd >= definition.FacilityCapexUsd;

            var order = new Button(() =>
            {
                simulation.TryOrderDatacenter(out _);
                Show(Screen.Fleet);
            })
            {
                text = Loc.T("fleet.dc_commission", UiFormat.Money(definition.FacilityCapexUsd))
            };

            order.AddToClassList("chip");
            order.SetEnabled(affordable);

            InsightTip.AttachKeyed(order, "fleet.dc_commission_title", "fleet.dc_commission_note");

            if (!affordable)
            {
                var short_ = new Label(Loc.T("fleet.dc_short",
                    UiFormat.Money(definition.FacilityCapexUsd - state.CashUsd)));

                short_.AddToClassList("readout__value");
                short_.AddToClassList("readout__value--warn");
                short_.style.marginRight = 10;
                side.Add(short_);
            }

            side.Add(order);
            return side;
        }

        private VisualElement BuildHardwareCard(HardwareGeneration generation, ComputeTier tier)
        {
            const int batch = 64;
            // **A refusal here used to vanish.** The reason was thrown away with `out _`, so a card
            // turned down for power looked exactly like a card that had been clicked and ignored.
            var card = new Button(() =>
            {
                if (!simulation.TryBuyHardware(generation.Id, batch, tier, out var why))
                {
                    AudioDirector.Deny();
                    startedNotice?.Show(Loc.T("notice.refused"), why);
                }

                Show(Screen.Fleet);
            });
            card.AddToClassList("card");
            CardArt.Apply(card, CardArt.ForHardware(generation.Class));

            var unlocked = state.IsTierUnlocked(tier);
            if (!unlocked)
            {
                card.AddToClassList("card--locked");
            }

            var title = new Label(generation.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var spec = new Label($"{UiFormat.Number(generation.PetaflopsPerUnit, 2)} PF   "
                + $"{generation.MemoryGigabytes} GB   {UiFormat.Number(generation.PowerKilowatts, 2)} kW");
            spec.AddToClassList("card__line");
            card.Add(spec);

            var price = new Label(Loc.T("money.buy_batch", batch,
                UiFormat.Money(generation.LaunchPriceUsd * batch)));
            price.AddToClassList("card__line");
            card.Add(price);

            var power = simulation.PowerAfterOrder(generation, batch, tier);
            if (unlocked)
            {
                card.Add(UiParts.SitePowerLine(power));
            }

            if (generation.IsProjection)
            {
                var badge = new Label(Loc.T("panel.projected"));
                badge.AddToClassList("card__badge");
                card.Add(badge);
            }

            card.SetEnabled(unlocked);
            card.tooltip = generation.IsProjection
                ? Loc.T("money.card.projection")
                : Loc.T("money.card.shipped", generation.ReleaseDate);
            return card;
        }

        /// <summary>
        /// Pricing, the free tier and marketing. The free tier slider is the most dangerous control
        /// in the game and the screen says so with a number rather than a warning.
        /// </summary>
        private VisualElement BuildBusinessScreen()
        {
            // **The strap quoted a price per token and the page opened on billing per token.** A
            // player never meets a token price anywhere else: the creator asks for a monthly fee,
            // the release card asks for a monthly fee. The company now bills that way by default
            // and this page leads with it.
            var page = NewPage(Loc.T("page.business"),
                Loc.T("page.business.strap",
                    UiFormat.Money((long)state.Monetization.SubscriptionPriceUsdPerMonth)));

            UiParts.ExplainPage(page, TechNotes.Revenue, TechNotes.Margin, TechNotes.TokenPrice);

            // **The pricing column is its own panel now.** It carries a locked slider, the four
            // figures the official page already draws, and a picker saying which product those
            // four are about. The lock is state that survives a day rolling over and has to be
            // drivable from a test, which is more than a local in this method can hold.
            businessPricing.Refresh();

            var priceRow = new VisualElement();
            priceRow.AddToClassList("panel-row");
            priceRow.AddToClassList("price-row");
            priceRow.Add(businessPricing.Root);
            page.Add(priceRow);

            // **Both marketing sections have gone to MARKETING**, which is what the note asked
            // for and is plainly right: a tab named after the subject, and a spend panel on a
            // page about pricing, were two doors into the same decision. What is left here is
            // one subject, money coming in and the standing cost of the people who make it.
            //
            // Benefits stay because they are neither: a monthly cost that scales with headcount
            // and belongs to no campaign. The team page is about who is here; this is about what
            // the company spends on them.
            page.Add(benefits.Build());

            return page;
        }

        /// <summary>
        /// What the company gives away, drawn beside what it charges.
        ///
        /// Its own method because the pricing panel asks for it. The two belong side by side, which
        /// is a decision from August worth keeping: what you charge and what you give away are the
        /// same question asked twice, and reading them a screen apart is what makes a generous free
        /// tier look free. The shell builds it because the shell owns the slider that writes it.
        /// </summary>
        private VisualElement BuildFreeTierPanel()
        {
            var policy = state.Monetization;

            var free = new VisualElement();
            free.AddToClassList("panel");
            var freeHeading = new Label(Loc.T("panel.free_tier"));
            freeHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(freeHeading, TechNotes.FreeTier);
            free.Add(freeHeading);

            var freeLabel = new Label(
                Loc.T("money.free_tokens", UiFormat.Count(policy.FreeTierTokensPerUserPerDay)));
            freeLabel.AddToClassList("field__label");
            free.Add(freeLabel);

            var freeSlider = new Slider(0f, (float)MonetizationCatalog.GenerousFreeTierTokensPerDay)
            {
                value = (float)policy.FreeTierTokensPerUserPerDay
            };
            freeSlider.AddToClassList("field");
            freeSlider.SetEnabled(policy.Model != PricingModel.FreeOnly);
            freeSlider.RegisterValueChangedCallback(evt =>
            {
                policy.FreeTierTokensPerUserPerDay = evt.newValue;
                Show(Screen.Business);
            });
            free.Add(freeSlider);

            free.Add(Row(Loc.T("money.reach"),
                Loc.T("money.reach_value", UiFormat.Number(policy.ReachMultiplier, 2))));

            var givenAway = new VisualElement();
            givenAway.AddToClassList("readout");
            givenAway.Add(new Label(Loc.T("books.served_for_nothing")));
            var givenValue = new Label(UiFormat.Percent(policy.FreeShareOfTokens));
            givenValue.AddToClassList("readout__value");
            givenValue.AddToClassList(policy.FreeShareOfTokens > 0.45 ? "readout__value--bad" : "readout__value--warn");
            givenAway.Add(givenValue);
            free.Add(givenAway);

            // **The slider does not control all of that figure, and the screen never said so.**
            //
            // With the tier at zero the readout still printed eight per cent given away, beside a
            // control reading "0 tokens per free account per day", and there was no way to tell
            // which of the two was wrong. Neither was: `MonetizationCatalog.BaseFreeShare` is what
            // trials and evaluation cost a company that offers no free tier at all, and it is only
            // a floor. Split rather than hidden, because the player is entitled to know which part
            // of the giveaway their slider can actually take back.
            if (policy.Model != PricingModel.FreeOnly)
            {
                free.Add(Row(Loc.T("money.free_floor"),
                    UiFormat.Percent(MonetizationCatalog.BaseFreeShare)));

                free.Add(Row(Loc.T("money.free_from_tier"),
                    UiFormat.Percent(Math.Max(0.0,
                        policy.FreeShareOfTokens - MonetizationCatalog.BaseFreeShare))));
            }

            free.Add(Row(Loc.T("money.given_yesterday"),
                Loc.T("money.tokens_value", UiFormat.Billions(state.FreeTokensServedBillions))));
            free.Add(Row(Loc.T("money.given_total"),
                Loc.T("money.tokens_value", UiFormat.Billions(state.LifetimeFreeTokensBillions))));

            free.Add(Hint(Loc.T("money.free_hint")));
            return free;
        }


        private VisualElement BuildCampaignPanel(CampaignKind kind, string heading, string blurb)
        {
            var policy = state.Monetization;
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var label = new Label(heading);
            label.AddToClassList("panel__heading");
            panel.Add(label);

            var current = kind == CampaignKind.Company
                ? policy.CompanyMarketingDailyUsd
                : policy.ModelMarketingDailyUsd;

            panel.Add(Row(Loc.T("money.spending_now"),
                Loc.T("money.spend_per_day", UiFormat.Money(current))));
            if (kind == CampaignKind.Model)
            {
                panel.Add(Row(Loc.T("money.awareness_held"),
                    Loc.T("money.brand_points", UiFormat.Number(policy.ModelAwareness, 3))));
            }

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            panel.Add(grid);

            var stop = new Button(() =>
            {
                if (kind == CampaignKind.Company)
                {
                    policy.CompanyMarketingDailyUsd = 0;
                }
                else
                {
                    policy.ModelMarketingDailyUsd = 0;
                }

                Show(Screen.Marketing);
            })
            { text = Loc.T("common.stop") };
            stop.AddToClassList("card");
            stop.Add(new Label(Loc.T("common.none")));
            grid.Add(stop);

            foreach (var campaign in MonetizationCatalog.OfKind(kind))
            {
                var captured = campaign;
                var card = new Button(() =>
                {
                    if (kind == CampaignKind.Company)
                    {
                        policy.CompanyMarketingDailyUsd = captured.DailyBudgetUsd;
                    }
                    else
                    {
                        policy.ModelMarketingDailyUsd = captured.DailyBudgetUsd;
                    }

                    Show(Screen.Marketing);
                });
                card.AddToClassList("card");
                card.EnableInClassList("card--ahead", current == campaign.DailyBudgetUsd);

                var title = new Label(campaign.DisplayName.ToUpperInvariant());
                title.AddToClassList("card__title");
                card.Add(title);

                var cost = new Label(Loc.T("release.per_day", UiFormat.Money(campaign.DailyBudgetUsd))
                    + "   " + Loc.T("release.per_month", UiFormat.Money(campaign.MonthlyBudgetUsd)));
                cost.AddToClassList("card__line");
                card.Add(cost);

                card.tooltip = campaign.Description;
                card.SetEnabled(state.Date.IsOnOrAfter(campaign.EarliestDate));
                grid.Add(card);
            }

            panel.Add(Hint(blurb));
            return panel;
        }

        /// <summary>
        /// The last look before anybody can buy it: price, free tier, and a picture of the thing.
        /// </summary>
        private void OpenReleaseConfirm(int slot)
        {
            releaseConfirm ??= new ReleaseConfirmDialog(() => simulation,
                _ => Show(Screen.Site),
                () => Show(Screen.Release))
            {
                announce = (title, note, tone) => startedNotice?.Show(title, note, tone)
            };

            releaseConfirm.Show(shellRoot, slot);
        }

        private ReleaseConfirmDialog releaseConfirm;

        /// <summary>The term sheets on the table, over the screen. Held so it survives a rebuild.</summary>
        private OfferBook offerBookCard;

        private OfferBook offerBook =>
            offerBookCard ??= new OfferBook(() => simulation, () =>
            {
                RefreshChrome();
                Show(Screen.Funding);
            });

        /// <summary>
        /// The pricing column, kept across rebuilds because the lock is state.
        ///
        /// Built lazily from `simulation`, which is not assigned when the shell is constructed.
        /// Holding it rather than rebuilding it is what lets CHANGE survive a day rolling over:
        /// `Show` runs about every second and a half, and a lock that reset itself on the tick
        /// would be a control the player can never finish using.
        /// </summary>
        private BusinessPricingPanel businessPricingPanel;

        private readonly ConfirmCard businessConfirm = new();

        private BusinessPricingPanel businessPricing =>
            businessPricingPanel ??= new BusinessPricingPanel(
                () => simulation,
                () =>
                {
                    RefreshChrome();
                    Show(Screen.Business);
                },
                (title, body, confirmLabel, onYes) =>
                    businessConfirm.Ask(shellRoot, title, body, confirmLabel, onYes),
                BuildFreeTierPanel)
            {
                announce = (title, note, tone) => startedNotice?.Show(title, note, tone)
            };

        private VisualElement BuildReleaseScreen()
        {
            var page = NewPage(Loc.T("page.release"), Loc.T("page.release.strap"));
UiParts.ExplainPage(page, TechNotes.MarketPar, TechNotes.WaitingToRelease);

            if (state.Shelf.Count == 0)
            {
                // **An empty page with one grey sentence on it reads as a screen that failed to
                // load.** It is the state a new player meets first, so it gets a panel, a reason,
                // and the door to the thing that would fill it.
                var empty = new VisualElement();
                empty.AddToClassList("panel");
                empty.AddToClassList("emptystate");

                var emptyHeading = new Label(Loc.T("release.empty.title"));
                emptyHeading.AddToClassList("emptystate__title");
                empty.Add(emptyHeading);

                var emptyBody = new Label(Loc.T("release.empty.body"));
                emptyBody.AddToClassList("emptystate__body");
                empty.Add(emptyBody);

                var go = new Button(DesignANewModel) { text = Loc.T("release.empty.go") };
                go.AddToClassList("button");
                go.AddToClassList("button--primary");
                go.AddToClassList("emptystate__go");
                empty.Add(go);

                page.Add(empty);
                return page;
            }

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            page.Add(grid);

            for (var index = 0; index < state.Shelf.Count; index++)
            {
                var slot = index;
                var shelved = state.Shelf[index];

                // **Clicking this used to ship the model.** Two hundred days of training and most
                // of the company's cash went on sale on one click, at whatever price the company
                // happened to be charging, with no screen in between. The tutorial tells the
                // player at step 44 to click it and set something; there was nothing to set.
                var card = new Button(() => OpenReleaseConfirm(slot));
                card.AddToClassList("card");
                card.AddToClassList("relopt");

                // The silicon plate from the upgrade screen. This card was a title and two grey
                // lines, which is a row in a list rather than the thing the player has spent the
                // last two hundred days building.
                var die = ChipPreview.Build(state.CompanyName, shelved.Name);
                die.AddToClassList("die--tile");
                card.Add(die);

                var title = new Label(shelved.Name.ToUpperInvariant());
                title.AddToClassList("card__title");
                card.Add(title);

                var scoreLine = new Label(
                    Loc.T("money.ships_at", UiFormat.Number(shelved.CapabilityIfReleasedOn(state.Date)),
                UiFormat.Number(shelved.Capability)));
                scoreLine.AddToClassList("card__line");
                card.Add(scoreLine);

                var waitLine = new Label(
                    Loc.T("money.days_on_shelf", shelved.DaysOnShelf(state.Date),
                UiFormat.Number(simulation.Market.FrontierCapability)));
                waitLine.AddToClassList("card__line");
                card.Add(waitLine);

                var go = new Label(Loc.T("release.confirm.open"));
                go.AddToClassList("relopt__go");
                card.Add(go);

                grid.Add(card);
            }

            return page;
        }

        private VisualElement BuildFundingScreen()
        {
            var capTable = state.CapTable;
            var page = NewPage(Loc.T("funding.title"), string.Empty);
            UiParts.ExplainPage(page, TechNotes.Valuation, TechNotes.FounderStake);

            // **The two halves of "where does money come from", side by side.** This page was three
            // full-width panels stacked and the grants were most of a screen below the fold, which
            // is where the author stopped reading it.
            var top = new VisualElement();
            top.AddToClassList("panel-row");
            top.AddToClassList("panel-row--split");

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("bank-half");

            var worthHeading = new Label(Loc.T("bank.worth"));
            worthHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(worthHeading, TechNotes.Valuation);
            panel.Add(worthHeading);

            var assets = simulation.Assets();

            panel.Add(Row(Loc.T("bank.cash"), UiFormat.Money(assets.CashUsd)));
            panel.Add(Row(Loc.T("bank.hardware"), UiFormat.Money(assets.HardwareUsd)));
            panel.Add(Row(Loc.T("bank.property"), UiFormat.Money(assets.PropertyUsd)));
            panel.Add(Row(Loc.T("bank.furniture"), UiFormat.Money(assets.FurnitureUsd)));

            panel.Add(BuildValuationBand(assets));

            panel.Add(Row(Loc.T("bank.run_rate"), UiFormat.Money(state.AnnualRevenueRunRateUsd)));
            panel.Add(Row(Loc.T("bank.founder_stake"),
                UiFormat.Money(capTable.FounderStakeValueUsd(simulation.CurrentValuationUsd()))));

            // **Who owns it, as a bar, with the names under it.** A single founder percentage said
            // nothing about who had the rest or what it was earning them, so an investor was an
            // abstraction the player diluted themselves against rather than somebody on their board.
            panel.Add(OwnershipBar.Build(simulation));

            var browse = new Button(() => offerBook.Show(shellRoot))
            {
                text = Loc.T("own.browse_offers") + "  (" + state.Investors.Count + ")"
            };

            browse.AddToClassList("button");
            browse.AddToClassList("ownbar__offers");
            browse.SetEnabled(state.Investors.Count > 0);
            panel.Add(browse);

            var offer = state.CurrentFundingOffer;
            if (offer.IsOpen)
            {
                var definition = FundingCatalog.Get(offer.Stage);
                panel.Add(Row(Loc.T("funding.on_the_table", definition.DisplayName),
                    Loc.T("funding.raise_for",
                        UiFormat.Money(offer.RaiseUsd), UiFormat.Percent(offer.EquitySold))));
                panel.Add(Row(Loc.T("funding.expires"),
                    Loc.T("funding.in_days",
                        Loc.Counted(offer.DaysRemaining(state.Date), "noun.day"))));

                var sign = new Button(() =>
                {
                    simulation.TryAcceptFundingOffer(out _);
                    Show(Screen.Funding);
                })
                {
                    text = offer.IsDownRound
                        ? Loc.T("funding.sign_down_round")
                        : Loc.T("funding.sign_term_sheet")
                };
                sign.AddToClassList("button");
                sign.AddToClassList("button--primary");
                sign.style.marginTop = 14;
                panel.Add(sign);
            }
            else
            {
                var availability = simulation.NextRoundAvailability();
                var open = new Button(() =>
                {
                    simulation.TryOpenFundingRound(out _);
                    Show(Screen.Funding);
                })
                { text = Loc.T("funding.open_round") };
                open.AddToClassList("button");
                open.SetEnabled(availability.IsAvailable);
                open.style.marginTop = 14;
                panel.Add(open);

                if (!availability.IsAvailable)
                {
                    panel.Add(Hint(availability.Reason));
                }
            }

            // Grants before borrowing, smallest commitment first. They were under five loan
            // tiles on the first render and fell straight off the bottom of the page, on a tab
            // now named after them.
            var grants = BuildGrantsPanel();
            grants.AddToClassList("bank-half");

            top.Add(panel);
            top.Add(grants);
            page.Add(top);

            page.Add(BuildDebtPanel());
            return page;
        }

        /// <summary>
        /// Borrowing.
        ///
        /// **The whole debt system existed and had no button anywhere.** `LoanBook`, `LoanCatalog`,
        /// `DebtTests` and four kinds of event were all written and reachable only from a test, while
        /// the capital screen offered equity and nothing else.
        ///
        /// It belongs beside equity rather than on a screen of its own, because they are the same
        /// decision seen from two sides: a round costs a share of everything the company ever earns
        /// and never has to be repaid, and a facility costs a fixed sum on a fixed date whether or
        /// not the quarter went well. Putting them on one screen is what makes that a choice.
        /// </summary>
        /// <summary>
        /// The band that ends the valuation panel.
        ///
        /// **Two numbers, because they are two numbers.** The valuation is what an investor would
        /// price the company at; the book value is the sum of the rows above it. Adding the rows up
        /// and calling that the valuation would be the "two readings of one figure" fault this
        /// project has now fixed six times, and the gap between them is the interesting part: a
        /// young lab with one good model is worth far more than its parts.
        ///
        /// The colour is the reading. The ramp runs green as far as the share of the price the
        /// company's own assets cover, so a valuation that is mostly promise stays blue.
        /// </summary>
        private VisualElement BuildValuationBand(AssetSheet assets)
        {
            var valuation = simulation.CurrentValuationUsd();

            var wrap = new VisualElement();
            wrap.AddToClassList("vband__wrap");

            var band = new ValuationBand();
            band.SetBacked(assets.BackedShare(valuation));
            wrap.Add(band);

            var words = new VisualElement();
            words.AddToClassList("vband__words");

            var kicker = new Label(Loc.T("bank.valuation"));
            kicker.AddToClassList("vband__kicker");
            words.Add(kicker);

            var figure = new Label(UiFormat.Money(valuation));
            figure.AddToClassList("vband__figure");
            words.Add(figure);

            var owned = new Label(Loc.T("bank.book_value", UiFormat.Money(assets.BookValueUsd)));
            owned.AddToClassList("vband__note");
            words.Add(owned);

            wrap.Add(words);
            return wrap;
        }

        private VisualElement BuildDebtPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("funding.borrowing"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.Instalment);
            panel.Add(heading);

            var book = state.Loans;

            if (book.OpenCount == 0)
            {
                var nothing = new Label(Loc.T("loan.nothing_drawn"));
                nothing.AddToClassList("field__hint");
                panel.Add(nothing);
            }
            else
            {
                // **The two numbers a borrower plans around, side by side.**
                //
                // The instalment pays the loan down and stops when it is settled. The commission is
                // rent on the facility and runs for as long as it is open. One figure a day for
                // both said neither, which is why nobody could tell what a loan actually cost.
                var summary = new VisualElement();
                summary.AddToClassList("loanbill");

                // **It read $0 beside a $72k commission**, because a facility just drawn is in its
                // grace period and nothing is being charged yet. The instalment that is coming is the
                // one a borrower plans around, so it is shown with the day it starts.
                var instalment = book.MonthlyInstalmentUsd(state.Date);
                GameDate? instalmentFrom = null;

                if (instalment == 0L)
                {
                    var scheduled = book.ScheduledMonthlyInstalmentUsd();

                    if (scheduled > 0L)
                    {
                        instalment = scheduled;
                        instalmentFrom = book.FirstInstalmentAfterGrace(state.Date);
                    }
                }

                var instalmentFigure = LoanFigure(Loc.T("loan.monthly_instalment"),
                    UiFormat.Money(instalment), false);

                if (instalmentFrom.HasValue)
                {
                    var from = new Label(Loc.T("loan.instalment_from", instalmentFrom.Value.ToString()));
                    from.AddToClassList("loanbill__from");
                    instalmentFigure.Add(from);
                }

                summary.Add(instalmentFigure);

                summary.Add(LoanFigure(Loc.T("loan.monthly_commission"),
                    UiFormat.Money(book.MonthlyCommissionUsd()), true));

                summary.Add(LoanFigure(Loc.T("loan.open_facilities"),
                    $"{book.OpenCount} / {LoanCatalog.MaximumConcurrentLoans}", false));

                panel.Add(summary);
            }

            foreach (var open in book.Loans)
            {
                var definition = LoanCatalog.Get(open.Product);

                var row = new VisualElement();
                row.AddToClassList("loan-open");

                var name = new Label(definition.DisplayName.ToUpperInvariant());
                name.AddToClassList("loan-open__name");
                row.Add(name);

                var left = new Label(Loc.T("loan.left_of",
                    UiFormat.Money(open.OutstandingUsd),
                    UiFormat.Money(definition.TotalRepaymentUsd)));

                left.AddToClassList("loan-open__left");
                row.Add(left);

                // How far through it is, drawn. A pair of figures does not answer "am I nearly out
                // of this" and a bar does.
                var track = new VisualElement();
                track.AddToClassList("loan-open__track");

                var fill = new VisualElement();
                fill.AddToClassList("loan-open__fill");
                fill.style.width = Length.Percent(definition.TotalRepaymentUsd <= 0L
                    ? 0f
                    : (float)(100.0 * open.RepaidUsd / definition.TotalRepaymentUsd));

                track.Add(fill);
                row.Add(track);

                panel.Add(row);
            }

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            panel.Add(grid);

            // Commercial first, then both state programmes, smallest of those first. The full
            // sovereign tile is twice the width of the others, so anywhere but the very end it
            // breaks the row it lands in and leaves a hole beside it.
            foreach (var offer in simulation.LoanOffers()
                         .OrderBy(entry => entry.Product == LoanProduct.SovereignCompute
                                        || entry.Product == LoanProduct.SovereignSeed ? 1 : 0)
                         .ThenBy(entry => LoanCatalog.Get(entry.Product).PrincipalUsd))
            {
                grid.Add(BuildLoanCard(offer));
            }

            return panel;
        }

        /// <summary>One figure in the running bill, with the fee picked out.</summary>
        private static VisualElement LoanFigure(string caption, string value, bool isFee)
        {
            var block = new VisualElement();
            block.AddToClassList("loanbill__cell");

            var label = new Label(caption);
            label.AddToClassList("loanbill__caption");
            block.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("loanbill__value");
            reading.EnableInClassList("loanbill__value--fee", isFee);
            block.Add(reading);

            return block;
        }

        /// <summary>
        /// What a loan just did to the company, said once: the money in, the instalment and when it
        /// starts, the commission, and the whole sum owed. Asked for by name, with those figures.
        /// </summary>
        private void AnnounceLoan(LoanProduct product)
        {
            var definition = LoanCatalog.Get(product);

            startedNotice?.Show(Loc.T("notice.loan"),
                Loc.T("notice.loan.note", definition.DisplayName,
                    UiFormat.Money(definition.PrincipalUsd),
                    UiFormat.Money(definition.MonthlyInstalmentUsd),
                    simulation.State.Date.AddDays(definition.GraceDays).ToString(),
                    UiFormat.Money(definition.MonthlyCommissionUsd),
                    UiFormat.Money(definition.TotalRepaymentUsd)));
        }

        private VisualElement BuildLoanCard(LoanAvailability offer)
        {
            var definition = LoanCatalog.Get(offer.Product);

            var card = new Button(() =>
            {
                if (simulation.TryTakeLoan(offer.Product, out _))
                {
                    AnnounceLoan(offer.Product);
                }

                Show(Screen.Funding);
            });

            card.AddToClassList("ltile");
            card.EnableInClassList("ltile--open", offer.IsAvailable);

            // The state programme is not one more product in a row. It is ten billion dollars and a
            // government that will not renegotiate, so it gets its own colour and its own width.
            var sovereign = offer.Product == LoanProduct.SovereignCompute
                            || offer.Product == LoanProduct.SovereignSeed;
            card.EnableInClassList("ltile--state", sovereign);

            var art = Resources.Load<Texture2D>("Cards/" + LoanArt(offer.Product));

            if (art != null)
            {
                card.style.backgroundImage = new StyleBackground(art);
            }

            var kicker = new Label(Loc.T(sovereign ? "loan.state" : "loan.commercial"));
            kicker.AddToClassList("ltile__kicker");
            card.Add(kicker);

            var title = new Label(definition.DisplayName);
            title.AddToClassList("ltile__title");
            card.Add(title);

            var principal = new Label(UiFormat.Money(definition.PrincipalUsd));
            principal.AddToClassList("ltile__principal");
            card.Add(principal);

            var figures = new VisualElement();
            figures.AddToClassList("ltile__figures");

            figures.Add(LoanFigure(Loc.T("loan.monthly_instalment"),
                UiFormat.Money(definition.MonthlyInstalmentUsd), false));

            figures.Add(LoanFigure(Loc.T("loan.monthly_commission"),
                UiFormat.Money(definition.MonthlyCommissionUsd), true));

            figures.Add(LoanFigure(Loc.T("loan.back_in_total"),
                UiFormat.Percent(definition.EffectiveMultiple, 0), false));

            card.Add(figures);

            var terms = new Label(offer.IsAvailable
                ? Loc.T("loan.terms", UiFormat.Days(definition.TermDays),
                    UiFormat.Days(definition.GraceDays))
                : offer.Reason);

            terms.AddToClassList("ltile__terms");
            terms.EnableInClassList("ltile__terms--blocked", !offer.IsAvailable);
            card.Add(terms);

            InsightTip.Attach(card, definition.DisplayName, definition.Description,
                InsightTip.Placement.Above);

            card.SetEnabled(offer.IsAvailable);
            return card;
        }

        /// <summary>Which plate a product carries. Named here so the catalog stays free of art.</summary>
        private static string LoanArt(LoanProduct product) => product switch
        {
            LoanProduct.BridgeFacility => "loan_bridge",
            LoanProduct.EquipmentFinance => "loan_equipment",
            LoanProduct.VentureDebt => "loan_venture",
            LoanProduct.CorporateBond => "loan_bond",
            _ => "loan_sovereign"
        };

    }
}
