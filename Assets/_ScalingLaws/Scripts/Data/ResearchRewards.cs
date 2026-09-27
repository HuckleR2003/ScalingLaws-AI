using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>What kind of thing a node hands over. Seven, because seven is what the game has.</summary>
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
        Ceiling = 5,

        /// <summary>A control on a screen that does nothing until this node lands.</summary>
        Control = 6
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
        /// <summary>
        /// Nodes whose reward is a control rather than a thing in a catalogue.
        ///
        /// Written out because there is nowhere else for it to live: the rule each of these opens
        /// is a line in `CompanySimulation`, and a node has no field that could point at one. Keep
        /// it to genuine controls. Anything that unlocks a corpus, a family, a tier, a type, an
        /// upgrade line or a ceiling is already read off the node and must not be repeated here.
        /// </summary>
        private static readonly Dictionary<ResearchNodeId, string> Controls = new()
        {
            { ResearchNodeId.ModelSeries, "unlock.series" },

            // The four Operations nodes. `RoomUpgrades.From` reads each one directly, so there is
            // no table to walk: the rule is a constructor argument in `Data/RoomUpgrades.cs`.
            { ResearchNodeId.RackTelemetry, "unlock.telemetry" },
            { ResearchNodeId.AirflowModelling, "unlock.airflow" },
            { ResearchNodeId.LiquidLoops, "unlock.liquid" },
            { ResearchNodeId.OwnSubstation, "unlock.substation" },

            // Statecraft. Each is one `HasResearch` in `CompanySimulation.Statecraft.cs`.
            { ResearchNodeId.SovereignLiaison, "unlock.liaison" },
            { ResearchNodeId.ContinuousOversight, "unlock.oversight" },
            { ResearchNodeId.RedundantInference, "unlock.redundant" },

            // The knowledge cutoff on the DATA stage, which this node makes cheaper to keep fresh.
            { ResearchNodeId.ContinuousDataPipeline, "unlock.pipeline" }
        };

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
            // **A node whose whole reward is a control that starts working.** Nothing on the
            // node itself can say so: there is no field for "the line picker on the FOUNDATION
            // stage", and the rule lives in `CompanySimulation`. So the pairs are written out here,
            // one row each, and `ResearchRewardTests` fails on a node that gives nothing at all.
            //
            // This exists because `ModelSeries` cost three million dollars, seventy five days and a
            // hundred and twenty petaflop-days and was read by no caller anywhere in the game. The
            // author found it by reading a card that listed nothing under a node he had paid for.
            // **Every catalogue that names a node, read the same way the ceilings already are.**
            // The first version of this method only knew about fields on the node itself, so the
            // thirty nodes whose whole job is to open a row in some other table came back empty and
            // their cards listed nothing. Each block below is one of those tables, walked in the
            // one direction that cannot drift: the catalogue says which node opens it, so a rung
            // added tomorrow reports itself without anybody editing a description.

            foreach (var precision in TrainingChoiceCatalog.AllPrecisions)
            {
                if (TrainingChoiceCatalog.GateFor(precision.Precision) == node.Id)
                {
                    rewards.Add(new ResearchReward(RewardKind.Control, precision.DisplayName));
                }
            }

            foreach (var pass in TrainingChoiceCatalog.AllPasses)
            {
                if (TrainingChoiceCatalog.GateFor(pass.Pass) == node.Id)
                {
                    rewards.Add(new ResearchReward(RewardKind.Control, pass.DisplayName));
                }
            }

            foreach (var tier in SafetyModuleCatalog.All)
            {
                if (tier.Requires == node.Id)
                {
                    rewards.Add(new ResearchReward(RewardKind.Control, tier.DisplayName));
                }
            }

            foreach (var rung in TokenizerCatalog.All)
            {
                if (rung.OpensWith == node.Id)
                {
                    rewards.Add(new ResearchReward(RewardKind.Control, rung.DisplayName));
                }
            }

            foreach (var place in OfficeCatalog.All)
            {
                if (OfficeUnlocks.RequiredFor(place.Tier) == node.Id)
                {
                    rewards.Add(new ResearchReward(RewardKind.Control, place.DisplayName));
                }
            }

            if (Controls.TryGetValue(node.Id, out var control))
            {
                rewards.Add(new ResearchReward(RewardKind.Control, Loc.T(control)));
            }

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

            // **A node whose only job is to be on the way to others still has a job.** Three of
            // them are pure junctions: the starting node and two late-era roots that nothing reads
            // but four and three nodes respectively need. A card listing nothing under one of those
            // reads as a node that does not work, when the honest answer is that it opens the road.
            //
            // Last, and only when nothing else was found, so a node that genuinely hands something
            // over says that instead: "opens two more nodes" is the weakest true thing a card can
            // say and it must never crowd out a corpus or a ceiling.
            if (rewards.Count == 0)
            {
                var opens = 0;

                foreach (var other in ResearchTree.All)
                {
                    foreach (var prerequisite in other.Prerequisites)
                    {
                        if (prerequisite == node.Id)
                        {
                            opens++;
                        }
                    }
                }

                if (opens > 0)
                {
                    rewards.Add(new ResearchReward(RewardKind.Control,
                        Loc.T("unlock.opens_road", opens.ToString())));
                }
            }

            return rewards;
        }

        /// <summary>Whether this node hands over anything at all a card could show.</summary>
        public static bool GivesAnything(ResearchNodeId id) => Of(id).Count > 0;
    }
}
