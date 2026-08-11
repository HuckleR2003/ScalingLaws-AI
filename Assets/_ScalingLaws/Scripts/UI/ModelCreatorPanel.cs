using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
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

        private readonly VisualElement stageRail = new();
        private readonly VisualElement effectBanner = new();
        private readonly VisualElement stageHost = new();
        private readonly Button backButton = new();
        private readonly Button nextButton = new();

        private int stage;
        private double previousCapability;
        private bool commercialise;
        private readonly DemographicPanel demographics = new();
        private int dots;
        private Label laptopName;
        private Label laptopStatus;
        private Label laptopArchitecture;
        private readonly ScaleBelt belt = new();
        private readonly Label tokenBytesLabel = new();
        private readonly Label memoryLabel = new();
        private Label beltRatio;
        private Label beltProfile;
        private VisualElement scaleReadout;
        private VisualElement scaleNotes;
        private Label laptopType;
        private ModelType chosenType = ModelType.General;

        /// <summary>
        /// Keeps the laptop reading what the player has actually chosen. Called from the same place
        /// the projection is repriced, so the screen cannot drift from the blueprint behind it.
        /// </summary>
        private void RefreshLaptopConsole()
        {
            if (laptopArchitecture == null)
            {
                return;
            }

            var architecture = CurrentBlueprint().Architecture;
            laptopArchitecture.text = $"> {simulation.State.ResolveArchitecture(architecture).DisplayName}";
            laptopType.text = $"> {ModelTypeCatalog.Get(chosenType).DisplayName}";
        }

        /// <summary>
        /// The run is defined by four decisions and reviewed as a fifth. Showing all of them at once
        /// is honest and unreadable: a player who has not internalised the scaling law cannot tell
        /// which of eleven controls moved the number. One decision at a time can afford to explain
        /// itself, which is how Devices Tycoon and Smartphone Tycoon both handle a build.
        /// </summary>
        private static readonly string[] StageNames =
            { "FOUNDATION", "SCALE", "DATA", "COMPUTE", "REVIEW", "AFTER THE RUN" };

        private static readonly string[] StageBlurbs =
        {
            "What the model is built on, and what it is called. The family sets the ceiling for "
            + "everything chosen after it.",
            "How big it is, and how much it reads. This single trade decides most of the result.",
            "What it learns from. The run draws from the best corpus first, so one good archive "
            + "lifts the whole mix.",
            "How much throughput to rent. This buys time and never quality, which is the point.",
            "What the run is projected to produce, and what it costs to find out.",
            "What happens the day it finishes. This can be changed later, and the market will have "
            + "moved by then."
        };

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
            // Title and stage rail share a line. Two full width rows of chrome above a creator that
            // must not scroll is height spent on saying where you are rather than on the decision.
            var header = new VisualElement();
            header.AddToClassList("stage-header");

            var title = new Label("NEW MODEL");
            title.AddToClassList("page-title");
            title.AddToClassList("stage-header__title");
            header.Add(title);

            stageRail.AddToClassList("stage-rail");
            header.Add(stageRail);
            root.Add(header);

            effectBanner.AddToClassList("effect-banner");
            root.Add(effectBanner);

            stageHost.AddToClassList("stage-host");
            root.Add(stageHost);

            var footer = new VisualElement();
            footer.AddToClassList("stage-footer");

            backButton.text = "BACK";
            backButton.AddToClassList("menu-button");
            backButton.AddToClassList("menu-button--quiet");
            backButton.style.width = 130;
            backButton.clicked += () => GoTo(stage - 1);
            footer.Add(backButton);

            nextButton.AddToClassList("menu-button");
            nextButton.AddToClassList("menu-button--primary");
            nextButton.style.width = 230;
            nextButton.style.marginLeft = 10;
            nextButton.clicked += OnNext;
            footer.Add(nextButton);

            root.Add(footer);

            // Subscribed once. The panels are rebuilt whenever the stage changes, so a subscription
            // made inside one of them would fire as many times as the player had visited it.
            startButton.clicked += StartTraining;

            ShowStage();
        }

        private void OnNext()
        {
            if (stage >= StageNames.Length - 1)
            {
                StartTraining();
                return;
            }

            GoTo(stage + 1);
        }

        private void GoTo(int target)
        {
            var clamped = Math.Clamp(target, 0, StageNames.Length - 1);
            if (clamped == stage)
            {
                return;
            }

            stage = clamped;
            ShowStage();

            // The same diagonal arrival the screens use, so moving through the creator belongs to
            // the rest of the interface rather than reading as a form redrawing itself.
            stageHost.AddToClassList("stage-host--entering");
            stageHost.schedule.Execute(() => stageHost.RemoveFromClassList("stage-host--entering"))
                .ExecuteLater(16);
        }

        /// <summary>
        /// Puts the current stage in front. The controls are shared instances rather than copies, so
        /// adding one to a new stage moves it: there is exactly one parameter slider in existence and
        /// no way for a second copy to drift out of step with the blueprint.
        /// </summary>
        private void ShowStage()
        {
            stageRail.Clear();
            for (var index = 0; index < StageNames.Length; index++)
            {
                var step = index;
                var pip = new Button(() => GoTo(step));
                pip.AddToClassList("stage-pip");
                pip.EnableInClassList("stage-pip--on", index == stage);
                pip.EnableInClassList("stage-pip--done", index < stage);

                var number = new Label((index + 1).ToString());
                number.AddToClassList("stage-pip__number");
                pip.Add(number);

                var name = new Label(StageNames[index]);
                name.AddToClassList("stage-pip__name");
                pip.Add(name);

                stageRail.Add(pip);
            }

            stageHost.Clear();

            var blurb = new Label(StageBlurbs[stage]);
            blurb.AddToClassList("stage__blurb");
            stageHost.Add(blurb);

            stageHost.Add(stage switch
            {
                0 => WithArt("newmodel_1", BuildFoundationColumn(), BuildLaptopScreen()),
                1 => WithArt("newmodel_2", BuildShapePanel()),
                2 => BuildDataPanel(),
                3 => BuildComputePanel(),
                4 => BuildProjectionPanel(),
                _ => BuildDeployStage()
            });

            backButton.SetEnabled(stage > 0);
            nextButton.text = stage >= StageNames.Length - 1 ? "START TRAINING" : "NEXT";

            Reprice();
        }

        /// <summary>
        /// Art on the left, the decision on the right.
        ///
        /// The picture is not decoration: it is what stops the creator reading as a settings dialog.
        /// It is given a fixed share of the width rather than its own size, so a taller or shorter
        /// photograph changes nothing about where the controls sit. A missing file collapses the
        /// column and the controls take the whole width, which is the same failure rule the page
        /// banners use.
        /// </summary>
        private static VisualElement WithArt(string artName, VisualElement body, VisualElement overlay = null)
        {
            var row = new VisualElement();
            row.AddToClassList("stage-split");

            var texture = PageArt.Page(artName);
            if (texture != null)
            {
                var art = new VisualElement();
                art.AddToClassList("stage-art");
                art.style.backgroundImage = new StyleBackground(texture);

                if (overlay != null)
                {
                    art.Add(overlay);
                }

                row.Add(art);
            }

            var column = new VisualElement();
            column.AddToClassList("stage-split__body");
            column.Add(body);
            row.Add(column);

            return row;
        }

        /// <summary>
        /// What is written on the laptop in the first stage: the model's name, and a line under it
        /// saying it has not been built yet.
        ///
        /// It is a real element positioned over the picture rather than baked into the art, so the
        /// name updates as it is typed and the art stays one file with nothing written on it.
        /// </summary>
        private VisualElement BuildLaptopScreen()
        {
            var screen = new VisualElement();
            screen.AddToClassList("laptop-screen");

            laptopName = new Label(DisplayName());
            laptopName.AddToClassList("laptop-screen__name");
            screen.Add(laptopName);

            var rule = new VisualElement();
            rule.AddToClassList("laptop-screen__rule");
            screen.Add(rule);

            laptopStatus = new Label("PREPARING");
            laptopStatus.AddToClassList("laptop-screen__status");
            screen.Add(laptopStatus);

            // Two lines along the bottom of the laptop, written in the terminal voice. It costs
            // nothing and it turns a stock photograph into a machine that is being configured.
            var console = new VisualElement();
            console.AddToClassList("laptop-console");

            laptopArchitecture = new Label();
            laptopArchitecture.AddToClassList("laptop-console__line");
            console.Add(laptopArchitecture);

            laptopType = new Label();
            laptopType.AddToClassList("laptop-console__line");
            laptopType.AddToClassList("laptop-console__line--type");
            console.Add(laptopType);

            screen.Add(console);
            RefreshLaptopConsole();

            // Three dots that fill and clear. Cheap, and it is the difference between a still and a
            // machine that is doing something.
            screen.schedule.Execute(() =>
            {
                if (laptopStatus == null || laptopStatus.panel == null)
                {
                    return;
                }

                dots = (dots + 1) % 4;
                laptopStatus.text = "PREPARING" + new string('.', dots);
            }).Every(420);

            return screen;
        }

        private string DisplayName() =>
            string.IsNullOrWhiteSpace(nameField.value) ? "UNTITLED" : nameField.value.ToUpperInvariant();

        /// <summary>
        /// The first stage: who the model is, plus the two decisions that are designed and not yet
        /// built. They are shown rather than hidden because a player deciding what to train should
        /// be able to see that a series and a type exist before they can use either.
        /// </summary>
        private VisualElement BuildFoundationColumn()
        {
            var column = new VisualElement();
            column.Add(BuildIdentityPanel());

            var series = NewPanel("SERIES / MODEL UPGRADE RELEASE");
            series.Add(ComingRow("Model family", "Needs research"));
            series.Add(ComingRow("Type", ModelTypeCatalog.Get(chosenType).DisplayName));
            column.Add(series);

            // The market, right under the decision it informs. Half the gap the panels above use,
            // because this is the evidence for that choice rather than a separate subject.
            demographics.Show(simulation.MarketByType());
            demographics.Root.AddToClassList("demographics--tight");
            column.Add(demographics.Root);

            return column;
        }

        /// <summary>A row for a decision that exists in the design and not yet in the game.</summary>
        private static VisualElement ComingRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("coming-row");

            var name = new Label(label);
            name.AddToClassList("coming-row__label");
            row.Add(name);

            var amount = new Label(value);
            amount.AddToClassList("coming-row__value");
            row.Add(amount);

            var tag = new Label("INCOMING");
            tag.AddToClassList("coming-row__tag");
            row.Add(tag);

            return row;
        }

        /// <summary>
        /// The last stage: what happens the day the run finishes.
        ///
        /// Two tiles rather than a dropdown, because this is the decision that turns a research
        /// project into a business and it should not look like a setting. Keeping it local is a real
        /// option and not a delay: the model can be trained on, upgraded and released whenever, and
        /// the whole cost of waiting is that the frontier moves while you do.
        ///
        /// Everything on the commercial side writes straight into
        /// <see cref="MonetizationPolicy"/>, so nothing here is a promise the simulation has not
        /// already agreed to.
        /// </summary>
        private VisualElement BuildDeployStage()
        {
            var page = new VisualElement();

            var tiles = new VisualElement();
            tiles.AddToClassList("deploy-tiles");
            tiles.Add(DeployTile("KEEP IT LOCAL",
                "It finishes onto the shelf and stays there. Train on it, upgrade it, release it the "
                + "day the market looks right. Holding costs nothing but time.",
                !commercialise, () => { commercialise = false; Reprice(); }));

            tiles.Add(DeployTile("COMMERCIALISE",
                "It ships the day it finishes. You decide now what a free account gets and what a "
                + "paid one costs, and both of those decide how many people ever try it.",
                commercialise, () => { commercialise = true; Reprice(); }));

            page.Add(tiles);

            if (!commercialise)
            {
                var note = new Label(
                    "Nothing else to set. The release screen will be waiting when you want it.");
                note.AddToClassList("field__hint");
                page.Add(note);
                return page;
            }

            var policy = simulation.State.Monetization;
            var market = simulation.Market;

            var free = NewPanel("FREE TIER");

            var freeRow = new VisualElement();
            freeRow.AddToClassList("deploy-row");

            var noFree = new Button(() =>
            {
                policy.Model = PricingModel.Subscription;
                policy.FreeTierTokensPerUserPerDay = 0.0;
                Reprice();
            })
            { text = "NO FREE ACCESS" };
            noFree.AddToClassList("chip");
            noFree.EnableInClassList("chip--on", policy.FreeTierTokensPerUserPerDay <= 0.0);
            freeRow.Add(noFree);

            var freeOnly = new Button(() =>
            {
                policy.Model = PricingModel.FreeOnly;
                Reprice();
            })
            { text = "FREE ONLY" };
            freeOnly.AddToClassList("chip");
            freeOnly.EnableInClassList("chip--on", policy.Model == PricingModel.FreeOnly);
            freeRow.Add(freeOnly);

            free.Add(freeRow);

            free.Add(BuildSlider("TOKENS A DAY, FREE ACCOUNT",
                UiFormat.Count(policy.FreeTierTokensPerUserPerDay),
                0f, (float)(MonetizationCatalog.GenerousFreeTierTokensPerDay * 1.5),
                (float)policy.FreeTierTokensPerUserPerDay,
                value =>
                {
                    policy.FreeTierTokensPerUserPerDay = value;
                    if (value > 0.0 && policy.Model == PricingModel.Subscription)
                    {
                        policy.Model = PricingModel.Subscription;
                    }

                    Reprice();
                }));

            // What a free account costs the company, in dollars, at today's token price. This is the
            // trap the design wants visible: generosity is bought reach and it is billed daily.
            var costPerFreeUserMonth = policy.FreeTierTokensPerUserPerDay / 1_000_000.0
                * market.PricePerMillionTokensUsd * 30.0;

            free.Add(LoadBar("REACH BOUGHT",
                (policy.ReachMultiplier - 1.0) / MonetizationCatalog.FreeTierReachBonus,
                $"A free account costs you about {UiFormat.Money((long)Math.Round(costPerFreeUserMonth))} "
                + "a month to serve, every month, whether or not it ever pays."));

            page.Add(free);

            var paid = NewPanel("SUBSCRIPTION");
            paid.Add(BuildSlider("PRICE A MONTH",
                $"${policy.SubscriptionPriceUsdPerMonth:0}",
                0f, 200f, (float)policy.SubscriptionPriceUsdPerMonth,
                value =>
                {
                    policy.Model = policy.Model == PricingModel.FreeOnly
                        ? PricingModel.FreeOnly
                        : PricingModel.Subscription;
                    policy.SubscriptionPriceUsdPerMonth = value;
                    Reprice();
                }));

            var paidNote = new Label(
                "A subscription ignores the market rate, which protects a good position and traps a "
                + "bad one. Price it high and fewer people ever sign up; price it low and you are "
                + "serving tokens at a loss the moment the frontier moves.");
            paidNote.AddToClassList("field__hint");
            paid.Add(paidNote);
            page.Add(paid);

            return page;
        }

        private static VisualElement DeployTile(string title, string body, bool picked, Action onClick)
        {
            var tile = new Button(onClick);
            tile.AddToClassList("deploy-tile");
            tile.EnableInClassList("deploy-tile--on", picked);

            var name = new Label(title);
            name.AddToClassList("deploy-tile__title");
            tile.Add(name);

            var text = new Label(body);
            text.AddToClassList("deploy-tile__body");
            tile.Add(text);

            return tile;
        }

        /// <summary>A filling gauge. Used wherever the question is "does this feel like enough".</summary>
        private static VisualElement LoadBar(string label, double fraction, string verdict)
        {
            var block = new VisualElement();
            block.AddToClassList("load-bar");

            var head = new VisualElement();
            head.AddToClassList("load-bar__head");

            var name = new Label(label);
            name.AddToClassList("load-bar__label");
            head.Add(name);

            var amount = new Label(UiFormat.Percent(Math.Clamp(fraction, 0.0, 1.0)));
            amount.AddToClassList("load-bar__value");
            head.Add(amount);

            block.Add(head);

            var track = new VisualElement();
            track.AddToClassList("load-bar__track");

            var fill = new VisualElement();
            fill.AddToClassList("load-bar__fill");
            fill.style.width = Length.Percent((float)(Math.Clamp(fraction, 0.0, 1.0) * 100.0));
            HudAccent.PaintRamp(fill, new Color(0.72f, 0.64f, 0.96f), new Color(0.42f, 0.24f, 0.72f));
            track.Add(fill);

            block.Add(track);

            var note = new Label(verdict);
            note.AddToClassList("load-bar__note");
            block.Add(note);

            return block;
        }

        private static VisualElement BuildSlider(string label, string readout, float minimum, float maximum,
            float value, Action<float> onChange)
        {
            var block = new VisualElement();
            block.AddToClassList("stage-slider");

            var head = new VisualElement();
            head.AddToClassList("stage-slider__head");

            var name = new Label(label);
            name.AddToClassList("stage-slider__label");
            head.Add(name);

            var amount = new Label(readout);
            amount.AddToClassList("stage-slider__value");
            head.Add(amount);

            block.Add(head);

            var slider = new Slider(minimum, maximum) { value = Mathf.Clamp(value, minimum, maximum) };
            slider.AddToClassList("stage-slider__control");
            slider.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            block.Add(slider);

            return block;
        }

        private VisualElement BuildIdentityPanel()
        {
            var panel = NewPanel("IDENTITY");

            nameField.label = "Model name";
            nameField.value = "Muse 1";
            nameField.AddToClassList("field");
            nameField.RegisterValueChangedCallback(_ =>
            {
                if (laptopName != null)
                {
                    laptopName.text = DisplayName();
                }

                Reprice();
            });
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

        /// <summary>
        /// The Scale stage: shape, then what the shape is worth, then what is wrong with it.
        ///
        /// It used to be two sliders and a sentence saying "around twenty tokens per parameter",
        /// which is true and useless, because nothing on the screen said whether you were at four or
        /// at eighty. The belt answers that without being read.
        /// </summary>
        private VisualElement BuildShapePanel()
        {
            var column = new VisualElement();
            column.AddToClassList("creator-column");

            var panel = NewPanel("SHAPE");

            parameterLabel.AddToClassList("field__label");
            panel.Add(parameterLabel);
            ConfigureSlider(parameterSlider, MinimumLogParameters, MaximumLogParameters, 1.3f);
            panel.Add(parameterSlider);

            tokenLabel.AddToClassList("field__label");
            panel.Add(tokenLabel);
            ConfigureSlider(tokenSlider, MinimumLogTokens, MaximumLogTokens, 2.6f);
            panel.Add(tokenSlider);

            tokenBytesLabel.AddToClassList("field__hint");
            panel.Add(tokenBytesLabel);

            memoryLabel.AddToClassList("scale-memory");
            panel.Add(memoryLabel);

            column.Add(panel);
            column.Add(BuildBeltBlock());

            scaleReadout = NewPanel("SCALING READOUT");
            column.Add(scaleReadout);

            scaleNotes = NewPanel("NOTES");
            column.Add(scaleNotes);

            return column;
        }

        /// <summary>
        /// The belt, its three zone captions, and the one word describing where the marker landed.
        /// </summary>
        private VisualElement BuildBeltBlock()
        {
            var block = new VisualElement();
            block.AddToClassList("belt-block");

            var head = new VisualElement();
            head.AddToClassList("belt-block__head");

            var title = new Label("TOKENS PER PARAMETER");
            title.AddToClassList("belt-block__title");
            head.Add(title);

            beltRatio = new Label();
            beltRatio.AddToClassList("belt-block__ratio");
            head.Add(beltRatio);

            beltProfile = new Label();
            beltProfile.AddToClassList("belt-block__badge");
            head.Add(beltProfile);

            block.Add(head);
            block.Add(belt);

            var zones = new VisualElement();
            zones.AddToClassList("belt-block__zones");
            zones.Add(ZoneCaption("COMPUTE-STARVED", "belt-zone--left"));
            zones.Add(ZoneCaption("EFFICIENT ZONE", "belt-zone--mid"));
            zones.Add(ZoneCaption("DATA-HEAVY SPILL", "belt-zone--right"));
            block.Add(zones);

            var balance = new Button(BalanceShape) { text = "MATCH THE OPTIMUM" };
            balance.AddToClassList("button");
            balance.style.marginTop = 8;
            balance.style.marginLeft = 0;
            balance.style.marginRight = 0;
            balance.style.marginBottom = 0;
            block.Add(balance);

            return block;
        }

        private static Label ZoneCaption(string text, string extra)
        {
            var label = new Label(text);
            label.AddToClassList("belt-zone");
            label.AddToClassList(extra);
            return label;
        }

        /// <summary>
        /// Four thin bars and a note list, rebuilt from one <see cref="TrainingProfile"/> each time the
        /// blueprint is repriced. The panel computes none of these.
        /// </summary>
        private void RefreshScale(TrainingProjection projection, ModelBlueprint blueprint)
        {
            if (scaleReadout == null)
            {
                return;
            }

            var profile = TrainingProfile.Read(projection);

            belt.Set(profile);
            beltRatio.text = $"{UiFormat.Number(projection.TokensPerParameter)} : 1"
                + $"   (optimum {UiFormat.Number(projection.OptimalTokensPerParameter)})";

            beltProfile.text = profile.ProfileName;
            beltProfile.EnableInClassList("belt-block__badge--good",
                profile.Profile == ShapeProfile.Balanced);
            beltProfile.EnableInClassList("belt-block__badge--bad",
                profile.Profile == ShapeProfile.Oversized);

            tokenBytesLabel.text = $"About {TokenBytes(blueprint.TrainingTokensBillions)} of text, "
                + "at roughly four bytes a token.";

            memoryLabel.text = $"Estimated memory need: "
                + $"{UiFormat.Number(projection.MemoryRequiredGigabytes, 0)} GB of "
                + $"{UiFormat.Number(projection.MemoryAvailableGigabytes, 0)} GB available";
            memoryLabel.EnableInClassList("scale-memory--over", !profile.Fits);

            scaleReadout.Clear();
            scaleReadout.Add(SectionHeading("SCALING READOUT"));

            // Capability is shown against the scale it is measured on rather than against a private
            // maximum, so the bar means the same thing here as it does on the rankings screen.
            scaleReadout.Add(ThinBar("Expected capability",
                UiFormat.Number(projection.ProjectedCapability), projection.ProjectedCapability / 100.0));
            scaleReadout.Add(ThinBar("Training efficiency",
                UiFormat.Percent(profile.TrainingEfficiency), profile.TrainingEfficiency));
            scaleReadout.Add(ThinBar("Budget efficiency",
                UiFormat.Percent(profile.BudgetEfficiency), profile.BudgetEfficiency));
            scaleReadout.Add(ThinBar("Memory used",
                UiFormat.Percent(Math.Min(1.0, profile.MemoryPressure)),
                Math.Min(1.0, profile.MemoryPressure)));

            scaleNotes.Clear();
            scaleNotes.Add(SectionHeading("NOTES"));

            foreach (var note in profile.Notes)
            {
                var line = new Label(note);
                line.AddToClassList("scale-note");
                scaleNotes.Add(line);
            }
        }

        private static Label SectionHeading(string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("panel__heading");
            return heading;
        }

        private static VisualElement ThinBar(string label, string value, double fraction)
        {
            var row = new VisualElement();
            row.AddToClassList("thin-bar");

            var caption = new Label(label);
            caption.AddToClassList("thin-bar__label");
            row.Add(caption);

            var track = new VisualElement();
            track.AddToClassList("thin-bar__track");

            var fill = new VisualElement();
            fill.AddToClassList("thin-bar__fill");
            fill.style.width = Length.Percent((float)(Math.Clamp(fraction, 0.0, 1.0) * 100.0));
            HudAccent.PaintRamp(fill, CoolLow, CoolHigh);
            track.Add(fill);

            row.Add(track);

            var amount = new Label(value);
            amount.AddToClassList("thin-bar__value");
            row.Add(amount);

            return row;
        }

        /// <summary>
        /// Tokens as a pile of text on a disk, which is a size people have a feel for. Four bytes a
        /// token is the usual figure for English prose in a byte pair vocabulary.
        /// </summary>
        private static string TokenBytes(double tokensBillions)
        {
            var bytes = Math.Max(0.0, tokensBillions) * 1e9 * 4.0;

            return bytes switch
            {
                >= 1e15 => $"{bytes / 1e15:0.0} PB",
                >= 1e12 => $"{bytes / 1e12:0.0} TB",
                >= 1e9 => $"{bytes / 1e9:0.0} GB",
                _ => $"{bytes / 1e6:0.0} MB"
            };
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

            panel.Add(ComingRow("Mix by percentage", "Fixed shares for now"));
            panel.Add(ComingRow("Data cleaning", "Standard"));
            panel.Add(ComingRow("Recency cutoff", "Everything available"));

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
            startButton.style.display = DisplayStyle.None;
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
            nextButton.SetEnabled(stage < StageNames.Length - 1
                || (projection.IsFeasible && simulation.State.ActiveRun == null));

            if (simulation.State.ActiveRun != null)
            {
                verdict.text = "A run is already in flight. One at a time.";
            }

            RenderEffectBanner(projection);
            RefreshLaptopConsole();
            RefreshScale(projection, blueprint);
        }

        /// <summary>
        /// The band under the stage rail. It answers the only question that matters while the player
        /// is dragging something: did that help, and can I afford it.
        ///
        /// The delta is measured against the previous repricing rather than against a baseline,
        /// because the lesson being taught is what each change is worth, not what the model is worth.
        /// </summary>
        private void RenderEffectBanner(TrainingProjection projection)
        {
            var delta = previousCapability > 0.0
                ? projection.ProjectedCapability - previousCapability
                : 0.0;
            previousCapability = projection.ProjectedCapability;

            effectBanner.Clear();
            effectBanner.EnableInClassList("effect-banner--blocked", !projection.IsFeasible);

            var bill = projection.ComputeCashCostUsd;
            var frontier = Math.Max(1.0, simulation.Market.FrontierCapability);
            var cash = Math.Max(1.0, simulation.State.CashUsd);

            // Each figure carries a bar rather than a sentence. The bar is measured against the thing
            // that makes the number mean something: capability against the frontier it has to beat,
            // the bill against the money actually in the account.
            effectBanner.Add(EffectFigure("PROJECTED CAPABILITY",
                UiFormat.Number(projection.ProjectedCapability),
                projection.ProjectedCapability / frontier, FigureTone.Cool, delta));
            effectBanner.Add(EffectFigure("FRONTIER TODAY",
                UiFormat.Number(simulation.Market.FrontierCapability),
                simulation.Market.FrontierCapability / 100.0, FigureTone.Cool, 0.0));
            effectBanner.Add(EffectFigure("TIME TO TRAIN",
                UiFormat.Days(projection.TrainingDays),
                projection.TrainingDays / 365.0, FigureTone.Warm, 0.0));
            effectBanner.Add(EffectFigure("CASH IT BURNS", UiFormat.Money(bill),
                bill / cash, FigureTone.Warm, 0.0));

            if (!projection.IsFeasible)
            {
                var blocked = new Label(projection.BlockingReason);
                blocked.AddToClassList("effect-banner__blocked");
                effectBanner.Add(blocked);
            }
        }

        private enum FigureTone
        {
            Cool,
            Warm
        }

        private static readonly Color CoolLow = new(0.44f, 0.72f, 0.98f);
        private static readonly Color CoolHigh = new(0.13f, 0.36f, 0.78f);
        private static readonly Color WarmLow = new(0.72f, 0.64f, 0.96f);
        private static readonly Color WarmHigh = new(0.42f, 0.24f, 0.72f);

        /// <summary>
        /// One reading: a caption, the number, and a bar that fills with it. The bar carries the
        /// comparison the sentence used to make, in less height and without having to be read.
        /// </summary>
        private static VisualElement EffectFigure(string label, string value, double fraction,
            FigureTone tone, double delta)
        {
            var figure = new VisualElement();
            figure.AddToClassList("effect-figure");

            var caption = new Label(label);
            caption.AddToClassList("effect-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("effect-figure__value");
            amount.EnableInClassList("effect-figure__value--up", delta > 0.05);
            amount.EnableInClassList("effect-figure__value--down", delta < -0.05);
            figure.Add(amount);

            var track = new VisualElement();
            track.AddToClassList("effect-figure__track");

            var fill = new VisualElement();
            fill.AddToClassList("effect-figure__fill");
            fill.style.width = Length.Percent((float)(Math.Clamp(fraction, 0.0, 1.0) * 100.0));
            HudAccent.PaintRamp(fill,
                tone == FigureTone.Cool ? CoolLow : WarmLow,
                tone == FigureTone.Cool ? CoolHigh : WarmHigh);
            track.Add(fill);

            figure.Add(track);
            return figure;
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
