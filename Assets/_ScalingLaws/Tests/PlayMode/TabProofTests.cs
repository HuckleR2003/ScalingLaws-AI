using System.Collections;
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

        /// <summary>
        /// Two years of company, assembled rather than played.
        ///
        /// Playing it would be more honest and take a hundred times as long, and the point here is
        /// to fill the screens, not to prove the economy. Everything set is something a real
        /// campaign would have by 2024: money, a fleet, staff, two live models with version
        /// history, research behind it and a corpus.
        /// </summary>
        private static void Furnish(CompanySimulation simulation)
        {
            var state = simulation.State;

            // The tutorial phone lays itself over the middle of the screen and the task strip sits
            // on top of the page. Both are correct on day one and both hide the thing being
            // reviewed, so this campaign is one that has already been through them.
            state.Guide.Restore(GuideStage.Finished, 0, 0L, true);

            state.Date = GameDate.FromCalendar(2024, 6, 18);
            state.CashUsd = 148_000_000;
            state.Reputation = 0.46;
            state.LifetimeRevenueUsd = 402_000_000;
            state.LifetimeOperatingCostUsd = 291_000_000;
            state.LifetimeCapitalSpentUsd = 96_000_000;

            simulation.SetRentedPetaflops(1_250.0);

            state.Pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 384,
                GameDate.FromCalendar(2023, 4, 11), 33_000, 45));

            state.OwnedDataSources |= DatasetSource.CuratedWeb | DatasetSource.CodeCorpus;
            state.AdoptedArchitectures.Add(ArchitectureId.SparseMixture);

            var flagship = new DeployedModel(
                "Aurora", ArchitectureId.SparseMixture, 54.0,
                GameDate.FromCalendar(2023, 8, 2), 6e10, 1.0);

            state.AddDeployedModel(flagship);
            flagship.SeedLine(20.0, 10_400.0);
            flagship.Line.Publish("Aurora 2", GameDate.FromCalendar(2024, 1, 9), 61.0, 22.0, 10_400.0);

            for (var day = 0; day < 40; day++)
            {
                flagship.Line.Advance();
            }

            // The one nobody liked, which is what the release list exists to show.
            flagship.Line.Publish("Aurora 3", GameDate.FromCalendar(2024, 5, 2), 48.0, 30.0, 8_000.0);

            for (var day = 0; day < 25; day++)
            {
                flagship.Line.Advance();
            }

            state.AddDeployedModel(new DeployedModel(
                "Kestrel", ArchitectureId.DenseTransformer, 41.0,
                GameDate.FromCalendar(2023, 2, 14), 2e10, 0.8));

            // One tick so everything derived from the above actually exists: market standing, the
            // books, awareness, service quality. Without it half the screens read zero, and the top
            // bar keeps printing the boot values it was built with: the chrome is refreshed on a
            // day rolling over, so a campaign assembled after Start never reaches it.
            simulation.AdvanceDay();
        }

        private static IEnumerator Capture(Camera _, PanelSettings settings, RenderTexture texture,
            string fileName)
        {
            for (var pass = 0; pass < 10; pass++)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.7f);

            var readable = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
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

            Furnish(shell.Simulation);

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

            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(settings);

            Assert.IsEmpty(missed, "Screens the shell would not open: " + string.Join(", ", missed));

            Debug.Log($"[Scaling Laws] {GameShell.ScreenNames.Count} tabs in {ProofFolder}.");
        }
    }
}
