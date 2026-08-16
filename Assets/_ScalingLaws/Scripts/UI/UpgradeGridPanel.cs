using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Post-training work on a model that is already out there.
    ///
    /// **Rebuilt around a basket rather than a button per card.** Every tile used to commission a
    /// programme the instant it was clicked, which made the screen a row of landmines: there was no
    /// way to compare two upgrades, no way to see what three of them together would cost, and no
    /// way back from a misclick. Now tiles are picked, the panel on the right shows exactly what
    /// the selection would do to the model, and nothing is commissioned until the player says so.
    ///
    /// The tiles are landscape rather than square because each one carries a photograph and a row
    /// of numbers, and a square card forced the numbers into three lines of two words.
    /// </summary>
    public sealed class UpgradeGridPanel
    {
        /// <summary>Milliseconds the difference panel takes to draw itself in.</summary>
        public const int RevealMilliseconds = 24;

        /// <summary>Widest a difference bar goes, as a share of its track.</summary>
        public const double BarCeiling = 0.92;

        private readonly CompanySimulation simulation;
        private readonly VisualElement root;
        private readonly VisualElement grid = new();
        private readonly VisualElement diff = new();
        private readonly DropdownField modelField = new();
        private readonly Label status = new();
        private readonly List<int> modelIndices = new();

        /// <summary>True for each entry that points into the shelf rather than the deployed list.</summary>
        private readonly List<bool> modelOnShelf = new();

        /// <summary>
        /// What the player has picked, for the model they are looking at.
        ///
        /// Cleared when the model changes, because a basket built for one model means nothing
        /// against another: the levels differ, so the prices and the gains do too.
        /// </summary>
        private readonly HashSet<ModelTrait> chosen = new();

        private int basketModelIndex = -1;

        /// <summary>Which list the basket was built against. Shelf 0 and deployed 0 are not the same model.</summary>
        private bool basketOnShelf;

        public UpgradeGridPanel(CompanySimulation simulation)
        {
            this.simulation = simulation;
            root = new VisualElement();
            root.AddToClassList("content");
            Build();
        }

        public VisualElement Root => root;

        /// <summary>What is in the basket. Public so a test can drive it without a panel.</summary>
        public IReadOnlyCollection<ModelTrait> Chosen => chosen;

        private void Build()
        {
            var title = new Label("UPGRADE MODEL");
            title.AddToClassList("page-title");
            root.Add(title);

            var blurb = new Label(
                "Post-training work on a model that is already live. Pick everything you want done, "
                + "see what it costs together, then commission it.");

            blurb.AddToClassList("page-subtitle");
            root.Add(blurb);

            modelField.label = "Model";
            modelField.AddToClassList("field");
            modelField.RegisterValueChangedCallback(_ =>
            {
                chosen.Clear();
                Refresh();
            });

            root.Add(modelField);

            var columns = new VisualElement();
            columns.AddToClassList("upg__columns");

            grid.AddToClassList("upg__grid");
            columns.Add(grid);

            diff.AddToClassList("upg__diff");
            columns.Add(diff);

            root.Add(columns);

            status.AddToClassList("verdict");
            root.Add(status);
        }

        public void Refresh()
        {
            RebuildModelChoices();

            var modelIndex = SelectedModelIndex();

            var onShelf = SelectedIsOnShelf();

            if (modelIndex != basketModelIndex || onShelf != basketOnShelf)
            {
                chosen.Clear();
                basketModelIndex = modelIndex;
                basketOnShelf = onShelf;
            }

            grid.Clear();
            diff.Clear();

            if (modelIndex < 0)
            {
                var none = new Label("Nothing is live yet. Release a model and this fills up.");
                none.AddToClassList("upg__empty");
                grid.Add(none);
                return;
            }

            var traits = onShelf
                ? simulation.State.Shelf[modelIndex].Traits
                : simulation.State.DeployedModels[modelIndex].Traits;

            var standings = traits.Standings(simulation.State.Date).ToList();

            foreach (var standing in standings)
            {
                grid.Add(BuildTile(modelIndex, standing, onShelf));
            }

            diff.Add(BuildDiff(modelIndex, standings, onShelf));
        }

        // ---- one tile ---------------------------------------------------------------------------

        private VisualElement BuildTile(int modelIndex, TraitStanding standing, bool onShelf)
        {
            var definition = ModelTraitCatalog.Get(standing.Trait);
            var inFlight = simulation.State.IsUpgradeInFlight(modelIndex, standing.Trait, onShelf);
            var picked = chosen.Contains(standing.Trait);
            var pickable = standing.IsAvailable && !standing.IsMaxed && !inFlight;

            var tile = new Button(() =>
            {
                if (!pickable)
                {
                    return;
                }

                if (!chosen.Remove(standing.Trait))
                {
                    chosen.Add(standing.Trait);
                }

                Refresh();
            });

            tile.AddToClassList("utile");
            tile.EnableInClassList("utile--picked", picked);
            tile.EnableInClassList("utile--locked", !standing.IsAvailable);
            tile.EnableInClassList("utile--busy", inFlight);
            tile.EnableInClassList("utile--behind", standing.IsBehindMarket && pickable);

            // The photograph is its own strip on the left rather than the tile's background, so the
            // numbers sit on flat colour and stay legible whatever the image is doing.
            var art = new VisualElement();
            art.AddToClassList("utile__art");
            CardArt.Apply(art, CardArt.ForTrait(standing.Trait));
            tile.Add(art);

            var body = new VisualElement();
            body.AddToClassList("utile__body");

            var head = new VisualElement();
            head.AddToClassList("utile__head");

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("utile__name");
            head.Add(name);

            if (inFlight)
            {
                head.Add(Tag("IN PROGRESS", "utile__tag--busy"));
            }
            else if (!standing.IsAvailable)
            {
                head.Add(Tag($"FROM {definition.AvailableFrom}", "utile__tag--locked"));
            }
            else if (standing.IsMaxed)
            {
                head.Add(Tag("AT THE CEILING", "utile__tag--max"));
            }
            else if (standing.IsBehindMarket)
            {
                head.Add(Tag($"{standing.Shortfall} BEHIND", "utile__tag--behind"));
            }

            body.Add(head);

            // The level against what the market treats as normal, drawn rather than written: the
            // gap is the entire reason to press this tile and a pair of numbers hid it.
            body.Add(BuildLevelBar(standing));

            var line = new Label(standing.IsMaxed || !standing.IsAvailable
                ? definition.Description
                : $"{UiFormat.Money(standing.UpgradeCostUsd)}   ·   {UiFormat.Days(standing.UpgradeDays)}"
                  + $"   ·   {UiFormat.PetaflopDays(standing.UpgradePetaflopDays)}");

            line.AddToClassList("utile__line");
            body.Add(line);

            tile.Add(body);

            var mark = new VisualElement();
            mark.AddToClassList("utile__check");
            mark.EnableInClassList("utile__check--on", picked);
            tile.Add(mark);

            tile.SetEnabled(pickable);
            return tile;
        }

        private static VisualElement BuildLevelBar(TraitStanding standing)
        {
            var block = new VisualElement();
            block.AddToClassList("ulevel");

            var track = new VisualElement();
            track.AddToClassList("ulevel__track");

            var ceiling = Math.Max(1, ModelTraitSetLimits.MaximumLevel);

            var mine = new VisualElement();
            mine.AddToClassList("ulevel__mine");
            mine.style.width = Length.Percent(standing.Level / (float)ceiling * 100f);
            track.Add(mine);

            // The market's line sits on top of the bar rather than beside it, so "behind" is a
            // picture instead of a subtraction the player has to do.
            var market = new VisualElement();
            market.AddToClassList("ulevel__market");
            market.style.left = Length.Percent(standing.ExpectedLevel / (float)ceiling * 100f);
            track.Add(market);

            block.Add(track);

            var reading = new Label($"LEVEL {standing.Level}   ·   MARKET {standing.ExpectedLevel}");
            reading.AddToClassList("ulevel__reading");
            block.Add(reading);

            return block;
        }

        private static VisualElement Tag(string text, string style)
        {
            var tag = new Label(text);
            tag.AddToClassList("utile__tag");
            tag.AddToClassList(style);
            return tag;
        }

        // ---- the difference panel ------------------------------------------------------------------

        /// <summary>
        /// What the basket would do to this model, before against after.
        ///
        /// **Everything here is the catalog's own arithmetic, not a mock-up.** Capability, brand and
        /// serving efficiency each move by a stated amount per level, so the after column is the
        /// before column plus the levels being bought. The bars animate in because the difference is
        /// the point of the panel and a static number does not read as a change.
        /// </summary>
        private VisualElement BuildDiff(int modelIndex, IReadOnlyList<TraitStanding> standings,
            bool onShelf)
        {
            var panel = new VisualElement();
            panel.AddToClassList("udiff");

            var heading = new Label("WHAT IT WOULD DO");
            heading.AddToClassList("udiff__heading");
            panel.Add(heading);

            var today = simulation.State.Date;

            var name = onShelf
                ? simulation.State.Shelf[modelIndex].Name
                : simulation.State.DeployedModels[modelIndex].Name;

            var subject = new Label(name);
            subject.AddToClassList("udiff__subject");
            panel.Add(subject);

            if (onShelf)
            {
                var note = new Label("Not released yet. Anything done now ships with it.");
                note.AddToClassList("udiff__shelf");
                panel.Add(note);
            }

            if (chosen.Count == 0)
            {
                var hint = new Label(
                    "Pick one or more upgrades on the left. They can be commissioned together, and "
                    + "the cluster splits its time between them.");

                hint.AddToClassList("udiff__hint");
                panel.Add(hint);
                return panel;
            }

            var picked = standings.Where(entry => chosen.Contains(entry.Trait)).ToList();

            double capability = 0.0;
            double brand = 0.0;
            double efficiency = 0.0;
            long cash = 0L;
            var days = 0;
            var petaflopDays = 0.0;

            foreach (var standing in picked)
            {
                var definition = ModelTraitCatalog.Get(standing.Trait);

                capability += definition.CapabilityPerLevel;
                brand += definition.BrandPerLevel;
                efficiency += definition.EfficiencyPerLevel;

                cash += standing.UpgradeCostUsd;
                petaflopDays += standing.UpgradePetaflopDays;

                // The cluster runs the programmes side by side, so the calendar is the longest of
                // them rather than the sum. The compute is the sum, which is what actually slows
                // them all down, and that is already in the petaflop-day total.
                days = Math.Max(days, standing.UpgradeDays);
            }

            var nowCapability = onShelf
                ? simulation.State.Shelf[modelIndex].CapabilityIfReleasedOn(today)
                : simulation.State.DeployedModels[modelIndex].EffectiveCapability(today);

            var traits = onShelf
                ? simulation.State.Shelf[modelIndex].Traits
                : simulation.State.DeployedModels[modelIndex].Traits;

            panel.Add(BuildDiffRow("CAPABILITY", nowCapability, nowCapability + capability, 1));

            panel.Add(BuildDiffRow("BRAND", traits.BrandBonus(today) * 100.0,
                (traits.BrandBonus(today) + brand) * 100.0, 1, "%"));

            panel.Add(BuildDiffRow("SERVING EFFICIENCY", traits.EfficiencyMultiplier(today) * 100.0,
                (traits.EfficiencyMultiplier(today) + efficiency) * 100.0, 1, "%"));

            var list = new VisualElement();
            list.AddToClassList("udiff__list");

            foreach (var standing in picked)
            {
                var row = new Label($"{ModelTraitCatalog.Get(standing.Trait).DisplayName}  "
                    + $"{standing.Level} → {standing.Level + 1}");

                row.AddToClassList("udiff__item");
                list.Add(row);
            }

            panel.Add(list);

            var bill = new VisualElement();
            bill.AddToClassList("udiff__bill");
            bill.Add(BillCell("COST", UiFormat.Money(cash)));
            bill.Add(BillCell("CALENDAR", UiFormat.Days(days)));
            bill.Add(BillCell("COMPUTE", UiFormat.PetaflopDays(petaflopDays)));
            panel.Add(bill);

            var buttons = new VisualElement();
            buttons.AddToClassList("udiff__buttons");

            var cancel = new Button(() =>
            {
                chosen.Clear();
                Refresh();
            })
            { text = "CANCEL" };

            cancel.AddToClassList("udiff__cancel");
            buttons.Add(cancel);

            var start = new Button(() => StartAll(modelIndex, onShelf))
            {
                text = picked.Count == 1 ? "START UPGRADE" : $"START {picked.Count} UPGRADES"
            };

            start.AddToClassList("udiff__start");
            start.SetEnabled(simulation.State.CashUsd >= cash);
            buttons.Add(start);

            panel.Add(buttons);

            if (simulation.State.CashUsd < cash)
            {
                var short_ = new Label(
                    $"Needs {UiFormat.Money(cash)} and the company has "
                    + $"{UiFormat.Money(simulation.State.CashUsd)}.");

                short_.AddToClassList("udiff__short");
                panel.Add(short_);
            }

            return panel;
        }

        /// <summary>
        /// One before-and-after line, with the gain drawn on top of the current value.
        ///
        /// Born at zero width and released a frame later so the transition has somewhere to move
        /// from. Same trick the page arrival and the tooltips use.
        /// </summary>
        private static VisualElement BuildDiffRow(string caption, double before, double after,
            int decimals, string suffix = "")
        {
            var row = new VisualElement();
            row.AddToClassList("udrow");

            var head = new VisualElement();
            head.AddToClassList("udrow__head");

            var label = new Label(caption);
            label.AddToClassList("udrow__caption");
            head.Add(label);

            var numbers = new VisualElement();
            numbers.AddToClassList("udrow__numbers");

            var from = new Label(UiFormat.Number(before, decimals) + suffix);
            from.AddToClassList("udrow__before");
            numbers.Add(from);

            var arrow = new Label("→");
            arrow.AddToClassList("udrow__arrow");
            numbers.Add(arrow);

            var to = new Label(UiFormat.Number(after, decimals) + suffix);
            to.AddToClassList("udrow__after");
            numbers.Add(to);

            head.Add(numbers);
            row.Add(head);

            var track = new VisualElement();
            track.AddToClassList("udrow__track");

            var scale = Math.Max(after, 1e-6);

            var held = new VisualElement();
            held.AddToClassList("udrow__held");
            held.style.width = Length.Percent((float)(Math.Clamp(before / scale, 0.0, 1.0)
                * BarCeiling * 100.0));

            track.Add(held);

            var gained = new VisualElement();
            gained.AddToClassList("udrow__gained");
            gained.style.width = 0;
            track.Add(gained);

            row.Add(track);

            var target = (float)(Math.Clamp((after - before) / scale, 0.0, 1.0) * BarCeiling * 100.0);

            gained.schedule.Execute(() => gained.style.width = Length.Percent(target))
                .ExecuteLater(RevealMilliseconds);

            return row;
        }

        private static VisualElement BillCell(string caption, string value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("udiff__cell");

            var label = new Label(caption);
            label.AddToClassList("udiff__cellcaption");
            cell.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("udiff__cellvalue");
            cell.Add(reading);

            return cell;
        }

        // ---- commissioning -------------------------------------------------------------------------

        /// <summary>
        /// Commissions everything in the basket.
        ///
        /// Public so a test can drive it without a panel to dispatch clicks into. Whatever could not
        /// be started is reported by name rather than swallowed: a basket that half worked and said
        /// nothing is worse than one that failed.
        /// </summary>
        public void StartAll(int modelIndex, bool onShelf = false)
        {
            status.RemoveFromClassList("verdict--ok");
            status.RemoveFromClassList("verdict--blocked");

            var started = 0;
            var refused = new List<string>();

            foreach (var trait in chosen.ToList())
            {
                if (simulation.TryStartUpgrade(modelIndex, trait, out var reason, onShelf))
                {
                    started++;
                    chosen.Remove(trait);
                }
                else
                {
                    refused.Add($"{ModelTraitCatalog.Get(trait).DisplayName}: {reason}");
                }
            }

            if (refused.Count == 0)
            {
                status.AddToClassList("verdict--ok");
                status.text = started == 1
                    ? "Programme commissioned."
                    : $"{started} programmes commissioned. They share the cluster.";
            }
            else
            {
                status.AddToClassList("verdict--blocked");
                status.text = started > 0
                    ? $"{started} started. {string.Join("  ", refused)}"
                    : string.Join("  ", refused);
            }

            Refresh();
        }

        private void RebuildModelChoices()
        {
            modelIndices.Clear();
            modelOnShelf.Clear();
            var labels = new List<string>();

            // **The shelf comes first.** A finished run waiting to be released is the moment a lab
            // actually does its post-training work, and it was the one state this screen could not
            // see: the list filtered on IsLiveOn, so a company whose only model was on the shelf
            // opened UPGRADE and found nothing at all.
            for (var index = 0; index < simulation.State.Shelf.Count; index++)
            {
                var shelved = simulation.State.Shelf[index];

                modelIndices.Add(index);
                modelOnShelf.Add(true);
                labels.Add($"{shelved.Name}  (on the shelf, "
                    + $"{UiFormat.Number(shelved.CapabilityIfReleasedOn(simulation.State.Date))})");
            }

            for (var index = 0; index < simulation.State.DeployedModels.Count; index++)
            {
                var model = simulation.State.DeployedModels[index];
                if (!model.IsLiveOn(simulation.State.Date))
                {
                    continue;
                }

                modelIndices.Add(index);
                modelOnShelf.Add(false);
                labels.Add($"{model.Name}  ({UiFormat.Number(model.EffectiveCapability(simulation.State.Date))})");
            }

            modelField.choices = labels;
            if (labels.Count > 0 && (modelField.index < 0 || modelField.index >= labels.Count))
            {
                modelField.index = 0;
            }
        }

        private int SelectedModelIndex()
        {
            if (modelIndices.Count == 0 || modelField.index < 0)
            {
                return -1;
            }

            return modelIndices[Math.Clamp(modelField.index, 0, modelIndices.Count - 1)];
        }

        /// <summary>Whether the chosen entry is a shelved model rather than one on sale.</summary>
        private bool SelectedIsOnShelf() =>
            modelOnShelf.Count != 0 && modelField.index >= 0
            && modelOnShelf[Math.Clamp(modelField.index, 0, modelOnShelf.Count - 1)];
    }
}
