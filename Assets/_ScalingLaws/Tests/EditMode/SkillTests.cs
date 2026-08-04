using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests
{
    /// <summary>
    /// The skill set is the one number in the game that money cannot move, so the tests care most
    /// about two things: that the starting level is genuinely neutral, and that a skill left alone
    /// is a real handicap rather than a missing bonus.
    /// </summary>
    public sealed class SkillTests
    {
        [Test]
        public void EverySkillStartsAtTheBaseline()
        {
            var skills = new SkillSet();

            foreach (var definition in PlayerSkillCatalog.All)
            {
                Assert.AreEqual(PlayerSkillLimits.StartingLevel, skills.Level(definition.Skill),
                    $"{definition.DisplayName} did not start at the baseline.");
            }

            Assert.AreEqual(0, skills.TotalAllocated);
        }

        [Test]
        public void TheBaselineHasNoEffectInEitherDirection()
        {
            var skills = new SkillSet();

            Assert.AreEqual(1.0, skills.TrainingSpreadMultiplier(), 1e-9);
            Assert.AreEqual(1.0, skills.OperatingCostMultiplier(), 1e-9);
            Assert.AreEqual(1.0, skills.TeamSaturationMultiplier(), 1e-9);
            Assert.AreEqual(1.0, skills.ResearchDepthMultiplier(), 1e-9);
            Assert.AreEqual(1.0, skills.ServingCostMultiplier(), 1e-9);
            Assert.AreEqual(1.0, skills.DataQualityMultiplier(), 1e-9);
            Assert.AreEqual(1.0, skills.IncidentRiskMultiplier(), 1e-9);
        }

        [Test]
        public void DumpingASkillBelowTheBaselineHurts()
        {
            var skills = new SkillSet();
            skills.SetLevel(PlayerSkill.Safety, 0);

            Assert.Greater(skills.IncidentRiskMultiplier(), 1.0,
                "A founder who knows nothing about safety should have more incidents, not fewer.");
        }

        [Test]
        public void AMaxedSkillMovesItsNumberTheHelpfulWay()
        {
            var skills = new SkillSet();
            skills.SetLevel(PlayerSkill.Management, PlayerSkillLimits.MaximumLevel);
            skills.SetLevel(PlayerSkill.Safety, PlayerSkillLimits.MaximumLevel);

            Assert.Less(skills.OperatingCostMultiplier(), 1.0);
            Assert.Less(skills.IncidentRiskMultiplier(), 1.0);
        }

        [Test]
        public void TheStartingBudgetCannotMaxEverything()
        {
            var perSkill = PlayerSkillLimits.MaximumLevel - PlayerSkillLimits.StartingLevel;
            var toMaxThemAll = perSkill * PlayerSkillCatalog.All.Count;

            Assert.Less(PlayerSkillLimits.StartingPoints, toMaxThemAll,
                "Creation has to be a choice. If the budget covers everything it is not one.");
        }

        [Test]
        public void ExperienceRollsOverIntoLevelsAndKeepsTheRemainder()
        {
            var skills = new SkillSet();
            var start = skills.Level(PlayerSkill.Development);

            var gained = skills.AddExperience(PlayerSkill.Development, 100_000L);

            Assert.Greater(gained, 0);
            Assert.AreEqual(start + gained, skills.Level(PlayerSkill.Development));
        }

        [Test]
        public void ExperienceStopsMatteringAtTheCeiling()
        {
            var skills = new SkillSet();
            skills.SetLevel(PlayerSkill.Concept, PlayerSkillLimits.MaximumLevel);

            Assert.AreEqual(0, skills.AddExperience(PlayerSkill.Concept, 500_000L));
            Assert.AreEqual(PlayerSkillLimits.MaximumLevel, skills.Level(PlayerSkill.Concept));
        }

        [Test]
        public void LevelsSurviveASaveRoundTrip()
        {
            var skills = new SkillSet();
            skills.SetLevel(PlayerSkill.Teamwork, 70);
            skills.AddExperience(PlayerSkill.Software, 40L);

            var restored = new SkillSet();
            restored.Restore(skills.LevelsToArray(), skills.ExperienceToArray());

            foreach (var definition in PlayerSkillCatalog.All)
            {
                Assert.AreEqual(skills.Level(definition.Skill), restored.Level(definition.Skill));
            }
        }

        [Test]
        public void OutOfRangeLevelsAreClampedRatherThanTrusted()
        {
            var skills = new SkillSet();
            skills.Restore(new[] { -50, 4000, 0, 0, 0, 0, 0 }, new long[0]);

            foreach (var definition in PlayerSkillCatalog.All)
            {
                var level = skills.Level(definition.Skill);
                Assert.GreaterOrEqual(level, 0);
                Assert.LessOrEqual(level, PlayerSkillLimits.MaximumLevel);
            }
        }
    }
}
