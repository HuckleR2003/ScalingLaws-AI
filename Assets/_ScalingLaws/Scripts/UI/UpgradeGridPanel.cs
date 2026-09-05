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
    /// **Two columns, and the split is the design.** The left is a wall of things you could improve,
    /// each carrying its own photograph behind a scrim so the list reads as a shelf of products
    /// rather than a settings page. The right is one panel about the model itself: what it is, what
    /// the basket would do to it, and the two ways out.
    ///
    /// Nothing is commissioned from this screen. Picking tiles builds a basket, the right-hand panel
    /// prices it, and the green button carries the whole thing through to the release planner where
    /// the version is named and the price is set. Work that starts the moment a card is clicked is
    /// work the player cannot compare, cost, or back out of.
    /// </summary>
    public sealed class UpgradeGridPanel
    {
        /// <summary>Milliseconds before a difference bar animates in.</summary>
        public const int RevealMilliseconds = 24;

        /// <summary>Widest a difference bar goes, as a share of its track.</summary>
        public const double BarCeiling = 0.92;

        /// <summary>
        /// How wide the two halves of a difference bar are, as shares of the track.
        ///
        /// **Scaling by the after value alone breaks the moment a reading can be negative**, and one
        /// of the three on this panel is: brand is measured against the market, so a model behind
        /// par reads -28.6%. Dividing by that gave a near-zero denominator, which pinned the gain
        /// bar to full width and drew a two point improvement as if it were a total transformation.
        /// Found by rendering the screen and looking at it.
        ///
        /// Scaling by the larger magnitude instead means the track is always the size of the bigger
        /// number, the held bar is what is already there (nothing, when the reading is negative,
        /// which is the truth), and the gain is the change measured against the same ruler.
        /// </summary>
        public static (double Held, double Gained) BarWidths(double before, double after)
        {
            var scale = Math.Max(Math.Max(Math.Abs(before), Math.Abs(after)), 1e-6);

            return (Math.Clamp(before / scale, 0.0, 1.0) * BarCeiling,
                Math.Clamp((after - before) / scale, 0.0, 1.0) * BarCeiling);
        }

        private readonly CompanySimulation simulation;
        private readonly Action<int, IReadOnlyList<ModelTrait>> planRelease;
        private readonly Action goBack;

        private readonly VisualElement root;
        private readonly VisualElement tiles = new();
        private readonly VisualElement detail = new();
        private readonly DropdownField modelField = new();
        private readonly List<int> modelIndices = new();

        /// <summary>What the player has picked, for the model they are looking at.</summary>
        private readonly HashSet<ModelTrait> chosen = new();

        private int basketModelIndex = -1;
        private string problem = string.Empty;

        public UpgradeGridPanel(CompanySimulation simulation,
            Action<int, IReadOnlyList<ModelTrait>> planRelease, Action goBack)
        {
            this.simulation = simulation;
            this.planRelease = planRelease;
            this.goBack = goBack;

            root = new VisualElement();
            root.AddToClassList("content");

            // The two words this whole screen is made of, explained where somebody who does not
            // know them is standing.
            UiParts.ExplainPage(root, TechNotes.Traits, TechNotes.TraitLevels);
            root.AddToClassList("upg");

            Build();
        }

        public VisualElement Root => root;

        /// <summary>What is in the basket. Public so a test can drive it without a panel.</summary>
        public IReadOnlyCollection<ModelTrait> Chosen => chosen;

        /// <summary>
        /// Adds or removes one upgrade, exactly as clicking its tile does.
        ///
        /// **The tile calls this rather than keeping its own copy.** An EditMode test has no panel,
        /// so a click sent to a button is never dispatched and the basket could only ever be driven
        /// from here; two bodies would mean the tested path and the played path could drift, which
        /// is how the model type came to be chosen everywhere and passed nowhere.
        /// </summary>
        public void Pick(ModelTrait trait)
        {
            if (!chosen.Remove(trait))
            {
                chosen.Add(trait);
            }

            Refresh();
        }

        private void Build()
        {
            var head = new VisualElement();
            head.AddToClassList("upg__head");

            var title = new Label(Loc.T("upgrade.title"));
            title.AddToClassList("upg__title");
            head.Add(title);

            modelField.AddToClassList("upg__picker");
            modelField.RegisterValueChangedCallback(_ =>
            {
                chosen.Clear();
                Refresh();
            });

            head.Add(modelField);
            root.Add(head);

            var columns = new VisualElement();
            columns.AddToClassList("upg__columns");

            tiles.AddToClassList("upg__tiles");
            columns.Add(tiles);

            detail.AddToClassList("upg__detail");
            columns.Add(detail);

            root.Add(columns);
        }

        /// <summary>Opens the model creator, for the empty state. Null when there is nowhere to go.</summary>
        public System.Action goToCreator;

        public void Refresh()
        {
            RebuildModelChoices();

            var modelIndex = SelectedModelIndex();

            if (modelIndex != basketModelIndex)
            {
                chosen.Clear();
                basketModelIndex = modelIndex;
            }

            tiles.Clear();
            detail.Clear();

            if (modelIndex < 0)
            {
                // **A panel with a reason and a door, not one grey sentence.** The release screen
                // was given this treatment last week and this screen was left as a line of text in
                // an otherwise empty page, which reads as a screen that failed to load rather than
                // as a screen with nothing on it yet.
                var empty = new VisualElement();
                empty.AddToClassList("panel");
                empty.AddToClassList("emptystate");

                var heading = new Label(Loc.T("upgrade.empty.title"));
                heading.AddToClassList("emptystate__title");
                empty.Add(heading);

                var body = new Label(Loc.T("upgrade.empty.body"));
                body.AddToClassList("emptystate__body");
                empty.Add(body);

                if (goToCreator != null)
                {
                    var go = new Button(goToCreator) { text = Loc.T("upgrade.empty.go") };
                    go.AddToClassList("button");
                    go.AddToClassList("button--primary");
                    go.AddToClassList("emptystate__go");
                    empty.Add(go);
                }

                tiles.Add(empty);
                return;
            }

            var model = simulation.State.DeployedModels[modelIndex];
            var standings = model.Traits
                .Standings(simulation.State.Date, simulation.State.HasResearch)
                .ToList();

            foreach (var standing in standings)
            {
                tiles.Add(BuildTile(modelIndex, standing));
            }

            detail.Add(BuildDetail(modelIndex, model, standings));
        }

        // ---- the wall on the left -----------------------------------------------------------------

        /// <summary>
        /// One thing you could improve: a photograph, a name, a level, and a ring when it is picked.
        ///
        /// The picture is the tile's own background with a scrim over it rather than a strip beside
        /// it, because at this size a photograph in a corner reads as an icon and a photograph
        /// behind the words reads as a product. The scrim is what keeps the words legible over
        /// whatever the image happens to be doing.
        /// </summary>
        private VisualElement BuildTile(int modelIndex, TraitStanding standing)
        {
            var definition = ModelTraitCatalog.Get(standing.Trait);
            var inFlight = simulation.State.IsUpgradeInFlight(modelIndex, standing.Trait);
            var picked = chosen.Contains(standing.Trait);
            var pickable = standing.IsAvailable && !standing.IsMaxed && !inFlight;

            var tile = new Button(() =>
            {
                if (pickable)
                {
                    Pick(standing.Trait);
                }
            });

            tile.AddToClassList("utile");
            tile.EnableInClassList("utile--picked", picked);
            tile.EnableInClassList("utile--locked", !standing.IsAvailable);
            tile.EnableInClassList("utile--busy", inFlight);

            var art = Resources.Load<Texture2D>("Cards/" + CardArt.ForTrait(standing.Trait));

            if (art != null)
            {
                tile.style.backgroundImage = new StyleBackground(art);
                tile.AddToClassList("utile--art");
            }

            // The scrim. Its own element rather than a background colour, so it can be a gradient
            // that is heavy where the words are and light where the picture should show through.
            var scrim = new VisualElement();
            scrim.AddToClassList("utile__scrim");
            scrim.pickingMode = PickingMode.Ignore;
            tile.Add(scrim);

            var words = new VisualElement();
            words.AddToClassList("utile__words");
            words.pickingMode = PickingMode.Ignore;

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("utile__name");
            words.Add(name);

            // **Three reasons a tile can be shut and they are not the same sentence.** The
            // date has not arrived, or the research has not been done, or it is already at the
            // ceiling. A grey tile with a level on it and no explanation is the thing that sent a
            // playtest looking for a bug in the upgrade system.
            // **The level is meaningless without the number it is measured against**, and that
            // number only appeared once the model had fallen behind, as a red badge. So the two
            // states a player meets first, at par and ahead of it, drew a level and no ruler. A
            // model ships at market par on every trait, and a first-time reader with no ruler on
            // screen reads those levels as having come from nowhere.
            var level = new Label(
                standing.IsAvailable
                    ? Loc.T("upgrade.level_market", standing.Level, standing.ExpectedLevel)
                : standing.Needs != ResearchNodeId.None
                    ? Loc.T("upgrade.needs", ResearchTree.Get(standing.Needs).DisplayName)
                    : Loc.T("upgrade.from", definition.AvailableFrom));

            level.AddToClassList("utile__level");
            words.Add(level);

            if (standing.IsAvailable && !standing.IsMaxed)
            {
                var next = new Label($"→ {standing.Level + 1}   ·   "
                    + $"{UiFormat.Money(standing.UpgradeCostUsd)}");

                next.AddToClassList("utile__next");
                words.Add(next);
            }

            tile.Add(words);

            // The state badge, out of flow in the corner so a long name cannot push it off.
            if (inFlight)
            {
                tile.Add(Badge(Loc.T("common.in_progress"), "utile__badge--busy"));
            }
            else if (standing.IsMaxed)
            {
                tile.Add(Badge(Loc.T("upgrade.max"), "utile__badge--max"));
            }
            else if (standing.IsBehindMarket)
            {
                tile.Add(Badge(Loc.T("upgrade.behind", standing.Shortfall), "utile__badge--behind"));
            }

            var check = new VisualElement();
            check.AddToClassList("utile__check");
            check.EnableInClassList("utile__check--on", picked);
            check.pickingMode = PickingMode.Ignore;
            tile.Add(check);

            tile.SetEnabled(pickable);
            return tile;
        }

        private static VisualElement Badge(string text, string style)
        {
            var badge = new Label(text);
            badge.AddToClassList("utile__badge");
            badge.AddToClassList(style);
            badge.pickingMode = PickingMode.Ignore;
            return badge;
        }

        // ---- the panel on the right ------------------------------------------------------------------

        /// <summary>
        /// The model, the thing being built, and what it would cost.
        ///
        /// Top to bottom: what this is, a picture of it, what changes, what it costs, and the two
        /// ways out. That order is the order somebody actually decides in, and the buttons are last
        /// because a commit button above the numbers it commits to is a trap.
        /// </summary>
        private VisualElement BuildDetail(int modelIndex, DeployedModel model,
            IReadOnlyList<TraitStanding> standings)
        {
            var panel = new VisualElement();
            panel.AddToClassList("udet");

            var kicker = new Label(Loc.T("upgrade.upgrading"));
            kicker.AddToClassList("udet__kicker");
            panel.Add(kicker);

            var name = new Label(model.Name);
            name.AddToClassList("udet__name");
            panel.Add(name);

            var version = new Label(Loc.T("upgrade.version", model.Line.PreviousName));
            version.AddToClassList("udet__version");
            panel.Add(version);

            // Beside the model rather than in the page note at the top, because it is a fact about
            // this model on the day it shipped and not a definition of the word "trait".
            var par = new Label(Loc.T("upgrade.par_note"));
            par.AddToClassList("udet__par");
            panel.Add(par);

            panel.Add(BuildChip(model));

            var picked = standings.Where(entry => chosen.Contains(entry.Trait)).ToList();

            if (picked.Count == 0)
            {
                var hint = new Label(Loc.T("upgrade.pick_hint"));

                hint.AddToClassList("udet__hint");
                panel.Add(hint);
                panel.Add(BuildButtons(modelIndex, false, 0L));
                return panel;
            }

            double capability = 0.0, brand = 0.0, efficiency = 0.0;
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

                // **The sum, and through the same scaling the commission uses.** This took the
                // longest of them, on the theory that the cluster ran them side by side. It did —
                // that was the bug: four programmes counting the same calendar down together. One
                // team does one job after another, so the days add up, and quoting anything else
                // here would price the basket at a fraction of what it costs.
                days += simulation.ScaleResearchDuration(standing.UpgradeDays);
            }

            var today = simulation.State.Date;

            var changes = new VisualElement();
            changes.AddToClassList("udet__changes");

            changes.Add(Row(Loc.T("upgrade.capability"), model.EffectiveCapability(today),
                model.EffectiveCapability(today) + capability, 1));

            changes.Add(Row(Loc.T("upgrade.brand"), model.BrandBonus(today) * 100.0,
                (model.BrandBonus(today) + brand) * 100.0, 1, "%"));

            changes.Add(Row(Loc.T("upgrade.efficiency"), model.EfficiencyMultiplier(today) * 100.0,
                (model.EfficiencyMultiplier(today) + efficiency) * 100.0, 1, "%"));

            panel.Add(changes);

            var list = new VisualElement();
            list.AddToClassList("udet__list");

            foreach (var standing in picked)
            {
                var line = new Label($"{ModelTraitCatalog.Get(standing.Trait).DisplayName}   "
                    + $"{standing.Level} → {standing.Level + 1}");

                line.AddToClassList("udet__item");
                list.Add(line);
            }

            panel.Add(list);

            var bill = new VisualElement();
            bill.AddToClassList("udet__bill");
            bill.Add(Cell(Loc.T("upgrade.cost"), UiFormat.Money(cash)));
            bill.Add(Cell(Loc.T("upgrade.time"), UiFormat.Days(days)));
            bill.Add(Cell(Loc.T("upgrade.compute"), UiFormat.PetaflopDays(petaflopDays)));
            panel.Add(bill);

            if (!string.IsNullOrEmpty(problem))
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("udet__problem");
                panel.Add(trouble);
            }

            panel.Add(BuildButtons(modelIndex, true, cash));
            return panel;
        }

        /// <summary>
        /// The picture of the thing being improved.
        ///
        /// A silicon plate for now, drawn rather than photographed: there is no per-model art in the
        /// project and an empty frame in the middle of the panel reads as a failed load. Listed in
        /// Docs/NeededGraphics.md as the one image this screen actually wants.
        /// </summary>
        private static VisualElement BuildChip(DeployedModel model)
        {
            var stage = new VisualElement();
            stage.AddToClassList("uchip");

            var art = Resources.Load<Texture2D>("Cards/chip_model");

            if (art != null)
            {
                stage.style.backgroundImage = new StyleBackground(art);
                return stage;
            }

            var die = new VisualElement();
            die.AddToClassList("uchip__die");

            // Contact rows down both sides, which is the whole reason a rectangle reads as a chip.
            for (var side = 0; side < 2; side++)
            {
                var rail = new VisualElement();
                rail.AddToClassList("uchip__rail");
                rail.AddToClassList(side == 0 ? "uchip__rail--left" : "uchip__rail--right");

                for (var pin = 0; pin < 9; pin++)
                {
                    var contact = new VisualElement();
                    contact.AddToClassList("uchip__pin");
                    rail.Add(contact);
                }

                die.Add(rail);
            }

            var stamp = new Label(model.Name.Length > 14 ? model.Name[..14] : model.Name);
            stamp.AddToClassList("uchip__stamp");
            die.Add(stamp);

            stage.Add(die);
            return stage;
        }

        private VisualElement BuildButtons(int modelIndex, bool anyPicked, long cash)
        {
            var row = new VisualElement();
            row.AddToClassList("udet__buttons");

            var back = new Button(() => goBack?.Invoke()) { text = Loc.T("common.back") };
            back.AddToClassList("udet__back");
            row.Add(back);

            var affordable = simulation.State.CashUsd >= cash;

            var go = new Button(() =>
            {
                if (!affordable)
                {
                    problem = $"Needs {UiFormat.Money(cash)} and the company has "
                        + $"{UiFormat.Money(simulation.State.CashUsd)}.";

                    Refresh();
                    return;
                }

                planRelease?.Invoke(modelIndex, chosen.ToList());
            })
            { text = Loc.T("upgrade.plan_release") };

            go.AddToClassList("udet__go");
            go.SetEnabled(anyPicked && affordable);
            row.Add(go);

            return row;
        }

        /// <summary>One before-and-after line, with the gain drawn on top of what is already there.</summary>
        private static VisualElement Row(string caption, double before, double after, int decimals,
            string suffix = "")
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

            var (heldShare, gainedShare) = BarWidths(before, after);

            var held = new VisualElement();
            held.AddToClassList("udrow__held");
            held.style.width = Length.Percent((float)(heldShare * 100.0));
            track.Add(held);

            var gained = new VisualElement();
            gained.AddToClassList("udrow__gained");
            gained.style.width = 0;
            track.Add(gained);

            row.Add(track);

            var target = (float)(gainedShare * 100.0);

            // Born at zero and released a frame later, so the gain reads as a change rather than as
            // a bar that was always that wide.
            gained.schedule.Execute(() => gained.style.width = Length.Percent(target))
                .ExecuteLater(RevealMilliseconds);

            return row;
        }

        private static VisualElement Cell(string caption, string value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("udet__cell");

            var label = new Label(caption);
            label.AddToClassList("udet__cellcaption");
            cell.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("udet__cellvalue");
            cell.Add(reading);

            return cell;
        }

        // ---- the model picker ---------------------------------------------------------------------------

        private void RebuildModelChoices()
        {
            modelIndices.Clear();
            var labels = new List<string>();

            for (var index = 0; index < simulation.State.DeployedModels.Count; index++)
            {
                var model = simulation.State.DeployedModels[index];

                if (!model.IsLiveOn(simulation.State.Date))
                {
                    continue;
                }

                modelIndices.Add(index);
                labels.Add(model.Name);
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
    }
}
