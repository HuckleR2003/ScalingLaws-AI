using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The team, the room they sit in, and what happens when a capable model misbehaves in public.
    /// </summary>
    public sealed class StaffAndSafetyTests
    {
        private static CompanySimulation Company(long cash, GameDate date, double capability = 0.0)
        {
            var state = new CompanyState("Staffed", 31)
            {
                Date = date,
                CashUsd = cash
            };

            if (capability > 0.0)
            {
                state.AddDeployedModel(new DeployedModel(
                    "Flagship", ArchitectureId.DenseTransformer, capability, date, 2e10, 1.0));
            }

            return new CompanySimulation(state);
        }

        // ------------------------------------------------------------------ hiring

        [Test]
        public void ACompanyStartsInAGarageWithNobodyInIt()
        {
            var state = new CompanyState("Two founders");

            Assert.That(state.Staff.Office, Is.EqualTo(OfficeTier.Garage));
            Assert.That(state.Staff.Headcount, Is.Zero);
            // Zero, and it is the point. The house has nowhere for a second person to sit, so the
            // first hire is not a purchase, it is a move. Changed with the office ladder on
            // 2026-08-15; this used to be the garage's four desks.
            Assert.That(state.Staff.Desks, Is.EqualTo(0));
            Assert.That(state.Staff.DailyPayrollUsd, Is.Zero);
            Assert.That(state.Staff.DailyRentUsd, Is.GreaterThan(0L), "Even a garage costs something.");
        }

        /// <summary>
        /// Desks still cap headcount, checked through the door the player actually uses.
        ///
        /// **This used to drive a TryHire method that no longer exists.** Hiring goes through an
        /// approach now, so the cap has to be enforced at the point where the company writes to
        /// somebody; a test still pointed at the old call would have kept passing while the screen
        /// in front of the player let them overfill the floor.
        /// </summary>
        [Test]
        public void DesksAreAHardCapOnHeadcount()
        {
            var simulation = Company(50_000_000, GameDate.Start);

            Candidate Somebody() =>
                simulation.Shortlist(PlayerSkill.Development, HireSource.Agency, 40, 1)[0];

            // The house has no desks at all, so the first hire is a move rather than a purchase.
            Assert.That(simulation.TryApproach(Somebody()), Does.Contain("desk"),
                "Nobody can be seated at home, so nobody can be approached for a seat there.");

            Assert.That(simulation.TryMoveOffice(OfficeTier.Loft, out var moveReason), Is.True, moveReason);

            var desks = simulation.State.Staff.Desks;
            for (var index = 0; index < desks; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 2, GameDate.Start));
            }

            Assert.That(simulation.TryApproach(Somebody()), Does.Contain("desk"),
                "The desk count is a hard cap, whatever the office.");

            Assert.That(simulation.State.Staff.Headcount, Is.EqualTo(desks));

            Assert.That(simulation.TryMoveOffice(OfficeTier.Floor, out var bigger), Is.True, bigger);
            Assert.That(simulation.TryApproach(Somebody()), Is.Empty,
                "A bigger lease has to actually free the constraint.");
        }

        [Test]
        public void SalaryRisesFasterThanSkillDoes()
        {
            var definition = StaffCatalog.Get(StaffRole.ResearchScientist);

            var one = definition.SalaryPerYearUsd(1);
            var five = definition.SalaryPerYearUsd(5);

            Assert.That(five, Is.GreaterThan(one * 5),
                "A five has to cost more than five ones, or nobody would ever hire a one.");
        }

        [Test]
        public void EveryExtraHeadInARoleIsWorthLessThanTheLastOne()
        {
            var roster = new StaffRoster();
            roster.SetOffice(OfficeTier.Campus);

            var strengths = new double[8];
            for (var index = 0; index < strengths.Length; index++)
            {
                roster.Add(new Hire(StaffRole.ResearchScientist, 3, GameDate.Start));
                strengths[index] = roster.Strength(StaffRole.ResearchScientist);
            }

            for (var index = 2; index < strengths.Length; index++)
            {
                var previousStep = strengths[index - 1] - strengths[index - 2];
                var thisStep = strengths[index] - strengths[index - 1];
                Assert.That(thisStep, Is.LessThan(previousStep),
                    $"Head {index + 1} added at least as much as head {index}.");
            }
        }

        [Test]
        public void PayrollAndRentLeaveTheAccountEveryDay()
        {
            var staffed = Company(60_000_000, GameDate.Start);
            staffed.TryMoveOffice(OfficeTier.Loft, out _);
            for (var index = 0; index < 6; index++)
            {
                staffed.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 4, GameDate.Start));
            }

            var empty = Company(60_000_000, GameDate.Start);
            var cashBefore = staffed.State.CashUsd;

            staffed.Advance(180);
            empty.Advance(180);

            Assert.That(staffed.State.CashUsd, Is.LessThan(cashBefore));
            Assert.That(staffed.State.CashUsd, Is.LessThan(empty.State.CashUsd),
                "A payroll has to be visible in the numbers.");
        }

        // ------------------------------------------------------------------ the borrowed gem

        [Test]
        public void TwoIdenticalRunsWithDifferentTeamsDoNotProduceIdenticalModels()
        {
            // The idea taken from Devices Tycoon: employee quality is a hidden driver of the result.
            // Here it lands on the spread of a run, not on its ceiling.
            static double SpreadFor(int scientists, int skill)
            {
                var roster = new StaffRoster();
                roster.SetOffice(OfficeTier.Floor);
                for (var index = 0; index < scientists; index++)
                {
                    roster.Add(new Hire(StaffRole.ResearchScientist, skill, GameDate.Start));
                }

                return roster.OutcomeVarianceMultiplier();
            }

            var nobody = SpreadFor(0, 1);
            var juniors = SpreadFor(3, 1);
            var seniors = SpreadFor(3, 5);

            Assert.That(nobody, Is.EqualTo(1.0).Within(1e-9), "No team means the full spread.");
            Assert.That(juniors, Is.LessThan(nobody));
            Assert.That(seniors, Is.LessThan(juniors), "Skill has to beat headcount.");
            Assert.That(seniors, Is.GreaterThan(0.2), "A team can tighten a run, never make it certain.");
        }

        [Test]
        public void EachRoleMovesTheThingItIsSupposedToAndNothingElse()
        {
            static StaffRoster With(StaffRole role)
            {
                var roster = new StaffRoster();
                roster.SetOffice(OfficeTier.Floor);
                for (var index = 0; index < 4; index++)
                {
                    roster.Add(new Hire(role, 4, GameDate.Start));
                }

                return roster;
            }

            Assert.That(With(StaffRole.InfrastructureEngineer).UtilizationBonus(), Is.GreaterThan(0.0));
            Assert.That(With(StaffRole.InfrastructureEngineer).IncidentRiskMultiplier(), Is.EqualTo(1.0).Within(1e-9));

            Assert.That(With(StaffRole.DataEngineer).DataQualityMultiplier(), Is.GreaterThan(1.0));
            Assert.That(With(StaffRole.DataEngineer).UtilizationBonus(), Is.Zero);

            Assert.That(With(StaffRole.SafetyEngineer).IncidentRiskMultiplier(), Is.LessThan(1.0));
            Assert.That(With(StaffRole.GoToMarket).BrandBonus(), Is.GreaterThan(0.0));
            Assert.That(With(StaffRole.ResearchScientist).ResearchSpeedMultiplier(), Is.LessThan(1.0));
        }

        [Test]
        public void ABetterOfficeMakesTheSamePeopleWorthMore()
        {
            static double StrengthIn(OfficeTier tier)
            {
                var roster = new StaffRoster();
                roster.SetOffice(tier);
                for (var index = 0; index < 4; index++)
                {
                    roster.Add(new Hire(StaffRole.ResearchScientist, 3, GameDate.Start));
                }

                return roster.Strength(StaffRole.ResearchScientist);
            }

            Assert.That(StrengthIn(OfficeTier.Campus), Is.GreaterThan(StrengthIn(OfficeTier.Garage)));
        }

        [Test]
        public void YouCannotMoveIntoAnOfficeSmallerThanTheTeam()
        {
            var simulation = Company(400_000_000, GameDate.FromCalendar(2024, 1, 1));
            simulation.TryMoveOffice(OfficeTier.Floor, out _);
            for (var index = 0; index < 10; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.GoToMarket, 1, GameDate.Start));
            }

            Assert.That(simulation.TryMoveOffice(OfficeTier.Garage, out var reason), Is.False);
            Assert.That(reason, Does.Contain("holds 0"));
        }

        // ------------------------------------------------------------------ incidents

        [Test]
        public void AModelAtParOnSafetyIsRiskyButNotReckless()
        {
            var date = GameDate.FromCalendar(2024, 6, 1);
            var model = new DeployedModel("Par", ArchitectureId.SparseMixture, 50, date, 5e10, 1.0);

            var risk = IncidentModel.DailyRisk(model, date, 1.0);
            var yearly = 1.0 - Math.Pow(1.0 - risk, 365.0);

            Assert.That(yearly, Is.InRange(0.02, 0.35),
                $"A par model should not be safe and should not be doomed. Annual risk {yearly:P1}.");
        }

        [Test]
        public void NeglectingSafetyRaisesTheRiskAndInvestingInItLowersIt()
        {
            var released = GameDate.FromCalendar(2023, 1, 1);
            var later = GameDate.FromCalendar(2025, 6, 1);

            var neglected = new DeployedModel("Neglected", ArchitectureId.DenseTransformer, 55, released, 4e10, 1.0);
            var maintained = new DeployedModel("Maintained", ArchitectureId.DenseTransformer, 55, released, 4e10, 1.0);
            maintained.Traits.SetLevel(
                ModelTrait.Safety, ModelTraitCatalog.Get(ModelTrait.Safety).ExpectedLevelOn(later) + 2);

            var neglectedRisk = IncidentModel.DailyRisk(neglected, later, 1.0);
            var maintainedRisk = IncidentModel.DailyRisk(maintained, later, 1.0);

            Assert.That(neglectedRisk, Is.GreaterThan(maintainedRisk * 2.0),
                $"Neglected {neglectedRisk:E2} against maintained {maintainedRisk:E2}.");
        }

        [Test]
        public void TheBetterTheModelTheMoreANeglectedSafetyLineCosts()
        {
            // The inversion that makes Safety worth maintaining: a weak model misbehaving is a
            // support ticket, a frontier model misbehaving is a regulatory event.
            var date = GameDate.FromCalendar(2025, 1, 1);

            var weak = new DeployedModel("Weak", ArchitectureId.DenseTransformer, 25, date, 2e10, 1.0);
            var strong = new DeployedModel("Strong", ArchitectureId.DenseTransformer, 75, date, 2e10, 1.0);

            Assert.That(IncidentModel.DailyRisk(strong, date, 1.0),
                Is.GreaterThan(IncidentModel.DailyRisk(weak, date, 1.0) * 2.0));
        }

        [Test]
        public void ASafetyTeamIsTheOnlyThingThatMovesTheRiskFromOutsideTheModel()
        {
            var date = GameDate.FromCalendar(2025, 1, 1);
            var model = new DeployedModel("Flagship", ArchitectureId.DenseTransformer, 65, date, 3e10, 1.0);

            var roster = new StaffRoster();
            roster.SetOffice(OfficeTier.Floor);
            for (var index = 0; index < 5; index++)
            {
                roster.Add(new Hire(StaffRole.SafetyEngineer, 4, GameDate.Start));
            }

            var unguarded = IncidentModel.DailyRisk(model, date, 1.0);
            var guarded = IncidentModel.DailyRisk(model, date, roster.IncidentRiskMultiplier());

            Assert.That(guarded, Is.LessThan(unguarded * 0.7));
            Assert.That(guarded, Is.GreaterThan(0.0), "No team makes a capable model safe.");
        }

        [Test]
        public void AFineScalesWithTheCompanyAndNeverFallsBelowItsFloor()
        {
            var date = GameDate.FromCalendar(2025, 1, 1);
            var model = new DeployedModel("Flagship", ArchitectureId.DenseTransformer, 70, date, 3e10, 1.0);

            var sawMajorOrWorse = false;
            var random = new DeterministicRandom(4);

            for (var attempt = 0; attempt < 200; attempt++)
            {
                var big = IncidentModel.Resolve(model, date, 5_000_000_000L, random);
                var small = IncidentModel.Resolve(model, date, 0L, random);

                if (big.Severity >= IncidentSeverity.Major)
                {
                    sawMajorOrWorse = true;
                    Assert.That(big.FineUsd, Is.GreaterThan(IncidentModel.MinimumMajorFineUsd),
                        "A large company has to pay more than the floor.");
                }

                if (small.Severity >= IncidentSeverity.Major)
                {
                    Assert.That(small.FineUsd, Is.GreaterThanOrEqualTo(IncidentModel.MinimumMajorFineUsd),
                        "A pre-revenue lab still has to feel it.");
                }

                Assert.That(big.ReputationLoss, Is.GreaterThan(0.0));
            }

            Assert.That(sawMajorOrWorse, Is.True, "Two hundred incidents should include a serious one.");
        }

        [Test]
        public void ASevereIncidentTakesTheModelOffTheMarket()
        {
            var date = GameDate.FromCalendar(2025, 1, 1);
            var model = new DeployedModel("Flagship", ArchitectureId.DenseTransformer, 70, date, 3e10, 1.0);

            // Two levels behind par pushes the severity roll upward.
            model.Traits.SetLevel(ModelTrait.Safety, 0);

            var random = new DeterministicRandom(11);
            var sawWithdrawal = false;
            for (var attempt = 0; attempt < 300 && !sawWithdrawal; attempt++)
            {
                if (IncidentModel.Resolve(model, date, 100_000_000L, random).ForcedWithdrawal)
                {
                    sawWithdrawal = true;
                }
            }

            Assert.That(sawWithdrawal, Is.True, "A badly neglected model has to be pullable from sale.");
        }

        [Test]
        public void AnIncidentInPlayCostsCashStandingAndSometimesTheProduct()
        {
            // Risk is a daily roll, so one campaign proves nothing. Across several seeds a frontier
            // model with no safety work at all has to go wrong at least once.
            var campaignsWithAnIncident = 0;
            var reputationFell = 0;

            for (uint seed = 1; seed <= 5; seed++)
            {
                var state = new CompanyState("Reckless", seed)
                {
                    Date = GameDate.FromCalendar(2025, 1, 1),
                    CashUsd = 900_000_000
                };
                state.AddDeployedModel(new DeployedModel(
                    "Flagship", ArchitectureId.DenseTransformer, 78, state.Date, 2e10, 1.0));
                state.DeployedModels[0].Traits.SetLevel(ModelTrait.Safety, 0);

                var simulation = new CompanySimulation(state);
                var reputationBefore = state.Reputation;
                simulation.Advance(900);

                if (state.Incidents.Count > 0)
                {
                    campaignsWithAnIncident++;
                    if (state.Reputation < reputationBefore)
                    {
                        reputationFell++;
                    }
                }
            }

            Assert.That(campaignsWithAnIncident, Is.GreaterThan(0),
                "Across five campaigns, a neglected frontier model has to go wrong at least once.");
            Assert.That(reputationFell, Is.EqualTo(campaignsWithAnIncident),
                "Every incident has to cost standing.");
        }

        [Test]
        public void ACompanyWithNothingLiveCannotHaveAnIncident()
        {
            var simulation = Company(50_000_000, GameDate.FromCalendar(2025, 1, 1));

            Assert.That(simulation.DailyIncidentRisk(), Is.Zero);
            simulation.Advance(400);
            Assert.That(simulation.State.Incidents, Is.Empty);
        }

        // ------------------------------------------------------------------ persistence

        [Test]
        public void TheTeamTheOfficeAndTheRecordSurviveASaveAndReload()
        {
            var simulation = Company(300_000_000, GameDate.FromCalendar(2024, 6, 1), capability: 60.0);
            simulation.TryMoveOffice(OfficeTier.Floor, out _);
            simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 5, GameDate.Start));
            simulation.State.Staff.Add(new Hire(StaffRole.SafetyEngineer, 3, GameDate.Start));
            simulation.State.Staff.Add(new Hire(StaffRole.GoToMarket, 2, GameDate.Start));
            simulation.Advance(120);

            var original = simulation.State;
            var restored = SaveStore.Restore(SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(original))));

            Assert.That(restored.Staff.Office, Is.EqualTo(OfficeTier.Floor));
            Assert.That(restored.Staff.Headcount, Is.EqualTo(original.Staff.Headcount));
            Assert.That(restored.Staff.DailyPayrollUsd, Is.EqualTo(original.Staff.DailyPayrollUsd));
            Assert.That(restored.Staff.CountOf(StaffRole.ResearchScientist), Is.EqualTo(1));
            Assert.That(restored.Staff.Strength(StaffRole.SafetyEngineer),
                Is.EqualTo(original.Staff.Strength(StaffRole.SafetyEngineer)).Within(1e-9));
            Assert.That(restored.Incidents.Count, Is.EqualTo(original.Incidents.Count));
            Assert.That(restored.LifetimeFinesUsd, Is.EqualTo(original.LifetimeFinesUsd));
        }

        [Test]
        public void ASaveFromEveryOlderVersionStillLoads()
        {
            // The migration chain is a loop now, not a hand-written nest, because the nest had
            // already been caught stopping short of the newest version.
            var legacy = new SaveDataV1
            {
                version = 1,
                companyName = "Legacy",
                dayIndex = GameDate.FromCalendar(2024, 1, 1).DayIndex,
                cashUsd = 30_000_000,
                randomState = 99,
                ownedDataSources = (int)DatasetSource.WebCrawl,
                ownedAccelerators = 256,
                rentedAccelerators = 100
            };

            var upgraded = SaveStore.Parse(JsonUtility.ToJson(legacy));

            Assert.That(upgraded, Is.Not.Null);
            Assert.That(upgraded.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(upgraded.staff, Is.Not.Null);
            Assert.That(upgraded.loans, Is.Not.Null);
            Assert.That(upgraded.customArchitectures, Is.Not.Null);
            Assert.That(upgraded.rentedPetaflops, Is.GreaterThan(0.0));
            Assert.That(SaveMigration.LastMigrationNotes, Does.Contain("v7 to v8"),
                "Every step in the chain has to have run.");

            var state = SaveStore.Restore(upgraded);
            Assert.That(state.Staff.Office, Is.EqualTo(OfficeTier.Garage));
            new CompanySimulation(state).Advance(60);
            Assert.That(state.Date, Is.EqualTo(GameDate.FromCalendar(2024, 3, 1)));
        }
    }
}
