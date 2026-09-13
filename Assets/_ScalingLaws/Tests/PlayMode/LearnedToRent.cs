using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// A company that has learned how to take on the places it is about to move into.
    ///
    /// The EditMode assembly has the same helper and the reasoning is written out there. Copied
    /// rather than shared because the two test assemblies do not reference each other, and it is
    /// four lines whose only job is to say "this fixture is not about the gate".
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
