using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The topicality bar says which of its two halves is holding it down.
    ///
    /// **Reported 12 October 2026 by a tester**: "I assume you have made it so that the topicality
    /// stat always goes down, no matter what over time, but you should change this, as if I have a
    /// model that is ahead of frontier, and after two months it [falls]".
    ///
    /// He was half right and the half he was right about is the one that matters. The calendar is
    /// flat for the first 240 days, so a model inside that window loses nothing to age. What he was
    /// almost certainly watching was the frontier moving under him: it gains about a capability
    /// point a month through 2024 and 2025, and each point takes a tenth off the reading. The bar
    /// could not tell him that, because a reading falling because the world moved looks exactly
    /// like one falling because the product got old.
    ///
    /// The rule is not changing. Being current is a property of the distance between your model and
    /// everybody else's, and that is the spine. What changed is that the screen now says so.
    /// </summary>
    public sealed class WhyTopicalityFallsTests
    {
        [Test]
        public void AModelLevelWithTheFrontierLosesNothingToAgeForTwoHundredAndFortyDays()
        {
            Assert.That(ProductStanding.FreshnessByAge(0), Is.EqualTo(1.0));
            Assert.That(ProductStanding.FreshnessByAge(Standing.FreshDays), Is.EqualTo(1.0),
                "The calendar is flat right up to the freshness band, which is what the tester "
                + "could not see.");

            Assert.That(ProductStanding.FreshnessByAge(60), Is.EqualTo(1.0),
                "Two months is well inside the flat part, so age cannot be what he watched fall.");

            Assert.That(ProductStanding.FreshnessByAge(Standing.StaleDays), Is.EqualTo(0.0));
        }

        [Test]
        public void TheFrontierHalfIsWhatMovesUnderAStandingStillModel()
        {
            Assert.That(ProductStanding.FreshnessByFrontier(50.0, 50.0), Is.EqualTo(1.0),
                "Level with the frontier is current.");

            Assert.That(ProductStanding.FreshnessByFrontier(60.0, 50.0), Is.EqualTo(1.0),
                "Ahead of the frontier is not more than current.");

            Assert.That(ProductStanding.FreshnessByFrontier(48.0, 50.0), Is.EqualTo(0.8).Within(1e-9),
                "Two points behind takes a fifth off, which is roughly two months of 2024.");

            Assert.That(ProductStanding.FreshnessByFrontier(40.0, 50.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void TheWorseHalfIsTheOneThatCounts()
        {
            // Old but still level with the frontier: age decides.
            var old = ProductStanding.TopicalityOf(600, 50.0, 50.0);
            Assert.That(old, Is.EqualTo(ProductStanding.FreshnessByAge(600)).Within(1e-9));

            // New but five points behind: the frontier decides.
            var behind = ProductStanding.TopicalityOf(10, 45.0, 50.0);
            Assert.That(behind, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void TheSentenceNamesTheHalfThatIsActuallyDragging()
        {
            Loc.Current = Language.English;

            var byFrontier = new ProductStanding("Coconut", true, 0.5, 0.0, 1000.0, 0L, 0L,
                10, 45.0, 50.0, 0L, 0L);

            Assert.That(byFrontier.WhyNotCurrent, Does.Contain("5.0"),
                "A new model five points behind is held back by the world, and the gap is the "
                + "number the player needs.");

            var byAge = new ProductStanding("Coconut", true, 0.5, 0.0, 1000.0, 0L, 0L,
                600, 50.0, 50.0, 0L, 0L);

            Assert.That(byAge.WhyNotCurrent, Does.Contain("600"),
                "An old model level with the frontier is held back by the calendar.");

            var neither = new ProductStanding("Coconut", true, 0.5, 0.0, 1000.0, 0L, 0L,
                10, 50.0, 50.0, 0L, 0L);

            Assert.That(neither.WhyNotCurrent, Is.Not.Empty,
                "A model that is current in both senses still says so rather than going blank.");

            var nothing = new ProductStanding(null, false, 0.0, 0.0, 0.0, 0L, 0L, 0, 0.0, 0.0,
                0L, 0L);

            Assert.That(nothing.WhyNotCurrent, Is.Empty,
                "A company with nothing on sale has no product to explain.");
        }

        [Test]
        public void TheGapIsPrintedInvariantlyBecauseSimulationCannotAskUiFormat()
        {
            Loc.Current = Language.Polish;

            var behind = new ProductStanding("Coconut", true, 0.5, 0.0, 1000.0, 0L, 0L,
                10, 47.5, 50.0, 0L, 0L);

            Assert.That(behind.WhyNotCurrent, Does.Contain("2.5"),
                "A raw format string follows the machine culture, which on this machine is Polish "
                + "and would print 2,5. That fault has shipped five times in this project.");

            Loc.Current = Language.English;
        }
    }
}
