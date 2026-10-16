using System.IO;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The opened cabinet and the floor tile describe the same box, so they have to read the same
    /// number.
    ///
    /// **Found 17 October 2026 while answering a tester who asked why his room said it was
    /// overheating while his cards ran at full speed.** That part turned out to be correct. What
    /// was not correct sat two methods away: `RackEditorPanel` computed a cabinet's heat as
    /// `cards * the fleet average`, while `ServerHall.HeatAt`, which colours the floor tile,
    /// counts the cards in that cabinet by generation. An accelerator draws between 0.30 kW
    /// (V100) and 2.20 kW (Next), so once a player fills a cabinet by hand with one generation
    /// the two readings part company, and the panel can print a comfortable bar over a cabinet
    /// the simulation is throttling.
    ///
    /// The doc comment on `ServerHall.HeatRatio` already said, in as many words, that two formulas
    /// for one quantity "would show as a green cabinet next to a panel saying it is throttling".
    /// It was written to prevent this and the panel had never adopted it. **A comment cannot hold
    /// a boundary. A test can.**
    /// </summary>
    public sealed class CabinetPanelReadsTheHallTests
    {
        private static readonly string PanelSource = Path.Combine(
            Application.dataPath, "_ScalingLaws", "Scripts", "UI", "RackEditorPanel.cs");

        private static CompanySimulation RoomWith(params (HardwareGenerationId Id, int Units)[] cards)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 4242));
            simulation.State.CashUsd = 50_000_000;
            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);

            foreach (var (id, units) in cards)
            {
                simulation.State.Pool.AddAsset(new HardwareAsset(
                    id, ComputeTier.ColocatedServers, units, simulation.State.Date, 10_000, 0));
            }

            return simulation;
        }

        /// <summary>
        /// The reading the panel now asks for is per generation, and provably not the average.
        ///
        /// A100 draws 0.40 kW and B200 draws 1.00 kW. With four of each owned the fleet average is
        /// 0.70, so a cabinet of four A100s is 1.6 kW and the average would have called it 2.8.
        /// </summary>
        [Test]
        public void AHandFilledCabinetReadsItsOwnCardsAndNotTheFleetAverage()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorA100, 4),
                (HardwareGenerationId.AcceleratorB200, 4));

            for (var card = 0; card < 4; card++)
            {
                Assert.IsTrue(simulation.TryFitCard(0, 0, HardwareGenerationId.AcceleratorA100,
                    out var why), why);
                Assert.IsTrue(simulation.TryFitCard(1, 0, HardwareGenerationId.AcceleratorB200,
                    out why), why);
            }

            var hall = simulation.State.Hall;
            var (_, averageKw) = simulation.HallPerAccelerator();

            Assert.IsTrue(HardwareCatalog.TryGet(HardwareGenerationId.AcceleratorA100, out var a100));
            Assert.IsTrue(HardwareCatalog.TryGet(HardwareGenerationId.AcceleratorB200, out var b200));
            Assert.That(a100.PowerKilowatts, Is.Not.EqualTo(b200.PowerKilowatts),
                "This fixture needs two generations that draw different power to mean anything.");

            var cool = hall.CabinetHeatKilowatts(0, 0, averageKw, simulation.Room);
            var hot = hall.CabinetHeatKilowatts(1, 0, averageKw, simulation.Room);

            Assert.That(cool, Is.EqualTo(4 * a100.PowerKilowatts).Within(0.0001),
                "The cabinet of A100s was not priced on the A100s in it.");
            Assert.That(hot, Is.EqualTo(4 * b200.PowerKilowatts).Within(0.0001),
                "The cabinet of B200s was not priced on the B200s in it.");

            // The whole point: the naive reading the panel used to take would have given both of
            // them the same number, and it is a number neither cabinet has.
            var naive = 4 * averageKw;
            Assert.That(cool, Is.Not.EqualTo(naive).Within(0.0001));
            Assert.That(hot, Is.Not.EqualTo(naive).Within(0.0001));
        }

        /// <summary>
        /// The floor tile's colour and the panel's bar run off one ratio.
        ///
        /// Asserting that the two cabinets simply differ is the weak version of this: any number of
        /// wrong readings differ. This pins the panel's inputs to the hall's own ratio.
        /// </summary>
        [Test]
        public void TheBarAndTheTileCannotDisagreeAboutOneCabinet()
        {
            var simulation = RoomWith((HardwareGenerationId.AcceleratorB200, 6));

            for (var card = 0; card < 6; card++)
            {
                Assert.IsTrue(simulation.TryFitCard(0, 0, HardwareGenerationId.AcceleratorB200,
                    out var why), why);
            }

            var hall = simulation.State.Hall;
            var (_, averageKw) = simulation.HallPerAccelerator();

            var heat = hall.CabinetHeatKilowatts(0, 0, averageKw, simulation.Room);
            var cooling = hall.CabinetCoolingKilowatts(0, 0, averageKw, simulation.Room);

            Assert.That(cooling, Is.GreaterThan(0.0), "A standing cabinet sheds something.");

            var fromParts = heat / cooling;
            var fromHall = hall.HeatRatio(0, 0, averageKw, simulation.Room);

            Assert.That(fromParts, Is.EqualTo(fromHall).Within(0.0001),
                "The panel builds its bar from heat over cooling and the tile asks HeatRatio. "
                + "Those two have to be the same division or one box gets two colours.");

            Assert.That(ServerRackCatalog.HeatOf(fromParts),
                Is.EqualTo(hall.HeatAt(0, 0, averageKw, simulation.Room)),
                "Same ratio, different state word.");
        }

        /// <summary>
        /// The source guard, and it is the one that actually catches the regression.
        ///
        /// The arithmetic above passes whatever the panel chooses to draw; only reading the panel
        /// catches somebody computing its heat inline again. The pattern is the exact shape of the
        /// fault: the number of cards in a cabinet multiplied by the fleet-wide average.
        /// </summary>
        [Test]
        public void ThePanelNeverComputesACabinetsHeatFromTheFleetAverage()
        {
            Assert.IsTrue(File.Exists(PanelSource), $"Cannot find {PanelSource}.");

            var source = File.ReadAllText(PanelSource);

            Assert.That(source, Does.Not.Contain("square.Accelerators * perCardKw"),
                "RackEditorPanel is pricing a cabinet's heat at the fleet average again. "
                + "Ask ServerHall.CabinetHeatKilowatts, which counts the cards in that cabinet.");

            Assert.That(source, Does.Contain("hall.CabinetHeatKilowatts("),
                "The panel has stopped asking the hall for the cabinet's heat.");

            Assert.That(source, Does.Contain("hall.CabinetCoolingKilowatts("),
                "The panel has stopped asking the hall for the cabinet's cooling.");
        }
    }
}
