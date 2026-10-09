using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The model that just finished training can be reached from the screen that improves models.
    ///
    /// **Reported by Natalia.** With one model on sale and a second finished and waiting, UPGRADE
    /// offered only the first. The only way to reach the new one was to release it, which is exactly
    /// the decision the upgrade work is supposed to inform.
    ///
    /// `ShelfUpgradeTests` already held that the simulation can do this: `TryStartUpgrades` has taken
    /// an `onShelf` argument since it was written and there is a fixture driving it. Nothing in
    /// `UI/` ever passed it. That is the twelfth time in this project a finished mechanism has had no
    /// control on top of it, and it is why the sweep exists.
    /// </summary>
    public sealed class UpgradeReachTests
    {
        private static CompanySimulation Trading()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 400_000_000L;
            simulation.SetRentedPetaflops(400.0);
            return simulation;
        }

        /// <summary>One model on sale, one finished and not released. The state Natalia described.</summary>
        private static CompanySimulation OneSellingOneWaiting()
        {
            var simulation = Trading();

            simulation.State.AddDeployedModel(new DeployedModel("Atlas",
                ArchitectureId.DenseTransformer, 40.0, simulation.State.Date, 2e10, 1.0,
                ModelType.General, "Atlas"));

            simulation.State.AddToShelf(new TrainedModel("Pebble",
                ArchitectureId.DenseTransformer, 46.0, simulation.State.Date, 2e10, 46.0));

            return simulation;
        }

        [Test]
        public void TheShelfIsOnTheListOfThingsThatCanBeImproved()
        {
            var simulation = OneSellingOneWaiting();
            var subjects = simulation.UpgradeSubjects();

            Assert.That(subjects.Select(s => s.Name), Is.EquivalentTo(new[] { "Atlas", "Pebble" }),
                "The model that finished training last is missing from the screen that improves "
                + "models, so the only way to reach it is to release it first.");

            var waiting = subjects.Single(s => s.Name == "Pebble");

            Assert.That(waiting.OnShelf, Is.True);
            Assert.That(waiting.Index, Is.EqualTo(0),
                "A shelved subject is numbered against the shelf, not against the deployed list. "
                + "An index without its flag addresses a different model.");

            Assert.That(subjects.Single(s => s.Name == "Atlas").OnShelf, Is.False);
        }

        /// <summary>
        /// The two lists are numbered separately, which is the one way this can go quietly wrong:
        /// commission against the wrong list and the work lands on a model the player did not pick.
        /// </summary>
        [Test]
        public void TheIndexOnASubjectAddressesTheListItsFlagNames()
        {
            var simulation = OneSellingOneWaiting();

            var waiting = simulation.UpgradeSubjects().Single(s => s.OnShelf);
            var trait = simulation.State.Shelf[waiting.Index].Traits
                .Standings(simulation.State.Date)
                .First(standing => standing.IsAvailable && !standing.IsMaxed)
                .Trait;

            Assert.That(
                simulation.TryStartUpgrades(waiting.Index, new[] { trait }, out var why,
                    onShelf: true),
                Is.True, why);

            Assert.That(simulation.State.IsUpgradeInFlight(waiting.Index, trait, onShelf: true),
                Is.True, "The programme was booked against the wrong list.");
        }

        /// <summary>
        /// A model on the shelf is losing ground while it waits, and the screen has to say so rather
        /// than quoting what it measured on the day it finished.
        /// </summary>
        [Test]
        public void AWaitingModelIsQuotedAtWhatItIsWorthToday()
        {
            var simulation = OneSellingOneWaiting();
            var fresh = simulation.UpgradeSubjects().Single(s => s.OnShelf).Capability;

            for (var day = 0; day < 240; day++)
            {
                simulation.AdvanceDay();
            }

            var later = simulation.UpgradeSubjects().Single(s => s.OnShelf);

            Assert.That(later.Capability, Is.LessThan(fresh),
                "Eight months on the shelf cost this model nothing, so the screen is quoting the "
                + "figure from the day the run finished and telling the player that waiting is free.");

            Assert.That(later.DaysWaiting, Is.EqualTo(240));
        }

        /// <summary>
        /// And the panel itself offers both, because that is where the fault actually was.
        /// </summary>
        [Test]
        public void ThePickerOffersBoth()
        {
            var simulation = OneSellingOneWaiting();

            var picked = new List<string>();

            var panel = new UpgradeGridPanel(simulation,
                (_, _) => picked.Add("release"),
                () => { },
                (_, _) => picked.Add("shelf"));

            panel.Refresh();

            var field = panel.Root.Q<DropdownField>();
            Assert.That(field, Is.Not.Null, "The screen has no model picker.");

            Assert.That(field.choices, Has.Count.EqualTo(2),
                "The picker offers " + field.choices.Count + " of the two models the company has.");

            Assert.That(field.choices.Any(choice => choice.Contains("Pebble")), Is.True,
                "The model waiting to ship is not on the picker.");
        }

        /// <summary>
        /// **Every trait has to buy something, and the screen has to say what.**
        ///
        /// A tester asked what upgrading Speed does and there was no answer anywhere: the trait
        /// carried a description the whole time and no screen on the upgrade page drew it, and
        /// nothing at all said which of the three figures a level of it moves. Speed moves brand
        /// and optimisation and no capability, which the catalogue has always known.
        ///
        /// This is a design check wearing a test as well. A trait that moves none of the three is
        /// a chore with a price on it rather than a decision.
        /// </summary>
        [Test]
        public void EveryTraitSaysWhatALevelOfItBuys()
        {
            var nothing = Loc.T("upgrade.moves.nothing");

            foreach (var definition in ModelTraitCatalog.All)
            {
                var moves = UpgradeGridPanel.MovesPerLevel(definition);

                Assert.That(moves, Is.Not.Empty, definition.DisplayName);
                Assert.That(moves, Is.Not.EqualTo(nothing),
                    $"{definition.DisplayName} moves none of capability, brand or optimisation, "
                    + "so paying for a level of it buys nothing a player can read.");

                Assert.That(definition.Description, Is.Not.Empty,
                    $"{definition.DisplayName} has no description, and the tile card draws it.");
            }
        }

        /// <summary>The line is read off the trait, so it cannot drift from the numbers it quotes.</summary>
        [Test]
        public void TheEffectLineQuotesTheTraitsOwnNumbers()
        {
            var speed = ModelTraitCatalog.Get(ModelTrait.Latency);
            var moves = UpgradeGridPanel.MovesPerLevel(speed);

            Assert.That(moves, Does.Not.Contain(Loc.T("upgrade.moves.capability", string.Empty).Trim()),
                "Speed buys no capability, so the line must not offer any.");

            Assert.That(speed.BrandPerLevel, Is.GreaterThan(0.0));
            Assert.That(speed.EfficiencyPerLevel, Is.GreaterThan(0.0));
            Assert.That(moves, Does.Contain("%"),
                "brand and optimisation are shares, so the line has to carry the sign of one");
        }
    }
}
