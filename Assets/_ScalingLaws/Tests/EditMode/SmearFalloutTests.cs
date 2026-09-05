using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What happens to a company that is caught paying for a smear.
    ///
    /// **The reported fault was "it does nothing but drop reputation", and that was exactly true.**
    /// The lab being lied about never wrote, never rang and never went near a court, so the loudest
    /// thing a player can do to a rival was the one with nobody on the other end of it.
    ///
    /// Everything here is about the traced path. A campaign that lands still costs the relationship
    /// because they know, and knowing is not proving: no letter, no case. Holding those apart is
    /// what stops a backfire being the same penalty charged twice, and the first test says so.
    /// </summary>
    public sealed class SmearFalloutTests
    {
        private const CompetitorId Target = CompetitorId.OpenAi;

        private static CompanySimulation Fresh(uint seed = 0x5A1Eu) =>
            new(new CompanyState("Smears", seed));

        /// <summary>
        /// Smears the same lab until one is traced back, or gives up.
        ///
        /// **Drives the real call rather than reaching in.** The backfire is a roll keyed on the day
        /// and the target, so the honest way to a traced campaign is to keep paying for them the way
        /// a player would; the quiet period is what the day loop is here to run out.
        /// </summary>
        private static bool SmearUntilCaught(CompanySimulation simulation, SmearTier tier,
            int mostDays = 4000)
        {
            simulation.State.CashUsd = 40_000_000_000L;

            for (var day = 0; day < mostDays; day++)
            {
                if (simulation.CanSmear(Target, out _)
                    && simulation.TrySmear(Target, tier, out var backfired, out _)
                    && backfired)
                {
                    return true;
                }

                simulation.AdvanceDay();
                simulation.State.CashUsd = 40_000_000_000L;
            }

            return false;
        }

        [Test]
        public void ACampaignThatLandsBringsNoLetterAndNoCase()
        {
            var simulation = Fresh();

            // The quietest tier, so the overwhelming majority of attempts land rather than being
            // traced, and enough days for the thirty day quiet period to run out between them. The
            // first pass counted attempts rather than days and spent all twelve of them waiting out
            // one backfire, so it skipped itself and tested nothing.
            for (var day = 0; day < 900; day++)
            {
                simulation.State.CashUsd = 40_000_000_000L;

                // What was open before this campaign, so a threat left over from an earlier one
                // is not mistaken for this one's. The first version of this test asked for no
                // threat at all and caught a spent one from thirty days earlier, which is the
                // object staying on the state after being answered rather than a fault.
                var standing = simulation.State.SmearThreat;
                var letters = simulation.State.Mail.All
                    .Count(item => item.Kind == MailKind.LegalThreat);

                if (simulation.CanSmear(Target, out _)
                    && simulation.TrySmear(Target, SmearTier.Whisper, out var backfired, out _)
                    && !backfired)
                {
                    Assert.That(simulation.State.SmearThreat, Is.SameAs(standing),
                        "nobody files a case on a suspicion, and a landed campaign is a suspicion");

                    Assert.That(simulation.State.Mail.All
                        .Count(item => item.Kind == MailKind.LegalThreat),
                        Is.EqualTo(letters), "a landed campaign brings no letter");

                    return;
                }

                simulation.AdvanceDay();
            }

            Assert.Fail("Nothing landed in nine hundred days at a six per cent backfire rate.");
        }

        [Test]
        public void BeingTracedBackPutsTheirLawyersInTheInbox()
        {
            var simulation = Fresh();

            Assert.That(SmearUntilCaught(simulation, SmearTier.Campaign), Is.True,
                "the loudest tier is traced back four times in ten and this ran for years");

            var threat = simulation.State.SmearThreat;

            Assert.That(threat, Is.Not.Null);
            Assert.That(threat.Lab, Is.EqualTo(Target));
            Assert.That(threat.IsAnswered, Is.False);
            Assert.That(threat.SettlementUsd, Is.GreaterThan(0L));

            var letter = simulation.State.Mail.All
                .FirstOrDefault(item => item.Kind == MailKind.LegalThreat);

            Assert.That(letter, Is.Not.Null, "the threat is a letter, because it wants an answer");
            Assert.That(letter.Id, Is.EqualTo(threat.MailId));
            Assert.That(letter.AmountUsd, Is.EqualTo(threat.SettlementUsd));
            Assert.That(letter.Actions, Does.Contain(MailAction.Pay));
            Assert.That(letter.Actions, Does.Contain(MailAction.Decline));
        }

        [Test]
        public void SettlingCostsTheMoneyAndEndsIt()
        {
            var simulation = Fresh();

            Assert.That(SmearUntilCaught(simulation, SmearTier.Campaign), Is.True);

            var threat = simulation.State.SmearThreat;
            var owed = threat.SettlementUsd;
            var before = simulation.State.CashUsd;

            Assert.That(simulation.TryActOnMail(threat.MailId, MailAction.Pay, out var why),
                Is.True, why);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - owed));
            Assert.That(simulation.State.SmearThreat.IsAnswered, Is.True);

            // A year of days, which is well past the letter's own thirty. Nothing may arrive.
            for (var day = 0; day < 365; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.Lawsuits.Any(suit => suit.AgainstUs), Is.False,
                "a settlement that can still be followed by a case is not a settlement");
        }

        /// <summary>
        /// Refusing is a gamble rather than a delayed bill.
        ///
        /// Both outcomes are legal, so the assertion is that the threat is closed and that whatever
        /// case exists is one the company is defending. A test that demanded a case would be
        /// asserting the seed rather than the rule.
        /// </summary>
        [Test]
        public void RefusingClosesTheLetterAndMayPutItInFrontOfACourt()
        {
            var simulation = Fresh();

            Assert.That(SmearUntilCaught(simulation, SmearTier.Campaign), Is.True);

            var threat = simulation.State.SmearThreat;
            var before = simulation.State.CashUsd;

            Assert.That(simulation.TryActOnMail(threat.MailId, MailAction.Decline, out var why),
                Is.True, why);

            Assert.That(simulation.State.SmearThreat.IsAnswered, Is.True);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(before),
                "refusing costs nothing today, which is the whole reason it is tempting");

            foreach (var suit in simulation.State.Lawsuits.Where(suit => suit.AgainstUs))
            {
                Assert.That(suit.Target, Is.EqualTo(Target));
                Assert.That(suit.DamagesDemandedUsd, Is.GreaterThan(0L));
            }
        }

        /// <summary>
        /// Saying nothing is answered for. The clock runs out and they decide without you.
        /// </summary>
        [Test]
        public void IgnoringTheLetterStillEndsInADecision()
        {
            var simulation = Fresh();

            Assert.That(SmearUntilCaught(simulation, SmearTier.Campaign), Is.True);

            var mailId = simulation.State.SmearThreat.MailId;

            for (var day = 0; day <= SmearThreat.AnswerDays; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.SmearThreat.IsAnswered, Is.True,
                "an open threat must not sit unanswered forever, or the roll never happens");

            Assert.That(simulation.State.Mail.TryGet(mailId, out var letter), Is.True);
            Assert.That(letter.IsClosed, Is.True);
        }

        /// <summary>
        /// A case the company is defending is heard the same way, and the money goes the other way.
        ///
        /// Every outcome is allowed; what is not allowed is a defended case paying the company.
        /// </summary>
        [Test]
        public void ACaseFiledAgainstTheCompanyNeverPaysIt()
        {
            var simulation = Fresh();
            simulation.State.CashUsd = 40_000_000_000L;

            simulation.State.Lawsuits.Add(new Lawsuit(Target, simulation.State.Date,
                90_000_000L, 0L, "suit.grounds.smear", true));

            var before = simulation.State.CashUsd;

            for (var day = 0; day <= Lawsuit.DaysInCourt; day++)
            {
                simulation.AdvanceDay();
                simulation.State.CashUsd = before;
            }

            var suit = simulation.State.Lawsuits.Single(entry => entry.AgainstUs);

            Assert.That(suit.IsClosed, Is.True, "a case must be decided on the day it closes");
            Assert.That(suit.AwardedUsd, Is.GreaterThanOrEqualTo(0L));

            Assert.That(suit.Verdict, Is.Not.EqualTo(LawsuitVerdict.Pending));
        }

        /// <summary>
        /// The three sums the catalog derives, and the order they have to be in.
        ///
        /// Settling has to be the cheap answer or nobody takes it, and a louder campaign has to be
        /// worth more to a court or the tiers stop meaning anything on this axis too.
        /// </summary>
        [Test]
        public void SettlingIsAlwaysCheaperThanBeingSuedAndLouderCostsMore()
        {
            foreach (var definition in SmearCatalog.All)
            {
                Assert.That(SmearCatalog.SettlementFor(definition.Tier),
                    Is.LessThan(SmearCatalog.DamagesFor(definition.Tier)),
                    $"{definition.Tier}: settling has to be the cheap answer");
            }

            Assert.That(SmearCatalog.DamagesFor(SmearTier.Campaign),
                Is.GreaterThan(SmearCatalog.DamagesFor(SmearTier.Whisper)));

            Assert.That(SmearCatalog.SettlementFor(SmearTier.Campaign),
                Is.GreaterThan(SmearCatalog.SettlementFor(SmearTier.Whisper)));
        }

        /// <summary>
        /// Silence is punished harder than a refusal, because a refusal is an answer.
        /// </summary>
        [Test]
        public void SayingNothingIsWorseOddsThanSayingNo()
        {
            Assert.That(SmearCatalog.SuitChanceAfterSilence,
                Is.GreaterThan(SmearCatalog.SuitChanceAfterRefusal));

            Assert.That(SmearCatalog.SuitChanceAfterSilence, Is.LessThan(1.0),
                "refusing must stay a gamble rather than a delayed bill");
        }
    }
}
