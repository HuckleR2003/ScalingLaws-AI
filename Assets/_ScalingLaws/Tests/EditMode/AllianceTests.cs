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

    }
}
