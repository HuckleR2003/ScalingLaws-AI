using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Hiring, end to end.
    ///
    /// **The old system turned cash into headcount in one call, and this exists so the new one
    /// cannot quietly do the same.** Every test below walks the whole road: a search that costs a
    /// draw, an approach that takes days, a letter that arrives, a wage that is argued over, and a
    /// person who ends up on the payroll at the number that was actually agreed. A suite that only
    /// checked the arithmetic would pass on a system the player could never reach.
    /// </summary>
    public sealed class HiringTests
    {
        private static CompanySimulation Rich(long cash = 40_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = cash;

            // The garage seats four. Most of these tests want somewhere to put people.
            simulation.State.Staff.SetOffice(OfficeTier.Floor);
            return simulation;
        }

        private static Candidate FirstCandidate(CompanySimulation simulation, PlayerSkill skill,
            HireSource source) =>
            simulation.Shortlist(skill, source, 50, 4).First();

        /// <summary>Runs days until the approach answers and returns the letter it produced.</summary>
        private static MailItem WaitForTheLetter(CompanySimulation simulation)
        {
            for (var day = 0; day < 12; day++)
            {
                simulation.AdvanceDay();

                var letter = simulation.State.Mail.All
                    .FirstOrDefault(item => item.Candidate != null && !item.IsClosed);

                if (letter != null)
                {
                    return letter;
                }
            }

            return null;
        }

        // ---- the catalogs -------------------------------------------------------------------

        [Test]
        public void ThereIsOnePositionPerFounderSkill()
        {
            foreach (PlayerSkill skill in Enum.GetValues(typeof(PlayerSkill)))
            {
                if (skill == PlayerSkill.None)
                {
                    continue;
                }

                Assert.That(PositionCatalog.TryGet(skill, out _), Is.True,
                    $"{skill} can be levelled by the founder but nobody can be hired to do it.");
            }

            Assert.That(PositionCatalog.All.Count,
                Is.EqualTo(Enum.GetValues(typeof(PlayerSkill)).Length - 1),
                "A position with no skill behind it is a job title the player never chose.");
        }

        [Test]
        public void EveryPositionHasItsOwnColourAndIcon()
        {
            var colours = PositionCatalog.All.Select(entry => entry.AccentHex).ToList();

            Assert.That(colours.Distinct().Count(), Is.EqualTo(colours.Count),
                "Two tiles the same colour makes the count ring useless at a glance.");
        }

        [Test]
        public void TheChannelsTradePriceAgainstQuality()
        {
            var remote = HiringChannels.Get(HireSource.Remote);
            var agency = HiringChannels.Get(HireSource.Agency);
            var specialist = HiringChannels.Get(HireSource.Specialist);

            Assert.That(remote.WageMultiplier, Is.EqualTo(0.70).Within(1e-9));
            Assert.That(remote.QualityMultiplier, Is.EqualTo(0.40).Within(1e-9),
                "Sixty per cent off the skill is what makes remote a bridge rather than a strategy.");

            Assert.That(agency.WageMultiplier, Is.EqualTo(1.00).Within(1e-9),
                "The agency is the neutral option and must cost exactly the standard wage.");

            Assert.That(agency.QualityMultiplier, Is.EqualTo(0.70).Within(1e-9));

            Assert.That(specialist.WageMultiplier, Is.EqualTo(1.20).Within(1e-9));
            Assert.That(specialist.QualityMultiplier, Is.EqualTo(1.50).Within(1e-9));
        }

        [Test]
        public void ASpecialistIsWorthMoreThanTheirAdvertAndRemoteIsWorthLess()
        {
            var random = new DeterministicRandom(99);

            var remote = Candidate.Roll(1, PlayerSkill.Safety, HireSource.Remote, 60, random);
            var specialist = Candidate.Roll(2, PlayerSkill.Safety, HireSource.Specialist, 60, random);

            Assert.That(remote.TrueLevel, Is.LessThan(remote.AdvertisedLevel));
            Assert.That(specialist.TrueLevel, Is.GreaterThan(specialist.AdvertisedLevel));
        }

        // ---- approaching ---------------------------------------------------------------------

        [Test]
        public void NobodyIsHiredTheMomentTheyAreClicked()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Software, HireSource.Agency);

            var problem = simulation.TryApproach(candidate);

            Assert.That(problem, Is.Empty);
            Assert.That(simulation.State.Staff.Headcount, Is.Zero,
                "Turning a click straight into headcount is exactly what this replaced.");

            Assert.That(simulation.State.Hiring.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void AnApproachTakesBetweenTwoAndFourDays()
        {
            for (var seed = 1u; seed <= 40u; seed++)
            {
                var simulation = Rich();
                simulation.State.Hiring.Random.State = seed;

                var candidate = FirstCandidate(simulation, PlayerSkill.Concept, HireSource.Agency);
                simulation.TryApproach(candidate);

                var days = simulation.State.Hiring.Approaches.Single().DaysNeeded;

                Assert.That(days, Is.InRange(HiringChannels.FastestContactDays,
                    HiringChannels.SlowestContactDays),
                    "Every route into hiring has to wait the same two to four days.");
            }
        }

        [Test]
        public void TheApproachTurnsIntoALetterWithThePersonInIt()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.DataEngineering, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            Assert.That(letter, Is.Not.Null, "The approach never produced a letter.");
            Assert.That(letter.Candidate.Id, Is.EqualTo(candidate.Id));
            Assert.That(letter.Sender, Is.EqualTo(candidate.Name));
            Assert.That(letter.Kind, Is.EqualTo(MailKind.JobOffer));
            Assert.That(simulation.State.Hiring.OpenCount, Is.Zero,
                "The approach should be closed once it has been answered.");
        }

        // ---- negotiating ----------------------------------------------------------------------

        [Test]
        public void AcceptingWhatTheyAskedForHiresThemAtThatRate()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Software, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            var verdict = simulation.AcceptAsking(letter, out _);

            Assert.That(verdict, Is.EqualTo(OfferVerdict.Accepted));
            Assert.That(simulation.State.Staff.Headcount, Is.EqualTo(1));

            var hire = simulation.State.Staff.Hires.Single();

            Assert.That(hire.Name, Is.EqualTo(candidate.Name));
            Assert.That(hire.HourlyWageUsd, Is.EqualTo(candidate.AskingHourlyUsd).Within(1e-6),
                "The negotiated rate is what gets paid, not the catalog's.");

            Assert.That(letter.IsClosed, Is.True);
        }

        [Test]
        public void AnOfferAtTheirHiddenFloorIsAccepted()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Management, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            var verdict = simulation.Negotiate(letter, candidate.ReservationHourlyUsd, 0L, out _);

            Assert.That(verdict, Is.EqualTo(OfferVerdict.Accepted));
            Assert.That(simulation.State.Staff.Hires.Single().HourlyWageUsd,
                Is.EqualTo(candidate.ReservationHourlyUsd).Within(1e-6),
                "Haggling has to actually save the company money or it is theatre.");
        }

        [Test]
        public void AnOfferJustUnderTheFloorMakesThemHoldFirm()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Teamwork, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            var verdict = simulation.Negotiate(letter, candidate.ReservationHourlyUsd * 0.95, 0L,
                out var note);

            Assert.That(verdict, Is.EqualTo(OfferVerdict.HeldFirm));
            Assert.That(note, Does.Contain(candidate.Name));
            Assert.That(letter.IsClosed, Is.False, "Holding firm keeps them at the table.");
            Assert.That(simulation.State.Staff.Headcount, Is.Zero);
        }

        [Test]
        public void ALowballOfferEndsTheConversation()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Safety, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            var verdict = simulation.Negotiate(letter, candidate.ReservationHourlyUsd * 0.4, 0L, out _);

            Assert.That(verdict, Is.EqualTo(OfferVerdict.WalkedAway),
                "Lowballing has to cost the candidate or there is no reason not to try zero first.");

            Assert.That(letter.IsClosed, Is.True);
            Assert.That(simulation.State.Staff.Headcount, Is.Zero);
        }

        [Test]
        public void PatienceRunsOut()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Development, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            var stubborn = candidate.ReservationHourlyUsd * 0.95;

            for (var round = 0; round < Negotiation.Patience - 1; round++)
            {
                Assert.That(simulation.Negotiate(letter, stubborn, 0L, out _),
                    Is.EqualTo(OfferVerdict.HeldFirm), $"Round {round} should have held firm.");
            }

            Assert.That(simulation.Negotiate(letter, stubborn, 0L, out _),
                Is.EqualTo(OfferVerdict.WalkedAway),
                "A negotiation that can be retried forever has no stakes.");
        }

        [Test]
        public void ASigningBonusCanCloseAGapTheWageCannot()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Concept, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            // A wage two per cent short, and a bonus that covers exactly the shortfall.
            var shortBy = candidate.ReservationHourlyUsd * 0.02;
            var wage = candidate.ReservationHourlyUsd - shortBy;
            var bonus = (long)Math.Ceiling(shortBy * PositionCatalog.PaidHoursPerYear);

            var verdict = simulation.Negotiate(letter, wage, bonus, out _);

            Assert.That(verdict, Is.EqualTo(OfferVerdict.Accepted),
                "The bonus is a real lever or the second field should not exist.");

            Assert.That(simulation.State.Staff.Hires.Single().HourlyWageUsd,
                Is.EqualTo(wage).Within(1e-6),
                "The bonus closed the deal; it must not become part of the salary.");
        }

        [Test]
        public void TheSigningBonusIsActuallyPaid()
        {
            var simulation = Rich();
            var candidate = FirstCandidate(simulation, PlayerSkill.Software, HireSource.Agency);

            simulation.TryApproach(candidate);
            var letter = WaitForTheLetter(simulation);

            var before = simulation.State.CashUsd;
            simulation.Negotiate(letter, candidate.AskingHourlyUsd, 50_000L, out _);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - 50_000L),
                "A bonus that closes a deal without leaving the bank is free money.");
        }

        // ---- remote --------------------------------------------------------------------------

        [Test]
        public void RemoteWorkNeedsNoDesk()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 20_000_000;

            // The garage, filled to the last desk.
            var desks = simulation.State.Staff.Desks;

            for (var index = 0; index < desks; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 2, GameDate.Start));
            }

            Assert.That(simulation.State.Staff.HasFreeSeat, Is.False);

            var candidate = FirstCandidate(simulation, PlayerSkill.Software, HireSource.Remote);

            Assert.That(simulation.TryApproach(candidate), Is.Empty,
                "Remote exists so a company with no room can still start a team.");
        }

        [Test]
        public void RemoteIsCappedAtFiveWithoutThePartnership()
        {
            var simulation = Rich();

            for (var index = 0; index < HiringChannels.FreeRemoteSeats; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.InfrastructureEngineer, 1,
                    GameDate.Start, $"Contractor {index}", PlayerSkill.Software,
                    HireSource.Remote, 40.0));
            }

            var candidate = FirstCandidate(simulation, PlayerSkill.Software, HireSource.Remote);

            Assert.That(simulation.TryApproach(candidate), Is.Not.Empty,
                "The sixth remote contract has to be blocked until IThand is paid.");
        }

        [Test]
        public void ThePartnershipLiftsTheCapAndCostsMoney()
        {
            var simulation = Rich();
            var before = simulation.State.CashUsd;

            Assert.That(simulation.TryBuyRemotePartnership(), Is.Empty);
            Assert.That(simulation.State.CashUsd,
                Is.EqualTo(before - HiringChannels.PartnershipCostUsd));

            Assert.That(simulation.State.Hiring.RemoteSeats,
                Is.EqualTo(HiringChannels.PartneredRemoteSeats));
        }

        [Test]
        public void SomebodyOnSiteStillNeedsADesk()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 20_000_000;

            for (var index = 0; index < simulation.State.Staff.Desks; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 2, GameDate.Start));
            }

            var candidate = FirstCandidate(simulation, PlayerSkill.Concept, HireSource.Agency);

            Assert.That(simulation.TryApproach(candidate), Does.Contain("desk"),
                "The lease is still the cap for anybody who actually comes in.");
        }

        // ---- the specialist fee ------------------------------------------------------------------

        [Test]
        public void AskingForMoreCostsMore()
        {
            var cheap = CompanySimulation.SpecialistFeeUsd(PlayerSkill.Development, 20);
            var dear = CompanySimulation.SpecialistFeeUsd(PlayerSkill.Development, 90);

            Assert.That(dear, Is.GreaterThan(cheap * 2L),
                "Without a rising fee the player would always drag the slider to a hundred.");
        }

        // ---- what the tiles count -----------------------------------------------------------------

        [Test]
        public void TilesCountByPositionNotByDepartment()
        {
            var roster = new StaffRoster();
            roster.SetOffice(OfficeTier.Floor);

            // Two disciplines that share one department. A tile counting roles would show 2 on both.
            roster.Add(new Hire(StaffRole.ResearchScientist, 3, GameDate.Start, "A",
                PlayerSkill.Development, HireSource.Agency, 100.0));

            roster.Add(new Hire(StaffRole.ResearchScientist, 3, GameDate.Start, "B",
                PlayerSkill.Concept, HireSource.Agency, 100.0));

            Assert.That(roster.CountOfPosition(PlayerSkill.Development), Is.EqualTo(1));
            Assert.That(roster.CountOfPosition(PlayerSkill.Concept), Is.EqualTo(1));
        }

        [Test]
        public void ALegacyHireStillCostsWhatItAlwaysCost()
        {
            var old = new Hire(StaffRole.DataEngineer, 3, GameDate.Start);

            Assert.That(old.HourlyWageUsd, Is.Zero);
            Assert.That(old.SalaryPerYearUsd,
                Is.EqualTo(StaffCatalog.Get(StaffRole.DataEngineer).SalaryPerYearUsd(3)),
                "Migrating a save must not give anybody a pay rise or a cut.");
        }

        // ---- the save ------------------------------------------------------------------------------

        [Test]
        public void AnApproachAndANegotiatedHireSurviveASave()
        {
            var simulation = Rich();

            var signed = FirstCandidate(simulation, PlayerSkill.Safety, HireSource.Specialist);
            simulation.TryApproach(signed);
            var letter = WaitForTheLetter(simulation);
            simulation.AcceptAsking(letter, out _);

            var waiting = FirstCandidate(simulation, PlayerSkill.Software, HireSource.Remote);
            simulation.TryApproach(waiting);
            simulation.TryBuyRemotePartnership();

            var data = SaveStore.Capture(simulation.State);
            var reloaded = SaveStore.Restore(data);

            var hire = reloaded.Staff.Hires.Single();

            Assert.That(hire.Name, Is.EqualTo(signed.Name));
            Assert.That(hire.Position, Is.EqualTo(PlayerSkill.Safety));
            Assert.That(hire.Source, Is.EqualTo(HireSource.Specialist));
            Assert.That(hire.HourlyWageUsd, Is.EqualTo(signed.AskingHourlyUsd).Within(0.01));

            Assert.That(reloaded.Hiring.HasRemotePartnership, Is.True);
            Assert.That(reloaded.Hiring.OpenCount, Is.EqualTo(1));
            Assert.That(reloaded.Hiring.Approaches.Single().Candidate.Name,
                Is.EqualTo(waiting.Name),
                "A conversation that vanishes on load is a banner counting down to nothing.");
        }
    }
}
