using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        /// **Nothing in the corner lands on anything else, however much is happening.**
        ///
        /// Reported by a playtester: with models on sale, a run going and a node researching, the
        /// banners in the top right ran over each other. They did, and by construction: the column
        /// starts at 54 and grows with every product, while the research banner was pinned to 214
        /// and the upgrade banner to 458.
        ///
        /// The worst case is the one that has to be measured, so this builds it: three products, a
        /// node, an upgrade, and a run.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCornerBannersNeverLandOnEachOther()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            TabProofCampaign.Furnish(shell.Simulation);

            var simulation = shell.Simulation;
            var state = simulation.State;
            state.CashUsd = 900_000_000L;
            state.ResearchPoints = 400_000.0;

            // Two more products beside the flagship, each its own line so none supersedes another.
            for (var index = 0; index < 2; index++)
            {
                var extra = new DeployedModel(
                    "Follower " + (index + 1), ArchitectureId.DenseTransformer, 44.0 + index,
                    state.Date, 2e10, 1.0, ModelType.General, "Line " + (index + 1));

                state.AddDeployedModel(extra);
                extra.SeedLine(MonetizationPolicy.OpeningSubscriptionUsdPerMonth, 0.0);
            }

            simulation.TryStartUpgrades(0, new[] { ModelTrait.Reasoning }, out _);

            foreach (var node in ResearchTree.All)
            {
                if (simulation.TryStartResearch(node.Id, out _))
                {
                    break;
                }
            }

            Assert.That(shell.OpenScreenByName("Site"), Is.True);

            yield return null;
            yield return null;
            yield return null;

            var corner = Root.Q(className: "mb-stack");
            Assert.That(corner, Is.Not.Null, "There is no corner stack.");

            // **Counted by kind, and asserted rather than assumed.** A guard that only counts
            // banners passes on three products with the research and upgrade strips missing
            // entirely, which is the regression most likely to arrive here: the two that moved
            // into slots are the two that could stop being drawn without anything else noticing.
            // **What the player can see, not what the layout holds.** The product half of the
            // column is a scroller, so a banner below the fold keeps a world rectangle that runs
            // straight through the research strip underneath it while being clipped to nothing on
            // screen. Measuring raw boxes reports that as two banners drawn over each other, which
            // is a real failure of this test and not of the interface.
            static Rect Seen(VisualElement element)
            {
                var box = element.worldBound;

                for (var parent = element.hierarchy.parent;
                     parent != null;
                     parent = parent.hierarchy.parent)
                {
                    // Against the scroller's viewport rather than against anything that reports
                    // itself clipped: `resolvedStyle` carries no overflow, and the viewport is the
                    // rectangle a scrolled page is actually shown through.
                    if (parent is not ScrollView scroller)
                    {
                        continue;
                    }

                    var clip = scroller.contentViewport.worldBound;

                    var xMin = Mathf.Max(box.xMin, clip.xMin);
                    var yMin = Mathf.Max(box.yMin, clip.yMin);
                    var xMax = Mathf.Min(box.xMax, clip.xMax);
                    var yMax = Mathf.Min(box.yMax, clip.yMax);

                    box = new Rect(xMin, yMin, Mathf.Max(0f, xMax - xMin),
                        Mathf.Max(0f, yMax - yMin));
                }

                return box;
            }

            List<VisualElement> Up(string css) => Root.Query(className: css)
                .ToList()
                .Where(element => element.resolvedStyle.display != DisplayStyle.None)
                .Where(element => Seen(element).height > 1f)
                .ToList();

            var products = Up("mb");
            var research = Up("rb");
            var upgrade = Up("ub");

            Assert.That(products.Count, Is.GreaterThan(1),
                "The company is selling three models and fewer than two product banners are up, "
                + "so the worst case this fixture exists to measure was never built.");

            Assert.That(research.Count, Is.EqualTo(1),
                "A node is running and its banner is not on screen, so the overlap below is "
                + "measured over whatever is left rather than over what the playtester saw.");

            Assert.That(upgrade.Count, Is.EqualTo(1),
                "An upgrade is running and its banner is not on screen, same reading.");

            var panels = products.Concat(research).Concat(upgrade).ToList();

            // **And the column stays inside its own box.** It is anchored top and bottom now so
            // it cannot reach the bottom bar, which is only true if the part that gives way
            // actually gives way: anything overflowing the box is drawn over the bar.
            var stackBox = corner.worldBound;

            foreach (var child in corner.hierarchy.Children())
            {
                if (child.worldBound.height <= 1f)
                {
                    continue;
                }

                Assert.That(child.worldBound.yMax, Is.LessThanOrEqualTo(stackBox.yMax + 0.5f),
                    "The corner column overflows its own box, so its last strip is drawn over "
                    + "the bottom bar: column " + stackBox + ", child " + child.worldBound + ".");
            }

            for (var left = 0; left < panels.Count; left++)
            {
                for (var right = left + 1; right < panels.Count; right++)
                {
                    var a = Seen(panels[left]);
                    var b = Seen(panels[right]);

                    var overlaps = a.xMin < b.xMax && b.xMin < a.xMax
                        && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;

                    Assert.That(overlaps, Is.False,
                        "Two banners in the corner are drawn over each other: "
                        + a + " and " + b + ". That is what the playtester saw with three things "
                        + "happening at once.");
                }
            }
        }

        /// <summary>
        /// **The way out of an upgrade is on screen and inside its own row.**
        ///
        /// A tester asked for this by name and there was no way to stop one at all. The strip pools
        /// its rows and rebinds them, which is two chances to draw a button nobody can press: a
        /// handler bound to a row rather than to the programme it is showing, and a control that
        /// overflows the strip the way the office buttons overflowed their card.
        /// </summary>
        [UnityTest]
        public IEnumerator TheUpgradeStripOffersAWayOut()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            TabProofCampaign.Furnish(shell.Simulation);

            var simulation = shell.Simulation;
            simulation.State.CashUsd = 400_000_000L;

            // **Commissioned only if the campaign is not already running one.** It is now, and
            // asking for a second on the same model is correctly refused, which turned this
            // fixture inconclusive rather than red: it went on reporting success while measuring
            // nothing. What this test needs is a programme in flight, not one it started itself.
            if (simulation.State.UpgradeProjects.Count == 0)
            {
                Assume.That(
                    simulation.TryStartUpgrades(0, new[] { ModelTrait.Reasoning }, out var why),
                    Is.True, "no upgrade could be commissioned: " + why);
            }

            Assert.That(simulation.State.UpgradeProjects.Count, Is.GreaterThan(0),
                "Nothing is being upgraded, so the strip below is correctly empty and this "
                + "fixture would pass on a game with no abandon button in it at all.");

            Assert.That(shell.OpenScreenByName("Site"), Is.True);

            yield return null;
            yield return null;
            yield return null;

            var stop = Root.Q<Button>(className: "ustrip__stop");
            Assert.That(stop, Is.Not.Null, "There is no way to stop an upgrade on screen.");

            Assert.That(stop.enabledSelf, Is.True,
                "The abandon button is drawn and refuses every press, which is the shape this "
                + "project has already shipped twice as something a player reads as broken.");

            var row = stop.parent;
            Assert.That(stop.worldBound.yMax, Is.LessThanOrEqualTo(row.worldBound.yMax + 0.5f),
                "The abandon button is drawn past the bottom of its own row, which is exactly how "
                + "the office buttons were invisible for months.");

            Assert.That(stop.worldBound.width, Is.GreaterThan(1f), "It has no width.");

            // **And the row it sits in has to be on the screen.** Being inside its own card is
            // what the office buttons failed; being above the bottom bar is the other half, and a
            // control drawn under the bar is exactly as unreachable as one drawn under a clipping
            // edge. The corner column grows with everything the company is doing, so this is the
            // one that goes first.
            // **Beside the programme, not under it.** A row is a column by default in UI Toolkit,
            // so the button was laid out below the text inside a box 40px tall, which squashed the
            // name of the thing being stopped to half a line to make room. Both fitted their own
            // box and the row read as broken. Centres, because that is what "beside" means and it
            // does not care what either one is worth in pixels.
            var rowBox = row.worldBound;
            var stopBox = stop.worldBound;

            Assert.That(stopBox.center.y, Is.EqualTo(rowBox.center.y).Within(3f),
                "The abandon button is stacked inside its row rather than sitting beside the "
                + "programme: row " + rowBox + ", button " + stopBox + ".");

            // The row has to be tall enough for what is written in it. A kicker and a name at
            // 12px and 14.5px with padding want 45px and the row was 40, so the name of the
            // programme being stopped was cut across the middle: legible enough to pass a test
            // that only asked whether it existed, and plainly broken to look at.
            var name = row.Q(className: "ustrip__name");
            Assert.That(name, Is.Not.Null, "The row does not say what is being stopped.");

            Assert.That(name.worldBound.yMax, Is.LessThanOrEqualTo(row.worldBound.yMax + 0.5f),
                "The name of the programme is drawn past the bottom of its own row: row "
                + row.worldBound + ", name " + name.worldBound + ".");

            var bar = Root.Q(className: "hud__bar");
            Assert.That(bar, Is.Not.Null, "There is no bottom bar to measure against.");

            Assert.That(stop.worldBound.yMax, Is.LessThanOrEqualTo(bar.worldBound.yMin + 0.5f),
                "The way out of an upgrade is drawn under the bottom bar, so a player with a "
                + "busy corner cannot press it: button " + stop.worldBound + ", bar "
                + bar.worldBound + ".");
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
