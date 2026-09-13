using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Moving is a decision before it is a bill.
    ///
    /// Rent is the largest recurring cost in the game and a move used to be a cheque and a click,
    /// so the one thing that can end a company quietly had nothing in front of it. One node per
    /// office, cheap and early, drawn beside the funding panel rather than in an era band.
    /// </summary>
    public sealed class OfficeUnlockTests
    {
        private static CompanySimulation Fresh()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 900_000_000L;
            return simulation;
        }

        [Test]
        public void ACompanyThatHasNotStudiedLeasingCannotRentOrBuy()
        {
            var simulation = Fresh();

            Assert.That(simulation.TryMoveOffice(OfficeTier.Loft, out var rentWhy), Is.False,
                "A company with no premises research moved in anyway.");

            Assert.That(rentWhy, Does.Contain(
                ResearchTree.Get(ResearchNodeId.LeaseASmallHub).DisplayName),
                "The refusal has to name the node, or 'needs research' over a tree of fifty nine "
                + "of them is not an instruction.");

            // **Both ways in, because there are two.** A rule enforced on one of them is not a
            // rule: a player with the cash would simply buy the place instead of renting it.
            Assert.That(simulation.TryBuyOffice(OfficeTier.Loft, out _), Is.False,
                "Buying walked straight past the gate that renting respects.");
        }

        [Test]
        public void TheResearchIsTheWholeDifference()
        {
            var simulation = Fresh();

            Assume.That(simulation.TryMoveOffice(OfficeTier.Loft, out _), Is.False);

            simulation.State.UnlockedResearch.Add(ResearchNodeId.LeaseASmallHub);

            Assert.That(simulation.TryMoveOffice(OfficeTier.Loft, out var why), Is.True, why);
        }

        /// <summary>
        /// The two lists have to grow together.
        ///
        /// The chooser offers whatever has a place built for it, and the gate reads its own table.
        /// An office added to one and not the other is either a place nobody can take or a move
        /// with nothing in front of it, and both are silent.
        /// </summary>
        [Test]
        public void EveryPlaceOnTheChooserHasAResearchBehindIt()
        {
            foreach (var place in OfficeCatalog.Places())
            {
                if (place.Tier == OfficeTier.Garage)
                {
                    // Where a campaign starts. Nobody researches their way into their own house.
                    continue;
                }

                Assert.That(OfficeUnlocks.IsGated(place.Tier), Is.True,
                    place.DisplayName + " can be moved into with no research behind it.");
            }
        }

        /// <summary>Every node the table names is a node that exists, on the premises track.</summary>
        [Test]
        public void TheNodesAreRealAndOnTheirOwnTrack()
        {
            foreach (OfficeTier tier in System.Enum.GetValues(typeof(OfficeTier)))
            {
                var id = OfficeUnlocks.RequiredFor(tier);

                if (id == ResearchNodeId.None)
                {
                    continue;
                }

                var node = ResearchTree.All.FirstOrDefault(entry => entry.Id == id);

                Assert.That(node.Id, Is.EqualTo(id), tier + " names a node the tree does not have.");

                Assert.That(node.Track, Is.EqualTo(ResearchTrack.Premises),
                    tier + " is gated on a node that is not a premises node, so it would be drawn "
                    + "in an era band rather than beside the funding panel.");
            }
        }

        /// <summary>
        /// The first one costs what the author asked for: a hundred points.
        ///
        /// Points and cash are derived from one figure by rule, so this is the check that the
        /// figure is still the one that lands on a round hundred.
        /// </summary>
        [Test]
        public void TheFirstOneIsAHundredPoints()
        {
            var node = ResearchTree.Get(ResearchNodeId.LeaseASmallHub);

            Assert.That(ResearchBudget.PointCostOf(node.CostUsd), Is.EqualTo(100.0));
            Assert.That(ResearchBudget.CashCostOf(node.CostUsd), Is.EqualTo(135_000L));
        }
    }
}
