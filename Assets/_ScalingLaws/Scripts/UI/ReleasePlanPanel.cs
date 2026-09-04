using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Naming a version, pricing it, and seeing who is still on the old ones.
    ///
    /// **The list on the right is the point of this screen.** Shipping an update does not move
    /// everybody onto it: a version people did not like sits at a low share while the one they
    /// already had keeps most of the audience. That is true of every real product and almost no
    /// tycoon game shows it, so it gets the largest, cleanest block on the page — current release
    /// picked out in gold, everything still in use underneath it with its date and its share.
    ///
    /// The left is the two decisions that come with a release: what it is called, and what it costs
    /// people. Both sliders start locked. **A price control that is live the moment the screen opens
    /// is a price the player will change by accident**, and subscription price is the one number in
    /// this game that punishes a slip immediately.
    /// </summary>
    public sealed class ReleasePlanPanel
    {
        /// <summary>Cheapest and dearest a monthly subscription can be, in dollars.</summary>
        public const float MinimumPriceUsd = 0f;

        public const float MaximumPriceUsd = 120f;

        /// <summary>The free allowance range, in tokens per user per day.</summary>
        public const float MinimumFreeTokens = 0f;

        /// <summary>
        /// The top of the range, and it is **the point where generosity saturates**, not a round
        /// number above it.
        ///
        /// `MonetizationPolicy.Generosity` clamps at `GenerousFreeTierTokensPerDay`, so every token
        /// past that bought no reach and was still served and still billed. This read 400,000 and a
        /// playtest raised an allowance from 40k to 370k and reported nothing happened: above 250k
        /// that was exactly right, and a slider with 37% dead travel that costs money in the dead
        /// part is worse than no slider.
        /// </summary>
        public const float MaximumFreeTokens =
            (float)MonetizationCatalog.GenerousFreeTierTokensPerDay;

        private readonly CompanySimulation simulation;
        private readonly Action<string> commit;
        private readonly Action goBack;

        private readonly VisualElement root;

        private int modelIndex = -1;
        private List<ModelTrait> basket = new();

        private string versionName = string.Empty;
        private float price;
        private float freeTokens;

        /// <summary>Which control the player has unlocked, or none. One at a time, deliberately.</summary>
        private enum Editing
        {
            Nothing,
            Price,
            FreeTokens
        }

        private Editing editing = Editing.Nothing;

        private float priceBeforeEditing;
        private float freeBeforeEditing;

        public ReleasePlanPanel(CompanySimulation simulation, Action<string> commit, Action goBack)
        {
            this.simulation = simulation;
            this.commit = commit;
            this.goBack = goBack;

            root = new VisualElement();
            root.AddToClassList("content");
            root.AddToClassList("rel");
        }

        public VisualElement Root => root;

        /// <summary>What the player typed. Public so a test can drive the commit without a panel.</summary>
        public string VersionName => versionName;

        public float PriceUsdPerMonth => price;

        public float FreeTokensPerDay => freeTokens;

        /// <summary>Which model is being versioned. Negative when nothing is open.</summary>
        public int ModelIndex => modelIndex;

        /// <summary>The upgrades that ship with this version.</summary>
        public IReadOnlyList<ModelTrait> Basket => basket;

        /// <summary>
        /// Opens the planner for a model and a basket of upgrades.
        ///
        /// The price and allowance start at whatever the company is charging now, so a player who
        /// changes nothing ships the same terms they already had.
        /// </summary>
        public void Open(int index, IReadOnlyList<ModelTrait> traits)
        {
            modelIndex = index;
            basket = traits?.ToList() ?? new List<ModelTrait>();

            var policy = simulation.State.Monetization;

            price = (float)Math.Clamp(policy.SubscriptionPriceUsdPerMonth,
                MinimumPriceUsd, MaximumPriceUsd);

            freeTokens = (float)Math.Clamp(policy.FreeTierTokensPerUserPerDay,
                MinimumFreeTokens, MaximumFreeTokens);

            editing = Editing.Nothing;
            versionName = SuggestName();

            Refresh();
        }

        /// <summary>
        /// A name the player will probably keep.
        ///
        /// Reads the last version and steps the trailing number, because that is what everybody
        /// does anyway and a blank field on a screen with two sliders on it is one more thing to
        /// fill in before anything can ship.
        /// </summary>
        private string SuggestName()
        {
            var model = Model();

            if (model == null)
            {
                return "1.1";
            }

            var previous = model.Line.PreviousName;
            var digits = new string(previous.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());

            if (digits.Length > 0 && int.TryParse(digits, out var number))
            {
                return previous[..^digits.Length] + (number + 1);
            }

            return $"{previous} 2";
        }

        private DeployedModel Model() =>
            modelIndex >= 0 && modelIndex < simulation.State.DeployedModels.Count
                ? simulation.State.DeployedModels[modelIndex]
                : null;

        public void Refresh()
        {
            root.Clear();

            var model = Model();

            if (model == null)
            {
                var none = new Label(Loc.T("common.nothing_selected"));
                none.AddToClassList("upg__empty");
                root.Add(none);
                return;
            }

            root.Add(BuildHeader(model));

            var columns = new VisualElement();
            columns.AddToClassList("rel__columns");

            columns.Add(BuildSettings(model));
            columns.Add(BuildRight(model));

            root.Add(columns);
        }

        // ---- the header --------------------------------------------------------------------------

        private VisualElement BuildHeader(DeployedModel model)
        {
            var head = new VisualElement();
            head.AddToClassList("rel__head");

            var name = new Label(model.Name.ToUpperInvariant());
            name.AddToClassList("rel__model");
            head.Add(name);

            // Italic, underneath, and it says what this is following — "Base" when nothing has
            // shipped yet, which is the only honest thing to print on a first release.
            var version = new Label(Loc.T("upgrade.version", model.Line.PreviousName));
            version.AddToClassList("rel__previous");
            head.Add(version);

            return head;
        }

        // ---- the left column ------------------------------------------------------------------------

        private VisualElement BuildSettings(DeployedModel model)
        {
            var column = new VisualElement();
            column.AddToClassList("rel__left");

            var card = new VisualElement();
            card.AddToClassList("relcard");

            var nameLabel = new Label(Loc.T("release.version_name"));
            nameLabel.AddToClassList("relcard__label");
            card.Add(nameLabel);

            var field = new TextField { value = versionName };
            field.AddToClassList("relcard__name");
            field.RegisterValueChangedCallback(change => versionName = change.newValue);
            card.Add(field);

            card.Add(BuildPriceRow());
            card.Add(BuildFreeRow());

            column.Add(card);
            column.Add(BuildWhatShips(model));
            column.Add(BuildButtons());

            return column;
        }

        /// <summary>
        /// The subscription price, behind a lock.
        ///
        /// CHANGE PRICE unlocks the slider and swaps itself for Apply and Back. Back restores what
        /// the price was when the lock was opened, so the player can drag it around, see what the
        /// numbers do, and leave without having changed anything — which is what makes it safe to
        /// experiment with a control that otherwise moves the company's revenue.
        /// </summary>
        private VisualElement BuildPriceRow()
        {
            var row = new VisualElement();
            row.AddToClassList("relrow");

            var open = editing == Editing.Price;
            row.EnableInClassList("relrow--live", open);

            var head = new VisualElement();
            head.AddToClassList("relrow__head");

            // **A noun, not the verb on the button.** Both carried `release.change_price`, so the
            // row was headed CHANGE PRICE and never said which price, and a playtest guessed it was
            // the server rent.
            var caption = new Label(Loc.T("release.price_caption"));
            caption.AddToClassList("relrow__caption");
            head.Add(caption);

            var reading = new Label(
                $"${price.ToString("N2", CultureInfo.InvariantCulture)} / month");
            reading.AddToClassList("relrow__value");
            head.Add(reading);

            row.Add(head);

            var slider = new Slider(MinimumPriceUsd, MaximumPriceUsd) { value = price };
            slider.AddToClassList("relrow__slider");
            slider.SetEnabled(open);
            slider.RegisterValueChangedCallback(change =>
            {
                price = change.newValue;
                Refresh();
            });

            row.Add(slider);

            var buttons = new VisualElement();
            buttons.AddToClassList("relrow__buttons");

            if (!open)
            {
                var unlock = new Button(() =>
                {
                    priceBeforeEditing = price;
                    editing = Editing.Price;
                    Refresh();
                })
                { text = Loc.T("release.change") };

                unlock.AddToClassList("relrow__unlock");
                buttons.Add(unlock);
            }
            else
            {
                var apply = new Button(() =>
                {
                    editing = Editing.Nothing;
                    Refresh();
                })
                { text = Loc.T("release.apply") };

                apply.AddToClassList("relrow__apply");
                buttons.Add(apply);

                var back = new Button(() =>
                {
                    price = priceBeforeEditing;
                    editing = Editing.Nothing;
                    Refresh();
                })
                { text = Loc.T("common.back") };

                back.AddToClassList("relrow__cancel");
                buttons.Add(back);
            }

            row.Add(buttons);

            var note = new Label(Loc.T("release.price_note"));
            note.AddToClassList("relrow__note");
            row.Add(note);

            return row;
        }

        /// <summary>The free allowance, same lock, same restore-on-back.</summary>
        private VisualElement BuildFreeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("relrow");

            var open = editing == Editing.FreeTokens;
            row.EnableInClassList("relrow--live", open);

            var head = new VisualElement();
            head.AddToClassList("relrow__head");

            var caption = new Label(Loc.T("release.free_tokens"));
            caption.AddToClassList("relrow__caption");
            head.Add(caption);

            var reading = new Label(Loc.T("release.per_day", UiFormat.Count(freeTokens)));
            reading.AddToClassList("relrow__value");
            head.Add(reading);

            row.Add(head);

            var slider = new Slider(MinimumFreeTokens, MaximumFreeTokens) { value = freeTokens };
            slider.AddToClassList("relrow__slider");
            slider.SetEnabled(open);
            slider.RegisterValueChangedCallback(change =>
            {
                freeTokens = change.newValue;
                Refresh();
            });

            row.Add(slider);

            var buttons = new VisualElement();
            buttons.AddToClassList("relrow__buttons");

            if (!open)
            {
                var unlock = new Button(() =>
                {
                    freeBeforeEditing = freeTokens;
                    editing = Editing.FreeTokens;
                    Refresh();
                })
                { text = Loc.T("release.change") };

                unlock.AddToClassList("relrow__unlock");
                buttons.Add(unlock);
            }
            else
            {
                var apply = new Button(() =>
                {
                    editing = Editing.Nothing;
                    Refresh();
                })
                { text = Loc.T("release.apply") };

                apply.AddToClassList("relrow__apply");
                buttons.Add(apply);

                var back = new Button(() =>
                {
                    freeTokens = freeBeforeEditing;
                    editing = Editing.Nothing;
                    Refresh();
                })
                { text = Loc.T("common.back") };

                back.AddToClassList("relrow__cancel");
                buttons.Add(back);
            }

            row.Add(buttons);

            var note = new Label(freeTokens >= MaximumFreeTokens
                ? Loc.T("release.free_note_max")
                : Loc.T("release.free_note"));

            note.AddToClassList("relrow__note");
            row.Add(note);

            return row;
        }

        private VisualElement BuildWhatShips(DeployedModel model)
        {
            var panel = new VisualElement();
            panel.AddToClassList("relships");

            var heading = new Label(Loc.T("release.what_ships"));
            heading.AddToClassList("relships__heading");
            panel.Add(heading);

            if (basket.Count == 0)
            {
                var none = new Label(Loc.T("release.nothing_ships"));
                none.AddToClassList("relships__none");
                panel.Add(none);
                return panel;
            }

            var standings = model.Traits.Standings(simulation.State.Date)
                .Where(entry => basket.Contains(entry.Trait))
                .ToList();

            foreach (var standing in standings)
            {
                var line = new Label($"{ModelTraitCatalog.Get(standing.Trait).DisplayName}   "
                    + $"{standing.Level} → {standing.Level + 1}");

                line.AddToClassList("relships__item");
                panel.Add(line);
            }

            // Summed and scaled, matching the commission exactly. The longest-of-them reading here
            // priced a four-card basket at the length of its longest card.
            var days = standings.Sum(entry => simulation.ScaleResearchDuration(entry.UpgradeDays));
            var cash = standings.Sum(entry => entry.UpgradeCostUsd);

            var bill = new Label(Loc.T("release.cash_days", UiFormat.Money(cash), UiFormat.Days(days)));
            bill.AddToClassList("relships__bill");
            panel.Add(bill);

            return panel;
        }

        private VisualElement BuildButtons()
        {
            var row = new VisualElement();
            row.AddToClassList("rel__buttons");

            var back = new Button(() => goBack?.Invoke()) { text = Loc.T("common.back") };
            back.AddToClassList("rel__back");
            row.Add(back);

            var ship = new Button(() => commit?.Invoke(versionName)) { text = Loc.T("release.ship") };
            ship.AddToClassList("rel__ship");
            ship.SetEnabled(!string.IsNullOrWhiteSpace(versionName));
            row.Add(ship);

            return row;
        }

        // ---- the right column -----------------------------------------------------------------------

        private VisualElement BuildRight(DeployedModel model)
        {
            var column = new VisualElement();
            column.AddToClassList("rel__right");

            column.Add(UiParts.VersionList(model));
            column.Add(BuildCharts(model));

            return column;
        }

        /// <summary>
        /// Every version still in use, and how much of the audience is on it.
        ///
        /// Current in gold at the top, the rest underneath in the order they shipped. The share is
        /// drawn as a bar behind the row rather than printed beside it, because the thing worth
        /// seeing at a glance is whether the newest release is actually where people are — and two
        /// columns of percentages do not answer that as fast as two bars of different lengths.
        /// </summary>
        /// <summary>
        /// Two readings, drawn: what the market actually sees, and what the average user pays.
        ///
        /// Both are weighted by adoption rather than taken from the newest version, which is the
        /// whole consequence of letting people stay put — a company's score is the average of what
        /// its users are running, not the best thing it has ever shipped.
        /// </summary>
        private VisualElement BuildCharts(DeployedModel model)
        {
            var panel = new VisualElement();
            panel.AddToClassList("vcharts");

            var heading = new Label(Loc.T("release.market_sees"));
            heading.AddToClassList("vcharts__heading");
            panel.Add(heading);

            var versions = model.Line.Versions;
            var best = versions.Count == 0 ? 0.0 : versions.Max(version => version.Capability);

            panel.Add(Gauge(Loc.T("release.effective_capability"),
                UiFormat.Number(model.Line.EffectiveCapability(), 1),
                best <= 0.0 ? 0.0 : model.Line.EffectiveCapability() / best,
                "#7ED89E"));

            panel.Add(Gauge(Loc.T("release.best_version"), UiFormat.Number(best, 1), 1.0, "#5B8DEF"));

            panel.Add(Gauge(Loc.T("release.average_price"),
                "$" + model.Line.EffectivePriceUsdPerMonth()
                    .ToString("N2", CultureInfo.InvariantCulture),
                MaximumPriceUsd <= 0f
                    ? 0.0
                    : model.Line.EffectivePriceUsdPerMonth() / MaximumPriceUsd,
                "#E0B83C"));

            // The spread of the audience across versions, as one stacked bar. It is the same data as
            // the list, but a single bar answers "is this fragmented" in a way rows cannot.
            var spread = new VisualElement();
            spread.AddToClassList("vspread");

            for (var index = 0; index < versions.Count; index++)
            {
                var slice = new VisualElement();
                slice.AddToClassList("vspread__slice");
                slice.style.width = Length.Percent((float)(versions[index].Adoption * 100.0));

                // Oldest darkest, newest lightest, so the bar reads left to right as a history
                // rather than as a set of unrelated colours.
                var shade = 0.28f + 0.5f * (index / (float)Math.Max(1, versions.Count));
                slice.style.backgroundColor = new Color(shade, shade + 0.16f, shade + 0.34f);

                spread.Add(slice);
            }

            panel.Add(spread);

            var caption = new Label(versions.Count == 1
                ? Loc.T("release.count_one")
                : Loc.T("release.count_many", versions.Count));

            caption.AddToClassList("vcharts__caption");
            panel.Add(caption);

            return panel;
        }

        private static VisualElement Gauge(string caption, string reading, double fill, string tint)
        {
            var block = new VisualElement();
            block.AddToClassList("vgauge");

            var head = new VisualElement();
            head.AddToClassList("vgauge__head");

            var label = new Label(caption);
            label.AddToClassList("vgauge__caption");
            head.Add(label);

            var value = new Label(reading);
            value.AddToClassList("vgauge__value");
            head.Add(value);

            block.Add(head);

            var track = new VisualElement();
            track.AddToClassList("vgauge__track");

            var bar = new VisualElement();
            bar.AddToClassList("vgauge__fill");
            bar.style.width = Length.Percent((float)(Math.Clamp(fill, 0.0, 1.0) * 100.0));

            if (ColorUtility.TryParseHtmlString(tint, out var colour))
            {
                bar.style.backgroundColor = colour;
            }

            track.Add(bar);
            block.Add(track);

            return block;
        }
    }
}
