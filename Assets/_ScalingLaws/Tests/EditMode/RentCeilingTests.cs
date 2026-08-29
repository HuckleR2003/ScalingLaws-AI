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
