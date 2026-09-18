using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A player's hands in the server room, for fixtures that need cards in the cabinets.
    ///
    /// **Since 2026-09-18 nothing puts a card in a cabinet but the player.** Fixtures that used to
    /// rely on the room filling itself do what a player does instead: click FIT until the room is
    /// full or the store is empty. Through the same call the cabinet window makes, so a fixture can
    /// never reach a state the interface cannot.
    /// </summary>
    public static class RoomHands
    {
        /// <summary>Fits every card in the store into whatever cabinet has room. Returns how many.</summary>
        public static int FitEverything(CompanySimulation simulation)
        {
            var hall = simulation.State.Hall;
            var fitted = 0;

            for (var row = 0; row < hall.Rows; row++)
            {
                for (var column = 0; column < hall.Columns; column++)
                {
                    while (simulation.TryFitCard(column, row, out _))
                    {
                        fitted++;
                    }
                }
            }

            return fitted;
        }
    }
}
