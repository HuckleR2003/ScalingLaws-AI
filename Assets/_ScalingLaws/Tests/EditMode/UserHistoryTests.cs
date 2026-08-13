using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The day by day trace the charts read, and the difference between an account and a person who
    /// is using the product right now.
    ///
    /// Registered is a stock the simulation records once a day. Online is a rate over that stock.
    /// Confusing the two is how a dashboard ends up claiming ten million people are typing at once,
    /// so the shape lives in the simulation where a test can hold it rather than in the panel.
    /// </summary>
    public sealed class UserHistoryTests
    {
        [Test]
        public void AFreshCompanyHasNoHistoryRatherThanAFlatLine()
        {
            var history = new UserHistory();

            Assert.AreEqual(0, history.Count);
            Assert.IsEmpty(history.Recent(15),
                "An empty chart is honest. A flat line at zero looks like a measurement.");
        }

        [Test]
        public void TheTraceReadsOldestFirstWhichIsHowAChartReads()
        {
            var history = new UserHistory();
            history.Record(10.0);
            history.Record(20.0);
            history.Record(30.0);

            var series = history.Recent(3);

            Assert.AreEqual(3, series.Count);
            Assert.AreEqual(10.0, series[0], 1e-9);
            Assert.AreEqual(30.0, series[2], 1e-9);
            Assert.AreEqual(30.0, history.Latest, 1e-9);
        }

        [Test]
        public void OnlyTheLastNinetyDaysAreKept()
        {
            var history = new UserHistory();

            for (var day = 0; day < UserHistory.DaysKept * 3; day++)
            {
                history.Record(day);
            }

            Assert.AreEqual(UserHistory.DaysKept, history.Count,
                "A fifteen year game would otherwise carry five and a half thousand numbers in every "
                + "save for a chart nobody scrolls.");

            var series = history.Recent(UserHistory.DaysKept);
            Assert.AreEqual(UserHistory.DaysKept * 3 - 1, series[^1], 1e-9,
                "The newest day has to survive the wrap.");
        }

        [Test]
        public void AskingForMoreDaysThanExistReturnsWhatThereIs()
        {
            var history = new UserHistory();
            history.Record(5.0);
            history.Record(6.0);

            Assert.AreEqual(2, history.Recent(50).Count);
            Assert.IsEmpty(history.Recent(0));
        }

        // ---- concurrency --------------------------------------------------------------------

        [Test]
        public void OnlyAFewPercentOfAccountsAreEverOnlineAtOnce()
        {
            var registered = 10_000_000.0;
            var busiest = Concurrency.OnlineAt(registered, 19.0);

            Assert.Less(busiest, registered * 0.06,
                $"{busiest:N0} of {registered:N0} online at the peak. A dashboard claiming most of "
                + "the account base is typing simultaneously is not describing a real service.");

            Assert.Greater(busiest, registered * 0.02,
                "And it cannot be so small that the number reads as nobody.");
        }

        [Test]
        public void TheNightIsQuietAndTheEveningIsBusy()
        {
            var registered = 1_000_000.0;

            var night = Concurrency.OnlineAt(registered, 3.0);
            var morning = Concurrency.OnlineAt(registered, 10.0);
            var evening = Concurrency.OnlineAt(registered, 19.0);

            Assert.Less(night, morning);
            Assert.Less(morning, evening);
        }

        [Test]
        public void TheRhythmWrapsCleanlyAroundMidnight()
        {
            var registered = 500_000.0;

            Assert.AreEqual(Concurrency.OnlineAt(registered, 2.0),
                Concurrency.OnlineAt(registered, 26.0), 1e-6);

            Assert.AreEqual(Concurrency.OnlineAt(registered, 2.0),
                Concurrency.OnlineAt(registered, -22.0), 1e-6,
                "An hour before midnight is an hour of the same day, not a negative one.");
        }

        [Test]
        public void NobodyRegisteredMeansNobodyOnlineAtAnyHour()
        {
            for (var hour = 0.0; hour < 24.0; hour += 1.0)
            {
                Assert.AreEqual(0.0, Concurrency.OnlineAt(0.0, hour), 1e-12, hour.ToString());
            }
        }

        [Test]
        public void NoAbsurdInputProducesANonsenseReading()
        {
            foreach (var registered in new[] { -5.0, double.NaN, double.PositiveInfinity })
            {
                var online = Concurrency.OnlineAt(registered, 12.0);
                Assert.IsFalse(double.IsNaN(online), registered.ToString());
                Assert.GreaterOrEqual(online, 0.0, registered.ToString());
            }

            Assert.IsFalse(double.IsNaN(Concurrency.OnlineAt(100.0, double.NaN)));
        }

        // ---- through the simulation -----------------------------------------------------------

        [Test]
        public void ACampaignFillsTheTraceAndItSurvivesASave()
        {
            var simulation = new CompanySimulation(new CompanyState("Traceco", 21));
            simulation.SetRentedPetaflops(40.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, 48.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            for (var day = 0; day < 200; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.AreEqual(UserHistory.DaysKept, simulation.State.Users.Count,
                "Two hundred days should have filled the ring.");

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(simulation.State.Users.Count, restored.Users.Count);
            Assert.AreEqual(simulation.State.Users.Latest, restored.Users.Latest, 1e-6,
                "A chart that empties on load is worse than no chart.");
        }

        [Test]
        public void AnOlderSaveStartsWithAnEmptyTraceRatherThanAnInventedOne()
        {
            var data = new SaveData { version = 19 };
            var upgraded = SaveMigration.UpgradeV19ToV20(data);

            Assert.AreEqual(20, upgraded.version);
            Assert.IsEmpty(upgraded.userHistory,
                "Back-filling a flat line would draw a company that had been steady for three months "
                + "when it may have doubled last week.");
        }

        /// <summary>
        /// The lab the author asked for: one that starts where the player starts and does not make it.
        /// </summary>
        [Test]
        public void ThereIsARivalStrugglingAlongsideThePlayerFromTheStart()
        {
            var earliest = int.MaxValue;
            var best = 0.0;
            var found = false;

            foreach (var release in CompetitorCatalog.All)
            {
                if (release.Competitor != CompetitorId.Groq)
                {
                    continue;
                }

                found = true;
                earliest = Math.Min(earliest, release.ReleaseDate.DayIndex);
                best = Math.Max(best, release.Capability);
            }

            Assert.IsTrue(found, "Groq has no releases, so the lab exists in name only.");

            Assert.Less(earliest, GameDate.FromCalendar(2022, 11, 30).DayIndex,
                "It has to be on the market before the first real launch, or it is not starting "
                + "alongside the player.");

            Assert.Less(best, 40.0,
                "It is supposed to be left behind. A struggling lab that reaches the frontier is "
                + "just another competitor.");
        }
    }
}
