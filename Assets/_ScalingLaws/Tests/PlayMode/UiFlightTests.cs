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
    /// Records a walk through the interface, one frame at a time, for a video.
    ///
    /// **A screen recorder would have been easier and would have recorded the wrong thing.** The
    /// page transition is 260ms and the insight card is 140ms; a capture that samples wherever the
    /// frame rate lands catches those at a different point every run, so two recordings of the same
    /// build differ for reasons that have nothing to do with the build. Worse, batchmode runs as
    /// fast as it can, so real-time capture would produce a flipbook.
    ///
    /// `Time.captureDeltaTime` is the tool for exactly this: it pins the clock to a fixed step and
    /// every animation, transition and scheduled callback advances by that step per frame regardless
    /// of how long the frame actually took. Frame 412 is the same picture on every run forever.
    ///
    /// Frames land in <c>UiFlight~/</c>, and `Tools/make_videos.py` encodes them.
    /// </summary>
    public sealed class UiFlightTests
    {
        private const int Width = 1600;
        private const int Height = 900;

        /// <summary>30fps, and the clock is pinned to it.</summary>
        private const int FramesPerSecond = 30;

        private const float Step = 1f / FramesPerSecond;

        /// <summary>Frames each screen is held for. A shade under two seconds.</summary>
        private const int HoldFrames = 52;

        /// <summary>Frames the opening title is held for.</summary>
        private const int TitleFrames = 40;

        private static string OutputFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "UiFlight~");

        /// <summary>
        /// The order the tour walks in, which is the order a player meets them.
        ///
        /// Not `GameShell.ScreenNames`: that is declaration order, and it opens on a release planner
        /// with nothing in it. This is a route, so it starts where the company is and ends on the
        /// thing the whole game is about.
        /// </summary>
        private static readonly string[] Route =
        {
            "Site", "Model", "Create", "Research", "Family", "Upgrade",
            "Team", "Hiring", "Fleet", "Business", "Marketing",
            "Management", "News", "Ranking", "Offices", "Funding"
        };

        [UnityTest]
        public IEnumerator RecordTheInterfaceTour()
        {
            // **The campaign is saved first and then resumed, rather than assembled after the
            // scene loads.** The top bar is built once when the shell starts and refreshed when a
            // day rolls over, so a company furnished afterwards leaves the chrome printing the boot
            // values: fourteen million dollars and January 2022 over a screen showing two years of
            // history. That is invisible in a contact sheet and glaring in a video, because it is on
            // screen for the whole thirty seconds.
            //
            // Going in through the save also means the tour walks the load path a returning player
            // walks, which is the one worth filming.
            var seed = new CompanySimulation(new CompanyState("Prometheus AI", 12));
            TabProofCampaign.Furnish(seed);
            SaveStore.Save(seed.State);

            SceneFlow.ResumeSavedCampaign = true;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            var document = Object.FindFirstObjectByType<UIDocument>();

            Assert.That(shell, Is.Not.Null);
            Assert.That(document, Is.Not.Null);

            Assert.That(shell.Simulation.State.CashUsd, Is.GreaterThan(50_000_000L),
                "The shell did not resume the campaign, so the video would show an empty company.");

            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            var settings = Object.Instantiate(document.panelSettings);
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            document.panelSettings = settings;

            if (Directory.Exists(OutputFolder))
            {
                Directory.Delete(OutputFolder, true);
            }

            Directory.CreateDirectory(OutputFolder);

            // Pin the clock. Everything below this line advances by exactly one thirtieth of a
            // second per frame, whatever the machine is doing.
            var wasCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = Step;

            var readable = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var frame = 0;

            try
            {
                // Open on the site, held, so the video does not begin mid-transition.
                shell.OpenScreenByName("Site");
                yield return null;

                for (var index = 0; index < TitleFrames; index++)
                {
                    yield return Shoot(readable, texture, frame++);
                }

                foreach (var screen in Route)
                {
                    Assert.That(shell.OpenScreenByName(screen), Is.True, $"No screen {screen}.");

                    for (var index = 0; index < HoldFrames; index++)
                    {
                        yield return Shoot(readable, texture, frame++);
                    }
                }

                // Finish on the architecture screen with a card open, because the tour is partly
                // about the interface explaining itself and that is the one control that does.
                shell.OpenScreenByName("Family");
                yield return null;

                var badge = document.rootVisualElement.Q(className: "infodot");

                if (badge != null)
                {
                    for (var index = 0; index < 14; index++)
                    {
                        yield return Shoot(readable, texture, frame++);
                    }

                    using (var submit = NavigationSubmitEvent.GetPooled())
                    {
                        submit.target = badge;
                        badge.SendEvent(submit);
                    }

                    for (var index = 0; index < HoldFrames; index++)
                    {
                        yield return Shoot(readable, texture, frame++);
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = wasCapture;

                Object.DestroyImmediate(readable);
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(settings);
            }

            Assert.That(frame, Is.GreaterThan(300), "The tour came out too short to be a video.");

            Debug.Log($"[Scaling Laws] {frame} frames at {Width}x{Height} in {OutputFolder}. "
                + $"{frame / (float)FramesPerSecond:0.0} seconds at {FramesPerSecond}fps.");
        }

        private static IEnumerator Shoot(Texture2D readable, RenderTexture texture, int frame)
        {
            yield return null;

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(
                Path.Combine(OutputFolder, $"ui_{frame:0000}.png"),
                readable.EncodeToPNG());
        }
    }
}
