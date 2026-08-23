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

            TabProofCampaign.Furnish(shell.Simulation);

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

            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(settings);

            Assert.IsEmpty(missed, "Screens the shell would not open: " + string.Join(", ", missed));

            Debug.Log($"[Scaling Laws] {GameShell.ScreenNames.Count} tabs in {ProofFolder}.");
        }
    }
}
