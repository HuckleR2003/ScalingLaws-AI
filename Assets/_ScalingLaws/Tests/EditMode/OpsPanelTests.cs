using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The operations band: what each tile says, and the one thing it must never do.
    ///
    /// **An EditMode test has no panel, so a click sent to a button is never dispatched.** These
    /// read the words that land in the tree and drive the state directly, the same seam
    /// `ManagementScreen.ShowDesk` and `PartsShop.Order` exist for. What a frame is needed for is
    /// whether it reads, and `ScreenProofTests.TheOperationsBandDrawsLockedAndOpen` renders all
    /// three states for exactly that.
    /// </summary>
    [TestFixture]
    public sealed class OpsPanelTests
    {
        private static CompanySimulation Company(double petaflops = 4_000.0)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2023, 9, 1),
                CashUsd = 400_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(petaflops);
            return simulation;
        }

        /// <summary>
        /// Every word on the band, buttons included.
        ///
        /// `TextElement` rather than `Label`, because `Button` is a `TextElement` and is not a
        /// `Label`: querying labels alone quietly misses every control on the card, which is the
        /// half these tests are mostly about.
        /// </summary>
        private static List<string> Words(VisualElement root) =>
            root.Query<TextElement>().ToList().Select(part => part.text ?? string.Empty).ToList();

        private static VisualElement Band(CompanySimulation simulation)
        {
            OpsPanel.Forget();
            return new OpsPanel(simulation, () => { }, _ => { }).Build();
        }

        /// <summary>
        /// A company that cannot count duplicates is told what counting them would show.
        ///
        /// An empty box reads as broken. A list of what is behind the door is an argument for
        /// opening it, and it is the state a new company lives in for its whole first year.
        /// </summary>
        [Test]
        public void TheLockedTileSaysWhatItWouldShowRatherThanNothing()
        {
            var words = Words(Band(Company()));

            Assert.That(words, Contains.Item(Loc.T("ops.nodata")),
                "A tile with nothing to show drew an empty box.");

            foreach (var promise in new[] { "ops.dupes.promise1", "ops.dupes.promise2",
                "ops.dupes.promise3" })
            {
                Assert.That(words, Contains.Item(Loc.T(promise)),
                    $"{promise} is not on the locked tile, so it does not say what it would show.");
            }

            Assert.That(words, Contains.Item(Loc.T("ops.show_me")),
                "The locked tile has no way through to the research that opens it.");
        }

        /// <summary>
        /// Knowing your own cluster is idle takes no technology, so that half is never gated.
        ///
        /// **This is the deliberate difference between the two tiles.** The duplicate figures need
        /// a node because counting duplicates genuinely needs one. An idle fleet is the company's
        /// own cluster doing nothing, it is the most expensive thing a player here fails to
        /// notice, and putting it behind a $2.1M node would hide the biggest number on the screen
        /// from exactly the company that most needs to see it.
        /// </summary>
        [Test]
        public void TheIdleReadingIsNeverGatedEvenThoughTheSellingIs()
        {
            var simulation = Company();
            for (var day = 0; day < 20; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.IsFalse(simulation.RunsAResaleDesk(), "This company is supposed to have no desk.");

            // **The second tile only**, because the first one is legitimately locked on this
            // company and the band therefore says "no data" somewhere whatever happens. Asserting
            // against the whole band would have been a test that could never pass.
            var capacity = Band(simulation).Query<OpsTile>().ToList()[1];
            var words = Words(capacity);

            Assert.That(words, Contains.Item(Loc.T("ops.idle.spare")),
                "A company with no resale desk cannot see how much of its fleet is idle.");

            Assert.That(words, Contains.Item(Loc.T("ops.idle.burning")),
                "It is not told what the idle share costs to hold, which is the whole point.");

            Assert.That(words, Contains.Item(Loc.T("ops.sell_it")),
                "There is no way through to the desk that would place it.");

            Assert.That(words, Has.No.Member(Loc.T("ops.nodata")),
                "The spare capacity tile is reporting no data. The reading is the company's own "
                + "cluster doing nothing and is never gated; only the selling is.");
        }

        /// <summary>
        /// A company that has done the research reads its own figures off the tile.
        /// </summary>
        [Test]
        public void TheOpenTilesPrintTheFiguresTheSimulationHolds()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.SpotCapacityDesk);
            simulation.State.CapacityResaleLevel = 3;

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var words = Words(Band(simulation));

            Assert.That(words, Contains.Item(UiFormat.Number(simulation.AccountsPerPerson().Free, 2)),
                "The free accounts per person on the tile is not the one the simulation holds.");

            Assert.That(words, Contains.Item(UiFormat.Money(simulation.CapacitySalesLastMonthUsd())),
                "The month of resale takings on the tile is not the one the books recorded.");

            Assert.That(words, Has.No.Member(Loc.T("ops.nodata")),
                "A company that has researched both is still being told there is no data.");
        }

        /// <summary>
        /// The ladder draws one block per rung, which is what a player counts.
        ///
        /// **Found by rendering it.** The bar fits segments to its width, so in a wide card it
        /// drew twenty five blocks for a ladder of six and the lit ones were not the level. It was
        /// right as a fraction and wrong as a count, and a ladder is a count.
        /// </summary>
        [Test]
        public void TheLadderDrawsOneBlockPerRung()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.SpotCapacityDesk);

            var bars = Band(simulation).Query<SegmentBar>().ToList();

            Assert.That(bars.Count, Is.EqualTo(2), "Both ladders should carry a bar.");

            var counts = bars.Select(bar => bar.Segments).OrderBy(value => value).ToList();

            Assert.That(counts, Is.EqualTo(new[]
            {
                AccountIntegrity.MostDetectionLevels, CapacityResale.MostLevels
            }.OrderBy(value => value).ToList()),
                "A bar is drawing a number of blocks that is not the number of rungs on its "
                + "ladder, so counting the lit ones gives a level nobody has.");
        }

        /// <summary>
        /// The drawer survives a rebuild, because the screen rebuilds every simulated day.
        ///
        /// At normal speed that is every second and a half. A drawer that closed each time would
        /// be a drawer nobody could use, which is the fault the research card and the insight tip
        /// were both fixed for.
        /// </summary>
        [Test]
        public void TheOpenLadderSurvivesTheDayRollingOver()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);

            OpsPanel.Forget();
            var panel = new OpsPanel(simulation, () => { }, _ => { });

            Assert.IsFalse(panel.Build().Query<OpsTile>().First().DrawerOpen,
                "The ladder is folded out before anybody asked for it.");

            OpsPanel.Show(0, "accounts", true);

            Assert.IsTrue(panel.Build().Query<OpsTile>().First().DrawerOpen,
                "The ladder was folded out and the next rebuild closed it again, which at normal "
                + "speed happens every second and a half.");

            OpsPanel.Show(0, "accounts", false);

            Assert.IsFalse(panel.Build().Query<OpsTile>().First().DrawerOpen,
                "Folding it away did not take.");

            OpsPanel.Forget();
        }
    }
}
