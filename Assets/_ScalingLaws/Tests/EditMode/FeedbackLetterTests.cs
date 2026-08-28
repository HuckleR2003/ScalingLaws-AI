using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The one letter asking the player where they got stuck.
    ///
    /// **It exists because the alternative is silence.** A player who gets irritated closes the game
    /// and never says why, and for a first public build that feedback is worth more than the
    /// downloads. This fixture holds the two things that would quietly turn it into nothing: that it
    /// actually arrives, and that it never arrives twice.
    /// </summary>
    public sealed class FeedbackLetterTests
    {
        private static CompanySimulation Company(long cash = 12_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 11));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        private static int LetterCount(CompanySimulation simulation) =>
            simulation.State.Mail.All.Count(letter => letter.Kind == MailKind.Feedback);

        [Test]
        public void NoLetterArrivesOnTheFirstDay()
        {
            var simulation = Company();
            simulation.AdvanceDay();

            Assert.That(LetterCount(simulation), Is.Zero,
                "Asking somebody what they think before they have done anything is asking nothing.");
        }

        /// <summary>
        /// A company that has shipped gets asked, because it has seen the loop.
        /// </summary>
        [Test]
        public void ShippingAModelBringsTheLetter()
        {
            var simulation = Company();

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, capability: 30.0,
                releaseDate: simulation.State.Date, activeParameterCount: 8.0,
                priceMultiplier: 1.0));

            simulation.AdvanceDay();

            Assert.That(LetterCount(simulation), Is.EqualTo(1));
        }

        /// <summary>
        /// And so does a company that ran out of money, which is the other moment worth asking
        /// about: the player has just hit the wall and has the most to say.
        /// </summary>
        [Test]
        public void GoingUnderBringsTheLetterToo()
        {
            var simulation = Company(cash: -CompanyState.CreditLineUsd - 1_000_000);
            simulation.AdvanceDay();

            Assert.IsTrue(simulation.State.IsBankrupt, "This fixture needs an insolvent company.");
            Assert.That(LetterCount(simulation), Is.EqualTo(1));
        }

        /// <summary>A campaign that has done neither still gets asked eventually.</summary>
        [Test]
        public void ADriftingCampaignIsAskedOnTheFallbackDay()
        {
            var simulation = Company(cash: 400_000_000);

            for (var day = 0; day < CompanySimulation.FeedbackLetterDays + 2; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(LetterCount(simulation), Is.EqualTo(1));
        }

        /// <summary>
        /// **Once, ever.** A request for help that keeps arriving is an advertisement, and the flag
        /// that stops it has to survive a reload or every load posts another one.
        /// </summary>
        [Test]
        public void ItNeverArrivesTwiceHoweverLongTheCampaignRuns()
        {
            var simulation = Company(cash: 400_000_000);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, capability: 30.0,
                releaseDate: simulation.State.Date, activeParameterCount: 8.0,
                priceMultiplier: 1.0));

            for (var day = 0; day < 400; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(LetterCount(simulation), Is.EqualTo(1));
            Assert.IsTrue(simulation.State.FeedbackLetterSent);
        }

        /// <summary>
        /// The letter offers a way out of the game and a way to put it away.
        ///
        /// Without the second, a letter nobody wants to answer becomes furniture in the mailbox for
        /// the rest of the campaign.
        /// </summary>
        [Test]
        public void TheLetterOffersBothALinkAndAWayToCloseIt()
        {
            var simulation = Company();

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, capability: 30.0,
                releaseDate: simulation.State.Date, activeParameterCount: 8.0,
                priceMultiplier: 1.0));

            simulation.AdvanceDay();

            var letter = simulation.State.Mail.All.First(entry => entry.Kind == MailKind.Feedback);

            Assert.That(letter.Actions, Does.Contain(MailAction.OpenLink));
            Assert.That(letter.Actions, Does.Contain(MailAction.Decline));

            Assert.That(letter.Subject, Is.Not.Empty);
            Assert.That(letter.Body, Is.Not.Empty);
        }

        /// <summary>
        /// The address carries the build and the day, and nothing else.
        ///
        /// **Both are useless to identify anybody and both are what makes a report actionable.** A
        /// report that does not say which build it came from is a report about a game that may no
        /// longer exist.
        /// </summary>
        [Test]
        public void TheLinkCarriesTheBuildAndTheDayAndNothingElse()
        {
            var url = UI.FeedbackLink.UrlFor(new GameDate(137), "0.1.0");

            Assert.That(url, Does.StartWith(UI.FeedbackLink.BaseUrl));
            Assert.That(url, Does.Contain("build=0.1.0"));
            Assert.That(url, Does.Contain("day=137"));

            // One question mark and one separator: two parameters, no more.
            Assert.That(url.Count(character => character == '&'), Is.EqualTo(1),
                "Anything else on this address is something the player did not agree to send.");
        }

        /// <summary>An unknown build still produces a usable address rather than a broken one.</summary>
        [Test]
        public void AMissingVersionDoesNotProduceARaggedUrl()
        {
            var url = UI.FeedbackLink.UrlFor(new GameDate(0), string.Empty);

            Assert.That(url, Does.Contain("build=unknown"));
            Assert.That(url, Does.Contain("day=0"));
        }
    }
}
