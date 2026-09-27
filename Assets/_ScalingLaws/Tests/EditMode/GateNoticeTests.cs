using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The reply to clicking something the company cannot use yet.
    ///
    /// **Every one of these controls was silent, and two of them could not even be clicked.** A card
    /// built with `SetEnabled(false)` dispatches no pointer event at all, so a player pressing
    /// AGGRESSIVE on a company that has not researched deduplication got nothing: no sentence, no
    /// sound, no way to tell a locked control from a broken one. These tests hold the repair from
    /// both ends, because the half that is easy to get wrong is the one nothing renders.
    /// </summary>
    public sealed class GateNoticeTests
    {
        private VisualElement host;

        [SetUp]
        public void Mount()
        {
            host = new VisualElement();
            GateNotice.Host = host;
            GateNotice.Wanted = null;
            GateNotice.Hide();
        }

        [TearDown]
        public void Unmount()
        {
            GateNotice.Hide();
            GateNotice.Host = null;
            GateNotice.Wanted = null;
        }

        /// <summary>Everything written on the notice, so a test can read what a player would.</summary>
        private static List<string> Words(VisualElement root)
        {
            var found = new List<string>();

            void Walk(VisualElement element)
            {
                if (element is Label label && !string.IsNullOrEmpty(label.text))
                {
                    found.Add(label.text);
                }

                foreach (var child in element.Children())
                {
                    Walk(child);
                }
            }

            if (root != null)
            {
                Walk(root);
            }

            return found;
        }

        [Test]
        public void ANodeTheCompanyDoesNotHaveIsNamedAndOffered()
        {
            var node = ResearchNodeId.CorpusDeduplication;

            GateNotice.NeedsResearch("AGGRESSIVE", new[] { node }, _ => false);

            Assert.IsNotNull(GateNotice.Frame, "the notice did not go up");

            var words = Words(GateNotice.Frame);

            Assert.That(words, Does.Contain(ResearchTree.Get(node).DisplayName),
                "the player is told the name of what is missing");
            Assert.That(words, Does.Contain(Loc.T("gate.needs_research")));

            var buttons = GateNotice.Frame.Query<Button>().ToList();

            Assert.That(buttons.Count, Is.EqualTo(2),
                "one way to the research and one way to close it, and nothing else");
        }

        /// <summary>
        /// A notice about a node the company already holds is worse than the silence it replaced.
        ///
        /// It reads as the game having lost track of what has been researched, which is the one
        /// thing a screen about research must never say.
        /// </summary>
        [Test]
        public void ANodeTheCompanyAlreadyHasRaisesNothing()
        {
            GateNotice.NeedsResearch("AGGRESSIVE",
                new[] { ResearchNodeId.CorpusDeduplication }, _ => true);

            Assert.IsNull(GateNotice.Frame);
        }

        /// <summary>
        /// **The button hands back what was asked for and decides nothing itself.**
        ///
        /// `UI/` may draw the game and may not know how to navigate it, which is why the notice
        /// carries a delegate rather than a reference to the shell.
        /// </summary>
        [Test]
        public void PressingTheButtonHandsTheNodesBackAndClosesTheNotice()
        {
            IReadOnlyList<ResearchNodeId> asked = null;
            GateNotice.Wanted = nodes => asked = nodes;

            GateNotice.NeedsResearch("FP8",
                new[] { ResearchNodeId.LowPrecisionTraining }, _ => false);

            var go = GateNotice.Frame.Query<Button>(className: "gate__go").First();
            Assert.IsNotNull(go, "there is no way to the research");

            // **An EditMode element has no panel, so a click sent to a button is never dispatched.**
            // `TakeTheOffer` is what that button calls and what a test can reach, which is the same
            // seam `ManagementScreen.ShowDesk` exists for.
            GateNotice.TakeTheOffer();

            Assert.IsNotNull(asked, "nothing was handed back");
            Assert.IsTrue(asked.Contains(ResearchNodeId.LowPrecisionTraining));
            Assert.IsNull(GateNotice.Frame, "the notice stayed up over the screen it sent us to");
        }

        /// <summary>
        /// A lock no research can lift still says so. The creator's calendar gates go through here.
        /// </summary>
        [Test]
        public void ARefusalWithNoResearchBehindItIsStillASentence()
        {
            GateNotice.Says("FP8", Loc.T("gate.silicon", "2024"));

            Assert.IsNotNull(GateNotice.Frame);
            Assert.That(Words(GateNotice.Frame), Does.Contain(Loc.T("gate.silicon", "2024")));

            Assert.That(GateNotice.Frame.Query<Button>(className: "gate__go").ToList(), Is.Empty,
                "there is nothing to research, so there is nothing to press");
        }

        /// <summary>
        /// The mark on the board is a decision, so it is saved.
        ///
        /// A player who clicks a locked precision card, presses GO TO RESEARCH and then quits for
        /// the night has said what they are going for. A mark that evaporates on load is a mark
        /// they have to find again from a screen that no longer explains why.
        /// </summary>
        [Test]
        public void WhatThePlayerAskedForSurvivesASaveAndIsDroppedOnceItIsDone()
        {
            var state = new CompanyState("Adco", 9u);
            state.WantedResearch.Add(ResearchNodeId.CorpusDeduplication);

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state))));

            Assert.IsTrue(back.WantedResearch.Contains(ResearchNodeId.CorpusDeduplication));

            // The same file, with the node finished in the meantime. Nothing should still be asking
            // for work the company has done.
            var data = SaveStore.Capture(state);
            data.unlockedResearch.Add((int)ResearchNodeId.CorpusDeduplication);

            var done = SaveStore.Restore(SaveStore.Parse(UnityEngine.JsonUtility.ToJson(data)));

            Assert.IsFalse(done.WantedResearch.Contains(ResearchNodeId.CorpusDeduplication));
        }

        /// <summary>
        /// v64 to v65: nothing is wanted, and the empty list is the only true reading.
        ///
        /// Reconstructing it from what the company has not researched would mark most of the board
        /// yellow on the first load, which is the opposite of what the mark is for.
        /// </summary>
        [Test]
        public void AnOlderCampaignAsksForNothingOnTheBoard()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 64;
            data.wantedResearch = null;

            var upgraded = SaveMigration.UpgradeV64ToV65(data);

            Assert.That(upgraded.version, Is.EqualTo(65));
            Assert.That(upgraded.wantedResearch, Is.Empty);
            StringAssert.Contains("v64 to v65", SaveMigration.LastMigrationNotes);
        }
    }
}
