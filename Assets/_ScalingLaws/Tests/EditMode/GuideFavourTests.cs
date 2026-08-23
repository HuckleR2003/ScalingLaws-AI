using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The favour Emil calls in: the company's first research node is on the house.
    ///
    /// **This is the seventh place in this project where a screen could have described a mechanic
    /// that did not exist.** A tutorial line saying "it is on me" over a node that charges normally
    /// is worse than no offer at all: the player is told they have been given something, watches
    /// their points go down, and learns that the game lies. Everything here checks the money and the
    /// points, not the dialogue.
    /// </summary>
    public sealed class GuideFavourTests
    {
        /// <summary>
        /// A company that can afford nothing, which is the state the favour exists for.
        ///
        /// Points are the real gate on research and a new lab has almost none, so a gift that only
        /// covered the cash would be no gift at all.
        /// </summary>
        private static CompanySimulation Broke()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 2_000_000;
            simulation.State.ResearchPoints = 0.0;
            return simulation;
        }

        private static ResearchNodeId AnEarlyNode(CompanySimulation simulation) =>
            ResearchTree.All
                .First(node => node.Id != ResearchTree.StartingNode
                    && node.IsAvailableOn(simulation.State.Date)
                    && node.Prerequisites.Count == 0)
                .Id;

        [Test]
        public void WithoutTheFavourANewCompanyCannotAffordAnything()
        {
            var simulation = Broke();

            Assert.That(simulation.TryStartResearch(AnEarlyNode(simulation), out var why), Is.False,
                "This test is worthless if a broke company could start research anyway.");

            Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void TheFavourPaysForBothCurrencies()
        {
            var simulation = Broke();
            simulation.State.Guide.FreeResearchOwed = true;

            var cashBefore = simulation.State.CashUsd;
            var pointsBefore = simulation.State.ResearchPoints;

            Assert.That(simulation.TryStartResearch(AnEarlyNode(simulation), out var why), Is.True, why);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cashBefore),
                "Not a penny. Covering only the points would still leave a bill the player was told "
                + "they would not get.");

            Assert.That(simulation.State.ResearchPoints, Is.EqualTo(pointsBefore).Within(1e-9));
            Assert.That(simulation.State.ActiveResearch, Is.Not.Null);
        }

        [Test]
        public void TheFavourIsSpentOnce()
        {
            var simulation = Broke();
            simulation.State.Guide.FreeResearchOwed = true;

            Assert.That(simulation.TryStartResearch(AnEarlyNode(simulation), out _), Is.True);

            Assert.That(simulation.State.Guide.FreeResearchOwed, Is.False,
                "One favour. Leaving the flag set would make every node in the campaign free.");
        }

        /// <summary>
        /// A promise the player has been made and has not spent survives a reload.
        ///
        /// Dropping it would take back something the tutorial already handed over, which is worse
        /// than never offering it. It is causal state rather than a record: the next programme reads
        /// it and decides whether to charge.
        /// </summary>
        [Test]
        public void AnUnspentFavourSurvivesASave()
        {
            var simulation = Broke();
            simulation.State.Guide.FreeResearchOwed = true;

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.That(restored.Guide.FreeResearchOwed, Is.True);
        }

        [Test]
        public void ASpentFavourDoesNotComeBackOnReload()
        {
            var simulation = Broke();
            simulation.State.Guide.FreeResearchOwed = true;
            simulation.TryStartResearch(AnEarlyNode(simulation), out _);

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.That(restored.Guide.FreeResearchOwed, Is.False,
                "Reloading after spending it would hand the player a second one.");
        }

        [Test]
        public void ACampaignFromBeforeTheFavourIsOwedNothing()
        {
            var data = SaveStore.Capture(Broke().State);
            data.version = 36;
            data.guideFreeResearchOwed = true;

            var upgraded = SaveMigration.UpgradeV36ToV37(data);

            Assert.That(upgraded.version, Is.EqualTo(37));

            Assert.That(upgraded.guideFreeResearchOwed, Is.False,
                "The favour did not exist in v36, so a fifteen year old company cannot have been "
                + "promised one. Granting it would hand an established lab a gift meant for its "
                + "first week.");
        }

        /// <summary>
        /// The node the favour will pay for reads as startable rather than as blocked.
        ///
        /// The research screen greys out anything the company cannot afford, so without this the
        /// tutorial would say "pick anything, it is on me" over a tree with nothing clickable in it.
        /// </summary>
        [Test]
        public void TheScreenShowsTheGiftedNodeAsAvailable()
        {
            var simulation = Broke();
            var node = AnEarlyNode(simulation);

            bool CanStart() => simulation.ResearchBoard()
                .First(standing => standing.Node.Id == node).CanStart;

            Assert.That(CanStart(), Is.False);

            simulation.State.Guide.FreeResearchOwed = true;

            Assert.That(CanStart(), Is.True,
                "A node the favour is about to pay for must not be drawn as unaffordable.");
        }
    }
}
