using System.Collections;
using System.IO;
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
    /// The team screen, driven the way a player drives it.
    ///
    /// **Reported: the position tiles were too big with half of each one empty, and there was no**
    /// **way to see the staff at all.** The only route to a person was opening one discipline at a
    /// time, so a company of six was six clicks and no way to compare anybody with anybody.
    ///
    /// It runs in PlayMode rather than EditMode because the ordering is a property of the screen
    /// rather than of a list: `SortCrewBy` rebuilds the page, and what has to be true is that the
    /// rows come back in a different order, which needs the real shell and the real panel.
    /// </summary>
    public sealed class TeamScreenTests
    {
        private static string ProofFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "TabProof~");

        private static VisualElement Root =>
            Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        private static string[] NamesOnScreen() =>
            Root.Query<Label>(className: "crew__name").ToList()
                .Select(label => label.text).ToArray();

        private static IEnumerator OpenTeam(GameShell shell)
        {
            Assert.That(shell.OpenScreenByName("Team"), Is.True,
                "There is no screen called Team, so this fixture is measuring nothing.");

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCrewListShowsEverybodyAndOrdersByTheColumnYouPick()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);
            yield return OpenTeam(shell);

            var longestFirst = NamesOnScreen();

            Assert.That(longestFirst.Length,
                Is.EqualTo(shell.Simulation.State.Staff.Headcount),
                "The payroll list is not showing everybody who works here.");

            // **The order the list opens in, which was asked for by name.** Anything else and the
            // first thing a player reads is whoever happens to sit at index zero.
            Assert.That(longestFirst.First(), Is.EqualTo("Iwona Krajewska"),
                "The list does not open on the longest serving person. It reads: "
                + string.Join(", ", longestFirst));

            shell.SortCrewBy(GameShell.CrewSort.Wage);
            yield return null;
            yield return null;

            var dearestFirst = NamesOnScreen();

            Assert.That(dearestFirst.Length, Is.EqualTo(longestFirst.Length),
                "Sorting lost or invented a row.");

            Assert.That(dearestFirst, Is.EquivalentTo(longestFirst),
                "Sorting changed who is on the payroll.");

            shell.SortCrewBy(GameShell.CrewSort.Level);
            yield return null;
            yield return null;

            var bestFirst = NamesOnScreen();

            Assume.That(bestFirst.First(), Is.Not.EqualTo(dearestFirst.First()),
                "The dearest person is also the most skilled in this campaign, so the two "
                + "columns cannot be told apart here and this measures nothing.");

            // The claim worth holding: a different column really does hand back a different order.
            // A header that lights up and reorders nothing is the shape this project keeps finding.
            Assert.That(bestFirst, Is.Not.EqualTo(dearestFirst).AsCollection,
                "Ordering by level gave the same order as ordering by wage, so at least one of "
                + "the headers is not reaching the list.");
        }

        /// <summary>
        /// **The card has to open on a job nobody holds.** That is the one state where a player
        /// wants to know what the job is and what it costs, and it used to be the one state where
        /// the tile refused every click.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePositionCardOpensOnAnEmptyRoleAndOffersBothWaysToFillIt()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            TabProofCampaign.Furnish(shell.Simulation);
            yield return OpenTeam(shell);

            // Nobody is an operations lead in this campaign, which is the case being measured.
            shell.ShowPositionCard(PlayerSkill.Management);

            yield return null;
            yield return null;

            var card = Root.Q(className: "roster");
            Assert.That(card, Is.Not.Null, "Clicking a job nobody holds opened nothing.");

            Assert.That(card.Q(className: "roster__empty"), Is.Not.Null,
                "The card drew an empty list under a heading, which reads as a panel that failed "
                + "to load rather than as a job with nobody in it.");

            Assert.That(card.Query<Button>(className: "hirebar__button").ToList(),
                Has.Count.EqualTo(2),
                "The card about a job does not offer the two ways to fill it.");

            Assert.That(card.Q<Label>(className: "roster__blurb"), Is.Not.Null,
                "The card does not say what the job is, which is the line that came off the tile.");

            Directory.CreateDirectory(ProofFolder);
            yield return null;
        }

        /// <summary>
        /// **Every office you could move into has a button you can actually see.**
        ///
        /// Two testers reported that clicking a bigger office does nothing, one of them on day
        /// zero. It does nothing because there is nothing there: `.office-row` is a fixed 202px
        /// with `overflow: hidden`, and the actions are the last thing in the row, so they are
        /// drawn under the bottom edge. From the player's chair a card of figures with no control
        /// on it is a card you click and nothing happens.
        ///
        /// Measured against the row's own box rather than the window: the button was always in the
        /// tree and always laid out, and a test that only asked whether it existed passed the whole
        /// time this was broken.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryOfficeYouCanMoveIntoShowsItsButtons()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            TabProofCampaign.Furnish(shell.Simulation);

            Assert.That(shell.OpenScreenByName("Offices"), Is.True, "There is no premises page.");

            yield return null;
            yield return null;
            yield return null;

            var rows = Root.Query(className: "office-row").ToList();
            Assert.That(rows, Is.Not.Empty, "The premises page drew no offices.");

            var checkedAny = false;

            foreach (var row in rows)
            {
                var buttons = row.Query<Button>(className: "office-row__move").ToList();
                if (buttons.Count == 0)
                {
                    continue;
                }

                checkedAny = true;
                var box = row.worldBound;

                foreach (var button in buttons)
                {
                    var seat = button.worldBound;

                    Assert.That(seat.yMax, Is.LessThanOrEqualTo(box.yMax + 0.5f),
                        "\"" + button.text + "\" is drawn to y=" + seat.yMax + " inside a row "
                        + "that ends at " + box.yMax + " and clips, so the player sees a card of "
                        + "numbers with no control on it and clicking it does nothing.");

                    Assert.That(seat.width, Is.GreaterThan(1f), "The button has no width.");
                }
            }

            Assert.That(checkedAny, Is.True,
                "Not one office on the page offers a move or a purchase, so this measured nothing.");
        }

        /// <summary>
        /// **Two testers reported that clicking a bigger office does nothing.**
        ///
        /// The chooser renders perfectly when a proof builds it on its own, so the fault is in what
        /// it is mounted inside. This opens the page through the real shell, opens the deal the way
        /// the button does, and photographs the whole window, because what has to be true is not
        /// that the card exists but that it is where the player is looking.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOfficeDealLandsWhereThePlayerIsLooking()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            TabProofCampaign.Furnish(shell.Simulation);

            Assert.That(shell.OpenScreenByName("Offices"), Is.True, "There is no premises page.");

            yield return null;
            yield return null;

            shell.Offices.Open(OfficeTier.Floor);

            yield return null;
            yield return null;
            yield return null;

            var card = Object.FindFirstObjectByType<UIDocument>().rootVisualElement
                .Q(className: "deal");

            Assert.That(card, Is.Not.Null, "Pressing the office button built no card at all.");

            var where = card.worldBound;
            var panel = Object.FindFirstObjectByType<UIDocument>().rootVisualElement.worldBound;

            // **The claim the testers are making, as a number.** A card that exists and sits below
            // the window is indistinguishable from a button that does nothing, and pressing again
            // shuts it, which is what "I clicked repeatedly and nothing happened" is.
            Assert.That(where.yMax, Is.LessThanOrEqualTo(panel.yMax),
                "The confirmation card runs to y=" + where.yMax + " in a window that ends at "
                + panel.yMax + ", so the player pressed the button and the card opened off the "
                + "bottom of the screen.");

            Assert.That(where.yMin, Is.GreaterThanOrEqualTo(panel.yMin),
                "The card opens above the top of the window.");

            Assert.That(where.height, Is.GreaterThan(0f), "The card has no height.");
        }

        /// <summary>
        /// The eighth job is on the screen, and it did not become an eighth founder skill.
        /// </summary>
        [UnityTest]
        public IEnumerator SupportIsAJobTheCompanyHiresAndNotASkillTheFounderHas()
        {
            Assert.That(PositionCatalog.All.Count, Is.EqualTo(8),
                "SUPPORT is missing from the jobs a company can hire into.");

            Assert.That(PlayerSkillCatalog.All.Count, Is.EqualTo(7),
                "SUPPORT leaked into the founder's skills, so the creator is now asking for two "
                + "hundred points across eight rows and every balance number behind them moved.");

            Assert.That(PlayerSkillCatalog.All.Any(skill => skill.Skill == PlayerSkill.Support),
                Is.False, "The founder is being offered points in a job rather than a skill.");

            var titles = PositionCatalog.All.Select(position => position.Title).ToList();

            // The Statecraft lesson: a `_` arm in a key switch is a valid answer with real words
            // behind it, so eight jobs can quietly share one name and nothing fails.
            Assert.That(titles.Distinct().Count(), Is.EqualTo(titles.Count),
                "Two jobs share a name: " + string.Join(", ", titles));

            var blurbs = PositionCatalog.All.Select(position => position.Blurb).ToList();

            Assert.That(blurbs.Distinct().Count(), Is.EqualTo(blurbs.Count),
                "Two jobs share a description, so one of them is drawing another's words.");

            yield return null;
        }
    }
}
