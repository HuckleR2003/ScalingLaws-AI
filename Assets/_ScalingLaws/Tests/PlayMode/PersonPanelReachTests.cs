using System.Collections;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Clicking a person opens the card about them.
    ///
    /// **Written because a playtest reported that it does not**, and because the two guards this
    /// project already has both pass on it: the panel renders correctly on its own in
    /// `ScreenProofTests`, and `UiWiringTests` sees `personPanel.Show` called from two places. What
    /// neither can see is whether a click actually arrives, which is the whole difference between a
    /// mechanism existing and a mechanism working.
    ///
    /// This drives the real button in the real shell. It is a PlayMode test because a panel is
    /// needed for an event to be dispatched at all: in EditMode nothing is ever delivered, which is
    /// exactly why the gap existed.
    /// </summary>
    public sealed class PersonPanelReachTests
    {
        [UnityTest]
        public IEnumerator ClickingSomebodyOnTheTeamPageOpensTheirCard()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);

            // Somebody to click on. Hiring through the simulation rather than through the screen,
            // because the hiring flow is not what is being tested here.
            var state = shell.Simulation.State;
            state.CashUsd = 50_000_000L;
            state.Staff.SetOffice(OfficeTier.Loft);

            state.Staff.Add(new Hire(StaffRole.ResearchScientist, 3, state.Date, "Ada Kowalska",
                PlayerSkill.Concept, HireSource.Agency, 100.0));

            Assert.Greater(state.Staff.Headcount, 0, "Nobody was hired, so there is nobody to click.");

            Assert.IsTrue(shell.OpenScreenByName("Team"), "The team screen would not open.");

            yield return null;

            var root = document.rootVisualElement;
            var rows = root.Query<Button>(className: "crew__row").ToList();

            Assert.IsNotEmpty(rows,
                "The team page has no crew rows, so there is nothing on it to click.");

            // **A submit rather than a synthetic ClickEvent, and the difference matters.** A
            // `Button` is driven by `Clickable`, which listens for pointer down and up; a bare
            // `ClickEvent` sent at it invokes nothing, so the first version of this test failed
            // against a perfectly working button. Submit runs the same `clicked` delegate a mouse
            // release does, which is the thing under test.
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = rows[0];
                rows[0].SendEvent(submit);
            }

            yield return null;

            var card = root.Query(className: "pp").ToList();

            Assert.IsNotEmpty(card,
                "Clicking a person on the team page put nothing on screen. The panel renders "
                + "correctly on its own and the call site exists, so what is missing is the click "
                + "arriving or the card being mounted.");
        }
    }
}
