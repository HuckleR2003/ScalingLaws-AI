using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Whether a player can actually reach the deduplication pass, and whether what they pick
    /// survives the trip into the blueprint.
    ///
    /// **`DeduplicationTests` proves the mechanic works. It proves nothing about the screen.** It
    /// builds blueprints in code and hands them to the planner, which is precisely the blind spot
    /// that let the model type be split across the market, gated behind four research nodes, drawn
    /// in the demographic panel, and never passed by `CurrentBlueprint()`, so every model the
    /// player released was general forever with 244 tests green.
    ///
    /// So this fixture asks the two questions that one cannot: are the three cards on the stage,
    /// and does choosing one change the blueprint the screen would start.
    /// </summary>
    public sealed class DeduplicationReachTests
    {
        /// <summary>DATA is the fourth pip: branding, foundation, scale, **data**, compute, safety, review.</summary>
        private const int DataStage = 3;

        private static ModelCreatorPanel OnTheDataStage(out CompanySimulation simulation)
        {
            simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 80_000_000;
            simulation.SetRentedPetaflops(600.0);

            var panel = new ModelCreatorPanel(simulation) { Stage = DataStage };
            panel.Refresh();
            return panel;
        }

        private static string[] CardTitles(ModelCreatorPanel panel) =>
            panel.Root.Query<Button>(className: "choice-card").ToList()
                .SelectMany(card => card.Query<Label>().ToList())
                .Select(label => label.text)
                .ToArray();

        [Test]
        public void AllThreePassesAreOnTheDataStage()
        {
            var panel = OnTheDataStage(out _);
            var titles = CardTitles(panel);

            // The cards upper-case their own titles, so the comparison has to as well. Matching on
            // the catalogue's own casing failed here and read as a missing card.
            foreach (var definition in TrainingChoiceCatalog.AllPasses)
            {
                Assert.That(titles, Does.Contain(definition.DisplayName.ToUpperInvariant()),
                    $"{definition.Pass} has no card on the DATA stage, so no player can ever "
                    + "choose it however well the simulation handles it.");
            }
        }

        /// <summary>
        /// The check this project exists to keep making. A control that is drawn, clamped, priced
        /// and then dropped on the way to the blueprint looks entirely correct on screen.
        /// </summary>
        [Test]
        public void ChoosingAPassActuallyReachesTheBlueprint()
        {
            var panel = OnTheDataStage(out _);

            Assert.That(panel.PlannedBlueprint().Deduplication,
                Is.EqualTo(DeduplicationPass.Standard),
                "The untouched creator has to plan the neutral pass.");

            panel.ChooseDeduplication(DeduplicationPass.None);

            Assert.That(panel.PlannedBlueprint().Deduplication,
                Is.EqualTo(DeduplicationPass.None),
                "The card changed the field and the field never reached the run.");
        }

        /// <summary>
        /// The hard scrub is gated behind `CorpusDeduplication`. A gate the screen does not show is
        /// a card that refuses every click for no stated reason, which this project has shipped as
        /// a bug three times.
        /// </summary>
        [Test]
        public void TheScrubIsShutUntilItIsResearchedAndThenItOpens()
        {
            var panel = OnTheDataStage(out var simulation);

            var shut = panel.Root.Query<Button>(className: "choice-card--shut").ToList();
            Assert.That(shut, Is.Not.Empty,
                "Nothing on the DATA stage is locked, so the research node behind the hard scrub "
                + "is either not wired to the screen or no longer gates anything.");

            panel.ChooseDeduplication(DeduplicationPass.Aggressive);
            Assert.That(panel.PlannedBlueprint().Deduplication,
                Is.EqualTo(DeduplicationPass.Standard),
                "A pass the company has not researched must never reach a blueprint, whatever the "
                + "field says.");

            simulation.State.UnlockedResearch.Add(ResearchNodeId.CorpusDeduplication);
            panel.Refresh();
            panel.ChooseDeduplication(DeduplicationPass.Aggressive);

            Assert.That(panel.PlannedBlueprint().Deduplication,
                Is.EqualTo(DeduplicationPass.Aggressive),
                "The node is researched and the pass still does not reach the run.");
        }
    }
}
