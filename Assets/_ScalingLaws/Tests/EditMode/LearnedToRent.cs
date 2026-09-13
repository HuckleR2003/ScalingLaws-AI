using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A company that has learned how to take on the places it is about to move into.
    ///
    /// **The fixtures below this are about moving, not about the gate**, and they were all written
    /// before there was one. Rather than each of them asserting the premises research separately,
    /// they say plainly that the company has it, the way a player who researched it would.
    ///
    /// Same repair as the scale ceiling in July and the catalogue nesting in September: when a new
    /// rule breaks a scripted company, the question is whether the company could use the control. A
    /// player researches this in the first month; a fixture written in 2026-08 could not know to.
    /// <c>OfficeUnlockTests</c> is where the gate itself is tested.
    /// </summary>
    internal static class LearnedToRentExtensions
    {
        public static CompanySimulation LearnedToRent(this CompanySimulation simulation)
        {
            foreach (var node in ResearchTree.All)
            {
                if (node.Track == ResearchTrack.Premises)
                {
                    simulation.State.UnlockedResearch.Add(node.Id);
                }
            }

            return simulation;
        }
    }
}
