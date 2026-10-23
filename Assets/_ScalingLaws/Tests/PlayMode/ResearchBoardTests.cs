using System.Collections;
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
    /// Where the research board is looking when it opens.
    ///
    /// **Reported on 2026-10-17 as "research just sometimes opens at the bottom".** The board
    /// scrolls the page to the era band holding the node it means to show, which is right when
    /// the player pressed GO TO RESEARCH on a control that refused them, and wrong on every
    /// other visit. The node it picked was the one in flight, so a company with a long project
    /// running in a late era opened the board part-way down, every single time, and the
    /// "sometimes" in the report is the era: it only shows once the project is far enough along
    /// to sit below the fold.
    ///
    /// There is no assertion anywhere in this project that could have caught it. The board was
    /// built, the nodes were there, the marks were on the right ones, and the page was simply
    /// looking somewhere the player had not asked it to look.
    /// </summary>
    public sealed class ResearchBoardTests
    {
        /// <summary>
        /// Opening the board with a long project running leaves the page at its own top.
        ///
        /// The campaign is built the hard way rather than with a sandbox flag: everything in the
        /// tree is marked researched except one node in the last era of the model line, which is
        /// then started. That is the state the report came from, and the furthest the page could
        /// possibly be scrolled.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningTheBoardWithAProjectInFlightStartsAtTheTop()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var simulation = shell.Simulation;
            Assert.That(simulation, Is.Not.Null, "The shell has no campaign behind it.");

            // The tour mounts its phone on the panel root and it stays up across screen changes,
            // which is correct in the game and in the way of everything here.
            foreach (var ringing in Object.FindFirstObjectByType<UIDocument>()
                         .rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            var target = FurthestNode();

            foreach (var node in ResearchTree.All)
            {
                if (node.Id != target.Id)
                {
                    simulation.State.UnlockedResearch.Add(node.Id);
                }
            }

            // **And the calendar, which the first run of this fixture forgot.** The deepest
            // nodes carry a date as well as a prerequisite, so starting one in 2022 is refused
            // with "the field gets here around 2029-01-01" and the test then fails for a reason
            // that has nothing to do with scrolling. A ratchet that fails whatever the code does
            // is not a ratchet.
            simulation.State.Date = GameDate.FromCalendar(2032, 1, 1);
            simulation.State.CashUsd = 5_000_000_000;
            simulation.State.ResearchPoints = 500_000;

            Assert.That(simulation.TryStartResearch(target.Id, out var why), Is.True, why);

            Assert.That(shell.OpenScreenByName("Research"), Is.True, "No screen called Research.");

            // The board schedules its look-at a frame later and the scroller defers again on a
            // geometry event, so one frame proves nothing. Both of those have run by here.
            for (var frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            Assert.That(
                shell.PageScrollOffset.y,
                Is.EqualTo(0f).Within(1f),
                "The research board scrolled itself down on a plain visit. The page is only "
                + "allowed to move when the player asked for a particular node, which is what "
                + "GateNotice sets through CompanyState.WantedResearch. A project being in "
                + "flight is not asking.");
        }

        /// <summary>
        /// The node in the deepest era, which is as far down the board as the page can go.
        ///
        /// `ResearchNode` is a struct, so there is no null to track a running best with and the
        /// ordering does the work instead.
        /// </summary>
        private static ResearchNode FurthestNode() =>
            ResearchTree.All.OrderByDescending(node => node.Era).First();
    }
}
