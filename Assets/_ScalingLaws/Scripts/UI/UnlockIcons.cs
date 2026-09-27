using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The six glyphs that say what a research node hands over.
    ///
    /// **One per kind, not one per node.** There are 59 nodes and six kinds of reward, and the kind
    /// is what a player reads at a glance on a board: this one opens a corpus, that one opens a
    /// family, the third lifts a ceiling. The node's own name is written beside it, and the opened
    /// card still spells the whole thing out.
    ///
    /// Same contract as <see cref="ResearchIcons"/> and <see cref="SkillIcons"/>: a missing file
    /// draws nothing rather than throwing, so the board is complete before the art is.
    /// </summary>
    public static class UnlockIcons
    {
        private const string ResourceFolder = "Unlocks/";

        private static readonly Dictionary<RewardKind, string> FileNames = new()
        {
            { RewardKind.Architecture, "unlock_architecture" },
            { RewardKind.Corpus, "unlock_corpus" },
            { RewardKind.Tier, "unlock_tier" },
            { RewardKind.UpgradeLine, "unlock_upgrade" },
            { RewardKind.ModelType, "unlock_type" },
            { RewardKind.Ceiling, "unlock_ceiling" }

            // **`Control` is deliberately absent.** There is no picture for it yet and `Get` answers a
            // missing row with null, which draws the empty plate every loader here falls back to.
            // Naming a file that is not on disk is the fault `ArtTests` exists to catch, and it has
            // caught it once already: three research nodes shipped with named icons and no art.
            // Listed in `Docs/NeededGraphics.md`.
        };

        private static readonly Dictionary<RewardKind, Texture2D> Loaded = new();

        public static Texture2D Get(RewardKind kind)
        {
            if (Loaded.TryGetValue(kind, out var cached))
            {
                return cached;
            }

            var texture = FileNames.TryGetValue(kind, out var file)
                ? Resources.Load<Texture2D>(ResourceFolder + file)
                : null;

            Loaded[kind] = texture;
            return texture;
        }

        /// <summary>What to call this kind of reward, as a phrase-book key.</summary>
        public static string KeyFor(RewardKind kind) => kind switch
        {
            RewardKind.Architecture => "unlock.architecture",
            RewardKind.Corpus => "unlock.corpus",
            RewardKind.Tier => "unlock.tier",
            RewardKind.UpgradeLine => "unlock.upgrade_line",
            RewardKind.ModelType => "unlock.model_type",
            RewardKind.Ceiling => "unlock.ceiling",
            RewardKind.Control => "unlock.control",
            _ => "unlock.model_type"
        };
    }
}
