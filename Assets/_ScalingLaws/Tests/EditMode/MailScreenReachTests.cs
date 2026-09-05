using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Every letter that asks for an answer has a button on the real screen.
    ///
    /// **Written because an outside review reported the legal threat as unanswerable.** It was
    /// wrong about the fact and right about the risk, and the difference is worth keeping.
    ///
    /// It was wrong because the route does exist: the inbox calls `TryActOnMail`, which routes a
    /// `LegalThreat` to `AnswerThreatLetter`, which calls `TryAnswerSmearThreat`. Nothing in
    /// `Scripts/UI/` names that method, so a sweep of `UI/` for callers comes back empty. That sweep
    /// is the method this project uses everywhere and it has earned its place; here it produced a
    /// false positive because the entry point and the destination are two different public methods.
    ///
    /// It was right because **nothing asserted that the buttons are drawn.** `letter.Actions` was
    /// tested and `TryActOnMail` was driven, and between those two there was a screen nobody had
    /// built with that letter in it. This is the gap between the rules working and the player being
    /// able to reach them, which is the failure this project has had eleven times.
    ///
    /// So the fixture builds the actual `MailScreen`, selects the letter and counts the buttons.
    /// It walks every kind rather than only the one that was queried: the next kind added gets the
    /// same guard for free.
    /// </summary>
    public sealed class MailScreenReachTests
    {
        private static CompanySimulation Fresh() =>
            new(new CompanyState("Inbox", 0x1B0Fu));

        /// <summary>Every button the screen drew, by caption.</summary>
        private static List<string> ButtonsOn(MailScreen screen)
        {
            var found = new List<string>();

            void Walk(VisualElement element)
            {
                if (element is Button button && element.ClassListContains("mail-action"))
                {
                    found.Add(button.text);
                }

                foreach (var child in element.Children())
                {
                    Walk(child);
                }
            }

            Walk(screen.Root);
            return found;
        }

        /// <summary>
        /// A lab's notice before action can be settled or refused, on the screen.
        ///
        /// The reported fault, tested the way it was reported: open the inbox, find the letter,
        /// count what the player can press.
        /// </summary>
        [Test]
        public void TheLegalThreatCanBeAnsweredFromTheInbox()
        {
            var simulation = Fresh();
            var state = simulation.State;

            state.CashUsd = 40_000_000_000L;

            // Through the real door. A letter reached in by hand would test a shape the game never
            // builds, and the shape is most of what could be wrong.
            var caught = false;

            for (var day = 0; day < 4000 && !caught; day++)
            {
                state.CashUsd = 40_000_000_000L;

                if (simulation.CanSmear(CompetitorId.OpenAi, out _)
                    && simulation.TrySmear(CompetitorId.OpenAi, SmearTier.Campaign,
                        out var backfired, out _))
                {
                    caught = backfired;
                }

                if (!caught)
                {
                    simulation.AdvanceDay();
                }
            }

            Assert.That(caught, Is.True, "no campaign was traced back in eleven years");

            var letter = state.Mail.All.FirstOrDefault(item => item.Kind == MailKind.LegalThreat);
            Assert.That(letter, Is.Not.Null);

            var screen = new MailScreen(simulation, () => { });
            screen.Select(letter.Id);

            var buttons = ButtonsOn(screen);

            Assert.That(buttons, Is.Not.Empty,
                "the letter has a deadline printed on it and nothing to press, which is the one "
                + "shape of unreachable mechanism a player actually sees");

            Assert.That(buttons.Count, Is.EqualTo(2),
                "settling and refusing, and nothing else: " + string.Join(" / ", buttons));

            // And pressing one actually reaches the rules.
            screen.Act(letter.Id, MailAction.Pay);

            Assert.That(state.SmearThreat.IsAnswered, Is.True,
                "the button was drawn and did nothing, which is worse than no button");
        }

        /// <summary>
        /// No letter anywhere asks for an answer it cannot be given.
        ///
        /// `NeedsAnswer` is what the list counts under NEEDS AN ANSWER and what the row prints, so a
        /// letter that says it needs one and offers no control is the screen contradicting itself.
        /// </summary>
        [Test]
        public void EveryLetterThatNeedsAnAnswerOffersOne()
        {
            foreach (MailKind kind in System.Enum.GetValues(typeof(MailKind)))
            {
                var simulation = Fresh();

                var letter = simulation.State.Mail.Add(kind, simulation.State.Date,
                    "Somebody", "Subject", "Body");

                letter.AmountUsd = 1_000_000L;
                letter.DueDayIndex = simulation.State.Date.DayIndex + 30;

                if (!letter.NeedsAnswer)
                {
                    continue;
                }

                var screen = new MailScreen(simulation, () => { });
                screen.Select(letter.Id);

                Assert.That(ButtonsOn(screen), Is.Not.Empty,
                    $"a {kind} letter reports that it needs an answer and draws no way to give one");
            }
        }
    }
}
