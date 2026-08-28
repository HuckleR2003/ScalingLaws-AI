using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The world that has opinions about you: loyalty, benefits, relations, timed effects, poaching.
    ///
    /// **These four are one system with four faces.** People stay or leave for reasons, rivals
    /// remember what you did to them, and some things are true about the company only for a while.
    /// Each half is worthless alone: loyalty nobody can act on is a number, and poaching without
    /// loyalty is a purchase.
    /// </summary>
    public sealed class LivingWorldTests
    {
        private static CompanySimulation Company(long cash = 200_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 4321));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        private static Hire Person(int yearsAgo, double wage = 0.0) => new(
            StaffRole.ResearchScientist, 3,
            new GameDate(GameDate.Start.DayIndex),
            "Subject", PlayerSkill.Development, HireSource.Agency, wage);

        // ---- loyalty -------------------------------------------------------------------------

        /// <summary>Time is the biggest single term, and it saturates rather than climbing forever.</summary>
        [Test]
        public void StayingLongerMakesSomebodyHarderToTakeAway()
        {
            var person = Person(0);
            var market = StaffCatalog.Get(StaffRole.ResearchScientist).SalaryPerYearUsd(3);

            var week = Loyalty.For(person, new GameDate(7), 0.0, market);
            var twoYears = Loyalty.For(person, new GameDate(730), 0.0, market);
            var eightYears = Loyalty.For(person, new GameDate(2920), 0.0, market);

            Assert.That(twoYears, Is.GreaterThan(week));
            Assert.That(eightYears, Is.GreaterThan(twoYears));

            Assert.That(eightYears - twoYears, Is.LessThan(twoYears - week),
                "Tenure has to saturate. The difference between six months and eighteen is most of "
                + "the effect, and a curve that keeps climbing makes every old company untouchable.");
        }

        /// <summary>Benefits buy loyalty, and the whole set buys more than none of it.</summary>
        [Test]
        public void BenefitsRaiseLoyaltyAndTheCapHolds()
        {
            var person = Person(0);
            var market = StaffCatalog.Get(StaffRole.ResearchScientist).SalaryPerYearUsd(3);

            var bare = Loyalty.For(person, new GameDate(365), 0.0, market);
            var everything = Loyalty.For(person, new GameDate(365),
                BenefitCatalog.PointsFor(BenefitCatalog.All.Select(entry => entry.Benefit)), market);

            Assert.That(everything, Is.GreaterThan(bare));

            Assert.That(
                BenefitCatalog.PointsFor(BenefitCatalog.All.Select(entry => entry.Benefit)),
                Is.LessThanOrEqualTo(BenefitCatalog.MaximumPoints),
                "Uncapped benefits would make loyalty a thing you buy outright.");
        }

        /// <summary>
        /// Pay moves it symmetrically in log space.
        ///
        /// Halving somebody's salary has to be resented exactly as much as doubling it is liked. A
        /// linear ratio makes underpaying nearly free, which is the wrong shape for a number people
        /// compare against their friends.
        /// </summary>
        [Test]
        public void PayingOverTheOddsIsLikedAsMuchAsPayingUnderIsResented()
        {
            var market = StaffCatalog.Get(StaffRole.ResearchScientist).SalaryPerYearUsd(3);
            var hourly = market / (double)PositionCatalog.PaidHoursPerYear;

            var fair = Loyalty.For(Person(0, hourly), new GameDate(365), 0.0, market);
            var generous = Loyalty.For(Person(0, hourly * 2.0), new GameDate(365), 0.0, market);
            var mean = Loyalty.For(Person(0, hourly * 0.5), new GameDate(365), 0.0, market);

            Assert.That(generous, Is.GreaterThan(fair));
            Assert.That(mean, Is.LessThan(fair));

            Assert.That(generous - fair, Is.EqualTo(fair - mean).Within(0.5),
                "Symmetric in log space, or underpaying is a free saving.");
        }

        // ---- benefits ------------------------------------------------------------------------

        /// <summary>
        /// The cheap ones are not simply worse, which is what stops the list being a ladder.
        /// </summary>
        [Test]
        public void NoBenefitIsSimplyBetterThanAnother()
        {
            foreach (var left in BenefitCatalog.All)
            {
                foreach (var right in BenefitCatalog.All)
                {
                    if (left.Benefit == right.Benefit)
                    {
                        continue;
                    }

                    var strictlyBetter = left.MonthlyCostPerHeadUsd <= right.MonthlyCostPerHeadUsd
                        && left.LoyaltyPoints >= right.LoyaltyPoints
                        && (left.MonthlyCostPerHeadUsd < right.MonthlyCostPerHeadUsd
                            || left.LoyaltyPoints > right.LoyaltyPoints);

                    Assert.IsFalse(strictlyBetter,
                        $"{left.DisplayName} is cheaper and better than {right.DisplayName}, so "
                        + "the second one is never worth ticking and the list is a ladder.");
                }
            }
        }

        /// <summary>The bill scales with headcount, which is the entire trade.</summary>
        [Test]
        public void BenefitsCostMoreAsTheCompanyGrows()
        {
            var simulation = Company();
            simulation.State.Staff.SetOffice(OfficeTier.Floor);
            simulation.State.Benefits.Add(StaffBenefit.Healthcare);

            Assert.That(simulation.State.DailyBenefitCostUsd, Is.Zero, "Nobody to pay it for yet.");

            for (var index = 0; index < 5; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 2, GameDate.Start));
            }

            var five = simulation.State.DailyBenefitCostUsd;
            Assert.That(five, Is.GreaterThan(0L));

            for (var index = 0; index < 5; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 2, GameDate.Start));
            }

            Assert.That(simulation.State.DailyBenefitCostUsd, Is.GreaterThan(five),
                "It is charged per head, so hiring makes it dearer. That is the decision.");
        }

        // ---- relations -----------------------------------------------------------------------

        /// <summary>A relation cannot move without a reason the player can read.</summary>
        [Test]
        public void NothingMovesARelationWithoutSayingWhy()
        {
            var relations = new RivalRelations();

            Assert.Throws<System.ArgumentException>(() =>
                relations.Record(CompetitorId.OpenAi, GameDate.Start, -10.0, string.Empty));

            Assert.That(relations.History, Is.Empty);
        }

        [Test]
        public void ARelationRemembersWhatMovedIt()
        {
            var relations = new RivalRelations();

            relations.Record(CompetitorId.OpenAi, new GameDate(100), -14.0,
                "relation.reason.poached", "Daniel");

            Assert.That(relations.With(CompetitorId.OpenAi), Is.EqualTo(-14.0).Within(0.001));
            Assert.That(relations.BandWith(CompetitorId.OpenAi), Is.EqualTo(RelationBand.Tense));

            var history = relations.HistoryWith(CompetitorId.OpenAi);

            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history[0].Subject, Is.EqualTo("Daniel"));
            Assert.That(history[0].Reason, Does.Contain("Daniel"),
                "The sentence is the feature. A number with no cause is indistinguishable from a bug.");

            Assert.That(relations.HistoryWith(CompetitorId.Cohere), Is.Empty,
                "One lab's grudge must not appear under another's name.");
        }

        /// <summary>
        /// Companies forget, slowly, and forgetting is never recorded as a thing that happened.
        /// </summary>
        [Test]
        public void RelationsDriftBackTowardNeutralAndTimePassingIsNotAnEvent()
        {
            var relations = new RivalRelations();
            relations.Record(CompetitorId.OpenAi, GameDate.Start, -60.0, "relation.reason.poached");

            var entries = relations.History.Count;

            for (var day = 0; day < 200; day++)
            {
                relations.Advance();
            }

            var after = relations.With(CompetitorId.OpenAi);

            Assert.That(after, Is.GreaterThan(-60.0), "Nothing was forgotten at all.");
            Assert.That(after, Is.LessThan(0.0),
                "Two hundred days is not long enough to wipe a serious insult, or waiting is a strategy.");

            Assert.That(relations.History.Count, Is.EqualTo(entries),
                "Drift is time passing, which is not a thing that happened to anybody.");
        }

        [Test]
        public void ARelationCannotLeaveItsScale()
        {
            var relations = new RivalRelations();

            for (var index = 0; index < 40; index++)
            {
                relations.Record(CompetitorId.OpenAi, GameDate.Start, -25.0, "relation.reason.poached");
            }

            Assert.That(relations.With(CompetitorId.OpenAi),
                Is.GreaterThanOrEqualTo(RivalRelations.Worst));

            Assert.That(relations.History.Count, Is.LessThanOrEqualTo(RivalRelations.HistoryKept),
                "Past the cap it is an archive nobody reads rather than a memory.");
        }

        // ---- timed effects ---------------------------------------------------------------------

        [Test]
        public void AnEffectExpiresAndFadesRatherThanStopping()
        {
            var effect = new ModelEffect(ModelEffectKind.Viral, GameDate.Start, 40, 0.30);

            Assert.That(effect.Multiplier(new GameDate(1)), Is.EqualTo(1.30).Within(0.001));

            var late = effect.Multiplier(new GameDate(35));

            Assert.That(late, Is.LessThan(1.30), "It has to taper, or the user chart has a cliff in it.");
            Assert.That(late, Is.GreaterThan(1.0));

            Assert.IsFalse(effect.IsActive(new GameDate(41)));
            Assert.That(effect.Multiplier(new GameDate(41)), Is.EqualTo(1.0).Within(0.001));
        }

        [Test]
        public void OnlyOneEffectOfEachKindRunsAtATime()
        {
            var book = new EffectBook();
            var today = new GameDate(10);

            book.Add(new ModelEffect(ModelEffectKind.Viral, today, 30, 0.20), today);
            book.Add(new ModelEffect(ModelEffectKind.Viral, today, 30, 0.40), today);

            Assert.That(book.Active(today).Count(effect => effect.Kind == ModelEffectKind.Viral),
                Is.EqualTo(1),
                "Two viral windows would multiply, which reads as the game being broken.");
        }

        /// <summary>A campaign is presentation. Counting it here would pay for the spend twice.</summary>
        [Test]
        public void ARunningCampaignDoesNotMoveDemandOnItsOwn()
        {
            var book = new EffectBook();
            var today = new GameDate(10);

            book.Add(new ModelEffect(ModelEffectKind.Campaign, today, 60, 0.50), today);

            Assert.That(book.DemandMultiplier(today), Is.EqualTo(1.0).Within(0.001));
        }

        /// <summary>
        /// A clean year earns Safe Harbour, and one bad afternoon takes it.
        ///
        /// **This is the only thing in the game that has to be kept rather than bought**, which is
        /// the whole reason it is worth something.
        /// </summary>
        [Test]
        public void AYearWithoutTroubleEarnsSafeHarbourAndAnIncidentEndsIt()
        {
            var simulation = Company();
            simulation.SetRentedPetaflops(300.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, 40.0,
                simulation.State.Date, 8.0, 1.0));

            for (var day = 0; day < EffectBook.SafeHarbourDays + 5; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.IsTrue(simulation.State.Effects.Has(ModelEffectKind.SafeHarbour, simulation.State.Date),
                "A year with nothing going wrong and no harbour.");

            simulation.State.Effects.End(ModelEffectKind.SafeHarbour);
            Assert.IsFalse(
                simulation.State.Effects.Has(ModelEffectKind.SafeHarbour, simulation.State.Date));
        }

        // ---- poaching ------------------------------------------------------------------------

        /// <summary>The rosters are a place, not a slot machine.</summary>
        [Test]
        public void ARivalHasTheSamePeopleEveryTimeYouLook()
        {
            var today = new GameDate(900);

            var first = RivalStaff.RosterFor(CompetitorId.OpenAi, today, 4321);
            var second = RivalStaff.RosterFor(CompetitorId.OpenAi, today, 4321);

            Assert.That(second.Count, Is.EqualTo(first.Count));

            for (var index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Id, Is.EqualTo(first[index].Id));
                Assert.That(second[index].Name, Is.EqualTo(first[index].Name));
                Assert.That(second[index].Rating, Is.EqualTo(first[index].Rating));
            }

            var other = RivalStaff.RosterFor(CompetitorId.Cohere, today, 4321);

            Assert.That(other.Select(person => person.Name),
                Is.Not.EquivalentTo(first.Select(person => person.Name)),
                "Two labs with the same staff would read as a bug.");
        }

        /// <summary>The best people are what the top membership is actually selling.</summary>
        [Test]
        public void TheStrongestPeopleAreHiddenWithoutTheTopMembership()
        {
            var roster = RivalStaff.RosterFor(CompetitorId.OpenAi, new GameDate(900), 4321);

            var open = RivalStaff.Visible(roster, hasTopMembership: false);
            var paid = RivalStaff.Visible(roster, hasTopMembership: true);

            Assert.That(paid.Count, Is.GreaterThanOrEqualTo(open.Count));

            foreach (var member in open)
            {
                Assert.That(member.Rating, Is.LessThanOrEqualTo(RivalStaff.VisibleCeiling));
            }
        }

        /// <summary>
        /// Loyalty is the wall, and money is the ladder over it.
        ///
        /// Both halves matter. If loyalty were absolute the mechanic would be a filter rather than a
        /// decision, and if money alone decided it there would be nothing to read on the list.
        /// </summary>
        [Test]
        public void LoyaltyResistsAnOfferAndMoneyOvercomesIt()
        {
            var today = new GameDate(3000);

            var newcomer = new RivalStaffMember(1, CompetitorId.OpenAi, "New", PlayerSkill.Development,
                60, new GameDate(2960));

            var lifer = new RivalStaffMember(2, CompetitorId.OpenAi, "Old", PlayerSkill.Development,
                60, new GameDate(200));

            var bonus = Poaching.SalaryAt(newcomer) / 2;

            Assert.That(Poaching.AcceptanceChance(newcomer, today, bonus),
                Is.GreaterThan(Poaching.AcceptanceChance(lifer, today, bonus)),
                "Somebody who arrived last month has to be easier than a lifer.");

            Assert.That(Poaching.AcceptanceChance(lifer, today, bonus * 6),
                Is.GreaterThan(Poaching.AcceptanceChance(lifer, today, bonus)),
                "A committed person is expensive, not unbuyable.");
        }

        /// <summary>And aiming at a lifer is how a company finds out you have been calling.</summary>
        [Test]
        public void ALoyalPersonIsFarMoreLikelyToMentionTheCall()
        {
            var today = new GameDate(3000);

            var newcomer = new RivalStaffMember(1, CompetitorId.OpenAi, "New", PlayerSkill.Development,
                60, new GameDate(2960));

            var lifer = new RivalStaffMember(2, CompetitorId.OpenAi, "Old", PlayerSkill.Development,
                60, new GameDate(200));

            Assert.That(Poaching.ReportChance(lifer, today),
                Is.GreaterThan(Poaching.ReportChance(newcomer, today) * 2.0),
                "The warning on the panel has to be true, or it is decoration.");
        }

        /// <summary>The bonus is spent on the call, whatever the answer is.</summary>
        [Test]
        public void AnApproachCostsTheMoneyEvenWhenItFails()
        {
            var simulation = Company();
            simulation.State.Staff.SetOffice(OfficeTier.Floor);

            var lifer = new RivalStaffMember(9001, CompetitorId.OpenAi, "Old",
                PlayerSkill.Development, 60, new GameDate(1));

            var before = simulation.State.CashUsd;
            simulation.TryPoach(lifer, 250_000, out var outcome, out _);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - 250_000),
                "Approaching somebody costs money whether or not it works, or this is a free "
                + "lottery ticket.");

            Assert.That(outcome, Is.Not.EqualTo(PoachOutcome.Blocked));
        }

        [Test]
        public void ThereIsNowhereToPutSomebodyWithNoDesk()
        {
            var simulation = Company();

            var member = new RivalStaffMember(9002, CompetitorId.OpenAi, "Anyone",
                PlayerSkill.Development, 60, new GameDate(1));

            var before = simulation.State.CashUsd;

            Assert.IsFalse(simulation.TryPoach(member, 100_000, out var outcome, out var note));
            Assert.That(outcome, Is.EqualTo(PoachOutcome.Blocked));
            Assert.That(note, Is.Not.Empty);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(before), "Blocked costs nothing.");
        }

        /// <summary>Taking somebody costs the relationship, and the record says who.</summary>
        [Test]
        public void ASuccessfulRaidIsRememberedByName()
        {
            var simulation = Company();
            simulation.State.Staff.SetOffice(OfficeTier.Floor);

            var easy = new RivalStaffMember(9003, CompetitorId.OpenAi, "Daniel",
                PlayerSkill.Development, 60, new GameDate(GameDate.Start.DayIndex));

            // Enough money that the roll is close to certain, so the test is about the record
            // rather than about the dice.
            var taken = false;

            for (var attempt = 0; attempt < 40 && !taken; attempt++)
            {
                taken = simulation.TryPoach(easy, 4_000_000, out var outcome, out _)
                    && outcome == PoachOutcome.Accepted;

                if (!taken)
                {
                    simulation.State.PoachedRivalStaff.Remove(easy.Id);
                    simulation.State.CashUsd = 200_000_000;
                }
            }

            Assert.IsTrue(taken, "Four million and forty tries never landed one hire.");

            Assert.That(simulation.State.Relations.With(CompetitorId.OpenAi), Is.LessThan(0.0));

            var history = simulation.State.Relations.HistoryWith(CompetitorId.OpenAi);

            Assert.That(history, Is.Not.Empty);
            Assert.That(history.Any(entry => entry.Subject == "Daniel"), Is.True,
                "The record has to name the person, or a player cannot tell two raids apart.");

            Assert.That(simulation.State.Staff.Hires.Any(hire => hire.Name == "Daniel"), Is.True,
                "They agreed and never turned up, which is the failure this project has hit eight times.");
        }

        /// <summary>Somebody already taken cannot be taken again.</summary>
        [Test]
        public void APersonWhoAlreadyLeftIsNotOnTheListAnyMore()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            var roster = simulation.RosterOf(lab);
            Assert.That(roster, Is.Not.Empty);

            simulation.State.PoachedRivalStaff.Add(roster[0].Id);

            Assert.That(simulation.RosterOf(lab).Any(person => person.Id == roster[0].Id), Is.False);
        }
    }
}
