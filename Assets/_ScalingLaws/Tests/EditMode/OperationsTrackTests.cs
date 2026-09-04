using System.Linq;
using System.IO;
using UnityEngine;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The Operations track: the first four nodes in this game that research the company.
    ///
    /// **Every test here asserts a number moved, not that a node exists.** A research node whose
    /// effect is not measured is a card with a price on it, and this project has shipped that exact
    /// thing: four research nodes handed over a corpus and an architecture family that nothing ever
    /// granted, with 531 tests green, because every test asked whether the node completed rather
    /// than what the company owned afterwards.
    ///
    /// The other half of the risk is the opposite one. `RoomUpgrades` is threaded through the hall,
    /// the pool and the cabinet panel as an optional argument, so a caller that forgets to pass it
    /// still compiles and quietly gives the player nothing. `TheRoomTheGameChargesForIsTheRoomThe
    /// PanelDraws` is the guard against that, and it goes through `CompanySimulation` rather than
    /// through `RoomUpgrades` directly.
    /// </summary>
    public sealed class OperationsTrackTests
    {
        private static readonly ResearchNodeId[] TheFour =
        {
            ResearchNodeId.RackTelemetry,
            ResearchNodeId.AirflowModelling,
            ResearchNodeId.LiquidLoops,
            ResearchNodeId.OwnSubstation
        };

        private static RoomUpgrades With(params ResearchNodeId[] held) =>
            RoomUpgrades.For(node => held.Contains(node));

        // ---- the track itself -------------------------------------------------------------------

        [Test]
        public void TheFourNodesAreOnTheOperationsTrackAndNothingElseIs()
        {
            foreach (var id in TheFour)
            {
                Assert.IsTrue(ResearchTree.TryGet(id, out var node), $"{id} is not in the tree.");

                Assert.AreEqual(ResearchTrack.Operations, node.Track,
                    $"{node.DisplayName} is not on the Operations track, so it draws in the "
                    + "capability line beside nodes about the model.");
            }

            var onTrack = ResearchTree.All
                .Where(node => node.Track == ResearchTrack.Operations)
                .Select(node => node.Id)
                .ToList();

            CollectionAssert.AreEquivalent(TheFour, onTrack,
                "A node joined the Operations track without joining this fixture, so whatever it "
                + "moves is unmeasured.");
        }

        /// <summary>
        /// All four are optional technology.
        ///
        /// They are worth a great deal to a company with a basement and worth nothing at all to one
        /// that rents, so the scripted operator in the balance suite has to skip them. Without this
        /// the five year campaign spends months of calendar on cooling for a room it never opened,
        /// and `PlayabilityTests` stops measuring the economy and starts measuring this.
        /// </summary>
        [Test]
        public void NoneOfThemIsWorthBuyingWithoutARoomAndTheTreeSaysSo()
        {
            foreach (var id in TheFour)
            {
                Assert.IsTrue(ResearchTree.Get(id).OptionalTechnology,
                    $"{ResearchTree.Get(id).DisplayName} is not marked optional, so the balance "
                    + "campaign will research it whether or not it owns a room.");
            }
        }

        /// <summary>
        /// The research screen sorts the Operations nodes into their own band.
        ///
        /// **The proof render cannot answer this one.** It frames the top of the page, which is era
        /// one, and all four of these sit in Scaling and Autonomy below the fold.
        ///
        /// What it defends against is the switch in `BuildResearchScreen` being simplified back to
        /// an if/else on `ModelImprovement`. Nothing would fail: the four nodes would quietly rejoin
        /// the capability line and read as research about the model, which is the one thing this
        /// track exists to say they are not.
        /// </summary>
        [Test]
        public void TheResearchScreenGivesTheTrackItsOwnBand()
        {
            var path = Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI",
                "GameShell.Research.cs");

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("ResearchTrack.Operations"),
                "The research screen never asks whether a node is on the Operations track, so the "
                + "four of them are drawn in the capability line beside nodes about the model.");

            Assert.That(source, Does.Contain("research.operations"),
                "The band has no heading of its own.");

            Assert.That(source, Does.Contain("deepening--ops"),
                "The band is not marked, so it draws identically to the model improvement band "
                + "above it and the two read as one row split for no reason.");
        }

        // ---- airflow modelling: wide and shallow -------------------------------------------------

        /// <summary>
        /// Airflow modelling buys throughput on every cabinet at once.
        ///
        /// Two identical floors, one company that has researched it and one that has not, and the
        /// only difference allowed is the research. A test that built one floor and asserted the
        /// number was above some figure would pass on a node that did nothing, because the figure
        /// would have been read off the same build.
        /// </summary>
        [Test]
        public void AirflowModellingRecoversThroughputOnEveryCabinetAtOnce()
        {
            var plain = Floor(ServerRack.Enclosed, 4);
            var modelled = Floor(ServerRack.Enclosed, 4);

            // **1.9 kW a card, and the figure is chosen rather than picked.** Eight slots makes
            // 15.2 kW against an enclosed cabinet's 14 kW rating, which is past the 5% of headroom
            // the throttle curve allows and inside the 15.96 kW that same cabinet allows once
            // airflow modelling has added its 1.2 kW. So the plain floor throttles and the
            // modelled one does not, which is the claim.
            //
            // Far over the rating both arms clamp at WorstThrottle and the test measures the
            // clamp. The first pass used 2.0 and failed on the second assertion for the honest
            // reason: the throughput came back and the cabinets were still fractionally over.
            var before = plain.Output(1.0, 1.9);
            var after = modelled.Output(1.0, 1.9, With(ResearchNodeId.AirflowModelling));

            Assert.Greater(after.Petaflops, before.Petaflops * 1.05,
                "Airflow modelling did not measurably recover throughput on a warm floor.");

            Assert.Less(after.ThrottledRacks, before.ThrottledRacks,
                "Cabinets are still reported as throttling, so the room reads as a problem the "
                + "player has already paid to fix.");

            // And it costs nothing to run. The whole shape of this node against the fan is that the
            // fan takes a slot and this takes only the research.
            Assert.AreEqual(before.DrawKilowatts, after.DrawKilowatts, 0.001,
                "Airflow modelling changed the power bill, which is the fan's trade, not this one's.");
        }

        // ---- liquid loops: immersion only ---------------------------------------------------------

        /// <summary>
        /// Liquid loops make the dearest cabinet the one that ages best, and touch nothing else.
        ///
        /// The immersion tank is the most expensive thing in the catalog and until now it aged on
        /// exactly the same curve as the four-post frame: past its rating, the same penalty. This is
        /// the reason to buy it, and the second assertion is the one that keeps it a reason rather
        /// than a general discount.
        /// </summary>
        [Test]
        public void LiquidLoopsHelpTheImmersionTankAndNothingElse()
        {
            var upgrades = With(ResearchNodeId.LiquidLoops);

            var tankBefore = Floor(ServerRack.Immersion, 1).Output(1.0, 4.0);
            var tankAfter = Floor(ServerRack.Immersion, 1).Output(1.0, 4.0, upgrades);

            Assert.Greater(tankAfter.Petaflops, tankBefore.Petaflops * 1.10,
                "An immersion tank over its rating throttles exactly as hard with liquid loops "
                + "fitted, so the node changes nothing on the one cabinet it is about.");

            var boxBefore = Floor(ServerRack.Enclosed, 1).Output(1.0, 2.0);
            var boxAfter = Floor(ServerRack.Enclosed, 1).Output(1.0, 2.0, upgrades);

            Assert.AreEqual(boxBefore.Petaflops, boxAfter.Petaflops, 0.0001,
                "Liquid loops helped an air cooled cabinet, which makes it a general discount "
                + "rather than the trade that justifies the price of a tank.");
        }

        // ---- the substation: the one cost that grows with the room ---------------------------------

        /// <summary>
        /// The substation cuts the power bill of a company that owns a room, through the books.
        ///
        /// Measured on `DailyOperatingCostUsd` rather than on `RoomUpgrades.TariffUsd`, because the
        /// tariff being right on the struct proves only that the struct is right. What has failed in
        /// this project is the step after that: the value existing and never travelling.
        /// </summary>
        [Test]
        public void TheSubstationCutsThePowerBillAndTheBooksShowIt()
        {
            var market = MarketModel.Evaluate(GameDate.FromCalendar(2024, 1, 1), 60.0);

            var plain = PoolWithABasement(out var plainHall);
            var wired = PoolWithABasement(out var wiredHall);

            var before = plain.BuildProfile(GameDate.FromCalendar(2024, 1, 1), market, plainHall);
            var after = wired.BuildProfile(GameDate.FromCalendar(2024, 1, 1), market, wiredHall,
                With(ResearchNodeId.OwnSubstation));

            Assert.Less(after.DailyOperatingCostUsd, before.DailyOperatingCostUsd,
                "The substation did not reach the daily bill, so it is a research card that "
                + "changes no number the player can see.");

            // It buys nothing else. A node that also raised throughput would be two nodes.
            Assert.AreEqual(before.RawPetaflops, after.RawPetaflops, 0.001);
            Assert.AreEqual(before.PowerDrawKilowatts, after.PowerDrawKilowatts, 0.001);
        }

        [Test]
        public void TheTwoTariffsAreRealRatesAndTheIndustrialOneIsCheaper()
        {
            Assert.Less(ComputePoolTariff.SubstationUsd, ComputePoolTariff.DomesticUsd,
                "An industrial connection costs more per unit than a household one, which would "
                + "make the node a punishment.");

            Assert.AreEqual(ComputePoolTariff.DomesticUsd, ComputePool.DomesticTariffUsd,
                "There are two domestic tariffs again, which is how a rate gets changed in one "
                + "place and quoted from the other.");
        }

        // ---- telemetry: the node that buys information ---------------------------------------------

        /// <summary>
        /// Rack telemetry puts the next card's reading on the panel, and without it the panel is
        /// silent.
        ///
        /// **Both halves matter.** Asserting only that the line appears would pass on a panel that
        /// showed it to everybody, which is a node the player pays for and already had.
        /// </summary>
        [Test]
        public void RackTelemetryPutsTheNextCardsReadingOnTheCabinetPanel()
        {
            var before = Loc.Current;

            try
            {
                Loc.Current = Language.English;

                var simulation = CompanyWithARoom();
                var panel = new RackEditorPanel(() => simulation, () => { });

                Assert.IsFalse(Reads(panel.Build(0, 0), Loc.T("rack.next_card")),
                    "A company that has not researched telemetry is already being told what the "
                    + "next card would do, so the node buys nothing.");

                simulation.State.UnlockedResearch.Add(ResearchNodeId.RackTelemetry);

                Assert.IsTrue(Reads(panel.Build(0, 0), Loc.T("rack.next_card")),
                    "Rack telemetry is researched and the cabinet panel says nothing, which is the "
                    + "eighth time a finished mechanism in this project had no way to reach the "
                    + "player.");
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// The room the game charges for is the room the panel draws.
        ///
        /// `CompanySimulation.Room` is the single reading of the tree, and everything else takes it
        /// as an argument. Two readings would let the cabinet panel promise cooling the books never
        /// gave, which is the disagreement-with-a-date-on-it this project keeps rediscovering.
        /// </summary>
        [Test]
        public void TheRoomTheGameChargesForIsTheRoomThePanelDraws()
        {
            var simulation = CompanyWithARoom();

            Assert.AreEqual(RoomUpgrades.None.ExtraCoolingKilowatts,
                simulation.Room.ExtraCoolingKilowatts, 0.0001);

            foreach (var id in TheFour)
            {
                simulation.State.UnlockedResearch.Add(id);
            }

            var room = simulation.Room;

            Assert.AreEqual(RoomUpgrades.AirflowCoolingKilowatts, room.ExtraCoolingKilowatts, 0.0001);
            Assert.AreEqual(RoomUpgrades.LiquidLoopPenalty, room.ImmersionThrottlePenalty, 0.0001);
            Assert.AreEqual(ComputePoolTariff.SubstationUsd, room.TariffUsd, 0.0001);
            Assert.IsTrue(room.ShowsTelemetry);
        }

        /// <summary>
        /// `default(RoomUpgrades)` never runs the constructor, so it is not a legal argument.
        ///
        /// This is the fault that shipped as a repeating `NullReferenceException` in `InsightTip`
        /// three days ago: a readonly struct that coalesced its nulls in the constructor, handed
        /// around as `default`. Here the same slip would be silent instead of loud - a zero tariff
        /// makes power free - so every parameter is `RoomUpgrades?` and this test says why.
        /// </summary>
        [Test]
        public void TheDefaultStructIsNotAUsableRoomAndNoneIs()
        {
            var wrong = default(RoomUpgrades);

            Assert.AreEqual(0.0, wrong.TariffUsd, 0.0001,
                "If this is ever non-zero the constructor is running on a default, and the "
                + "nullable parameters everywhere else can be simplified away.");

            Assert.AreEqual(ComputePoolTariff.DomesticUsd, RoomUpgrades.None.TariffUsd, 0.0001);
            Assert.AreEqual(ServerRackCatalog.ThrottlePenalty,
                RoomUpgrades.None.PenaltyFor(ServerRack.Immersion), 0.0001);
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>A floor of one kind of cabinet, filled to the last slot.</summary>
        private static ServerHall Floor(ServerRack rack, int squares)
        {
            var hall = new ServerHall(squares, 1);
            var slots = 0;

            for (var column = 0; column < squares; column++)
            {
                Assert.IsTrue(hall.TryPlace(column, 0, rack, out var why), why);
                slots += ServerRackCatalog.Get(rack).Slots;
            }

            hall.Stock(slots);
            return hall;
        }

        private static ComputePool PoolWithABasement(out ServerHall hall)
        {
            var pool = new ComputePool();

            pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 8,
                GameDate.FromCalendar(2023, 1, 1), 30_000, 0));

            hall = Floor(ServerRack.Enclosed, 2);
            return pool;
        }

        private static CompanySimulation CompanyWithARoom()
        {
            var simulation = new CompanySimulation(new CompanyState("Roomco", 88));
            simulation.State.CashUsd = 5_000_000L;

            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);
            Assert.IsFalse(simulation.State.Hall.At(0, 0).IsEmpty,
                "Opening the room left the first square empty, so there is no cabinet to open.");

            // Something in it, or the panel has nothing to predict about.
            simulation.State.Hall.Stock(6);
            return simulation;
        }

        private static bool Reads(VisualElement tree, string text) =>
            tree.Query<Label>().ToList().Any(label => label.text == text);
    }
}
