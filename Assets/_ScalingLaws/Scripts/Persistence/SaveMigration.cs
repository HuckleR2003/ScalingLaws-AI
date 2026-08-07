using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Persistence
{
    /// <summary>
    /// The v1 file. Compute was two counters and nothing else: no purchase dates, no tiers, no
    /// prices. It is kept here verbatim so the upgrade path has a real thing to read rather than a
    /// guess about what used to be written.
    /// </summary>
    [Serializable]
    public sealed class SaveDataV1
    {
        public int version = 1;
        public string companyName = "Newco";
        public int dayIndex;
        public long cashUsd;
        public uint randomState;
        public double reputation;
        public int ownedDataSources;
        public long lifetimeRevenueUsd;
        public long lifetimeOperatingCostUsd;
        public bool isBankrupt;

        /// <summary>v1 tracked rented and owned accelerators as bare counts.</summary>
        public int rentedAccelerators;

        public int ownedAccelerators;

        public List<DeployedModelData> models = new();
    }

    /// <summary>
    /// The ONE upgrade path for saves. Each step moves a file forward exactly one version, and the
    /// runner applies them in order, so adding v3 later means writing one more method and nothing else.
    ///
    /// The v1 to v2 step has real work in it. v1 stored owned compute as a single integer, which
    /// carries no purchase date, so the depreciation curve that v2 is built on has nothing to read.
    /// The upgrade reconstructs a plausible batch and says so in <see cref="LastMigrationNotes"/>
    /// rather than pretending the information was there: the accelerator is whatever the catalog
    /// says was current on the save date, and the purchase date is set half a value half-life back,
    /// which is the least flattering assumption that is still defensible.
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>Plain description of what the last upgrade had to invent. Empty when nothing was migrated.</summary>
        public static string LastMigrationNotes { get; private set; } = string.Empty;

        /// <summary>Version the last call read from the file, before any upgrade.</summary>
        public static int LastDetectedVersion { get; private set; }

        /// <summary>
        /// Reads a save of any supported version and returns it at <see cref="SaveData.CurrentVersion"/>.
        /// Returns null when the payload cannot be understood at all.
        /// </summary>
        public static SaveData Upgrade(string json, Func<string, Type, object> deserializer)
        {
            LastMigrationNotes = string.Empty;
            LastDetectedVersion = 0;

            if (string.IsNullOrWhiteSpace(json) || deserializer == null)
            {
                return null;
            }

            var envelope = deserializer(json, typeof(SaveEnvelope)) as SaveEnvelope;
            var version = envelope?.version ?? 0;
            LastDetectedVersion = version;

            switch (version)
            {
                case 1:
                    var legacy = deserializer(json, typeof(SaveDataV1)) as SaveDataV1;
                    return legacy == null ? null : RunChainFrom(UpgradeV1ToV2(legacy), 2);

                case >= 2 and < SaveData.CurrentVersion:
                    var older = deserializer(json, typeof(SaveData)) as SaveData;
                    return older == null ? null : RunChainFrom(older, version);

                case SaveData.CurrentVersion:
                    return deserializer(json, typeof(SaveData)) as SaveData;

                default:
                    // A file from the future, or one with no version at all. Neither is safe to guess at.
                    return null;
            }
        }

        /// <summary>
        /// Walks a save forward one version at a time until it is current.
        ///
        /// Written as a loop rather than as nested calls because the nesting had already gone wrong
        /// once: a hand-chained expression quietly stopped short of the newest version and left the
        /// later fields uninitialised. A loop cannot skip a step.
        /// </summary>
        private static SaveData RunChainFrom(SaveData data, int fromVersion)
        {
            var current = data;
            for (var version = fromVersion; current != null && version < SaveData.CurrentVersion; version++)
            {
                current = version switch
                {
                    2 => UpgradeV2ToV3(current),
                    3 => UpgradeV3ToV4(current),
                    4 => UpgradeV4ToV5(current),
                    5 => UpgradeV5ToV6(current),
                    6 => UpgradeV6ToV7(current),
                    7 => UpgradeV7ToV8(current),
                    8 => UpgradeV8ToV9(current),
                    9 => UpgradeV9ToV10(current),
                    10 => UpgradeV10ToV11(current),
                    11 => UpgradeV11ToV12(current),
                    12 => UpgradeV12ToV13(current),
                    _ => current
                };
            }

            return current;
        }

        /// <summary>
        /// v2 to v3: three things that did not exist get built rather than defaulted.
        ///
        /// Trait levels are set to market par on each model's own release date, which is exactly
        /// where a v2 model implicitly sat: v2 had no upgrade grid, so every model was neither ahead
        /// nor behind. Setting them to zero instead would load a saved campaign with every model
        /// several levels behind the market through no decision the player made.
        ///
        /// The rival field is replayed from day zero to the save date with no player pressure. A v2
        /// save has no record of which labs were waiting on hardware, so the field is reconstructed
        /// from the reference timeline. Any deviation the original campaign caused is lost, and
        /// there is no way to recover it from what v2 wrote down.
        /// </summary>
        public static SaveData UpgradeV2ToV3(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 3;
            data.shelf ??= new List<TrainedModelData>();
            data.upgrades ??= new List<UpgradeProjectData>();
            data.fundingRounds ??= new List<FundingRoundData>();
            data.rivals ??= new List<CompetitorAgentData>();
            data.revenueWindow ??= new List<long>();
            data.founderEquity = 1.0;
            data.intelSubscription = (int)IntelTier.PublicNews;
            data.hasFundingOffer = false;

            var restoredModels = 0;
            if (data.models != null)
            {
                foreach (var model in data.models)
                {
                    if (model == null)
                    {
                        continue;
                    }

                    if (model.traitLevels != null && model.traitLevels.Count > 0)
                    {
                        continue;
                    }

                    var par = ModelTraitSet.AtMarketPar(new GameDate(model.releaseDayIndex));
                    model.traitLevels = new List<int>(par.ToArray());
                    restoredModels++;
                }
            }

            var saveDate = new GameDate(data.dayIndex);
            var replayed = ReplayRivalField(saveDate);
            foreach (var agent in replayed.Agents)
            {
                data.rivals.Add(new CompetitorAgentData
                {
                    competitor = (int)agent.Competitor,
                    hasShipped = agent.HasShipped,
                    liveModelName = agent.LiveModelName,
                    liveCapability = agent.LiveCapability,
                    liveBrand = agent.LiveBrand,
                    livePrice = agent.LivePrice,
                    liveReleaseDayIndex = agent.LiveReleaseDate.DayIndex,
                    nextReleaseDayIndex = agent.NextReleaseDate.DayIndex,
                    hasPlannedRelease = agent.HasPlannedRelease,
                    accumulatedDelayDays = agent.AccumulatedDelayDays,
                    isWaitingForHardware = agent.IsWaitingForHardware
                });
            }

            LastMigrationNotes =
                $"v2 to v3: set trait levels to market par on {restoredModels} model(s), and rebuilt " +
                $"{data.rivals.Count} rival labs by replaying the reference timeline to {saveDate}. " +
                "v2 recorded no rival state, so any deviation the original campaign caused is gone.";

            return data;
        }

        /// <summary>
        /// v3 to v4: in-house architecture families did not exist, so there is nothing to convert
        /// and nothing to invent. The lists are created empty and the campaign continues with the
        /// six slots free. This is the honest shape of a migration that genuinely has no work to do,
        /// and it is written out rather than skipped so the chain stays uniform.
        /// </summary>
        public static SaveData UpgradeV3ToV4(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 4;
            data.customArchitectures ??= new List<CustomArchitectureData>();
            data.architectureProject ??= new ArchitectureProjectData();
            data.hasArchitectureProject = false;

            LastMigrationNotes = string.IsNullOrEmpty(LastMigrationNotes)
                ? "v3 to v4: no in-house families existed before v4, so all six slots start free."
                : LastMigrationNotes + " v3 to v4: all six family slots start free.";

            return data;
        }

        /// <summary>
        /// v4 to v5: rental was a unit count, and is now contracted throughput.
        ///
        /// The conversion is exact rather than assumed, which is unusual for a migration here: the
        /// save records the date, the date determines which generation the clouds were renting, and
        /// that generation determines how many petaflops those units were. A campaign loaded through
        /// this step keeps precisely the capacity it had.
        /// </summary>
        public static SaveData UpgradeV4ToV5(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 5;

            if (data.rentedPetaflops <= 0.0 && data.rentedAccelerators > 0)
            {
                var saveDate = new GameDate(data.dayIndex);
                var rentable = MarketModel.RentableGenerationOn(saveDate);
                var perUnit = HardwareCatalog.TryGet(rentable, out var generation)
                    ? generation.PetaflopsPerUnit
                    : 0.0;

                data.rentedPetaflops = data.rentedAccelerators * perUnit;

                LastMigrationNotes = Append(LastMigrationNotes,
                    $"v4 to v5: converted {data.rentedAccelerators:N0} rented units to "
                    + $"{data.rentedPetaflops:N0} PF using {rentable}, the generation the clouds were offering on {saveDate}.");
            }
            else
            {
                LastMigrationNotes = Append(LastMigrationNotes, "v4 to v5: nothing was rented, so nothing to convert.");
            }

            return data;
        }

        /// <summary>
        /// v5 to v6: founders, company archetypes and the technology tree arrive.
        ///
        /// The tree is the awkward one. Before v6 a company bought architectures and corpora with
        /// cash alone, so a save can legitimately hold things the tree now gates. Locking them
        /// retroactively would break a campaign that did nothing wrong, so the upgrade grants every
        /// node whose unlocks the company already owns, and says how many. The founder is neutral,
        /// because there was nobody to be.
        /// </summary>
        public static SaveData UpgradeV5ToV6(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 6;
            data.founderTraits ??= new List<int>();
            data.unlockedResearch ??= new List<int>();
            data.archetype = (int)CompanyArchetype.Custom;
            data.defaultPriceMultiplier = 1.0;
            data.hasResearchProject = false;

            data.unlockedResearch.Add((int)ResearchTree.StartingNode);

            var owned = (DatasetSource)data.ownedDataSources;
            var granted = 0;
            foreach (var node in ResearchTree.All)
            {
                if (node.Id == ResearchTree.StartingNode)
                {
                    continue;
                }

                var earnedByData = node.UnlocksData != DatasetSource.None
                    && (owned & node.UnlocksData) == node.UnlocksData;

                var earnedByArchitecture = node.UnlocksArchitecture != ArchitectureId.None
                    && data.adoptedArchitectures != null
                    && data.adoptedArchitectures.Contains((int)node.UnlocksArchitecture);

                var earnedByTier = node.UnlocksTier == ComputeTier.ColocatedServers
                    && data.assets != null
                    && data.assets.Count > 0;

                if (earnedByData || earnedByArchitecture || earnedByTier)
                {
                    data.unlockedResearch.Add((int)node.Id);
                    granted++;
                }
            }

            LastMigrationNotes = Append(LastMigrationNotes,
                $"v5 to v6: founder set to neutral and {granted} research node(s) granted for things the "
                + "company already owned, because the tree did not exist when they were bought.");

            return data;
        }

        /// <summary>
        /// v6 to v7: debt arrives. A campaign saved before v7 had no way to borrow, so there is
        /// nothing to convert and nothing to invent. Written out rather than skipped so the chain
        /// stays uniform and the next step has one obvious place to hook into.
        /// </summary>
        public static SaveData UpgradeV6ToV7(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 7;
            data.loans ??= new List<LoanData>();

            LastMigrationNotes = Append(LastMigrationNotes,
                "v6 to v7: no facilities existed before v7, so the company starts debt free.");

            return data;
        }

        /// <summary>
        /// v7 to v8: staff, offices and safety incidents arrive. A campaign saved before v8 had no
        /// payroll, so it starts in the garage with nobody in it and a clean record. Nothing to
        /// convert; written out so the chain has every step present.
        /// </summary>
        public static SaveData UpgradeV7ToV8(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 8;
            data.staff ??= new List<HireData>();
            data.incidents ??= new List<IncidentData>();
            data.officeTier = (int)OfficeTier.Garage;
            data.lifetimeFinesUsd = 0;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v7 to v8: no payroll existed before v8, so the company starts in the garage with a clean record.");

            return data;
        }
        /// <summary>
        /// v8 to v9: pricing, the free tier and marketing arrive. A campaign saved before v9 was
        /// implicitly charging per token at the market rate with no free tier and no advertising, so
        /// those are exactly the defaults written in. Nothing is invented.
        /// </summary>
        public static SaveData UpgradeV8ToV9(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 9;
            data.pricingModel = (int)PricingModel.PayPerToken;
            data.paidPriceMultiplier = 1.0;
            data.subscriptionPriceUsdPerMonth = 20.0;
            data.freeTierTokensPerUserPerDay = 0.0;
            data.companyMarketingDailyUsd = 0;
            data.modelMarketingDailyUsd = 0;
            data.modelAwareness = 0.0;
            data.lifetimeFreeTokensBillions = 0.0;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v8 to v9: pricing set to pay per token at the market rate, no free tier, no marketing.");

            return data;
        }
        /// <summary>
        /// v9 to v10: the founder gains a name and seven skills. A campaign saved before v10 was run
        /// by nobody in particular, so the name is left blank and every skill starts at the baseline
        /// where all effects read zero. That leaves an older save exactly as balanced as it was, and
        /// the 200 creation points are simply never spent.
        /// </summary>
        public static SaveData UpgradeV9ToV10(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 10;
            data.founderName = string.IsNullOrWhiteSpace(data.companyName) ? "Anonymous" : "Founder";
            data.skillLevels ??= new List<int>();
            data.skillExperience ??= new List<long>();

            LastMigrationNotes = Append(LastMigrationNotes,
                "v9 to v10: skills start at the baseline, so an older save keeps the balance it had.");

            return data;
        }

        /// <summary>
        /// v10 to v11: the company gains a home country.
        ///
        /// There is nothing in a v10 file to infer one from, so it is registered in the United
        /// States. That is the least flattering defensible choice rather than a kind one: it is the
        /// most crowded market in the game and its tax rate is mid-table. Picking a tax haven here
        /// would hand an old save money it never earned.
        /// </summary>
        public static SaveData UpgradeV10ToV11(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 11;
            data.worldRegion = (int)WorldRegion.America;
            data.homeCountry = (int)Country.UnitedStates;
            data.lifetimeTaxPaidUsd = 0L;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v10 to v11: no home country was recorded, so the company is registered in the "
                + "United States. Tax paid before the upgrade is reported as zero because it was "
                + "never charged.");

            return data;
        }



        /// <summary>
        /// v11 to v12: models gain a type.
        ///
        /// Everything written before this was built without types and competed for one
        /// undifferentiated crowd, which is exactly what a general model does. So every existing
        /// model becomes general. That is not a kindness: general is the type that ages worst as the
        /// market specialises, and it is the only reading of an old file that is actually true.
        /// </summary>
        public static SaveData UpgradeV11ToV12(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 12;
            data.activeRunType = (int)ModelType.General;

            if (data.models != null)
            {
                foreach (var model in data.models)
                {
                    if (model != null)
                    {
                        model.modelType = (int)ModelType.General;
                    }
                }
            }

            if (data.shelf != null)
            {
                foreach (var model in data.shelf)
                {
                    if (model != null)
                    {
                        model.modelType = (int)ModelType.General;
                    }
                }
            }

            LastMigrationNotes = Append(LastMigrationNotes,
                "v11 to v12: nothing in an older save recorded what a model was for, so every one of "
                + "them is general. That is what they were competing as.");

            return data;
        }

        /// <summary>
        /// v12 to v13: the market gains a memory.
        ///
        /// Before this, share was recomputed from scratch every day, so there is nothing in an older
        /// file to read a user base out of. Rather than invent one, the standing is left empty and
        /// flagged as unrecorded, and the first tick after loading snaps it to whatever today's
        /// products deserve.
        ///
        /// That is the honest translation. The old file's world really did say "share is whatever
        /// the models are worth right now", so jumping to exactly that is not a guess, it is the
        /// same statement in the new format.
        /// </summary>
        public static SaveData UpgradeV12ToV13(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 13;
            data.segmentPlayerShares = new List<double>();
            data.segmentRivalShares = new List<double>();
            data.segmentRivalCount = 0;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v12 to v13: no user base was recorded because share used to be recomputed daily. "
                + "The first day after loading sets the standing to what the live models are worth, "
                + "which is what the older file meant.");

            return data;
        }

        private static string Append(string existing, string addition) =>
            string.IsNullOrEmpty(existing) ? addition : existing + " " + addition;

        /// <summary>
        /// Runs the rival field forward from day zero with no player in it. Deterministic: the same
        /// save date always reconstructs the same field.
        /// </summary>
        private static CompetitorField ReplayRivalField(GameDate upTo)
        {
            var field = CompetitorField.CreateFromCatalog();
            var random = new DeterministicRandom(0x5CA1AB1E);

            for (var day = 0; day <= upTo.DayIndex; day++)
            {
                field.Tick(new GameDate(day), 0.0, random);
            }

            return field;
        }

        /// <summary>
        /// v1 to v2: turn the bare owned-accelerator count into a dated batch so depreciation has
        /// something to work from.
        /// </summary>
        public static SaveData UpgradeV1ToV2(SaveDataV1 legacy)
        {
            if (legacy == null)
            {
                return null;
            }

            var upgraded = new SaveData
            {
                // Deliberately 2, not current. Each step moves the file forward exactly one version
                // and the runner chains them, so this method never has to know what v3 looks like.
                version = 2,
                hardwareCatalogVersion = HardwareCatalog.CatalogVersion,
                companyName = legacy.companyName,
                dayIndex = legacy.dayIndex,
                cashUsd = legacy.cashUsd,
                randomState = legacy.randomState,
                reputation = legacy.reputation,
                trainingComputeShare = 0.7,
                ownedDataSources = legacy.ownedDataSources,
                lifetimeRevenueUsd = legacy.lifetimeRevenueUsd,
                lifetimeOperatingCostUsd = legacy.lifetimeOperatingCostUsd,
                lifetimeCapitalSpentUsd = 0,
                isBankrupt = legacy.isBankrupt,
                rentedAccelerators = Math.Max(0, legacy.rentedAccelerators),
                models = legacy.models ?? new List<DeployedModelData>(),
                adoptedArchitectures = new List<int> { (int)ArchitectureId.DenseTransformer }
            };

            var owned = Math.Max(0, legacy.ownedAccelerators);
            if (owned == 0)
            {
                LastMigrationNotes = "v1 to v2: no owned accelerators to reconstruct.";
                return upgraded;
            }

            var saveDate = new GameDate(legacy.dayIndex);
            if (!HardwareCatalog.TryGetFrontier(saveDate, HardwareClass.Accelerator, out var generation))
            {
                generation = HardwareCatalog.Get(HardwareGenerationId.AcceleratorA100);
            }

            // v1 never recorded when the hardware was bought. Assume half a value half-life of age:
            // old enough to have lost real value, not so old that the save is punished for a gap in
            // the format rather than a decision the player made.
            var assumedAgeDays = generation.ValueHalfLifeDays / 2;
            var purchaseDate = saveDate.AddDays(-assumedAgeDays);

            upgraded.assets.Add(new HardwareAssetData
            {
                generationId = (int)generation.Id,
                tier = (int)ComputeTier.ColocatedServers,
                units = owned,
                purchaseDayIndex = purchaseDate.DayIndex,
                pricePerUnitUsd = generation.LaunchPriceUsd,
                leadTimeDays = 0
            });

            upgraded.lifetimeCapitalSpentUsd = generation.LaunchPriceUsd * owned;

            LastMigrationNotes =
                $"v1 to v2: reconstructed {owned:N0} owned accelerators as {generation.DisplayName} " +
                $"bought {purchaseDate} at list price. v1 stored no purchase date, so the age is an assumption, not a record.";

            return upgraded;
        }
    }
}
