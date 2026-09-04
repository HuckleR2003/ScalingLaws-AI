using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Era five: the state programme, and the trade it is built on.
    ///
    /// **The claim being tested is that this is not free money.** A sector pays more in a day than
    /// most of this campaign earns in a month, and every one of them takes capacity off the top,
    /// before training and before the customers who got the company here. A player who signs for
    /// four sectors and stops building has sold their own market and put a government on a cluster
    /// that cannot serve it.
    /// </summary>
    public sealed class StatecraftTests
    {
        private static CompanySimulation Company(uint seed = 41)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.State.CashUsd = 20_000_000_000L;
            simulation.State.ResearchPoints = 200_000;

            return simulation;
        }

        private static void GrantResearch(CompanyState state, params ResearchNodeId[] nodes)
        {
            foreach (var node in nodes)
            {
                state.UnlockedResearch.Add(node);
            }
        }

        // ---- the gate -----------------------------------------------------------------------------

        /// <summary>
        /// A government wants the research and five clean years, and refuses to say why only once.
        ///
        /// **Both halves matter and the second is the point of the era.** The node is money and
        /// calendar, which this game is full of. The record is neither: it is a fact about what the
        /// company has done, and the only way to move it is to have not done the thing.
        /// </summary>
        [Test]
        public void NobodySignsWithoutTheResearchAndFiveCleanYears()
        {
            var simulation = Company();
            var state = simulation.State;

            Assert.IsFalse(simulation.CanSignStateProgramme(out var noNode));
            StringAssert.Contains(ResearchTree.Get(ResearchNodeId.SovereignLiaison).DisplayName,
                noNode, "The refusal has to name the node that would fix it.");

            GrantResearch(state, ResearchNodeId.SovereignLiaison);

            Assert.IsTrue(simulation.CanSignStateProgramme(out _),
                "A clean company with the research should be able to sign.");

            // One severe incident, and the door shuts.
            state.Incidents.Add(new SafetyIncident(IncidentSeverity.Severe, state.Date,
                "A severe incident", 0.2, 90_000_000L, false));

            Assert.IsFalse(simulation.CanSignStateProgramme(out var record));
            StringAssert.Contains("%", record, "The refusal has to say where the record stands.");

            Assert.Less(SafetyRecord.For(state, state.Date), SafetyRecord.ContractThreshold);
        }

        /// <summary>
        /// The record heals with time and nothing else.
        ///
        /// The one gate money cannot move. A company that has a severe incident today is below the
        /// bar for years however rich it gets, and this asserts both that it is below and that
        /// waiting is genuinely a way back rather than a permanent ban.
        /// </summary>
        [Test]
        public void TheRecordHealsWithTimeAndWithNothingElse()
        {
            var state = new CompanyState("Adco", 7);
            var start = state.Date;

            state.Incidents.Add(new SafetyIncident(IncidentSeverity.Severe, start,
                "A severe incident", 0.2, 90_000_000L, false));

            Assert.Less(SafetyRecord.For(state, start), SafetyRecord.ContractThreshold);

            // Money changes nothing.
            state.CashUsd = 500_000_000_000L;
            Assert.Less(SafetyRecord.For(state, start), SafetyRecord.ContractThreshold);

            // Time does.
            var later = start.AddDays(SafetyRecord.WindowDays + 1);
            Assert.AreEqual(1.0, SafetyRecord.For(state, later), 0.001,
                "An incident older than the window still counts against the company.");

            // And it climbs on the way rather than stepping on one invisible anniversary.
            var half = SafetyRecord.For(state, start.AddDays(SafetyRecord.WindowDays / 2));

            Assert.Greater(half, SafetyRecord.For(state, start));
            Assert.Less(half, 1.0);
        }

        // ---- the board -----------------------------------------------------------------------------

        /// <summary>
        /// No sector is cheaper, safer and better paid than another.
        ///
        /// **The same guard the marketing channels and the smear tiers are held to.** The moment
        /// one row wins on every axis the board stops being a decision and becomes a list to work
        /// down in a fixed order, and the whole ending is a formality.
        /// </summary>
        [Test]
        public void NoSectorIsSimplyBetterThanAnother()
        {
            var beaten = new List<string>();

            foreach (var a in StateSectorCatalog.All)
            {
                foreach (var b in StateSectorCatalog.All)
                {
                    if (a.Sector == b.Sector)
                    {
                        continue;
                    }

                    var paysMore = a.FeeUsdPerDay > b.FeeUsdPerDay;
                    var holdsLess = a.PetaflopsRequired <= b.PetaflopsRequired;
                    var safer = a.FailureWeight <= b.FailureWeight;
                    var cheaperToLearn = a.ResearchPoints <= b.ResearchPoints;
                    var costsLessWhenItBreaks = a.FailureCostUsd <= b.FailureCostUsd;

                    if (paysMore && holdsLess && safer && cheaperToLearn && costsLessWhenItBreaks)
                    {
                        beaten.Add($"{a.DisplayName} beats {b.DisplayName} on every axis");
                    }
                }
            }

            CollectionAssert.IsEmpty(beaten, string.Join("; ", beaten));
        }

        /// <summary>
        /// The dangerous end pays better and the safe end is safer, and neither is a free lunch.
        ///
        /// Reading the fee column alone should lead a player to Defence, and reading the risk column
        /// alone should lead them to Bureaucracy. That tension is the board.
        /// </summary>
        [Test]
        public void TheBestPaidSectorIsAlsoTheMostDangerous()
        {
            StateSectorDefinition richest = null;
            StateSectorDefinition riskiest = null;
            StateSectorDefinition safest = null;

            foreach (var sector in StateSectorCatalog.All)
            {
                if (richest == null || sector.FeeUsdPerDay > richest.FeeUsdPerDay)
                {
                    richest = sector;
                }

                if (riskiest == null || sector.FailureWeight > riskiest.FailureWeight)
                {
                    riskiest = sector;
                }

                if (safest == null || sector.FailureWeight < safest.FailureWeight)
                {
                    safest = sector;
                }
            }

            Assert.AreEqual(richest.Sector, riskiest.Sector,
                "The best paid sector is not the most dangerous, so the fee column is safe to read "
                + "on its own and the board has no trade in it.");

            Assert.Less(safest.FeeUsdPerDay, richest.FeeUsdPerDay);
            Assert.AreEqual(StateSector.Bureaucracy, safest.Sector);
        }

        /// <summary>A sector needs its chain, its points and its money, and says which is missing.</summary>
        [Test]
        public void ASectorNeedsItsChainItsPointsAndItsMoney()
        {
            var simulation = Company();
            var state = simulation.State;

            GrantResearch(state, ResearchNodeId.SovereignLiaison);

            Assert.IsFalse(simulation.CanStartSector(StateSector.Bureaucracy, out var unsigned));
            Assert.IsNotEmpty(unsigned);

            Assert.IsTrue(simulation.TrySignStateProgramme(out var why), why);

            // Logistics stands on Bureaucracy.
            Assert.IsFalse(simulation.CanStartSector(StateSector.Logistics, out var chain));
            StringAssert.Contains(StateSectorCatalog.Get(StateSector.Bureaucracy).DisplayName, chain);

            Assert.IsTrue(simulation.TryStartSector(StateSector.Bureaucracy, out why), why);
            Assert.IsTrue(state.Programme.IsRunning(StateSector.Bureaucracy));

            Assert.IsTrue(simulation.CanStartSector(StateSector.Logistics, out _));

            // Points are the real gate, as everywhere in the tree.
            state.ResearchPoints = 10;

            Assert.IsFalse(simulation.CanStartSector(StateSector.Logistics, out var points));
            StringAssert.Contains("10", points);
        }

        /// <summary>
        /// A sector cannot be handed back while something is standing on it.
        ///
        /// Otherwise the chain in the catalog stops meaning anything and a player can run Defence
        /// with no Security underneath it, which is exactly the configuration the chain exists to
        /// forbid.
        /// </summary>
        [Test]
        public void ASectorHoldingUpAnotherCannotBeHandedBack()
        {
            var simulation = Company();
            var state = simulation.State;

            GrantResearch(state, ResearchNodeId.SovereignLiaison);
            simulation.TrySignStateProgramme(out _);

            Assert.IsTrue(simulation.TryStartSector(StateSector.Bureaucracy, out var why), why);
            Assert.IsTrue(simulation.TryStartSector(StateSector.Logistics, out why), why);

            Assert.IsFalse(simulation.TryStopSector(StateSector.Bureaucracy, out var held));
            StringAssert.Contains(StateSectorCatalog.Get(StateSector.Logistics).DisplayName, held);

            Assert.IsTrue(simulation.TryStopSector(StateSector.Logistics, out why), why);
            Assert.IsTrue(simulation.TryStopSector(StateSector.Bureaucracy, out why), why);

            // And nothing is refunded, which is what makes taking one a commitment.
            Assert.Less(state.ResearchPoints, 200_000);
        }

        // ---- the trade -------------------------------------------------------------------------------

        /// <summary>
        /// **The claim the whole ending rests on: the state takes capacity from the customers.**
        ///
        /// Measured through the profile rather than asserted, because a reservation that does not
        /// reach the market is a fee with no cost attached and the endgame becomes free money.
        /// </summary>
        [Test]
        public void EverySectorTakesCapacityFromThePayingPublic()
        {
            var simulation = Company();
            var state = simulation.State;

            GrantResearch(state, ResearchNodeId.SovereignLiaison);
            simulation.TrySignStateProgramme(out _);

            Assert.Greater(simulation.StateReservedPetaflops(), 0.0,
                "A signed programme holds nothing at all, so it costs the company nothing.");

            var beforeSector = simulation.StateReservedPetaflops();

            Assert.IsTrue(simulation.TryStartSector(StateSector.Bureaucracy, out var why), why);

            Assert.Greater(simulation.StateReservedPetaflops(), beforeSector,
                "Running a sector took no capacity, so it is a fee with no cost.");

            // And the profile the market is served from is genuinely smaller.
            var profile = new ComputeProfile(10, 0, 0, 4_000, 3_000, 0.8, 1, 1, 0, 0, 0, 0, 0, 0);
            var open = profile.WithReserved(simulation.StateReservedPetaflops());

            Assert.Less(open.EffectivePetaflops, profile.EffectivePetaflops);
            Assert.AreEqual(profile.RawPetaflops, open.RawPetaflops,
                "The company still owns the cards and still pays for them.");
        }

        /// <summary>
        /// Falling short costs money first and safety second, in that order.
        ///
        /// The warning shot is the point: a programme at eighty per cent is paid at eighty per cent,
        /// which shows in the books the same week, months before the failure it is also making more
        /// likely.
        /// </summary>
        [Test]
        public void FallingShortCutsTheFeeBeforeItRaisesTheRisk()
        {
            var simulation = Company();
            var state = simulation.State;

            GrantResearch(state, ResearchNodeId.SovereignLiaison);
            simulation.TrySignStateProgramme(out _);
            simulation.TryStartSector(StateSector.Bureaucracy, out _);

            var programme = state.Programme;

            var full = programme.EarnedUsdPerDay(1.0);
            var short8 = programme.EarnedUsdPerDay(0.8);

            Assert.Greater(full, 0L);
            Assert.Less(short8, full, "A programme that is short is paid in full.");

            var safeRisk = simulation.StateFailureRisk(1.0);
            var shortRisk = simulation.StateFailureRisk(0.8);
            var brokenRisk = simulation.StateFailureRisk(0.0);

            Assert.Greater(safeRisk, 0.0, "Perfect delivery is free of risk, so the ending is safe.");
            Assert.Greater(shortRisk, safeRisk);
            Assert.Greater(brokenRisk, shortRisk * 2.0,
                "Delivering nothing is barely worse than delivering most of it.");
        }

        /// <summary>
        /// The two safety nodes are worth having and are never worth skipping capacity for.
        /// </summary>
        [Test]
        public void OversightAndRedundancyHelpWithoutReplacingCapacity()
        {
            var simulation = Company();
            var state = simulation.State;

            GrantResearch(state, ResearchNodeId.SovereignLiaison);
            simulation.TrySignStateProgramme(out _);
            simulation.TryStartSector(StateSector.Bureaucracy, out _);

            var bare = simulation.StateFailureRisk(0.8);

            GrantResearch(state, ResearchNodeId.ContinuousOversight);
            var watched = simulation.StateFailureRisk(0.8);

            Assert.Less(watched, bare, "Continuous oversight changes nothing.");

            // Redundancy improves the delivery reading rather than the risk directly.
            var profile = new ComputeProfile(10, 0, 0, 4_000, 200, 0.8, 1, 1, 0, 0, 0, 0, 0, 0);
            var without = simulation.StateDelivery(profile);

            GrantResearch(state, ResearchNodeId.RedundantInference);
            var with = simulation.StateDelivery(profile);

            Assert.Greater(with, without);
            Assert.Less(with, 1.0,
                "Redundant inference is standing in for capacity, so nobody ever has to build any.");
        }

        /// <summary>
        /// The most dangerous sector is the most likely one to be the one that fails.
        ///
        /// Otherwise the failure weights are decoration and Defence is simply the best paid row.
        /// </summary>
        [Test]
        public void TheRiskiestSectorIsTheOneMostLikelyToFail()
        {
            var programme = new StateProgramme();
            programme.Sign(Country.Poland, new GameDate(0));

            programme.Start(StateSector.Bureaucracy);
            programme.Start(StateSector.Health);

            var bureaucracy = StateSectorCatalog.Get(StateSector.Bureaucracy).FailureWeight;
            var health = StateSectorCatalog.Get(StateSector.Health).FailureWeight;

            Assert.Greater(health, bureaucracy);

            // A roll just inside the first band picks the first sector in catalog order, and one
            // past it picks the other. The bands are the weights, so the larger weight is the
            // larger band.
            Assert.AreEqual(StateSector.Bureaucracy, programme.SectorForRoll(bureaucracy * 0.5));
            Assert.AreEqual(StateSector.Health, programme.SectorForRoll(bureaucracy + health * 0.5));

            // Deterministic: the same roll gives the same answer whatever order the set was built.
            var mirrored = new StateProgramme();
            mirrored.Sign(Country.Poland, new GameDate(0));
            mirrored.Start(StateSector.Health);
            mirrored.Start(StateSector.Bureaucracy);

            Assert.AreEqual(programme.SectorForRoll(bureaucracy * 0.5),
                mirrored.SectorForRoll(bureaucracy * 0.5),
                "The same roll picked different sectors depending on the order the set was built, "
                + "so a reloaded campaign does not replay.");
        }

        // ---- the save -----------------------------------------------------------------------------

        /// <summary>
        /// The programme survives a save, including yesterday's delivery.
        ///
        /// **Delivery looks derived and is causal**, which is the sixth time in this project. A
        /// campaign reloaded without it rolls different odds than the run that wrote it.
        /// </summary>
        [Test]
        public void TheProgrammeSurvivesASave()
        {
            var simulation = Company(88);
            var state = simulation.State;

            GrantResearch(state, ResearchNodeId.SovereignLiaison);
            simulation.TrySignStateProgramme(out _);
            simulation.TryStartSector(StateSector.Bureaucracy, out _);
            simulation.TryStartSector(StateSector.Logistics, out _);

            state.Programme.RecordDelivery(0.73);
            state.Programme.RecordFailure(state.Date, 1_000_000_000L);

            var json = UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state));
            var restored = SaveStore.Restore(SaveStore.Parse(json));

            Assert.IsTrue(restored.Programme.IsSigned, "The contract was lost on load.");
            Assert.AreEqual(state.HomeCountry, restored.Programme.Signatory);

            Assert.IsTrue(restored.Programme.IsRunning(StateSector.Bureaucracy));
            Assert.IsTrue(restored.Programme.IsRunning(StateSector.Logistics));
            Assert.IsFalse(restored.Programme.IsRunning(StateSector.Defence));

            Assert.AreEqual(0.73, restored.Programme.LastDelivery, 0.0001,
                "Yesterday's delivery was lost, so tomorrow's odds differ from the run that saved.");

            Assert.AreEqual(1, restored.Programme.Failures);
            Assert.AreEqual(1_000_000_000L, restored.Programme.PaidOutUsd);
        }

        /// <summary>A v48 file arrives unsigned, because era five did not exist when it was played.</summary>
        [Test]
        public void AnOlderSaveHasNoContract()
        {
            var upgraded = SaveMigration.UpgradeV48ToV49(new SaveData { version = 48 });

            Assert.AreEqual(49, upgraded.version);
            Assert.IsFalse(upgraded.programmeSigned);
            CollectionAssert.IsEmpty(upgraded.programmeSectors);
            Assert.AreEqual(1.0, upgraded.programmeLastDelivery, 0.0001);
        }

        /// <summary>Every sector and every refusal has words in both languages.</summary>
        [Test]
        public void TheProgrammeSpeaksBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var sector in StateSectorCatalog.All)
                    {
                        Assert.IsFalse(sector.DisplayName.StartsWith("sector."),
                            $"{sector.Sector} has no name in {language}.");

                        Assert.IsFalse(sector.Blurb.StartsWith("sector."),
                            $"{sector.Sector} has no description in {language}.");
                    }

                    foreach (var key in new[]
                    {
                        "state.title", "state.strap", "state.record", "state.sign", "state.delivery",
                        "state.risk", "state.held", "state.on_notice", "state.start", "state.stop",
                        "research.era.5"
                    })
                    {
                        Assert.AreNotEqual(key, Loc.T(key), $"{key} has no words in {language}.");
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }
    }
}
