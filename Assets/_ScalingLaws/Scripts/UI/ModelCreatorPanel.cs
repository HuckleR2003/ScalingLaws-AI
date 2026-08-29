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

        // The locked end of the parameter slider, drawn over its own track. A slider that simply
        // stops has no way of saying why, and "why" is the whole point: the cap is a research
        // result, so the bar names the node that would move it.
        private readonly VisualElement parameterLock = new();
        private readonly Label parameterLockLabel = new();
        private readonly Slider tokenSlider = new();

        /// <summary>The shaded right-hand end of the token slider, and what it says.</summary>
        private readonly VisualElement tokenLock = new();

        private readonly Label tokenLockLabel = new();
        private readonly Slider rentedSlider = new();
        private readonly VisualElement dataToggles = new();
        private readonly VisualElement readouts = new();
        private readonly Label verdict = new();
        private readonly Button startButton = new();

        // Stopping a run. Two clicks, because nothing comes back: the compute is spent, the days are
        // gone and the model does not exist. One click on a button this destructive is a button that
        // ends campaigns by accident.
        private readonly Button abandonButton = new();
        private bool abandonArmed;

        // The SAFETY stage. Tiers are what the arrows are pointing at, which is not necessarily
        // what the company can build: AllowedTier is the gate and these are only the display.
        private int assaTier;
        private int redTeamTier;
        private int dataTier;
        private int safetyEffort = 1;
        private readonly Label parameterLabel = new();
        private readonly Label tokenLabel = new();
        private readonly Label rentedLabel = new();

        private readonly List<ArchitectureId> architectureOptions = new();
        private readonly Dictionary<DatasetSource, Toggle> dataSourceToggles = new();

        private readonly VisualElement stageRail = new();
        private readonly VisualElement effectBanner = new();
        private readonly VisualElement stageHost = new();

        // The four choices the cards set. Held here rather than on a kept blueprint because the
        // blueprint is rebuilt from the controls on every reprice, which is what keeps one source of
        // truth for what the run will be. Every one starts on the neutral option.
        /// <summary>
        /// Full width until the company researches its way down. See TrainingChoices.GateFor.
        /// </summary>
        private TrainingPrecision blueprintPrecision = TrainingPrecision.Float64;
        private ModelShape blueprintShape = ModelShape.Balanced;
        private DeduplicationPass blueprintDedup = DeduplicationPass.Standard;
        private int blueprintCutoffMonths;

        /// <summary>Raised once a run actually starts, so the shell can leave this screen.</summary>
        public event Action started;
        private readonly Button backButton = new();
        private readonly Button nextButton = new();

        private int stage;
        private double previousCapability;
        private bool commercialise;
        private readonly DemographicPanel demographics = new();
        private int dots;
        private BrowserPreview browser;
        private VisualElement chipHost;
        private readonly SpendMeter spendMeter = new();

        /// <summary>
        /// Holds the three rent meters, refilled on every repricing.
        ///
        /// A container rather than the meters themselves, because `RentReadout.Meters` builds a
        /// fresh block each time and the panel needs somewhere stable to put it. Same reading as
        /// the compute tab, from the same function, so the creator cannot quote a different daily
        /// bill from the screen the player checks it against.
        /// </summary>
        private readonly VisualElement rentMeters = new();
        private readonly Label spendCaption = new();
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
        private VisualElement typePicker;
        private VisualElement dataReadout;
        private Button unblockButton;
        private double unblockCapacity;
        private DropdownField familyField;
        private Label familyHint;
        private readonly List<string> familyLines = new();
        private const string NewLineOption = "Start a new line";
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
        /// <summary>
        /// The eight page names, resolved every read.
        ///
        /// **A `static readonly` array of `Loc.T` calls freezes the language at type load.** The
        /// cold open shipped exactly this fault: the words were fetched once, before the player had
        /// chosen anything, and no amount of translating the rest changed them. A property costs an
        /// allocation per repaint and cannot go out of step with the setting.
        /// </summary>
        private static string[] StageNames => new[]
        {
            Loc.T("create.stage.branding"),
            Loc.T("create.stage.foundation"), Loc.T("create.stage.scale"),
            Loc.T("create.stage.data"), Loc.T("create.stage.compute"),
            Loc.T("create.stage.safety"), Loc.T("create.stage.review"),
            Loc.T("create.stage.after")
        };

        private static readonly string[] StageBlurbs =
        {
            "What the model is built on, and what it is called. The family sets the ceiling for "
            + "everything chosen after it.",
            "How big it is, and how much it reads. This single trade decides most of the result.",
            "What it learns from. The run draws from the best corpus first, so one good archive "
            + "lifts the whole mix.",
            "How much throughput to rent. This buys time and never quality, which is the point.",
            "What happens when this goes wrong, and how much of the calendar to spend making that "
            + "less likely. None of it makes a better model.",
            "What the run is projected to produce, and what it costs to find out.",
            "What happens the day it finishes. This can be changed later, and the market will have "
            + "moved by then."
        };

        /// <summary>Sliders move in log space so one drag covers a billion to a hundred trillion.</summary>
        //
        // The parameter bounds are the blueprint's, because the ceiling is enforced in the
        // simulation and a rule cannot read a constant that only exists up here. Two copies of
        // "the slider runs to ten thousand billion" is one copy that goes stale.
        private const float MinimumLogParameters = (float)ModelBlueprint.LowLogParameters;
        private const float MaximumLogParameters = (float)ModelBlueprint.HighLogParameters;
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

            var title = new Label(Loc.T("create.new_model"));
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
            abandonButton.clicked += AbandonTraining;

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
        /// <summary>
        /// Which stage the creator is on.
        ///
        /// **Public so a test can walk them.** Only the current stage is in the visual tree, so a
        /// check that the screen explains its controls sees nothing at all unless it can turn the
        /// pages, and the badges were added to four different stages.
        /// </summary>
        public int Stage
        {
            get => stage;
            set
            {
                stage = Math.Clamp(value, 0, StageNames.Length - 1);
                ShowStage();
            }
        }

        /// <summary>How many stages there are, so a caller can walk them without knowing the list.</summary>
        public static int StageCount => StageNames.Length;

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

            stageHost.Add(stage switch
            {
                0 => BuildBrandingStage(),
                1 => WithArt("newmodel_1", BuildFoundationColumn(), BuildLaptopScreen()),
                2 => WithArt("newmodel_2", BuildShapePanel()),
                3 => BuildDataPanel(),
                4 => BuildComputePanel(),
                5 => BuildSafetyPanel(),
                6 => BuildProjectionPanel(),
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
        /// The first stage: what the thing is called, and what it looks like to everybody else.
        ///
        /// **This is in front of the engineering on purpose.** Every other page in the creator is a
        /// trade with a wrong side; this one is the only page where the player is simply deciding
        /// who they are, and putting it first means the six pages after it are about a product that
        /// already has a name and a face rather than about an unnamed configuration.
        ///
        /// The browser on the left is a live mock, not a picture: it re-letters as the name is
        /// typed. The chip under the controls is the same element the model hub and the upgrade
        /// screen draw, so the thing being designed here is recognisably the thing that shows up in
        /// the list afterwards.
        /// </summary>
        private VisualElement BuildBrandingStage()
        {
            var row = new VisualElement();
            row.AddToClassList("brand-stage");

            var left = new VisualElement();
            left.AddToClassList("brand-stage__screen");

            browser = new BrowserPreview();
            left.Add(browser.Root);

            var caption = new Label(Loc.T("create.branding.caption"));
            caption.AddToClassList("brand-stage__caption");
            left.Add(caption);

            row.Add(left);

            var right = new VisualElement();
            right.AddToClassList("brand-stage__side");
            right.Add(BuildIdentityPanel());

            var silicon = NewPanel(Loc.T("create.branding.silicon"));
            silicon.AddToClassList("brand-silicon");

            chipHost = new VisualElement();
            chipHost.AddToClassList("brand-silicon__stage");
            silicon.Add(chipHost);

            var siliconNote = new Label(Loc.T("create.branding.silicon_note"));
            siliconNote.AddToClassList("field__hint");
            silicon.Add(siliconNote);

            right.Add(silicon);
            row.Add(right);

            RefreshBranding();
            return row;
        }

        /// <summary>
        /// Repaints the mock and the chip from whatever is currently typed.
        ///
        /// Called from the name field and from the architecture field, because both are on this page
        /// and a preview that only follows one of them is worse than one that follows neither.
        /// </summary>
        private void RefreshBranding()
        {
            var company = simulation.State.CompanyName;

            browser?.Show(company, DisplayName(), simulation.State.FounderName);

            if (chipHost == null)
            {
                return;
            }

            chipHost.Clear();
            chipHost.Add(ChipPreview.Build(company, DisplayName()));
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

            laptopStatus = new Label(Loc.T("create.preparing"));
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

            // Identity moved to the branding stage in front of this one. What is left here is the
            // three decisions about who the model is *for*, which is a different subject from what
            // it is called.
            var top = new VisualElement();
            top.AddToClassList("panel-row");

            var series = NewPanel(Loc.T("create.series"));
            familyField = new DropdownField("Model family");
            familyField.AddToClassList("field");
            familyField.RegisterValueChangedCallback(_ => Reprice());
            series.Add(familyField);

            familyHint = new Label();
            familyHint.AddToClassList("field__hint");
            series.Add(familyHint);

            RefreshFamilyField();
            top.Add(series);
            column.Add(top);
            column.Add(BuildTypePicker());

            // The market, right under the decision it informs. Half the gap the panels above use,
            // because this is the evidence for that choice rather than a separate subject.
            demographics.Show(simulation.MarketByType());
            demographics.Root.AddToClassList("demographics--tight");
            column.Add(demographics.Root);

            return column;
        }

        /// <summary>A row for a decision that exists in the design and not yet in the game.</summary>
        /// <summary>
        /// What the model is for. Five tiles, locked until their research node lands, in the order the
        /// research chain opens them.
        ///
        /// This is the control that was missing. The market has been split by type for two sessions and
        /// the player had no way to choose one, so every model shipped general and the whole type axis
        /// was visible but unreachable.
        /// </summary>
        /// <summary>
        /// The lines the company already sells, plus the option to start one. Rebuilt whenever the
        /// stage is, because a run finishing between two visits adds a line.
        /// </summary>
        private void RefreshFamilyField()
        {
            if (familyField == null)
            {
                return;
            }

            familyLines.Clear();
            familyLines.Add(NewLineOption);

            foreach (var model in simulation.State.DeployedModels)
            {
                if (model != null && model.Family.Length > 0 && !familyLines.Contains(model.Family))
                {
                    familyLines.Add(model.Family);
                }
            }

            familyField.choices = familyLines;
            if (familyField.index < 0 || familyField.index >= familyLines.Count)
            {
                familyField.index = 0;
            }

            familyField.tooltip = ChosenFamily().Length == 0
                ? "A new line stands on its own and starts with nobody using it."
                : $"This supersedes whatever {ChosenFamily()} currently sells. One line is one product, "
                    + "so the older model stops competing the day this one ships.";

            familyHint.text = ChosenFamily().Length == 0 ? "New line" : "Supersedes " + ChosenFamily();
        }

        /// <summary>Empty for a new line, otherwise the line the player picked.</summary>
        private string ChosenFamily()
        {
            if (familyField == null || familyField.index <= 0
                || familyField.index >= familyLines.Count)
            {
                return string.Empty;
            }

            return familyLines[familyField.index];
        }

        private VisualElement BuildTypePicker()
        {
            typePicker = NewPanel("WHAT IS IT FOR");
            RefreshTypePicker();
            return typePicker;
        }

        private void RefreshTypePicker()
        {
            if (typePicker == null)
            {
                return;
            }

            typePicker.Clear();
            typePicker.Add(SectionHeading("WHAT IS IT FOR"));

            var grid = new VisualElement();
            grid.AddToClassList("type-grid");

            // If the current choice is no longer legal, fall back before drawing, so the highlighted
            // tile is always the type the blueprint will actually use.
            if (!simulation.State.CanBuildType(chosenType))
            {
                chosenType = ModelType.General;
            }

            foreach (var definition in ModelTypeCatalog.All)
            {
                grid.Add(BuildTypeTile(definition));
            }

            typePicker.Add(grid);
        }

        private VisualElement BuildTypeTile(ModelTypeDefinition definition)
        {
            var unlocked = simulation.State.CanBuildType(definition.Type);
            var picked = definition.Type == chosenType;

            var tile = new Button(() =>
            {
                if (!unlocked)
                {
                    return;
                }

                chosenType = definition.Type;
                RefreshTypePicker();
                Reprice();
            });

            tile.AddToClassList("type-tile");
            tile.EnableInClassList("type-tile--on", picked);
            tile.EnableInClassList("type-tile--locked", !unlocked);
            tile.SetEnabled(unlocked);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("type-tile__name");
            tile.Add(name);

            if (unlocked)
            {
                // What it costs to serve, because that is the part a player forgets until the bill
                // arrives months later.
                var serving = new Label(definition.ServingCostMultiplier <= 1.0
                    ? "Cheap to serve"
                    : $"{UiFormat.Number(definition.ServingCostMultiplier, 2)}x to serve");

                serving.AddToClassList("type-tile__note");
                tile.Add(serving);
            }
            else
            {
                var gate = new Label($"Needs {ResearchTree.Get(definition.Requires).DisplayName}");
                gate.AddToClassList("type-tile__note");
                gate.AddToClassList("type-tile__note--locked");
                tile.Add(gate);
            }

            tile.tooltip = definition.Description;
            return tile;
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
                // ShowStage, not Reprice. Reprice recalculates the numbers and leaves the page as it
                // is, so choosing COMMERCIALISE set the flag and the pricing panels it is supposed to
                // reveal were never built. The tile looked dead because nothing it did was visible.
                !commercialise, () => { commercialise = false; ShowStage(); }));

            tiles.Add(DeployTile("COMMERCIALISE",
                "It ships the day it finishes. You decide now what a free account gets and what a "
                + "paid one costs, and both of those decide how many people ever try it.",
                commercialise, () => { commercialise = true; ShowStage(); }));

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
                RepriceAndRebuild();
            })
            { text = Loc.T("create.no_free_access") };
            noFree.AddToClassList("chip");
            noFree.EnableInClassList("chip--on", policy.FreeTierTokensPerUserPerDay <= 0.0);
            freeRow.Add(noFree);

            var freeOnly = new Button(() =>
            {
                policy.Model = PricingModel.FreeOnly;
                RepriceAndRebuild();
            })
            { text = Loc.T("create.free_only") };
            freeOnly.AddToClassList("chip");
            freeOnly.EnableInClassList("chip--on", policy.Model == PricingModel.FreeOnly);
            freeRow.Add(freeOnly);

            free.Add(freeRow);

            // **The slider stops where the effect stops.** It ran to 1.5x the generous mark, and
            // `Generosity` clamps at 1.0, so the top third of the travel bought nothing at all and
            // still billed for every token in it. A playtest raised the allowance from 40k to 370k
            // and reported that nothing happened, which was true above 250k.
            free.Add(BuildSlider(Loc.T("creator.free_tokens"),
                amount => UiFormat.Count(amount),
                0f, (float)MonetizationCatalog.GenerousFreeTierTokensPerDay,
                (float)policy.FreeTierTokensPerUserPerDay,
                value =>
                {
                    policy.FreeTierTokensPerUserPerDay = value;
                    if (value > 0.0 && policy.Model == PricingModel.Subscription)
                    {
                        policy.Model = PricingModel.Subscription;
                    }

                    RepriceAndRebuild();
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
            paid.Add(BuildSlider(Loc.T("creator.price_month"),
                amount => UiFormat.Money((long)Math.Round(amount)),
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

        /// <summary>
        /// A labelled slider whose reading follows the handle.
        ///
        /// **The reading is re-lettered, not rebuilt.** It used to be a string handed in once, so
        /// PRICE A MONTH sat at $20 however far the handle moved and only came right when something
        /// else happened to repaint the page. The free-tokens slider looked correct purely because
        /// its callback rebuilt everything — and rebuilding a control from inside its own drag is
        /// the fault that ate the tutorial's clicks, so it is not the fix here either.
        ///
        /// <paramref name="format"/> is asked for the text, so the caller decides the units and
        /// there is no second copy of the formatting.
        /// </summary>
        private static VisualElement BuildSlider(string label, Func<float, string> format,
            float minimum, float maximum, float value, Action<float> onChange)
        {
            var block = new VisualElement();
            block.AddToClassList("stage-slider");

            var head = new VisualElement();
            head.AddToClassList("stage-slider__head");

            var name = new Label(label);
            name.AddToClassList("stage-slider__label");
            head.Add(name);

            var start = Mathf.Clamp(value, minimum, maximum);

            var amount = new Label(format(start));
            amount.AddToClassList("stage-slider__value");
            head.Add(amount);

            block.Add(head);

            var slider = new Slider(minimum, maximum) { value = start };
            slider.AddToClassList("stage-slider__control");
            slider.RegisterValueChangedCallback(evt =>
            {
                amount.text = format(evt.newValue);
                onChange(evt.newValue);
            });

            block.Add(slider);

            return block;
        }

        /// <summary>
        /// Repaints the spend meter and says in words what the colour means.
        ///
        /// The words matter more than the colour: a bar that turns orange tells a player something
        /// is wrong and not what, and this is the control that has cost real campaigns real money.
        /// </summary>
        private void RefreshSpend(long perDay)
        {
            spendMeter.PerDayUsd = perDay;

            var key = perDay > SpendMeter.SevereAbove ? "spend.ruin"
                : perDay > SpendMeter.HeavyAbove ? "spend.severe"
                : perDay > SpendMeter.WarnAbove ? "spend.heavy"
                : perDay > SpendMeter.AdvisedUsdPerDay ? "spend.warn"
                : "spend.calm";

            spendCaption.text = Loc.T(key, UiFormat.Money(perDay));
            spendCaption.style.color = SpendMeter.ToneFor(perDay);
        }

        private VisualElement BuildIdentityPanel()
        {
            var panel = NewPanel(Loc.T("create.identity"));

            nameField.label = Loc.T("create.model_name");
            nameField.value = "Muse 1";
            nameField.AddToClassList("field");
            nameField.RegisterValueChangedCallback(_ =>
            {
                if (laptopName != null)
                {
                    laptopName.text = DisplayName();
                }

                RefreshBranding();
                Reprice();
            });
            panel.Add(nameField);

            architectureField.label = Loc.T("create.architecture");
            architectureField.AddToClassList("field");
            architectureField.RegisterValueChangedCallback(_ => Reprice());
            panel.Add(architectureField);

            // The sentence lives in the tooltip rather than on the page. The same rule the founder
            // skills follow: a full sentence at this column width wraps to two lines and the panel
            // below it falls off the bottom of the screen.
            architectureField.tooltip =
                "A sparse mixture costs a quarter of the FLOPs per token and a little quality per "
                + "parameter. On a fixed budget that trade is usually worth taking.";

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

            // The two sliders take the left half. The two decisions that are not slider shaped
            // take the right, because a stage with four sliders on it is a settings dialog and this
            // one is meant to be the interesting page in the creator.
            var top = new VisualElement();
            top.AddToClassList("panel-row");

            var panel = NewPanel(Loc.T("create.size"));
            panel.AddToClassList("scale-half");

            panel.Add(Explained(parameterLabel, TechNotes.Parameters));

            // The slider and the locked overlay share a host, because the overlay is positioned
            // against the track and has to move with it rather than against the panel.
            var parameterTrack = new VisualElement();
            parameterTrack.AddToClassList("scale-track");

            ConfigureSlider(parameterSlider, MinimumLogParameters, MaximumLogParameters, 0.9f);
            parameterTrack.Add(parameterSlider);

            parameterLock.AddToClassList("scale-lock");
            parameterLock.pickingMode = PickingMode.Ignore;

            parameterLockLabel.AddToClassList("scale-lock__label");
            parameterLock.Add(parameterLockLabel);
            parameterTrack.Add(parameterLock);

            panel.Add(parameterTrack);

            panel.Add(Explained(tokenLabel, TechNotes.TokensPerParameter));
            var tokenTrack = new VisualElement();
            tokenTrack.AddToClassList("scale-track");

            ConfigureSlider(tokenSlider, MinimumLogTokens, MaximumLogTokens, 2.6f);
            tokenTrack.Add(tokenSlider);

            tokenLock.AddToClassList("scale-lock");
            tokenLock.Add(tokenLockLabel);
            tokenLockLabel.AddToClassList("scale-lock__label");
            tokenTrack.Add(tokenLock);

            panel.Add(tokenTrack);

            tokenBytesLabel.AddToClassList("field__hint");
            panel.Add(tokenBytesLabel);

            memoryLabel.AddToClassList("scale-memory");
            panel.Add(memoryLabel);

            top.Add(panel);
            top.Add(BuildPrecisionPanel());
            column.Add(top);
            column.Add(BuildArrangementPanel());
            column.Add(BuildBeltBlock());

            var bottom = new VisualElement();
            bottom.AddToClassList("panel-row");

            scaleReadout = NewPanel("SCALING READOUT");
            bottom.Add(scaleReadout);

            scaleNotes = NewPanel("NOTES");
            bottom.Add(scaleNotes);

            column.Add(bottom);

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

            var title = new Label(Loc.T("create.tokens_per_parameter"));
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

            var balance = new Button(BalanceShape) { text = Loc.T("create.match_optimum") };
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

            // Never quote an optimum that was not computed. With no usable compute the planner cannot
            // produce one, and printing "optimum 0.0" beside a confident OVERSIZED badge was the screen
            // asserting something it had failed to work out.
            beltRatio.text = profile.IsEstimated
                ? $"{UiFormat.Number(projection.TokensPerParameter)} : 1"
                    + $"   (optimum {UiFormat.Number(projection.OptimalTokensPerParameter)})"
                : $"{UiFormat.Number(projection.TokensPerParameter)} : 1   (no optimum until you have compute)";

            beltProfile.text = profile.ProfileName;
            beltProfile.EnableInClassList("belt-block__badge--good",
                profile.IsEstimated && profile.Profile == ShapeProfile.Balanced);
            beltProfile.EnableInClassList("belt-block__badge--bad",
                profile.IsEstimated && profile.Profile == ShapeProfile.Oversized);

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
            // Relative to a twenty billion parameter model, so one is neutral. Capped at three on the
            // bar because past that the exact figure matters less than the fact it is bad.
            scaleReadout.Add(ThinBar("Cost to serve",
                $"{UiFormat.Number(profile.ServingBurden, 2)}x",
                Math.Clamp(profile.ServingBurden / 3.0, 0.0, 1.0)));

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

        /// <summary>
        /// A field caption with an "(i)" beside it.
        ///
        /// **One helper rather than a badge added by hand at each site**, because a caption that
        /// explains itself is the pattern now and the next control added to this screen should get
        /// one without anybody having to remember.
        /// </summary>
        private static VisualElement Explained(Label caption, TechNotes.Note note)
        {
            var row = new VisualElement();
            row.AddToClassList("explained");

            caption.AddToClassList("field__label");
            row.Add(caption);
            row.Add(InsightTip.InfoBadge(note.Title,
                new InsightTip.Reading(note.What, note.Affects, note.High, note.Low)));

            return row;
        }

        /// <summary>
        /// Puts an "(i)" after a panel's own heading, for a stage rather than a single field.
        ///
        /// The heading is lifted out and re-inserted inside a row at the same position, so the panel
        /// keeps the order its builder gave it whatever else has already been added.
        /// </summary>
        private static VisualElement ExplainedHeading(VisualElement panel, TechNotes.Note note)
        {
            var heading = panel.Q<Label>(className: "panel__heading");

            if (heading == null)
            {
                return panel;
            }

            var index = panel.IndexOf(heading);
            heading.RemoveFromHierarchy();

            var row = new VisualElement();
            row.AddToClassList("explained");
            row.Add(heading);
            row.Add(InsightTip.InfoBadge(note.Title,
                new InsightTip.Reading(note.What, note.Affects, note.High, note.Low)));

            panel.Insert(index, row);
            return panel;
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

        /// <summary>
        /// What the numbers are kept in.
        ///
        /// The one decision on this stage that is about the run rather than about the model, and the
        /// only one that can lose the whole thing: FP8 nearly halves the calendar and more than
        /// doubles how far the finished model lands from this projection.
        /// </summary>
        private VisualElement BuildPrecisionPanel()
        {
            var panel = NewPanel("PRECISION");
            panel.AddToClassList("scale-half");

            var row = new VisualElement();
            row.AddToClassList("choice-row");

            foreach (var definition in TrainingChoiceCatalog.AllPrecisions)
            {
                var captured = definition.Precision;
                var open = TrainingChoiceCatalog.IsAvailableOn(captured, simulation.State.Date);

                var card = NewChoiceCard(
                    definition.DisplayName,
                    definition.Pitch,
                    blueprintPrecision == captured,
                    open,
                    open ? $"{definition.Throughput:0.00}x compute, {definition.Instability:0.0}x spread"
                         : $"needs {definition.Earliest} silicon",
                    () => { blueprintPrecision = captured; RepriceAndRebuild(); });

                row.Add(card);
            }

            panel.Add(row);

            var note = new Label("Narrower numbers move more of them through the same cluster. What "
                + "that costs is not a worse model, it is a less predictable one.");

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        /// <summary>Many thin layers or few fat ones. Capability against what a token costs.</summary>
        private VisualElement BuildArrangementPanel()
        {
            var panel = NewPanel("ARRANGEMENT");

            var row = new VisualElement();
            row.AddToClassList("choice-row");

            foreach (var definition in TrainingChoiceCatalog.AllShapes)
            {
                var captured = definition.Shape;

                row.Add(NewChoiceCard(
                    definition.DisplayName,
                    definition.Pitch,
                    blueprintShape == captured,
                    true,
                    $"{definition.Capability:0.00}x capability, {definition.ServingBurden:0.00}x to serve",
                    () => { blueprintShape = captured; RepriceAndRebuild(); }));
            }

            panel.Add(row);

            var note = new Label("Depth is sequential and width is parallel, so the same parameters "
                + "arranged deep think better and cost more per token, every day, forever.");

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        /// <summary>
        /// One card in a row of them. The shape the creator should have been using all along.
        /// </summary>
        private VisualElement NewChoiceCard(string title, string pitch, bool picked, bool open,
            string figures, Action clicked)
        {
            var card = new Button(open ? clicked : null);
            card.AddToClassList("choice-card");
            card.EnableInClassList("choice-card--on", picked);
            card.EnableInClassList("choice-card--shut", !open);
            card.SetEnabled(open);

            var name = new Label(title.ToUpperInvariant());
            name.AddToClassList("choice-card__title");
            card.Add(name);

            var figure = new Label(figures);
            figure.AddToClassList("choice-card__figures");
            card.Add(figure);

            var body = new Label(pitch);
            body.AddToClassList("choice-card__body");
            card.Add(body);

            return card;
        }

        private VisualElement BuildDataPanel()
        {
            var panel = NewPanel(Loc.T("create.data_mix"));
            ExplainedHeading(panel, TechNotes.CuratedWeb);
            panel.Add(dataToggles);

            var hint = new Label(Loc.T("create.data_hint"));
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            dataReadout = new VisualElement();
            dataReadout.AddToClassList("data-readout");
            panel.Add(dataReadout);

            var column = new VisualElement();
            column.AddToClassList("creator-column");
            column.Add(panel);

            var row = new VisualElement();
            row.AddToClassList("panel-row");
            row.Add(BuildCutoffPanel());
            row.Add(BuildDedupPanel());
            column.Add(row);

            return column;
        }

        /// <summary>
        /// Where the corpus stops.
        ///
        /// The one choice in the creator that is a date, which is why it is the one that ties to the
        /// game's own clock: a corpus cut two years back is a third cheaper to license and describes
        /// a world that has moved on. A cheap corpus and a slow release are the same mistake twice.
        /// </summary>
        private VisualElement BuildCutoffPanel()
        {
            var panel = NewPanel("KNOWLEDGE CUTOFF");

            var row = new VisualElement();
            row.AddToClassList("choice-row");

            foreach (var months in TrainingChoiceCatalog.CutoffMonths)
            {
                var captured = months;
                var pipeline = simulation.State.HasResearch(ResearchNodeId.ContinuousDataPipeline);
                var title = months == 0 ? "TODAY" : $"{months} MONTHS BACK";

                var pitch = months == 0
                    ? "Everything up to the day the run starts. Dearest, messiest, and right about "
                      + "the present."
                    : months >= 24
                        ? "Two years back. Cheap, clean, thoroughly studied, and wrong about "
                          + "anything that has happened since."
                        : "A compromise. Most of the saving, some of the staleness.";

                row.Add(NewChoiceCard(
                    title,
                    pitch,
                    blueprintCutoffMonths == captured,
                    true,
                    $"{TrainingChoiceCatalog.CutoffCapabilityMultiplier(months):0.00}x capability, "
                    + $"{TrainingChoiceCatalog.CutoffCostMultiplier(months, pipeline):0.00}x the data bill"
                    + (pipeline && months < 12 ? "  (pipeline)" : string.Empty),
                    () => { blueprintCutoffMonths = captured; RepriceAndRebuild(); }));
            }

            panel.Add(row);
            return panel;
        }

        /// <summary>How hard the corpus is scrubbed before the run sees it.</summary>
        private VisualElement BuildDedupPanel()
        {
            var panel = NewPanel("DEDUPLICATION");

            var row = new VisualElement();
            row.AddToClassList("choice-row");

            foreach (var definition in TrainingChoiceCatalog.AllPasses)
            {
                var captured = definition.Pass;
                var gate = TrainingChoiceCatalog.GateFor(captured);
                var open = gate == ResearchNodeId.None || simulation.State.HasResearch(gate);

                row.Add(NewChoiceCard(
                    definition.DisplayName,
                    definition.Pitch,
                    blueprintDedup == captured,
                    open,
                    open
                        ? $"{definition.TokensKept:P0} of the tokens, {definition.Quality:0.00}x each"
                        : $"needs {ResearchTree.Get(gate).DisplayName}",
                    () => { blueprintDedup = captured; RepriceAndRebuild(); }));
            }

            panel.Add(row);

            var note = new Label("It lands in both terms at once: fewer tokens, worth more each. "
                + "Whether that pays depends on whether the run had tokens to spare, which the belt "
                + "on the last stage already answered.");

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        /// <summary>
        /// What the chosen corpus actually is, in the four numbers the blend already computes.
        ///
        /// This stage used to end in three rows reading "fixed shares for now", "standard" and
        /// "everything available". Meanwhile the blend behind it had a real quality multiplier, a real
        /// token ceiling, a real bill and a real sufficiency check, and the player could see none of
        /// them. Three labels promising features hid four facts that were already true.
        /// </summary>
        private void RefreshDataReadout(TrainingProjection projection, ModelBlueprint blueprint)
        {
            if (dataReadout == null)
            {
                return;
            }

            var blend = projection.Blend;
            dataReadout.Clear();

            // Quality is a multiplier around one, so it is shown against a band rather than as a
            // percentage of nothing. Below one actively hurts the run.
            dataReadout.Add(ThinBar("Corpus quality",
                $"{UiFormat.Number(blend.QualityMultiplier, 2)}x",
                Math.Clamp((blend.QualityMultiplier - 0.5) / 1.0, 0.0, 1.0)));

            var needed = Math.Max(1.0, blueprint.TrainingTokensBillions);
            dataReadout.Add(ThinBar("Tokens available",
                $"{UiFormat.Billions(blend.AvailableTokensBillions)}",
                Math.Clamp(blend.AvailableTokensBillions / needed, 0.0, 1.0)));

            var costRow = new Label(blend.AcquisitionCostUsd > 0L
                ? $"Licensing this mix costs {UiFormat.Money(blend.AcquisitionCostUsd)}."
                : "Nothing in this mix has to be paid for.");
            costRow.AddToClassList("scale-note");
            dataReadout.Add(costRow);

            if (blend.SourceCount == 0)
            {
                var none = new Label(Loc.T("create.no_corpus_selected"));
                none.AddToClassList("scale-note");
                none.AddToClassList("scale-note--bad");
                dataReadout.Add(none);
            }
            else if (!blend.IsSufficient)
            {
                var short_ = new Label(
                    $"This mix holds {UiFormat.Billions(blend.AvailableTokensBillions)} and the run "
                    + $"wants {UiFormat.Billions(blueprint.TrainingTokensBillions)}. Add a source or "
                    + "train on fewer tokens.");

                short_.AddToClassList("scale-note");
                short_.AddToClassList("scale-note--bad");
                dataReadout.Add(short_);
            }
            else
            {
                var ok = new Label($"{blend.SourceCount} sources, enough for this run.");
                ok.AddToClassList("scale-note");
                dataReadout.Add(ok);
            }
        }

        private VisualElement BuildComputePanel()
        {
            var panel = NewPanel("COMPUTE");

            rentedLabel.AddToClassList("field__label");
            panel.Add(rentedLabel);

            // What is contracted, what it costs a day and what actually arrives, above the control
            // that changes all three. This is the block the compute tab leads with, and a player
            // sizing a run here was deciding the same thing with fewer of the figures.
            panel.Add(rentMeters);

            // Sized from the company rather than fixed, the same way the compute tab's is. A fixed
            // twenty five thousand is six hundred million accounts, which is not a decision a lab
            // with no product is making.
            ConfigureSlider(rentedSlider, 0f,
                (float)RentReadout.CeilingPetaflops(HeldUsers(), simulation.State.Pool.RentedPetaflops),
                150f);
            panel.Add(rentedSlider);

            // What the day costs, with a mark at what he tells you to stay under. The one control
            // in this creator that bills every day whether or not anything is training.
            panel.Add(spendMeter);

            spendCaption.AddToClassList("spend__caption");
            panel.Add(spendCaption);

            var hint = new Label(
                "Rented capacity is contracted in petaflops, not in boxes, so the bill does not move on "
                + "its own when the clouds change generation. It never ages, and it bills every day it "
                + "is held whether or not it is doing anything.");
            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        /// <summary>
        /// The safety stage: three modules, four tiers each, and how hard to work them.
        ///
        /// **Nothing here makes a better model and every line of it says so.** This is the stage
        /// that costs calendar and buys a smaller chance of something, which is the hardest kind of
        /// value to see and the easiest to skip. The tiles show the art, the tier, what it does in
        /// plain percentages and what it adds to the run, because a player who cannot price it will
        /// not take it.
        /// </summary>
        private VisualElement BuildSafetyPanel()
        {
            var column = new VisualElement();
            column.AddToClassList("creator-column");

            var top = new VisualElement();
            top.AddToClassList("panel-row");

            // **RepriceAndRebuild, not Reprice.** Reprice refreshes the numbers and nothing else,
            // so the arrows moved the tier, changed the price, and left the plate showing the
            // previous tier's art, name and description. Every one of the three modules looked
            // broken, and a player who had just finished researching a tier had no way to tell
            // that the research had landed. The stage has to be rebuilt because the thing that
            // changed is the picture, not a caption.
            top.Add(BuildSafetyModule(SafetyModule.Assa, assaTier, tier =>
            {
                assaTier = tier;
                RepriceAndRebuild();
            }));

            top.Add(BuildSafetyModule(SafetyModule.RedTeam, redTeamTier, tier =>
            {
                redTeamTier = tier;
                RepriceAndRebuild();
            }));

            column.Add(top);

            var bottom = new VisualElement();
            bottom.AddToClassList("panel-row");

            bottom.Add(BuildSafetyModule(SafetyModule.DataProtection, dataTier, tier =>
            {
                dataTier = tier;
                RepriceAndRebuild();
            }));

            bottom.Add(BuildEffortPanel());
            column.Add(bottom);

            column.Add(BuildSafetySummary());
            return column;
        }

        /// <summary>One module: art in the middle, arrows either side, the numbers underneath.</summary>
        private VisualElement BuildSafetyModule(SafetyModule module, int shown, Action<int> pick)
        {
            var panel = NewPanel(SafetyModuleCatalog.NameOf(module));
            panel.AddToClassList("scale-half");

            var pitch = new Label(SafetyModuleCatalog.PitchOf(module));
            pitch.AddToClassList("field__hint");
            panel.Add(pitch);

            var tier = SafetyModuleCatalog.Get(module, shown);
            var open = Allowed(tier.Requires);

            var row = new VisualElement();
            row.AddToClassList("tier-row");

            var back = new Button(() => pick(
                (shown - 1 + SafetyModuleCatalog.TierCount) % SafetyModuleCatalog.TierCount))
            { text = "<" };

            back.AddToClassList("tier-arrow");
            row.Add(back);

            var plate = new VisualElement();
            plate.AddToClassList("tier-plate");
            plate.EnableInClassList("tier-plate--locked", !open);

            var art = ResearchIcons.ByName(tier.Icon);
            if (art != null)
            {
                plate.style.backgroundImage = new StyleBackground(art);
            }

            if (!open)
            {
                // Across the middle of the picture, which is where the author asked for it and also
                // the only place a player is definitely looking.
                var locked = new Label(Loc.T("create.locked_research"));
                locked.AddToClassList("tier-plate__locked");
                plate.Add(locked);
            }

            row.Add(plate);

            var next = new Button(() => pick((shown + 1) % SafetyModuleCatalog.TierCount))
            { text = ">" };

            next.AddToClassList("tier-arrow");
            row.Add(next);

            panel.Add(row);

            var name = new Label(tier.DisplayName);
            name.AddToClassList("tier-name");
            panel.Add(name);

            var effect = new Label(SafetyEffectLine(module, tier));
            effect.AddToClassList("tier-effect");
            panel.Add(effect);

            var body = new Label(tier.Description);
            body.AddToClassList("tier-body");
            panel.Add(body);

            var bill = new Label(open
                ? $"+{tier.ExtraDays} days   ·   {UiFormat.Money(tier.ExtraCostUsd)}"
                : $"+{tier.ExtraDays} days   ·   {UiFormat.Money(tier.ExtraCostUsd)}   ·   NOT AVAILABLE");

            bill.AddToClassList("tier-bill");
            bill.EnableInClassList("tier-bill--locked", !open);
            panel.Add(bill);

            return panel;
        }

        /// <summary>The percentages, written the way a player reads them rather than as fractions.</summary>
        private static string SafetyEffectLine(SafetyModule module, in SafetyTier tier)
        {
            var parts = new List<string>(3);

            if (tier.RiskReduction > 0.0)
            {
                parts.Add($"-{tier.RiskReduction:P1} incident risk");
            }

            if (tier.SaveChance > 0.0)
            {
                parts.Add($"{tier.SaveChance:P1} to avoid a penalty");
            }

            if (tier.PerModelBonus > 0.0)
            {
                var what = module == SafetyModule.RedTeam ? "to avoid" : "risk";
                parts.Add($"+{tier.PerModelBonus:P1} {what} per live model, up to {tier.PerModelCap}");
            }

            return string.Join("   ·   ", parts);
        }

        /// <summary>How much extra work to put into the stage. Only the safety days move.</summary>
        private VisualElement BuildEffortPanel()
        {
            var panel = NewPanel(Loc.T("create.effort"));
            ExplainedHeading(panel, TechNotes.SafetyEffort);
            panel.AddToClassList("scale-half");

            var pitch = new Label(Loc.T("create.effort_hint"));

            pitch.AddToClassList("field__hint");
            panel.Add(pitch);

            var row = new VisualElement();
            row.AddToClassList("effort-row");

            foreach (var effort in SafetyModuleCatalog.Efforts)
            {
                var captured = effort.Multiplier;

                // RepriceAndRebuild, for the same reason the tier arrows needed it: the selected
                // chip is a class set when the row was built, so Reprice alone changed the price
                // and left x1 looking chosen however many times the player pressed x3.
                var button = new Button(() =>
                {
                    safetyEffort = captured;
                    RepriceAndRebuild();
                })
                { text = $"x{captured}" };

                button.AddToClassList("effort-chip");
                button.EnableInClassList("effort-chip--on", safetyEffort == captured);
                row.Add(button);
            }

            panel.Add(row);

            var chosen = SafetyModuleCatalog.EffortOf(safetyEffort);
            var reading = new Label(chosen.Multiplier == 1
                ? "The stage takes exactly as long as the modules cost."
                : $"Safety work takes {chosen.TimeMultiplier:0.0}x as long. Every safety figure "
                  + $"gains {chosen.StatBonus:P1}.");

            reading.AddToClassList("tier-effect");
            panel.Add(reading);

            return panel;
        }

        /// <summary>What all of it adds up to, which is the only number that decides anything.</summary>
        private VisualElement BuildSafetySummary()
        {
            var panel = NewPanel("WHAT THIS BUYS");

            var plan = new SafetyPlan(
                AllowedTier(SafetyModule.Assa, assaTier),
                AllowedTier(SafetyModule.RedTeam, redTeamTier),
                AllowedTier(SafetyModule.DataProtection, dataTier),
                safetyEffort,
                simulation.State.DeployedModels.Count);

            panel.Add(Row("Incident risk", $"-{plan.RiskReduction:P1}"));
            panel.Add(Row("Chance a penalty is dropped", $"{plan.SaveChance:P1}"));
            panel.Add(Row("Added to the run", $"{plan.ExtraDays} days"));
            panel.Add(Row("Added to the bill", UiFormat.Money(plan.ExtraCostUsd)));

            var note = new Label(
                "Modules stack on what is left rather than adding up, so two at half strength are "
                + "not one at full. Nothing here can reach certainty, and the protection travels "
                + "with this model rather than with the company: researching a tier next year does "
                + "not harden a model shipped today.");

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        /// <summary>A label and a figure on one line. The summary is four of these.</summary>
        private static VisualElement Row(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("readout");
            row.Add(new Label(label));

            var figure = new Label(value);
            figure.AddToClassList("readout__value");
            row.Add(figure);

            return row;
        }

        private VisualElement BuildProjectionPanel()
        {
            var panel = NewPanel("PROJECTION");
            panel.Add(readouts);

            verdict.AddToClassList("verdict");
            panel.Add(verdict);

            unblockButton = new Button(() =>
            {
                simulation.SetRentedPetaflops(unblockCapacity);
                rentedSlider.SetValueWithoutNotify((float)unblockCapacity);
                Reprice();
            });

            unblockButton.AddToClassList("button");
            unblockButton.AddToClassList("button--unblock");
            unblockButton.style.marginTop = 10;
            unblockButton.style.marginLeft = 0;
            unblockButton.style.marginRight = 0;
            unblockButton.style.marginBottom = 0;
            unblockButton.style.width = Length.Percent(100);
            unblockButton.style.display = DisplayStyle.None;
            panel.Add(unblockButton);

            startButton.text = "START TRAINING";
            startButton.AddToClassList("button");
            startButton.AddToClassList("button--primary");
            startButton.style.marginTop = 14;
            startButton.style.width = Length.Percent(100);
            startButton.style.display = DisplayStyle.None;

            abandonButton.AddToClassList("button");
            abandonButton.AddToClassList("button--abandon");
            abandonButton.style.marginTop = 10;
            abandonButton.style.marginLeft = 0;
            abandonButton.style.marginRight = 0;
            abandonButton.style.width = Length.Percent(100);
            abandonButton.style.display = DisplayStyle.None;
            panel.Add(startButton);
            panel.Add(abandonButton);

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

        /// <summary>
        /// Holds the slider inside what the company knows how to train, and draws the rest as
        /// locked.
        ///
        /// The slider keeps its full range rather than having `highValue` moved down. Shrinking the
        /// range would rescale the whole control every time a node completes, so a drag that used
        /// to land on thirty billion would land somewhere else, and the player would have no way of
        /// seeing that the cap had moved at all. Clamping the value and covering the rest says the
        /// same thing and shows the gain.
        /// </summary>
        private void RefreshParameterCeiling()
        {
            var fraction = ScaleCeiling.FractionFor(simulation.State.HasResearch);
            var ceilingLog = (float)(MinimumLogParameters
                + (MaximumLogParameters - MinimumLogParameters) * fraction);

            if (parameterSlider.value > ceilingLog)
            {
                parameterSlider.SetValueWithoutNotify(ceilingLog);
            }

            var locked = 1.0 - fraction;
            parameterLock.style.display = locked <= 0.0005 ? DisplayStyle.None : DisplayStyle.Flex;
            parameterLock.style.width = Length.Percent((float)(locked * 100.0));

            if (locked <= 0.0005)
            {
                return;
            }

            var ceiling = simulation.ParameterCeilingBillions();
            parameterLockLabel.text =
                ScaleCeiling.TryNextRung(simulation.State.HasResearch, out var rung, out _)
                    ? $"LOCKED  ·  {UiFormat.Billions(ceiling)} CAP  ·  {ResearchTree.Get(rung).DisplayName.ToUpperInvariant()}"
                    : $"LOCKED  ·  {UiFormat.Billions(ceiling)} CAP";
        }

        /// <summary>
        /// Holds the token slider under whatever the company's data pipeline can actually feed it.
        ///
        /// The same shape as the parameter ceiling and for the same reason: a company that has not
        /// solved its corpus problem cannot train on a corpus it does not have. Half the travel to
        /// begin with, and each rung of the data ladder opens more of it.
        /// </summary>
        private void RefreshTokenCeiling()
        {
            var fraction = TokenCeiling.FractionFor(simulation.State.HasResearch);
            var ceilingLog = (float)(MinimumLogTokens
                + (MaximumLogTokens - MinimumLogTokens) * fraction);

            if (tokenSlider.value > ceilingLog)
            {
                tokenSlider.SetValueWithoutNotify(ceilingLog);
            }

            var locked = 1.0 - fraction;
            tokenLock.style.display = locked <= 0.0005 ? DisplayStyle.None : DisplayStyle.Flex;
            tokenLock.style.width = Length.Percent((float)(locked * 100.0));

            if (locked <= 0.0005)
            {
                return;
            }

            var ceiling = Math.Pow(10.0, ceilingLog);

            tokenLockLabel.text =
                TokenCeiling.TryNextRung(simulation.State.HasResearch, out var rung, out _)
                    ? $"LOCKED  ·  {UiFormat.Billions(ceiling)} CAP  ·  {ResearchTree.Get(rung).DisplayName.ToUpperInvariant()}"
                    : $"LOCKED  ·  {UiFormat.Billions(ceiling)} CAP";
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
                var empty = new Label(Loc.T("create.no_corpora"));
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

            // A type the player has not researched must never reach a blueprint, whatever the field
            // says. Loading a save from a run that had the research, into one that does not, is the
            // case that would otherwise ship a model nobody could have built.
            var type = simulation.State.CanBuildType(chosenType) ? chosenType : ModelType.General;

            // The same guard the type gets, for the same reason. A save carrying FP8 loaded into a
            // company without the research would otherwise start a run nobody could have started.
            var precision = Allowed(TrainingChoiceCatalog.GateFor(blueprintPrecision))
                ? blueprintPrecision
                : TrainingPrecision.Float64;

            var dedup = Allowed(TrainingChoiceCatalog.GateFor(blueprintDedup))
                ? blueprintDedup
                : DeduplicationPass.Standard;

            return new ModelBlueprint(
                string.IsNullOrWhiteSpace(nameField.value) ? "Untitled model" : nameField.value,
                architecture,
                Math.Pow(10.0, parameterSlider.value),
                Math.Pow(10.0, tokenSlider.value),
                sources,
                type,
                ChosenFamily(),
                precision,
                blueprintShape,
                dedup,
                blueprintCutoffMonths,
                AllowedTier(SafetyModule.Assa, assaTier),
                AllowedTier(SafetyModule.RedTeam, redTeamTier),
                AllowedTier(SafetyModule.DataProtection, dataTier),
                safetyEffort);
        }

        /// <summary>Whether the company has the node an option needs, or the option needs none.</summary>
        private bool Allowed(ResearchNodeId gate) =>
            gate == ResearchNodeId.None || simulation.State.HasResearch(gate);

        /// <summary>
        /// The highest tier at or below the one shown that the company has actually researched.
        ///
        /// **The arrows walk past locked tiers on purpose**, because a player has to be able to see
        /// what they are missing and what it would be worth. What must never happen is a locked tier
        /// reaching a blueprint: a save carrying tier three loaded into a company that has not
        /// researched it would otherwise start a run nobody could have started. Same guard the model
        /// type and the precision already have, for the same reason.
        ///
        /// Data protection returns minus one when nothing is open, which is what "none" means for it.
        /// </summary>
        private int AllowedTier(SafetyModule module, int shown)
        {
            for (var tier = Math.Clamp(shown, 0, SafetyModuleCatalog.TierCount - 1); tier >= 0; tier--)
            {
                if (Allowed(SafetyModuleCatalog.Get(module, tier).Requires))
                {
                    return tier;
                }
            }

            return module == SafetyModule.DataProtection ? -1 : 0;
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

        /// <summary>How many people the company holds today, which is what sizes the rent slider.</summary>
        private double HeldUsers()
        {
            var audience = simulation.MarketByType();
            return audience.TotalUsersOverall * audience.OverallShareOf(0);
        }

        private void Reprice()
        {
            simulation.SetRentedPetaflops(rentedSlider.value);

            // **The ceilings run first, before anything reads a slider.**
            //
            // They used to run after the blueprint was built, which meant the clamp moved the
            // handle back and every number on the screen had already been computed from the
            // position it was dragged to. The locked half of the parameter slider looked like it
            // did nothing: the handle snapped back but the parameter count, the projected
            // capability and the bill all reported the value the player had reached.
            RefreshParameterCeiling();
            RefreshTokenCeiling();

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

            RefreshSpend(SimUnitsToDaily(profile));

            var rentCeiling = RentReadout.CeilingPetaflops(
                HeldUsers(), simulation.State.Pool.RentedPetaflops);

            rentedSlider.highValue = (float)rentCeiling;

            rentMeters.Clear();
            rentMeters.Add(RentReadout.Meters(
                profile, simulation.Market, simulation.State.Pool.RentedPetaflops, rentCeiling));

            BeginReadouts();
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
            EndReadouts();

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

            RefreshUnblockButton(projection);

            startButton.SetEnabled(projection.IsFeasible && simulation.State.ActiveRun == null);
            nextButton.SetEnabled(stage < StageNames.Length - 1
                || (projection.IsFeasible && simulation.State.ActiveRun == null));

            // A run in flight used to be a dead end: the creator said "one at a time" and there
            // was no way to stop the one that was running, so a blueprint the player regretted cost
            // the whole two hundred days. CancelTraining existed the entire time and nothing called
            // it.
            var running = simulation.State.ActiveRun;
            abandonButton.style.display = running != null ? DisplayStyle.Flex : DisplayStyle.None;

            if (running != null)
            {
                var elapsed = running.DaysElapsed(simulation.State.Date);
                verdict.text = $"{running.Blueprint.Name} is in flight: "
                    + $"{UiFormat.Percent(running.Progress, 0)} done after {elapsed} days. "
                    + "One at a time.";

                abandonButton.text = abandonArmed
                    ? "CONFIRM, THE RUN IS LOST"
                    : "ABANDON THIS RUN";

                abandonButton.EnableInClassList("button--armed", abandonArmed);
            }
            else
            {
                abandonArmed = false;
            }

            RenderEffectBanner(projection);
            RefreshLaptopConsole();
            RefreshScale(projection, blueprint);
            RefreshDataReadout(projection, blueprint);
            RefreshFamilyField();
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

            effectBanner.EnableInClassList("effect-banner--blocked", !projection.IsFeasible);

            var bill = projection.ComputeCashCostUsd;
            var frontier = Math.Max(1.0, simulation.Market.FrontierCapability);
            var cash = Math.Max(1.0, simulation.State.CashUsd);

            // Each figure carries a bar rather than a sentence. The bar is measured against the thing
            // that makes the number mean something: capability against the frontier it has to beat,
            // the bill against the money actually in the account.
            SetFigure(0, "PROJECTED CAPABILITY", UiFormat.Number(projection.ProjectedCapability),
                projection.ProjectedCapability / frontier, FigureTone.Cool, delta);
            SetFigure(1, "FRONTIER TODAY", UiFormat.Number(simulation.Market.FrontierCapability),
                simulation.Market.FrontierCapability / 100.0, FigureTone.Cool, 0.0);
            SetFigure(2, "TIME TO TRAIN", UiFormat.Days(projection.TrainingDays),
                projection.TrainingDays / 365.0, FigureTone.Warm, 0.0);
            SetFigure(3, "CASH IT BURNS", UiFormat.Money(bill), bill / cash, FigureTone.Warm, 0.0);

            if (blockedLabel.parent == null)
            {
                // Added after the figures so it sits under them, and kept rather than rebuilt so an
                // infeasible run does not churn the banner on every frame of a drag.
                blockedLabel.AddToClassList("effect-banner__blocked");
                effectBanner.Add(blockedLabel);
            }

            blockedLabel.text = projection.IsFeasible ? string.Empty : projection.BlockingReason;
            blockedLabel.style.display = projection.IsFeasible
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        // The same pooling as the readout table, for the same reason: this banner sat under a slider
        // and was rebuilt from nothing on every frame of every drag.
        private readonly List<(Label Name, Label Value, VisualElement Fill)> figures = new();
        private readonly Label blockedLabel = new();

        private void SetFigure(int slot, string label, string value, double fraction, FigureTone tone,
            double delta)
        {
            while (figures.Count <= slot)
            {
                // Built with this slot's tone, because the bar's gradient is a baked texture painted
                // once at construction rather than a class that could be toggled later.
                var built = EffectFigure(string.Empty, string.Empty, 0.0, tone, 0.0);
                effectBanner.Add(built);

                figures.Add((
                    (Label)built.ElementAt(0),
                    (Label)built.ElementAt(1),
                    built.ElementAt(2).ElementAt(0)));
            }

            var (name, amount, fill) = figures[slot];

            name.text = label;
            amount.text = value;
            amount.EnableInClassList("effect-figure__value--up", delta > 0.05);
            amount.EnableInClassList("effect-figure__value--down", delta < -0.05);

            fill.style.width = Length.Percent(
                (float)(Math.Clamp(Core.SimUnits.Finite(fraction), 0.0, 1.0) * 100.0));
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

        // ---- the readout table, pooled ------------------------------------------------------
        //
        // Reprice runs on **every frame the player is dragging a slider**, and it used to clear this
        // panel and rebuild it: eleven rows of three elements each, thirty three created and thrown
        // away per frame, every one of them forcing a fresh style resolve and layout pass on the
        // subtree. The projection behind it costs five microseconds; the elements were the stutter.
        //
        // Rows are built once and reused. Only the two strings and one class change afterwards, and
        // the surplus is hidden rather than destroyed, so a shorter run costs nothing either.

        private readonly List<(VisualElement Row, Label Name, Label Value)> readoutRows = new();
        private int readoutCursor;

        private void BeginReadouts() => readoutCursor = 0;

        private void EndReadouts()
        {
            for (var index = readoutCursor; index < readoutRows.Count; index++)
            {
                readoutRows[index].Row.style.display = DisplayStyle.None;
            }
        }

        private void AddReadout(string label, string value, Tone tone)
        {
            if (readoutCursor >= readoutRows.Count)
            {
                var built = new VisualElement();
                built.AddToClassList("readout");

                var builtName = new Label();
                built.Add(builtName);

                var builtValue = new Label();
                builtValue.AddToClassList("readout__value");
                built.Add(builtValue);

                readouts.Add(built);
                readoutRows.Add((built, builtName, builtValue));
            }

            var (row, name, valueLabel) = readoutRows[readoutCursor++];

            row.style.display = DisplayStyle.Flex;
            name.text = label;
            valueLabel.text = value;

            // Stated in full every time. Leaving a tone class behind from the previous frame is how a
            // green number stays green after the run it describes has become infeasible.
            valueLabel.EnableInClassList("readout__value--good", tone == Tone.Good);
            valueLabel.EnableInClassList("readout__value--warn", tone == Tone.Warn);
            valueLabel.EnableInClassList("readout__value--bad", tone == Tone.Bad);
        }

        /// <summary>
        /// The way out of the dead end.
        ///
        /// A new company owns no compute, so the very first run is always infeasible, START TRAINING
        /// is disabled, and the screen explains the problem without offering anywhere to go. The
        /// player has to know that the answer is on another stage, and that renting is the answer at
        /// all. This finds the smallest rented capacity that makes the run possible and offers it at
        /// its real price.
        ///
        /// It does not rent anything by itself. Committing to a daily bill is a decision.
        /// </summary>
        private void RefreshUnblockButton(TrainingProjection projection)
        {
            if (unblockButton == null)
            {
                return;
            }

            if (projection.IsFeasible || simulation.State.ActiveRun != null)
            {
                unblockButton.style.display = DisplayStyle.None;
                return;
            }

            var needed = SmallestCapacityThatWorks();
            if (needed <= 0.0)
            {
                // Not a compute problem, or no amount of renting fixes it. Saying "rent more" when
                // renting is not the answer would send the player to spend money for nothing.
                unblockButton.style.display = DisplayStyle.None;
                return;
            }

            var market = simulation.Market;
            var daily = Core.SimUnits.ToDollars(needed * market.RentPricePerPetaflopDayUsd);

            unblockButton.style.display = DisplayStyle.Flex;
            unblockButton.text =
                $"RENT {UiFormat.Petaflops(needed)} TO MAKE THIS POSSIBLE  ({UiFormat.Money(daily)} A DAY)";

            unblockCapacity = needed;
        }

        /// <summary>
        /// Walks capacity upward until the planner stops refusing, then stops.
        ///
        /// Geometric rather than linear because the range runs from tens of petaflops to hundreds of
        /// thousands, and the answer only has to be close: the player can move the slider afterwards.
        /// Returns zero when no reachable capacity helps, which is the honest answer for a run that is
        /// blocked on something other than compute.
        /// </summary>
        private double SmallestCapacityThatWorks()
        {
            var before = simulation.State.Pool.RentedPetaflops;
            var blueprint = CurrentBlueprint();
            var answer = 0.0;

            try
            {
                for (var capacity = 25.0; capacity <= 400_000.0; capacity *= 1.6)
                {
                    simulation.SetRentedPetaflops(capacity);
                    if (simulation.Project(blueprint).IsFeasible)
                    {
                        answer = capacity;
                        break;
                    }
                }
            }
            finally
            {
                // Whatever happens, the company must be left exactly as it was found. This is a
                // question, not a purchase.
                simulation.SetRentedPetaflops(before);
            }

            return answer;
        }

        /// <summary>
        /// Reprices and rebuilds the open stage.
        ///
        /// For controls whose own caption is part of what changed. Reprice alone leaves a slider
        /// reading the value it had when the stage was built, which is why the pricing stage had to
        /// be left and re-entered before it showed anything but zero.
        /// </summary>
        private void RepriceAndRebuild()
        {
            Reprice();
            ShowStage();
        }

        /// <summary>
        /// Stops the run. Armed first, because the compute is spent and the model never existed.
        /// </summary>
        private void AbandonTraining()
        {
            if (!abandonArmed)
            {
                abandonArmed = true;
                Reprice();
                return;
            }

            abandonArmed = false;
            simulation.CancelTraining();
            Reprice();
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

            // Back to the room. The run is weeks long and there is nothing further to do on this
            // screen, so staying on it is staying on a form that has already been submitted.
            started?.Invoke();
        }
    }
}
