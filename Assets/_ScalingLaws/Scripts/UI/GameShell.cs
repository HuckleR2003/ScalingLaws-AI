using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The shell: money and standing across the top, the screen itself filling the middle, and the
    /// bottom interface carrying the clock, the speed controls and every category the player can
    /// open. There is no side rail: eleven text buttons down the left was a list of screens rather
    /// than a control panel, and it spent a fifth of the window on navigation.
    ///
    /// The shell owns the clock and nothing else. It never computes a game number itself: every
    /// value on screen is read from <see cref="CompanySimulation"/> or a snapshot of it, so the UI
    /// cannot drift away from the simulation the way a second copy of the rules would.
    ///
    /// Structure is built in C# against classes in ScalingLaws.uss. Restyling means editing that
    /// stylesheet and dropping card art in, not touching this file.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameShell : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;
        [SerializeField] private string companyName = "Prometheus AI";

        private CompanyState state;
        private CompanySimulation simulation;
        private SimClock clock;

        /// <summary>Days between automatic saves. Roughly a season of game time.</summary>
        public const int AutoSaveIntervalDays = 90;

        private ModelCreatorPanel creator;
        private UpgradeGridPanel upgrades;
        private ArchitectureCreatorPanel families;
        private int daysSinceAutoSave;
        private bool gameOverShown;

        private VisualElement contentHost;
        private Label cashLabel;
        private Label valuationLabel;
        private Label companyLabel;
        private Label dateLabel;
        private Label rankLabel;
        private GameHud hud;
        private readonly List<CompanyEvent> recentEvents = new();

        private Screen current = Screen.Create;

        private enum Screen
        {
            Create,
            Research,
            Family,
            Team,
            Fleet,
            Business,
            Upgrade,
            Release,
            Funding,
            Ranking,
            Feed
        }

        private void OnEnable()
        {
            try
            {
                Boot();
            }
            catch (Exception exception)
            {
                // A screen that throws while building used to render nothing, which looks exactly
                // like a hung game. Show the reason instead.
                UiBootstrap.ShowFailure(GetComponent<UIDocument>()?.rootVisualElement, "The game screen", exception);
            }
        }

        private void Boot()
        {
            // Resuming is the menu's decision. Loading a corrupt or missing save falls back to a new
            // campaign rather than failing, which is the same rule SaveStore applies everywhere.
            state = SceneFlow.ResumeSavedCampaign
                ? SaveStore.LoadOrCreate(SceneFlow.RequestedCompanyName)
                : CompanyState.FromOpeningChoice(
                    string.IsNullOrWhiteSpace(SceneFlow.RequestedCompanyName)
                        ? companyName
                        : SceneFlow.RequestedCompanyName,
                    (CompanyArchetype)SceneFlow.RequestedArchetype,
                    (FounderTrait)SceneFlow.RequestedTraitA,
                    (FounderTrait)SceneFlow.RequestedTraitB);

            if (!SceneFlow.ResumeSavedCampaign)
            {
                state.FounderName = SceneFlow.RequestedFounderName;
                state.Skills.Restore(SceneFlow.RequestedSkillLevels, Array.Empty<long>());

                var region = Enum.IsDefined(typeof(WorldRegion), SceneFlow.RequestedRegion)
                             && SceneFlow.RequestedRegion != 0
                    ? (WorldRegion)SceneFlow.RequestedRegion
                    : WorldRegion.America;
                var country = Enum.IsDefined(typeof(Country), SceneFlow.RequestedCountry)
                              && SceneFlow.RequestedCountry != 0
                    ? (Country)SceneFlow.RequestedCountry
                    : WorldRegionCatalog.FirstIn(region);

                state.Region = region;
                state.HomeCountry = WorldRegionCatalog.Get(country).Region == region
                    ? country
                    : WorldRegionCatalog.FirstIn(region);
            }

            simulation = new CompanySimulation(state);
            clock = new SimClock(state.Date, SimSpeed.Paused);

            creator = new ModelCreatorPanel(simulation);
            upgrades = new UpgradeGridPanel(simulation);
            families = new ArchitectureCreatorPanel(simulation);

            BuildTree();
            Show(Screen.Create);
        }

        private void OnDisable()
        {
            // Leaving the scene at all, including stopping play, keeps the campaign.
            if (state != null && !state.IsBankrupt)
            {
                SaveStore.Save(state);
            }
        }

        private void Update()
        {
            if (state.IsBankrupt)
            {
                clock.Speed = SimSpeed.Paused;
                if (!gameOverShown)
                {
                    gameOverShown = true;
                    SaveStore.Clear();
                    ShowGameOver();
                }

                return;
            }

            var days = clock.Advance(Time.unscaledDeltaTime);
            if (days <= 0)
            {
                return;
            }

            for (var index = 0; index < days; index++)
            {
                simulation.AdvanceDay();
                if (state.IsBankrupt)
                {
                    break;
                }
            }

            clock.SetDate(state.Date);
            DrainEvents();
            RefreshChrome();

            daysSinceAutoSave += days;
            if (daysSinceAutoSave >= AutoSaveIntervalDays && !state.IsBankrupt)
            {
                daysSinceAutoSave = 0;
                SaveStore.Save(state);
            }

            // Only the screen in front needs repricing; the others rebuild when they are opened.
            if (current == Screen.Create)
            {
                creator.Refresh();
            }
            else if (current == Screen.Upgrade)
            {
                upgrades.Refresh();
            }
            else if (current == Screen.Family)
            {
                families.Refresh();
            }
            else
            {
                Show(current);
            }
        }

        private void BuildTree()
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.Clear();

            UiBootstrap.Prepare(root, theme);
            root.AddToClassList("root");

            root.Add(BuildTopBar());

            var shell = new VisualElement();
            shell.AddToClassList("shell");
            root.Add(shell);

            contentHost = new VisualElement();
            contentHost.style.flexGrow = 1;
            shell.Add(contentHost);

            hud = new GameHud(SetSpeed, SkipDay);
            AddHudSlots();
            root.Add(hud.Root);

            RefreshChrome();
        }

        private VisualElement BuildTopBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("topbar");

            var left = new VisualElement();
            left.AddToClassList("topbar__group");
            cashLabel = new Label();
            cashLabel.AddToClassList("topbar__stat");
            valuationLabel = new Label();
            valuationLabel.AddToClassList("topbar__stat");
            valuationLabel.AddToClassList("topbar__stat--muted");
            left.Add(cashLabel);
            left.Add(valuationLabel);
            bar.Add(left);

            var right = new VisualElement();
            right.AddToClassList("topbar__group");
            rankLabel = new Label();
            rankLabel.AddToClassList("topbar__stat");
            rankLabel.AddToClassList("topbar__stat--muted");
            companyLabel = new Label();
            companyLabel.AddToClassList("topbar__stat");
            dateLabel = new Label();
            dateLabel.AddToClassList("topbar__stat");
            dateLabel.AddToClassList("topbar__stat--muted");
            right.Add(rankLabel);
            right.Add(companyLabel);
            right.Add(dateLabel);

            // The date is on the dial now, so this is the redundant copy. It stays because the top
            // bar is what the player reads while the dial is what they operate.
            var save = new Button(() =>
            {
                SaveStore.Save(state);
                daysSinceAutoSave = 0;
            })
            { text = "SAVE" };
            save.AddToClassList("topbar__action");
            right.Add(save);

            var menu = new Button(SceneFlow.LoadMainMenu) { text = "MENU" };
            menu.AddToClassList("topbar__action");
            right.Add(menu);

            bar.Add(right);

            return bar;
        }

        private void AddHudSlots()
        {
            hud.AddSlot("MODEL", Screen.Create, () => Show(Screen.Create));
            hud.AddSlot("RESEARCH", Screen.Research, () => Show(Screen.Research));
            hud.AddSlot("ARCH", Screen.Family, () => Show(Screen.Family));
            hud.AddSlot("UPGRADE", Screen.Upgrade, () => Show(Screen.Upgrade));
            hud.AddSlot("TEAM", Screen.Team, () => Show(Screen.Team));
            hud.AddSlot("FLEET", Screen.Fleet, () => Show(Screen.Fleet));
            hud.AddSlot("BUSINESS", Screen.Business, () => Show(Screen.Business));
            hud.AddSlot("RELEASE", Screen.Release, () => Show(Screen.Release));
            hud.AddSlot("FUNDING", Screen.Funding, () => Show(Screen.Funding));
            hud.AddSlot("RANKING", Screen.Ranking, () => Show(Screen.Ranking));
            hud.AddSlot("INTEL", Screen.Feed, () => Show(Screen.Feed));
        }

        private void SetSpeed(SimSpeed speed)
        {
            clock.Speed = speed;
            RefreshChrome();
        }

        /// <summary>
        /// Runs exactly one day and stops. It is the only control that moves the calendar without
        /// real time passing, which is what makes it useful while paused: the player decides, then
        /// steps, rather than waiting for a bar to fill.
        /// </summary>
        private void SkipDay()
        {
            if (state.IsBankrupt)
            {
                return;
            }

            simulation.AdvanceDay();
            clock.SetDate(state.Date);

            DrainEvents();
            RefreshChrome();
            Show(current);
        }

        private void ShowGameOver()
        {
            contentHost.Clear();

            var page = NewPage("INSOLVENT",
                $"{state.CompanyName} ran out of credit on {state.Date}. "
                + "The cluster kept billing after the revenue stopped, which is how this usually ends.");

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            page.Add(panel);

            panel.Add(Row("Survived", UiFormat.Days(state.Date.DayIndex)));
            panel.Add(Row("Models shipped", state.ReleasedModelCount.ToString()));
            panel.Add(Row("Best capability reached", UiFormat.Number(state.BestCapability)));
            panel.Add(Row("Frontier at the end", UiFormat.Number(simulation.Market.FrontierCapability)));
            panel.Add(Row("Lifetime revenue", UiFormat.Money(state.LifetimeRevenueUsd)));
            panel.Add(Row("Lifetime operating cost", UiFormat.Money(state.LifetimeOperatingCostUsd)));
            panel.Add(Row("Capital spent on hardware", UiFormat.Money(state.LifetimeCapitalSpentUsd)));
            panel.Add(Row("Raised from investors", UiFormat.Money(state.CapTable.TotalRaisedUsd)));
            panel.Add(Row("Founders held", UiFormat.Percent(state.CapTable.FounderEquity)));

            var back = new Button(SceneFlow.LoadMainMenu) { text = "BACK TO MENU" };
            back.AddToClassList("button");
            back.AddToClassList("button--primary");
            back.style.marginTop = 16;
            panel.Add(back);

            contentHost.Add(page);
        }

        private void Show(Screen screen)
        {
            current = screen;
            contentHost.Clear();

            hud.SetActiveSlot(screen);

            switch (screen)
            {
                case Screen.Create:
                    creator.Refresh();
                    contentHost.Add(creator.Root);
                    break;
                case Screen.Research:
                    contentHost.Add(BuildResearchScreen());
                    break;
                case Screen.Team:
                    contentHost.Add(BuildTeamScreen());
                    break;
                case Screen.Fleet:
                    contentHost.Add(BuildFleetScreen());
                    break;
                case Screen.Business:
                    contentHost.Add(BuildBusinessScreen());
                    break;
                case Screen.Family:
                    families.Refresh();
                    contentHost.Add(families.Root);
                    break;
                case Screen.Upgrade:
                    upgrades.Refresh();
                    contentHost.Add(upgrades.Root);
                    break;
                case Screen.Release:
                    contentHost.Add(BuildReleaseScreen());
                    break;
                case Screen.Funding:
                    contentHost.Add(BuildFundingScreen());
                    break;
                case Screen.Ranking:
                    contentHost.Add(BuildRankingScreen());
                    break;
                default:
                    contentHost.Add(BuildFeedScreen());
                    break;
            }
        }

        /// <summary>
        /// The technology tree, grouped by era. Every node is visible from day one including the one
        /// at the end, because the whole point of the last era is that the player can see it coming
        /// for years before they can touch it.
        /// </summary>
        private VisualElement BuildResearchScreen()
        {
            var active = state.ActiveResearch;
            var page = NewPage("RESEARCH",
                active != null
                    ? $"{ResearchTree.Get(active.Node).DisplayName} in progress: {UiFormat.Percent(active.Progress, 0)}, "
                      + $"{active.DaysCompleted} of {active.DurationDays} days."
                    : "Nothing being researched. Every architecture, corpus, upgrade line and compute tier "
                      + "in the game sits behind a node here, and the calendar cost cannot be bought out of.");

            var board = simulation.ResearchBoard();

            foreach (ResearchEra era in Enum.GetValues(typeof(ResearchEra)))
            {
                var section = new VisualElement();
                section.AddToClassList("panel");

                var heading = new Label(EraTitle(era));
                heading.AddToClassList("panel__heading");
                section.Add(heading);

                var grid = new VisualElement();
                grid.AddToClassList("grid");
                section.Add(grid);

                var any = false;
                foreach (var standing in board)
                {
                    if (standing.Node.Era != era)
                    {
                        continue;
                    }

                    any = true;
                    grid.Add(BuildResearchCard(standing));
                }

                if (any)
                {
                    page.Add(section);
                }
            }

            return page;
        }

        private VisualElement BuildResearchCard(ResearchStanding standing)
        {
            var node = standing.Node;
            var card = new Button(() =>
            {
                simulation.TryStartResearch(node.Id, out _);
                Show(Screen.Research);
            });
            card.AddToClassList("card");
            CardArt.Apply(card, CardArt.ForEra(node.Era));

            if (standing.IsUnlocked)
            {
                card.AddToClassList("card--ahead");
            }
            else if (!standing.CanStart)
            {
                card.AddToClassList("card--locked");
            }

            var title = new Label(node.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var line = new Label(standing.IsUnlocked
                ? "COMPLETE"
                : standing.IsInProgress
                    ? "IN PROGRESS"
                    : $"{UiFormat.Money(node.CostUsd)}   {UiFormat.Days(standing.DurationDays)}   "
                      + $"{UiFormat.PetaflopDays(node.PetaflopDaysRequired)}");
            line.AddToClassList("card__line");
            card.Add(line);

            if (!standing.IsUnlocked && !standing.CanStart && !standing.IsInProgress)
            {
                var blocked = new Label(standing.BlockedReason);
                blocked.AddToClassList("card__line");
                blocked.style.whiteSpace = WhiteSpace.Normal;
                card.Add(blocked);
            }

            if (node.HasWarning)
            {
                var badge = new Label("ALERT");
                badge.AddToClassList("card__badge");
                card.Add(badge);
                card.tooltip = node.Warning;
            }
            else
            {
                card.tooltip = node.Description;
            }

            card.SetEnabled(standing.CanStart);
            return card;
        }

        private static string EraTitle(ResearchEra era) => era switch
        {
            ResearchEra.Foundations => "ERA 1   FOUNDATIONS   2022 TO 2023",
            ResearchEra.Scaling => "ERA 2   THE SCALING RACE   2023 TO 2024",
            ResearchEra.Autonomy => "ERA 3   AUTONOMY   2024 TO 2025",
            _ => "ERA 4   SUPERINTELLIGENCE   2026 ONWARD"
        };

        /// <summary>
        /// Team and office on one screen, because they are one decision: desks cap headcount, so a
        /// lease signed months ago is what decides whether the person you need today can start.
        /// </summary>
        private VisualElement BuildTeamScreen()
        {
            var roster = state.Staff;
            var page = NewPage("TEAM",
                $"{roster.Headcount} of {roster.Desks} desks in {roster.OfficeDefinition.DisplayName}. "
                + $"Payroll {UiFormat.Money(roster.DailyPayrollUsd)} a day, rent "
                + $"{UiFormat.Money(roster.DailyRentUsd)} a day. Every role saturates, so a seventh "
                + "person in one discipline adds a fraction of what the second one did.");

            var effects = new VisualElement();
            effects.AddToClassList("panel");
            effects.Add(Row("Training outcome spread",
                $"{UiFormat.Percent(roster.OutcomeVarianceMultiplier())} of baseline"));
            effects.Add(Row("Cluster utilization", $"+{UiFormat.Percent(roster.UtilizationBonus())}"));
            effects.Add(Row("Data quality", $"x{UiFormat.Number(roster.DataQualityMultiplier(), 3)}"));
            effects.Add(Row("Incident risk", $"x{UiFormat.Number(roster.IncidentRiskMultiplier(), 2)}"));
            effects.Add(Row("Brand from the team", $"+{UiFormat.Number(roster.BrandBonus(), 3)}"));
            effects.Add(Row("Research pace", $"x{UiFormat.Number(roster.ResearchSpeedMultiplier(), 3)}"));
            page.Add(effects);

            var hiring = new VisualElement();
            hiring.AddToClassList("panel");
            var hiringHeading = new Label("HIRE");
            hiringHeading.AddToClassList("panel__heading");
            hiring.Add(hiringHeading);

            var hireGrid = new VisualElement();
            hireGrid.AddToClassList("grid");
            hiring.Add(hireGrid);

            foreach (var definition in StaffCatalog.All)
            {
                hireGrid.Add(BuildHireCard(definition));
            }

            page.Add(hiring);

            if (roster.Headcount > 0)
            {
                var team = new VisualElement();
                team.AddToClassList("panel");
                var teamHeading = new Label("ON THE PAYROLL");
                teamHeading.AddToClassList("panel__heading");
                team.Add(teamHeading);

                for (var index = 0; index < roster.Headcount; index++)
                {
                    var slot = index;
                    var hire = roster.Hires[index];
                    var row = new VisualElement();
                    row.AddToClassList("readout");
                    row.Add(new Label(
                        $"{StaffCatalog.Get(hire.Role).DisplayName}, skill {hire.Skill}, since {hire.StartedOn}"));

                    var release = new Button(() =>
                    {
                        simulation.TryLetGo(slot, out _);
                        Show(Screen.Team);
                    })
                    { text = $"{UiFormat.Money(hire.SalaryPerYearUsd)}/yr   LET GO" };
                    release.AddToClassList("button");
                    release.style.height = 28;
                    release.style.minWidth = 210;
                    row.Add(release);
                    team.Add(row);
                }

                page.Add(team);
            }

            var offices = new VisualElement();
            offices.AddToClassList("panel");
            var officeHeading = new Label("OFFICE");
            officeHeading.AddToClassList("panel__heading");
            offices.Add(officeHeading);

            var officeGrid = new VisualElement();
            officeGrid.AddToClassList("grid");
            offices.Add(officeGrid);

            foreach (var definition in OfficeCatalog.All)
            {
                officeGrid.Add(BuildOfficeCard(definition));
            }

            page.Add(offices);
            return page;
        }

        private VisualElement BuildHireCard(StaffRoleDefinition definition)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.style.height = 176;

            var title = new Label(definition.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var count = new Label($"{state.Staff.CountOf(definition.Role)} on the team");
            count.AddToClassList("card__line");
            card.Add(count);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6;

            // One button per skill level, so the salary curve is visible at the point of decision.
            for (var skill = 1; skill <= StaffLimits.MaximumSkill; skill++)
            {
                var level = skill;
                var button = new Button(() =>
                {
                    simulation.TryHire(definition.Role, level, out _);
                    Show(Screen.Team);
                })
                { text = level.ToString() };
                button.AddToClassList("button");
                button.style.height = 30;
                button.style.minWidth = 40;
                button.style.marginRight = 4;
                button.tooltip =
                    $"Skill {level}: {UiFormat.Money(definition.SalaryPerYearUsd(level))} a year, "
                    + $"{UiFormat.Money(definition.HiringCostUsd_ForSkill(level))} to hire.";
                button.SetEnabled(state.Staff.HasFreeDesk
                    && state.CashUsd >= definition.HiringCostUsd_ForSkill(level));
                row.Add(button);
            }

            card.Add(row);
            card.tooltip = definition.Description;
            return card;
        }

        private VisualElement BuildOfficeCard(OfficeDefinition definition)
        {
            var current = state.Staff.Office == definition.Tier;
            var card = new Button(() =>
            {
                simulation.TryMoveOffice(definition.Tier, out _);
                Show(Screen.Team);
            });
            card.AddToClassList("card");
            card.EnableInClassList("card--ahead", current);

            var title = new Label(definition.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var desks = new Label($"{definition.Desks} DESKS   x{UiFormat.Number(definition.EffectivenessMultiplier, 2)}");
            desks.AddToClassList("card__line");
            card.Add(desks);

            var cost = new Label(current
                ? "CURRENT"
                : $"{UiFormat.Money(definition.MonthlyRentUsd)}/month   {UiFormat.Money(definition.FitOutCostUsd)} to move");
            cost.AddToClassList("card__line");
            card.Add(cost);

            card.tooltip = definition.Description;
            card.SetEnabled(!current);
            return card;
        }

        /// <summary>
        /// The fleet. Rented capacity on top, owned batches below with what each one is worth now
        /// against what it cost, which is the number the whole hardware design exists to show.
        /// </summary>
        private VisualElement BuildFleetScreen()
        {
            var profile = simulation.Profile;
            var market = simulation.Market;

            var page = NewPage("FLEET",
                $"{UiFormat.Petaflops(profile.RawPetaflops)} nameplate, "
                + $"{UiFormat.Petaflops(profile.EffectivePetaflops)} usable. "
                + $"{UiFormat.Money(profile.DailyOperatingCostUsd is var c ? (long)c : 0)} a day to run, "
                + $"{UiFormat.Money((long)profile.DailyDepreciationUsd)} a day in value lost.");

            var rental = new VisualElement();
            rental.AddToClassList("panel");
            var rentalHeading = new Label("RENTED CAPACITY");
            rentalHeading.AddToClassList("panel__heading");
            rental.Add(rentalHeading);

            var rentedLabel = new Label();
            rentedLabel.AddToClassList("field__label");
            rental.Add(rentedLabel);

            var rentedSlider = new Slider(0f, 40000f) { value = (float)state.Pool.RentedPetaflops };
            rentedSlider.AddToClassList("field");
            rentedSlider.RegisterValueChangedCallback(evt =>
            {
                simulation.SetRentedPetaflops(evt.newValue);
                Show(Screen.Fleet);
            });
            rental.Add(rentedSlider);

            rentedLabel.text =
                $"{UiFormat.Petaflops(state.Pool.RentedPetaflops)} at "
                + $"{UiFormat.Money((long)market.RentPricePerPetaflopDayUsd)} per PF-day, currently "
                + $"{HardwareCatalog.Get(market.RentableGeneration).DisplayName}";

            rental.Add(Hint(
                "Contracted in petaflops, not boxes, so the bill does not move when the clouds change "
                + "generation. It never ages and it bills every day it is held."));
            page.Add(rental);

            var ladder = new VisualElement();
            ladder.AddToClassList("panel");
            var ladderHeading = new Label("COMPUTE TIERS");
            ladderHeading.AddToClassList("panel__heading");
            ladder.Add(ladderHeading);

            foreach (var status in state.ComputeTierLadder())
            {
                var definition = ComputeTierCatalog.Get(status.Tier);
                var row = new VisualElement();
                row.AddToClassList("readout");
                row.Add(new Label(definition.DisplayName));

                var value = new Label(status.IsUnlocked ? "OPEN" : status.LockReason);
                value.AddToClassList("readout__value");
                value.style.whiteSpace = WhiteSpace.Normal;
                value.style.maxWidth = 620;
                if (!status.IsUnlocked)
                {
                    value.AddToClassList("readout__value--warn");
                }

                row.Add(value);
                ladder.Add(row);
            }

            page.Add(ladder);

            var owned = new VisualElement();
            owned.AddToClassList("panel");
            var ownedHeading = new Label("OWNED HARDWARE");
            ownedHeading.AddToClassList("panel__heading");
            owned.Add(ownedHeading);

            if (state.Pool.Assets.Count == 0)
            {
                owned.Add(Hint("Nothing owned. Everything is rented, which is the right answer until "
                    + "the cluster is busy enough to justify the capital."));
            }
            else
            {
                for (var index = 0; index < state.Pool.Assets.Count; index++)
                {
                    var slot = index;
                    var asset = state.Pool.Assets[index];
                    var generation = HardwareCatalog.Get(asset.GenerationId);
                    var residual = HardwareValuation.ResidualValueUsd(asset, state.Date);
                    var paid = asset.TotalPurchasePriceUsd;
                    var kept = paid <= 0 ? 0.0 : residual / (double)paid;

                    var row = new VisualElement();
                    row.AddToClassList("readout");
                    row.Add(new Label($"{asset.Units:N0}x {generation.DisplayName}, bought {asset.PurchaseDate}"
                        + (asset.IsOnline(state.Date) ? string.Empty : $" (arrives in {asset.DaysUntilOnline(state.Date)}d)")));

                    var right = new VisualElement();
                    right.style.flexDirection = FlexDirection.Row;
                    right.style.alignItems = Align.Center;

                    var worth = new Label($"{UiFormat.Money(residual)} of {UiFormat.Money(paid)}  ({UiFormat.Percent(kept, 0)})");
                    worth.AddToClassList("readout__value");
                    worth.AddToClassList(kept < 0.4 ? "readout__value--bad" : "readout__value--good");
                    worth.style.marginRight = 10;
                    right.Add(worth);

                    var sell = new Button(() =>
                    {
                        simulation.TrySellHardware(slot, asset.Units, out _, out _);
                        Show(Screen.Fleet);
                    })
                    { text = "SELL" };
                    sell.AddToClassList("button");
                    sell.style.height = 28;
                    sell.style.minWidth = 90;
                    right.Add(sell);

                    row.Add(right);
                    owned.Add(row);
                }
            }

            page.Add(owned);

            var buy = new VisualElement();
            buy.AddToClassList("panel");
            var buyHeading = new Label("BUY");
            buyHeading.AddToClassList("panel__heading");
            buy.Add(buyHeading);

            var buyGrid = new VisualElement();
            buyGrid.AddToClassList("grid");
            buy.Add(buyGrid);

            var tier = state.IsTierUnlocked(ComputeTier.OwnDatacenter) && state.IsDatacenterOnline
                ? ComputeTier.OwnDatacenter
                : ComputeTier.ColocatedServers;

            foreach (var generation in HardwareCatalog.AvailableOn(state.Date, HardwareClass.Accelerator))
            {
                buyGrid.Add(BuildHardwareCard(generation, tier));
            }

            page.Add(buy);
            return page;
        }

        private VisualElement BuildHardwareCard(HardwareGeneration generation, ComputeTier tier)
        {
            const int batch = 64;
            var card = new Button(() =>
            {
                simulation.TryBuyHardware(generation.Id, batch, tier, out _);
                Show(Screen.Fleet);
            });
            card.AddToClassList("card");
            CardArt.Apply(card, CardArt.ForHardware(generation.Class));

            var unlocked = state.IsTierUnlocked(tier);
            if (!unlocked)
            {
                card.AddToClassList("card--locked");
            }

            var title = new Label(generation.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var spec = new Label($"{UiFormat.Number(generation.PetaflopsPerUnit, 2)} PF   "
                + $"{generation.MemoryGigabytes} GB   {UiFormat.Number(generation.PowerKilowatts, 2)} kW");
            spec.AddToClassList("card__line");
            card.Add(spec);

            var price = new Label($"BUY {batch}   {UiFormat.Money(generation.LaunchPriceUsd * batch)} at list");
            price.AddToClassList("card__line");
            card.Add(price);

            if (generation.IsProjection)
            {
                var badge = new Label("PROJECTED");
                badge.AddToClassList("card__badge");
                card.Add(badge);
            }

            card.SetEnabled(unlocked);
            card.tooltip = generation.IsProjection
                ? "Roadmap extrapolation, not a shipped product."
                : $"Shipped {generation.ReleaseDate}.";
            return card;
        }

        /// <summary>
        /// Pricing, the free tier and marketing. The free tier slider is the most dangerous control
        /// in the game and the screen says so with a number rather than a warning.
        /// </summary>
        private VisualElement BuildBusinessScreen()
        {
            var policy = state.Monetization;
            var market = simulation.Market;

            var page = NewPage("BUSINESS",
                $"Market rate today is {UiFormat.Money((long)(market.PricePerMillionTokensUsd * 1000))} "
                + "per billion tokens. What you charge against that decides your share; what you give "
                + "away decides how much of what you serve is worth anything.");

            var pricing = new VisualElement();
            pricing.AddToClassList("panel");
            var pricingHeading = new Label("PRICING");
            pricingHeading.AddToClassList("panel__heading");
            pricing.Add(pricingHeading);

            var modelRow = new VisualElement();
            modelRow.style.flexDirection = FlexDirection.Row;
            foreach (PricingModel option in Enum.GetValues(typeof(PricingModel)))
            {
                var captured = option;
                var button = new Button(() =>
                {
                    policy.Model = captured;
                    Show(Screen.Business);
                })
                { text = MonetizationCatalog.PricingName(option).ToUpperInvariant() };
                button.AddToClassList("button");
                button.style.marginRight = 8;
                button.SetEnabled(policy.Model != option);
                modelRow.Add(button);
            }

            pricing.Add(modelRow);

            if (policy.Model == PricingModel.PayPerToken)
            {
                var priceLabel = new Label(
                    $"Your rate: x{UiFormat.Number(policy.PaidPriceMultiplier, 2)} of market "
                    + $"({UiFormat.Money((long)(policy.RatePerMillionTokensUsd(market.PricePerMillionTokensUsd) * 1000))} per billion)");
                priceLabel.AddToClassList("field__label");
                priceLabel.style.marginTop = 12;
                pricing.Add(priceLabel);

                var priceSlider = new Slider(0.1f, 3f) { value = (float)policy.PaidPriceMultiplier };
                priceSlider.AddToClassList("field");
                priceSlider.RegisterValueChangedCallback(evt =>
                {
                    policy.PaidPriceMultiplier = evt.newValue;
                    Show(Screen.Business);
                });
                pricing.Add(priceSlider);
                pricing.Add(Hint("Metered against the market. When token prices fall, so does your revenue "
                    + "per token, whether or not anything else changed."));
            }
            else if (policy.Model == PricingModel.Subscription)
            {
                var subLabel = new Label(
                    $"Monthly fee: {UiFormat.Money((long)policy.SubscriptionPriceUsdPerMonth)} "
                    + $"(works out at {UiFormat.Money((long)(policy.RatePerMillionTokensUsd(market.PricePerMillionTokensUsd) * 1000))} per billion tokens)");
                subLabel.AddToClassList("field__label");
                subLabel.style.marginTop = 12;
                pricing.Add(subLabel);

                var subSlider = new Slider(0f, 200f) { value = (float)policy.SubscriptionPriceUsdPerMonth };
                subSlider.AddToClassList("field");
                subSlider.RegisterValueChangedCallback(evt =>
                {
                    policy.SubscriptionPriceUsdPerMonth = evt.newValue;
                    Show(Screen.Business);
                });
                pricing.Add(subSlider);
                pricing.Add(Hint("A fee you set, decoupled from the market. It protects a good position "
                    + "when prices fall and traps a bad one when they do not."));
            }
            else
            {
                pricing.Add(Hint("Nobody pays. Reach is the highest it can be and revenue is zero. "
                    + "The serving bill is not."));
            }

            page.Add(pricing);

            var free = new VisualElement();
            free.AddToClassList("panel");
            var freeHeading = new Label("FREE TIER");
            freeHeading.AddToClassList("panel__heading");
            free.Add(freeHeading);

            var freeLabel = new Label(
                $"{UiFormat.Count(policy.FreeTierTokensPerUserPerDay)} tokens per free account per day");
            freeLabel.AddToClassList("field__label");
            free.Add(freeLabel);

            var freeSlider = new Slider(0f, (float)MonetizationCatalog.GenerousFreeTierTokensPerDay)
            {
                value = (float)policy.FreeTierTokensPerUserPerDay
            };
            freeSlider.AddToClassList("field");
            freeSlider.SetEnabled(policy.Model != PricingModel.FreeOnly);
            freeSlider.RegisterValueChangedCallback(evt =>
            {
                policy.FreeTierTokensPerUserPerDay = evt.newValue;
                Show(Screen.Business);
            });
            free.Add(freeSlider);

            free.Add(Row("Reach", $"x{UiFormat.Number(policy.ReachMultiplier, 2)} of your normal share"));

            var givenAway = new VisualElement();
            givenAway.AddToClassList("readout");
            givenAway.Add(new Label("Served for nothing"));
            var givenValue = new Label(UiFormat.Percent(policy.FreeShareOfTokens));
            givenValue.AddToClassList("readout__value");
            givenValue.AddToClassList(policy.FreeShareOfTokens > 0.45 ? "readout__value--bad" : "readout__value--warn");
            givenAway.Add(givenValue);
            free.Add(givenAway);

            free.Add(Row("Given away yesterday",
                $"{UiFormat.Billions(state.FreeTokensServedBillions)} tokens"));
            free.Add(Row("Given away in total",
                $"{UiFormat.Billions(state.LifetimeFreeTokensBillions)} tokens"));

            free.Add(Hint("Serving capacity does not care which kind of token it is producing and "
                + "neither does the bill. A generous tier widens the funnel and can quietly turn most "
                + "of the fleet into a cost centre."));
            page.Add(free);

            page.Add(BuildCampaignPanel(CampaignKind.Company, "COMPANY MARKETING",
                "Reputation, slowly, and it survives a model going out of date."));
            page.Add(BuildCampaignPanel(CampaignKind.Model, "MODEL MARKETING",
                "Attention on the current flagship. It stops working the day the invoices stop."));

            return page;
        }

        private VisualElement BuildCampaignPanel(CampaignKind kind, string heading, string blurb)
        {
            var policy = state.Monetization;
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var label = new Label(heading);
            label.AddToClassList("panel__heading");
            panel.Add(label);

            var current = kind == CampaignKind.Company
                ? policy.CompanyMarketingDailyUsd
                : policy.ModelMarketingDailyUsd;

            panel.Add(Row("Spending now", $"{UiFormat.Money(current)} a day"));
            if (kind == CampaignKind.Model)
            {
                panel.Add(Row("Awareness held", $"+{UiFormat.Number(policy.ModelAwareness, 3)} brand"));
            }

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            panel.Add(grid);

            var stop = new Button(() =>
            {
                if (kind == CampaignKind.Company)
                {
                    policy.CompanyMarketingDailyUsd = 0;
                }
                else
                {
                    policy.ModelMarketingDailyUsd = 0;
                }

                Show(Screen.Business);
            })
            { text = "STOP" };
            stop.AddToClassList("card");
            stop.Add(new Label("NOTHING"));
            grid.Add(stop);

            foreach (var campaign in MonetizationCatalog.OfKind(kind))
            {
                var captured = campaign;
                var card = new Button(() =>
                {
                    if (kind == CampaignKind.Company)
                    {
                        policy.CompanyMarketingDailyUsd = captured.DailyBudgetUsd;
                    }
                    else
                    {
                        policy.ModelMarketingDailyUsd = captured.DailyBudgetUsd;
                    }

                    Show(Screen.Business);
                });
                card.AddToClassList("card");
                card.EnableInClassList("card--ahead", current == campaign.DailyBudgetUsd);

                var title = new Label(campaign.DisplayName.ToUpperInvariant());
                title.AddToClassList("card__title");
                card.Add(title);

                var cost = new Label($"{UiFormat.Money(campaign.DailyBudgetUsd)}/day   "
                    + $"{UiFormat.Money(campaign.MonthlyBudgetUsd)}/month");
                cost.AddToClassList("card__line");
                card.Add(cost);

                card.tooltip = campaign.Description;
                card.SetEnabled(state.Date.IsOnOrAfter(campaign.EarliestDate));
                grid.Add(card);
            }

            panel.Add(Hint(blurb));
            return panel;
        }

        private VisualElement BuildReleaseScreen()
        {
            var page = NewPage("RELEASE",
                "Finished runs wait here. Waiting costs nothing directly, and costs position every day: "
                + "market par keeps rising under a model that has not shipped.");

            if (state.Shelf.Count == 0)
            {
                page.Add(Hint("Nothing on the shelf. A run has to finish before there is a decision to make."));
                return page;
            }

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            page.Add(grid);

            for (var index = 0; index < state.Shelf.Count; index++)
            {
                var slot = index;
                var shelved = state.Shelf[index];
                var card = new Button(() =>
                {
                    simulation.TryReleaseModel(slot, state.DefaultPriceMultiplier, out _);
                    Show(Screen.Release);
                });
                card.AddToClassList("card");

                var title = new Label(shelved.Name.ToUpperInvariant());
                title.AddToClassList("card__title");
                card.Add(title);

                var scoreLine = new Label(
                    $"SHIPS AT {UiFormat.Number(shelved.CapabilityIfReleasedOn(state.Date))}  (was {UiFormat.Number(shelved.Capability)})");
                scoreLine.AddToClassList("card__line");
                card.Add(scoreLine);

                var waitLine = new Label(
                    $"{shelved.DaysOnShelf(state.Date)} days on the shelf, frontier {UiFormat.Number(simulation.Market.FrontierCapability)}");
                waitLine.AddToClassList("card__line");
                card.Add(waitLine);

                grid.Add(card);
            }

            return page;
        }

        private VisualElement BuildFundingScreen()
        {
            var capTable = state.CapTable;
            var page = NewPage("FUNDING",
                $"Founders hold {UiFormat.Percent(capTable.FounderEquity)} after {capTable.RoundCount} round(s). "
                + $"Raised {UiFormat.Money(capTable.TotalRaisedUsd)} in total. "
                + $"Investor mood is {FundingCatalog.SentimentLabel(FundingCatalog.SentimentOn(state.Date))}.");

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            page.Add(panel);

            panel.Add(Row("Company valuation", UiFormat.Money(simulation.CurrentValuationUsd())));
            panel.Add(Row("Annual revenue run rate", UiFormat.Money(state.AnnualRevenueRunRateUsd)));
            panel.Add(Row("Founder stake worth",
                UiFormat.Money(capTable.FounderStakeValueUsd(simulation.CurrentValuationUsd()))));

            var offer = state.CurrentFundingOffer;
            if (offer.IsOpen)
            {
                var definition = FundingCatalog.Get(offer.Stage);
                panel.Add(Row($"{definition.DisplayName} on the table",
                    $"{UiFormat.Money(offer.RaiseUsd)} for {UiFormat.Percent(offer.EquitySold)}"));
                panel.Add(Row("Term sheet expires", $"in {offer.DaysRemaining(state.Date)} days"));

                var sign = new Button(() =>
                {
                    simulation.TryAcceptFundingOffer(out _);
                    Show(Screen.Funding);
                })
                { text = offer.IsDownRound ? "SIGN THE DOWN ROUND" : "SIGN THE TERM SHEET" };
                sign.AddToClassList("button");
                sign.AddToClassList("button--primary");
                sign.style.marginTop = 14;
                panel.Add(sign);
            }
            else
            {
                var availability = simulation.NextRoundAvailability();
                var open = new Button(() =>
                {
                    simulation.TryOpenFundingRound(out _);
                    Show(Screen.Funding);
                })
                { text = "OPEN A ROUND" };
                open.AddToClassList("button");
                open.SetEnabled(availability.IsAvailable);
                open.style.marginTop = 14;
                panel.Add(open);

                if (!availability.IsAvailable)
                {
                    panel.Add(Hint(availability.Reason));
                }
            }

            return page;
        }

        private VisualElement BuildRankingScreen()
        {
            var page = NewPage("RANKING",
                "Capability, market share and brand, weighted. Every number here is the same one the "
                + "revenue side runs on, so a position on this board and an income statement cannot disagree.");

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            page.Add(panel);

            foreach (var entry in simulation.Ranking())
            {
                var row = new VisualElement();
                row.AddToClassList("readout");

                var name = new Label($"{entry.Position}.  {entry.LabName}  -  {entry.ModelName}");
                if (entry.IsPlayer)
                {
                    name.AddToClassList("readout__value");
                }

                row.Add(name);
                row.Add(new Label(
                    $"{UiFormat.Number(entry.Score)}   cap {UiFormat.Number(entry.Capability)}   share {UiFormat.Percent(entry.MarketShare, 2)}"));
                panel.Add(row);
            }

            return page;
        }

        private VisualElement BuildFeedScreen()
        {
            var page = NewPage("INTELLIGENCE",
                "What the research desk believes is coming. Confidence is what the desk claims about "
                + "itself, and it is always higher than how often the desk turns out to be right.");

            var tiers = new VisualElement();
            tiers.AddToClassList("panel");
            page.Add(tiers);

            foreach (IntelTier tier in Enum.GetValues(typeof(IntelTier)))
            {
                var captured = tier;
                var button = new Button(() =>
                {
                    simulation.SetIntelSubscription(captured);
                    Show(Screen.Feed);
                })
                {
                    text = tier == IntelTier.PublicNews
                        ? "PUBLIC NEWS (FREE)"
                        : $"{tier.ToString().ToUpperInvariant()}  {UiFormat.Money(IntelligenceService.MonthlyRetainerUsd(tier))}/MONTH"
                };
                button.AddToClassList("button");
                button.style.marginBottom = 8;
                button.style.width = Length.Percent(100);
                button.SetEnabled(state.IntelSubscription != tier);
                tiers.Add(button);
            }

            var feed = new VisualElement();
            feed.AddToClassList("panel");
            page.Add(feed);

            var signals = state.Signals;
            if (signals.Count == 0)
            {
                feed.Add(Hint("No notes yet. A desk on retainer files its first one within a few weeks."));
                return page;
            }

            for (var index = signals.Count - 1; index >= 0 && index > signals.Count - 15; index--)
            {
                var signal = signals[index];
                var row = new VisualElement();
                row.AddToClassList("panel");
                row.Add(new Label($"{signal.IssuedOn}   {signal.Headline}"));

                var detail = new Label(signal.Detail);
                detail.AddToClassList("field__hint");
                row.Add(detail);

                var meta = new Label($"{signal.Tier}, desk confidence {UiFormat.Percent(signal.Confidence, 0)}");
                meta.AddToClassList("field__hint");
                row.Add(meta);

                feed.Add(row);
            }

            return page;
        }

        private static VisualElement NewPage(string title, string subtitle)
        {
            var page = new VisualElement();
            page.AddToClassList("content");

            var heading = new Label(title);
            heading.AddToClassList("page-title");
            page.Add(heading);

            var sub = new Label(subtitle);
            sub.AddToClassList("page-subtitle");
            page.Add(sub);

            return page;
        }

        private static VisualElement Row(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("readout");
            row.Add(new Label(label));

            var valueLabel = new Label(value);
            valueLabel.AddToClassList("readout__value");
            row.Add(valueLabel);

            return row;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("field__hint");
            return label;
        }

        private void DrainEvents()
        {
            while (state.TryDequeueEvent(out var companyEvent))
            {
                recentEvents.Add(companyEvent);
                if (recentEvents.Count > 60)
                {
                    recentEvents.RemoveAt(0);
                }
            }
        }

        private void RefreshChrome()
        {
            cashLabel.text = UiFormat.Money(state.CashUsd);
            valuationLabel.text = $"valued {UiFormat.Money(simulation.CurrentValuationUsd())}";
            companyLabel.text = state.CompanyName;
            dateLabel.text = state.Date.ToString();

            var position = RankingBoard.PlayerPosition(simulation.Ranking());
            rankLabel.text = position > 0 ? $"rank #{position}" : "unranked";

            hud.Refresh(state.Date, clock.Speed, clock.DayProgress);
        }
    }
}
