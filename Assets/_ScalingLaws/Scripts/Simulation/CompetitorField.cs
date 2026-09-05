using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Every rival lab, running as agents rather than as a lookup table.
    ///
    /// Seeded from <see cref="CompetitorCatalog"/> so an untouched campaign follows the real 2022 to
    /// 2026 timeline. From there the agents can wait, rush and keep going past the end of the table,
    /// which means the frontier is something that reacts to the player rather than something that
    /// happens to them.
    /// </summary>
    public sealed class CompetitorField
    {
        private readonly List<CompetitorAgent> agents = new();

        public IReadOnlyList<CompetitorAgent> Agents => agents;

        /// <summary>The world of search boxes and ordinary software the first model has to beat.</summary>
        public const double IncumbentCapability = 24.0;

        public const double IncumbentBrand = 0.5;

        public static CompetitorField CreateFromCatalog()
        {
            var field = new CompetitorField();

            foreach (var pair in Strategies)
            {
                field.agents.Add(new CompetitorAgent(pair.Key, LabName(pair.Key), pair.Value));
            }

            // Releases are queued in date order per lab, which is the order the catalog holds them.
            foreach (var release in CompetitorCatalog.All)
            {
                var agent = field.Find(release.Competitor);
                agent?.QueuePlan(release);
            }

            return field;
        }

        /// <summary>
        /// Rebuilds a saved field. The catalog plan is queued fresh and then wound forward past
        /// everything that already shipped, so a loaded campaign keeps its future without replaying
        /// its past.
        /// </summary>
        public void RestoreAgent(
            CompetitorId competitor,
            bool hasShipped,
            string liveModelName,
            double liveCapability,
            double liveBrand,
            double livePrice,
            GameDate liveReleaseDate,
            GameDate nextReleaseDate,
            bool hasPlannedRelease,
            int accumulatedDelayDays,
            bool isWaitingForHardware,
            double drift = 0.0,
            double pendingCapabilityAdjustment = 0.0,
            CompetitorRelease? pending = null,
            int plannedRemaining = -1,
            HardwareGenerationId waitingFor = HardwareGenerationId.None)
        {
            var agent = Find(competitor);
            if (agent == null)
            {
                return;
            }

            if (hasShipped)
            {
                agent.SkipPlannedReleasesUpTo(liveReleaseDate);
            }

            // A lab that already pulled its next release off the plan must not be handed it a second
            // time. Skipping only up to what it last shipped left the pending entry still queued, so
            // a restored lab shipped the same model twice and its whole later cadence was offset.
            if (plannedRemaining >= 0)
            {
                agent.TrimPlanTo(plannedRemaining);
            }

            agent.Restore(
                hasShipped,
                liveModelName,
                liveCapability,
                liveBrand,
                livePrice,
                liveReleaseDate,
                nextReleaseDate,
                hasPlannedRelease,
                accumulatedDelayDays,
                isWaitingForHardware,
                drift,
                pendingCapabilityAdjustment,
                waitingFor);

            if (pending.HasValue)
            {
                agent.RestorePending(pending.Value);
            }
        }

        /// <summary>Position of a lab in the field, or -1. The segment standing is indexed by it.</summary>
        public int IndexOf(CompetitorId competitor)
        {
            for (var index = 0; index < agents.Count; index++)
            {
                if (agents[index].Competitor == competitor)
                {
                    return index;
                }
            }

            return -1;
        }

        public CompetitorAgent Find(CompetitorId competitor)
        {
            foreach (var agent in agents)
            {
                if (agent.Competitor == competitor)
                {
                    return agent;
                }
            }

            return null;
        }

        /// <summary>Runs every lab for one day. Returns the labs that shipped something.</summary>
        public List<CompetitorAgent> Tick(GameDate date, double playerCapability, DeterministicRandom random)
        {
            var shipped = new List<CompetitorAgent>();
            foreach (var agent in agents)
            {
                if (agent.Think(date, playerCapability, random))
                {
                    shipped.Add(agent);
                }
            }

            return shipped;
        }

        public List<RivalModel> LiveModels(GameDate date)
        {
            var models = new List<RivalModel>(agents.Count);
            foreach (var agent in agents)
            {
                if (agent.TryGetLiveModel(date, out var model))
                {
                    models.Add(model);
                }
            }

            return models;
        }

        /// <summary>Best capability any rival has on the market. Falls back to the incumbent world.</summary>
        public double FrontierCapability(GameDate date)
        {
            var best = IncumbentCapability;
            foreach (var agent in agents)
            {
                var capability = agent.CurrentCapability(date);
                if (capability > best)
                {
                    best = capability;
                }
            }

            return best;
        }

        /// <summary>Labs currently sitting out a hardware transition on purpose.</summary>
        public List<CompetitorAgent> LabsWaitingForHardware()
        {
            var waiting = new List<CompetitorAgent>();
            foreach (var agent in agents)
            {
                if (agent.IsWaitingForHardware)
                {
                    waiting.Add(agent);
                }
            }

            return waiting;
        }

        private static readonly Dictionary<CompetitorId, CompetitorStrategy> Strategies = new()
        {
            { CompetitorId.OpenAi, CompetitorStrategy.FrontierRace },
            { CompetitorId.Anthropic, CompetitorStrategy.PatientScaler },
            // The cloud lab is the enterprise play, and until this line existed nothing in the field
            // was EnterpriseFocus at all, which is why Automation had no builder in any year.
            { CompetitorId.GoogleDeepMind, CompetitorStrategy.EnterpriseFocus },
            { CompetitorId.MetaAi, CompetitorStrategy.OpenWeights },

            // The four added 2026-08-15. Two open weight labs whose whole problem is that open
            // weights do not bill, one cost leader that never gets cheap enough to matter, and the
            // enterprise survivor.
            { CompetitorId.StabilityAi, CompetitorStrategy.OpenWeights },
            { CompetitorId.InflectionAi, CompetitorStrategy.CostLeader },
            { CompetitorId.AlephAlpha, CompetitorStrategy.EnterpriseFocus },
            { CompetitorId.Cohere, CompetitorStrategy.EnterpriseFocus },
            { CompetitorId.MistralAi, CompetitorStrategy.OpenWeights },
            { CompetitorId.DeepSeek, CompetitorStrategy.CostLeader },
            { CompetitorId.XAi, CompetitorStrategy.FrontierRace },
            // **The one lab that watches the player.** Four labs shared `CostLeader` and this is
            // the one whose own history is following the frontier closely across whatever it was
            // doing, rather than competing on price. See `CompetitorStrategy.FastFollower`.
            { CompetitorId.AlibabaQwen, CompetitorStrategy.FastFollower },

            // Cheap, because it has nothing else to sell.
            { CompetitorId.Groq, CompetitorStrategy.CostLeader },

            // Emil sells to companies who want the boring thing to work. Same strategy as the two
            // enterprise labs, a quarter of the size.
            { CompetitorId.ESolutions, CompetitorStrategy.EnterpriseFocus }
        };

        private static string LabName(CompetitorId competitor) =>
            CompetitorCatalog.NameOf(competitor);
    }
}
