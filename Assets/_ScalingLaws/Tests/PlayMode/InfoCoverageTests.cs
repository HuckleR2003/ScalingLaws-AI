using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Which screens explain themselves, counted rather than remembered.
    ///
    /// **This is the instrument, and it had to exist before the work.** The badges were built for
    /// the architecture screen, then for the model creator, and both times nobody walked the rest of
    /// the game to see what had been left behind. A missing explanation is invisible from the
    /// inside: the screen renders perfectly and the numbers are right, and only somebody who does
    /// not already know what a token is finds out that nothing on the page will tell them.
    ///
    /// Runs in PlayMode because the shell is a MonoBehaviour and the real scene is the only place it
    /// exists. One scene load, every screen counted.
    /// </summary>
    public sealed class InfoCoverageTests
    {
        /// <summary>
        /// What each screen owes a player who has never seen the words on it.
        ///
        /// Floors, not targets. Raising one is deliberate; a screen sliding under its floor in a
        /// refactor is the regression this file exists to catch.
        /// </summary>
        private static readonly (string Screen, int Least)[] Expected =
        {
            ("Business", 4),
            ("Funding", 3),
            ("Fleet", 3),
            ("Team", 3),
            ("Marketing", 3),
            ("Release", 2),
            ("Ranking", 2),
            ("Research", 2),
            ("Upgrade", 2),
            ("Management", 2),
            ("Family", 7),
        };

        [UnityTest]
        public IEnumerator EveryScreenThatCarriesJargonExplainsIt()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            TabProofCampaign.Furnish(shell.Simulation);
            Loc.Current = Language.Polish;

            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            var thin = new List<string>();
            var table = new List<string>();
            var blank = new List<string>();

            foreach (var (screen, least) in Expected)
            {
                Assert.IsTrue(shell.OpenScreenByName(screen), $"No screen called {screen}.");
                yield return null;

                var badges = document.rootVisualElement.Query<Button>(className: "infodot").ToList();
                table.Add($"{screen,-12} {badges.Count,3} / {least}");

                if (badges.Count < least)
                {
                    thin.Add($"{screen}: {badges.Count} of at least {least}");
                }

                // A badge that opens onto nothing is worse than no badge: the player clicks a
                // control that promises an explanation and gets a heading over empty space, which
                // reads as a broken game rather than as missing copy.
                foreach (var badge in badges)
                {
                    if (badge.userData is not InsightTip.Reading reading)
                    {
                        blank.Add($"{screen}: a badge carrying no reading at all");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(reading.What)
                        || string.IsNullOrWhiteSpace(reading.Affects))
                    {
                        blank.Add($"{screen}: \"{reading.What}\" / \"{reading.Affects}\"");
                    }
                }
            }

            Debug.Log("Info badges per screen:\n  " + string.Join("\n  ", table));

            Assert.IsEmpty(blank, "Badges that open onto nothing:\n  " + string.Join("\n  ", blank));
            Assert.IsEmpty(thin,
                "Screens carrying words a new player has never seen, with nothing on the page to "
                + "explain them:\n  " + string.Join("\n  ", thin));
        }
    }
}
