using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The house architecture: what every later model in a family inherits from.
    ///
    /// **This was a form and it is a screen now.** The old version stacked a caption, a slider and a
    /// paragraph five times down a column, then answered with eleven rows of `label ... value` on
    /// the right, three of which read `0.918 .. 0.937 .. 0.955` and meant nothing at a glance. Every
    /// control looked exactly as important as every other one, the INVESTMENT panel fell below the
    /// fold, and the three text fields were the runtime theme's white slabs on a dark page.
    ///
    /// Three things changed and they are the whole design:
    ///
    /// **The directions are the screen.** Five rows, each a name, a reading, an "(i)" and one track.
    /// The track is the decision, so it is the widest thing on the page.
    ///
    /// **Most of every track is locked.** A two person company in 2022 could previously ask for a
    /// perfectly routed sparse mixture and simply chose not to; research opens the rest. The slider
    /// keeps its full range and the locked part is covered, the same arrangement the parameter
    /// slider already uses, because shrinking the range would rescale the control every time a node
    /// lands and the player would never see that the cap had moved.
    ///
    /// **The answer is a band, drawn.** A family programme does not have one outcome, it has a
    /// range, and the range narrows as the programme is better funded. Three numbers separated by
    /// dots said that and nobody read it. A bar with the expected value marked on it says it in one
    /// look, and the width of the bar *is* the risk.
    /// </summary>
    public sealed class ArchitectureCreatorPanel
    {
        /// <summary>About $2M to about $2B, logarithmic.</summary>
        public const float MinimumLogBudget = 6.3f;

        public const float MaximumLogBudget = 9.3f;

        private readonly CompanySimulation simulation;
        private readonly VisualElement root;

        private readonly TextField nameField = new();
        private readonly DropdownField slotField = new();
        private readonly DropdownField baseField = new();
        private readonly Slider budgetSlider = new(MinimumLogBudget, MaximumLogBudget);
        private readonly Slider durationSlider = new(
            ArchitectureBlueprint.MinimumDurationDays, ArchitectureBlueprint.MaximumDurationDays);

        private readonly VisualElement directionRows = new();
        private readonly VisualElement outcome = new();
        private readonly VisualElement ownedList = new();
        private readonly Label budgetReading = new();
        private readonly Label durationReading = new();

        private readonly Dictionary<ResearchDirection, Slider> directions = new();
        private readonly Dictionary<ResearchDirection, VisualElement> locks = new();
        private readonly Dictionary<ResearchDirection, Label> lockLabels = new();
        private readonly Dictionary<ResearchDirection, Label> readings = new();

        private readonly List<ArchitectureId> slotOptions = new();
        private readonly List<ArchitectureId> baseOptions = new();

        private bool abandonArmed;
        private string problem = string.Empty;

        /// <summary>Name, the note that explains it, and where the slider starts.</summary>
        /// <summary>
        /// The five directions, where each slider starts, and the note that explains it.
        ///
        /// **The name is a key rather than a word.** It is read when the row is built, so switching
        /// language rebuilds the screen into the other one; a string baked in here would leave five
        /// English headings on an otherwise Polish page, which is exactly how it first shipped.
        /// </summary>
        private static readonly (ResearchDirection Direction, string Key, Func<TechNotes.Note> Note,
            float Initial)[] Directions =
            {
                (ResearchDirection.Sparsity, "tech.sparsity.title", () => TechNotes.Sparsity, 0.35f),
                (ResearchDirection.Throughput, "tech.throughput.title", () => TechNotes.Throughput, 0.20f),
                (ResearchDirection.Quality, "tech.quality.title", () => TechNotes.Quality, 0.30f),
                (ResearchDirection.Serving, "tech.serving.title", () => TechNotes.Serving, 0.15f),
                (ResearchDirection.Reasoning, "tech.reasoning.title", () => TechNotes.Reasoning, 0.10f)
            };

        public ArchitectureCreatorPanel(CompanySimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

            root = new VisualElement();
            root.AddToClassList("content");
            root.AddToClassList("arx");

            Build();
        }

        public VisualElement Root => root;

        /// <summary>What the sliders currently say. Public so a test can commit without a panel.</summary>
        public ArchitectureBlueprint Blueprint => CurrentBlueprint();

        public void Refresh()
        {
            RebuildSlots();
            RebuildBases();
            RebuildCeilings();
            RebuildOwned();
            Reprice();
        }

        // ---- the frame ---------------------------------------------------------------------------

        private void Build()
        {
            var head = new VisualElement();
            head.AddToClassList("arx__head");

            var title = new Label(Loc.T("arch.title"));
            title.AddToClassList("arx__title");
            head.Add(title);

            var strap = new Label(Loc.T("arch.strap"));

            strap.AddToClassList("arx__strap");
            head.Add(strap);
            root.Add(head);

            var columns = new VisualElement();
            columns.AddToClassList("arx__columns");

            var left = new VisualElement();
            left.AddToClassList("arx__left");
            left.Add(BuildProgrammeCard());
            left.Add(BuildDirectionsCard());
            left.Add(BuildInvestmentCard());
            columns.Add(left);

            var right = new VisualElement();
            right.AddToClassList("arx__right");
            right.Add(outcome);
            right.Add(BuildOwnedCard());
            columns.Add(right);

            root.Add(columns);
        }

        private static VisualElement Card(string heading, VisualElement into = null)
        {
            var card = into ?? new VisualElement();
            card.AddToClassList("arx__card");

            var label = new Label(heading);
            label.AddToClassList("arx__cardhead");
            card.Add(label);

            return card;
        }

        private VisualElement BuildProgrammeCard()
        {
            var card = Card(Loc.T("arch.programme"));

            var row = new VisualElement();
            row.AddToClassList("arx__fields");

            nameField.value = "House family 1";
            nameField.AddToClassList("arx__name");
            nameField.RegisterValueChangedCallback(_ => Reprice());
            row.Add(Field(Loc.T("arch.family_name"), nameField));

            slotField.AddToClassList("arx__pick");
            slotField.RegisterValueChangedCallback(_ => Reprice());
            row.Add(Field(Loc.T("arch.slot"), slotField));

            baseField.AddToClassList("arx__pick");
            baseField.RegisterValueChangedCallback(_ => Reprice());
            row.Add(Field(Loc.T("arch.build_on"), baseField));

            card.Add(row);

            var hint = new Label(Loc.T("arch.iterate_hint"));

            hint.AddToClassList("arx__hint");
            card.Add(hint);

            return card;
        }

        private static VisualElement Field(string caption, VisualElement control)
        {
            var block = new VisualElement();
            block.AddToClassList("arx__field");

            var label = new Label(caption);
            label.AddToClassList("arx__fieldlabel");
            block.Add(label);
            block.Add(control);

            return block;
        }

        // ---- the five directions -------------------------------------------------------------------

        private VisualElement BuildDirectionsCard()
        {
            var card = Card(Loc.T("arch.directions"));

            var note = new Label(Loc.T("arch.directions_hint"));

            note.AddToClassList("arx__hint");
            card.Add(note);
            card.Add(directionRows);

            foreach (var (direction, key, note1, initial) in Directions)
            {
                directionRows.Add(BuildDirectionRow(direction, Loc.T(key), note1(), initial));
            }

            return card;
        }

        private VisualElement BuildDirectionRow(ResearchDirection direction, string label,
            TechNotes.Note note, float initial)
        {
            var row = new VisualElement();
            row.AddToClassList("drow");

            var head = new VisualElement();
            head.AddToClassList("drow__head");

            var name = new Label(label);
            name.AddToClassList("drow__name");
            head.Add(name);

            head.Add(InsightTip.InfoBadge(note.Title, Reading(note)));

            var spacer = new VisualElement();
            spacer.AddToClassList("drow__spacer");
            head.Add(spacer);

            var reading = new Label();
            reading.AddToClassList("drow__value");
            readings[direction] = reading;
            head.Add(reading);

            row.Add(head);

            // The track: the slider, with the part research has not opened covered over it.
            var track = new VisualElement();
            track.AddToClassList("drow__track");

            var slider = new Slider(0f, 1f) { value = initial };
            slider.AddToClassList("drow__slider");
            slider.RegisterValueChangedCallback(_ =>
            {
                ClampToCeiling(direction);
                Reprice();
            });

            directions[direction] = slider;
            track.Add(slider);

            var cover = new VisualElement();
            cover.AddToClassList("dlock");
            cover.pickingMode = PickingMode.Ignore;

            var coverLabel = new Label();
            coverLabel.AddToClassList("dlock__label");
            cover.Add(coverLabel);

            locks[direction] = cover;
            lockLabels[direction] = coverLabel;
            track.Add(cover);

            row.Add(track);
            return row;
        }

        private static InsightTip.Reading Reading(TechNotes.Note note) =>
            new(note.What, note.Affects, note.High, note.Low);

        /// <summary>
        /// Holds a slider under what research has opened, and says what would open more.
        ///
        /// The value is clamped rather than the range shrunk. Moving `highValue` down would rescale
        /// the whole control every time a node lands, so a drag that used to mean one thing would
        /// mean another and the player would never see that the cap had moved at all.
        /// </summary>
        private void RebuildCeilings()
        {
            foreach (var (direction, _, _, _) in Directions)
            {
                var ceiling = ArchitectureCeiling.FractionFor(direction, simulation.State.HasResearch);
                ClampToCeiling(direction);

                var locked = 1.0 - ceiling;
                var cover = locks[direction];

                cover.style.display = locked <= 0.0005 ? DisplayStyle.None : DisplayStyle.Flex;
                cover.style.width = Length.Percent((float)(locked * 100.0));

                if (locked <= 0.0005)
                {
                    continue;
                }

                lockLabels[direction].text = ArchitectureCeiling.TryNextRung(
                    direction, simulation.State.HasResearch, out var rung, out var next)
                        ? $"{ResearchTree.Get(rung).DisplayName.ToUpperInvariant()}  ·  {next:P0}"
                        : Loc.T("common.locked");
            }
        }

        /// <summary>
        /// Sets the five directions to something defensible, up to what the company has researched.
        ///
        /// **Not the optimum, because there is no optimum.** The five directions buy different
        /// things and which one is worth most depends on what the company is selling, so a button
        /// claiming to compute the best answer would be lying about the whole screen. What this does
        /// is what a friend does: a shape that is not wrong, weighted toward the two directions that
        /// pay in every strategy, and clamped to what has actually been unlocked.
        ///
        /// One button, and pressing it leaves every slider live. The player can move all five
        /// afterwards, which is the difference between advice and an autopilot.
        /// </summary>
        public void TakeTheAdvice()
        {
            // Cheap to run and good for a family is where a first programme should sit. Reasoning is
            // the one that cannot be bought with scale later, so it gets the third share.
            var wanted = new Dictionary<ResearchDirection, float>
            {
                [ResearchDirection.Sparsity] = 0.30f,
                [ResearchDirection.Quality] = 0.26f,
                [ResearchDirection.Reasoning] = 0.20f,
                [ResearchDirection.Throughput] = 0.14f,
                [ResearchDirection.Serving] = 0.10f
            };

            foreach (var (direction, share) in wanted)
            {
                if (!directions.TryGetValue(direction, out var slider))
                {
                    continue;
                }

                var ceiling = (float)ArchitectureCeiling.FractionFor(
                    direction, simulation.State.HasResearch);

                slider.value = Math.Min(share, ceiling);
            }

            Refresh();
        }

        private void ClampToCeiling(ResearchDirection direction)
        {
            var ceiling = (float)ArchitectureCeiling.FractionFor(
                direction, simulation.State.HasResearch);

            if (directions[direction].value > ceiling)
            {
                directions[direction].SetValueWithoutNotify(ceiling);
            }
        }

        // ---- money and calendar ----------------------------------------------------------------------

        private VisualElement BuildInvestmentCard()
        {
            var card = Card(Loc.T("arch.investment"));

            var pair = new VisualElement();
            pair.AddToClassList("arx__fields");

            budgetSlider.value = 7.0f;
            budgetSlider.AddToClassList("arx__slider");
            budgetSlider.RegisterValueChangedCallback(_ => Reprice());
            pair.Add(Money(Loc.T("arch.budget"), TechNotes.ResearchBudget, budgetReading, budgetSlider));

            durationSlider.value = 365f;
            durationSlider.AddToClassList("arx__slider");
            durationSlider.RegisterValueChangedCallback(_ => Reprice());
            pair.Add(Money(Loc.T("arch.length"), TechNotes.ProgrammeLength, durationReading, durationSlider));

            card.Add(pair);

            var hint = new Label(Loc.T("arch.investment_hint"));

            hint.AddToClassList("arx__hint");
            card.Add(hint);

            return card;
        }

        private static VisualElement Money(string caption, TechNotes.Note note, Label reading,
            Slider slider)
        {
            var block = new VisualElement();
            block.AddToClassList("drow");
            block.AddToClassList("arx__field");

            var head = new VisualElement();
            head.AddToClassList("drow__head");

            var label = new Label(caption);
            label.AddToClassList("drow__name");
            head.Add(label);

            head.Add(InsightTip.InfoBadge(note.Title, Reading(note)));

            var spacer = new VisualElement();
            spacer.AddToClassList("drow__spacer");
            head.Add(spacer);

            reading.AddToClassList("drow__value");
            head.Add(reading);

            block.Add(head);
            block.Add(slider);

            return block;
        }

        // ---- the answer ---------------------------------------------------------------------------

        private VisualElement BuildOwnedCard()
        {
            var card = Card(Loc.T("arch.families"));
            card.Add(ownedList);
            return card;
        }

        private void Reprice()
        {
            var blueprint = CurrentBlueprint();
            var projection = simulation.ProjectArchitecture(blueprint);

            // **Both numbers, because one of them is meaningless alone.**
            //
            // The setting is what the slider says. The share is what the programme will actually
            // spend there, and it is the one that decides the outcome: effort is normalised, so
            // pushing one direction up takes from every other one whether the player meant it or
            // not. Showing only the setting hid the entire trade this screen is about.
            var effort = 0.0;

            foreach (var (direction, _, _, _) in Directions)
            {
                effort += directions[direction].value;
            }

            foreach (var (direction, _, _, _) in Directions)
            {
                var value = directions[direction].value;
                var share = effort <= 0.0 ? 0.0 : value / effort;

                readings[direction].text = effort <= 0.0
                    ? $"{value:P0}"
                    : Loc.T("arch.setting_and_share", $"{value:P0}", $"{share:P0}");
            }

            budgetReading.text = UiFormat.Money(blueprint.ResearchBudgetUsd)
                + (blueprint.IsIteration
                    ? $"  ·  pays {UiFormat.Money(ArchitectureDesigner.CashCostUsd(blueprint))}"
                    : string.Empty);

            durationReading.text = UiFormat.Days(ArchitectureDesigner.DurationDays(blueprint));

            outcome.Clear();
            Card(Loc.T("arch.outcome"), outcome);
            outcome.AddToClassList("arx__outcome");

            // The three numbers that say how good a bet this is, before any of the numbers that say
            // what it would produce. A player deciding whether to commit a year reads these first.
            var gauges = new VisualElement();
            gauges.AddToClassList("arx__gauges");
            gauges.Add(Gauge(Loc.T("arch.research_power"), projection.ResearchPower / 1.2,
                UiFormat.Percent(projection.ResearchPower / 1.2), "#7ED89E"));

            gauges.Add(Gauge(Loc.T("arch.focus"), blueprint.Focus, UiFormat.Percent(blueprint.Focus), "#5B8DEF"));

            // Drawn as "how sure is this", so full means certain. Spread is the opposite of that.
            gauges.Add(Gauge(Loc.T("arch.certainty"), 1.0 - projection.Variance,
                UiFormat.Percent(1.0 - projection.Variance), "#E0B83C"));

            outcome.Add(gauges);

            var what = new Label(Loc.T("arch.would_produce"));
            what.AddToClassList("arx__subhead");
            outcome.Add(what);

            // One sentence naming the thing, before five bars describing it. A player who reads
            // nothing else on this panel should still come away knowing what they are commissioning.
            var shape = new Label(DescribeFamily(blueprint));
            shape.AddToClassList("arx__shape");
            outcome.Add(shape);

            // Everything is stated against the dense transformer, which is what a company with no
            // house family of its own is running. A bare 0.953 is unreadable; "5% cheaper to run
            // than today" is the same number and needs no explanation.
            var today = ArchitectureCatalog.Baseline;

            outcome.Add(Band(Loc.T("arch.active_parameters"),
                projection.Ceiling.ActiveParameterFraction,
                projection.Expected.ActiveParameterFraction,
                projection.Floor.ActiveParameterFraction, 3, lowerIsBetter: true,
                versus: Against(projection.Expected.ActiveParameterFraction,
                    today.ActiveParameterFraction, lowerIsBetter: true, "arch.cheaper_to_run")));

            outcome.Add(Band(Loc.T("arch.quality_per_parameter"),
                projection.Floor.ParameterEfficiency,
                projection.Expected.ParameterEfficiency,
                projection.Ceiling.ParameterEfficiency, 2,
                versus: Against(projection.Expected.ParameterEfficiency,
                    today.ParameterEfficiency, lowerIsBetter: false, "arch.more_from_each")));

            outcome.Add(Band(Loc.T("arch.training_efficiency"),
                projection.Floor.TrainingEfficiency,
                projection.Expected.TrainingEfficiency,
                projection.Ceiling.TrainingEfficiency, 2,
                versus: Against(projection.Expected.TrainingEfficiency,
                    today.TrainingEfficiency, lowerIsBetter: false, "arch.faster_runs")));

            outcome.Add(Band(Loc.T("arch.serving_multiplier"),
                projection.Ceiling.InferenceCostMultiplier,
                projection.Expected.InferenceCostMultiplier,
                projection.Floor.InferenceCostMultiplier, 2, lowerIsBetter: true,
                versus: Against(projection.Expected.InferenceCostMultiplier,
                    today.InferenceCostMultiplier, lowerIsBetter: true, "arch.cheaper_tokens")));

            outcome.Add(Band(Loc.T("arch.capability_bonus"),
                projection.Floor.CapabilityBonus,
                projection.Expected.CapabilityBonus,
                projection.Ceiling.CapabilityBonus, 1,
                versus: projection.Expected.CapabilityBonus - today.CapabilityBonus < 0.05
                    ? Loc.T("arch.no_better")
                    : Loc.T("arch.points_better",
                        UiFormat.Number(projection.Expected.CapabilityBonus - today.CapabilityBonus, 1))));

            var bill = new VisualElement();
            bill.AddToClassList("arx__bill");

            var cash = ArchitectureDesigner.CashCostUsd(blueprint);
            bill.Add(Cell(Loc.T("arch.cash"), UiFormat.Money(cash), cash > simulation.State.CashUsd));
            bill.Add(Cell(Loc.T("arch.compute"), UiFormat.PetaflopDays(projection.PetaflopDaysRequired), false));
            bill.Add(Cell(Loc.T("arch.saves"), UiFormat.Percent(projection.ComputeSavingVersusBaseline), false));
            outcome.Add(bill);

            outcome.Add(BuildVerdict(blueprint, projection));
        }

        /// <summary>
        /// What this programme is aiming at, said as a kind of thing rather than as five numbers.
        ///
        /// **Named from the direction the effort actually goes into**, using the normalised share
        /// rather than the raw setting, because that is what the simulation spends. A programme with
        /// no direction above a third of the budget is not aiming at anything, and it says so.
        /// </summary>
        private string DescribeFamily(ArchitectureBlueprint blueprint)
        {
            var leading = ResearchDirection.Sparsity;
            var best = 0.0;

            foreach (var (direction, _, _, _) in Directions)
            {
                var share = blueprint.NormalizedWeight(direction);

                if (share > best)
                {
                    best = share;
                    leading = direction;
                }
            }

            // Written out rather than built from the enum name. A key assembled by concatenation is
            // invisible to the guard that checks every key exists, which is the whole reason that
            // guard is there: it can only see literals.
            if (best < 0.34)
            {
                return Loc.T("arch.shape.none");
            }

            return leading switch
            {
                ResearchDirection.Sparsity => Loc.T("arch.shape.sparsity"),
                ResearchDirection.Throughput => Loc.T("arch.shape.throughput"),
                ResearchDirection.Quality => Loc.T("arch.shape.quality"),
                ResearchDirection.Serving => Loc.T("arch.shape.serving"),
                _ => Loc.T("arch.shape.reasoning")
            };
        }

        private VisualElement BuildVerdict(ArchitectureBlueprint blueprint,
            ArchitectureProjection projection)
        {
            var block = new VisualElement();

            var busy = simulation.State.ActiveArchitectureProject != null;
            var within = ArchitectureCeiling.IsWithinCeiling(
                blueprint.Weight, simulation.State.HasResearch, out _);

            var verdict = new Label();
            verdict.AddToClassList("arx__verdict");

            var runnable = projection.IsFeasible && !busy && within;

            if (busy)
            {
                verdict.text = Loc.T("arch.verdict.busy");
            }
            else if (!projection.IsFeasible)
            {
                verdict.text = projection.BlockingReason;
            }
            else if (projection.Variance > 0.3)
            {
                verdict.text = Loc.T("arch.verdict.coin_toss");
            }
            else if (blueprint.Focus < 0.2)
            {
                verdict.text = Loc.T("arch.verdict.spread");
            }
            else
            {
                verdict.text = Loc.T("arch.verdict.good");
            }

            verdict.EnableInClassList("arx__verdict--ok", runnable);
            verdict.EnableInClassList("arx__verdict--blocked", !runnable);
            block.Add(verdict);

            if (!string.IsNullOrEmpty(problem))
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("arx__problem");
                block.Add(trouble);
            }

            var commit = new Button(Commit) { text = Loc.T("arch.commit") };
            commit.AddToClassList("arx__commit");
            commit.SetEnabled(runnable);
            block.Add(commit);

            return block;
        }

        /// <summary>
        /// One outcome, drawn as the range it actually is.
        ///
        /// **The width of the bar is the risk and that is the point.** The old screen printed three
        /// numbers separated by dots, which is the same information and reads as one long number.
        /// A player comparing a cheap programme against an expensive one wants to see the band
        /// narrow, and nothing in a row of digits shows that.
        /// </summary>
        /// <summary>
        /// One reading against what the company runs on today, in words.
        ///
        /// Returns an empty string when the difference is not worth a sentence, so a family that
        /// changes nothing says nothing rather than claiming a 0.2% improvement.
        /// </summary>
        private static string Against(double expected, double baseline, bool lowerIsBetter, string key)
        {
            if (baseline <= 0.0)
            {
                return string.Empty;
            }

            var change = lowerIsBetter
                ? 1.0 - expected / baseline
                : expected / baseline - 1.0;

            return change < 0.005 ? Loc.T("arch.no_better") : Loc.T(key, $"{change:P0}");
        }

        private static VisualElement Band(string caption, double worst, double expected, double best,
            int decimals, bool lowerIsBetter = false, string versus = null)
        {
            var row = new VisualElement();
            row.AddToClassList("aband");

            var head = new VisualElement();
            head.AddToClassList("aband__head");

            var label = new Label(caption);
            label.AddToClassList("aband__caption");
            head.Add(label);

            var value = new Label(UiFormat.Number(expected, decimals));
            value.AddToClassList("aband__value");
            head.Add(value);

            row.Add(head);

            var track = new VisualElement();
            track.AddToClassList("aband__track");

            var low = Math.Min(worst, best);
            var high = Math.Max(worst, best);

            // **The track is a fixed window around the expected value, not the band itself.**
            // Scaling the track to the band made every bar full width, so a certain programme and
            // a coin toss drew identically and the one thing this is for was invisible. A quarter
            // either side of expected means the width of the fill *is* the uncertainty.
            var window = Math.Max(Math.Abs(expected) * 0.25, 1e-6);
            var from = expected - window;
            var span = window * 2.0;

            double Place(double value) => Math.Clamp((value - from) / span, 0.0, 1.0);

            var left = Place(low);
            var right = Place(high);

            var fill = new VisualElement();
            fill.AddToClassList("aband__fill");
            fill.EnableInClassList("aband__fill--inverted", lowerIsBetter);
            fill.style.left = Length.Percent((float)(left * 100.0));
            fill.style.width = Length.Percent((float)Math.Max((right - left) * 100.0, 1.5));
            track.Add(fill);

            var tick = new VisualElement();
            tick.AddToClassList("aband__tick");
            tick.style.left = Length.Percent((float)(Place(expected) * 100.0));

            track.Add(tick);
            row.Add(track);

            var ends = new VisualElement();
            ends.AddToClassList("aband__ends");

            var worstLabel = new Label(UiFormat.Number(worst, decimals));
            worstLabel.AddToClassList("aband__end");
            ends.Add(worstLabel);

            var bestLabel = new Label(UiFormat.Number(best, decimals));
            bestLabel.AddToClassList("aband__end");
            ends.Add(bestLabel);

            row.Add(ends);

            if (!string.IsNullOrEmpty(versus))
            {
                var meaning = new Label(versus);
                meaning.AddToClassList("aband__versus");
                row.Add(meaning);
            }

            return row;
        }

        private static VisualElement Gauge(string caption, double fill, string reading, string tint)
        {
            var block = new VisualElement();
            block.AddToClassList("agauge");

            var value = new Label(reading);
            value.AddToClassList("agauge__value");
            block.Add(value);

            var label = new Label(caption);
            label.AddToClassList("agauge__caption");
            block.Add(label);

            var track = new VisualElement();
            track.AddToClassList("agauge__track");

            var bar = new VisualElement();
            bar.AddToClassList("agauge__fill");
            bar.style.width = Length.Percent((float)(Math.Clamp(fill, 0.0, 1.0) * 100.0));

            if (ColorUtility.TryParseHtmlString(tint, out var colour))
            {
                bar.style.backgroundColor = colour;
            }

            track.Add(bar);
            block.Add(track);

            return block;
        }

        private static VisualElement Cell(string caption, string value, bool bad)
        {
            var cell = new VisualElement();
            cell.AddToClassList("arx__cell");

            var label = new Label(caption);
            label.AddToClassList("arx__cellcaption");
            cell.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("arx__cellvalue");
            reading.EnableInClassList("arx__cellvalue--bad", bad);
            cell.Add(reading);

            return cell;
        }

        // ---- what the company already has ---------------------------------------------------------

        private void RebuildOwned()
        {
            ownedList.Clear();

            var project = simulation.State.ActiveArchitectureProject;

            if (project != null)
            {
                var running = new VisualElement();
                running.AddToClassList("afam");
                running.AddToClassList("afam--running");

                var name = new Label(project.Blueprint.Name);
                name.AddToClassList("afam__name");
                running.Add(name);

                var progress = new Label(
                    $"{UiFormat.Percent(project.Progress, 0)}  ·  day {project.DaysCompleted} "
                    + $"of {project.DurationDays}");

                progress.AddToClassList("afam__stats");
                running.Add(progress);

                var track = new VisualElement();
                track.AddToClassList("afam__track");

                var fill = new VisualElement();
                fill.AddToClassList("afam__fill");
                fill.style.width = Length.Percent((float)(Math.Clamp(project.Progress, 0.0, 1.0) * 100.0));
                track.Add(fill);
                running.Add(track);

                // A family programme is months long and blocks the next one, so there has to be a
                // way out. Armed before it fires: nothing comes back.
                var stop = new Button(() =>
                {
                    if (abandonArmed)
                    {
                        abandonArmed = false;
                        simulation.CancelArchitectureProgramme();
                    }
                    else
                    {
                        abandonArmed = true;
                    }

                    Refresh();
                })
                {
                    text = Loc.T(abandonArmed ? "arch.abandon_confirm" : "arch.abandon")
                };

                stop.AddToClassList("afam__stop");
                stop.EnableInClassList("afam__stop--armed", abandonArmed);
                running.Add(stop);

                ownedList.Add(running);
            }

            if (simulation.State.CustomArchitectures.Count == 0)
            {
                var empty = new Label(Loc.T("arch.no_families"));

                empty.AddToClassList("arx__hint");
                ownedList.Add(empty);
                return;
            }

            foreach (var pair in simulation.State.CustomArchitectures)
            {
                var definition = pair.Value;

                var row = new VisualElement();
                row.AddToClassList("afam");

                var name = new Label(
                    $"{definition.DisplayName}   ·   gen {simulation.State.FamilyGeneration(pair.Key)}");

                name.AddToClassList("afam__name");
                row.Add(name);

                var stats = new Label(
                    $"active {UiFormat.Number(definition.ActiveParameterFraction, 3)}     "
                    + $"bonus {UiFormat.Number(definition.CapabilityBonus)}     "
                    + $"serve {UiFormat.Number(definition.InferenceCostMultiplier, 2)}x");

                stats.AddToClassList("afam__stats");
                row.Add(stats);
                ownedList.Add(row);
            }
        }

        // ---- the pickers -----------------------------------------------------------------------------

        private void RebuildSlots()
        {
            slotOptions.Clear();
            var labels = new List<string>();

            foreach (var slot in ArchitectureCatalog.CustomSlots)
            {
                slotOptions.Add(slot);
                labels.Add(simulation.State.CustomArchitectures.TryGetValue(slot, out var existing)
                    ? Loc.T("arch.slot_overwrite", SlotLetter(slot), existing.DisplayName)
                    : Loc.T("arch.slot_empty", SlotLetter(slot)));
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
            var labels = new List<string> { Loc.T("arch.clean_sheet") };
            baseOptions.Add(ArchitectureId.None);

            foreach (var pair in simulation.State.CustomArchitectures)
            {
                baseOptions.Add(pair.Key);
                labels.Add($"{pair.Value.DisplayName} (gen {simulation.State.FamilyGeneration(pair.Key)})");
            }

            baseField.choices = labels;

            if (baseField.index < 0 || baseField.index >= labels.Count)
            {
                baseField.index = 0;
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

        private void Commit()
        {
            if (!simulation.TryStartArchitectureProgramme(CurrentBlueprint(), out var reason))
            {
                problem = reason;
                Reprice();
                return;
            }

            problem = string.Empty;
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
