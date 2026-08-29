using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The ratchet for the ninth unreachable mechanism in this project, and it was mine.
    ///
    /// `TryPoach`, `AnswerTheCall` and `RosterOf` were written complete, tested through the
    /// simulation, and called from nowhere in `UI/`. Every one of the eight hundred tests passed on
    /// a poaching system no player could open, which is exactly the failure this codebase has now
    /// hit nine times and the reason the reachability sweep exists.
    ///
    /// **A test that calls `TryPoach` directly would not have caught it.** That is what the
    /// simulation fixtures already do, and they were green the whole time. This one builds the real
    /// panel and looks for the control, because the question is whether a player can get there.
    /// </summary>
    public sealed class RivalPanelTests
    {
        private static CompanySimulation Company()
        {
            var state = new CompanyState("Test Lab");
            return new CompanySimulation(state);
        }

        private static VisualElement Panel(CompanySimulation simulation, CompetitorId lab)
        {
            var panel = new RivalPanel(() => simulation, () => { });
            return panel.Build(lab);
        }

        /// <summary>
        /// The card with the roster section open.
        ///
        /// The three sections are tabs now, so the people are one click from the standing rather
        /// than under it. A test has no panel and a click on a tab is never dispatched, which is
        /// why the panel exposes the switch these call.
        /// </summary>
        private static VisualElement People(CompanySimulation simulation, CompetitorId lab)
        {
            var panel = new RivalPanel(() => simulation, () => { });
            panel.ShowPeople();
            return panel.Build(lab);
        }

        /// <summary>
        /// There is a way in: the roster is drawn and every person on it has a button.
        ///
        /// Counting the buttons rather than asserting one exists, because a roster that renders its
        /// header and no rows would satisfy a weaker check and is the same dead screen.
        /// </summary>
        [Test]
        public void ThePlayerCanSeeTheirStaffAndHasAWayToMakeAnOffer()
        {
            var simulation = Company();
            var tree = People(simulation, CompetitorId.OpenAi);

            var people = tree.Query<VisualElement>(className: "person").ToList();
            var offers = tree.Query<Button>(className: "person__offer").ToList();

            Assert.That(people, Is.Not.Empty,
                "Nobody is drawn, so the roster is a heading over an empty box.");

            Assert.That(offers.Count, Is.EqualTo(people.Count),
                "Every person shown has to be approachable, or the ones without a button are "
                + "decoration the player will try to click.");
        }

        /// <summary>
        /// The roster the panel draws is the roster the simulation would act on.
        ///
        /// Two sources for this would let the player make an offer to somebody who does not exist
        /// on the other side of the call, which is the shape the model type failed in.
        /// </summary>
        [Test]
        public void TheNamesOnScreenAreTheOnesTheSimulationHolds()
        {
            var simulation = Company();
            var roster = simulation.RosterOf(CompetitorId.OpenAi);
            var tree = People(simulation, CompetitorId.OpenAi);

            var drawn = tree.Query<Label>(className: "person__name").ToList()
                .Select(label => label.text)
                .ToList();

            Assert.That(drawn, Is.Not.Empty);

            foreach (var name in drawn)
            {
                Assert.That(roster.Any(member => member.Name == name), Is.True,
                    $"The panel drew {name} and the simulation has no such person.");
            }
        }

        /// <summary>
        /// A relationship nothing has happened to draws no history, and one that has, does.
        ///
        /// The empty case is the one worth holding: a HISTORY heading over nothing reads as a
        /// screen that failed to load rather than as a company you have never dealt with.
        /// </summary>
        [Test]
        public void TheHistoryOnlyAppearsOnceSomethingHasHappened()
        {
            var simulation = Company();

            var quiet = Panel(simulation, CompetitorId.OpenAi)
                .Query<VisualElement>(className: "rival__history").ToList();

            Assert.That(quiet, Is.Empty,
                "Nothing has happened with this lab, so there is nothing to list.");

            simulation.State.Relations.Record(CompetitorId.OpenAi, simulation.State.Date,
                -14.0, "relation.reason.poached", "Somebody");

            var loud = Panel(simulation, CompetitorId.OpenAi)
                .Query<VisualElement>(className: "rival__entry").ToList();

            Assert.That(loud, Is.Not.Empty, "The thing that happened is not on the card.");
        }

        /// <summary>
        /// Nothing on the card reads as a phrase-book key.
        ///
        /// Same check `BuiltKeyTests` makes of the catalogs, made of the assembled screen, because a
        /// key that exists can still be asked for with a typo at the call site.
        /// </summary>
        [Test]
        public void EveryWordOnTheCardIsProseInBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var simulation = Company();
                    var tree = Panel(simulation, CompetitorId.OpenAi);

                    foreach (var label in tree.Query<Label>().ToList())
                    {
                        var text = label.text;

                        if (string.IsNullOrEmpty(text) || text.Contains(' '))
                        {
                            continue;
                        }

                        Assert.That(text.Contains('.') && text.ToLowerInvariant() == text, Is.False,
                            $"{language}: \"{text}\" is a key sitting where a sentence should be.");
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// The rosters are stable across two reads of the same company.
        ///
        /// They are generated from a seed rather than stored, so a second call that reshuffled them
        /// would rename everybody the moment the card was redrawn, which happens after every offer.
        /// </summary>
        [Test]
        public void OpeningTheSameLabTwiceShowsTheSamePeople()
        {
            var simulation = Company();

            var first = simulation.RosterOf(CompetitorId.OpenAi).Select(member => member.Name);
            var second = simulation.RosterOf(CompetitorId.OpenAi).Select(member => member.Name);

            CollectionAssert.AreEqual(first.ToList(), second.ToList());
        }
    }
}
