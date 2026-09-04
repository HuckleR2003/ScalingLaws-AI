using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>Who the company hires. Explicit values, saved, never renumbered.</summary>
    public enum StaffRole
    {
        None = 0,
        ResearchScientist = 1,
        InfrastructureEngineer = 2,
        DataEngineer = 3,
        SafetyEngineer = 4,
        GoToMarket = 5
    }

    /// <summary>
    /// What one role does per head at skill 1, and what it costs.
    ///
    /// The gem borrowed from Devices Tycoon is that employee quality is a hidden driver of the
    /// result: two identical devices built by different teams score differently, because the game
    /// applies a quality modifier from the team's aggregate skill. Here that lands on the one place
    /// it belongs, the spread of a training run. A good research team does not raise the ceiling on
    /// what a blueprint can produce. It stops the run landing three points under its projection.
    /// </summary>
    public readonly struct StaffRoleDefinition
    {
        public StaffRoleDefinition(
            StaffRole role,
            string displayName,
            string description,
            long baseSalaryPerYearUsd,
            long hiringCostUsd,
            double outcomeVarianceReductionPerHead = 0.0,
            double utilizationBonusPerHead = 0.0,
            double dataQualityBonusPerHead = 0.0,
            double incidentRiskReductionPerHead = 0.0,
            double brandBonusPerHead = 0.0,
            double researchSpeedBonusPerHead = 0.0,
            double researchPointShare = 0.0)
        {
            Role = role;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? role.ToString() : displayName;
            Description = description ?? string.Empty;
            BaseSalaryPerYearUsd = Math.Clamp(baseSalaryPerYearUsd, 10_000L, 10_000_000L);
            HiringCostUsd = Math.Clamp(hiringCostUsd, 0L, 10_000_000L);
            OutcomeVarianceReductionPerHead = Math.Clamp(SimUnits.Finite(outcomeVarianceReductionPerHead), 0.0, 0.2);
            UtilizationBonusPerHead = Math.Clamp(SimUnits.Finite(utilizationBonusPerHead), 0.0, 0.2);
            DataQualityBonusPerHead = Math.Clamp(SimUnits.Finite(dataQualityBonusPerHead), 0.0, 0.2);
            IncidentRiskReductionPerHead = Math.Clamp(SimUnits.Finite(incidentRiskReductionPerHead), 0.0, 0.2);
            BrandBonusPerHead = Math.Clamp(SimUnits.Finite(brandBonusPerHead), 0.0, 0.1);
            ResearchSpeedBonusPerHead = Math.Clamp(SimUnits.Finite(researchSpeedBonusPerHead), 0.0, 0.2);
            ResearchPointShare = Math.Clamp(SimUnits.Finite(researchPointShare), 0.0, 1.0);
        }

        public StaffRole Role { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public long BaseSalaryPerYearUsd { get; }

        /// <summary>Recruiting, relocation and a signing bonus. Paid once, on the day they start.</summary>
        public long HiringCostUsd { get; }

        /// <summary>Share of a training run's spread this role removes per head, before skill.</summary>
        public double OutcomeVarianceReductionPerHead { get; }

        public double UtilizationBonusPerHead { get; }
        public double DataQualityBonusPerHead { get; }
        public double IncidentRiskReductionPerHead { get; }
        public double BrandBonusPerHead { get; }
        public double ResearchSpeedBonusPerHead { get; }

        /// <summary>
        /// How much one head of this role adds to the research the company is doing.
        ///
        /// **This used to be flat across every role and it made the job title decorative.** A
        /// go-to-market hire moved research points exactly as much as a research scientist did, and
        /// points are the gate that money cannot open. The role affected the calendar through
        /// <see cref="ResearchSpeedBonusPerHead"/> and never the currency.
        ///
        /// The five average 0.36 against the flat 0.40 they replace, so a balanced team lands about
        /// where it was and a research-heavy one is genuinely better.
        /// </summary>
        public double ResearchPointShare { get; }

        /// <summary>Salary scales steeply with skill. A five is not five times a one, it is worth more.</summary>
        public long SalaryPerYearUsd(int skill) =>
            SimUnits.ToDollars(BaseSalaryPerYearUsd * Math.Pow(1.55, Math.Clamp(skill, 1, StaffLimits.MaximumSkill) - 1));

        public long HiringCostUsd_ForSkill(int skill) =>
            SimUnits.ToDollars(HiringCostUsd * Math.Pow(1.7, Math.Clamp(skill, 1, StaffLimits.MaximumSkill) - 1));

        public override string ToString() => $"{DisplayName} ({BaseSalaryPerYearUsd:N0}/yr at skill 1)";
    }

    /// <summary>Shared limits, kept out of the structs so they can reference them in clamps.</summary>
    public static class StaffLimits
    {
        public const int MaximumSkill = 5;

        /// <summary>Heads past which a role stops adding anything. A tenth researcher is a meeting.</summary>
        public const int DiminishingReturnsAfter = 6;
    }

    /// <summary>The ONE staff library.</summary>
    public static class StaffCatalog
    {
        public const string CatalogVersion = "2026.08.03";

        private static readonly StaffRoleDefinition[] Entries =
        {
            new(StaffRole.ResearchScientist, "Research scientist",
                "Reads the papers, runs the ablations, and is the reason a run lands where the plan said "
                + "it would rather than three points under.",
                baseSalaryPerYearUsd: 320_000,
                hiringCostUsd: 90_000,
                outcomeVarianceReductionPerHead: 0.075,
                researchSpeedBonusPerHead: 0.018,
                researchPointShare: 0.60),

            new(StaffRole.InfrastructureEngineer, "Infrastructure engineer",
                "Keeps the cluster fed. The difference between a fleet running at its rating and one "
                + "running at two thirds of it.",
                baseSalaryPerYearUsd: 280_000,
                hiringCostUsd: 70_000,
                utilizationBonusPerHead: 0.028,
                researchPointShare: 0.30),

            new(StaffRole.DataEngineer, "Data engineer",
                "Deduplication, filtering, licensing paperwork. Unglamorous, and it moves the quality of "
                + "every token the company will ever train on.",
                baseSalaryPerYearUsd: 240_000,
                hiringCostUsd: 55_000,
                dataQualityBonusPerHead: 0.016,
                researchPointShare: 0.40),

            new(StaffRole.SafetyEngineer, "Safety engineer",
                "Red teams the model before somebody else does it in public. Invisible when it works, "
                + "and the only thing standing between a capable model and a very expensive week.",
                baseSalaryPerYearUsd: 300_000,
                hiringCostUsd: 80_000,
                incidentRiskReductionPerHead: 0.085,
                outcomeVarianceReductionPerHead: 0.012,
                researchPointShare: 0.35),

            new(StaffRole.GoToMarket, "Go to market",
                "Developer relations, enterprise sales, the conference circuit. Does nothing for the model "
                + "and a great deal for whether anyone picks it.",
                baseSalaryPerYearUsd: 210_000,
                hiringCostUsd: 45_000,
                brandBonusPerHead: 0.011,
                researchPointShare: 0.15)
        };

        private static readonly Dictionary<StaffRole, StaffRoleDefinition> ByRole = BuildIndex();

        public static IReadOnlyList<StaffRoleDefinition> All => Entries;

        public static StaffRoleDefinition Get(StaffRole role)
        {
            if (!ByRole.TryGetValue(role, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
            }

            return definition;
        }

        public static bool TryGet(StaffRole role, out StaffRoleDefinition definition) =>
            ByRole.TryGetValue(role, out definition);

        private static Dictionary<StaffRole, StaffRoleDefinition> BuildIndex()
        {
            var index = new Dictionary<StaffRole, StaffRoleDefinition>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Role] = entry;
            }

            return index;
        }
    }
}
