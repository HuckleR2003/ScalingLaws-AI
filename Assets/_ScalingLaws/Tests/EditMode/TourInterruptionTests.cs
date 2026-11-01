using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Nothing else telephones while the opening conversation is still going.
    ///
    /// **This is the ratchet for the worst fault of 2026-10-25.** The cousin rang about the support
    /// desk in the middle of the tour. The call offers a walkthrough, taking it replaces the tour,
    /// and the tour never came back: the player was left part way through a conversation that was
    /// still explaining how to name a version of a model they had already released.
    ///
    /// The doc comment on `RingAboutTheDesk` already said a call is skipped while his own phone is
    /// up, for precisely this reason. Checking the phone and not the tour was the whole hole, and
    /// it is the same conversation by a different route.
    /// </summary>
    public sealed class TourInterruptionTests
    {
        [Test]
        public void EveryStageBeforeTheEndCountsAsStillRunning()
        {
            var progress = new GuideProgress();

            foreach (var stage in new[]
                     {
                         GuideStage.Unseen, GuideStage.Talking,
                         GuideStage.Touring, GuideStage.Paused
                     })
            {
                progress.Stage = stage;

                Assert.That(progress.IsStillRunning, Is.True,
                    stage + " does not count as the tour still running, so the cousin can ring "
                    + "about something else over the top of it and replace it with a walkthrough "
                    + "that never hands back.");
            }
        }

        /// <summary>
        /// **And it has to end**, or nothing else in the game ever telephones again.
        /// </summary>
        [Test]
        public void AFinishedTourLetsEverythingElseRingAgain()
        {
            var progress = new GuideProgress { Stage = GuideStage.Finished };

            Assert.That(progress.IsStillRunning, Is.False,
                "A finished tour still blocks the phone, so the support desk, a renewal and the "
                + "lawyers are all silent for the rest of the campaign.");
        }

        /// <summary>
        /// The chain, as far as a fixture without a scene can follow it.
        ///
        /// **`GameShell` is a MonoBehaviour and the ring site is private**, so no EditMode test can
        /// press it. What can be checked is that every call offering a walkthrough sits in a method
        /// that consults the gate, which is the fault's actual shape: the rule existed in the doc
        /// comment and the code asked a different question. A second ring site added tomorrow
        /// fails this rather than shipping the same bug again.
        /// </summary>
        [Test]
        public void EveryCallThatOffersAWalkthroughChecksTheTourFirst()
        {
            var shell = Path.Combine(Application.dataPath,
                "_ScalingLaws", "Scripts", "UI", "GameShell.cs");

            Assert.That(File.Exists(shell), Is.True, "GameShell.cs has moved: " + shell);

            var source = File.ReadAllText(shell);

            // Every method body, so the search for the call and the search for the gate are inside
            // the same one rather than anywhere in a six thousand line file.
            var methods = Regex.Matches(source,
                @"private\s+void\s+(Ring\w+)\s*\([^)]*\)\s*\{(.*?)\n        \}",
                RegexOptions.Singleline);

            var offering = 0;

            foreach (Match method in methods)
            {
                var body = method.Groups[2].Value;

                if (!body.Contains("RingAbout("))
                {
                    continue;
                }

                offering++;

                Assert.That(body, Does.Contain("TheOpeningTourIsStillRunning"),
                    method.Groups[1].Value + " offers a walkthrough and never asks whether the "
                    + "opening tour is still running. Taking that walkthrough replaces the tour, "
                    + "and the tour does not come back.");
            }

            Assert.That(offering, Is.GreaterThan(0),
                "No method in GameShell was found offering a walkthrough, so this test is "
                + "measuring nothing. The pattern it searches for has probably been renamed.");
        }
    }
}
