using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>What kind of thing a node hands over. Six, because six is what the game has.</summary>
    public enum RewardKind
    {
        /// <summary>A family the creator can build from.</summary>
        Architecture = 0,

        /// <summary>A corpus the company may train on.</summary>
        Corpus = 1,

        /// <summary>A compute tier.</summary>
        Tier = 2,

        /// <summary>A line the upgrade screen can commission.</summary>
        UpgradeLine = 3,

        /// <summary>A kind of model the creator can aim at.</summary>
        ModelType = 4,

        /// <summary>More room on a slider: scale, or one of the architecture directions.</summary>
        Ceiling = 5
    }

    /// <summary>One thing a node gives, as a kind and a name.</summary>
    public readonly struct ResearchReward
    {
        public ResearchReward(RewardKind kind, string name)
        {
            Kind = kind;
            Name = name ?? string.Empty;
        }

        public RewardKind Kind { get; }

        /// <summary>Already translated: every catalogue reads the phrase book at access time.</summary>
        public string Name { get; }

        public override string ToString() => $"{Kind}: {Name}";
    }

    /// <summary>
    /// What each research node actually gives the player.
    ///
    /// **Read off the node rather than written out beside it.** A tester asked for this directly:
    /// *"can you simplify what each research node does? Sometimes you dont really know what
    /// something does"*. The honest way to answer that is not 59 hand-written sentences, which go
    /// stale the first time a node changes and which nothing can check. Every one of these is
    /// already a field on the node or a row in a catalogue that points at it, so this reads them,
    /// and a node that starts unlocking something new says so without anybody remembering to edit a
    /// description.
    ///
    /// In Data because both the board and the opened card need the same answer, and because a test
    /// can then ask the question without a panel.
    /// </summary>
    public static class ResearchRewards
    {
        /// <summary>Everything this node opens, in the order a card should read them.</summary>
        public static IReadOnlyList<ResearchReward> Of(ResearchNodeId id) => Of(ResearchTree.Get(id));

        /// <inheritdoc cref="Of(ResearchNodeId)"/>
        public static IReadOnlyList<ResearchReward> Of(ResearchNode node)
        {
            var rewards = new List<ResearchReward>();

            if (node.UnlocksArchitecture != ArchitectureId.None)
            {
                rewards.Add(new ResearchReward(RewardKind.Architecture,
                    ArchitectureCatalog.Get(node.UnlocksArchitecture).DisplayName));
            }

            if (node.UnlocksData != DatasetSource.None)
            {
                foreach (var corpus in DatasetCatalog.All)
                {
                    if ((node.UnlocksData & corpus.Flag) == corpus.Flag)
                    {
                        rewards.Add(new ResearchReward(RewardKind.Corpus, corpus.DisplayName));
                    }
                }
            }

            if (node.UnlocksTier != ComputeTier.None)
            {
                rewards.Add(new ResearchReward(RewardKind.Tier, node.UnlocksTier.ToString()));
            }

            // ModelTrait has no None member, so the gate flag is the only honest signal that a node
            // opens an upgrade line rather than defaulting to the zero trait.
            if (node.GatesTrait)
            {
                rewards.Add(new ResearchReward(RewardKind.UpgradeLine, node.UnlocksTrait.ToString()));
            }

            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires == node.Id)
                {
                    rewards.Add(new ResearchReward(RewardKind.ModelType, definition.DisplayName));
                }
            }

            // **The ceilings are the reward nobody could see.** A node that lifts the parameter
            // slider or one of the five architecture directions changes what the player is allowed
            // to build, and it said so nowhere: the ladders are tables that name a node, so the node
            // itself carried no sign of being on one.
            if (ScaleCeiling.Ladder.Any(rung => rung.Node == node.Id))
            {
                rewards.Add(new ResearchReward(RewardKind.Ceiling, Loc.T("unlock.scale_ceiling")));
            }

            foreach (var direction in ArchitectureCeiling.Ladders)
            {
                if (direction.Value.Any(rung => rung.Node == node.Id))
                {
                    rewards.Add(new ResearchReward(RewardKind.Ceiling,
                        Loc.T("unlock.arch_ceiling", Loc.T(ArchitectureCeiling.KeyFor(direction.Key)))));
                }
            }

            return rewards;
        }

        /// <summary>Whether this node hands over anything at all a card could show.</summary>
        public static bool GivesAnything(ResearchNodeId id) => Of(id).Count > 0;
    }
}
