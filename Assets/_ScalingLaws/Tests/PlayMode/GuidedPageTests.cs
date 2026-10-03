using System.Collections;
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
    /// What the tour costs the page it is drawn over.
    ///
    /// **Reported on 2026-09-30 as "the blue background covers the tab".** It is not a background
    /// and nothing is drawn over anything: the clearance for the strip was set as
    /// <c>paddingBottom</c> on the <c>ScrollView</c> itself, and a ScrollView lays its viewport out
    /// inside its own padding box. So the reserve came out of the *visible* half of the page rather
    /// than out of the scrollable half: three hundred pixels of every tab stopped being drawn, the
    /// window's own colour showed through the hole, and the page could not be scrolled into it
    /// because as far as the scroller was concerned there was nothing there to scroll to.
    ///
    /// The distinction this fixture measures is the whole repair. A reserve belongs on the content
    /// container, where it lengthens the page; on the box it shortens the window.
    /// </summary>
    public sealed class GuidedPageTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        /// <summary>
        /// A tab that is not the creator and not one of the two rooms, opened with the tour up.
        ///
        /// MOC is the one the report came from, and it is a long page, which is the case where
        /// losing the bottom of the window actually removes something a player needs.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTourDoesNotTakeTheBottomOfTheWindowAwayFromThePage()
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

            var root = document.rootVisualElement;

            foreach (var ringing in root.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            var previous = Loc.Current;

            try
            {
                // Polish, because it is the longer language and therefore the taller strip, which
                // is the worst case for anything measured against the strip's height.
                Loc.Current = Language.Polish;

                shell.Simulation.State.Guide.Stage = GuideStage.Touring;
                shell.Simulation.State.Guide.Step = 3;

                Assert.IsTrue(shell.OpenScreenByName("Fleet"), "MOC did not open.");

                for (var pass = 0; pass < 40; pass++)
                {
                    yield return null;
                }

                var strip = root.Q(className: "guide");
                var scroller = root.Q<ScrollView>(className: "page-scroll");

                Assert.That(strip, Is.Not.Null, "The tour strip is not up, so this measures nothing.");
                Assert.That(scroller, Is.Not.Null, "MOC is not in the shell's page scroller.");

                var box = scroller.worldBound;
                var window = scroller.contentViewport.worldBound;

                Assert.That(window.height, Is.GreaterThan(box.height - 2f),
                    "The tour is taking its clearance out of the part of the page that is drawn. "
                    + $"The scroller is {box.height:N0}px tall and only {window.height:N0}px of it "
                    + "is a viewport, so the rest of the tab is not rendered at all and the window "
                    + "shows through. Padding belongs on the content container, which makes the "
                    + "page longer, not on the box, which makes the window shorter.");

                // And the clearance still has to exist, or the last band of a long page sits under
                // the strip with no way to bring it out. Moving the fault is not fixing it.
                Assert.That(scroller.contentContainer.resolvedStyle.paddingBottom,
                    Is.GreaterThan(100f),
                    "Nothing is holding the foot of the page clear of the strip any more.");

                // And a frame of it, because the report was about what the screen looks like and a
                // measurement is not a picture.
                var folder = System.IO.Path.Combine(
                    System.IO.Directory.GetParent(Application.dataPath)!.FullName, "GuideProof~");

                System.IO.Directory.CreateDirectory(folder);

                for (var pass = 0; pass < 6; pass++)
                {
                    yield return null;
                }

                var previousTarget = RenderTexture.active;
                RenderTexture.active = texture;

                var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                frame.Apply();

                RenderTexture.active = previousTarget;

                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(folder, "guided_fleet.png"), frame.EncodeToPNG());

                Object.DestroyImmediate(frame);
            }
            finally
            {
                shell.Simulation.State.Guide.Stage = GuideStage.Finished;
                Loc.Current = previous;

                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
