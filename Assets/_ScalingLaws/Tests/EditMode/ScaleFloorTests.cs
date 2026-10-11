using NUnit.Framework;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// How far the panel is allowed to shrink the type, and where that has to give way.
    ///
    /// **A font size in the stylesheet is not a font size on the screen.** The panel scales with
    /// the window against a 1920x1080 reference, so the 11px floor `StylesheetTests` guards is
    /// 11px at the reference and nowhere else: measured on the real interface it drew at 9.2px on
    /// a 1600x900 window and 7.8px on a 1366x768 one, which is the commonest laptop panel still
    /// shipping. 340 rules sit on exactly 11px.
    ///
    /// `CorrectionFor` is the pure half of the fix, so it is the half that can be held here. The
    /// other half is a render, and `TheOperationsTypeIsReadableOnASmallerWindow` is that.
    /// </summary>
    [TestFixture]
    public sealed class ScaleFloorTests
    {
        private static readonly Vector2 Reference = new(1920f, 1080f);

        private static float Natural(Vector2 window) =>
            Mathf.Sqrt(window.x / Reference.x) * Mathf.Sqrt(window.y / Reference.y);

        private static float Effective(Vector2 window) =>
            Natural(window) * UiBootstrap.CorrectionFor(window, Reference);

        /// <summary>
        /// At or above the reference nothing is corrected at all.
        ///
        /// The neutral-option rule this project holds everything to: a player on the resolution
        /// the game was laid out for sees exactly what was laid out, or this change has quietly
        /// retuned every screen that was ever looked at.
        /// </summary>
        [Test]
        public void AtTheReferenceAndAboveNothingIsTouched()
        {
            foreach (var window in new[]
            {
                new Vector2(1920f, 1080f), new Vector2(2560f, 1440f), new Vector2(3840f, 2160f)
            })
            {
                Assert.AreEqual(1f, UiBootstrap.CorrectionFor(window, Reference), 1e-4f,
                    $"{window.x}x{window.y} is at or past the reference and is being corrected.");
            }
        }

        /// <summary>
        /// On the windows people actually have, the type stops shrinking at the floor.
        /// </summary>
        [Test]
        public void TheCommonLaptopWindowsAreHeldAtTheFloor()
        {
            foreach (var window in new[]
            {
                new Vector2(1600f, 900f), new Vector2(1536f, 864f), new Vector2(1366f, 768f)
            })
            {
                var before = Natural(window);
                var after = Effective(window);

                Assert.That(before, Is.LessThan(UiBootstrap.ScaleFloor),
                    $"{window.x}x{window.y} was never below the floor, so it proves nothing.");

                Assert.AreEqual(UiBootstrap.ScaleFloor, after, 1e-3f,
                    $"{window.x}x{window.y} draws its smallest type at {11f * after:F1}px rather "
                    + $"than the {11f * UiBootstrap.ScaleFloor:F1}px the floor promises.");
            }
        }

        /// <summary>
        /// The floor gives way rather than handing the layout a canvas it cannot stand up in.
        ///
        /// **Found by the suite, not by thinking.** Holding the type at 0.85 means handing the
        /// layout `window / 0.85` of virtual room, and on a small enough window that is less than
        /// the interface needs. In batchmode the screen is a few hundred pixels, the first version
        /// computed a correction of 2.2, and three PlayMode fixtures reported cards opening off
        /// the bottom of the window. A player who drags the window small would have met the same
        /// thing. A page that does not fit is worse than a page that is small.
        /// </summary>
        [Test]
        public void ACanvasIsNeverSqueezedBelowWhatTheInterfaceNeeds()
        {
            foreach (var window in new[]
            {
                new Vector2(1280f, 720f), new Vector2(1024f, 768f), new Vector2(640f, 480f),
                new Vector2(320f, 240f)
            })
            {
                var canvas = window.x / Effective(window);

                Assert.That(canvas, Is.GreaterThanOrEqualTo(UiBootstrap.MinimumCanvasWidth - 1f),
                    $"{window.x}x{window.y} leaves the layout {canvas:F0} virtual pixels, under "
                    + $"the {UiBootstrap.MinimumCanvasWidth:F0} the bottom bar alone needs.");
            }
        }

        /// <summary>
        /// The correction never makes the type smaller than leaving it alone would.
        ///
        /// It is a floor, so its one direction is up. A correction under one would mean the fix
        /// had become the thing it was written to prevent.
        /// </summary>
        [Test]
        public void TheCorrectionOnlyEverMakesThingsBigger()
        {
            for (var width = 320f; width <= 3840f; width += 64f)
            {
                var window = new Vector2(width, width * 9f / 16f);

                Assert.That(UiBootstrap.CorrectionFor(window, Reference),
                    Is.GreaterThanOrEqualTo(1f),
                    $"{window.x:F0}x{window.y:F0} is being shrunk by the thing that exists to "
                    + "stop it being shrunk.");
            }
        }

        /// <summary>Nonsense in gets one out, rather than a division by zero.</summary>
        [Test]
        public void AWindowWithNoSizeIsLeftAlone()
        {
            Assert.AreEqual(1f, UiBootstrap.CorrectionFor(Vector2.zero, Reference), 1e-4f);
            Assert.AreEqual(1f, UiBootstrap.CorrectionFor(new Vector2(1366f, 768f), Vector2.zero),
                1e-4f);
        }
    }
}
