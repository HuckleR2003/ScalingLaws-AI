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
    /// VS TOP MODEL: what the best thing on the market does, beside the plan.
    ///
    /// **Asked for by name.** The projection says 15.2 and nothing on that screen said whether that
    /// was good, so the one question a player has at the moment they commit a hundred million
    /// dollars had to be answered by leaving the screen and coming back.
    /// </summary>
    public sealed class CreatorComparisonTests
    {
        /// <summary>
        /// A company in a year where somebody else has actually shipped something.
        ///
        /// **The clock has to move rather than be set.** The rival field builds its live models as
        /// the days pass, so a state handed a 2024 date and nothing else has a frontier that falls
        /// back to the incumbent world and no named model at all. A proof frame taken that way came
        /// back with the button lit and no figures beside it, which reads exactly like the feature
        /// failing.
        /// </summary>
        private static CompanySimulation InAYearWithRivals()
        {
            var state = new CompanyState("Prometheus AI", 12)
            {
                CashUsd = 4_000_000_000L
            };

            var simulation = new CompanySimulation(state);
            simulation.Advance(GameDate.FromCalendar(2024, 6, 1).DayIndex - state.Date.DayIndex);

            Assert.That(state.IsBankrupt, Is.False,
                "The fixture went broke before it got to 2024, so it is measuring nothing.");

            Assert.That(state.Rivals.LiveModels(state.Date), Is.Not.Empty,
                "No rival has a model on the market, so there is nothing to compare against and "
                + "this fixture would pass on a game with no comparison in it at all.");

            return simulation;
        }

        [Test]
        public void TheComparisonIsOffUntilItIsAskedFor()
        {
            var panel = new ModelCreatorPanel(InAYearWithRivals())
            {
                Stage = ModelCreatorPanel.ReviewStage
            };

            panel.Refresh();

            foreach (var rival in panel.Root.Query<Label>(className: "effect-figure__rival").ToList())
            {
                Assert.That(rival.text, Is.Empty,
                    "The four figures are about the plan. A second number beside each of them is "
                    + "a thing to turn on, not a thing to read past.");
            }
        }

        [Test]
        public void TurningItOnPutsTheBestModelOnTheMarketBesideThePlan()
        {
            var simulation = InAYearWithRivals();

            var best = simulation.State.Rivals.LiveModels(simulation.State.Date)
                .OrderByDescending(model => model.Capability)
                .First();

            var panel = new ModelCreatorPanel(simulation)
            {
                Stage = ModelCreatorPanel.ReviewStage
            };

            panel.Refresh();
            panel.ShowVersus(true);

            var rivals = panel.Root.Query<Label>(className: "effect-figure__rival").ToList();
            var whose = panel.Root.Query<Label>(className: "effect-figure__whose").ToList();

            Assert.That(rivals, Is.Not.Empty, "The comparison has nowhere to draw.");

            Assert.That(rivals[0].text, Is.EqualTo(UiFormat.Number(best.Capability)),
                "The capability figure has to be the strongest live model in the field, read from "
                + "the same list the market is served from.");

            Assert.That(whose.Any(label => label.text == best.DisplayName), Is.True,
                "And it has to say whose it is, or it is a red number from nowhere.");
        }

        /// <summary>
        /// Nothing is invented for the two figures nobody publishes.
        ///
        /// **This is the honesty flag, on a comparison.** No lab says what a run cost it or how
        /// long it took, so a rival figure under TIME TO TRAIN or CASH IT BURNS would be the game
        /// passing a guess off as a fact on the screen where the player commits the money.
        /// </summary>
        [Test]
        public void NothingIsInventedForTheFiguresNobodyPublishes()
        {
            var panel = new ModelCreatorPanel(InAYearWithRivals())
            {
                Stage = ModelCreatorPanel.ReviewStage
            };

            panel.Refresh();
            panel.ShowVersus(true);

            var rivals = panel.Root.Query<Label>(className: "effect-figure__rival").ToList();

            Assert.That(rivals.Count, Is.EqualTo(4), "There are four figures.");

            Assert.That(rivals[2].text, Is.Empty, "Nobody publishes what a rival's run took.");
            Assert.That(rivals[3].text, Is.Empty, "Nor what it cost them.");
        }

        [Test]
        public void ItGoesAwayAgain()
        {
            var panel = new ModelCreatorPanel(InAYearWithRivals())
            {
                Stage = ModelCreatorPanel.ReviewStage
            };

            panel.Refresh();
            panel.ShowVersus(true);
            panel.ShowVersus(false);

            foreach (var rival in panel.Root.Query<Label>(className: "effect-figure__rival").ToList())
            {
                Assert.That(rival.text, Is.Empty);
            }
        }

        [Test]
        public void ReviewIsNotTheLastStage()
        {
            Assert.That(ModelCreatorPanel.ReviewStage,
                Is.EqualTo(ModelCreatorPanel.StageCount - 2),
                "AFTER THE RUN is the last page and nothing is decided on it. The first version of "
                + "the comparison button counted from the end and landed there.");
        }

        [Test]
        public void TheButtonHasWordsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (Language language in System.Enum.GetValues(typeof(Language)))
                {
                    Loc.Current = language;

                    Assert.That(Loc.T("create.versus"), Is.Not.EqualTo("create.versus"));
                    Assert.That(Loc.T("create.reuse"), Is.Not.EqualTo("create.reuse"));
                    Assert.That(Loc.T("create.reuse.note"), Is.Not.EqualTo("create.reuse.note"));
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
