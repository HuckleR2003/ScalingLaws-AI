using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Renders a screen to a PNG so it can be looked at.
    ///
    /// **Every layout fault in this project was found by looking, never by a test**, and until now
    /// there was no way to look at a UI Toolkit screen without opening the editor. The stylesheet was
    /// unreferenced for weeks with 170 tests green; a missing class collapsed the whole right-hand
    /// half of the creator; four trait cards overflowed their page by twelve pixels. Not one of
    /// those is visible to an assertion, and all of them are obvious in a picture.
    ///
    /// The frames land in <c>ScreenProof~/</c>. The tilde keeps Unity from importing them, the same
    /// arrangement the portrait probe already uses.
    ///
    /// It asserts the two things a picture cannot be trusted on: that something was actually drawn
    /// (a render texture nobody drew into is a flat rectangle of the clear colour, which on a dark
    /// page is indistinguishable from a screen that rendered correctly), and that nothing spilled
    /// off the right-hand edge.
    /// </summary>
    public sealed class ScreenProofTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        /// <summary>Where the frames go. Tilde-suffixed, so Unity ignores the folder.</summary>
        private static string ProofFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "ScreenProof~");

        // ---- the campaign the screens are drawn against ----------------------------------------

        /// <summary>
        /// A company with a model that has been updated three times, badly once.
        ///
        /// The interesting state is deliberately built rather than played: the release screen exists
        /// to show an older version out-holding the newest one, and a screenshot of a company with
        /// one release proves nothing about the layout that case needs.
        /// </summary>
        private static CompanySimulation Campaign()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 12));
            simulation.State.CashUsd = 240_000_000;
            simulation.SetRentedPetaflops(900.0);

            var model = new DeployedModel(
                "Aurora", ArchitectureId.SparseMixture, 52.0,
                GameDate.FromCalendar(2024, 2, 1), 6e10, 1.0);

            simulation.State.AddDeployedModel(model);

            model.SeedLine(20.0, 10_400.0);
            model.Line.Publish("Aurora 2", GameDate.FromCalendar(2024, 5, 12), 61.0, 22.0, 10_400.0);

            for (var day = 0; day < 40; day++)
            {
                model.Line.Advance();
            }

            // The one nobody liked. Dearer and worse, which is what leaves the audience behind.
            model.Line.Publish("Aurora 3", GameDate.FromCalendar(2024, 9, 3), 47.0, 30.0, 8_000.0);

            for (var day = 0; day < 22; day++)
            {
                model.Line.Advance();
            }

            simulation.State.Date = GameDate.FromCalendar(2024, 9, 25);
            return simulation;
        }

        // ---- the rig ------------------------------------------------------------------------------

        /// <summary>
        /// Draws one page into a texture and writes it out.
        ///
        /// The panel settings are a runtime copy of the game's own, so the scale mode, the theme and
        /// the reference resolution are the ones the player gets. Instantiated rather than used
        /// directly because pointing the real asset at a render texture would dirty it.
        /// </summary>
        private static IEnumerator Capture(VisualElement page, string fileName)
        {
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme")
                ?? Resources.Load<ThemeStyleSheet>("unity default runtime theme");

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.043f, 0.055f, 0.078f, 1f);

            var host = new GameObject("ScreenProof");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;

            var root = document.rootVisualElement;
            UiBootstrap.Prepare(root, null);
            root.style.flexGrow = 1;

            var frame = new VisualElement();
            frame.AddToClassList("content-host");
            frame.style.flexGrow = 1;
            frame.Add(page);
            root.Add(frame);

            // Layout, then the entry transition, then the scheduled bar reveals. Reading the texture
            // before those have run captures a page mid-animation, which is a picture of nothing.
            for (var pass = 0; pass < 12; pass++)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.9f);
            yield return null;

            var readable = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(ProofFolder);
            var path = Path.Combine(ProofFolder, fileName);
            File.WriteAllBytes(path, readable.EncodeToPNG());

            Assert.That(Spread(readable), Is.GreaterThan(0.02f),
                $"{fileName} came back flat. A render texture nobody drew into is a rectangle of "
                + "the clear colour, and on a dark page that looks exactly like a screen that "
                + "worked. Nothing reached this panel.");

            Assert.That(NothingSpillsOffTheRight(page), Is.True,
                $"{fileName} has content past the right-hand edge of the page. UI Toolkit overflows "
                + "silently, so this is only ever found by measuring or by looking.");

            Debug.Log($"[Scaling Laws] wrote {path}");

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
            texture.Release();
            Object.DestroyImmediate(texture);
        }

        /// <summary>How much of the picture is not one flat colour.</summary>
        private static float Spread(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            var lowest = 255;
            var highest = 0;

            for (var index = 0; index < pixels.Length; index += 37)
            {
                var value = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                lowest = Mathf.Min(lowest, value);
                highest = Mathf.Max(highest, value);
            }

            return (highest - lowest) / 255f;
        }

        /// <summary>
        /// Whether every child sits inside the page it is drawn on.
        ///
        /// Four trait cards once measured 1352px against a 1340px page and the fourth silently
        /// wrapped, which read as a layout decision rather than an overflow. Two pixels of slack,
        /// because layout rounding is not worth a failure.
        /// </summary>
        private static bool NothingSpillsOffTheRight(VisualElement page)
        {
            var right = page.worldBound.xMax + 2f;
            return Inside(page, right);
        }

        /// <summary>
        /// True for an element built by the runtime theme rather than by this project.
        ///
        /// Every class it carries has to be a `unity-` class. An element with no classes is not
        /// theme-internal, it is an unstyled element this project made, and those are exactly the
        /// ones worth catching.
        /// </summary>
        private static bool IsThemeInternal(VisualElement element)
        {
            var any = false;

            foreach (var name in element.GetClasses())
            {
                any = true;

                if (!name.StartsWith("unity-", System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return any;
        }

        /// <summary>
        /// Where an element sits, named by the styled ancestors around it and how wide each is.
        ///
        /// A control the runtime theme assembles reports its own class as
        /// `unity-base-slider__dragger-border`, which identifies nothing. The panel it is in is
        /// what the stylesheet can actually be aimed at, and the widths say which link in the
        /// chain is the one that stopped fitting.
        /// </summary>
        private static string Chain(VisualElement element)
        {
            var parts = new List<string>();
            var walk = element;

            while (walk != null && parts.Count < 8)
            {
                var classes = string.Join(".", walk.GetClasses());

                parts.Add(string.IsNullOrEmpty(classes)
                    ? $"<{walk.GetType().Name} {walk.worldBound.width:0}w>"
                    : $"{classes} [{walk.worldBound.xMin:0}..{walk.worldBound.xMax:0}]");

                walk = walk.parent;
            }

            return string.Join("  <  ", parts);
        }

        private static bool Inside(VisualElement element, float right)
        {
            foreach (var child in element.Children())
            {
                if (child.resolvedStyle.position == Position.Absolute)
                {
                    continue;
                }

                // Parts the runtime theme assembles inside its own controls. A slider's
                // dragger-border is sized to the whole track and offset by the value, so it leaves
                // its parent by design; no stylesheet here can reach it and there is nothing to
                // fix. Skipped only when every class it carries is a theme class, so anything this
                // project styles is still checked.
                if (IsThemeInternal(child))
                {
                    continue;
                }

                if (child.worldBound.width > 0f && child.worldBound.xMax > right)
                {
                    Debug.LogError($"[Scaling Laws] {child.GetType().Name} "
                        + $"[{string.Join(" ", child.GetClasses())}] reaches "
                        + $"{child.worldBound.xMax:0} against a page edge at {right:0}."
                        + $"\n  in: {Chain(child)}");

                    return false;
                }

                if (!Inside(child, right))
                {
                    return false;
                }
            }

            return true;
        }

        // ---- the screens ----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheUpgradeScreenDraws()
        {
            var simulation = Campaign();
            var panel = new UpgradeGridPanel(simulation, (_, _) => { }, () => { });
            panel.Refresh();

            yield return Capture(panel.Root, "upgrade.png");
        }

        /// <summary>
        /// The same screen with a basket in it, which is the half that is otherwise never seen.
        ///
        /// An empty right-hand panel is two labels and a hint. Everything that can actually go wrong
        /// there — the before-and-after bars, the list, the three-cell bill, the green button coming
        /// alive — only exists once something is picked.
        /// </summary>
        [UnityTest]
        public IEnumerator TheUpgradeScreenDrawsWithABasketInIt()
        {
            var simulation = Campaign();
            var panel = new UpgradeGridPanel(simulation, (_, _) => { }, () => { });
            panel.Refresh();

            panel.Pick(ModelTrait.Reasoning);
            panel.Pick(ModelTrait.Safety);
            panel.Pick(ModelTrait.ContextLength);

            Assert.That(panel.Chosen.Count, Is.EqualTo(3));

            yield return Capture(panel.Root, "upgrade_picked.png");
        }

        /// <summary>
        /// The rival card, with a relationship that has gone wrong and an offer form open.
        ///
        /// **The empty state teaches nothing here.** A lab nothing has happened with draws a neutral
        /// band, no history and twelve calm rows, which is the one arrangement where none of the
        /// colour rules, none of the bands and none of the form can be wrong. So the frame is taken
        /// against a company that has been poached from and reported.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRivalCardDraws()
        {
            var simulation = Campaign();
            var lab = CompetitorId.OpenAi;

            simulation.State.Relations.Record(lab, simulation.State.Date, -14.0,
                "relation.reason.poached", "Somebody");

            simulation.State.Relations.Record(lab, simulation.State.Date, -9.0,
                "relation.reason.reported", "Somebody");

            var panel = new RivalPanel(() => simulation, () => { });
            var acts = new RivalActionsPanel(() => simulation, () => { });

            // Both sections, because the card is tabs now and a proof of one of them proves
            // nothing about the other. The roster is the half that changed most.
            yield return Capture(panel.Build(lab, () => acts.Build(lab)), "rival_card.png");

            panel.ShowPeople();
            yield return Capture(panel.Build(lab, () => acts.Build(lab)), "rival_people.png");

            panel.ShowActions();
            yield return Capture(panel.Build(lab, () => acts.Build(lab)), "rival_actions.png");
        }

        /// <summary>
        /// The premises page, which now ends with the two places that are announced and not built.
        ///
        /// Worth a frame of its own because those rows have to read as unavailable at a glance. A
        /// row that looks like an option and refuses every click reads as a bug, and no test can
        /// tell the difference.
        /// </summary>
        /// <summary>
        /// The stock screen, with a position already open.
        ///
        /// An empty portfolio draws zeroes in every figure on the right, which is the one
        /// arrangement where none of the good/bad colouring, none of the ownership bar and none of
        /// the takeover block can be wrong. So the frame is taken holding a real parcel.
        /// </summary>
        [UnityTest]
        public IEnumerator TheInvestingScreenDraws()
        {
            var simulation = Campaign();
            simulation.State.CashUsd = 40_000_000_000L;

            simulation.TryBuyShares(CompetitorId.OpenAi, 30_000_000, out _, out _);

            var screen = new InvestingScreen(() => simulation, () => { });
            screen.Select(CompetitorId.OpenAi);
            screen.Refresh();

            yield return Capture(screen.Root, "investing.png");
        }

        /// <summary>
        /// The cabinet, opened, with the drawn parts in it.
        ///
        /// **The one frame that cannot be replaced by a test.** Whether the sleds line up inside
        /// the cabinet, whether the lights land on the indicator apertures rather than beside them,
        /// and whether a fan reads as a fan at that size are all questions only a picture answers.
        /// The geometry is a measured table and a measured table can still be measured wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOpenedCabinetDraws()
        {
            var simulation = Campaign();
            simulation.State.CashUsd = 4_000_000_000L;

            simulation.TryOpenServerRoom(true, out _);

            // Cards in the cabinet, and one fan, so the frame shows all three kinds of slot.
            simulation.State.Pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorA100, ComputeTier.ColocatedServers, 9,
                simulation.State.Date, 10_000, 0));

            simulation.Advance(1);
            simulation.TryFitFan(0, 0, out _);
            simulation.Advance(1);

            var panel = new RackEditorPanel(() => simulation, () => { });

            yield return Capture(panel.Build(0, 0), "cabinet.png");
        }

        /// <summary>
        /// The room in build mode: the floor on the left, the shop and the store room on the right.
        ///
        /// Until the four cabinets could all be bought, the room placed an enclosed rack and only
        /// ever an enclosed rack, so the choice this frame is about could not be made at all. The
        /// frame is also the only way to see whether the 3D room actually arrives behind the
        /// controls, because a render texture nobody drew into and a dark panel look identical to
        /// every assertion.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCabinetChooserDraws()
        {
            var simulation = Campaign();
            simulation.State.CashUsd = 4_000_000_000L;
            simulation.TryOpenServerRoom(true, out _);

            // Something in the store room, or half the rail is an empty-state message. A player
            // who has just bought a cabinet is the state this screen is for.
            simulation.TryBuyRack(ServerRack.Immersion, out _);
            simulation.TryBuyRack(ServerRack.HighDensity, out _);
            simulation.TryFitFan(0, 0, out _);
            simulation.TryStoreFan(0, 0);

            var screen = new ServerRoomScreen(() => simulation, () => { });

            // A test has no panel, so an event sent to a button is never dispatched. The screen
            // carries an entry point for exactly this, the way GameShell carries OpenScreenByName.
            screen.PickFor(1, 1);

            yield return Capture(screen.Build(), "cabinets.png");
        }

        /// <summary>The pause menu, which is what Escape opens.</summary>
        [UnityTest]
        public IEnumerator ThePauseMenuDraws()
        {
            var simulation = Campaign();
            var pause = new PauseMenu(() => simulation, () => { });
            pause.Open();

            yield return Capture(pause.Build(), "pause.png");
        }

        /// <summary>
        /// The four slots, with one of them holding a campaign.
        ///
        /// An empty list of four says nothing about what a full slot looks like, and the full one
        /// is where the summary line, the two-click overwrite and the date all live.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSaveSlotsDraw()
        {
            var simulation = Campaign();

            SaveStore.SaveTo(2, simulation.State);

            var pause = new PauseMenu(() => simulation, () => { });
            pause.Open();
            pause.OpenTab(PauseTab.Save);

            yield return Capture(pause.Build(), "slots.png");

            SaveStore.ClearSlot(2);
        }

        /// <summary>The card that asks for a name before it opens the form.</summary>
        [UnityTest]
        public IEnumerator TheReportCardDraws()
        {
            var simulation = Campaign();

            var report = new FeedbackDialog(() => simulation.State.Date, () => { });
            report.Open();

            yield return Capture(report.Build("0.9.0"), "report.png");
        }

        /// <summary>
        /// One person, opened, on all three tabs.
        ///
        /// The card is where everything the simulation knew about somebody finally appears, and a
        /// list of rows cannot show any of it. Three frames because three tabs, and the schedule is
        /// the one whose bar has to be looked at rather than measured.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePersonCardDraws()
        {
            var simulation = Campaign();

            simulation.State.Staff.SetOffice(OfficeTier.Floor);

            // **Somebody who actually asked for something.** Most people want nothing in
            // particular, which is the right rule and a useless frame: a review of this card needs
            // the case where the right-hand column has work to do. The names are walked until one
            // of them has expectations rather than hard-coding a name that a change to the hash
            // would quietly turn into the boring case again.
            var names = new[]
            {
                "Ada Kowalska", "Marek Nowak", "Ola Zielinska", "Piotr Lis", "Iga Wrona",
                "Jan Debski", "Zofia Mazur", "Kamil Sowa"
            };

            var chosen = names[0];

            foreach (var name in names)
            {
                var candidate = new Hire(StaffRole.ResearchScientist, 4,
                    simulation.State.Date.AddDays(-430), name,
                    PlayerSkill.Concept, HireSource.Specialist, 128.0);

                if (StaffExpectations.For(candidate).Count >= 1)
                {
                    chosen = name;
                    break;
                }
            }

            simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 4,
                simulation.State.Date.AddDays(-430), chosen,
                PlayerSkill.Concept, HireSource.Specialist, 128.0));

            // One benefit offered and the rest not, so the card shows a met expectation beside an
            // unmet one. Both halves have to be legible or the colour coding proves nothing.
            var wanted = StaffExpectations.For(simulation.State.Staff.Hires[^1]);

            if (wanted.Count > 0)
            {
                simulation.State.Benefits.Add(wanted[0]);
            }

            var panel = new PersonPanel(() => simulation, () => { });
            panel.Show(simulation.State.Staff.Headcount - 1);

            yield return Capture(panel.Build(), "person.png");

            panel.ShowSchedule();
            yield return Capture(panel.Build(), "person_schedule.png");

            panel.ShowRole();
            yield return Capture(panel.Build(), "person_role.png");
        }

        [UnityTest]
        public IEnumerator TheOfficeChooserDraws()
        {
            var simulation = Campaign();
            var chooser = new OfficeChooser(
                () => simulation.State, (_, _) => string.Empty, () => { });
            chooser.Refresh();

            yield return Capture(chooser.Root, "offices.png");

            // And the deal card, which is the half a list of rows cannot show. Opened through the
            // same method the row's button calls, because an EditMode or PlayMode proof dispatches
            // no pointer events into a detached panel.
            chooser.Open(OfficeTier.Loft);
            yield return Capture(chooser.Root, "offices_deal.png");
        }

        [UnityTest]
        public IEnumerator TheReleasePlannerDraws()
        {
            var simulation = Campaign();
            var planner = new ReleasePlanPanel(simulation, _ => { }, () => { });

            planner.Open(0, new[] { ModelTrait.Reasoning, ModelTrait.Safety });

            yield return Capture(planner.Root, "release_plan.png");
        }
    }
}
