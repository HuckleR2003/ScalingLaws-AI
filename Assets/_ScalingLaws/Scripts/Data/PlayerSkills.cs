using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What the founder personally is good at. Explicit values, saved, never renumbered.
    ///
    /// Seven, and each one is wired to a different system. A skill that does not change a number
    /// somewhere in Simulation has no business being on this list.
    /// </summary>
    public enum PlayerSkill
    {
        None = 0,
        Development = 1,
        Management = 2,
        Teamwork = 3,
        Concept = 4,
        Software = 5,
        DataEngineering = 6,
        Safety = 7
    }

    /// <summary>
    /// One skill: what it is, what it moves, and how far it can move it.
    ///
    /// Everything is measured from <see cref="PlayerSkillLimits.StartingLevel"/> rather than from
    /// zero. A founder at the default 20 has no bonus and no penalty, so the 200 points spent at
    /// character creation are the entire early difference between two players, and every point
    /// earned later is a real gain rather than a slow climb out of a hole.
    /// </summary>
    public readonly struct PlayerSkillDefinition
    {
        public PlayerSkillDefinition(
            PlayerSkill skill,
            string displayName,
            string description,
            string effectAtFull,
            double fullEffect)
        {
            Skill = skill;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? skill.ToString() : displayName;
            Description = description ?? string.Empty;
            EffectAtFull = effectAtFull ?? string.Empty;
            FullEffect = Math.Clamp(SimUnits.Finite(fullEffect), 0.0, 1.0);
        }

        public PlayerSkill Skill { get; }
        public string DisplayName { get; }

        /// <summary>One line on what this is, for the creator screen.</summary>
        public string Description { get; }

        /// <summary>Plain statement of the effect at level 100, for the card.</summary>
        public string EffectAtFull { get; }

        /// <summary>
        /// Size of the effect at level 100, as a fraction. What that fraction means is decided by
        /// whichever system consumes it, and each one documents its own reading.
        /// </summary>
        public double FullEffect { get; }

        /// <summary>
        /// How much of the effect a given level delivers, 0 at the starting level and 1 at 100.
        /// Below the starting level it goes negative, which is intentional: dumping a skill is a
        /// real choice with a real cost, not simply a missed bonus.
        /// </summary>
        public double EffectAt(int level)
        {
            var clamped = Math.Clamp(level, 0, PlayerSkillLimits.MaximumLevel);
            var span = PlayerSkillLimits.MaximumLevel - PlayerSkillLimits.StartingLevel;
            return FullEffect * (clamped - PlayerSkillLimits.StartingLevel) / span;
        }

        public override string ToString() => $"{DisplayName} (max {FullEffect:P0})";
    }

    /// <summary>Shared limits, kept out of the struct so it can reference them in clamps.</summary>
    public static class PlayerSkillLimits
    {
        public const int MaximumLevel = 100;

        /// <summary>Where every skill starts, and the point at which every effect reads zero.</summary>
        public const int StartingLevel = 20;

        /// <summary>Points to spend at character creation.</summary>
        public const int StartingPoints = 200;

        /// <summary>Levels a single click buys. Twenty clicks, so the choice stays a choice.</summary>
        public const int PointsPerClick = 10;

        /// <summary>
        /// Experience to go from <paramref name="level"/> to the next one. Deliberately close to
        /// linear: a steep curve would make the last twenty levels unreachable inside a campaign and
        /// turn the whole system into a character creation screen with a progress bar attached.
        /// </summary>
        public static long ExperienceForNextLevel(int level)
        {
            var clamped = Math.Clamp(level, 0, MaximumLevel);
            return 50L + 8L * clamped;
        }
    }

    /// <summary>The ONE player skill library.</summary>
    public static class PlayerSkillCatalog
    {
        public const string CatalogVersion = "2026.08.03";

        private static readonly PlayerSkillDefinition[] Entries =
        {
            new(PlayerSkill.Development, "Development",
                "Running the training itself. Schedules, checkpoints, and knowing which run to kill.",
                "Training lands within a third of the usual spread of its projection",
                fullEffect: 0.65),

            new(PlayerSkill.Management, "Management",
                "Payroll, contracts, and the difference between spending money and wasting it.",
                "18 percent off everything the company spends running itself",
                fullEffect: 0.18),

            new(PlayerSkill.Teamwork, "Teamwork",
                "Getting more out of the people already hired before hiring more of them.",
                "Each discipline keeps improving 60 percent further before it saturates",
                fullEffect: 0.60),

            new(PlayerSkill.Concept, "Concept",
                "Architecture and research direction. Choosing what to build before building it.",
                "In-house research reaches 35 percent further and lands more predictably",
                fullEffect: 0.35),

            new(PlayerSkill.Software, "Software",
                "Serving infrastructure. Making a token cheaper to produce without making it worse.",
                "22 percent off the cost of every token served",
                fullEffect: 0.22),

            new(PlayerSkill.DataEngineering, "Data Engineering",
                "Corpora, cleaning and licensing. The unglamorous half of model quality.",
                "Every corpus behaves as if it were 20 percent better",
                fullEffect: 0.20),

            new(PlayerSkill.Safety, "Safety",
                "Evaluation and red teaming. Finding it before a regulator does.",
                "70 percent less likely to have a public failure",
                fullEffect: 0.70)
        };

        private static readonly Dictionary<PlayerSkill, PlayerSkillDefinition> BySkill = BuildIndex();

        public static IReadOnlyList<PlayerSkillDefinition> All => Entries;

        public static PlayerSkillDefinition Get(PlayerSkill skill)
        {
            if (!BySkill.TryGetValue(skill, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown player skill.");
            }

            return definition;
        }

        public static bool TryGet(PlayerSkill skill, out PlayerSkillDefinition definition) =>
            BySkill.TryGetValue(skill, out definition);

        private static Dictionary<PlayerSkill, PlayerSkillDefinition> BuildIndex()
        {
            var index = new Dictionary<PlayerSkill, PlayerSkillDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Skill] = entry;
            }

            return index;
        }
    }
}
