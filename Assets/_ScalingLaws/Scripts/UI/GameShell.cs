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
    /// The shell: money and date across the top, a rail of screens down the left, a speed toolbar
    /// along the bottom. The layout the tycoon games this borrows from all share, because it works.
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
        private readonly List<Button> railButtons = new();
        private readonly List<CompanyEvent> recentEvents = new();

        private Screen current = Screen.Create;

        private enum Screen
        {
            Create,
            Research,
            Family,
            Upgrade,
            Release,
            Funding,
            Ranking,
            Feed
        }

        private void OnEnable()
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

            if (theme != null)
            {
                root.styleSheets.Add(theme);
            }

            root.AddToClassList("root");

            root.Add(BuildTopBar());

            var shell = new VisualElement();
            shell.AddToClassList("shell");
            root.Add(shell);

            shell.Add(BuildRail());

            contentHost = new VisualElement();
            contentHost.style.flexGrow = 1;
            shell.Add(contentHost);

            root.Add(BuildToolbar());
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
            bar.Add(right);

            return bar;
        }

        private VisualElement BuildRail()
        {
            var rail = new VisualElement();
            rail.AddToClassList("rail");

            var brand = new Label("MY MODEL");
            brand.AddToClassList("rail__brand");
            rail.Add(brand);

            var subtitle = new Label(companyName);
            subtitle.AddToClassList("rail__subtitle");
            rail.Add(subtitle);

            AddRailItem(rail, "NEW MODEL", Screen.Create);
            AddRailItem(rail, "RESEARCH", Screen.Research);
            AddRailItem(rail, "ARCHITECTURE", Screen.Family);
            AddRailItem(rail, "UPGRADE MODEL", Screen.Upgrade);
            AddRailItem(rail, "RELEASE", Screen.Release);
            AddRailItem(rail, "FUNDING", Screen.Funding);
            AddRailItem(rail, "RANKING", Screen.Ranking);
            AddRailItem(rail, "INTELLIGENCE", Screen.Feed);

            return rail;
        }

        private void AddRailItem(VisualElement rail, string label, Screen screen)
        {
            var button = new Button(() => Show(screen)) { text = label };
            button.AddToClassList("rail__item");
            button.userData = screen;
            railButtons.Add(button);
            rail.Add(button);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            AddSpeedButton(toolbar, "II", SimSpeed.Paused);
            AddSpeedButton(toolbar, ">", SimSpeed.Slow);
            AddSpeedButton(toolbar, ">>", SimSpeed.Normal);
            AddSpeedButton(toolbar, ">>>", SimSpeed.Fast);
            AddSpeedButton(toolbar, "MAX", SimSpeed.Turbo);

            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar__spacer");
            toolbar.Add(spacer);

            var save = new Button(() =>
            {
                SaveStore.Save(state);
                daysSinceAutoSave = 0;
            })
            { text = "SAVE" };
            save.AddToClassList("button");
            save.style.minWidth = 110;
            save.style.height = 40;
            save.style.marginRight = 8;
            toolbar.Add(save);

            var menu = new Button(() =>
            {
                if (!state.IsBankrupt)
                {
                    SaveStore.Save(state);
                }

                SceneFlow.LoadMainMenu();
            })
            { text = "MENU" };
            menu.AddToClassList("button");
            menu.style.minWidth = 110;
            menu.style.height = 40;
            toolbar.Add(menu);

            return toolbar;
        }

        /// <summary>
        /// The run is over. The save is already cleared by the caller, so this is a summary and a way
        /// back out, not a screen to be rescued from.
        /// </summary>
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

        private void AddSpeedButton(VisualElement toolbar, string label, SimSpeed speed)
        {
            var button = new Button(() => clock.Speed = speed) { text = label };
            button.AddToClassList("toolbar__button");
            toolbar.Add(button);
        }

        private void Show(Screen screen)
        {
            current = screen;
            contentHost.Clear();

            foreach (var button in railButtons)
            {
                var isActive = button.userData is Screen value && value == screen;
                button.EnableInClassList("rail__item--active", isActive);
            }

            switch (screen)
            {
                case Screen.Create:
                    creator.Refresh();
                    contentHost.Add(creator.Root);
                    break;
                case Screen.Research:
                    contentHost.Add(BuildResearchScreen());
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
        }
    }
}
