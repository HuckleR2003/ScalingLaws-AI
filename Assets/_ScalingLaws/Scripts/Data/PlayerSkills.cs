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
        public PlayerSkillDefinition(PlayerSkill skill, double fullEffect)
        {
            Skill = skill;
            FullEffect = Math.Clamp(SimUnits.Finite(fullEffect), 0.0, 1.0);
        }

        public PlayerSkill Skill { get; }

        /// <summary>
        /// The stem every word about this skill hangs off.
        ///
        /// Written out rather than assembled from the enum name, because a key built by
        /// concatenation is invisible to `LocalisationTests.EveryKeyTheInterfaceAsksForExists`,
        /// which can only read literals. That has already cost this project one shipped screen of
        /// raw keys.
        /// </summary>
        private static string KeyFor(PlayerSkill skill) => skill switch
        {
            PlayerSkill.Development => "skill.development",
            PlayerSkill.Management => "skill.management",
            PlayerSkill.Teamwork => "skill.teamwork",
            PlayerSkill.Concept => "skill.concept",
            PlayerSkill.Software => "skill.software",
            PlayerSkill.DataEngineering => "skill.data",
            _ => "skill.safety"
        };

        /// <summary>
        /// Read from the book at access time, never stored.
        ///
        /// A catalog is built once at type load, so a name captured there keeps whatever language
        /// the game happened to start in and the creator draws seven English rows on a Polish page
        /// forever. Thirteenth catalog to be moved for that reason.
        ///
        /// **`.title`, which `SkillNotes` has been reading since August.** The first pass of this
        /// gave the catalog its own set of names beside those, so the "(i)" card said PROGRAMOWANIE
        /// and the row beside it said ROZWÓJ: one fact at two addresses, which is the opposite of
        /// `feedback.body` and worth telling apart. Both call sites uppercase, so collapsing onto
        /// the older key moves nothing in English and fixes the Polish.
        /// </summary>
        public string DisplayName => Loc.T(KeyFor(Skill) + ".title");

        /// <summary>A sentence on what this is. Long enough to need a tooltip rather than a row.</summary>
        public string Description => Loc.T(KeyFor(Skill) + ".about");

        /// <summary>
        /// Two or three words naming the number this moves. The creator screen shows seven of these
        /// side by side, and a full sentence in that space wraps to two lines and doubles the height
        /// of the panel, which is the difference between the page fitting and the page scrolling.
        /// </summary>
        public string ShortEffect => Loc.T(KeyFor(Skill) + ".short");

        /// <summary>Plain statement of the effect at level 100, for the card.</summary>
        public string EffectAtFull => Loc.T(KeyFor(Skill) + ".full");

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

        // **The numbers, and nothing a player reads.** Every word about a skill is in the phrase
        // book now, keyed off `KeyFor`, because a string stored here is a string in whichever
        // language the game happened to start in.
        private static readonly PlayerSkillDefinition[] Entries =
        {
            new(PlayerSkill.Development, fullEffect: 0.65),
            new(PlayerSkill.Management, fullEffect: 0.18),
            new(PlayerSkill.Teamwork, fullEffect: 0.60),
            new(PlayerSkill.Concept, fullEffect: 0.35),
            new(PlayerSkill.Software, fullEffect: 0.22),
            new(PlayerSkill.DataEngineering, fullEffect: 0.20),
            new(PlayerSkill.Safety, fullEffect: 0.70)
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
