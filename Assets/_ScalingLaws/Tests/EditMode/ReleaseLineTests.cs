using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Versions of one model, and where its users actually are.
    ///
    /// **The case worth defending is the bad update.** A version list that always moved everybody
    /// onto the newest thing would be a label, not a mechanic — the whole reason to build this is
    /// that shipping something worse leaves most of the audience on what they already had, and the
    /// player can see that happening and has to decide what to do about it.
    /// </summary>
    public sealed class ReleaseLineTests
    {
        private static ReleaseLine Started(double capability = 40.0, double price = 20.0)
        {
            var line = new ReleaseLine();
            line.Publish("v1", GameDate.Start, capability, price, 10_000.0);
            return line;
        }

        private static void Days(ReleaseLine line, int count)
        {
            for (var day = 0; day < count; day++)
            {
                line.Advance();
            }
        }

        [Test]
        public void TheFirstVersionHasEverybody()
        {
            var line = Started();

            Assert.That(line.Count, Is.EqualTo(1));
            Assert.That(line.Newest.Adoption, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void SharesAlwaysAddUpToOne()
        {
            var line = Started();

            line.Publish("v2", GameDate.Start, 45.0, 20.0, 10_000.0);
            line.Publish("v3", GameDate.Start, 30.0, 25.0, 10_000.0);
            Days(line, 40);

            Assert.That(line.Versions.Sum(version => version.Adoption),
                Is.EqualTo(1.0).Within(1e-6),
                "A list that adds up to 99% is a list the player will notice and stop trusting.");
        }

        [Test]
        public void ABetterVersionWinsButNotOvernight()
        {
            var line = Started(40.0);
            var better = line.Publish("v2", GameDate.Start, 55.0, 20.0, 10_000.0);

            Assert.That(better.Adoption, Is.EqualTo(ReleaseLine.DayOneAdoption).Within(1e-9),
                "Day one is the automatic updaters and nobody else.");

            Days(line, 3);
            var afterThreeDays = better.Adoption;

            Assert.That(afterThreeDays, Is.GreaterThan(ReleaseLine.DayOneAdoption));
            Assert.That(afterThreeDays, Is.LessThan(0.85),
                "Three days in it must not already be finished, or shipping early buys nothing.");

            Days(line, 25);

            Assert.That(better.Adoption, Is.GreaterThan(0.85),
                "A clearly better version has to end up carrying the audience.");
        }

        [Test]
        public void ABadUpdateLosesToTheVersionPeopleLiked()
        {
            var line = Started(50.0);
            var worse = line.Publish("v2", GameDate.Start, 32.0, 50.0, 10_000.0);

            Days(line, 30);

            var loved = line.Versions[0];

            Assert.That(worse.Adoption, Is.LessThan(loved.Adoption),
                "This is the whole mechanic: a worse release does not take the audience.");

            Assert.That(loved.Adoption, Is.GreaterThan(0.6),
                "Most people stay on the version they already had and liked.");
        }

        [Test]
        public void TheScenarioFromTheBrief()
        {
            // "the newest has 36% because nobody liked it, but the one from three updates ago has
            // 40%" — the exact shape the author asked for, produced rather than hard-coded.
            var line = new ReleaseLine();

            line.Publish("v1", GameDate.Start, 34.0, 18.0, 10_000.0);
            line.Publish("v2", GameDate.Start, 52.0, 18.0, 10_000.0);
            Days(line, 30);

            line.Publish("v3", GameDate.Start, 44.0, 22.0, 10_000.0);
            Days(line, 20);

            line.Publish("v4", GameDate.Start, 41.0, 26.0, 10_000.0);
            Days(line, 20);

            var best = line.Versions[1];
            var newest = line.Newest;

            Assert.That(best.Adoption, Is.GreaterThan(newest.Adoption),
                $"v2 held {best.Adoption:P0} and the newest took {newest.Adoption:P0}. "
                + "An older version out-holding the newest is the case this exists for.");
        }

        /// <summary>
        /// A close race stays close.
        ///
        /// **This is the other half of `Decisiveness` and the half that is easy to lose.** Pushing
        /// the exponent up until every good update takes everybody would pass every other test in
        /// this fixture and quietly delete the mechanic: the list would read 100% / 0% forever and
        /// there would be nothing on the screen worth looking at. A version that is only slightly
        /// better has to leave a real minority behind.
        /// </summary>
        [Test]
        public void AVersionThatIsOnlySlightlyBetterSplitsTheAudience()
        {
            var line = Started(44.0, 20.0);
            var barelyBetter = line.Publish("v2", GameDate.Start, 47.0, 20.0, 10_000.0);

            Days(line, 120);

            var stayed = line.Versions[0];

            Assert.That(barelyBetter.Adoption, Is.GreaterThan(stayed.Adoption),
                "It is still the better version, so it still ends up ahead.");

            Assert.That(stayed.Adoption, Is.GreaterThan(0.2),
                "A marginal update must not empty the version it replaces. If it does, the list is "
                + "always 100/0 and this whole screen is decoration.");
        }

        [Test]
        public void PriceIsPartOfTheChoice()
        {
            var cheap = new ReleaseLine();
            cheap.Publish("v1", GameDate.Start, 40.0, 10.0, 10_000.0);
            var cheapUpdate = cheap.Publish("v2", GameDate.Start, 44.0, 10.0, 10_000.0);
            Days(cheap, 25);

            var dear = new ReleaseLine();
            dear.Publish("v1", GameDate.Start, 40.0, 10.0, 10_000.0);
            var dearUpdate = dear.Publish("v2", GameDate.Start, 44.0, 90.0, 10_000.0);
            Days(dear, 25);

            Assert.That(dearUpdate.Adoption, Is.LessThan(cheapUpdate.Adoption),
                "The same improvement at four times the price has to convince fewer people.");
        }

        [Test]
        public void WhatTheMarketSeesIsWhatUsersAreRunning()
        {
            var line = Started(50.0);
            line.Publish("v2", GameDate.Start, 20.0, 20.0, 10_000.0);

            var effective = line.EffectiveCapability();

            Assert.That(effective, Is.LessThan(50.0),
                "A quarter of the audience on a worse version has to drag the score down.");

            Assert.That(effective, Is.GreaterThan(20.0),
                "And the rest are still on the good one, so it cannot read as the worst version.");
        }

        [Test]
        public void TheListDoesNotGrowForever()
        {
            var line = Started();

            for (var index = 2; index < 20; index++)
            {
                line.Publish($"v{index}", GameDate.Start, 40.0 + index, 20.0, 10_000.0);
            }

            Assert.That(line.Count, Is.LessThanOrEqualTo(ReleaseLine.RetireAfterVersions),
                "Forty versions of one model is an archive, not a decision.");

            Assert.That(line.Versions.Sum(version => version.Adoption),
                Is.EqualTo(1.0).Within(1e-6),
                "Retiring a version must hand its users on, never drop them.");
        }

        [Test]
        public void PreviousNameIsBaseUntilSomethingShips()
        {
            var line = new ReleaseLine();
            Assert.That(line.PreviousName, Is.EqualTo("Base"));

            line.Publish("Aurora 1", GameDate.Start, 40.0, 20.0, 10_000.0);
            Assert.That(line.PreviousName, Is.EqualTo("Aurora 1"));
        }
    }
}
