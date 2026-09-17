using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// How the corpus is cut into tokens, which is the one number this whole economy is priced in.
    ///
    /// **Why this is on the DATA stage and not a piece of hardware.** The player reads a price per
    /// token on every screen in the game, so a technology that spends fewer tokens on the same
    /// sentence is legible without being explained: the same conversation costs less to serve and
    /// the audiences that mind the cost of serving notice. The accelerators are `HardwareCatalog`'s
    /// subject and a second ladder of silicon on a data screen would be two mechanisms for one
    /// thing.
    ///
    /// **Four rungs, and they are real.** A vocabulary taken off the shelf, splitting into subwords,
    /// byte level merges, and a vocabulary learned against the corpus the company actually holds.
    /// Every one of them is documented practice, in the order it became practice.
    /// </summary>
    public enum TokenizerKind
    {
        /// <summary>Somebody else's vocabulary, used as it came. What a company has on day one.</summary>
        OffTheShelf = 0,

        /// <summary>Words split into pieces that recur, so rare words stop costing a token each.</summary>
        Subword = 1,

        /// <summary>Merges over bytes: nothing is ever unknown, and the long tail gets much cheaper.</summary>
        ByteLevelBpe = 2,

        /// <summary>A vocabulary fitted to the corpus this company holds rather than to the web at large.</summary>
        LearnedVocabulary = 3
    }

    /// <summary>
    /// One rung: what it costs to run a token through, before and after the corpus has been adapted
    /// to it.
    ///
    /// **Two figures rather than one, and no arithmetic between rungs.** Stating the floor each rung
    /// can reach makes the whole ladder readable in one table and stops the top rung being a rung
    /// with nothing to invest in, which is what deriving the adaptation from "half the gap to the
    /// next one" would have produced.
    /// </summary>
    public readonly struct TokenizerDefinition
    {
        public TokenizerDefinition(TokenizerKind kind, double tokensPerText, double adaptedTokensPerText,
            ResearchNodeId opensWith)
        {
            Kind = kind;
            TokensPerText = Math.Clamp(SimUnits.Finite(tokensPerText, 1.0), 0.5, 1.0);
            AdaptedTokensPerText = Math.Clamp(SimUnits.Finite(adaptedTokensPerText, 1.0),
                0.5, TokensPerText);
            OpensWith = opensWith;
        }

        public TokenizerKind Kind { get; }

        /// <summary>Tokens spent on the same text, against an off-the-shelf vocabulary at 1.0.</summary>
        public double TokensPerText { get; }

        /// <summary>The same, with the corpus fully adapted to this vocabulary.</summary>
        public double AdaptedTokensPerText { get; }

        /// <summary>The node that opens this rung, or <see cref="ResearchNodeId.None"/> for the first.</summary>
        public ResearchNodeId OpensWith { get; }

        /// <summary>Read from the book at access time, like every other catalog here.</summary>
        public string DisplayName => Loc.T(KeyFor(Kind));

        public string Pitch => Loc.T(KeyFor(Kind) + ".pitch");

        public string Note => Loc.T(KeyFor(Kind) + ".note");

        /// <summary>
        /// What a token costs at a level of adaptation, between the two figures above.
        ///
        /// **Level zero is exactly <see cref="TokensPerText"/>.** The neutral-option rule this
        /// project holds every catalog to: whatever a player gets without choosing has to be the
        /// figure everything else was balanced against, or adding the control retunes the game
        /// underneath everyone who never touches it.
        /// </summary>
        public double TokensPerTextAt(int adaptationLevel)
        {
            var level = Math.Clamp(adaptationLevel, 0, TokenizerCatalog.AdaptationLevels - 1);
            var share = level / (double)(TokenizerCatalog.AdaptationLevels - 1);

            return TokensPerText - (TokensPerText - AdaptedTokensPerText) * share;
        }

        private static string KeyFor(TokenizerKind kind) => kind switch
        {
            TokenizerKind.Subword => "tok.subword",
            TokenizerKind.ByteLevelBpe => "tok.bytebpe",
            TokenizerKind.LearnedVocabulary => "tok.learned",
            _ => "tok.shelf"
        };
    }

    /// <summary>
    /// The ladder, and what adapting a corpus to a rung costs.
    ///
    /// **The cost is a share of the run's own bill, not a price list.** A fixed figure is a real
    /// decision in 2022 and a rounding error in 2031, so the six steps are shares: the choice stays
    /// the same size for the whole campaign. Step one is free and changes nothing.
    /// </summary>
    public static class TokenizerCatalog
    {
        public const string CatalogVersion = "tokenizer-1";

        /// <summary>Six steps on the bar under the field, the author's number.</summary>
        public const int AdaptationLevels = 6;

        /// <summary>
        /// What each step adds to the run's bill, as a share of it.
        ///
        /// The last step is a quarter of the whole run. It has to be felt: the technology is meant
        /// to do the heavy lifting and the money is meant to be the impatient way to some of it.
        /// </summary>
        private static readonly double[] AdaptationShares = { 0.0, 0.015, 0.04, 0.09, 0.16, 0.25 };

        private static readonly TokenizerDefinition[] Rungs =
        {
            new(TokenizerKind.OffTheShelf, tokensPerText: 1.00, adaptedTokensPerText: 0.965,
                opensWith: ResearchNodeId.None),

            new(TokenizerKind.Subword, tokensPerText: 0.95, adaptedTokensPerText: 0.925,
                opensWith: ResearchNodeId.SubwordTokenizer),

            new(TokenizerKind.ByteLevelBpe, tokensPerText: 0.90, adaptedTokensPerText: 0.87,
                opensWith: ResearchNodeId.ByteLevelTokenizer),

            new(TokenizerKind.LearnedVocabulary, tokensPerText: 0.84, adaptedTokensPerText: 0.80,
                opensWith: ResearchNodeId.LearnedVocabulary)
        };

        public static IReadOnlyList<TokenizerDefinition> All => Rungs;

        public static TokenizerDefinition Get(TokenizerKind kind)
        {
            foreach (var rung in Rungs)
            {
                if (rung.Kind == kind)
                {
                    return rung;
                }
            }

            return Rungs[0];
        }

        /// <summary>The node a rung needs, or None. One reading for the rule and for the screen.</summary>
        public static ResearchNodeId GateFor(TokenizerKind kind) => Get(kind).OpensWith;

        /// <summary>
        /// What a token costs on this run: the rung, adapted as far as the money went.
        ///
        /// Everything else in the game reads this one figure, so the number the creator prints is
        /// the number the market and the fleet are charged by.
        /// </summary>
        public static double TokensPerText(TokenizerKind kind, int adaptationLevel) =>
            Get(kind).TokensPerTextAt(adaptationLevel);

        /// <summary>Share of the training bill that a level of adaptation costs.</summary>
        public static double AdaptationShare(int adaptationLevel) =>
            AdaptationShares[Math.Clamp(adaptationLevel, 0, AdaptationLevels - 1)];

        /// <summary>The money, against a run that bills <paramref name="trainingBillUsd"/>.</summary>
        public static long AdaptationCostUsd(int adaptationLevel, double trainingBillUsd) =>
            (long)Math.Round(Math.Max(0.0, SimUnits.Finite(trainingBillUsd))
                             * AdaptationShare(adaptationLevel));
    }
}
