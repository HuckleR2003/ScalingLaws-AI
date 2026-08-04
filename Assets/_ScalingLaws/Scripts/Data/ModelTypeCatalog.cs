using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>What the model is for. Chosen once per model, at creation.</summary>
    public enum ModelType
    {
        None = 0,

        /// <summary>Good at everything, best at nothing. Available from day one, never researched.</summary>
        General = 1,

        /// <summary>Code, tests, refactoring. Worthless in 2022 and dominant three years later.</summary>
        Coding = 2,

        /// <summary>Conversation and tone. The segment that turns a product into a habit.</summary>
        Conversational = 3,

        /// <summary>Back office work done end to end. Slow to sell, hard to displace.</summary>
        Automation = 4,

        /// <summary>Given a machine and left to run it. The expensive one, and the late one.</summary>
        Agentic = 5
    }

    /// <summary>
    /// One model type: how well it serves each audience, and what it costs to be allowed to build it.
    ///
    /// Affinity is a multiplier on that segment's demand, not a share. A general model at 0.85
    /// everywhere is worse for developers than a coding model at 1.60 and better than it for
    /// everybody else, which is the whole trade. Nothing reaches 1.0 across the board, because a
    /// type that was good at everything would make the choice free.
    /// </summary>
    public sealed class ModelTypeDefinition
    {
        public ModelTypeDefinition(ModelType type, string displayName, string description,
            ResearchNodeId requires, double servingCostMultiplier,
            (AudienceSegment Segment, double Affinity)[] affinities)
        {
            Type = type;
            DisplayName = displayName ?? type.ToString();
            Description = description ?? string.Empty;
            Requires = requires;
            ServingCostMultiplier = Math.Clamp(servingCostMultiplier, 0.5, 3.0);

            var map = new Dictionary<AudienceSegment, double>();
            foreach (var (segment, affinity) in affinities ?? Array.Empty<(AudienceSegment, double)>())
            {
                map[segment] = Math.Clamp(affinity, 0.0, 3.0);
            }

            Affinities = map;
        }

        public ModelType Type { get; }
        public string DisplayName { get; }
        public string Description { get; }

        /// <summary>The node that has to be finished first. None means available from the start.</summary>
        public ResearchNodeId Requires { get; }

        /// <summary>What a token costs to serve for this type. An agent thinks for longer.</summary>
        public double ServingCostMultiplier { get; }

        public IReadOnlyDictionary<AudienceSegment, double> Affinities { get; }

        public double AffinityFor(AudienceSegment segment) =>
            Affinities.TryGetValue(segment, out var value) ? value : 0.0;

        public override string ToString() => $"{DisplayName} (needs {Requires})";
    }

    /// <summary>
    /// The ONE model type library.
    ///
    /// Types exist so that the question stops being "how good is this model" and starts being "good
    /// at what, and is anybody buying that yet". Combined with <see cref="AudienceCatalog"/>, a type
    /// researched too early is capital spent on a market with no customers in it, which is the same
    /// mistake as buying accelerators a year before they are needed. That is the spine of the game
    /// applied to something other than hardware.
    /// </summary>
    public static class ModelTypeCatalog
    {
        public const string CatalogVersion = "model-types-2026-08-04";

        private static readonly ModelTypeDefinition[] Entries =
        {
            new(ModelType.General, "General purpose",
                "Answers anything, leads at nothing. Every company ships one and none of them win "
                + "with it after the first two years.",
                ResearchNodeId.None, servingCostMultiplier: 1.00,
                new[]
                {
                    (AudienceSegment.Consumer, 1.00),
                    (AudienceSegment.Developer, 0.60),
                    (AudienceSegment.Enterprise, 0.55),
                    (AudienceSegment.Creative, 0.85),
                    (AudienceSegment.Agentic, 0.30)
                }),

            new(ModelType.Coding, "Coding",
                "Trained and tuned on repositories, tests and diffs. Reads a codebase instead of "
                + "guessing at one.",
                ResearchNodeId.CodingModels, servingCostMultiplier: 1.10,
                new[]
                {
                    (AudienceSegment.Consumer, 0.30),
                    (AudienceSegment.Developer, 1.75),
                    (AudienceSegment.Enterprise, 0.70),
                    (AudienceSegment.Creative, 0.20),
                    (AudienceSegment.Agentic, 0.80)
                }),

            new(ModelType.Conversational, "Conversational",
                "Tone, memory and the sense of talking to something. Turns a tool into a habit, "
                + "which is worth more than it sounds.",
                ResearchNodeId.ConversationalModels, servingCostMultiplier: 0.90,
                new[]
                {
                    (AudienceSegment.Consumer, 1.55),
                    (AudienceSegment.Developer, 0.35),
                    (AudienceSegment.Enterprise, 0.50),
                    (AudienceSegment.Creative, 1.30),
                    (AudienceSegment.Agentic, 0.25)
                }),

            new(ModelType.Automation, "Automation",
                "Long documents, structured output and processes that used to need a department. "
                + "Sells slowly and never leaves.",
                ResearchNodeId.AutomationModels, servingCostMultiplier: 1.25,
                new[]
                {
                    (AudienceSegment.Consumer, 0.25),
                    (AudienceSegment.Developer, 0.55),
                    (AudienceSegment.Enterprise, 1.80),
                    (AudienceSegment.Creative, 0.45),
                    (AudienceSegment.Agentic, 0.90)
                }),

            new(ModelType.Agentic, "Autonomous agent",
                "Given a machine, a task and no supervision. The most expensive thing on this list to "
                + "build and the only one that owns the late game.",
                ResearchNodeId.AgenticWorkstation, servingCostMultiplier: 1.85,
                new[]
                {
                    (AudienceSegment.Consumer, 0.20),
                    (AudienceSegment.Developer, 1.05),
                    (AudienceSegment.Enterprise, 1.30),
                    (AudienceSegment.Creative, 0.20),
                    (AudienceSegment.Agentic, 2.00)
                })
        };

        private static readonly Dictionary<ModelType, ModelTypeDefinition> ByType = BuildIndex();

        public static IReadOnlyList<ModelTypeDefinition> All => Entries;

        public static ModelTypeDefinition Get(ModelType type) =>
            ByType.TryGetValue(type, out var found) ? found : Entries[0];

        private static Dictionary<ModelType, ModelTypeDefinition> BuildIndex()
        {
            var map = new Dictionary<ModelType, ModelTypeDefinition>();
            foreach (var entry in Entries)
            {
                map[entry.Type] = entry;
            }

            return map;
        }

        /// <summary>
        /// How much of the market this type can reach on a date: every segment's share weighted by
        /// how well the type serves it. One is the whole market perfectly served, which nothing
        /// reaches. General sits near 0.75 in 2022 and drifts down as the market it is worst at grows.
        /// </summary>
        public static double ReachOn(ModelType type, GameDate date)
        {
            var definition = Get(type);
            var shares = AudienceCatalog.SharesOn(date);
            var segments = AudienceCatalog.All;

            var reach = 0.0;
            for (var index = 0; index < segments.Count; index++)
            {
                reach += shares[index] * definition.AffinityFor(segments[index].Segment);
            }

            return reach;
        }

        /// <summary>
        /// What this type's audience will pay, relative to the baseline consumer. A coding model
        /// reaches people who mind a price rise less, so the same subscription costs it fewer users.
        /// </summary>
        public static double PriceToleranceOn(ModelType type, GameDate date)
        {
            var definition = Get(type);
            var shares = AudienceCatalog.SharesOn(date);
            var segments = AudienceCatalog.All;

            var weighted = 0.0;
            var total = 0.0;

            for (var index = 0; index < segments.Count; index++)
            {
                var weight = shares[index] * definition.AffinityFor(segments[index].Segment);
                weighted += weight * segments[index].WillingnessToPay;
                total += weight;
            }

            return total <= 0.0 ? 1.0 : weighted / total;
        }

        /// <summary>
        /// Where this type's users actually come from on a date, normalised. This is what the
        /// audience readout draws, and it is the same arithmetic the demand split uses rather than
        /// a second copy of it.
        /// </summary>
        public static double[] AudienceMixOn(ModelType type, GameDate date)
        {
            var definition = Get(type);
            var shares = AudienceCatalog.SharesOn(date);
            var segments = AudienceCatalog.All;

            var mix = new double[segments.Count];
            var total = 0.0;

            for (var index = 0; index < segments.Count; index++)
            {
                mix[index] = shares[index] * definition.AffinityFor(segments[index].Segment);
                total += mix[index];
            }

            if (total <= 0.0)
            {
                return mix;
            }

            for (var index = 0; index < mix.Length; index++)
            {
                mix[index] /= total;
            }

            return mix;
        }
    }
}
