using System.Collections.Generic;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Reads a campaign and says which achievements it satisfies.
    ///
    /// **A pure function over state, and nothing else.** It never writes to the campaign, never
    /// touches a save, never asks what time it is, and holds no state of its own, so calling it
    /// twice on the same day gives the same answer and calling it on a loaded save gives the same
    /// answer as calling it on the day that save was written. That is also why it lives in
    /// <c>Simulation/</c> and imports no UnityEngine: the whole thing runs in an EditMode test
    /// without opening a scene.
    ///
    /// It does not decide what is new. Remembering what has already been earned belongs to
    /// <c>Persistence/AchievementStore</c>, because that is the part that outlives a campaign.
    ///
    /// **Achievements never affect the simulation.** Nothing in here is read back by a rule, so a
    /// change to this file can move what a player is told and can never move what a player is
    /// charged.
    /// </summary>
    public static class AchievementEvaluator
    {
        /// <summary>
        /// Every achievement this campaign currently satisfies, earned or not.
        ///
        /// <paramref name="lifetimeBankruptcies"/> comes from the store rather than from the state
        /// because it counts across campaigns, and a campaign that has gone under does not know
        /// about the ones before it.
        /// </summary>
        public static List<AchievementDefinition> Satisfied(CompanyState state, int lifetimeBankruptcies)
        {
            var earned = new List<AchievementDefinition>();

            if (state == null)
            {
                return earned;
            }

            foreach (var definition in AchievementCatalog.All)
            {
                // Three achievements describe something nothing counts yet. They carry their copy
                // and their Steam id so neither has to be invented later, and they are never given
                // out, because an achievement that unlocks on a number nobody measures is worse
                // than one that does not exist.
                if (definition.NeedsCounter)
                {
                    continue;
                }

                if (Read(state, definition.Metric, lifetimeBankruptcies) >= definition.Threshold)
                {
                    earned.Add(definition);
                }
            }

            return earned;
        }

        /// <summary>
        /// The current value of one metric. Booleans read as 1 or 0.
        ///
        /// Public so a test can assert a single number without going through the whole table.
        /// </summary>
        public static double Read(CompanyState state, AchievementMetric metric, int lifetimeBankruptcies)
        {
            if (state == null)
            {
                return 0.0;
            }

            return metric switch
            {
                AchievementMetric.CashUsd => state.CashUsd,
                AchievementMetric.ReleasedModels => state.ReleasedModelCount,
                AchievementMetric.LiveModels => CountLive(state),
                AchievementMetric.ModelsOnOwnFamily => CountOnOwnFamily(state),
                AchievementMetric.CodingModels => CountOfType(state, ModelType.Coding),
                AchievementMetric.ConversationalModels => CountOfType(state, ModelType.Conversational),
                AchievementMetric.AgenticModels => CountOfType(state, ModelType.Agentic),
                AchievementMetric.AutomationModels => CountOfType(state, ModelType.Automation),
                AchievementMetric.DistinctModelTypes => CountDistinctTypes(state),

                AchievementMetric.OwnFamilies => state.CustomArchitectures.Count,
                AchievementMetric.BestFamilyGeneration => BestGeneration(state),

                AchievementMetric.ServerRoom => state.HasServerRoom ? 1.0 : 0.0,
                AchievementMetric.DatacenterOnline => state.IsDatacenterOnline ? 1.0 : 0.0,
                AchievementMetric.CapitalSpentUsd => state.LifetimeCapitalSpentUsd,

                AchievementMetric.ResearchNodes => CountResearch(state, false),
                AchievementMetric.SuperintelligenceNodes => CountResearch(state, true),
                AchievementMetric.AsiNode =>
                    state.HasResearch(ResearchNodeId.ArtificialSuperintelligence) ? 1.0 : 0.0,

                AchievementMetric.FinesPaidUsd => state.LifetimeFinesUsd,
                AchievementMetric.CleanFourYears => CleanRecord(state) ? 1.0 : 0.0,
                AchievementMetric.FullSafetyRelease => FullySafeRelease(state) ? 1.0 : 0.0,

                AchievementMetric.DaysInDebt => state.DaysInDebt,
                AchievementMetric.DaysInDebtAtLoad => BusyAndBroke(state),
                AchievementMetric.BankruptciesLifetime => lifetimeBankruptcies,
                AchievementMetric.FanBeatsACard => FanEarnsItsSlot(state) ? 1.0 : 0.0,

                AchievementMetric.Fans => state.Fans,
                AchievementMetric.Headcount => state.Staff.Headcount,
                AchievementMetric.FreeTokensBillions => state.LifetimeFreeTokensBillions,
                AchievementMetric.LabsAcquired => state.AcquiredLabs.Count,
                AchievementMetric.BestCapability => state.BestCapability,

                AchievementMetric.CampaignStarted => 1.0,
                AchievementMetric.TaxPaidUsd => state.LifetimeTaxPaidUsd,
                AchievementMetric.YearReached => state.Date.Year,
                AchievementMetric.CompanySold => state.AcquiredForUsd > 0L ? 1.0 : 0.0,

                // NotWiredYet, and anything a later table adds before this switch learns about it.
                _ => 0.0
            };
        }

        /// <summary>What counts as the fleet being at work rather than idling.</summary>
        private const double LoadThatCounts = 0.70;

        /// <summary>
        /// Days under water, but only while the fleet is genuinely busy.
        ///
        /// Reads yesterday's load rather than today's, because that is the figure the whole service
        /// model is built on and the only one that exists before the market has been served.
        /// </summary>
        private static double BusyAndBroke(CompanyState state) =>
            state.LastQuality.Utilisation >= LoadThatCounts ? state.DaysInDebt : 0.0;

        /// <summary>
        /// Somewhere on the floor, a fan is worth more than the card it displaced.
        ///
        /// **The note called this the best of the forty seven and it is right**: no other tycoon has
        /// an achievement about cooling. It is also readable rather than a moment, so it needs no
        /// hook in the rules at all.
        ///
        /// The comparison is the honest one and it is the same arithmetic the room draws with: take
        /// a cabinet that has at least one fan, and ask what it would deliver if that fan's slot
        /// held a card instead. The fan has earned its place when the answer is less.
        ///
        /// Per-accelerator heat and throughput are averaged over what the company owns and has
        /// online, which is the figure `ComputePool` houses the room with. A company with no online
        /// silicon reads false rather than comparing zero against zero.
        /// </summary>
        private static bool FanEarnsItsSlot(CompanyState state)
        {
            if (!state.HasServerRoom)
            {
                return false;
            }

            var units = 0;
            var petaflops = 0.0;
            var kilowatts = 0.0;

            foreach (var asset in state.Pool.Assets)
            {
                if (asset.Units <= 0
                    || !asset.IsOnline(state.Date)
                    || !HardwareCatalog.TryGet(asset.GenerationId, out var generation)
                    || generation.Class != HardwareClass.Accelerator)
                {
                    continue;
                }

                units += asset.Units;
                petaflops += generation.PetaflopsPerUnit * asset.Units;
                kilowatts += generation.PowerKilowatts * asset.Units;
            }

            if (units <= 0)
            {
                return false;
            }

            var perUnitPetaflops = petaflops / units;
            var perUnitKilowatts = kilowatts / units;

            foreach (var square in state.Hall.Occupied())
            {
                if (square.Fans <= 0 || square.Accelerators <= 0)
                {
                    continue;
                }

                var definition = ServerRackCatalog.Get(square.Rack);

                var withFan = square.Accelerators * perUnitPetaflops * ServerRackCatalog.ThrottleFactor(
                    square.Accelerators * perUnitKilowatts,
                    definition.CoolingCapacityKilowatts
                    + square.Fans * ServerRackCatalog.FanCoolingKilowatts);

                // One fan out, one card in. Everything else about the cabinet is unchanged.
                var packed = square.Accelerators + ServerRackCatalog.FanSlots;

                var withCard = packed * perUnitPetaflops * ServerRackCatalog.ThrottleFactor(
                    packed * perUnitKilowatts,
                    definition.CoolingCapacityKilowatts
                    + (square.Fans - 1) * ServerRackCatalog.FanCoolingKilowatts);

                if (withFan > withCard)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountLive(CompanyState state)
        {
            var live = 0;
            foreach (var model in state.DeployedModels)
            {
                if (!model.IsRetired)
                {
                    live++;
                }
            }

            return live;
        }

        private static int CountOfType(CompanyState state, ModelType type)
        {
            var count = 0;
            foreach (var model in state.DeployedModels)
            {
                if (model.Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDistinctTypes(CompanyState state)
        {
            var seen = new HashSet<ModelType>();
            foreach (var model in state.DeployedModels)
            {
                seen.Add(model.Type);
            }

            return seen.Count;
        }

        /// <summary>
        /// Models built on a family the player designed, rather than one bought off the shelf.
        ///
        /// Matched on <c>Architecture</c> rather than on the family's name, because the name is a
        /// string the player types and two families can share one.
        /// </summary>
        private static int CountOnOwnFamily(CompanyState state)
        {
            var count = 0;
            foreach (var model in state.DeployedModels)
            {
                if (state.CustomArchitectures.ContainsKey(model.Architecture))
                {
                    count++;
                }
            }

            return count;
        }

        private static int BestGeneration(CompanyState state)
        {
            var best = 0;
            foreach (var pair in state.CustomArchitectures)
            {
                var generation = state.FamilyGeneration(pair.Key);
                if (generation > best)
                {
                    best = generation;
                }
            }

            return best;
        }

        /// <summary>
        /// Nodes the company actually finished.
        ///
        /// **`ResearchTree.StartingNode` does not count and that is not a detail.** Every campaign
        /// opens with fine-tuning already unlocked, so counting it handed out "finish your first
        /// research node" on day one, to a player who had researched nothing. Caught by
        /// `AchievementTests.AFreshCompanyHasEarnedNothingButStarting`, which exists for exactly
        /// this: a threshold that a brand new company already satisfies.
        /// </summary>
        private static int CountResearch(CompanyState state, bool lastEraOnly)
        {
            var count = 0;
            foreach (var node in ResearchTree.All)
            {
                if (node.Id == ResearchTree.StartingNode)
                {
                    continue;
                }

                if (lastEraOnly && node.Era != ResearchEra.Superintelligence)
                {
                    continue;
                }

                if (state.HasResearch(node.Id))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Four calendar years and not one penalty.
        ///
        /// The campaign opens on 1 January 2022, so reaching 2026 with nothing paid is four clear
        /// years. Read off the fine total rather than off a counter of quiet days, because a total
        /// that is still zero cannot be wrong about its own history.
        /// </summary>
        private static bool CleanRecord(CompanyState state) =>
            state.LifetimeFinesUsd == 0L && state.Date.Year >= 2026;

        /// <summary>
        /// A model shipped with all three protections at the top tier.
        ///
        /// The top tier is <c>TierCount - 1</c> rather than a written number, which is the same
        /// expression <c>SaveStore</c> clamps to, so a fifth tier moves both at once.
        /// </summary>
        private static bool FullySafeRelease(CompanyState state)
        {
            var top = SafetyModuleCatalog.TierCount - 1;

            foreach (var model in state.DeployedModels)
            {
                if (model.AssaTier >= top && model.RedTeamTier >= top && model.DataProtectionTier >= top)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
