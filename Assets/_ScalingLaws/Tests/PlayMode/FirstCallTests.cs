using System.Collections;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// **Reported by the author, 2026-09-18: "there is a good chance we open a tab straight away,
    /// and then the tutorial disappears."** Walking to another screen closes the phone, because it
    /// covers the creator's NEXT button, and it used to count as "call me back in three days" even
    /// on the first call nobody had answered.
    /// </summary>
    public sealed class FirstCallTests
    {
        [UnitySetUp]
        public IEnumerator OpenTheGame()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingAwayFromTheFirstCallKeepsTheTutorialAndHeRingsAgainInTheOffice()
        {
            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null);

            var guide = shell.Simulation.State.Guide;

            // The opening drive-in holds the phone back; wait for the ring rather than assume it.
            for (var frame = 0; frame < 600 && guide.Stage != GuideStage.Talking; frame++)
            {
                yield return null;
            }

            Assume.That(guide.Stage, Is.EqualTo(GuideStage.Talking), "The phone never rang.");

            shell.OpenScreenByName("Create");
            yield return null;

            Assert.That(guide.Stage, Is.Not.EqualTo(GuideStage.Paused),
                "Opening a tab during the first call put the tutorial off for three days.");
            Assert.That(guide.Stage, Is.Not.EqualTo(GuideStage.Talking),
                "The phone rang over the creator, where it covers NEXT.");

            shell.OpenScreenByName("Site");
            yield return null;
            yield return null;

            Assert.That(guide.Stage, Is.EqualTo(GuideStage.Talking),
                "Back in the office and the cousin did not ring again.");
        }
    }
}
