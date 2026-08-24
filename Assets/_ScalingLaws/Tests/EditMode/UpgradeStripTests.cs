using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The corner says an upgrade is running, and says how many days are left.
    ///
    /// **This is the guard against the fault this project has now hit seven times**: a mechanism that
    /// is complete in the simulation, charges the money, advances every day, and has nothing in the
    /// interface to show for it. An upgrade was exactly that. The player clicked, the cash went, and
    /// the only evidence for weeks was the balance being smaller.
    /// </summary>
    public sealed class UpgradeStripTests
    {
        /// <summary>A company with one model actually on sale, which is the only state that can be
        /// upgraded. Same shape the simulation fixtures already use.</summary>
        private static CompanySimulation Selling()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 1234));
            simulation.SetRentedAccelerators(500);
            simulation.State.CashUsd = 900_000_000;

            simulation.TryStartTraining(
                new ModelBlueprint("Muse 1", ArchitectureId.DenseTransformer, 20, 400,
                    DatasetSource.WebCrawl), out _);

            simulation.Advance(40);

            Assert.That(simulation.State.Shelf, Is.Not.Empty, "The run never finished.");
            Assert.IsTrue(simulation.TryReleaseModel(0, 1.0, out var why), why);

            simulation.State.CashUsd = 900_000_000;
            return simulation;
        }

        private static string TextIn(VisualElement root, string className) =>
            string.Join(" ", root.Query<Label>(className: className).ToList().Select(l => l.text));

        [Test]
        public void NothingIsShownWhileNothingIsBeingUpgraded()
        {
            var simulation = Selling();
            var strip = new UpgradeStrip(() => simulation.State);
            strip.Refresh();

            Assert.That(strip.Root.style.display.value, Is.EqualTo(DisplayStyle.None),
                "An empty strip has to take no room, or the corner carries a blank slab forever.");
        }

        [Test]
        public void StartingAnUpgradePutsItInTheCornerWithTheDaysOnIt()
        {
            var simulation = Selling();
            var strip = new UpgradeStrip(() => simulation.State);

            Assert.IsTrue(
                simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out var refused),
                refused);

            strip.Refresh();

            Assert.That(strip.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "An upgrade is running and the corner is showing nothing.");

            var rows = strip.Root.Query(className: "ustrip__row").ToList();
            Assert.That(rows.Count, Is.EqualTo(1), "One programme, one row.");

            var project = simulation.State.UpgradeProjects[0];
            var days = TextIn(strip.Root, "ustrip__days");

            Assert.That(days, Does.Contain(project.DurationDays.ToString()),
                "Days left is the number a player plans around and it is what the strip is for. "
                + $"Expected {project.DurationDays} somewhere in \"{days}\".");

            Assert.That(TextIn(strip.Root, "ustrip__name"), Is.Not.Empty,
                "The row has to say which trait is being upgraded.");
        }

        [Test]
        public void TheDaysCountDownAsTheProgrammeRuns()
        {
            var simulation = Selling();
            var strip = new UpgradeStrip(() => simulation.State);

            Assert.IsTrue(
                simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out var refused), refused);

            strip.Refresh();
            var atStart = TextIn(strip.Root, "ustrip__days");

            for (var day = 0; day < 10; day++)
            {
                simulation.Advance(1);
            }

            strip.Refresh();

            Assert.That(TextIn(strip.Root, "ustrip__days"), Is.Not.EqualTo(atStart),
                "Ten days passed and the corner still reads the same number, so the strip is a "
                + "label rather than a readout.");
        }

        [Test]
        public void TwoProgrammesGetTwoRows()
        {
            var simulation = Selling();
            var strip = new UpgradeStrip(() => simulation.State);

            Assert.IsTrue(
                simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out var first), first);
            Assert.IsTrue(
                simulation.TryStartUpgrade(0, ModelTrait.Knowledge, out var second), second);

            strip.Refresh();

            Assert.That(strip.Root.Query(className: "ustrip__row").ToList().Count, Is.EqualTo(2),
                "A company can run two at once, and one line saying \"2 upgrades\" answers none of "
                + "the questions somebody staring at the corner is asking.");
        }

        /// <summary>
        /// Both languages, because the strip is three phrases and a missing one renders the key.
        /// </summary>
        [Test]
        public void TheStripReadsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var simulation = Selling();
                    var strip = new UpgradeStrip(() => simulation.State);

                    Assert.IsTrue(
                        simulation.TryStartUpgrade(0, ModelTrait.Reasoning, out var why), why);

                    strip.Refresh();

                    var kicker = TextIn(strip.Root, "ustrip__kicker");

                    Assert.That(kicker, Does.Not.Contain("ustrip."),
                        $"{language}: the strip is printing its own key.");
                    Assert.That(kicker, Is.Not.Empty);
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
