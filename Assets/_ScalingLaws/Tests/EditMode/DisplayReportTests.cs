using NUnit.Framework;
using ScalingLaws.Persistence;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The display readout, and the window sizes it sits under.
    ///
    /// **This exists because two people spent three days describing two different screens.** The
    /// reports and the proof renders could not be reconciled and there was nothing on any screen
    /// that said what either machine was drawing. A line that is wrong about that would be worse
    /// than no line at all, so the parts of it that can be checked without a panel are checked.
    /// </summary>
    public sealed class DisplayReportTests
    {
        /// <summary>
        /// The aspect is a reduced ratio, because that is the fact that can differ between two
        /// machines and change the layout, and 16:9 against 16:10 is one glance apart where
        /// 1.778 against 1.600 is not.
        /// </summary>
        [Test]
        public void TheAspectIsAReducedRatio()
        {
            Assert.That(DisplayReport.Aspect(1920, 1080), Is.EqualTo("16:9"));
            Assert.That(DisplayReport.Aspect(1366, 768), Is.EqualTo("683:384"));
            Assert.That(DisplayReport.Aspect(1920, 1200), Is.EqualTo("8:5"));
            Assert.That(DisplayReport.Aspect(1280, 1024), Is.EqualTo("5:4"));
        }

        /// <summary>A display that reports nonsense is answered, never divided by.</summary>
        [Test]
        public void NothingAboutAnImpossibleDisplayThrows()
        {
            Assert.That(DisplayReport.Aspect(0, 0), Is.EqualTo("?"));
            Assert.That(DisplayReport.Aspect(-1, 1080), Is.EqualTo("?"));
            Assert.That(DisplayReport.Line(null), Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// Every offered window is the shape the interface was laid out for, to within half a pixel.
        ///
        /// **The first version of this asserted an exact ratio and it failed, correctly, on
        /// 1366x768.** That is the commonest laptop panel still shipping, it is sold as 16:9, and
        /// it is 683:384, which is 1.77865 against 1.77778. Nobody would drop it from the list for
        /// that, and pretending it is 16:9 would be writing down something untrue, so the guard
        /// measures the thing that matters instead: how far the logical page moves.
        ///
        /// Measured on the real page with the tour up, which is the tallest it gets:
        /// 1920x1080 and 1600x900 both give 1920.00 x 943.20, and 1366x768 gives 1920.47 x 943.36.
        /// Half a pixel, so the budget is one, and it is measured in pixels of logical page
        /// rather than in a ratio: the first version of this converted the two and got it wrong by
        /// a factor of two, which is its own small lesson about proxies.
        ///
        /// A 16:10 or a 4:3 would be a different matter entirely and would need every screen
        /// measured at that shape before it could be offered, which is why the tolerance is tight
        /// rather than generous.
        /// </summary>
        [Test]
        public void EveryOfferedWindowIsTheShapeTheGameWasLaidOutFor()
        {
            var offered = 0;

            foreach (var size in GameSettings.WindowSizes)
            {
                if (size.Width == 0 && size.Height == 0)
                {
                    continue;
                }

                offered++;

                // **The panel's own arithmetic, not a ratio with a tolerance bolted on.**
                // `ScaleWithScreenSize` with `MatchWidthOrHeight` at 0.5 takes the geometric mean
                // of the two axes against the reference, and the logical page is the window
                // divided by that. Writing it out here means the guard measures the thing that
                // actually reflows a screen rather than a proxy for it, and the first version of
                // this test got the proxy wrong by a factor of two.
                var scale = System.Math.Sqrt(size.Width / 1920.0)
                    * System.Math.Sqrt(size.Height / 1080.0);

                var logical = size.Width / scale;

                Assert.That(logical, Is.EqualTo(1920.0).Within(1.0),
                    $"{size.Width}x{size.Height} ({DisplayReport.Aspect(size.Width, size.Height)}) "
                    + $"gives a logical page {logical:0.0} wide rather than 1920, which reflows "
                    + "screens nobody has measured at that shape");
            }

            Assert.That(offered, Is.GreaterThan(1),
                "a chooser with one entry is not a chooser");
        }

        /// <summary>
        /// "Let the game decide" is in the list and is the only pair with a zero in it.
        ///
        /// Zero rather than a default pair, so it stays a real option rather than being one row
        /// that happens to match today's arithmetic and silently stops matching tomorrow.
        /// </summary>
        [Test]
        public void LettingTheGameDecideIsAnOptionAndIsWrittenAsNothing()
        {
            var zeroes = 0;

            foreach (var size in GameSettings.WindowSizes)
            {
                if (size.Width == 0 || size.Height == 0)
                {
                    zeroes++;

                    Assert.That(size.Width, Is.EqualTo(0));
                    Assert.That(size.Height, Is.EqualTo(0));
                }
            }

            Assert.That(zeroes, Is.EqualTo(1),
                "exactly one row means the game works the size out for itself");
        }
    }
}
