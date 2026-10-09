using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Relations that can go up, and what a company signs when they do.
    ///
    /// **Every way a relation moved before this was something the player did to somebody.** Seven
    /// recorders, all negative, so a player who never attacked anybody sat at Neutral with fourteen
    /// labs for fourteen years and a good relation bought nothing, because there was no way to have
    /// one. These hold the other direction, and the two rules that keep it from being a purchase:
    /// an offer is answered in days rather than on the click, and a level is earned in days that
    /// cannot be paid for.
    /// </summary>
    public sealed class AllianceTests
    {
        private static CompanySimulation Company(uint seed = 31)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.State.CashUsd = 200_000_000;
            simulation.State.ResearchPoints = 5_000;
            simulation.SetRentedPetaflops(200.0);

            return simulation;
        }

        private static void Warm(CompanySimulation simulation, CompetitorId lab, double to)
        {
            simulation.State.Relations.Record(lab, simulation.State.Date,
                to - simulation.State.Relations.With(lab), "relation.reason.published");
        }

        /// <summary>
        /// The band a fresh campaign is in with everybody, which has to be the one it is named.
        ///
        /// **It was not.** `Neutral` is documented as where everybody starts and its threshold sat
        /// at 5.0 against a start of 0.0, so all fourteen labs were drawn as `Tense` on day one:
        /// every company the player had never touched reading as cooling. Nothing caught it because
        /// nothing until now asked a question the answer mattered to. An offer that needs Neutral
        /// could not be made to anybody, ever, which is how it surfaced.
        /// </summary>
        [Test]
        public void EverybodyStartsInTheBandThatIsNamedAfterStarting()
        {
            Assert.That(RelationScale.BandFor(RelationScale.Start), Is.EqualTo(RelationBand.Neutral));

            Assert.That(RelationScale.BandFor(RelationScale.CousinBaseline),
                Is.EqualTo(RelationBand.Friendly),
                "and the cousin starts as family");

            var simulation = Company();

            Assert.That(simulation.State.Relations.BandWith(CompetitorId.Cohere),
                Is.EqualTo(RelationBand.Neutral),
                "a company on its first morning has not annoyed anybody");
        }

        [Test]
        public void AnOfferIsPaidForOnSendingAndAnsweredDaysLater()
        {
            var simulation = Company();
            var before = simulation.State.ResearchPoints;

            Assert.IsTrue(simulation.TrySendOffer(
                CompetitorId.Cohere, RelationOffer.PublishFinding, out var why), why);

            Assert.That(simulation.State.ResearchPoints,
                Is.EqualTo(before - RelationOfferCatalog.Get(RelationOffer.PublishFinding).PointCost),
                "the cost is paid on sending, whatever they answer");

            Assert.That(simulation.State.PendingOffers.Count, Is.EqualTo(1),
                "and it is waiting, not decided");

            for (var day = 0; day < RelationOfferCatalog.Get(RelationOffer.PublishFinding)
                     .DaysToAnswer + 1; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.PendingOffers, Is.Empty, "nobody answered");
        }

        /// <summary>
        /// **The cheap rung has to stay reachable**, so a published finding is never refused and it
        /// reaches everybody. It is the only move a company with no money can make, and a relation
        /// system whose first rung costs a million dollars is one the early game cannot see.
        /// </summary>
        [Test]
        public void APublishedFindingIsNeverRefusedAndEverybodyNoticesALittle()
        {
            Assert.That(
                RelationOfferCatalog.AcceptanceChance(
                    RelationOffer.PublishFinding, RivalRelations.Worst, 90.0, 1.0),
                Is.EqualTo(1.0),
                "nobody turns down reading a paper, even a lab that hates you");

            var simulation = Company();

            Assert.IsTrue(simulation.TrySendOffer(
                CompetitorId.Cohere, RelationOffer.PublishFinding, out var why), why);

            Assert.That(simulation.State.Relations.With(CompetitorId.OpenAi),
                Is.GreaterThan(RivalRelations.Start),
                "a paper is public, so a lab it was not aimed at still read it");

            for (var day = 0; day < 6; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.Relations.With(CompetitorId.Cohere),
                Is.GreaterThan(RivalRelations.Start),
                "and the lab it was published with gains more than the rest");
        }

        /// <summary>
        /// A lab well ahead of you has less to gain and says so. Nothing new is invented for that:
        /// it reads the relation and the capability gap, both of which the game already keeps.
        /// </summary>
        [Test]
        public void ALabFarAheadIsHarderToSignThanOneBehind()
        {
            var ahead = RelationOfferCatalog.AcceptanceChance(
                RelationOffer.DistributionLicence, 50.0, theirCapability: 80.0, yourCapability: 20.0);

            var behind = RelationOfferCatalog.AcceptanceChance(
                RelationOffer.DistributionLicence, 50.0, theirCapability: 30.0, yourCapability: 60.0);

            Assert.That(behind, Is.GreaterThan(ahead));

            var hated = RelationOfferCatalog.AcceptanceChance(
                RelationOffer.DistributionLicence, -80.0, 40.0, 40.0);

            var liked = RelationOfferCatalog.AcceptanceChance(
                RelationOffer.DistributionLicence, 80.0, 40.0, 40.0);

            Assert.That(liked, Is.GreaterThan(hated * 2.0),
                "where the relation stands is the strongest term, which is the whole point");
        }

        /// <summary>
        /// **The calendar on an alliance cannot be bought.** Level three needs a year at level two,
        /// and a company with two hundred million dollars is refused exactly as a poor one is. That
        /// is the spine of this game applied to a relationship.
        /// </summary>
        [Test]
        public void ALevelIsEarnedInDaysAndMoneyCannotBringItForward()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.FriendlyAbove + 20.0);

            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);
            Assert.That(simulation.State.Alliances.LevelWith(lab), Is.EqualTo(1));

            Assert.IsFalse(simulation.TrySignAlliance(lab, out var tooSoon),
                "the second level is ninety days away and the account is full");

            StringAssert.Contains(
                Loc.T("alliance.fail.days", LabAlliances.DaysNeededFor(2).ToString()).Substring(0, 6),
                tooSoon);

            for (var day = 0; day < LabAlliances.DaysNeededFor(2) + 1; day++)
            {
                // Kept warm on purpose: the drift would take an untended relation back to neutral
                // and the level would fall, which is the next test rather than this one.
                Warm(simulation, lab, RivalRelations.FriendlyAbove + 20.0);
                simulation.AdvanceDay();
            }

            Assert.IsTrue(simulation.TrySignAlliance(lab, out var later), later);
            Assert.That(simulation.State.Alliances.LevelWith(lab), Is.EqualTo(2));
        }

        /// <summary>
        /// An alliance nobody keeps warm falls apart, and it falls faster than it climbed.
        ///
        /// **Derived from the band rather than hooked onto every hostile act.** Six places in this
        /// simulation charge a relation and a seventh will be written one day; a rule that has to be
        /// remembered at each of them is a rule that gets forgotten at one.
        /// </summary>
        [Test]
        public void AnAllianceLeftToCoolLosesItsLevelWithoutAnybodyCallingIn()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.FriendlyAbove + 5.0);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);

            // One smear's worth of damage, recorded the way every hostile act in the game does.
            simulation.State.Relations.Record(lab, simulation.State.Date, -60.0,
                "relation.reason.smeared", "Adco");

            simulation.AdvanceDay();

            Assert.That(simulation.State.Alliances.LevelWith(lab), Is.Zero,
                "nothing in the smear code knows about alliances, and it did not have to");

            Assert.IsTrue(simulation.State.Alliances.CanCall(lab),
                "the number survives the friendship, which is the author's own rule");
        }

        [Test]
        public void ADealRunsForItsTermAndThenStopsMattering()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);

            simulation.State.Deals.Add(new StandingDeal(lab, RelationOffer.CapacityPurchase,
                simulation.State.Date, simulation.State.Date.AddDays(10)));

            Assert.That(simulation.AlliedPetaflops(), Is.GreaterThan(0.0));

            for (var day = 0; day < 12; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.Deals, Is.Empty);
            Assert.That(simulation.AlliedPetaflops(), Is.Zero,
                "a term that ran out must stop paying, or it is an income guarantee");
        }

        /// <summary>
        /// Everything here is causal, so all of it is saved. Eleventh time in this project.
        /// </summary>
        [Test]
        public void OffersDealsAndAlliancesAllSurviveASave()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.FriendlyAbove + 20.0);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);
            Assert.IsTrue(simulation.TrySendOffer(
                CompetitorId.OpenAi, RelationOffer.CapacityPurchase, out var offerWhy), offerWhy);

            simulation.State.Deals.Add(new StandingDeal(lab, RelationOffer.DistributionLicence,
                simulation.State.Date, simulation.State.Date.AddDays(100)));

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.That(back.Alliances.LevelWith(lab), Is.EqualTo(1));
            Assert.IsTrue(back.Alliances.CanCall(lab));
            Assert.That(back.PendingOffers.Count, Is.EqualTo(1));
            Assert.That(back.PendingOffers[0].Lab, Is.EqualTo(CompetitorId.OpenAi));
            Assert.That(back.Deals.Count, Is.EqualTo(1));
            Assert.That(back.Deals[0].Offer, Is.EqualTo(RelationOffer.DistributionLicence));
        }

        /// <summary>
        /// v65 to v66: nothing signed, nothing waiting, and the relations kept exactly as they are.
        /// </summary>
        [Test]
        public void AnOlderCampaignKeepsItsEnemiesAndHasNothingSigned()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 65;
            data.allianceLabs = null;
            data.offerLabs = null;

            var upgraded = SaveMigration.UpgradeV65ToV66(data);

            Assert.That(upgraded.version, Is.EqualTo(66));
            Assert.That(upgraded.allianceLabs, Is.Empty);
            Assert.That(upgraded.offerLabs, Is.Empty);
            StringAssert.Contains("v65 to v66", SaveMigration.LastMigrationNotes);
        }

        /// <summary>
        /// **Nothing here pays a dividend**, and that is the line this system is not allowed to
        /// cross. Everything an alliance buys is points, capacity, reach or a smaller chance of
        /// something going wrong. A relation that paid money would be an income guarantee, which is
        /// against the spine of the game.
        /// </summary>
        [Test]
        public void NoOfferEverPutsMoneyIntoTheAccount()
        {
            foreach (var definition in RelationOfferCatalog.All)
            {
                Assert.That(definition.CashCostUsd, Is.GreaterThanOrEqualTo(0),
                    $"{definition.Offer} pays the company to accept it");
            }

            var simulation = Company();
            var before = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TrySendOffer(
                CompetitorId.Cohere, RelationOffer.CapacityPurchase, out var why), why);

            Assert.That(simulation.State.CashUsd, Is.LessThan(before));
        }

        /// <summary>
        /// Both ways into this system are reachable from a screen.
        ///
        /// **This project has shipped thirteen mechanisms a player could not reach**, and the last
        /// one was a research node that cost three million dollars and was read by no caller. The
        /// sweep is crude on purpose: it proves the name appears in `Scripts/UI/`, not that a click
        /// arrives, which is what the render is for.
        /// </summary>
        [Test]
        public void TheOfferAndTheSigningAreBothReachableFromTheInterface()
        {
            var root = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "_ScalingLaws", "Scripts", "UI");

            var code = string.Join("\n",
                System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories)
                    .Select(System.IO.File.ReadAllText));

            foreach (var name in new[] { "TrySendOffer", "TrySignAlliance" })
            {
                StringAssert.Contains(name, code,
                    $"{name} is complete in Simulation and no screen calls it, which is the "
                    + "fault this project has recorded thirteen times");
            }
        }


        /// <summary>
        /// The consortium's whole claim: the money goes two to three times further inside one.
        ///
        /// **The author gave this number and this is it measured from the other side.** Two members
        /// each pay sixty per cent of what one would and the room makes 2.2 times the points, so a
        /// dollar buys about three and two thirds of what it buys alone. Reaching the same place by
        /// yourself costs about three and a half times as much.
        /// </summary>
        [Test]
        public void TheMoneyGoesAboutThreeTimesFurtherInsideAConsortium()
        {
            var two = ResearchCampaignCatalog.ValueMultiple(2);
            var three = ResearchCampaignCatalog.ValueMultiple(3);

            Assert.That(two, Is.GreaterThan(2.0),
                "the author asked for two to three times, and under two it is not worth signing");

            Assert.That(three, Is.GreaterThan(two),
                "a third member has to be worth asking");

            Assert.That(ResearchCampaignCatalog.PointsMultiplier(3),
                Is.LessThan(ResearchCampaignCatalog.PointsMultiplier(2) * 1.5),
                "and sublinear, or a full room is simply the correct answer and who you ask stops "
                + "mattering");
        }

        /// <summary>
        /// **Nine months of somebody liking you, and no way to pay it down.** The working-group
        /// level is a hundred and eighty days at level one, which is itself ninety days of Friendly.
        /// A company with two hundred million dollars is refused exactly as a poor one is.
        /// </summary>
        [Test]
        public void ACompanyWithNoAlliesCannotOpenAProgrammeAtAnyPrice()
        {
            var simulation = Company();
            simulation.State.CashUsd = 5_000_000_000;

            Assert.IsFalse(simulation.TryStartCampaign(CampaignTerm.Quarter,
                new[] { CompetitorId.Cohere }, out var why));

            StringAssert.Contains(CompetitorCatalog.NameOf(CompetitorId.Cohere), why,
                "the refusal names the lab that is not far enough along");

            StringAssert.Contains(
                ResearchCampaignCatalog.NeedsAllianceLevel.ToString(), why,
                "and the level it would have to reach, because that is the answer to \"why not\"");
        }

        [Test]
        public void APointOfTheProgrammeArrivesEveryDayRatherThanAtTheEnd()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Ally(simulation, lab, ResearchCampaignCatalog.NeedsAllianceLevel);

            var before = simulation.State.ResearchPoints;

            Assert.IsTrue(simulation.TryStartCampaign(CampaignTerm.Quarter, new[] { lab },
                out var why), why);

            simulation.AdvanceDay();

            var afterOneDay = simulation.State.ResearchPoints;

            Assert.That(afterOneDay, Is.GreaterThan(before),
                "a laboratory that has run for a day has learned a day of things");

            for (var day = 0; day < 10; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.ResearchPoints, Is.GreaterThan(afterOneDay),
                "and it keeps arriving");
        }

        /// <summary>
        /// Walking out early forfeits the term and costs the fee.
        ///
        /// **Without it, joining and leaving on the last profitable day is the dominant line** and
        /// every programme in the game would be taken and abandoned.
        /// </summary>
        [Test]
        public void WalkingOutEarlyCostsMoreThanSittingOutTheTerm()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Ally(simulation, lab, ResearchCampaignCatalog.NeedsAllianceLevel);

            Assert.IsTrue(simulation.TryStartCampaign(CampaignTerm.Year, new[] { lab },
                out var why), why);

            var before = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryLeaveCampaign(out var leaveWhy), leaveWhy);

            Assert.That(simulation.State.CashUsd, Is.LessThan(before),
                "the break fee is charged on the way out");

            Assert.IsNull(simulation.State.Campaign);
        }

        /// <summary>
        /// A programme cannot outlive the alliance holding it up.
        ///
        /// Read from the alliance level rather than hooked onto the break, which is the same rule
        /// the levels themselves follow: one place decides an alliance has ended.
        /// </summary>
        [Test]
        public void AProgrammeCollapsesWithTheAllianceBehindIt()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Ally(simulation, lab, ResearchCampaignCatalog.NeedsAllianceLevel);

            Assert.IsTrue(simulation.TryStartCampaign(CampaignTerm.Half, new[] { lab },
                out var why), why);

            // One smear's worth, recorded exactly the way the smear code records it.
            simulation.State.Relations.Record(lab, simulation.State.Date, -80.0,
                "relation.reason.smeared", "Adco");

            simulation.AdvanceDay();

            Assert.IsNull(simulation.State.Campaign,
                "nothing in the smear code knows about research programmes, and it did not have to");
        }

        [Test]
        public void AProgrammeSurvivesASaveWithEverybodyStillInTheRoom()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Ally(simulation, lab, ResearchCampaignCatalog.NeedsAllianceLevel);

            Assert.IsTrue(simulation.TryStartCampaign(CampaignTerm.Year, new[] { lab },
                out var why), why);

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.IsNotNull(back.Campaign);
            Assert.That(back.Campaign.Term, Is.EqualTo(CampaignTerm.Year));
            Assert.That(back.Campaign.Members, Has.Count.EqualTo(1));
            Assert.That(back.Campaign.Members[0], Is.EqualTo(lab));
        }

        /// <summary>Puts a lab at an alliance level without waiting the calendar out.</summary>
        private static void Ally(CompanySimulation simulation, CompetitorId lab, int level)
        {
            simulation.State.Relations.Record(lab, simulation.State.Date,
                RivalRelations.Best - simulation.State.Relations.With(lab),
                "relation.reason.published");

            for (var step = 0; step < level; step++)
            {
                simulation.State.Alliances.Sign(lab, simulation.State.Date);
            }
        }


        /// <summary>
        /// **Drawing a card must not change the company.** This is a ratchet for a shipped bug.
        ///
        /// The rival card called `TrySignAlliance` to read the refusal out of its `out` parameter,
        /// which is fine on a card that cannot sign and signs the alliance on a card that can. A
        /// render caught it: a company at level one came back from being looked at sitting at level
        /// two, with the fee gone from the account and an event on the wire nobody had asked for.
        ///
        /// A `Try` method is a write however harmless its out parameter looks. `WhyNotAlliance`
        /// exists because a screen needs the sentence without the signing.
        /// </summary>
        [Test]
        public void ReadingWhyAnAllianceIsBlockedNeverSignsIt()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.FriendlyAbove + 20.0);

            var cash = simulation.State.CashUsd;
            var level = simulation.State.Alliances.LevelWith(lab);

            for (var look = 0; look < 5; look++)
            {
                simulation.WhyNotAlliance(lab);
            }

            Assert.That(simulation.State.Alliances.LevelWith(lab), Is.EqualTo(level),
                "looking at the card signed the alliance");

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cash),
                "and charged the fee for it");

            Assert.That(simulation.State.HasQueuedEvents, Is.False,
                "and told the player it had happened");

            // And the sentence is still there to read when it genuinely cannot be signed.
            Warm(simulation, lab, RivalRelations.Start);

            Assert.That(simulation.WhyNotAlliance(lab), Is.Not.Empty);
        }

        /// <summary>
        /// A signed level makes an offer land more often, which is what level one is for.
        ///
        /// Before this, levels one and three moved no number in the game at all: one set a flag for
        /// a telephone nothing calls yet and three did nothing whatsoever. A ladder whose middle
        /// rung is the only real one is a ladder with two decorations on it.
        /// </summary>
        [Test]
        public void EachSignedLevelIsWorthSomethingAndTheTopOneSellsCapacityAtCost()
        {
            var cold = RelationOfferCatalog.AcceptanceChance(
                RelationOffer.DistributionLicence, 50.0, 40.0, 40.0, allianceLevel: 0);

            var signed = RelationOfferCatalog.AcceptanceChance(
                RelationOffer.DistributionLicence, 50.0, 40.0, 40.0, allianceLevel: 1);

            Assert.That(signed, Is.GreaterThan(cold),
                "a lab that has put its name to an alliance weighs the letter differently");

            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Assert.That(simulation.CapacityPremiumWith(lab),
                Is.EqualTo(RelationOfferCatalog.CapacityPremium),
                "a stranger charges the premium");

            Warm(simulation, lab, RivalRelations.Best);

            for (var step = 0; step < LabAlliances.TopLevel; step++)
            {
                simulation.State.Alliances.Sign(lab, simulation.State.Date);
            }

            Assert.That(simulation.CapacityPremiumWith(lab), Is.EqualTo(1.0),
                "and the deepest alliance sells it at cost, which is what the top rung is for");
        }

        /// <summary>
        /// What happened between the two companies is kept after it stops mattering.
        ///
        /// **The author asked for the list of current and older work by name**, and nothing in the
        /// game remembered a finished term: the row was removed when it ran out and written nowhere.
        /// </summary>
        [Test]
        public void AFinishedTermAndARefusalBothStayInTheRecord()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);

            simulation.State.Deals.Add(new StandingDeal(lab, RelationOffer.DistributionLicence,
                simulation.State.Date, simulation.State.Date.AddDays(3)));

            for (var day = 0; day < 5; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.State.DealHistory, Is.Not.Empty,
                "a term that ran out is the thing a history is mostly made of");

            Assert.That(simulation.State.DealHistory[0].Outcome, Is.EqualTo(DealOutcome.Finished));

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.That(back.DealHistory, Has.Count.EqualTo(simulation.State.DealHistory.Count));
            Assert.That(back.DealHistory[0].Lab, Is.EqualTo(lab));
            Assert.That(back.DealHistory[0].Outcome, Is.EqualTo(DealOutcome.Finished));
        }

        /// <summary>v66 to v67: the record starts empty, because a v66 file never kept one.</summary>
        [Test]
        public void AnOlderCampaignHasNoRecordOfWorkAndNoneIsInvented()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 66;
            data.dealPastLabs = null;

            var upgraded = SaveMigration.UpgradeV66ToV67(data);

            Assert.That(upgraded.version, Is.EqualTo(67));
            Assert.That(upgraded.dealPastLabs, Is.Empty);
            StringAssert.Contains("v66 to v67", SaveMigration.LastMigrationNotes);
        }


        /// <summary>
        /// A term that runs out is sometimes brought up by the other side, and taking it is cheap.
        ///
        /// **Half the time, which is the author's own number**, and it is the difference between a
        /// relationship and a subscription: a term that always waits to be noticed is the second
        /// one. Driven across many seeds rather than asserted on one, because a coin that came up
        /// heads once proves nothing about the coin.
        /// </summary>
        [Test]
        public void SometimesTheyRingAboutATermThatRanOutAndSometimesTheyDoNot()
        {
            var rang = 0;
            var runs = 40;

            for (var seed = 0u; seed < runs; seed++)
            {
                var simulation = Company(seed + 1);
                var lab = CompetitorId.Cohere;

                Warm(simulation, lab, RivalRelations.Best);

                simulation.State.Deals.Add(new StandingDeal(lab, RelationOffer.CapacityPurchase,
                    simulation.State.Date, simulation.State.Date.AddDays(2)));

                for (var day = 0; day < 4; day++)
                {
                    simulation.AdvanceDay();
                }

                if (simulation.RenewalIsOnTheTable)
                {
                    rang++;
                }
            }

            Assert.That(rang, Is.GreaterThan(runs / 6),
                "they never ring, so a term ending is a subscription lapsing");

            Assert.That(rang, Is.LessThan(runs * 5 / 6),
                "they always ring, so the roll is not a roll");
        }

        /// <summary>
        /// Taking a renewal they offered costs the money and starts today, with no roll.
        ///
        /// **That is the whole value of being called.** Sending the same offer again is always
        /// available and costs a wait and a chance of a no; when they rang, they have already said
        /// yes and the only question left is whether the company can pay.
        /// </summary>
        [Test]
        public void ARenewalTheyOfferedStartsAtOnceAndCostsWhatTheOfferCosts()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;
            var offer = RelationOffer.CapacityPurchase;

            Warm(simulation, lab, RivalRelations.Best);

            simulation.State.Renewals.Add(new PendingRenewal(lab, offer, simulation.State.Date));

            var before = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryAcceptRenewal(out var why), why);

            Assert.That(simulation.State.CashUsd,
                Is.EqualTo(before - RelationOfferCatalog.Get(offer).CashCostUsd),
                "it costs what the offer costs and nothing extra for the convenience");

            Assert.That(simulation.State.Deals, Has.Count.EqualTo(1),
                "and it is running today rather than waiting on an answer");

            Assert.IsEmpty(simulation.State.Renewals,
                "an accepted renewal leaves the table rather than sitting there answered");
        }

        [Test]
        public void ARenewalLeftAloneGoesOffTheTableAndCostsNothing()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);

            simulation.State.Renewals.Add(new PendingRenewal(lab, RelationOffer.CapacityPurchase,
                simulation.State.Date));

            Assert.IsTrue(simulation.RenewalIsOnTheTable);

            var before = simulation.State.CashUsd;

            for (var day = 0; day < CompanySimulation.RenewalOpenDays + 1; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.IsFalse(simulation.RenewalIsOnTheTable,
                "a fortnight of silence is an answer");

            Assert.That(simulation.State.CashUsd, Is.LessThanOrEqualTo(before),
                "and ignoring it charges nothing beyond the ordinary daily bill");

            Assert.IsFalse(simulation.TryAcceptRenewal(out var why));
            Assert.That(why, Is.Not.Empty);
        }

        /// <summary>
        /// A lab the company has fallen out with in the meantime does not ring.
        ///
        /// Read from the band rather than from a second record, the same way everything else in
        /// this system reads it.
        /// </summary>
        [Test]
        public void NobodyRingsSomebodyTheyHaveFallenOutWith()
        {
            for (var seed = 0u; seed < 20; seed++)
            {
                var simulation = Company(seed + 100);
                var lab = CompetitorId.Cohere;

                Warm(simulation, lab, RivalRelations.HostileAbove);

                simulation.State.Deals.Add(new StandingDeal(lab, RelationOffer.CapacityPurchase,
                    simulation.State.Date, simulation.State.Date.AddDays(2)));

                for (var day = 0; day < 4; day++)
                {
                    simulation.AdvanceDay();
                }

                Assert.IsFalse(simulation.RenewalIsOnTheTable,
                    "a lab that is hostile telephoned to ask about carrying on");
            }
        }

        [Test]
        public void ARenewalOnTheTableSurvivesASave()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            simulation.State.Renewals.Add(new PendingRenewal(lab, RelationOffer.DistributionLicence,
                simulation.State.Date));

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.That(back.Renewals, Is.Not.Empty,
                "the roll for whether they rang has happened, so a reload must not get a second go");

            Assert.That(back.Renewals[0].Lab, Is.EqualTo(lab));
            Assert.That(back.Renewals[0].Offer, Is.EqualTo(RelationOffer.DistributionLicence));
        }

        /// <summary>v67 to v68: nobody has offered to renew anything, because nothing could.</summary>
        [Test]
        public void AnOlderCampaignHasNoRenewalWaiting()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 67;
            data.renewalLab = 999;

            var upgraded = SaveMigration.UpgradeV67ToV68(data);

            Assert.That(upgraded.version, Is.EqualTo(68));
            Assert.That(upgraded.renewalLab, Is.EqualTo(-1));
            StringAssert.Contains("v67 to v68", SaveMigration.LastMigrationNotes);
        }

        /// <summary>
        /// A lab nothing was ever signed with has no number, however well the two get on.
        ///
        /// **The gate is having signed something, not the relation**, which is what stops the
        /// telephone being a free way in: it is a thing an alliance gave the player rather than a
        /// way to reach one.
        /// </summary>
        [Test]
        public void ALabNothingWasEverSignedWithCannotBeRung()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);

            var before = simulation.State.Relations.With(lab);

            Assert.IsFalse(simulation.TryCallLab(lab, out var why),
                "a lab with no history at all answered the telephone");

            Assert.That(why, Is.Not.Empty);

            Assert.That(simulation.State.Relations.With(lab), Is.EqualTo(before),
                "a refused call moved the relation anyway");
        }

        /// <summary>A lab that was signed with once stays callable even after it all fell apart.</summary>
        [Test]
        public void ALabSignedWithOnceStaysCallableAfterTheAllianceIsGone()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);

            simulation.BreakAlliance(lab);
            Warm(simulation, lab, RivalRelations.HostileAbove);

            Assert.That(simulation.State.Alliances.LevelWith(lab), Is.Zero,
                "the fixture did not actually take the alliance away");

            Assert.IsTrue(simulation.TryCallLab(lab, out var refused), refused);
        }

        /// <summary>
        /// One call a month, and reloading is not a second one.
        ///
        /// **The day is saved for exactly this reason.** Every other roll this project keeps in the
        /// file is kept because a reload must not get another go at it, and a cooldown living only
        /// in memory is the same hole with a cheaper prize.
        /// </summary>
        [Test]
        public void ACallSpendsTheMonthAndSurvivesASave()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);
            Assert.IsTrue(simulation.TryCallLab(lab, out var first), first);

            Assert.IsFalse(simulation.TryCallLab(lab, out var second),
                "two calls in one afternoon");

            Assert.That(second, Is.Not.Empty);

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.That(back.Alliances.LastCalled(lab),
                Is.EqualTo(simulation.State.Alliances.LastCalled(lab)),
                "a reload handed the player a fresh call");

            Assert.That(simulation.DaysUntilCallable(lab),
                Is.EqualTo(CompanySimulation.CallCooldownDays));
        }

        /// <summary>The month runs out and they can be rung again.</summary>
        [Test]
        public void TheMonthRunsOutAndTheyCanBeRungAgain()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);
            Assert.IsTrue(simulation.TryCallLab(lab, out var first), first);

            for (var day = 0; day < CompanySimulation.CallCooldownDays; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.That(simulation.DaysUntilCallable(lab), Is.Zero);
            Assert.IsTrue(simulation.TryCallLab(lab, out var again), again);
        }

        /// <summary>
        /// **A call is worth less than a month of drift, and that ordering is the mechanic.**
        ///
        /// If ringing somebody up outpaced the cooling, a player who signed one alliance in 2023
        /// would hold it for the rest of the campaign for free and an alliance could never cool,
        /// which is the case the break rule was written for. So this measures a year of a company
        /// whose only move is the telephone and requires it to have lost ground.
        /// </summary>
        [Test]
        public void AMonthlyCallAloneDoesNotHoldARelationUp()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);

            var start = simulation.State.Relations.With(lab);
            var calls = 0;

            for (var day = 0; day < 365; day++)
            {
                if (simulation.DaysUntilCallable(lab) == 0 && simulation.TryCallLab(lab, out _))
                {
                    calls++;
                }

                simulation.AdvanceDay();
            }

            Assert.That(calls, Is.GreaterThanOrEqualTo(12),
                "the fixture never actually rang anybody, so it measured nothing");

            Assert.That(simulation.State.Relations.With(lab), Is.LessThan(start),
                "a year of nothing but telephone calls held the relation where it was");
        }

        /// <summary>
        /// A call the rule allowed moves the relation, and it says why in the history.
        ///
        /// Nothing in this system may move a relation without a reason the player can read, and a
        /// free move with no line against it is the shape that rule exists to forbid.
        /// </summary>
        [Test]
        public void ACallWarmsTheRelationAndSaysSoInTheHistory()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);
            Assert.IsTrue(simulation.TrySignAlliance(lab, out var why), why);

            Warm(simulation, lab, 20.0);

            var before = simulation.State.Relations.With(lab);

            Assert.IsTrue(simulation.TryCallLab(lab, out var refused), refused);

            Assert.That(simulation.State.Relations.With(lab),
                Is.EqualTo(before + CompanySimulation.CallWarmth).Within(0.0001));

            Assert.That(simulation.State.Relations.History.Last().ReasonKey,
                Is.EqualTo("relation.reason.called"));
        }

        /// <summary>v68 to v69: nobody has been telephoned, so nobody is on cooldown.</summary>
        [Test]
        public void AnOlderCampaignHasRungNobody()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 68;
            data.allianceCalledLabs = new System.Collections.Generic.List<int> { 3 };
            data.allianceCalledDays = new System.Collections.Generic.List<int> { 900 };

            var upgraded = SaveMigration.UpgradeV68ToV69(data);

            Assert.That(upgraded.version, Is.EqualTo(69));
            Assert.That(upgraded.allianceCalledLabs, Is.Empty);
            Assert.That(upgraded.allianceCalledDays, Is.Empty);
            StringAssert.Contains("v68 to v69", SaveMigration.LastMigrationNotes);
        }


        /// <summary>
        /// A distribution licence reaches somebody, and the partner is paid for reaching them.
        ///
        /// **Both halves of this were written and neither was read.** `AllianceReachMultiplier` had
        /// no caller anywhere in the game and `DistributionShare` was named only in its own
        /// catalogue, so a licence cost $1.2M, warmed the relation and did nothing at all for the
        /// whole of its 270 day term. Fourteenth mechanism in this project finished underneath with
        /// nothing on top of it, and the first one where the missing half was a *charge* rather
        /// than a control: what shipped was not a dead feature, it was free money waiting to be
        /// connected.
        ///
        /// The two are tested together on purpose. Wiring the reach without the cut would have
        /// handed the player twenty-two per cent more audience for nothing, which the spine of this
        /// game forbids in as many words.
        /// </summary>
        [Test]
        public void ADistributionLicenceBuysReachAndThePartnerIsPaidForIt()
        {
            Assert.That(RelationOfferCatalog.DistributionShare, Is.GreaterThan(0.0));
            Assert.That(RelationOfferCatalog.DistributionReach, Is.GreaterThan(0.0));

            var alone = new CompanySimulation(new CompanyState("Alone", 4242u));
            var signed = new CompanySimulation(new CompanyState("Signed", 4242u));

            Assert.That(alone.AllianceReachMultiplier(), Is.EqualTo(1.0),
                "a company with no licence is reached by nobody on its behalf");

            Assert.That(alone.DistributionCutUsd(1_000_000L), Is.EqualTo(0L),
                "a company with no partner pays no partner");

            signed.State.Deals.Add(new StandingDeal(
                CompetitorId.Cohere, RelationOffer.DistributionLicence,
                signed.State.Date, signed.State.Date.AddDays(270)));

            Assert.That(signed.AllianceReachMultiplier(),
                Is.EqualTo(1.0 + RelationOfferCatalog.DistributionReach).Within(1e-9),
                "the licence has to reach the market as the catalogue says it does");

            // **The cut is on what came through them, not on everything.** At the catalogue's own
            // figures the channel brings 22% more audience, so 22/122 of today's takings arrived
            // that way and the partner keeps 30% of those. Written out rather than copied from the
            // method, so the two have to agree rather than being the same line twice.
            var reach = 1.0 + RelationOfferCatalog.DistributionReach;
            var expected = (long)System.Math.Round(
                1_000_000L * ((reach - 1.0) / reach) * RelationOfferCatalog.DistributionShare);

            Assert.That(signed.DistributionCutUsd(1_000_000L), Is.EqualTo(expected),
                "the partner keeps their share of what their own channel sold");

            Assert.That(signed.DistributionCutUsd(1_000_000L), Is.LessThan(1_000_000L),
                "a partner cannot keep more than the company earned");
        }

        /// <summary>
        /// The licence is worth taking and it is not free money.
        ///
        /// **The number that matters is the one in between.** Twenty-two per cent more audience
        /// against thirty per cent of what that audience paid nets the company somewhere under
        /// twenty and comfortably over nothing; if it ever reached the reach figure the cut would
        /// have stopped being charged, and if it ever went negative nobody would sign one.
        /// </summary>
        [Test]
        public void TheChannelIsWorthTakingAndIsNotFree()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 77u));

            simulation.State.Deals.Add(new StandingDeal(
                CompetitorId.Cohere, RelationOffer.DistributionLicence,
                simulation.State.Date, simulation.State.Date.AddDays(270)));

            var reach = simulation.AllianceReachMultiplier();
            const long takings = 10_000_000L;

            var kept = takings - simulation.DistributionCutUsd(takings);
            var net = kept / (double)takings * reach;

            Assert.That(net, Is.GreaterThan(1.0),
                "nobody would sign a channel that leaves them worse off");

            Assert.That(net, Is.LessThan(reach),
                "the channel is not free: some of what it sold stays with the partner");
        }

        /// <summary>
        /// Agreeing to a licence is not signing one, and the term does not start until it is signed.
        ///
        /// **The author's reading and the reason this offer is different.** The other three are a
        /// yes or a no to terms that are fixed by what the thing is. A distribution licence is
        /// somebody selling your product in their shop on their own margin, so what they agree to
        /// is talking about it; the contract arrives, the player reads what it is worth, and signs
        /// or does not.
        ///
        /// Driven through the state rather than by waiting out a roll, because what is being
        /// measured is the branch and not the dice.
        /// </summary>
        [Test]
        public void AgreeingToALicenceOpensAContractRatherThanStartingATerm()
        {
            var simulation = Company();
            Warm(simulation, CompetitorId.Cohere, 45.0);

            // A licence needs something to licence: `needsLiveModel` is true on that row and the
            // refusal says so plainly, which is how this fixture found out.
            simulation.State.AddDeployedModel(new DeployedModel(
                "Aurora", ArchitectureId.DenseTransformer, capability: 40.0,
                releaseDate: simulation.State.Date, activeParameterCount: 8.0,
                priceMultiplier: 1.0));

            simulation.TrySendOffer(CompetitorId.Cohere, RelationOffer.DistributionLicence,
                out var why);

            Assert.That(why, Is.Null.Or.Empty, "the offer could not be sent at all");

            var definition = RelationOfferCatalog.Get(RelationOffer.DistributionLicence);

            // Answer day. Run it until the pending offer has been decided one way or the other.
            for (var day = 0; day <= definition.DaysToAnswer + 1; day++)
            {
                simulation.Advance(1);
            }

            if (!simulation.RenewalIsOnTheTable)
            {
                // They said no, which is a legal answer and not what this test is about.
                Assert.That(simulation.HasDeal(RelationOffer.DistributionLicence), Is.False,
                    "a refused licence cannot have started a term either");

                return;
            }

            Assert.That(simulation.NextRenewal.Value.Offer,
                Is.EqualTo(RelationOffer.DistributionLicence));

            Assert.That(simulation.HasDeal(RelationOffer.DistributionLicence), Is.False,
                "the term must not start until the contract is signed");

            Assert.That(simulation.AllianceReachMultiplier(), Is.EqualTo(1.0),
                "an unsigned contract reaches nobody");

            simulation.TryAcceptRenewal(out var refusal);

            Assert.That(refusal, Is.Null.Or.Empty, $"the contract could not be signed: {refusal}");

            Assert.That(simulation.HasDeal(RelationOffer.DistributionLicence), Is.True,
                "signing the contract starts the term");

            Assert.That(simulation.AllianceReachMultiplier(), Is.GreaterThan(1.0));
        }

        /// <summary>
        /// The contract quotes four figures and two of them are catalogue facts.
        ///
        /// The term and the partner's share cannot be wrong; the audience and the money are
        /// today's trading multiplied by a reach the licence has not had yet, which is a projection
        /// and is labelled as one on the card. What this holds is that none of the four is ever
        /// nonsense: no negative money, no share outside nought and one, no term of zero.
        /// </summary>
        [Test]
        public void TheContractQuotesFiguresThatAreNeverNonsense()
        {
            var simulation = Company();
            var terms = simulation.DistributionEstimate();

            Assert.That(terms.TermDays, Is.EqualTo(
                RelationOfferCatalog.Get(RelationOffer.DistributionLicence).TermDays));

            Assert.That(terms.PartnerShare,
                Is.EqualTo(RelationOfferCatalog.DistributionShare).Within(1e-9));

            Assert.That(terms.PartnerShare, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(terms.ExtraUsersPerMonth, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(terms.OurExtraPerMonth, Is.GreaterThanOrEqualTo(0L));
        }

        /// <summary>
        /// **The offer lives for a fortnight and the only way to accept it lived for one card.**
        /// `ShowRenewalCard` is raised from the `RenewalOffered` event and from nowhere else, so
        /// pressing "not yet" threw the answer away while the offer went on sitting in the save,
        /// blocking every other lab from ringing for the rest of the fortnight. The doc comment on
        /// that card claimed this badge already carried it. It did not, and Francisco found out.
        /// </summary>
        [Test]
        public void TheBadgeCarriesARenewalSoItCanBeAnsweredAfterTheDayItArrives()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);

            var badge = new ScalingLaws.UI.AllianceBadge(() => { });
            badge.Refresh(simulation);
            var before = badge.Carrying;

            Assert.IsFalse(badge.WaitingOnAnAnswer,
                "nothing has been offered yet, so there is nothing to answer");

            simulation.State.Renewals.Add(new PendingRenewal(lab, RelationOffer.CapacityPurchase,
                simulation.State.Date));

            badge.Refresh(simulation);

            Assert.IsTrue(simulation.RenewalIsOnTheTable);
            Assert.IsTrue(badge.WaitingOnAnAnswer,
                "a renewal on the table has to be reachable from the corner, not only on the day it rang");
            Assert.That(badge.Carrying, Is.EqualTo(before + 1),
                "and it has to be a line the player can see, not only a flag");
        }

        /// <summary>
        /// The other half: the badge must let go when the fortnight runs out, or the corner goes on
        /// offering an answer the simulation will refuse.
        /// </summary>
        [Test]
        public void TheBadgeStopsCarryingARenewalOnceTheFortnightHasRunOut()
        {
            var simulation = Company();
            var lab = CompetitorId.Cohere;

            Warm(simulation, lab, RivalRelations.Best);

            simulation.State.Renewals.Add(new PendingRenewal(lab, RelationOffer.CapacityPurchase,
                simulation.State.Date));

            var badge = new ScalingLaws.UI.AllianceBadge(() => { });
            badge.Refresh(simulation);
            Assert.IsTrue(badge.WaitingOnAnAnswer);

            for (var day = 0; day < CompanySimulation.RenewalOpenDays + 1; day++)
            {
                simulation.AdvanceDay();
            }

            badge.Refresh(simulation);

            Assert.IsFalse(simulation.RenewalIsOnTheTable);
            Assert.IsFalse(badge.WaitingOnAnAnswer,
                "an expired offer must stop being advertised in the corner");
        }

        /// <summary>
        /// v71 to v72: the one renewal a v71 file could hold becomes the first entry of the list.
        ///
        /// **There is no second offer to reconstruct and inventing one would be a lie.** v71
        /// refused to raise a renewal while that field was occupied, so a lab whose term ran out
        /// inside somebody else's fortnight never rang and no record of the call was ever written.
        /// </summary>
        [Test]
        public void TheRenewalOnTheTableSurvivesBecomingAList()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 71;
            data.renewalLabs.Clear();
            data.renewalKinds.Clear();
            data.renewalDays.Clear();
            data.renewalLab = (int)CompetitorId.Cohere;
            data.renewalKind = (int)RelationOffer.DistributionLicence;
            data.renewalDay = 900;

            var upgraded = SaveMigration.UpgradeV71ToV72(data);

            Assert.That(upgraded.version, Is.EqualTo(72));
            Assert.That(upgraded.renewalLabs.Count, Is.EqualTo(1),
                "the offer that was on the table has to still be on it after the upgrade");
            Assert.That(upgraded.renewalLabs[0], Is.EqualTo((int)CompetitorId.Cohere));
            Assert.That(upgraded.renewalKinds[0], Is.EqualTo((int)RelationOffer.DistributionLicence));
            Assert.That(upgraded.renewalDays[0], Is.EqualTo(900));
            StringAssert.Contains("v71 to v72", SaveMigration.LastMigrationNotes);
        }

        /// <summary>A v71 file with nothing on the table starts v72 with an empty list, not a ghost.</summary>
        [Test]
        public void AV71FileWithNoRenewalStartsV72Empty()
        {
            var data = SaveStore.Capture(new CompanyState("Quiet lab", 77u));
            data.version = 71;
            data.renewalLabs.Clear();
            data.renewalKinds.Clear();
            data.renewalDays.Clear();
            data.renewalLab = -1;

            var upgraded = SaveMigration.UpgradeV71ToV72(data);

            Assert.That(upgraded.version, Is.EqualTo(72));
            Assert.IsEmpty(upgraded.renewalLabs);
        }

        /// <summary>
        /// The fault Francisco found: a second lab never rang at all while somebody else was
        /// waiting, so one offer the player had walked away from silenced everyone for a fortnight.
        /// </summary>
        [Test]
        public void ASecondLabCanAskWhileAnotherRenewalIsStillWaiting()
        {
            var simulation = Company();

            simulation.State.Renewals.Add(new PendingRenewal(CompetitorId.Cohere,
                RelationOffer.CapacityPurchase, simulation.State.Date));
            simulation.State.Renewals.Add(new PendingRenewal(CompetitorId.AlephAlpha,
                RelationOffer.DistributionLicence, simulation.State.Date));

            Assert.That(simulation.OpenRenewals.Count, Is.EqualTo(2),
                "two labs can be waiting on an answer at the same time");

            Assert.IsTrue(simulation.RenewalIsOnTheTable);
            Assert.That(simulation.NextRenewal.Value.Lab, Is.EqualTo(CompetitorId.Cohere),
                "the card is about the oldest one waiting");

            simulation.DeclineRenewal();

            Assert.That(simulation.OpenRenewals.Count, Is.EqualTo(1),
                "putting one down must not put the other one down with it");
            Assert.That(simulation.NextRenewal.Value.Lab, Is.EqualTo(CompetitorId.AlephAlpha));
        }
    }
}
