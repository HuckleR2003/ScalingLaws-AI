using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One skill as the creator screen and the profile screen need to draw it.</summary>
    public readonly struct SkillStanding
    {
        public SkillStanding(PlayerSkill skill, int level, long experience, long experienceForNext)
        {
            Skill = skill;
            Level = Math.Clamp(level, 0, PlayerSkillLimits.MaximumLevel);
            Experience = Math.Max(0L, experience);
            ExperienceForNext = Math.Max(1L, experienceForNext);
        }

        public PlayerSkill Skill { get; }
        public int Level { get; }
        public long Experience { get; }
        public long ExperienceForNext { get; }

        public double Progress => Math.Clamp(Experience / (double)ExperienceForNext, 0.0, 1.0);
        public bool IsMaxed => Level >= PlayerSkillLimits.MaximumLevel;

        public override string ToString() => $"{Skill} {Level}/{PlayerSkillLimits.MaximumLevel}";
    }

    /// <summary>
    /// The founder's own skills, and the only thing in the game that grows from playing rather than
    /// from spending.
    ///
    /// Everything else the player improves is bought: hardware, research, staff, data. Skills are
    /// earned by doing the thing, which is what makes a second campaign feel different from the
    /// first even when the same decisions are made. They are also the one progression track that
    /// cannot be accelerated with money, which keeps them from collapsing into another purchase.
    ///
    /// Levels start at 20 and every effect is measured from there, so the 200 points spent at
    /// creation are meaningful immediately and a dumped skill is a real handicap rather than a
    /// missed bonus.
    /// </summary>
    public sealed class SkillSet
    {
        private readonly Dictionary<PlayerSkill, int> levels = new();
        private readonly Dictionary<PlayerSkill, long> experience = new();

        public SkillSet()
        {
            foreach (var definition in PlayerSkillCatalog.All)
            {
                levels[definition.Skill] = PlayerSkillLimits.StartingLevel;
                experience[definition.Skill] = 0L;
            }
        }

        public int Level(PlayerSkill skill) =>
            levels.TryGetValue(skill, out var level) ? level : PlayerSkillLimits.StartingLevel;

        public long Experience(PlayerSkill skill) =>
            experience.TryGetValue(skill, out var value) ? value : 0L;

        /// <summary>Total levels above the starting point. What the 200 creation points bought.</summary>
        public int TotalAllocated
        {
            get
            {
                var total = 0;
                foreach (var definition in PlayerSkillCatalog.All)
                {
                    total += Math.Max(0, Level(definition.Skill) - PlayerSkillLimits.StartingLevel);
                }

                return total;
            }
        }

        public void SetLevel(PlayerSkill skill, int level)
        {
            if (skill == PlayerSkill.None)
            {
                return;
            }

            levels[skill] = Math.Clamp(level, 0, PlayerSkillLimits.MaximumLevel);
        }

        /// <summary>
        /// Adds experience and rolls over as many levels as it covers. Returns the levels gained so
        /// the caller can tell the player, because a skill that levels silently may as well not.
        /// </summary>
        public int AddExperience(PlayerSkill skill, long amount)
        {
            if (skill == PlayerSkill.None || amount <= 0)
            {
                return 0;
            }

            var level = Level(skill);
            if (level >= PlayerSkillLimits.MaximumLevel)
            {
                return 0;
            }

            var pool = Experience(skill) + amount;
            var gained = 0;

            while (level < PlayerSkillLimits.MaximumLevel)
            {
                var needed = PlayerSkillLimits.ExperienceForNextLevel(level);
                if (pool < needed)
                {
                    break;
                }

                pool -= needed;
                level++;
                gained++;
            }

            levels[skill] = level;
            experience[skill] = level >= PlayerSkillLimits.MaximumLevel ? 0L : pool;
            return gained;
        }

        /// <summary>Effect of a skill right now, 0 at the starting level and 1 at 100.</summary>
        public double Effect(PlayerSkill skill) =>
            PlayerSkillCatalog.TryGet(skill, out var definition)
                ? definition.EffectAt(Level(skill))
                : 0.0;

        // ---- what each skill actually does, named so the call sites read plainly ----

        /// <summary>Multiplier on the spread of a training run. Below one is more predictable.</summary>
        public double TrainingSpreadMultiplier() =>
            Math.Clamp(1.0 - Effect(PlayerSkill.Development), 0.2, 1.6);

        /// <summary>Multiplier on everything the company spends running itself.</summary>
        public double OperatingCostMultiplier() =>
            Math.Clamp(1.0 - Effect(PlayerSkill.Management), 0.6, 1.4);

        /// <summary>How much further each discipline improves before it saturates.</summary>
        public double TeamSaturationMultiplier() =>
            Math.Clamp(1.0 + Effect(PlayerSkill.Teamwork), 0.5, 2.0);

        /// <summary>Multiplier on in-house research depth.</summary>
        public double ResearchDepthMultiplier() =>
            Math.Clamp(1.0 + Effect(PlayerSkill.Concept), 0.6, 1.6);

        /// <summary>Multiplier on what a served token costs to produce.</summary>
        public double ServingCostMultiplier() =>
            Math.Clamp(1.0 - Effect(PlayerSkill.Software), 0.6, 1.4);

        /// <summary>Multiplier on the quality of every token trained on.</summary>
        public double DataQualityMultiplier() =>
            Math.Clamp(1.0 + Effect(PlayerSkill.DataEngineering), 0.7, 1.4);

        /// <summary>Multiplier on the daily chance of a public safety failure.</summary>
        public double IncidentRiskMultiplier() =>
            Math.Clamp(1.0 - Effect(PlayerSkill.Safety), 0.1, 1.6);

        public List<SkillStanding> Standings()
        {
            var standings = new List<SkillStanding>(PlayerSkillCatalog.All.Count);
            foreach (var definition in PlayerSkillCatalog.All)
            {
                var level = Level(definition.Skill);
                standings.Add(new SkillStanding(
                    definition.Skill,
                    level,
                    Experience(definition.Skill),
                    PlayerSkillLimits.ExperienceForNextLevel(level)));
            }

            return standings;
        }

        public void Restore(IReadOnlyList<int> restoredLevels, IReadOnlyList<long> restoredExperience)
        {
            var all = PlayerSkillCatalog.All;
            for (var index = 0; index < all.Count; index++)
            {
                var skill = all[index].Skill;

                levels[skill] = restoredLevels != null && index < restoredLevels.Count
                    ? Math.Clamp(restoredLevels[index], 0, PlayerSkillLimits.MaximumLevel)
                    : PlayerSkillLimits.StartingLevel;

                experience[skill] = restoredExperience != null && index < restoredExperience.Count
                    ? Math.Max(0L, restoredExperience[index])
                    : 0L;
            }
        }

        public int[] LevelsToArray()
        {
            var all = PlayerSkillCatalog.All;
            var result = new int[all.Count];
            for (var index = 0; index < all.Count; index++)
            {
                result[index] = Level(all[index].Skill);
            }

            return result;
        }

        public long[] ExperienceToArray()
        {
            var all = PlayerSkillCatalog.All;
            var result = new long[all.Count];
            for (var index = 0; index < all.Count; index++)
            {
                result[index] = Experience(all[index].Skill);
            }

            return result;
        }

        public override string ToString() => $"{TotalAllocated} levels above baseline";
    }
}
