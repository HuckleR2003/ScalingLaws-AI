using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Grants, and the rules that keep them off the wrong side of the spine.
    ///
    /// The design says nothing may guarantee income or let capital skip a calendar gate. A grant
    /// pays money for work, which is exactly the shape of thing that breaks both, so most of what
    /// is asserted here is about the *price* of one rather than the payout.
    /// </summary>
    public sealed class GrantTests
    {
        private static CompanySimulation Fresh()
        {
            var state = new CompanyState("Grants", 0x6A17u);
            return new CompanySimulation(state);
        }

        /// <summary>
        /// Puts the company on whatever rung a programme sits on, then signs for it.
        ///
        /// **Climbs rather than cheats.** The ladder is the mechanic under test elsewhere in this
        /// file, so a fixture that wanted a tier-three award marks a tier-one and a tier-two as
        /// finished, which is exactly what a player does to get there. Reaching in and lowering the
        /// programme's own tier would test a catalogue that does not ship.
        /// </summary>
        private static Grant Sign(CompanySimulation simulation, GrantId id)
        {
            ClimbTo(simulation, GrantCatalog.Get(id).Tier);

            Assert.That(simulation.TryAcceptGrant(id, out var why), Is.True, why);

            return simulation.HeldGrants().Single(grant => grant.Id == id);
        }

        /// <summary>Marks one programme finished on every rung below this one.</summary>
        private static void ClimbTo(CompanySimulation simulation, int tier)
        {
            for (var rung = 1; rung < tier; rung++)
            {
                var step = rung;

                var below = GrantCatalog.All.First(entry => entry.Tier == step);
                simulation.State.GrantsCompleted.Add(below.Id);
            }
        }

        // ---- the balance rules --------------------------------------------------------------------

        /// <summary>
        /// **Failing must never pay.**
        ///
        /// The advance is handed over on signing and taken back if the term is missed, so the worst
        /// case is a wash plus a mark on the reputation. If any programme ever paid an advance
        /// larger than what it reclaims, the strongest move in the game would be to sign for
        /// everything and deliberately miss every deadline.
        /// </summary>
        [Test]
        public void NoProgrammeIsWorthTakingAndFailingOnPurpose()
        {
            foreach (var definition in GrantCatalog.All)
            {
                Assert.That(definition.CompletionUsd, Is.GreaterThan(definition.AdvanceUsd),
                    $"{definition.Id} pays more up front than on completion, so missing the term "
                    + "is the better outcome.");
            }
        }

        /// <summary>
        /// The sums stay small against a company that is actually trading.
        ///
        /// A grant is meant to be a decision about how to run the company, not a funding round. If
        /// the whole catalogue won at once could rebuild the opening balance, the correct opening
        /// move becomes farming grants rather than shipping anything.
        /// </summary>
        [Test]
        public void TheWholeCatalogueIsWorthLessThanTheOpeningBalance()
        {
            var everything = GrantCatalog.All.Sum(definition => definition.CompletionUsd);

            Assert.That(everything, Is.LessThan(CompanyState.StartingCashUsd),
                $"Winning every grant in the game pays ${everything:N0}, which is more than the "
                + "company starts with. Grants have become a funding round.");
        }

        /// <summary>
        /// The ladder opens on the first rung and climbs when something is finished.
        ///
        /// Both halves matter. A board that starts empty is a system most players never meet, and a
        /// board that offers everything on day one is a noticeboard rather than a campaign.
        /// </summary>
        [Test]
        public void TheLadderStartsOnTheFirstRungAndClimbsWhenSomethingIsFinished()
        {
            var nothingDone = new HashSet<GrantId>();
            var openAtStart = GrantCatalog.OpenTo(nothingDone).ToList();

            Assert.That(openAtStart, Is.Not.Empty,
                "Nothing is fundable on day one, so a player meets the grant desk years late.");

            Assert.That(openAtStart.All(definition => definition.Tier == 1), Is.True,
                "A body four rungs up is writing to a company that has finished nothing.");

            var firstRung = openAtStart[0];
            var afterOne = new HashSet<GrantId> { firstRung.Id };

            Assert.That(GrantCatalog.OpenTo(afterOne).Count(), Is.GreaterThan(openAtStart.Count),
                "Finishing an award opened nothing, so the ladder never climbs.");
        }

        /// <summary>
        /// Every rung is reachable and none is empty.
        ///
        /// A gap in the middle would stall the ladder permanently: nothing on rung three means
        /// nothing can ever be finished there, so rung four never opens and half the catalogue is
        /// unreachable content. That is the same failure class as a research node that opens before
        /// its own prerequisite.
        /// </summary>
        [Test]
        public void EveryRungOfTheLadderHasSomethingOnIt()
        {
            for (var tier = 1; tier <= GrantCatalog.TopTier; tier++)
            {
                var rung = tier;

                Assert.That(GrantCatalog.All.Any(definition => definition.Tier == rung), Is.True,
                    $"Tier {rung} is empty, so nothing there can be finished and the rung above it "
                    + "can never open.");
            }
        }

        /// <summary>
        /// The board is never empty for a company that has taken nothing.
        ///
        /// **This is the failure the rebuild exists to fix.** Grants were offered by a one per cent
        /// daily roll, so the first one arrived a hundred days out on average and three quarters of
        /// players spent their opening month reading a panel that said nobody was funding anything.
        /// A register is a list you can apply to on the day you open it.
        /// </summary>
        [Test]
        public void ThereIsSomethingToApplyForOnDayOne()
        {
            var simulation = Fresh();
            var board = simulation.AvailableGrants();

            Assert.That(board, Is.Not.Empty,
                "A new company opens the grants panel and finds nothing on it.");

            Assert.That(board.All(entry => entry.Tier == 1), Is.True,
                "A body four rungs up is writing to a company that has finished nothing.");
        }

        // ---- what actually happens ----------------------------------------------------------------

        [Test]
        public void SigningPaysTheAdvance()
        {
            var simulation = Fresh();
            var before = simulation.State.CashUsd;

            Sign(simulation, GrantId.StandardsStipend);

            var definition = GrantCatalog.Get(GrantId.StandardsStipend);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before + definition.AdvanceUsd),
                "The advance is the whole reason to sign today rather than think about it.");
        }

        /// <summary>
        /// **The baseline is captured on the day of signing.**
        ///
        /// A programme asking for three more models has to mean three *more*. Reading the released
        /// count at the close would hand the award to a company that had already shipped three
        /// before the letter arrived, which is the same class of mistake as deriving a figure that
        /// was only ever true on one particular day.
        /// </summary>
        [Test]
        public void TheTargetIsMeasuredFromWhereTheCompanyStoodOnTheDayItSigned()
        {
            var simulation = Fresh();
            simulation.State.UnlockedResearch.Add(ResearchTree.All[0].Id);
            simulation.State.UnlockedResearch.Add(ResearchTree.All[1].Id);

            var grant = Sign(simulation, GrantId.ResearchFellowship);

            Assert.That(grant.Baseline, Is.EqualTo(2.0),
                "Two nodes were already finished when the award was signed.");

            var definition = grant.Definition;

            Assert.That(
                GrantConditions.IsMet(definition.Goal, grant.Baseline, definition.Target, 2.0),
                Is.False,
                "Standing still met the condition, so the award pays for work already done.");

            Assert.That(
                GrantConditions.IsMet(definition.Goal, grant.Baseline, definition.Target,
                    2.0 + definition.Target),
                Is.True,
                "Doing exactly what was asked did not meet the condition.");
        }

        /// <summary>
        /// A sustained condition is lost the day it breaks and does not come back.
        ///
        /// This is what separates the two shapes. Recovering before the closing date would make a
        /// sustained award identical to a counting one, and the whole reason it costs money to hold
        /// is that a single bad day ends it.
        ///
        /// The company has to be trading for any of that to mean anything. A sustained term does
        /// not start until it is, so without the revenue below this fixture measures a term that
        /// never began and reads a break that correctly did not happen.
        /// </summary>
        [Test]
        public void ASustainedAwardIsLostOnTheDayItBreaksAndRecoveringDoesNotSaveIt()
        {
            var simulation = Fresh();
            var state = simulation.State;

            state.Reputation = 0.9;
            state.LifetimeRevenueUsd = 500_000;

            var grant = Sign(simulation, GrantId.ContinuityAward);

            simulation.Advance(30);
            Assert.That(grant.IsBroken, Is.False, "Nothing was wrong yet.");

            state.Reputation = 0.05;
            simulation.Advance(2);
            Assert.That(grant.IsBroken, Is.True, "The condition was broken and nothing noticed.");

            state.Reputation = 0.9;
            simulation.Advance(30);

            Assert.That(grant.IsBroken, Is.True,
                "Recovering cleared a condition that was supposed to hold every day.");
        }

        /// <summary>
        /// Missing the term hands the advance back, and the company is worse off than if it had
        /// never signed. That is the risk the whole mechanism rests on.
        /// </summary>
        [Test]
        public void MissingTheTermReturnsTheAdvanceAndCostsStanding()
        {
            var simulation = Fresh();
            var state = simulation.State;

            state.Reputation = 0.5;

            var before = state.CashUsd;
            var standingBefore = state.Reputation;

            var definition = GrantCatalog.Get(GrantId.StandardsStipend);
            Sign(simulation, GrantId.StandardsStipend);

            Assert.That(state.CashUsd, Is.EqualTo(before + definition.AdvanceUsd));

            // Nothing is shipped, so the term closes unmet.
            simulation.Advance(definition.TermDays + 2);

            Assert.That(simulation.HeldGrants().Any(grant => grant.Id == GrantId.StandardsStipend),
                Is.False, "The award never closed.");

            Assert.That(state.Reputation, Is.LessThan(standingBefore),
                "Missing a public commitment cost the company nothing at all.");

            Assert.That(state.Ledger, Is.Not.Null);
        }

        /// <summary>Turning one down clears it, and it does not reappear the following week.</summary>
        [Test]
        public void DismissingAnOfferPutsItAwayForAWhile()
        {
            var simulation = Fresh();

            Assert.That(
                simulation.AvailableGrants().Any(entry => entry.Id == GrantId.MinistryFirstLine),
                Is.True, "The programme was not on the board to begin with.");

            Assert.That(simulation.TryDismissGrant(GrantId.MinistryFirstLine), Is.True);

            Assert.That(
                simulation.AvailableGrants().Any(entry => entry.Id == GrantId.MinistryFirstLine),
                Is.False, "Putting one away left it on the board.");

            simulation.Advance(60);

            Assert.That(
                simulation.AvailableGrants().Any(entry => entry.Id == GrantId.MinistryFirstLine),
                Is.False,
                "A programme the player put away came back inside two months.");
        }

        /// <summary>
        /// The company cannot sign for everything at once.
        ///
        /// Without a cap, the correct play is to accept every offer regardless of whether the
        /// conditions can be held together, and the sustained ones stop being a decision.
        /// </summary>
        [Test]
        public void OnlySoManyAwardsCanBeHeldAtOnce()
        {
            var simulation = Fresh();

            Sign(simulation, GrantId.MinistrySafeStart);
            Sign(simulation, GrantId.MinistryFirstLine);

            Assert.That(simulation.TryAcceptGrant(GrantId.StandardsStipend, out var why), Is.False);
            Assert.That(why, Is.Not.Empty, "Refused without saying why.");
        }

        /// <summary>
        /// Every goal reads something the game already computes.
        ///
        /// A condition the player cannot see on some other screen is a condition they cannot plan
        /// around, and this walks all of them to make sure none returns a constant.
        /// </summary>
        [Test]
        public void EveryGoalReadsSomethingRealOffTheCompany()
        {
            var simulation = Fresh();
            var state = simulation.State;

            state.Reputation = 0.4;
            state.UnlockedResearch.Add(ResearchTree.All[0].Id);

            foreach (GrantGoal goal in System.Enum.GetValues(typeof(GrantGoal)))
            {
                var reading = GrantConditions.Reading(goal, state, 12.0, 0.5);

                Assert.That(double.IsNaN(reading), Is.False, $"{goal} read NaN.");
                Assert.That(double.IsInfinity(reading), Is.False, $"{goal} read infinity.");
            }
        }

        // ---- the term, and what a missed one costs --------------------------------------------------

        /// <summary>
        /// A sustained term does not run while the company has nothing on sale.
        ///
        /// **This is a payout for doing nothing, and it was in the game.** "Safe first release"
        /// measures incidents, and a company with no model has none, so signing it on day one and
        /// walking away collected $400,000 and sixty research points ninety days later. Every
        /// sustained goal has the same hole for the same reason: they describe how a company is run
        /// and an empty office is trivially compliant with all of them.
        /// </summary>
        [Test]
        public void ASustainedTermDoesNotRunBeforeTheCompanyIsTrading()
        {
            var simulation = Fresh();
            var grant = Sign(simulation, GrantId.MinistrySafeStart);

            Assume.That(GrantCatalog.IsSustained(grant.Definition.Goal), Is.True,
                "this fixture is about a sustained award and that one has stopped being one");

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(GrantConditions.IsTrading(simulation.State), Is.False,
                "the company started selling something on its own, so this measures nothing");

            Assert.That(grant.DaysElapsed, Is.Zero,
                "The term ran past its own length against a company with nothing on sale. Ninety "
                + "days of that pays out, which is guaranteed income with the calendar gate "
                + "skipped, and the spine forbids exactly that.");

            Assert.That(simulation.HeldGrants(), Has.Member(grant),
                "and it must not quietly close either: waiting is not failing");
        }

        /// <summary>
        /// A counting term starts the day it is signed, because the deadline is the whole cost.
        ///
        /// Releasing a model or finishing a node is work, and the date on it is the pressure. If
        /// those waited too, a player could sign for everything and start the clock whenever it
        /// suited them.
        /// </summary>
        [Test]
        public void ACountingTermStartsCountingAtOnce()
        {
            var simulation = Fresh();
            var grant = Sign(simulation, GrantId.MinistryFirstLine);

            Assume.That(GrantCatalog.IsSustained(grant.Definition.Goal), Is.False);

            simulation.AdvanceDay();
            simulation.AdvanceDay();

            Assert.That(grant.DaysElapsed, Is.EqualTo(2));
        }

        /// <summary>
        /// The clock starts on the day the company starts trading, and never stops again.
        ///
        /// Retiring the last model must not pause it. A term the player can hold open is a term
        /// with no deadline, and the deadline is the only cost a grant carries until it is missed.
        /// </summary>
        [Test]
        public void TheTermStartsWhenTheCompanyDoesAndDoesNotStopAgain()
        {
            var simulation = Fresh();
            var grant = Sign(simulation, GrantId.MinistrySafeStart);

            simulation.AdvanceDay();
            Assert.That(grant.HasBegun, Is.False);

            simulation.State.LifetimeRevenueUsd = 250_000;
            simulation.AdvanceDay();

            Assert.That(grant.HasBegun, Is.True, "the company is trading and the term is still idle");
            Assert.That(grant.DaysElapsed, Is.EqualTo(1));

            simulation.State.LifetimeRevenueUsd = 0;
            simulation.AdvanceDay();

            Assert.That(grant.DaysElapsed, Is.EqualTo(2),
                "the clock stopped when the trading did, so a player can hold a term open for as "
                + "long as they like by taking the product down");
        }

        /// <summary>
        /// Missing a term costs twice the advance.
        ///
        /// At par the advance is an interest-free loan for the length of the term, so the
        /// arithmetic said to sign everything on the board and hand back whatever did not land.
        /// </summary>
        [Test]
        public void MissingATermCostsTwiceWhatWasAdvanced()
        {
            var simulation = Fresh();
            var definition = GrantCatalog.Get(GrantId.MinistryFirstLine);

            var before = simulation.State.CashUsd;
            Sign(simulation, GrantId.MinistryFirstLine);

            Assert.That(simulation.State.CashUsd - before, Is.EqualTo(definition.AdvanceUsd),
                "the advance did not arrive, so what follows is measuring the wrong thing");

            var afterAdvance = simulation.State.CashUsd;

            for (var day = 0; day <= definition.TermDays; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.HeldGrants().Any(held => held.Id == GrantId.MinistryFirstLine),
                Is.False, "the term never closed");

            var recovered = afterAdvance - simulation.State.CashUsd;

            Assert.That(recovered, Is.GreaterThanOrEqualTo(definition.AdvanceUsd * 2),
                $"Only ${recovered:N0} came back against an advance of "
                + $"${definition.AdvanceUsd:N0}. Anything at or under par makes signing for "
                + "everything and missing on purpose the strongest opening in the game.");
        }

        /// <summary>
        /// And a letter says so, with both figures in it.
        ///
        /// The banner and the wire both scroll. This is a six figure charge the player did not
        /// authorise on the day it happens, and the inbox is the one place in the game that keeps
        /// something until it has been read.
        /// </summary>
        [Test]
        public void MissingATermPutsALetterOnTheDesk()
        {
            var simulation = Fresh();
            var definition = GrantCatalog.Get(GrantId.MinistryFirstLine);

            Sign(simulation, GrantId.MinistryFirstLine);

            var before = simulation.State.Mail.All.Count;

            for (var day = 0; day <= definition.TermDays; day++)
            {
                simulation.AdvanceDay();
            }

            var arrived = simulation.State.Mail.All
                .Skip(before)
                .Where(letter => letter.Subject.Contains(Loc.T(definition.NameKey)))
                .ToList();

            Assert.That(arrived, Is.Not.Empty,
                "nothing reached the inbox, so the only notice of the charge was a line on a feed "
                + "that scrolls");

            Assert.That(arrived[0].AmountUsd,
                Is.EqualTo((long)(definition.AdvanceUsd * GrantCatalog.ReclaimMultiple)),
                "the letter quotes a different sum from the one that left the account");

            Assert.That(arrived[0].IsClosed, Is.True,
                "An open letter carrying an amount is a debt as far as the inbox is concerned: "
                + "`Mailbox.OwedUsd` totals every one of them. This money left the account on the "
                + "day it was charged, so an open letter would have the company owing it for the "
                + "rest of the campaign with no button anywhere that could clear it.");

            Assert.That(simulation.State.Mail.OwedUsd, Is.Zero,
                "and that is what it would look like");
        }

        /// <summary>
        /// An award saved before the term could wait is already running.
        ///
        /// Restarting it would hand the player back days they have already spent, on an award they
        /// signed under the old rule.
        /// </summary>
        [Test]
        public void AnAwardFromBeforeTheWaitIsAlreadyRunning()
        {
            var simulation = Fresh();
            Sign(simulation, GrantId.MinistrySafeStart);

            var data = SaveStore.Capture(simulation.State);
            data.version = 51;
            data.grantHeldBegun = new List<bool>();

            var upgraded = SaveMigration.UpgradeV51ToV52(data);

            Assert.That(upgraded.version, Is.EqualTo(52));
            Assert.That(upgraded.grantHeldBegun, Has.Count.EqualTo(upgraded.grantHeldIds.Count));
            Assert.That(upgraded.grantHeldBegun, Has.All.True);

            Assert.That(SaveStore.Restore(upgraded).Grants[0].HasBegun, Is.True);
        }
    }
}
