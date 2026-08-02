using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    public enum IncidentSeverity
    {
        None = 0,
        Minor = 1,
        Major = 2,
        Severe = 3
    }

    /// <summary>What went wrong, and what it cost.</summary>
    public readonly struct SafetyIncident
    {
        public SafetyIncident(
            IncidentSeverity severity,
            GameDate date,
            string headline,
            double reputationLoss,
            long fineUsd,
            bool forcedWithdrawal)
        {
            Severity = severity;
            Date = date;
            Headline = headline ?? string.Empty;
            ReputationLoss = Math.Clamp(SimUnits.Finite(reputationLoss), 0.0, 1.0);
            FineUsd = Math.Max(0L, fineUsd);
            ForcedWithdrawal = forcedWithdrawal;
        }

        public IncidentSeverity Severity { get; }
        public GameDate Date { get; }
        public string Headline { get; }
        public double ReputationLoss { get; }
        public long FineUsd { get; }

        /// <summary>The model came off the market. Severe incidents only.</summary>
        public bool ForcedWithdrawal { get; }

        public override string ToString() => $"{Severity} on {Date}: {Headline}";
    }

    /// <summary>
    /// The ONE place a model can go publicly wrong.
    ///
    /// Safety used to pay only into brand, which made it the trait a rational player skipped. It now
    /// gates a tail risk, and the risk scales with capability: a weak model that misbehaves is a
    /// support ticket, a frontier model that misbehaves is a regulatory event. That inversion is the
    /// point. The better the company gets, the more the neglected trait costs, so the pressure to
    /// maintain it arrives exactly when the player is most tempted to spend elsewhere.
    ///
    /// Nothing here is a coin flip on a good day. At market par on Safety with a small safety team,
    /// a company sees roughly one minor incident every few years. Two levels behind par at the
    /// frontier, it is a different game.
    /// </summary>
    public static class IncidentModel
    {
        /// <summary>Daily probability for a par-safety model at capability 50 with no safety team.</summary>
        public const double BaseDailyRisk = 0.00035;

        /// <summary>Capability at which the base risk applies. Above it, risk climbs faster than linearly.</summary>
        public const double ReferenceCapability = 50.0;

        public const double CapabilityExponent = 1.6;

        /// <summary>Extra risk multiplier per level of Safety shortfall against market par.</summary>
        public const double ShortfallRiskPerLevel = 0.85;

        /// <summary>Risk cut per level of Safety held above par. Overinvesting is not wasted.</summary>
        public const double SurplusRiskCutPerLevel = 0.22;

        /// <summary>A fine is a share of the annual run rate, so it scales with the company.</summary>
        public const double MajorFineShareOfRunRate = 0.06;

        public const double SevereFineShareOfRunRate = 0.22;

        /// <summary>Floors so a pre-revenue lab still feels a fine.</summary>
        public const long MinimumMajorFineUsd = 2_000_000;

        public const long MinimumSevereFineUsd = 25_000_000;

        /// <summary>
        /// Chance of something going publicly wrong today, given the best live model and the team.
        /// </summary>
        public static double DailyRisk(
            DeployedModel model,
            GameDate date,
            double staffRiskMultiplier)
        {
            if (model == null || !model.IsLiveOn(date))
            {
                return 0.0;
            }

            var capability = model.EffectiveCapability(date);
            if (capability <= 0.0)
            {
                return 0.0;
            }

            var scale = Math.Pow(Math.Max(0.1, capability / ReferenceCapability), CapabilityExponent);

            var definition = ModelTraitCatalog.Get(ModelTrait.Safety);
            var advantage = model.Traits.Advantage(definition, date);

            var exposure = advantage >= 0
                ? Math.Pow(1.0 - SurplusRiskCutPerLevel, advantage)
                : 1.0 + ShortfallRiskPerLevel * -advantage;

            var risk = BaseDailyRisk * scale * exposure
                * Math.Clamp(SimUnits.Finite(staffRiskMultiplier, 1.0), 0.05, 2.0);

            return Math.Clamp(risk, 0.0, 0.05);
        }

        /// <summary>
        /// Rolls the consequence once an incident has fired. Severity leans on how far behind par the
        /// model was: a company that kept up gets a bad week, one that did not gets a bad year.
        /// </summary>
        public static SafetyIncident Resolve(
            DeployedModel model,
            GameDate date,
            long annualRevenueRunRateUsd,
            DeterministicRandom random)
        {
            var definition = ModelTraitCatalog.Get(ModelTrait.Safety);
            var advantage = model.Traits.Advantage(definition, date);
            var shortfall = Math.Max(0, -advantage);

            var roll = random.NextDouble() + shortfall * 0.16;
            var severity = roll switch
            {
                >= 0.93 => IncidentSeverity.Severe,
                >= 0.62 => IncidentSeverity.Major,
                _ => IncidentSeverity.Minor
            };

            return severity switch
            {
                IncidentSeverity.Severe => new SafetyIncident(
                    severity,
                    date,
                    $"{model.Name} has been pulled from sale after a regulator opened a formal investigation.",
                    reputationLoss: 0.22,
                    fineUsd: FineFor(annualRevenueRunRateUsd, SevereFineShareOfRunRate, MinimumSevereFineUsd),
                    forcedWithdrawal: true),

                IncidentSeverity.Major => new SafetyIncident(
                    severity,
                    date,
                    $"{model.Name} produced output that made the evening news. The regulator has issued a penalty.",
                    reputationLoss: 0.09,
                    fineUsd: FineFor(annualRevenueRunRateUsd, MajorFineShareOfRunRate, MinimumMajorFineUsd),
                    forcedWithdrawal: false),

                _ => new SafetyIncident(
                    severity,
                    date,
                    $"{model.Name} embarrassed itself publicly. No regulator involved, and everyone saw it.",
                    reputationLoss: 0.03,
                    fineUsd: 0L,
                    forcedWithdrawal: false)
            };
        }

        private static long FineFor(long annualRevenueRunRateUsd, double share, long floor)
        {
            var scaled = SimUnits.ToDollars(Math.Max(0L, annualRevenueRunRateUsd) * share);
            return Math.Max(floor, scaled);
        }
    }
}
