using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One person on the payroll.</summary>
    public readonly struct Hire
    {
        public Hire(StaffRole role, int skill, GameDate startedOn)
        {
            Role = role;
            Skill = Math.Clamp(skill, 1, StaffLimits.MaximumSkill);
            StartedOn = startedOn;
        }

        public StaffRole Role { get; }

        /// <summary>One to five. Salary rises faster than skill does, which is the whole decision.</summary>
        public int Skill { get; }

        public GameDate StartedOn { get; }

        public long SalaryPerYearUsd => StaffCatalog.Get(Role).SalaryPerYearUsd(Skill);

        public override string ToString() => $"{Role} skill {Skill}";
    }

    /// <summary>
    /// Everyone the company employs and the room they work in.
    ///
    /// Two rules make this a decision rather than a slider. Desks are a hard cap, so headcount is
    /// gated on a lease signed months earlier. And every role saturates: the seventh research
    /// scientist adds a fraction of what the second one did, so a company cannot solve a problem by
    /// pointing more salary at it.
    ///
    /// The team does not raise the ceiling on what a model can be. It decides how reliably the
    /// company reaches its own plan, which is the quiet difference between two labs with identical
    /// blueprints.
    /// </summary>
    public sealed class StaffRoster
    {
        /// <summary>Skill points per role past which extra people stop mattering much.</summary>
        public const double SaturationPoint = 14.0;

        private const double DaysPerYear = 365.2425;

        private readonly List<Hire> hires = new();

        public IReadOnlyList<Hire> Hires => hires;

        public OfficeTier Office { get; private set; } = OfficeTier.Garage;

        /// <summary>
        /// How much further each discipline improves before it flattens. Driven by the founder's
        /// Teamwork skill and set once per tick, so the roster never has to know a skill exists.
        /// </summary>
        public double SaturationMultiplier { get; set; } = 1.0;

        public int Headcount => hires.Count;

        /// <summary>
        /// Seats bought from the furniture shop, on top of the ones the lease came with.
        ///
        /// A property the decorator writes rather than a reference to it, for the same reason
        /// SaturationMultiplier is: the roster must not have to know that a furniture shop exists in
        /// order to count its own chairs.
        /// </summary>
        public int ExtraDesks { get; set; }

        /// <summary>
        /// What the furniture on the floor adds to how well people work here.
        ///
        /// **This is where the shop's morale numbers actually land.** The game has no separate
        /// morale stat, and a bonus with nowhere to go would be a mechanic that reads well in the
        /// shop and does nothing in the campaign. Office effectiveness is the existing channel for
        /// "this is a better place to work", so a sofa moves the same number a bigger lease does.
        /// </summary>
        public double ComfortBonus { get; set; }

        public int Desks => OfficeCatalog.Get(Office).Desks + ExtraDesks;

        public bool HasFreeDesk => Headcount < Desks;

        public OfficeDefinition OfficeDefinition => OfficeCatalog.Get(Office);

        public void SetOffice(OfficeTier tier) => Office = tier;

        /// <summary>
        /// Places the company owns outright. Owning one means never paying rent on it again.
        ///
        /// A set rather than a flag on the current office, because a company that buys the small hub
        /// and later moves up still owns the small hub, and moving back has to be free.
        /// </summary>
        public HashSet<OfficeTier> Owned { get; } = new();

        public bool Owns(OfficeTier tier) => Owned.Contains(tier);

        public bool Add(Hire hire)
        {
            if (!HasFreeDesk || hire.Role == StaffRole.None)
            {
                return false;
            }

            hires.Add(hire);
            return true;
        }

        /// <summary>Lets one person go. Returns false when the index does not exist.</summary>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= hires.Count)
            {
                return false;
            }

            hires.RemoveAt(index);
            return true;
        }

        public void Clear() => hires.Clear();

        public int CountOf(StaffRole role)
        {
            var count = 0;
            foreach (var hire in hires)
            {
                if (hire.Role == role)
                {
                    count++;
                }
            }

            return count;
        }

        public long DailyPayrollUsd
        {
            get
            {
                var yearly = 0L;
                foreach (var hire in hires)
                {
                    yearly += hire.SalaryPerYearUsd;
                }

                return SimUnits.ToDollars(yearly / DaysPerYear);
            }
        }

        /// <summary>
        /// Rent on where the company is sitting, or nothing when it owns the place.
        ///
        /// This is the whole return on a purchase and it is why the price is ten years of it.
        /// </summary>
        public long DailyRentUsd => Owns(Office) ? 0L : OfficeCatalog.Get(Office).DailyRentUsd;

        public long DailyCostUsd => DailyPayrollUsd + DailyRentUsd;

        /// <summary>
        /// Saturating strength of one role: skill points, scaled by how well the office works, then
        /// bent so the curve flattens. Doubling a team never doubles its output.
        /// </summary>
        public double Strength(StaffRole role)
        {
            var raw = 0.0;
            foreach (var hire in hires)
            {
                if (hire.Role == role)
                {
                    raw += hire.Skill;
                }
            }

            if (raw <= 0.0)
            {
                return 0.0;
            }

            raw *= OfficeCatalog.Get(Office).EffectivenessMultiplier
                + Math.Clamp(ComfortBonus, 0.0, 0.25);
            return raw / (1.0 + raw / (SaturationPoint * Math.Clamp(SaturationMultiplier, 0.5, 2.0)));
        }

        /// <summary>
        /// Multiplier on the spread of a training run. This is the Devices Tycoon idea applied where
        /// it belongs: a good team does not make a better plan possible, it makes the plan land.
        /// </summary>
        public double OutcomeVarianceMultiplier()
        {
            var reduction =
                StaffCatalog.Get(StaffRole.ResearchScientist).OutcomeVarianceReductionPerHead
                * Strength(StaffRole.ResearchScientist)
                + StaffCatalog.Get(StaffRole.SafetyEngineer).OutcomeVarianceReductionPerHead
                * Strength(StaffRole.SafetyEngineer);

            return Math.Clamp(1.0 - reduction, 0.25, 1.0);
        }

        /// <summary>Added to the fleet's realised utilization. Infrastructure people earn this.</summary>
        public double UtilizationBonus() =>
            StaffCatalog.Get(StaffRole.InfrastructureEngineer).UtilizationBonusPerHead
            * Strength(StaffRole.InfrastructureEngineer);

        /// <summary>Multiplier on the quality of every token the company trains on.</summary>
        public double DataQualityMultiplier() =>
            1.0 + StaffCatalog.Get(StaffRole.DataEngineer).DataQualityBonusPerHead
            * Strength(StaffRole.DataEngineer);

        /// <summary>Multiplier on daily incident risk. Safety engineers are the only thing that moves it.</summary>
        public double IncidentRiskMultiplier() => Math.Clamp(
            1.0 - StaffCatalog.Get(StaffRole.SafetyEngineer).IncidentRiskReductionPerHead
            * Strength(StaffRole.SafetyEngineer),
            0.15,
            1.0);

        public double BrandBonus() =>
            StaffCatalog.Get(StaffRole.GoToMarket).BrandBonusPerHead * Strength(StaffRole.GoToMarket);

        /// <summary>Multiplier on research and upgrade durations. Below one is faster.</summary>
        public double ResearchSpeedMultiplier() => Math.Clamp(
            1.0 - StaffCatalog.Get(StaffRole.ResearchScientist).ResearchSpeedBonusPerHead
            * Strength(StaffRole.ResearchScientist),
            0.6,
            1.0);

        public void Restore(OfficeTier office, IEnumerable<Hire> restored)
        {
            hires.Clear();
            Office = office;
            if (restored == null)
            {
                return;
            }

            foreach (var hire in restored)
            {
                hires.Add(hire);
            }
        }

        public override string ToString() =>
            $"{Headcount}/{Desks} in {Office}, ${DailyCostUsd:N0}/day";
    }
}
