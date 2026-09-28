using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Photographs every tab in the game, against a campaign worth photographing.
    ///
    /// **A new company renders nineteen empty screens.** No models, no research, no staff, no
    /// history: every tab says "nothing here yet" and a design review of that teaches nothing. The
    /// campaign below is built first, so the pictures show the screens as a player two years in
    /// actually meets them, which is the state they have to be readable in.
    ///
    /// Frames land in <c>TabProof~/</c>. This is a review tool, so it asserts only the two things
    /// that would make a picture a lie: that the screen was drawn at all, and that the shell did not
    /// throw on the way in. Everything else is for looking at.
    /// </summary>
    public sealed class TabProofTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        private static string ProofFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "TabProof~");

        private static IEnumerator Capture(Camera _, PanelSettings settings, RenderTexture texture,
            string fileName)
        {
            for (var pass = 0; pass < 10; pass++)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.7f);

            // **The texture's own size, never the constant.** Every frame here used to be
            // 1920x1080 so the two could not disagree, and the first proof taken at another size
            // read past the end of the buffer and failed with a d3d12 message about bounds rather
            // than anything to do with the page.
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(ProofFolder);
            File.WriteAllBytes(Path.Combine(ProofFolder, fileName), readable.EncodeToPNG());

            var spread = Spread(readable);
            Object.DestroyImmediate(readable);

            Assert.That(spread, Is.GreaterThan(0.02f),
                $"{fileName} came back flat, so nothing was drawn into the panel.");
        }

        private static float Spread(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            var lowest = 255;
            var highest = 0;

            for (var index = 0; index < pixels.Length; index += 41)
            {
                var value = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                lowest = Mathf.Min(lowest, value);
                highest = Mathf.Max(highest, value);
            }

            return (highest - lowest) / 255f;
        }

        [UnityTest]
        public IEnumerator EveryTabDraws()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.panelSettings, Is.Not.Null);

            TabProofCampaign.Furnish(shell.Simulation);

            // Half the pass in Polish, so the review sees what a Polish player sees. A phrase that
            // is twice as long as its English original overflows a fixed-width control, and that is
            // only ever visible in a picture.
            Loc.Current = Language.English;

            // A runtime copy pointed at a texture, so the real asset is never dirtied and the shot
            // is a known size whatever window the run happens to have.
            var settings = Object.Instantiate(document.panelSettings);
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);

            document.panelSettings = settings;

            // The phone rings on the first frame a new company is looked at, and it mounts on the
            // panel root rather than inside the page, so it stays up across every screen change
            // until somebody answers it. That is right in the game and wrong in a contact sheet.
            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            var missed = new System.Collections.Generic.List<string>();

            foreach (var name in GameShell.ScreenNames)
            {
                if (!shell.OpenScreenByName(name))
                {
                    missed.Add(name);
                    continue;
                }

                yield return Capture(null, settings, texture, $"tab_{name.ToLowerInvariant()}.png");
            }

            // One more frame with an info card open. The card only exists while a pointer is resting
            // on a badge, so it is invisible to every other pass over these screens, and it is the
            // one piece of this interface that is nothing but text: if a band wraps badly or a
            // section runs off the bottom, only a picture says so.
            shell.OpenScreenByName("Family");
            yield return null;

            var badge = document.rootVisualElement.Q(className: "infodot");
            Assert.That(badge, Is.Not.Null, "No info badge on the architecture screen.");

            // Buttons answer a submit the same way they answer a click, and a synthetic pointer
            // press is three events that have to arrive in the right order to do the same thing.
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = badge;
                badge.SendEvent(submit);
            }

            yield return Capture(null, settings, texture, "card_open.png");

            // The tour, mid-conversation. It only exists while the guide is running, so every other
            // pass over these screens is blind to it, and it is the one part of the interface that
            // is nothing but a paragraph of somebody talking: a line that wraps to four rows pushes
            // its own buttons off the strip and only a picture says so.
            shell.Simulation.State.Guide.Stage = GuideStage.Touring;
            shell.Simulation.State.Guide.Step = 9;   // the step that hands over the favour
            shell.OpenScreenByName("Research");
            yield return Capture(null, settings, texture, "guide_en.png");

            Loc.Current = Language.Polish;
            shell.OpenScreenByName("Research");
            yield return Capture(null, settings, texture, "guide_pl.png");

            shell.Simulation.State.Guide.Stage = GuideStage.Finished;
            Loc.Current = Language.English;

            // **The joint research panel, which draws nothing until somebody is at the working
            // group level.** Nine months of a relationship is a long way past where any assembled
            // campaign starts, so without putting a lab there by hand this panel has never once
            // appeared in a proof frame.
            var ally = CompetitorId.Cohere;

            shell.Simulation.State.Relations.Record(ally, shell.Simulation.State.Date,
                RivalRelations.Best, "relation.reason.published");

            shell.Simulation.State.Alliances.Sign(ally, shell.Simulation.State.Date);
            shell.Simulation.State.Alliances.Sign(ally, shell.Simulation.State.Date);

            shell.OpenScreenByName("Research");
            yield return Capture(null, settings, texture, "consortium_en.png");

            // The board with something signed on it. The main sweep runs before any of this exists,
            // so the sections it is there to show have never been in a frame.
            shell.Simulation.State.Deals.Add(new StandingDeal(ally, RelationOffer.DistributionLicence,
                shell.Simulation.State.Date, shell.Simulation.State.Date.AddDays(164)));

            shell.OpenScreenByName("Ranking");
            yield return Capture(null, settings, texture, "allies_en.png");

            Loc.Current = Language.Polish;
            shell.OpenScreenByName("Ranking");
            yield return Capture(null, settings, texture, "allies_pl.png");
            Loc.Current = Language.English;

            Loc.Current = Language.Polish;
            shell.OpenScreenByName("Research");
            yield return Capture(null, settings, texture, "consortium_pl.png");
            Loc.Current = Language.English;

            // And again in Polish, on the four screens the author asked to have translated.
            Loc.Current = Language.Polish;

            // **`Feed` is on this list because it was reported as untranslated.** The whole right
            // hand column of the news screen was English in a Polish campaign: `NewsCatalog` stored
            // its own words, which is the catalog fault eighteen others were converted for, and
            // `NewsDesk` built its headlines by interpolation. A render is the only thing that
            // answers whether that is actually finished.
            foreach (var name in new[] { "Family", "Upgrade", "ReleasePlan", "Research", "News" })
            {
                shell.OpenScreenByName(name);
                yield return Capture(null, settings, texture, $"pl_{name.ToLowerInvariant()}.png");
            }

            Loc.Current = Language.English;

            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(settings);

            Assert.IsEmpty(missed, "Screens the shell would not open: " + string.Join(", ", missed));

            Debug.Log($"[Scaling Laws] {GameShell.ScreenNames.Count} tabs in {ProofFolder}.");
        }

        /// <summary>
        /// The creator with the tour running on it, on the two pages a tester reported cut off.
        ///
        /// **Reported twice and in almost the same words**: on PRZEGLĄD the section heading and its
        /// buttons are not there, the right-hand side of MARKA goes missing, and leaving the page
        /// and coming back repairs both. A fault that repairs itself on a rebuild is a fault about
        /// the first layout pass, which no EditMode test can see and which no other frame in this
        /// file catches, because every other creator shot here is taken with the tour finished.
        ///
        /// Two frames per page: the first paint, and the same page after going away and returning.
        /// If they differ, the pair says what the difference is, which is why this exists.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCreatorUnderTheTourIsNotCutOff()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);

            // **The language is flipped after the shell was built, so a handful of labels in
            // these frames read English on a Polish page.** That is this fixture, not the game:
            // the creator's title and its BACK button are set once in `Build`, and the game picks
            // its language before the shell exists and reloads the scene when it is changed. Do
            // not go looking for a translation bug in `tour_*.png`.
            Loc.Current = Language.Polish;

            var settings = Object.Instantiate(document.panelSettings);
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);

            document.panelSettings = settings;

            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            shell.Simulation.State.Guide.Stage = GuideStage.Touring;

            foreach (var page in new[] { "create_brand", "create_review" })
            {
                var step = -1;

                for (var index = 0; index < GuideScript.Steps.Count; index++)
                {
                    if (GuideScript.Steps[index].Id == page)
                    {
                        step = index;
                        break;
                    }
                }

                Assert.That(step, Is.GreaterThanOrEqualTo(0), "no tour step called " + page);

                shell.Simulation.State.Guide.Step = step;

                var name = page.Replace("create_", string.Empty);

                // How a player gets here: the tour opens the screen and puts the creator on the
                // page the step names. Nothing else is touched, so this is the first paint.
                shell.OpenScreenByName("Create");

                yield return Capture(null, settings, texture, $"tour_{name}_first.png");

                // And this is "go away and come back", which the tester says repairs it.
                shell.OpenScreenByName("Research");
                yield return null;
                shell.OpenScreenByName("Create");

                yield return Capture(null, settings, texture, $"tour_{name}_again.png");
            }

            shell.Simulation.State.Guide.Stage = GuideStage.Finished;
            Loc.Current = Language.English;

            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(settings);
        }

        /// <summary>
        /// The creator's heading and its two buttons stay on the screen while the tour is running.
        ///
        /// **Reported twice, on two different stages, in almost the same words**: on PRZEGLĄD the
        /// section heading and its buttons are not there, the right-hand side of MARKA goes
        /// missing, and leaving the page and coming back repairs it. Measured, the cause was one
        /// thing: the whole creator sat inside the shell's page scroller, the tour reserves the
        /// foot of the screen for its strip, and the tour then scrolls its own highlight into
        /// view. On a page 838px tall in a 647px window that lands somewhere in the middle, which
        /// cuts the heading off the top and the buttons off the bottom at the same time. Going
        /// away and coming back put the offset back to zero, which restores the heading only.
        ///
        /// So this does not measure the reserve, which was never the fault. It measures the two
        /// things that must never move: the heading is on the screen, the buttons are on the
        /// screen, and the buttons are above the strip rather than behind it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCreatorStillFitsWhileTheTourIsUp()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            var document = Object.FindFirstObjectByType<UIDocument>();

            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");
            Assert.That(document, Is.Not.Null);

            var settings = Object.Instantiate(document.panelSettings);
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);

            document.panelSettings = settings;

            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            // Polish on purpose: it is what the tester was playing, and it is the longer of the
            // two languages, so the strip at the foot of the screen is at its tallest here.
            Loc.Current = Language.Polish;

            shell.Simulation.State.Guide.Stage = GuideStage.Touring;

            var root = document.rootVisualElement;

            // Every step of the tour that opens the creator, because the two the tester found were
            // two different stages and nothing says a third is safe.
            for (var at = 0; at < GuideScript.Steps.Count; at++)
            {
                var step = GuideScript.Steps[at];

                if (step.CreatorStage < 0)
                {
                    continue;
                }

                shell.Simulation.State.Guide.Step = at;

                shell.OpenScreenByName("Create");

                // The tour rings its highlight on a delay and scrolls to it afterwards, which is
                // the half of this that did the damage. Waiting past that is the whole point.
                for (var pass = 0; pass < 40; pass++)
                {
                    yield return null;
                }

                var header = root.Q(className: "stage-header");
                var footer = root.Q(className: "stage-footer");
                var page = root.Q(className: "content-host");
                var strip = root.Q(className: "guide");

                Assert.That(header, Is.Not.Null, $"{step.Id}: no stage header on the creator.");
                Assert.That(footer, Is.Not.Null, $"{step.Id}: no stage footer on the creator.");
                Assert.That(page, Is.Not.Null, $"{step.Id}: no content host.");
                Assert.That(strip, Is.Not.Null, $"{step.Id}: the tour strip is not up.");

                var window = page.worldBound;
                var head = header.worldBound;
                var foot = footer.worldBound;
                var bar = strip.worldBound;

                Assert.That(head.yMin, Is.GreaterThanOrEqualTo(window.yMin - 0.5f),
                    $"{step.Id}: the stage heading is above the top of the page, so it is cut off. "
                    + $"heading {head}, page {window}");

                Assert.That(foot.yMax, Is.LessThanOrEqualTo(window.yMax + 0.5f),
                    $"{step.Id}: the WSTECZ/DALEJ row is below the bottom of the page, so it is cut "
                    + $"off. footer {foot}, page {window}");

                Assert.That(foot.yMax, Is.LessThanOrEqualTo(bar.yMin + 0.5f),
                    $"{step.Id}: the WSTECZ/DALEJ row is behind the tour strip. footer {foot}, "
                    + $"strip {bar}");
            }

            shell.Simulation.State.Guide.Stage = GuideStage.Finished;
            Loc.Current = Language.English;

            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(settings);
        }

        /// <summary>
        /// The creator under the tour, in a window smaller than the one every other proof uses.
        ///
        /// **Every "cut off at the bottom" report in this project has come from a laptop**, and the
        /// panel is `ScaleWithScreenSize` against a 1920x1080 reference with `match 0.5`, so at
        /// exactly 1920x1080 the scale is one and a proof render and the laptop see the same page.
        /// They plainly do not, which means the game's window is not 1920x1080 on that machine:
        /// windowed mode, a taskbar, or a display scale. Rather than guess which, this renders the
        /// page at sizes a laptop actually gives and looks at what breaks.
        ///
        /// It is a proof rather than an assertion on purpose. What is wrong at a small size is a
        /// layout judgement, and the frames are the evidence.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCreatorUnderTheTourOnASmallerWindow()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            var document = Object.FindFirstObjectByType<UIDocument>();

            Assert.That(shell, Is.Not.Null);
            Assert.That(document, Is.Not.Null);

            Loc.Current = Language.Polish;

            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            shell.Simulation.State.Guide.Stage = GuideStage.Touring;

            // 1366x768 is the commonest laptop panel still shipping; 1600x900 is a 1920 screen
            // running windowed with a taskbar. Both are 16:9, so nothing here is about aspect.
            // **Nothing is destroyed until the loop is over.** The document still points at the
            // settings it was last given, so freeing one at the end of an iteration leaves the
            // next `Instantiate(document.panelSettings)` reading a destroyed object, and the run
            // dies on a missing reference rather than on anything about the page.
            var spent = new List<Object>();

            foreach (var size in new[] { new Vector2Int(1600, 900), new Vector2Int(1366, 768) })
            {
                var settings = Object.Instantiate(document.panelSettings);
                var texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                texture.Create();

                settings.targetTexture = texture;
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(Width, Height);

                document.panelSettings = settings;

                yield return null;

                foreach (var step in new[] { "create_review", "create_data" })
                {
                    for (var index = 0; index < GuideScript.Steps.Count; index++)
                    {
                        if (GuideScript.Steps[index].Id != step)
                        {
                            continue;
                        }

                        shell.Simulation.State.Guide.Step = index;
                        break;
                    }

                    shell.OpenScreenByName("Create");

                    for (var pass = 0; pass < 30; pass++)
                    {
                        yield return null;
                    }

                    var name = step.Replace("create_", string.Empty);
                    yield return Capture(null, settings, texture, $"small_{size.y}_{name}.png");

                    var footer = document.rootVisualElement.Q(className: "stage-footer");
                    var header = document.rootVisualElement.Q(className: "stage-header");
                    var page = document.rootVisualElement.Q(className: "content-host");

                    Debug.Log($"SMALL {size.x}x{size.y} {step}: "
                        + $"page={page?.worldBound} header={header?.worldBound} "
                        + $"footer={footer?.worldBound}");
                }

                spent.Add(texture);
                spent.Add(settings);
            }

            shell.Simulation.State.Guide.Stage = GuideStage.Finished;
            Loc.Current = Language.English;

            foreach (var thing in spent)
            {
                Object.DestroyImmediate(thing);
            }
        }
    }
}
