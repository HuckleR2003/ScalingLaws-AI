using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The founder page refuses its own CONTINUE when the name is empty, and says so on the field.
    ///
    /// **Reported on 2026-09-30, and the fault was where the rule lived rather than whether it
    /// existed.** The name was gated on the last page of the creator, three pages further on, where
    /// the only sentence a refusal can offer is "go back": a player who left the field empty picked
    /// their traits, their lab and their country and was then stopped by a page that could not show
    /// them the box. The rule is on the page with the box now.
    ///
    /// An EditMode test has no panel, so a click sent to CONTINUE is never dispatched and the
    /// footer's own lambda cannot be driven. What is driven is the seam the lambda calls, the same
    /// shape as <c>ManagementScreen.ShowDesk</c> and <c>GateNotice.TakeTheOffer</c>; the wiring
    /// between the two is read out of the source, which is what <see cref="ShellChromeTests"/>
    /// already does for everything else on this screen.
    /// </summary>
    public sealed class FounderNameGateTests
    {
        private static string Source(params string[] parts) =>
            File.ReadAllText(Path.Combine(
                new[] { Application.dataPath, "_ScalingLaws" }.Concat(parts).ToArray()));

        private static void Mark(VisualElement page, bool named)
        {
            var mark = typeof(MainMenuController).GetMethod(
                "MarkWhatTheFounderPageNeeds",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(mark, "The founder page has no way to say what it is refusing over.");

            mark.Invoke(null, new object[] { page, named });
        }

        /// <summary>
        /// A stand-in for the page, built out of the classes the real one uses.
        ///
        /// The page itself cannot be built here: it opens a <c>PortraitStudio</c>, which is a
        /// camera and a render texture, and the question being asked has nothing to do with either.
        /// </summary>
        private static VisualElement PageWithANameFieldAndTwoTraits()
        {
            var page = new VisualElement();

            var name = new TextField();
            name.AddToClassList("creator__name");
            page.Add(name);

            var picked = new VisualElement();
            picked.AddToClassList("trait-card");
            picked.AddToClassList("trait-card--picked");
            page.Add(picked);

            var open = new VisualElement();
            open.AddToClassList("trait-card");
            page.Add(open);

            return page;
        }

        private static VisualElement Name(VisualElement page) =>
            page.Query<VisualElement>(className: "creator__name").First();

        private static VisualElement OpenTrait(VisualElement page) =>
            page.Query<VisualElement>(className: "trait-card").ToList()
                .First(card => !card.ClassListContains("trait-card--picked"));

        [Test]
        public void AnEmptyNameIsMarkedOnTheFieldAndNowhereElse()
        {
            var page = PageWithANameFieldAndTwoTraits();

            Mark(page, named: false);

            Assert.IsTrue(Name(page).ClassListContains("creator__name--missing"),
                "Greying CONTINUE says the page will not move on and never says why. The field is "
                + "the thing that is empty, so the field is the thing that has to be marked.");

            Assert.IsFalse(OpenTrait(page).ClassListContains("creator-ring"),
                "A refusal that rings eight trait cards over an empty name field is pointing at "
                + "the wrong thing. One reason at a time, in the order the page reads.");
        }

        [Test]
        public void ANameTakesTheMarkDownAndTheRefusalMovesToTheTraits()
        {
            var page = PageWithANameFieldAndTwoTraits();

            Mark(page, named: false);
            Mark(page, named: true);

            Assert.IsFalse(Name(page).ClassListContains("creator__name--missing"),
                "The mark is a reply to one refusal. Leaving it up after the name is typed makes "
                + "the field read as broken for the rest of the creator.");

            Assert.IsTrue(OpenTrait(page).ClassListContains("creator-ring"),
                "With a name in the box the only thing left to refuse over is the traits.");
        }

        /// <summary>
        /// The footer asks its question again when something it reads has changed.
        ///
        /// **This is the bug the first version of the gate shipped**, reported the same evening:
        /// the refusal appeared correctly, the player typed their name, and CONTINUE went on
        /// refusing. Readiness was a `bool` captured when the page was drawn, and nothing on that
        /// page is rebuilt by typing, so the only way through was to leave the page and come back.
        /// A captured answer to a live question.
        /// </summary>
        [Test]
        public void TheFooterAsksAgainWhenTheAnswerHasChanged()
        {
            var host = new GameObject("FooterProbe");
            host.SetActive(false);

            try
            {
                var menu = host.AddComponent<MainMenuController>();

                var build = typeof(MainMenuController).GetMethod("Footer",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(build, "The creator pages have no footer builder.");

                var ready = false;

                var footer = (VisualElement)build.Invoke(menu, new object[]
                {
                    "DALEJ",
                    (Action)(() => { }),
                    (Action)(() => { }),
                    (Func<bool>)(() => ready),
                    (Func<string>)(() => "still missing"),
                    null,
                    null
                });

                var forward = footer.Query<Button>(className: "menu-button--primary").First();

                Assert.IsNotNull(forward, "The footer has no CONTINUE on it.");
                Assert.IsTrue(forward.ClassListContains("menu-button--shut"),
                    "A page that is not ready has to draw its CONTINUE as unavailable.");

                // What the player did: they filled the box in. Nothing rebuilds the page.
                ready = true;

                var refresh = (Action)typeof(MainMenuController)
                    .GetField("refreshFooter", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(menu);

                Assert.IsNotNull(refresh,
                    "Nothing can tell the footer that what it reads has changed, so a page that "
                    + "becomes ready without being rebuilt can never be left.");

                refresh();

                Assert.IsFalse(forward.ClassListContains("menu-button--shut"),
                    "The name is in the box and CONTINUE is still drawn as refusing. This is the "
                    + "reported bug: the only way forward was to leave the page and come back.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ContinueOnTheFounderPageActuallyReadsTheName()
        {
            var source = Source("Scripts", "UI", "MainMenuController.cs");

            StringAssert.Contains("() => Named() && remaining == 0", source,
                "The founder page's CONTINUE has to be ready on both, or the name is still being "
                + "asked for three pages later by a screen that cannot show the field.");

            StringAssert.Contains("Loc.T(\"gate.founder_unnamed\")", source,
                "gate.founder_missing tells the player to go back a page, which is the sentence "
                + "for the page that has no field on it. This one has the field.");

            StringAssert.Contains("MarkWhatTheFounderPageNeeds(page, Named())", source,
                "The refusal has to reach the field. A gate with no nudge greys a button and "
                + "leaves the player to guess which of nine controls it meant.");
        }

        /// <summary>
        /// WSTECZ on the first page of the creator is the way out of the creator.
        ///
        /// Reported on 2026-09-30: once NEW GAME was pressed there was no route back to the main
        /// menu at all. BACK went to the cold open, which is a page of typed text with CONTINUE
        /// under it, so the only way out of a campaign you had not started was to start it.
        /// </summary>
        [Test]
        public void BackOnTheFirstCreatorPageLeadsToTheMainMenu()
        {
            var source = Source("Scripts", "UI", "MainMenuController.cs");

            StringAssert.Contains("() => Show(Stage.Company), () => Show(Stage.Menu)", source,
                "The founder page's BACK has to reach the menu. Stage.Intro is the cold open, "
                + "which is not a way out of anything.");
        }

        [Test]
        public void TheRedRuleSitsLaterInTheSheetThanTheOneItOverrides()
        {
            var sheet = Source("Resources", "ScalingLaws.uss");

            var plain = sheet.IndexOf(".creator__name > .unity-text-field__input",
                StringComparison.Ordinal);
            var marked = sheet.IndexOf(".creator__name--missing > .unity-text-field__input",
                StringComparison.Ordinal);

            Assert.Greater(plain, 0, "The name field has lost its own styling.");
            Assert.Greater(marked, 0,
                "A class named from C# and absent from the sheet is the fault that once made a "
                + "whole creator column invisible.");

            Assert.Greater(marked, plain,
                "Both are two classes and a child, so they carry the same weight and the later "
                + "one wins. Above the plain rule the red border is written and then painted over, "
                + "which is the repair .rackface.roombuild__face and .family-offers .corpus-row "
                + "both needed.");
        }
    }
}
