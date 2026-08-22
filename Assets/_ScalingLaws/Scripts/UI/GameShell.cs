using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Where a version gets its name and its price. Opened from UPGRADE.</summary>
        private ReleasePlanPanel releasePlan;
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
        private FounderPresence founder;

        /// <summary>What the generated game scene calls the room the office camera looks at.</summary>
        private const string OfficeStageRoot = "OfficeRoom";

        /// <summary>The room on screen. Null in tests, where there is no scene to find it in.</summary>
        private OfficeStage officeStage;

        /// <summary>The three hiring sites. Owns its own shortlists so they survive a redraw.</summary>
        private HiringPortals portals;

        /// <summary>The product landing page. Counts its own visits, for the opening flourish.</summary>
        private ModelDashboard modelHub;

        /// <summary>The Agency-or-Specialist card, while it is up.</summary>
        private VisualElement hiringChoice;

        /// <summary>The green corner strip while somebody is being contacted.</summary>
        private VisualElement approachBanner;

        /// <summary>The colour-cycling strip while post-training work is running.</summary>
        private VisualElement upgradeBanner;

        /// <summary>The who-works-here card, while it is up.</summary>
        private VisualElement rosterCard;

        /// <summary>The phone. Rings once on a new company and comes back for story beats.</summary>
        private PhonePanel phone;

        /// <summary>Emil talking over the game while the tour runs.</summary>
        private GuideOverlay guide;

        /// <summary>The quiet strip under the corner banners with the next task on it.</summary>
        private TaskBanner tasks;

        /// <summary>The opening drive-in, while it is running. Null afterwards.</summary>
        private ArrivalSequence arrival;

        /// <summary>Days done on the programme the banner is drawn for. Stops it rebuilding per frame.</summary>
        private int upgradeBannerDays = -1;

        /// <summary>Which of the three colours the strip is wearing. Cycles on its own schedule.</summary>
        private int upgradeBannerTint;

        /// <summary>Days left on the approach the banner is drawn for. Stops it rebuilding per frame.</summary>
        private int approachBannerDays = -1;
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

        // Rebuilt on the day count rather than every frame: the flash is a CSS animation and
        // rebuilding restarts it, which at sixty frames a second is a solid colour.
        private VisualElement regulatoryBanner;
        private int regulatoryDay = -1;

        // Research has its own corner now. Same day-count rebuild rule as the regulatory banner.
        private VisualElement researchBanner;
        private int researchBannerDay = -1;
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

            // **Research is deliberately not here any more.** This banner is the product, and it
            // swaps itself out for whatever it is told is in flight. With a model on sale and a node
            // running, the research took the banner and the product disappeared: no users, no mood,
            // no way in to the management desk, for four months. Research has its own strip below.
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

        /// <summary>Whether the furniture shop is laid over the office.</summary>
        private bool decorOpen;

        /// <summary>What the last shop action reported, or empty. Cleared when the panel opens.</summary>
        private string decorProblem = string.Empty;
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

            /// <summary>One of the three hiring sites. Which one is on <c>portals.Open</c>.</summary>
            Hiring,

            /// <summary>
            /// What MODEL opens on: the product, what it earns, and the two ways to change it.
            /// The training designer is one click further in, behind NEW MODEL.
            /// </summary>
            Model,

            /// <summary>
            /// Naming and pricing a version before it ships. Reached from UPGRADE and from
            /// nowhere else, because it always carries a basket built on that screen.
            /// </summary>
            ReleasePlan,

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
                state.FounderLook = SceneFlow.RequestedFounderLook ?? string.Empty;
                state.FounderGlasses = Math.Max(0, SceneFlow.RequestedFounderGlasses);
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
            // UPGRADE hands its basket to the planner rather than commissioning anything itself,
            // so the version is named and priced before a single day of work is paid for.
            upgrades = new UpgradeGridPanel(simulation,
                (index, traits) =>
                {
                    releasePlan.Open(index, traits);
                    Show(Screen.ReleasePlan);
                },
                () => Show(Screen.Site));

            releasePlan = new ReleasePlanPanel(simulation, ShipTheVersion,
                () => Show(Screen.Upgrade));
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

            // The founder decides the clock's pace, in one direction only: the day sweeps six
            // times faster while they are asleep upstairs. Presentation, not simulation. The same
            // days happen and they take less of the player's evening.
            founder?.Refresh(state.Date.DayIndex);
            RefreshRegulatoryBanner();
            RefreshResearchBanner();
            RefreshApproachBanner();
            RefreshUpgradeBanner();
            tasks.Refresh();
            RingTheCousinIfThisIsDayOne();

            // They reached the car. This is where the loading screen and the world map go once the
            // map itself exists; until then it opens the board, which is what the icon did before.
            if (founder != null && founder.HasReachedTheCar && current == Screen.Site)
            {
                founder.ComeBack();
                Show(Screen.Ranking);
            }

            var days = clock.Advance(Time.unscaledDeltaTime * (founder?.TimeScale ?? 1f));

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
                // **Keep the reading position.** A day rolls over every second and a half at normal
                // speed, and the page is rebuilt each time, so a player half way down the research
                // tree was thrown back to the top before they could finish a sentence.
                var wasAt = OpenScrollOffset();
                Show(current);
                RestoreScrollOffset(wasAt);
            }
        }

        /// <summary>Where the open page is scrolled to, or zero when it does not scroll.</summary>
        private Vector2 OpenScrollOffset()
        {
            var scroller = contentHost?.Q<ScrollView>();
            return scroller != null ? scroller.scrollOffset : Vector2.zero;
        }

        /// <summary>
        /// Puts the reading position back after a rebuild.
        ///
        /// Deferred a frame on purpose: the new page has not been laid out yet when this is called,
        /// so its scroller has no range and setting an offset against a zero-height content does
        /// nothing at all.
        /// </summary>
        private void RestoreScrollOffset(Vector2 offset)
        {
            if (offset == Vector2.zero)
            {
                return;
            }

            var scroller = contentHost?.Q<ScrollView>();
            scroller?.schedule.Execute(() => scroller.scrollOffset = offset).ExecuteLater(1);
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

            // The room has had an empty Staff group since the day it was generated. Somebody lives
            // here now.
            // The room the office camera points at, which changes with the lease. Built before the
            // founder spawns so they walk into the floor the company is actually renting rather
            // than into the garage it left three years ago.
            portals = new HiringPortals(() => state, simulation, () => Show(Screen.Hiring),
                () => Show(Screen.Mail));

            phone = new PhonePanel(root, AnswerTheCousin);

            // The opening: dark room, headlights, the car reversing in, then the lamps one by one.
            // Only on a company that has never been played, and only when the office scene is
            // actually there — a missing prefab means a lit room and no sequence, never a hang.
            if (state.Guide.Stage == GuideStage.Unseen)
            {
                var room = GameObject.Find(OfficeStageRoot);

                if (room != null)
                {
                    arrival = gameObject.AddComponent<ArrivalSequence>();

                    if (!arrival.Prepare(room.transform, founder?.Model))
                    {
                        Destroy(arrival);
                        arrival = null;
                    }
                }
            }

            guide = new GuideOverlay(root, () => state.Guide, GoToGuideTarget, RefreshChrome);

            tasks = new TaskBanner(root, () => state, () => state.Guide, RefreshChrome);

            modelHub = new ModelDashboard(() => simulation, () => Show(Screen.Create),
                () => Show(Screen.Upgrade), () => Show(Screen.Release));

            officeStage = new OfficeStage(GameObject.Find(OfficeStageRoot));
            officeStage.Show(state.Staff.Office, state.Decor);

            founder = new FounderPresence(() => state);
            founder.Spawn();
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

            // The simulation is told where the furniture goes rather than working it out, because it
            // knows which tier the company is in and nothing about the shape of the room.
            offices = new OfficeChooser(
                () => simulation.State,
                (tier, furnished) =>
                    simulation.TryMoveOffice(tier, FurnishZone(tier, furnished), out var why)
                        ? string.Empty
                        : why,
                () => Show(Screen.Team),
                (tier, furnished) =>
                    simulation.TryBuyOffice(tier, FurnishZone(tier, furnished), out var why)
                        ? string.Empty
                        : why);

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

            hud.AddSlot("MODEL", Screen.Model, () => Show(Screen.Model), "hud_model",
                "What the company sells, what it earns, and the two ways to change it: build a new "
                + "one, or improve one already out there.");

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

                case Screen.Hiring:
                    host.Add(BuildHiringScreen());
                    break;

                case Screen.Model:
                    host.Add(modelHub.Build());
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

                case Screen.ReleasePlan:
                    releasePlan.Refresh();
                    host.Add(releasePlan.Root);
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

            // The tour is drawn last, over whatever the new screen turned out to be. Its highlight
            // is a query against the live tree, so it has to run after the page exists — refreshing
            // it before the rebuild rings elements that are about to be thrown away.
            guide?.Refresh();
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
                    // The capability line, on a board you can lean into. It opens showing the whole
                    // era, because a map that starts zoomed in hides the thing the player came for;
                    // the wheel and the drag are for leaning closer, not for finding your way back.
                    var map = new ResearchMap();

                    var track = new VisualElement();
                    track.AddToClassList("tree-track");

                    var spine = new VisualElement();
                    spine.AddToClassList("tree-spine");
                    track.Add(spine);

                    for (var index = 0; index < nodes.Count; index++)
                    {
                        track.Add(BuildTreeNode(nodes[index], index % 2 == 0));
                    }

                    map.Surface.Add(track);
                    section.Add(map);
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
        /// <summary>
        /// Team and office on one screen, because they are one decision: desks cap headcount, so a
        /// lease signed months ago is what decides whether the person you need today can start.
        ///
        /// **The hiring grid is one tile per founder skill.** The player already learned those seven
        /// words when they spent two hundred points at character creation; making them learn a
        /// second vocabulary to hire would be asking twice for the same thing.
        /// </summary>
        private VisualElement BuildTeamScreen()
        {
            var roster = state.Staff;

            var page = NewPage("TEAM",
                $"{roster.SeatedHeadcount} of {roster.Desks} desks in {roster.OfficeDefinition.DisplayName}"
                + (roster.CountFrom(HireSource.Remote) > 0
                    ? $", plus {roster.CountFrom(HireSource.Remote)} working remotely"
                    : string.Empty)
                + $". Payroll {UiFormat.Money(roster.DailyPayrollUsd)} a day. Every discipline "
                + "saturates, so a seventh person in one adds a fraction of what the second did.");

            page.Add(BuildPositionGrid());
            page.Add(BuildHireButtons());

            if (roster.Headcount > 0)
            {
                page.Add(BuildPayrollList());
            }

            // The two bottom panels share a line. What the team is worth is a table of six
            // readings and wants the width; where you work is one line and a picture and does not.
            var bottom = new VisualElement();
            bottom.AddToClassList("team__bottom");

            var effects = new VisualElement();
            effects.AddToClassList("panel");
            effects.AddToClassList("team__worth");

            var effectsHeading = new Label("WHAT THE TEAM IS WORTH");
            effectsHeading.AddToClassList("panel__heading");
            effects.Add(effectsHeading);

            effects.Add(Row("Training outcome spread",
                $"{UiFormat.Percent(roster.OutcomeVarianceMultiplier())} of baseline"));
            effects.Add(Row("Cluster utilization", $"+{UiFormat.Percent(roster.UtilizationBonus())}"));
            effects.Add(Row("Data quality", $"x{UiFormat.Number(roster.DataQualityMultiplier(), 3)}"));
            effects.Add(Row("Incident risk", $"x{UiFormat.Number(roster.IncidentRiskMultiplier(), 2)}"));
            effects.Add(Row("Brand from the team", $"+{UiFormat.Number(roster.BrandBonus(), 3)}"));
            effects.Add(Row("Research pace", $"x{UiFormat.Number(roster.ResearchSpeedMultiplier(), 3)}"));
            bottom.Add(effects);

            var offices = new VisualElement();
            offices.AddToClassList("panel");
            offices.AddToClassList("team__where");

            var officeHeading = new Label("WHERE YOU WORK");
            officeHeading.AddToClassList("panel__heading");
            offices.Add(officeHeading);

            var current = state.Staff.OfficeDefinition;

            var where = new Label(
                $"LVL {current.Level}  ·  {current.DisplayName}  ·  "
                + $"{state.Staff.SeatedHeadcount} of {current.Desks} desks  ·  "
                + $"{UiFormat.Money(current.MonthlyRentUsd)} a month");

            where.AddToClassList("office-now");
            offices.Add(where);

            offices.Add(BuildUpgradeButton());
            bottom.Add(offices);

            page.Add(bottom);
            return page;
        }

        /// <summary>
        /// Seven tiles, one per discipline, in two rows.
        ///
        /// The count sits in a ring in the corner in the position's own colour, so the shape of the
        /// company is readable without reading a single word: four blue and nothing else is a lab
        /// that has never hired anybody to sell anything.
        /// </summary>
        private VisualElement BuildPositionGrid()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("POSITIONS");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var grid = new VisualElement();
            grid.AddToClassList("posgrid");

            foreach (var position in PositionCatalog.All)
            {
                grid.Add(BuildPositionTile(position));
            }

            panel.Add(grid);
            return panel;
        }

        private VisualElement BuildPositionTile(PositionDefinition position)
        {
            var count = state.Staff.CountOfPosition(position.Skill);

            // A button rather than a plate. The count was already the most useful thing on the
            // tile and it was the one thing you could not act on: seeing "3" and having no way to
            // find out who the three are is a dead end on the screen that is about people.
            var tile = new Button(() => ShowRoster(position.Skill));
            tile.AddToClassList("postile");
            tile.EnableInClassList("postile--staffed", count > 0);
            tile.SetEnabled(count > 0);

            if (ColorUtility.TryParseHtmlString(position.AccentHex, out var accent))
            {
                tile.style.borderLeftColor = accent;
            }

            // 52 to 62: the tiles got narrower so all seven fit on one line, and the icon was
            // the one thing that must not shrink with them.
            var icon = SkillIcons.Badge(position.Skill, 62);
            icon.AddToClassList("postile__icon");
            tile.Add(icon);

            var title = new Label(position.Title.ToUpperInvariant());
            title.AddToClassList("postile__title");
            tile.Add(title);

            var blurb = new Label(position.Blurb);
            blurb.AddToClassList("postile__blurb");
            tile.Add(blurb);

            // The ring is out of flow so it sits in the corner rather than pushing the title down.
            var ring = new VisualElement();
            ring.AddToClassList("postile__ring");

            if (ColorUtility.TryParseHtmlString(position.AccentHex, out var ringColour))
            {
                ring.style.borderTopColor = ringColour;
                ring.style.borderBottomColor = ringColour;
                ring.style.borderLeftColor = ringColour;
                ring.style.borderRightColor = ringColour;
                ring.style.color = ringColour;
            }

            var number = new Label(count.ToString());
            number.AddToClassList("postile__count");
            ring.Add(number);

            tile.Add(ring);

            InsightTip.Attach(tile, position.Title.ToUpperInvariant(),
                $"{position.Blurb} An ordinary one asks about ${position.BaseHourlyWageUsd:N0} an "
                + $"hour. {count} on the team.");

            return tile;
        }

        /// <summary>
        /// Everybody in one discipline, over the screen.
        ///
        /// Built on the same card the finished-run notice uses, because it is the same kind of
        /// moment: something the player asked to look at, over the top of what they were doing,
        /// dismissed by clicking away from it. Reusing that shape means one veil, one card, one set
        /// of manners, rather than a second modal that behaves almost the same.
        /// </summary>
        private void ShowRoster(PlayerSkill position)
        {
            rosterCard?.RemoveFromHierarchy();

            var definition = PositionCatalog.Get(position);
            var people = new List<int>();

            for (var index = 0; index < state.Staff.Headcount; index++)
            {
                if (state.Staff.Hires[index].Position == position)
                {
                    people.Add(index);
                }
            }

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => rosterCard?.RemoveFromHierarchy());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("roster");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var head = new VisualElement();
            head.AddToClassList("roster__head");

            var icon = SkillIcons.Badge(position, 54);
            icon.AddToClassList("roster__icon");
            head.Add(icon);

            var words = new VisualElement();
            words.AddToClassList("roster__words");

            var title = new Label(definition.Title.ToUpperInvariant());
            title.AddToClassList("roster__title");
            words.Add(title);

            var under = new Label(people.Count == 1
                ? "One person, and what they cost."
                : $"{people.Count} people, and what they cost.");

            under.AddToClassList("roster__under");
            words.Add(under);
            head.Add(words);

            if (ColorUtility.TryParseHtmlString(definition.AccentHex, out var accent))
            {
                card.style.borderLeftColor = accent;
                title.style.color = accent;
            }

            card.Add(head);

            var list = new ScrollView();
            list.AddToClassList("roster__list");

            foreach (var slot in people)
            {
                list.Add(BuildRosterRow(slot));
            }

            card.Add(list);

            var close = new Button(() => rosterCard?.RemoveFromHierarchy()) { text = "CLOSE" };
            close.AddToClassList("notice__button");
            card.Add(close);

            veil.Add(card);
            rosterCard = veil;
            shellRoot.Add(veil);
        }

        private VisualElement BuildRosterRow(int slot)
        {
            var hire = state.Staff.Hires[slot];
            var channel = HiringChannels.Get(hire.Source);

            var row = new VisualElement();
            row.AddToClassList("rperson");

            var tag = new Label(channel.DisplayName.ToUpperInvariant());
            tag.AddToClassList("rperson__tag");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var tint))
            {
                tag.style.color = tint;
                tag.style.borderTopColor = tint;
                tag.style.borderBottomColor = tint;
                tag.style.borderLeftColor = tint;
                tag.style.borderRightColor = tint;
            }

            row.Add(tag);

            var words = new VisualElement();
            words.AddToClassList("rperson__words");

            var name = new Label(hire.Label);
            name.AddToClassList("rperson__name");
            words.Add(name);

            var since = new Label(hire.HourlyWageUsd > 0.0
                ? $"${hire.HourlyWageUsd:N2} an hour  ·  since {hire.StartedOn}"
                : $"{UiFormat.Money(hire.SalaryPerYearUsd)} a year  ·  since {hire.StartedOn}");

            since.AddToClassList("rperson__since");
            words.Add(since);
            row.Add(words);

            // The way into their own page, which does not exist yet. It is here rather than absent
            // because the row is the only place it will ever belong, and a disabled control that
            // says what it is for is a promise; a missing one is a redesign later.
            var open = new Button { text = "DETAILS" };
            open.AddToClassList("rperson__open");
            open.SetEnabled(false);
            open.tooltip = "Their own page is not built yet.";
            row.Add(open);

            var release = new Button(() =>
            {
                simulation.TryLetGo(slot, out _);
                rosterCard?.RemoveFromHierarchy();
                Show(Screen.Team);
            })
            { text = "LET GO" };

            release.AddToClassList("rperson__release");
            row.Add(release);

            return row;
        }

        /// <summary>
        /// The two ways to start hiring, under the grid.
        ///
        /// Both say what they cost the player before they are pressed: how many desks are free, and
        /// how many remote contracts are left. A hire button that opens a screen only to say no
        /// wastes the click that was the whole point of the screen.
        /// </summary>
        private VisualElement BuildHireButtons()
        {
            var row = new VisualElement();
            row.AddToClassList("hirebar");

            var free = Math.Max(0, state.Staff.Desks - state.Staff.SeatedHeadcount);

            var onSite = new Button(ShowHiringChoice)
            {
                text = $"HIRE NOW     -     ({free} workplace{(free == 1 ? string.Empty : "s")} available)"
            };

            onSite.AddToClassList("hirebar__button");
            onSite.AddToClassList("hirebar__button--main");
            onSite.SetEnabled(free > 0);

            InsightTip.Attach(onSite, "HIRE INTO THE OFFICE",
                "Two routes: the employment register, which is free and sends ordinary people, or "
                + "a specialist search, which costs a fee and finds exactly what you asked for.");

            row.Add(onSite);

            var seats = state.Hiring.RemoteSeats;
            var usedRemote = state.Staff.CountFrom(HireSource.Remote);

            var remote = new Button(() =>
            {
                portals.Open = HiringPortal.Remote;
                Show(Screen.Hiring);
            })
            { text = $"HIRE NOW - REMOTE ({seats - usedRemote})" };

            remote.AddToClassList("hirebar__button");
            remote.AddToClassList("hirebar__button--remote");
            remote.SetEnabled(usedRemote < seats);

            InsightTip.Attach(remote, "HIRE REMOTELY",
                $"IThand.hck. No desk needed, {HiringChannels.Get(HireSource.Remote).WageMultiplier:P0} "
                + "of the usual wage, and the people are much weaker than their profiles claim. "
                + "This is how a company with no office starts.");

            row.Add(remote);
            return row;
        }

        /// <summary>
        /// Who is on the payroll, and where each of them came from.
        ///
        /// The source is a coloured tag rather than a word in a sentence, because the one thing a
        /// player wants from this list at a glance is how much of their company is the cheap kind.
        /// </summary>
        private VisualElement BuildPayrollList()
        {
            var roster = state.Staff;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label("ON THE PAYROLL");
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var list = new VisualElement();
            list.AddToClassList("crew");

            for (var index = 0; index < roster.Headcount; index++)
            {
                var slot = index;
                var hire = roster.Hires[index];

                var row = new VisualElement();
                row.AddToClassList("crew__row");

                if (hire.Position != PlayerSkill.None)
                {
                    var icon = SkillIcons.Badge(hire.Position, 24);
                    icon.AddToClassList("crew__icon");
                    row.Add(icon);
                }

                var channel = HiringChannels.Get(hire.Source);

                var tag = new Label(channel.DisplayName.ToUpperInvariant());
                tag.AddToClassList("crew__tag");

                if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var accent))
                {
                    tag.style.color = accent;
                    tag.style.borderTopColor = accent;
                    tag.style.borderBottomColor = accent;
                    tag.style.borderLeftColor = accent;
                    tag.style.borderRightColor = accent;
                }

                row.Add(tag);

                var name = new Label(hire.Label);
                name.AddToClassList("crew__name");
                row.Add(name);

                var job = new Label(hire.Position != PlayerSkill.None
                    ? PositionCatalog.Get(hire.Position).Title
                    : StaffCatalog.Get(hire.Role).DisplayName);

                job.AddToClassList("crew__job");
                row.Add(job);

                var pay = new Label(hire.HourlyWageUsd > 0.0
                    ? $"${hire.HourlyWageUsd:N2}/h"
                    : UiFormat.Money(hire.SalaryPerYearUsd) + "/yr");

                pay.AddToClassList("crew__pay");
                row.Add(pay);

                var release = new Button(() =>
                {
                    simulation.TryLetGo(slot, out _);
                    Show(Screen.Team);
                })
                { text = "LET GO" };

                release.AddToClassList("crew__release");
                row.Add(release);

                list.Add(row);
            }

            panel.Add(list);
            return panel;
        }

        /// <summary>
        /// Agency or specialist, on a card over the screen.
        ///
        /// The same shape as the card that appears when a training run finishes, because it is the
        /// same moment: the player has committed to something and the game is asking which of two
        /// roads they want. Two very different prices, stated on the buttons.
        /// </summary>
        private void ShowHiringChoice()
        {
            hiringChoice?.RemoveFromHierarchy();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => hiringChoice?.RemoveFromHierarchy());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("notice--hiring");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var title = new Label("WHERE ARE YOU LOOKING?");
            title.AddToClassList("notice__title");
            card.Add(title);

            var body = new Label(
                "Both routes take two to four days to come back, and both end in the inbox with a "
                + "wage to agree. What differs is who answers.");

            body.AddToClassList("notice__body");
            card.Add(body);

            var choices = new VisualElement();
            choices.AddToClassList("hirechoice");

            choices.Add(BuildChoiceTile(HireSource.Agency, "EMPLOYMENT AGENCY",
                "Free to use. Sends whoever is on the register, and they are worse than their "
                + "paperwork says. Standard wages.",
                () =>
                {
                    hiringChoice?.RemoveFromHierarchy();
                    portals.Open = HiringPortal.Agency;
                    Show(Screen.Hiring);
                }));

            choices.Add(BuildChoiceTile(HireSource.Specialist, "FIND A SPECIALIST",
                "Costs a search fee whether or not they sign. You set the discipline and the "
                + "minimum level, and what arrives beats the advert. Wages a fifth higher.",
                () =>
                {
                    hiringChoice?.RemoveFromHierarchy();
                    portals.Open = HiringPortal.Specialist;
                    Show(Screen.Hiring);
                }));

            card.Add(choices);

            var cancel = new Button(() => hiringChoice?.RemoveFromHierarchy()) { text = "NOT NOW" };
            cancel.AddToClassList("notice__button");
            card.Add(cancel);

            veil.Add(card);
            hiringChoice = veil;
            shellRoot.Add(veil);
        }

        private VisualElement BuildChoiceTile(HireSource source, string title, string blurb,
            Action go)
        {
            var channel = HiringChannels.Get(source);

            var tile = new Button(go);
            tile.AddToClassList("hirechoice__tile");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var accent))
            {
                tile.style.borderLeftColor = accent;
            }

            var address = new Label(channel.SiteName);
            address.AddToClassList("hirechoice__url");
            tile.Add(address);

            var name = new Label(title);
            name.AddToClassList("hirechoice__title");
            tile.Add(name);

            var text = new Label(blurb);
            text.AddToClassList("hirechoice__blurb");
            tile.Add(text);

            var numbers = new Label(
                $"wage x{channel.WageMultiplier:0.00}   ·   quality x{channel.QualityMultiplier:0.00}");

            numbers.AddToClassList("hirechoice__numbers");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var tint))
            {
                numbers.style.color = tint;
            }

            tile.Add(numbers);
            return tile;
        }

        /// <summary>Whichever site the player walked into.</summary>
        private VisualElement BuildHiringScreen()
        {
            var page = new VisualElement();
            page.AddToClassList("content");

            page.Add(portals.Build());

            if (state.Hiring.OpenCount > 0)
            {
                page.Add(portals.InboxLink());
            }

            var back = new Button(() => Show(Screen.Team)) { text = "BACK TO THE TEAM" };
            back.AddToClassList("portal__back");
            page.Add(back);

            return page;
        }

        /// <summary>
        /// The strip while post-training work is running, and the one thing on screen that moves.
        ///
        /// **It cycles through three colours rather than sitting on one.** The other two corners are
        /// a state — there is a product, there is research — and they are still. This one is a job
        /// in flight on a model the player already sells, and the shift from green through pink to
        /// violet is what makes it read as something happening rather than something true.
        ///
        /// UI Toolkit has transitions but no keyframes, so the cycle is a scheduled class swap and
        /// the transition on the border does the blending. It shows the longest-running programme,
        /// because that is the one that decides when the player gets their model back.
        /// </summary>
        private void RefreshUpgradeBanner()
        {
            var projects = state.UpgradeProjects;
            var showing = projects.Count > 0 && current == Screen.Site;

            if (!showing)
            {
                upgradeBanner?.RemoveFromHierarchy();
                upgradeBanner = null;
                upgradeBannerDays = -1;
                return;
            }

            // Whichever finishes last. Two programmes on one model finish when the slower does, and
            // a banner counting down the quicker one would promise the model back too early.
            ModelUpgradeProject slowest = null;

            foreach (var project in projects)
            {
                if (slowest == null || project.DaysRemaining > slowest.DaysRemaining)
                {
                    slowest = project;
                }
            }

            if (slowest == null)
            {
                return;
            }

            if (upgradeBanner != null && upgradeBannerDays == slowest.DaysCompleted)
            {
                return;
            }

            upgradeBannerDays = slowest.DaysCompleted;
            upgradeBanner?.RemoveFromHierarchy();

            var banner = new Button(() => Show(Screen.Upgrade));
            banner.AddToClassList("ub");
            banner.AddToClassList(UpgradeTintClass(upgradeBannerTint));

            var kicker = new Label(projects.Count > 1
                ? $"WORKING ON UPGRADE  ({projects.Count})"
                : "WORKING ON UPGRADE");

            kicker.AddToClassList("ub__kicker");
            banner.Add(kicker);

            var subject = slowest.ModelIndex >= 0 && slowest.ModelIndex < state.DeployedModels.Count
                ? state.DeployedModels[slowest.ModelIndex].Name
                : "a model";

            var name = new Label(subject);
            name.AddToClassList("ub__name");
            banner.Add(name);

            var what = new Label(ModelTraitCatalog.Get(slowest.Trait).DisplayName);
            what.AddToClassList("ub__what");
            banner.Add(what);

            var track = new VisualElement();
            track.AddToClassList("ub__track");

            var fill = new VisualElement();
            fill.AddToClassList("ub__fill");
            fill.style.width = Length.Percent((float)(slowest.Progress * 100.0));
            track.Add(fill);

            banner.Add(track);

            var left = slowest.DaysRemaining;
            var days = new Label(left <= 0
                ? "finishing today"
                : left == 1 ? "1 day left" : $"{left} days left");

            days.AddToClassList("ub__days");
            banner.Add(days);

            // The cycle. Scheduled on the banner itself, so it dies with it and never leaves a
            // callback pointing at an element that has left the tree.
            banner.schedule.Execute(() =>
            {
                banner.RemoveFromClassList(UpgradeTintClass(upgradeBannerTint));
                upgradeBannerTint = (upgradeBannerTint + 1) % 3;
                banner.AddToClassList(UpgradeTintClass(upgradeBannerTint));
            }).Every(1400);

            upgradeBanner = banner;
            shellRoot.Add(banner);
        }

        private static string UpgradeTintClass(int step) => step switch
        {
            0 => "ub--green",
            1 => "ub--pink",
            _ => "ub--violet"
        };

        /// <summary>
        /// The phone rings once, on the first frame a brand new company is looked at.
        ///
        /// **Only on a company that has done nothing.** A save loaded mid-campaign must never be
        /// interrupted by a tutorial it already answered, and the stage on the guide is what says
        /// so — not a flag on the session, which would forget across a reload.
        /// </summary>
        private void RingTheCousinIfThisIsDayOne()
        {
            if (state.Guide.Stage != GuideStage.Unseen || phone.IsOpen)
            {
                return;
            }

            // The phone waits for the car. Ringing over a dark garage would throw away the one
            // piece of theatre the opening has.
            if (arrival != null && arrival.IsPlaying)
            {
                return;
            }

            state.Guide.Stage = GuideStage.Talking;
            state.Guide.StartingCashUsd = state.CashUsd;

            phone.Ring();
        }

        /// <summary>
        /// What happens when the player answers him.
        ///
        /// Either way the task strip takes over from the phone, because the three opening tasks are
        /// the shape of the first hour whether or not somebody wanted the tour.
        /// </summary>
        private void AnswerTheCousin(bool accepted)
        {
            state.Guide.Stage = accepted ? GuideStage.Touring : GuideStage.Finished;
            state.Guide.Step = 0;

            RefreshChrome();

            if (accepted)
            {
                guide.Refresh();
            }
        }

        /// <summary>
        /// Commissions the basket and publishes the version.
        ///
        /// **The version is published even when the work takes months.** What ships today is the
        /// name, the price and the allowance — users move onto those immediately — and the
        /// post-training programmes land on the same version as they finish. Holding the version
        /// back until the last programme completed would mean a price change nobody could make
        /// without also committing to three months of engineering.
        /// </summary>
        private void ShipTheVersion(string versionName)
        {
            var index = releasePlan.ModelIndex;

            if (index < 0 || index >= state.DeployedModels.Count)
            {
                Show(Screen.Upgrade);
                return;
            }

            var model = state.DeployedModels[index];
            var refused = new List<string>();

            foreach (var trait in releasePlan.Basket)
            {
                if (!simulation.TryStartUpgrade(index, trait, out var reason))
                {
                    refused.Add($"{ModelTraitCatalog.Get(trait).DisplayName}: {reason}");
                }
            }

            state.Monetization.SubscriptionPriceUsdPerMonth = releasePlan.PriceUsdPerMonth;
            state.Monetization.FreeTierTokensPerUserPerDay = releasePlan.FreeTokensPerDay;

            model.Line.Publish(versionName, state.Date, model.EffectiveCapability(state.Date),
                releasePlan.PriceUsdPerMonth, releasePlan.FreeTokensPerDay);

            simulation.State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ModelReleased, state.Date,
                refused.Count == 0
                    ? $"{model.Name} {versionName} shipped."
                    : $"{model.Name} {versionName} shipped, but {string.Join("  ", refused)}",
                0L));

            RefreshChrome();
            Show(Screen.Management);
        }

        /// <summary>Opens the screen a guide step is about.</summary>
        private void GoToGuideTarget(GuideTarget target)
        {
            switch (target)
            {
                case GuideTarget.Site: Show(Screen.Site); break;
                case GuideTarget.Compute: Show(Screen.Fleet); break;
                case GuideTarget.Model: Show(Screen.Model); break;
                case GuideTarget.Create: Show(Screen.Create); break;
                case GuideTarget.Research: Show(Screen.Research); break;
                case GuideTarget.Team: Show(Screen.Team); break;
                case GuideTarget.Release: Show(Screen.Release); break;
            }
        }

        /// <summary>
        /// The green strip while somebody is being contacted.
        ///
        /// Green because the other two corners are already taken and mean other things: gold is the
        /// product, blue is research. A third colour is cheaper to learn than a third position.
        ///
        /// It shows the soonest answer rather than all of them, for the same reason the research
        /// banner shows one node: a corner that grows a row per approach would cover the office.
        /// </summary>
        private void RefreshApproachBanner()
        {
            var soonest = state.Hiring.Soonest;
            var showing = soonest != null && current == Screen.Site;

            if (!showing)
            {
                approachBanner?.RemoveFromHierarchy();
                approachBanner = null;
                approachBannerDays = -1;
                return;
            }

            if (approachBanner != null && approachBannerDays == soonest.DaysElapsed)
            {
                return;
            }

            approachBannerDays = soonest.DaysElapsed;
            approachBanner?.RemoveFromHierarchy();

            var banner = new Button(() => Show(Screen.Mail));
            banner.AddToClassList("hb");

            var kicker = new Label(state.Hiring.OpenCount > 1
                ? $"ARRANGING  ({state.Hiring.OpenCount})"
                : "ARRANGING");

            kicker.AddToClassList("hb__kicker");
            banner.Add(kicker);

            var name = new Label(soonest.Candidate.Name);
            name.AddToClassList("hb__name");
            banner.Add(name);

            var track = new VisualElement();
            track.AddToClassList("hb__track");

            var fill = new VisualElement();
            fill.AddToClassList("hb__fill");
            fill.style.width = Length.Percent((float)(soonest.Progress * 100.0));
            track.Add(fill);

            banner.Add(track);

            var left = soonest.DaysLeft;
            var days = new Label(left <= 0
                ? "answering today"
                : left == 1 ? "1 day until they answer" : $"{left} days until they answer");

            days.AddToClassList("hb__days");
            banner.Add(days);

            approachBanner = banner;
            shellRoot.Add(banner);
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
            panel.Add(row);

            // **The two stat cards used to sit on the same line as the dial**, which gave five
            // things one row and left every one of them too narrow to read. They are the money and
            // the audience; they deserve the width of the panel rather than a quarter of it. The
            // reserved-capacity control moves up into the space they left, next to the dial, which
            // is also where it belongs: it is the lever the dial is measuring.
            var below = new VisualElement();
            below.AddToClassList("service__below");
            below.Add(BuildRightNowCard());
            below.Add(BuildUserCharts());

            panel.Add(below);
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
            page.Add(BuildRunningPanel());

            // **Out of the flow, pinned to the bottom right.** Booking used to be a panel stacked
            // under the channels, which meant picking a channel scrolled the thing you pick it
            // into off the screen. It is a control surface, not a section, so it behaves like one:
            // it stays where it is while the page moves behind it.
            page.Add(BuildBookingPanel());

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
            panel.AddToClassList("mkbook");

            var heading = new Label("START A NEW CAMPAIGN");
            heading.AddToClassList("mkbook__heading");
            panel.Add(heading);

            var audiences = new VisualElement();
            audiences.AddToClassList("mkbook__chips");

            foreach (var audience in AudienceCatalog.All)
            {
                var segment = audience.Segment;
                var chip = new Button(() => { pickedAudience = segment; Show(Screen.Marketing); })
                { text = audience.DisplayName.ToUpperInvariant() };

                chip.AddToClassList("chip");
                chip.AddToClassList("mkchip");
                chip.EnableInClassList("chip--on", pickedAudience == segment);
                audiences.Add(chip);
            }

            panel.Add(audiences);

            var terms = new VisualElement();
            terms.AddToClassList("mkbook__chips");

            foreach (var months in MarketingCatalog.TermsInMonths)
            {
                var term = months;
                var label = months <= 0 ? "OPEN ENDED" : $"{months} MONTH" + (months > 1 ? "S" : string.Empty);

                var chip = new Button(() => { pickedTerm = term; Show(Screen.Marketing); })
                { text = label };

                chip.AddToClassList("chip");
                chip.AddToClassList("mkchip");
                chip.EnableInClassList("chip--on", pickedTerm == term);
                terms.Add(chip);
            }

            panel.Add(terms);

            var draft = new MarketingCampaign(pickedChannels, pickedAudience, pickedTerm,
                simulation.State.Date);

            var daily = draft.DailyCostUsd;
            var total = draft.IsOpenEnded ? 0L : daily * draft.DaysBooked;

            if (pickedChannels.Count == 0)
            {
                var pick = new Label("Pick at least one channel above.");
                pick.AddToClassList("mkbook__pick");
                panel.Add(pick);
            }
            else
            {
                // Two blocks, because the player is answering two questions: what am I buying, and
                // what does it cost. The old single sentence made them read a paragraph to find a
                // number they were going to compare against another number.
                panel.Add(BookRow("AUDIENCE",
                    AudienceCatalog.Get(pickedAudience).DisplayName));

                panel.Add(BookRow("CHANNELS", string.Join(" + ", pickedChannels
                    .Select(channel => MarketingCatalog.Get(channel).DisplayName))));

                panel.Add(BookRow("RUNS FOR", draft.IsOpenEnded
                    ? "until you stop it"
                    : $"{draft.DaysBooked} days"));

                var split = new VisualElement();
                split.AddToClassList("mkbook__split");
                panel.Add(split);

                panel.Add(BookRow("PER DAY", UiFormat.Money(daily), true));

                panel.Add(BookRow("TOTAL", draft.IsOpenEnded
                    ? "open ended"
                    : UiFormat.Money(total), true));

                if (draft.IsOpenEnded)
                {
                    var why = new Label(
                        $"An open contract costs {MarketingCatalog.OpenEndedSurcharge:P0} of the "
                        + "committed rate. Nobody sells one at the price of a booked one.");

                    why.AddToClassList("mkbook__why");
                    panel.Add(why);
                }
            }

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

            book.AddToClassList("mkbook__go");
            book.SetEnabled(pickedChannels.Count > 0);
            panel.Add(book);

            return panel;
        }

        /// <summary>One caption and one reading, on a line. The whole booker is made of these.</summary>
        private static VisualElement BookRow(string caption, string value, bool loud = false)
        {
            var row = new VisualElement();
            row.AddToClassList("mkrow");

            var label = new Label(caption);
            label.AddToClassList("mkrow__caption");
            row.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("mkrow__value");
            reading.EnableInClassList("mkrow__value--loud", loud);
            row.Add(reading);

            return row;
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

                var words = new VisualElement();
                words.AddToClassList("run-row__words");

                var what = new Label(string.Join(" + ", names));
                what.AddToClassList("run-row__what");
                words.Add(what);

                var who = new Label($"to {AudienceCatalog.Get(campaign.Target).DisplayName}");
                who.AddToClassList("run-row__who");
                words.Add(who);

                row.Add(words);

                // The term as a bar rather than a sentence: a campaign three days from ending and
                // one three months from it read identically as text.
                if (!campaign.IsOpenEnded)
                {
                    var track = new VisualElement();
                    track.AddToClassList("run-row__track");

                    var fill = new VisualElement();
                    fill.AddToClassList("run-row__fill");

                    var run = Math.Max(1, campaign.DaysBooked);
                    var gone = Math.Clamp(1.0 - campaign.DaysLeft(state.Date) / (double)run, 0.0, 1.0);
                    fill.style.width = Length.Percent((float)(gone * 100.0));

                    track.Add(fill);
                    row.Add(track);
                }

                var left = new Label(campaign.IsOpenEnded
                    ? "OPEN ENDED"
                    : $"{campaign.DaysLeft(state.Date)} DAYS LEFT");

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

                stop.AddToClassList("run-row__stop");
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

            // The two panels at the top of FLEET are the ones a player reads while deciding what to
            // spend, and they were the smallest things on the screen. Forty percent taller.
            rental.AddToClassList("fleet-panel");
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
            ladder.AddToClassList("fleet-panel");
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

        /// <summary>
        /// The regulator's open file, across the top of whatever the player is looking at.
        ///
        /// **Not a screen and not a mail item, because neither of those is happening to you.** A
        /// penalty used to arrive the same tick the incident did: one moment the company was fine,
        /// the next there was a nine figure demand in the inbox. The outcome is decided either way;
        /// this is the five days of not knowing, and it is the only thing in the game that
        /// interrupts every screen at once.
        ///
        /// Rebuilt only when the day count changes. It carries a CSS animation and rebuilding it
        /// every frame would restart the flash sixty times a second, which is a solid colour.
        /// </summary>
        private void RefreshRegulatoryBanner()
        {
            var action = state.PendingAction;

            if (action == null)
            {
                regulatoryBanner?.RemoveFromHierarchy();
                regulatoryBanner = null;
                regulatoryDay = -1;
                return;
            }

            if (regulatoryBanner != null && regulatoryDay == action.DaysElapsed)
            {
                return;
            }

            regulatoryDay = action.DaysElapsed;
            regulatoryBanner?.RemoveFromHierarchy();

            regulatoryBanner = new VisualElement();
            regulatoryBanner.AddToClassList("regulatory");
            regulatoryBanner.pickingMode = PickingMode.Ignore;

            var headline = new Label("REGULATORY ACTION");
            headline.AddToClassList("regulatory__headline");
            regulatoryBanner.Add(headline);

            var what = new Label(action.Subtitle);
            what.AddToClassList("regulatory__what");
            regulatoryBanner.Add(what);

            // The caption sits on the bar rather than above it, because they are one object: the
            // sentence is what the bar is measuring.
            var status = new Label($"Inspection and clarification underway...   {action.DaysLeft} "
                + (action.DaysLeft == 1 ? "day left" : "days left"));

            status.AddToClassList("regulatory__status");
            regulatoryBanner.Add(status);

            var track = new VisualElement();
            track.AddToClassList("regulatory__track");

            var fill = new VisualElement();
            fill.AddToClassList("regulatory__fill");
            fill.style.width = Length.Percent((float)(action.Progress * 100.0));
            track.Add(fill);

            regulatoryBanner.Add(track);
            shellRoot.Add(regulatoryBanner);
        }

        /// <summary>
        /// A node in flight, in its own strip under the model banner.
        ///
        /// **Blue, and beside the product rather than instead of it.** The model banner swaps itself
        /// for whatever work is running, so starting a four month research programme while a model
        /// was on sale hid the product completely: no users, no mood, no way through to the
        /// management desk until the node finished. Two different things were sharing one corner.
        ///
        /// Rebuilt on the day, like the regulatory banner and for the same reason.
        /// </summary>
        private void RefreshResearchBanner()
        {
            var project = state.ActiveResearch;
            var showing = project != null && current == Screen.Site;

            if (!showing)
            {
                researchBanner?.RemoveFromHierarchy();
                researchBanner = null;
                researchBannerDay = -1;
                return;
            }

            if (researchBanner != null && researchBannerDay == project.DaysCompleted)
            {
                return;
            }

            researchBannerDay = project.DaysCompleted;
            researchBanner?.RemoveFromHierarchy();

            var node = ResearchTree.Get(project.Node);

            researchBanner = new Button(() => Show(Screen.Research));
            researchBanner.AddToClassList("rb");

            var kicker = new Label(project.IsWaitingForCompute ? "RESEARCH WAITING" : "RESEARCHING");
            kicker.AddToClassList("rb__kicker");
            researchBanner.Add(kicker);

            var name = new Label(node.DisplayName);
            name.AddToClassList("rb__name");
            researchBanner.Add(name);

            var track = new VisualElement();
            track.AddToClassList("rb__track");

            var fill = new VisualElement();
            fill.AddToClassList("rb__fill");
            fill.style.width = Length.Percent((float)(project.Progress * 100.0));
            track.Add(fill);

            researchBanner.Add(track);

            var left = Math.Max(0, project.DurationDays - project.DaysCompleted);
            var days = new Label(project.IsWaitingForCompute
                ? $"{UiFormat.Number(project.PetaflopDaysRemaining, 0)} PF-days owed"
                : (left == 1 ? "1 day left" : $"{left:N0} days left")
                  + $"   ({project.Progress:P0})");

            days.AddToClassList("rb__days");
            researchBanner.Add(days);

            shellRoot.Add(researchBanner);
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
            // Zero while paused. The points figure is yesterday's and does not change, so the
            // bubbles went on rising out of a desk nobody was sitting at, which reads as the game
            // still running.
            bubbles = new ResearchBubbles(bubbleHost,
                () => clock.Speed == SimSpeed.Paused ? 0.0 : simulation.State.ResearchPointsToday);

            // Cheap when nothing changed, and the only place that has to notice a move: the office
            // is not on screen anywhere else, so re-dressing it on every tab change would be work
            // done for a camera nobody is looking through.
            officeStage?.Show(state.Staff.Office, state.Decor);

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

            // Clicking the map sends the founder out through the garage to the car, and the screen
            // follows once they are in it. Cutting straight to the map is a scene change; walking
            // out of the room is somebody leaving.
            //
            // It falls straight through when there is nobody to walk, because a player whose office
            // scene has not loaded must not be stranded on a journey that will never finish.
            var map = new Button(() =>
            {
                if (founder == null || !founder.BeginLeaving())
                {
                    Show(Screen.Ranking);
                }
            });

            map.AddToClassList("site-icon");
            SetIcon(map, "Ui/map", "MAP");

            InsightTip.Attach(map, "THE WORLD MAP",
                "Travel. Who is building what, and where. Today it opens the board; the drive out to "
                + "it is being built.",
                InsightTip.Placement.LeftOf);

            rail.Add(map);

            // The furniture shop. Third in the rail rather than a tab of its own, because it is a
            // thing you do *to the room you are looking at*, and walking away from the room to
            // furnish it is the wrong way round.
            if (FurnishingShopIsOpen)
            {
                var decorate = new Button(() =>
                {
                    decorOpen = !decorOpen;
                    decorProblem = string.Empty;
                    Show(Screen.Site);
                });

                decorate.AddToClassList("site-icon");
                decorate.EnableInClassList("site-icon--on", decorOpen);
                SetIcon(decorate, "Ui/office_decorate", "DECOR");

                InsightTip.Attach(decorate, "FURNISH THE OFFICE",
                    "Buy desks, sofas and everything else. Desks raise the hiring cap; the rest makes "
                    + "the floor a better place to work. Anything can be sold back at "
                    + $"{FurnitureCatalog.ResaleFraction:P0} of what it cost.",
                    InsightTip.Placement.LeftOf);

                rail.Add(decorate);
            }

            stage.Add(rail);

            if (FurnishingShopIsOpen && decorOpen)
            {
                page.Add(BuildDecorator());
            }

            page.Add(stage);
            return page;
        }

        /// <summary>
        /// Whether the player can place furniture piece by piece.
        ///
        /// **Suspended on 2026-08-22 at the author's call**, in favour of the WITH FURNISHINGS tick
        /// on the office chooser: a standard pack that arrives on the day of the move, cheaper than
        /// the same pieces bought one at a time.
        ///
        /// Nothing under it is deleted. `DecorPlan`, `FurnitureCatalog` and `BuildDecorator` are
        /// intact and still tested, the furnished move buys through the same `DecorPlan.Buy`, and
        /// saves keep carrying whatever is on the floor. Turning this back on is one word, which is
        /// the only reason it is a constant rather than a commit that tore the shop out.
        /// </summary>
        private const bool FurnishingShopIsOpen = false;

        /// <summary>
        /// The furniture shop, laid over the room it changes.
        ///
        /// Two columns: what can be bought on the left, what is already owned on the right. The
        /// room stays visible behind it on purpose. The player is deciding what the office should
        /// look like, and hiding the office to do that would be perverse.
        /// </summary>
        private VisualElement BuildDecorator()
        {
            var panel = new VisualElement();
            panel.AddToClassList("decor");

            var title = new Label("FURNISH THE OFFICE");
            title.AddToClassList("page-title");
            panel.Add(title);

            var decor = state.Decor ?? new DecorPlan();
            var room = RoomCatalog.For(state.Staff.Office);

            var subtitle = new Label(
                $"{state.Staff.Headcount} of {state.Staff.Desks} desks taken. "
                + $"{decor.Placed.Count()} pieces on the floor, "
                + $"{UiFormat.Money((long)decor.InvestedUsd)} spent on them. "
                + "Only what is standing up counts.");

            subtitle.AddToClassList("page-subtitle");
            panel.Add(subtitle);

            if (!string.IsNullOrEmpty(decorProblem))
            {
                var problem = new Label(decorProblem);
                problem.AddToClassList("decor__problem");
                panel.Add(problem);
            }

            if (!room.AllowsFurniture)
            {
                var closed = new Label(
                    "There is no floor to spare here. The sofa, the bench, the rack and the stairs "
                    + "are already touching, and anything else would be standing in the middle of "
                    + "them. Rent a floor and the shop opens.");

                closed.AddToClassList("decor__empty");
                panel.Add(closed);
            }
            else
            {
                var columns = new VisualElement();
                columns.AddToClassList("decor__columns");

                columns.Add(BuildShop(room));
                columns.Add(BuildOwned(decor, room));

                panel.Add(columns);
            }

            var close = new Button(() =>
            {
                decorOpen = false;
                Show(Screen.Site);
            })
            { text = "DONE" };

            close.AddToClassList("decor__close");
            panel.Add(close);

            return panel;
        }

        private VisualElement BuildShop(RoomView room)
        {
            var column = new VisualElement();
            column.AddToClassList("decor__column");

            var heading = new Label("THE SHOP");
            heading.AddToClassList("decor__heading");
            column.Add(heading);

            var list = new ScrollView();
            list.AddToClassList("decor__list");

            foreach (var piece in FurnitureCatalog.All)
            {
                list.Add(BuildShopRow(piece, room));
            }

            column.Add(list);
            return column;
        }

        private VisualElement BuildShopRow(FurniturePiece piece, RoomView room)
        {
            var row = new VisualElement();
            row.AddToClassList("decor-row");

            // The swatch is the only thing tying this list to the boxes in the room. Without it a
            // player cannot tell which of five brown rectangles is the shelf they just bought.
            var swatch = new VisualElement();
            swatch.AddToClassList("decor-row__swatch");

            if (ColorUtility.TryParseHtmlString(piece.Tint, out var tint))
            {
                swatch.style.backgroundColor = tint;
            }

            row.Add(swatch);

            var text = new VisualElement();
            text.AddToClassList("decor-row__text");

            var name = new Label(piece.DisplayName);
            name.AddToClassList("decor-row__name");
            text.Add(name);

            var blurb = new Label(piece.Blurb);
            blurb.AddToClassList("decor-row__blurb");
            text.Add(blurb);

            var effect = new Label(EffectLine(piece));
            effect.AddToClassList("decor-row__effect");
            text.Add(effect);

            row.Add(text);

            var owned = (state.Decor ?? new DecorPlan()).CountOf(piece.Kind);
            var affordable = state.CashUsd >= piece.PriceUsd;

            var buy = new Button(() =>
            {
                decorProblem = simulation.TryBuyFurniture(piece.Kind, ZoneOf(room));

                Show(Screen.Site);
            })
            {
                text = owned > 0
                    ? $"BUY   {UiFormat.Money((long)piece.PriceUsd)}   ({owned} owned)"
                    : $"BUY   {UiFormat.Money((long)piece.PriceUsd)}"
            };

            buy.AddToClassList("decor-row__buy");
            buy.SetEnabled(affordable);

            if (!affordable)
            {
                buy.text = $"NEEDS {UiFormat.Money((long)piece.PriceUsd)}";
            }

            row.Add(buy);
            return row;
        }

        /// <summary>What a piece does, in one line, or that it is only there to look at.</summary>
        private static string EffectLine(FurniturePiece piece)
        {
            var parts = new List<string>();

            if (piece.DeskSeats > 0)
            {
                parts.Add(piece.DeskSeats == 1 ? "+1 desk" : $"+{piece.DeskSeats} desks");
            }

            if (piece.MoraleBonus > 0.0)
            {
                parts.Add($"+{piece.MoraleBonus:P1} how well people work");
            }

            if (piece.ResearchBonus > 0.0)
            {
                parts.Add($"+{piece.ResearchBonus:P1} research");
            }

            parts.Add($"sells back for {UiFormat.Money((long)piece.ResaleValueUsd)}");

            return string.Join("  -  ", parts);
        }

        private VisualElement BuildOwned(DecorPlan decor, RoomView room)
        {
            var column = new VisualElement();
            column.AddToClassList("decor__column");

            var heading = new Label("WHAT THE COMPANY OWNS");
            heading.AddToClassList("decor__heading");
            column.Add(heading);

            var list = new ScrollView();
            list.AddToClassList("decor__list");

            if (decor.Items.Count == 0)
            {
                var empty = new Label("Nothing yet. The floor is as the lease left it.");
                empty.AddToClassList("decor__empty");
                list.Add(empty);
            }

            // Placed first, because those are the ones the player can see in the room behind this
            // panel and the ones they are most likely to want to move or sell.
            foreach (var item in decor.Items
                .OrderByDescending(entry => entry.IsPlaced)
                .ThenBy(entry => entry.Definition.DisplayName))
            {
                list.Add(BuildOwnedRow(item, room));
            }

            column.Add(list);
            return column;
        }

        private VisualElement BuildOwnedRow(DecorItem item, RoomView room)
        {
            var piece = item.Definition;

            var row = new VisualElement();
            row.AddToClassList("decor-row");
            row.EnableInClassList("decor-row--stored", !item.IsPlaced);

            var swatch = new VisualElement();
            swatch.AddToClassList("decor-row__swatch");

            if (ColorUtility.TryParseHtmlString(piece.Tint, out var tint))
            {
                swatch.style.backgroundColor = tint;
            }

            row.Add(swatch);

            var text = new VisualElement();
            text.AddToClassList("decor-row__text");

            var name = new Label(piece.DisplayName);
            name.AddToClassList("decor-row__name");
            text.Add(name);

            var where = new Label(item.IsPlaced
                ? $"On the floor at {item.X:0.#} by {item.Z:0.#}."
                : "In storage. It does nothing until it is standing up.");

            where.AddToClassList("decor-row__blurb");
            text.Add(where);

            row.Add(text);

            var buttons = new VisualElement();
            buttons.AddToClassList("decor-row__buttons");

            var move = new Button(() =>
            {
                decorProblem = item.IsPlaced
                    ? simulation.TryStoreFurniture(item)
                    : simulation.TryPlaceFurniture(item, ZoneOf(room));

                Show(Screen.Site);
            })
            { text = item.IsPlaced ? "STORE" : "PLACE" };

            move.AddToClassList("decor-row__move");
            buttons.Add(move);

            // One click. The refund is small enough that an accidental sale is a real loss but not
            // a campaign-ending one, and a second click on every row would make clearing a floor a
            // chore.
            var sell = new Button(() =>
            {
                var got = simulation.SellFurniture(item);
                decorProblem = got > 0.0
                    ? $"Sold the {piece.DisplayName.ToLowerInvariant()} for {UiFormat.Money((long)got)}."
                    : string.Empty;

                Show(Screen.Site);
            })
            { text = $"SELL   {UiFormat.Money((long)piece.ResaleValueUsd)}" };

            sell.AddToClassList("decor-row__sell");
            buttons.Add(sell);

            row.Add(buttons);
            return row;
        }

        /// <summary>The patch of floor this room leaves clear for furniture.</summary>
        private static DecorZone ZoneOf(RoomView room) =>
            new(room.DecorX, room.DecorZ, room.DecorWidth, room.DecorDepth);

        /// <summary>
        /// Where a furnished move would stand its pack, or nothing at all.
        ///
        /// Null for a place with no open floor, so the garage cannot be charged for six pieces it
        /// has nowhere to put. The chooser already hides the cost in that case; this is the guard
        /// that makes it true rather than merely displayed.
        /// </summary>
        private static DecorZone? FurnishZone(OfficeTier tier, bool furnished)
        {
            if (!furnished)
            {
                return null;
            }

            var room = RoomCatalog.For(tier);
            return room.AllowsFurniture ? ZoneOf(room) : null;
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
