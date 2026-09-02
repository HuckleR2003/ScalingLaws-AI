using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The product's own two faces: the page the world sees, and the desk behind it.
    ///
    /// They are one screen with two tabs rather than two entries in the bar, because they describe
    /// the same product and the interesting thing is the distance between them. A page can advertise
    /// a service at ninety five percent load and read as calm while the desk shows requests queueing
    /// ten deep, and a player who can flip between those two views in one click learns something a
    /// pair of separate screens would never have told them.
    ///
    /// **Nothing here decides anything.** Every figure on this screen already exists somewhere in the
    /// simulation and is read, never recomputed: the flagship comes from `CompanySimulation.Flagship`
    /// so the banner and the page can never disagree about what the company is selling, the reviews
    /// are the standing's own drivers written as sentences, and the status line is yesterday's
    /// measured load. A dashboard that computes its own numbers is a second simulation with a
    /// prettier font.
    /// </summary>
    public sealed class ManagementScreen
    {
        private static readonly Color Good = new(0.30f, 0.68f, 0.38f);
        private static readonly Color Warn = new(0.86f, 0.62f, 0.22f);
        private static readonly Color Bad = new(0.82f, 0.28f, 0.26f);
        private static readonly Color Blue = new(0.36f, 0.62f, 0.88f);
        private static readonly Color Violet = new(0.58f, 0.48f, 0.86f);

        private readonly CompanySimulation simulation;
        private readonly Action openRelease;
        private readonly Action openMarketing;
        private readonly Action openFleet;
        private readonly Action openUpgrade;

        private Tab showing = Tab.Page;

        /// <summary>
        /// The model waiting for a second click on SHUTDOWN, or null.
        ///
        /// Withdrawing a product cannot be undone, and the control sits next to UPGRADE, so a single
        /// click would eventually be a mis-click with no way back. Held on the screen rather than in
        /// the simulation because arming is a property of this interface, not of the company.
        /// </summary>
        private DeployedModel armed;

        private string lastFailure = string.Empty;

        private enum Tab
        {
            Page,
            Desk,
            Archive,

            /// <summary>
            /// Which versions of the product people are running.
            ///
            /// Asked for after a playtest, and it belongs here: the other three tabs are what a
            /// stranger sees, what the desk sees and what the company used to sell, and "what are
            /// my users actually on" is the fourth question about the same product.
            /// </summary>
            Versions
        }

        public ManagementScreen(CompanySimulation simulation, Action openRelease,
            Action openMarketing, Action openFleet, Action openUpgrade)
        {
            this.simulation = simulation;
            this.openRelease = openRelease;
            this.openMarketing = openMarketing;
            this.openFleet = openFleet;
            this.openUpgrade = openUpgrade;

            Root = new VisualElement();
            Root.AddToClassList("content");
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Which half is open. A method rather than the tab's own lambda because an EditMode test
        /// has no panel to dispatch a click into, and a screen whose only route between its two
        /// halves is a closure is a screen no test can walk.
        /// </summary>
        public void ShowDesk(bool desk) => Open(desk ? Tab.Desk : Tab.Page);

        /// <summary>Opens the archive. Named so a test and a button reach it the same way.</summary>
        public void ShowArchive() => Open(Tab.Archive);

        /// <summary>Opens the version list. Tooling and the tab reach it the same way.</summary>
        public void ShowVersions() => Open(Tab.Versions);

        private void Open(Tab tab)
        {
            showing = tab;
            armed = null;
            lastFailure = string.Empty;
            Refresh();
        }

        public void Refresh()
        {
            Root.Clear();

            var product = simulation.Product();

            var title = new Label(showing switch
            {
                Tab.Desk => Loc.T("mg.management"),
                Tab.Archive => Loc.T("mg.archive"),
                Tab.Versions => Loc.T("mg.versions"),
                _ => Loc.T("mg.official_page")
            });

            title.AddToClassList("page-title");
            Root.Add(title);

            var subtitle = new Label(showing switch
            {
                Tab.Desk => "What the numbers say. Held users, what they think, what it costs to keep them.",
                Tab.Versions => Loc.T("mg.versions.strap"),
                Tab.Archive => "Every model the company ever put on sale, newest first. What each one "
                    + "scored, what it earned, and whether anyone is still using it.",
                _ => Loc.T("mg.stranger_sees")
            });

            subtitle.AddToClassList("page-subtitle");

            // The desk is where a price is set, so the two words the price is made of live here.
            UiParts.ExplainPage(Root, TechNotes.Pricing, TechNotes.FreeTier);
            Root.Add(subtitle);

            Root.Add(BuildTabs());

            if (lastFailure.Length > 0)
            {
                var problem = new Label(lastFailure);
                problem.AddToClassList("mcb-problem");
                Root.Add(problem);
            }

            // The archive is the one tab that is worth opening with nothing on sale, because that is
            // exactly when a player wants to see what they used to have.
            if (showing == Tab.Archive)
            {
                BuildArchive();
                return;
            }

            if (!product.Exists)
            {
                Root.Add(BuildNothingToShow());
                return;
            }

            if (showing == Tab.Versions)
            {
                // The flagship rather than the standing, because the version list belongs to a
                // product line and the standing is a reading taken off one.
                var flagship = simulation.Flagship();

                if (flagship != null)
                {
                    Root.Add(UiParts.VersionList(flagship));
                }

                return;
            }

            if (showing == Tab.Desk)
            {
                BuildDesk(product);
            }
            else
            {
                BuildPage(product);
            }
        }

        private VisualElement BuildTabs()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList("mg-tabs");

            tabs.Add(TabButton(Loc.T("mg.official_page"), showing == Tab.Page, () => Open(Tab.Page)));
            tabs.Add(TabButton(Loc.T("mg.management"), showing == Tab.Desk, () => Open(Tab.Desk)));
            tabs.Add(TabButton(Loc.T("mg.versions"), showing == Tab.Versions, () => Open(Tab.Versions)));
            tabs.Add(TabButton(Loc.T("mg.archive"), showing == Tab.Archive, () => Open(Tab.Archive)));

            return tabs;
        }

        private static Button TabButton(string text, bool on, Action click)
        {
            var tab = new Button(click) { text = text };
            tab.AddToClassList("mg-tab");
            tab.EnableInClassList("mg-tab--on", on);
            return tab;
        }

        private VisualElement BuildNothingToShow()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("mg-empty");

            var heading = new Label(Loc.T("mg.nothing_on_sale"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var line = new Label(simulation.State.Shelf.Count > 0
                ? $"{simulation.State.Shelf.Count} finished run"
                    + (simulation.State.Shelf.Count == 1 ? " is" : "s are")
                    + " waiting on the shelf. A page needs something to put on it."
                : "No model has been released. Train one, then release it, and this becomes the page "
                    + "people land on.");

            line.AddToClassList("field__hint");
            panel.Add(line);

            var go = new Button(openRelease) { text = Loc.T("mg.go_to_release") };
            go.AddToClassList("button");
            go.AddToClassList("button--primary");
            go.style.marginLeft = 0;
            go.style.marginTop = 12;
            panel.Add(go);

            return panel;
        }

        // ---- the page the world sees -------------------------------------------------------------

        private void BuildPage(ProductStanding product)
        {
            Root.Add(BuildHero(product));
            Root.Add(BuildStatusStrip());
            Root.Add(BuildPlans());
            Root.Add(BuildReviews(product));
        }

        private VisualElement BuildHero(ProductStanding product)
        {
            var flagship = simulation.Flagship();

            // **What a stranger actually sees, above what the numbers say about it.** This page has
            // always claimed to be the public view and then shown a card of figures; the mock is the
            // page. It is the same element the creator brands, so a company that renamed itself last
            // month looks renamed here too.
            //
            // Returned inside a wrapper rather than added to the root from here, because this method
            // is called into a layout that decides where its result goes.
            var block = new VisualElement();

            var shopfront = new VisualElement();
            shopfront.AddToClassList("mg-front");

            var preview = new BrowserPreview();
            preview.Show(simulation.State.CompanyName, flagship?.Name, simulation.State.FounderName);
            shopfront.Add(preview.Root);

            block.Add(shopfront);

            var hero = new VisualElement();
            hero.AddToClassList("mg-hero");

            var left = new VisualElement();
            left.AddToClassList("mg-hero__left");

            var name = new Label(product.Name.ToUpperInvariant());
            name.AddToClassList("mg-hero__name");
            left.Add(name);

            if (flagship != null)
            {
                var line = new Label(
                    $"{ModelTypeCatalog.Get(flagship.Type).DisplayName} model  ·  {flagship.Family} line  "
                    + $"·  released {UiFormat.Days(product.DaysOld)} ago");

                line.AddToClassList("mg-hero__line");
                left.Add(line);
            }

            var standing = new Label(
                Loc.T("manage.scores_against", UiFormat.Number(product.Capability),
                UiFormat.Number(product.Frontier), product.Freshness));

            standing.AddToClassList("mg-hero__line");
            left.Add(standing);

            hero.Add(left);

            var right = new VisualElement();
            right.AddToClassList("mg-hero__right");

            right.Add(Pips(product.Happiness));

            var score = new Label(UiFormat.Number(product.Happiness * 5.0, 1) + " / 5");
            score.AddToClassList("mg-hero__score");
            right.Add(score);

            var who = new Label(UiFormat.Count(product.Subscribers) + " people use it");
            who.AddToClassList("mg-hero__who");
            right.Add(who);

            hero.Add(right);
            block.Add(hero);
            return block;
        }

        /// <summary>Five pips, filled by how much people prefer this to their next best option.</summary>
        private static VisualElement Pips(double fraction)
        {
            var row = new VisualElement();
            row.AddToClassList("mg-rate");

            var lit = Math.Clamp(fraction, 0.0, 1.0) * 5.0;

            for (var index = 0; index < 5; index++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("mg-pip");

                // Half a pip counts as lit, so four and a half does not read as four.
                pip.EnableInClassList("mg-pip--on", lit >= index + 0.5);
                row.Add(pip);
            }

            return row;
        }

        /// <summary>
        /// The status line every service puts on its own page, and the reason it is worth drawing is
        /// that it can be wrong in the player's favour: it reports yesterday's measured load, which
        /// is exactly what a real one does.
        /// </summary>
        private VisualElement BuildStatusStrip()
        {
            var quality = simulation.State.LastQuality;

            var strip = new VisualElement();
            strip.AddToClassList("mg-status");

            var colour = quality.Status switch
            {
                ServiceStatus.Critical => Bad,
                ServiceStatus.Unstable => Warn,
                _ => Good
            };

            var dot = new VisualElement();
            dot.AddToClassList("mg-status__dot");
            dot.style.backgroundColor = colour;
            strip.Add(dot);

            var text = new Label(quality.Headline);
            text.AddToClassList("mg-status__text");
            text.style.color = colour;
            strip.Add(text);

            var detail = new Label(
                Loc.T("manage.response_line", UiFormat.Number(quality.ResponseMilliseconds, 0),
                UiFormat.Percent(quality.Utilisation, 0)));

            detail.AddToClassList("mg-status__detail");
            strip.Add(detail);

            var go = new Button(openFleet) { text = Loc.T("hud.compute") };
            go.AddToClassList("chip");
            strip.Add(go);

            return strip;
        }

        private VisualElement BuildPlans()
        {
            var money = simulation.State.Monetization;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("mg.plans"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("mg-plans");

            var freeTokens = money.FreeTierTokensPerUserPerDay;
            row.Add(Plan(Loc.T("mg.free"), freeTokens <= 0.0 ? Loc.T("mg.closed") : "$0",
                freeTokens <= 0.0 ? string.Empty : "a month",
                freeTokens <= 0.0
                    ? new[]
                    {
                        "No free tier.",
                        "Everyone who tries it pays first.",
                        "Cheapest to serve, hardest to be discovered."
                    }
                    : new[]
                    {
                        $"{UiFormat.Number(freeTokens / 1000.0, 0)}k tokens a day",
                        "No commitment",
                        "Costs you to serve, buys you goodwill"
                    },
                money.FreeShareOfTokens > 0.5));

            row.Add(Plan("PAID", UiFormat.Money((long)Math.Round(money.SubscriptionPriceUsdPerMonth)),
                "a month",
                new[]
                {
                    "Everything, no daily ceiling",
                    money.PaidPriceMultiplier > 1.05
                        ? $"{UiFormat.Percent(money.PaidPriceMultiplier - 1.0, 0)} above the going rate"
                        : money.PaidPriceMultiplier < 0.95
                            ? $"{UiFormat.Percent(1.0 - money.PaidPriceMultiplier, 0)} under the going rate"
                            : "Priced at the going rate",
                    $"{UiFormat.Count(simulation.Product().Subscribers * PaidShare())} on it"
                },
                money.FreeShareOfTokens <= 0.5));

            panel.Add(row);

            var note = new Label(Loc.T("manage.price_note"));

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        private double PaidShare() =>
            Math.Clamp(1.0 - simulation.State.Monetization.FreeShareOfTokens, 0.0, 1.0);

        private static VisualElement Plan(string name, string price, string per, string[] lines,
            bool favoured)
        {
            var card = new VisualElement();
            card.AddToClassList("mg-plan");
            card.EnableInClassList("mg-plan--on", favoured);

            var title = new Label(name);
            title.AddToClassList("mg-plan__name");
            card.Add(title);

            var amount = new Label(price);
            amount.AddToClassList("mg-plan__price");
            card.Add(amount);

            if (!string.IsNullOrEmpty(per))
            {
                var unit = new Label(per);
                unit.AddToClassList("mg-plan__per");
                card.Add(unit);
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var item = new Label(line);
                item.AddToClassList("mg-plan__line");
                card.Add(item);
            }

            if (favoured)
            {
                var tag = new Label(Loc.T("mg.most_used"));
                tag.AddToClassList("mg-plan__tag");
                card.Add(tag);
            }

            return card;
        }

        /// <summary>
        /// What people are saying, which is the standing's own drivers written as sentences.
        ///
        /// Every line quotes a number the simulation already holds, and none of them is decoration:
        /// if the page says the thing is slow, the fleet is genuinely at ninety percent. This is the
        /// cheapest way to answer "why is my satisfaction falling" without printing a table of
        /// coefficients on the page a customer is supposed to be reading.
        /// </summary>
        private VisualElement BuildReviews(ProductStanding product)
        {
            var quality = simulation.State.LastQuality;
            var money = simulation.State.Monetization;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("mg.what_people_say"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            // Service, price, freshness and capability. Four facts, each one either a complaint or a
            // compliment depending on where it actually sits.
            panel.Add(Review("On the speed",
                quality.Status == ServiceStatus.Critical
                    ? $"Timed out three times this morning. {UiFormat.Number(quality.ResponseMilliseconds, 0)} ms "
                        + "when it answers at all."
                    : quality.Status == ServiceStatus.Unstable
                        ? $"Noticeably slower than it was. {UiFormat.Number(quality.ResponseMilliseconds, 0)} ms "
                            + "and climbing at busy hours."
                        : $"Answers straight away, {UiFormat.Number(quality.ResponseMilliseconds, 0)} ms, "
                            + "never seen it queue.",
                quality.Reliability));

            panel.Add(Review("On the price",
                money.PaidPriceMultiplier > 1.2
                    ? "Good, but they know it. Costs well over what the others charge."
                    : money.PaidPriceMultiplier < 0.85
                        ? "Cheaper than everything comparable. Hard to argue with."
                        : "About what everyone charges. Fine.",
                Math.Clamp(1.3 - money.PaidPriceMultiplier * 0.6, 0.0, 1.0)));

            panel.Add(Review("On how current it is",
                product.Topicality >= 0.8
                    ? "Newest thing available. Nothing else touches it right now."
                    : product.Topicality >= 0.55
                        ? $"Still good. {UiFormat.Days(product.DaysOld)} old and holding up."
                        : $"Feels dated. {UiFormat.Days(product.DaysOld)} old and the frontier is at "
                            + $"{UiFormat.Number(product.Frontier)} now.",
                product.Topicality));

            panel.Add(Review("Overall",
                $"{simulation.State.LastStandingChange.Headline}, and it shows.",
                product.Happiness));

            return panel;
        }

        private static VisualElement Review(string who, string text, double rating)
        {
            var review = new VisualElement();
            review.AddToClassList("mg-review");

            var head = new VisualElement();
            head.AddToClassList("mg-review__head");

            var name = new Label(who);
            name.AddToClassList("mg-review__who");
            head.Add(name);

            head.Add(Pips(rating));
            review.Add(head);

            var body = new Label(text);
            body.AddToClassList("mg-review__text");
            review.Add(body);

            return review;
        }

        // ---- the desk behind it -------------------------------------------------------------------

        private void BuildDesk(ProductStanding product)
        {
            Root.Add(BuildKpis(product));
            Root.Add(BuildFlagshipControl());
            Root.Add(BuildStandingPanel());
            Root.Add(BuildAudienceTable());
            Root.Add(BuildRivalPanel());
        }

        // ---- the archive -----------------------------------------------------------------------

        /// <summary>
        /// Every model the company ever sold, newest first, each with its own management bar.
        ///
        /// The comparison is the point: the first one against the thirtieth, what each scored and
        /// what each earned. Superseded models are drawn as plainly as retired ones, because a player
        /// looking at a list of live models would otherwise expect all of them to be earning and be
        /// wrong about most of them.
        /// </summary>
        private void BuildArchive()
        {
            var history = simulation.ModelHistory();

            if (history.Count == 0)
            {
                var panel = new VisualElement();
                panel.AddToClassList("panel");

                var heading = new Label(Loc.T("mg.nothing_shipped"));
                heading.AddToClassList("panel__heading");
                panel.Add(heading);

                var line = new Label(Loc.T("manage.archive_empty"));

                line.AddToClassList("field__hint");
                panel.Add(line);
                Root.Add(panel);
                return;
            }

            Root.Add(BuildArchiveSummary(history));

            var scroll = new ScrollView();
            scroll.AddToClassList("archive-scroll");

            var ordinal = history.Count;
            foreach (var record in history)
            {
                scroll.Add(BuildArchiveCard(record, ordinal));
                ordinal--;
            }

            Root.Add(scroll);
        }

        /// <summary>The three numbers that make a list of models into a history.</summary>
        private VisualElement BuildArchiveSummary(IReadOnlyList<ModelRecord> history)
        {
            var earned = 0L;
            var best = 0.0;
            var live = 0;

            foreach (var record in history)
            {
                earned += record.Model.LifetimeRevenueUsd;
                best = Math.Max(best, record.CapabilityToday);

                if (record.IsLive)
                {
                    live++;
                }
            }

            var row = new VisualElement();
            row.AddToClassList("mg-kpis");

            row.Add(Kpi("SHIPPED", history.Count.ToString(), live + " still on sale", null));
            row.Add(Kpi("EARNED ALL TIME", UiFormat.Money(earned), "across every model", null));
            row.Add(Kpi("BEST EVER", UiFormat.Number(best),
                $"frontier is {UiFormat.Number(simulation.Market.FrontierCapability)}", null));

            return row;
        }

        private VisualElement BuildArchiveCard(ModelRecord record, int ordinal)
        {
            var model = record.Model;

            var card = new VisualElement();
            card.AddToClassList("arch");
            card.EnableInClassList("arch--live", record.IsMarketed);
            card.EnableInClassList("arch--gone", !record.IsLive);

            var head = new VisualElement();
            head.AddToClassList("arch__head");

            var left = new VisualElement();

            var name = new Label($"#{ordinal}  {model.Name.ToUpperInvariant()}");
            name.AddToClassList("arch__name");
            left.Add(name);

            var line = new Label(
                $"{ModelTypeCatalog.Get(model.Type).DisplayName}  ·  {model.Family} line  ·  "
                + $"released {model.ReleaseDate}"
                + (record.IsLive ? string.Empty : $", withdrawn {model.RetiredOn}"));

            line.AddToClassList("arch__line");
            left.Add(line);

            head.Add(left);

            var state = new Label(record.StateWord);
            state.AddToClassList("arch__state");
            state.AddToClassList(record.IsMarketed
                ? "arch__state--live"
                : record.IsLive ? "arch__state--shadow" : "arch__state--gone");

            head.Add(state);
            card.Add(head);

            var figures = new VisualElement();
            figures.AddToClassList("arch__figures");

            figures.Add(ArchFigure("SCORED", UiFormat.Number(record.CapabilityToday),
                Math.Abs(record.CapabilityToday - model.Capability) > 0.05
                    ? $"shipped at {UiFormat.Number(model.Capability)}"
                    : "unchanged since release"));

            figures.Add(ArchFigure("EARNED", UiFormat.Money(model.LifetimeRevenueUsd),
                model.DaysOnSale > 0
                    ? $"over {UiFormat.Days(model.DaysOnSale)} on sale"
                    : "never sold a token"));

            figures.Add(ArchFigure("PEAK", UiFormat.Count(model.PeakUsers), "most it ever held"));

            card.Add(figures);

            card.Add(ModelControlBar.Build(record,
                record.CanRetire ? () => Shutdown(model) : null,
                openUpgrade,
                ReferenceEquals(armed, model)));

            return card;
        }

        private static VisualElement ArchFigure(string label, string value, string foot)
        {
            var figure = new VisualElement();
            figure.AddToClassList("arch-figure");

            var caption = new Label(label);
            caption.AddToClassList("arch-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("arch-figure__value");
            figure.Add(amount);

            var under = new Label(foot);
            under.AddToClassList("arch-figure__foot");
            figure.Add(under);

            return figure;
        }

        /// <summary>
        /// Two clicks, because withdrawing a product cannot be undone and the control sits beside
        /// UPGRADE. The first arms it and changes the word; the second does it.
        /// </summary>
        public void RequestShutdown(DeployedModel model) => Shutdown(model);

        private void Shutdown(DeployedModel model)
        {
            if (!ReferenceEquals(armed, model))
            {
                armed = model;
                lastFailure = string.Empty;
                Refresh();
                return;
            }

            armed = null;
            lastFailure = simulation.TryRetireModel(model, out var reason) ? string.Empty : reason;
            Refresh();
        }

        private VisualElement BuildKpis(ProductStanding product)
        {
            var state = simulation.State;
            var month = Ledger.MonthKeyOf(state.Date);
            var spent = state.Ledger.MonthCost(month);
            var hour = 12.0;

            var row = new VisualElement();
            row.AddToClassList("mg-kpis");

            row.Add(Kpi("REGISTERED", UiFormat.Count(product.Subscribers),
                UiFormat.Count(Concurrency.OnlineAt(product.Subscribers, hour)) + " on at midday",
                null));

            row.Add(Kpi("PAYING", UiFormat.Count(product.Subscribers * PaidShare()),
                UiFormat.Percent(PaidShare(), 0) + " of them", null));

            row.Add(Kpi("EARNED THIS MONTH", UiFormat.Money(product.MonthEarningsUsd),
                "against " + UiFormat.Money(spent) + " spent", null));

            row.Add(Kpi("NET THIS MONTH", UiFormat.Money(product.MonthNetUsd),
                product.IsProfitable ? "in the black" : "burning cash",
                product.IsProfitable));

            return row;
        }

        private static VisualElement Kpi(string label, string value, string foot, bool? good)
        {
            var tile = new VisualElement();
            tile.AddToClassList("mg-kpi");

            var caption = new Label(label);
            caption.AddToClassList("mg-kpi__label");
            tile.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("mg-kpi__value");

            if (good.HasValue)
            {
                amount.style.color = good.Value ? Good : Bad;
            }

            tile.Add(amount);

            var under = new Label(foot);
            under.AddToClassList("mg-kpi__foot");
            tile.Add(under);

            return tile;
        }

        /// <summary>
        /// The management bar for the thing currently on sale.
        ///
        /// The same control the archive draws, on the model the desk is already describing, so a
        /// player who has just read that the fleet is at ninety five percent can act on it here
        /// rather than going looking for the model in a list.
        /// </summary>
        private VisualElement BuildFlagshipControl()
        {
            var flagship = simulation.Flagship();
            if (flagship == null)
            {
                return new VisualElement();
            }

            foreach (var record in simulation.ModelHistory())
            {
                if (!ReferenceEquals(record.Model, flagship))
                {
                    continue;
                }

                var panel = new VisualElement();
                panel.AddToClassList("panel");

                var heading = new Label(Loc.T("manage.on_sale_now", flagship.Name.ToUpperInvariant()));
                heading.AddToClassList("panel__heading");
                panel.Add(heading);

                panel.Add(ModelControlBar.Build(record, () => Shutdown(flagship), openUpgrade,
                    ReferenceEquals(armed, flagship)));

                var note = new Label(Loc.T("manage.withdraw_note"));

                note.AddToClassList("field__hint");
                panel.Add(note);

                return panel;
            }

            return new VisualElement();
        }

        private VisualElement BuildStandingPanel()
        {
            var state = simulation.State;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("mg.how_seen"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            panel.Add(UiParts.ThinBarRow("Reputation", UiFormat.Percent(state.Reputation, 0),
                state.Reputation, Violet));

            panel.Add(UiParts.ThinBarRow("Known", UiFormat.Percent(state.Awareness.Overall, 0),
                state.Awareness.Overall, Blue));

            var sentiment = simulation.Sentiment();
            panel.Add(UiParts.ThinBarRow("Satisfaction", UiFormat.Percent(sentiment.Satisfaction, 0),
                sentiment.Satisfaction, Good));

            panel.Add(UiParts.StatLine("Fans", UiFormat.Count(state.Fans)));
            panel.Add(UiParts.StatLine("Campaigns running", state.Campaigns.Count.ToString()));

            var note = new Label(Loc.T("manage.biggest_mover", state.LastStandingChange.Headline));

            note.AddToClassList("field__hint");
            panel.Add(note);

            if (state.Campaigns.Count == 0)
            {
                var go = new Button(openMarketing) { text = Loc.T("hud.marketing") };
                go.AddToClassList("chip");
                go.style.marginLeft = 0;
                panel.Add(go);
            }

            return panel;
        }

        /// <summary>
        /// Who is using the product, audience by audience, and who is beating you in each.
        ///
        /// The leader column is the one that earns this table its space. A single overall share hides
        /// the case the whole segmented market exists to expose: holding a fifth of everybody while
        /// being nowhere at all in the audience that is about to become the largest one.
        /// </summary>
        private VisualElement BuildAudienceTable()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("mg.who_is_using"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            panel.Add(TableRow("AUDIENCE", "USERS", "YOUR SHARE", "LEADER", true, 0.0));

            var standings = simulation.SegmentStandings();
            foreach (var standing in standings)
            {
                var audience = AudienceCatalog.Get(standing.Segment).DisplayName;
                var leader = standing.LeaderIndex == 0 ? "you" : standing.LeaderName;

                panel.Add(TableRow(audience,
                    UiFormat.Count(standing.PlayerUsers),
                    UiFormat.Percent(standing.PlayerShare, 1),
                    leader,
                    false,
                    standing.PlayerShare));
            }

            if (standings.Count == 0)
            {
                var none = new Label(Loc.T("mg.nobody_yet"));
                none.AddToClassList("field__hint");
                panel.Add(none);
            }

            return panel;
        }

        private static VisualElement TableRow(string what, string users, string share, string leader,
            bool head, double fraction)
        {
            var row = new VisualElement();
            row.AddToClassList("mg-row");
            row.EnableInClassList("mg-row--head", head);

            var name = new Label(what);
            name.AddToClassList("mg-cell");
            name.AddToClassList("mg-cell--wide");
            row.Add(name);

            var count = new Label(users);
            count.AddToClassList("mg-cell");
            count.AddToClassList("mg-cell--num");
            row.Add(count);

            if (head)
            {
                var caption = new Label(share);
                caption.AddToClassList("mg-cell");
                caption.AddToClassList("mg-cell--num");
                row.Add(caption);
            }
            else
            {
                // A bare share bar, not the labelled row widget: the label is already the first
                // column, and reusing that widget here would spend 42% of the cell on nothing.
                var cell = new VisualElement();
                cell.AddToClassList("mg-cell");
                cell.AddToClassList("mg-cell--share");

                var figure = new Label(share);
                figure.AddToClassList("mg-share__value");
                cell.Add(figure);

                var track = new VisualElement();
                track.AddToClassList("mg-share__track");

                var fill = new VisualElement();
                fill.AddToClassList("mg-share__fill");
                fill.style.width = Length.Percent(
                    (float)(Math.Clamp(double.IsNaN(fraction) ? 0.0 : fraction, 0.0, 1.0) * 100.0));

                track.Add(fill);
                cell.Add(track);
                row.Add(cell);
            }

            var who = new Label(leader);
            who.AddToClassList("mg-cell");
            who.AddToClassList("mg-cell--lead");
            who.EnableInClassList("mg-cell--mine", !head && leader == "you");
            row.Add(who);

            return row;
        }

        private VisualElement BuildRivalPanel()
        {
            var breakdown = simulation.MarketByType();

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("mg.the_field"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            // Everyone with anybody, biggest first, player included so the comparison is direct.
            var order = new List<int>();
            for (var owner = 0; owner < breakdown.OwnerUsersOverall.Count; owner++)
            {
                order.Add(owner);
            }

            order.Sort((left, right) =>
                breakdown.OwnerUsersOverall[right].CompareTo(breakdown.OwnerUsersOverall[left]));

            var shown = 0;
            foreach (var owner in order)
            {
                var users = breakdown.OwnerUsersOverall[owner];
                if (users <= 0.0 || shown >= 6)
                {
                    continue;
                }

                var name = owner < breakdown.OwnerNames.Count
                    ? breakdown.OwnerNames[owner]
                    : "unknown";

                panel.Add(UiParts.ThinBarRow(owner == 0 ? name + "  (you)" : name,
                    UiFormat.Count(users), breakdown.OverallShareOf(owner),
                    owner == 0 ? Violet : Blue));

                shown++;
            }

            var unserved = new Label(
                Loc.T("manage.unserved", UiFormat.Percent(breakdown.UnservedShare, 0)));

            unserved.AddToClassList("field__hint");
            panel.Add(unserved);

            return panel;
        }
    }
}
