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
        private ManagementScreen management;
        private NewsScreen news;
        private NewsBanner newsBanner;
        private MailScreen mail;
        private OfficeChooser offices;
        private VisualElement bannerStack;

        /// <summary>
        /// One banner per product on sale, after the lead one.
        ///
        /// Rebuilt only when the set of marketed models changes, never per frame: they are compared
        /// by the model each describes rather than by count, because superseding one line while
        /// shipping in another leaves the count the same and the products different.
        /// </summary>
        private readonly List<ModelBanner> followerBanners = new();

        private readonly List<DeployedModel> followerModels = new();
        private int daysSinceAutoSave;
        private bool gameOverShown;

        private VisualElement contentHost;
        private Label cashLabel;
        private Label valuationLabel;
        private Label companyLabel;
        private Label dateLabel;
        private Label rankLabel;
        private GameHud hud;
        private KeyboardShortcuts shortcuts;
        private ResearchNodeId selectedResearch = ResearchNodeId.None;
        private string researchProblem = string.Empty;
        private bool cancelArmed;
        private VisualElement trainingBanner;
        private VisualElement pulseBanner;
        private Button cashButton;
        private Label reputationLabel;
        private Button pointsButton;
        private VisualElement researchCard;
        private VisualElement labCard;
        private VisualElement runFinished;
        private readonly List<MarketingChannel> pickedChannels = new();
        private AudienceSegment pickedAudience = AudienceSegment.Consumer;
        private int pickedTerm = 3;
        private VisualElement shellRoot;
        private ResearchBubbles bubbles;
        private Label pointsLabel;
        private Label fansLabel;
        private ModelBanner modelBanner;

        /// <summary>
        /// One net figure a day for the month so far, for the banner's chart.
        ///
        /// Read from the ledger rather than accumulated separately, so the bars and the finance
        /// report are the same numbers at two sizes.
        /// </summary>
        /// <summary>
        /// Whichever long job is running, described the one way the banner understands.
        ///
        /// Training first when both are somehow live, because it is the one with money burning
        /// against it every day.
        /// </summary>
        private WorkInFlight WorkInFlightNow()
        {
            var state = simulation.State;

            if (state.ActiveRun != null)
            {
                var run = state.ActiveRun;
                var progress = Math.Clamp(run.Progress, 0.0, 1.0);
                var left = run.PetaflopDaysRequired - run.PetaflopDaysCompleted;
                var perDay = Math.Max(1.0, simulation.Profile.EffectivePetaflops);

                return new WorkInFlight("TRAINING MODEL", run.Blueprint.Name, progress,
                    (int)Math.Ceiling(Math.Max(0.0, left) / perDay));
            }

            if (state.ActiveResearch != null)
            {
                var project = state.ActiveResearch;
                var node = ResearchTree.Get(project.Node);
                var elapsed = state.Date.DayIndex - project.StartedOn.DayIndex;

                return new WorkInFlight("RESEARCHING", node.DisplayName,
                    Math.Clamp(project.Progress, 0.0, 1.0),
                    Math.Max(0, project.DurationDays - elapsed));
            }

            return WorkInFlight.Idle;
        }

        private IReadOnlyList<long> DailyNetSeries()
        {
            var ledger = simulation.State.Ledger;
            var today = simulation.State.Date.Day;
            var series = new List<long>(today);

            for (var day = 1; day <= today; day++)
            {
                var net = 0L;
                foreach (var info in Ledger.Lines)
                {
                    if (!info.IsCash)
                    {
                        continue;
                    }

                    var amount = ledger.DayTotal(day, info.Line);
                    net += info.IsIncome ? amount : -amount;
                }

                series.Add(net);
            }

            return series;
        }
        private Label cashArrows;
        private FinanceReport financeReport;
        private VisualElement financeHost;
        private Label pulseUsers;
        private Label pulseMood;
        private Label pulseSatisfaction;
        private Label pulseArrows;

        /// <summary>Seconds the opening reveal holds before the office takes the screen back.</summary>
        public const float CompanyInfoRevealSeconds = 3f;

        private bool companyInfoOpen = true;
        private float companyInfoTimer = CompanyInfoRevealSeconds;
        private VisualElement trainingFill;
        private Label trainingLabel;
        private Label trainingDays;
        private readonly List<CompanyEvent> recentEvents = new();

        private Screen current = Screen.Site;

        private enum Screen
        {
            Site,
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
            Feed,

            Marketing,

            /// <summary>
            /// The product's own page and the desk behind it. Reached from the corner banner rather
            /// than the bar, because it is about the thing on sale and the banner is where the thing
            /// on sale already lives.
            /// </summary>
            Management,

            /// <summary>The inbox. Demands, applications and everything waiting on an answer.</summary>
            Mail,

            /// <summary>The places the company can be. First piece of the second map.</summary>
            Offices,

            /// <summary>The wire. Reached from its own corner banner and from the bottom bar.</summary>
            News
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
            creator.started += () => Show(Screen.Site);
            upgrades = new UpgradeGridPanel(simulation);
            families = new ArchitectureCreatorPanel(simulation);

            BuildTree();

            // The campaign opens on the office rather than on a form. It is the only screen that
            // shows the company as a place instead of as a number, and it is what a new player
            // should be looking at in the first ten seconds.
            Show(Screen.Site);
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
            shortcuts?.Poll();

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

            // The opening reveal. A new campaign shows who the company is, then gets out of the way
            // so the room is the thing on screen. It is a countdown rather than a one shot because
            // the player can dismiss it early by opening it again.
            if (companyInfoTimer > 0f)
            {
                companyInfoTimer -= Time.unscaledDeltaTime;
                if (companyInfoTimer <= 0f && companyInfoOpen)
                {
                    companyInfoOpen = false;
                    hud.SetCompanyInfoOpen(false);
                    if (current == Screen.Site)
                    {
                        Show(Screen.Site);
                    }
                }
            }

            var days = clock.Advance(Time.unscaledDeltaTime);

            // The clock has to be pushed every frame, not once a day. It used to be refreshed only
            // inside the branch below, which runs when a day rolls over, so the dial and the line
            // along the bottom edge were redrawn at exactly the moment they reset to zero. Both
            // looked frozen, and the game looked paused while it was running.
            hud.Refresh(state.Date, clock.Speed, clock.DayProgress);


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

            // Kept so panels that float over everything, like the research card, have somewhere to
            // attach that is not whichever screen happens to be open.
            shellRoot = root;
            root.Clear();

            UiBootstrap.Prepare(root, theme);
            root.AddToClassList("root");

            root.Add(BuildTopBar());

            var shell = new VisualElement();
            shell.AddToClassList("shell");
            root.Add(shell);

            contentHost = new VisualElement();
            contentHost.AddToClassList("content-host");
            contentHost.style.flexGrow = 1;
            shell.Add(contentHost);

            hud = new GameHud(SetSpeed, SkipDay, ToggleCompanyInfo);
            shortcuts = new KeyboardShortcuts(root, () => clock.Speed, SetSpeed);
            AddHudSlots();
            root.Add(hud.Root);

            // Read through a function rather than captured once. Loading a save replaces the state
            // object, and a report holding the old ledger would show the books of a game that is no
            // longer being played while insisting they were current.
            financeReport = new FinanceReport(() => simulation.State.Ledger,
                () => simulation.State.Date, ToggleFinanceReport);

            financeHost = new VisualElement();
            financeHost.AddToClassList("finance-host");
            financeHost.style.display = DisplayStyle.None;
            financeHost.Add(financeReport.Root);
            root.Add(financeHost);

            // The product banner replaces the pulse counter in the corner. It answers the same
            // question and three more, and two banners fighting for one corner is one too many.
            modelBanner = new ModelBanner(
                () => simulation.Product(),
                WorkInFlightNow,
                DailyNetSeries,
                () => Show(Screen.Management));

            management = new ManagementScreen(simulation,
                () => Show(Screen.Release),
                () => Show(Screen.Marketing),
                () => Show(Screen.Fleet),
                () => Show(Screen.Upgrade));

            mail = new MailScreen(simulation, RefreshChrome);

            offices = new OfficeChooser(
                () => simulation.State,
                tier => simulation.TryMoveOffice(tier, out var why) ? string.Empty : why,
                () => Show(Screen.Team));

            news = new NewsScreen(simulation, (tier, joined) =>
            {
                simulation.SetIntelSubscription(tier, joined);
                Show(Screen.News);
            });

            // Read through a function rather than captured, same as the finance report: the feed
            // object survives a load but the state around it is replaced.
            newsBanner = new NewsBanner(() => simulation.State.News, () => Show(Screen.News));
            root.Add(newsBanner.Root);

            // The corner is a stack now. One product on sale is one banner, because a company
            // running three lines is running three products and a single panel describing only the
            // strongest was hiding two of them.
            bannerStack = new VisualElement();
            bannerStack.AddToClassList("mb-stack");
            bannerStack.Add(modelBanner.Root);
            root.Add(bannerStack);

            RefreshChrome();
        }

        /// <summary>
        /// Which way the money is going, in the same arrows the people counter uses.
        ///
        /// Measured on this month's cash flow so far against the month before it, because a single
        /// day swings on whether an invoice happened to land, and a player glancing at the corner
        /// wants the trend rather than yesterday.
        /// </summary>
        private void RefreshCashArrows(CompanyState state)
        {
            if (cashArrows == null)
            {
                return;
            }

            var ledger = state.Ledger;
            var thisMonth = Ledger.MonthKeyOf(state.Date);
            var flow = ledger.MonthCashFlow(thisMonth);

            // There has to be a previous month to compare against. Without this the first month of
            // the game compared itself to nothing, so a single day of ordinary costs read as the
            // largest possible collapse and the corner showed three red arrows on day one.
            var recorded = ledger.RecordedMonths();
            var hasHistory = recorded.Count > 1 && recorded.Contains(thisMonth - 1);
            var before = hasHistory ? ledger.MonthCashFlow(thisMonth - 1) : 0L;

            // Measured against the company's own scale, so a small firm does not show three arrows
            // over a rounding error, and clamped so one enormous month cannot pin it forever.
            var scale = Math.Max(Math.Abs(before), Math.Abs(state.CashUsd) * 0.02);
            var momentum = scale <= 0.0
                ? 0.0
                : Math.Clamp((flow - before) / scale, -1.0, 1.0);

            var steps = (int)(Math.Abs(momentum) / 0.2);
            var arrows = hasHistory ? Math.Sign(momentum) * Math.Clamp(steps, 0, 3) : 0;

            cashArrows.text = arrows == 0
                ? "="
                : new string(arrows > 0 ? '▲' : '▼', Math.Abs(arrows));

            cashArrows.EnableInClassList("topbar__arrows--up", arrows > 0);
            cashArrows.EnableInClassList("topbar__arrows--down", arrows < 0);
            cashArrows.EnableInClassList("topbar__arrows--flat", arrows == 0);

            cashButton.tooltip = flow >= 0
                ? $"This month is up {UiFormat.Money(flow)} so far. Click for the books."
                : $"This month is down {UiFormat.Money(Math.Abs(flow))} so far. Click for the books.";
        }

        /// <summary>
        /// Standing in the header: what the public thinks, and how many of them follow the company.
        ///
        /// Two numbers rather than one because they behave differently. Reputation is an opinion and
        /// moves in days; fans are a stock and move in months. The tooltip names whichever driver is
        /// currently doing the most, so a falling number is never a mystery.
        /// </summary>
        private void RefreshStanding(CompanyState state)
        {
            if (reputationLabel == null)
            {
                return;
            }

            var change = state.LastStandingChange;

            reputationLabel.text = "REP " + UiFormat.Percent(state.Reputation, 0);
            reputationLabel.EnableInClassList("topbar__standing--up", change.Total > 0.0);
            reputationLabel.EnableInClassList("topbar__standing--down", change.Total < 0.0);

            reputationLabel.tooltip = change.Total >= 0.0
                ? $"Rising, mostly on {change.Headline}."
                : $"Falling, mostly on {change.Headline}.";

            fansLabel.text = UiFormat.Count(state.Fans) + " FANS";
            fansLabel.tooltip =
                "People who follow the company rather than the product. They arrive slowly, they "
                + "leave slowly, and they are still here between releases.";
        }

        /// <summary>Opens the books over whatever screen is showing, or closes them.</summary>
        private void ToggleFinanceReport()
        {
            if (financeHost == null)
            {
                return;
            }

            var opening = financeHost.style.display == DisplayStyle.None;
            financeHost.style.display = opening ? DisplayStyle.Flex : DisplayStyle.None;

            if (opening)
            {
                financeReport.Open();
            }
        }
        private VisualElement BuildTopBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("topbar");

            var left = new VisualElement();
            left.AddToClassList("topbar__group");
            // The money is a button. Clicking it, or the arrows beside it, opens the books.
            cashButton = new Button(ToggleFinanceReport);
            cashButton.AddToClassList("topbar__money");

            cashLabel = new Label();
            cashLabel.AddToClassList("topbar__stat");
            cashButton.Add(cashLabel);

            cashArrows = new Label();
            cashArrows.AddToClassList("topbar__arrows");
            cashButton.Add(cashArrows);
            valuationLabel = new Label();
            valuationLabel.AddToClassList("topbar__stat");
            valuationLabel.AddToClassList("topbar__stat--muted");
            left.Add(cashButton);
            left.Add(valuationLabel);

            // Standing sits beside the money because it is the other resource the player spends.
            // Research points sit with the money because they are the other currency, and clicking
            // them goes where they are spent.
            pointsButton = new Button(() => Show(Screen.Research));
            pointsButton.AddToClassList("topbar__points");

            var pointsIcon = new VisualElement();
            pointsIcon.AddToClassList("topbar__points-icon");

            var pointsArt = Resources.Load<Texture2D>("Hud/research_points");
            if (pointsArt != null)
            {
                pointsIcon.style.backgroundImage = new StyleBackground(pointsArt);
            }

            pointsButton.Add(pointsIcon);

            pointsLabel = new Label();
            pointsLabel.AddToClassList("topbar__points-value");
            pointsButton.Add(pointsLabel);
            left.Add(pointsButton);

            reputationLabel = new Label();
            reputationLabel.AddToClassList("topbar__standing");
            left.Add(reputationLabel);

            fansLabel = new Label();
            fansLabel.AddToClassList("topbar__standing");
            fansLabel.AddToClassList("topbar__standing--fans");
            left.Add(fansLabel);
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
            // The third argument on each of these is what the card over the bar says. Written to
            // answer "why would I go there", never to repeat the word already printed on the slot.
            hud.AddSlot("SITE", Screen.Site, () => Show(Screen.Site), "hud_site",
                "The room, and everything the company owns in it. Where the day is watched from.");

            hud.AddSlot("MODEL", Screen.Create, () => Show(Screen.Create), "hud_model",
                "Design a training run and start it. Five decisions: what it is, how big, what it "
                + "reads, what it runs on, and whether the bill is worth it.");

            hud.AddSlot("RESEARCH", Screen.Research, () => Show(Screen.Research), "hud_research",
                "Buy the understanding that unlocks everything else. Points come from work you are "
                + "already doing, and money alone will not keep pace.");

            hud.AddSlot("ARCHITECTURE", Screen.Family, () => Show(Screen.Family), "hud_architecture",
                "Which family of model you build. A sparse mixture is cheap to serve for its size; "
                + "that is the whole reason to own one.");

            hud.AddSlot("UPGRADE", Screen.Upgrade, () => Show(Screen.Upgrade), "hud_upgrade",
                "Programmes that improve a model already on sale, without training it again.");

            hud.AddSlot("TEAM", Screen.Team, () => Show(Screen.Team), "hud_team",
                "Hire, and see what the payroll costs. Desks cap the headcount, so this is also "
                + "where the office starts to matter.");

            hud.AddSlot("COMPUTE", Screen.Fleet, () => Show(Screen.Fleet), "hud_fleet",
                "Rent it or buy it. Buy too early and you own a depreciating asset; buy too late "
                + "and somebody else already has the customers.");

            hud.AddSlot("BUSINESS", Screen.Business, () => Show(Screen.Business), "hud_business",
                "The books. Revenue, burn, tax accruing, and what the company is actually worth.");

            hud.AddSlot("RELEASE", Screen.Release, () => Show(Screen.Release), "hud_release",
                "Put a finished model on sale, set its price, or take one off the market.");

            hud.AddSlot("CAPITAL", Screen.Funding, () => Show(Screen.Funding), "hud_funding",
                "Raise money and service what you owe. Debt is cheaper than equity right up to the "
                + "month you cannot pay it.");

            hud.AddSlot("RANKING", Screen.Ranking, () => Show(Screen.Ranking), "hud_ranking",
                "Every rival on the same capability scale as you, and what they have shipped.");

            hud.AddSlot("INTEL", Screen.Feed, () => Show(Screen.Feed), "hud_intelligence",
                "Advance warning, bought. The cheap desk is wrong about one thing in three and "
                + "sounds exactly as confident as the expensive one.");

            hud.AddSlot("MARKETING", Screen.Marketing, () => Show(Screen.Marketing), "hud_marketing",
                "Campaigns buy attention and never quality. A bad model advertised hard gets tried "
                + "and abandoned, which costs you twice.");

            hud.AddSlot("NEWS", Screen.News, () => Show(Screen.News), "hud_news",
                "The wire. Launches, scandals, regulators, and what is being said about you.");

            hud.AddSlot("@ MAIL", Screen.Mail, () => Show(Screen.Mail), "hud_mail",
                "Letters that need an answer: salary negotiations, the tax bill, and any fine the "
                + "company has earned.");
        }

        private void ToggleCompanyInfo()
        {
            companyInfoOpen = !companyInfoOpen;
            companyInfoTimer = 0f;
            hud.SetCompanyInfoOpen(companyInfoOpen);

            if (current == Screen.Site)
            {
                Show(Screen.Site);
            }
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
            var changed = current != screen;
            current = screen;
            contentHost.Clear();

            // A floating card belongs to the screen that opened it. Leaving it up over a different
            // tab is the same fault the corner banners had.
            researchCard?.RemoveFromHierarchy();

            hud.SetActiveSlot(screen);

            // Hidden here rather than only in RefreshChrome, which runs when a day rolls over. While
            // the game is paused, which is most of the time a player spends reading a screen, no day
            // rolls over, so both corner banners stayed on top of whatever tab had just been opened.
            modelBanner?.SetHidden(screen != Screen.Site);
            newsBanner?.SetHidden(screen != Screen.Site);

            foreach (var follower in followerBanners)
            {
                follower.SetHidden(screen != Screen.Site);
            }

            // Borrowed from Baka Bake Bakery, where the opening changes screens on a diagonal rather
            // than by cutting. It costs nothing and it is the difference between a screen appearing
            // and a screen arriving. Only on a real change: the clock rebuilds the open page every
            // tick, and animating that would make the whole interface twitch once a day.
            if (changed)
            {
                PlayPageTransition();
            }

            // Every screen scrolls, and none of them shows a bar for it.
            //
            // Half the tabs had grown past the window and UI Toolkit shrinks children rather than
            // overflowing them, so the bottom of a long page was not clipped, it was squashed. One
            // scroller here rather than one per screen, because the next screen would forget.
            var scroller = new ScrollView();
            scroller.AddToClassList("page-scroll");
            scroller.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroller.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            // The office is the exception: it is a room that fills the window, not a document, and
            // putting it in a scroller gives it a scrollbar's worth of nothing to slide.
            var host = screen == Screen.Site ? contentHost : scroller;
            if (screen != Screen.Site)
            {
                contentHost.Add(scroller);
            }

            switch (screen)
            {
                case Screen.Site:
                    host.Add(BuildSiteScreen());
                    break;
                case Screen.Create:
                    creator.Refresh();
                    host.Add(creator.Root);
                    break;
                case Screen.Research:
                    host.Add(BuildResearchScreen());
                    break;
                case Screen.Team:
                    host.Add(BuildTeamScreen());
                    break;
                case Screen.Fleet:
                    host.Add(BuildFleetScreen());
                    break;

                case Screen.Marketing:
                    host.Add(BuildMarketingScreen());
                    break;
                case Screen.Management:
                    management.Refresh();
                    host.Add(management.Root);
                    break;
                case Screen.News:
                    news.Refresh();
                    host.Add(news.Root);
                    break;
                case Screen.Mail:
                    mail.Refresh();
                    host.Add(mail.Root);
                    break;
                case Screen.Offices:
                    offices.Refresh();
                    host.Add(offices.Root);
                    break;
                case Screen.Business:
                    host.Add(BuildBusinessScreen());
                    break;
                case Screen.Family:
                    families.Refresh();
                    host.Add(families.Root);
                    break;
                case Screen.Upgrade:
                    upgrades.Refresh();
                    host.Add(upgrades.Root);
                    break;
                case Screen.Release:
                    host.Add(BuildReleaseScreen());
                    break;
                case Screen.Funding:
                    host.Add(BuildFundingScreen());
                    break;
                case Screen.Ranking:
                    host.Add(BuildRankingScreen());
                    break;
                default:
                    host.Add(BuildFeedScreen());
                    break;
            }
        }

        /// <summary>
        /// The technology tree, grouped by era. Every node is visible from day one including the one
        /// at the end, because the whole point of the last era is that the player can see it coming
        /// for years before they can touch it.
        /// </summary>
        /// <summary>
        /// The tree, laid out as a run of nodes that alternates above and below a spine.
        ///
        /// A grid of cards is a list with borders on it, and it hides the one thing the tree is for:
        /// that these are a sequence with prerequisites, not a menu. The zigzag makes the order
        /// readable at a glance and fits three times as many nodes on a screen, because a circle
        /// with an icon in it is smaller than a card with a paragraph in it.
        ///
        /// Selecting a node opens its card underneath. That is where the paragraph goes, and it is
        /// where a node gets room to say what it actually unlocks rather than a one line summary.
        /// </summary>
        private VisualElement BuildResearchScreen()
        {
            var active = state.ActiveResearch;
            // No standing blurb. The tree is the explanation, and a paragraph above it pushed the
            // first era half a screen down for something nobody reads twice.
            var page = NewPage("RESEARCH",
                active == null
                    ? string.Empty
                    : active.IsWaitingForCompute
                        ? $"{ResearchTree.Get(active.Node).DisplayName} has run its calendar and is "
                          + "waiting on the cluster."
                        : $"{ResearchTree.Get(active.Node).DisplayName} in progress: "
                          + $"{UiFormat.Percent(active.Progress, 0)}, "
                          + $"{Math.Min(active.DaysCompleted, active.DurationDays)} of "
                          + $"{active.DurationDays} days.");

            var board = simulation.ResearchBoard();
            var funding = BuildResearchFunding();
            var placedFunding = false;

            if (researchProblem.Length > 0)
            {
                var trouble = new Label(researchProblem);
                trouble.AddToClassList("mcb-problem");
                page.Add(trouble);
                researchProblem = string.Empty;
            }

            // The corner banner carries this too, and the corner banner is hidden on every screen
            // but the office. So the one screen that is about research had no way of telling the
            // player that research was running.
            if (active != null)
            {
                page.Add(BuildResearchingStrip(active));
            }

            foreach (ResearchEra era in Enum.GetValues(typeof(ResearchEra)))
            {
                var nodes = new List<ResearchStanding>();
                var deepening = new List<ResearchStanding>();

                foreach (var standing in board)
                {
                    if (standing.Node.Era != era)
                    {
                        continue;
                    }

                    if (standing.Node.Track == ResearchTrack.ModelImprovement)
                    {
                        deepening.Add(standing);
                    }
                    else
                    {
                        nodes.Add(standing);
                    }
                }

                if (nodes.Count == 0 && deepening.Count == 0)
                {
                    continue;
                }

                var section = new VisualElement();
                section.AddToClassList("era");

                var heading = new Label(EraTitle(era));
                heading.AddToClassList("era__heading");
                section.Add(heading);

                if (nodes.Count > 0)
                {
                    var track = new VisualElement();
                    track.AddToClassList("tree-track");

                    var spine = new VisualElement();
                    spine.AddToClassList("tree-spine");
                    track.Add(spine);

                    for (var index = 0; index < nodes.Count; index++)
                    {
                        track.Add(BuildTreeNode(nodes[index], index % 2 == 0));
                    }

                    section.Add(track);
                }

                // The second line. A capability node opens a direction the company could not go at
                // all; these deepen something it already does, and reading them as the same kind of
                // decision is what made the tree feel like a shopping list.
                if (deepening.Count > 0)
                {
                    var band = new VisualElement();
                    band.AddToClassList("deepening");

                    var bandHeading = new Label("MODEL IMPROVEMENT");
                    bandHeading.AddToClassList("deepening__heading");
                    band.Add(bandHeading);

                    var row = new VisualElement();
                    row.AddToClassList("deepening__row");

                    foreach (var standing in deepening)
                    {
                        var node = BuildTreeNode(standing, false);
                        node.AddToClassList("tree-node--small");
                        row.Add(node);
                    }

                    band.Add(row);
                    section.Add(band);
                }

                // Funding rides alongside the first era rather than sitting above everything. It is
                // a setting the player touches twice a campaign and the tree is what they came for,
                // so the tree starts at the top of the screen and the setting fills the gap beside
                // it that the first era's short track leaves empty anyway.
                if (!placedFunding)
                {
                    placedFunding = true;

                    var row = new VisualElement();
                    row.AddToClassList("era-row");

                    section.AddToClassList("era--beside");
                    row.Add(section);
                    row.Add(funding);
                    page.Add(row);
                }
                else
                {
                    page.Add(section);
                }
            }

            if (!placedFunding)
            {
                page.Add(funding);
            }

            return page;
        }

        /// <summary>
        /// One node on the spine. High or low, and its state is carried by the ring rather than by
        /// a word, so a whole era reads without any of it being spelled out.
        /// </summary>
        /// <summary>
        /// How the company pays for discovery, at the top of the research screen.
        ///
        /// Two ways, and they are a real choice rather than a preference. A fixed budget is paid
        /// whatever happens, which is a promise a struggling company cannot keep. A share of revenue
        /// costs nothing in a bad month and nothing is what it discovers, so a company that stops
        /// earning also stops learning exactly when it most needs to catch up.
        /// </summary>
        /// <summary>
        /// Research in progress, on the screen research lives on.
        ///
        /// Days left is the headline because that is the number a player plans around, and the bar
        /// is behind the words rather than under them so the whole strip is the progress, the same
        /// shape the training strip uses in the corner.
        /// </summary>
        private VisualElement BuildResearchingStrip(ResearchProject active)
        {
            var node = ResearchTree.Get(active.Node);
            var left = Math.Max(0, active.DurationDays - active.DaysCompleted);

            var strip = new VisualElement();
            strip.AddToClassList("researching");

            var fill = new VisualElement();
            fill.AddToClassList("researching__fill");
            fill.style.width = Length.Percent(
                (float)(Math.Clamp(active.Progress, 0.0, 1.0) * 100.0));

            strip.Add(fill);

            var text = new VisualElement();
            text.AddToClassList("researching__text");

            var title = new Label("RESEARCHING IN PROGRESS");
            title.AddToClassList("researching__title");
            text.Add(title);

            // A node needs days *and* compute, and only the days pass on their own. A company with
            // its whole fleet on a training run reaches the end of the calendar and stops, and this
            // used to read "0 days left, 30% done" for the rest of the campaign.
            var what = new Label(active.IsWaitingForCompute
                ? $"{node.DisplayName}  ·  the calendar is done, the cluster is not  ·  "
                  + $"{UiFormat.Number(active.PetaflopDaysRemaining, 0)} PF-days still owed"
                : $"{node.DisplayName}  ·  {left} days left  ·  "
                  + $"{UiFormat.Percent(active.Progress, 0)} done");

            what.AddToClassList("researching__what");
            what.EnableInClassList("researching__what--waiting", active.IsWaitingForCompute);
            text.Add(what);

            if (active.IsWaitingForCompute)
            {
                var why = new Label(
                    "Research shares the fleet with training, upgrades and family programmes. Free "
                    + "some capacity or rent more and this finishes on its own.");

                why.AddToClassList("researching__why");
                text.Add(why);
            }

            strip.Add(text);

            var stop = new Button(() =>
            {
                if (cancelArmed)
                {
                    cancelArmed = false;
                    simulation.TryCancelResearch(out _);
                }
                else
                {
                    cancelArmed = true;
                }

                Show(Screen.Research);
            })
            { text = cancelArmed ? "CONFIRM, NOTHING COMES BACK" : "CANCEL" };

            stop.AddToClassList("researching__stop");
            stop.EnableInClassList("researching__stop--armed", cancelArmed);

            stop.tooltip = "Abandons the programme. The cash and the points were spent on the day it "
                + "started and none of it comes back; what you get is the right to start something "
                + "else today rather than in four months.";

            strip.Add(stop);
            return strip;
        }

        private VisualElement BuildResearchFunding()
        {
            var state = simulation.State;

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("rfund-half");

            var head = new VisualElement();
            head.AddToClassList("rfund__head");

            var heading = new Label("FUNDING");
            heading.AddToClassList("panel__heading");
            heading.style.marginBottom = 0;
            head.Add(heading);

            var banked = new Label(
                $"{UiFormat.Number(state.ResearchPoints, 0)} points banked, "
                + $"{state.ResearchPointsToday:N1} a day");

            banked.AddToClassList("rfund__banked");
            head.Add(banked);
            panel.Add(head);

            var modes = new VisualElement();
            modes.AddToClassList("rfund__modes");

            modes.Add(FundingChip("A FIXED BUDGET", ResearchFundingMode.Fixed,
                state.ResearchFunding == ResearchFundingMode.Fixed));

            modes.Add(FundingChip("A SHARE OF REVENUE", ResearchFundingMode.RevenueShare,
                state.ResearchFunding == ResearchFundingMode.RevenueShare));

            panel.Add(modes);

            if (state.ResearchFunding == ResearchFundingMode.Fixed)
            {
                var label = new Label($"{UiFormat.Money(state.ResearchMonthlyUsd)} a month");
                label.AddToClassList("field__label");
                panel.Add(label);

                // Logarithmic, because the range runs from a thousand to five million and a linear
                // slider would spend nine tenths of its travel on amounts that change nothing.
                var slider = new Slider(
                    Mathf.Log10(ResearchBudget.MinimumMonthlyUsd),
                    Mathf.Log10(ResearchBudget.MaximumMonthlyUsd))
                {
                    value = Mathf.Log10(Math.Max(ResearchBudget.MinimumMonthlyUsd,
                        state.ResearchMonthlyUsd))
                };

                slider.AddToClassList("field");
                slider.RegisterValueChangedCallback(evt =>
                {
                    state.ResearchMonthlyUsd = (long)Math.Round(Math.Pow(10.0, evt.newValue));
                    Show(Screen.Research);
                });

                panel.Add(slider);
            }
            else
            {
                var revenue = simulation.MonthlyRevenueUsd();

                var label = new Label(
                    $"{UiFormat.Percent(state.ResearchRevenueShare, 0)} of "
                    + $"{UiFormat.Money(revenue)} a month, which is "
                    + $"{UiFormat.Money((long)Math.Round(revenue * state.ResearchRevenueShare))}");

                label.AddToClassList("field__label");
                panel.Add(label);

                var slider = new Slider(0f, 0.5f) { value = (float)state.ResearchRevenueShare };
                slider.AddToClassList("field");
                slider.RegisterValueChangedCallback(evt =>
                {
                    state.ResearchRevenueShare = evt.newValue;
                    Show(Screen.Research);
                });

                panel.Add(slider);
            }

            var budget = ResearchBudget.MonthlyBudgetUsd(state.ResearchFunding,
                state.ResearchMonthlyUsd, state.ResearchRevenueShare,
                simulation.MonthlyRevenueUsd());

            var hint = new Label(
                $"Buys about {ResearchBudget.PointsFromFunding(budget):N0} points a month. "
                + "Four times the money buys twice the points, so nobody purchases the tree outright. "
                + "Building things earns more than paying for them.");

            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private Button FundingChip(string text, ResearchFundingMode mode, bool on)
        {
            var chip = new Button(() =>
            {
                simulation.State.ResearchFunding = mode;
                Show(Screen.Research);
            })
            { text = text };

            chip.AddToClassList("chip");
            chip.EnableInClassList("chip--on", on);
            return chip;
        }

        /// <summary>
        /// The card that opens when a node is clicked: what it is, what it costs, what it gives.
        ///
        /// It exists because the tree was twenty one circles with a word under each and no way to
        /// find out what any of them did before committing. A player should be able to read a branch
        /// before spending three months on it.
        /// </summary>
        private void ShowResearchCard(ResearchStanding standing, Vector2 at)
        {
            researchCard?.RemoveFromHierarchy();

            var node = standing.Node;
            researchCard = new VisualElement();
            researchCard.AddToClassList("rcard");
            researchCard.style.left = Mathf.Clamp(at.x, 8f, 1400f);
            researchCard.style.top = Mathf.Clamp(at.y, 8f, 700f);

            var head = new VisualElement();
            head.AddToClassList("rcard__head");

            var icon = new VisualElement();
            icon.AddToClassList("rcard__icon");

            var art = ResearchIcons.Get(node.Id);
            if (art != null)
            {
                icon.style.backgroundImage = new StyleBackground(art);
            }

            head.Add(icon);

            var titles = new VisualElement();
            titles.AddToClassList("rcard__titles");

            var title = new Label(node.DisplayName.ToUpperInvariant());
            title.AddToClassList("rcard__title");
            titles.Add(title);

            var state = standing.IsUnlocked ? "RESEARCHED"
                : standing.IsInProgress ? "IN PROGRESS"
                : standing.CanStart ? "READY" : "LOCKED";

            var badge = new Label(state);
            badge.AddToClassList("rcard__badge");
            badge.EnableInClassList("rcard__badge--ready", standing.CanStart);
            badge.EnableInClassList("rcard__badge--done", standing.IsUnlocked);
            titles.Add(badge);

            head.Add(titles);
            researchCard.Add(head);

            var body = new Label(node.Description);
            body.AddToClassList("rcard__body");
            researchCard.Add(body);

            // What it opens. This is the half of a node that decides whether it is worth doing, and
            // it was only ever drawn on the card at the bottom of the page that nobody scrolled to.
            // Built into a list first, because the heading above them should not print when the
            // node opens nothing, and the source is a lazy sequence that cannot be counted twice.
            var effects = new List<VisualElement>(UnlockLines(node));
            if (effects.Count > 0)
            {
                var opens = new Label("WHAT IT OPENS");
                opens.AddToClassList("rcard__opens");
                researchCard.Add(opens);

                foreach (var line in effects)
                {
                    line.AddToClassList("rcard__unlock");
                    researchCard.Add(line);
                }
            }

            var points = ResearchBudget.PointCostOf(node.CostUsd);
            var cash = ResearchBudget.CashCostOf(node.CostUsd);

            // Three figures rather than a sentence, because these are the three the player is
            // comparing against what they have, and a sentence makes them read it to find them.
            var cost = new VisualElement();
            cost.AddToClassList("rcard__costs");

            cost.Add(RCardFigure("POINTS", $"{points:N0}",
                simulation.State.ResearchPoints >= points));

            cost.Add(RCardFigure("CASH", UiFormat.Money(cash),
                simulation.State.CashUsd >= cash));

            cost.Add(RCardFigure("TAKES", UiFormat.Days(standing.DurationDays), true));

            researchCard.Add(cost);

            var have = new Label(
                $"You have {simulation.State.ResearchPoints:N0} points and "
                + $"{UiFormat.Money(simulation.State.CashUsd)}.");

            have.AddToClassList("rcard__have");
            researchCard.Add(have);

            if (!standing.CanStart && !standing.IsUnlocked && !standing.IsInProgress)
            {
                var why = new Label(standing.BlockedReason);
                why.AddToClassList("rcard__blocked");
                researchCard.Add(why);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("rcard__buttons");

            if (standing.CanStart)
            {
                var start = new Button(() =>
                {
                    if (!simulation.TryStartResearch(node.Id, out var why))
                    {
                        // It should not be reachable, since the card only offers this when the
                        // standing says it can start. If it ever is, say why rather than doing
                        // nothing, which is what made this button feel broken before.
                        researchProblem = why;
                        Show(Screen.Research);
                        return;
                    }

                    researchCard?.RemoveFromHierarchy();

                    // Same as starting a run. The work is months long and there is nothing further
                    // to do on this screen, so the room is where the player belongs.
                    Show(Screen.Site);
                })
                {
                    text = $"BEGIN  ·  {points:N0} POINTS AND {UiFormat.Money(cash)}"
                };

                start.AddToClassList("button");
                start.AddToClassList("button--primary");
                start.style.marginLeft = 0;
                buttons.Add(start);
            }

            var close = new Button(() => researchCard?.RemoveFromHierarchy()) { text = "CLOSE" };
            close.AddToClassList("button");
            close.style.marginLeft = 6;
            buttons.Add(close);

            researchCard.Add(buttons);
            shellRoot.Add(researchCard);
        }

        /// <summary>One figure on the card, greyed when the company cannot cover it.</summary>
        private static VisualElement RCardFigure(string label, string value, bool affordable)
        {
            var figure = new VisualElement();
            figure.AddToClassList("rcard-figure");
            figure.EnableInClassList("rcard-figure--short", !affordable);

            var caption = new Label(label);
            caption.AddToClassList("rcard-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("rcard-figure__value");
            figure.Add(amount);

            return figure;
        }

        private VisualElement BuildTreeNode(ResearchStanding standing, bool above)
        {
            var node = standing.Node;

            var column = new VisualElement();
            column.AddToClassList("tree-node");
            column.EnableInClassList("tree-node--above", above);

            // ShowResearchCard was written in full and never called from anywhere, so clicking a
            // node only moved a ring and the player was left to guess what the node did. The click
            // carries its own position, which is why this is a ClickEvent rather than the Button
            // action: the card opens where the finger is rather than in a fixed corner.
            var button = new Button();
            button.RegisterCallback<ClickEvent>(click =>
            {
                selectedResearch = node.Id;
                ShowResearchCard(standing, click.position);
            });

            button.AddToClassList("tree-pip");
            button.EnableInClassList("tree-pip--done", standing.IsUnlocked);
            button.EnableInClassList("tree-pip--running", standing.IsInProgress);
            button.EnableInClassList("tree-pip--ready", !standing.IsUnlocked && standing.CanStart);
            button.EnableInClassList("tree-pip--picked", selectedResearch == node.Id);

            var icon = new VisualElement();
            icon.AddToClassList("tree-pip__icon");

            // One icon lookup, and it reads the catalogued names. The lookup this replaced
            // guessed at names like research_code and research_chat that were never drawn, so every
            // node fell through to the empty badge while the real files sat in Resources/Research.
            var art = ResearchIcons.Get(node.Id);
            if (art != null)
            {
                icon.style.backgroundImage = new StyleBackground(art);
            }
            else
            {
                icon.AddToClassList("tree-pip__icon--none");
            }

            button.Add(icon);
            column.Add(button);

            var label = new Label(node.DisplayName.ToUpperInvariant());
            label.AddToClassList("tree-node__label");
            column.Add(label);

            return column;
        }

        private static IEnumerable<VisualElement> UnlockLines(ResearchNode node)
        {
            if (node.UnlocksArchitecture != ArchitectureId.None)
            {
                yield return UnlockLine("ARCHITECTURE",
                    ArchitectureCatalog.Get(node.UnlocksArchitecture).DisplayName);
            }

            if (node.UnlocksData != DatasetSource.None)
            {
                foreach (var corpus in DatasetCatalog.All)
                {
                    if ((node.UnlocksData & corpus.Flag) == corpus.Flag)
                    {
                        yield return UnlockLine("CORPUS", corpus.DisplayName);
                    }
                }
            }

            if (node.UnlocksTier != ComputeTier.None)
            {
                yield return UnlockLine("COMPUTE TIER", node.UnlocksTier.ToString());
            }

            // ModelTrait has no None member, so the gate flag is the only honest signal that a node
            // actually opens an upgrade line rather than defaulting to the zero trait.
            if (node.GatesTrait)
            {
                yield return UnlockLine("UPGRADE LINE", node.UnlocksTrait.ToString());
            }

            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires == node.Id)
                {
                    yield return UnlockLine("MODEL TYPE", definition.DisplayName);
                }
            }

            foreach (var required in node.Prerequisites)
            {
                yield return UnlockLine("NEEDS FIRST", ResearchTree.Get(required).DisplayName);
            }
        }

        private static VisualElement UnlockLine(string kind, string what)
        {
            var row = new VisualElement();
            row.AddToClassList("unlock-row");

            var tag = new Label(kind);
            tag.AddToClassList("unlock-row__tag");
            tag.EnableInClassList("unlock-row__tag--needs", kind == "NEEDS FIRST");
            row.Add(tag);

            var name = new Label(what);
            name.AddToClassList("unlock-row__name");
            row.Add(name);

            return row;
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
            var officeHeading = new Label("WHERE YOU WORK");
            officeHeading.AddToClassList("panel__heading");
            offices.Add(officeHeading);

            // One button rather than a grid of five cards. The places have photographs now and
            // deserve a screen; a card the size of a hardware card cannot show a room.
            var current = state.Staff.OfficeDefinition;

            var where = new Label(
                $"LVL {current.Level}  ·  {current.DisplayName}  ·  "
                + $"{state.Staff.Headcount} of {current.Desks} desks  ·  "
                + $"{UiFormat.Money(current.MonthlyRentUsd)} a month");

            where.AddToClassList("office-now");
            offices.Add(where);

            offices.Add(BuildUpgradeButton());
            page.Add(offices);
            return page;
        }

        /// <summary>
        /// The way into the places screen.
        ///
        /// A picture with a word on it rather than a plain button, because it opens the one screen
        /// that is about somewhere rather than about a number, and because the author drew it.
        /// </summary>
        private VisualElement BuildUpgradeButton()
        {
            var button = new Button(() => Show(Screen.Offices));
            button.AddToClassList("office-upgrade");

            var art = Resources.Load<Texture2D>("Ui/office_upgrade");
            if (art != null)
            {
                button.style.backgroundImage = new StyleBackground(art);
                button.AddToClassList("office-upgrade--art");
            }

            var caption = new Label("UPGRADE THE OFFICE");
            caption.AddToClassList("office-upgrade__caption");
            button.Add(caption);

            return button;
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

        /// <summary>
        /// The fleet. Rented capacity on top, owned batches below with what each one is worth now
        /// against what it cost, which is the number the whole hardware design exists to show.
        /// </summary>
        /// <summary>
        /// What the fleet costs, and what the cost is made of.
        ///
        /// The screen used to say one number a day to run. Four separate bills go into that number
        /// and the player pays them for different reasons: rent stops the day you release capacity,
        /// electricity scales with what you own and run, housing is floor space and cooling, upkeep
        /// is the hardware wearing out while it works. A single figure hides which lever moves it.
        /// </summary>
        /// <summary>
        /// The two ways to have compute, as one slanted strip across the top of the screen.
        ///
        /// Renting is the whole game for now and owning a datacenter is years away for any company,
        /// so the second half is deliberately shut rather than hidden: a player should be able to see
        /// that owning exists and what it will take, because that is a goal rather than a secret.
        /// </summary>
        private VisualElement BuildHostingSwitch()
        {
            var strip = new VisualElement();
            strip.AddToClassList("hswitch");

            var artLeft = new VisualElement();
            artLeft.AddToClassList("hswitch__art");
            artLeft.AddToClassList("hswitch__art--left");

            var rentArt = Resources.Load<Texture2D>("Hosting/hosting_renting");
            if (rentArt != null)
            {
                artLeft.style.backgroundImage = new StyleBackground(rentArt);
            }

            strip.Add(artLeft);

            // It did nothing at all, which reads as broken rather than as the only option. Renting
            // is where the fleet controls already are, so the half that is live says so and scrolls
            // to them instead of pretending to be a mode switch that has nothing to switch to.
            var renting = new Button(() => Show(Screen.Fleet)) { text = "RENTING HOSTING" };
            renting.tooltip = "The fleet you rent. Everything below this bar is it.";
            renting.AddToClassList("hswitch__half");
            renting.AddToClassList("hswitch__half--on");
            strip.Add(renting);

            var owning = new Button(() => { }) { text = "YOUR OWN DATACENTER" };
            owning.tooltip = "Locked until the Datacenter programme research lands. Renting is the "
                + "only way to buy compute until then.";
            owning.AddToClassList("hswitch__half");
            owning.AddToClassList("hswitch__half--locked");
            owning.SetEnabled(false);
            owning.tooltip =
                "Not yet. Owning silicon needs two released models, eighty million in cash, two "
                + "hundred million of lifetime revenue and the datacenter programme researched. "
                + "Renting is the right answer until the cluster is busy enough to justify capital.";

            strip.Add(owning);

            var artRight = new VisualElement();
            artRight.AddToClassList("hswitch__art");
            artRight.AddToClassList("hswitch__art--right");

            // Deliberately dimmed by the stylesheet as well: the half it belongs to is locked, and
            // a bright picture on a disabled control reads as a control that should work.
            var ownArt = Resources.Load<Texture2D>("Hosting/hosting_datacenter");
            if (ownArt != null)
            {
                artRight.style.backgroundImage = new StyleBackground(ownArt);
            }
            strip.Add(artRight);

            return strip;
        }

        /// <summary>
        /// What the service is like right now: the load dial, the severity scale, the response time.
        ///
        /// This is the readout for the mechanic that decides whether users stay. It sits directly
        /// under the rent controls because those two things are one decision.
        /// </summary>
        private VisualElement BuildServicePanel()
        {
            var quality = simulation.State.LastQuality;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("SERVICE");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("service__row");

            var dialBlock = new VisualElement();
            dialBlock.AddToClassList("service__dial");

            var gauge = new ServiceGauge();
            gauge.Set(quality);
            dialBlock.Add(gauge);

            var percent = new Label(UiFormat.Percent(quality.Utilisation, 0));
            percent.AddToClassList("service__percent");
            dialBlock.Add(percent);

            var caption = new Label("Server Usage");
            caption.AddToClassList("service__caption");
            dialBlock.Add(caption);

            row.Add(dialBlock);

            var scale = new ServiceScale();
            scale.Set(quality.Status);
            row.Add(scale);

            var words = new VisualElement();
            words.AddToClassList("service__words");

            var response = new Label($"Response Time: {quality.ResponseMilliseconds:N0}ms");
            response.AddToClassList("service__response");
            response.style.color = ServiceGauge.ColourFor(quality.Status);
            words.Add(response);

            var headline = new Label(quality.Headline);
            headline.AddToClassList("service__headline");
            words.Add(headline);

            var effect = new Label(quality.Reliability >= 1.0
                ? "No penalty. The market sees the product at its full strength."
                : $"Costing you {UiFormat.Percent(1.0 - quality.Reliability)} of how attractive the "
                    + "product looks to everyone deciding today.");

            effect.AddToClassList("service__effect");
            effect.EnableInClassList("service__effect--bad", quality.Reliability < 1.0);
            words.Add(effect);

            row.Add(words);
            row.Add(BuildRightNowCard());
            row.Add(BuildUserCharts());

            panel.Add(row);
            return panel;
        }

        /// <summary>
        /// The stat card from the reference: who is on right now, and the four numbers that put that
        /// in context.
        ///
        /// Online is not a stored number. Registered is a stock the simulation records once a day;
        /// how many of those are typing at this minute is a rhythm over that stock, so it is derived
        /// here from the clock. Confusing the two is how a dashboard ends up claiming ten million
        /// people are using something simultaneously.
        /// </summary>
        private VisualElement BuildRightNowCard()
        {
            var breakdown = simulation.MarketByType();
            var registered = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);
            var hour = clock.DayProgress * 24.0;
            var online = Concurrency.OnlineAt(registered, hour);

            var card = new VisualElement();
            card.AddToClassList("rnow");

            var left = new VisualElement();
            left.AddToClassList("rnow__left");

            var caption = new Label("Right now");
            caption.AddToClassList("rnow__caption");
            left.Add(caption);

            var big = new Label(UiFormat.Count(online));
            big.AddToClassList("rnow__big");
            left.Add(big);

            var under = new Label("Online users");
            under.AddToClassList("rnow__under");
            left.Add(under);

            card.Add(left);

            var right = new VisualElement();
            right.AddToClassList("rnow__right");

            var heading = new Label("Today's Estimated Income");
            heading.AddToClassList("rnow__heading");
            right.Add(heading);

            var month = Ledger.MonthKeyOf(simulation.State.Date);
            var earned = simulation.State.Ledger.MonthTotal(month, LedgerLine.Subscriptions);
            var spent = simulation.State.Ledger.MonthCost(month);

            var income = new Label(UiFormat.Money(earned));
            income.AddToClassList("rnow__income");
            right.Add(income);

            right.Add(UiParts.StatLine("Registered Users", UiFormat.Count(registered)));
            right.Add(UiParts.StatLine("Potential Users", UiFormat.Count(breakdown.AddressableUsers)));
            right.Add(UiParts.StatLine("Subscribers", UiFormat.Count(registered * PaidShare())));
            right.Add(UiParts.StatLine("All Expenses", "-" + UiFormat.Money(spent)));

            card.Add(right);
            return card;
        }

        /// <summary>What share of the people held are on a paid account rather than the free tier.</summary>
        private double PaidShare() =>
            Math.Clamp(1.0 - simulation.State.Monetization.FreeShareOfTokens, 0.0, 1.0);

        /// <summary>
        /// The two charts side by side: the day by day account base, and today's traffic curve.
        ///
        /// Registered is filled because it is a stock and the area reads as accumulation. Online is a
        /// bare line because it is a rate, and filling it would suggest a total that does not exist.
        /// </summary>
        private VisualElement BuildUserCharts()
        {
            var block = new VisualElement();
            block.AddToClassList("charts");

            var breakdown = simulation.MarketByType();
            var registered = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);

            var history = simulation.State.Users.Recent(15);

            var left = new VisualElement();
            left.AddToClassList("chart-block");

            var leftTitle = new Label("Registered Users");
            leftTitle.AddToClassList("chart-block__title");
            left.Add(leftTitle);

            var registeredChart = new LineChart();
            registeredChart.Set(history, new Color(0.29f, 0.68f, 0.90f), true);
            left.Add(registeredChart);

            var leftFoot = new Label(history.Count < 2
                ? "Filling in as the days pass."
                : $"Last {history.Count} days");

            leftFoot.AddToClassList("chart-block__foot");
            left.Add(leftFoot);

            block.Add(left);

            var right = new VisualElement();
            right.AddToClassList("chart-block");

            var rightTitle = new Label("Online users");
            rightTitle.AddToClassList("chart-block__title");
            right.Add(rightTitle);

            // Every second hour of today, which is the shape the reference shows. It is a curve over
            // a number the simulation owns rather than a second source of truth.
            var curve = new List<double>(13);
            for (var hour = 0.0; hour <= 24.0; hour += 2.0)
            {
                curve.Add(Concurrency.OnlineAt(registered, hour));
            }

            var onlineChart = new LineChart();
            onlineChart.Set(curve, new Color(0.92f, 0.45f, 0.32f), false);
            right.Add(onlineChart);

            var rightFoot = new Label("00:00 to 23:00");
            rightFoot.AddToClassList("chart-block__foot");
            right.Add(rightFoot);

            block.Add(right);
            return block;
        }

        /// <summary>
        /// The three reserved blocks, bought in whole units that stack.
        ///
        /// Not a ladder. Standard is the sensible default, the edge tier buys experience rather than
        /// volume, and bulk buys volume at the cost of experience. A player who takes bulk to chase a
        /// large audience and then cannot keep it has made a real mistake rather than hit a rule
        /// nobody told them about, which is why each card states what it does under load.
        /// </summary>
        private VisualElement BuildPackagePanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("RESERVED CAPACITY");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var row = new VisualElement();
            row.AddToClassList("pack-row");

            foreach (var definition in HostingCatalog.All)
            {
                row.Add(BuildPackageCard(definition));
            }

            panel.Add(row);
            return panel;
        }

        private VisualElement BuildPackageCard(HostingPackageDefinition definition)
        {
            var held = simulation.State.Pool.PackageCount(definition.Id);

            var card = new VisualElement();
            card.AddToClassList("pack");
            card.EnableInClassList("pack--on", held > 0);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("pack__name");
            card.Add(name);

            var size = new Label(
                $"{UiFormat.Petaflops(definition.Petaflops)}  "
                + $"about {UiFormat.Count(HostingCatalog.CoversAccounts(definition.Petaflops))} accounts");

            size.AddToClassList("pack__size");
            card.Add(size);

            var pitch = new Label(definition.Pitch);
            pitch.AddToClassList("pack__pitch");
            card.Add(pitch);

            var price = new Label($"{UiFormat.Money(definition.MonthlyCostUsd)} a month each");
            price.AddToClassList("pack__price");
            card.Add(price);

            var controls = new VisualElement();
            controls.AddToClassList("pack__controls");

            var fewer = new Button(() => SetPackage(definition.Id, held - 1)) { text = "-" };
            fewer.AddToClassList("pack__step");
            fewer.SetEnabled(held > 0);
            controls.Add(fewer);

            var count = new Label(held > 0 ? $"x{held}" : "none");
            count.AddToClassList("pack__count");
            controls.Add(count);

            var more = new Button(() => SetPackage(definition.Id, held + 1)) { text = "+" };
            more.AddToClassList("pack__step");
            more.SetEnabled(held < definition.UnitCap);
            controls.Add(more);

            card.Add(controls);

            if (held > 0)
            {
                var total = new Label(
                    $"{UiFormat.Petaflops(definition.Petaflops * held)} for "
                    + $"{UiFormat.Money(definition.MonthlyCostUsd * held)} a month");

                total.AddToClassList("pack__total");
                card.Add(total);
            }

            return card;
        }

        private void SetPackage(HostingPackage id, int units)
        {
            simulation.State.Pool.SetPackageCount(id, units);
            Show(Screen.Fleet);
        }

        private VisualElement BuildFleetBill(ComputeProfile profile)
        {
            var block = new VisualElement();
            block.AddToClassList("panel");
            block.AddToClassList("fleet-bill");

            var head = new VisualElement();
            head.AddToClassList("fleet-bill__head");

            var heading = new Label("WHAT THE DAY COSTS");
            heading.AddToClassList("panel__heading");
            heading.style.marginBottom = 0;
            head.Add(heading);

            var total = new Label(UiFormat.Money((long)profile.Bill.TotalUsd) + " a day");
            total.AddToClassList("fleet-bill__total");
            head.Add(total);

            block.Add(head);

            var bar = new FleetBillBar();
            bar.Set(profile.Bill);
            block.Add(bar);

            var legend = new VisualElement();
            legend.AddToClassList("fleet-bill__legend");
            legend.Add(BillKey("CLOUD RENT", profile.Bill.CloudRentUsd, FleetBillBar.RentColour));
            legend.Add(BillKey("ELECTRICITY", profile.Bill.ElectricityUsd, FleetBillBar.PowerColour));
            legend.Add(BillKey("HOUSING", profile.Bill.HousingUsd, FleetBillBar.HousingColour));
            legend.Add(BillKey("UPKEEP", profile.Bill.MaintenanceUsd, FleetBillBar.UpkeepColour));
            block.Add(legend);

            // Power is the one that can stop the fleet rather than only cost money.
            var power = new Label(
                $"Drawing {profile.PowerDrawKilowatts:N0} kW of {profile.PowerCapacityKilowatts:N0} kW available."
                + (profile.IsOverPowerBudget ? "  OVER BUDGET: capacity is being wasted." : string.Empty));

            power.AddToClassList("fleet-bill__power");
            power.EnableInClassList("fleet-bill__power--over", profile.IsOverPowerBudget);
            block.Add(power);

            return block;
        }

        private static VisualElement BillKey(string name, double amount, Color colour)
        {
            var key = new VisualElement();
            key.AddToClassList("fleet-key");

            var swatch = new VisualElement();
            swatch.AddToClassList("fleet-key__swatch");
            swatch.style.backgroundColor = colour;
            key.Add(swatch);

            var label = new Label($"{name}  {UiFormat.Money((long)amount)}");
            label.AddToClassList("fleet-key__label");
            key.Add(label);

            return key;
        }

        /// <summary>
        /// Marketing: pick up to three channels, an audience and a term, then book it.
        ///
        /// The tiles are the screen. Each one is a picture and a name, because the decision is
        /// "which of these feels right for what I am selling" rather than a table of coefficients,
        /// and the numbers that back it are underneath for the player who wants them.
        ///
        /// Three at once is the cap, and the reason is that channels cover each other's weaknesses:
        /// television is broad and slow, social is fast and forgets, press hardly moves the numbers
        /// and is the only thing that reliably builds standing. Allowing all six would make the
        /// combination meaningless.
        /// </summary>
        private VisualElement BuildMarketingScreen()
        {
            var state = simulation.State;

            var page = NewPage("MARKETING",
                "Advertising buys attention, never quality. A product people have not heard of loses "
                + "to one they have, and a bad product they have heard of gets tried and dropped.");

            page.Add(BuildAwarenessPanel());

            var channels = new VisualElement();
            channels.AddToClassList("panel");

            var heading = new Label("CHANNELS");
            heading.AddToClassList("panel__heading");
            channels.Add(heading);

            var grid = new VisualElement();
            grid.AddToClassList("chan-grid");

            foreach (var definition in MarketingCatalog.All)
            {
                grid.Add(BuildChannelTile(definition));
            }

            channels.Add(grid);
            page.Add(channels);

            page.Add(BuildBookingPanel());
            page.Add(BuildRunningPanel());

            return page;
        }

        /// <summary>How well known the company is, audience by audience.</summary>
        private VisualElement BuildAwarenessPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var head = new VisualElement();
            head.AddToClassList("rfund__head");

            var heading = new Label("WHO HAS HEARD OF YOU");
            heading.AddToClassList("panel__heading");
            heading.style.marginBottom = 0;
            head.Add(heading);

            var overall = new Label(UiFormat.Percent(simulation.State.Awareness.Overall, 0)
                + " overall");

            overall.AddToClassList("rfund__banked");
            head.Add(overall);
            panel.Add(head);

            foreach (var audience in AudienceCatalog.All)
            {
                var known = simulation.State.Awareness.In(audience.Segment);
                panel.Add(UiParts.ThinBarRow(audience.DisplayName, UiFormat.Percent(known, 0), known));
            }

            var note = new Label(
                "Being used counts as being known. A company people already have on the service does "
                + "not become anonymous, so this floor rises with the audience you hold.");

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        /// <summary>
        /// One channel: a darkened photograph, a name across the bottom, and what it actually does.
        /// </summary>
        private VisualElement BuildChannelTile(MarketingChannelDefinition definition)
        {
            var picked = pickedChannels.Contains(definition.Id);

            var tile = new Button(() =>
            {
                if (picked)
                {
                    pickedChannels.Remove(definition.Id);
                }
                else if (pickedChannels.Count < MarketingCatalog.MostChannelsAtOnce)
                {
                    pickedChannels.Add(definition.Id);
                }

                Show(Screen.Marketing);
            });

            tile.AddToClassList("chan");
            tile.EnableInClassList("chan--on", picked);

            var art = new VisualElement();
            art.AddToClassList("chan__art");

            var picture = Resources.Load<Texture2D>("Marketing/" + definition.Art);
            if (picture != null)
            {
                art.style.backgroundImage = new StyleBackground(picture);
            }
            else
            {
                art.AddToClassList("chan__art--missing");
            }

            tile.Add(art);

            // The scrim is what makes a realistic photograph sit inside a dark interface instead of
            // shouting over it. Same rule as the card art everywhere else in the game.
            var scrim = new VisualElement();
            scrim.AddToClassList("chan__scrim");
            tile.Add(scrim);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("chan__name");
            tile.Add(name);

            var price = new Label(UiFormat.Money(definition.DailyCostUsd) + " a day");
            price.AddToClassList("chan__price");
            tile.Add(price);

            tile.tooltip = definition.Pitch
                + $"\n\nBest with: {AudienceCatalog.Get(definition.Favours).DisplayName}."
                + $"\nReach {definition.Reach:0.00}, speed {definition.Speed:P0}, "
                + $"sticks {definition.Persistence:P0}, swings {definition.Volatility:P0}.";

            return tile;
        }

        /// <summary>Audience, term, the bill, and the button that commits to it.</summary>
        private VisualElement BuildBookingPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("BOOK A CAMPAIGN");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var audiences = new VisualElement();
            audiences.AddToClassList("rfund__modes");

            foreach (var audience in AudienceCatalog.All)
            {
                var segment = audience.Segment;
                var chip = new Button(() => { pickedAudience = segment; Show(Screen.Marketing); })
                { text = audience.DisplayName.ToUpperInvariant() };

                chip.AddToClassList("chip");
                chip.EnableInClassList("chip--on", pickedAudience == segment);
                audiences.Add(chip);
            }

            panel.Add(audiences);

            var terms = new VisualElement();
            terms.AddToClassList("rfund__modes");

            foreach (var months in MarketingCatalog.TermsInMonths)
            {
                var term = months;
                var label = months <= 0 ? "OPEN ENDED" : $"{months} MONTH" + (months > 1 ? "S" : string.Empty);

                var chip = new Button(() => { pickedTerm = term; Show(Screen.Marketing); })
                { text = label };

                chip.AddToClassList("chip");
                chip.EnableInClassList("chip--on", pickedTerm == term);
                terms.Add(chip);
            }

            panel.Add(terms);

            var draft = new MarketingCampaign(pickedChannels, pickedAudience, pickedTerm,
                simulation.State.Date);

            var daily = draft.DailyCostUsd;
            var total = draft.IsOpenEnded ? 0L : daily * draft.DaysBooked;

            var bill = new Label(pickedChannels.Count == 0
                ? "Pick at least one channel."
                : draft.IsOpenEnded
                    ? $"{UiFormat.Money(daily)} a day, until you stop it. "
                        + $"{MarketingCatalog.OpenEndedSurcharge:P0} of the committed rate, because "
                        + "nobody sells an open contract at the price of a booked one."
                    : $"{UiFormat.Money(daily)} a day for {draft.DaysBooked} days, "
                        + $"{UiFormat.Money(total)} in total.");

            bill.AddToClassList("field__label");
            panel.Add(bill);

            var book = new Button(() =>
            {
                if (pickedChannels.Count == 0)
                {
                    return;
                }

                simulation.State.AddCampaign(new MarketingCampaign(
                    pickedChannels, pickedAudience, pickedTerm, simulation.State.Date));

                pickedChannels.Clear();
                Show(Screen.Marketing);
            })
            { text = "BOOK IT" };

            book.AddToClassList("button");
            book.AddToClassList("button--primary");
            book.style.marginLeft = 0;
            book.SetEnabled(pickedChannels.Count > 0);
            panel.Add(book);

            return panel;
        }

        private VisualElement BuildRunningPanel()
        {
            var state = simulation.State;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("RUNNING");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            if (state.Campaigns.Count == 0)
            {
                var none = new Label("Nothing booked. Only the people already using the service have "
                    + "heard of you.");

                none.AddToClassList("field__hint");
                panel.Add(none);
                return panel;
            }

            foreach (var campaign in state.Campaigns)
            {
                var row = new VisualElement();
                row.AddToClassList("run-row");

                var names = new List<string>();
                foreach (var channel in campaign.Channels)
                {
                    names.Add(MarketingCatalog.Get(channel).DisplayName);
                }

                var what = new Label(string.Join(" + ", names)
                    + $"  to {AudienceCatalog.Get(campaign.Target).DisplayName}");

                what.AddToClassList("run-row__what");
                row.Add(what);

                var left = new Label(campaign.IsOpenEnded
                    ? "open ended"
                    : $"{campaign.DaysLeft(state.Date)} days left");

                left.AddToClassList("run-row__left");
                row.Add(left);

                var cost = new Label(UiFormat.Money(campaign.DailyCostUsd) + "/day");
                cost.AddToClassList("run-row__cost");
                row.Add(cost);

                var stop = new Button(() =>
                {
                    simulation.State.RemoveCampaign(campaign);
                    Show(Screen.Marketing);
                })
                { text = "STOP" };

                stop.AddToClassList("chip");
                row.Add(stop);

                panel.Add(row);
            }

            return panel;
        }

        private VisualElement BuildFleetScreen()
        {
            var profile = simulation.Profile;
            var market = simulation.Market;

            var page = NewPage("FLEET",
                $"{UiFormat.Petaflops(profile.RawPetaflops)} nameplate, "
                + $"{UiFormat.Petaflops(profile.EffectivePetaflops)} usable. "
                + $"{UiFormat.Money(profile.DailyOperatingCostUsd is var c ? (long)c : 0)} a day to run, "
                + $"{UiFormat.Money((long)profile.DailyDepreciationUsd)} a day in value lost.");

            page.Add(BuildHostingSwitch());
            page.Add(BuildServicePanel());
            page.Add(BuildPackagePanel());
            page.Add(BuildFleetBill(profile));

            var topRow = new VisualElement();
            topRow.AddToClassList("panel-row");

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
            topRow.Add(rental);

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

            topRow.Add(ladder);
            page.Add(topRow);

            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("panel-row");

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

            bottomRow.Add(owned);

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

            bottomRow.Add(buy);
            page.Add(bottomRow);
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

            page.Add(BuildDebtPanel());
            return page;
        }

        /// <summary>
        /// Borrowing.
        ///
        /// **The whole debt system existed and had no button anywhere.** `LoanBook`, `LoanCatalog`,
        /// `DebtTests` and four kinds of event were all written and reachable only from a test, while
        /// the capital screen offered equity and nothing else.
        ///
        /// It belongs beside equity rather than on a screen of its own, because they are the same
        /// decision seen from two sides: a round costs a share of everything the company ever earns
        /// and never has to be repaid, and a facility costs a fixed sum on a fixed date whether or
        /// not the quarter went well. Putting them on one screen is what makes that a choice.
        /// </summary>
        private VisualElement BuildDebtPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("BORROWING");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var book = state.Loans;
            var servicing = new Label(book.OpenCount == 0
                ? "Nothing drawn. A facility is cash now against a fixed sum on a fixed date, whether "
                    + "or not the quarter went well."
                : $"Servicing {book.OpenCount} of {LoanCatalog.MaximumConcurrentLoans} facilities, "
                    + $"{UiFormat.Money(DailyDebtServiceUsd())} a day.");

            servicing.AddToClassList("field__hint");
            panel.Add(servicing);

            foreach (var open in book.Loans)
            {
                var definition = LoanCatalog.Get(open.Product);

                var row = new VisualElement();
                row.AddToClassList("loan-open");

                var name = new Label(definition.DisplayName.ToUpperInvariant());
                name.AddToClassList("loan-open__name");
                row.Add(name);

                var left = new Label(
                    $"{UiFormat.Money(open.OutstandingUsd)} left of "
                    + $"{UiFormat.Money(definition.TotalRepaymentUsd)}");

                left.AddToClassList("loan-open__left");
                row.Add(left);

                panel.Add(row);
            }

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            panel.Add(grid);

            foreach (var offer in simulation.LoanOffers())
            {
                grid.Add(BuildLoanCard(offer));
            }

            return panel;
        }

        /// <summary>What every open facility costs today, summed. The book holds them one by one.</summary>
        private long DailyDebtServiceUsd()
        {
            var total = 0L;
            foreach (var loan in state.Loans.Loans)
            {
                total += loan.DueToday(state.Date);
            }

            return total;
        }

        private VisualElement BuildLoanCard(LoanAvailability offer)
        {
            var definition = LoanCatalog.Get(offer.Product);

            var card = new Button(() =>
            {
                simulation.TryTakeLoan(offer.Product, out _);
                Show(Screen.Funding);
            });

            card.AddToClassList("card");
            card.EnableInClassList("card--ahead", offer.IsAvailable);

            var title = new Label(definition.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var principal = new Label(UiFormat.Money(definition.PrincipalUsd) + " now");
            principal.AddToClassList("card__line");
            card.Add(principal);

            // Both halves of the price, because the multiple alone hides the schedule and the daily
            // instalment alone hides what it adds up to.
            var terms = new Label(
                $"Repay {UiFormat.Money(definition.TotalRepaymentUsd)} over "
                + $"{UiFormat.Days(definition.TermDays)}, "
                + $"{UiFormat.Money(definition.DailyInstalmentUsd)} a day");

            terms.AddToClassList("card__line");
            card.Add(terms);

            var grace = new Label(offer.IsAvailable
                ? $"{UiFormat.Days(definition.GraceDays)} before the first instalment"
                : offer.Reason);

            grace.AddToClassList("card__line");
            grace.EnableInClassList("card__line--blocked", !offer.IsAvailable);
            card.Add(grace);

            card.tooltip = definition.Description;
            card.SetEnabled(offer.IsAvailable);
            return card;
        }

        private VisualElement BuildRankingScreen()
        {
            var page = NewPage("RANKING",
                "Capability, market share and brand, weighted. Every number here is the same one the "
                + "revenue side runs on, so a position on this board and an income statement cannot disagree.");

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("rank-grid");
            page.Add(panel);

            foreach (var entry in simulation.Ranking())
            {
                var captured = entry;

                // A row that opens the company behind it. The board was twelve rows of arithmetic:
                // a player could see a lab was ahead and had no way of knowing what it was for or
                // whether it was about to fall over, which is most of what makes rivals interesting.
                var row = new Button(() => ShowLabDossier(captured.Competitor));
                row.AddToClassList("rank-row");
                row.EnableInClassList("rank-row--mine", entry.IsPlayer);
                row.SetEnabled(!entry.IsPlayer);

                var place = new Label(entry.Position.ToString());
                place.AddToClassList("rank-row__place");
                row.Add(place);

                // The mark is what makes a board of nine names scannable. Nine rows of text read as
                // a list to be searched; nine marks read as a field the eye can find itself in.
                row.Add(LabLogos.Badge(entry.Competitor, entry.LabName, entry.IsPlayer));

                var text = new VisualElement();
                text.AddToClassList("rank-row__text");

                var name = new Label(entry.LabName);
                name.AddToClassList("rank-row__name");
                text.Add(name);

                var model = new Label(entry.ModelName);
                model.AddToClassList("rank-row__model");
                text.Add(model);

                row.Add(text);

                var score = new Label(UiFormat.Number(entry.Score));
                score.AddToClassList("rank-row__score");
                row.Add(score);

                var detail = new Label(
                    $"cap {UiFormat.Number(entry.Capability)}   "
                    + $"share {UiFormat.Percent(entry.MarketShare, 2)}");

                detail.AddToClassList("rank-row__detail");
                row.Add(detail);

                panel.Add(row);
            }

            return page;
        }

        /// <summary>
        /// Who a rival actually is: when they started, what they are for, and everything that has
        /// happened to them so far.
        ///
        /// **Only what has already happened.** A dossier that lists a collapse two years before it
        /// lands turns the field into a spoiler, so the chapters are filtered by today's date, the
        /// same rule the projection flag exists to protect. What the card can say is what the
        /// player could have read in a newspaper by now.
        /// </summary>
        private void ShowLabDossier(CompetitorId lab)
        {
            labCard?.RemoveFromHierarchy();

            if (!LabDossiers.TryGet(lab, out var dossier))
            {
                return;
            }

            labCard = new VisualElement();
            labCard.AddToClassList("dossier");

            var head = new VisualElement();
            head.AddToClassList("dossier__head");
            head.Add(LabLogos.Badge(lab, dossier.Name));

            var titles = new VisualElement();
            titles.AddToClassList("dossier__titles");

            var name = new Label(dossier.Name.ToUpperInvariant());
            name.AddToClassList("dossier__name");
            titles.Add(name);

            var founded = new Label(
                $"FOUNDED {dossier.Founded.Year}  ·  {dossier.Home.ToUpperInvariant()}  ·  "
                + FateWord(dossier, state.Date));

            founded.AddToClassList("dossier__meta");
            titles.Add(founded);

            head.Add(titles);

            var close = new Button(() => labCard?.RemoveFromHierarchy()) { text = "CLOSE" };
            close.AddToClassList("chip");
            head.Add(close);

            labCard.Add(head);

            var pitch = new Label(dossier.Positioning);
            pitch.AddToClassList("dossier__pitch");
            labCard.Add(pitch);

            var story = new Label(dossier.Story);
            story.AddToClassList("dossier__story");
            labCard.Add(story);

            var chapters = dossier.ChaptersBy(state.Date);
            if (chapters.Count > 0)
            {
                var heading = new Label("WHAT HAS HAPPENED");
                heading.AddToClassList("dossier__heading");
                labCard.Add(heading);

                var scroller = new ScrollView();
                scroller.AddToClassList("dossier__scroll");
                scroller.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scroller.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

                // Newest first. A company's most recent year is what a player is deciding against.
                for (var index = chapters.Count - 1; index >= 0; index--)
                {
                    scroller.Add(BuildChapterRow(chapters[index]));
                }

                labCard.Add(scroller);
            }
            else
            {
                var nothing = new Label(
                    "Nothing has happened to them yet that anybody outside the company would know "
                    + "about.");

                nothing.AddToClassList("dossier__story");
                labCard.Add(nothing);
            }

            shellRoot.Add(labCard);
        }

        private static VisualElement BuildChapterRow(LabChapter chapter)
        {
            var row = new VisualElement();
            row.AddToClassList("chapter");

            var rail = new VisualElement();
            rail.AddToClassList("chapter__rail");
            rail.AddToClassList(chapter.Kind switch
            {
                LabChapterKind.Scandal => "chapter__rail--bad",
                LabChapterKind.Setback => "chapter__rail--bad",
                LabChapterKind.Exit => "chapter__rail--end",
                LabChapterKind.Funding => "chapter__rail--money",
                _ => "chapter__rail--good"
            });

            row.Add(rail);

            var body = new VisualElement();
            body.AddToClassList("chapter__body");

            var when = new Label(chapter.On.ToString()
                + (chapter.IsProjection ? "   ·   PROJECTION" : string.Empty));

            when.AddToClassList("chapter__when");
            when.EnableInClassList("chapter__when--projection", chapter.IsProjection);
            body.Add(when);

            var headline = new Label(chapter.Headline);
            headline.AddToClassList("chapter__headline");
            body.Add(headline);

            var text = new Label(chapter.Body);
            text.AddToClassList("chapter__text");
            body.Add(text);

            row.Add(body);
            return row;
        }

        /// <summary>
        /// What the company is today, in one word, and only if the player could know it.
        ///
        /// The fate is authored for the whole arc, so printing it on day one would say that a lab
        /// is doomed years before anything has gone wrong with it.
        /// </summary>
        private static string FateWord(in LabDossier dossier, GameDate today)
        {
            var latest = LabChapterKind.Founding;
            var seen = false;

            foreach (var chapter in dossier.Chapters)
            {
                if (!chapter.HasHappenedBy(today))
                {
                    continue;
                }

                seen = true;
                if (chapter.Kind == LabChapterKind.Exit || chapter.Kind == LabChapterKind.Setback)
                {
                    latest = chapter.Kind;
                }
            }

            if (!seen)
            {
                return "INDEPENDENT";
            }

            return latest switch
            {
                LabChapterKind.Exit => dossier.Fate == LabFate.Absorbed ? "ABSORBED" : "CONSOLIDATED",
                LabChapterKind.Setback => "STRUGGLING",
                _ => "INDEPENDENT"
            };
        }

        private VisualElement BuildFeedScreen()
        {
            var page = NewPage("INTELLIGENCE",
                "What the research desk believes is coming. Confidence is what the desk claims about "
                + "itself, and it is always higher than how often the desk turns out to be right.");

            var tiers = new VisualElement();
            tiers.AddToClassList("panel");
            page.Add(tiers);

            // Memberships are bought and cancelled here and on the news screen, and both go through
            // the same call, because a retainer the player can start in one place and only stop in
            // another is a subscription trap rather than a decision.
            foreach (var tier in NewsCatalog.Memberships)
            {
                var captured = tier;
                var held = state.IsMember(tier);

                var button = new Button(() =>
                {
                    simulation.SetIntelSubscription(captured, !held);
                    Show(Screen.Feed);
                })
                {
                    text = (held ? "CANCEL  " : "JOIN  ")
                        + NewsCatalog.OutletName(tier).ToUpperInvariant()
                        + $"  {UiFormat.Money(IntelligenceService.MonthlyRetainerUsd(tier))}/MONTH"
                };

                button.AddToClassList("button");
                button.EnableInClassList("button--primary", held);
                button.style.marginBottom = 8;
                button.style.marginLeft = 0;
                button.style.width = Length.Percent(100);
                button.tooltip = NewsCatalog.OutletPitch(tier);
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

        /// <summary>
        /// Slides the incoming page up and in from the lower right, and sweeps a thin skewed band of
        /// the accent across behind it.
        ///
        /// Both are done by setting the finished state one frame after the starting state, because a
        /// USS transition only fires on a change and an element that is born at its target value has
        /// not changed. The sweep removes itself when it is done so nothing accumulates.
        /// </summary>
        private void PlayPageTransition()
        {
            contentHost.AddToClassList("content-host--entering");
            contentHost.schedule.Execute(() => contentHost.RemoveFromClassList("content-host--entering"))
                .ExecuteLater(16);

            var sweep = new VisualElement();
            sweep.AddToClassList("page-sweep");
            sweep.pickingMode = PickingMode.Ignore;
            HudAccent.PaintSlice(sweep, 0.1f, 0.9f);
            contentHost.Add(sweep);

            sweep.schedule.Execute(() => sweep.AddToClassList("page-sweep--gone")).ExecuteLater(16);
            sweep.schedule.Execute(() => sweep.RemoveFromHierarchy()).ExecuteLater(520);
        }

        /// <summary>Which photograph belongs under which heading. Pages with no art get none.</summary>
        private static string BannerFor(Screen screen) => screen switch
        {
            Screen.Business => "background_business",
            Screen.Funding => "background_funding",
            Screen.Ranking => "background_ranking",
            Screen.Release => "background_release",
            Screen.Upgrade => "background_upgrade",
            Screen.Research => "background_research",
            Screen.Family => "background_architecture",
            Screen.Fleet => "background_compute",
            Screen.Team => "background_team",
            _ => null
        };

        private VisualElement NewPage(string title, string subtitle)
        {
            var page = new VisualElement();
            page.AddToClassList("content");

            var heading = new Label(title);
            heading.AddToClassList("page-title");
            page.Add(heading);

            // The strip goes between the heading and the body, never behind the heading. Text over a
            // photograph is a legibility gamble even after the vignette; text above one is not.
            var banner = PageArt.BannerFor(BannerFor(current));
            if (banner != null)
            {
                page.Add(banner);
            }

            var sub = new Label(subtitle);
            sub.AddToClassList("page-subtitle");
            page.Add(sub);

            return page;
        }

        /// <summary>
        /// The office. Today it is a still of the room with the company written over it; the scene
        /// itself is built and lives in a prefab, and this is where it gets mounted once the camera
        /// and the render target are wired up.
        ///
        /// It is a screen rather than a background because everything that will eventually be
        /// clickable is in it: the people, the racks, the desk the player sits at.
        /// </summary>
        private VisualElement BuildSiteScreen()
        {
            var page = new VisualElement();
            page.AddToClassList("content");
            page.AddToClassList("site-page");

            // The office fills the screen and everything else is laid over it. The readouts are the
            // guests here, not the room: a tycoon that opens on a table of numbers has already told
            // the player what kind of game it thinks it is.
            var stage = new VisualElement();
            stage.AddToClassList("site-stage");

            // Bubbles lift off the desk while the lab is learning. Attached to the stage rather than
            // the page so they travel over the office rather than over the whole window, and rebuilt
            // with the screen because the screen is rebuilt on every tab change anyway.
            var bubbleHost = new VisualElement();
            stage.Add(bubbleHost);
            bubbles = new ResearchBubbles(bubbleHost, () => simulation.State.ResearchPointsToday);

            var view = Resources.Load<RenderTexture>("OfficeView");
            if (view != null)
            {
                stage.style.backgroundImage = Background.FromRenderTexture(view);
                stage.AddToClassList("site-stage--live");
            }
            else
            {
                var pending = new Label("THE OFFICE");
                pending.AddToClassList("site-stage__title");
                stage.Add(pending);

                var note = new Label("The room exists but the scene has not been rebuilt since it was "
                    + "wired up. Run Scaling Laws, Rebuild scenes.");
                note.AddToClassList("site-stage__note");
                stage.Add(note);
            }

            if (companyInfoOpen)
            {
                var overlay = new VisualElement();
                overlay.AddToClassList("site-overlay");

                var title = new Label(state.CompanyName.ToUpperInvariant());
                title.AddToClassList("page-title");
                overlay.Add(title);

                var subtitle = new Label(
                    $"{state.FounderName}, {WorldRegionCatalog.Get(state.HomeCountry).DisplayName}. "
                    + "Everything the company owns is in this room.");
                subtitle.AddToClassList("page-subtitle");
                overlay.Add(subtitle);

                var strip = new VisualElement();
                strip.AddToClassList("site-strip");
                strip.Add(SiteFigure("STAFF", state.Staff.Headcount.ToString()));
                strip.Add(SiteFigure("MODELS LIVE", state.DeployedModels.Count.ToString()));
                strip.Add(SiteFigure("CASH", UiFormat.Money(state.CashUsd)));
                strip.Add(SiteFigure("DAY", UiFormat.Days(state.Date.DayIndex)));
                overlay.Add(strip);

                stage.Add(overlay);

                // Born off to the left, then released a frame later so the transition has a change
                // to animate from. Same trick the page arrival uses.
                overlay.AddToClassList("site-overlay--entering");
                overlay.schedule.Execute(() => overlay.RemoveFromClassList("site-overlay--entering"))
                    .ExecuteLater(16);
            }

            // The two ways out of the room, one above the other in the corner.
            //
            // The office used to be a 260px plate across the bottom left: a slab of text laid over a
            // photograph of a room the player is already looking at. It is the same decision either
            // way, so it is the same size and shape as the map now, and the numbers that used to be
            // printed on it are in the card that opens under the cursor.
            var rail = new VisualElement();
            rail.AddToClassList("site-rail");

            var place = state.Staff.OfficeDefinition;

            var upgrade = new Button(() => Show(Screen.Offices));
            upgrade.AddToClassList("site-icon");
            SetIcon(upgrade, "Ui/office_upgrade", "OFFICE");

            InsightTip.Attach(upgrade, "THE OFFICE",
                $"Upgrade the office, rent or buy. You are in {place.DisplayName.ToLowerInvariant()}: "
                + $"{state.Staff.Headcount} of {place.Desks} desks at "
                + $"{UiFormat.Money(place.MonthlyRentUsd)} a month, and desks are what caps hiring.",
                InsightTip.Placement.LeftOf);

            rail.Add(upgrade);

            var map = new Button(() => Show(Screen.Ranking));
            map.AddToClassList("site-icon");
            SetIcon(map, "Ui/map", "MAP");

            InsightTip.Attach(map, "THE WORLD MAP",
                "Travel. Who is building what, and where. Today it opens the board; the drive out to "
                + "it is being built.",
                InsightTip.Placement.LeftOf);

            rail.Add(map);
            stage.Add(rail);

            page.Add(stage);
            return page;
        }

        /// <summary>
        /// Puts art on a control, or the word on it if the art is not there.
        ///
        /// A round button with nothing in it is indistinguishable from a rendering fault, and both
        /// of these open a screen the player needs.
        /// </summary>
        private static void SetIcon(Button button, string resourcePath, string fallback)
        {
            var art = Resources.Load<Texture2D>(resourcePath);
            if (art != null)
            {
                button.style.backgroundImage = new StyleBackground(art);
                return;
            }

            button.text = fallback;
        }

        private static VisualElement SiteFigure(string label, string value)
        {
            var figure = new VisualElement();
            figure.AddToClassList("site-figure");

            var caption = new Label(label);
            caption.AddToClassList("site-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("site-figure__value");
            figure.Add(amount);

            return figure;
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

        /// <summary>
        /// Keeps one compact banner per product on sale behind the lead one.
        ///
        /// The lead banner already describes the strongest model, so the followers start at the
        /// second. They are rebuilt only when the actual set of models changes: doing it per frame
        /// would churn several panels of elements sixty times a second for a list that changes a
        /// handful of times in a whole campaign, which is the mistake the creator's readouts made.
        /// </summary>
        private void RefreshFollowerBanners()
        {
            if (bannerStack == null)
            {
                return;
            }

            var marketed = simulation.MarketedModels();
            var wanted = Math.Max(0, marketed.Count - 1);

            var same = followerModels.Count == wanted;
            for (var index = 0; same && index < wanted; index++)
            {
                same = ReferenceEquals(followerModels[index], marketed[index + 1].Model);
            }

            if (!same)
            {
                foreach (var banner in followerBanners)
                {
                    banner.Root.RemoveFromHierarchy();
                }

                followerBanners.Clear();
                followerModels.Clear();

                for (var index = 0; index < wanted; index++)
                {
                    var model = marketed[index + 1].Model;
                    followerModels.Add(model);

                    // Read through a function so the banner follows the model rather than a snapshot
                    // taken at the moment it was built.
                    var banner = new ModelBanner(
                        () => StandingOf(model),
                        () => default,
                        Array.Empty<long>,
                        () => Show(Screen.Management),
                        true);

                    followerBanners.Add(banner);
                    bannerStack.Add(banner.Root);
                }
            }

            foreach (var banner in followerBanners)
            {
                banner.SetHidden(current != Screen.Site);
                banner.Refresh();
            }
        }

        /// <summary>This model's own standing, or an empty one once it stops being sold.</summary>
        private ProductStanding StandingOf(DeployedModel model)
        {
            foreach (var record in simulation.MarketedModels())
            {
                if (ReferenceEquals(record.Model, model))
                {
                    return simulation.ProductFor(record);
                }
            }

            return default;
        }

        private void DrainEvents()
        {
            while (state.TryDequeueEvent(out var companyEvent))
            {
                // A finished run is the one event that has to interrupt. Everything else can wait
                // for the player to look at the wire; this one leaves a decision sitting on a shelf
                // and the whole point of the shelf is that waiting on it costs position.
                if (companyEvent.Type == CompanyEventType.TrainingCompleted)
                {
                    ShowRunFinished(companyEvent.Message);
                }

                recentEvents.Add(companyEvent);
                if (recentEvents.Count > 60)
                {
                    recentEvents.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// The one notice that interrupts: a run has finished and is waiting on the shelf.
        ///
        /// It offers the two things worth doing with it rather than only an acknowledgement, because
        /// a dialog whose only button is OK has told the player something and then made them find
        /// the screen themselves. Clicking anywhere off it dismisses it, same as the research card.
        /// </summary>
        private void ShowRunFinished(string message)
        {
            runFinished?.RemoveFromHierarchy();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");

            // The veil is the dismiss target. Clicks inside the card stop there, so only a click on
            // the darkened area outside it closes the thing.
            veil.RegisterCallback<ClickEvent>(_ => runFinished?.RemoveFromHierarchy());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var title = new Label("THE RUN HAS FINISHED");
            title.AddToClassList("notice__title");
            card.Add(title);

            var body = new Label(string.IsNullOrWhiteSpace(message)
                ? "A training run has completed and is waiting on the shelf."
                : message);

            body.AddToClassList("notice__body");
            card.Add(body);

            var note = new Label("It scores what it scores from today. Waiting costs nothing "
                + "directly and costs position every day, because par keeps rising under it.");

            note.AddToClassList("notice__note");
            card.Add(note);

            var buttons = new VisualElement();
            buttons.AddToClassList("notice__buttons");

            var release = new Button(() =>
            {
                runFinished?.RemoveFromHierarchy();
                Show(Screen.Release);
            })
            { text = "GO TO RELEASE" };

            release.AddToClassList("notice__button");
            release.AddToClassList("notice__button--go");
            buttons.Add(release);

            var upgrade = new Button(() =>
            {
                runFinished?.RemoveFromHierarchy();
                Show(Screen.Upgrade);
            })
            { text = "GO TO UPGRADE" };

            upgrade.AddToClassList("notice__button");
            buttons.Add(upgrade);

            var ok = new Button(() => runFinished?.RemoveFromHierarchy()) { text = "OK" };
            ok.AddToClassList("notice__button");
            buttons.Add(ok);

            card.Add(buttons);
            veil.Add(card);

            runFinished = veil;
            shellRoot.Add(veil);
        }

        private void RefreshChrome()
        {
            cashLabel.text = UiFormat.Money(state.CashUsd);
            RefreshCashArrows(state);
            RefreshStanding(state);

            pointsLabel.text = UiFormat.Number(state.ResearchPoints, 0);
            pointsButton.tooltip = state.ResearchPointsToday > 0.0
                ? $"Earning {state.ResearchPointsToday:N1} a day. Click to spend them."
                : "Nothing is being built and no funding is set, so nothing is being learned.";
            // Only on the site screen. It sat on top of the model creator and the research tree,
            // which is the same mistake the counter it replaced made.
            modelBanner?.SetHidden(current != Screen.Site);

            // Same rule as the model banner: the corner belongs to the office. On any other screen
            // it would sit on top of the page rather than beside it.
            newsBanner?.SetHidden(current != Screen.Site);
            newsBanner?.Refresh();

            RefreshFollowerBanners();

            // Letters waiting on an answer, not letters unread: an unread notice is not a task, and
            // a badge that counts things the player cannot act on trains them to ignore the badge.
            var waiting = 0;
            foreach (var letter in state.Mail.All)
            {
                if (!letter.IsClosed && letter.Actions.Count > 0)
                {
                    waiting++;
                }
            }

            hud?.SetBadge(Screen.Mail, waiting);
            modelBanner?.Refresh();
            valuationLabel.text = $"valued {UiFormat.Money(simulation.CurrentValuationUsd())}";
            companyLabel.text = state.CompanyName;
            dateLabel.text = state.Date.ToString();

            var position = RankingBoard.PlayerPosition(simulation.Ranking());
            rankLabel.text = position > 0 ? $"rank #{position}" : "unranked";

            hud.Refresh(state.Date, clock.Speed, clock.DayProgress);

        }
    }
}
