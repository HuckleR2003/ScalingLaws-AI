using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The faults the author found in the second long playtest, each with the assertion that would
    /// have caught it.
    ///
    /// Every one of these shipped past a green suite, so the fixture is grouped by report rather
    /// than by class: what a player said happened, and the smallest thing that proves it cannot.
    /// </summary>
    public sealed class PlaytestFixesTests
    {
        private static CompanySimulation WithALiveModel(long cash = 400_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 7));
            simulation.State.CashUsd = cash;
            simulation.SetRentedPetaflops(600.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, capability: 40.0,
                releaseDate: simulation.State.Date, activeParameterCount: 8.0,
                priceMultiplier: 1.0));

            return simulation;
        }

        /// <summary>Every trait a fresh company is actually allowed to commission.</summary>
        private static List<ModelTrait> AffordableTraits(CompanySimulation simulation, int wanted)
        {
            var picked = new List<ModelTrait>();

            foreach (var trait in ModelTraitCatalog.All.Select(entry => entry.Trait))
            {
                var definition = ModelTraitCatalog.Get(trait);

                if (!definition.IsAvailableOn(simulation.State.Date))
                {
                    continue;
                }

                if (!simulation.State.HasResearch(ResearchTree.GateForTrait(trait)))
                {
                    continue;
                }

                picked.Add(trait);

                if (picked.Count == wanted)
                {
                    break;
                }
            }

            return picked;
        }

        // ---- "several UPGRADE IN PROGRESS rows, all counting the same days" ----------------------

        /// <summary>
        /// A basket is one programme, and its days are the sum of the work in it.
        ///
        /// **Reported: picking four cards produced four programmes.** Each advanced its own calendar
        /// by a day every day, so all four ran the same weeks simultaneously, finished together, and
        /// filled the mail with four separate "ships" messages. Worse, a programme could complete
        /// inside another programme it was supposed to follow.
        /// </summary>
        [Test]
        public void AWholeBasketIsCommissionedAsOneProgramme()
        {
            var simulation = WithALiveModel();
            var traits = AffordableTraits(simulation, 3);

            Assume.That(traits.Count, Is.EqualTo(3), "This fixture needs three commissionable traits.");

            Assert.IsTrue(simulation.TryStartUpgrades(0, traits, out var why), why);

            Assert.That(simulation.State.UpgradeProjects.Count, Is.EqualTo(1),
                "Three cards must be one job, not three racing each other on the same calendar.");

            var project = simulation.State.UpgradeProjects[0];

            Assert.That(project.Steps.Count, Is.EqualTo(3));
            Assert.IsTrue(project.IsBatch);

            foreach (var trait in traits)
            {
                Assert.IsTrue(project.Covers(trait), $"{trait} was picked and is not in the programme.");
            }
        }

        /// <summary>
        /// Three traits take longer than one of them, and it is the sum rather than the longest.
        ///
        /// The old screen quoted the longest card, on the theory that the cluster ran them side by
        /// side. It did, and that was the bug.
        /// </summary>
        [Test]
        public void ThreeImprovementsTakeLongerThanOne()
        {
            var one = WithALiveModel();
            var many = WithALiveModel();

            var traits = AffordableTraits(one, 3);
            Assume.That(traits.Count, Is.EqualTo(3));

            Assert.IsTrue(one.TryStartUpgrades(0, new[] { traits[0] }, out _));
            Assert.IsTrue(many.TryStartUpgrades(0, traits, out _));

            var alone = one.State.UpgradeProjects[0].DurationDays;
            var together = many.State.UpgradeProjects[0].DurationDays;

            Assert.That(together, Is.GreaterThan(alone),
                $"One trait took {alone} days and three took {together}. One team does one job "
                + "after another, so the days add up.");
        }

        /// <summary>
        /// The whole basket lands on one day, in one message.
        ///
        /// **The mail spam was the visible half of the fault and the smaller half.** The real damage
        /// was that four programmes on one model finished at four different moments while claiming
        /// to be one release.
        /// </summary>
        [Test]
        public void TheBasketLandsTogetherAndSaysSoOnce()
        {
            var simulation = WithALiveModel();
            var traits = AffordableTraits(simulation, 3);
            Assume.That(traits.Count, Is.EqualTo(3));

            var before = traits.ToDictionary(
                trait => trait,
                trait => simulation.State.DeployedModels[0].Traits.GetLevel(trait));

            Assert.IsTrue(simulation.TryStartUpgrades(0, traits, out _));

            var completions = 0;

            // Drained the way the shell drains it, because the queue is bounded and a long run would
            // otherwise drop the very messages this test is counting.
            for (var day = 0; day < 900 && simulation.State.UpgradeProjects.Count > 0; day++)
            {
                simulation.AdvanceDay();

                while (simulation.State.TryDequeueEvent(out var entry))
                {
                    if (entry.Type == CompanyEventType.UpgradeCompleted)
                    {
                        completions++;
                    }
                }
            }

            Assert.That(simulation.State.UpgradeProjects, Is.Empty, "The programme never finished.");

            Assert.That(completions, Is.EqualTo(1),
                $"{completions} completion messages for one programme.");

            foreach (var (trait, level) in before)
            {
                Assert.That(simulation.State.DeployedModels[0].Traits.GetLevel(trait),
                    Is.EqualTo(level + 1),
                    $"{trait} was in the basket and did not move.");
            }
        }

        /// <summary>A trait already inside a running batch cannot be commissioned again.</summary>
        [Test]
        public void ATraitInsideARunningBatchIsNotCommissionedTwice()
        {
            var simulation = WithALiveModel();
            var traits = AffordableTraits(simulation, 3);
            Assume.That(traits.Count, Is.EqualTo(3));

            Assert.IsTrue(simulation.TryStartUpgrades(0, traits, out _));

            // The second trait, which is not the programme's headline. Checking only `Trait` here
            // was how the guard would have missed it.
            Assert.IsFalse(simulation.TryStartUpgrades(0, new[] { traits[1] }, out var why));
            Assert.That(why, Is.Not.Empty);
        }

        /// <summary>Nothing is charged when any one trait in the basket is refused.</summary>
        [Test]
        public void APartlyImpossibleBasketTakesNoMoney()
        {
            var simulation = WithALiveModel();
            var traits = AffordableTraits(simulation, 2);
            Assume.That(traits.Count, Is.EqualTo(2));

            // A trait behind research the company does not have.
            var locked = ModelTraitCatalog.All
                .Select(entry => entry.Trait)
                .First(trait => !simulation.State.HasResearch(ResearchTree.GateForTrait(trait)));

            var basket = new List<ModelTrait>(traits) { locked };
            var before = simulation.State.CashUsd;

            Assert.IsFalse(simulation.TryStartUpgrades(0, basket, out var why));
            Assert.That(why, Is.Not.Empty);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before),
                "A partial commission would take the money for the traits it accepted and leave the "
                + "player to work out which of their picks vanished.");

            Assert.That(simulation.State.UpgradeProjects, Is.Empty);
        }

        // ---- "he rings back after three days and the tour starts from the beginning" -------------

        /// <summary>
        /// The favour is handed over by reaching the step, however the player got there.
        ///
        /// **Reported twice, and the second time it was still broken.** It was armed inside the
        /// branch of `GuideOverlay.Refresh` that rebuilds the strip, so whether the player was ever
        /// given it depended on whether that one step happened to trigger a rebuild. A checkpoint
        /// resume, a skip forward, or two steps advancing in one frame all walked straight past it,
        /// and the playtest reached the research screen still owing 278 points.
        /// </summary>
        [Test]
        public void TheFavourIsOwedOnceTheTourReachesTheStepThatOffersIt()
        {
            var guide = new GuideProgress();
            var gift = GuideScript.IndexOf(GuideScript.GiftStepId);

            Assert.That(gift, Is.GreaterThanOrEqualTo(0), "The gift step is not in the tour.");

            Assert.IsFalse(guide.GrantGiftsUpTo(gift - 1));
            Assert.IsFalse(guide.FreeResearchOwed, "Given before he offered it.");

            Assert.IsTrue(guide.GrantGiftsUpTo(gift));
            Assert.IsTrue(guide.FreeResearchOwed);
        }

        /// <summary>
        /// **Landing several steps past it still hands it over.** This is the case that was
        /// actually broken: nothing observed the exact step, so nothing armed the favour.
        /// </summary>
        [Test]
        public void SkippingPastTheOfferStillHandsTheFavourOver()
        {
            var guide = new GuideProgress();
            var gift = GuideScript.IndexOf(GuideScript.GiftStepId);

            Assert.IsTrue(guide.GrantGiftsUpTo(gift + 6));
            Assert.IsTrue(guide.FreeResearchOwed);
        }

        /// <summary>It is handed over once, and spending it does not bring it back.</summary>
        [Test]
        public void TheFavourIsNotHandedOverTwice()
        {
            var guide = new GuideProgress();
            var gift = GuideScript.IndexOf(GuideScript.GiftStepId);

            Assert.IsTrue(guide.GrantGiftsUpTo(gift));

            guide.FreeResearchOwed = false;

            Assert.IsFalse(guide.GrantGiftsUpTo(gift),
                "Spent is not the same as never given, and re-granting would be a second free node "
                + "every time the tour repainted.");

            Assert.IsFalse(guide.FreeResearchOwed);
        }

        /// <summary>A reloaded campaign that still owes the favour has obviously been given it.</summary>
        [Test]
        public void ALoadedCampaignThatStillOwesTheFavourCountsAsHavingBeenGivenIt()
        {
            var guide = new GuideProgress();
            guide.Restore(GuideStage.Touring, 20, 12_000_000, false, freeResearchOwed: true);

            Assert.IsTrue(guide.FavourGranted);
            Assert.IsFalse(guide.GrantGiftsUpTo(GuideScript.Steps.Count - 1));
        }

        /// <summary>The favour actually pays for the node, points and all.</summary>
        [Test]
        public void TheFavourWaivesThePointsThatWereBlockingTheNode()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 3));
            simulation.State.CashUsd = 20_000_000;
            simulation.SetRentedPetaflops(200.0);

            var node = ResearchTree.All
                .First(entry => entry.Id != ResearchTree.StartingNode
                    && entry.Prerequisites.All(need => need == ResearchTree.StartingNode));

            Assert.IsFalse(simulation.TryStartResearch(node.Id, out var blocked),
                "This fixture needs a node the company cannot afford in points.");

            Assert.That(blocked, Does.Contain("points"));

            simulation.State.Guide.GrantGiftsUpTo(GuideScript.IndexOf(GuideScript.GiftStepId));

            Assert.IsTrue(simulation.TryStartResearch(node.Id, out var why), why);
            Assert.IsNotNull(simulation.State.ActiveResearch);
            Assert.IsFalse(simulation.State.Guide.FreeResearchOwed, "It was spent.");
        }

        // ---- "the free allowance from 40k to 370k and nothing happened" --------------------------

        /// <summary>
        /// The allowance slider stops where generosity stops.
        ///
        /// **Reported as "nothing happened", and above 250k that was literally true.** `Generosity`
        /// clamps at `GenerousFreeTierTokensPerDay`, so every token past it bought no reach and was
        /// still served and still billed. A control whose top third costs money and changes nothing
        /// is worse than one that is not there.
        /// </summary>
        [Test]
        public void TheFreeAllowanceSliderStopsWhereItsEffectStops()
        {
            Assert.That(UI.ReleasePlanPanel.MaximumFreeTokens,
                Is.EqualTo((float)MonetizationCatalog.GenerousFreeTierTokensPerDay),
                "Travel past saturation is travel that only spends money.");
        }

        /// <summary>And moving it inside that range genuinely moves something.</summary>
        [Test]
        public void MovingTheAllowanceInsideTheRangeChangesTheReach()
        {
            var policy = new MonetizationPolicy { FreeTierTokensPerUserPerDay = 40_000 };
            var stingy = policy.ReachMultiplier;

            policy.FreeTierTokensPerUserPerDay = UI.ReleasePlanPanel.MaximumFreeTokens;
            var generous = policy.ReachMultiplier;

            Assert.That(generous, Is.GreaterThan(stingy),
                "The whole travel of the slider has to buy something or it is decoration.");
        }

        // ---- "when the tutorial finishes the phone disappears" -----------------------------------

        /// <summary>
        /// The handset is on screen exactly when the conversation is not.
        ///
        /// **A skipped tutorial used to be permanent.** "I'll take it from here" in the first minute
        /// removed the only character in the game with no route back to him, which is a dead end
        /// reached by one click. It is also where the messages are going to live.
        /// </summary>
        [Test]
        public void TheHandsetIsDockedOnceTheConversationIsOver()
        {
            var guide = new GuideProgress();
            var host = new UnityEngine.UIElements.VisualElement();

            var rings = 0;
            var dock = new UI.PhoneDock(host, () => guide, () => rings++);

            guide.Stage = GuideStage.Unseen;
            dock.Refresh();
            Assert.IsFalse(dock.IsShowing, "Nobody has been called yet.");

            guide.Stage = GuideStage.Touring;
            dock.Refresh();
            Assert.IsFalse(dock.IsShowing,
                "He is already on screen while he is talking, and two of him is one too many.");

            guide.Stage = GuideStage.Finished;
            dock.Refresh();
            Assert.IsTrue(dock.IsShowing, "Skipping the tour must not remove the way back to it.");

            guide.Stage = GuideStage.Paused;
            dock.Refresh();
            Assert.IsTrue(dock.IsShowing);

            Assert.That(rings, Is.Zero, "Showing the dock must not place a call by itself.");
        }

        /// <summary>Repainting does not rebuild it, which is what ate the tutorial's clicks once.</summary>
        [Test]
        public void TheHandsetIsNotRebuiltOnEveryRepaint()
        {
            var guide = new GuideProgress { Stage = GuideStage.Finished };
            var host = new UnityEngine.UIElements.VisualElement();

            var dock = new UI.PhoneDock(host, () => guide, () => { });

            dock.Refresh();
            var built = host[0];

            dock.Refresh();
            dock.Refresh();

            Assert.That(host.childCount, Is.EqualTo(1));
            Assert.That(host[0], Is.SameAs(built),
                "Rebuilding it every repaint restarts the rise and destroys the button between the "
                + "press and the release, which is exactly how the tour lost its NEXT button.");
        }
    }
}
