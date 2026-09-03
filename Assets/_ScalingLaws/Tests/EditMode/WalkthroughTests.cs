using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The replayable tutorials, and the one thing that must never be true of them.
    ///
    /// **A walkthrough holds the bottom bar shut for its whole length.** That is what the author
    /// asked for and it is right: a three minute tour somebody wanders out of halfway is worse than
    /// no tour at all. It also means a step that can never be satisfied is not a cosmetic fault, it
    /// is a player locked inside a screen with no way back to the game. Every assertion here is
    /// pointed at that.
    /// </summary>
    public sealed class WalkthroughTests
    {
        private static string UiSource =>
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_ScalingLaws", "Scripts", "UI");

        /// <summary>Every file in the interface, joined, so a name can be looked for across it.</summary>
        private static string AllInterfaceCode()
        {
            var text = new System.Text.StringBuilder();

            foreach (var file in Directory.GetFiles(UiSource, "*.cs", SearchOption.AllDirectories))
            {
                text.Append(File.ReadAllText(file));
            }

            return text.ToString();
        }

        /// <summary>
        /// **The one that matters.** Every step that waits for the player is told about by the
        /// interface, by the step's own id.
        ///
        /// A waiting step in a walkthrough deliberately draws no NEXT button: the whole claim of the
        /// thing is that the player did what was asked, and a NEXT beside "click the cabinet" is a
        /// way to finish without ever clicking a cabinet. So the *only* way past such a step is a
        /// call to `GuideOverlay.Reached` with that id, and a step whose id appears nowhere is a dead
        /// end inside a locked interface.
        ///
        /// This is the ninth outing for the class of fault this project keeps hitting: a mechanism
        /// complete on both sides with nothing joining them. Here it would be complete, locked, and
        /// unescapable.
        /// </summary>
        [Test]
        public void EveryWaitingStepCanActuallyBeSatisfiedByTheInterface()
        {
            var code = AllInterfaceCode();
            var unreachable = new List<string>();

            foreach (var walkthrough in WalkthroughCatalog.All)
            {
                foreach (var step in walkthrough.Steps)
                {
                    if (!step.WaitForClick)
                    {
                        continue;
                    }

                    if (!code.Contains($"\"{step.Id}\""))
                    {
                        unreachable.Add($"{walkthrough.Id}/{step.Id}");
                    }
                }
            }

            CollectionAssert.IsEmpty(unreachable,
                "These steps wait for something no screen ever reports, and a walkthrough holds the "
                + "bottom bar shut, so reaching one locks the player in: "
                + string.Join(", ", unreachable));
        }

        /// <summary>
        /// A walkthrough never ends on a step that is waiting.
        ///
        /// The last step is the one that says it is over, and it has to be a step the player can
        /// finish by pressing the button. Ending on a waiting step means the run is completed by an
        /// action rather than by an acknowledgement, which reads as the tutorial having crashed.
        /// </summary>
        [Test]
        public void NoWalkthroughEndsOnAStepThatIsStillWaiting()
        {
            foreach (var walkthrough in WalkthroughCatalog.All)
            {
                Assert.Greater(walkthrough.Steps.Count, 0, walkthrough.Id);

                var last = walkthrough.Steps[walkthrough.Steps.Count - 1];

                Assert.IsFalse(last.WaitForClick,
                    $"{walkthrough.Id} ends on a step waiting for an action, so nothing tells the "
                    + "player it is over.");
            }
        }

        /// <summary>
        /// Every ring points at a class some screen actually puts on an element.
        ///
        /// A highlight class that exists nowhere rings nothing, reports nothing and looks exactly
        /// like a step that forgot to say what it meant. The unstyled-bars fault that shipped a whole
        /// screen once was this, in the other direction.
        /// </summary>
        [Test]
        public void EveryStepRingsSomethingTheInterfaceDraws()
        {
            var code = AllInterfaceCode();
            var missing = new List<string>();

            foreach (var walkthrough in WalkthroughCatalog.All)
            {
                foreach (var step in walkthrough.Steps)
                {
                    if (string.IsNullOrEmpty(step.Highlight))
                    {
                        continue;
                    }

                    if (!code.Contains($"\"{step.Highlight}\""))
                    {
                        missing.Add($"{walkthrough.Id}/{step.Id} -> {step.Highlight}");
                    }
                }
            }

            CollectionAssert.IsEmpty(missing,
                "These steps ring a class no screen adds: " + string.Join(", ", missing));
        }

        /// <summary>Ids are what the save records, so two of them must never collide.</summary>
        [Test]
        public void IdsAreUniqueAcrossEveryWalkthrough()
        {
            var seen = new HashSet<string>();

            foreach (var walkthrough in WalkthroughCatalog.All)
            {
                Assert.IsTrue(seen.Add(walkthrough.Id), $"Two walkthroughs share {walkthrough.Id}.");

                foreach (var step in walkthrough.Steps)
                {
                    Assert.IsTrue(seen.Add(step.Id), $"Two steps share {step.Id}.");
                }
            }

            Assert.IsTrue(WalkthroughCatalog.TryGet(WalkthroughCatalog.ServerRoomId, out var room));
            Assert.AreEqual(WalkthroughCatalog.ServerRoomId, room.Id);
            Assert.IsFalse(WalkthroughCatalog.TryGet("nothing_like_this", out _));
        }

        /// <summary>Both languages carry every phrase a walkthrough asks for.</summary>
        [Test]
        public void EveryLineIsWrittenInBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var walkthrough in WalkthroughCatalog.All)
                    {
                        Assert.IsNotEmpty(walkthrough.Title, walkthrough.Id);
                        Assert.IsFalse(walkthrough.Title.Contains("walk."),
                            $"{walkthrough.Id} title fell through to its key in {language}.");

                        Assert.IsFalse(walkthrough.Blurb.Contains("walk."),
                            $"{walkthrough.Id} blurb fell through to its key in {language}.");

                        foreach (var step in walkthrough.Steps)
                        {
                            Assert.IsFalse(step.Line.Contains("walk."),
                                $"{step.Id} fell through to its key in {language}: {step.Line}");
                        }
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// The offer appears once the tour is over, goes away when it is taken, and stays away.
        /// </summary>
        [Test]
        public void TheOfferArrivesAfterTheTourAndLeavesWhenItIsTaken()
        {
            var guide = new GuideProgress();

            guide.Stage = GuideStage.Touring;

            Assert.IsFalse(guide.IsOffering(WalkthroughCatalog.ServerRoomId),
                "Offered during the opening tour, so two tutorials compete for one corner.");

            guide.Stage = GuideStage.Finished;

            Assert.IsTrue(guide.IsOffering(WalkthroughCatalog.ServerRoomId));

            guide.WalkthroughsDone.Add(WalkthroughCatalog.ServerRoomId);

            Assert.IsFalse(guide.IsOffering(WalkthroughCatalog.ServerRoomId));
            Assert.IsTrue(guide.HasWalked(WalkthroughCatalog.ServerRoomId));

            // And waving it away is its own answer, distinct from finishing it.
            var second = new GuideProgress { Stage = GuideStage.Finished };
            second.WalkthroughsDismissed.Add(WalkthroughCatalog.ServerRoomId);

            Assert.IsFalse(second.IsOffering(WalkthroughCatalog.ServerRoomId));
            Assert.IsFalse(second.HasWalked(WalkthroughCatalog.ServerRoomId),
                "Waving the chip away marked the walkthrough as taken.");
        }

        /// <summary>
        /// What has been walked survives a save, or the chip comes back forever.
        /// </summary>
        [Test]
        public void WhatHasBeenWalkedSurvivesASave()
        {
            var state = new CompanyState("Adco", 9);

            state.Guide.Stage = GuideStage.Finished;
            state.Guide.WalkthroughsDone.Add(WalkthroughCatalog.ServerRoomId);
            state.Guide.WalkthroughsDismissed.Add("walk_something_else");

            var json = UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state));
            var restored = SaveStore.Restore(SaveStore.Parse(json));

            Assert.IsTrue(restored.Guide.HasWalked(WalkthroughCatalog.ServerRoomId),
                "The finished walkthrough was lost on load, so the chip offers it again forever.");

            Assert.IsFalse(restored.Guide.IsOffering(WalkthroughCatalog.ServerRoomId));

            CollectionAssert.Contains(restored.Guide.WalkthroughsDismissed, "walk_something_else");
        }

        /// <summary>A v46 file has taken none of them, because it was played without any.</summary>
        [Test]
        public void AnOlderSaveHasWalkedNothing()
        {
            var older = new SaveData { version = 46 };

            var upgraded = SaveMigration.UpgradeV46ToV47(older);

            Assert.AreEqual(47, upgraded.version);
            CollectionAssert.IsEmpty(upgraded.walkthroughsDone);
            CollectionAssert.IsEmpty(upgraded.walkthroughsDismissed);
        }

        /// <summary>
        /// The tour still says the number it promises, and it says it before the gift.
        ///
        /// The cap only means something as a temporary measure, so a player who hears the figure
        /// after being handed the basement has been given a rule with no reason attached to it.
        /// </summary>
        [Test]
        public void EmilCapsTheRentBeforeHeHandsOverTheBasement()
        {
            var cap = -1;
            var gift = -1;

            for (var index = 0; index < GuideScript.Steps.Count; index++)
            {
                if (GuideScript.Steps[index].Id == "compute_cap")
                {
                    cap = index;
                }

                if (GuideScript.Steps[index].Id == GuideScript.BasementStepId)
                {
                    gift = index;
                }
            }

            Assert.GreaterOrEqual(cap, 0, "The rent cap step is gone.");
            Assert.GreaterOrEqual(gift, 0);
            Assert.Less(cap, gift, "He gives the basement before he explains the cap it replaces.");

            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var line = GuideScript.Steps[cap].Line;

                    Assert.IsTrue(Regex.IsMatch(line, @"80"),
                        $"The cap line no longer says a number in {language}: {line}");
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }
    }
}
