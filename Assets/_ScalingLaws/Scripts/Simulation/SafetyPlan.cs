using System;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the SAFETY stage of a run costs, and what it is worth afterwards.
    ///
    /// **One calculator, read by three places that must never disagree**: the creator quoting the
    /// bill, the planner adding the days, and the incident roll deciding whether the company gets
    /// away with it. The stage is a promise made at build time and cashed years later, and a promise
    /// computed twice is a promise the player cannot trust.
    ///
    /// **Effort multiplies the safety days and nothing else.** That is the whole design of it: a run
    /// at x4 is not a better run, it is the same run with a season nailed onto the front of it for a
    /// few tenths of a percent. If it ever looks worth taking every time, the numbers are wrong.
    /// </summary>
    public readonly struct SafetyPlan
    {
        public SafetyPlan(int assaTier, int redTeamTier, int dataProtectionTier, int effort,
            int liveModels)
        {
            AssaTier = Math.Clamp(assaTier, 0, SafetyModuleCatalog.TierCount - 1);
            RedTeamTier = Math.Clamp(redTeamTier, 0, SafetyModuleCatalog.TierCount - 1);
            DataProtectionTier = Math.Clamp(dataProtectionTier, -1, SafetyModuleCatalog.TierCount - 1);
            Effort = SafetyModuleCatalog.EffortOf(effort);
            LiveModels = Math.Max(0, liveModels);
        }

        public int AssaTier { get; }
        public int RedTeamTier { get; }

        /// <summary>Minus one when the company never bought the first rung of it.</summary>
        public int DataProtectionTier { get; }

        public SafetyEffort Effort { get; }

        /// <summary>Models on sale. The fleet bonus reads this and every module caps it.</summary>
        public int LiveModels { get; }

        /// <summary>Reads a run's own choices. The one place a blueprint becomes a plan.</summary>
        public static SafetyPlan For(in ModelBlueprint blueprint, int liveModels) =>
            new(blueprint.AssaTier, blueprint.RedTeamTier, blueprint.DataProtectionTier,
                blueprint.SafetyEffort, liveModels);

        private SafetyTier Assa => SafetyModuleCatalog.Get(SafetyModule.Assa, AssaTier);
        private SafetyTier RedTeam => SafetyModuleCatalog.Get(SafetyModule.RedTeam, RedTeamTier);

        /// <summary>
        /// Days the safety stage adds to the run.
        ///
        /// Effort applies here and only here. A run's training time is set by physics and a budget;
        /// this is the part somebody chose to spend.
        /// </summary>
        public int ExtraDays
        {
            get
            {
                var days = Assa.ExtraDays + RedTeam.ExtraDays;
                if (DataProtectionTier >= 0)
                {
                    days += SafetyModuleCatalog.Get(SafetyModule.DataProtection, DataProtectionTier).ExtraDays;
                }

                return (int)Math.Round(days * Effort.TimeMultiplier);
            }
        }

        /// <summary>Cash the stage adds. Effort does not touch it: it buys time, not equipment.</summary>
        public long ExtraCostUsd
        {
            get
            {
                var cost = Assa.ExtraCostUsd + RedTeam.ExtraCostUsd;
                if (DataProtectionTier >= 0)
                {
                    cost += SafetyModuleCatalog.Get(SafetyModule.DataProtection, DataProtectionTier).ExtraCostUsd;
                }

                return cost;
            }
        }

        /// <summary>
        /// How much less likely an incident is to happen at all, as a fraction removed.
        ///
        /// **Combined by what is left rather than by adding.** Two modules that each remove a third
        /// of the risk remove five ninths of it together, not two thirds, and can never remove all
        /// of it. Adding percentages is how a safety system reaches 100% and the mechanic stops
        /// existing.
        /// </summary>
        public double RiskReduction
        {
            get
            {
                var left = 1.0 - Assa.RiskReductionWith(LiveModels);

                if (DataProtectionTier >= 0)
                {
                    left *= 1.0 - SafetyModuleCatalog
                        .Get(SafetyModule.DataProtection, DataProtectionTier)
                        .RiskReductionWith(LiveModels);
                }

                left *= 1.0 - Effort.StatBonus;

                return Math.Clamp(1.0 - left, 0.0, 0.92);
            }
        }

        /// <summary>
        /// The chance of walking away from a penalty that has already been decided.
        ///
        /// Combined the same way, for the same reason. A company with everything at the top is very
        /// hard to fine and is never impossible to fine.
        /// </summary>
        public double SaveChance
        {
            get
            {
                var failed = 1.0 - RedTeam.SaveChanceWith(LiveModels);

                if (DataProtectionTier >= 0)
                {
                    failed *= 1.0 - SafetyModuleCatalog
                        .Get(SafetyModule.DataProtection, DataProtectionTier)
                        .SaveChanceWith(LiveModels);
                }

                failed *= 1.0 - Effort.StatBonus;

                return Math.Clamp(1.0 - failed, 0.0, 0.90);
            }
        }

        /// <summary>
        /// Which module talked the regulator down, given a roll.
        ///
        /// Named rather than derived, because the letter that arrives has to say what saved the
        /// company. "Something worked" is not a lesson; "the red team had already found it" is.
        /// </summary>
        public SafetyModule? Saviour(double roll)
        {
            var redTeam = RedTeam.SaveChanceWith(LiveModels);
            if (roll < redTeam)
            {
                return SafetyModule.RedTeam;
            }

            if (DataProtectionTier < 0)
            {
                return roll < SaveChance ? SafetyModule.RedTeam : null;
            }

            return roll < SaveChance ? SafetyModule.DataProtection : null;
        }

        public override string ToString() =>
            $"ASSA {AssaTier}, red {RedTeamTier}, data {DataProtectionTier}, x{Effort.Multiplier}: "
            + $"-{RiskReduction:P1} risk, {SaveChance:P1} save, +{ExtraDays}d";
    }
}
