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
            var rentalHeading = new Label(Loc.T("panel.rented_capacity"));
            rentalHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(rentalHeading, TechNotes.RentOrOwn);
            rental.Add(rentalHeading);

            // **Three meters instead of one sentence.** The capacity, the daily bill and what the
            // pool actually delivers were run together in a line of prose over a bare slider, so the
            // number the player is deciding was a fragment in the middle of it.
            rental.Add(RentReadout.Meters(profile, market, state.Pool.RentedPetaflops));

            var rentedSlider = new Slider(0f, (float)RentReadout.FullScalePetaflops)
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
            var breakdown = simulation.MarketByType();

            rental.Add(RentReadout.CapacityBand(
                state.Pool.RentedPetaflops,
                breakdown.TotalUsersOverall * breakdown.OverallShareOf(0)));

            rental.Add(Hint(
                "Contracted in petaflops, not boxes, so the bill does not move when the clouds change "
                + "generation. It never ages and it bills every day it is held."));

            // Reserved beside rented, half the page each. They are the same question asked two ways,
            // and reading them one under the other made the comparison a scroll rather than a look.
            var capacityRow = new VisualElement();
            capacityRow.AddToClassList("panel-row");

            var reserved = BuildPackagePanel();
            reserved.AddToClassList("fleet-half");
            rental.AddToClassList("fleet-half");

            capacityRow.Add(reserved);
            capacityRow.Add(rental);
            page.Add(capacityRow);

            var ladder = new VisualElement();
            ladder.AddToClassList("panel");
            ladder.AddToClassList("fleet-panel");
            var ladderHeading = new Label(Loc.T("panel.compute_tiers"));
            ladderHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(ladderHeading, TechNotes.PetaflopDay);
            ladder.Add(ladderHeading);

            foreach (var status in state.ComputeTierLadder())
            {
                var definition = ComputeTierCatalog.Get(status.Tier);
                var row = new VisualElement();
                row.AddToClassList("readout");
                row.Add(new Label(definition.DisplayName));

                var value = new Label(status.IsUnlocked ? "OPEN" : status.LockReason);
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
                owned.Add(Hint("Nothing owned. Everything is rented, which is the right answer until "
                    + "the cluster is busy enough to justify the capital."));
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
                    row.Add(new Label($"{asset.Units:N0}x {generation.DisplayName}, bought {asset.PurchaseDate}"
                        + (asset.IsOnline(state.Date) ? string.Empty : $" (arrives in {asset.DaysUntilOnline(state.Date)}d)")));

                    var right = new VisualElement();
                    right.style.flexDirection = FlexDirection.Row;
                    right.style.alignItems = Align.Center;

                    var worth = new Label($"{UiFormat.Money(residual)} of {UiFormat.Money(paid)}  ({UiFormat.Percent(kept, 0)})");
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

        private VisualElement BuildHardwareCard(HardwareGeneration generation, ComputeTier tier)
        {
            const int batch = 64;
            var card = new Button(() =>
            {
                simulation.TryBuyHardware(generation.Id, batch, tier, out _);
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

            var price = new Label($"BUY {batch}   {UiFormat.Money(generation.LaunchPriceUsd * batch)} at list");
            price.AddToClassList("card__line");
            card.Add(price);

            if (generation.IsProjection)
            {
                var badge = new Label(Loc.T("panel.projected"));
                badge.AddToClassList("card__badge");
                card.Add(badge);
            }

            card.SetEnabled(unlocked);
            card.tooltip = generation.IsProjection
                ? "Roadmap extrapolation, not a shipped product."
                : $"Shipped {generation.ReleaseDate}.";
            return card;
        }

        /// <summary>
        /// Pricing, the free tier and marketing. The free tier slider is the most dangerous control
        /// in the game and the screen says so with a number rather than a warning.
        /// </summary>
        private VisualElement BuildBusinessScreen()
        {
            var policy = state.Monetization;
            var market = simulation.Market;

            var page = NewPage(Loc.T("page.business"),
                Loc.T("page.business.strap",
                    UiFormat.Money((long)(market.PricePerMillionTokensUsd * 1000))));

            UiParts.ExplainPage(page, TechNotes.Revenue, TechNotes.Margin, TechNotes.TokenPrice);

            // The two halves of one decision, side by side. What you charge and what you give
            // away are the same question asked twice, and reading them a screen apart is what makes
            // a generous free tier look free.
            var priceRow = new VisualElement();
            priceRow.AddToClassList("panel-row");
            priceRow.AddToClassList("price-row");

            var pricing = new VisualElement();
            pricing.AddToClassList("panel");
            pricing.AddToClassList("price-row__half");
            var pricingHeading = new Label(Loc.T("panel.pricing"));
            pricingHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(pricingHeading, TechNotes.Pricing);
            pricing.Add(pricingHeading);

            var modelRow = new VisualElement();
            modelRow.style.flexDirection = FlexDirection.Row;
            foreach (PricingModel option in Enum.GetValues(typeof(PricingModel)))
            {
                var captured = option;
                var button = new Button(() =>
                {
                    policy.Model = captured;
                    Show(Screen.Business);
                })
                { text = MonetizationCatalog.PricingName(option).ToUpperInvariant() };
                button.AddToClassList("button");
                button.style.marginRight = 8;
                button.SetEnabled(policy.Model != option);
                modelRow.Add(button);
            }

            pricing.Add(modelRow);

            if (policy.Model == PricingModel.PayPerToken)
            {
                var priceLabel = new Label(
                    $"Your rate: x{UiFormat.Number(policy.PaidPriceMultiplier, 2)} of market "
                    + $"({UiFormat.Money((long)(policy.RatePerMillionTokensUsd(market.PricePerMillionTokensUsd) * 1000))} per billion)");
                priceLabel.AddToClassList("field__label");
                priceLabel.style.marginTop = 12;
                pricing.Add(priceLabel);

                var priceSlider = new Slider(0.1f, 3f) { value = (float)policy.PaidPriceMultiplier };
                priceSlider.AddToClassList("field");
                priceSlider.RegisterValueChangedCallback(evt =>
                {
                    policy.PaidPriceMultiplier = evt.newValue;
                    Show(Screen.Business);
                });
                pricing.Add(priceSlider);
                pricing.Add(Hint("Metered against the market. When token prices fall, so does your revenue "
                    + "per token, whether or not anything else changed."));
            }
            else if (policy.Model == PricingModel.Subscription)
            {
                var subLabel = new Label(
                    $"Monthly fee: {UiFormat.Money((long)policy.SubscriptionPriceUsdPerMonth)} "
                    + $"(works out at {UiFormat.Money((long)(policy.RatePerMillionTokensUsd(market.PricePerMillionTokensUsd) * 1000))} per billion tokens)");
                subLabel.AddToClassList("field__label");
                subLabel.style.marginTop = 12;
                pricing.Add(subLabel);

                var subSlider = new Slider(0f, 200f) { value = (float)policy.SubscriptionPriceUsdPerMonth };
                subSlider.AddToClassList("field");
                subSlider.RegisterValueChangedCallback(evt =>
                {
                    policy.SubscriptionPriceUsdPerMonth = evt.newValue;
                    Show(Screen.Business);
                });
                pricing.Add(subSlider);
                pricing.Add(Hint("A fee you set, decoupled from the market. It protects a good position "
                    + "when prices fall and traps a bad one when they do not."));
            }
            else
            {
                pricing.Add(Hint("Nobody pays. Reach is the highest it can be and revenue is zero. "
                    + "The serving bill is not."));
            }

            priceRow.Add(pricing);

            var free = new VisualElement();
            free.AddToClassList("panel");
            var freeHeading = new Label(Loc.T("panel.free_tier"));
            freeHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(freeHeading, TechNotes.FreeTier);
            free.Add(freeHeading);

            var freeLabel = new Label(
                $"{UiFormat.Count(policy.FreeTierTokensPerUserPerDay)} tokens per free account per day");
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

            free.Add(Row("Reach", $"x{UiFormat.Number(policy.ReachMultiplier, 2)} of your normal share"));

            var givenAway = new VisualElement();
            givenAway.AddToClassList("readout");
            givenAway.Add(new Label(Loc.T("books.served_for_nothing")));
            var givenValue = new Label(UiFormat.Percent(policy.FreeShareOfTokens));
            givenValue.AddToClassList("readout__value");
            givenValue.AddToClassList(policy.FreeShareOfTokens > 0.45 ? "readout__value--bad" : "readout__value--warn");
            givenAway.Add(givenValue);
            free.Add(givenAway);

            free.Add(Row("Given away yesterday",
                $"{UiFormat.Billions(state.FreeTokensServedBillions)} tokens"));
            free.Add(Row("Given away in total",
                $"{UiFormat.Billions(state.LifetimeFreeTokensBillions)} tokens"));

            free.Add(Hint("Serving capacity does not care which kind of token it is producing and "
                + "neither does the bill. A generous tier widens the funnel and can quietly turn most "
                + "of the fleet into a cost centre."));
            free.AddToClassList("price-row__half");
            priceRow.Add(free);
            page.Add(priceRow);

            page.Add(BuildCampaignPanel(CampaignKind.Company, "COMPANY MARKETING",
                "Reputation, slowly, and it survives a model going out of date."));
            page.Add(BuildCampaignPanel(CampaignKind.Model, "MODEL MARKETING",
                "Attention on the current flagship. It stops working the day the invoices stop."));

            // Benefits sit on the business page rather than on the team page, because what they
            // are is a standing monthly cost that scales with headcount. The team page is about
            // who is here; this is about what the company spends.
            page.Add(benefits.Build());

            return page;
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

            panel.Add(Row("Spending now", $"{UiFormat.Money(current)} a day"));
            if (kind == CampaignKind.Model)
            {
                panel.Add(Row("Awareness held", $"+{UiFormat.Number(policy.ModelAwareness, 3)} brand"));
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

                Show(Screen.Business);
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

                    Show(Screen.Business);
                });
                card.AddToClassList("card");
                card.EnableInClassList("card--ahead", current == campaign.DailyBudgetUsd);

                var title = new Label(campaign.DisplayName.ToUpperInvariant());
                title.AddToClassList("card__title");
                card.Add(title);

                var cost = new Label($"{UiFormat.Money(campaign.DailyBudgetUsd)}/day   "
                    + $"{UiFormat.Money(campaign.MonthlyBudgetUsd)}/month");
                cost.AddToClassList("card__line");
                card.Add(cost);

                card.tooltip = campaign.Description;
                card.SetEnabled(state.Date.IsOnOrAfter(campaign.EarliestDate));
                grid.Add(card);
            }

            panel.Add(Hint(blurb));
            return panel;
        }

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

                var go = new Button(() => Show(Screen.Create)) { text = Loc.T("release.empty.go") };
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
                var card = new Button(() =>
                {
                    simulation.TryReleaseModel(slot, state.DefaultPriceMultiplier, out _);
                    Show(Screen.Release);
                });
                card.AddToClassList("card");

                var title = new Label(shelved.Name.ToUpperInvariant());
                title.AddToClassList("card__title");
                card.Add(title);

                var scoreLine = new Label(
                    $"SHIPS AT {UiFormat.Number(shelved.CapabilityIfReleasedOn(state.Date))}  (was {UiFormat.Number(shelved.Capability)})");
                scoreLine.AddToClassList("card__line");
                card.Add(scoreLine);

                var waitLine = new Label(
                    $"{shelved.DaysOnShelf(state.Date)} days on the shelf, frontier {UiFormat.Number(simulation.Market.FrontierCapability)}");
                waitLine.AddToClassList("card__line");
                card.Add(waitLine);

                grid.Add(card);
            }

            return page;
        }

        private VisualElement BuildFundingScreen()
        {
            var capTable = state.CapTable;
            var page = NewPage(Loc.T("funding.title"), string.Empty);
            UiParts.ExplainPage(page, TechNotes.Valuation, TechNotes.FounderStake);

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            page.Add(panel);

            panel.Add(Row("Company valuation", UiFormat.Money(simulation.CurrentValuationUsd())));
            panel.Add(Row("Annual revenue run rate", UiFormat.Money(state.AnnualRevenueRunRateUsd)));
            panel.Add(Row("Founder stake worth",
                UiFormat.Money(capTable.FounderStakeValueUsd(simulation.CurrentValuationUsd()))));

            var offer = state.CurrentFundingOffer;
            if (offer.IsOpen)
            {
                var definition = FundingCatalog.Get(offer.Stage);
                panel.Add(Row($"{definition.DisplayName} on the table",
                    $"{UiFormat.Money(offer.RaiseUsd)} for {UiFormat.Percent(offer.EquitySold)}"));
                panel.Add(Row("Term sheet expires", $"in {offer.DaysRemaining(state.Date)} days"));

                var sign = new Button(() =>
                {
                    simulation.TryAcceptFundingOffer(out _);
                    Show(Screen.Funding);
                })
                { text = offer.IsDownRound ? "SIGN THE DOWN ROUND" : "SIGN THE TERM SHEET" };
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
            page.Add(BuildGrantsPanel());
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

                summary.Add(LoanFigure(Loc.T("loan.monthly_instalment"),
                    UiFormat.Money(book.MonthlyInstalmentUsd(state.Date)), false));

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

        private VisualElement BuildLoanCard(LoanAvailability offer)
        {
            var definition = LoanCatalog.Get(offer.Product);

            var card = new Button(() =>
            {
                simulation.TryTakeLoan(offer.Product, out _);
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
                $"{definition.EffectiveMultiple:P0}", false));

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
