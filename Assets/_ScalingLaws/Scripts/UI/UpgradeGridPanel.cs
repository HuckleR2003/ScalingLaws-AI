using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The upgrade grid, laid out like the OS upgrade screen in the tycoon games this borrows from:
    /// a card per trait with a title, a level and a price.
    ///
    /// One difference, and it is the important one. Each card also shows what the market expects
    /// today. A card at level 4 when par is 6 is not merely missing a bonus, it is actively costing
    /// brand, capability and margin, and the card says so in red. That is what turns the grid from a
    /// shopping list into a maintenance schedule.
    /// </summary>
    public sealed class UpgradeGridPanel
    {
        private readonly CompanySimulation simulation;
        private readonly VisualElement root;
        private readonly VisualElement grid = new();
        private readonly DropdownField modelField = new();
        private readonly Label summary = new();
        private readonly Label status = new();
        private readonly List<int> modelIndices = new();

        public UpgradeGridPanel(CompanySimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            root = new VisualElement();
            root.AddToClassList("content");
            Build();
        }

        public VisualElement Root => root;

        private void Build()
        {
            var title = new Label("UPGRADE MODEL");
            title.AddToClassList("page-title");
            root.Add(title);

            var subtitle = new Label(
                "Post-training work on a model that is already live. Every card shows its level against "
                + "what the market now treats as normal. Falling behind is not neutral.");
            subtitle.AddToClassList("page-subtitle");
            root.Add(subtitle);

            modelField.label = "Model";
            modelField.AddToClassList("field");
            modelField.RegisterValueChangedCallback(_ => Refresh());
            root.Add(modelField);

            summary.AddToClassList("page-subtitle");
            root.Add(summary);

            grid.AddToClassList("grid");
            root.Add(grid);

            status.AddToClassList("verdict");
            root.Add(status);
        }

        public void Refresh()
        {
            RebuildModelChoices();
            grid.Clear();

            var modelIndex = SelectedModelIndex();
            if (modelIndex < 0)
            {
                summary.text = "Nothing is live yet. Ship a model before there is anything to upgrade.";
                return;
            }

            var model = simulation.State.DeployedModels[modelIndex];
            var date = simulation.State.Date;
            var shortfall = model.Traits.TotalShortfall(date);

            summary.text =
                $"Measured on release {UiFormat.Number(model.Capability)}, scoring {UiFormat.Number(model.EffectiveCapability(date))} today. "
                + $"Serving cost multiplier {UiFormat.Number(model.EfficiencyMultiplier(date), 2)}. "
                + (shortfall > 0
                    ? $"{shortfall} level(s) behind the market across the grid."
                    : "Level with or ahead of the market on every trait.");

            foreach (var standing in simulation.UpgradeGrid(modelIndex))
            {
                grid.Add(BuildCard(modelIndex, standing));
            }
        }

        private VisualElement BuildCard(int modelIndex, TraitStanding standing)
        {
            var definition = ModelTraitCatalog.Get(standing.Trait);
            var card = new Button();
            card.AddToClassList("card");
            CardArt.Apply(card, CardArt.ForTrait(standing.Trait));

            if (!standing.IsAvailable)
            {
                card.AddToClassList("card--locked");
            }
            else if (standing.IsBehindMarket)
            {
                card.AddToClassList("card--behind");
            }
            else if (standing.Level > standing.ExpectedLevel)
            {
                card.AddToClassList("card--ahead");
            }

            var title = new Label(definition.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var levelLine = new Label(standing.IsAvailable
                ? $"LEVEL {standing.Level}   (market {standing.ExpectedLevel})"
                : $"UNSOLVED UNTIL {definition.AvailableFrom}");
            levelLine.AddToClassList("card__line");
            card.Add(levelLine);

            var costLine = new Label(standing.IsMaxed
                ? "AT THE CEILING"
                : $"{UiFormat.Money(standing.UpgradeCostUsd)}   {UiFormat.Days(standing.UpgradeDays)}   {UiFormat.PetaflopDays(standing.UpgradePetaflopDays)}");
            costLine.AddToClassList("card__line");
            card.Add(costLine);

            if (simulation.State.IsUpgradeInFlight(modelIndex, standing.Trait))
            {
                var badge = new Label("IN PROGRESS");
                badge.AddToClassList("card__badge");
                card.Add(badge);
            }
            else if (standing.IsBehindMarket)
            {
                var badge = new Label($"{standing.Shortfall} BEHIND");
                badge.AddToClassList("card__badge");
                card.Add(badge);
            }

            card.SetEnabled(standing.IsAvailable && !standing.IsMaxed);
            card.clicked += () => StartUpgrade(modelIndex, standing.Trait);
            return card;
        }

        private void StartUpgrade(int modelIndex, ModelTrait trait)
        {
            status.RemoveFromClassList("verdict--ok");
            status.RemoveFromClassList("verdict--blocked");

            if (simulation.TryStartUpgrade(modelIndex, trait, out var reason))
            {
                status.AddToClassList("verdict--ok");
                status.text = $"{ModelTraitCatalog.Get(trait).DisplayName} programme commissioned.";
            }
            else
            {
                status.AddToClassList("verdict--blocked");
                status.text = reason;
            }

            Refresh();
        }

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
    }
}
