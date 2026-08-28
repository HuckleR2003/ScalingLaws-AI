using System.Collections;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
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

        private static bool Inside(VisualElement element, float right)
        {
            foreach (var child in element.Children())
            {
                if (child.resolvedStyle.position == Position.Absolute)
                {
                    continue;
                }

                if (child.worldBound.width > 0f && child.worldBound.xMax > right)
                {
                    Debug.LogError($"[Scaling Laws] {child.GetType().Name} "
                        + $"[{string.Join(" ", child.GetClasses())}] reaches "
                        + $"{child.worldBound.xMax:0} against a page edge at {right:0}.");

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

            var card = panel.Build(lab);
            card.Add(acts.Build(lab));

            yield return Capture(card, "rival_card.png");
        }

        /// <summary>
        /// The premises page, which now ends with the two places that are announced and not built.
        ///
        /// Worth a frame of its own because those rows have to read as unavailable at a glance. A
        /// row that looks like an option and refuses every click reads as a bug, and no test can
        /// tell the difference.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOfficeChooserDraws()
        {
            var simulation = Campaign();
            var chooser = new OfficeChooser(
                () => simulation.State, (_, _) => string.Empty, () => { });
            chooser.Refresh();

            yield return Capture(chooser.Root, "offices.png");
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
