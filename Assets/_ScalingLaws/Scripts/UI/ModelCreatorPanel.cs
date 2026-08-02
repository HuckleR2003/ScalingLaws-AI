using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The model creator. Four design decisions and a compute decision, with every consequence
    /// priced live above the button.
    ///
    /// Two rules this screen exists to enforce. First, nothing here is a guess: every readout comes
    /// from <see cref="TrainingPlanner"/>, which is the same code the run itself will use, so the
    /// screen cannot promise something the simulation will not deliver. Second, the capability
    /// number is labelled a projection everywhere it appears, because it is one.
    ///
    /// Compute starts rented. Buying is a later decision made on a different screen once the tier
    /// gates open, and the creator only needs to know how many accelerators are pointed at the run.
    /// </summary>
    public sealed class ModelCreatorPanel
    {
        private readonly CompanySimulation simulation;
        private readonly VisualElement root;

        private readonly TextField nameField = new();
        private readonly DropdownField architectureField = new();
        private readonly Slider parameterSlider = new();
        private readonly Slider tokenSlider = new();
        private readonly Slider rentedSlider = new();
        private readonly VisualElement dataToggles = new();
        private readonly VisualElement readouts = new();
        private readonly Label verdict = new();
        private readonly Button startButton = new();
        private readonly Label parameterLabel = new();
        private readonly Label tokenLabel = new();
        private readonly Label rentedLabel = new();

        private readonly List<ArchitectureId> architectureOptions = new();
        private readonly Dictionary<DatasetSource, Toggle> dataSourceToggles = new();

        /// <summary>Sliders move in log space so one drag covers a billion to a hundred trillion.</summary>
        private const float MinimumLogParameters = -1.0f;   // 0.1B
        private const float MaximumLogParameters = 4.0f;    // 10,000B
        private const float MinimumLogTokens = 1.0f;        // 10B
        private const float MaximumLogTokens = 5.3f;        // 200,000B

        public ModelCreatorPanel(CompanySimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            root = new VisualElement();
            root.AddToClassList("content");
            Build();
        }

        public VisualElement Root => root;

        /// <summary>Rebuilds everything that can change between visits, then reprices the plan.</summary>
        public void Refresh()
        {
            RebuildArchitectures();
            RebuildDataSources();
            rentedSlider.SetValueWithoutNotify((float)simulation.State.Pool.RentedPetaflops);
            Reprice();
        }

        private void Build()
        {
            var title = new Label("NEW MODEL");
            title.AddToClassList("page-title");
            root.Add(title);

            var subtitle = new Label(
                "Four decisions define a run and none of them can be changed once it starts. "
                + "The numbers below are a projection, not a result.");
            subtitle.AddToClassList("page-subtitle");
            root.Add(subtitle);

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            root.Add(columns);

            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.marginRight = 18;
            columns.Add(left);

            var right = new VisualElement();
            right.style.width = 380;
            columns.Add(right);

            left.Add(BuildIdentityPanel());
            left.Add(BuildShapePanel());
            left.Add(BuildDataPanel());
            left.Add(BuildComputePanel());

            right.Add(BuildProjectionPanel());
        }

        private VisualElement BuildIdentityPanel()
        {
            var panel = NewPanel("IDENTITY");

            nameField.label = "Model name";
            nameField.value = "Muse 1";
            nameField.AddToClassList("field");
            nameField.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(nameField);

            architectureField.label = "Architecture";
            architectureField.AddToClassList("field");
            architectureField.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(architectureField);

            var hint = new Label(
                "A sparse mixture costs a quarter of the FLOPs per token and a little quality per parameter. "
                + "On a fixed budget that trade is usually worth taking.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private VisualElement BuildShapePanel()
        {
            var panel = NewPanel("SHAPE");

            parameterLabel.AddToClassList("field__label");
            panel.Add(parameterLabel);
            ConfigureSlider(parameterSlider, MinimumLogParameters, MaximumLogParameters, 1.3f);
            panel.Add(parameterSlider);

            tokenLabel.AddToClassList("field__label");
            panel.Add(tokenLabel);
            ConfigureSlider(tokenSlider, MinimumLogTokens, MaximumLogTokens, 2.6f);
            panel.Add(tokenSlider);

            var hint = new Label(
                "Around twenty tokens per parameter converts the most compute into capability. "
                + "Far from it and the same bill buys a worse model.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            var balance = new Button(BalanceShape) { text = "MATCH THE OPTIMUM" };
            balance.AddToClassList("button");
            balance.style.marginTop = 10;
            panel.Add(balance);

            return panel;
        }

        private VisualElement BuildDataPanel()
        {
            var panel = NewPanel("DATA MIX");
            panel.Add(dataToggles);

            var hint = new Label(
                "The run draws from the best corpus first. A small licensed archive lifts the top of the mix; "
                + "raw crawl adds volume and drags the average down.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private VisualElement BuildComputePanel()
        {
            var panel = NewPanel("COMPUTE");

            rentedLabel.AddToClassList("field__label");
            panel.Add(rentedLabel);
            ConfigureSlider(rentedSlider, 0f, 25000f, 150f);
            panel.Add(rentedSlider);

            var hint = new Label(
                "Rented capacity is contracted in petaflops, not in boxes, so the bill does not move on "
                + "its own when the clouds change generation. It never ages, and it bills every day it "
                + "is held whether or not it is doing anything.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private VisualElement BuildProjectionPanel()
        {
            var panel = NewPanel("PROJECTION");
            panel.Add(readouts);

            verdict.AddToClassList("verdict");
            panel.Add(verdict);

            startButton.text = "START TRAINING";
            startButton.AddToClassList("button");
            startButton.AddToClassList("button--primary");
            startButton.style.marginTop = 14;
            startButton.style.width = Length.Percent(100);
            startButton.clicked += StartTraining;
            panel.Add(startButton);

            return panel;
        }

        private static VisualElement NewPanel(string heading)
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var label = new Label(heading);
            label.AddToClassList("panel__heading");
            panel.Add(label);

            return panel;
        }

        private void ConfigureSlider(Slider slider, float low, float high, float initial)
        {
            slider.lowValue = low;
            slider.highValue = high;
            slider.value = initial;
            slider.AddToClassList("field");
            slider.RegisterValueChangedCallback(_ => Reprice());
        }

        private void RebuildArchitectures()
        {
            architectureOptions.Clear();
            var labels = new List<string>();

            // House families first: they are the ones the company paid to design and the ones a
            // player is most likely to be looking for.
            foreach (var pair in simulation.State.CustomArchitectures)
            {
                architectureOptions.Add(pair.Key);
                labels.Add($"{pair.Value.DisplayName}  (house)");
            }

            foreach (var definition in ArchitectureCatalog.AvailableOn(simulation.State.Date))
            {
                if (!simulation.State.HasArchitecture(definition.Id))
                {
                    continue;
                }

                architectureOptions.Add(definition.Id);
                labels.Add(definition.DisplayName);
            }

            architectureField.choices = labels;
            if (labels.Count > 0 && architectureField.index < 0)
            {
                architectureField.index = 0;
            }
        }

        private void RebuildDataSources()
        {
            dataToggles.Clear();
            dataSourceToggles.Clear();

            foreach (var definition in DatasetCatalog.All)
            {
                if (!simulation.State.HasDataSource(definition.Flag))
                {
                    continue;
                }

                var toggle = new Toggle(
                    $"{definition.DisplayName}  ({UiFormat.Billions(definition.TokenSupplyBillions)} tokens, quality {UiFormat.Number(definition.QualityMultiplier, 2)})");
                toggle.value = definition.Flag == DatasetSource.WebCrawl;
                toggle.RegisterValueChangedCallback(_ => Reprice());
                dataSourceToggles[definition.Flag] = toggle;
                dataToggles.Add(toggle);
            }

            if (dataSourceToggles.Count == 0)
            {
                var empty = new Label("No corpora owned. Acquire data before training anything.");
                empty.AddToClassList("field__hint");
                dataToggles.Add(empty);
            }
        }

        private ModelBlueprint CurrentBlueprint()
        {
            var architecture = architectureOptions.Count > 0 && architectureField.index >= 0
                ? architectureOptions[Math.Clamp(architectureField.index, 0, architectureOptions.Count - 1)]
                : ArchitectureId.DenseTransformer;

            var sources = DatasetSource.None;
            foreach (var pair in dataSourceToggles)
            {
                if (pair.Value.value)
                {
                    sources |= pair.Key;
                }
            }

            return new ModelBlueprint(
                string.IsNullOrWhiteSpace(nameField.value) ? "Untitled model" : nameField.value,
                architecture,
                Math.Pow(10.0, parameterSlider.value),
                Math.Pow(10.0, tokenSlider.value),
                sources);
        }

        /// <summary>Sets the token count to the compute-optimal partner for the current size.</summary>
        private void BalanceShape()
        {
            var blueprint = CurrentBlueprint();
            var architecture = simulation.State.ResolveArchitecture(blueprint.Architecture);
            var flop = ScalingLaw.TrainingFlop(
                blueprint.ParameterCount, blueprint.TrainingTokens, architecture.ActiveParameterFraction);
            var ratio = ScalingLaw.OptimalTokensPerParameter(flop, architecture.ActiveParameterFraction);

            var tokensBillions = Math.Clamp(
                blueprint.ParameterCountBillions * ratio,
                Math.Pow(10.0, MinimumLogTokens),
                Math.Pow(10.0, MaximumLogTokens));

            tokenSlider.value = (float)Math.Log10(tokensBillions);
        }

        private void Reprice()
        {
            simulation.SetRentedPetaflops(rentedSlider.value);

            var blueprint = CurrentBlueprint();
            var projection = simulation.Project(blueprint);
            var profile = simulation.Profile;

            parameterLabel.text = $"Parameters: {UiFormat.Billions(blueprint.ParameterCountBillions)}";
            tokenLabel.text = $"Training tokens: {UiFormat.Billions(blueprint.TrainingTokensBillions)}";
            rentedLabel.text =
                $"Rented capacity: {UiFormat.Petaflops(rentedSlider.value)}  "
                + $"({profile.RentedAcceleratorCount:N0} units of today's part, "
                + $"{UiFormat.Petaflops(profile.EffectivePetaflops)} usable, "
                + $"{UiFormat.Money(SimUnitsToDaily(profile))}/day)";

            readouts.Clear();
            AddReadout("Projected capability", UiFormat.Number(projection.ProjectedCapability), Tone.Neutral);
            AddReadout("Frontier today", UiFormat.Number(simulation.Market.FrontierCapability), Tone.Neutral);
            AddReadout(
                "Tokens per parameter",
                $"{UiFormat.Number(projection.TokensPerParameter)} against {UiFormat.Number(projection.OptimalTokensPerParameter)} optimal",
                projection.IsUndertrained || projection.IsOvertrained ? Tone.Warn : Tone.Good);
            AddReadout("Budget converted", UiFormat.Percent(projection.ShapeEfficiency),
                projection.ShapeEfficiency > 0.9 ? Tone.Good : Tone.Warn);
            AddReadout("Compute", UiFormat.PetaflopDays(projection.TrainingPetaflopDays), Tone.Neutral);
            AddReadout("Duration", UiFormat.Days(projection.TrainingDays), Tone.Neutral);
            AddReadout("Cash it burns", UiFormat.Money(projection.ComputeCashCostUsd),
                projection.ComputeCashCostUsd > simulation.State.CashUsd ? Tone.Bad : Tone.Neutral);
            AddReadout("With value lost", UiFormat.Money(projection.ComputeEconomicCostUsd), Tone.Neutral);
            AddReadout(
                "Accelerator memory",
                $"{UiFormat.Count(projection.MemoryRequiredGigabytes)} GB of {UiFormat.Count(projection.MemoryAvailableGigabytes)} GB",
                projection.MemoryRequiredGigabytes > projection.MemoryAvailableGigabytes ? Tone.Bad : Tone.Good);
            AddReadout("Data quality", UiFormat.Number(projection.Blend.QualityMultiplier, 2),
                projection.Blend.IsSufficient ? Tone.Good : Tone.Bad);

            verdict.RemoveFromClassList("verdict--ok");
            verdict.RemoveFromClassList("verdict--blocked");

            if (projection.IsFeasible)
            {
                verdict.AddToClassList("verdict--ok");
                verdict.text = projection.IsUndertrained
                    ? "Runnable, but this model is too large for the data it will see. The same bill would buy more capability in a smaller model."
                    : projection.IsOvertrained
                        ? "Runnable, but the parameters run out long before the tokens do. Most of this compute is being wasted."
                        : "Runnable and well shaped.";
            }
            else
            {
                verdict.AddToClassList("verdict--blocked");
                verdict.text = projection.BlockingReason;
            }

            startButton.SetEnabled(projection.IsFeasible && simulation.State.ActiveRun == null);
            if (simulation.State.ActiveRun != null)
            {
                verdict.text = "A run is already in flight. One at a time.";
            }
        }

        private static long SimUnitsToDaily(ComputeProfile profile) =>
            Core.SimUnits.ToDollars(profile.DailyOperatingCostUsd);

        private enum Tone
        {
            Neutral,
            Good,
            Warn,
            Bad
        }

        private void AddReadout(string label, string value, Tone tone)
        {
            var row = new VisualElement();
            row.AddToClassList("readout");

            row.Add(new Label(label));

            var valueLabel = new Label(value);
            valueLabel.AddToClassList("readout__value");
            switch (tone)
            {
                case Tone.Good:
                    valueLabel.AddToClassList("readout__value--good");
                    break;
                case Tone.Warn:
                    valueLabel.AddToClassList("readout__value--warn");
                    break;
                case Tone.Bad:
                    valueLabel.AddToClassList("readout__value--bad");
                    break;
            }

            row.Add(valueLabel);
            readouts.Add(row);
        }

        private void StartTraining()
        {
            if (!simulation.TryStartTraining(CurrentBlueprint(), out var reason))
            {
                verdict.RemoveFromClassList("verdict--ok");
                verdict.AddToClassList("verdict--blocked");
                verdict.text = reason;
                return;
            }

            Reprice();
        }
    }
}
