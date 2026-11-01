using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Picking the trait your lab already brings, and the slot it used to cost you.
    ///
    /// **Reported as a question on 2026-10-27, not as a bug:** "if i have a founder trait and the
    /// lab i choose has the same trait, do they stack?". They do not. The founder keeps three
    /// traits, two picked and one from the lab, and `FounderProfile` discards a repeat, so a
    /// player who picked the lab's own trait finished the creator holding two.
    ///
    /// Nothing refused the click, nothing said anything, and the page had already told them they
    /// were choosing two on top of what the lab brings. The worst shape this fault takes: the
    /// control accepted the input, stored it, and the layer underneath threw it away.
    /// </summary>
    public sealed class FounderTraitStackTests
    {
        private static string MenuSource() =>
            File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "UI", "MainMenuController.cs"));

        /// <summary>
        /// A lab that brings a trait, and the trait it brings. Read from the catalogue so the
        /// fixture cannot drift from the roster.
        /// </summary>
        private static (CompanyArchetype Archetype, FounderTrait House) ALabWithATrait()
        {
            foreach (var identity in CompanyIdentityCatalog.All)
            {
                if (identity.HouseTrait != FounderTrait.None)
                {
                    return (identity.Archetype, identity.HouseTrait);
                }
            }

            Assert.Fail("No lab brings a trait, so this whole fixture is measuring nothing.");
            return default;
        }

        /// <summary>
        /// The reason the interface has to refuse. This is the behaviour underneath, it is correct
        /// on its own terms, and it is exactly why a duplicate must never be allowed to reach it.
        /// </summary>
        [Test]
        public void TheFounderDiscardsARepeatedTraitAndEndsUpWithOneFewer()
        {
            var (_, house) = ALabWithATrait();

            var other = FounderTraitCatalog.All
                .Select(definition => definition.Trait)
                .First(trait => trait != house);

            var distinct = new FounderProfile(other, house, house);

            Assert.That(distinct.Traits.Count, Is.EqualTo(2),
                "Two distinct traits and a repeat has to come out as two, or the repeat is "
                + "stacking and the whole question was moot.");
        }

        /// <summary>
        /// Three distinct traits is what the creator promises: two picked plus the lab's own.
        /// </summary>
        [Test]
        public void ThreeDistinctTraitsAllSurvive()
        {
            var traits = FounderTraitCatalog.All.Select(d => d.Trait).Take(3).ToArray();

            Assert.That(new FounderProfile(traits).Traits.Count, Is.EqualTo(3));
        }

        /// <summary>
        /// The rule itself, driven rather than read. Toggling a card rebuilds the whole creator,
        /// which needs a panel an EditMode test does not have, so the rule lives in a static of
        /// its own and the wiring is asserted separately below.
        /// </summary>
        [Test]
        public void TheLabsOwnTraitIsNotThePlayersToPick()
        {
            var (_, house) = ALabWithATrait();

            var rule = typeof(MainMenuController).GetMethod(
                "TraitIsTheirsToPick",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(rule, Is.Not.Null,
                "The creator has no stated rule about the lab's own trait.");

            Assert.That(rule.Invoke(null, new object[] { house, house }), Is.EqualTo(false),
                "The lab already brings this one, and picking it spends a slot on nothing.");

            var other = FounderTraitCatalog.All
                .Select(definition => definition.Trait)
                .First(trait => trait != house);

            Assert.That(rule.Invoke(null, new object[] { other, house }), Is.EqualTo(true),
                "Every other trait has to stay pickable, or the lab choice costs a slot instead "
                + "of granting one.");
        }

        /// <summary>
        /// A rule nothing calls is a rule the player never meets. `FounderNameGateTests` reads the
        /// source for the same reason: the handler cannot be driven without a panel, so the wiring
        /// between the handler and the rule is checked as text.
        /// </summary>
        [Test]
        public void TheToggleActuallyAsksTheRule()
        {
            var source = MenuSource();

            Assert.That(source, Does.Contain("if (!TraitIsTheirsToPick(trait, HouseTrait()))"),
                "ToggleTrait no longer asks the rule, so the cards are the only thing enforcing "
                + "it and a second way in would walk straight past.");
        }

        /// <summary>
        /// **The derived fault, and the one worth more than the original.** The lab is chosen on an
        /// earlier page than the traits, so a player can pick a trait, go back, and switch to the
        /// lab that brings it. Refusing the click on the traits page does nothing about that
        /// order, and the founder would discard the repeat exactly as before.
        /// </summary>
        [Test]
        public void ChangingTheLabGivesBackASlotSpentOnItsOwnTrait()
        {
            var source = MenuSource();

            Assert.That(source,
                Does.Contain("chosenTraits.Remove(CompanyIdentityCatalog.Get(archetype).HouseTrait);"),
                "Choosing a lab does not drop a pick that has just become a repeat, so going back "
                + "a page still costs the player a trait.");
        }

        /// <summary>
        /// The badge, because a card that refuses every click and says nothing reads as broken.
        /// This project has shipped that three times.
        /// </summary>
        [Test]
        public void TheCardSaysTheLabAlreadyBringsIt()
        {
            Assert.That(MenuSource(), Does.Contain("create.trait_from_lab"),
                "Nothing on the card explains why it cannot be picked.");

            Assert.That(Loc.English.ContainsKey("create.trait_from_lab"), Is.True,
                "The badge has no English text, so it renders as the key.");
            Assert.That(Loc.Polish.ContainsKey("create.trait_from_lab"), Is.True,
                "The badge has no Polish text, so it renders as the key on a Polish build.");
        }

        /// <summary>
        /// Every lab's house trait has to be a real catalogue entry: `FounderTraitCatalog.Get`
        /// throws on an unknown one, and the lab grid calls it while drawing.
        /// </summary>
        [Test]
        public void EveryHouseTraitTheGridDrawsIsARealTrait()
        {
            foreach (var identity in CompanyIdentityCatalog.Tiles())
            {
                Assert.That(FounderTraitCatalog.TryGet(identity.HouseTrait, out _), Is.True,
                    $"{identity.Archetype} brings {identity.HouseTrait}, which is not in the "
                    + "catalogue, and the lab card throws while drawing it.");
            }
        }
    }
}
