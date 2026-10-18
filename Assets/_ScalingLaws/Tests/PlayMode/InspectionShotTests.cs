using System.Collections;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// A photograph of the regulator's open file, for the store pages and the devlog.
    ///
    /// **The one moment in this game that interrupts every screen at once, and there was no picture
    /// of it anywhere.** `TabProofTests` walks the tabs and `ScreenProofTests` renders single pages
    /// into their own panel, so neither can see this: the banner is mounted on the shell root rather
    /// than inside a page, which is the whole point of it.
    ///
    /// So this loads the real scene, opens a file on a furnished company and photographs the shell.
    /// It is a tool rather than a guard, but it still asserts, because a render texture nobody drew
    /// into is a rectangle of the clear colour and on a dark page that is indistinguishable from a
    /// screen that worked.
    /// </summary>
    public sealed class InspectionShotTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        private static string ProofFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "ScreenProof~");

        /// <summary>How much the frame varies, so a flat rectangle can be told from a picture.</summary>
        private static float Spread(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            byte low = 255;
            byte high = 0;

            for (var index = 0; index < pixels.Length; index += 97)
            {
                var value = (byte)((pixels[index].r + pixels[index].g + pixels[index].b) / 3);
                if (value < low) { low = value; }
                if (value > high) { high = value; }
            }

            return (high - low) / 255f;
        }

        /// <summary>
        /// The file a regulator opens, built by hand rather than played into.
        ///
        /// **Severe on purpose.** A minor incident lands as a small notice and never opens a file,
        /// so photographing one would be a picture of the wrong mechanism. The figures are the shape
        /// a real severe incident carries: a fine in the tens of millions and the model withdrawn.
        /// </summary>
        private static RegulatoryAction OpenFile(CompanyState state, string modelName)
        {
            var incident = new SafetyIncident(
                IncidentSeverity.Severe,
                state.Date,
                "User data from the assistant was reachable without authentication",
                reputationLoss: 0.22,
                fineUsd: 94_000_000L,
                forcedWithdrawal: true);

            return new RegulatoryAction(incident, state.Date, modelName);
        }

        [UnityTest]
        public IEnumerator TheRegulatorsOpenFileIsPhotographed()
        {
            // **English before the scene loads, not after.** The shell builds its chrome once on
            // the first frame, so setting the language afterwards leaves a page with an English
            // banner over a Polish top bar. The first frame of this shot came back exactly like
            // that, and a store page cannot carry a screenshot in two languages.
            var spoken = GameSettings.Language;
            GameSettings.SetLanguage(Language.English);

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

            var state = shell.Simulation.State;

            // Named after whatever the company is actually selling, so the banner reads as a
            // sentence about this campaign rather than about a placeholder.
            var flagship = shell.Simulation.Flagship();
            state.PendingAction = OpenFile(state, flagship?.Name ?? "AURORA");

            // A runtime copy pointed at a texture, so the real asset is never dirtied and the shot
            // is a known size whatever window the run happens to have.
            var settings = Object.Instantiate(document.panelSettings);
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);
            document.panelSettings = settings;

            // The banner is rebuilt from the shell's own refresh, and opening a tab is what runs it.
            Assert.That(shell.OpenScreenByName("Site"), Is.True, "no screen called Site");

            yield return null;

            // **Taken down after the screen opens, not before.** The cousin rings on the first
            // frame a company is looked at and mounts on the panel root, so clearing it earlier
            // just means it comes back: the first version of this shot had his phone over the
            // right-hand third of the frame.
            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            // It carries a CSS animation that flashes the headline white to red on a 1.7 second
            // cycle. Reading the texture immediately catches it mid-fade, which photographs as a
            // washed out headline rather than as the alarm it is.
            for (var pass = 0; pass < 12; pass++)
            {
                yield return null;
            }

            yield return new WaitForSeconds(1.1f);
            yield return null;

            var banner = document.rootVisualElement.Q(className: "regulatory");

            Assert.That(banner, Is.Not.Null,
                "The shell drew no regulatory banner for an open file. Either the refresh no longer "
                + "runs on a screen change, or the class name moved.");

            Assert.That(banner.resolvedStyle.width, Is.GreaterThan(400f),
                "The banner is on screen but almost no width, so the photograph would show a sliver.");

            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(ProofFolder);
            var path = Path.Combine(ProofFolder, "inspection_banner.png");
            File.WriteAllBytes(path, readable.EncodeToPNG());

            var spread = Spread(readable);
            Object.DestroyImmediate(readable);

            Assert.That(spread, Is.GreaterThan(0.02f),
                "inspection_banner.png came back flat, so nothing reached this panel.");

            Debug.Log($"[Scaling Laws] wrote {path}");

            GameSettings.SetLanguage(spoken);
        }
    }
}
