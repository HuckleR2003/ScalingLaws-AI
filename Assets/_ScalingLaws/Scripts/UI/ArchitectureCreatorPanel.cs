using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The family creator. Deliberately the same screen grammar as the model creator, because it is
    /// the same kind of decision one level up: weight the directions, set a budget, set a deadline,
    /// commit, and live with it.
    ///
    /// The one thing this screen says that the model creator does not is that the answer is not
    /// known in advance. Instead of a single projected number it shows a band, and the band gets
    /// narrower as the programme gets better funded and longer. A cheap rushed family is a lottery
    /// ticket and the screen shows you the size of the lottery before you buy it.
    /// </summary>
    public sealed class ArchitectureCreatorPanel
    {
        private readonly CompanySimulation simulation;
        private readonly VisualElement root;

        private readonly TextField nameField = new();
        private readonly DropdownField slotField = new();
        private readonly DropdownField baseField = new();
        private readonly Slider budgetSlider = new();
        private readonly Slider durationSlider = new();
        private readonly VisualElement directionSliders = new();
        private readonly VisualElement readouts = new();
        private readonly Label budgetLabel = new();
        private readonly Label durationLabel = new();
        private readonly Label verdict = new();
        private readonly Button startButton = new();
        private readonly VisualElement ownedList = new();

        private readonly Dictionary<ResearchDirection, Slider> directions = new();
        private readonly List<ArchitectureId> slotOptions = new();
        private readonly List<ArchitectureId> baseOptions = new();

        private const float MinimumLogBudget = 6.3f;   // about 2M
        private const float MaximumLogBudget = 9.3f;   // about 2B

        public ArchitectureCreatorPanel(CompanySimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            root = new VisualElement();
            root.AddToClassList("content");
            Build();
        }

        public VisualElement Root => root;

        public void Refresh()
        {
            RebuildSlots();
            RebuildBases();
            RebuildOwned();
            Reprice();
        }

        private void Build()
        {
            var title = new Label("ARCHITECTURE FAMILY");
            title.AddToClassList("page-title");
            root.Add(title);

            var subtitle = new Label(
                "The family every later model inherits from. Budget and calendar together decide how far "
                + "the programme reaches; focus decides whether it reaches anywhere at all.");
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
            right.style.width = 400;
            columns.Add(right);

            left.Add(BuildIdentityPanel());
            left.Add(BuildDirectionPanel());
            left.Add(BuildInvestmentPanel());

            right.Add(BuildOutcomePanel());
            right.Add(BuildOwnedPanel());
        }

        private VisualElement BuildIdentityPanel()
        {
            var panel = NewPanel("PROGRAMME");

            nameField.label = "Family name";
            nameField.value = "House family 1";
            nameField.AddToClassList("field");
            nameField.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(nameField);

            slotField.label = "Slot";
            slotField.AddToClassList("field");
            slotField.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(slotField);

            baseField.label = "Build on";
            baseField.AddToClassList("field");
            baseField.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(baseField);

            var hint = new Label(
                "Iterating a family you already own costs 40 percent less and takes 40 percent less time, "
                + "and each generation reaches about half as far as the one before it. Families plateau. "
                + "A clean sheet costs full price and has no such ceiling.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private VisualElement BuildDirectionPanel()
        {
            var panel = NewPanel("RESEARCH DIRECTIONS");
            panel.Add(directionSliders);

            AddDirection(ResearchDirection.Sparsity, "Sparsity",
                "Fewer parameters firing per token. The biggest lever on what a run costs.", 1.0f);
            AddDirection(ResearchDirection.Throughput, "Throughput",
                "Better cluster utilisation while training. Shortens the calendar.", 0.4f);
            AddDirection(ResearchDirection.Quality, "Quality per parameter",
                "Raises the ceiling on every model in the family.", 0.5f);
            AddDirection(ResearchDirection.Serving, "Serving cost",
                "Cheaper tokens once models are live. Invisible until the price war.", 0.3f);
            AddDirection(ResearchDirection.Reasoning, "Reasoning",
                "Structural gains that scaling alone does not buy.", 0.2f);

            var hint = new Label(
                "Effort is relative. Spreading it evenly across all five buys a fifth of the depth in each, "
                + "which is a real choice and usually a bad one.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private void AddDirection(ResearchDirection direction, string label, string hint, float initial)
        {
            var caption = new Label(label);
            caption.AddToClassList("field__label");
            directionSliders.Add(caption);

            var slider = new Slider(0f, 1f) { value = initial };
            slider.AddToClassList("field");
            slider.RegisterValueChangedCallback(_ => Reprice());
            directions[direction] = slider;
            directionSliders.Add(slider);

            var note = new Label(hint);
            note.AddToClassList("field__hint");
            directionSliders.Add(note);
        }

        private VisualElement BuildInvestmentPanel()
        {
            var panel = NewPanel("INVESTMENT");

            budgetLabel.AddToClassList("field__label");
            panel.Add(budgetLabel);
            budgetSlider.lowValue = MinimumLogBudget;
            budgetSlider.highValue = MaximumLogBudget;
            budgetSlider.value = 7.0f;
            budgetSlider.AddToClassList("field");
            budgetSlider.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(budgetSlider);

            durationLabel.AddToClassList("field__label");
            panel.Add(durationLabel);
            durationSlider.lowValue = ArchitectureBlueprint.MinimumDurationDays;
            durationSlider.highValue = ArchitectureBlueprint.MaximumDurationDays;
            durationSlider.value = 365f;
            durationSlider.AddToClassList("field");
            durationSlider.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(durationSlider);

            var hint = new Label(
                "Money and time are a geometric mean, not a sum. A billion dollars in three months is not "
                + "a breakthrough, and neither is three years of two people.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private VisualElement BuildOutcomePanel()
        {
            var panel = NewPanel("LIKELY OUTCOME");
            panel.Add(readouts);

            verdict.AddToClassList("verdict");
            panel.Add(verdict);

            startButton.text = "COMMIT THE PROGRAMME";
            startButton.AddToClassList("button");
            startButton.AddToClassList("button--primary");
            startButton.style.marginTop = 14;
            startButton.style.width = Length.Percent(100);
            startButton.clicked += Commit;
            panel.Add(startButton);

            return panel;
        }

        private VisualElement BuildOwnedPanel()
        {
            var panel = NewPanel("HOUSE FAMILIES");
            panel.Add(ownedList);
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

        private void RebuildSlots()
        {
            slotOptions.Clear();
            var labels = new List<string>();

            foreach (var slot in ArchitectureCatalog.CustomSlots)
            {
                slotOptions.Add(slot);
                labels.Add(simulation.State.CustomArchitectures.TryGetValue(slot, out var existing)
                    ? $"{SlotLetter(slot)} - overwrite {existing.DisplayName}"
                    : $"{SlotLetter(slot)} - empty");
            }

            slotField.choices = labels;
            if (slotField.index < 0)
            {
                var free = simulation.State.FirstFreeArchitectureSlot();
                slotField.index = free == ArchitectureId.None ? 0 : slotOptions.IndexOf(free);
            }
        }

        private void RebuildBases()
        {
            baseOptions.Clear();
            var labels = new List<string>();

            baseOptions.Add(ArchitectureId.None);
            labels.Add("Clean sheet");

            foreach (var pair in simulation.State.CustomArchitectures)
            {
                var generation = simulation.State.FamilyGeneration(pair.Key);
                baseOptions.Add(pair.Key);
                labels.Add($"{pair.Value.DisplayName} (gen {generation})");
            }

            baseField.choices = labels;
            if (baseField.index < 0 || baseField.index >= labels.Count)
            {
                baseField.index = 0;
            }
        }

        private void RebuildOwned()
        {
            ownedList.Clear();

            var project = simulation.State.ActiveArchitectureProject;
            if (project != null)
            {
                var running = new Label(
                    $"{project.Blueprint.Name} in progress: {UiFormat.Percent(project.Progress, 0)} "
                    + $"({project.DaysCompleted} of {project.DurationDays} days)");
                running.AddToClassList("readout__value");
                ownedList.Add(running);
            }

            if (simulation.State.CustomArchitectures.Count == 0)
            {
                var empty = new Label("No house families yet. Everything is running on published techniques.");
                empty.AddToClassList("field__hint");
                ownedList.Add(empty);
                return;
            }

            foreach (var pair in simulation.State.CustomArchitectures)
            {
                var definition = pair.Value;
                var row = new VisualElement();
                row.AddToClassList("readout");
                row.Add(new Label($"{definition.DisplayName} (gen {simulation.State.FamilyGeneration(pair.Key)})"));

                var stats = new Label(
                    $"active {UiFormat.Number(definition.ActiveParameterFraction, 3)}  "
                    + $"bonus {UiFormat.Number(definition.CapabilityBonus)}  "
                    + $"serve {UiFormat.Number(definition.InferenceCostMultiplier, 2)}x");
                stats.AddToClassList("readout__value");
                row.Add(stats);
                ownedList.Add(row);
            }
        }

        private ArchitectureBlueprint CurrentBlueprint()
        {
            var slot = slotOptions.Count > 0 && slotField.index >= 0
                ? slotOptions[Math.Clamp(slotField.index, 0, slotOptions.Count - 1)]
                : ArchitectureId.CustomFamilyA;

            var baseFamily = baseOptions.Count > 0 && baseField.index >= 0
                ? baseOptions[Math.Clamp(baseField.index, 0, baseOptions.Count - 1)]
                : ArchitectureId.None;

            return new ArchitectureBlueprint(
                nameField.value,
                slot,
                baseFamily,
                Weight(ResearchDirection.Sparsity),
                Weight(ResearchDirection.Throughput),
                Weight(ResearchDirection.Quality),
                Weight(ResearchDirection.Serving),
                Weight(ResearchDirection.Reasoning),
                (long)Math.Pow(10.0, budgetSlider.value),
                (int)durationSlider.value);
        }

        private double Weight(ResearchDirection direction) =>
            directions.TryGetValue(direction, out var slider) ? slider.value : 0.0;

        private void Reprice()
        {
            var blueprint = CurrentBlueprint();
            var projection = simulation.ProjectArchitecture(blueprint);

            budgetLabel.text = $"Research budget: {UiFormat.Money(blueprint.ResearchBudgetUsd)}"
                + (blueprint.IsIteration
                    ? $"  (pays {UiFormat.Money(ArchitectureDesigner.CashCostUsd(blueprint))})"
                    : string.Empty);
            durationLabel.text = $"Programme length: {UiFormat.Days(ArchitectureDesigner.DurationDays(blueprint))}";

            readouts.Clear();
            AddReadout("Research power", UiFormat.Percent(projection.ResearchPower / 1.2),
                projection.ResearchPower > 0.6 ? Tone.Good : Tone.Warn);
            AddReadout("Focus", UiFormat.Percent(blueprint.Focus),
                blueprint.Focus > 0.35 ? Tone.Good : Tone.Warn);
            AddReadout("Outcome spread", UiFormat.Percent(projection.Variance),
                projection.Variance < 0.2 ? Tone.Good : Tone.Bad);

            AddBand("Active parameter fraction",
                projection.Ceiling.ActiveParameterFraction,
                projection.Expected.ActiveParameterFraction,
                projection.Floor.ActiveParameterFraction, 3);
            AddBand("Quality per parameter",
                projection.Floor.ParameterEfficiency,
                projection.Expected.ParameterEfficiency,
                projection.Ceiling.ParameterEfficiency, 2);
            AddBand("Training efficiency",
                projection.Floor.TrainingEfficiency,
                projection.Expected.TrainingEfficiency,
                projection.Ceiling.TrainingEfficiency, 2);
            AddBand("Serving multiplier",
                projection.Ceiling.InferenceCostMultiplier,
                projection.Expected.InferenceCostMultiplier,
                projection.Floor.InferenceCostMultiplier, 2);
            AddBand("Capability bonus",
                projection.Floor.CapabilityBonus,
                projection.Expected.CapabilityBonus,
                projection.Ceiling.CapabilityBonus, 1);

            AddReadout("Compute it consumes", UiFormat.PetaflopDays(projection.PetaflopDaysRequired), Tone.Neutral);
            AddReadout("Cash it costs", UiFormat.Money(ArchitectureDesigner.CashCostUsd(blueprint)),
                ArchitectureDesigner.CashCostUsd(blueprint) > simulation.State.CashUsd ? Tone.Bad : Tone.Neutral);
            AddReadout("Compute saved against baseline",
                UiFormat.Percent(projection.ComputeSavingVersusBaseline),
                projection.ComputeSavingVersusBaseline > 0.2 ? Tone.Good : Tone.Warn);

            verdict.RemoveFromClassList("verdict--ok");
            verdict.RemoveFromClassList("verdict--blocked");

            if (projection.IsFeasible)
            {
                verdict.AddToClassList("verdict--ok");
                verdict.text = projection.Variance > 0.3
                    ? "Runnable, but underfunded enough that the result is close to a coin toss. More money or more time narrows it."
                    : blueprint.Focus < 0.2
                        ? "Runnable, but the effort is spread so evenly that no direction goes deep."
                        : "Runnable, and focused enough to land somewhere useful.";
            }
            else
            {
                verdict.AddToClassList("verdict--blocked");
                verdict.text = projection.BlockingReason;
            }

            var busy = simulation.State.ActiveArchitectureProject != null;
            startButton.SetEnabled(projection.IsFeasible && !busy);
            if (busy)
            {
                verdict.text = "A family programme is already running. One at a time.";
            }
        }

        private enum Tone
        {
            Neutral,
            Good,
            Warn,
            Bad
        }

        private void AddBand(string label, double worst, double expected, double best, int decimals)
        {
            AddReadout(
                label,
                $"{UiFormat.Number(worst, decimals)}  ..  {UiFormat.Number(expected, decimals)}  ..  {UiFormat.Number(best, decimals)}",
                Tone.Neutral);
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

        private void Commit()
        {
            if (!simulation.TryStartArchitectureProgramme(CurrentBlueprint(), out var reason))
            {
                verdict.RemoveFromClassList("verdict--ok");
                verdict.AddToClassList("verdict--blocked");
                verdict.text = reason;
                return;
            }

            Refresh();
        }

        private static string SlotLetter(ArchitectureId slot) => slot switch
        {
            ArchitectureId.CustomFamilyA => "Slot A",
            ArchitectureId.CustomFamilyB => "Slot B",
            ArchitectureId.CustomFamilyC => "Slot C",
            ArchitectureId.CustomFamilyD => "Slot D",
            ArchitectureId.CustomFamilyE => "Slot E",
            _ => "Slot F"
        };
    }
}
