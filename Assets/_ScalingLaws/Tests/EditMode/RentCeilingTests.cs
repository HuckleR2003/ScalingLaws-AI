using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The rent slider's range, which used to be the same on every day of every campaign.
    ///
    /// Forty petaflops serve a million ordinary accounts and the slider ran to forty thousand, so
    /// its travel was exactly one billion users from the first frame. On a fourteen hundred pixel
    /// row that is seven hundred thousand accounts per pixel, and a playtest reported the obvious
    /// consequence: the smallest movement anybody can make costs tens of thousands a day.
    /// </summary>
    public sealed class RentCeilingTests
    {
        /// <summary>
        /// A company with nobody on it is offered a fleet it could plausibly want.
        ///
        /// Both halves matter. Too low and the slider cannot buy enough for a first product; too
        /// high and it has no useful resolution, which is the fault being fixed.
        /// </summary>
        [Test]
        public void ANewCompanyIsOfferedRoomToGrowIntoAndNotAGiganticFleet()
        {
            var ceiling = RentReadout.CeilingPetaflops(0.0, 0.0);

            Assert.That(ceiling, Is.EqualTo(RentReadout.OpeningCeilingPetaflops),
                "A lab with no product should open on the small scale.");

            var accounts = HostingCatalog.CoversAccounts(ceiling);

            Assert.That(accounts, Is.LessThan(200_000_000.0),
                $"The opening slider still reaches {accounts:N0} accounts, which is not a decision "
                + "a company with no product is making.");

            Assert.That(accounts, Is.GreaterThan(10_000_000.0),
                "The opening slider cannot buy enough capacity for a first product.");
        }

        /// <summary>The range follows the audience up.</summary>
        [Test]
        public void TheCeilingRisesWithTheAudience()
        {
            var small = RentReadout.CeilingPetaflops(1_000_000.0, 0.0);
            var large = RentReadout.CeilingPetaflops(200_000_000.0, 0.0);

            Assert.That(large, Is.GreaterThan(small),
                "A company holding two hundred million people is offered no more than one holding "
                + "a million, so the slider stops being usable exactly when it matters most.");

            Assert.That(large, Is.LessThanOrEqualTo(RentReadout.FullScalePetaflops),
                "The ceiling ran past the largest fleet the game models.");
        }

        /// <summary>
        /// **It ratchets.** This is the half that would quietly cost somebody a campaign.
        ///
        /// A ceiling that fell when an audience shrank would clamp the player's own setting down
        /// without being asked, change the daily bill they had chosen, and do it on the worst day
        /// they were having. Asked for by name after a playtest.
        /// </summary>
        [Test]
        public void TheCeilingNeverFallsBelowWhatThePlayerHasAlreadySet()
        {
            const double alreadyRented = 9_000.0;

            var ceiling = RentReadout.CeilingPetaflops(0.0, alreadyRented);

            Assert.That(ceiling, Is.GreaterThanOrEqualTo(alreadyRented),
                "A company that lost its audience would have its rented fleet clamped down by the "
                + "interface, and the first it would know is the bill changing.");
        }

        /// <summary>
        /// The same promise, above the cap, which is the only place it could ever have broken.
        ///
        /// **This is report 10, and the test above it held nothing.** Nine thousand petaflops is
        /// comfortably under <see cref="RentReadout.FullScalePetaflops"/>, so the floor and the
        /// roof never disagreed and the fixture passed on a function that threw the floor away on
        /// its last line. A company can hold more than the cap: the creator's unblock button walks
        /// capacity to 400,000 petaflops to clear a run that cannot start, and commits what it
        /// finds. Those companies were handed a slider whose range ended below their own fleet,
        /// and a Slider clamps into the range it is given.
        ///
        /// Measured at four values, because one above the cap can pass by coincidence and four is
        /// a shape.
        /// </summary>
        [Test]
        public void TheCeilingStillDoesNotFallWhenTheFleetIsLargerThanTheSliderWouldOffer()
        {
            foreach (var rented in new[] { 40_001.0, 65_536.0, 160_000.0, 400_000.0 })
            {
                var ceiling = RentReadout.CeilingPetaflops(0.0, rented);

                Assert.That(ceiling, Is.GreaterThanOrEqualTo(rented),
                    $"A company renting {rented:N0} petaflops is offered a slider that stops at "
                    + $"{ceiling:N0}. The handle clamps into that range, the clamp raises the same "
                    + "change event a drag raises, and the fleet is cut without anybody asking.");
            }
        }

        /// <summary>
        /// And the cap still caps, which is the half this must not have bought with the other.
        /// </summary>
        [Test]
        public void TheCapStillHoldsForEveryCompanyThatHasNotPassedIt()
        {
            foreach (var users in new[] { 0.0, 50_000_000.0, 2_000_000_000.0 })
            {
                foreach (var rented in new[] { 0.0, 2_000.0, 39_000.0 })
                {
                    var ceiling = RentReadout.CeilingPetaflops(users, rented);

                    Assert.That(ceiling, Is.LessThanOrEqualTo(RentReadout.FullScalePetaflops),
                        $"{users:N0} users holding {rented:N0} petaflops were offered "
                        + $"{ceiling:N0}, past the largest fleet the game models. The floor is for "
                        + "fleets already held, not a way round the cap.");
                }
            }
        }

        /// <summary>A garbage reading cannot produce a slider with no range or an infinite one.</summary>
        [Test]
        public void NonsenseReadingsStillProduceAUsableSlider()
        {
            foreach (var users in new[] { double.NaN, double.PositiveInfinity, -5_000_000.0 })
            {
                var ceiling = RentReadout.CeilingPetaflops(users, 0.0);

                Assert.That(double.IsNaN(ceiling), Is.False, $"{users} produced NaN.");

                Assert.That(ceiling, Is.InRange(
                    RentReadout.OpeningCeilingPetaflops, RentReadout.FullScalePetaflops),
                    $"{users} produced a ceiling outside the band the slider can draw.");
            }
        }
    }
}
