using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>What the numbers are kept in during the run.</summary>
    public enum TrainingPrecision
    {
        /// <summary>Full width. Slow, expensive, and it never surprises you.</summary>
        Float32 = 0,

        /// <summary>
        /// Double width. What you train at before you have researched anything at all.
        ///
        /// Numbered after the others because the enum is saved as an integer and renumbering it
        /// would silently rewrite the precision of every model in every existing save.
        /// </summary>
        Float64 = 3,

        /// <summary>The default from 2023 onward. Half the width, none of the drama.</summary>
        BFloat16 = 1,

        /// <summary>Eight bits. Twice the throughput and the run can come apart.</summary>
        Float8 = 2
    }

    /// <summary>How the parameters are arranged: many thin layers or few fat ones.</summary>
    public enum ModelShape
    {
        Deep = 0,
        Balanced = 1,
        Wide = 2
    }

    /// <summary>How hard the corpus is scrubbed for repeats before the run sees it.</summary>
    public enum DeduplicationPass
    {
        None = 0,
        Standard = 1,
        Aggressive = 2
    }

    /// <summary>
    /// The three decisions the Scale and Data stages were missing, as cards rather than sliders.
    ///
    /// **Every one of them is a trade with a real published shape**, which is the bar the rest of
    /// this project holds itself to. None of them is a straight upgrade, and the multipliers are
    /// centred so that the middle option is exactly 1.0 on every axis. That last part matters more
    /// than it looks: it means adding these does not rebalance a single number for a player who
    /// leaves them alone, the same rule the audience catalog follows.
    /// </summary>
    public readonly struct PrecisionDefinition
    {
        public PrecisionDefinition(TrainingPrecision precision,
            double throughput, double instability, GameDate earliest, bool hasWarning = false)
        {
            Precision = precision;
            Throughput = Math.Clamp(SimUnits.Finite(throughput, 1.0), 0.25, 4.0);
            Instability = Math.Clamp(SimUnits.Finite(instability), 0.0, 4.0);
            Earliest = earliest;
            HasWarning = hasWarning;
        }

        public TrainingPrecision Precision { get; }

        /// <summary>
        /// The phrase-book stem for a width.
        ///
        /// Written out rather than derived from the enum name, same as every other catalog here:
        /// `Float64` is not what a player reads and a derived key is one nobody would think to
        /// look for.
        /// </summary>
        private static string KeyFor(TrainingPrecision precision) => precision switch
        {
            TrainingPrecision.Float64 => "width.fp64",
            TrainingPrecision.Float32 => "width.fp32",
            TrainingPrecision.BFloat16 => "width.bf16",
            _ => "width.fp8"
        };

        /// <summary>
        /// Read from the book at access time, never stored.
        ///
        /// **A catalog that stores a string keeps whatever language it was built in.** These are
        /// built once at type load, before the player has chosen anything, so a stored name leaves
        /// the busiest screen in the game half English however much else is translated. Found by
        /// rendering the DATA stage on a Polish machine and looking at it.
        /// </summary>
        public string DisplayName => Loc.T(KeyFor(Precision));
        public string Pitch => Loc.T(KeyFor(Precision) + ".pitch");

        /// <summary>Compute the same cluster delivers at this width. More is faster and cheaper.</summary>
        public double Throughput { get; }

        /// <summary>
        /// How much wider the finished run lands from its projection.
        ///
        /// This is the cost of the fast option and it is the honest one: low precision training does
        /// not make a worse model on average, it makes a **less predictable** one. Multiplies the
        /// founder's own spread, so a strong Development skill is what buys the right to gamble.
        /// </summary>
        public double Instability { get; }

        /// <summary>
        /// When the silicon can do it at all.
        ///
        /// FP8 is not a research node, it is a property of the accelerator, which puts it squarely
        /// on the spine: the option arrives on a date and buying into it early is the same mistake
        /// as buying the hardware early.
        /// </summary>
        public GameDate Earliest { get; }

        /// <summary>Whether this width carries a warning at all. Only FP8 does.</summary>
        public bool HasWarning { get; }

        public string Warning => HasWarning ? Loc.T(KeyFor(Precision) + ".warning") : string.Empty;
    }

    public readonly struct ShapeDefinition
    {
        public ShapeDefinition(ModelShape shape, double capability, double servingBurden)
        {
            Shape = shape;
            Capability = Math.Clamp(SimUnits.Finite(capability, 1.0), 0.8, 1.2);
            ServingBurden = Math.Clamp(SimUnits.Finite(servingBurden, 1.0), 0.7, 1.4);
        }

        public ModelShape Shape { get; }

        private static string KeyFor(ModelShape shape) => shape switch
        {
            ModelShape.Deep => "shape.deep",
            ModelShape.Wide => "shape.wide",
            _ => "shape.balanced"
        };

        /// <summary>Read from the book at access time. See <see cref="PrecisionDefinition"/>.</summary>
        public string DisplayName => Loc.T(KeyFor(Shape));
        public string Pitch => Loc.T(KeyFor(Shape) + ".pitch");

        /// <summary>What the same parameter count is worth arranged this way.</summary>
        public double Capability { get; }

        /// <summary>
        /// What a token costs to produce on it.
        ///
        /// Depth is sequential and width is parallel, so a deep model is dearer to serve at the same
        /// size. That is the trade, and it is the reason this is not simply a capability slider.
        /// </summary>
        public double ServingBurden { get; }

        public string Note => Loc.T(KeyFor(Shape) + ".note");
    }

    public readonly struct DeduplicationDefinition
    {
        public DeduplicationDefinition(DeduplicationPass pass, double tokensKept, double quality)
        {
            Pass = pass;
            TokensKept = Math.Clamp(SimUnits.Finite(tokensKept, 1.0), 0.4, 1.2);
            Quality = Math.Clamp(SimUnits.Finite(quality, 1.0), 0.8, 1.3);
        }

        public DeduplicationPass Pass { get; }

        private static string KeyFor(DeduplicationPass pass) => pass switch
        {
            DeduplicationPass.None => "dedup.raw",
            DeduplicationPass.Aggressive => "dedup.aggressive",
            _ => "dedup.standard"
        };

        /// <summary>Read from the book at access time. See <see cref="PrecisionDefinition"/>.</summary>
        public string DisplayName => Loc.T(KeyFor(Pass));
        public string Pitch => Loc.T(KeyFor(Pass) + ".pitch");

        /// <summary>Share of the corpus that survives the pass.</summary>
        public double TokensKept { get; }

        /// <summary>What the surviving tokens are worth each.</summary>
        public double Quality { get; }

        public string Note => Loc.T(KeyFor(Pass) + ".note");
    }

    /// <summary>
    /// The three catalogs, and the rules for reading them.
    ///
    /// Numbers are shaped from published practice and rounded hard. FP8 roughly doubling throughput
    /// over BF16 is the vendor claim for the 2023 generation onward; deduplication improving quality
    /// while shrinking the corpus is the finding every large corpus paper reports. They are coarse
    /// on purpose, like every other catalog here.
    /// </summary>
    public static class TrainingChoiceCatalog
    {
        public const string CatalogVersion = "choices-1";

        /// <summary>
        /// How far back a corpus can be cut, in months before the run starts.
        ///
        /// Nought is everything up to today and is the dearest and messiest. Two years back is
        /// cheap, clean, well studied, and describes a world that has moved on.
        /// </summary>
        public static readonly int[] CutoffMonths = { 0, 6, 12, 24 };

        private static readonly PrecisionDefinition[] Precisions =
        {
            // **The rungs are stated against FP64, because FP64 is the neutral option.**
            //
            // All four widths are now a ladder and the bottom of it is the one a company with no
            // research trains at, so that is where the 1.0 sits. This is the third time this file
            // has been re-anchored and the rule has been the same every time: whatever a player
            // gets for free has to be 1.0, or the whole economy is quietly retuned underneath them.
            //
            // The top of the ladder is also an anchor. FP8 stays at 3.36 rather than being scaled
            // up with everything else, because late-game throughput was balanced against that
            // number and a fourth rung is not a reason to move the ceiling.
            // **The words live in the phrase book, not here.** Every name, pitch, note and warning
            // for these ten entries is `width.*`, `shape.*` and `dedup.*`, read at access time. A
            // catalog built at type load keeps whatever language it was built in, which left the
            // busiest screen in the game half English on a Polish machine.

            new(TrainingPrecision.Float64,
                throughput: 1.0,
                instability: 1.0,
                earliest: GameDate.Start),

            new(TrainingPrecision.Float32,
                throughput: 1.30,
                instability: 1.05,
                earliest: GameDate.Start),

            new(TrainingPrecision.BFloat16,
                throughput: 2.10,
                instability: 1.40,
                earliest: GameDate.Start),

            new(TrainingPrecision.Float8,
                throughput: 3.36,
                instability: 3.2,
                earliest: GameDate.FromCalendar(2023, 3, 1),
                hasWarning: true)
        };

        private static readonly ShapeDefinition[] Shapes =
        {
            new(ModelShape.Deep, capability: 1.07, servingBurden: 1.22),
            new(ModelShape.Balanced, capability: 1.0, servingBurden: 1.0),
            new(ModelShape.Wide, capability: 0.95, servingBurden: 0.82)
        };

        private static readonly DeduplicationDefinition[] Passes =
        {
            new(DeduplicationPass.None, tokensKept: 1.0, quality: 0.92),
            new(DeduplicationPass.Standard, tokensKept: 1.0, quality: 1.0),
            new(DeduplicationPass.Aggressive, tokensKept: 0.80, quality: 1.14)
        };

        public static IReadOnlyList<PrecisionDefinition> AllPrecisions => Precisions;
        public static IReadOnlyList<ShapeDefinition> AllShapes => Shapes;
        public static IReadOnlyList<DeduplicationDefinition> AllPasses => Passes;

        public static PrecisionDefinition Get(TrainingPrecision precision)
        {
            foreach (var entry in Precisions)
            {
                if (entry.Precision == precision)
                {
                    return entry;
                }
            }

            return Precisions[1];
        }

        public static ShapeDefinition Get(ModelShape shape)
        {
            foreach (var entry in Shapes)
            {
                if (entry.Shape == shape)
                {
                    return entry;
                }
            }

            return Shapes[1];
        }

        public static DeduplicationDefinition Get(DeduplicationPass pass)
        {
            foreach (var entry in Passes)
            {
                if (entry.Pass == pass)
                {
                    return entry;
                }
            }

            return Passes[1];
        }

        public static bool IsAvailableOn(TrainingPrecision precision, GameDate date) =>
            date.IsOnOrAfter(Get(precision).Earliest);

        /// <summary>
        /// The node that opens an option, or None when it needs no research.
        ///
        /// One place where the choices and the tree meet, so a node renamed or a gate moved is a
        /// change to this table rather than a hunt through the creator. The neutral option of every
        /// catalog is ungated by construction: the middle is what the game did before any of this
        /// existed and it can never be locked away.
        /// </summary>
        /// <summary>
        /// What has to be researched before a run may be trained at this width.
        ///
        /// **A ladder now, not a single gate.** Only FP8 used to be locked, so every company
        /// started with the modern default and the choice was "the good one, or the deliberately
        /// worse one". Now the company starts at full width — slow, expensive, and it never
        /// surprises you — and buys its way down: mixed precision opens BF16, low precision opens
        /// FP8. That makes the early game genuinely more expensive and makes two research nodes
        /// worth something the player can feel on the next run.
        ///
        /// **FP32 is ungated and always will be.** It is the neutral option, and this catalog has
        /// already learned once what happens when a neutral option is put behind a node: every
        /// existing company loses behaviour it always had, and twenty five tests say so within a
        /// minute. A company with no research must still be able to train something.
        /// </summary>
        public static ResearchNodeId GateFor(TrainingPrecision precision) => precision switch
        {
            TrainingPrecision.Float8 => ResearchNodeId.LowPrecisionTraining,
            TrainingPrecision.BFloat16 => ResearchNodeId.MixedPrecisionTraining,
            TrainingPrecision.Float32 => ResearchNodeId.SinglePrecisionTraining,
            _ => ResearchNodeId.None
        };

        public static ResearchNodeId GateFor(DeduplicationPass pass) =>
            pass == DeduplicationPass.Aggressive
                ? ResearchNodeId.CorpusDeduplication
                : ResearchNodeId.None;

        /// <summary>
        /// Nothing. The cutoff is never gated, and the first attempt at this was a mistake.
        ///
        /// Cutoff zero, everything up to the day the run starts, is what the game has always done.
        /// Putting a node in front of it locked away the behaviour every existing company already
        /// had, and twenty five tests said so within a minute. **The neutral option of a catalog can
        /// never require research.**
        ///
        /// The pipeline earns its place by making fresh text cheaper rather than possible, which is
        /// a real benefit that takes nothing away.
        /// </summary>
        public static ResearchNodeId GateForCutoff(int monthsBack) => ResearchNodeId.None;

        /// <summary>
        /// What the Continuous data pipeline takes off the bill for recent text.
        ///
        /// Only on the fresh end, because that is the part the pipeline actually touches: licensing
        /// a two year old archive is the same job whether or not there is an ingest running.
        /// </summary>
        public const double PipelineDiscount = 0.7;

        public static double CutoffCostMultiplier(int monthsBack, bool hasPipeline) =>
            CutoffCostMultiplier(monthsBack)
            * (hasPipeline && monthsBack < 12 ? PipelineDiscount : 1.0);

        /// <summary>
        /// What a stale corpus costs, as a multiplier on the finished capability.
        ///
        /// A model that has never read anything from the last two years is not a worse model, it is
        /// a model that is wrong about the present, and the market scores it accordingly. The penalty
        /// is small per month and it compounds with the model's own ageing, which is the point: a
        /// cheap corpus and a slow release are the same mistake twice.
        /// </summary>
        public const double StalePenaltyPerMonth = 0.006;

        public static double CutoffCapabilityMultiplier(int monthsBack) =>
            Math.Clamp(1.0 - Math.Max(0, monthsBack) * StalePenaltyPerMonth, 0.80, 1.0);

        /// <summary>
        /// What a fresh corpus costs to license, as a multiplier on the data bill.
        ///
        /// Recent text is dearer because nobody has cleaned it yet and the people who own it know
        /// what it is for. Two years back is a third off.
        /// </summary>
        /// <summary>
        /// What the corpus costs, relative to taking everything up to today.
        ///
        /// **Today is exactly 1.0 and going back is cheaper.** The first version of this had today
        /// at 1.35, which quietly raised the data bill by a third on every run in the game including
        /// every one that had never touched the control, and the five year balance test caught it at
        /// 0.055% share. It is the same rule the audience catalog and the shape catalog follow, and
        /// writing it in a comment two files ago did not stop me breaking it here.
        /// </summary>
        public static double CutoffCostMultiplier(int monthsBack) =>
            Math.Clamp(1.0 - Math.Max(0, monthsBack) * 0.013, 0.70, 1.0);
    }
}
