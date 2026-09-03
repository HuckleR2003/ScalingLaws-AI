using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Two operations the fixtures need and the game deliberately does not offer.
    ///
    /// Both used to sit on <see cref="CompanySimulation"/> as public methods, and both had exactly
    /// one kind of caller: a test. A sweep for player-facing mechanisms with no control behind them
    /// reported them as gaps. They are not gaps. They were test scaffolding parked in the
    /// production API, where the next person to read that file would see two things a screen was
    /// apparently missing and go looking for the missing screen.
    ///
    /// <see cref="SetRentedAccelerators"/> is the one worth the explanation. Renting is denominated
    /// in petaflops on purpose: if it were a unit count, the day the clouds moved from one
    /// generation to the next the bill would change on its own with no decision made and no extra
    /// work getting done. A unit-count entry point is the same contract with the wrong units on it,
    /// and it must never acquire a slider. Here, where only a fixture can reach it, it cannot.
    ///
    /// Extension methods, so every existing call site reads exactly as it did before the move.
    /// </summary>
    public static class SimulationOperators
    {
        /// <summary>Rents in units of whatever the clouds are offering today. Fixtures only.</summary>
        public static void SetRentedAccelerators(this CompanySimulation simulation, int units) =>
            simulation.State.Pool.SetRentedAcceleratorEquivalent(
                units, simulation.Market.RentableGeneration);

        /// <summary>
        /// Commissions one trait as a programme of its own.
        ///
        /// The player commissions a basket, because the team does one job at a time and four
        /// separate programmes ran the same four weeks simultaneously. A fixture testing one trait
        /// wants one trait, which is what this is for.
        /// </summary>
        public static bool TryStartUpgrade(this CompanySimulation simulation, int modelIndex,
            ModelTrait trait, out string failureReason, bool onShelf = false) =>
            simulation.TryStartUpgrades(modelIndex, new[] { trait }, out failureReason, onShelf);
    }
}
