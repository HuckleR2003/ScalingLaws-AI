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
                    13 => UpgradeV13ToV14(current),
                    14 => UpgradeV14ToV15(current),
                    15 => UpgradeV15ToV16(current),
                    16 => UpgradeV16ToV17(current),
                    17 => UpgradeV17ToV18(current),
                    18 => UpgradeV18ToV19(current),
                    19 => UpgradeV19ToV20(current),
                    20 => UpgradeV20ToV21(current),
                    21 => UpgradeV21ToV22(current),
                    22 => UpgradeV22ToV23(current),
                    23 => UpgradeV23ToV24(current),
                    24 => UpgradeV24ToV25(current),
                    25 => UpgradeV25ToV26(current),
                    26 => UpgradeV26ToV27(current),
                    27 => UpgradeV27ToV28(current),
                    28 => UpgradeV28ToV29(current),
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

        /// <summary>
        /// v13 to v14: the standing gains a third axis, the model type.
        ///
        /// A v13 file recorded who held each audience but not what they were selling them, and there
        /// is no way to read that back out. Every recorded share would have to be assigned to some
        /// type, and picking one would be inventing a market history that never happened.
        ///
        /// So the standing is dropped and flagged as unrecorded, and the first tick after loading
        /// sets it to whatever today's products deserve. The same treatment v13 gave a v12 file, for
        /// the same reason: a market with no recorded history has no inertia to respect.
        /// </summary>
        public static SaveData UpgradeV13ToV14(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 14;
            data.segmentShares = new List<double>();
            data.segmentOwnerCount = 0;
            data.segmentTypeCount = 0;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v13 to v14: the market standing now records model type as well as audience and lab. "
                + "An older file cannot say what its users were being sold, so the standing is "
                + "rebuilt from what is on the market the first day after loading.");

            return data;
        }

        /// <summary>
        /// v14 to v15: product lines, and the type a run in flight is building.
        ///
        /// Every existing model becomes a line of its own, which is exactly what it was: before lines
        /// existed each release stood alone in the market and none of them superseded another. Grouping
        /// them by name would look tidier and would be a guess, and it would silently withdraw models
        /// the player still has on sale.
        ///
        /// A run in flight is the awkward one. v14 never wrote the model type into the run at all, so a
        /// file saved during training genuinely does not know what was being built. It comes back as a
        /// general model, which is what the game did with it before this field existed, and the player
        /// keeps the run rather than losing it.
        /// </summary>
        public static SaveData UpgradeV14ToV15(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 15;

            foreach (var model in data.models)
            {
                if (model != null)
                {
                    model.family = string.Empty;
                }
            }

            foreach (var shelved in data.shelf)
            {
                if (shelved != null)
                {
                    shelved.family = string.Empty;
                }
            }

            var rebuiltRun = false;
            if (data.hasActiveRun && data.activeRun != null)
            {
                data.activeRun.family = string.Empty;
                rebuiltRun = data.activeRun.modelType == 0;
            }

            LastMigrationNotes = Append(LastMigrationNotes,
                "v14 to v15: every existing model became a product line of its own, which is what it "
                + "already was, because before lines existed no release superseded another."
                + (rebuiltRun
                    ? " A training run was in flight and v14 never recorded what type it was building, "
                      + "so it resumes as a general model."
                    : string.Empty));

            return data;
        }

        /// <summary>
        /// v15 to v16: the books.
        ///
        /// An older file has no ledger and there is no way to reconstruct one. The daily totals it did
        /// keep are lifetime sums, not a month by month breakdown by reason, and inventing a history
        /// from them would put numbers in front of the player that no day of their game produced. The
        /// report starts empty and fills from the next day played, which is the only honest reading.
        /// </summary>
        public static SaveData UpgradeV15ToV16(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 16;
            data.ledgerMonths = new List<int>();
            data.ledgerAmounts = new List<long>();

            LastMigrationNotes = Append(LastMigrationNotes,
                "v15 to v16: the financial report starts empty. An older save recorded lifetime totals "
                + "but never why the money moved, so its history cannot be rebuilt without inventing "
                + "months that never happened.");

            return data;
        }

        /// <summary>
        /// v16 to v17: the carried forward total.
        ///
        /// A v16 file kept at most sixty months and nothing else, so once a game passed five years
        /// the books could no longer explain the balance. There is no way to recover what the dropped
        /// months contained, so the carried total starts at zero: the report will under-explain an old
        /// save by whatever aged out before it was loaded, and it says so rather than inventing a
        /// figure that would make the arithmetic look right.
        /// </summary>
        public static SaveData UpgradeV16ToV17(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 17;
            data.ledgerCarriedForward = 0L;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v16 to v17: months older than five years are now carried forward as a total. An "
                + "older save cannot say what its dropped months held, so the carried figure starts "
                + "at zero and the report explains everything from here on.");

            return data;
        }

        /// <summary>
        /// v17 to v18: fans, and the date the newest model shipped.
        ///
        /// Fans start at zero rather than being back-filled from reputation. A following is earned
        /// day by day and a v17 file has no record of the days, so handing a loaded company a fan
        /// base it never built would be inventing history. It rebuilds from the next day played.
        ///
        /// The release date is set to the last model's own release date where one exists, which is
        /// recorded and therefore not a guess. A company with no models keeps day zero, which reads
        /// as a very stale line, and that is the honest description of a lab that has never shipped.
        /// </summary>
        public static SaveData UpgradeV17ToV18(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 18;
            data.fans = 0.0;
            data.lastReleaseDayIndex = 0;

            foreach (var model in data.models)
            {
                if (model != null && model.releaseDayIndex > data.lastReleaseDayIndex)
                {
                    data.lastReleaseDayIndex = model.releaseDayIndex;
                }
            }

            LastMigrationNotes = Append(LastMigrationNotes,
                "v17 to v18: the fan base starts at zero, because a following is earned day by day "
                + "and an older save has no record of those days. The freshness of the product line "
                + "is read from the newest model's own release date, which was recorded.");

            return data;
        }

        /// <summary>
        /// v18 to v19: hosting packages, and yesterday's service load.
        ///
        /// A v18 company held no packages, so zero of each is the true reading rather than a guess.
        /// The service load is left empty, which restores as a fleet with nothing queueing: that is
        /// the most flattering assumption in the file and the only defensible one, because an older
        /// save has no record of how loaded the cluster was and inventing a bad day would punish a
        /// player for loading a game.
        /// </summary>
        public static SaveData UpgradeV18ToV19(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 19;
            data.hostingPackages = new List<int>();
            data.qualityDemanded = 0.0;
            data.qualityCapacity = 0.0;
            data.qualityPackagedShare = 0.0;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v18 to v19: no hosting packages, which is what a v18 company had. The service load "
                + "starts empty, so the first day after loading is measured rather than assumed.");

            return data;
        }

        /// <summary>
        /// v19 to v20: ninety days of user history for the charts.
        ///
        /// A v19 file recorded totals and never a day by day trace, so there is nothing to recover.
        /// The history starts empty and the charts fill in as the game is played, which is honest.
        /// Back-filling a flat line at today's count would draw a company that had been steady for
        /// three months when it may have doubled last week.
        /// </summary>
        public static SaveData UpgradeV19ToV20(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 20;
            data.userHistory = new List<double>();

            LastMigrationNotes = Append(LastMigrationNotes,
                "v19 to v20: the user charts start empty. An older save has no day by day record and "
                + "drawing a flat line would invent three months of history that never happened.");

            return data;
        }

        /// <summary>
        /// v20 to v21: research points.
        ///
        /// A v20 company banked none, because there was nothing to bank them in. Starting at zero is
        /// the true reading and it is also the fair one: the points a company would have earned are a
        /// function of days it spent building, and an older save has no record of which days those
        /// were. Funding starts at the smallest fixed budget, which is what a new company chooses.
        /// </summary>
        public static SaveData UpgradeV20ToV21(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 21;
            data.researchPoints = 0.0;
            data.researchFundingMode = 0;
            data.researchMonthlyUsd = 1_000;
            data.researchRevenueShare = 0.0;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v20 to v21: research points start at zero. They are earned by building, and an older "
                + "save has no record of which days were spent building.");

            return data;
        }

        /// <summary>
        /// v21 to v22: awareness and booked campaigns.
        ///
        /// A v21 company ran no campaigns, because there were none to run, so an empty list is the
        /// true reading. Awareness is left empty rather than estimated: the first tick after loading
        /// rebuilds it from standing and from the share the company already holds, both of which are
        /// recorded, so nothing is invented and nothing is lost.
        /// </summary>
        public static SaveData UpgradeV21ToV22(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 22;
            data.awareness = new List<double>();
            data.campaigns = new List<CampaignData>();

            LastMigrationNotes = Append(LastMigrationNotes,
                "v21 to v22: no campaigns, which is what a v21 company had. Awareness rebuilds on the "
                + "first day from standing and from the audience already being served.");

            return data;
        }

        /// <summary>
        /// v22 to v23: independent memberships, the news feed, and what each model earned.
        ///
        /// **The membership is the honest reading of the one tier a v22 company was paying for.** It
        /// held exactly one subscription, so it gets exactly that one back rather than everything up
        /// to it: granting the cheaper desks too would be handing the player two retainers they never
        /// bought and then invoicing them for both.
        ///
        /// The feed starts empty. News is a record of things that were announced, and a v22 file has
        /// no record of which days had announcements, so inventing a back catalogue would be printing
        /// stories about events that may never have happened.
        ///
        /// Lifetime earnings start at zero for every model already on sale, and that **understates**
        /// them, deliberately. The alternative is to divide the company's lifetime revenue among
        /// models by a rule nobody can check, which would put a confident wrong number on a history
        /// page whose entire job is to be trustworthy. A model that shipped before v23 reads zero
        /// until it earns, and the page can say why.
        /// </summary>
        public static SaveData UpgradeV22ToV23(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 23;
            data.memberships = new List<int>();
            data.news = new List<NewsItemData>();
            data.newsUnread = 0;
            data.signalCountdowns = new List<int>();
            data.daysUntilNextDossier = 0;
            data.nextDossierLab = 0;

            if (Enum.IsDefined(typeof(IntelTier), data.intelSubscription)
                && (IntelTier)data.intelSubscription != IntelTier.PublicNews)
            {
                data.memberships.Add(data.intelSubscription);
            }

            foreach (var model in data.models)
            {
                if (model == null)
                {
                    continue;
                }

                model.lifetimeRevenueUsd = 0L;
                model.daysOnSale = 0;
                model.peakUsers = 0.0;
                model.retiredDayIndex = GameDate.MinimumDayIndex;
            }

            LastMigrationNotes = Append(LastMigrationNotes,
                "v22 to v23: the one desk on retainer became the one membership held, not the ladder "
                + "below it. The news feed starts empty, because a v22 file has no record of what was "
                + "announced. Per model earnings start at zero and therefore understate every model "
                + "already on sale, which is preferred to splitting lifetime revenue by a rule nobody "
                + "can check.");

            return data;
        }

        /// <summary>
        /// v23 to v24: the inbox, and corporation tax billed once a year rather than taken daily.
        ///
        /// **A v23 company had already paid its tax**, every day, as it went. So the accrual starts
        /// at zero and the first demand covers only the days after loading. Carrying a balance
        /// forward would bill the player a second time for a year they have already settled, and
        /// guessing one from lifetime tax paid would be inventing which year it belonged to.
        ///
        /// The inbox starts empty for the same reason the news feed did: a v23 file has no record of
        /// what was asked of the company, and a back catalogue of demands would be asking for money
        /// against events that may never have happened.
        /// </summary>
        public static SaveData UpgradeV23ToV24(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 24;
            data.mail = new List<MailItemData>();
            data.accruedTaxUsd = 0L;
            data.taxYear = 0;
            data.daysUntilNextApplicant = 40;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v23 to v24: empty inbox, and the tax accrual starts at zero because a v23 company "
                + "paid its tax daily as it went. The first annual demand therefore covers only the "
                + "days after loading, which understates that one year and never double bills it.");

            return data;
        }

        /// <summary>
        /// v24 to v25: precision, shape, deduplication and the corpus cutoff.
        ///
        /// **Every one of them defaults to the neutral option and that is the true reading**, not a
        /// convenience. A v24 company trained in BF16 at balanced proportions on a standard clean of
        /// everything available, because those were the only behaviours the game had. The middle of
        /// each catalog is exactly 1.0 on every axis, so a restored campaign computes the same
        /// numbers it computed before these existed.
        /// </summary>
        /// <summary>
        /// v25 to v26: the founder gets a face.
        ///
        /// **Left empty on purpose, and that is the honest reconstruction.** A v25 campaign was
        /// started before the portrait existed, so nobody chose anything. Empty means "whichever
        /// look comes first", which is what the game already does for a founder with no preference,
        /// rather than inventing a choice the player never made and then claiming they made it.
        /// </summary>
        /// <summary>
        /// v26 to v27: models remember what they were hardened with.
        ///
        /// **Every existing model gets tier zero of the two free modules and no data protection**,
        /// which is the least flattering reading that is still defensible. A v26 company was playing
        /// a game with no safety stage in it, so it never chose any of this; giving it the tiers it
        /// could afford today would hand it protection it never paid for, and giving it nothing at
        /// all would leave it worse off than a company starting fresh.
        /// </summary>
        /// <summary>
        /// v27 to v28: regulators take five days now.
        ///
        /// Nothing to reconstruct. A v27 campaign had no such thing as an open inspection, because
        /// penalties landed the same day they were decided, so there is no file to reopen and
        /// inventing one would fine somebody for a run that already finished.
        /// </summary>
        /// <summary>
        /// v28 to v29: offices can be bought.
        ///
        /// Nothing owned. A v28 company could only ever rent, so it paid rent every month it was
        /// open and crediting it with a building now would hand it a purchase it never made and
        /// wipe a bill it has already been paying.
        /// </summary>
        public static SaveData UpgradeV28ToV29(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 29;
            data.ownedOffices = new List<int>();

            LastMigrationNotes = Append(LastMigrationNotes,
                "v28 to v29: offices could only be rented before this version, so the company owns "
                + "nothing and keeps paying the rent it has been paying all along.");

            return data;
        }

        public static SaveData UpgradeV27ToV28(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 28;
            data.actionOpen = false;

            LastMigrationNotes = Append(LastMigrationNotes,
                "v27 to v28: penalties used to land the day they were decided, so there was no open "
                + "inspection to carry over and none was invented.");

            return data;
        }

        public static SaveData UpgradeV26ToV27(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 27;

            // Two different record types, same four fields, so this is written twice rather than
            // through a shared interface the save format does not have.
            foreach (var model in data.models)
            {
                if (model == null)
                {
                    continue;
                }

                model.assaTier = 0;
                model.redTeamTier = 0;
                model.dataProtectionTier = -1;
                model.safetyEffort = 1;
            }

            foreach (var shelved in data.shelf)
            {
                if (shelved == null)
                {
                    continue;
                }

                shelved.assaTier = 0;
                shelved.redTeamTier = 0;
                shelved.dataProtectionTier = -1;
                shelved.safetyEffort = 1;
            }

            if (data.activeRun?.choices != null)
            {
                data.activeRun.choices.assaTier = 0;
                data.activeRun.choices.redTeamTier = 0;
                data.activeRun.choices.dataProtectionTier = -1;
                data.activeRun.choices.safetyEffort = 1;
            }

            LastMigrationNotes = Append(LastMigrationNotes,
                "v26 to v27: no run recorded a safety stage before this version, so every model "
                + "keeps the two modules a company starts with and none of the data protection it "
                + "would have had to buy.");

            return data;
        }

        public static SaveData UpgradeV25ToV26(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 26;
            data.founderLook = string.Empty;
            data.founderGlasses = 0;

            // Appended, never assigned. The notes are the whole chain's record and a test walks them
            // looking for every step: overwriting here erased everything v7 onward had said.
            LastMigrationNotes = Append(LastMigrationNotes,
                "v25 to v26: no face was recorded before this version, so the founder keeps the "
                + "default look rather than being assigned one at random.");

            return data;
        }

        public static SaveData UpgradeV24ToV25(SaveData data)
        {
            if (data == null)
            {
                return null;
            }

            data.version = 25;

            foreach (var model in data.models)
            {
                if (model != null)
                {
                    model.shape = (int)ModelShape.Balanced;
                }
            }

            foreach (var shelved in data.shelf)
            {
                if (shelved != null)
                {
                    shelved.shape = (int)ModelShape.Balanced;
                }
            }

            data.activeRun ??= new TrainingRunData();
            data.activeRun.choices = new ChoiceData
            {
                precision = (int)TrainingPrecision.BFloat16,
                shape = (int)ModelShape.Balanced,
                deduplication = (int)DeduplicationPass.Standard,
                cutoffMonthsBack = 0
            };

            LastMigrationNotes = Append(LastMigrationNotes,
                "v24 to v25: every model and every run takes the neutral option on all four new "
                + "choices, which is what a v24 company actually did, since those were the only "
                + "behaviours available. Nothing about a restored campaign changes.");

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
