using System.Linq;
using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The tax demand is a decision with two buttons on it.
    ///
    /// **Reported by Natalia: there was no way to pay when the letter arrived.** The demand came as a
    /// strip and an inbox entry; POSTPONE was one click from the strip and PAY meant opening the
    /// inbox and finding the letter. So the dearer answer was the easier one, and the postponement
    /// committed the company to another year of interest with nothing in between saying what that
    /// cost.
    ///
    /// The fixture drives `Answer`, which is what the buttons call: an EditMode element has no panel,
    /// so a click sent to a button is never dispatched and the lambda behind it goes unmeasured.
    /// </summary>
    public sealed class TaxDialogTests
    {
        private static CompanySimulation Owing(long cash)
        {
            var simulation = new CompanySimulation(new CompanyState("Arrears"));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        private static MailItem Demand(CompanySimulation simulation, long amount)
        {
            var letter = simulation.State.Mail.Add(MailKind.TaxDemand, simulation.State.Date,
                "Revenue", "Corporation tax", "The demand for the year that just ended.");

            letter.AmountUsd = amount;
            letter.DueDayIndex = simulation.State.Date.DayIndex + CompanySimulation.DemandGraceDays;
            return letter;
        }

        private static (TaxDemandDialog Card, VisualElement Host, int Closes) Open(
            CompanySimulation simulation, MailItem letter)
        {
            var host = new VisualElement();
            var closes = 0;

            var card = new TaxDemandDialog(() => simulation, () => closes++);
            card.Show(host, letter);

            return (card, host, closes);
        }

        [Test]
        public void BothAnswersAreOnTheCard()
        {
            var simulation = Owing(500_000_000L);
            var letter = Demand(simulation, 10_000_000L);

            var (_, host, _) = Open(simulation, letter);

            var buttons = host.Query<Button>(className: "taxcard__button").ToList();

            Assert.That(buttons, Has.Count.EqualTo(2),
                "The card offers " + buttons.Count + " answers. Paying and postponing are the two "
                + "the player has, and the whole complaint was that only one of them was reachable.");

            Assert.That(buttons.All(button => button.enabledSelf), Is.True,
                "A company holding half a billion cannot press one of them.");
        }

        /// <summary>
        /// A company that cannot cover the bill still sees the button, greyed, with the reason.
        /// A card that simply omitted it would read as the game hiding the option.
        /// </summary>
        [Test]
        public void PayingIsRefusedRatherThanHiddenWhenTheMoneyIsNotThere()
        {
            var simulation = Owing(1_000L);
            var letter = Demand(simulation, 10_000_000L);

            var (_, host, _) = Open(simulation, letter);

            var buttons = host.Query<Button>(className: "taxcard__button").ToList();
            Assert.That(buttons, Has.Count.EqualTo(2));

            Assert.That(buttons[0].enabledSelf, Is.False, "PAY NOW is live on an empty account.");
            Assert.That(buttons[1].enabledSelf, Is.True, "POSTPONE is dead as well, so the card is a wall.");

            Assert.That(host.Q<Label>(className: "taxcard__cannot"), Is.Not.Null,
                "The button is refused and nothing says why.");
        }

        [Test]
        public void PayingSettlesTheLetterAndClosesTheCard()
        {
            var simulation = Owing(500_000_000L);
            var letter = Demand(simulation, 10_000_000L);

            var host = new VisualElement();
            var closes = 0;
            var card = new TaxDemandDialog(() => simulation, () => closes++);
            card.Show(host, letter);

            card.Answer(letter, MailAction.Pay);

            Assert.That(letter.IsClosed, Is.True, "the demand is still open after being paid");
            Assert.That(simulation.State.CashUsd, Is.EqualTo(490_000_000L));
            Assert.That(closes, Is.EqualTo(1), "the card did not hand the clock back");
        }

        /// <summary>
        /// **The two lines the report asked for by name.** Postponing used to happen silently and the
        /// price appeared in next January's total, which is where a player stops connecting the two.
        /// </summary>
        [Test]
        public void PostponingSaysWhatItCostAndWhenTheNewDateIs()
        {
            var simulation = Owing(500_000_000L);
            var letter = Demand(simulation, 10_000_000L);

            var host = new VisualElement();
            var card = new TaxDemandDialog(() => simulation, () => { });
            card.Show(host, letter);

            var wasDue = letter.DueDayIndex;
            card.Answer(letter, MailAction.Defer);

            Assert.That(letter.AmountUsd, Is.GreaterThan(10_000_000L),
                "postponing charged nothing");

            Assert.That(letter.DueDayIndex, Is.GreaterThan(wasDue),
                "postponing moved no date");

            Assert.That(host.Q<Label>(className: "taxcard__rate"), Is.Not.Null,
                "The card does not say what the postponement added.");

            Assert.That(host.Q<Label>(className: "taxcard__total"), Is.Not.Null,
                "The card does not say what is now owed or when.");

            Assert.That(host.Q<Label>(className: "taxcard__last"), Is.Null,
                "The last-postponement warning fired on the first one.");
        }

        /// <summary>
        /// The warning belongs on the postponement that earns it. A player told afterwards is a
        /// player told too late: the next date is not another card with buttons, it is the whole
        /// arrears leaving the account.
        /// </summary>
        [Test]
        public void TheLastPostponementSaysItIsTheLastOne()
        {
            var simulation = Owing(500_000_000L);
            var letter = Demand(simulation, 10_000_000L);

            var host = new VisualElement();
            var card = new TaxDemandDialog(() => simulation, () => { });

            for (var taken = 0; taken < CompanySimulation.MostPostponements; taken++)
            {
                card.Show(host, letter);
                card.Answer(letter, MailAction.Defer);
            }

            Assert.That(letter.DeferredDays,
                Is.EqualTo(CompanySimulation.LongestDeferralDays),
                "three postponements did not reach the ceiling");

            Assert.That(host.Q<Label>(className: "taxcard__last"), Is.Not.Null,
                "The company has used its last postponement and the card did not say so.");
        }
    }
}
