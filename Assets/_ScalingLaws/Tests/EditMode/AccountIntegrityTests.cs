using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Duplicate accounts: the arithmetic, and whether the money actually moves.
    ///
    /// **The two levers here are deliberately not a weak one and a strong one**, and most of this
    /// fixture exists to hold that shape. The ladder is bought with research and finds the
    /// duplicates that are costing money. The ban is bought with cash alone, catches more of them,
    /// and cannot tell a duplicate that is costing money from one that is paying, so it turns
    /// paying customers away and charges the company's standing every day it is on.
    ///
    /// If either of those two halves stops being true, one of them becomes the obvious answer and
    /// the other becomes dead content. That is the same rule `NoChannelIsSimplyBetterThanAnother`
    /// and `NoSmearTierIsSimplyBetterThanAnother` hold for the marketing channels and the smear
    /// tiers, and it is the third place in this project that needs it.
    /// </summary>
    [TestFixture]
    public sealed class AccountIntegrityTests
    {
        private const int FullLadder = AccountIntegrity.MostDetectionLevels;

        // ---- the ladder --------------------------------------------------------------------

        /// <summary>
        /// Every level is worth more than the one below it, and the last one lands on the stated
        /// ceiling rather than near it.
        ///
        /// A ladder whose steps are not monotonic is a ladder with a level nobody should buy, and
        /// a ceiling that is wherever the steps happened to stop is a number nobody chose.
        /// </summary>
        [Test]
        public void TheLadderClimbsAndStopsExactlyWhereItSaysItStops()
        {
            Assert.AreEqual(0.0, AccountIntegrity.DetectionAt(0), 1e-9,
                "Level zero has to be nothing, or a company that has done nothing is already ahead.");

            for (var level = 1; level <= FullLadder; level++)
            {
                Assert.That(AccountIntegrity.DetectionAt(level),
                    Is.GreaterThan(AccountIntegrity.DetectionAt(level - 1)),
                    $"Level {level} buys no more than level {level - 1}, so nobody should buy it.");
            }

            Assert.AreEqual(AccountIntegrity.MostDetectionCut,
                AccountIntegrity.DetectionAt(FullLadder), 1e-9,
                "The top of the ladder is not the ceiling the catalog documents.");

            Assert.AreEqual(AccountIntegrity.MostDetectionCut,
                AccountIntegrity.DetectionAt(FullLadder + 50), 1e-9,
                "Asking past the top has to answer the top rather than climbing forever.");
        }

        /// <summary>
        /// The ladder cuts the duplicates that cost money and leaves the ones that are paying.
        ///
        /// **This is the whole reason it is not simply a weaker ban.** There is nothing for a fraud
        /// system to recover from a duplicate subscription: it is paying, and a company that went
        /// looking for it would be spending research to find revenue and throw it away.
        /// </summary>
        [Test]
        public void TheLadderCutsTheDuplicatesThatCostMoneyAndLeavesTheOnesThatPay()
        {
            var nothing = AccountIntegrity.Serving(0, false);
            var full = AccountIntegrity.Serving(FullLadder, false);

            Assert.That(full.FreeExcess, Is.LessThan(nothing.FreeExcess),
                "A fully researched ladder serves as many duplicate free accounts as a company "
                + "that never looked, so the research bought nothing.");

            Assert.AreEqual(nothing.PaidExcess, full.PaidExcess, 1e-9,
                "The ladder moved the paid duplicates. They are paying, and nothing here is "
                + "supposed to turn away a customer the company did not decide to turn away.");
        }

        // ---- the ban -----------------------------------------------------------------------

        /// <summary>
        /// The ban cuts both halves, because linking payment cards cannot tell them apart.
        /// </summary>
        [Test]
        public void TheBanCutsBothBecauseItCannotTellThemApart()
        {
            var nothing = AccountIntegrity.Serving(0, false);
            var banned = AccountIntegrity.Serving(0, true);

            Assert.That(banned.FreeExcess, Is.LessThan(nothing.FreeExcess),
                "The ban did nothing to the free duplicates, which is most of what it is for.");

            Assert.That(banned.PaidExcess, Is.LessThan(nothing.PaidExcess),
                "The ban left the paid duplicates alone, which would make it strictly better than "
                + "the ladder at no cost in customers and the ladder pointless.");
        }

        /// <summary>
        /// The ban suppresses more than the whole ladder does, and therefore has to cost something
        /// every single day it is switched on.
        ///
        /// **Both halves of this are the author's decision and they only work together.** A ban
        /// that is stronger than the full research ladder for a flat fee and no ongoing cost makes
        /// the ladder decoration: every player switches it on in year one and never researches
        /// anything. The ongoing cost is what keeps the ladder the better long-run answer for a
        /// company with paying customers to lose.
        /// </summary>
        [Test]
        public void TheBanBeatsTheLadderOnSuppressionAndPaysForItEveryDay()
        {
            var ladder = AccountIntegrity.Serving(FullLadder, false);
            var ban = AccountIntegrity.Serving(0, true);

            Assert.That(ban.FreeExcess, Is.LessThan(ladder.FreeExcess),
                "The ban suppresses less than the full ladder, so the $1.4M buys a worse outcome "
                + "than research does and nobody would ever switch it on.");

            Assert.That(AccountIntegrity.BanReputationDragPerDay, Is.GreaterThan(0.0),
                "The ban is stronger than the whole ladder and costs the company's standing "
                + "nothing, which makes the ladder dead content.");

            var quiet = Standing.Today(1_000_000.0, 50.0, 0.3, 30, 1.0, 0.2, 1.0, 0.5);
            var checked_ = Standing.Today(1_000_000.0, 50.0, 0.3, 30, 1.0, 0.2, 1.0, 0.5,
                identityChecksOn: true);

            Assert.AreEqual(0.0, quiet.IdentityChecks, 1e-9,
                "A company running no such policy is being charged for one.");

            Assert.That(checked_.IdentityChecks, Is.LessThan(0.0),
                "The policy is on and the driver is not negative, so nothing is being charged.");

            Assert.That(checked_.Total, Is.LessThan(quiet.Total),
                "The drag does not reach the total, so it is a number on a panel rather than a "
                + "cost, and this project has shipped exactly that before.");
        }

        /// <summary>
        /// The two cuts stack on what is left, never by adding, and never reach certainty.
        ///
        /// Added together a full ladder and a ban would come to 140% and the arithmetic would start
        /// handing back fewer accounts than there are people. That is the rule `SafetyModules` is
        /// held to and the reason is identical: a suppression system that reaches 100% is a
        /// mechanic that has stopped existing.
        /// </summary>
        [Test]
        public void TheTwoCutsStackOnWhatIsLeftAndNeverReachCertainty()
        {
            var both = AccountIntegrity.Serving(FullLadder, true);

            Assert.That(both.FreeExcess, Is.GreaterThan(0.0),
                "Everything together removed every duplicate free account, so the mechanic ends "
                + "the moment a player finishes the ladder.");

            var expected = (AccountIntegrity.FreeAccountsPerPerson - 1.0)
                * (1.0 - AccountIntegrity.MostDetectionCut)
                * (1.0 - AccountIntegrity.BanCut);

            Assert.AreEqual(expected, both.FreeExcess, 1e-9,
                "The two cuts are not stacking multiplicatively, so they are being added "
                + "somewhere and the ceiling is no longer the one the catalog states.");

            Assert.That(both.FreeExcess, Is.LessThan(AccountIntegrity.Serving(FullLadder, false).FreeExcess),
                "The ban on top of a full ladder changed nothing, so one of them is being ignored.");
        }

        /// <summary>
        /// Nothing ever serves fewer accounts than there are people.
        ///
        /// One real person is at least one account. A cut that pushed this under one would start
        /// inventing people who are not there, and every user figure in the game is derived from
        /// this, so it would be wrong on every screen at once.
        /// </summary>
        [Test]
        public void NothingEverServesFewerAccountsThanThereArePeople()
        {
            foreach (var level in new[] { -5, 0, 1, FullLadder, FullLadder + 10 })
            {
                foreach (var ban in new[] { false, true })
                {
                    var load = AccountIntegrity.Serving(level, ban);

                    Assert.That(load.Free, Is.GreaterThanOrEqualTo(1.0),
                        $"level {level}, ban {ban}: fewer free accounts than people.");
                    Assert.That(load.Paid, Is.GreaterThanOrEqualTo(1.0),
                        $"level {level}, ban {ban}: fewer subscriptions than people.");
                }
            }
        }

        // ---- the chain ---------------------------------------------------------------------

        private static CompanySimulation Company(double petaflops = 4_000.0)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2023, 6, 1),
                CashUsd = 400_000_000
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            var simulation = new CompanySimulation(state);

            // **A company with no cluster serves nothing, so it pays nothing for anything.** The
            // first version of this fixture built a model and no fleet, and the three chain tests
            // below read a serving bill of exactly zero for both arms: not a wrong answer, an
            // unasked question. Rented rather than owned, because the duplicate problem has
            // nothing to do with where the petaflops come from and renting needs no lead time.
            simulation.SetRentedPetaflops(petaflops);

            return simulation;
        }

        /// <summary>
        /// A generous free tier costs more than the slider says, because it is handed to the
        /// duplicates too.
        ///
        /// **That is the point of putting this on demand rather than on a cost line.** The company
        /// decides what one person gets; the duplicates decide how many people that is handed to,
        /// and nothing in the game said so before.
        /// </summary>
        [Test]
        public void AMoreGenerousFreeTierHandsMoreToTheDuplicatesAsWell()
        {
            var mean = Company();
            mean.State.Monetization.FreeTierTokensPerUserPerDay = 0.0;

            var generous = Company();
            generous.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            Assert.That(generous.AccountInflation(), Is.GreaterThan(mean.AccountInflation()),
                "Two companies with the same duplicate problem and very different free tiers "
                + "serve the same amount of extra demand, so the free tier is not reaching it.");

            Assert.That(generous.DuplicateFreeShareOfTokens(),
                Is.GreaterThan(mean.DuplicateFreeShareOfTokens()),
                "The share of served tokens that nobody is invoiced for and nobody asked for does "
                + "not move with the free tier, which is the one thing it should follow.");
        }

        /// <summary>
        /// The effective free share is not the slider, and the difference is the duplicates.
        ///
        /// Reading `MonetizationPolicy.FreeShareOfTokens` where the tokens are split would charge
        /// the free tier for volume it did not hand out, and the books would then report a giveaway
        /// the company never decided on.
        /// </summary>
        [Test]
        public void TheEffectiveFreeShareIsHigherThanThePolicyWhileDuplicatesAreUnchecked()
        {
            var simulation = Company();
            simulation.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            var policy = simulation.State.Monetization.FreeShareOfTokens;

            Assert.That(simulation.EffectiveFreeShareOfTokens(), Is.GreaterThan(policy),
                "Duplicate free accounts take the allowance again, so more of what arrives is "
                + "free than the company decided to give away.");

            simulation.State.AccountDetectionLevel = FullLadder;
            simulation.State.MultiAccountBanOn = true;

            var tidy = simulation.EffectiveFreeShareOfTokens();

            Assert.That(tidy, Is.GreaterThan(policy),
                "Suppression is not total and never should be, so a little is still above policy.");

            simulation.State.AccountDetectionLevel = 0;
            simulation.State.MultiAccountBanOn = false;

            Assert.That(tidy, Is.LessThan(simulation.EffectiveFreeShareOfTokens()),
                "Doing something about the duplicates did not bring the effective free share "
                + "down, so the suppression is not reaching the token split.");
        }

        /// <summary>
        /// With capacity to spare, duplicates cost a share of a fleet bill that was already paid.
        ///
        /// **This is the finding, and the first version of this test asserted the opposite.** It
        /// claimed the ladder leaves more in the bank and failed with the two companies holding
        /// identical cash to the dollar. The code was right: a rented cluster is paid for by the
        /// petaflop and not by the token, so a company sitting on eight thousand times the
        /// capacity it needs serves its duplicates out of headroom it is already paying for.
        ///
        /// So the figure the panel reports is a share of the fleet bill rather than a new cost, and
        /// it halves when the ladder is finished. That is worth saying on a screen: this much of
        /// your cluster is going to people who already have an account. What it is not is a saving
        /// until the cluster is the thing in short supply, which is the test below.
        /// </summary>
        [Test]
        public void WithCapacityToSpareDuplicatesCostAShareOfTheFleetRatherThanMoreMoney()
        {
            var careless = Company();
            var careful = Company();

            careless.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;
            careful.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            careful.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            careful.State.AccountDetectionLevel = FullLadder;

            for (var day = 0; day < 180; day++)
            {
                careless.AdvanceDay();
                careful.AdvanceDay();
            }

            Assert.That(careless.State.LastQuality.Utilisation, Is.LessThan(0.5),
                "This test is about a company with room to spare, and this one is busy, so it is "
                + "measuring the other case and the assertions below mean something else.");

            var wasted = careless.DuplicateServingLastMonthUsd();

            Assert.That(wasted, Is.GreaterThan(0L),
                "Nothing was ever recorded as going to duplicates, so the panel has no month to "
                + "report and the recording site is not being reached.");

            // **Not half, and the arithmetic is worth writing out because the obvious guess is
            // wrong in both directions.** The ladder takes 60% off the free excess, so the first
            // guess is that the figure falls to 0.40 of itself. It does not: this is a share of
            // what arrived, and suppressing duplicates shrinks the denominator too, so the top and
            // the bottom both fall and the figure lands near 0.50. Measured at 0.5003.
            //
            // The bound is derived rather than typed, so moving `MostDetectionCut` moves the test
            // with it instead of leaving a magic fraction behind.
            var excess = AccountIntegrity.FreeAccountsPerPerson - 1.0;
            var kept = excess * (1.0 - AccountIntegrity.MostDetectionCut);
            var policy = careless.State.Monetization.FreeShareOfTokens;
            var ratio = (policy * kept / (policy * (1.0 + kept) + 1.0 - policy))
                / (policy * excess / (policy * (1.0 + excess) + 1.0 - policy));

            Assert.That(careful.DuplicateServingLastMonthUsd(),
                Is.LessThan((long)(wasted * (ratio + 0.02)) + 1),
                "A finished ladder did not cut what the fleet spends on duplicates by what the "
                + $"catalog's own numbers come to, which is a factor of {ratio:F3}.");

            Assert.That(careful.DuplicateServingLastMonthUsd(),
                Is.GreaterThan((long)(wasted * (ratio - 0.02))),
                "It cut more than the catalog allows, so something else is suppressing duplicates "
                + "as well and the ladder is not the only thing reaching them.");
        }

        /// <summary>
        /// When the cluster is the thing in short supply, duplicates cost real money.
        ///
        /// **This is the half that makes the mechanic a decision**, and it is the same shape as the
        /// surplus finding above it, seen from the other end. A duplicate free account takes a
        /// place in the queue that a paying one would have had, so a company that cannot serve
        /// everybody is giving away the part of its capacity it most needs to sell.
        ///
        /// Measured on a deliberately small fleet, which is the only honest way to reach this
        /// state: the company is at 97% against 95% for the one that did something about it, and
        /// the difference shows up in the bank rather than only on a panel.
        /// </summary>
        [Test]
        public void WhenCapacityIsTheConstraintDuplicatesCostRealMoney()
        {
            var careless = Company(0.25);
            var careful = Company(0.25);

            careless.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;
            careful.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            careful.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            careful.State.AccountDetectionLevel = FullLadder;

            for (var day = 0; day < 180; day++)
            {
                careless.AdvanceDay();
                careful.AdvanceDay();
            }

            Assert.That(careless.State.LastQuality.Utilisation, Is.GreaterThan(0.9),
                "This test is about a company that cannot serve everybody, and this one has room, "
                + "so it is measuring the case above instead.");

            Assert.That(careful.State.CashUsd, Is.GreaterThan(careless.State.CashUsd),
                "Six months of duplicate free accounts crowding out paying ones cost the careless "
                + "company nothing, so the whole mechanic is a figure on a panel.");

            Assert.That(careful.State.LastQuality.Utilisation,
                Is.LessThan(careless.State.LastQuality.Utilisation),
                "Suppressing duplicates did not take any load off the cluster, so they are not "
                + "reaching the queue and the latency everybody else waits in.");
        }

        /// <summary>
        /// Duplicates cost money from the first day, researched or not.
        ///
        /// **The figures are gated and the problem is not.** A real service pays that bill long
        /// before it builds the dashboard, and gating the cost as well would mean a player who
        /// never touches this node plays a game where duplicate accounts do not exist, which is
        /// the opposite of the point.
        /// </summary>
        [Test]
        public void TheBillArrivesBeforeTheDashboardDoes()
        {
            var simulation = Company();
            simulation.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            Assert.IsFalse(simulation.UnderstandsAccounts(),
                "A fresh company already knows how to count duplicates, so there is nothing for "
                + "the node to open.");

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.DuplicateServingLastMonthUsd(), Is.GreaterThan(0L),
                "A company that has not researched recognition is paying nothing for its "
                + "duplicates, so the cost is gated behind the node along with the figures.");
        }

        // ---- the two ways in ---------------------------------------------------------------

        /// <summary>
        /// Neither lever can be pulled before the company can see the problem.
        ///
        /// You cannot ban what you cannot measure, and a refusal here is a sentence rather than a
        /// dead control: `GateNotice` is what the screen does with it.
        /// </summary>
        [Test]
        public void NeitherLeverWorksBeforeTheCompanyCanCountAtAll()
        {
            var simulation = Company();

            Assert.IsFalse(simulation.TryUpgradeAccountDetection(out var ladderWhy));
            Assert.IsNotNull(ladderWhy, "A refusal with no sentence is a button that does nothing.");

            Assert.IsFalse(simulation.TrySetMultiAccountBan(true, out var banWhy));
            Assert.IsNotNull(banWhy, "A refusal with no sentence is a button that does nothing.");

            Assert.IsFalse(simulation.State.MultiAccountBanOn);
            Assert.IsFalse(simulation.State.MultiAccountBanBuilt);
            Assert.AreEqual(0, simulation.State.AccountDetectionLevel);
        }

        /// <summary>
        /// The ban is paid for once, however many times it is switched.
        ///
        /// The firm that links the payment cards builds the integration one time. Charging again on
        /// the way back on would make switching it off a punishment for having tried it, which is
        /// not a trade, and the author asked for a policy that can be turned off.
        /// </summary>
        [Test]
        public void TheBanIsPaidForOnceHoweverManyTimesItIsSwitched()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);

            var opening = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out _));
            var afterFirst = simulation.State.CashUsd;

            Assert.AreEqual(AccountIntegrity.BanCostUsd, opening - afterFirst,
                "The ban did not cost what the catalog says it costs.");

            Assert.IsTrue(simulation.TrySetMultiAccountBan(false, out _));
            Assert.AreEqual(afterFirst, simulation.State.CashUsd,
                "Switching the policy off refunded or charged something. It does neither.");

            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out _));
            Assert.AreEqual(afterFirst, simulation.State.CashUsd,
                "Switching it back on charged for the integration a second time.");
        }

        /// <summary>
        /// Switching the ban on costs paying customers the same day, and nothing had to say so.
        ///
        /// The duplicates simply stop being served, so the user count falls by exactly the
        /// duplicates there were. Some of them were paying. **That is the whole price of the blunt
        /// instrument and it is derived rather than written beside it as a penalty.**
        /// </summary>
        [Test]
        public void SwitchingTheBanOnCostsSubscriptionsBecauseSomeDuplicatesWerePaying()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);

            var before = simulation.AccountsPerPerson().Paid;

            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out _));

            Assert.That(simulation.AccountsPerPerson().Paid, Is.LessThan(before),
                "The ban is on and the company is serving as many subscriptions per head as "
                + "before, so nobody was turned away and the policy is free.");

            Assert.That(simulation.SubscriptionSeatMultiplier(), Is.LessThan(1.0),
                "The second subscriptions stopped being served and the company is still being "
                + "paid for them, so the ban costs no revenue and is a purchase rather than a "
                + "decision.");
        }

        /// <summary>
        /// A company that has done nothing about duplicates is billed exactly as it always was.
        ///
        /// **The neutral-option rule, and this one was learned by measuring.** Billing the
        /// duplicate subscriptions outright paid every scripted operator between 7% and 107% more
        /// than before duplicate accounts existed in the game at all, because a surplus fleet
        /// served the extra subscriptions for nothing. The duplicates were always in the baseline
        /// the whole economy was tuned against; what is new is being able to turn them away.
        ///
        /// So the multiplier is measured from the untouched world and can only ever fall. With the
        /// seats billed from a baseline of one, the seven probe operators came back on exactly the
        /// figures they had with no seat billing at all, to the dollar.
        /// </summary>
        [Test]
        public void ACompanyThatHasDoneNothingIsBilledExactlyAsItAlwaysWas()
        {
            var simulation = Company();
            simulation.State.Monetization.Model = PricingModel.Subscription;

            Assert.AreEqual(1.0, simulation.SubscriptionSeatMultiplier(), 1e-9,
                "A fresh company is billed off its baseline, so every campaign balanced before "
                + "duplicate accounts existed now earns a different amount.");

            simulation.State.AccountDetectionLevel = FullLadder;

            Assert.AreEqual(1.0, simulation.SubscriptionSeatMultiplier(), 1e-9,
                "The recognition ladder cost the company subscription revenue. It only ever "
                + "touches the free duplicates, so there is nothing here for it to take.");
        }

        /// <summary>
        /// Metered pricing bills the tokens, so a duplicate account that uses none pays nothing.
        /// </summary>
        [Test]
        public void MeteredPricingIsNeverBilledPerSeat()
        {
            var simulation = Company();
            simulation.State.Monetization.Model = PricingModel.PayPerToken;
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);

            Assert.AreEqual(1.0, simulation.SubscriptionSeatMultiplier(), 1e-9);

            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out _));

            Assert.AreEqual(1.0, simulation.SubscriptionSeatMultiplier(), 1e-9,
                "A company billing by the token lost revenue to the ban. It was never being paid "
                + "for the second accounts, so there is nothing to lose.");
        }

        /// <summary>
        /// The ladder stops at the top rather than taking money forever.
        /// </summary>
        [Test]
        public void TheLadderRefusesOnceItIsFinished()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            simulation.State.ResearchPoints = 100_000.0;

            for (var level = 0; level < FullLadder; level++)
            {
                Assert.IsTrue(simulation.TryUpgradeAccountDetection(out var why),
                    $"Level {level + 1} was refused: {why}");
            }

            Assert.AreEqual(FullLadder, simulation.State.AccountDetectionLevel);

            var cash = simulation.State.CashUsd;
            Assert.IsFalse(simulation.TryUpgradeAccountDetection(out _),
                "A finished ladder sold another level.");
            Assert.AreEqual(cash, simulation.State.CashUsd,
                "A refused upgrade still took the money.");
        }

        /// <summary>
        /// A level costs research points, which is the currency that makes it a decision.
        ///
        /// Money alone buys the ban. If money alone also bought the ladder there would be one
        /// lever in this mechanic wearing two names.
        /// </summary>
        [Test]
        public void TheLadderCannotBeBoughtWithMoneyAlone()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            simulation.State.ResearchPoints = 0.0;

            Assert.IsFalse(simulation.TryUpgradeAccountDetection(out var why),
                "A company with no research points and a full bank bought a level anyway.");
            Assert.IsNotNull(why);
            Assert.AreEqual(0, simulation.State.AccountDetectionLevel);
        }

        // ---- the save ----------------------------------------------------------------------

        /// <summary>
        /// The policy and the ladder survive a save, and the month of spending survives with them.
        ///
        /// The month is a record: each day's figure is a share of that day's serving bill taken
        /// against that day's free tier and that day's detection level, and none of the three
        /// survives the day. **Twelfth time in this project that something which looked derivable
        /// turned out to be a fact about a day.**
        /// </summary>
        [Test]
        public void TheLadderThePolicyAndTheMonthAllSurviveASave()
        {
            var simulation = Company();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.AccountRecognition);
            simulation.State.ResearchPoints = 100_000.0;
            simulation.State.Monetization.FreeTierTokensPerUserPerDay =
                MonetizationCatalog.GenerousFreeTierTokensPerDay;

            Assert.IsTrue(simulation.TryUpgradeAccountDetection(out _));
            Assert.IsTrue(simulation.TryUpgradeAccountDetection(out _));
            Assert.IsTrue(simulation.TrySetMultiAccountBan(true, out _));

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var spent = simulation.DuplicateServingLastMonthUsd();
            Assert.That(spent, Is.GreaterThan(0L), "Nothing was recorded, so nothing is proven.");

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));
            var after = new CompanySimulation(restored);

            Assert.AreEqual(2, restored.AccountDetectionLevel, "The ladder was not saved.");
            Assert.IsTrue(restored.MultiAccountBanOn, "The policy was not saved.");
            Assert.IsTrue(restored.MultiAccountBanBuilt,
                "The purchase was not saved, so reloading would charge $1.4M again.");
            Assert.AreEqual(spent, after.DuplicateServingLastMonthUsd(),
                "The month of duplicate spending did not survive, so the panel reports an empty "
                + "month to a company that has been paying it for weeks.");
        }

        /// <summary>
        /// A save claiming the policy is on without having paid for it is read as off.
        ///
        /// The one combination of these two flags nothing in the game can produce, so an edited
        /// file is the only way to get it, and a company running a policy it never bought is the
        /// wrong way to resolve that.
        /// </summary>
        [Test]
        public void APolicyThatWasNeverPaidForIsReadAsOff()
        {
            var simulation = Company();
            var data = SaveStore.Capture(simulation.State);

            data.multiAccountBanOn = true;
            data.multiAccountBanBuilt = false;

            var restored = SaveStore.Restore(data);

            Assert.IsFalse(restored.MultiAccountBanOn,
                "A file saying the ban is running without the integration having been paid for was "
                + "taken at its word.");
        }
    }
}
