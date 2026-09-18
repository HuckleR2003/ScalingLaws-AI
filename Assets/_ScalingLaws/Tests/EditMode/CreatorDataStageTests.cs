using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The DATA page of the creator, from the author's playtest of 2026-09-18.
    ///
    /// Two reports: *"everything ticked reverts to the defaults when a day passes"* and *"changing the
    /// tokenizer quickly leaves the stage empty with no options"*. Both are about a page that is
    /// rebuilt under the player while they are using it, which is why these drive the real panel.
    /// </summary>
    public sealed class CreatorDataStageTests
    {
        private const int DataStage = 3;

        private static CompanySimulation Ready()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 5_000_000_000L;
            simulation.SetRentedPetaflops(4000.0);
            simulation.UnlockEveryResearchNode();
            return simulation;
        }

        [Test]
        public void TheCorporaTickedSurviveTheDayRollingOver()
        {
            var simulation = Ready();
            var panel = new ModelCreatorPanel(simulation) { Stage = DataStage };
            panel.Refresh();

            var toggles = panel.Root.Query<Toggle>().ToList();

            Assume.That(toggles.Count, Is.GreaterThan(2),
                "the data page offers fewer than three corpora, so this measures nothing");

            // Everything the other way round from how it opened.
            foreach (var toggle in toggles)
            {
                toggle.value = !toggle.value;
            }

            var chosen = toggles.Select(toggle => toggle.value).ToList();

            // The day rollover: the shell calls Refresh on the open creator every day.
            panel.Refresh();

            var after = panel.Root.Query<Toggle>().ToList().Select(toggle => toggle.value).ToList();

            Assert.That(after, Is.EqualTo(chosen), "A day passed and the corpora went back to the default.");
        }

        [Test]
        public void SwitchingTheTokenizerQuicklyNeverLeavesThePageEmpty()
        {
            var simulation = Ready();
            var panel = new ModelCreatorPanel(simulation) { Stage = DataStage };
            panel.Refresh();

            var step = typeof(ModelCreatorPanel).GetMethod("StepRung",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var adaptation = typeof(ModelCreatorPanel).GetField("blueprintAdaptation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var rebuild = typeof(ModelCreatorPanel).GetMethod("RepriceAndRebuild",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assume.That(step, Is.Not.Null);
            Assume.That(adaptation, Is.Not.Null);
            Assume.That(rebuild, Is.Not.Null);

            // Up the ladder and back, with the adaptation bar moved between every step, as fast as
            // the methods can be called: faster than any player.
            foreach (var by in new[] { 1, 1, 1, -1, 1, -1, -1, -1, 1, 1 })
            {
                step.Invoke(panel, new object[] { by });

                for (var level = 0; level < TokenizerCatalog.AdaptationLevels; level++)
                {
                    adaptation.SetValue(panel, level);
                    rebuild.Invoke(panel, null);
                }

                Assert.That(panel.Root.Q(className: "tok-field"), Is.Not.Null,
                    "The tokenizer field is gone from the page.");
                Assert.That(panel.Root.Query(className: "tok-step").ToList().Count,
                    Is.EqualTo(TokenizerCatalog.AdaptationLevels),
                    "The adaptation bar is gone from the page.");
            }
        }
    }
}
