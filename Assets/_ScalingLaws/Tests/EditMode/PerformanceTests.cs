using System.Diagnostics;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// How long the game takes to think.
    ///
    /// There were no performance tests here until something made the tick slower: per model earnings
    /// and the rival dossiers both ask the segmented market for a full breakdown, which allocates. On
    /// its own that is nothing; the point of this fixture is that **nobody would have noticed the
    /// third or fourth thing to do the same**, and a tycoon that stutters at triple speed in year
    /// eleven is a tycoon nobody finishes.
    ///
    /// The bounds are deliberately loose. They are a ratchet against something becoming an order of
    /// magnitude worse, not a benchmark, and a loose bound that never gives a false failure is worth
    /// more than a tight one everybody learns to rerun. Times are measured on the build machine and
    /// the CI budget is generous.
    /// </summary>
    public sealed class PerformanceTests
    {
        /// <summary>A decade of simulated days, which is most of a campaign.</summary>
        private const int Decade = 3650;

        /// <summary>Slider drags. A player crossing the parameter slider produces a few hundred.</summary>
        private const int Drags = 600;

        private static CompanySimulation Busy(uint seed = 900)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(120.0);

            // A company with several lines, staff and a paid desk is the expensive case, not the
            // empty one. Measuring an idle company would measure nothing.
            simulation.State.AddDeployedModel(new DeployedModel("Atlas One",
                ArchitectureId.DenseTransformer, 46.0, simulation.State.Date, 2e10, 1.0,
                ModelType.General, "Atlas"));

            simulation.State.AddDeployedModel(new DeployedModel("Sonnet One",
                ArchitectureId.DenseTransformer, 44.0, simulation.State.Date, 1.5e10, 1.0,
                ModelType.Coding, "Sonnet"));

            simulation.SetIntelSubscription(IntelTier.KnownWords, true);
            simulation.SetIntelSubscription(IntelTier.TrendSearch, true);

            return simulation;
        }

        [Test]
        public void ADecadeOfDaysStaysWellUnderASecond()
        {
            var simulation = Busy();
            var clock = Stopwatch.StartNew();

            for (var day = 0; day < Decade; day++)
            {
                simulation.AdvanceDay();
            }

            clock.Stop();

            TestContext.WriteLine(
                $"{Decade} days in {clock.ElapsedMilliseconds} ms "
                + $"({clock.Elapsed.TotalMilliseconds / Decade:0.000} ms a day)");

            Assert.Less(clock.ElapsedMilliseconds, 4000L,
                $"Ten years took {clock.ElapsedMilliseconds} ms. At triple speed the tick has a few "
                + "milliseconds of frame to work in, so a day costing more than about one is a "
                + "stutter the player feels rather than a number in a log.");
        }

        /// <summary>
        /// The creator's hot path. Every movement of a slider reprices, and repricing runs the whole
        /// planner: the scaling law, the shape, the corpus blend and the bill.
        /// </summary>
        [Test]
        public void RepricingTheBlueprintIsCheapEnoughToRunOnEveryFrameOfADrag()
        {
            var simulation = Busy(901);
            var blueprint = new ModelBlueprint();

            // Warm the paths once so the measurement is not the first call's setup.
            simulation.Project(blueprint);

            var clock = Stopwatch.StartNew();
            for (var drag = 0; drag < Drags; drag++)
            {
                // A real drag changes the value every frame, so no cache on an unchanged blueprint
                // could rescue a slow projection.
                simulation.Project(blueprint.WithParameters(8.0 + drag * 0.05));
            }

            clock.Stop();

            var each = clock.Elapsed.TotalMilliseconds / Drags;
            TestContext.WriteLine($"{Drags} reprices in {clock.ElapsedMilliseconds} ms ({each:0.000} ms each)");

            Assert.Less(each, 1.0,
                $"A reprice costs {each:0.000} ms and it runs on every frame the player is dragging. "
                + "Past about a millisecond the slider stops following the cursor, which reads as the "
                + "game being broken rather than as the model being expensive.");
        }

        [Test]
        public void AskingTheMarketWhoHoldsWhomIsCheapEnoughToDoDaily()
        {
            var simulation = Busy(902);

            for (var day = 0; day < 400; day++)
            {
                simulation.AdvanceDay();
            }

            var clock = Stopwatch.StartNew();
            for (var call = 0; call < 2000; call++)
            {
                simulation.MarketByType();
            }

            clock.Stop();

            var each = clock.Elapsed.TotalMilliseconds / 2000.0;
            TestContext.WriteLine($"2000 breakdowns in {clock.ElapsedMilliseconds} ms ({each:0.000} ms each)");

            Assert.Less(each, 0.5,
                $"A breakdown costs {each:0.000} ms and the tick asks for one every day to credit "
                + "each model its earnings. The interface asks for several more per frame.");
        }
    }
}
