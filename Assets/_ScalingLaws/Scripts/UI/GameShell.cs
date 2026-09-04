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
    public sealed partial class GameShell : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;
        [SerializeField] private string companyName = "Prometheus AI";

        private CompanyState state;
        private CompanySimulation simulation;
        private SimClock clock;

        /// <summary>Days between automatic saves. Roughly a season of game time.</summary>
        public const int AutoSaveIntervalDays = 90;

        // ---- the screen proof ---------------------------------------------------------------------
        //
        // Three members that exist for tooling rather than for the game, and they are worth the
        // space. Every layout fault in this project has been found by looking, and until these
        // existed there was no way to look at a tab without opening the editor and clicking to it.
        // The proof walks all of them in one pass, so a stylesheet change can be checked against
        // nineteen screens instead of against the one that prompted it.

        /// <summary>Every screen this shell can open, by name.</summary>
        public static IReadOnlyList<string> ScreenNames =>
            Array.AsReadOnly(Enum.GetNames(typeof(Screen)));

        /// <summary>The campaign behind the interface, so a proof can build a state worth photographing.</summary>
        public CompanySimulation Simulation => simulation;

        /// <summary>
        /// Opens a screen by name. False when there is no such screen, rather than an exception:
        /// the caller is usually iterating <see cref="ScreenNames"/> and a typo should read as a
        /// missing picture, not as a crashed run.
        /// </summary>
        public bool OpenScreenByName(string name)
        {
            if (!Enum.TryParse<Screen>(name, out var screen))
            {
                return false;
            }

            Show(screen);

            // **The chrome too, and this is not decoration.** A proof fixture builds a campaign
            // straight onto the simulation without ticking a day, and only the day rollover
            // refreshes the top bar. Without this the frame shows a 2024 page under a day-one
            // money figure, which reads exactly like a bug in the top bar and is not one. It cost
            // a full investigation once.
            RefreshChrome();
            return true;
        }

        private ModelCreatorPanel creator;
        private UpgradeGridPanel upgrades;

        /// <summary>What the company offers its people. Lives on the business page.</summary>
        private BenefitsPanel benefits;

        /// <summary>Relations, their staff and the offers. Opens inside the lab card.</summary>
        private RivalPanel rivals;

        /// <summary>Which lab's card is open, so an offer can redraw the one it happened on.</summary>
        private CompetitorId openLab;

        /// <summary>Paying for a story, and taking them to court. Same card, below the roster.</summary>
        private RivalActionsPanel rivalActs;

        /// <summary>
        /// The stock screen.
        ///
        /// Held rather than rebuilt so which company the player was looking at survives a day
        /// rolling over, which happens every second and a half at normal speed.
        /// </summary>
        private InvestingScreen investing;

        /// <summary>What Escape opens: settings, statistics, four slots and the report button.</summary>
        private PauseMenu pause;

        /// <summary>The card that asks for a name before opening the form.</summary>
        private FeedbackDialog report;

        /// <summary>The two sheets, held so they can be taken down without a full rebuild.</summary>
        private VisualElement pauseSheet;

        private VisualElement reportSheet;

        /// <summary>Real seconds since the last automatic save. Not simulated time.</summary>
        private float sinceAutosave;

        /// <summary>The open offer to buy the company, or null.</summary>
        private VisualElement buyoutBanner;

        /// <summary>The day the offer banner was last built on, so it is not remade per frame.</summary>
        private int buyoutDay = -1;

        /// <summary>The handset resting under the bottom bar once the tour is over.</summary>
        private PhoneDock phoneDock;

        /// <summary>Where a version gets its name and its price. Opened from UPGRADE.</summary>
        private ReleasePlanPanel releasePlan;
        private ArchitectureCreatorPanel families;
        private ManagementScreen management;
        private NewsScreen news;
        private NewsBanner newsBanner;
        private GrantBanner grantBanner;
        private StartedNotice startedNotice;
        private MailScreen mail;
        private OfficeChooser offices;
        private VisualElement bannerStack;
        private UpgradeStrip upgradeStrip;

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
        private ServerRoomScreen serverRoom;

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

        /// <summary>How long he leaves it before ringing back after a "come back later".</summary>
        private const int DaysBeforeHeRingsBack = 3;

        /// <summary>The day the player stepped out, so he does not ring the same afternoon.</summary>
        private int pausedOn;

        /// <summary>The quiet strip under the corner banners with the next task on it.</summary>
        private TaskBanner tasks;

        /// <summary>The corner where walkthroughs wait to be taken. See <see cref="PromptChips"/>.</summary>
        private PromptChips prompts;

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
        private EffectBadges effectBadges;

        /// <summary>
        /// One person, opened.
        ///
        /// Held by the shell rather than by the team screen, because the same card has to open
        /// from a row on that page and from clicking somebody in the office. Two panels would be
        /// two places to fix the next thing that is wrong with it.
        /// </summary>
        private PersonPanel personPanel;

        /// <summary>The hired people, standing in the office. See <see cref="StaffPresence"/>.</summary>
        private StaffPresence staff;
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

                // **Both clocks, and the rate the run actually gets.** This divided the remaining
                // petaflop-days by the size of the whole fleet and ignored the safety stage
                // entirely, so a playtest was quoted twenty-one days, told four, and then watched
                // "0 days" sit on screen while the calendar kept turning.
                return new WorkInFlight("TRAINING MODEL", run.Blueprint.Name, progress,
                    run.DaysRemaining(simulation.RunPetaflopDaysPerDay()));
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

            /// <summary>
            /// The stock screen. Appended rather than inserted: `Screen` is compared by value in
            /// the tour's target table and by the bottom bar's slot userData.
            /// </summary>
            Investing,
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

            /// <summary>The basement, and the cabinets in it. Reached from the office rail.</summary>
            Room,

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
            upgrades.goToCreator = () => Show(Screen.Create);
                    Show(Screen.ReleasePlan);
                },
                () => Show(Screen.Site));

            benefits = new BenefitsPanel(() => simulation, () => Show(Screen.Business));

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
            RefreshBuyoutBanner();
            RefreshResearchBanner();
            RefreshApproachBanner();
            RefreshUpgradeBanner();
            tasks.Refresh();
            prompts?.Refresh();
            RingTheCousinIfThisIsDayOne();

            // They reached the car. This is where the loading screen and the world map go once the
            // map itself exists; until then it opens the board, which is what the icon did before.
            if (founder != null && founder.HasReachedTheCar && current == Screen.Site)
            {
                founder.ComeBack();
                Show(Screen.Ranking);
            }

            PollPauseKey();
            PollAutosave();

            // The clock does not advance while the pause menu is up. Pausing has to actually pause,
            // or a player who opened the menu to save comes back to a company three days older.
            if (pause is { IsOpen: true })
            {
                return;
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
                // The reading position is kept by `Show` itself now, for every rebuild rather than
                // only for this one. A day rolls over every second and a half at normal speed and a
                // player half way down the research tree could not finish a sentence.
                Show(current);
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

            phone = new PhonePanel(root, AnswerTheCousin, () => simulation)
            {
                progressForMenu = () => state.Guide
            };

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

            guide = new GuideOverlay(root, () => state.Guide, GoToGuideTarget, RefreshChrome,
                target => ScreenForGuideTarget(target) is { } screen ? hud?.SlotFor(screen) : null,
                PutCreatorOnStage,
                target => hud?.LockToSlot(
                    target.HasValue && ScreenForGuideTarget(target.Value) is { } shown
                        ? shown
                        : null));

            guide.handOverBasement = () =>
            {
                if (simulation.TryOpenServerRoom(true, out _))
                {
                    RefreshChrome();
                }
            };

            // **The offer, which was declared and never assigned.** `GuideOverlay.offerFor` and
            // `ArchitectureCreatorPanel.TakeTheAdvice` were both written and nothing joined them, so
            // the one step in the tour that hands the player something to press drew no button at
            // all. Same failure class as the server hall: complete on both sides, no wire.
            guide.offerFor = step => step.Id == GuideScript.ArchitectureOfferStepId
                ? new GuideOverlay.GuideOffer(Loc.T("guide.offer.arch"), () =>
                {
                    families?.TakeTheAdvice();
                    RefreshChrome();
                })
                : null;

            guide.leftForNow = () =>
            {
                pausedOn = state.Date.Day;
            };

            // **He does not leave when the tour ends.** Pressing "I'll take it from here" in the
            // first minute used to skip the tutorial permanently with no way back to it, which is a
            // dead end reached by one click. The handset stays docked under the bar and rings him.
            //
            // **The handset opens the phone rather than replaying the call.** It used to ring him
            // straight back, which meant the only thing a player could do with a phone was be
            // talked at. The tour's next step is now one item on a home screen, beside his number.
            phoneDock = new PhoneDock(root, () => state.Guide, () =>
            {
                phoneDock.Hide();
                phone.OpenMenu();
            });

            // **The phone can start a walkthrough too**, which is where a player goes looking for
            // one they waved away in the corner. Same door as the chip: the overlay owns the strip.
            phone.startWalkthrough = walkthrough => guide?.StartWalkthrough(walkthrough);

            tasks = new TaskBanner(root, () => state, () => state.Guide, RefreshChrome);

            // **Above the task strip and separate from it.** The chip that offers a walkthrough is
            // not one of the five opening tasks, and hanging it off that list would have made its
            // counter read 4/5 over something that is not one of the five.
            prompts = new PromptChips(root, () => state, () => state.Guide,
                // **Through the phone, not straight to the strip.** The author asked for the card
                // to pull the handset out, and it is the right call: a walkthrough that begins
                // because Emil rang is a favour, and one that begins because a panel appeared is a
                // feature. Same door either way, so the two cannot drift.
                walkthrough => phone.OpenGuide(walkthrough), RefreshChrome,
                () => guide is { IsWalking: true });

            modelHub = new ModelDashboard(() => simulation, () => Show(Screen.Create),
                () => Show(Screen.Upgrade), () => Show(Screen.Release));

            // The basement. Built here rather than lazily, because the corner banner in it reads the
            // live simulation and a screen constructed on first open would miss the day it opened.
            serverRoom = new ServerRoomScreen(() => simulation, () => Show(Screen.Room));

            officeStage = new OfficeStage(GameObject.Find(OfficeStageRoot));
            officeStage.Show(state.Staff.Office, state.Decor);

            founder = new FounderPresence(() => state);

            // **The room has had a Staff group since it was generated and only ever held the
            // founder.** A company could hire twelve people and the office they were in was empty.
            staff = new StaffPresence(() => state);
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
            // **One column, so the two cannot overlap.** Both used to be positioned against the
            // window and staying clear of each other meant guessing the taller one's height, which
            // was wrong the moment a headline wrapped.
            var leftStack = new VisualElement();
            leftStack.AddToClassList("leftstack");
            leftStack.pickingMode = PickingMode.Ignore;

            newsBanner = new NewsBanner(() => simulation.State.News, () => Show(Screen.News));
            leftStack.Add(newsBanner.Root);

            grantBanner = new GrantBanner(() => simulation, () => Show(Screen.Funding));
            leftStack.Add(grantBanner.Root);

            root.Add(leftStack);

            startedNotice = new StartedNotice(root);

            // The corner is a stack now. One product on sale is one banner, because a company
            // running three lines is running three products and a single panel describing only the
            // strongest was hiding two of them.
            bannerStack = new VisualElement();
            bannerStack.AddToClassList("mb-stack");
            bannerStack.Add(modelBanner.Root);

            // Under the product, because an upgrade is work happening to the thing above it.
            upgradeStrip = new UpgradeStrip(() => simulation.State);
            bannerStack.Add(upgradeStrip.Root);

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
                ? Loc.T("shell.month_up", UiFormat.Money(flow))
                : Loc.T("shell.month_down", UiFormat.Money(Math.Abs(flow)));
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
                ? Loc.T("shell.rep_rising", change.Headline)
                : Loc.T("shell.rep_falling", change.Headline);

            fansLabel.text = UiFormat.Count(state.Fans) + " " + Loc.T("shell.fans");
            fansLabel.tooltip = Loc.T("shell.fans_note");
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

            // **What is temporarily true about the company, which nothing has ever shown.**
            //
            // `EffectBook` multiplies demand between 0.15x and 4.0x every day and the player was
            // never told any of it existed. Beside the standing because that is what these move:
            // a viral window and a backlash are both statements about how many people are looking.
            effectBadges = new EffectBadges();
            left.Add(effectBadges.Root);

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
            { text = Loc.T("common.save") };
            save.AddToClassList("topbar__action");
            right.Add(save);

            var menu = new Button(SceneFlow.LoadMainMenu) { text = Loc.T("common.menu") };
            menu.AddToClassList("topbar__action");
            right.Add(menu);

            bar.Add(right);

            return bar;
        }

        private void AddHudSlots()
        {
            // Every argument here is a phrase-book key. The card over the bar answers
            // "why would I go there", never repeating the word printed on the slot itself.

            hud.AddSlot("hud.site", Screen.Site, () => Show(Screen.Site), "hud_site",
                "insight.site");

            hud.AddSlot("hud.model", Screen.Model, () => Show(Screen.Model), "hud_model",
                "insight.model");

            hud.AddSlot("hud.research", Screen.Research, () => Show(Screen.Research), "hud_research",
                "insight.research");

            hud.AddSlot("hud.architecture", Screen.Family, () => Show(Screen.Family), "hud_architecture",
                "insight.architecture");

            hud.AddSlot("hud.upgrade", Screen.Upgrade, () => Show(Screen.Upgrade), "hud_upgrade",
                "insight.upgrade");

            hud.AddSlot("hud.team", Screen.Team, () => Show(Screen.Team), "hud_team",
                "insight.team");

            hud.AddSlot("hud.compute", Screen.Fleet, () => Show(Screen.Fleet), "hud_fleet",
                "insight.compute");

            hud.AddSlot("hud.business", Screen.Business, () => Show(Screen.Business), "hud_business",
                "insight.business");

            hud.AddSlot("hud.release", Screen.Release, () => Show(Screen.Release), "hud_release",
                "insight.release");

            hud.AddSlot("hud.capital", Screen.Funding, () => Show(Screen.Funding), "hud_funding",
                "insight.capital");

            hud.AddSlot("hud.ranking", Screen.Ranking, () => Show(Screen.Ranking), "hud_ranking",
                "insight.ranking");

            hud.AddSlot("hud.intel", Screen.Feed, () => Show(Screen.Feed), "hud_intelligence",
                "insight.intel");

            hud.AddSlot("hud.marketing", Screen.Marketing, () => Show(Screen.Marketing), "hud_marketing",
                "insight.marketing");

            hud.AddSlot("hud.news", Screen.News, () => Show(Screen.News), "hud_news",
                "insight.news");

            hud.AddSlot("hud.mail", Screen.Mail, () => Show(Screen.Mail), "hud_mail",
                "insight.mail");
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

        /// <summary>
        /// The end of a campaign, however it ended.
        ///
        /// **Two endings share this page and they are not the same event.** Running out of credit
        /// and selling the company both stop the campaign, and the figures underneath are worth
        /// reading either way, but printing INSOLVENT over a four billion dollar exit would be the
        /// game misreading its own best outcome.
        ///
        /// Both headlines are keyed. The old ones were English literals on a page a Polish player
        /// reaches at the end of every campaign they lose.
        /// </summary>
        private void ShowGameOver(string headline = null, string sentence = null)
        {
            contentHost.Clear();

            var page = NewPage(
                headline ?? Loc.T("ending.insolvent"),
                sentence ?? Loc.T("ending.insolvent.note", state.CompanyName, state.Date.ToString()));

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            page.Add(panel);

            panel.Add(Row(Loc.T("over.survived"), UiFormat.Days(state.Date.DayIndex)));
            panel.Add(Row(Loc.T("over.models_shipped"), state.ReleasedModelCount.ToString()));
            panel.Add(Row(Loc.T("over.best_capability"), UiFormat.Number(state.BestCapability)));
            panel.Add(Row(Loc.T("over.frontier_end"),
                UiFormat.Number(simulation.Market.FrontierCapability)));
            panel.Add(Row(Loc.T("over.lifetime_revenue"), UiFormat.Money(state.LifetimeRevenueUsd)));
            panel.Add(Row(Loc.T("over.lifetime_cost"),
                UiFormat.Money(state.LifetimeOperatingCostUsd)));
            panel.Add(Row(Loc.T("over.capital_spent"),
                UiFormat.Money(state.LifetimeCapitalSpentUsd)));
            panel.Add(Row(Loc.T("over.raised"), UiFormat.Money(state.CapTable.TotalRaisedUsd)));
            panel.Add(Row(Loc.T("over.founders_held"),
                UiFormat.Percent(state.CapTable.FounderEquity)));

            var back = new Button(SceneFlow.LoadMainMenu) { text = Loc.T("menu.back_to_menu") };
            back.AddToClassList("button");
            back.AddToClassList("button--primary");
            back.style.marginTop = 16;
            panel.Add(back);

            contentHost.Add(page);
        }

        private void Show(Screen screen)
        {
            var changed = current != screen;

            // **Keep the reading position whenever the same page is rebuilt.**
            //
            // This used to live only on the day-rollover path, so a day passing preserved the
            // scroll and a click did not. Every control on every screen answers by calling
            // `Show(current)`, which is a full rebuild, so ticking a staff benefit or nudging the
            // rent slider threw the player back to the top of the page. Four tabs were unplayable
            // for that reason and the cause was one missing line, not four bugs.
            //
            // Only when the screen has not changed: opening a different tab should start at its
            // top, and carrying an offset across would land the player half way down a page they
            // have never seen.
            var wasAt = changed ? Vector2.zero : OpenScrollOffset();

            // The basement runs a second camera into a render texture. Leaving it on behind every
            // other tab is a room nobody is looking at, drawn sixty times a second, for the whole
            // campaign.
            if (changed && current == Screen.Room)
            {
                serverRoom?.Hide();
            }

            current = screen;

            // **The right corner is not free on every screen.** The basement runs a build rail down
            // that edge, which is why the room's own corner banner already sits at 344 rather than
            // 22. The task strip and the prompt cards live on the panel root, outside any screen, so
            // nothing told them: all three drew on top of each other there. One class, set here
            // because this is where the shell already knows what is open.
            contentHost?.panel?.visualTree?.EnableInClassList("has-right-rail", screen == Screen.Room);

            // **The corner cards belong to the screens whose corner is actually free.** A page that
            // uses the full width has content there, and a render of the business page came back
            // with the guide card sitting on top of two figures. This is the rule the model banner
            // has followed since it was built, for the same reason and after the same discovery.
            contentHost?.panel?.visualTree?.EnableInClassList("corner-is-free",
                screen == Screen.Site || screen == Screen.Room);

            contentHost.Clear();

            // A floating card belongs to the screen that opened it. Leaving it up over a different
            // tab is the same fault the corner banners had.
            researchCard?.RemoveFromHierarchy();

            hud.SetActiveSlot(screen);

            personPanel ??= new PersonPanel(() => simulation, () => Show(current));

            // The bar re-reads its own words here, because `Show` already rebuilds the whole page
            // and the bar is the one piece of the interface that survives that. Twenty one
            // dictionary lookups against a full page rebuild is nothing, and it is what stops the
            // bottom of the screen being the only part of the game still in the old language.
            hud.Retext();

            // **Is this a room the player is standing in, or a page they are reading?**
            //
            // One fact with three consequences: whether the page goes in a scroller, and now which
            // clock the bar carries. Computed once, here, because a second reading of it is how the
            // room ends up scrolled and the disc ends up over a document at the same time.
            var fillsTheWindow = screen is Screen.Site or Screen.Room;

            // The disc hangs about 170px above the bar and no page reserves for it, so it was
            // covering the bottom-left corner of every document screen. A room has nothing there
            // to lose and keeps it; a document gets the rectangular 24-hour plate in the bar.
            hud.ShowDial(fillsTheWindow);

            // Hidden here rather than only in RefreshChrome, which runs when a day rolls over. While
            // the game is paused, which is most of the time a player spends reading a screen, no day
            // rolls over, so both corner banners stayed on top of whatever tab had just been opened.
            modelBanner?.SetHidden(screen != Screen.Site);
            newsBanner?.SetHidden(screen != Screen.Site);
            grantBanner?.SetHidden(screen != Screen.Site);

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
                AudioDirector.Play(UiSound.Tab);
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

            // The two rooms are the exception: they fill the window rather than being documents,
            // and putting one in a scroller gives it a scrollbar's worth of nothing to slide.
            //
            // **The basement joined this list because of a render.** A ScrollView's content
            // container sizes to its content, so `flex-grow` inside one does nothing: the room
            // sat at its minimum height with the floor cropped across the middle and a band of
            // empty page underneath. The build rail carries its own scroller, which is the part
            // of that screen that is a document.
            var host = fillsTheWindow ? contentHost : scroller;
            if (!fillsTheWindow)
            {
                contentHost.Add(scroller);
            }

            // **A screen that throws while it is being built leaves the player on nothing.**
            //
            // Half a page is worse than an error: the tab is lit, the bar still works, and the
            // content area is empty, so the only reading available is that the game has hung. This
            // catches, logs the whole exception so the console still carries the stack, and puts
            // the screen's name and the first line of the fault on the page. A player can then
            // report something actionable instead of "it went blank".
            //
            // It does not swallow anything. `Debug.LogException` keeps the full trace, and the
            // panel says plainly that this is a fault rather than a state.
            try
            {
                BuildScreenInto(host, screen);
            }
            catch (Exception error)
            {
                Debug.LogException(error);

                host.Clear();
                host.Add(BuildScreenFailure(screen, error));
            }

            // The tour is drawn last, over whatever the new screen turned out to be. Its highlight
            // is a query against the live tree, so it has to run after the page exists — refreshing
            // it before the rebuild rings elements that are about to be thrown away.
            // The person card goes on last, over whatever the page turned out to be, because an
            // absolutely positioned element in UI Toolkit still paints in document order. This
            // project learned that when the server room's corner banner came out under the floor.
            if (personPanel is { IsOpen: true })
            {
                contentHost.Add(personPanel.Build());
            }

            guide?.Refresh();

            // **Reserve the strip's height at the foot of the page rather than hiding the strip.**
            //
            // The tour bar floats over every screen at `bottom: 96px`, so the last band of a long
            // page sat underneath it and could not be read. Hiding it outside the office was the
            // other option and it is worse: half the steps say "open COMPUTE", and a tour whose
            // instructions vanish on the tab it just told you to open is no tour at all.
            scroller.EnableInClassList("page-scroll--guided", guide is { IsShowing: true });

            RestoreScrollOffset(wasAt);
        }

        /// <summary>
        /// What each screen actually builds.
        ///
        /// Split out of `Show` so the whole switch sits inside one guard rather than each case
        /// carrying its own, and so the guard's scope is obvious from the shape of the code.
        /// </summary>
        private void BuildScreenInto(VisualElement host, Screen screen)
        {
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
                case Screen.Room:
                    host.Add(serverRoom.Build());
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
                case Screen.Investing:
                    investing ??= new InvestingScreen(() => simulation, () => Show(Screen.Investing));
                    investing.Refresh();
                    host.Add(investing.Root);
                    break;
                default:
                    host.Add(BuildFeedScreen());
                    break;
            }
        }

        /// <summary>
        /// The page a screen leaves behind when building it threw.
        ///
        /// Names the screen and quotes the first line of the fault, because "it went blank" is not
        /// a report anybody can act on and this is going in front of players.
        /// </summary>
        private VisualElement BuildScreenFailure(Screen screen, Exception error)
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("shell.screen_failed"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var which = new Label(Loc.T("shell.screen_failed.where", screen.ToString()));
            which.AddToClassList("page-subtitle");
            panel.Add(which);

            var what = new Label(error?.Message ?? string.Empty);
            what.AddToClassList("gcard__terms");
            panel.Add(what);

            panel.Add(Hint(Loc.T("shell.screen_failed.note")));
            return panel;
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
                ? Loc.T("shell.upgrading_many", projects.Count.ToString())
                : Loc.T("shell.upgrading"));

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
                ? Loc.T("shell.finishing_today")
                : Loc.T("shell.days_left", Loc.Counted(left, "noun.day")));

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
            // Day one, or a player who stepped out and has had a few days to notice the corner is
            // quiet. Both routes end in the same phone.
            var paused = state.Guide.Stage == GuideStage.Paused;

            if ((state.Guide.Stage != GuideStage.Unseen && !paused) || phone.IsOpen)
            {
                return;
            }

            if (paused && state.Date.Day - pausedOn < DaysBeforeHeRingsBack)
            {
                return;
            }

            // The phone waits for the car. Ringing over a dark garage would throw away the one
            // piece of theatre the opening has.
            if (arrival != null && arrival.IsPlaying)
            {
                return;
            }

            if (!paused)
            {
                state.Guide.StartingCashUsd = state.CashUsd;
            }

            state.Guide.Stage = GuideStage.Talking;
            phone.Ring(callingBack: paused);
        }

        /// <summary>
        /// What happens when the player answers him.
        ///
        /// Either way the task strip takes over from the phone, because the three opening tasks are
        /// the shape of the first hour whether or not somebody wanted the tour.
        /// </summary>
        private void AnswerTheCousin(bool accepted)
        {
            // **Only a first acceptance starts at the beginning.** Somebody resuming after stepping
            // out is picked up where they left off, or the button would be a restart wearing the
            // word "later".
            //
            // Read off the step rather than off the stage. `Paused` is overwritten with `Talking`
            // by the ring itself, several frames before anybody answers, so this always saw
            // `Talking`, always reset to zero, and the playtest watched the whole tour start again
            // three days after asking him to call back. The step is the checkpoint, it survives a
            // save, and nothing else touches it.
            var resuming = state.Guide.Step > 0;

            state.Guide.Stage = accepted ? GuideStage.Touring : GuideStage.Finished;

            if (!resuming)
            {
                state.Guide.Step = 0;
            }

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

            // **The whole basket as one programme.** This looped and commissioned one per trait, so
            // four picks became four programmes each ticking the same calendar, landing together,
            // and filling the mail with four separate completions.
            if (releasePlan.Basket.Count > 0
                && !simulation.TryStartUpgrades(index, releasePlan.Basket, out var reason))
            {
                refused.Add(reason);
            }

            state.Monetization.SubscriptionPriceUsdPerMonth = releasePlan.PriceUsdPerMonth;
            state.Monetization.FreeTierTokensPerUserPerDay = releasePlan.FreeTokensPerDay;

            model.Line.Publish(versionName, state.Date, model.EffectiveCapability(state.Date),
                releasePlan.PriceUsdPerMonth, releasePlan.FreeTokensPerDay);

            simulation.State.RaiseEvent(new CompanyEvent(
                CompanyEventType.ModelReleased, state.Date,
                refused.Count == 0
                    ? Loc.T("shell.version_shipped", model.Name + " " + versionName)
                    : Loc.T("shell.version_shipped_but", model.Name + " " + versionName,
                        string.Join("  ", refused)),
                0L));

            if (refused.Count == 0)
            {
                AudioDirector.Confirm();
            }
            else
            {
                AudioDirector.Warning();
            }

            RefreshChrome();

            // **Say that the work started.** The money leaves, the version publishes and the player
            // lands on the official page, where the progress banner does not draw. Without this the
            // whole thing reads as an upgrade that completed the instant it was paid for, which is
            // exactly how a playtest reported it.
            if (releasePlan.Basket.Count > 0 && refused.Count == 0)
            {
                startedNotice?.Show(
                    Loc.T("upgrade.started.title"),
                    Loc.T("upgrade.started.note", model.Name));
            }

            Show(Screen.Management);
        }

        /// <summary>
        /// Which screen a guide target means, or null when it means none.
        ///
        /// **One table, because there are now three callers.** The tour opens the screen, rings the
        /// tab, and shuts the other tabs while it waits, and a second copy of this mapping would let
        /// the tour point at one tab and open another. That is exactly the class of drift that left
        /// six steps saying "click COMPUTE" while highlighting nothing.
        /// </summary>
        private static Screen? ScreenForGuideTarget(GuideTarget target) => target switch
        {
            GuideTarget.Site => Screen.Site,
            GuideTarget.Compute => Screen.Fleet,
            GuideTarget.Model => Screen.Model,
            GuideTarget.Create => Screen.Create,
            GuideTarget.Research => Screen.Research,
            GuideTarget.Team => Screen.Team,
            GuideTarget.Release => Screen.Release,
            GuideTarget.Upgrade => Screen.Upgrade,
            GuideTarget.Architecture => Screen.Family,
            GuideTarget.Offices => Screen.Offices,
            GuideTarget.Funding => Screen.Funding,
            GuideTarget.Ranking => Screen.Ranking,
            GuideTarget.Room => Screen.Room,
            _ => null
        };

        /// <summary>
        /// Puts the creator on the page a guide step is talking about.
        ///
        /// **The tour got lost here in the first playtest.** Opening the creator left the player on
        /// whatever page they were last on while Emil described the scale belt, then precision, then
        /// safety, none of which were on screen. A guide that names a page and does not open it is a
        /// voice-over.
        ///
        /// Called from the tour's own refresh rather than from the button, so a step reached by any
        /// route lands on the right page.
        /// </summary>
        private void PutCreatorOnStage(int stage)
        {
            if (creator == null || stage < 0 || stage >= ModelCreatorPanel.StageCount)
            {
                return;
            }

            if (current != Screen.Create)
            {
                Show(Screen.Create);
            }

            if (creator.Stage != stage)
            {
                creator.Stage = stage;
            }
        }

        /// <summary>Opens the screen a guide step is about.</summary>
        private void GoToGuideTarget(GuideTarget target)
        {
            var screen = ScreenForGuideTarget(target);

            if (screen.HasValue)
            {
                Show(screen.Value);
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
                ? Loc.T("shell.arranging_many", state.Hiring.OpenCount.ToString())
                : Loc.T("shell.arranging"));

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
                ? Loc.T("shell.answering_today")
                : Loc.T("shell.days_until_answer", Loc.Counted(left, "noun.day")));

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

            var caption = new Label(Loc.T("panel.upgrade_office"));
            caption.AddToClassList("office-upgrade__caption");
            button.Add(caption);

            return button;
        }

        /// <summary>
        /// The strip that carries the player from the board to the stock screen.
        ///
        /// Styled as one wide button rather than as a row of text, because it is a door and doors
        /// on this page are cards. It names both halves the way the screen itself does, so arriving
        /// there is not a surprise.
        /// </summary>
        private VisualElement BuildInvestingBanner()
        {
            var banner = new Button(() => Show(Screen.Investing));
            banner.AddToClassList("investbanner");

            var titles = new VisualElement();
            titles.AddToClassList("investbanner__titles");

            var row = new VisualElement();
            row.AddToClassList("investbanner__row");

            var mark = new Label(Loc.T("invest.title"));
            mark.AddToClassList("investbanner__mark");
            row.Add(mark);

            var rule = new VisualElement();
            rule.AddToClassList("investbanner__rule");
            row.Add(rule);

            var name = new Label(Loc.T("invest.banner"));
            name.AddToClassList("investbanner__name");
            row.Add(name);

            titles.Add(row);

            var note = new Label(Loc.T("invest.banner.note"));
            note.AddToClassList("investbanner__note");
            titles.Add(note);

            banner.Add(titles);

            var arrow = new Label(">");
            arrow.AddToClassList("investbanner__arrow");
            banner.Add(arrow);

            return banner;
        }

        private VisualElement BuildRankingScreen()
        {
            // The strap is carried by the terms row rather than sitting above it, so the board
            // opens on one line of explanation instead of two bands of small grey text.
            var page = NewPage(Loc.T("page.ranking"), string.Empty);
            UiParts.ExplainInline(page, Loc.T("page.ranking.strap"),
                TechNotes.Capability, TechNotes.MarketShare);

            // The way in to the stock screen, at the top of the board rather than in the bottom
            // bar. This is the page where a player is already looking at a list of companies and
            // wondering what they could do about them, which is the only moment the offer means
            // anything. A sixteenth slot on the bar would also overflow it.
            page.Add(BuildInvestingBanner());

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

                // **The share price rather than the capability score.** The score is already the
                // large figure one column to the left, so printing the capability beside it said
                // the same thing twice. What a player looking at a rival actually wants next is
                // what buying a piece of them costs, which is the number the investing screen
                // trades on and the one this board never showed.
                var detail = new Label(entry.IsPlayer
                    ? Loc.T("rank.share", UiFormat.Percent(entry.MarketShare, 2))
                    : Loc.T("rank.price_and_share",
                        UiFormat.SharePrice(simulation.SharePriceOf(entry.Competitor)),
                        UiFormat.Percent(entry.MarketShare, 2)));

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

            // A half-typed offer belongs to the lab it was typed about. Carrying it to the next
            // card would let a bonus meant for one company be sent to another by one click.
            rivals ??= new RivalPanel(() => simulation, () => ShowLabDossier(openLab));
            rivalActs ??= new RivalActionsPanel(() => simulation, () => ShowLabDossier(openLab));

            if (openLab != lab)
            {
                rivals.Reset();
                rivalActs.Reset();
            }

            openLab = lab;

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
                Loc.T("shell.founded_in", dossier.Founded.Year.ToString())
                + "  ·  " + dossier.Home.ToUpperInvariant() + "  ·  "
                + FateWord(dossier, state.Date));

            founded.AddToClassList("dossier__meta");
            titles.Add(founded);

            head.Add(titles);

            var close = new Button(() => labCard?.RemoveFromHierarchy()) { text = Loc.T("common.close") };
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
                var heading = new Label(Loc.T("panel.what_happened"));
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
                    Loc.T("labs.nothing_known"));

                nothing.AddToClassList("dossier__story");
                labCard.Add(nothing);
            }

            // The actions desk is handed in rather than appended, because the panel decides which
            // of its three sections is open and the smear desk is one of them. The card has a
            // fixed top and bottom, so a second sibling beside the scroller would be squashed
            // rather than scrolled, which is the deformation this stylesheet has shipped once.
            labCard.Add(rivals.Build(lab, () => rivalActs.Build(lab)));

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

            var headline = new Label(Loc.T("panel.regulatory"));
            headline.AddToClassList("regulatory__headline");
            regulatoryBanner.Add(headline);

            var what = new Label(action.Subtitle);
            what.AddToClassList("regulatory__what");
            regulatoryBanner.Add(what);

            // The caption sits on the bar rather than above it, because they are one object: the
            // sentence is what the bar is measuring.
            var status = new Label(Loc.T("regulatory.underway",
                Loc.Counted(action.DaysLeft, "noun.day")));

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
        /// Escape, and what it opens.
        ///
        /// **The game had no Escape at all.** `KeyboardShortcuts` bound space and the speeds; there
        /// was no pause menu, no deliberate save, no way to load without leaving to the main menu
        /// and no settings once a campaign had started.
        ///
        /// The key is read here rather than added to `KeyboardShortcuts.All`, because that table is
        /// the one the interface prints as a list of speed controls and Escape is not one of those.
        /// </summary>
        private void PollPauseKey()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            // A card over the menu closes first. Escape closing two things at once is how a player
            // ends up back in the game when they meant to step back one screen.
            if (report is { IsOpen: true })
            {
                report.Close();
                return;
            }

            EnsurePauseMenu();
            pause.Toggle();
            RefreshSheets();
        }

        private void EnsurePauseMenu()
        {
            if (pause != null)
            {
                return;
            }

            pause = new PauseMenu(() => simulation, RefreshSheets)
            {
                Closed = RefreshSheets,
                Quit = SceneFlow.LoadMainMenu
            };

            report = new FeedbackDialog(() => state.Date, RefreshSheets)
            {
                Closed = RefreshSheets
            };

            pause.Feedback = () =>
            {
                report.Open();
                RefreshSheets();
            };
        }

        /// <summary>
        /// Puts the pause sheet and the report card up or takes them down.
        ///
        /// Both are mounted on the shell root rather than inside the page, so a day rolling over
        /// underneath does not throw away the menu the player is reading. That is the same reason
        /// the tutorial phone lives there.
        /// </summary>
        private void RefreshSheets()
        {
            EnsurePauseMenu();

            pauseSheet?.RemoveFromHierarchy();
            pauseSheet = null;

            reportSheet?.RemoveFromHierarchy();
            reportSheet = null;

            if (pause.IsOpen)
            {
                pauseSheet = pause.Build();
                shellRoot.Add(pauseSheet);
            }

            if (report.IsOpen)
            {
                reportSheet = report.Build(Application.version);
                shellRoot.Add(reportSheet);
            }
        }

        /// <summary>
        /// The automatic save, on a real clock rather than a simulated one.
        ///
        /// **Real seconds, deliberately.** A player at x3 would otherwise be saved to disk three
        /// times as often as one at x1 for asking to watch the same week go by faster, and the
        /// setting says minutes.
        ///
        /// It does not fire while the pause menu is up. Writing over a slot underneath somebody who
        /// is looking at that slot is the one moment this could do real harm.
        /// </summary>
        private void PollAutosave()
        {
            var minutes = GameSettings.AutosaveMinutes;

            if (minutes <= 0 || state == null || pause is { IsOpen: true })
            {
                sinceAutosave = 0f;
                return;
            }

            sinceAutosave += Time.unscaledDeltaTime;

            if (sinceAutosave < minutes * 60f)
            {
                return;
            }

            sinceAutosave = 0f;
            SaveStore.Save(state);
        }

        /// <summary>
        /// Somebody has offered to buy the company.
        ///
        /// **The largest single decision in the game, so it is a banner rather than a card.** A
        /// player who never opens the stock screen would otherwise watch a nine or ten figure offer
        /// arrive and expire without ever being told. `AcceptAcquisition` and `DeclineAcquisition`
        /// were both written and called from nowhere until this existed.
        ///
        /// Rebuilt on the day count, never per frame, for the reason the regulatory banner is: it
        /// carries a transition and remaking it sixty times a second is a still image.
        /// </summary>
        private void RefreshBuyoutBanner()
        {
            var offer = state.PendingAcquisition;

            if (offer == null)
            {
                buyoutBanner?.RemoveFromHierarchy();
                buyoutBanner = null;
                buyoutDay = -1;
                return;
            }

            if (buyoutBanner != null && buyoutDay == offer.DaysElapsed)
            {
                return;
            }

            buyoutDay = offer.DaysElapsed;
            buyoutBanner?.RemoveFromHierarchy();

            buyoutBanner = new VisualElement();
            buyoutBanner.AddToClassList("buyout");

            var from = CompetitorCatalog.NameOf(offer.From);

            var headline = new Label(Loc.T("buyout.title"));
            headline.AddToClassList("buyout__headline");
            buyoutBanner.Add(headline);

            var body = new Label(Loc.T("buyout.body", from));
            body.AddToClassList("buyout__body");
            buyoutBanner.Add(body);

            var figures = new VisualElement();
            figures.AddToClassList("buyout__figures");

            figures.Add(UiParts.StatLine(Loc.T("buyout.offered"),
                UiFormat.Money(offer.AmountUsd)));

            figures.Add(UiParts.StatLine(Loc.T("buyout.book"),
                UiFormat.Money(simulation.BookValueUsd())));

            figures.Add(UiParts.StatLine(Loc.T("buyout.multiple"),
                offer.ValuationMultiple.ToString("0.00x",
                    System.Globalization.CultureInfo.InvariantCulture)));

            figures.Add(UiParts.StatLine(Loc.T("buyout.expires"),
                Loc.Counted(offer.DaysLeft, "noun.day")));

            buyoutBanner.Add(figures);

            var buttons = new VisualElement();
            buttons.AddToClassList("buyout__buttons");

            var sell = new Button(() =>
            {
                if (simulation.AcceptAcquisition(out var amount))
                {
                    ShowGameOver(
                        Loc.T("ending.sold"),
                        Loc.T("ending.sold.note", from, UiFormat.Money(amount)));

                    return;
                }

                Show(current);
            })
            { text = Loc.T("buyout.accept") };

            sell.AddToClassList("button");
            sell.AddToClassList("button--armed");
            buttons.Add(sell);

            var keep = new Button(() =>
            {
                simulation.DeclineAcquisition();
                Show(current);
            })
            { text = Loc.T("buyout.decline") };

            keep.AddToClassList("button");
            keep.AddToClassList("button--primary");
            buttons.Add(keep);

            buyoutBanner.Add(buttons);
            shellRoot.Add(buyoutBanner);
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

            var starved = project.IsWaitingForCompute || project.IsComputeStarved;

            var kicker = new Label(Loc.T(starved ? "research.waiting" : "research.running"));
            kicker.AddToClassList("rb__kicker");
            researchBanner.Add(kicker);

            var name = new Label(node.DisplayName);
            name.AddToClassList("rb__name");
            researchBanner.Add(name);

            var track = new VisualElement();
            track.AddToClassList("rb__track");

            // **Two marks, because a node needs two things and only one of them passes on its own.**
            // The bar is the true progress, which is the slower half. Behind it sits a ghost of how
            // far the calendar alone has run, so a player whose cluster is busy can see the gap
            // rather than a bar that simply refuses to move.
            var calendar = new VisualElement();
            calendar.AddToClassList("rb__calendar");
            calendar.style.width = Length.Percent((float)(project.CalendarProgress * 100.0));
            track.Add(calendar);

            var fill = new VisualElement();
            fill.AddToClassList("rb__fill");
            fill.EnableInClassList("rb__fill--starved", starved);
            fill.style.width = Length.Percent((float)(project.Progress * 100.0));
            track.Add(fill);

            researchBanner.Add(track);

            var left = Math.Max(0, project.DurationDays - project.DaysCompleted);

            var days = new Label(starved
                ? Loc.T("research.owed", UiFormat.Number(project.PetaflopDaysRemaining, 0))
                : Loc.T("research.days_left", Loc.Counted(left, "noun.day"),
                    UiFormat.Percent(project.Progress, 0)));

            days.AddToClassList("rb__days");
            researchBanner.Add(days);

            shellRoot.Add(researchBanner);
        }

        private VisualElement BuildFeedScreen()
        {
            var page = NewPage(Loc.T("intel.title"), Loc.T("intel.strap"));

            // Three cards side by side rather than three full-width bars.
            //
            // **The bars read as empty input fields, not as things to buy.** They were the same
            // width, the same height and the same colour as each other, so the one decision on the
            // page, which of these is worth four hundred thousand a month, was three identical
            // rectangles with different words in them. The pitch was in a tooltip nobody opens.
            var tiers = new VisualElement();
            tiers.AddToClassList("dcards");
            page.Add(tiers);

            // Memberships are bought and cancelled here and on the news screen, and both go through
            // the same call, because a retainer the player can start in one place and only stop in
            // another is a subscription trap rather than a decision.
            foreach (var tier in NewsCatalog.Memberships)
            {
                var captured = tier;
                var held = state.IsMember(tier);
                var monthly = IntelligenceService.MonthlyRetainerUsd(tier);

                var card = new Button(() =>
                {
                    simulation.SetIntelSubscription(captured, !held);
                    Show(Screen.Feed);
                });

                card.AddToClassList("dcard");
                card.EnableInClassList("dcard--held", held);

                var name = new Label(NewsCatalog.OutletName(tier));
                name.AddToClassList("dcard__name");
                card.Add(name);

                var pitch = new Label(NewsCatalog.OutletPitch(tier));
                pitch.AddToClassList("dcard__pitch");
                card.Add(pitch);

                var price = new Label(Loc.T("shell.per_month", UiFormat.Money(monthly)));
                price.AddToClassList("dcard__price");
                card.Add(price);

                var action = new Label(held
                    ? Loc.T("shell.on_retainer")
                    : Loc.T("shell.join_desk"));
                action.AddToClassList("dcard__action");
                card.Add(action);

                tiers.Add(card);
            }

            var feed = new VisualElement();
            feed.AddToClassList("panel");
            page.Add(feed);

            var signals = state.Signals;
            if (signals.Count == 0)
            {
                feed.Add(Hint(Loc.T("shell.no_notes")));
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

                var meta = new Label(Loc.T("shell.desk_confidence",
                    signal.Tier.ToString(), UiFormat.Percent(signal.Confidence, 0)));
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
            Screen.Team => "background_team",
            Screen.Marketing => "background_marketing",
            Screen.Mail => "background_mail",
            Screen.Hiring => "background_hiring",
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
                PlayEventCue(companyEvent.Type);
                if (recentEvents.Count > 60)
                {
                    recentEvents.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Event audio belongs to the presenter, not to the simulation. The same campaign must
        /// produce the same ledger with sound disabled, missing, or turned down to zero.
        /// </summary>
        private static void PlayEventCue(CompanyEventType type)
        {
            switch (type)
            {
                case CompanyEventType.TrainingCompleted:
                case CompanyEventType.ModelReleased:
                case CompanyEventType.ResearchCompleted:
                case CompanyEventType.UpgradeCompleted:
                case CompanyEventType.ArchitectureResearchCompleted:
                case CompanyEventType.FundingClosed:
                    AudioDirector.Positive();
                    break;

                case CompanyEventType.SafetyIncident:
                case CompanyEventType.CreditLineBreached:
                case CompanyEventType.LoanMissed:
                case CompanyEventType.LoanDefaulted:
                case CompanyEventType.DemandOverdue:
                case CompanyEventType.TaxDemanded:
                    AudioDirector.Warning();
                    break;
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

            var title = new Label(Loc.T("panel.run_finished"));
            title.AddToClassList("notice__title");
            card.Add(title);

            var body = new Label(string.IsNullOrWhiteSpace(message)
                ? Loc.T("shell.run_on_shelf")
                : message);

            body.AddToClassList("notice__body");
            card.Add(body);

            var note = new Label(Loc.T("shelf.scores_today"));

            note.AddToClassList("notice__note");
            card.Add(note);

            var buttons = new VisualElement();
            buttons.AddToClassList("notice__buttons");

            var release = new Button(() =>
            {
                runFinished?.RemoveFromHierarchy();
                Show(Screen.Release);
            })
            { text = Loc.T("mg.go_to_release") };

            release.AddToClassList("notice__button");
            release.AddToClassList("notice__button--go");
            buttons.Add(release);

            var upgrade = new Button(() =>
            {
                runFinished?.RemoveFromHierarchy();
                Show(Screen.Upgrade);
            })
            { text = Loc.T("mg.go_to_upgrade") };

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
            upgradeStrip?.Refresh();

            cashLabel.text = UiFormat.Money(state.CashUsd);
            RefreshCashArrows(state);
            RefreshStanding(state);
            effectBadges?.Refresh(state);

            pointsLabel.text = UiFormat.Number(state.ResearchPoints, 0);
            pointsButton.tooltip = state.ResearchPointsToday > 0.0
                ? Loc.T("shell.earning_points", UiFormat.Number(state.ResearchPointsToday, 1))
                : Loc.T("shell.learning_nothing");
            // Only on the site screen. It sat on top of the model creator and the research tree,
            // which is the same mistake the counter it replaced made.
            modelBanner?.SetHidden(current != Screen.Site);

            // Same rule as the model banner: the corner belongs to the office. On any other screen
            // it would sit on top of the page rather than beside it.
            newsBanner?.SetHidden(current != Screen.Site);
            newsBanner?.Refresh();
            grantBanner?.SetHidden(current != Screen.Site);
            grantBanner?.Refresh();

            RefreshFollowerBanners();

            // Hidden while the phone itself is up, or the sliver would sit under the call it is
            // meant to start.
            if (phone != null && phone.IsOpen)
            {
                phoneDock?.Hide();
            }
            else
            {
                phoneDock?.Refresh();
            }

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
            valuationLabel.text = Loc.T("shell.valued_at",
                UiFormat.Money(simulation.CurrentValuationUsd()));
            companyLabel.text = state.CompanyName;
            dateLabel.text = state.Date.ToString();

            var position = RankingBoard.PlayerPosition(simulation.Ranking());
            rankLabel.text = position > 0 ? $"rank #{position}" : "unranked";

            hud.Refresh(state.Date, clock.Speed, clock.DayProgress);

        }
    }
}
