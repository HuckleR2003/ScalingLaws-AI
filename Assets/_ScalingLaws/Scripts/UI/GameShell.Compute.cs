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
    /// Compute: rented capacity, the three hosting packages, and what the fleet bills.
    ///
    /// Part of <see cref="GameShell"/>, split out on 2026-08-29. `partial` is a file boundary and
    /// nothing else: the compiler builds the same type either way, so no field changed lifetime and
    /// no call site moved. The shell had reached 5,800 lines because every screen it ever grew was
    /// written into it rather than beside it, and that is the only thing being corrected here.
    /// </summary>
    public sealed partial class GameShell
    {
        /// <summary>
        /// The fleet. Rented capacity on top, owned batches below with what each one is worth now
        /// against what it cost, which is the number the whole hardware design exists to show.
        /// </summary>
        /// <summary>
        /// What the fleet costs, and what the cost is made of.
        ///
        /// The screen used to say one number a day to run. Four separate bills go into that number
        /// and the player pays them for different reasons: rent stops the day you release capacity,
        /// electricity scales with what you own and run, housing is floor space and cooling, upkeep
        /// is the hardware wearing out while it works. A single figure hides which lever moves it.
        /// </summary>
        /// <summary>
        /// The two ways to have compute, as one slanted strip across the top of the screen.
        ///
        /// Renting is the whole game for now and owning a datacenter is years away for any company,
        /// so the second half is deliberately shut rather than hidden: a player should be able to see
        /// that owning exists and what it will take, because that is a goal rather than a secret.
        /// </summary>
        private VisualElement BuildHostingSwitch()
        {
            var strip = new VisualElement();
            strip.AddToClassList("hswitch");

            var artLeft = new VisualElement();
            artLeft.AddToClassList("hswitch__art");
            artLeft.AddToClassList("hswitch__art--left");

            var rentArt = Resources.Load<Texture2D>("Hosting/hosting_renting");
            if (rentArt != null)
            {
                artLeft.style.backgroundImage = new StyleBackground(rentArt);
            }

            strip.Add(artLeft);

            // It did nothing at all, which reads as broken rather than as the only option. Renting
            // is where the fleet controls already are, so the half that is live says so and scrolls
            // to them instead of pretending to be a mode switch that has nothing to switch to.
            var renting = new Button(() => Show(Screen.Fleet)) { text = Loc.T("fleet.renting") };
            renting.tooltip = "The fleet you rent. Everything below this bar is it.";
            renting.AddToClassList("hswitch__half");
            renting.AddToClassList("hswitch__half--on");
            strip.Add(renting);

            var owning = new Button(() => { }) { text = Loc.T("fleet.own_datacenter") };
            owning.tooltip = "Locked until the Datacenter programme research lands. Renting is the "
                + "only way to buy compute until then.";
            owning.AddToClassList("hswitch__half");
            owning.AddToClassList("hswitch__half--locked");
            owning.SetEnabled(false);
            owning.tooltip =
                "Not yet. Owning silicon needs two released models, eighty million in cash, two "
                + "hundred million of lifetime revenue and the datacenter programme researched. "
                + "Renting is the right answer until the cluster is busy enough to justify capital.";

            strip.Add(owning);

            var artRight = new VisualElement();
            artRight.AddToClassList("hswitch__art");
            artRight.AddToClassList("hswitch__art--right");

            // Deliberately dimmed by the stylesheet as well: the half it belongs to is locked, and
            // a bright picture on a disabled control reads as a control that should work.
            var ownArt = Resources.Load<Texture2D>("Hosting/hosting_datacenter");
            if (ownArt != null)
            {
                artRight.style.backgroundImage = new StyleBackground(ownArt);
            }
            strip.Add(artRight);

            return strip;
        }

        /// <summary>
        /// What the service is like right now: the load dial, the severity scale, the response time.
        ///
        /// This is the readout for the mechanic that decides whether users stay. It sits directly
        /// under the rent controls because those two things are one decision.
        /// </summary>
        /// <summary>
        /// How the cluster is divided between building things and serving customers.
        ///
        /// **The ninth mechanism in this project that was complete in the simulation and had no
        /// control.** `CompanyState.TrainingComputeShare` decides how much of the fleet a training
        /// run, an upgrade programme, an architecture programme or a research node gets, and how
        /// much is left for the people paying. It is persisted, it is read in four places, one test
        /// fixture sets it to 0.55, and nothing else in the game has ever touched it: every
        /// campaign ever played ran at the constructor's 0.7 forever.
        ///
        /// It was found by fixing something else. Serving used to take the whole fleet whenever no
        /// training run was in flight, while research went on taking its share regardless, so the
        /// cluster did a hundred and seventy per cent of its own work. Making the two halves add to
        /// one turns this number into a real cost, and a real cost the player cannot touch is not a
        /// decision, it is a tax.
        ///
        /// The reading below the slider says what is actually happening today, not what the split
        /// would be: with nothing being built there is nothing to divide, and a panel insisting on
        /// thirty per cent while the dial above it reads a hundred is the contradiction this whole
        /// pass was about.
        /// </summary>
        private VisualElement BuildClusterSplitPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.cluster_split"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.ClusterSplit);
            panel.Add(heading);

            var share = state.TrainingComputeShare;

            var reading = new Label(Loc.T("fleet.split_reading",
                UiFormat.Percent(share, 0), UiFormat.Percent(1.0 - share, 0)));

            reading.AddToClassList("field__label");
            panel.Add(reading);

            var slider = new Slider(10f, 90f) { value = (float)(share * 100.0) };
            slider.AddToClassList("field");
            slider.RegisterValueChangedCallback(evt =>
            {
                state.TrainingComputeShare = evt.newValue / 100.0;
                Show(Screen.Fleet);
            });

            panel.Add(slider);

            // What the split is doing right now, which is not always what it says. Read from the
            // simulation rather than worked out here, so the two cannot disagree.
            panel.Add(Row(Loc.T("fleet.split_today"),
                simulation.ClusterIsBuildingSomething()
                    ? Loc.T("fleet.split_claimed", UiFormat.Percent(1.0 - share, 0))
                    : Loc.T("fleet.split_idle")));

            panel.Add(Hint(Loc.T("fleet.split_hint")));
            return panel;
        }

        private VisualElement BuildServicePanel()
        {
            var quality = simulation.State.LastQuality;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.service"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.ReservedCapacity);
            panel.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("service__row");

            var dialBlock = new VisualElement();
            dialBlock.AddToClassList("service__dial");

            var gauge = new ServiceGauge();
            gauge.Set(quality);
            dialBlock.Add(gauge);

            var percent = new Label(UiFormat.Percent(quality.Utilisation, 0));
            percent.AddToClassList("service__percent");
            dialBlock.Add(percent);

            var caption = new Label(Loc.T("fleet.server_usage"));
            caption.AddToClassList("service__caption");
            dialBlock.Add(caption);

            row.Add(dialBlock);

            var scale = new ServiceScale();
            scale.Set(quality.Status);
            row.Add(scale);

            var words = new VisualElement();
            words.AddToClassList("service__words");

            var response = new Label($"Response Time: {quality.ResponseMilliseconds:N0}ms");
            response.AddToClassList("service__response");
            response.style.color = ServiceGauge.ColourFor(quality.Status);
            words.Add(response);

            var headline = new Label(quality.Headline);
            headline.AddToClassList("service__headline");
            words.Add(headline);

            var effect = new Label(quality.Reliability >= 1.0
                ? "No penalty. The market sees the product at its full strength."
                : $"Costing you {UiFormat.Percent(1.0 - quality.Reliability)} of how attractive the "
                    + "product looks to everyone deciding today.");

            effect.AddToClassList("service__effect");
            effect.EnableInClassList("service__effect--bad", quality.Reliability < 1.0);
            words.Add(effect);

            row.Add(words);

            // **The three figures a player checks while looking at the dial**, on the same line as
            // it. They were in a wide card underneath with the label at one edge and the number at
            // the other, which is a lot of empty space between a thing and its own value.
            row.Add(BuildServiceFigures());

            // **The charts come up onto this line too**, at the right end and narrower. They used to
            // take the full width underneath on the grounds that a shape squeezed into a quarter of
            // a row is a smudge, which is true of a quarter and not of a third: at this width both
            // curves still read, and the panel is a third shorter for it.
            var charts = BuildUserCharts();
            charts.AddToClassList("service__charts");
            row.Add(charts);

            panel.Add(row);
            return panel;
        }

        /// <summary>
        /// The stat card from the reference: who is on right now, and the four numbers that put that
        /// in context.
        ///
        /// Online is not a stored number. Registered is a stock the simulation records once a day;
        /// how many of those are typing at this minute is a rhythm over that stock, so it is derived
        /// here from the clock. Confusing the two is how a dashboard ends up claiming ten million
        /// people are using something simultaneously.
        /// </summary>
        /// <summary>
        /// Today's money and today's audience, in three lines, beside the dial.
        ///
        /// **This replaces a card that was twice as wide and said the same thing.** The label sat at
        /// one edge and the figure at the other, so the eye had to cross the panel to join a word to
        /// its own number. Three stacked pairs read in one glance.
        /// </summary>
        private VisualElement BuildServiceFigures()
        {
            var breakdown = simulation.MarketByType();
            var registered = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);
            var online = Concurrency.OnlineAt(registered, clock.DayProgress * 24.0);

            var month = Ledger.MonthKeyOf(simulation.State.Date);
            var earned = simulation.State.Ledger.MonthTotal(month, LedgerLine.Subscriptions);

            var strip = new VisualElement();
            strip.AddToClassList("sfig");

            strip.Add(Figure(Loc.T("fleet.today_income"), UiFormat.Money(earned), true));
            strip.Add(Figure(Loc.T("fleet.registered_users"), UiFormat.Count(registered), false));
            strip.Add(Figure(Loc.T("fleet.online_users"), UiFormat.Count(online), false));

            return strip;

            static VisualElement Figure(string caption, string value, bool lead)
            {
                var block = new VisualElement();
                block.AddToClassList("sfig__block");

                var label = new Label(caption);
                label.AddToClassList("sfig__caption");
                block.Add(label);

                var figure = new Label(value);
                figure.AddToClassList("sfig__value");
                figure.EnableInClassList("sfig__value--lead", lead);
                block.Add(figure);

                return block;
            }
        }

        /// <summary>
        /// The two charts side by side: the day by day account base, and today's traffic curve.
        ///
        /// Registered is filled because it is a stock and the area reads as accumulation. Online is a
        /// bare line because it is a rate, and filling it would suggest a total that does not exist.
        /// </summary>
        private VisualElement BuildUserCharts()
        {
            var block = new VisualElement();
            block.AddToClassList("charts");

            var breakdown = simulation.MarketByType();
            var registered = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);

            var history = simulation.State.Users.Recent(15);

            var left = new VisualElement();
            left.AddToClassList("chart-block");

            var leftTitle = new Label(Loc.T("fleet.registered_users"));
            leftTitle.AddToClassList("chart-block__title");
            left.Add(leftTitle);

            var registeredChart = new LineChart();
            registeredChart.Set(history, new Color(0.29f, 0.68f, 0.90f), true);
            left.Add(registeredChart);

            var leftFoot = new Label(history.Count < 2
                ? "Filling in as the days pass."
                : $"Last {history.Count} days");

            leftFoot.AddToClassList("chart-block__foot");
            left.Add(leftFoot);

            block.Add(left);

            var right = new VisualElement();
            right.AddToClassList("chart-block");

            var rightTitle = new Label(Loc.T("fleet.online_users"));
            rightTitle.AddToClassList("chart-block__title");
            right.Add(rightTitle);

            // Every second hour of today, which is the shape the reference shows. It is a curve over
            // a number the simulation owns rather than a second source of truth.
            var curve = new List<double>(13);
            for (var hour = 0.0; hour <= 24.0; hour += 2.0)
            {
                curve.Add(Concurrency.OnlineAt(registered, hour));
            }

            var onlineChart = new LineChart();
            onlineChart.Set(curve, new Color(0.92f, 0.45f, 0.32f), false);
            right.Add(onlineChart);

            var rightFoot = new Label(Loc.T("fleet.hours"));
            rightFoot.AddToClassList("chart-block__foot");
            right.Add(rightFoot);

            block.Add(right);
            return block;
        }

        /// <summary>
        /// The three reserved blocks, bought in whole units that stack.
        ///
        /// Not a ladder. Standard is the sensible default, the edge tier buys experience rather than
        /// volume, and bulk buys volume at the cost of experience. A player who takes bulk to chase a
        /// large audience and then cannot keep it has made a real mistake rather than hit a rule
        /// nobody told them about, which is why each card states what it does under load.
        /// </summary>
        private VisualElement BuildPackagePanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.reserved_capacity"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.ReservedCapacity);
            panel.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("pack-row");

            foreach (var definition in HostingCatalog.All)
            {
                row.Add(BuildPackageCard(definition));
            }

            panel.Add(row);
            return panel;
        }

        /// <summary>
        /// Which cover belongs to which package.
        ///
        /// Written out rather than built from the enum name, so a renamed package fails to compile
        /// here instead of quietly losing its picture.
        /// </summary>
        private static string HostingCoverFor(HostingPackage id) => id switch
        {
            HostingPackage.Standard => "hosting_growth",
            HostingPackage.LowLatency => "hosting_edge",
            HostingPackage.Bulk => "hosting_bulk",
            _ => null
        };

        private VisualElement BuildPackageCard(HostingPackageDefinition definition)
        {
            var held = simulation.State.Pool.PackageCount(definition.Id);

            var card = new VisualElement();
            card.AddToClassList("pack");
            card.EnableInClassList("pack--on", held > 0);

            // The author drew a cover for each package. A missing one leaves the plate as it was
            // rather than throwing, same rule every other loader in the project follows.
            var cover = PageArt.Hosting(HostingCoverFor(definition.Id));

            // The picture and the name are one block: a cover with its title on it. Built even when
            // the art is missing, so the name lands in the same place either way and a missing file
            // reads as a dark plate rather than as a differently shaped card.
            var plate = new VisualElement();
            plate.AddToClassList("pack__cover");

            if (cover != null)
            {
                plate.style.backgroundImage = new StyleBackground(cover);
            }

            var scrim = new VisualElement();
            scrim.AddToClassList("pack__scrim");
            plate.Add(scrim);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("pack__name");
            plate.Add(name);

            card.Add(plate);

            var body = new VisualElement();
            body.AddToClassList("pack__body");
            card.Add(body);

            var size = new Label(
                Loc.T("compute.pf_accounts", UiFormat.Petaflops(definition.Petaflops),
                UiFormat.Count(HostingCatalog.CoversAccounts(definition.Petaflops))));

            size.AddToClassList("pack__size");
            body.Add(size);

            var pitch = new Label(definition.Pitch);
            pitch.AddToClassList("pack__pitch");
            body.Add(pitch);

            var price = new Label(Loc.T("compute.a_month_each", UiFormat.Money(definition.MonthlyCostUsd)));
            price.AddToClassList("pack__price");
            body.Add(price);

            var controls = new VisualElement();
            controls.AddToClassList("pack__controls");

            var fewer = new Button(() => SetPackage(definition.Id, held - 1)) { text = "-" };
            fewer.AddToClassList("pack__step");
            fewer.SetEnabled(held > 0);
            controls.Add(fewer);

            var count = new Label(held > 0 ? $"x{held}" : "none");
            count.AddToClassList("pack__count");
            controls.Add(count);

            var more = new Button(() => SetPackage(definition.Id, held + 1)) { text = "+" };
            more.AddToClassList("pack__step");
            more.SetEnabled(held < definition.UnitCap);
            controls.Add(more);

            body.Add(controls);

            if (held > 0)
            {
                var total = new Label(
                    Loc.T("compute.held_for", UiFormat.Petaflops(definition.Petaflops * held),
                    UiFormat.Money(definition.MonthlyCostUsd * held)));

                total.AddToClassList("pack__total");
                body.Add(total);
            }

            return card;
        }

        private void SetPackage(HostingPackage id, int units)
        {
            simulation.State.Pool.SetPackageCount(id, units);
            Show(Screen.Fleet);
        }

        private VisualElement BuildFleetBill(ComputeProfile profile)
        {
            var block = new VisualElement();
            block.AddToClassList("panel");
            block.AddToClassList("fleet-bill");

            var head = new VisualElement();
            head.AddToClassList("fleet-bill__head");

            var heading = new Label(Loc.T("panel.day_costs"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.DailyBurn);
            heading.style.marginBottom = 0;
            head.Add(heading);

            var total = new Label(UiFormat.Money((long)profile.Bill.TotalUsd) + " a day");
            total.AddToClassList("fleet-bill__total");
            head.Add(total);

            block.Add(head);

            var bar = new FleetBillBar();
            bar.Set(profile.Bill);
            block.Add(bar);

            var legend = new VisualElement();
            legend.AddToClassList("fleet-bill__legend");
            legend.Add(BillKey("CLOUD RENT", profile.Bill.CloudRentUsd, FleetBillBar.RentColour));
            legend.Add(BillKey("ELECTRICITY", profile.Bill.ElectricityUsd, FleetBillBar.PowerColour));
            legend.Add(BillKey("HOUSING", profile.Bill.HousingUsd, FleetBillBar.HousingColour));
            legend.Add(BillKey("UPKEEP", profile.Bill.MaintenanceUsd, FleetBillBar.UpkeepColour));
            block.Add(legend);

            // Power is the one that can stop the fleet rather than only cost money.
            var power = new Label(
                Loc.T("compute.power_draw", UiFormat.Number(profile.PowerDrawKilowatts, 0),
                    UiFormat.Number(profile.PowerCapacityKilowatts, 0))
                + (profile.IsOverPowerBudget ? Loc.T("compute.over_budget") : string.Empty));

            power.AddToClassList("fleet-bill__power");
            power.EnableInClassList("fleet-bill__power--over", profile.IsOverPowerBudget);
            block.Add(power);

            return block;
        }

        private static VisualElement BillKey(string name, double amount, Color colour)
        {
            var key = new VisualElement();
            key.AddToClassList("fleet-key");

            var swatch = new VisualElement();
            swatch.AddToClassList("fleet-key__swatch");
            swatch.style.backgroundColor = colour;
            key.Add(swatch);

            var label = new Label($"{name}  {UiFormat.Money((long)amount)}");
            label.AddToClassList("fleet-key__label");
            key.Add(label);

            return key;
        }

    }
}
