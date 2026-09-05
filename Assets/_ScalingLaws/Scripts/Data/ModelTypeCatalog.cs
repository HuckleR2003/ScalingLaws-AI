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
        public ModelTypeDefinition(ModelType type,
            ResearchNodeId requires, double servingCostMultiplier,
            (AudienceSegment Segment, double Affinity)[] affinities)
        {
            Type = type;
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

        private static string KeyFor(ModelType type) => type switch
        {
            ModelType.Coding => "modeltype.coding",
            ModelType.Conversational => "modeltype.conversational",
            ModelType.Automation => "modeltype.automation",
            ModelType.Agentic => "modeltype.agentic",
            _ => "modeltype.general"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Type));

        /// <summary>
        /// One word for the tile, where the full name does not fit.
        ///
        /// **A second key rather than a shorter first one.** `DisplayName` is read by the
        /// demographics screen, the research tree and the market summary, and "General purpose"
        /// says something there that "General" does not. The creator's tiles are 200px wide and
        /// wrapped the long one onto two lines with half of it outside the box.
        /// </summary>
        public string ShortName => Loc.T(KeyFor(Type) + ".short");
        public string Description => Loc.T(KeyFor(Type) + ".desc");

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
            new(ModelType.General, ResearchNodeId.None, servingCostMultiplier: 1.00,
                new[]
                {
                    (AudienceSegment.Consumer, 1.00),
                    (AudienceSegment.Developer, 0.60),
                    (AudienceSegment.Enterprise, 0.55),
                    (AudienceSegment.Creative, 0.85),
                    (AudienceSegment.Agentic, 0.30)
                }),

            new(ModelType.Coding, ResearchNodeId.CodingModels, servingCostMultiplier: 1.10,
                new[]
                {
                    (AudienceSegment.Consumer, 0.30),
                    (AudienceSegment.Developer, 1.75),
                    (AudienceSegment.Enterprise, 0.70),
                    (AudienceSegment.Creative, 0.20),
                    (AudienceSegment.Agentic, 0.80)
                }),

            new(ModelType.Conversational, ResearchNodeId.ConversationalModels, servingCostMultiplier: 0.90,
                new[]
                {
                    (AudienceSegment.Consumer, 1.55),
                    (AudienceSegment.Developer, 0.35),
                    (AudienceSegment.Enterprise, 0.50),
                    (AudienceSegment.Creative, 1.30),
                    (AudienceSegment.Agentic, 0.25)
                }),

            new(ModelType.Automation, ResearchNodeId.AutomationModels, servingCostMultiplier: 1.25,
                new[]
                {
                    (AudienceSegment.Consumer, 0.25),
                    (AudienceSegment.Developer, 0.55),
                    (AudienceSegment.Enterprise, 1.80),
                    (AudienceSegment.Creative, 0.45),
                    (AudienceSegment.Agentic, 0.90)
                }),

            new(ModelType.Agentic, ResearchNodeId.AgenticWorkstation, servingCostMultiplier: 1.85,
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

        /// <summary>
        /// Whether the calendar has opened this type yet, on the same clock the player researches on.
        ///
        /// A type with no prerequisite is available from day one. Everything else waits for its own
        /// node's earliest date, so moving a research date moves the whole field with it rather than
        /// leaving a hand-copied year behind to drift.
        /// </summary>
        public static bool IsReachableOn(ModelType type, GameDate date)
        {
            var definition = Get(type);
            if (definition.Requires == ResearchNodeId.None)
            {
                return true;
            }

            return date.DayIndex >= ResearchTree.Get(definition.Requires).EarliestDate.DayIndex;
        }

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
