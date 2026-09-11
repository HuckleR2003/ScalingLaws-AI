using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Moving a page with the keyboard, and drawing where you are in it.
    ///
    /// **Reported by the author, who plays on a laptop with no mouse.** Half the screens here are
    /// taller than the window; the scrollers were built with the theme bar switched off, because that
    /// bar takes its width out of the content and would re-flow every page in the game the moment one
    /// got long enough to need it. So there was no bar to drag and no key that moved anything, and a
    /// section continuing below the fold read as a section that had been cut off.
    ///
    /// Both halves are arithmetic and both are tested here. `Input` cannot be driven from a test, so
    /// the keyboard reading is split from the answer, the same way `Resolve` was split from `Poll`
    /// when the speed keys were written.
    /// </summary>
    public sealed class PageScrollTests
    {
        private const float Viewport = 800f;
        private const float Content = 2000f;
        private const float Scrollable = Content - Viewport;

        /// <summary>A sixtieth of a second, which is the frame the arrows are actually held for.</summary>
        private const float Frame = 1f / 60f;

        private static float? Down(float offset) =>
            KeyboardShortcuts.ResolveScroll(false, true, false, false, false, false,
                offset, Viewport, Content, Frame);

        [Test]
        public void AHeldArrowMovesThePageAndNothingElseDoes()
        {
            Assert.That(KeyboardShortcuts.ResolveScroll(false, false, false, false, false, false,
                0f, Viewport, Content, Frame), Is.Null,
                "A frame with no key down must not write an offset, or the page fights every other "
                + "thing that wants to move it.");

            var moved = Down(0f);
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Value, Is.GreaterThan(0f).And.LessThan(Viewport),
                "One frame of a held arrow moved " + moved + " pixels. A frame should be a nudge, "
                + "not a jump.");
        }

        /// <summary>
        /// A long frame is a scene load or an alt-tab, not the player holding the key for a quarter
        /// of a second. Without the clamp the page leaps the length of the screen.
        /// </summary>
        [Test]
        public void OneSlowFrameDoesNotThrowThePage()
        {
            var normal = KeyboardShortcuts.ResolveScroll(false, true, false, false, false, false,
                0f, Viewport, Content, Frame);

            var stalled = KeyboardShortcuts.ResolveScroll(false, true, false, false, false, false,
                0f, Viewport, Content, 0.9f);

            Assert.That(stalled, Is.Not.Null);
            Assert.That(stalled.Value, Is.LessThan(normal.Value * 4f),
                "A frame that took nearly a second scrolled " + stalled + " pixels against "
                + normal + " for a normal one.");
        }

        [Test]
        public void ThePageStopsAtBothEnds()
        {
            Assert.That(KeyboardShortcuts.ResolveScroll(true, false, false, false, false, false,
                0f, Viewport, Content, Frame), Is.Null,
                "Holding up at the top of the page writes the same offset every frame forever.");

            Assert.That(Down(Scrollable), Is.Null,
                "Holding down at the bottom writes the same offset every frame forever.");

            var nearTheEnd = Down(Scrollable - 2f);
            Assert.That(nearTheEnd.Value, Is.EqualTo(Scrollable).Within(0.001f),
                "The last step overshot the bottom of the page.");
        }

        [Test]
        public void APageThatFitsTheWindowRefusesToScroll()
        {
            Assert.That(KeyboardShortcuts.ResolveScroll(false, true, false, false, true, false,
                0f, Viewport, 400f, Frame), Is.Null,
                "There is nothing below the fold, so nothing should move and no key should be "
                + "taken from whatever else might want it.");
        }

        /// <summary>
        /// A screenful keeps a sliver of the last one. Jumping exactly one window loses the line the
        /// reader stopped on, every single time.
        /// </summary>
        [Test]
        public void AScreenfulOverlapsTheScreenBeforeIt()
        {
            var jumped = KeyboardShortcuts.ResolveScroll(false, false, false, true, false, false,
                0f, Viewport, Content, Frame);

            Assert.That(jumped, Is.Not.Null);
            Assert.That(jumped.Value, Is.LessThan(Viewport),
                "A screenful moved the whole window, so the line being read left the screen.");

            Assert.That(jumped.Value, Is.GreaterThan(Viewport * 0.5f),
                "A screenful moved less than half a window, which makes a long page a chore.");
        }

        [Test]
        public void HomeAndEndBeatEverythingElseHeldAtTheSameTime()
        {
            Assert.That(KeyboardShortcuts.ResolveScroll(false, true, false, true, true, false,
                500f, Viewport, Content, Frame), Is.EqualTo(0f));

            Assert.That(KeyboardShortcuts.ResolveScroll(true, false, true, false, false, true,
                500f, Viewport, Content, Frame), Is.EqualTo(Scrollable));
        }

        // ---- the bar -------------------------------------------------------------------------------

        [Test]
        public void TheThumbIsTheShareOfThePageThatIsOnScreen()
        {
            var (height, top) = PageScrollbar.ThumbGeometry(Viewport, Content, 0f, 600f);

            Assert.That(height, Is.EqualTo(600f * (Viewport / Content)).Within(0.01f));
            Assert.That(top, Is.EqualTo(0f).Within(0.01f));
        }

        /// <summary>
        /// The travel is measured against the track **minus the thumb**, or the thumb runs off the
        /// bottom by exactly its own length and the bar stops meaning anything at the end of a page.
        /// </summary>
        [Test]
        public void TheThumbLandsFlushWithTheBottomAtTheBottom()
        {
            const float track = 600f;
            var (height, top) = PageScrollbar.ThumbGeometry(Viewport, Content, Scrollable, track);

            Assert.That(top + height, Is.EqualTo(track).Within(0.01f),
                "At the bottom of the page the thumb sits at " + (top + height) + " on a "
                + track + " track.");
        }

        [Test]
        public void AVeryLongPageStillHasSomethingToGrab()
        {
            var (height, _) = PageScrollbar.ThumbGeometry(Viewport, 400_000f, 0f, 600f);

            Assert.That(height, Is.GreaterThanOrEqualTo(PageScrollbar.MinimumThumb),
                "The thumb is " + height + " pixels tall, which is nothing to aim at.");
        }

        [Test]
        public void APageThatFitsGetsNoBarAtAll()
        {
            var (height, _) = PageScrollbar.ThumbGeometry(Viewport, 400f, 0f, 600f);

            Assert.That(height, Is.EqualTo(0f),
                "A bar over a page that fits is furniture, and furniture that says the page "
                + "continues when it does not.");
        }

        // ---- the table -----------------------------------------------------------------------------

        /// <summary>
        /// Every binding is described, and described in both languages.
        ///
        /// The action text is a phrase-book key rather than a sentence, because this table is static
        /// and a table built once at type load keeps whatever language it was built in. That is the
        /// same fault this project has now found in nineteen catalogs.
        /// </summary>
        [Test]
        public void EveryKeyIsDescribedInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var shortcut in KeyboardShortcuts.All)
                    {
                        Assert.That(Loc.T(shortcut.Action), Is.Not.EqualTo(shortcut.Action),
                            shortcut.KeyName + " renders its own key as its description in "
                            + language + ", so the phrase is missing.");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
