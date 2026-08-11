using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The banner in the corner. It is three numbers a player glances at rather than reads, which is
    /// exactly the kind of readout that can be quietly wrong for months, so each one is pinned here.
    /// </summary>
    public sealed class SentimentTests
    {
        private static CompanySimulation RunFor(int days, uint seed = 515)
        {
            var simulation = new CompanySimulation(new CompanyState("Moodco", seed));
            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        private static void Ship(CompanySimulation simulation, double capability, double price = 1.0)
        {
            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, capability,
                simulation.State.Date, 2e10, price, ModelType.General));
        }

        [Test]
        public void ACompanyWithNothingOnSaleHasNoUsersAndNoOpinion()
        {
            var sentiment = RunFor(400).Sentiment();

            Assert.AreEqual(0.0, sentiment.Users, 1e-6, "Nothing is on sale, so nobody is using it.");
            Assert.AreEqual(0.0, sentiment.Satisfaction, 1e-9,
                "An audience the company does not serve has no opinion of it to report.");
        }

        [Test]
        public void ShippingSomethingGoodMakesPeopleArrive()
        {
            var simulation = RunFor(400);
            Ship(simulation, 60.0);

            for (var day = 0; day < 200; day++)
            {
                simulation.AdvanceDay();
            }

            var sentiment = simulation.Sentiment();

            Assert.Greater(sentiment.Users, 0.0, "A strong model finds people.");
            Assert.Greater(sentiment.Satisfaction, 0.0, "Its users have an opinion.");
        }

        /// <summary>
        /// The claim the readout makes: satisfaction is measured against the alternative, not against
        /// an absolute bar. The same model priced four times higher has to read worse, because its
        /// users are the ones who would rather be somewhere else.
        ///
        /// The price is set on the monetization policy rather than on the model. A model carries a
        /// price field, but the tick rewrites it from the company's policy every day, so a per model
        /// price is not a thing the game has. Setting it in a test looks like it works and is erased
        /// before the next reading.
        /// </summary>
        [Test]
        public void TheSameModelSatisfiesLessWhenItCostsFourTimesAsMuch()
        {
            static double Play(double price)
            {
                var simulation = RunFor(400, 515);
                simulation.State.Monetization.PaidPriceMultiplier = price;
                Ship(simulation, 60.0);

                for (var day = 0; day < 200; day++)
                {
                    simulation.AdvanceDay();
                }

                return simulation.Sentiment().Satisfaction;
            }

            var cheap = Play(1.0);
            var dear = Play(4.0);

            Assert.Greater(cheap, dear,
                $"Cheap read {cheap:P1} and expensive read {dear:P1}. If price does not move "
                + "satisfaction, the figure is not measuring anything a player controls.");
        }

        [Test]
        public void ArrowsAreAForecastAndTheyRunBothWays()
        {
            var flat = new UserSentiment(1000.0, 0.5, 0.0, 0.0);
            Assert.AreEqual(0, flat.Arrows, "A still market gets no arrows.");

            var rising = new UserSentiment(1000.0, 0.5, UserSentiment.ArrowStep * 3.5, 0.0);
            Assert.AreEqual(3, rising.Arrows, "A hard climb is three arrows, and three is the cap.");

            var falling = new UserSentiment(1000.0, 0.5, -UserSentiment.ArrowStep * 9.0, 0.0);
            Assert.AreEqual(-3, falling.Arrows,
                "A collapse cannot report more than three arrows down, or the banner grows.");

            var slight = new UserSentiment(1000.0, 0.5, UserSentiment.ArrowStep * 1.2, 0.0);
            Assert.AreEqual(1, slight.Arrows);
        }

        /// <summary>
        /// Found by looking at the game. On the first day, with nothing shipped, the corner banner read
        /// LEAVING in red over a market of zero people. A company that has not released anything is not
        /// a company whose customers are walking out.
        /// </summary>
        [Test]
        public void ACompanyThatHasShippedNothingIsNotACompanyLosingCustomers()
        {
            var nothing = new UserSentiment(0.0, 0.0, 0.0, 0.0);

            Assert.IsFalse(nothing.HasAudience);
            Assert.AreEqual("NO USERS YET", nothing.Mood,
                "LEAVING over an empty market is an alarm about something that has not happened.");

            var someone = new UserSentiment(1.0, 0.05, 0.0, 0.0);
            Assert.IsTrue(someone.HasAudience);
            Assert.AreEqual("LEAVING", someone.Mood,
                "With real users at five percent satisfaction the alarm is earned.");
        }

        [Test]
        public void TheMoodWordAlwaysMatchesTheNumberBesideIt()
        {
            // The word and the percentage sit next to each other in the banner, so a mismatch would be
            // visible and confusing rather than subtle.
            Assert.AreEqual("DELIGHTED", new UserSentiment(1.0, 0.90, 0.0, 0.0).Mood);
            Assert.AreEqual("HAPPY", new UserSentiment(1.0, 0.70, 0.0, 0.0).Mood);
            Assert.AreEqual("CONTENT", new UserSentiment(1.0, 0.50, 0.0, 0.0).Mood);
            Assert.AreEqual("RESTLESS", new UserSentiment(1.0, 0.30, 0.0, 0.0).Mood);
            Assert.AreEqual("LEAVING", new UserSentiment(1.0, 0.10, 0.0, 0.0).Mood);
            Assert.AreEqual("NO USERS YET", new UserSentiment(0.0, 0.90, 0.0, 0.0).Mood,
                "No users outranks any satisfaction figure, because there is nobody to be satisfied.");
        }

        [Test]
        public void NothingInTheBannerCanBeNaNOrOffItsScale()
        {
            var broken = new UserSentiment(double.NaN, 12.0, -8.0, double.NegativeInfinity);

            Assert.AreEqual(0.0, broken.Users, 1e-9);
            Assert.AreEqual(1.0, broken.Satisfaction, 1e-9, "Satisfaction is a fraction.");
            Assert.AreEqual(-1.0, broken.Momentum, 1e-9);
            Assert.AreEqual(0.0, broken.BestRivalUsers, 1e-9);
        }

        /// <summary>
        /// The banner reads the same market the tick moved. If it built its own view of the company,
        /// the two would drift, which is the exact bug that cost a save replay to find once already.
        /// </summary>
        [Test]
        public void TheBannerAgreesWithTheMarketItDescribes()
        {
            var simulation = RunFor(700);
            Ship(simulation, 55.0);

            for (var day = 0; day < 300; day++)
            {
                simulation.AdvanceDay();
            }

            var sentiment = simulation.Sentiment();
            var breakdown = simulation.MarketByType();
            var fromBreakdown = breakdown.TotalUsersOverall * breakdown.OverallShareOf(0);

            Assert.AreEqual(fromBreakdown, sentiment.Users, fromBreakdown * 1e-9 + 1e-6,
                "The corner and the Foundation panel are counting different companies.");
        }
    }
}
