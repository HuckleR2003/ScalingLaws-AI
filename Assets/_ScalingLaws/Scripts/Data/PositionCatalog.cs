using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>
    /// One job the company can hire into, one per founder skill.
    ///
    /// **The positions are the skills, deliberately.** The player spent two hundred points at
    /// character creation deciding what they are good at; a hiring screen with a different set of
    /// nouns on it would make those points a separate game. Wanting a safety engineer and wanting
    /// Safety are the same want, and now they are the same word.
    ///
    /// Each position still carries the legacy <see cref="StaffRole"/> it feeds, because every
    /// existing effect in the simulation — utilization, data quality, incident risk, research
    /// pace — is written against roles. Two positions can share a role: what the player picks is
    /// the discipline, what the simulation reads is the department it lands in.
    /// </summary>
    public sealed class PositionDefinition
    {
        public PositionDefinition(PlayerSkill skill, StaffRole role, string title, string blurb,
            double baseHourlyWageUsd, string accentHex)
        {
            Skill = skill;
            Role = role;
            Title = title;
            Blurb = blurb;
            BaseHourlyWageUsd = baseHourlyWageUsd;
            AccentHex = accentHex;
        }

        /// <summary>The founder skill this job is the hired version of.</summary>
        public PlayerSkill Skill { get; }

        /// <summary>The department it counts towards. Not shown; the simulation reads it.</summary>
        public StaffRole Role { get; }

        /// <summary>What the job is called on the tile and in the letter.</summary>
        public string Title { get; }

        /// <summary>What this person does, in the player's terms.</summary>
        public string Blurb { get; }

        /// <summary>
        /// What a wholly average person in this job asks an hour.
        ///
        /// Hourly rather than annual because that is the number being negotiated, and a candidate
        /// who says "four hundred and thirty nine dollars an hour" is a person with a position
        /// rather than a row in a salary table.
        /// </summary>
        public double BaseHourlyWageUsd { get; }

        /// <summary>Colour of the count ring on the tile. One per position, so the grid reads fast.</summary>
        public string AccentHex { get; }
    }

    public static class PositionCatalog
    {
        /// <summary>Hours a full-time year is billed at. Turns an hourly rate into a salary.</summary>
        public const double PaidHoursPerYear = 2080.0;

        private static readonly List<PositionDefinition> Entries = new()
        {
            new PositionDefinition(PlayerSkill.Development, StaffRole.ResearchScientist,
                "ML Engineer",
                "Writes the training code and the evaluation harness. The people who make the plan "
                + "actually run.",
                168.0, "#5B8DEF"),

            new PositionDefinition(PlayerSkill.Concept, StaffRole.ResearchScientist,
                "Research Scientist",
                "Decides what is worth training before anybody trains it. Expensive, and the reason "
                + "a lab is a lab.",
                242.0, "#A66BE0"),

            new PositionDefinition(PlayerSkill.Software, StaffRole.InfrastructureEngineer,
                "Software Engineer",
                "The product around the model: the API, the app, the thing customers touch.",
                146.0, "#3FB6A8"),

            new PositionDefinition(PlayerSkill.DataEngineering, StaffRole.DataEngineer,
                "Data Engineer",
                "Corpora, cleaning, deduplication. Quiet work that decides what the model is made of.",
                134.0, "#D6A03C"),

            new PositionDefinition(PlayerSkill.Safety, StaffRole.SafetyEngineer,
                "Safety Engineer",
                "Red teams the model before a regulator does. Cheap next to one inspection.",
                158.0, "#E06B6B"),

            new PositionDefinition(PlayerSkill.Management, StaffRole.GoToMarket,
                "Operations Lead",
                "Runs the floor, the spend and the launches. Nobody notices them until they leave.",
                152.0, "#E0883C"),

            new PositionDefinition(PlayerSkill.Teamwork, StaffRole.GoToMarket,
                "Team Coordinator",
                "Keeps seven disciplines pointed the same way. Raises the ceiling on everybody else.",
                118.0, "#7FBF5F")
        };

        public static IReadOnlyList<PositionDefinition> All => Entries;

        public static PositionDefinition Get(PlayerSkill skill) =>
            Entries.FirstOrDefault(entry => entry.Skill == skill)
            ?? throw new ArgumentOutOfRangeException(nameof(skill), skill, "No such position.");

        public static bool TryGet(PlayerSkill skill, out PositionDefinition definition)
        {
            definition = Entries.FirstOrDefault(entry => entry.Skill == skill);
            return definition != null;
        }

        /// <summary>
        /// The five-point role skill a hundred-point position level lands on.
        ///
        /// The roster's whole effect model is written against one to five, and rewriting it to take
        /// a hundred-point scale would retune every balance number in the game. So the position
        /// level is what the player sees and negotiates over, and this is the band it falls into.
        /// Level 20, the founder's own starting level, lands on 2: an ordinary person.
        /// </summary>
        public static int RoleSkillFor(int positionLevel) => Math.Clamp(positionLevel switch
        {
            < 12 => 1,
            < 32 => 2,
            < 55 => 3,
            < 78 => 4,
            _ => 5
        }, 1, StaffLimits.MaximumSkill);
    }
}
